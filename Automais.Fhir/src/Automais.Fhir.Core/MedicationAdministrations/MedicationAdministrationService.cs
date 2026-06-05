using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Core.MedicationAdministrations;

public sealed class MedicationAdministrationService(FhirDbContext db, TimeProvider clock) : IMedicationAdministrationService
{
    private const string TipoRecurso = "MedicationAdministration";
    private const int LimiteBusca = 500;

    public async Task<MedicationAdministration> CriarAsync(MedicationAdministration ma, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = ma.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(ma, id, versao: 1, agora, source);

        var row = new MedicationAdministrationRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, ma);
        row.Content = FhirJson.Serialize(ma);

        db.MedicationAdministrations.Add(row);
        await db.SaveChangesAsync(ct);
        return ma;
    }

    public async Task<MedicationAdministration> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.MedicationAdministrations.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<MedicationAdministration>(row.Content);
    }

    public async Task<MedicationAdministration> AtualizarAsync(Guid id, MedicationAdministration ma, CancellationToken ct = default)
    {
        var row = await db.MedicationAdministrations.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = ma.Meta?.Source ?? row.MetaSource;

        CarimbarMeta(ma, id, versao, agora, source);

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.MetaSource = source;
        ExtrairSearchParams(row, ma);
        row.Content = FhirJson.Serialize(ma);

        await db.SaveChangesAsync(ct);
        return ma;
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.MedicationAdministrations.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct);
        if (row is null)
            return;

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(MedicationAdministrationBusca filtro, CancellationToken ct = default)
    {
        var query = db.MedicationAdministrations.AsNoTracking().Where(m => !m.IsDeleted);

        if (filtro.PatientId is { } pid)
            query = query.Where(m => m.PatientId == pid);
        if (filtro.EncounterId is { } eid)
            query = query.Where(m => m.EncounterId == eid);

        var rows = await query
            .OrderByDescending(m => m.Effective)
            .Take(LimiteBusca)
            .ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<MedicationAdministration>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    private static void CarimbarMeta(MedicationAdministration m, Guid id, int versao, DateTimeOffset agora, string source)
    {
        m.Id = id.ToString();
        m.Meta ??= new Meta();
        m.Meta.VersionId = versao.ToString();
        m.Meta.LastUpdated = agora;
        m.Meta.Source = source;
    }

    private static void ExtrairSearchParams(MedicationAdministrationRow row, MedicationAdministration m)
    {
        row.PatientId = FhirRef.ParseId(m.Subject?.Reference);
        row.EncounterId = FhirRef.ParseId(m.Context?.Reference);
        row.Medicamento = (m.Medication as CodeableConcept)?.Text;
        row.Effective = m.Effective switch
        {
            FhirDateTime fdt => ParseData(fdt.Value),
            Period p => ParseData(p.Start),
            _ => null,
        };
    }

    private static DateTimeOffset? ParseData(string? d) =>
        DateTimeOffset.TryParse(d, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var r) ? r.ToUniversalTime() : null;
}
