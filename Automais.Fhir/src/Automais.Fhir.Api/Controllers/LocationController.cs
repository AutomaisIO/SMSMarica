using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Core.Locations;

namespace Automais.Fhir.Api.Controllers;

/// <summary>Endpoint REST FHIR do recurso <c>Location</c> (setor/quarto/leito — ADR-0025).</summary>
[ApiController]
[Route("fhir/Location")]
public sealed class LocationController(ILocationService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var l = await LerCorpoAsync(ct);
        var criado = await service.CriarAsync(l, ct);
        Response.Headers.Location = $"/fhir/Location/{criado.Id}";
        return FhirResponse.Recurso(criado, StatusCodes.Status201Created);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Ler(string id, CancellationToken ct) =>
        FhirResponse.Recurso(await service.LerAsync(ParseId(id), ct));

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(string id, CancellationToken ct)
    {
        var l = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.AtualizarAsync(ParseId(id), l, ct));
    }

    /// <summary>PUT /fhir/Location?identifier=system|value — conditional update (upsert idempotente).</summary>
    [HttpPut]
    public async Task<IActionResult> AtualizarCondicional([FromQuery] string? identifier, CancellationToken ct)
    {
        var (system, value) = FhirIdentifier.ParseParam(identifier);
        var l = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.UpsertPorIdentifierAsync(system, value, l, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Excluir(string id, CancellationToken ct)
    {
        await service.ExcluirAsync(ParseId(id), ct);
        return NoContent();
    }

    /// <summary>GET /fhir/Location?identifier=system|value&amp;partof={id}&amp;physicaltype=bd — busca do cadastro físico.</summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? identifier,
        [FromQuery] string? partof,
        [FromQuery] string? physicaltype,
        CancellationToken ct)
    {
        string? system = null, value = null;
        if (!string.IsNullOrWhiteSpace(identifier))
            (system, value) = FhirIdentifier.ParseParam(identifier);
        var bundle = await service.BuscarAsync(new LocationBusca(system, value, FhirRef.ParseId(partof), physicaltype), ct);
        return FhirResponse.Recurso(bundle);
    }

    private static Guid ParseId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : throw new RecursoNaoEncontradoException("Location", id);

    private async Task<Location> LerCorpoAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            throw new RecursoInvalidoException("Corpo da requisição vazio; envie um recurso Location.");

        return FhirJson.Parse<Location>(json);
    }
}
