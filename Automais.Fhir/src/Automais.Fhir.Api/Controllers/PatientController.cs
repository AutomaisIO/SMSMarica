using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Automais.Fhir.Api.Infra;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Core.Patients;

namespace Automais.Fhir.Api.Controllers;

/// <summary>Endpoint REST FHIR do recurso <c>Patient</c>.</summary>
[ApiController]
[Route("fhir/Patient")]
public sealed class PatientController(IPatientService service) : ControllerBase
{
    /// <summary>POST /fhir/Patient — cria um novo Patient.</summary>
    [HttpPost]
    public async Task<IActionResult> Criar(CancellationToken ct)
    {
        var patient = await LerCorpoAsync(ct);
        var criado = await service.CriarAsync(patient, ct);
        Response.Headers.Location = $"/fhir/Patient/{criado.Id}";
        return FhirResponse.Recurso(criado, StatusCodes.Status201Created);
    }

    /// <summary>GET /fhir/Patient/{id} — lê um Patient pelo id lógico.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Ler(string id, CancellationToken ct)
    {
        var patient = await service.LerAsync(ParseId(id), ct);
        return FhirResponse.Recurso(patient);
    }

    /// <summary>PUT /fhir/Patient/{id} — substitui um Patient existente.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(string id, CancellationToken ct)
    {
        var patient = await LerCorpoAsync(ct);
        var atualizado = await service.AtualizarAsync(ParseId(id), patient, ct);
        return FhirResponse.Recurso(atualizado);
    }

    /// <summary>DELETE /fhir/Patient/{id} — exclusão lógica.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Excluir(string id, CancellationToken ct)
    {
        await service.ExcluirAsync(ParseId(id), ct);
        return NoContent();
    }

    /// <summary>GET /fhir/Patient?identifier=system|valor&amp;name=... — busca.</summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? identifier,
        [FromQuery] string? name,
        [FromQuery] string? telecom,
        CancellationToken ct)
    {
        var (cpf, cns) = SepararIdentifier(identifier);
        var bundle = await service.BuscarAsync(new PatientBusca(cpf, cns, name, telecom), ct);
        return FhirResponse.Recurso(bundle);
    }

    private static Guid ParseId(string id) =>
        Guid.TryParse(id, out var guid)
            ? guid
            : throw new RecursoNaoEncontradoException("Patient", id);

    private async Task<Patient> LerCorpoAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            throw new RecursoInvalidoException("Corpo da requisição vazio; envie um recurso Patient.");

        return FhirJson.Parse<Patient>(json);
    }

    private static (string? Cpf, string? Cns) SepararIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return (null, null);

        // Formato FHIR: system|value. Sem '|', trata como valor solto (tenta CPF).
        var partes = identifier.Split('|', 2);
        var (system, valor) = partes.Length == 2 ? (partes[0], partes[1]) : (string.Empty, partes[0]);

        return system switch
        {
            FhirSystems.Cpf => (valor, null),
            FhirSystems.Cns => (null, valor),
            _ => (valor, null),
        };
    }
}
