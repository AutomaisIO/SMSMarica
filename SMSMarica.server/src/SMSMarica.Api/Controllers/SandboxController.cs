using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Sandbox;
using SMSMarica.Core.Sandbox.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Sandbox de QA (admin). Opera sobre um paciente REAL para testar magic link, visualização no
/// PWA e o fluxo de confirmação — sem fabricar dados clínicos. Ver <see cref="ISandboxService"/>.
/// </summary>
[ApiController]
[Route("sandbox")]
public sealed class SandboxController(ISandboxService service) : ControllerBase
{
    /// <summary>Busca paciente real por nome/CPF (mín. 3 caracteres).</summary>
    [HttpGet("pacientes")]
    [RequerPermissao(ModuloPermissao.Sandbox, AcoesPermissao.Consulta)]
    [ProducesResponseType<IEnumerable<SandboxPacienteDto>>(StatusCodes.Status200OK)]
    public async Task<IEnumerable<SandboxPacienteDto>> BuscarPacientes([FromQuery] string termo, CancellationToken ct) =>
        await service.BuscarPacientesAsync(termo, ct);

    /// <summary>Solicitações de exame do paciente (para escolher em qual testar a confirmação).</summary>
    [HttpGet("pacientes/{pacienteId:guid}/solicitacoes")]
    [RequerPermissao(ModuloPermissao.Sandbox, AcoesPermissao.Consulta)]
    [ProducesResponseType<IEnumerable<SandboxSolicitacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IEnumerable<SandboxSolicitacaoDto>> ListarSolicitacoes(Guid pacienteId, CancellationToken ct) =>
        await service.ListarSolicitacoesAsync(pacienteId, ct);

    /// <summary>Gera um magic link (login como o paciente) com destino configurável.</summary>
    [HttpPost("magic-link")]
    [RequerPermissao(ModuloPermissao.Sandbox, AcoesPermissao.Edicao)]
    [ProducesResponseType<LinkTesteDto>(StatusCodes.Status200OK)]
    public async Task<LinkTesteDto> GerarLink([FromBody] GerarLinkRequest req, CancellationToken ct) =>
        await service.GerarLinkAsync(req, ct);

    /// <summary>Envia mensagem de TEXTO LIVRE (janela de 24h) para um telefone, com link opcional.</summary>
    [HttpPost("mensagem")]
    [RequerPermissao(ModuloPermissao.Sandbox, AcoesPermissao.Edicao)]
    [ProducesResponseType<ResultadoEnvioTesteDto>(StatusCodes.Status200OK)]
    public async Task<ResultadoEnvioTesteDto> Enviar([FromBody] EnviarMensagemTesteRequest req, CancellationToken ct) =>
        await service.EnviarMensagemAsync(req, ct);

    /// <summary>Força o estado de confirmação de uma solicitação (reversível): pendente|confirmada|cancelada.</summary>
    [HttpPost("confirmacao")]
    [RequerPermissao(ModuloPermissao.Sandbox, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DefinirConfirmacao([FromBody] DefinirConfirmacaoRequest req, CancellationToken ct)
    {
        await service.DefinirConfirmacaoAsync(req, ct);
        return NoContent();
    }
}
