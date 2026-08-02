using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Core.Locations;

public sealed class LocationService(FhirDbContext db, TimeProvider clock) : ILocationService
{
    private const string TipoRecurso = "Location";
    private const int LimiteBusca = 1000; // cadastro físico é pequeno (~600 no HMCML)

    public async Task<Location> CriarAsync(Location location, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = location.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(location, id, versao: 1, agora, source);

        var row = new LocationRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, location);
        row.Content = FhirJson.Serialize(location);

        db.Locations.Add(row);
        await db.SaveChangesAsync(ct);
        return location;
    }

    public async Task<Location> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<Location>(row.Content);
    }

    public async Task<Location> AtualizarAsync(Guid id, Location location, CancellationToken ct = default)
    {
        var row = await db.Locations.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = location.Meta?.Source ?? row.MetaSource;

        CarimbarMeta(location, id, versao, agora, source);

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.MetaSource = source;
        ExtrairSearchParams(row, location);
        row.Content = FhirJson.Serialize(location);

        await db.SaveChangesAsync(ct);
        return location;
    }

    public async Task<Location> UpsertPorIdentifierAsync(string system, string value, Location location, CancellationToken ct = default)
    {
        FhirIdentifier.Garantir(location.Identifier ??= [], system, value);

        var existente = await db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.IdentifierSystem == system && l.IdentifierValue == value && !l.IsDeleted, ct);
        if (existente is not null)
            return await AtualizarAsync(existente.Id, location, ct);

        try
        {
            return await CriarAsync(location, ct);
        }
        catch (DbUpdateException) // corrida: outro create do mesmo identifier venceu (índice único)
        {
            db.ChangeTracker.Clear();
            existente = await db.Locations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.IdentifierSystem == system && l.IdentifierValue == value && !l.IsDeleted, ct)
                ?? throw new RecursoNaoEncontradoException(TipoRecurso, $"{system}|{value}");
            return await AtualizarAsync(existente.Id, location, ct);
        }
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Locations.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct);
        if (row is null)
            return; // DELETE FHIR é idempotente.

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(LocationBusca filtro, CancellationToken ct = default)
    {
        var query = db.Locations.AsNoTracking().Where(l => !l.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filtro.IdentifierSystem) && !string.IsNullOrWhiteSpace(filtro.IdentifierValue))
            query = query.Where(l => l.IdentifierSystem == filtro.IdentifierSystem && l.IdentifierValue == filtro.IdentifierValue);
        if (filtro.PartOfId is { } pid)
            query = query.Where(l => l.PartOfId == pid);
        if (!string.IsNullOrWhiteSpace(filtro.PhysicalType))
            query = query.Where(l => l.PhysicalType == filtro.PhysicalType);

        var rows = await query
            .OrderBy(l => l.Name)
            .Take(LimiteBusca)
            .ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<Location>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    private static void CarimbarMeta(Location l, Guid id, int versao, DateTimeOffset agora, string source)
    {
        l.Id = id.ToString();
        l.Meta ??= new Meta();
        l.Meta.VersionId = versao.ToString();
        l.Meta.LastUpdated = agora;
        l.Meta.Source = source;
    }

    private static void ExtrairSearchParams(LocationRow row, Location l)
    {
        row.Name = l.Name;
        row.Status = l.Status?.ToString().ToLowerInvariant();
        row.PhysicalType = l.PhysicalType?.Coding?.FirstOrDefault()?.Code;
        row.PartOfId = FhirRef.ParseId(l.PartOf?.Reference);
        (row.IdentifierSystem, row.IdentifierValue) = FhirIdentifier.Primeiro(l.Identifier);
    }
}
