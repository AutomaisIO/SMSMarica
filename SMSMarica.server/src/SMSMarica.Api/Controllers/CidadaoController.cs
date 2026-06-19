using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.Cidadao;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Pacientes;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Área autenticada do app do cidadão (token <c>tipo=cidadao</c>, single-device).
/// O paciente só enxerga/edita os próprios dados — o id vem do <c>sub</c> do token,
/// nunca do corpo/rota. Os endpoints clínicos hoje são stub (lista vazia); o
/// preenchimento virá das fontes numa próxima leva.
/// </summary>
[ApiController]
[Route("auth/paciente")]
[Authorize]
public sealed class CidadaoController(
    IPacientesService pacientes,
    ICidadaoSessaoService sessoes) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<PerfilCidadaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PerfilCidadaoDto>> Me(CancellationToken ct)
    {
        var p = await pacientes.ObterPorIdAsync(PacienteId(), ct);
        return new PerfilCidadaoDto(
            p.Id, p.NomeCompleto, p.NomeSocial, p.Cpf, p.Cns, p.DataNascimento,
            p.Email, p.TelefonePrincipal, p.TelefoneCelular, p.TelefoneResidencial, p.FotoBase64);
    }

    [HttpPut("me/contato")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarContato(
        [FromBody] AtualizarContatoCidadaoRequest req, CancellationToken ct)
    {
        await pacientes.AtualizarContatoAsync(
            PacienteId(), req.Email, req.TelefonePrincipal, req.TelefoneCelular, req.TelefoneResidencial, ct);
        return NoContent();
    }

    [HttpPut("me/foto")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarFoto(
        [FromBody] AtualizarFotoCidadaoRequest req, CancellationToken ct)
    {
        await pacientes.AtualizarFotoAsync(PacienteId(), req.FotoBase64, ct);
        return NoContent();
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Jti() is Guid jti) await sessoes.RevogarAsync(jti, ct);
        return NoContent();
    }

    // --- Stubs clínicos: shape estável, lista vazia por enquanto. ---

    [HttpGet("meus-translados")]
    public ActionResult<IEnumerable<TransladoResumoDto>> MeusTranslados() =>
        Ok(Array.Empty<TransladoResumoDto>());

    [HttpGet("atendimentos")]
    public ActionResult<IEnumerable<AtendimentoResumoDto>> Atendimentos() =>
        Ok(Array.Empty<AtendimentoResumoDto>());

    [HttpGet("exames")]
    public ActionResult<IEnumerable<ExameResumoDto>> Exames() =>
        Ok(Array.Empty<ExameResumoDto>());

    [HttpGet("laudos")]
    public ActionResult<IEnumerable<LaudoResumoDto>> Laudos() =>
        Ok(Array.Empty<LaudoResumoDto>());

    /// <summary>Id do paciente (FHIR) a partir do <c>sub</c>; 403 se o token não for de cidadão.</summary>
    private Guid PacienteId()
    {
        if (User.FindFirstValue("tipo") != "cidadao")
            throw new UnauthorizedAccessException("Token não é de cidadão.");

        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token sem identificação de paciente.");
    }

    private Guid? Jti()
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        return Guid.TryParse(jti, out var id) ? id : null;
    }
}
