using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Rastreamento;
using SMSMarica.Core.Rastreamento.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("rastreamento")]
public sealed class RastreamentoController(IRastreamentoService service) : ControllerBase
{
    private readonly IRastreamentoService _service = service;

    // --- Pontos GPS (append-only) ---

    /// <summary>Registra um ponto GPS capturado por um motorista.</summary>
    [HttpPost("pontos")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarPonto(
        [FromBody] RegistrarPontoGpsRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.RegistrarPontoAsync(request, cancellationToken);
        return Created($"/rastreamento/pontos/{id}", id);
    }

    /// <summary>Lista pontos GPS de um motorista. <c>desde</c> e <c>ate</c> são opcionais.</summary>
    [HttpGet("pontos/motorista/{motoristaId:guid}")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<PontoGpsDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PontoGpsDto>> ListarPontosPorMotorista(
        Guid motoristaId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? ate,
        CancellationToken cancellationToken) =>
        await _service.ListarPontosPorMotoristaAsync(motoristaId, desde, ate, cancellationToken);

    // --- Geofences (CRUD) ---

    [HttpGet("geofences")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<GeofenceDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<GeofenceDto>> ListarGeofences(CancellationToken cancellationToken) =>
        await _service.ListarGeofencesAsync(cancellationToken);

    [HttpGet("geofences/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<GeofenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<GeofenceDto> ObterGeofencePorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterGeofencePorIdAsync(id, cancellationToken);

    [HttpPost("geofences")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CadastrarGeofence(
        [FromBody] CadastrarGeofenceRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarGeofenceAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterGeofencePorId), new { id }, id);
    }

    [HttpPut("geofences/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarGeofence(
        Guid id,
        [FromBody] AtualizarGeofenceRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarGeofenceAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("geofences/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletarGeofence(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeletarGeofenceAsync(id, cancellationToken);
        return NoContent();
    }

    // --- Eventos de chegada (read-only) ---

    [HttpGet("eventos/rota/{rotaId:guid}")]
    [RequerPermissao(ModuloPermissao.Rastreamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<EventoChegadaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<EventoChegadaDto>> ListarEventosPorRota(
        Guid rotaId,
        CancellationToken cancellationToken) =>
        await _service.ListarEventosPorRotaAsync(rotaId, cancellationToken);
}
