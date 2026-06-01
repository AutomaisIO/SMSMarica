using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Core.DocumentReferences;

public sealed class DocumentReferenceService(FhirDbContext db, TimeProvider clock) : IDocumentReferenceService
{
    private const string TipoRecurso = "DocumentReference";
    private const int LimiteBusca = 200;

    public async Task<DocumentReference> CriarAsync(DocumentReference doc, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = doc.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(doc, id, versao: 1, agora, source);

        var row = new DocumentReferenceRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, doc);
        row.Content = FhirJson.Serialize(doc);

        db.DocumentReferences.Add(row);
        await db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task<DocumentReference> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.DocumentReferences.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<DocumentReference>(row.Content);
    }

    public async Task<DocumentReference> AtualizarAsync(Guid id, DocumentReference doc, CancellationToken ct = default)
    {
        var row = await db.DocumentReferences.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = doc.Meta?.Source ?? row.MetaSource;

        CarimbarMeta(doc, id, versao, agora, source);

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.MetaSource = source;
        ExtrairSearchParams(row, doc);
        row.Content = FhirJson.Serialize(doc);

        await db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.DocumentReferences.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        if (row is null)
            return;

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(DocumentReferenceBusca filtro, CancellationToken ct = default)
    {
        var query = db.DocumentReferences.AsNoTracking().Where(d => !d.IsDeleted);

        if (filtro.PatientId is { } pid)
            query = query.Where(d => d.PatientId == pid);
        if (filtro.EncounterId is { } eid)
            query = query.Where(d => d.EncounterId == eid);

        var rows = await query
            .OrderByDescending(d => d.Data)
            .Take(LimiteBusca)
            .ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<DocumentReference>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    private static void CarimbarMeta(DocumentReference d, Guid id, int versao, DateTimeOffset agora, string source)
    {
        d.Id = id.ToString();
        d.Meta ??= new Meta();
        d.Meta.VersionId = versao.ToString();
        d.Meta.LastUpdated = agora;
        d.Meta.Source = source;
    }

    private static void ExtrairSearchParams(DocumentReferenceRow row, DocumentReference d)
    {
        row.PatientId = FhirRef.ParseId(d.Subject?.Reference);
        row.EncounterId = FhirRef.ParseId(d.Context?.Encounter?.FirstOrDefault()?.Reference);
        row.Tipo = d.Type?.Text;
        row.Data = d.Date?.ToUniversalTime(); // Npgsql exige offset 0 em timestamptz
    }
}
