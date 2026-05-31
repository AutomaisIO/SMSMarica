using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Pacientes.Fhir;

/// <summary>Resumo do paciente resolvido do hub FHIR (para embutir em DTOs/PDF/worklist).</summary>
public sealed record PacienteResumo(
    Guid Id,
    string Nome,
    string? Cpf,
    string? Cns,
    DateOnly? DataNascimento,
    Sexo Sexo);

/// <summary>
/// Resolve dados de paciente do hub FHIR por id. Os dependentes (Laudo,
/// Tratamento, SolicitacaoExame, Translado) guardam só o PacienteId; este
/// resolver busca nome/CPF/CNS quando precisam exibir.
/// </summary>
public interface IPacienteResolver
{
    Task<PacienteResumo?> ResolverAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, PacienteResumo>> ResolverManyAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}

public sealed class PacienteResolver(IPacienteFhirClient fhir) : IPacienteResolver
{
    public async Task<PacienteResumo?> ResolverAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) return null;
        var patient = await fhir.ObterAsync(id, ct);
        if (patient is null) return null;

        var dto = PacienteFhirMapper.ParaDto(patient);
        return new PacienteResumo(dto.Id, dto.NomeCompleto, dto.Cpf, dto.Cns, dto.DataNascimento, dto.Sexo);
    }

    public async Task<IReadOnlyDictionary<Guid, PacienteResumo>> ResolverManyAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var distintos = ids.Where(i => i != Guid.Empty).Distinct().ToArray();
        if (distintos.Length == 0) return new Dictionary<Guid, PacienteResumo>();

        var resumos = await Task.WhenAll(distintos.Select(id => ResolverAsync(id, ct)));
        return resumos.Where(r => r is not null).ToDictionary(r => r!.Id, r => r!);
    }
}
