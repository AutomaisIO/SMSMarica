using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.DocumentReferences;
using Automais.Fhir.Core.Fhir;

namespace Automais.Fhir.Api.Controllers;

/// <summary>Endpoint REST FHIR do recurso <c>DocumentReference</c> (documento clínico).</summary>
[ApiController]
[Route("fhir/DocumentReference")]
public sealed class DocumentReferenceController(IDocumentReferenceService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var d = await LerCorpoAsync(ct);
        var criado = await service.CriarAsync(d, ct);
        Response.Headers.Location = $"/fhir/DocumentReference/{criado.Id}";
        return FhirResponse.Recurso(criado, StatusCodes.Status201Created);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Ler(string id, CancellationToken ct) =>
        FhirResponse.Recurso(await service.LerAsync(ParseId(id), ct));

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(string id, CancellationToken ct)
    {
        var d = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.AtualizarAsync(ParseId(id), d, ct));
    }

    /// <summary>PUT /fhir/DocumentReference?identifier=system|value — conditional update (upsert idempotente).</summary>
    [HttpPut]
    public async Task<IActionResult> AtualizarCondicional([FromQuery] string? identifier, CancellationToken ct)
    {
        var (system, value) = FhirIdentifier.ParseParam(identifier);
        var recurso = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.UpsertPorIdentifierAsync(system, value, recurso, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Excluir(string id, CancellationToken ct)
    {
        await service.ExcluirAsync(ParseId(id), ct);
        return NoContent();
    }

    /// <summary>GET /fhir/DocumentReference?patient={id}&amp;encounter={id} — documentos do paciente/atendimento.</summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? patient,
        [FromQuery] string? encounter,
        [FromQuery] string? identifier,
        CancellationToken ct)
    {
        string? idSystem = null, idValue = null;
        if (!string.IsNullOrWhiteSpace(identifier))
            (idSystem, idValue) = FhirIdentifier.ParseParam(identifier);
        var bundle = await service.BuscarAsync(new DocumentReferenceBusca(FhirRef.ParseId(patient), FhirRef.ParseId(encounter), IdentifierSystem: idSystem, IdentifierValue: idValue), ct);
        return FhirResponse.Recurso(bundle);
    }

    private static Guid ParseId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : throw new RecursoNaoEncontradoException("DocumentReference", id);

    private async Task<DocumentReference> LerCorpoAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            throw new RecursoInvalidoException("Corpo da requisição vazio; envie um recurso DocumentReference.");

        return FhirJson.Parse<DocumentReference>(json);
    }
}
