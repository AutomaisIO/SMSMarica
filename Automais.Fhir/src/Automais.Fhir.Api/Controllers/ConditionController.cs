using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Conditions;
using Automais.Fhir.Core.Fhir;

namespace Automais.Fhir.Api.Controllers;

/// <summary>Endpoint REST FHIR do recurso <c>Condition</c> (diagnóstico/CID).</summary>
[ApiController]
[Route("fhir/Condition")]
public sealed class ConditionController(IConditionService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var c = await LerCorpoAsync(ct);
        var criado = await service.CriarAsync(c, ct);
        Response.Headers.Location = $"/fhir/Condition/{criado.Id}";
        return FhirResponse.Recurso(criado, StatusCodes.Status201Created);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Ler(string id, CancellationToken ct) =>
        FhirResponse.Recurso(await service.LerAsync(ParseId(id), ct));

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(string id, CancellationToken ct)
    {
        var c = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.AtualizarAsync(ParseId(id), c, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Excluir(string id, CancellationToken ct)
    {
        await service.ExcluirAsync(ParseId(id), ct);
        return NoContent();
    }

    /// <summary>GET /fhir/Condition?patient={id}&amp;encounter={id} — diagnósticos do paciente/atendimento.</summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? patient,
        [FromQuery] string? encounter,
        CancellationToken ct)
    {
        var bundle = await service.BuscarAsync(new ConditionBusca(FhirRef.ParseId(patient), FhirRef.ParseId(encounter)), ct);
        return FhirResponse.Recurso(bundle);
    }

    private static Guid ParseId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : throw new RecursoNaoEncontradoException("Condition", id);

    private async Task<Condition> LerCorpoAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            throw new RecursoInvalidoException("Corpo da requisição vazio; envie um recurso Condition.");

        return FhirJson.Parse<Condition>(json);
    }
}
