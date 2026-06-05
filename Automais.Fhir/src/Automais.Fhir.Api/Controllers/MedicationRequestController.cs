using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Core.MedicationRequests;

namespace Automais.Fhir.Api.Controllers;

/// <summary>Endpoint REST FHIR do recurso <c>MedicationRequest</c> (item de prescrição).</summary>
[ApiController]
[Route("fhir/MedicationRequest")]
public sealed class MedicationRequestController(IMedicationRequestService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var m = await LerCorpoAsync(ct);
        var criado = await service.CriarAsync(m, ct);
        Response.Headers.Location = $"/fhir/MedicationRequest/{criado.Id}";
        return FhirResponse.Recurso(criado, StatusCodes.Status201Created);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Ler(string id, CancellationToken ct) =>
        FhirResponse.Recurso(await service.LerAsync(ParseId(id), ct));

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(string id, CancellationToken ct)
    {
        var m = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.AtualizarAsync(ParseId(id), m, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Excluir(string id, CancellationToken ct)
    {
        await service.ExcluirAsync(ParseId(id), ct);
        return NoContent();
    }

    /// <summary>GET /fhir/MedicationRequest?patient={id}&amp;encounter={id} — prescrições do paciente/atendimento.</summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? patient,
        [FromQuery] string? encounter,
        CancellationToken ct)
    {
        var bundle = await service.BuscarAsync(new MedicationRequestBusca(FhirRef.ParseId(patient), FhirRef.ParseId(encounter)), ct);
        return FhirResponse.Recurso(bundle);
    }

    private static Guid ParseId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : throw new RecursoNaoEncontradoException("MedicationRequest", id);

    private async Task<MedicationRequest> LerCorpoAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            throw new RecursoInvalidoException("Corpo da requisição vazio; envie um recurso MedicationRequest.");

        return FhirJson.Parse<MedicationRequest>(json);
    }
}
