using System.IdentityModel.Tokens.Jwt;
using System.Net.Mime;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Identidade;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Proxy para o motor do Agente IA (serviço Python em 127.0.0.1:5085).
///
/// O trabalho roda no servidor: cria-se o turno, recebe-se um id e o painel faz polling.
/// Fechar a aba não interrompe nada. O corpo das respostas é repassado cru — o formato é
/// contrato entre o motor e o painel, e tipar aqui só criaria um ponto a mais para
/// desincronizar.
/// </summary>
[ApiController]
[Route("agente-ia")]
public sealed class AgenteIaController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUsuarioAtualAccessor _usuarioAtual;
    private readonly ILogger<AgenteIaController> _logger;
    private readonly string _baseUrl;
    private readonly string? _internalKey;

    public AgenteIaController(
        IHttpClientFactory httpClientFactory,
        IUsuarioAtualAccessor usuarioAtual,
        IConfiguration configuration,
        ILogger<AgenteIaController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _usuarioAtual = usuarioAtual;
        _logger = logger;
        _baseUrl = (configuration["AgenteIa:BaseUrl"] ?? "http://127.0.0.1:5085").TrimEnd('/');
        _internalKey = configuration["AgenteIa:InternalKey"];
    }

    /// <summary>Abre (ou reusa, quando há ticket) uma sessão do agente.</summary>
    [HttpPost("sessions")]
    [RequerPermissao(ModuloPermissao.AgenteIa, AcoesPermissao.Consulta)]
    public Task<IActionResult> CriarSessao([FromBody] object? corpo, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Post, "/internal/ai/sessions", corpo, ct);

    [HttpGet("sessions")]
    [RequerPermissao(ModuloPermissao.AgenteIa, AcoesPermissao.Consulta)]
    public Task<IActionResult> ListarSessoes([FromQuery] bool arquivadas, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get, $"/internal/ai/sessions?include_archived={(arquivadas ? "true" : "false")}", null, ct);

    /// <summary>Renomeia a conversa. O título default é o começo do primeiro prompt.</summary>
    [HttpPatch("sessions/{sessionId}")]
    [RequerPermissao(ModuloPermissao.AgenteIa, AcoesPermissao.Edicao)]
    public Task<IActionResult> RenomearSessao(string sessionId, [FromBody] object corpo, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Patch, $"/internal/ai/sessions/{Uri.EscapeDataString(sessionId)}", corpo, ct);

    [HttpPost("sessions/{sessionId}/unarchive")]
    [RequerPermissao(ModuloPermissao.AgenteIa, AcoesPermissao.Edicao)]
    public Task<IActionResult> RestaurarSessao(string sessionId, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Post, $"/internal/ai/sessions/{Uri.EscapeDataString(sessionId)}/unarchive", null, ct);

    /// <summary>Histórico completo — o painel usa para reatar ao reabrir a tela.</summary>
    [HttpGet("sessions/{sessionId}")]
    [RequerPermissao(ModuloPermissao.AgenteIa, AcoesPermissao.Consulta)]
    public Task<IActionResult> DetalheSessao(string sessionId, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get, $"/internal/ai/sessions/{Uri.EscapeDataString(sessionId)}", null, ct);

    [HttpDelete("sessions/{sessionId}")]
    [RequerPermissao(ModuloPermissao.AgenteIa, AcoesPermissao.Edicao)]
    public Task<IActionResult> ArquivarSessao(string sessionId, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Delete, $"/internal/ai/sessions/{Uri.EscapeDataString(sessionId)}", null, ct);

    /// <summary>Envia uma instrução ao agente. Exige Edicao: o agente age no servidor.</summary>
    [HttpPost("sessions/{sessionId}/turns")]
    [RequerPermissao(ModuloPermissao.AgenteIa, AcoesPermissao.Edicao)]
    public Task<IActionResult> CriarTurno(string sessionId, [FromBody] object corpo, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Post, $"/internal/ai/sessions/{Uri.EscapeDataString(sessionId)}/turns", corpo, ct);

    [HttpGet("turns/{turnId}")]
    [RequerPermissao(ModuloPermissao.AgenteIa, AcoesPermissao.Consulta)]
    public Task<IActionResult> ObterTurno(string turnId, [FromQuery] int cursor, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get, $"/internal/ai/turns/{Uri.EscapeDataString(turnId)}?cursor={cursor}", null, ct);

    [HttpPost("turns/{turnId}/cancel")]
    [RequerPermissao(ModuloPermissao.AgenteIa, AcoesPermissao.Edicao)]
    public Task<IActionResult> CancelarTurno(string turnId, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Post, $"/internal/ai/turns/{Uri.EscapeDataString(turnId)}/cancel", null, ct);

    // ------------------------------------------------------------------ infra

    private async Task<IActionResult> ProxyAsync(
        HttpMethod metodo, string caminho, object? corpo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_internalKey))
        {
            _logger.LogError("AgenteIa:InternalKey não configurada — proxy indisponível.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Agente IA não configurado no servidor." });
        }

        try
        {
            var client = _httpClientFactory.CreateClient("agente-ia");
            using var req = new HttpRequestMessage(metodo, $"{_baseUrl}{caminho}");
            if (corpo is not null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(corpo);
                req.Content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
            }
            req.Headers.TryAddWithoutValidation("X-SMSMarica-Internal-Key", _internalKey.Trim());

            // Quem está do outro lado. A lista de sessões é global — sem isto não dá para
            // saber quem abriu cada conversa nem quem interagiu nela. O nome vai
            // percent-encoded: cabeçalho HTTP é latin-1 e nome brasileiro tem acento.
            if (_usuarioAtual.UsuarioId is Guid usuarioId)
            {
                req.Headers.TryAddWithoutValidation("X-SMSMarica-Usuario-Id", usuarioId.ToString());
            }
            var nome = User.FindFirstValue(JwtRegisteredClaimNames.Name)
                ?? User.FindFirstValue(ClaimTypes.Name);
            if (!string.IsNullOrWhiteSpace(nome))
            {
                req.Headers.TryAddWithoutValidation("X-SMSMarica-Usuario-Nome", Uri.EscapeDataString(nome));
            }

            using var res = await client.SendAsync(req, ct);
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
            _logger.LogWarning(ex, "Motor do Agente IA indisponível em {Url}", _baseUrl);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Motor do Agente IA indisponível. Verifique 'systemctl status smsmarica-aiengine'." });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout falando com o motor do Agente IA");
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new { message = "Tempo esgotado ao falar com o motor do Agente IA." });
        }
    }
}
