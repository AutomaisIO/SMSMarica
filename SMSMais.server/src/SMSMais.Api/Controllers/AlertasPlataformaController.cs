using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SMSMais.Api.Auth;
using SMSMais.Core.Alertas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Avisos de erro da plataforma no celular: quem recebe, o que é reportado (e o que está
/// silenciado) e o histórico do que saiu. Mesma permissão da tela de Erros do sistema — é o
/// mesmo público (quem cuida da plataforma).
/// </summary>
[ApiController]
[Route("alertas-plataforma")]
public sealed class AlertasPlataformaController(IAlertaPlataformaService service) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Consulta)]
    [ProducesResponseType<AlertaPainelDto>(StatusCodes.Status200OK)]
    public Task<AlertaPainelDto> Painel(CancellationToken cancellationToken) =>
        service.ObterPainelAsync(cancellationToken);

    /// <summary>Histórico de avisos, opcionalmente de uma fonte só.</summary>
    [HttpGet("envios")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AlertaEnvioDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AlertaEnvioDto>> Envios(
        [FromQuery] string? origem, [FromQuery] int limite = 100, CancellationToken cancellationToken = default) =>
        service.ListarEnviosAsync(origem, limite, cancellationToken);

    [HttpPost("destinatarios")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType<AlertaDestinatarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<AlertaDestinatarioDto> AdicionarDestinatario(
        [FromBody] SalvarAlertaDestinatarioRequest request, CancellationToken cancellationToken) =>
        service.AdicionarDestinatarioAsync(request, cancellationToken);

    [HttpPut("destinatarios/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType<AlertaDestinatarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<AlertaDestinatarioDto> AtualizarDestinatario(
        Guid id, [FromBody] SalvarAlertaDestinatarioRequest request, CancellationToken cancellationToken) =>
        service.AtualizarDestinatarioAsync(id, request, cancellationToken);

    [HttpDelete("destinatarios/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoverDestinatario(Guid id, CancellationToken cancellationToken)
    {
        await service.RemoverDestinatarioAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Silencia (ou volta a reportar) uma fonte. Silenciada continua sendo contada.</summary>
    [HttpPut("origens/{chave}/silencio")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType<AlertaOrigemDto>(StatusCodes.Status200OK)]
    public Task<AlertaOrigemDto> Silenciar(
        string chave, [FromBody] SilenciarAlertaOrigemRequest request, CancellationToken cancellationToken) =>
        service.SilenciarAsync(chave, request.Silenciada, cancellationToken);

    /// <summary>Manda um aviso de teste agora e devolve o que a Meta respondeu para cada número.</summary>
    [HttpPost("testar")]
    [RequerPermissao(ModuloPermissao.Erros, AcoesPermissao.Edicao)]
    [ProducesResponseType<AlertaEnvioDto>(StatusCodes.Status200OK)]
    public Task<AlertaEnvioDto> Testar(CancellationToken cancellationToken) =>
        service.TestarAsync(cancellationToken);

    public const string PoliticaDeLimiteExterno = "alerta-monitor-externo";

    /// <summary>
    /// Entrada do monitor que roda DENTRO do servidor do PACS (memória do Java, s3fs, reinício
    /// programado — docs/pacs.md §11.1). Sem o login do painel: a chave vem em
    /// <c>X-Monitor-Chave</c> e só aceita as fontes <c>pacs.*</c> do catálogo. O aviso segue o
    /// caminho de sempre (fila → freio → WhatsApp → histórico na tela Avisos no celular).
    /// </summary>
    [HttpPost("externo")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimiteExterno)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ReportarExterno(
        [FromBody] AlertaExternoRequest request,
        [FromServices] IAlertaPlataforma alerta,
        [FromServices] IOptions<AlertaPlataformaOptions> opcoes)
    {
        var chave = opcoes.Value.ChaveMonitorExterno;
        if (string.IsNullOrWhiteSpace(chave)) return NotFound();
        if (!ChaveConfere(Request.Headers["X-Monitor-Chave"].ToString(), chave)) return Unauthorized();

        if (!AlertaCatalogo.AceitaDeMonitorExterno(request.Origem ?? string.Empty))
            return BadRequest(new { mensagem = $"Fonte '{request.Origem}' não aceita do monitor externo." });
        if (string.IsNullOrWhiteSpace(request.Titulo))
            return BadRequest(new { mensagem = "Informe 'titulo'." });

        var detalhe = (request.Detalhe ?? string.Empty).Trim();
        if (detalhe.Length > 4000) detalhe = detalhe[..4000];

        alerta.Reportar(request.Origem!, request.Titulo.Trim(), detalhe);
        return Accepted();
    }

    private static bool ChaveConfere(string apresentada, string esperada)
    {
        if (string.IsNullOrEmpty(apresentada)) return false;
        var a = Encoding.UTF8.GetBytes(apresentada);
        var b = Encoding.UTF8.GetBytes(esperada);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

public sealed record AlertaExternoRequest(string? Origem, string? Titulo, string? Detalhe);
