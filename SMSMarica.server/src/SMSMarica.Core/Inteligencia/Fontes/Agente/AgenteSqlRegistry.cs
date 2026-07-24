using System.Collections.Concurrent;
using System.Text.Json;
using SMSMarica.Core.Inteligencia.Fontes;

namespace SMSMarica.Core.Inteligencia.Fontes.Agente;

/// <summary>Implementação em memória de <see cref="IAgenteSqlRegistry"/>. Ver ADR-0023.</summary>
public sealed class AgenteSqlRegistry : IAgenteSqlRegistry
{
    private readonly ConcurrentDictionary<string, Conexao> _conexoes = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable Registrar(string agenteId, Func<string, CancellationToken, Task> enviar)
    {
        var conexao = new Conexao(enviar);

        // Última conexão vence: se havia uma anterior, é descartada (falha os pendentes dela).
        _conexoes.AddOrUpdate(agenteId, conexao, (_, antiga) =>
        {
            antiga.Encerrar("Conexão substituída por uma nova sessão do agente.");
            return conexao;
        });

        return new Registro(() =>
        {
            if (_conexoes.TryGetValue(agenteId, out var atual) && ReferenceEquals(atual, conexao))
            {
                _conexoes.TryRemove(agenteId, out _);
            }
            conexao.Encerrar("Agente desconectou.");
        });
    }

    public void EntregarResposta(string agenteId, string json)
    {
        if (_conexoes.TryGetValue(agenteId, out var conexao))
        {
            conexao.Resolver(json);
        }
    }

    public bool EstaConectado(string agenteId) => _conexoes.ContainsKey(agenteId);

    public async Task<ResultadoConsulta> ExecutarAsync(
        string agenteId, string sql, int timeoutSegundos, int maxLinhas, CancellationToken ct = default)
    {
        if (!_conexoes.TryGetValue(agenteId, out var conexao))
        {
            return ResultadoConsulta.ComErro(
                $"O agente '{agenteId}' não está conectado. Verifique se o proxy está rodando no servidor de destino.");
        }

        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<RespostaAgente>(TaskCreationOptions.RunContinuationsAsynchronously);
        conexao.Pendentes[id] = tcs;

        try
        {
            var pedido = JsonSerializer.Serialize(new
            {
                tipo = "query",
                id,
                sql,
                maxLinhas,
                timeoutSeg = timeoutSegundos,
            });

            await conexao.Enviar(pedido, ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSegundos + 5)); // folga sobre o timeout do banco

            await using (timeout.Token.Register(() =>
                tcs.TrySetException(new TimeoutException("O agente não respondeu no tempo esperado."))))
            {
                var resposta = await tcs.Task;
                return resposta.Ok
                    ? new ResultadoConsulta(true, resposta.Colunas, resposta.Linhas)
                    : ResultadoConsulta.ComErro(resposta.Erro ?? "Erro não especificado do agente.");
            }
        }
        catch (TimeoutException ex)
        {
            return ResultadoConsulta.ComErro(ex.Message);
        }
        catch (Exception ex)
        {
            return ResultadoConsulta.ComErro($"Falha ao consultar o agente: {ex.Message}");
        }
        finally
        {
            conexao.Pendentes.TryRemove(id, out _);
        }
    }

    private sealed record RespostaAgente(
        bool Ok, IReadOnlyList<string> Colunas, IReadOnlyList<IReadOnlyList<object?>> Linhas, string? Erro);

    private sealed class Conexao(Func<string, CancellationToken, Task> enviar)
    {
        public Func<string, CancellationToken, Task> Enviar { get; } = enviar;

        public ConcurrentDictionary<string, TaskCompletionSource<RespostaAgente>> Pendentes { get; } = new();

        public void Resolver(string json)
        {
            RespostaAgente resposta;
            string? id;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var raiz = doc.RootElement;
                if (raiz.TryGetProperty("tipo", out var tp) && tp.GetString() != "resultado")
                {
                    return; // pings e outros frames de controle são ignorados aqui
                }

                id = raiz.GetProperty("id").GetString();
                var ok = raiz.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
                if (!ok)
                {
                    var erro = raiz.TryGetProperty("erro", out var e) ? e.GetString() : null;
                    resposta = new RespostaAgente(false, [], [], erro);
                }
                else
                {
                    var colunas = raiz.TryGetProperty("colunas", out var cols)
                        ? cols.EnumerateArray().Select(c => c.GetString() ?? "").ToArray()
                        : [];
                    var linhas = new List<IReadOnlyList<object?>>();
                    if (raiz.TryGetProperty("linhas", out var lins))
                    {
                        foreach (var linha in lins.EnumerateArray())
                        {
                            linhas.Add(linha.EnumerateArray().Select(Valor).ToArray());
                        }
                    }
                    resposta = new RespostaAgente(true, colunas, linhas, null);
                }
            }
            catch (Exception ex)
            {
                // Frame malformado: não deixa o pedido pendurar — falha o mais recente sem id.
                id = null;
                resposta = new RespostaAgente(false, [], [], $"Resposta do agente ilegível: {ex.Message}");
            }

            if (id is not null && Pendentes.TryGetValue(id, out var tcs))
            {
                tcs.TrySetResult(resposta);
            }
        }

        public void Encerrar(string motivo)
        {
            foreach (var tcs in Pendentes.Values)
            {
                tcs.TrySetException(new InvalidOperationException(motivo));
            }
            Pendentes.Clear();
        }

        /// <summary>Converte o valor JSON de uma célula para um primitivo CLR simples.</summary>
        private static object? Valor(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => el.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            _ => el.ToString(),
        };
    }

    private sealed class Registro(Action aoDescartar) : IDisposable
    {
        private Action? _aoDescartar = aoDescartar;

        public void Dispose()
        {
            Interlocked.Exchange(ref _aoDescartar, null)?.Invoke();
        }
    }
}
