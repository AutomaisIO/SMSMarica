using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Integracoes.Pep;
using SMSMarica.Core.Integracoes.Pep.Divergencias;
using SMSMarica.Core.Integracoes.Pep.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Sincronização/importação de bases de PEP (Salux e futuros) para o hub FHIR. Seleciona a
/// base (IaFonte), dispara um job em background e acompanha status/progresso. Ver ADR-0014.
/// </summary>
[ApiController]
[Route("pep-sincronizacao")]
public sealed class PepSincronizacaoController(IPepSincronizacaoService service) : ControllerBase
{
    /// <summary>Bases disponíveis para importação (com flag de suporte e última sincronização).</summary>
    [HttpGet("bases")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<BasePepDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<BasePepDto>> Bases(CancellationToken ct) =>
        await service.ListarBasesAsync(ct);

    /// <summary>Dispara uma importação (background). Devolve o id da execução criada.</summary>
    [HttpPost("importar")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Importar([FromBody] IniciarImportacaoRequest request, CancellationToken ct)
    {
        var execucaoId = await service.IniciarAsync(request, ct);
        return AcceptedAtAction(nameof(Status), new { }, new { execucaoId });
    }

    /// <summary>
    /// Para a importação em andamento. Com <c>pausarHoras</c>, também pausa o motor — sem
    /// isso o scheduler religa sozinho no próximo intervalo (parar não seria backout).
    /// </summary>
    [HttpPost("cancelar")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar([FromQuery] int? pausarHoras, CancellationToken ct)
    {
        await service.CancelarAsync(pausarHoras, ct);
        return Accepted();
    }

    /// <summary>Pausa (horas &gt; 0) ou retoma (horas ausente/0) o motor de uma base.</summary>
    [HttpPost("agenda/pausar")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType<AgendaPepDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AgendaPepDto> PausarMotor([FromBody] PausarMotorRequest request, CancellationToken ct) =>
        await service.PausarMotorAsync(request.FonteId, request.Horas, ct);

    /// <summary>Agendas do sincronismo contínuo (uma por base), com estado de backoff. ADR-0024.</summary>
    [HttpGet("agenda")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AgendaPepDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AgendaPepDto>> Agendas(CancellationToken ct) =>
        await service.ListarAgendasAsync(ct);

    /// <summary>Cria/atualiza a agenda do sincronismo contínuo de uma base.</summary>
    [HttpPut("agenda")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType<AgendaPepDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<AgendaPepDto> SalvarAgenda([FromBody] SalvarAgendaPepRequest request, CancellationToken ct) =>
        await service.SalvarAgendaAsync(request, ct);

    /// <summary>
    /// Diagnóstico origem×hub de uma base (ADR-0024): marcas d'água, pendências na origem e
    /// contagens do hub. Abre conexão Oracle pontual — pode levar alguns segundos.
    /// </summary>
    [HttpGet("diagnostico")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<DiagnosticoPepDto>(StatusCodes.Status200OK)]
    public async Task<DiagnosticoPepDto> Diagnostico([FromQuery] Guid fonteId, CancellationToken ct) =>
        await service.ObterDiagnosticoAsync(fonteId, ct);

    /// <summary>Status do run vivo (se houver) ou da última execução.</summary>
    [HttpGet("status")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<StatusImportacaoDto>(StatusCodes.Status200OK)]
    public async Task<StatusImportacaoDto> Status(CancellationToken ct) =>
        await service.ObterStatusAsync(ct);

    /// <summary>Histórico de execuções (mais recentes primeiro), opcionalmente por base.</summary>
    [HttpGet("execucoes")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ExecucaoImportacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ExecucaoImportacaoDto>> Execucoes([FromQuery] Guid? fonteId, CancellationToken ct) =>
        await service.ListarExecucoesAsync(fonteId, ct);

    /// <summary>
    /// Falhas duráveis de importação (mais recentes primeiro). Filtra por execução/base e, com
    /// <paramref name="somentePendentes"/>, só as ainda não resolvidas — insumo do reimport por cd.
    /// </summary>
    [HttpGet("falhas")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<FalhaImportacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<FalhaImportacaoDto>> Falhas(
        [FromQuery] Guid? execucaoId, [FromQuery] Guid? fonteId, [FromQuery] bool somentePendentes,
        CancellationToken ct) =>
        await service.ListarFalhasAsync(execucaoId, fonteId, somentePendentes, ct);

    /// <summary>
    /// Relatório de divergências de identidade origem×hub (mesmo CPF, nascimento diferente) —
    /// com o veredicto da consulta oficial de CPF sobre qual valor é o correto.
    /// </summary>
    [HttpGet("divergencias")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<DivergenciaIdentidadeDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<DivergenciaIdentidadeDto>> Divergencias(
        [FromQuery] Guid? fonteId, [FromQuery] StatusDivergenciaIdentidade? status, CancellationToken ct) =>
        await service.ListarDivergenciasAsync(fonteId, status, ct);

    /// <summary>Contadores do relatório de divergências (inclui quantos CPFs estão congelados).</summary>
    [HttpGet("divergencias/resumo")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Consulta)]
    [ProducesResponseType<ResumoDivergenciasDto>(StatusCodes.Status200OK)]
    public async Task<ResumoDivergenciasDto> ResumoDivergencias(
        [FromQuery] Guid? fonteId, CancellationToken ct) =>
        await service.ResumoDivergenciasAsync(fonteId, ct);

    /// <summary>
    /// Arbitra agora as divergências pendentes (consulta paga: respeita o teto por rodada).
    /// A arbitragem também roda sozinha ao fim de cada sincronização.
    /// </summary>
    [HttpPost("divergencias/verificar")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType<ResultadoVerificacaoDivergencias>(StatusCodes.Status200OK)]
    public async Task<ResultadoVerificacaoDivergencias> VerificarDivergencias(
        [FromBody] VerificarDivergenciasRequest request, CancellationToken ct) =>
        await service.VerificarDivergenciasAsync(request.FonteId, request.Max, ct);

    /// <summary>
    /// Reprocessa da origem os pacientes das divergências arbitradas como "origem correta".
    /// Sem isto o hub só se corrige quando o paciente voltar a ter atendimento.
    /// </summary>
    [HttpPost("divergencias/reprocessar")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReprocessarDivergencias(
        [FromBody] ReprocessarDivergenciasRequest request, CancellationToken ct)
    {
        var id = await service.ReprocessarDivergenciasResolvidasAsync(request.FonteId, request.Ids, ct);
        return Accepted(new { execucaoId = id });
    }

    /// <summary>Marca a divergência como falso positivo: descongela o campo para a origem.</summary>
    [HttpPost("divergencias/{id:guid}/ignorar")]
    [RequerPermissao(ModuloPermissao.SincronizacaoPep, AcoesPermissao.Edicao)]
    [ProducesResponseType<DivergenciaIdentidadeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<DivergenciaIdentidadeDto> IgnorarDivergencia(
        Guid id, [FromBody] IgnorarDivergenciaRequest request, CancellationToken ct) =>
        await service.IgnorarDivergenciaAsync(id, request.Motivo, ct);
}
