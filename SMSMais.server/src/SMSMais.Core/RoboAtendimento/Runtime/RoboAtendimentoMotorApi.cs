using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Core.RoboAtendimento.Comandos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>
/// Motor do robô sobre a <b>Messages API</b> da Anthropic (ADR-0050). O loop de tool-use é NOSSO:
/// o modelo pede ferramentas, nós executamos in-process pelo <see cref="IRoboComandoDispatcher"/>
/// e devolvemos o resultado — sem serviço Python no meio e sem guichê de loopback.
///
/// Três garantias que o motor antigo (assinatura) não dava:
/// <list type="bullet">
/// <item>a resposta ao cidadão só sai da ferramenta terminal <c>responder_cidadao</c>; se o modelo
/// terminar sem chamá-la, a prosa dele é <b>descartada</b> (foi o que vazou jargão em produção);</item>
/// <item>custo e tokens vêm em <c>usage</c> a cada chamada — o relatório por assunto passa a ser real;</item>
/// <item>o bloco <c>system</c> vai com <c>cache_control: ephemeral</c>, então persona e treinos não
/// são cobrados inteiros a cada turno.</item>
/// </list>
/// Espelha o padrão já em produção no <c>DistribuidorIa</c> (auth, versão da API, cache_control).
/// </summary>
public sealed class RoboAtendimentoMotorApi(
    HttpClient http,
    SmsMaisDbContext db,
    IProtetorSegredos protetor,
    IRoboComandoDispatcher dispatcher,
    ILogger<RoboAtendimentoMotorApi> logger) : IRoboAtendimentoMotor
{
    public const string HttpClientName = "RoboIaProvedor";

    private const string EndpointMensagens = "v1/messages";
    private const string VersaoApi = "2023-06-01";
    private const int MaxTokens = 1024;

    /// <summary>Teto de idas ao modelo por tarefa (equivale ao <c>ATENDIMENTO_MAX_TURNS</c> antigo).
    /// Protege contra laço de ferramenta e contra custo desgovernado num único atendimento.</summary>
    private const int MaxIteracoes = 6;

    /// <summary>Sobrecarga da API é transitória e o turno é de WhatsApp: vale reesperar segundos
    /// aqui em vez de devolver para o backoff do worker, que é de 4/8/16 MINUTOS.</summary>
    private const int MaxTentativasHttp = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RespostaMotorRobo> ResponderAsync(EntradaMotorRobo entrada, CancellationToken ct)
    {
        var token = await ObterTokenAsync(ct)
            ?? throw new InvalidOperationException(
                "Token da Anthropic não configurado (Configuração da IA) — motor do robô indisponível.");

        var ferramentas = MontarFerramentas(entrada.ComandosHabilitados);
        var mensagens = MontarMensagens(entrada);
        long tokensEntrada = 0, tokensSaida = 0;
        var iteracoes = 0;

        while (iteracoes++ < MaxIteracoes)
        {
            var body = new
            {
                model = entrada.Modelo,
                max_tokens = MaxTokens,
                system = new object[]
                {
                    new
                    {
                        type = "text",
                        text = entrada.InstrucaoSistema.TrimEnd() + "\n" + RoboGuardrail.Texto,
                        // Persona + treinos + guardrail repetem entre turnos: cachear corta a maior
                        // fatia do custo (o motor antigo mandava ~42,8k tokens de entrada por tarefa).
                        cache_control = new { type = "ephemeral" },
                    },
                },
                tools = ferramentas,
                messages = mensagens,
            };

            using var doc = await EnviarAsync(token, body, ct);
            var raiz = doc.RootElement;
            SomarUso(raiz, ref tokensEntrada, ref tokensSaida);

            // A resposta final pode vir junto com chamadas de ferramenta no mesmo turno.
            if (TentarLerRespostaFinal(raiz, out var resposta))
                return resposta with
                {
                    TokensEntrada = tokensEntrada,
                    TokensSaida = tokensSaida,
                    CustoUsd = PrecoModeloIa.Calcular(entrada.Modelo, tokensEntrada, tokensSaida),
                };

            var chamadas = LerChamadasDeFerramenta(raiz);
            if (chamadas.Count == 0) break; // parou sem ferramenta terminal e sem pedir nada: fallback

            // Ecoa o turno do assistente e devolve os resultados como uma mensagem do usuário.
            mensagens.Add(new { role = "assistant", content = raiz.GetProperty("content").Clone() });
            var resultados = new List<object>(chamadas.Count);
            foreach (var (id, nome, argumentos) in chamadas)
                resultados.Add(await ExecutarFerramentaAsync(entrada, id, nome, argumentos, ct));
            mensagens.Add(new { role = "user", content = resultados });
        }

        logger.LogWarning(
            "Robô encerrou sem usar responder_cidadao na conversa {Conversa} ({Iteracoes} iterações) — prosa DESCARTADA.",
            entrada.ConversaId, iteracoes);
        return new RespostaMotorRobo(
            "Só um momento, por favor — já retorno com sua resposta.",
            HandOff: true,
            MotivoHandOff: "sem-uso-da-ferramenta-de-saida",
            Confianca: 0,
            TokensEntrada: tokensEntrada,
            TokensSaida: tokensSaida,
            CustoUsd: PrecoModeloIa.Calcular(entrada.Modelo, tokensEntrada, tokensSaida));
    }

    // ---------- montagem ----------

    private static object[] MontarFerramentas(IReadOnlyList<string> comandosHabilitados)
    {
        var lista = new List<object>
        {
            new
            {
                name = RoboFerramentaCatalogo.ResponderCidadao.Nome,
                description = RoboFerramentaCatalogo.ResponderCidadao.Descricao,
                input_schema = RoboFerramentaCatalogo.ResponderCidadao.InputSchema,
            },
        };

        foreach (var nome in comandosHabilitados)
        {
            if (!Enum.TryParse<ComandoRobo>(nome, ignoreCase: true, out var comando)) continue;
            if (!RoboFerramentaCatalogo.PorComando.TryGetValue(comando, out var f)) continue;
            lista.Add(new { name = f.Nome, description = f.Descricao, input_schema = f.InputSchema });
        }
        return [.. lista];
    }

    /// <summary>O histórico vira turnos <c>user</c>/<c>assistant</c> de verdade — o motor antigo
    /// achatava tudo num texto só, o que enfraquecia o contexto. Mensagens consecutivas do mesmo
    /// papel são fundidas (a API não aceita dois turnos seguidos do mesmo lado).</summary>
    private static List<object> MontarMensagens(EntradaMotorRobo entrada)
    {
        var turnos = new List<(string Papel, string Texto)>();
        foreach (var h in entrada.Historico)
        {
            if (string.IsNullOrWhiteSpace(h.Texto)) continue;
            // Só o cidadão é "user"; robô, atendente e sistema são a voz do serviço.
            var papel = h.Papel == "cidadao" ? "user" : "assistant";
            var texto = h.Papel is "atendente" or "sistema" ? $"[{h.Papel}] {h.Texto}" : h.Texto;
            if (turnos.Count > 0 && turnos[^1].Papel == papel)
                turnos[^1] = (papel, turnos[^1].Texto + "\n" + texto);
            else
                turnos.Add((papel, texto));
        }

        var atual = entrada.MensagemAtual?.Trim() ?? string.Empty;
        if (turnos.Count > 0 && turnos[^1].Papel == "user")
            turnos[^1] = ("user", turnos[^1].Texto + "\n" + atual);
        else
            turnos.Add(("user", atual));

        // A conversa precisa começar com o cidadão.
        while (turnos.Count > 0 && turnos[0].Papel != "user") turnos.RemoveAt(0);

        return [.. turnos.Select(object (t) => new { role = t.Papel, content = t.Texto })];
    }

    // ---------- leitura da resposta ----------

    private static bool TentarLerRespostaFinal(JsonElement raiz, out RespostaMotorRobo resposta)
    {
        resposta = default!;
        foreach (var bloco in Blocos(raiz))
        {
            if (!EhUsoDeFerramenta(bloco, out var nome, out var input)) continue;
            if (!string.Equals(nome, RoboFerramentaCatalogo.NomeResponderCidadao, StringComparison.OrdinalIgnoreCase))
                continue;

            var texto = LerTexto(input, "texto") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(texto)) continue;

            var motivo = LerTexto(input, "motivoHandoff");
            resposta = new RespostaMotorRobo(
                texto.Trim(),
                HandOff: input.TryGetProperty("handoff", out var h) && h.ValueKind == JsonValueKind.True,
                MotivoHandOff: string.IsNullOrWhiteSpace(motivo) ? null : motivo!.Trim(),
                Confianca: input.TryGetProperty("confianca", out var c) && c.TryGetDouble(out var v) ? v : null);
            return true;
        }
        return false;
    }

    private static List<(string Id, string Nome, JsonElement Args)> LerChamadasDeFerramenta(JsonElement raiz)
    {
        var chamadas = new List<(string, string, JsonElement)>();
        foreach (var bloco in Blocos(raiz))
        {
            if (!EhUsoDeFerramenta(bloco, out var nome, out var input)) continue;
            if (string.Equals(nome, RoboFerramentaCatalogo.NomeResponderCidadao, StringComparison.OrdinalIgnoreCase))
                continue; // terminal sem texto utilizável — não é comando a executar
            var id = bloco.TryGetProperty("id", out var i) ? i.GetString() ?? string.Empty : string.Empty;
            chamadas.Add((id, nome, input.Clone()));
        }
        return chamadas;
    }

    private async Task<object> ExecutarFerramentaAsync(
        EntradaMotorRobo entrada, string id, string nome, JsonElement argumentos, CancellationToken ct)
    {
        if (!RoboFerramentaCatalogo.PorNome.TryGetValue(nome, out var comando))
        {
            logger.LogWarning("Modelo pediu ferramenta desconhecida {Ferramenta}.", nome);
            return ResultadoFerramenta(id, "Ferramenta desconhecida.", erro: true);
        }

        var r = await dispatcher.ExecutarAsync(
            entrada.ConversaId, entrada.PacienteId, entrada.AssuntoId, comando, argumentos, ct);
        return ResultadoFerramenta(id, r.Mensagem, erro: !r.Sucesso);
    }

    private static object ResultadoFerramenta(string id, string conteudo, bool erro) => new
    {
        type = "tool_result",
        tool_use_id = id,
        content = conteudo,
        is_error = erro,
    };

    private static IEnumerable<JsonElement> Blocos(JsonElement raiz) =>
        raiz.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array
            ? c.EnumerateArray()
            : [];

    private static bool EhUsoDeFerramenta(JsonElement bloco, out string nome, out JsonElement input)
    {
        nome = string.Empty;
        input = default;
        if (!bloco.TryGetProperty("type", out var t) || t.GetString() != "tool_use") return false;
        nome = bloco.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        input = bloco.TryGetProperty("input", out var i) ? i : default;
        return nome.Length > 0;
    }

    private static string? LerTexto(JsonElement obj, string campo) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>Soma o uso do turno. Entrada inclui os tokens de cache (também são cobrados como
    /// entrada, com preço próprio — ver <see cref="PrecoModeloIa"/>).</summary>
    private static void SomarUso(JsonElement raiz, ref long entrada, ref long saida)
    {
        if (!raiz.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object) return;
        entrada += Ler(u, "input_tokens") + Ler(u, "cache_read_input_tokens") + Ler(u, "cache_creation_input_tokens");
        saida += Ler(u, "output_tokens");

        static long Ler(JsonElement o, string campo) =>
            o.TryGetProperty(campo, out var v) && v.TryGetInt64(out var n) ? n : 0;
    }

    // ---------- HTTP ----------

    private async Task<JsonDocument> EnviarAsync(string token, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, Json);

        for (var tentativa = 1; ; tentativa++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, EndpointMensagens)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("x-api-key", token);
            req.Headers.Add("anthropic-version", VersaoApi);

            using var resp = await http.SendAsync(req, ct);
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode) return JsonDocument.Parse(corpo);

            var transitorio = resp.StatusCode is HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                or (HttpStatusCode)529;
            if (!transitorio || tentativa >= MaxTentativasHttp)
                throw new InvalidOperationException($"Anthropic retornou {(int)resp.StatusCode}: {Truncar(corpo)}");

            var espera = TimeSpan.FromSeconds(Math.Pow(2, tentativa)); // 2s, 4s
            logger.LogWarning("Anthropic {Status} (tentativa {N}) — reesperando {Espera}s.",
                (int)resp.StatusCode, tentativa, espera.TotalSeconds);
            await Task.Delay(espera, ct);
        }
    }

    private async Task<string?> ObterTokenAsync(CancellationToken ct)
    {
        var cfg = await db.IaConfiguracoes.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.TokenCifrado)) return null;
        try
        {
            return protetor.Revelar(cfg.TokenCifrado);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível revelar o token da Anthropic para o robô.");
            return null;
        }
    }

    private static string Truncar(string s) => s.Length <= 300 ? s : s[..300];
}
