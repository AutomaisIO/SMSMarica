using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Rastreamento.Ocorrencias;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Central acompanha as ocorrências reportadas pelos tablets dos veículos (botão REPORT): ver
/// (Rastreamento/Consulta), atender e encerrar (Rastreamento/Edicao). O tablet registra em
/// <c>POST rastreamento/dispositivos/ocorrencias</c> (DispositivosVeiculoController).
/// </summary>
[ApiController]
[Route("rastreamento/ocorrencias")]
public sealed class OcorrenciasVeiculoController(IOcorrenciaVeiculoService service) : ControllerBase
{
    /// <summary>Em aberto (aberta ou em atendimento); com <c>encerradas=true</c>, as encerradas dos
    /// últimos 30 dias.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Consulta)]
    public Task<IReadOnlyList<OcorrenciaVeiculoDto>> Listar([FromQuery] bool encerradas, CancellationToken ct) =>
        service.ListarAsync(encerradas, ct);

    [HttpPost("{id:guid}/atender")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Edicao)]
    public Task<OcorrenciaVeiculoDto> Atender(Guid id, CancellationToken ct) =>
        service.AtenderAsync(id, ct);

    [HttpPost("{id:guid}/encerrar")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Edicao)]
    public Task<OcorrenciaVeiculoDto> Encerrar(Guid id, [FromBody] EncerrarOcorrenciaRequest request, CancellationToken ct) =>
        service.EncerrarAsync(id, request, ct);
}
