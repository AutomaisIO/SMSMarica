using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Core.Common.Excecoes;
using Automais.Fhir.Core.Fhir;
using Automais.Fhir.Data;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Core.Patients;

public sealed class PatientService(FhirDbContext db, TimeProvider clock) : IPatientService
{
    private const string TipoRecurso = "Patient";
    private const int LimiteBusca = 50;
    private const int LimiteMaximoBusca = 500;

    public async Task<Patient> CriarAsync(Patient patient, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = clock.GetUtcNow();
        var source = patient.Meta?.Source ?? MetaSources.Hub;

        CarimbarMeta(patient, id, versao: 1, agora, source);

        var row = new PatientRow { Id = id, VersionId = 1, LastUpdated = agora, MetaSource = source };
        ExtrairSearchParams(row, patient);
        row.Content = FhirJson.Serialize(patient);

        db.Patients.Add(row);
        await db.SaveChangesAsync(ct);
        return patient;
    }

    public async Task<Patient> LerAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Patients.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        return FhirJson.Parse<Patient>(row.Content);
    }

    public async Task<Patient> AtualizarAsync(Guid id, Patient patient, int? versaoEsperada = null, CancellationToken ct = default)
    {
        var row = await db.Patients.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new RecursoNaoEncontradoException(TipoRecurso, id.ToString());

        // Concorrência otimista (If-Match): rejeita escrita sobre versão obsoleta.
        if (versaoEsperada is int esperada && esperada != row.VersionId)
            throw new ConflitoVersaoException(TipoRecurso, id.ToString(), esperada, row.VersionId);

        var agora = clock.GetUtcNow();
        var versao = row.VersionId + 1;
        var source = patient.Meta?.Source ?? row.MetaSource;

        CarimbarMeta(patient, id, versao, agora, source);

        // Colunas derivadas do content sempre: podem estar dessincronizadas por backfill parcial,
        // e era a reescrita que vinha consertando isso em silêncio. Se SÓ elas mudarem, o
        // SaveChanges abaixo persiste a correção sem inventar uma versão nova.
        row.MetaSource = source;
        ExtrairSearchParams(row, patient);

        if (EscritaFhir.SemMudanca(patient, row.Content))
        {
            // Devolve a versão VIGENTE, nunca a incrementada: um versionId que não existe no
            // banco faria o próximo If-Match do chamador dar 409 para sempre.
            CarimbarMeta(patient, id, row.VersionId, row.LastUpdated, source);
            await db.SaveChangesAsync(ct);
            return patient;
        }

        row.VersionId = versao;
        row.LastUpdated = agora;
        row.Content = FhirJson.Serialize(patient);

        await db.SaveChangesAsync(ct);
        return patient;
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Patients.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
        if (row is null)
            return; // DELETE FHIR é idempotente.

        row.IsDeleted = true;
        row.LastUpdated = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Bundle> BuscarAsync(PatientBusca filtro, CancellationToken ct = default)
    {
        // Busca exata por identifier de QUALQUER system, por containment no jsonb — atendida
        // pelo índice GIN de expressão em (content->'identifier'). É o caminho pelo qual os
        // conectores reencontram paciente SEM CPF (chave local da base de origem); sem isso,
        // cada ciclo incremental criava uma cópia nova (25 pacientes viraram 260 recursos).
        var baseQuery = db.Patients.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filtro.IdentifierSystem) && !string.IsNullOrWhiteSpace(filtro.IdentifierValue))
        {
            var alvo = System.Text.Json.JsonSerializer.Serialize(
                new[] { new { system = filtro.IdentifierSystem, value = filtro.IdentifierValue } });
            baseQuery = db.Patients.FromSqlInterpolated(
                $"SELECT * FROM fhir.patient WHERE (content->'identifier') @> {alvo}::jsonb");
        }
        var query = baseQuery.AsNoTracking().Where(p => !p.IsDeleted);

        // Ids não-nulo (mesmo vazio) É filtro: _id sem match devolve searchset vazio.
        if (filtro.Ids is not null)
            query = query.Where(p => filtro.Ids.Contains(p.Id));
        if (!string.IsNullOrWhiteSpace(filtro.Cpf))
            query = query.Where(p => p.Cpf == filtro.Cpf);
        if (!string.IsNullOrWhiteSpace(filtro.Cns))
            query = query.Where(p => p.Cns == filtro.Cns);
        if (!string.IsNullOrWhiteSpace(filtro.Nome))
            // Insensível a acento E case: unaccent() (extensão, provisionada pela migration do
            // SMSMarica.server no mesmo banco) normaliza os dois lados; o ILIKE cuida do case.
            query = query.Where(p => p.Nome != null
                && EF.Functions.ILike(EF.Functions.Unaccent(p.Nome), EF.Functions.Unaccent($"%{filtro.Nome}%")));

        // Busca humana unificada: nome (contém) OU CPF/CNS por PREFIXO (não espera terminar).
        // `%` do termo é escapado para não virar wildcard vindo do usuário.
        var termo = filtro.Termo?.Trim();
        var digitos = Digitos(termo);
        var buscaTermo = !string.IsNullOrWhiteSpace(termo);
        if (buscaTermo)
        {
            var contemNome = $"%{EscaparLike(termo!)}%";
            var prefixoDoc = digitos.Length > 0 ? digitos + "%" : null;
            query = query.Where(p =>
                (p.Nome != null && EF.Functions.ILike(EF.Functions.Unaccent(p.Nome), EF.Functions.Unaccent(contemNome)))
                || (prefixoDoc != null && p.Cpf != null && EF.Functions.Like(p.Cpf, prefixoDoc))
                || (prefixoDoc != null && p.Cns != null && EF.Functions.Like(p.Cns, prefixoDoc)));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Telefone))
        {
            var fone = Digitos(filtro.Telefone);
            if (fone.Length > 0)
                query = query.Where(p => p.Telefone != null && p.Telefone.Contains(fone));
        }

        // Sem filtro: últimos incluídos primeiro (LastUpdated desc). Com filtro: por nome.
        var semFiltro = string.IsNullOrWhiteSpace(filtro.Cpf) && string.IsNullOrWhiteSpace(filtro.Cns)
                        && string.IsNullOrWhiteSpace(filtro.Nome) && string.IsNullOrWhiteSpace(filtro.Telefone)
                        && string.IsNullOrWhiteSpace(filtro.IdentifierValue) && !buscaTermo
                        && filtro.Ids is null;
        IOrderedQueryable<PatientRow> ordenada;
        if (semFiltro)
            ordenada = query.OrderByDescending(p => p.LastUpdated);
        else if (buscaTermo)
        {
            // Prefixo-primeiro: quem o NOME começa com o termo aparece no topo (antes dos
            // "contém no meio"). Resolve o caso do ticket #91 — nome fora dos 50 primeiros
            // alfabéticos sumia da busca mesmo estando na lista.
            // O `unaccent(...)` do padrão fica DENTRO da árvore de expressão (é função de banco;
            // chamá-lo em C# lançaria). Só a string do padrão é montada aqui.
            var padraoPrefixo = $"{EscaparLike(termo!)}%";
            ordenada = query
                .OrderByDescending(p => p.Nome != null
                    && EF.Functions.ILike(EF.Functions.Unaccent(p.Nome), EF.Functions.Unaccent(padraoPrefixo)))
                .ThenBy(p => p.Nome);
        }
        else
            ordenada = query.OrderBy(p => p.Nome);

        // Busca por _id é em lote (resolver de nomes do smsmarica): devolve TODOS os
        // ids pedidos, não limita ao teto. Demais buscas seguem o Limite pedido pela tela
        // (o "itens por página"), com fallback no padrão do serviço.
        var limite = filtro.Ids is { Count: > 0 } ids
            ? ids.Count
            : Math.Clamp(filtro.Limite ?? LimiteBusca, 1, LimiteMaximoBusca);
        var rows = await ordenada.Take(limite).ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                Resource = FhirJson.Parse<Patient>(row.Content),
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match },
            });
        }
        return bundle;
    }

    public async Task<Bundle> ListarParaManutencaoAsync(Guid? cursor, int count, CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 500);
        var query = db.Patients.AsNoTracking().Where(p => !p.IsDeleted);
        if (cursor is Guid c) query = query.Where(p => p.Id.CompareTo(c) > 0);
        var rows = await query.OrderBy(p => p.Id).Take(count).ToListAsync(ct);

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = rows.Count };
        foreach (var row in rows)
            bundle.Entry.Add(new Bundle.EntryComponent { Resource = FhirJson.Parse<Patient>(row.Content) });
        if (rows.Count == count)
            bundle.Link.Add(new Bundle.LinkComponent
            {
                Relation = "next",
                Url = $"fhir/Patient/_manutencao?_cursor={rows[^1].Id}&_count={count}",
            });
        return bundle;
    }

    private static void CarimbarMeta(Patient patient, Guid id, int versao, DateTimeOffset agora, string source)
    {
        patient.Id = id.ToString();
        patient.Meta ??= new Meta();
        patient.Meta.VersionId = versao.ToString();
        patient.Meta.LastUpdated = agora;
        patient.Meta.Source = source;
    }

    private static void ExtrairSearchParams(PatientRow row, Patient patient)
    {
        row.Cpf = ValorIdentifier(patient, FhirSystems.Cpf);
        row.Cns = ValorIdentifier(patient, FhirSystems.Cns);
        row.Nome = patient.Name.FirstOrDefault(n => n.Use == HumanName.NameUse.Official)?.Text
                   ?? patient.Name.FirstOrDefault()?.Text;
        row.Telefone = ExtrairTelefones(patient);
        row.Nascimento = ParseDataNascimento(patient.BirthDate);
    }

    private static string? ValorIdentifier(Patient patient, string system) =>
        patient.Identifier.FirstOrDefault(i => i.System == system)?.Value;

    /// <summary>Dígitos de todos os telefones (telecom[system=phone]), juntos por espaço.</summary>
    private static string? ExtrairTelefones(Patient patient)
    {
        if (patient.Telecom is null || patient.Telecom.Count == 0) return null;
        var fones = patient.Telecom
            .Where(t => t.System == ContactPoint.ContactPointSystem.Phone)
            .Select(t => Digitos(t.Value))
            .Where(d => d.Length > 0)
            .Distinct();
        var juntos = string.Join(' ', fones);
        return juntos.Length == 0 ? null : juntos;
    }

    private static string Digitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new string([.. valor.Where(char.IsDigit)]);

    /// <summary>Escapa os curingas do LIKE/ILIKE (<c>%</c>, <c>_</c>, <c>\</c>) num termo digitado
    /// pelo usuário, para que ele seja casado literalmente e não como padrão.</summary>
    private static string EscaparLike(string valor) =>
        valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static DateOnly? ParseDataNascimento(string? birthDate) =>
        DateOnly.TryParseExact(birthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
}
