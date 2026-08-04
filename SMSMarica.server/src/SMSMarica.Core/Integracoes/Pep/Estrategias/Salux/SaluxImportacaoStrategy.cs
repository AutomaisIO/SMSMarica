using System.Collections.Concurrent;
using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Integracoes.Pep.Leitura;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Pep.Estrategias.Salux;

/// <summary>
/// Estratégia de importação do Salux (Oracle INFOSAUDE) → hub FHIR. Canal único e persistente
/// (<see cref="LeitorOracleHis"/>), escritas paralelas no hub.
///
/// <b>Streaming por blocos</b> (keyset por cd): nunca materializa todos os registros — lê médicos
/// e pacientes em páginas, e processa os atendimentos de cada bloco antes de ler o próximo,
/// liberando a memória. Memória constante em qualquer escopo (Limitado ou Tudo).
///
/// Multi-base (ADR-0009): Patient/Practitioner canônicos (dedup por CPF, merge); recursos clínicos
/// com meta.source por base + identifiers prefixados pelo slug; purga escopada por base.
/// </summary>
public sealed class SaluxImportacaoStrategy(ILogger<SaluxImportacaoStrategy> logger) : IEstrategiaImportacaoPep
{
    private const string FmtDt = "'YYYY-MM-DD\"T\"HH24:MI:SS'";
    private const int TamanhoLote = 500;     // tuplas por query batched (prescrição/itens)
    private const int ChunkPacientes = 300;  // pacientes por página (memória constante)
    private const int ChunkMedicos = 500;
    private const int TamanhoLoteLog = 5000; // linhas do EDOC_MOVIMENTO_LOG por poll

    private static readonly string[] TiposClinicos =
        ["Observation", "MedicationRequest", "DocumentReference", "Condition", "Encounter"];

    public TipoFonte Tipo => TipoFonte.Salux;

