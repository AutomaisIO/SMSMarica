using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;

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

    /// <summary>Retorna o usuário autenticado (perfis incluídos).</summary>
    [HttpGet("me")]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    public async Task<UsuarioDto> ObterMeu(CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        return await _service.ObterPorIdAsync(usuarioId, cancellationToken);
    }

    /// <summary>Atualiza dados editáveis pelo próprio usuário (foto/telefone/endereço).</summary>
    [HttpPut("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarMinhaConta(
        [FromBody] AtualizarMinhaContaRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        await _service.AtualizarMinhaContaAsync(usuarioId, request, cancellationToken);
        return NoContent();
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

    /// <summary>Preferências de UI do usuário autenticado (ex.: tela default de cada seção do menu).</summary>
    [HttpGet("me/preferencias")]
    [ProducesResponseType<PreferenciasUiDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<PreferenciasUiDto> MinhasPreferencias(CancellationToken cancellationToken) =>
        await _service.ObterPreferenciasUiAsync(ExtrairUsuarioId(), cancellationToken);

    /// <summary>Salva as preferências de UI do próprio usuário.</summary>
    [HttpPut("me/preferencias")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SalvarMinhasPreferencias(
        [FromBody] PreferenciasUiDto request,
        CancellationToken cancellationToken)
    {
        await _service.SalvarPreferenciasUiAsync(ExtrairUsuarioId(), request, cancellationToken);
        return NoContent();
    }

    private Guid ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id)) return id;
        throw new ValidacaoException("auth.sub_invalido", "Token sem identificação do usuário.");
    }
}
