using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Sandbox;
using SMSMais.Core.Sandbox.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Sandbox de QA (admin). Opera sobre um paciente REAL para testar magic link, visualização no
/// PWA e o fluxo de confirmação — sem fabricar dados clínicos. Ver <see cref="ISandboxService"/>.
/// </summary>
[ApiController]
[Route("sandbox")]
public sealed class SandboxController(
    ISandboxService service,
    IPersonificacaoPacienteService personificacao) : ControllerBase
{
    /// <summary>"Entrar como paciente": se o operador logado pode usar e qual paciente está valendo.</summary>
    [HttpGet("personificacao")]
    [RequerPermissao(ModuloPermissao.Sandbox, AcoesPermissao.Consulta)]
    [ProducesResponseType<PersonificacaoStatusDto>(StatusCodes.Status200OK)]
    public async Task<PersonificacaoStatusDto> StatusPersonificacao(CancellationToken ct) =>
        await personificacao.ObterStatusAsync(ct);

    /// <summary>
    /// A partir de agora, o CPF do operador no app do cidadão abre como este paciente (o código
    /// continua indo para o WhatsApp do operador). Substitui o anterior; vale 12h ou até encerrar.
    /// </summary>
    [HttpPost("personificacao")]
    [RequerPermissao(ModuloPermissao.Sandbox, AcoesPermissao.Edicao)]
    [ProducesResponseType<PersonificacaoStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<PersonificacaoStatusDto> AtivarPersonificacao(
        [FromBody] AtivarPersonificacaoRequest req, CancellationToken ct) =>
        await personificacao.AtivarAsync(req.PacienteId, ct);

    /// <summary>Encerra: o CPF do operador volta a abrir o app como ele mesmo e as sessões abertas caem.</summary>
    [HttpDelete("personificacao")]
    [RequerPermissao(ModuloPermissao.Sandbox, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EncerrarPersonificacao(CancellationToken ct)
    {
        await personificacao.EncerrarAsync(ct);
        return NoContent();
    }

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

    /// <summary>Simula o ciclo dos checks do zap (✓ enviada, ✓✓ entregue, ✓✓ azul lida,
    /// visualizada, ⚠ falha, reset) de uma comunicação, sem falar com a Meta.</summary>
    [HttpPost("comunicacao")]
    [RequerPermissao(ModuloPermissao.Sandbox, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SimularComunicacao([FromBody] SimularComunicacaoRequest req, CancellationToken ct)
    {
        await service.SimularComunicacaoAsync(req, ct);
        return NoContent();
    }

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
