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

        return LerRecurso(row);
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
        {
            // Procura entre TODOS os CNS, nao so o oficial: quem busca pelo numero antigo -- uma
            // solicitacao ja gravada, uma carga do SISREG com o provisorio -- tem de achar a
            // pessoa. Sem isso ela parece nao existir e o chamador cria uma ficha nova.
            //
            // `Any(... Contains ...)` de proposito, e nao `CnsTodos.Contains(x)`: o segundo gera
            // `x = ANY(cns_todos)`, que o GIN NAO atende -- medido em producao com 344 mil linhas,
            // dava Parallel Seq Scan. Esta forma gera o operador `&&` (overlaps), que usa o indice
            // (Bitmap Index Scan). A diferenca importa porque esta busca roda uma vez por paciente
            // durante a importacao.
            var procurado = new[] { filtro.Cns! };
            query = query.Where(p => p.Cns == filtro.Cns
                || (p.CnsTodos != null && p.CnsTodos.Any(c => procurado.Contains(c))));
        }
        if (!string.IsNullOrWhiteSpace(filtro.Nome))
            // Insensível a acento E case: f_unaccent() (wrapper IMMUTABLE de unaccent) normaliza
            // os dois lados; o ILIKE cuida do case. f_unaccent (não unaccent) porque é o que casa
            // o índice GIN trigram ix_patient_nome_funaccent_trgm — sem ele, seq scan em 378k.
            query = query.Where(p => p.Nome != null
                && EF.Functions.ILike(FhirDbContext.FUnaccent(p.Nome), FhirDbContext.FUnaccent($"%{filtro.Nome}%")));

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
                (p.Nome != null && EF.Functions.ILike(FhirDbContext.FUnaccent(p.Nome), FhirDbContext.FUnaccent(contemNome)))
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
                    && EF.Functions.ILike(FhirDbContext.FUnaccent(p.Nome), FhirDbContext.FUnaccent(padraoPrefixo)))
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
                Resource = LerRecurso(row),
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
            bundle.Entry.Add(new Bundle.EntryComponent { Resource = LerRecurso(row) });
        if (rows.Count == count)
            bundle.Link.Add(new Bundle.LinkComponent
            {
                Relation = "next",
                Url = $"fhir/Patient/_manutencao?_cursor={rows[^1].Id}&_count={count}",
            });
        return bundle;
    }

    public async Task<ResultadoFusao> FundirAsync(Guid sobreviventeId, Guid absorvidoId,
        CancellationToken ct = default)
    {
        if (sobreviventeId == absorvidoId)
            throw new RecursoInvalidoException("Sobrevivente e absorvido são o mesmo Patient.");

        var rowS = await db.Patients.FirstOrDefaultAsync(p => p.Id == sobreviventeId && !p.IsDeleted, ct)
                   ?? throw new RecursoNaoEncontradoException(TipoRecurso, sobreviventeId.ToString());
        var rowA = await db.Patients.FirstOrDefaultAsync(p => p.Id == absorvidoId && !p.IsDeleted, ct)
                   ?? throw new RecursoNaoEncontradoException(TipoRecurso, absorvidoId.ToString());

        var sobrevivente = LerRecurso(rowS);
        var absorvido = LerRecurso(rowA);

        // Fundir duas vezes empilharia links e identifiers e tornaria o desfazer ambíguo.
        if (absorvido.Link.Any(l => l.Type == Patient.LinkType.ReplacedBy))
            throw new RecursoInvalidoException(
                $"Patient/{absorvidoId} já foi fundido antes (tem link replaced-by).");
        if (sobrevivente.Link.Any(l => l.Type == Patient.LinkType.ReplacedBy))
            throw new RecursoInvalidoException(
                $"Patient/{sobreviventeId} já foi absorvido por outro — não pode ser sobrevivente.");

        // ---- 1. as chaves MUDAM de dono (não são copiadas)
        //
        // Um identifier identifica uma PESSOA, e depois da fusão essa pessoa é o sobrevivente.
        // Copiar deixaria os dois Patients com o mesmo CNS — que é exatamente a anomalia sendo
        // consertada: a busca por aquele CNS passaria a devolver DOIS, e um conector fazendo
        // "achar por CNS" pegaria o registro absorvido e penduraria dado novo nele.
        // (Pego pelo teste `Sobrevivente_passa_a_ser_encontrado_pela_chave_do_absorvido`, que
        // encontrava 2 quando o código copiava.)
        var absorvidos = 0;
        foreach (var ident in absorvido.Identifier)
        {
            var mesmo = sobrevivente.Identifier.Any(x =>
                x.System == ident.System &&
                string.Equals(Digitos(x.Value), Digitos(ident.Value), StringComparison.Ordinal));
            if (mesmo) continue;

            var movido = (Identifier)ident.DeepCopy();
            // O CNS que vem do absorvido não é o número que representa a pessoa hoje — entra como
            // `old`, para o CnsOficial() não trocar o oficial do sobrevivente por ele. Continua
            // valendo como chave de busca: TodosOsCns() o coloca em cns_todos.
            if (movido.System == FhirSystems.Cns) movido.Use = Identifier.IdentifierUse.Old;
            sobrevivente.Identifier.Add(movido);
            absorvidos++;
        }
        // A lápide não guarda identificador: quem a procura, procura pelo id — e é o `link` que
        // conta a história. Os números todos seguem no sobrevivente.
        absorvido.Identifier.Clear();

        // ---- 2. o vínculo, nos dois sentidos
        sobrevivente.Link.Add(new Patient.LinkComponent
        {
            Other = new ResourceReference($"{TipoRecurso}/{absorvidoId}"),
            Type = Patient.LinkType.Replaces,
        });
        absorvido.Link.Add(new Patient.LinkComponent
        {
            Other = new ResourceReference($"{TipoRecurso}/{sobreviventeId}"),
            Type = Patient.LinkType.ReplacedBy,
        });
        // `active=false` e NÃO `is_deleted`: o registro tem de continuar legível para quem chegar
        // pelo id antigo encontrar o ponteiro. Excluir logicamente devolveria 404 e quebraria
        // link salvo, integração e o app do cidadão.
        absorvido.Active = false;

        var agora = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // ---- 3. repontar o clínico de fhir.*
        var encounters = await ReapontarAsync("encounter", absorvidoId, sobreviventeId, agora, ct);
        var conditions = await ReapontarAsync("condition", absorvidoId, sobreviventeId, agora, ct);
        var observations = await ReapontarAsync("observation", absorvidoId, sobreviventeId, agora, ct);
        var medRequests = await ReapontarAsync("medication_request", absorvidoId, sobreviventeId, agora, ct);
        var medAdmins = await ReapontarAsync("medication_administration", absorvidoId, sobreviventeId, agora, ct);
        var documentos = await ReapontarAsync("document_reference", absorvidoId, sobreviventeId, agora, ct);

        // ---- 4. gravar os dois Patients
        foreach (var (row, recurso) in new[] { (rowS, sobrevivente), (rowA, absorvido) })
        {
            var versao = row.VersionId + 1;
            CarimbarMeta(recurso, row.Id, versao, agora, row.MetaSource);
            row.VersionId = versao;
            row.LastUpdated = agora;
            ExtrairSearchParams(row, recurso);
            row.Content = FhirJson.Serialize(recurso);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new ResultadoFusao(sobreviventeId, absorvidoId, absorvidos,
            encounters, conditions, observations, medRequests, medAdmins, documentos);
    }

    /// <summary>
    /// Move os recursos clínicos de um paciente para outro — <b>na coluna E no documento</b>.
    ///
    /// <para>Repontar só a coluna <c>patient_id</c> não funciona, e falha em silêncio: a coluna é
    /// search param, mas quem responde uma leitura é o <c>content</c>. Com só a coluna mexida, a
    /// busca encontra o recurso no paciente novo e o <c>GET</c> devolve <c>subject</c> apontando
    /// para o antigo — coluna e documento discordando, sem erro nenhum. Foi o que o teste
    /// <c>Atendimento_do_absorvido_passa_para_o_sobrevivente</c> pegou.</para>
    ///
    /// <para>Todos os recursos clínicos daqui referenciam o paciente pelo mesmo caminho
    /// (<c>subject.reference</c>), então uma forma de SQL serve para os seis. O nome da tabela vem
    /// de lista fixa no chamador — nunca de entrada.</para>
    /// </summary>
    private static readonly string[] TabelasClinicas =
        ["encounter", "condition", "observation",
         "medication_request", "medication_administration", "document_reference"];

    private async Task<int> ReapontarAsync(string tabela, Guid de, Guid para,
        DateTimeOffset agora, CancellationToken ct)
    {
        // O nome da tabela entra na SQL por interpolação (não dá para parametrizar identificador),
        // então a garantia é esta lista fechada — verificada aqui, não prometida num comentário.
        if (!TabelasClinicas.Contains(tabela))
            throw new ArgumentOutOfRangeException(nameof(tabela), tabela, "Tabela clínica desconhecida.");

        // EF1002 suprimido: o único trecho interpolado é `tabela`, validado acima contra a lista
        // fixa; todo valor vai como parâmetro.
#pragma warning disable EF1002
        // `ARRAY['subject','reference']` em vez de `'{subject,reference}'`: o ExecuteSqlRaw trata a
        // SQL como *composite format string*, então chave literal vira placeholder inválido e
        // estoura `FormatException` em tempo de execução — compila, e falha só ao rodar.
        return await db.Database.ExecuteSqlRawAsync(
            $$"""
              UPDATE fhir.{{tabela}}
                 SET patient_id   = {0},
                     content      = jsonb_set(content, ARRAY['subject','reference'], to_jsonb({1}::text)),
                     last_updated = {2}
               WHERE patient_id = {3}
              """,
            [para, $"{TipoRecurso}/{para}", agora, de], ct);
#pragma warning restore EF1002
    }

    /// <summary>
    /// Lê o documento canônico da linha <b>garantindo que o recurso saia com id</b>.
    ///
    /// <para>A coluna <c>id</c> é a verdade — é a chave primária e é por ela que o recurso é
    /// endereçado. O <c>content</c> deveria repeti-la, mas basta um escritor esquecer para o
    /// recurso sair sem identidade, e quem consome não tem como se defender: em 08/09/2026 uma
    /// carga inseriu 36.257 fichas com o id só na coluna, e o <c>Guid.Parse(p.Id)</c> do
    /// SMSMais.server derrubou <c>/pacientes</c>, a lista de pacientes da conversa e o webhook do
    /// WhatsApp por quase quatro horas — a conversa com o paciente parada.</para>
    ///
    /// <para>Preencher aqui custa uma comparação por linha e fecha a classe inteira de falha,
    /// independentemente de quem gravou.</para>
    /// </summary>
    /// <summary>
    /// Devolve o recurso completando, a partir da linha, o que o documento gravado não trouxe.
    ///
    /// <para>O <c>id</c> entrou aqui depois do incidente de 08/09/2026 (36.257 fichas gravadas sem
    /// a chave <c>"id"</c> derrubaram <c>/pacientes</c> e o webhook do WhatsApp). O <c>meta</c> é
    /// a mesma classe de falha, um campo adiante: das mesmas 36.257, <b>36.253 continuam sem
    /// <c>meta.versionId</c> no documento</b>. Não derruba nada — o ETag só sai no PUT, que sempre
    /// passa pelo <see cref="CarimbarMeta"/> —, mas quem lê um recurso e o grava de volta não tem
    /// versão para mandar no <c>If-Match</c>, e a concorrência otimista deixa de existir sem
    /// avisar. Perda de atualização silenciosa é pior que erro.</para>
    ///
    /// <para><b>Só completa o que falta.</b> Documento com <c>meta</c> próprio é preservado: a
    /// linha é a autoridade sobre versão e instante, não sobre o resto do <c>meta</c>.</para>
    /// </summary>
    private static Patient LerRecurso(PatientRow row)
    {
        var patient = FhirJson.Parse<Patient>(row.Content);
        if (string.IsNullOrWhiteSpace(patient.Id)) patient.Id = row.Id.ToString();

        patient.Meta ??= new Meta();
        // A versão AUTORITATIVA é a coluna version_id (é ela que AtualizarAsync compara no
        // If-Match). O versionId embutido no JSONB pode estar defasado por backfill parcial
        // (incidente 08/09/2026) — se ele for menor que a coluna, o chamador mandaria um
        // If-Match obsoleto e levaria 409 para sempre. Sempre reflete a coluna, nunca o doc.
        patient.Meta.VersionId = row.VersionId.ToString();
        if (patient.Meta.LastUpdated is null)
            patient.Meta.LastUpdated = row.LastUpdated;
        if (string.IsNullOrWhiteSpace(patient.Meta.Source))
            patient.Meta.Source = row.MetaSource;

        return patient;
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
        row.Cns = CnsOficial(patient);
        row.CnsTodos = TodosOsCns(patient);
        row.Nome = patient.Name.FirstOrDefault(n => n.Use == HumanName.NameUse.Official)?.Text
                   ?? patient.Name.FirstOrDefault()?.Text;
        row.Telefone = ExtrairTelefones(patient);
        row.Nascimento = ParseDataNascimento(patient.BirthDate);
    }

    private static string? ValorIdentifier(Patient patient, string system) =>
        patient.Identifier.FirstOrDefault(i => i.System == system)?.Value;

    /// <summary>
    /// O CNS que representa a pessoa HOJE: o marcado <c>use=official</c> e, na falta dele, o
    /// primeiro que nao esteja marcado como <c>old</c>.
    ///
    /// <para>A ordem importa quando o paciente tem mais de um: sem a preferencia pelo oficial, um
    /// CNS antigo gravado antes ficaria na coluna e a ficha apareceria pelo numero que ja nao vale.</para>
    /// </summary>
    private static string? CnsOficial(Patient patient)
    {
        var cns = patient.Identifier.Where(i => i.System == FhirSystems.Cns).ToList();
        return (cns.FirstOrDefault(i => i.Use == Identifier.IdentifierUse.Official)
                ?? cns.FirstOrDefault(i => i.Use != Identifier.IdentifierUse.Old)
                ?? cns.FirstOrDefault())?.Value;
    }

    /// <summary>
    /// TODOS os CNS, separados por espaco — inclusive os que ja nao sao oficiais.
    ///
    /// <para>Um CNS antigo continua sendo chave de busca legitima: o legado (solicitacao, exame,
    /// laudo) aponta para ele, e a proxima carga do SISREG pode trazer o numero velho. Sem isto, a
    /// pessoa "some" quando procurada pelo identificador anterior e vira ficha duplicada.</para>
    /// </summary>
    private static string[]? TodosOsCns(Patient patient)
    {
        var todos = patient.Identifier
            .Where(i => i.System == FhirSystems.Cns)
            .Select(i => Digitos(i.Value))
            .Where(d => d.Length > 0)
            .Distinct()
            .ToArray();
        return todos.Length == 0 ? null : todos;
    }

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
