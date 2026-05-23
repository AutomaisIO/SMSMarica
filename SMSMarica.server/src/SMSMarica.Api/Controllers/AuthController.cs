using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Identidade.Dtos;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("identidade")]
public sealed class AuthController(IIdentidadeService service) : ControllerBase
{
    private readonly IIdentidadeService _service = service;

    /// <summary>Autentica usuário e devolve JWT + permissões resolvidas.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<LoginRespostaDto> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        await _service.LoginAsync(request, cancellationToken);

    /// <summary>Permissões resolvidas (herdadas + overrides) do usuário autenticado.</summary>
    [HttpGet("me/permissoes")]
    [ProducesResponseType<PermissoesResolvidasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<PermissoesResolvidasDto> MinhasPermissoes(CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        return await _service.ObterPermissoesResolvidasAsync(usuarioId, cancellationToken);
    }

    /// <summary>Troca a senha do próprio usuário (verifica a atual e limpa a flag de troca obrigatória).</summary>
    [HttpPut("me/senha")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AlterarMinhaSenha(
        [FromBody] AlterarMinhaSenhaRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        await _service.AlterarMinhaSenhaAsync(usuarioId, request, cancellationToken);
        return NoContent();
    }

    private Guid ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id)) return id;
        throw new ValidacaoException("auth.sub_invalido", "Token sem identificação do usuário.");
    }
}
