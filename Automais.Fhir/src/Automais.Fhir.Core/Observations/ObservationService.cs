using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Core.Observations;

public sealed class ObservationService(FhirDbContext db, TimeProvider clock) : IObservationService
{
    private const string TipoRecurso = "Observation";
    private const int LimiteBusca = 500;

    public async Task<Observation> CriarAsync(Observation o, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = o.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(o, id, versao: 1, agora, source);

        var row = new ObservationRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, o);
        row.Content = FhirJson.Serialize(o);

        db.Observations.Add(row);
        await db.SaveChangesAsync(ct);
        return o;
    }

    public async Task<Observation> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Observations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<Observation>(row.Content);
    }

    public async Task<Observation> AtualizarAsync(Guid id, Observation o, CancellationToken ct = default)
    {
        var row = await db.Observations.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = o.Meta?.Source ?? row.MetaSource;

        CarimbarMeta(o, id, versao, agora, source);

        // Colunas derivadas do content sempre: podem estar dessincronizadas por backfill parcial,
        // e era a reescrita que vinha consertando isso em silêncio. Se SÓ elas mudarem, o
        // SaveChanges abaixo persiste a correção sem inventar uma versão nova.
        row.MetaSource = source;
        ExtrairSearchParams(row, o);

        if (EscritaFhir.SemMudanca(o, row.Content))
        {
            // Devolve a versão VIGENTE, nunca a incrementada: um versionId que não existe no
            // banco faria o próximo If-Match do chamador dar 409 para sempre.
            CarimbarMeta(o, id, row.VersionId, row.LastUpdated, source);
            await db.SaveChangesAsync(ct);
            return o;
        }

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.Content = FhirJson.Serialize(o);

        await db.SaveChangesAsync(ct);
        return o;
    }

    public async Task<Observation> UpsertPorIdentifierAsync(string system, string value, Observation recurso, CancellationToken ct = default)
    {
        FhirIdentifier.Garantir(recurso.Identifier ??= [], system, value);

        var existente = await db.Observations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdentifierSystem == system && x.IdentifierValue == value && !x.IsDeleted, ct);
        if (existente is not null)
            return await AtualizarAsync(existente.Id, recurso, ct);

        try
        {
            return await CriarAsync(recurso, ct);
        }
        catch (DbUpdateException) // corrida: outro create do mesmo identifier venceu (índice único)
        {
            db.ChangeTracker.Clear();
            existente = await db.Observations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdentifierSystem == system && x.IdentifierValue == value && !x.IsDeleted, ct)
                ?? throw new RecursoNaoEncontradoException(TipoRecurso, $"{system}|{value}");
            return await AtualizarAsync(existente.Id, recurso, ct);
        }
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Observations.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);
        if (row is null)
            return;

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(ObservationBusca filtro, CancellationToken ct = default)
    {
        var query = db.Observations.AsNoTracking().Where(o => !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filtro.IdentifierSystem) && !string.IsNullOrWhiteSpace(filtro.IdentifierValue))
            query = query.Where(x => x.IdentifierSystem == filtro.IdentifierSystem && x.IdentifierValue == filtro.IdentifierValue);

        if (filtro.PatientId is { } pid)
            query = query.Where(o => o.PatientId == pid);
        if (filtro.EncounterId is { } eid)
            query = query.Where(o => o.EncounterId == eid);
        if (!string.IsNullOrWhiteSpace(filtro.Code))
            query = query.Where(o => o.Code == filtro.Code);

        var rows = await query
            .OrderByDescending(o => o.Effective)
            .Take(LimiteBusca)
            .ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<Observation>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    private static void CarimbarMeta(Observation o, Guid id, int versao, DateTimeOffset agora, string source)
    {
        o.Id = id.ToString();
        o.Meta ??= new Meta();
        o.Meta.VersionId = versao.ToString();
        o.Meta.LastUpdated = agora;
        o.Meta.Source = source;
    }

    private static void ExtrairSearchParams(ObservationRow row, Observation o)
    {
        (row.IdentifierSystem, row.IdentifierValue) = FhirIdentifier.Primeiro(o.Identifier);
        row.PatientId = FhirRef.ParseId(o.Subject?.Reference);
        row.EncounterId = FhirRef.ParseId(o.Encounter?.Reference);
        row.Code = o.Code?.Coding?.FirstOrDefault()?.Code;
        row.Effective = o.Effective switch
        {
            FhirDateTime fdt => ParseData(fdt.Value),
            Period p => ParseData(p.Start),
            Instant inst => inst.Value,
            _ => null,
        };
    }

    private static DateTimeOffset? ParseData(string? d) =>
        DateTimeOffset.TryParse(d, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var r) ? r.ToUniversalTime() : null;
}
