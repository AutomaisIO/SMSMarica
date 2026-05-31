using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Core.Practitioners;

public sealed class PractitionerService(FhirDbContext db, TimeProvider clock) : IPractitionerService
{
    private const string TipoRecurso = "Practitioner";
    private const int LimiteBusca = 50;

    public async Task<Practitioner> CriarAsync(Practitioner practitioner, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = practitioner.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(practitioner, id, versao: 1, agora, source);

        var row = new PractitionerRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, practitioner);
        row.Content = FhirJson.Serialize(practitioner);

        db.Practitioners.Add(row);
        await db.SaveChangesAsync(ct);
        return practitioner;
    }

    public async Task<Practitioner> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Practitioners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<Practitioner>(row.Content);
    }

    public async Task<Practitioner> AtualizarAsync(Guid id, Practitioner practitioner, CancellationToken ct = default)
    {
        var row = await db.Practitioners.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = practitioner.Meta?.Source ?? row.MetaSource;

        CarimbarMeta(practitioner, id, versao, agora, source);

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.MetaSource = source;
        ExtrairSearchParams(row, practitioner);
        row.Content = FhirJson.Serialize(practitioner);

        await db.SaveChangesAsync(ct);
        return practitioner;
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Practitioners.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
        if (row is null)
            return;

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(PractitionerBusca filtro, CancellationToken ct = default)
    {
        var query = db.Practitioners.AsNoTracking().Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filtro.Cpf))
            query = query.Where(p => p.Cpf == filtro.Cpf);
        if (!string.IsNullOrWhiteSpace(filtro.Crm))
            query = query.Where(p => p.Crm == filtro.Crm);
        if (!string.IsNullOrWhiteSpace(filtro.Nome))
            query = query.Where(p => p.Nome != null && EF.Functions.ILike(p.Nome, $"%{filtro.Nome}%"));

        var rows = await query.OrderBy(p => p.Nome).Take(LimiteBusca).ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<Practitioner>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    private static void CarimbarMeta(Practitioner p, Guid id, int versao, DateTimeOffset agora, string source)
    {
        p.Id = id.ToString();
        p.Meta ??= new Meta();
        p.Meta.VersionId = versao.ToString();
        p.Meta.LastUpdated = agora;
        p.Meta.Source = source;
    }

    private static void ExtrairSearchParams(PractitionerRow row, Practitioner p)
    {
        row.Cpf = p.Identifier.FirstOrDefault(i => i.System == FhirSystems.Cpf)?.Value;
        row.Crm = p.Identifier.FirstOrDefault(i => i.System != null && i.System.StartsWith(FhirSystems.CrmPrefix))?.Value;
        row.Nome = p.Name.FirstOrDefault(n => n.Use == HumanName.NameUse.Official)?.Text
                   ?? p.Name.FirstOrDefault()?.Text;
    }
}
