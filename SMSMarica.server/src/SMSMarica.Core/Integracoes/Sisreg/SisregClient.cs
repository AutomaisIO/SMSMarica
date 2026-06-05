using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Sisreg.Configuracao;
using SMSMarica.Core.Integracoes.Sisreg.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Sisreg;

/// <summary>
/// Cliente HTTP do Elasticsearch SISREG. A base URL e as credenciais vêm do banco
/// (não do registro de DI), então cada chamada monta a URL absoluta e o header de auth
/// a partir do <see cref="SisregContexto"/>. Erros viram exceções tratadas — nunca 500
/// cru — e nada de credencial/dado sensível vai para o log.
/// </summary>
public sealed class SisregClient(
    HttpClient http,
    ISisregConfiguracaoService configuracao,
    ILogger<SisregClient> logger) : ISisregClient
{
    public const string HttpClientName = "Sisreg";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<SisregBuscaResultado<T>> BuscarAsync<T>(
        TipoIndiceSisreg tipo,
        object corpoConsulta,
        CancellationToken cancellationToken = default)
    {
        var contexto = await configuracao.ObterContextoAsync(cancellationToken);

        var indice = MontarNomeIndice(tipo, contexto);
        var url = new Uri(new Uri(contexto.BaseUrl), $"{indice}/_search");

        var json = JsonSerializer.Serialize(corpoConsulta, JsonOpts);

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        AplicarAutenticacao(req, contexto);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Falha de rede ao consultar SISREG (índice {Indice}).", indice);
            throw new ConflitoException("sisreg.indisponivel", "Não foi possível alcançar a API do SISREG.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout ao consultar SISREG (índice {Indice}).", indice);
            throw new ConflitoException("sisreg.timeout", "A consulta ao SISREG excedeu o tempo limite.");
        }

        using (resp)
        {
            var corpo = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("SISREG retornou {Status} no índice {Indice}.", (int)resp.StatusCode, indice);
                var detalhe = resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                    ? "credenciais SISREG inválidas ou sem acesso ao índice."
                    : $"SISREG retornou {(int)resp.StatusCode}.";
                throw new ConflitoException("sisreg.erro", $"Consulta ao SISREG falhou: {detalhe}");
            }

            return Parsear<T>(corpo, indice);
        }
    }

    private static string MontarNomeIndice(TipoIndiceSisreg tipo, SisregContexto ctx)
    {
        var prefixo = tipo.Prefixo();
        if (ctx.Escopo == EscopoSisreg.Nacional)
        {
            return $"{prefixo}-nacional";
        }

        if (string.IsNullOrWhiteSpace(ctx.Uf) || string.IsNullOrWhiteSpace(ctx.Municipio))
        {
            throw new ValidacaoException("sisreg.escopo_municipal",
                "Escopo municipal exige UF e município configurados.");
        }

        return $"{prefixo}-{ctx.Uf.Trim().ToLowerInvariant()}-{ctx.Municipio.Trim()}";
    }

    private static void AplicarAutenticacao(HttpRequestMessage req, SisregContexto ctx)
    {
        switch (ctx.TipoAutenticacao)
        {
            case TipoAutenticacaoSisreg.Basic:
                if (string.IsNullOrWhiteSpace(ctx.Login) || string.IsNullOrWhiteSpace(ctx.Senha))
                {
                    throw new ValidacaoException("sisreg.credencial", "Login e senha do SISREG não configurados.");
                }

                var par = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ctx.Login}:{ctx.Senha}"));
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", par);
                break;

            case TipoAutenticacaoSisreg.Bearer:
                ExigirToken(ctx);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.Token);
                break;

            case TipoAutenticacaoSisreg.ApiKey:
                ExigirToken(ctx);
                req.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", ctx.Token);
                break;

            default:
                throw new ValidacaoException("sisreg.autenticacao", "Tipo de autenticação SISREG inválido.");
        }
    }

    private static void ExigirToken(SisregContexto ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Token))
        {
            throw new ValidacaoException("sisreg.credencial", "Token do SISREG não configurado.");
        }
    }

    private static SisregBuscaResultado<T> Parsear<T>(string corpo, string indice)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            var root = doc.RootElement;

            if (!root.TryGetProperty("hits", out var hits))
            {
                return new SisregBuscaResultado<T>(0, []);
            }

            long total = 0;
            if (hits.TryGetProperty("total", out var totalEl))
            {
                total = totalEl.ValueKind == JsonValueKind.Object && totalEl.TryGetProperty("value", out var v)
                    ? v.GetInt64()
                    : totalEl.ValueKind == JsonValueKind.Number ? totalEl.GetInt64() : 0;
            }

            var itens = new List<T>();
            if (hits.TryGetProperty("hits", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var hit in arr.EnumerateArray())
                {
                    if (!hit.TryGetProperty("_source", out var source))
                    {
                        continue;
                    }

                    var item = source.Deserialize<T>(JsonOpts);
                    if (item is not null)
                    {
                        itens.Add(item);
                    }
                }
            }

            return new SisregBuscaResultado<T>(total, itens);
        }
        catch (JsonException ex)
        {
            throw new ConflitoException("sisreg.resposta",
                $"Não foi possível interpretar a resposta do SISREG (índice {indice}): {ex.Message}");
        }
    }
}
