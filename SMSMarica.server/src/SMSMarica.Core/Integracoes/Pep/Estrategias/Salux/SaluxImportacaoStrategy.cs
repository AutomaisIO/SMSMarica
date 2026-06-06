using System.Collections.Concurrent;
using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Integracoes.Pep.Leitura;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Pep.Estrategias.Salux;

/// <summary>
/// Estratégia de importação do Salux (Oracle INFOSAUDE) → hub FHIR. Canal único e persistente
/// (<see cref="LeitorOracleHis"/>), queries batched, escritas paralelas no hub.
///
/// Multi-base (ADR-0009): Patient/Practitioner são <b>canônicos</b> (dedup por <b>CPF</b>, com
/// merge — acumula os identifiers/proveniência das várias bases). Os recursos clínicos
/// (Encounter/Condition/Medication/DocumentReference/Observation) levam <c>meta.source</c> por
/// base e identifiers internos prefixados pelo slug; a purga é <b>escopada por base</b> (não
/// apaga o que veio de outro hospital).
/// </summary>
public sealed class SaluxImportacaoStrategy(ILogger<SaluxImportacaoStrategy> logger) : IEstrategiaImportacaoPep
{
    private const string FmtDt = "'YYYY-MM-DD\"T\"HH24:MI:SS'";
    private const int TamanhoLote = 500;

    private static readonly string[] TiposClinicos =
        ["Observation", "MedicationRequest", "DocumentReference", "Condition", "Encounter"];

    public TipoFonte Tipo => TipoFonte.Salux;

