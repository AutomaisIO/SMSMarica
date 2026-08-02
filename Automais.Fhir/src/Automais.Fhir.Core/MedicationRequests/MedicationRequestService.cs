using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Core.MedicationRequests;

public sealed class MedicationRequestService(FhirDbContext db, TimeProvider clock) : IMedicationRequestService
{
    private const string TipoRecurso = "MedicationRequest";
    private const int LimiteBusca = 500;

    public async Task<MedicationRequest> CriarAsync(MedicationRequest mr, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = mr.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(mr, id, versao: 1, agora, source);

        var row = new MedicationRequestRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, mr);
        row.Content = FhirJson.Serialize(mr);

        db.MedicationRequests.Add(row);
        await db.SaveChangesAsync(ct);
        return mr;
    }

    public async Task<MedicationRequest> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.MedicationRequests.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<MedicationRequest>(row.Content);
    }

    public async Task<MedicationRequest> AtualizarAsync(Guid id, MedicationRequest mr, CancellationToken ct = default)
    {
        var row = await db.MedicationRequests.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = mr.Meta?.Source ?? row.MetaSource;

        CarimbarMeta(mr, id, versao, agora, source);

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.MetaSource = source;
        ExtrairSearchParams(row, mr);
        row.Content = FhirJson.Serialize(mr);

        await db.SaveChangesAsync(ct);
        return mr;
    }

    public async Task<MedicationRequest> UpsertPorIdentifierAsync(string system, string value, MedicationRequest recurso, CancellationToken ct = default)
    {
        FhirIdentifier.Garantir(recurso.Identifier ??= [], system, value);

        var existente = await db.MedicationRequests.AsNoTracking()
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
            existente = await db.MedicationRequests.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdentifierSystem == system && x.IdentifierValue == value && !x.IsDeleted, ct)
                ?? throw new RecursoNaoEncontradoException(TipoRecurso, $"{system}|{value}");
            return await AtualizarAsync(existente.Id, recurso, ct);
        }
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.MedicationRequests.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct);
        if (row is null)
            return;

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(MedicationRequestBusca filtro, CancellationToken ct = default)
    {
        var query = db.MedicationRequests.AsNoTracking().Where(m => !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filtro.IdentifierSystem) && !string.IsNullOrWhiteSpace(filtro.IdentifierValue))
            query = query.Where(x => x.IdentifierSystem == filtro.IdentifierSystem && x.IdentifierValue == filtro.IdentifierValue);

        if (filtro.PatientId is { } pid)
            query = query.Where(m => m.PatientId == pid);
        if (filtro.EncounterId is { } eid)
            query = query.Where(m => m.EncounterId == eid);

        var rows = await query
            .OrderByDescending(m => m.AuthoredOn)
            .Take(LimiteBusca)
            .ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<MedicationRequest>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    private static void CarimbarMeta(MedicationRequest m, Guid id, int versao, DateTimeOffset agora, string source)
    {
        m.Id = id.ToString();
        m.Meta ??= new Meta();
        m.Meta.VersionId = versao.ToString();
        m.Meta.LastUpdated = agora;
        m.Meta.Source = source;
    }

    private static void ExtrairSearchParams(MedicationRequestRow row, MedicationRequest m)
    {
        (row.IdentifierSystem, row.IdentifierValue) = FhirIdentifier.Primeiro(m.Identifier);
        row.PatientId = FhirRef.ParseId(m.Subject?.Reference);
        row.EncounterId = FhirRef.ParseId(m.Encounter?.Reference);
        row.Medicamento = (m.Medication as CodeableConcept)?.Text;
        row.AuthoredOn = ParseData(m.AuthoredOn);
    }

    private static DateTimeOffset? ParseData(string? d) =>
        DateTimeOffset.TryParse(d, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var r) ? r.ToUniversalTime() : null;
}
