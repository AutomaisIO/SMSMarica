using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Core.Encounters;

public sealed class EncounterService(FhirDbContext db, TimeProvider clock) : IEncounterService
{
    private const string TipoRecurso = "Encounter";
    private const int LimiteBusca = 200;

    public async Task<Encounter> CriarAsync(Encounter encounter, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = encounter.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(encounter, id, versao: 1, agora, source);

        var row = new EncounterRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, encounter);
        row.Content = FhirJson.Serialize(encounter);

        db.Encounters.Add(row);
        await db.SaveChangesAsync(ct);
        return encounter;
    }

    public async Task<Encounter> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Encounters.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<Encounter>(row.Content);
    }

    public async Task<Encounter> AtualizarAsync(Guid id, Encounter encounter, CancellationToken ct = default)
    {
        var row = await db.Encounters.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = encounter.Meta?.Source ?? row.MetaSource;

        CarimbarMeta(encounter, id, versao, agora, source);

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.MetaSource = source;
        ExtrairSearchParams(row, encounter);
        row.Content = FhirJson.Serialize(encounter);

        await db.SaveChangesAsync(ct);
        return encounter;
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Encounters.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
        if (row is null)
            return;

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(EncounterBusca filtro, CancellationToken ct = default)
    {
        var query = db.Encounters.AsNoTracking().Where(e => !e.IsDeleted);

        if (filtro.PatientId is { } pid)
            query = query.Where(e => e.PatientId == pid);
        if (!string.IsNullOrWhiteSpace(filtro.Status))
            query = query.Where(e => e.Status == filtro.Status);

        // Timeline: mais recente primeiro.
        var rows = await query
            .OrderByDescending(e => e.PeriodStart)
            .Take(LimiteBusca)
            .ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<Encounter>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    private static void CarimbarMeta(Encounter e, Guid id, int versao, DateTimeOffset agora, string source)
    {
        e.Id = id.ToString();
        e.Meta ??= new Meta();
        e.Meta.VersionId = versao.ToString();
        e.Meta.LastUpdated = agora;
        e.Meta.Source = source;
    }

    private static void ExtrairSearchParams(EncounterRow row, Encounter e)
    {
        row.PatientId = FhirRef.ParseId(e.Subject?.Reference);
        row.Status = e.Status?.ToString().ToLowerInvariant();
        row.Classe = e.Class?.Code;
        row.PeriodStart = ParseInstant(e.Period?.Start);
    }

    private static DateTimeOffset? ParseInstant(string? fhirDateTime) =>
        DateTimeOffset.TryParse(fhirDateTime, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var dt) ? dt : null;
}
