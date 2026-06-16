using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Core.Patients;

public sealed class PatientService(FhirDbContext db, TimeProvider clock) : IPatientService
{
    private const string TipoRecurso = "Patient";
    private const int LimiteBusca = 50;

    public async Task<Patient> CriarAsync(Patient patient, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = patient.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(patient, id, versao: 1, agora, source);

        var row = new PatientRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, patient);
        row.Content = FhirJson.Serialize(patient);

        db.Patients.Add(row);
        await db.SaveChangesAsync(ct);
        return patient;
    }

    public async Task<Patient> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Patients.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<Patient>(row.Content);
    }

    public async Task<Patient> AtualizarAsync(Guid id, Patient patient, CancellationToken ct = default)
    {
        var row = await db.Patients.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = patient.Meta?.Source ?? row.MetaSource;

        CarimbarMeta(patient, id, versao, agora, source);

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.MetaSource = source;
        ExtrairSearchParams(row, patient);
        row.Content = FhirJson.Serialize(patient);

        await db.SaveChangesAsync(ct);
        return patient;
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Patients.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
        if (row is null)
            return; // DELETE FHIR é idempotente.

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(PatientBusca filtro, CancellationToken ct = default)
    {
        var query = db.Patients.AsNoTracking().Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filtro.Cpf))
            query = query.Where(p => p.Cpf == filtro.Cpf);
        if (!string.IsNullOrWhiteSpace(filtro.Cns))
            query = query.Where(p => p.Cns == filtro.Cns);
        if (!string.IsNullOrWhiteSpace(filtro.Nome))
            query = query.Where(p => p.Nome != null && EF.Functions.ILike(p.Nome, $"%{filtro.Nome}%"));
        if (!string.IsNullOrWhiteSpace(filtro.Telefone))
        {
            var fone = Digitos(filtro.Telefone);
            if (fone.Length > 0)
                query = query.Where(p => p.Telefone != null && p.Telefone.Contains(fone));
        }

        // Sem filtro: últimos incluídos primeiro (LastUpdated desc). Com filtro: por nome.
        var semFiltro = string.IsNullOrWhiteSpace(filtro.Cpf) && string.IsNullOrWhiteSpace(filtro.Cns)
                        && string.IsNullOrWhiteSpace(filtro.Nome) && string.IsNullOrWhiteSpace(filtro.Telefone);
        var ordenada = semFiltro
            ? query.OrderByDescending(p => p.LastUpdated)
            : query.OrderBy(p => p.Nome);
        var rows = await ordenada.Take(LimiteBusca).ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<Patient>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    private static void CarimbarMeta(Patient patient, Guid id, int versao, DateTimeOffset agora, string source)
    {
        patient.Id = id.ToString();
        patient.Meta ??= new Meta();
        patient.Meta.VersionId = versao.ToString();
        patient.Meta.LastUpdated = agora;
        patient.Meta.Source = source;
    }

    private static void ExtrairSearchParams(PatientRow row, Patient patient)
    {
        row.Cpf = ValorIdentifier(patient, FhirSystems.Cpf);
        row.Cns = ValorIdentifier(patient, FhirSystems.Cns);
        row.Nome = patient.Name.FirstOrDefault(n => n.Use == HumanName.NameUse.Official)?.Text
                   ?? patient.Name.FirstOrDefault()?.Text;
        row.Telefone = ExtrairTelefones(patient);
        row.Nascimento = ParseDataNascimento(patient.BirthDate);
    }

    private static string? ValorIdentifier(Patient patient, string system) =>
        patient.Identifier.FirstOrDefault(i => i.System == system)?.Value;

    /// <summary>Dígitos de todos os telefones (telecom[system=phone]), juntos por espaço.</summary>
    private static string? ExtrairTelefones(Patient patient)
    {
        if (patient.Telecom is null || patient.Telecom.Count == 0) return null;
        var fones = patient.Telecom
            .Where(t => t.System == ContactPoint.ContactPointSystem.Phone)
            .Select(t => Digitos(t.Value))
            .Where(d => d.Length > 0)
            .Distinct();
        var juntos = string.Join(' ', fones);
        return juntos.Length == 0 ? null : juntos;
    }

    private static string Digitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new string([.. valor.Where(char.IsDigit)]);

    private static DateOnly? ParseDataNascimento(string? birthDate) =>
        DateOnly.TryParseExact(birthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
}
