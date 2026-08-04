using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;

namespace Automais.Fhir.Core.Organizations;

public sealed class OrganizationService(FhirDbContext db, TimeProvider clock) : IOrganizationService
{
    private const string TipoRecurso = "Organization";

    /// <summary>Cadastro de unidades é minúsculo (3 no Salux, 2 no Klinikos) — teto folgado.</summary>
    private const int LimiteBusca = 500;

    /// <summary>System do CNES — identificador nacional da unidade de saúde.</summary>
    public const string SysCnes = "https://fhir.saude.gov.br/sid/cnes";

    public async Task<Organization> CriarAsync(Organization org, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = org.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(org, id, versao: 1, agora, source);

        var row = new OrganizationRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, org);
        row.Content = FhirJson.Serialize(org);

        db.Organizations.Add(row);
        await db.SaveChangesAsync(ct);
        return org;
    }

    public async Task<Organization> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<Organization>(row.Content);
    }

    public async Task<Organization> AtualizarAsync(Guid id, Organization org, CancellationToken ct = default)
    {
        var row = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = org.Meta?.Source ?? row.MetaSource;

        // Identifiers ACUMULAM entre bases: a UPA Inoã é a mesma unidade no Salux e no
        // Klinikos, e cada conector anexa o seu código sem apagar o do outro. É o mesmo
        // princípio do Patient canônico (ADR-0009), aplicado à unidade.
        var atual = FhirJson.Parse<Organization>(row.Content);
        UnirIdentifiers(org, atual);
        EstabilizarNome(org, atual);

        CarimbarMeta(org, id, versao, agora, source);

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.MetaSource = source;
        ExtrairSearchParams(row, org);
        row.Content = FhirJson.Serialize(org);

        await db.SaveChangesAsync(ct);
        return org;
    }

    public async Task<Organization> UpsertPorIdentifierAsync(
        string system, string value, Organization org, CancellationToken ct = default)
    {
        FhirIdentifier.Garantir(org.Identifier ??= [], system, value);

        var existente = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.IdentifierSystem == system && o.IdentifierValue == value && !o.IsDeleted, ct);
        if (existente is not null)
            return await AtualizarAsync(existente.Id, org, ct);

        // Antes de criar, tenta casar pelo CNES: a unidade pode já existir no hub cadastrada
        // por OUTRO PEP, com outro identifier interno. Sem isto, o segundo conector criaria uma
        // segunda Organization para a mesma unidade — exatamente o que o ADR-0039 evita.
        var cnes = CnesDe(org);
        if (!string.IsNullOrWhiteSpace(cnes))
        {
            var porCnes = await db.Organizations.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Cnes == cnes && !o.IsDeleted, ct);
            if (porCnes is not null)
                return await AtualizarAsync(porCnes.Id, org, ct);
        }

        try
        {
            return await CriarAsync(org, ct);
        }
        catch (DbUpdateException) // corrida: outro create do mesmo identifier venceu (índice único)
        {
            db.ChangeTracker.Clear();
            existente = await db.Organizations.AsNoTracking()
                .FirstOrDefaultAsync(o => o.IdentifierSystem == system && o.IdentifierValue == value && !o.IsDeleted, ct)
                ?? throw new RecursoNaoEncontradoException(TipoRecurso, $"{system}|{value}");
            return await AtualizarAsync(existente.Id, org, ct);
        }
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);
        if (row is null)
            return; // DELETE FHIR é idempotente.

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(OrganizationBusca filtro, CancellationToken ct = default)
    {
        var query = db.Organizations.AsNoTracking().Where(o => !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filtro.IdentifierSystem) && !string.IsNullOrWhiteSpace(filtro.IdentifierValue))
            query = query.Where(o => o.IdentifierSystem == filtro.IdentifierSystem && o.IdentifierValue == filtro.IdentifierValue);
        if (!string.IsNullOrWhiteSpace(filtro.Cnes))
            query = query.Where(o => o.Cnes == filtro.Cnes);
        if (filtro.PartOfId is { } pid)
            query = query.Where(o => o.PartOfId == pid);

        var rows = await query.OrderBy(o => o.Name).Take(LimiteBusca).ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<Organization>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    /// <summary>CNES do recurso, se declarado entre os identifiers.</summary>
    public static string? CnesDe(Organization o) =>
        o.Identifier?.FirstOrDefault(i => i.System == SysCnes)?.Value?.Trim();

    /// <summary>Traz para o recurso novo os identifiers que só existiam no que está no hub.</summary>
    private static void UnirIdentifiers(Organization novo, Organization atual)
    {
        novo.Identifier ??= [];
        foreach (var id in atual.Identifier ?? [])
        {
            if (!novo.Identifier.Any(x => x.System == id.System && x.Value == id.Value))
                novo.Identifier.Add((Identifier)id.DeepCopy());
        }
    }

    /// <summary>
    /// O NOME da unidade não oscila: quem nomeou primeiro permanece, e o nome trazido pelos
    /// outros PEPs entra como <c>alias</c>.
    ///
    /// <para>Sem isto, a mesma unidade fica trocando de nome a cada ciclo — a UPA Inoã é
    /// "UPA 24H INOÃ" no Salux e "UPA MARICA" no Klinikos, e o último conector a rodar
    /// venceria. Medido em 04/08/2026: 5 versões a mais que as unidades de uma base só, com o
    /// nome exibido dependendo de quem sincronizou por último. Numa base cujo princípio é
    /// "a unidade é o eixo DURÁVEL" (ADR-0039), identidade que pisca é defeito — e é a mesma
    /// classe do PUT replace-all que apagava fato clínico do Patient entre bases.</para>
    ///
    /// <para>Nada se perde: os dois nomes ficam buscáveis, um em <c>name</c> e o outro em
    /// <c>alias</c>.</para>
    /// </summary>
    public static void EstabilizarNome(Organization novo, Organization atual)
    {
        if (string.IsNullOrWhiteSpace(atual.Name)) return;

        var entrante = novo.Name;
        novo.Name = atual.Name;

        var apelidos = novo.Alias?.ToList() ?? [];
        foreach (var a in atual.Alias ?? [])
            if (!apelidos.Contains(a, StringComparer.OrdinalIgnoreCase)) apelidos.Add(a);
        if (!string.IsNullOrWhiteSpace(entrante)
            && !string.Equals(entrante, atual.Name, StringComparison.OrdinalIgnoreCase)
            && !apelidos.Contains(entrante, StringComparer.OrdinalIgnoreCase))
        {
            apelidos.Add(entrante);
        }

        novo.AliasElement.Clear();
        foreach (var a in apelidos) novo.AliasElement.Add(new FhirString(a));
    }

    private static void CarimbarMeta(Organization o, Guid id, int versao, DateTimeOffset agora, string source)
    {
        o.Id = id.ToString();
        o.Meta ??= new Meta();
        o.Meta.VersionId = versao.ToString();
        o.Meta.LastUpdated = agora;
        o.Meta.Source = source;
    }

    private static void ExtrairSearchParams(OrganizationRow row, Organization o)
    {
        row.Name = o.Name;
        row.Cnes = CnesDe(o);
        row.Ativa = o.Active ?? true;
        row.PartOfId = FhirRef.ParseId(o.PartOf?.Reference);
        (row.IdentifierSystem, row.IdentifierValue) = FhirIdentifier.Primeiro(o.Identifier);
    }
}
