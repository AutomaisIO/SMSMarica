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

        // Colunas derivadas do content sempre: podem estar dessincronizadas por backfill parcial,
        // e era a reescrita que vinha consertando isso em silêncio. Se SÓ elas mudarem, o
        // SaveChanges abaixo persiste a correção sem inventar uma versão nova.
        row.MetaSource = source;
        ExtrairSearchParams(row, practitioner);

        if (EscritaFhir.SemMudanca(practitioner, row.Content))
        {
            // Devolve a versão VIGENTE, nunca a incrementada: um versionId que não existe no
            // banco faria o próximo If-Match do chamador dar 409 para sempre.
            CarimbarMeta(practitioner, id, row.VersionId, row.LastUpdated, source);
            await db.SaveChangesAsync(ct);
            return practitioner;
        }

        row.VersionId = versao;
        row.LastUpdated = agora;
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
        if (!string.IsNullOrWhiteSpace(filtro.Registro))
            query = query.Where(p => p.Registro == filtro.Registro);
        if (!string.IsNullOrWhiteSpace(filtro.Conselho))
        {
            var sigla = filtro.Conselho.Trim().ToUpperInvariant();
            query = query.Where(p => p.Conselho == sigla);
        }
        if (!string.IsNullOrWhiteSpace(filtro.ConselhoDiferenteDe))
        {
            var excluida = filtro.ConselhoDiferenteDe.Trim().ToUpperInvariant();
            query = query.Where(p => p.Conselho != excluida);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            // Cada palavra, em qualquer posição e ordem, sem acento e sem caixa — "jose silva" acha
            // JOSÉ DA SILVA (a frase inteira como um só "contém" não achava). Sem índice trigram de
            // propósito: são ~4 mil linhas, e o seq scan com f_unaccent mediu 30 ms em produção.
            query = query.Where(p => p.Nome != null);
            foreach (var palavra in Palavras(filtro.Nome))
            {
                var padrao = $"%{EscaparLike(palavra)}%";
                query = query.Where(p => EF.Functions.ILike(FhirDbContext.FUnaccent(p.Nome!), FhirDbContext.FUnaccent(padrao)));
            }
        }

        // Sem busca textual (nome): últimos incluídos primeiro. Com nome: quem começa pela frase
        // digitada no topo, depois ordem alfabética.
        IOrderedQueryable<PractitionerRow> ordenada;
        if (string.IsNullOrWhiteSpace(filtro.Nome))
            ordenada = query.OrderByDescending(p => p.LastUpdated);
        else
        {
            var padraoPrefixo = $"{EscaparLike(filtro.Nome.Trim())}%";
            ordenada = query
                .OrderByDescending(p => EF.Functions.ILike(FhirDbContext.FUnaccent(p.Nome!), FhirDbContext.FUnaccent(padraoPrefixo)))
                .ThenBy(p => p.Nome);
        }
        var rows = await ordenada.Take(LimiteBusca).ToListAsync(ct);

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
        var idConselho = p.Identifier.FirstOrDefault(i => i.System != null && i.System.StartsWith(FhirSystems.ConselhoPrefixRoot));
        row.Registro = idConselho?.Value;
        row.Conselho = FhirSystems.ParseConselho(idConselho?.System).Sigla;
        row.Nome = p.Name.FirstOrDefault(n => n.Use == HumanName.NameUse.Official)?.Text
                   ?? p.Name.FirstOrDefault()?.Text;
    }

    private static string[] Palavras(string texto) =>
        texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string EscaparLike(string valor) =>
        valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
