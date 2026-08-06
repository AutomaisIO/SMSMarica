using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Core.Conditions;

public sealed class ConditionService(FhirDbContext db, TimeProvider clock) : IConditionService
{
    private const string TipoRecurso = "Condition";
    private const int LimiteBusca = 200;

    public async Task<Condition> CriarAsync(Condition condition, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = condition.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(condition, id, versao: 1, agora, source);

        var row = new ConditionRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, condition);
        row.Content = FhirJson.Serialize(condition);

        db.Conditions.Add(row);
        await db.SaveChangesAsync(ct);
        return condition;
    }

    public async Task<Condition> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Conditions.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<Condition>(row.Content);
    }

    public async Task<Condition> AtualizarAsync(Guid id, Condition condition, CancellationToken ct = default)
    {
        var row = await db.Conditions.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = condition.Meta?.Source ?? row.MetaSource;

        CarimbarMeta(condition, id, versao, agora, source);

        // Colunas derivadas do content sempre: podem estar dessincronizadas por backfill parcial,
        // e era a reescrita que vinha consertando isso em silêncio. Se SÓ elas mudarem, o
        // SaveChanges abaixo persiste a correção sem inventar uma versão nova.
        row.MetaSource = source;
        ExtrairSearchParams(row, condition);

        if (EscritaFhir.SemMudanca(condition, row.Content))
        {
            // Devolve a versão VIGENTE, nunca a incrementada: um versionId que não existe no
            // banco faria o próximo If-Match do chamador dar 409 para sempre.
            CarimbarMeta(condition, id, row.VersionId, row.LastUpdated, source);
            await db.SaveChangesAsync(ct);
            return condition;
        }

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.Content = FhirJson.Serialize(condition);

        await db.SaveChangesAsync(ct);
        return condition;
    }

    public async Task<Condition> UpsertPorIdentifierAsync(string system, string value, Condition recurso, CancellationToken ct = default)
    {
        FhirIdentifier.Garantir(recurso.Identifier ??= [], system, value);

        var existente = await db.Conditions.AsNoTracking()
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
            existente = await db.Conditions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdentifierSystem == system && x.IdentifierValue == value && !x.IsDeleted, ct)
                ?? throw new RecursoNaoEncontradoException(TipoRecurso, $"{system}|{value}");
            return await AtualizarAsync(existente.Id, recurso, ct);
        }
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Conditions.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (row is null)
            return;

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(ConditionBusca filtro, CancellationToken ct = default)
    {
        var query = db.Conditions.AsNoTracking().Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filtro.IdentifierSystem) && !string.IsNullOrWhiteSpace(filtro.IdentifierValue))
            query = query.Where(x => x.IdentifierSystem == filtro.IdentifierSystem && x.IdentifierValue == filtro.IdentifierValue);

        if (filtro.PatientId is { } pid)
            query = query.Where(c => c.PatientId == pid);
        if (filtro.EncounterId is { } eid)
            query = query.Where(c => c.EncounterId == eid);

        var rows = await query
            .OrderByDescending(c => c.LastUpdated)
            .Take(LimiteBusca)
            .ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<Condition>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    private static void CarimbarMeta(Condition c, Guid id, int versao, DateTimeOffset agora, string source)
    {
        c.Id = id.ToString();
        c.Meta ??= new Meta();
        c.Meta.VersionId = versao.ToString();
        c.Meta.LastUpdated = agora;
        c.Meta.Source = source;
    }

    private static void ExtrairSearchParams(ConditionRow row, Condition c)
    {
        (row.IdentifierSystem, row.IdentifierValue) = FhirIdentifier.Primeiro(c.Identifier);
        row.PatientId = FhirRef.ParseId(c.Subject?.Reference);
        row.EncounterId = FhirRef.ParseId(c.Encounter?.Reference);
        row.Code = c.Code?.Coding?.FirstOrDefault()?.Code;
    }
}
