using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Identidade;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Core.PendenciasCadastro.Dtos;
using SMSMais.Core.RoboAtendimento.Runtime;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.PendenciasCadastro;

public interface IVarreduraContatoNegadoService
{
    /// <summary>Varre as conversas recentes atrás de "não sou essa pessoa" e registra as pendências.</summary>
    Task<VarreduraContatoNegadoResultadoDto> ExecutarAsync(int horas, CancellationToken ct = default);
}

/// <summary>
/// Varredura de CONTATO NEGADO em duas etapas — pedida pelo usuário em 02/09/2026 depois de um dia
/// com "muita quantidade de gente reclamando que não é a pessoa":
///
/// <para><b>Etapa 1 (recall, barata):</b> padrões amplos sobre o texto normalizado das mensagens de
/// entrada da janela ("nao sou", "numero errado", "engano"...). Pega demais de propósito — o filtro
/// fino não é daqui.</para>
///
/// <para><b>Etapa 2 (precisão, Haiku):</b> cada conversa candidata vai ao modelo com as últimas
/// mensagens; ele responde por ferramenta forçada se há negação de identidade, o vínculo declarado
/// e a confiança. Só negação com confiança ≥ 0,6 vira pendência.</para>
///
/// <para>O efeito reusa o caminho oficial: <see cref="IPendenciaCadastroService.RegistrarNumeroErradoAsync"/>
/// (idempotente por telefone+paciente aberta, e carimba o ❗ no telecom do paciente). Roda manual
/// pelo botão da tela de Pendências; se virar rotina, é só agendar chamando este mesmo serviço.</para>
/// </summary>
public sealed class VarreduraContatoNegadoService(
    IHttpClientFactory httpFactory,
    SmsMaisDbContext db,
    IProtetorSegredos protetor,
    IPendenciaCadastroService pendencias,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<VarreduraContatoNegadoService> logger) : IVarreduraContatoNegadoService
{
    /// <summary>Teto de conversas que vão ao modelo por execução — trava de custo.</summary>
    private const int MaxCandidatas = 200;

    private const int MensagensDeContexto = 10;

    /// <summary>Padrões AMPLOS (normalizados) da etapa 1. Errar para mais é o desenho: o Haiku
    /// descarta; errar para menos é caso perdido.</summary>
    private static readonly string[] Padroes =
    [
        "nao sou", "nao e essa pessoa", "nao e a pessoa", "pessoa errada", "numero errado",
        "contato errado", "engano", "nao conheco", "nao pertence", "nao mora", "nao e meu numero",
        "nao e o numero", "numero novo", "mudei de numero", "mudando de contato", "nao e dele",
        "nao e dela", "apertei errado", "mensagem errada", "nao e comigo",
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<VarreduraContatoNegadoResultadoDto> ExecutarAsync(int horas, CancellationToken ct = default)
    {
        if (horas is < 1 or > 24 * 14) horas = 48;
        var desde = DateTime.UtcNow.AddHours(-horas);

        // Etapa 1 — mensagens de entrada da janela; o padrão roda em memória (normalização com
        // remoção de acento não é traduzível para o LIKE do banco sem espalhar unaccent por aqui).
        var entradas = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.Direcao == DirecaoMensagem.Entrada && m.OcorridoEm >= desde
                && m.Conteudo != null && m.ConversaId != null)
            .Select(m => new { m.ConversaId, m.Conteudo })
            .ToListAsync(ct);

        var candidatas = entradas
            .Where(m => Padroes.Any(p => RoboClassificador.NormalizarTexto(m.Conteudo!).Contains(p, StringComparison.Ordinal)))
            .Select(m => m.ConversaId!.Value)
            .Distinct()
            .ToList();

        var resultado = new VarreduraContatoNegadoResultadoDto
        {
            Horas = horas,
            MensagensLidas = entradas.Count,
            ConversasCandidatas = candidatas.Count,
        };
        if (candidatas.Count == 0) return resultado;
        if (candidatas.Count > MaxCandidatas)
        {
            logger.LogWarning("Varredura de contato negado: {Total} candidatas, limitando a {Max}.",
                candidatas.Count, MaxCandidatas);
            candidatas = [.. candidatas.Take(MaxCandidatas)];
        }

        var token = await ObterTokenAsync(ct) ?? throw new InvalidOperationException(
            "Token da Anthropic não configurado (Configuração da IA) — varredura indisponível.");
        var modelo = await db.RoboConfiguracoes.AsNoTracking()
            .Select(c => c.ModeloPadrao).FirstOrDefaultAsync(ct) ?? "claude-haiku-4-5-20251001";

        foreach (var conversaId in candidatas)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var item = await AvaliarConversaAsync(conversaId, token, modelo, ct);
                if (item is not null) resultado.Itens.Add(item);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Varredura: falha ao avaliar a conversa {Conversa}.", conversaId);
                resultado.Erros++;
            }
        }
        return resultado;
    }

    private async Task<VarreduraContatoNegadoItemDto?> AvaliarConversaAsync(
        Guid conversaId, string token, string modelo, CancellationToken ct)
    {
        var conversa = await db.Conversas.AsNoTracking()
            .Where(c => c.Id == conversaId)
            .Select(c => new { c.TelefoneCanonical, c.PacienteId })
            .FirstOrDefaultAsync(ct);
        if (conversa is null) return null;

        // Paciente-alvo: a quem a última mensagem enviada se referia (o "dono errado" do número).
        var alvo = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.ConversaId == conversaId && m.Direcao == DirecaoMensagem.Saida && m.PacienteId != null)
            .OrderByDescending(m => m.OcorridoEm)
            .Select(m => m.PacienteId)
            .FirstOrDefaultAsync(ct) ?? conversa.PacienteId;

        // Já há pendência ABERTA para este telefone+paciente? Não gasta modelo à toa.
        var jaExiste = await db.PendenciasCadastro.AsNoTracking().AnyAsync(
            p => p.Status == StatusPendenciaCadastro.Aberta
                && p.TelefoneCanonical == conversa.TelefoneCanonical
                && p.PacienteId == alvo, ct);
        if (jaExiste)
            return new VarreduraContatoNegadoItemDto(conversaId, conversa.TelefoneCanonical, alvo,
                Acao: "ja-existia", Resumo: null);

        var contexto = await MontarContextoAsync(conversaId, ct);
        if (contexto.Length == 0) return null;

        var veredito = await ClassificarAsync(contexto, token, modelo, ct);
        if (veredito is null || !veredito.Negacao || veredito.Confianca < 0.6)
            return new VarreduraContatoNegadoItemDto(conversaId, conversa.TelefoneCanonical, alvo,
                Acao: "descartada-pelo-modelo", Resumo: veredito?.Resumo);

        await pendencias.RegistrarNumeroErradoAsync(
            conversaId, conversa.TelefoneCanonical, alvo, veredito.Vinculo,
            $"Varredura: {veredito.Resumo}", usuarioAtual.UsuarioId, ct);

        return new VarreduraContatoNegadoItemDto(conversaId, conversa.TelefoneCanonical, alvo,
            Acao: "pendencia-registrada", Resumo: veredito.Resumo);
    }

    private async Task<string> MontarContextoAsync(Guid conversaId, CancellationToken ct)
    {
        var recentes = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.ConversaId == conversaId && m.TipoMensagem != TipoMensagem.NotaInterna
                && m.Conteudo != null)
            .OrderByDescending(m => m.OcorridoEm)
            .Take(MensagensDeContexto)
            .Select(m => new { m.Direcao, m.TipoMensagem, m.AutorUsuarioId, m.Conteudo })
            .ToListAsync(ct);
        recentes.Reverse();

        var sb = new StringBuilder();
        foreach (var m in recentes)
        {
            var papel = m.Direcao == DirecaoMensagem.Entrada ? "CIDADAO"
                : m.TipoMensagem == TipoMensagem.Robo ? "ROBO"
                : m.AutorUsuarioId != null ? "ATENDENTE"
                : "SISTEMA";
            var texto = m.Conteudo!.Length > 400 ? m.Conteudo[..400] : m.Conteudo;
            sb.AppendLine($"[{papel}] {texto}");
        }
        return sb.ToString();
    }

    private sealed record Veredito(bool Negacao, VinculoContato Vinculo, double Confianca, string? Resumo);

    /// <summary>Etapa 2: uma chamada à Messages API com ferramenta única FORÇADA — mesma mecânica
    /// (e mesmo HttpClient/token) do motor do robô, sem loop.</summary>
    private async Task<Veredito?> ClassificarAsync(string contexto, string token, string modelo, CancellationToken ct)
    {
        var body = new
        {
            model = modelo,
            max_tokens = 300,
            system = "Você revisa conversas de WhatsApp da Secretaria de Saúde. Sua única tarefa: "
                + "dizer se QUEM RESPONDE nega ser o paciente a quem a mensagem se destinava — "
                + "\"não sou essa pessoa\", \"número errado\", \"não conheço\", \"foi engano\", "
                + "\"a pessoa não mora aqui\", \"mudei de número\" (o número antigo ficou para trás). "
                + "NÃO é negação: parente/responsável que ATENDE PELO paciente e segue a conversa "
                + "(\"sou a mãe dele\" ajudando), reclamação de atendimento, recusa de comparecer, "
                + "cancelamento de consulta. Responda SOMENTE pela ferramenta.",
            tools = new object[]
            {
                new
                {
                    name = "classificar_contato",
                    description = "Devolve o veredito sobre a conversa.",
                    input_schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            negacao = new { type = "boolean", description = "true se quem responde nega ser o paciente / diz que o número não é dele." },
                            vinculo = new { type = "string", @enum = new[] { "Parente", "Responsavel", "SemVinculo", "NaoInformado" }, description = "Vínculo declarado de quem responde com o paciente." },
                            confianca = new { type = "number", description = "0 a 1." },
                            resumo = new { type = "string", description = "Uma frase objetiva do que a pessoa disse." },
                        },
                        required = new[] { "negacao", "vinculo", "confianca", "resumo" },
                    },
                },
            },
            tool_choice = new { type = "tool", name = "classificar_contato" },
            messages = new object[] { new { role = "user", content = contexto } },
        };

        var http = httpFactory.CreateClient(RoboAtendimentoMotorApi.HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("x-api-key", token);
        req.Headers.Add("anthropic-version", "2023-06-01");

        using var resp = await http.SendAsync(req, ct);
        var corpo = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Varredura: Messages API {Status}: {Corpo}", (int)resp.StatusCode,
                corpo.Length > 300 ? corpo[..300] : corpo);
            return null;
        }

        using var doc = JsonDocument.Parse(corpo);
        foreach (var bloco in doc.RootElement.GetProperty("content").EnumerateArray())
        {
            if (bloco.GetProperty("type").GetString() != "tool_use") continue;
            var input = bloco.GetProperty("input");
            var vinculoTexto = input.TryGetProperty("vinculo", out var v) ? v.GetString() : null;
            Enum.TryParse<VinculoContato>(vinculoTexto, ignoreCase: true, out var vinculo);
            return new Veredito(
                input.TryGetProperty("negacao", out var n) && n.ValueKind == JsonValueKind.True,
                vinculo == 0 ? VinculoContato.NaoInformado : vinculo,
                input.TryGetProperty("confianca", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0,
                input.TryGetProperty("resumo", out var r) ? r.GetString() : null);
        }
        return null;
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
            logger.LogWarning(ex, "Não foi possível revelar o token da Anthropic para a varredura.");
            return null;
        }
    }
}
