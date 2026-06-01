using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Encounters;
using Automais.Fhir.Core.Fhir;

namespace Automais.Fhir.Api.Controllers;

/// <summary>Endpoint REST FHIR do recurso <c>Encounter</c> (atendimento).</summary>
[ApiController]
[Route("fhir/Encounter")]
public sealed class EncounterController(IEncounterService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var e = await LerCorpoAsync(ct);
        var criado = await service.CriarAsync(e, ct);
        Response.Headers.Location = $"/fhir/Encounter/{criado.Id}";
        return FhirResponse.Recurso(criado, StatusCodes.Status201Created);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Ler(string id, CancellationToken ct) =>
        FhirResponse.Recurso(await service.LerAsync(ParseId(id), ct));

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(string id, CancellationToken ct)
    {
        var e = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.AtualizarAsync(ParseId(id), e, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Excluir(string id, CancellationToken ct)
    {
        await service.ExcluirAsync(ParseId(id), ct);
        return NoContent();
    }

    /// <summary>GET /fhir/Encounter?patient={id}&amp;status=finished — atendimentos do paciente (timeline).</summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? patient,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var bundle = await service.BuscarAsync(new EncounterBusca(FhirRef.ParseId(patient), status), ct);
        return FhirResponse.Recurso(bundle);
    }

    private static Guid ParseId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : throw new RecursoNaoEncontradoException("Encounter", id);

    private async Task<Encounter> LerCorpoAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            throw new RecursoInvalidoException("Corpo da requisição vazio; envie um recurso Encounter.");

        return FhirJson.Parse<Encounter>(json);
    }
}
