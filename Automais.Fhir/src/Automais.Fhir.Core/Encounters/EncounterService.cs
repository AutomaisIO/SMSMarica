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

    public async Task<Encounter> AtualizarAsync(Guid id, Encounter encounter, int? versaoEsperada = null, CancellationToken ct = default)
    {
        var row = await db.Encounters.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        // Concorrência otimista (If-Match): rejeita escrita sobre versão obsoleta.
        if (versaoEsperada is int esperada && esperada != row.VersionId)
            throw new ConflitoVersaoException(TipoRecurso, id.ToString(), esperada, row.VersionId);

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

    public async Task<Encounter> UpsertPorIdentifierAsync(string system, string value, Encounter encounter, CancellationToken ct = default)
    {
        FhirIdentifier.Garantir(encounter.Identifier ??= [], system, value);

        var existente = await db.Encounters.AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdentifierSystem == system && e.IdentifierValue == value && !e.IsDeleted, ct);
        if (existente is not null)
            return await AtualizarAsync(existente.Id, encounter, null, ct);

        try
        {
            return await CriarAsync(encounter, ct);
        }
        catch (DbUpdateException) // corrida: outro create do mesmo identifier venceu (índice único)
        {
            db.ChangeTracker.Clear();
            existente = await db.Encounters.AsNoTracking()
                .FirstOrDefaultAsync(e => e.IdentifierSystem == system && e.IdentifierValue == value && !e.IsDeleted, ct)
                ?? throw new RecursoNaoEncontradoException(TipoRecurso, $"{system}|{value}");
            return await AtualizarAsync(existente.Id, encounter, null, ct);
        }
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
        if (!string.IsNullOrWhiteSpace(filtro.IdentifierSystem) && !string.IsNullOrWhiteSpace(filtro.IdentifierValue))
            query = query.Where(e => e.IdentifierSystem == filtro.IdentifierSystem && e.IdentifierValue == filtro.IdentifierValue);

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
        row.PeriodEnd = ParseInstant(e.Period?.End);
        (row.IdentifierSystem, row.IdentifierValue) = FhirIdentifier.Primeiro(e.Identifier);
    }

    private static DateTimeOffset? ParseInstant(string? fhirDateTime) =>
        DateTimeOffset.TryParse(fhirDateTime, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var dt)
            ? dt.ToUniversalTime() // Npgsql exige offset 0 em timestamptz
            : null;
}
