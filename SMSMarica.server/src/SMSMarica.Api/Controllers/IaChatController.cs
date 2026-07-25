using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Inteligencia.Conhecimento;
using SMSMarica.Core.Tfd.Configuracao;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Chat de dados do menu IA: cada pergunta vive numa sessão conversável, RESTRITA a consultar
/// a base escolhida. Faz proxy para o motor local (Claude Code por assinatura, em 127.0.0.1:5085)
/// no modo <c>dados</c> — sandbox sem shell/arquivo/git/rede, só o tool que chama o proxy SQL
/// interno. As sessões são por usuário e NÃO aparecem no Agente IA. Ver ADR-0023.
///
/// Diferença para o Agente IA: aqui, a cada turno, o contexto de schema (RAG dos embeddings da
/// base) é recuperado no .NET e anexado à pergunta — o motor não conhece o banco de negócio.
/// </summary>
[ApiController]
[Route("ia/chat")]
public sealed class IaChatController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUsuarioAtualAccessor _usuarioAtual;
    private readonly IRecuperadorContexto _recuperador;
    private readonly ITfdConfigService _tfdConfig;
    private readonly SmsMaricaDbContext _db;
    private readonly ILogger<IaChatController> _logger;
    private readonly string _baseUrl;
    private readonly string? _internalKey;

    public IaChatController(
        IHttpClientFactory httpClientFactory,
        IUsuarioAtualAccessor usuarioAtual,
        IRecuperadorContexto recuperador,
        ITfdConfigService tfdConfig,
        SmsMaricaDbContext db,
        IConfiguration configuration,
        ILogger<IaChatController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _usuarioAtual = usuarioAtual;
        _recuperador = recuperador;
        _tfdConfig = tfdConfig;
        _db = db;
        _logger = logger;
        _baseUrl = (configuration["AgenteIa:BaseUrl"] ?? "http://127.0.0.1:5085").TrimEnd('/');
        _internalKey = configuration["AgenteIa:InternalKey"];
    }

    /// <summary>
    /// Chave do Google Maps JS para renderizar os mapas do chat. Reusa a config da Google Maps
    /// Platform do TFD (mesma chave de geocoding/rotas). A chave é restrita por referrer HTTP ao
    /// domínio do painel — por isso pode ir ao navegador. Devolve null se não configurada/ativa.
    /// </summary>
    [HttpGet("maps-key")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    public async Task<IActionResult> MapsKey(CancellationToken ct)
    {
        var g = await _tfdConfig.ObterGoogleAsync(ct);
        if (!g.Ativo || !g.ChaveConfigurada)
        {
            return Ok(new { apiKey = (string?)null });
        }
        var ctx = await _tfdConfig.ObterGoogleContextoAsync(ct);
        return Ok(new { apiKey = string.IsNullOrWhiteSpace(ctx.ApiKey) ? null : ctx.ApiKey });
    }

    /// <summary>Abre uma sessão de dados presa a uma base (fonte). Uma sessão = uma conversa.</summary>
    [HttpPost("sessions")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    public async Task<IActionResult> CriarSessao([FromBody] CriarSessaoChatRequest req, CancellationToken ct)
    {
        var fonte = await _db.IaFontes.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == req.FonteId && f.Ativo && f.ExcluidoEm == null, ct);
        if (fonte is null)
        {
            return NotFound(new { message = "Base não encontrada ou inativa." });
        }

        var corpo = new { baseSlug = fonte.Slug, title = string.Empty };
        return await ProxyAsync(HttpMethod.Post, "/internal/ai/dados/sessions", corpo, ct);
    }

    [HttpGet("sessions")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    public Task<IActionResult> ListarSessoes([FromQuery] bool arquivadas, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get,
            $"/internal/ai/dados/sessions?include_archived={(arquivadas ? "true" : "false")}", null, ct);

    [HttpGet("sessions/{sessionId}")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    public Task<IActionResult> DetalheSessao(string sessionId, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get,
            $"/internal/ai/dados/sessions/{Uri.EscapeDataString(sessionId)}", null, ct);

    [HttpDelete("sessions/{sessionId}")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    public Task<IActionResult> ArquivarSessao(string sessionId, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Delete,
            $"/internal/ai/dados/sessions/{Uri.EscapeDataString(sessionId)}", null, ct);

    /// <summary>
    /// Envia uma pergunta. Antes de repassar, recupera o schema relevante (RAG dos embeddings da
    /// base) e anexa à pergunta — a pergunta vem primeiro para o título da sessão ficar limpo.
    /// </summary>
    [HttpPost("sessions/{sessionId}/turns")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    public async Task<IActionResult> CriarTurno(
        string sessionId, [FromBody] CriarTurnoChatRequest req, CancellationToken ct)
    {
        var pergunta = (req.Prompt ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(pergunta))
        {
            return BadRequest(new { message = "Informe a pergunta." });
        }

        var fonte = await _db.IaFontes.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == req.FonteId && f.Ativo && f.ExcluidoEm == null, ct);
        if (fonte is null)
        {
            return NotFound(new { message = "Base não encontrada ou inativa." });
        }

        var promptFinal = await MontarPromptComContextoAsync(
            fonte.Id, fonte.Dialeto.ToString(), pergunta, req.ModoDev, ct);
        return await ProxyAsync(HttpMethod.Post,
            $"/internal/ai/dados/sessions/{Uri.EscapeDataString(sessionId)}/turns",
            new { prompt = promptFinal }, ct);
    }

    [HttpGet("turns/{turnId}")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    public Task<IActionResult> ObterTurno(string turnId, [FromQuery] int cursor, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get,
            $"/internal/ai/turns/{Uri.EscapeDataString(turnId)}?cursor={cursor}", null, ct);

    [HttpPost("turns/{turnId}/cancel")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    public Task<IActionResult> CancelarTurno(string turnId, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Post,
            $"/internal/ai/turns/{Uri.EscapeDataString(turnId)}/cancel", null, ct);

    /// <summary>
    /// Avaliação de uma resposta (👍/👎). Um 👎 vira item pendente na tela de Melhorias de IA.
    /// Guarda o snapshot (pergunta + resposta) + a família da base, para tratar/enriquecer depois.
    /// </summary>
    [HttpPost("feedback")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    public async Task<IActionResult> Feedback([FromBody] FeedbackChatRequest req, CancellationToken ct)
    {
        if (req.FonteId == Guid.Empty || string.IsNullOrWhiteSpace(req.Pergunta))
        {
            return BadRequest(new { message = "Pergunta e base são obrigatórias." });
        }
        var fonte = await _db.IaFontes.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == req.FonteId && f.ExcluidoEm == null, ct);
        if (fonte is null)
        {
            return NotFound(new { message = "Base não encontrada." });
        }

        _db.IaConsultaFeedbacks.Add(new IaConsultaFeedback
        {
            Id = Guid.CreateVersion7(),
            FonteId = fonte.Id,
            Familia = fonte.Familia,
            Pergunta = req.Pergunta.Trim(),
            Resposta = string.IsNullOrWhiteSpace(req.Resposta) ? null : req.Resposta,
            Util = req.Util,
            Comentario = string.IsNullOrWhiteSpace(req.Comentario) ? null : req.Comentario.Trim(),
            Status = StatusFeedbackIa.Pendente,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { registrado = true });
    }

    // ------------------------------------------------------------------ contexto

    private async Task<string> MontarPromptComContextoAsync(
        Guid fonteId, string dialeto, string pergunta, bool modoDev, CancellationToken ct)
    {
        ContextoRecuperado contexto;
        try
        {
            contexto = await _recuperador.RecuperarAsync(fonteId, pergunta, ct);
        }
        catch (Exception ex)
        {
            // Sem contexto o agente ainda pode explorar via a ferramenta; não travar a pergunta.
            _logger.LogWarning(ex, "Falha ao recuperar contexto RAG da base {FonteId}", fonteId);
            contexto = new ContextoRecuperado(string.Empty, string.Empty);
        }

        var sb = new StringBuilder();
        sb.AppendLine(pergunta);
        sb.AppendLine();
        // Modo de exibição do operador (o checkbox do painel). Leigo = resposta sem NENHUM termo
        // técnico; desenvolvedor = pode detalhar tabelas/SQL na explicação (ele vê o raciocínio).
        sb.AppendLine(modoDev
            ? "[MODO DESENVOLVEDOR] O operador é técnico e vê o raciocínio. Pode detalhar tabelas/"
              + "colunas/SQL na explicação, se ajudar. A resposta final ainda deve ter uma linha clara."
            : "[OPERADOR LEIGO] A resposta final deve ser 100% em linguagem de negócio: sem nome de "
              + "base, tabela, coluna, slug ou SQL, e sem pedir nada técnico ao operador.");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine($"Contexto para montar o SQL (dialeto **{dialeto}**). Use como referência; " +
                      "não repita isto ao operador.");

        if (!string.IsNullOrWhiteSpace(contexto.ConhecimentoRecuperado))
        {
            sb.AppendLine().AppendLine("# Conhecimento recuperado (schema)")
              .AppendLine(contexto.ConhecimentoRecuperado);
        }

        if (!string.IsNullOrWhiteSpace(contexto.AprendizadosAtivos))
        {
            sb.AppendLine().AppendLine("# Aprendizados ativos").AppendLine(contexto.AprendizadosAtivos);
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------------ proxy

    private async Task<IActionResult> ProxyAsync(
        HttpMethod metodo, string caminho, object? corpo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_internalKey))
        {
            _logger.LogError("AgenteIa:InternalKey não configurada — chat de dados indisponível.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Motor de IA não configurado no servidor." });
        }

        try
        {
            var client = _httpClientFactory.CreateClient("agente-ia");
            using var reqMsg = new HttpRequestMessage(metodo, $"{_baseUrl}{caminho}");
            if (corpo is not null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(corpo);
                reqMsg.Content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
            }
            reqMsg.Headers.TryAddWithoutValidation("X-SMSMarica-Internal-Key", _internalKey.Trim());

            // Identidade do operador — é o que segmenta as sessões de dados por usuário.
            if (_usuarioAtual.UsuarioId is Guid usuarioId)
            {
                reqMsg.Headers.TryAddWithoutValidation("X-SMSMarica-Usuario-Id", usuarioId.ToString());
            }
            var nome = User.FindFirstValue(JwtRegisteredClaimNames.Name) ?? User.FindFirstValue(ClaimTypes.Name);
            if (!string.IsNullOrWhiteSpace(nome))
            {
                reqMsg.Headers.TryAddWithoutValidation("X-SMSMarica-Usuario-Nome", Uri.EscapeDataString(nome));
            }

            using var res = await client.SendAsync(reqMsg, ct);
            var payload = await res.Content.ReadAsStringAsync(ct);
            return new ContentResult
            {
                StatusCode = (int)res.StatusCode,
                Content = string.IsNullOrWhiteSpace(payload) ? "{}" : payload,
                ContentType = MediaTypeNames.Application.Json,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Motor de IA indisponível em {Url}", _baseUrl);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Motor de IA indisponível. Verifique 'systemctl status smsmarica-aiengine'." });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout falando com o motor de IA");
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new { message = "Tempo esgotado ao falar com o motor de IA." });
        }
    }
}

public sealed record CriarSessaoChatRequest(Guid FonteId);

public sealed record CriarTurnoChatRequest(Guid FonteId, string? Prompt, bool ModoDev = false);

public sealed record FeedbackChatRequest(
    Guid FonteId, string? Pergunta, string? Resposta, bool Util, string? Comentario);
