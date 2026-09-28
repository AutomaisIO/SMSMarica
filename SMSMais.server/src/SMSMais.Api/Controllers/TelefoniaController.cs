using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Telefonia;
using SMSMais.Core.Telefonia.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Softphone dos usuários. O admin habilita e escolhe o ramal na edição do usuário (módulo
/// Telefonia); o próprio usuário só lê o seu e busca a credencial para o navegador registrar.
/// </summary>
[ApiController]
public sealed class TelefoniaController(ITelefoniaService service) : ControllerBase
{
    [HttpGet("usuarios/{usuarioId:guid}/softphone")]
    [RequerPermissao(ModuloPermissao.Telefonia, AcoesPermissao.Consulta)]
    [ProducesResponseType<SoftphoneUsuarioDto>(StatusCodes.Status200OK)]
    public Task<SoftphoneUsuarioDto> ObterDoUsuario(Guid usuarioId, CancellationToken ct) =>
        service.ObterDoUsuarioAsync(usuarioId, ct);

    [HttpPut("usuarios/{usuarioId:guid}/softphone")]
    [RequerPermissao(ModuloPermissao.Telefonia, AcoesPermissao.Edicao)]
    [ProducesResponseType<SoftphoneUsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<SoftphoneUsuarioDto> Definir(Guid usuarioId, [FromBody] DefinirSoftphoneRequest request, CancellationToken ct) =>
        service.DefinirAsync(usuarioId, request, ct);

    [HttpDelete("usuarios/{usuarioId:guid}/softphone")]
    [RequerPermissao(ModuloPermissao.Telefonia, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remover(Guid usuarioId, CancellationToken ct)
    {
        await service.RemoverAsync(usuarioId, ct);
        return NoContent();
    }

    /// <summary>Próximos números livres da faixa de softphone (para sugerir ao admin).</summary>
    [HttpGet("telefonia/ramais/livres")]
    [RequerPermissao(ModuloPermissao.Telefonia, AcoesPermissao.Consulta)]
    [ProducesResponseType<RamaisLivresDto>(StatusCodes.Status200OK)]
    public Task<RamaisLivresDto> Livres(CancellationToken ct) =>
        service.SugerirRamaisAsync(ct);

    /// <summary>Softphone do usuário logado (sem segredo). Não exige módulo: é o dele.</summary>
    [HttpGet("identidade/me/softphone")]
    [ProducesResponseType<SoftphoneUsuarioDto>(StatusCodes.Status200OK)]
    public Task<SoftphoneUsuarioDto> MeuSoftphone(CancellationToken ct) =>
        service.ObterDoUsuarioAsync(ExtrairUsuarioId(), ct);

    /// <summary>
    /// Credencial SIP do softphone do usuário logado, para o navegador registrar sozinho. Só o
    /// próprio usuário, só softphone ativo; nunca cacheada.
    /// </summary>
    [HttpGet("identidade/me/softphone/credencial")]
    [EnableRateLimiting("softphone-credencial")]
    [ProducesResponseType<CredencialSoftphoneDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<CredencialSoftphoneDto> MinhaCredencial(CancellationToken ct)
    {
        var credencial = await service.ObterCredencialAsync(ExtrairUsuarioId(), ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return credencial;
    }

    private Guid ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id)) return id;
        throw new ValidacaoException("auth.sub_invalido", "Token sem identificação do usuário.");
    }
}
