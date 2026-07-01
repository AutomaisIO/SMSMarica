using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Busca ids de pacientes no hub FHIR por um termo livre (nome e/ou CPF/CNS).
    /// Para a busca da tela de solicitações. Nunca lança: hub indisponível → conjunto
    /// vazio (o chamador cai no match local por accession/código).
    /// </summary>
    Task<IReadOnlySet<Guid>> BuscarIdsPorTermoAsync(string termo, CancellationToken ct = default);
}

public sealed class PacienteResolver(IPacienteFhirClient fhir, ILogger<PacienteResolver> logger) : IPacienteResolver
{
    public async Task<PacienteResumo?> ResolverAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) return null;
        try
        {
            var patient = await fhir.ObterAsync(id, ct);
            if (patient is null) return null;

            var dto = PacienteFhirMapper.ParaDto(patient);
            return new PacienteResumo(dto.Id, dto.NomeCompleto, dto.Cpf, dto.Cns, dto.DataNascimento, dto.Sexo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Hub FHIR indisponível/erro NÃO pode derrubar a listagem que só quer
            // exibir o nome: degrada para "sem nome" (o chamador mostra fallback) em
            // vez de propagar e virar 500. A cancelação legítima segue propagando.
            logger.LogWarning(ex, "Falha ao resolver paciente {PacienteId} no hub FHIR — seguindo sem nome.", id);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, PacienteResumo>> ResolverManyAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var distintos = ids.Where(i => i != Guid.Empty).Distinct().ToArray();
        if (distintos.Length == 0) return new Dictionary<Guid, PacienteResumo>();

        var resumos = await Task.WhenAll(distintos.Select(id => ResolverAsync(id, ct)));

        // Indexador (não ToDictionary): se dois ids resolverem para o mesmo paciente
        // canônico (.Id), a chave duplicada sobrescreve em vez de lançar
        // ArgumentException — que viraria 500 na listagem.
        var mapa = new Dictionary<Guid, PacienteResumo>(distintos.Length);
        foreach (var r in resumos)
            if (r is not null) mapa[r.Id] = r;
        return mapa;
    }

    public async Task<IReadOnlySet<Guid>> BuscarIdsPorTermoAsync(string termo, CancellationToken ct = default)
    {
        var ids = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(termo)) return ids;

        var t = termo.Trim();
        var digitos = new string([.. t.Where(char.IsDigit)]);
        try
        {
            // Por nome (o hub trata acento/casing) — só quando o termo tem letra.
            if (t.Any(char.IsLetter))
                ColetarIds(await fhir.BuscarAsync(name: t, ct: ct), ids);

            // Por identifier (CPF=11 / CNS=15 dígitos).
            if (digitos.Length >= 11)
                ColetarIds(await fhir.BuscarAsync(identifier: digitos, ct: ct), ids);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Busca de pacientes por termo no hub FHIR falhou — seguindo sem ids.");
        }
        return ids;
    }

    private static void ColetarIds(Bundle? bundle, HashSet<Guid> ids)
    {
        if (bundle?.Entry is null) return;
        foreach (var e in bundle.Entry)
            if (e.Resource is Patient p && Guid.TryParse(p.Id, out var g))
                ids.Add(g);
    }
}
