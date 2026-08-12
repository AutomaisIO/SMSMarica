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

    /// <summary>
    /// PUT /fhir/Encounter/{id} — substitui um Encounter. Se enviado o header
    /// <c>If-Match: W/"&lt;versão&gt;"</c>, aplica concorrência otimista (409 em versão obsoleta).
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(string id, CancellationToken ct)
    {
        var e = await LerCorpoAsync(ct);
        var versaoEsperada = ParseIfMatch(Request.Headers.IfMatch.ToString());
        var atualizado = await service.AtualizarAsync(ParseId(id), e, versaoEsperada, ct);
        if (atualizado.Meta?.VersionId is { } v)
            Response.Headers.ETag = $"W/\"{v}\"";
        return FhirResponse.Recurso(atualizado);
    }

    /// <summary>PUT /fhir/Encounter?identifier=system|value — conditional update (upsert idempotente).</summary>
    [HttpPut]
    public async Task<IActionResult> AtualizarCondicional([FromQuery] string? identifier, CancellationToken ct)
    {
        var (system, value) = FhirIdentifier.ParseParam(identifier);
        var e = await LerCorpoAsync(ct);
        return FhirResponse.Recurso(await service.UpsertPorIdentifierAsync(system, value, e, ct));
    }

    /// <summary>Extrai a versão int de um header If-Match no formato <c>W/"5"</c> (ou <c>"5"</c>/<c>5</c>). Null se ausente/inválido.</summary>
    private static int? ParseIfMatch(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) || ifMatch == "*") return null;
        var digitos = new string([.. ifMatch.Where(char.IsDigit)]);
        return int.TryParse(digitos, out var v) ? v : null;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Excluir(string id, CancellationToken ct)
    {
        await service.ExcluirAsync(ParseId(id), ct);
        return NoContent();
    }

    /// <summary>
    /// GET /fhir/Encounter?patient={id}&amp;status=finished&amp;identifier=system|value —
    /// atendimentos do paciente (timeline) ou lookup pontual por identifier de negócio.
    ///
    /// <para><c>fim-de</c>/<c>fim-ate</c> varrem por FIM de atendimento, em janela semiaberta
    /// <c>[de, ate)</c> — é como se descobre quem teve alta num intervalo. Não é o <c>date</c>
    /// do R4, que casa por sobreposição do período inteiro.</para>
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? patient,
        [FromQuery] string? status,
        [FromQuery] string? identifier,
        [FromQuery(Name = "fim-de")] DateTimeOffset? fimDe,
        [FromQuery(Name = "fim-ate")] DateTimeOffset? fimAte,
        CancellationToken ct)
    {
        string? system = null, value = null;
        if (!string.IsNullOrWhiteSpace(identifier))
            (system, value) = FhirIdentifier.ParseParam(identifier);
        var bundle = await service.BuscarAsync(
            new EncounterBusca(FhirRef.ParseId(patient), status, system, value, fimDe, fimAte), ct);
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
