using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Core.Organizations;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Fhir.Api.Controllers;

/// <summary>
/// Endpoint REST FHIR do recurso <c>Organization</c> — a unidade de saúde (ADR-0039).
/// É o eixo durável do dado clínico: sobrevive à troca de PEP.
/// </summary>
[ApiController]
[Route("fhir/Organization")]
public sealed class OrganizationController(IOrganizationService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var o = await LerCorpoAsync(ct);
        var criado = await service.CriarAsync(o, ct);
        Response.Headers.Location = $"/fhir/Organization/{criado.Id}";
        return FhirResponse.Recurso(criado, StatusCodes.Status201Created);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Ler(string id, CancellationToken ct) =>
        FhirResponse.Recurso(await service.LerAsync(ParseId(id), ct));

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(string id, CancellationToken ct)
    {
        var o = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.AtualizarAsync(ParseId(id), o, ct));
    }

    /// <summary>
    /// PUT /fhir/Organization?identifier=system|value — conditional update. É por aqui que
    /// cada conector declara a sua unidade; identifiers de bases diferentes acumulam no MESMO
    /// recurso, e o CNES serve de ponte quando o código interno ainda não é conhecido.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> AtualizarCondicional([FromQuery] string? identifier, CancellationToken ct)
    {
        var (system, value) = FhirIdentifier.ParseParam(identifier);
        var o = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.UpsertPorIdentifierAsync(system, value, o, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Excluir(string id, CancellationToken ct)
    {
        await service.ExcluirAsync(ParseId(id), ct);
        return NoContent();
    }

    /// <summary>GET /fhir/Organization?identifier=system|value&amp;cnes=2266733&amp;partof={id}</summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? identifier,
        [FromQuery] string? cnes,
        [FromQuery] string? partof,
        CancellationToken ct)
    {
        string? system = null, value = null;
        if (!string.IsNullOrWhiteSpace(identifier))
            (system, value) = FhirIdentifier.ParseParam(identifier);
        var bundle = await service.BuscarAsync(
            new OrganizationBusca(system, value, cnes, FhirRef.ParseId(partof)), ct);
        return FhirResponse.Recurso(bundle);
    }

    private static Guid ParseId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : throw new RecursoNaoEncontradoException("Organization", id);

    private async Task<Organization> LerCorpoAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            throw new RecursoInvalidoException("Corpo da requisição vazio; envie um recurso Organization.");

        return FhirJson.Parse<Organization>(json);
    }
}