    public async Task ImportarAsync(ContextoImportacaoPep ctx, CancellationToken ct)
    {
        var p = ctx.Progresso;
        var incremental = ctx.Opcoes.Modo == ModoSincronizacao.Incremental;
        var limitado = ctx.Opcoes.Escopo == EscopoSincronizacao.Limitado;
        var gate = new SemaphoreSlim(LerConcorrencia(ctx.Opcoes.Concorrencia));
        var falhasLock = new object();

        var source = $"{SaluxFhirMapper.SourceBase}/salux/{ctx.BaseSlug}";
        var mapper = new SaluxFhirMapper(ctx.BaseSlug, source);
        var sourcesPurga = new HashSet<string> { source, $"{SaluxFhirMapper.SourceBase}/salux" };

        void Falhou(long cd, Exception ex)
        {
            var msg = ex.Message.Split('\n')[0];
            lock (falhasLock) { p.Falhas.Add((cd, msg)); }
            ctx.Falhas?.Registrar(cd, msg); // trilha durável + insumo do reimport direcionado
        }

        await using var oracle = new LeitorOracleHis(
            ctx.Conexao.Host, ctx.Conexao.Porta, ctx.Conexao.Servico,
            ctx.Conexao.Usuario, ctx.Conexao.Senha, ctx.Conexao.TimeoutSegundos);
        p.FaseAtual = "conectando ao Oracle…";
        await oracle.AbrirAsync(ct, n => p.FaseAtual = n == 1
            ? "conectando ao Oracle…"
            : $"conectando ao Oracle (tentativa {n})…");

        if (ctx.Opcoes.ApagarAntes && !incremental)
        {
            p.FaseAtual = "limpando base no hub";
            await PurgarBaseAsync(ctx, sourcesPurga, gate, ct);
        }

        var purgarPorPaciente = !incremental && !ctx.Opcoes.ApagarAntes;
        // Lag de segurança (ADR-0024): recua a marca p/ cobrir clock skew e transações Oracle
        // longas. Seguro porque TODA escrita clínica é upsert por identifier — reprocessar a
        // janela sobrescreve a mesma linha, não duplica.
        var lag = TimeSpan.FromMinutes(5);
        var sinceBaa = incremental ? ctx.Marca.AtendimentoEm - lag : null;
        var sinceEdoc = incremental ? ctx.Marca.DocumentoEm - lag : null;
        var sinceFia = incremental ? ctx.Marca.InternacaoEm - lag : null;
        DateTime? maxBaa = ctx.Marca.AtendimentoEm, maxEdoc = ctx.Marca.DocumentoEm, maxFia = ctx.Marca.InternacaoEm;
        var segPac = 0d; var segAtend = 0d;
        // Cache de Location por run: preenchido pela carga de referência (quando roda) e
        // completado sob demanda pelo hub.
        var leitoRefs = new ConcurrentDictionary<string, string?>();

        // ---------- Unidades de saúde (ADR-0039) ----------
        // Roda ANTES de qualquer coisa clínica, e todo ciclo: são 3 linhas, o upsert é
        // idempotente, e sem esse mapa nenhum Encounter sai com serviceProvider. O CNES vem da
        // própria origem (INFOSAUDE.HOSPITAL), então não há de-para configurado à mão.
        var orgPorHospital = new Dictionary<long, string>();
        {
            p.FaseAtual = "unidades";
            foreach (var h in await oracle.LerAsync(SqlHospitais(), MapHospital, ct))
            {
                try
                {
                    var org = await ctx.Escritor.UpsertPorIdentifierAsync(
                        mapper.BuildOrganization(h), SaluxFhirMapper.IdentSaluxHospital, mapper.Pref(h.Chave), ct);
                    orgPorHospital[h.Cd] = $"Organization/{org.Id}";
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { Falhou(h.Cd, ex); }
            }
        }
        // ---------- Médicos (paginado) ----------
        // Re-scan integral também quando o scheduler força (marca de médicos envelheceu) —
        // sem isso médico novo/alterado nunca mais entrava depois da 1ª importação.
        if (!incremental || ctx.Marca.ProfissionalEm is null || ctx.Opcoes.ForcarMedicos)
        {
            p.FaseAtual = "médicos";
            var t0 = Cronometro();
            var limite = limitado ? ctx.Opcoes.MaxMedicos : null;
            long? last = null; var count = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var tam = limite is { } L ? Math.Min(ChunkMedicos, L - count) : ChunkMedicos;
                if (tam <= 0) break;
                var chunk = await oracle.LerAsync(SqlMedicos(last, tam), MapMedico, ct);
                if (chunk.Count == 0) break;
                await ParaCada(chunk, gate, async m =>
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
                last = chunk.Min(x => x.Cd);
                count += chunk.Count;
                if (chunk.Count < tam) break;
            }
            p.Tempos["medicos"] = Decorrido(t0);

            // Estrutura física (setor→quarto→leito → Location) pega carona no mesmo gate:
            // cadastro pequeno (~600 no HMCML) que muda raramente — re-scan diário basta.
            p.FaseAtual = "estrutura física";
            var tEstr = Cronometro();
            await CargaEstruturaFisicaAsync(oracle, mapper, ctx, leitoRefs, ct);
            p.Tempos["estrutura"] = Decorrido(tEstr);

            // Médicos é re-scan integral: a marca é o instante do scan (não há watermark de
            // origem confiável em `medico`). Persistida já — fase concluída sobrevive a queda.
            ctx.Marca.ProfissionalEm = DateTime.UtcNow;
            if (ctx.SalvarMarca is { } salvarMed) await salvarMed(ctx.Marca, ct);
        }

        // ---------- Pacientes + Atendimentos ----------
        if (ctx.Opcoes.CdsPacientes is { Count: > 0 } cdsExpl)
        {
            // Lista explícita (medição / reimport pontual) — volume limitado, lê de uma vez.
            p.FaseAtual = "pacientes";
            var tpc = Cronometro();
            var linhas = await oracle.LerAsync(SqlPacientes(null, null, null, cdsExpl), MapPaciente, ct);
            var map = await UpsertPacientesChunkAsync(ctx, mapper, linhas, gate, p, Falhou, ct);
            segPac += Decorrido(tpc);
            p.FaseAtual = "atendimentos";
            var ta = Cronometro();
            var (cb, ce) = await ProcessarAtendimentosAsync(oracle, mapper, ctx, map, orgPorHospital, sinceBaa, sinceEdoc, purgarPorPaciente, sourcesPurga, gate, p, Falhou, ct);
            maxBaa = Max(maxBaa, cb); maxEdoc = Max(maxEdoc, ce);
            maxFia = Max(maxFia, await ProcessarInternacoesAsync(oracle, mapper, ctx, map, orgPorHospital, sinceFia, leitoRefs, gate, p, Falhou, ct));
            segAtend += Decorrido(ta);
        }
        else if (incremental)
        {
            // Pacientes criados/alterados desde a marca (paginado). O filtro cobre também o
            // cadastro novo: 72% dos pacientes novos nascem com dt_alteracao NULL (ADR-0024).
            p.FaseAtual = "pacientes";
            var tpc = Cronometro();
            long? last = null;
            DateTime? maxPacOrigem = null;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var chunk = await oracle.LerAsync(SqlPacientes(last, ChunkPacientes, ctx.Marca.PacienteEm - lag, null), MapPaciente, ct);
                if (chunk.Count == 0) break;
                await UpsertPacientesChunkAsync(ctx, mapper, chunk, gate, p, Falhou, ct);
                foreach (var pac in chunk)
                    maxPacOrigem = Max(maxPacOrigem, Max(SaluxTempo.ParseUtc(pac.DtAlteracao), SaluxTempo.ParseUtc(pac.DtCadastro)));
                last = chunk.Min(x => x.Cd);
                if (chunk.Count < ChunkPacientes) break;
            }
            segPac += Decorrido(tpc);
            // Fase INTEIRA concluída → avança a marca pelo máximo da ORIGEM e persiste.
            // (A paginação é por cd, não por data — avançar no meio pularia registros.)
            ctx.Marca.PacienteEm = maxPacOrigem ?? ctx.Marca.PacienteEm;
            if (ctx.SalvarMarca is { } salvarPac) await salvarPac(ctx.Marca, ct);

            // Atendimentos dirigidos pela ORIGEM: quem tem BAA/eDoc novo desde a marca —
            // inclui paciente que ainda não estava no hub. (Antes enumerava o hub, que
            // capava a busca em 50 pacientes — defeito D3 do ADR-0024.)
            p.FaseAtual = "atendimentos";
            var ta = Cronometro();
            var cdsNovos = await oracle.LerAsync(SqlCdsComAtendimentoNovo(sinceBaa, sinceEdoc, sinceFia), r => Col.Long(r, "cd"), ct);
            foreach (var lote in EmLotes(cdsNovos, TamanhoLote))
            {
                ct.ThrowIfCancellationRequested();
                var linhas = await oracle.LerAsync(SqlPacientes(null, null, null, lote), MapPaciente, ct);
                var map = await UpsertPacientesChunkAsync(ctx, mapper, linhas, gate, p, Falhou, ct);
                var (cb, ce) = await ProcessarAtendimentosAsync(oracle, mapper, ctx, map, orgPorHospital, sinceBaa, sinceEdoc, false, sourcesPurga, gate, p, Falhou, ct);
                maxBaa = Max(maxBaa, cb); maxEdoc = Max(maxEdoc, ce);
                maxFia = Max(maxFia, await ProcessarInternacoesAsync(oracle, mapper, ctx, map, orgPorHospital, sinceFia, leitoRefs, gate, p, Falhou, ct));
            }
            segAtend += Decorrido(ta);

            // ---------- CDC de eDoc (edições e exclusões) — poll por PK do log ----------
            p.FaseAtual = "edições de documentos";
            if (ctx.Marca.LogDocumentoId is null)
            {
                // Primeira ativação: ancora no fim do log. O passado já entrou (e continua
                // entrando) pelo fluxo normal de dt_inclusao — o CDC só cuida do que MUDA.
                var maxIds = await oracle.LerAsync(SqlEdocLogMax(), r => Col.Long(r, "id"), ct);
                ctx.Marca.LogDocumentoId = maxIds.Count > 0 ? maxIds[0] : 0;
                if (ctx.SalvarMarca is { } ancorar) await ancorar(ctx.Marca, ct);
            }
            else
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    var logs = await oracle.LerAsync(SqlEdocLog(ctx.Marca.LogDocumentoId.Value, TamanhoLoteLog), MapEdocLog, ct);
                    if (logs.Count == 0) break;
                    await ProcessarEdocLogAsync(oracle, mapper, ctx, logs, sourcesPurga, gate, p, Falhou, ct);
                    // Lote do log processado inteiro → a marca avança e persiste (retomável).
                    ctx.Marca.LogDocumentoId = logs[^1].Id;
                    if (ctx.SalvarMarca is { } salvarLog) await salvarLog(ctx.Marca, ct);
                    if (logs.Count < TamanhoLoteLog) break;
                }
            }
        }
        else
        {
            // COMPLETO: streaming end-to-end por bloco de pacientes (memória constante).
            // Cursor de retomada (só no escopo Tudo): começa do ponteiro informado/salvo
            // e grava o cd do último bloco CONCLUÍDO — permite retomar de onde parou.
            // Reprocessar o bloco de fronteira é seguro: Patient/Practitioner são upsert
            // idempotente e os clínicos passam por purga-por-paciente antes de regravar.
            var limite = limitado ? ctx.Opcoes.MaxPacientes : null;
            long? last = limitado ? null : ctx.Opcoes.CursorPacienteInicial;
            var count = 0;
            var exausto = false;
            DateTime? maxPacOrigem = null;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var tam = limite is { } L ? Math.Min(ChunkPacientes, L - count) : ChunkPacientes;
                if (tam <= 0) break;

                p.FaseAtual = "pacientes";
                var tpc = Cronometro();
                var chunk = await oracle.LerAsync(SqlPacientes(last, tam, null, null), MapPaciente, ct);
                if (chunk.Count == 0) { exausto = true; break; }
                var map = await UpsertPacientesChunkAsync(ctx, mapper, chunk, gate, p, Falhou, ct);
                foreach (var pac in chunk)
                    maxPacOrigem = Max(maxPacOrigem, Max(SaluxTempo.ParseUtc(pac.DtAlteracao), SaluxTempo.ParseUtc(pac.DtCadastro)));
                last = chunk.Min(x => x.Cd); count += chunk.Count;
                segPac += Decorrido(tpc);

                p.FaseAtual = "atendimentos";
                var ta = Cronometro();
                var (cb, ce) = await ProcessarAtendimentosAsync(oracle, mapper, ctx, map, orgPorHospital, null, null, purgarPorPaciente, sourcesPurga, gate, p, Falhou, ct);
                maxBaa = Max(maxBaa, cb); maxEdoc = Max(maxEdoc, ce);
                maxFia = Max(maxFia, await ProcessarInternacoesAsync(oracle, mapper, ctx, map, orgPorHospital, null, leitoRefs, gate, p, Falhou, ct));
                segAtend += Decorrido(ta);

                // Checkpoint: bloco totalmente concluído (pacientes + atendimentos).
                if (!limitado && ctx.SalvarCursorPaciente is { } salvar)
                    await salvar(last, ct);

                if (chunk.Count < tam) { exausto = true; break; }
            }

            // Base inteira concluída → zera o cursor (próxima rodada começa do topo) e a
            // marca de pacientes pode avançar pelo máximo da origem visto na varredura.
            if (exausto && !limitado)
            {
                if (ctx.SalvarCursorPaciente is { } limpar) await limpar(null, ct);
                ctx.Marca.PacienteEm = Max(maxPacOrigem, ctx.Marca.PacienteEm);
            }
        }

        p.Tempos["pacientes"] = segPac;
        p.Tempos["atendimentos"] = segAtend;

        // Marca = máximo da ORIGEM processado (nunca "agora"); persiste ao fim do run —
        // redundante com a persistência por fase, mas cobre os caminhos completo/lista.
        ctx.Marca.AtendimentoEm = maxBaa ?? ctx.Marca.AtendimentoEm;
        ctx.Marca.DocumentoEm = maxEdoc ?? ctx.Marca.DocumentoEm;
        ctx.Marca.InternacaoEm = maxFia ?? ctx.Marca.InternacaoEm;
        if (ctx.SalvarMarca is { } salvarFim) await salvarFim(ctx.Marca, ct);

        logger.LogInformation("Importação Salux ({Slug}) concluída: {Pac} pacientes, {Enc} atendimentos, {Falhas} falhas.",
            ctx.BaseSlug, p.Pacientes, p.Encounters, p.Falhas.Count);
    }

    /// <summary>
    /// Upsert de um bloco de pacientes; devolve o mapa cd → Patient/{id}.
    ///
    /// <para>Dois caminhos, e a diferença importa:</para>
    /// <list type="bullet">
    /// <item><b>Com CPF</b> — upsert CANÔNICO por CPF, com merge. É o caminho que une a mesma
    /// pessoa entre bases (Salux e Klinikos convergem para um único Patient).</item>
    /// <item><b>Sem CPF</b> — upsert pelo identificador LOCAL da base
    /// (<c>urn:salux:cd_paciente</c>), marcado com a tag de identidade incompleta. Nunca entra
    /// no casamento por CPF, porque não há o que casar.</item>
    /// </list>
    ///
    /// <para><b>Por que passou a entrar.</b> Até 03/08 o filtro exigia CPF e 63.324 pacientes
    /// do Salux (17,1%) eram descartados EM SILÊNCIO — junto com 127 mil atendimentos e 8.216
    /// internações, das quais <b>6.697 são recém-nascidos</b> (o HMCML é hospital maternal:
    /// bebê não tem CPF). Perder o registro de nascimento para preservar uma promessa de
    /// unicidade é a troca errada. Agora o dado entra e a incerteza fica <b>declarada</b>.</para>
    ///
    /// <para><b>A trava que protege a unicidade</b>: quem tem a tag só é encontrado pelo próprio
    /// identificador local. Nenhuma heurística — nome, nascimento, mãe — pode uni-lo a outro
    /// registro; entre bases ele PODE se repetir, e é exatamente isso que a tag declara.</para>
    /// </summary>
    private static async Task<ConcurrentDictionary<long, string>> UpsertPacientesChunkAsync(
        ContextoImportacaoPep ctx, SaluxFhirMapper mapper, IReadOnlyList<PacienteLinha> chunk,
        SemaphoreSlim gate, Progresso.ProgressoImportacao p, Action<long, Exception> falhou, CancellationToken ct)
    {
        var map = new ConcurrentDictionary<long, string>();
        await ParaCada(chunk, gate, async pac =>
        {
            if (pac.Nome is null) return;
            var cpf = Digitos(pac.Cpf);
            try
            {
                string fhirId;
                if (cpf.Length == 11)
                {
                    fhirId = await UpsertCanonicoAsync(
                        ctx, "Patient", SaluxFhirMapper.IdentCpf, cpf, mapper.BuildPatient(pac), ct);
                }
                else
                {
                    var recurso = mapper.BuildPatient(pac);
                    SaluxFhirMapper.MarcarIdentidadeIncompleta(recurso);
                    fhirId = await UpsertCanonicoAsync(
                        ctx, "Patient", SaluxFhirMapper.IdentSaluxPaciente, mapper.Pref(pac.Cd.ToString(CultureInfo.InvariantCulture)),
                        recurso, ct);
                    Interlocked.Increment(ref p.PacientesIdentidadeIncompleta);
                }
                map[pac.Cd] = $"Patient/{fhirId}";
                Interlocked.Increment(ref p.Pacientes);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { falhou(pac.Cd, ex); }
        }, ct);
        return map;
    }

    /// <summary>Lê e grava os atendimentos (Encounter/Condition/Medication/DocRef/Observation) de UM bloco de pacientes.</summary>
    private async Task<(DateTime? MaxBaa, DateTime? MaxEdoc)> ProcessarAtendimentosAsync(
        LeitorOracleHis oracle, SaluxFhirMapper mapper, ContextoImportacaoPep ctx,
        ConcurrentDictionary<long, string> pacientes, IReadOnlyDictionary<long, string> orgPorHospital,
        DateTime? sinceBaa, DateTime? sinceEdoc,
        bool purgarPorPaciente, IReadOnlySet<string> sourcesPurga, SemaphoreSlim gate,
        Progresso.ProgressoImportacao p, Action<long, Exception> falhou, CancellationToken ct)
    {
        var cds = pacientes.Keys.ToList();
        if (cds.Count == 0) return (null, null);
        DateTime? maxBaa = null, maxEdoc = null;

        var baas = await oracle.LerAsync(SqlBaas(cds, sinceBaa), MapBaa, ct);
        var chavesBaa = baas.Select(b => (b.H, b.Ano, b.Nr)).Distinct().ToList();
        var prescPorBaa = (await LerEmLotesTupla(oracle, chavesBaa, SqlPrescricoes, MapPrescricao, ct))
            .GroupBy(x => x.ChaveBaa).ToDictionary(g => g.Key, g => g.ToList());

        var edocs = await oracle.LerAsync(SqlEdocs(cds, sinceEdoc), MapEdoc, ct);
        var chavesDoc = edocs.Select(e => (e.H, e.Ano, e.Idm)).Distinct().ToList();
        var itensPorDoc = (await LerEmLotesTupla(oracle, chavesDoc, SqlEdocItens, MapEdocItem, ct))
            .GroupBy(i => i.ChaveDoc).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var b in baas) maxBaa = Max(maxBaa, SaluxTempo.ParseUtc(b.DtAtend));
        // A marca do eDoc avança pela MESMA coluna do filtro (dt_inclusao); dt_episodio
        // pode ser anterior à inclusão e atrasaria/adiantaria a marca indevidamente.
        foreach (var d in edocs) maxEdoc = Max(maxEdoc, SaluxTempo.ParseUtc(d.DtIncl) ?? SaluxTempo.ParseUtc(d.Dt));

        if (purgarPorPaciente)
            await ParaCada(cds, gate, cd => PurgarPacienteAsync(ctx, IdDe(pacientes[cd]), sourcesPurga, ct), ct);

        // Toda a clínica vai por UPSERT condicional (identifier determinístico — ADR-0024):
        // run re-executado, retry de falha, lag de segurança e BAA EDITADO atualizam a mesma
        // linha em vez de duplicar. Ids lógicos ficam estáveis (timeline não quebra).
        var encPorBaa = new ConcurrentDictionary<string, string>();
        await ParaCada(baas, gate, async b =>
        {
            if (!pacientes.TryGetValue(b.CdPaciente, out var patientRef)) return;
            try
            {
                var enc = (Encounter)await ctx.Escritor.UpsertPorIdentifierAsync(
                    mapper.BuildEncounter(b, patientRef, orgPorHospital.GetValueOrDefault(b.H)), SaluxFhirMapper.IdentSaluxBaa, mapper.Pref(b.Chave), ct);
                encPorBaa[b.Chave] = $"Encounter/{enc.Id}";
                Interlocked.Increment(ref p.Encounters);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { falhou(b.CdPaciente, ex); }
        }, ct);

        var deps = new List<Func<Task>>();
        foreach (var b in baas)
        {
            if (!encPorBaa.TryGetValue(b.Chave, out var encRef)) continue;
            if (!pacientes.TryGetValue(b.CdPaciente, out var patientRef)) continue;

            if (mapper.BuildCondition(b, patientRef, encRef) is { } cond)
                deps.Add(() => EscreverUpsert(ctx, b.CdPaciente, cond, () => Interlocked.Increment(ref p.Conditions), falhou, ct));

            var authored = DtIso(b.DtAtend) ?? DtIso(b.DtCheg);
            foreach (var item in prescPorBaa.GetValueOrDefault(b.Chave, []))
                deps.Add(() => EscreverUpsert(ctx, b.CdPaciente,
                    mapper.BuildMedicationRequest(item, patientRef, encRef, authored),
                    () => Interlocked.Increment(ref p.MedicationRequests), falhou, ct));

            if (b.RiscoDs is { } cor && cor.Trim().Length > 0)
                deps.Add(() => EscreverUpsert(ctx, b.CdPaciente,
                    mapper.BuildObsRisco(b.Chave, patientRef, encRef, DtIso(b.DtCheg) ?? DtIso(b.DtAtend), cor.Trim()),
                    () => Interlocked.Increment(ref p.Observations), falhou, ct));
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
                    await ctx.Escritor.UpsertPorIdentifierAsync(
                        mapper.BuildDocRef(doc, html, patientRef, encRef), SaluxFhirMapper.IdentSaluxEdoc, mapper.Pref(doc.ChaveDoc), ct);
                    Interlocked.Increment(ref p.DocumentReferences);
                    foreach (var o in mapper.ObservationsDeEdoc(doc.ChaveDoc, itens, patientRef, encRef, DtIso(doc.Dt)))
                    {
                        var ident = o.Identifier[0];
                        await ctx.Escritor.UpsertPorIdentifierAsync(o, ident.System!, ident.Value!, ct);
                        Interlocked.Increment(ref p.Observations);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { falhou(doc.CdPaciente, ex); }
            });
        }
        await ParaCada(deps, gate, t => t(), ct);
        return (maxBaa, maxEdoc);
    }

    // ---------------- escrita / upsert canônico / purga escopada ----------------

    /// <summary>Upsert de um recurso clínico pelo SEU identifier determinístico (primeiro da lista).</summary>
    private static async Task EscreverUpsert(ContextoImportacaoPep ctx, long cd, Resource r, Action contar,
        Action<long, Exception> falhou, CancellationToken ct)
    {
        try
        {
            var ident = ((IIdentifiable<List<Identifier>>)r).Identifier[0];
            await ctx.Escritor.UpsertPorIdentifierAsync(r, ident.System!, ident.Value!, ct);
            contar();
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { falhou(cd, ex); }
    }

    private static async Task<string> UpsertCanonicoAsync(ContextoImportacaoPep ctx, string tipo, string system, string valor, Resource novo, CancellationToken ct)
    {
        var existentes = await ctx.Escritor.BuscarPorIdentifierAsync(tipo, system, valor, ct);
        var atual = existentes.Entry.Select(e => e.Resource).FirstOrDefault(r => r is not null);

        for (var tentativa = 1; atual is not null; tentativa++)
        {
            UnirIdentifiers(novo, atual);
            // Merge/preserve (ADR-0020): reimport NÃO sobrescreve blob, campos editados no painel
            // nem telefones confirmados; o resto (identidade/filiação/extras) vem do Oracle.
            if (novo is Patient np && atual is Patient ap)
            {
                Pacientes.Fhir.PatientMergeFhir.PreservarDoExistente(np, ap);
                // Conflito de VERDADE (não de escrita): mesmo CPF, nascimento diferente. Registra
                // e CONGELA — origem não sobrescreve o hub até a arbitragem dizer quem está certo.
                ConciliarNascimento(ctx, system, valor, np, ap);
            }
            novo.Id = atual.Id;
            // If-Match: se o painel editou entre a leitura e o PUT, re-lê e re-mergeia (preserva a edição).
            novo.Meta ??= new Meta();
            novo.Meta.VersionId = atual.Meta?.VersionId;
            try
            {
                var atualizado = await ctx.Escritor.AtualizarAsync(tipo, atual.Id!, novo, ct);
                return atualizado.Id!;
            }
            catch (Pacientes.Fhir.ConflitoVersaoHubException) when (tentativa < 3)
            {
                var refetch = await ctx.Escritor.BuscarPorIdentifierAsync(tipo, system, valor, ct);
                atual = refetch.Entry.Select(e => e.Resource).FirstOrDefault(r => r is not null);
            }
        }

        var criado = await ctx.Escritor.CriarAsync(novo, ct);
        return criado.Id!;
    }

    /// <summary>
    /// Conciliação de nascimento no upsert canônico do Patient (ADR-0039 / plano §3.2). Medido
    /// em 01/08/2026: 142 CPFs com nascimento diferente entre hub e Salux (81 com ANO diferente)
    /// — candidatos a cadastro trocado. Fundir às cegas mistura o histórico de duas pessoas.
    ///
    /// <para>Regra: divergiu ⇒ o hub PREVALECE (congelamento) e a divergência vai para a fila de
    /// arbitragem, que pergunta à consulta oficial de CPF qual das duas datas confere. Quando o
    /// veredicto disser "origem correta", o CPF sai do congelamento e o próximo run corrige o hub
    /// sozinho — sem nenhuma escrita especial.</para>
    /// </summary>
    private static void ConciliarNascimento(
        ContextoImportacaoPep ctx, string system, string cpf, Patient novo, Patient atual)
    {
        if (system != SaluxFhirMapper.IdentCpf) return;

        var origem = DataCompleta(novo.BirthDate);
        var hub = DataCompleta(atual.BirthDate);
        if (origem is null || hub is null || origem == hub) return;

        // Já conhecida: respeita a decisão vigente (congelar ou não) sem re-registrar —
        // senão um ciclo de 30 min ficaria somando ocorrência no mesmo conflito para sempre.
        if (ctx.DivergenciasConhecidas.TryGetValue(cpf, out var congelar))
        {
            if (congelar) novo.BirthDate = atual.BirthDate;
            return;
        }

        novo.BirthDate = atual.BirthDate; // congela até a arbitragem
        ctx.Divergencias?.Registrar(new Divergencias.DivergenciaDetectada(
            CdPaciente: CdDe(novo),
            Cpf: cpf,
            Tipo: TipoDivergenciaIdentidade.NascimentoDivergente,
            ValorOrigem: origem,
            ValorHub: hub,
            NomeOrigem: NomeOficial(novo),
            NomeHub: NomeOficial(atual),
            PatientIdHub: atual.Id));
    }

    /// <summary>Data só quando é ISO completa (<c>yyyy-MM-dd</c>) — parcial não é divergência.</summary>
    private static string? DataCompleta(string? d) =>
        d is { Length: 10 } && DateOnly.TryParseExact(
            d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? d : null;

    private static string? NomeOficial(Patient p) =>
        p.Name?.FirstOrDefault(n => n.Use == HumanName.NameUse.Official)?.Text
        ?? p.Name?.FirstOrDefault()?.Text;

    /// <summary>cd_paciente da origem, lido do identifier interno (0 quando ausente).</summary>
    private static long CdDe(Patient p)
    {
        var v = p.Identifier?.FirstOrDefault(i => i.System == SaluxFhirMapper.IdentSaluxPaciente)?.Value;
        var digitos = new string([.. (v ?? string.Empty).Where(char.IsDigit)]);
        return long.TryParse(digitos, out var cd) ? cd : 0;
    }

    private static List<Identifier> IdentificadoresDe(Resource r) => r switch
    {
        Patient p => p.Identifier ??= [],
        Practitioner pr => pr.Identifier ??= [],
        _ => [],
    };

    private static void UnirIdentifiers(Resource novo, Resource existente)
    {
        var nv = IdentificadoresDe(novo);
        foreach (var id in IdentificadoresDe(existente))
            if (!nv.Any(x => x.System == id.System && x.Value == id.Value))
                nv.Add(id);
    }

    /// <summary>
    /// Purga TODOS os recursos clínicos (deste source) de um paciente, em laço até esvaziar.
    /// A busca do hub tem teto por página (200/500) sem paginação e <see cref="HubFhirEscritor"/>
    /// não segue <c>next</c>; como Criar é POST cego, parar no 1º teto deixaria recursos antigos
    /// vivos e o reimport DUPLICARIA. Exclui é soft-delete, então cada nova busca já omite os
    /// apagados e o laço converge em ceil(N/teto) rodadas. Sequencial por paciente (NÃO reentra
    /// no semáforo — isso travaria o run); a concorrência vem do <c>ParaCada</c> externo.
    /// </summary>
    private static async Task PurgarPacienteAsync(ContextoImportacaoPep ctx, string patientId, IReadOnlySet<string> sources, CancellationToken ct)
    {
        foreach (var tipo in TiposClinicos)
        {
            while (true)
            {
                var bundle = await ctx.Escritor.BuscarPorPacienteAsync(tipo, patientId, ct);
                var ids = bundle.Entry
                    .Where(e => e.Resource?.Id is not null && e.Resource.Meta?.Source is { } s && sources.Contains(s))
                    .Select(e => e.Resource!.Id!).ToList();
                if (ids.Count == 0) break;
                foreach (var rid in ids)
                    await ctx.Escritor.ExcluirAsync(tipo, rid, ct);
            }
        }
    }

    private static async Task PurgarBaseAsync(ContextoImportacaoPep ctx, IReadOnlySet<string> sources, SemaphoreSlim gate, CancellationToken ct)
    {
        foreach (var tipo in TiposClinicos)
        {
            // Laço até esvaziar: ListarAsync também é capado por página e sem paginação; parar na
            // 1ª página deixaria a maior parte da base viva (full refresh incompleto → duplicação).
            while (true)
            {
                var bundle = await ctx.Escritor.ListarAsync(tipo, ct);
                var ids = bundle.Entry
                    .Where(e => e.Resource?.Id is not null && e.Resource.Meta?.Source is { } s && sources.Contains(s))
                    .Select(e => e.Resource!.Id!).ToList();
                if (ids.Count == 0) break;
                await ParaCada(ids, gate, rid => ctx.Escritor.ExcluirAsync(tipo, rid, ct), ct);
            }
        }
    }


    private static string IdDe(string reference) => reference.Split('/')[^1];

    /// <summary>
    /// Reprocesso do CDC de eDoc: para cada movimento tocado no log, relê a ORIGEM e decide —
    /// existe (com BAA) → upsert do DocumentReference + Observations; sumiu → tombstone no hub,
    /// sempre restrito ao meta.source desta base. Não depende da semântica de IN_OPERACAO.
    /// </summary>
    private async Task ProcessarEdocLogAsync(
        LeitorOracleHis oracle, SaluxFhirMapper mapper, ContextoImportacaoPep ctx,
        IReadOnlyList<EdocLogLinha> logs, IReadOnlySet<string> sourcesPurga, SemaphoreSlim gate,
        Progresso.ProgressoImportacao p, Action<long, Exception> falhou, CancellationToken ct)
    {
        var chaves = logs.Select(l => (l.H, l.Ano, l.Idm)).Distinct().ToList();
        var movimentos = (await LerEmLotesTupla(oracle, chaves, SqlEdocsPorChave, MapEdoc, ct))
            .GroupBy(m => m.ChaveDoc).ToDictionary(g => g.Key, g => g.First());
        var chavesVivas = movimentos.Values.Select(e => (e.H, e.Ano, e.Idm)).Distinct().ToList();
        var itensPorDoc = (await LerEmLotesTupla(oracle, chavesVivas, SqlEdocItens, MapEdocItem, ct))
            .GroupBy(i => i.ChaveDoc).ToDictionary(g => g.Key, g => g.ToList());

        // Pacientes dos movimentos vivos: upsert canônico (mesmo caminho — preserva telefone).
        // LOTEADO: um poll traz até TamanhoLoteLog (5.000) linhas de log, que podem render bem
        // mais de 1.000 cd_paciente distintos — e um IN acima de 1.000 estoura ORA-01795, que
        // NÃO é transitório: o run morre, o LogDocumentoId não avança e o retry relê o MESMO lote,
        // travando o CDC para sempre. Por isso o IN é sempre quebrado em lotes.
        var cds = movimentos.Values.Select(m => m.CdPaciente).Where(c => c > 0).Distinct().ToList();
        var map = new ConcurrentDictionary<long, string>();
        foreach (var loteCds in EmLotes(cds, TamanhoLote))
        {
            var linhas = await oracle.LerAsync(SqlPacientes(null, null, null, loteCds), MapPaciente, ct);
            var parcial = await UpsertPacientesChunkAsync(ctx, mapper, linhas, gate, p, falhou, ct);
            foreach (var kv in parcial) map[kv.Key] = kv.Value;
        }

        await ParaCada(chaves, gate, async chaveT =>
        {
            var chave = $"{chaveT.Item1}-{chaveT.Item2}-{chaveT.Item3}";
            try
            {
                if (movimentos.TryGetValue(chave, out var doc))
                {
                    if (doc.Baa is null || !map.TryGetValue(doc.CdPaciente, out var patientRef)) return;
                    // Encounter do BAA: se ainda não existe, o fluxo normal o criará junto do doc.
                    var encBundle = await ctx.Escritor.BuscarPorIdentifierAsync(
                        "Encounter", SaluxFhirMapper.IdentSaluxBaa, mapper.Pref(doc.Baa), ct);
                    var encId = encBundle.Entry.Select(e => e.Resource?.Id).FirstOrDefault(i => i is not null);
                    if (encId is null) return;
                    var encRef = $"Encounter/{encId}";

                    var itens = itensPorDoc.GetValueOrDefault(chave, []);
                    var html = SaluxFhirMapper.MontarHtml(doc.Modelo, itens);
                    await ctx.Escritor.UpsertPorIdentifierAsync(
                        mapper.BuildDocRef(doc, html, patientRef, encRef),
                        SaluxFhirMapper.IdentSaluxEdoc, mapper.Pref(chave), ct);
                    Interlocked.Increment(ref p.DocumentReferences);
                    foreach (var o in mapper.ObservationsDeEdoc(chave, itens, patientRef, encRef, DtIso(doc.Dt)))
                    {
                        var ident = o.Identifier[0];
                        await ctx.Escritor.UpsertPorIdentifierAsync(o, ident.System!, ident.Value!, ct);
                        Interlocked.Increment(ref p.Observations);
                    }
                }
                else
                {
                    await ExcluirDocEObservacoesAsync(ctx, mapper, chave, sourcesPurga, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { falhou(0, ex); }
        }, ct);
    }

    private static readonly string[] TiposVitaisEdoc = ["pa", "fc", "fr", "temp", "spo2"];

    /// <summary>Tombstone de um movimento de eDoc: DocumentReference + Observations derivadas, só do source desta base.</summary>
    private static async Task ExcluirDocEObservacoesAsync(
        ContextoImportacaoPep ctx, SaluxFhirMapper mapper, string chaveDoc, IReadOnlySet<string> sources, CancellationToken ct)
    {
        var pref = mapper.Pref(chaveDoc);

        var docs = await ctx.Escritor.BuscarPorIdentifierAsync("DocumentReference", SaluxFhirMapper.IdentSaluxEdoc, pref, ct);
        foreach (var e in docs.Entry)
            if (e.Resource is DocumentReference dr && dr.Id is not null && dr.Meta?.Source is { } s && sources.Contains(s))
                await ctx.Escritor.ExcluirAsync("DocumentReference", dr.Id, ct);

        foreach (var tipo in TiposVitaisEdoc)
        {
            var obs = await ctx.Escritor.BuscarPorIdentifierAsync("Observation", SaluxFhirMapper.IdentSaluxEdoc, pref + ":" + tipo, ct);
            foreach (var e in obs.Entry)
                if (e.Resource is Observation o && o.Id is not null && o.Meta?.Source is { } s && sources.Contains(s))
                    await ctx.Escritor.ExcluirAsync("Observation", o.Id, ct);
        }
    }

    // ---------------- internação (FIA → Encounter IMP + Location) — ADR-0025 ----------------

    /// <summary>
    /// Carga de referência da estrutura física (setor→quarto→leito) como Locations, na ordem
    /// da hierarquia (partOf resolvido). Roda junto do re-scan de médicos — cadastro muda
    /// raramente; nos demais ciclos o leito é resolvido sob demanda no hub.
    /// </summary>
    private static async Task<int> CargaEstruturaFisicaAsync(
        LeitorOracleHis oracle, SaluxFhirMapper mapper, ContextoImportacaoPep ctx,
        ConcurrentDictionary<string, string?> leitoRefs, CancellationToken ct)
    {
        var unidades = await oracle.LerAsync(SqlUnidades(), MapUnidade, ct);
        var quartos = await oracle.LerAsync(SqlQuartos(), MapQuarto, ct);
        var leitos = await oracle.LerAsync(SqlLeitos(), MapLeito, ct);

        var setorRefs = new Dictionary<string, string>();
        foreach (var u in unidades)
        {
            ct.ThrowIfCancellationRequested();
            var loc = await ctx.Escritor.UpsertPorIdentifierAsync(
                mapper.BuildLocationSetor(u), SaluxFhirMapper.IdentSaluxUnidade, mapper.Pref(u.Chave), ct);
            setorRefs[u.Chave] = $"Location/{loc.Id}";
        }

        var quartoRefs = new Dictionary<string, string>();
        foreach (var q in quartos)
        {
            ct.ThrowIfCancellationRequested();
            var loc = await ctx.Escritor.UpsertPorIdentifierAsync(
                mapper.BuildLocationQuarto(q, setorRefs.GetValueOrDefault(q.ChaveUnidade)),
                SaluxFhirMapper.IdentSaluxQuarto, mapper.Pref(q.Chave), ct);
            quartoRefs[q.Chave] = $"Location/{loc.Id}";
        }

        foreach (var l in leitos)
        {
            ct.ThrowIfCancellationRequested();
            var loc = await ctx.Escritor.UpsertPorIdentifierAsync(
                mapper.BuildLocationLeito(l, quartoRefs.GetValueOrDefault(l.ChaveQuarto)),
                SaluxFhirMapper.IdentSaluxLeito, mapper.Pref(l.Chave), ct);
            leitoRefs[l.Chave] = $"Location/{loc.Id}";
        }

        return unidades.Count + quartos.Count + leitos.Count;
    }

    /// <summary>Referência Location/{id} de um leito, resolvida no hub por identifier e memoizada por run.</summary>
    private static async Task<string?> ResolverLeitoRefAsync(
        ContextoImportacaoPep ctx, SaluxFhirMapper mapper, string chaveLeito,
        ConcurrentDictionary<string, string?> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(chaveLeito, out var pronto)) return pronto;
        var bundle = await ctx.Escritor.BuscarPorIdentifierAsync(
            "Location", SaluxFhirMapper.IdentSaluxLeito, mapper.Pref(chaveLeito), ct);
        var id = bundle.Entry.Select(e => e.Resource?.Id).FirstOrDefault(i => i is not null);
        var referencia = id is null ? null : $"Location/{id}";
        // Cacheia até o null: leito recém-cadastrado no Salux entra no próximo re-scan diário.
        cache[chaveLeito] = referencia;
        return referencia;
    }

    /// <summary>
    /// Internações (FIA) de UM bloco de pacientes → Encounter IMP (+ Condition do CID), via
    /// upsert por identifier — internação em curso é re-lida a cada ciclo e a alta transiciona
    /// in-progress→finished preservando o id lógico. Devolve MAX(GREATEST(dt_baixa, dt_alta)).
    /// </summary>
    private async Task<DateTime?> ProcessarInternacoesAsync(
        LeitorOracleHis oracle, SaluxFhirMapper mapper, ContextoImportacaoPep ctx,
        ConcurrentDictionary<long, string> pacientes, IReadOnlyDictionary<long, string> orgPorHospital, DateTime? sinceFia,
        ConcurrentDictionary<string, string?> leitoRefs, SemaphoreSlim gate,
        Progresso.ProgressoImportacao p, Action<long, Exception> falhou, CancellationToken ct)
    {
        var cds = pacientes.Keys.ToList();
        if (cds.Count == 0) return null;

        var fias = await oracle.LerAsync(SqlFias(cds, sinceFia), MapFia, ct);
        if (fias.Count == 0) return null;

        var chavesFia = fias.Select(f => (f.H, f.Ano, f.Nr)).Distinct().ToList();
        var leitoAtual = (await LerEmLotesTupla(oracle, chavesFia, SqlLeitoAtual, MapFiaLeito, ct))
            .GroupBy(x => x.ChaveFia).ToDictionary(g => g.Key, g => g.First());

        DateTime? maxFia = null;
        foreach (var f in fias)
            maxFia = Max(maxFia, Max(SaluxTempo.ParseUtc(f.DtBaixa), SaluxTempo.ParseUtc(f.DtAlta)));

        var agora = DateTime.UtcNow;
        await ParaCada(fias, gate, async f =>
        {
            if (!pacientes.TryGetValue(f.CdPaciente, out var patientRef)) return;
            try
            {
                var la = leitoAtual.GetValueOrDefault(f.Chave);
                var leitoRef = la is null ? null : await ResolverLeitoRefAsync(ctx, mapper, la.ChaveLeito, leitoRefs, ct);
                var enc = (Encounter)await ctx.Escritor.UpsertPorIdentifierAsync(
                    mapper.BuildEncounterInternacao(f, patientRef, leitoRef, la, agora, orgPorHospital.GetValueOrDefault(f.H)),
                    SaluxFhirMapper.IdentSaluxFia, mapper.Pref(f.Chave), ct);
                Interlocked.Increment(ref p.Encounters);

                if (mapper.BuildConditionFia(f, patientRef, $"Encounter/{enc.Id}") is { } cond)
                {
                    await ctx.Escritor.UpsertPorIdentifierAsync(
                        cond, SaluxFhirMapper.IdentSaluxFia, mapper.Pref(f.Chave) + ":cond", ct);
                    Interlocked.Increment(ref p.Conditions);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { falhou(f.CdPaciente, ex); }
        }, ct);

        return maxFia;
    }

    // ---------------- SQL (paginado por keyset; lê colunas direto — sem JSON_OBJECT) ----------------

    private static string Keyset(string coluna, long? last) => last is { } l ? $"AND {coluna} < {l}" : string.Empty;

    internal static string SqlMedicos(long? last, int tam) => $"""
        SELECT med.cd_medico AS cd, med.nm_medico AS nome, med.nr_crm AS crm, med.uf_cd_uf AS uf,
               med.cd_conselho AS conselho, med.cpf AS cpf, med.cns AS cns, med.nr_rg AS rg,
               med.orgao_emissor AS orgao, TO_CHAR(med.dt_nascimento,'YYYY-MM-DD') AS nasc, med.sexo AS sexo,
               med.ds_email AS email, med.in_ativo AS ativo, med.nm_mae AS mae, med.nm_pai AS pai,
               med.id_categoria AS categoria, med.cd_cbo_smm AS cbo,
               (SELECT LISTAGG(e.ds_especialidade, ', ') WITHIN GROUP (ORDER BY e.ds_especialidade)
                FROM medico_especialidade me JOIN especialidade e ON e.cd_especialidade = me.cd_especialidade
                WHERE me.cd_medico = med.cd_medico) AS especialidade
        FROM (SELECT * FROM medico
              WHERE nr_crm IS NOT NULL AND nm_medico IS NOT NULL AND cpf IS NOT NULL AND dt_exclusao IS NULL {Keyset("cd_medico", last)}
              ORDER BY cd_medico DESC FETCH NEXT {tam} ROWS ONLY) med
        """;

    internal static string SqlPacientes(long? last, int? tam, DateTime? since, IReadOnlyList<long>? cds)
    {
        string filtro;
        string ordemLimite;
        if (cds is { Count: > 0 })
        {
            filtro = $"AND cd_paciente IN ({ListaInt(cds)})";
            ordemLimite = "ORDER BY cd_paciente DESC";
        }
        else
        {
            // OR dt_cadastro: 72% dos pacientes novos nascem com dt_alteracao NULL — sem
            // isso, cadastro novo nunca entrava no incremental (D4, ADR-0024).
            var sinceF = since is { } d
                ? $"AND (dt_alteracao > {Leitura.SaluxTempo.LiteralOracle(d)} OR dt_cadastro > {Leitura.SaluxTempo.LiteralOracle(d)})"
                : string.Empty;
            filtro = $"{Keyset("cd_paciente", last)} {sinceF}";
            ordemLimite = $"ORDER BY cd_paciente DESC FETCH NEXT {tam ?? ChunkPacientes} ROWS ONLY";
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
                   (SELECT bc.ds_barreira_comunicacao FROM barreira_comunicacao bc WHERE TO_CHAR(bc.cd_barreira_comunicacao)=TO_CHAR(pac.cd_barreira_comunicacao) AND ROWNUM=1) AS barreira_ds,
                   TO_CHAR(pac.dt_cadastro,{FmtDt}) AS dt_cadastro, TO_CHAR(pac.dt_alteracao,{FmtDt}) AS dt_alteracao
            FROM (SELECT * FROM paciente
                  WHERE nm_paciente IS NOT NULL AND dt_nascimento IS NOT NULL {filtro}
                  {ordemLimite}) pac
            """;
    }

    /// <summary>
    /// Pacientes com atendimento/internação NOVOS desde as marcas — dirige o incremental
    /// pela origem (quem mudou), em vez de enumerar o hub. UNION já elimina duplicatas.
    /// FIA: internação EM CURSO entra SEMPRE (re-lida a cada ciclo, ADR-0025); sem marca,
    /// o recorte fica na janela de 120 dias — o histórico completo vem pelo modo Completo.
    /// </summary>
    internal static string SqlCdsComAtendimentoNovo(DateTime? sinceBaa, DateTime? sinceEdoc, DateTime? sinceFia)
    {
        var fB = sinceBaa is { } b ? $"WHERE {ClausulaBaaNovoOuEditado("b", b)}" : string.Empty;
        var fE = sinceEdoc is { } e
            ? $"WHERE mov.nr_baa IS NOT NULL AND mov.dt_inclusao > {Leitura.SaluxTempo.LiteralOracle(e)}"
            : "WHERE mov.nr_baa IS NOT NULL";
        var emCurso = "(f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120)";
        var fF = sinceFia is { } fi
            ? $"WHERE f.dt_baixa > {Leitura.SaluxTempo.LiteralOracle(fi)} OR f.dt_alta > {Leitura.SaluxTempo.LiteralOracle(fi)} OR {emCurso}"
            : $"WHERE f.dt_baixa > SYSDATE - 120 OR f.dt_alta > SYSDATE - 120 OR {emCurso}";
        return $"""
            SELECT b.cd_paciente AS cd FROM infosaude.baa b {fB}
            UNION
            SELECT mov.cd_paciente AS cd FROM infosaude.edoc_movimento mov {fE}
            UNION
            SELECT NVL(f.cd_paciente_unificado, f.cd_paciente) AS cd FROM infosaude.fia f {fF}
            """;
    }

    /// <summary>Internações dos pacientes do bloco. Sem marca lê TODAS (backfill via Completo).</summary>
    internal static string SqlFias(IReadOnlyList<long> cds, DateTime? since)
    {
        var filtro = since is { } d
            ? $"AND (f.dt_baixa > {Leitura.SaluxTempo.LiteralOracle(d)} OR f.dt_alta > {Leitura.SaluxTempo.LiteralOracle(d)} OR f.dt_alta IS NULL)"
            : string.Empty;
        return $"""
            SELECT NVL(f.cd_paciente_unificado, f.cd_paciente) AS cd_paciente,
                   f.cd_hospital AS h, f.dt_ano_fia AS ano, f.nr_fia AS nr,
                   TO_CHAR(f.dt_baixa,{FmtDt}) AS dt_baixa, TO_CHAR(f.dt_alta,{FmtDt}) AS dt_alta,
                   TO_CHAR(f.dt_alta_medica,{FmtDt}) AS dt_alta_med, TO_CHAR(f.dt_previsao_alta,{FmtDt}) AS dt_prev_alta,
                   f.cd_cid AS cid,
                   (SELECT ds_cid FROM infosaude.cid WHERE cd_cid = f.cd_cid) AS cid_ds,
                   f.nr_obito AS nr_obito, f.cd_carater_internacao AS carater,
                   (SELECT c.ds_carater_internacao FROM infosaude.carater_internacao_sus c
                    WHERE c.cd_carater_internacao = f.cd_carater_internacao) AS carater_ds
            FROM infosaude.fia f
            WHERE NVL(f.cd_paciente_unificado, f.cd_paciente) IN ({ListaInt(cds)}) {filtro}
            ORDER BY f.cd_hospital, f.dt_ano_fia, f.nr_fia
            """;
    }

    /// <summary>Leito ATUAL de cada FIA (última transferência) — mesma regra do painel.</summary>
    internal static string SqlLeitoAtual(string tuplas) => $"""
        SELECT h, ano, nr, cd_unidade, cd_quarto, cd_leito, dt_transf, dt_saida FROM (
            SELECT fl.cd_hospital AS h, fl.dt_ano_fia AS ano, fl.nr_fia AS nr,
                   fl.cd_unidade AS cd_unidade, fl.cd_quarto AS cd_quarto, fl.cd_leito AS cd_leito,
                   TO_CHAR(fl.dt_transferencia,{FmtDt}) AS dt_transf,
                   TO_CHAR(fl.dt_saida_leito,{FmtDt}) AS dt_saida,
                   ROW_NUMBER() OVER (PARTITION BY fl.cd_hospital, fl.dt_ano_fia, fl.nr_fia
                                      ORDER BY fl.dt_transferencia DESC) AS rn
            FROM infosaude.fia_leito fl
            WHERE (fl.cd_hospital, fl.dt_ano_fia, fl.nr_fia) IN ({tuplas}))
        WHERE rn = 1
        """;

    // ---------------- contagens do diagnóstico origem×hub (só predicados indexáveis/baratos) ----------------

    internal static string SqlContagemBaasPendentes(DateTime since) =>
        $"SELECT COUNT(*) AS n FROM infosaude.baa b WHERE {ClausulaBaaNovoOuEditado("b", since)}";

    internal static string SqlContagemFiasPendentes(DateTime since)
    {
        var lit = Leitura.SaluxTempo.LiteralOracle(since);
        return $"SELECT COUNT(*) AS n FROM infosaude.fia f WHERE f.dt_baixa > {lit} OR f.dt_alta > {lit}";
    }

    internal static string SqlContagemPacientesPendentes(DateTime since)
    {
        var lit = Leitura.SaluxTempo.LiteralOracle(since);
        return $"SELECT COUNT(*) AS n FROM infosaude.paciente WHERE dt_alteracao > {lit} OR dt_cadastro > {lit}";
    }

    internal static string SqlContagemEdocLogPendentes(long aposId) =>
        $"SELECT COUNT(*) AS n FROM infosaude.edoc_movimento_log WHERE id_edoc_movimento_log > {aposId}";

    /// <summary>Âncora inicial do CDC de eDoc (o passado já entra pelo fluxo normal de dt_inclusao).</summary>
    internal static string SqlEdocLogMax() =>
        "SELECT MAX(id_edoc_movimento_log) AS id FROM infosaude.edoc_movimento_log";

    /// <summary>
    /// Poll do log de eDoc por PK sequencial (não há índice por data nas tabelas de log).
    /// Captura edições E exclusões — a semântica exata de IN_OPERACAO não importa: o
    /// reprocesso relê o movimento na origem e decide (existe → upsert; sumiu → tombstone).
    /// </summary>
    internal static string SqlEdocLog(long aposId, int limite) => $"""
        SELECT log.id_edoc_movimento_log AS id, log.cd_hospital AS h, log.ano_movimento AS ano,
               log.id_movimento AS idm, log.cd_paciente AS cd_paciente, log.in_operacao AS op
        FROM infosaude.edoc_movimento_log log
        WHERE log.id_edoc_movimento_log > {aposId}
        ORDER BY log.id_edoc_movimento_log
        FETCH NEXT {limite} ROWS ONLY
        """;

    /// <summary>Movimentos de eDoc por chave (reprocesso do CDC). Só os vinculados a BAA (fase atual).</summary>
    internal static string SqlEdocsPorChave(string tuplas) => $"""
        SELECT mov.cd_paciente AS cd_paciente, mov.cd_hospital AS h, mov.ano_movimento AS ano, mov.id_movimento AS idm,
               (SELECT ds_modelo FROM infosaude.edoc_modelo m WHERE m.cd_modelo = mov.cd_modelo) AS modelo,
               TO_CHAR(mov.dt_episodio,{FmtDt}) AS dt,
               COALESCE(mov.baa_cd_hospital, mov.cd_hospital)||'-'||mov.dt_ano_baa||'-'||mov.nr_baa AS baa,
               TO_CHAR(mov.dt_inclusao,{FmtDt}) AS dt_incl
        FROM infosaude.edoc_movimento mov
        WHERE (mov.cd_hospital, mov.ano_movimento, mov.id_movimento) IN ({tuplas}) AND mov.nr_baa IS NOT NULL
        """;

    /// <summary>
    /// As unidades de saúde da instalação (ADR-0039). São TRÊS no Salux de Maricá, e a tabela
    /// já traz CNES e nome oficial — por isso o conector resolve a unidade sem nenhum de-para
    /// configurado à mão.
    /// </summary>
    internal static string SqlHospitais() => """
        SELECT h.cd_hospital AS cd, h.ds_hospital AS nome, h.nr_cnes AS cnes, h.in_ativo AS ativo
        FROM infosaude.hospital h
        ORDER BY h.cd_hospital
        """;

    internal static string SqlUnidades() => """
        SELECT u.cd_hospital AS h, u.cd_unidade AS cd, u.sc_unidade AS nome, u.id_condicao_unidade AS cond
        FROM infosaude.unidade_hospitalar u
        ORDER BY u.cd_hospital, u.cd_unidade
        """;

    internal static string SqlQuartos() => """
        SELECT q.cd_hospital AS h, q.cd_unidade AS cd_unidade, q.cd_quarto AS cd_quarto,
               q.in_isolamento AS isolamento, q.sexo AS sexo
        FROM infosaude.quarto q
        ORDER BY q.cd_hospital, q.cd_unidade, q.cd_quarto
        """;

    internal static string SqlLeitos() => """
        SELECT l.cd_hospital AS h, l.cd_unidade AS cd_unidade, l.cd_quarto AS cd_quarto, l.cd_leito AS cd_leito,
               l.id_leito AS id_leito, l.id_condicao AS id_condicao, l.id_sit_leito AS id_sit
        FROM infosaude.leito l
        ORDER BY l.cd_hospital, l.cd_unidade, l.cd_quarto, l.cd_leito
        """;

    /// <summary>
    /// Cláusula de BAA novo OU editado desde a marca. Edição via <c>DT_ATUALIZACAO</c>
    /// (100% preenchida no HMCML; 43% dos BAAs são editados &gt;1h depois — ADR-0024),
    /// ancorada no índice de <c>DT_ATENDIMENTO</c> pela janela de 120 dias. Edição de BAA
    /// mais antigo que a janela fica para a rede de segurança via LOG_BAA (pendência A3).
    /// </summary>
    internal static string ClausulaBaaNovoOuEditado(string aliasTabela, DateTime since)
    {
        var lit = Leitura.SaluxTempo.LiteralOracle(since);
        return $"({aliasTabela}.dt_atendimento > {lit} OR ({aliasTabela}.dt_atendimento > SYSDATE - 120 AND {aliasTabela}.dt_atualizacao > {lit}))";
    }

    internal static string SqlBaas(IReadOnlyList<long> cds, DateTime? since)
    {
        var filtroSince = since is { } d ? $"AND {ClausulaBaaNovoOuEditado("b", d)}" : string.Empty;
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
               p.ds_horario AS horario, p.observacao AS obs,
               p.nr_prescricao AS nr_presc, p.seq_item AS seq_item
        FROM infosaude.presc_baa_opc_prod p
        WHERE (p.cd_hospital, p.dt_ano_baa, p.nr_baa) IN ({tuplas})
        ORDER BY p.cd_hospital, p.dt_ano_baa, p.nr_baa, p.nr_prescricao, p.seq_item
        """;

    internal static string SqlEdocs(IReadOnlyList<long> cds, DateTime? since)
    {
        var filtroSince = since is { } d ? $"AND mov.dt_inclusao > {Leitura.SaluxTempo.LiteralOracle(d)}" : string.Empty;
        return $"""
            SELECT mov.cd_paciente AS cd_paciente, mov.cd_hospital AS h, mov.ano_movimento AS ano, mov.id_movimento AS idm,
                   (SELECT ds_modelo FROM infosaude.edoc_modelo m WHERE m.cd_modelo = mov.cd_modelo) AS modelo,
                   TO_CHAR(mov.dt_episodio,{FmtDt}) AS dt,
                   COALESCE(mov.baa_cd_hospital, mov.cd_hospital)||'-'||mov.dt_ano_baa||'-'||mov.nr_baa AS baa,
                   TO_CHAR(mov.dt_inclusao,{FmtDt}) AS dt_incl
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
        Col.Str(r, "estado_civil_ds"), Col.Str(r, "instrucao_ds"), Col.Str(r, "religiao_ds"), Col.Str(r, "barreira_ds"),
        Col.Str(r, "dt_cadastro"), Col.Str(r, "dt_alteracao"));

    private static BaaLinha MapBaa(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "cd_paciente"), Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "nr"),
        Col.Str(r, "dt_cheg"), Col.Str(r, "dt_atend"), Col.Str(r, "dt_saida"), Col.Str(r, "cid"),
        Col.Str(r, "cid_ds"), Col.Str(r, "emerg"), Col.Str(r, "risco_ds"), Col.Str(r, "medico"));

    private static PrescricaoLinha MapPrescricao(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "nr"), Col.Str(r, "cd_mat"), Col.Str(r, "mat"),
        Col.Str(r, "qt"), Col.Str(r, "urg"), Col.Str(r, "medico"), Col.Str(r, "horario"), Col.Str(r, "obs"),
        Col.Long(r, "nr_presc"), Col.Long(r, "seq_item"));

    private static EdocLinha MapEdoc(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "cd_paciente"), Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "idm"),
        Col.Str(r, "modelo"), Col.Str(r, "dt"), Col.Str(r, "baa"), Col.Str(r, "dt_incl"));

    private static EdocItemLinha MapEdocItem(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "idm"), Col.Str(r, "label"), Col.Str(r, "resp"));

    private static FiaLinha MapFia(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "cd_paciente"), Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "nr"),
        Col.Str(r, "dt_baixa"), Col.Str(r, "dt_alta"), Col.Str(r, "dt_alta_med"), Col.Str(r, "dt_prev_alta"),
        Col.Str(r, "cid"), Col.Str(r, "cid_ds"), Col.Str(r, "nr_obito"), Col.Str(r, "carater"), Col.Str(r, "carater_ds"));

    private static FiaLeitoLinha MapFiaLeito(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "nr"), Col.Long(r, "cd_unidade"),
        Col.Str(r, "cd_quarto"), Col.Str(r, "cd_leito"), Col.Str(r, "dt_transf"), Col.Str(r, "dt_saida"));

    private static EdocLogLinha MapEdocLog(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "id"), Col.Long(r, "h"), Col.Long(r, "ano"), Col.Long(r, "idm"),
        Col.Long(r, "cd_paciente"), Col.Str(r, "op"));

    private static HospitalLinha MapHospital(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "cd"), Col.Str(r, "nome"), Col.Str(r, "cnes"), Col.Str(r, "ativo"));

    private static UnidadeLinha MapUnidade(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "h"), Col.Long(r, "cd"), Col.Str(r, "nome"), Col.Str(r, "cond"));

    private static QuartoLinha MapQuarto(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "h"), Col.Long(r, "cd_unidade"), Col.Str(r, "cd_quarto"), Col.Str(r, "isolamento"), Col.Str(r, "sexo"));

    private static LeitoLinha MapLeito(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new(
        Col.Long(r, "h"), Col.Long(r, "cd_unidade"), Col.Str(r, "cd_quarto"), Col.Str(r, "cd_leito"),
        Col.Str(r, "id_leito"), Col.Str(r, "id_condicao"), Col.Str(r, "id_sit"));

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

    /// <summary>Limite do Oracle para expressões numa lista <c>IN</c> — acima disso, ORA-01795.</summary>
    private const int LimiteInOracle = 1000;

    /// <summary>
    /// Lista para cláusula <c>IN</c>. Blindada: acima de <see cref="LimiteInOracle"/> o Oracle
    /// devolve ORA-01795, que não é transitório — o chamador TEM de lotear (ver
    /// <c>EmLotes</c>). Falhar aqui, alto e claro, é melhor que descobrir em produção com o
    /// CDC travado no mesmo lote para sempre.
    /// </summary>
    private static string ListaInt(IReadOnlyList<long> ids)
    {
        if (ids.Count > LimiteInOracle)
        {
            throw new InvalidOperationException(
                $"Lista IN com {ids.Count} itens excede o limite do Oracle ({LimiteInOracle}). " +
                "Lote a chamada com EmLotes(...) antes de montar o SQL.");
        }
        return ids.Count == 0 ? "NULL" : string.Join(",", ids.Select(i => i.ToString(CultureInfo.InvariantCulture)));
    }

    private static string? DtIso(string? v) => Leitura.SaluxTempo.DtIso(v);

    private static DateTime? Max(DateTime? a, DateTime? b) =>
        a is null ? b : b is null ? a : (a > b ? a : b);

    private static long Cronometro() => System.Diagnostics.Stopwatch.GetTimestamp();
    private static double Decorrido(long inicio) => System.Diagnostics.Stopwatch.GetElapsedTime(inicio).TotalSeconds;
}
