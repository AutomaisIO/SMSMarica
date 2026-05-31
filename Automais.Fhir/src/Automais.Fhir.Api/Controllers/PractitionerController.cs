using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Core.Practitioners;

namespace Automais.Fhir.Api.Controllers;

/// <summary>Endpoint REST FHIR do recurso <c>Practitioner</c>.</summary>
[ApiController]
[Route("fhir/Practitioner")]
public sealed class PractitionerController(IPractitionerService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var p = await LerCorpoAsync(ct);
        var criado = await service.CriarAsync(p, ct);
        Response.Headers.Location = $"/fhir/Practitioner/{criado.Id}";
        return FhirResponse.Recurso(criado, StatusCodes.Status201Created);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Ler(string id, CancellationToken ct) =>
        FhirResponse.Recurso(await service.LerAsync(ParseId(id), ct));

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(string id, CancellationToken ct)
    {
        var p = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.AtualizarAsync(ParseId(id), p, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Excluir(string id, CancellationToken ct)
    {
        await service.ExcluirAsync(ParseId(id), ct);
        return NoContent();
    }

    /// <summary>GET /fhir/Practitioner?identifier=system|valor&amp;name=... — busca.</summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? identifier,
        [FromQuery] string? name,
        CancellationToken ct)
    {
        var (cpf, crm) = SepararIdentifier(identifier);
        var bundle = await service.BuscarAsync(new PractitionerBusca(cpf, crm, name), ct);
        return FhirResponse.Recurso(bundle);
    }

    private static Guid ParseId(string id) =>
        Guid.TryParse(id, out var guid)
            ? guid
            : throw new RecursoNaoEncontradoException("Practitioner", id);

    private async Task<Practitioner> LerCorpoAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            throw new RecursoInvalidoException("Corpo da requisição vazio; envie um recurso Practitioner.");

        return FhirJson.Parse<Practitioner>(json);
    }

    private static (string? Cpf, string? Crm) SepararIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return (null, null);

        var partes = identifier.Split('|', 2);
        var (system, valor) = partes.Length == 2 ? (partes[0], partes[1]) : (string.Empty, partes[0]);

        if (system == FhirSystems.Cpf) return (valor, null);
        if (system.StartsWith(FhirSystems.CrmPrefix)) return (null, valor);
        return (valor, null);
    }
}