    public async Task ImportarAsync(ContextoImportacaoPep ctx, CancellationToken ct)
    {
        var p = ctx.Progresso;
        var incremental = ctx.Opcoes.Modo == ModoSincronizacao.Incremental;
        var maxConc = LerConcorrencia(ctx.Opcoes.Concorrencia);
        var gate = new SemaphoreSlim(maxConc);
        var falhasLock = new object();

        var source = $"{SaluxFhirMapper.SourceBase}/salux/{ctx.BaseSlug}";
        var mapper = new SaluxFhirMapper(ctx.BaseSlug, source);

        void Falhou(long cd, Exception ex)
        {
            lock (falhasLock) { p.Falhas.Add((cd, ex.Message.Split('\n')[0])); }
        }

        await using var oracle = new LeitorOracleHis(
            ctx.Conexao.Host, ctx.Conexao.Porta, ctx.Conexao.Servico,
            ctx.Conexao.Usuario, ctx.Conexao.Senha, ctx.Conexao.TimeoutSegundos);
        p.FaseAtual = "conectando ao Oracle…";
        await oracle.AbrirAsync(ct, n => p.FaseAtual = n == 1
            ? "conectando ao Oracle…"
            : $"conectando ao Oracle (tentativa {n})…");

        // "Apagar antes" = full refresh DESTA base (escopado por meta.source) — não toca outras bases.
        if (ctx.Opcoes.ApagarAntes && !incremental)
        {
            p.FaseAtual = "limpando base no hub";
            await PurgarBaseAsync(ctx, source, gate, ct);
        }

        // ---------- Médicos (canônico: dedup por CPF) ----------
        if (!incremental || ctx.Marca.MedicoEm is null)
        {
            p.FaseAtual = "médicos";
            var t0 = Cronometro();
            var limite = ctx.Opcoes.Escopo == EscopoSincronizacao.Limitado ? ctx.Opcoes.MaxMedicos : null;
            var medicos = await oracle.LerAsync(SqlMedicos(limite), MapMedico, ct);
            await ParaCada(medicos, gate, async m =>
            {
                var cpf = Digitos(m.Cpf);
                if (m.Nome is null || cpf.Length == 0) return;
                try
                {
                    await UpsertCanonicoAsync(ctx, "Practitioner", SaluxFhirMapper.IdentCpf, cpf, mapper.BuildPractitioner(m), ct);
                    Interlocked.Increment(ref p.Medicos);
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { Falhou(m.Cd, ex); }
            }, ct);
            p.Tempos["medicos"] = Decorrido(t0);
            ctx.Marca.MedicoEm = DateTime.UtcNow;
        }

        // ---------- Pacientes (canônico: dedup por CPF, merge) ----------
        p.FaseAtual = "pacientes";
        var tp = Cronometro();
        var limPac = ctx.Opcoes.Escopo == EscopoSincronizacao.Limitado ? ctx.Opcoes.MaxPacientes : null;
        var sincePac = incremental ? ctx.Marca.PacienteEm : null;

        var pacientes = new ConcurrentDictionary<long, string>(); // cd_paciente -> Patient/{id}
        var pacLinhas = await oracle.LerAsync(SqlPacientes(limPac, sincePac, ctx.Opcoes.CdsPacientes), MapPaciente, ct);
        await ParaCada(pacLinhas, gate, async pac =>
        {
            var cpf = Digitos(pac.Cpf);
            if (cpf.Length == 0 || pac.Nome is null) return;
            try
            {
                var fhirId = await UpsertCanonicoAsync(ctx, "Patient", SaluxFhirMapper.IdentCpf, cpf, mapper.BuildPatient(pac), ct);
                pacientes[pac.Cd] = $"Patient/{fhirId}";
                Interlocked.Increment(ref p.Pacientes);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { Falhou(pac.Cd, ex); }
        }, ct);
        p.Tempos["pacientes"] = Decorrido(tp);

        // Incremental: atendimentos novos valem para TODOS os pacientes DESTA base já no hub.
        if (incremental)
        {
            foreach (var (cd, fhirId) in await PacientesDaBaseNoHubAsync(ctx, mapper, ct))
                pacientes.TryAdd(cd, fhirId);
        }

        // ---------- Atendimentos ----------
        p.FaseAtual = "atendimentos";
        var ta = Cronometro();
        var sinceBaa = incremental ? ctx.Marca.BaaEm : null;
        var sinceEdoc = incremental ? ctx.Marca.EdocEm : null;
        // Completo (não-apagarAntes) purga a clínica DESTA base por paciente antes de recriar.
        var purgarPorPaciente = !incremental && !ctx.Opcoes.ApagarAntes;
        DateTime? maxBaa = ctx.Marca.BaaEm;
        DateTime? maxEdoc = ctx.Marca.EdocEm;

        foreach (var lote in EmLotes(pacientes.Keys.ToList(), TamanhoLote))
        {
            ct.ThrowIfCancellationRequested();
            var cds = lote;

            var baas = await oracle.LerAsync(SqlBaas(cds, sinceBaa), MapBaa, ct);
            var chavesBaa = baas.Select(b => (b.H, b.Ano, b.Nr)).Distinct().ToList();
            var prescPorBaa = (await LerEmLotesTupla(oracle, chavesBaa, SqlPrescricoes, MapPrescricao, ct))
                .GroupBy(x => x.ChaveBaa).ToDictionary(g => g.Key, g => g.ToList());

            var edocs = await oracle.LerAsync(SqlEdocs(cds, sinceEdoc), MapEdoc, ct);
            var chavesDoc = edocs.Select(e => (e.H, e.Ano, e.Idm)).Distinct().ToList();
            var itensPorDoc = (await LerEmLotesTupla(oracle, chavesDoc, SqlEdocItens, MapEdocItem, ct))
                .GroupBy(i => i.ChaveDoc).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var b in baas) AtualizarMax(ref maxBaa, b.DtAtend);
            foreach (var d in edocs) AtualizarMax(ref maxEdoc, d.Dt);

            // Fase 0: purga clínica DESTA base por paciente (escopada por meta.source).
            if (purgarPorPaciente)
                await ParaCada(cds, gate, cd => PurgarBaseDoPacienteAsync(ctx, IdDe(pacientes[cd]), source, gate, ct), ct);

            // Fase A: Encounters do lote em paralelo.
            var encPorBaa = new ConcurrentDictionary<string, string>();
            await ParaCada(baas, gate, async b =>
            {
                if (!pacientes.TryGetValue(b.CdPaciente, out var patientRef)) return;
                try
                {
                    var enc = (Encounter)await ctx.Escritor.CriarAsync(mapper.BuildEncounter(b, patientRef), ct);
                    encPorBaa[b.Chave] = $"Encounter/{enc.Id}";
                    Interlocked.Increment(ref p.Encounters);
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { Falhou(b.CdPaciente, ex); }
            }, ct);

            // Fase B: dependentes em paralelo.
            var deps = new List<Func<Task>>();
            foreach (var b in baas)
            {
                if (!encPorBaa.TryGetValue(b.Chave, out var encRef)) continue;
                if (!pacientes.TryGetValue(b.CdPaciente, out var patientRef)) continue;

                if (mapper.BuildCondition(b, patientRef, encRef) is { } cond)
                    deps.Add(() => Escrever(ctx, b.CdPaciente, cond, () => Interlocked.Increment(ref p.Conditions), Falhou, ct));

                var authored = DtIso(b.DtAtend) ?? DtIso(b.DtCheg);
                foreach (var item in prescPorBaa.GetValueOrDefault(b.Chave, []))
                    deps.Add(() => Escrever(ctx, b.CdPaciente,
                        mapper.BuildMedicationRequest(item, patientRef, encRef, authored),
                        () => Interlocked.Increment(ref p.MedicationRequests), Falhou, ct));

                if (b.RiscoDs is { } cor && cor.Trim().Length > 0)
                    deps.Add(() => Escrever(ctx, b.CdPaciente,
                        mapper.BuildObsRisco(patientRef, encRef, DtIso(b.DtCheg) ?? DtIso(b.DtAtend), cor.Trim()),
                        () => Interlocked.Increment(ref p.Observations), Falhou, ct));
            }
            foreach (var doc in edocs)
            {
                if (doc.Baa is null || !encPorBaa.TryGetValue(doc.Baa, out var encRef)) continue;
                if (!pacientes.TryGetValue(doc.CdPaciente, out var patientRef)) continue;
                var itens = itensPorDoc.GetValueOrDefault(doc.ChaveDoc, []);
                deps.Add(async () =>
                {
                    try
                    {
                        var html = SaluxFhirMapper.MontarHtml(doc.Modelo, itens);
                        await ctx.Escritor.CriarAsync(mapper.BuildDocRef(doc, html, patientRef, encRef), ct);
                        Interlocked.Increment(ref p.DocumentReferences);
                        foreach (var o in mapper.ObservationsDeEdoc(itens, patientRef, encRef, DtIso(doc.Dt)))
                        {
                            await ctx.Escritor.CriarAsync(o, ct);
                            Interlocked.Increment(ref p.Observations);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException) { Falhou(doc.CdPaciente, ex); }
                });
            }
            await ParaCada(deps, gate, t => t(), ct);
        }
        p.Tempos["atendimentos"] = Decorrido(ta);

        ctx.Marca.PacienteEm = DateTime.UtcNow;
        ctx.Marca.BaaEm = maxBaa ?? ctx.Marca.BaaEm;
        ctx.Marca.EdocEm = maxEdoc ?? ctx.Marca.EdocEm;

        logger.LogInformation("Importação Salux ({Slug}) concluída: {Enc} atendimentos, {Obs} observations, {Falhas} falhas.",
            ctx.BaseSlug, p.Encounters, p.Observations, p.Falhas.Count);
    }

    // ---------------- escrita / upsert canônico / purga escopada ----------------

    private static async Task Escrever(ContextoImportacaoPep ctx, long cd, Resource r, Action contar,
        Action<long, Exception> falhou, CancellationToken ct)
    {
        try { await ctx.Escritor.CriarAsync(r, ct); contar(); }
        catch (Exception ex) when (ex is not OperationCanceledException) { falhou(cd, ex); }
    }

    /// <summary>
    /// Upsert canônico por chave nacional: se já existe (mesmo CPF), <b>mescla</b> os identifiers
    /// (preserva os de outras bases) e atualiza no mesmo id FHIR; senão cria. Não duplica pessoa/médico.
    /// </summary>
    private static async Task<string> UpsertCanonicoAsync(ContextoImportacaoPep ctx, string tipo, string system, string valor, Resource novo, CancellationToken ct)
    {
        var existentes = await ctx.Escritor.BuscarPorIdentifierAsync(tipo, system, valor, ct);
        var atual = existentes.Entry.Select(e => e.Resource).FirstOrDefault(r => r is not null);
        if (atual is not null)
        {
            UnirIdentifiers(novo, atual);
            novo.Id = atual.Id;
            var atualizado = await ctx.Escritor.AtualizarAsync(tipo, atual.Id!, novo, ct);
            return atualizado.Id!;
        }
        var criado = await ctx.Escritor.CriarAsync(novo, ct);
        return criado.Id!;
    }

    private static List<Identifier> IdentificadoresDe(Resource r) => r switch
    {
        Patient p => p.Identifier ??= [],
        Practitioner pr => pr.Identifier ??= [],
        _ => [],
    };

    /// <summary>Adiciona em <paramref name="novo"/> os identifiers de <paramref name="existente"/> que faltam (união por system+value).</summary>
    private static void UnirIdentifiers(Resource novo, Resource existente)
    {
        var nv = IdentificadoresDe(novo);
        foreach (var id in IdentificadoresDe(existente))
            if (!nv.Any(x => x.System == id.System && x.Value == id.Value))
                nv.Add(id);
    }

    /// <summary>Purga a clínica DESTA base de um paciente (filtra por meta.source — não toca outras bases).</summary>
    private static async Task PurgarBaseDoPacienteAsync(ContextoImportacaoPep ctx, string patientId, string source, SemaphoreSlim gate, CancellationToken ct)
    {
        foreach (var tipo in TiposClinicos)
        {
            var bundle = await ctx.Escritor.BuscarPorPacienteAsync(tipo, patientId, ct);
            var ids = bundle.Entry
                .Where(e => e.Resource?.Id is not null && e.Resource.Meta?.Source == source)
                .Select(e => e.Resource!.Id!).ToList();
            await ParaCada(ids, gate, rid => ctx.Escritor.ExcluirAsync(tipo, rid, ct), ct);
        }
    }

    /// <summary>"Apagar antes" escopado: remove toda a clínica DESTA base do hub (por meta.source).</summary>
    private static async Task PurgarBaseAsync(ContextoImportacaoPep ctx, string source, SemaphoreSlim gate, CancellationToken ct)
    {
        foreach (var tipo in TiposClinicos)
        {
            var bundle = await ctx.Escritor.ListarAsync(tipo, ct);
            var ids = bundle.Entry
                .Where(e => e.Resource?.Id is not null && e.Resource.Meta?.Source == source)
                .Select(e => e.Resource!.Id!).ToList();
            await ParaCada(ids, gate, rid => ctx.Escritor.ExcluirAsync(tipo, rid, ct), ct);
        }
    }

    /// <summary>Pacientes DESTA base já no hub (cd nativo extraído do identifier prefixado pelo slug).</summary>
    private static async Task<List<(long Cd, string FhirId)>> PacientesDaBaseNoHubAsync(ContextoImportacaoPep ctx, SaluxFhirMapper mapper, CancellationToken ct)
    {
        var bundle = await ctx.Escritor.ListarAsync("Patient", ct);
        var lista = new List<(long, string)>();
        foreach (var e in bundle.Entry)
        {
            if (e.Resource is not Patient pat || pat.Id is null) continue;
            foreach (var i in pat.Identifier?.Where(i => i.System == SaluxFhirMapper.IdentSaluxPaciente) ?? [])
            {
                if (mapper.DesprefixarPaciente(i.Value) is { } nativo && long.TryParse(nativo, out var n))
                {
                    lista.Add((n, $"Patient/{pat.Id}"));
                    break;
                }
            }
        }
        return lista;
    }

    private static string IdDe(string reference) => reference.Split('/')[^1];

    // ---------------- SQL (batched, lê colunas direto — sem JSON_OBJECT) ----------------

    private static string SqlMedicos(int? limite)
    {
        var teto = limite is { } n ? $"WHERE ROWNUM <= {n}" : string.Empty;
        return $"""
            SELECT med.cd_medico AS cd, med.nm_medico AS nome, med.nr_crm AS crm, med.uf_cd_uf AS uf,
                   med.cd_conselho AS conselho, med.cpf AS cpf, med.cns AS cns, med.nr_rg AS rg,
                   med.orgao_emissor AS orgao, TO_CHAR(med.dt_nascimento,'YYYY-MM-DD') AS nasc, med.sexo AS sexo,
                   med.ds_email AS email, med.in_ativo AS ativo, med.nm_mae AS mae, med.nm_pai AS pai,
                   med.id_categoria AS categoria, med.cd_cbo_smm AS cbo,
                   (SELECT LISTAGG(e.ds_especialidade, ', ') WITHIN GROUP (ORDER BY e.ds_especialidade)
                    FROM medico_especialidade me JOIN especialidade e ON e.cd_especialidade = me.cd_especialidade
                    WHERE me.cd_medico = med.cd_medico) AS especialidade
            FROM (SELECT * FROM medico
                  WHERE nr_crm IS NOT NULL AND nm_medico IS NOT NULL AND cpf IS NOT NULL AND dt_exclusao IS NULL
                  ORDER BY cd_medico DESC) med
            {teto}
            """;
    }

    private static string SqlPacientes(int? limite, DateTime? since, IReadOnlyList<long>? cds)
    {
        string filtro;
        string teto;
        if (cds is { Count: > 0 })
        {
            filtro = $"AND cd_paciente IN ({ListaInt(cds)})";
            teto = string.Empty;
        }
        else
        {
            filtro = since is { } d ? $"AND dt_alteracao > {OracleData(d)}" : string.Empty;
            teto = limite is { } n ? $"WHERE ROWNUM <= {n}" : string.Empty;
        }
        return $"""
            SELECT pac.cd_paciente AS cd, pac.nm_paciente AS nome, pac.nm_paciente_social AS social,
                   pac.in_flag_social AS flag_social, TO_CHAR(pac.dt_nascimento,'YYYY-MM-DD') AS nasc, pac.sexo AS sexo,
                   pac.cpf_paciente AS cpf, pac.cns AS cns, pac.rg_paciente AS rg, pac.sc_orgao_emissor AS orgao,
                   pac.nr_pis_pasep AS pis, pac.sc_passaporte AS passaporte, pac.sc_rne AS rne,
                   pac.nr_certidao_nascimento AS certidao, pac.cd_pront_sgh AS sgh, pac.cd_pront_cem AS cem,
                   TO_CHAR(pac.dt_obito,'YYYY-MM-DD') AS obito, pac.in_ativo AS ativo,
                   pac.nm_logradouro AS logr, pac.nr_logradouro AS nr_logr, pac.compl_logradouro AS compl,
                   pac.bairro AS bairro, pac.cep AS cep, pac.sc_ponto_referencia AS ref,
                   pac.nr_ddd_fone AS ddd, pac.nr_fone AS fone, pac.nr_ddd_fone_resp AS ddd_resp, pac.nr_fone_resp AS fone_resp,
                   pac.email AS email, pac.nm_mae AS mae, pac.nm_pai AS pai, pac.nm_conjuge AS conjuge,
                   pac.nm_responsavel AS responsavel, pac.ds_grau_parentesco AS grau_parentesco,
                   pac.cd_cor AS cd_cor, pac.cd_nacionalidade AS cd_nacionalidade, pac.sc_pais AS pais,
                   pac.profissao AS profissao, pac.ocupacao AS ocupacao, pac.peso AS peso, pac.altura AS altura,
                   pac.id_sangue AS sangue, pac.id_fator_rh AS rh, pac.tu_cd_etnia AS etnia,
                   TO_CHAR(pac.dt_entrada_pais,'YYYY-MM-DD') AS entrada_pais,
                   (SELECT ci.ds_cidade FROM cidade ci WHERE ci.cd_uf=pac.cd_uf AND ci.cd_cidade=pac.cd_cidade AND ROWNUM=1) AS cidade,
                   pac.cd_uf AS uf_sigla,
                   (SELECT ec.ds_est_civil FROM estado_civil ec WHERE TO_CHAR(ec.cd_est_civil)=TRIM(pac.estado_civil) AND ROWNUM=1) AS estado_civil_ds,
                   (SELECT gi.ds_grau_instrucao FROM grau_instrucao gi WHERE TO_CHAR(gi.cd_grau_instrucao)=TO_CHAR(pac.id_instrucao) AND ROWNUM=1) AS instrucao_ds,
                   (SELECT r.ds_religiao FROM religiao r WHERE TO_CHAR(r.cd_religiao)=TO_CHAR(pac.cd_religiao) AND ROWNUM=1) AS religiao_ds,
                   (SELECT bc.ds_barreira_comunicacao FROM barreira_comunicacao bc WHERE TO_CHAR(bc.cd_barreira_comunicacao)=TO_CHAR(pac.cd_barreira_comunicacao) AND ROWNUM=1) AS barreira_ds
            FROM (SELECT * FROM paciente
                  WHERE cpf_paciente IS NOT NULL AND nm_paciente IS NOT NULL AND dt_nascimento IS NOT NULL {filtro}
                  ORDER BY cd_paciente DESC) pac
            {teto}
            """;
    }

    private static string SqlBaas(IReadOnlyList<long> cds, DateTime? since)
    {
        var filtroSince = since is { } d ? $"AND b.dt_atendimento > {OracleData(d)}" : string.Empty;
        return $"""
            SELECT b.cd_paciente AS cd_paciente, b.cd_hospital AS h, b.dt_ano_baa AS ano, b.nr_baa AS nr,
                   TO_CHAR(b.dt_chegada,{FmtDt}) AS dt_cheg, TO_CHAR(b.dt_atendimento,{FmtDt}) AS dt_atend,
                   TO_CHAR(b.dt_saida,{FmtDt}) AS dt_saida, b.cd_cid AS cid,
                   (SELECT ds_cid FROM infosaude.cid WHERE cd_cid = b.cd_cid) AS cid_ds,
                   b.in_emergencia AS emerg,
                   (SELECT ds_classificacao_risco FROM infosaude.classificacao_risco cr WHERE cr.cd_classificacao_risco = b.cd_classificacao_risco) AS risco_ds,
                   (SELECT nm_medico FROM infosaude.medico WHERE cd_medico = b.cd_medico) AS medico
            FROM infosaude.baa b
            WHERE b.cd_paciente IN ({ListaInt(cds)}) {filtroSince}
            ORDER BY b.cd_paciente, b.dt_atendimento DESC NULLS LAST
            """;
    }

    private static string SqlPrescricoes(string tuplas) => $"""
        SELECT p.cd_hospital AS h, p.dt_ano_baa AS ano, p.nr_baa AS nr, p.cd_material AS cd_mat,
               (SELECT ds_material FROM infosaude.matmed m WHERE m.cd_material = p.cd_material) AS mat,
               p.qt_material_prescrita AS qt, p.in_urgencia AS urg,
               (SELECT nm_medico FROM infosaude.medico me WHERE me.cd_medico = p.cd_medico) AS medico,
               p.ds_horario AS horario, p.observacao AS obs
        FROM infosaude.presc_baa_opc_prod p
        WHERE (p.cd_hospital, p.dt_ano_baa, p.nr_baa) IN ({tuplas})
        ORDER BY p.cd_hospital, p.dt_ano_baa, p.nr_baa, p.nr_prescricao, p.seq_item
        """;

    private static string SqlEdocs(IReadOnlyList<long> cds, DateTime? since)
    {
        var filtroSince = since is { } d ? $"AND mov.dt_inclusao > {OracleData(d)}" : string.Empty;
        return $"""
            SELECT mov.cd_paciente AS cd_paciente, mov.cd_hospital AS h, mov.ano_movimento AS ano, mov.id_movimento AS idm,
                   (SELECT ds_modelo FROM infosaude.edoc_modelo m WHERE m.cd_modelo = mov.cd_modelo) AS modelo,
                   TO_CHAR(mov.dt_episodio,{FmtDt}) AS dt,
                   COALESCE(mov.baa_cd_hospital, mov.cd_hospital)||'-'||mov.dt_ano_baa||'-'||mov.nr_baa AS baa
            FROM infosaude.edoc_movimento mov
            WHERE mov.cd_paciente IN ({ListaInt(cds)}) AND mov.nr_baa IS NOT NULL {filtroSince}
            ORDER BY mov.cd_paciente, mov.dt_episodio DESC NULLS LAST
            """;
    }

    private static string SqlEdocItens(string tuplas) => $"""
        SELECT i.cd_hospital AS h, i.ano_movimento AS ano, i.id_movimento AS idm, it.ds_item AS label, i.ds_resposta AS resp
        FROM infosaude.edoc_movimento_item i
        JOIN infosaude.edoc_item it ON it.cd_item = i.cd_item
        WHERE (i.cd_hospital, i.ano_movimento, i.id_movimento) IN ({tuplas}) AND i.ds_resposta IS NOT NULL
        ORDER BY i.cd_hospital, i.ano_movimento, i.id_movimento, i.seq_docto, i.cd_item_grupo, i.cd_item
        """;

    // ---------------- mapeadores de linha ----------------

    private static MedicoLinha MapMedico(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "cd"), Col.Str(r, "nome"), Col.Str(r, "crm"), Col.Str(r, "uf"), Col.Str(r, "conselho"),
        Col.Str(r, "cpf"), Col.Str(r, "cns"), Col.Str(r, "rg"), Col.Str(r, "orgao"), Col.Str(r, "nasc"),
        Col.Str(r, "sexo"), Col.Str(r, "email"), Col.Str(r, "ativo"), Col.Str(r, "mae"), Col.Str(r, "pai"),
        Col.Str(r, "categoria"), Col.Str(r, "cbo"), Col.Str(r, "especialidade"));

    private static PacienteLinha MapPaciente(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "cd"), Col.Str(r, "nome"), Col.Str(r, "social"), Col.Str(r, "flag_social"), Col.Str(r, "nasc"),
        Col.Str(r, "sexo"), Col.Str(r, "cpf"), Col.Str(r, "cns"), Col.Str(r, "rg"), Col.Str(r, "orgao"),
        Col.Str(r, "pis"), Col.Str(r, "passaporte"), Col.Str(r, "rne"), Col.Str(r, "certidao"), Col.Str(r, "sgh"),
        Col.Str(r, "cem"), Col.Str(r, "obito"), Col.Str(r, "ativo"), Col.Str(r, "logr"), Col.Str(r, "nr_logr"),
        Col.Str(r, "compl"), Col.Str(r, "bairro"), Col.Str(r, "cep"), Col.Str(r, "ref"), Col.Str(r, "ddd"),
        Col.Str(r, "fone"), Col.Str(r, "ddd_resp"), Col.Str(r, "fone_resp"), Col.Str(r, "email"), Col.Str(r, "mae"),
        Col.Str(r, "pai"), Col.Str(r, "conjuge"), Col.Str(r, "responsavel"), Col.Str(r, "grau_parentesco"),
        Col.Str(r, "cd_cor"), Col.Str(r, "cd_nacionalidade"), Col.Str(r, "pais"), Col.Str(r, "profissao"),
        Col.Str(r, "ocupacao"), Col.Str(r, "peso"), Col.Str(r, "altura"), Col.Str(r, "sangue"), Col.Str(r, "rh"),
        Col.Str(r, "etnia"), Col.Str(r, "entrada_pais"), Col.Str(r, "cidade"), Col.Str(r, "uf_sigla"),
        Col.Str(r, "estado_civil_ds"), Col.Str(r, "instrucao_ds"), Col.Str(r, "religiao_ds"), Col.Str(r, "barreira_ds"));

    private static BaaLinha MapBaa(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "cd_paciente"), Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "nr"),
        Col.Str(r, "dt_cheg"), Col.Str(r, "dt_atend"), Col.Str(r, "dt_saida"), Col.Str(r, "cid"),
        Col.Str(r, "cid_ds"), Col.Str(r, "emerg"), Col.Str(r, "risco_ds"), Col.Str(r, "medico"));

    private static PrescricaoLinha MapPrescricao(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "nr"), Col.Str(r, "cd_mat"), Col.Str(r, "mat"),
        Col.Str(r, "qt"), Col.Str(r, "urg"), Col.Str(r, "medico"), Col.Str(r, "horario"), Col.Str(r, "obs"));

    private static EdocLinha MapEdoc(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "cd_paciente"), Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "idm"),
        Col.Str(r, "modelo"), Col.Str(r, "dt"), Col.Str(r, "baa"));

    private static EdocItemLinha MapEdocItem(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "idm"), Col.Str(r, "label"), Col.Str(r, "resp"));

    // ---------------- utilitários ----------------

    private static string Digitos(string? v) => string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

    private static int LerConcorrencia(int? opcao)
    {
        if (opcao is int o && o is > 0 and <= 64) return o;
        var v = Environment.GetEnvironmentVariable("PEP_MAX_CONCORRENCIA");
        return int.TryParse(v, out var n) && n is > 0 and <= 64 ? n : 8;
    }

    private static async Task ParaCada<T>(IEnumerable<T> itens, SemaphoreSlim gate, Func<T, Task> acao, CancellationToken ct)
    {
        var tasks = new List<Task>();
        foreach (var it in itens)
        {
            await gate.WaitAsync(ct);
            var item = it;
            tasks.Add(Task.Run(async () =>
            {
                try { await acao(item); }
                finally { gate.Release(); }
            }, ct));
        }
        await Task.WhenAll(tasks);
    }

    private static async Task<List<T>> LerEmLotesTupla<T>(
        LeitorOracleHis oracle, IReadOnlyList<(long, long, long)> chaves,
        Func<string, string> sql, Func<Oracle.ManagedDataAccess.Client.OracleDataReader, T> map, CancellationToken ct)
    {
        var todos = new List<T>();
        foreach (var lote in EmLotes(chaves, TamanhoLote))
        {
            var tuplas = string.Join(",", lote.Select(k => $"({k.Item1},{k.Item2},{k.Item3})"));
            if (tuplas.Length == 0) continue;
            todos.AddRange(await oracle.LerAsync(sql(tuplas), map, ct));
        }
        return todos;
    }

    private static IEnumerable<List<T>> EmLotes<T>(IReadOnlyList<T> fonte, int tamanho)
    {
        for (var i = 0; i < fonte.Count; i += tamanho)
            yield return fonte.Skip(i).Take(tamanho).ToList();
    }

    private static string ListaInt(IReadOnlyList<long> ids) =>
        ids.Count == 0 ? "NULL" : string.Join(",", ids.Select(i => i.ToString(CultureInfo.InvariantCulture)));

    private static string OracleData(DateTime d) =>
        $"TO_DATE('{d.ToLocalTime():yyyy-MM-dd HH:mm:ss}','YYYY-MM-DD HH24:MI:SS')";

    private static string? DtIso(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim() + "-03:00";

    private static void AtualizarMax(ref DateTime? atual, string? dataIso)
    {
        if (string.IsNullOrWhiteSpace(dataIso)) return;
        if (DateTime.TryParse(dataIso, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
            && (atual is null || d > atual))
            atual = d;
    }

    private static long Cronometro() => System.Diagnostics.Stopwatch.GetTimestamp();
    private static double Decorrido(long inicio) => System.Diagnostics.Stopwatch.GetElapsedTime(inicio).TotalSeconds;
}
