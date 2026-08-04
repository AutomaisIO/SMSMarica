using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Pep.Leitura;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Pep.Estrategias.Klinikos;

/// <summary>
/// Importação Klinikos (SQL Server, via agente WSS) → hub FHIR. Atende as duas instâncias —
/// UPA Maricá e PA Santa Rita — pela família <c>klinikos</c> da <c>IaFonte</c>; cada uma roda
/// com o próprio slug, então os códigos internos nunca colidem.
///
/// <para>Desenho, e por que difere do conector do Salux:</para>
/// <list type="bullet">
/// <item><b>Sem cursor.</b> O agente responde requisição a requisição; não há conexão aberta
/// para varrer. Tudo é paginado por keyset sobre <c>rv_atualizacao</c>.</item>
/// <item><b>CDC numérico.</b> O corte é o <c>rv_atualizacao</c> (rowversion): bigint monotônico
/// que o SQL Server incrementa em toda escrita. Não tem fuso, não tem relógio, e nenhum
/// registro escapa porque a origem esqueceu de atualizar uma data.</item>
/// <item><b>O clínico sai da evolução.</b> As tabelas de atendimento estão vazias nesta
/// implantação; CID, nota e prescrição vêm de <c>UPA_Evolucao</c>, por <c>Tipo</c>.</item>
/// </list>
///
/// <para>Mapeamento medido e justificado em <c>docs/klinikos/mapeamento-fhir.md</c>.</para>
/// </summary>
internal sealed class KlinikosImportacaoStrategy(ILogger<KlinikosImportacaoStrategy> logger)
    : IEstrategiaImportacaoPep
{
    public TipoFonte Tipo => TipoFonte.SqlServer;

    public string? Familia => "klinikos";

    /// <summary>Linhas por página do keyset. Bem abaixo do teto do agente — página cheia é sinal de truncamento.</summary>
    private const int TamanhoPagina = 2_000;

    /// <summary>Lote de códigos por <c>IN</c>. O SQL Server admite 2.100 parâmetros; 500 dá folga larga.</summary>
    private const int TamanhoLote = 500;

    private const string FasePaciente = "paciente";
    private const string FaseAtendimento = "atendimento";
    private const string FaseEvolucao = "evolucao";
    private const string FaseSinais = "sinais-vitais";

    /// <summary>Boletim resolvido no hub: as duas referências que todo recurso clínico precisa.</summary>
    private readonly record struct Atendimento(string EncRef, string PacRef);

    public async Task ImportarAsync(ContextoImportacaoPep ctx, CancellationToken ct)
    {
        var consulta = ctx.Consulta
            ?? throw new ValidacaoException("pep.base_sem_agente",
                "O conector do Klinikos lê por agente (ADR-0023) e a base não tem canal de consulta.");

        var p = ctx.Progresso;
        var slug = ctx.BaseSlug;
        var mapper = new KlinikosFhirMapper(slug, $"{KlinikosFhirMapper.SourceBase}/klinikos/{slug}");
        var leitor = new LeitorAgenteSql(consulta, Math.Max(TamanhoPagina * 2, 5_000));
        var incremental = ctx.Opcoes.Modo == ModoSincronizacao.Incremental;

        void Falhou(string chave, Exception ex)
        {
            var msg = ex.Message.Split('\n')[0];
            lock (p.Falhas) { p.Falhas.Add((0, $"{chave}: {msg}")); }
            ctx.Falhas?.Registrar(0, $"{chave}: {msg}");
        }

        p.FaseAtual = "verificando o agente…";
        if (!await consulta.TestarConexaoAsync(ct))
        {
            throw new ValidacaoException("pep.agente_desconectado",
                $"O agente '{slug}' não está conectado. Suba o proxy no servidor da unidade e tente de novo.");
        }

        // Caches do run: código da origem → referência no hub. Evitam reconsultar o hub a cada
        // linha; o incremental de um ciclo é pequeno, então cabem em memória com folga.
        var orgPorUnidade = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pacPorCodigo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var atendPorBoletim = new Dictionary<string, Atendimento>(StringComparer.OrdinalIgnoreCase);

        // ---------- 1. Unidades (ADR-0039) ----------
        // São poucas linhas; upsert integral todo ciclo, sem watermark. Roda ANTES de tudo
        // porque o Encounter precisa da referência da Organization.
        p.FaseAtual = "unidades…";
        foreach (var linha in await leitor.ConsultarAsync(SqlUnidades(), ct))
        {
            if (MapUnidade(linha) is not { } u) continue;
            try
            {
                var salvo = await ctx.Escritor.UpsertPorIdentifierAsync(
                    mapper.BuildOrganization(u), KlinikosFhirMapper.IdentUnidade, mapper.Pref(u.Codigo), ct);
                orgPorUnidade[u.Codigo] = $"Organization/{salvo.Id}";
            }
            catch (Exception ex) { Falhou($"unidade {u.Codigo}", ex); }
        }
        logger.LogInformation("Klinikos {Slug}: {N} unidade(s) resolvida(s).", slug, orgPorUnidade.Count);

        // ---------- 2. Profissionais ----------
        // Varredura INTEGRAL todo ciclo: `profissional` é a única das tabelas de interesse que
        // NÃO tem `rv_atualizacao` — medido, e a razão pela qual "rowversion em quase toda
        // tabela" não vira "em toda tabela" sem conferir. São 495 linhas; varrer todas custa
        // menos que qualquer CDC improvisado sobre uma coluna que não foi feita para isso.
        p.FaseAtual = "profissionais…";
        foreach (var linha in await leitor.ConsultarAsync(SqlProfissionais(), ct))
        {
            if (MapProfissional(linha) is not { } pr) continue;
            try
            {
                await ctx.Escritor.UpsertPorIdentifierAsync(
                    mapper.BuildPractitioner(pr), KlinikosFhirMapper.IdentProfissional, mapper.Pref(pr.Codigo), ct);
                p.Medicos++;
            }
            catch (Exception ex) { Falhou($"profissional {pr.Codigo}", ex); }
        }

        // ---------- 3. Pacientes ----------
        p.FaseAtual = "pacientes…";
        await PaginarAsync(leitor, FasePaciente, ctx, incremental, SqlPacientes, async (linhas, marcar) =>
        {
            foreach (var linha in linhas)
            {
                if (MapPaciente(linha) is not { } pac) continue;
                try
                {
                    pacPorCodigo[pac.Codigo] = await UpsertPacienteAsync(ctx, mapper, pac, ct);
                    p.Pacientes++;
                    if (pac.CpfDigitos.Length == 0) p.PacientesIdentidadeIncompleta++;
                }
                catch (Exception ex) { Falhou($"paciente {pac.Codigo}", ex); }
                marcar(pac.Rv);
            }
        }, ct);

        // ---------- 4. Boletins → Encounter ----------
        p.FaseAtual = "atendimentos…";
        await PaginarAsync(leitor, FaseAtendimento, ctx, incremental, SqlBoletins, async (linhas, marcar) =>
        {
            var boletins = linhas.Select(MapBoletim).OfType<BoletimLinha>().ToList();
            await GarantirPacientesAsync(ctx, mapper, leitor, boletins.Select(b => b.PacCodigo), pacPorCodigo, Falhou, ct);

            foreach (var b in boletins)
            {
                try
                {
                    if (await UpsertBoletimAsync(ctx, mapper, b, orgPorUnidade, pacPorCodigo, ct) is { } a)
                    {
                        atendPorBoletim[b.Codigo] = a;
                        p.Encounters++;
                    }
                    else
                    {
                        Falhou($"boletim {b.Codigo}", new InvalidOperationException(
                            $"paciente {b.PacCodigo ?? "(nulo)"} não resolvido no hub"));
                    }
                }
                catch (Exception ex) { Falhou($"boletim {b.Codigo}", ex); }
                marcar(b.Rv);
            }
        }, ct);

        // ---------- 5. Evoluções → Condition / DocumentReference / MedicationRequest ----------
        p.FaseAtual = "evoluções…";
        await PaginarAsync(leitor, FaseEvolucao, ctx, incremental, SqlEvolucoes, async (linhas, marcar) =>
        {
            var evolucoes = linhas.Select(MapEvolucao).OfType<EvolucaoLinha>().ToList();
            await GarantirAtendimentosAsync(ctx, mapper, leitor, evolucoes.Select(e => e.SpaCodigo),
                orgPorUnidade, pacPorCodigo, atendPorBoletim, Falhou, ct);

            foreach (var e in evolucoes)
            {
                if (e.SpaCodigo is null || !atendPorBoletim.TryGetValue(e.SpaCodigo, out var a))
                {
                    Falhou($"evolução {e.Codigo}", new InvalidOperationException(
                        $"boletim {e.SpaCodigo ?? "(nulo)"} não resolvido no hub"));
                    marcar(e.Rv);
                    continue;
                }
                try { await ProcessarEvolucaoAsync(ctx, mapper, e, a, ct); }
                catch (Exception ex) { Falhou($"evolução {e.Codigo}", ex); }
                marcar(e.Rv);
            }
        }, ct);

        // ---------- 6. Sinais vitais → Observation ----------
        p.FaseAtual = "sinais vitais…";
        await PaginarAsync(leitor, FaseSinais, ctx, incremental, SqlSinaisVitais, async (linhas, marcar) =>
        {
            var vitais = linhas.Select(MapSinaisVitais).OfType<SinaisVitaisLinha>().ToList();
            await GarantirAtendimentosAsync(ctx, mapper, leitor, vitais.Select(v => v.SpaCodigo),
                orgPorUnidade, pacPorCodigo, atendPorBoletim, Falhou, ct);

            foreach (var sv in vitais)
            {
                if (sv.SpaCodigo is null || !atendPorBoletim.TryGetValue(sv.SpaCodigo, out var a))
                {
                    marcar(sv.Rv);
                    continue;   // sinal vital sem boletim resolvido: o boletim virá noutro ciclo
                }
                foreach (var (chave, obs) in mapper.BuildObservacoesVitais(sv, a.PacRef, a.EncRef))
                {
                    try
                    {
                        await ctx.Escritor.UpsertPorIdentifierAsync(
                            obs, KlinikosFhirMapper.IdentSinais, chave, ct);
                        p.Observations++;
                    }
                    catch (Exception ex) { Falhou($"sinal vital {sv.Codigo}", ex); }
                }
                marcar(sv.Rv);
            }
        }, ct);

        p.FaseAtual = "concluído";
    }

    // ================= paginação por rowversion =================

    /// <summary>
    /// Varre uma fase por keyset sobre <c>rv_atualizacao</c>. O ponteiro é persistido ao FIM da
    /// fase — mesma regra do conector do Salux: um run que morre no meio preserva o avanço das
    /// fases anteriores, e nunca o de uma fase pela metade.
    /// </summary>
    private async Task PaginarAsync(
        LeitorAgenteSql leitor,
        string fase,
        ContextoImportacaoPep ctx,
        bool incremental,
        Func<long, int, string> sql,
        Func<IReadOnlyList<LinhaSql>, Action<long>, Task> processar,
        CancellationToken ct)
    {
        var desde = incremental ? ctx.Marca.Ponteiro(fase) : 0;
        var maxVisto = desde;
        var paginas = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var linhas = await leitor.ConsultarAsync(sql(desde, TamanhoPagina), ct);
            if (linhas.Count == 0) break;

            if (leitor.PaginaTruncada(linhas.Count))
            {
                // O agente cortou a resposta no teto: há linhas que não vieram. Avançar o
                // ponteiro aqui pularia justamente essas, em silêncio e para sempre.
                throw new ValidacaoException("pep.pagina_truncada",
                    $"Fase '{fase}': o agente devolveu {linhas.Count} linhas, no teto dele. "
                    + "Reduza o tamanho da página ou aumente Pep:Agente:MaxLinhas.");
            }

            await processar(linhas, rv => { if (rv > maxVisto) maxVisto = rv; });
            paginas++;

            // Origem não avançou o rowversion (linha sem rv, ou tudo já visto): sair evita
            // reler a mesma página para sempre.
            if (maxVisto <= desde) break;
            desde = maxVisto;
            if (linhas.Count < TamanhoPagina) break;
        }

        ct.ThrowIfCancellationRequested();
        ctx.Marca.AvancarPonteiro(fase, maxVisto);
        if (ctx.SalvarMarca is not null) await ctx.SalvarMarca(ctx.Marca, ct);
        logger.LogInformation("Klinikos: fase '{Fase}' em {N} página(s); ponteiro {Rv}.", fase, paginas, maxVisto);
    }

    // ================= paciente =================

    /// <summary>
    /// Paciente COM CPF entra pelo caminho canônico (dedup nacional pelo CPF); SEM CPF entra
    /// pelo identifier local da base, já marcado pelo mapper. São 16,9% do cadastro da UPA —
    /// pessoas reais, todas com atendimento (ver <c>docs/klinikos/mapeamento-fhir.md §4</c>).
    /// </summary>
    private static async Task<string> UpsertPacienteAsync(
        ContextoImportacaoPep ctx, KlinikosFhirMapper mapper, PacienteLinha pac, CancellationToken ct)
    {
        var recurso = mapper.BuildPatient(pac);
        var cpf = pac.CpfDigitos;
        var salvo = cpf.Length > 0
            ? await ctx.Escritor.UpsertPorIdentifierAsync(recurso, KlinikosFhirMapper.IdentCpf, cpf, ct)
            : await ctx.Escritor.UpsertPorIdentifierAsync(recurso, KlinikosFhirMapper.IdentPaciente, mapper.Pref(pac.Codigo), ct);
        return $"Patient/{salvo.Id}";
    }

    /// <summary>
    /// Traz da origem os pacientes citados mas ainda não resolvidos neste run. No incremental um
    /// boletim novo é quase sempre de um paciente ANTIGO — cuja linha de cadastro não mudou, e
    /// portanto não veio na fase de pacientes.
    /// </summary>
    private static async Task GarantirPacientesAsync(
        ContextoImportacaoPep ctx, KlinikosFhirMapper mapper, LeitorAgenteSql leitor,
        IEnumerable<string?> codigos, Dictionary<string, string> cache,
        Action<string, Exception> falhou, CancellationToken ct)
    {
        var faltantes = codigos.OfType<string>()
            .Where(c => !cache.ContainsKey(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (faltantes.Count == 0) return;

        foreach (var lote in EmLotes(faltantes, TamanhoLote))
        {
            foreach (var linha in await leitor.ConsultarAsync(SqlPacientesPorCodigo(lote), ct))
            {
                if (MapPaciente(linha) is not { } pac) continue;
                try { cache[pac.Codigo] = await UpsertPacienteAsync(ctx, mapper, pac, ct); }
                catch (Exception ex) { falhou($"paciente {pac.Codigo}", ex); }
            }
        }
    }

    // ================= boletim =================

    private static async Task<Atendimento?> UpsertBoletimAsync(
        ContextoImportacaoPep ctx, KlinikosFhirMapper mapper, BoletimLinha b,
        IReadOnlyDictionary<string, string> orgPorUnidade,
        IReadOnlyDictionary<string, string> pacPorCodigo,
        CancellationToken ct)
    {
        if (b.PacCodigo is null || !pacPorCodigo.TryGetValue(b.PacCodigo, out var pacRef)) return null;

        var orgRef = b.UnidCodigo is not null ? orgPorUnidade.GetValueOrDefault(b.UnidCodigo) : null;

        // Todo boletim vira Encounter, inclusive o de quem desistiu antes de ser atendido: a
        // pessoa esteve na unidade, e isso é informação clínica. O status é refinado pela fase
        // de evoluções; presumir "atendido" acerta em 92,3% dos casos.
        var enc = mapper.BuildEncounter(b, pacRef, orgRef, teveAtendimento: true);
        var salvo = await ctx.Escritor.UpsertPorIdentifierAsync(
            enc, KlinikosFhirMapper.IdentBoletim, mapper.Pref(b.Codigo), ct);

        return new Atendimento($"Encounter/{salvo.Id}", pacRef);
    }

    /// <summary>
    /// Resolve boletins citados por evolução/sinal vital que ainda não estão no cache do run —
    /// é o caso normal no incremental: uma reavaliação de hoje pendura num boletim de ontem.
    /// </summary>
    private static async Task GarantirAtendimentosAsync(
        ContextoImportacaoPep ctx, KlinikosFhirMapper mapper, LeitorAgenteSql leitor,
        IEnumerable<string?> boletins,
        IReadOnlyDictionary<string, string> orgPorUnidade,
        Dictionary<string, string> pacPorCodigo,
        Dictionary<string, Atendimento> cache,
        Action<string, Exception> falhou, CancellationToken ct)
    {
        var faltantes = boletins.OfType<string>()
            .Where(c => !cache.ContainsKey(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (faltantes.Count == 0) return;

        foreach (var lote in EmLotes(faltantes, TamanhoLote))
        {
            var linhas = await leitor.ConsultarAsync(SqlBoletinsPorCodigo(lote), ct);
            var achados = linhas.Select(MapBoletim).OfType<BoletimLinha>().ToList();

            await GarantirPacientesAsync(ctx, mapper, leitor, achados.Select(b => b.PacCodigo),
                pacPorCodigo, falhou, ct);

            foreach (var b in achados)
            {
                try
                {
                    if (await UpsertBoletimAsync(ctx, mapper, b, orgPorUnidade, pacPorCodigo, ct) is { } a)
                        cache[b.Codigo] = a;
                }
                catch (Exception ex) { falhou($"boletim {b.Codigo}", ex); }
            }
        }
    }

    // ================= evolução =================

    private static async Task ProcessarEvolucaoAsync(
        ContextoImportacaoPep ctx, KlinikosFhirMapper mapper, EvolucaoLinha e, Atendimento a,
        CancellationToken ct)
    {
        // ESTORNO é anulação: importá-lo colocaria no prontuário um registro que a própria
        // origem considera cancelado.
        if (e.TipoNorm == "ESTORNO") return;

        var p = ctx.Progresso;
        var chave = mapper.Pref(e.Codigo.ToString(CultureInfo.InvariantCulture));

        // O CID vem junto da nota, não de uma tabela de diagnóstico. A Condition é do BOLETIM
        // (identifier por boletim, não por evolução): a reavaliação REVISA o diagnóstico da
        // mesma passagem — uma Condition por evolução encheria o prontuário de diagnósticos
        // concorrentes para um atendimento só. Como as páginas vêm em ordem de rowversion, a
        // última revisão processada é a que prevalece.
        if (e.SpaCodigo is { } boletim
            && mapper.BuildCondition(boletim, e.CidPrimario, a.PacRef, a.EncRef) is { } cond)
        {
            await ctx.Escritor.UpsertPorIdentifierAsync(
                cond, KlinikosFhirMapper.IdentBoletim, mapper.Pref(boletim) + ":cond", ct);
            p.Conditions++;
        }

        switch (e.TipoNorm)
        {
            case "RECEITA":
            case "PRESCRICAO":
                await ctx.Escritor.UpsertPorIdentifierAsync(
                    mapper.BuildMedicationRequest(e, a.PacRef, a.EncRef),
                    KlinikosFhirMapper.IdentEvolucao, chave + ":med", ct);
                p.MedicationRequests++;
                break;

            // Entradas de sala não têm texto útil: são marco de jornada, e viram statusHistory
            // do Encounter no dia em que o hub aceitar atualização parcial. Guardar uma
            // DocumentReference vazia para elas só sujaria o prontuário.
            case "ENTRADA NA SALA AMARELA":
            case "ENTRADA NA SALA VERMELHA":
                break;

            default:
                await ctx.Escritor.UpsertPorIdentifierAsync(
                    mapper.BuildDocRef(e, a.PacRef, a.EncRef),
                    KlinikosFhirMapper.IdentEvolucao, chave, ct);
                p.DocumentReferences++;
                break;
        }
    }

    // ================= SQL =================

    /// <summary>
    /// <c>unidade</c>: o CNES está em <c>unid_codigoCNES</c> — NÃO em
    /// <c>unidade_municipioCNES</c>, que existe e está vazia.
    /// </summary>
    internal static string SqlUnidades() => """
        SELECT unid_codigo, unid_descricao, Unid_nome_fantasia, unid_sigla,
               unid_codigoCNES, unid_telefone, unid_email
          FROM unidade
        """;

    /// <summary>
    /// <c>profissional</c> NÃO tem <c>rv_atualizacao</c> — é a exceção entre as tabelas que o
    /// conector lê. Varredura integral (495 linhas na UPA).
    /// </summary>
    internal static string SqlProfissionais() => """
        SELECT PROF_CODIGO, PROF_NOME, PROF_CPF, PROF_CNS, PROF_NUMCONSELHO,
               CBO_CODIGO, PROF_ATIVO
          FROM profissional
        """;

    private const string ColunasPaciente =
        """
        pac_codigo, pac_nome, pac_cpf, pac_cartao_nsaude, pac_nascimento, pac_sexo,
               pac_mae, pac_pai, pac_telefone, pac_celular, pac_email, pac_dtobito,
               pac_responsavel, pac_telefone_responsavel, pac_raca,
               CONVERT(BIGINT, rv_atualizacao) AS rv
        """;

    internal static string SqlPacientes(long desde, int top) => $"""
        SELECT TOP {top} {ColunasPaciente}
          FROM paciente
         WHERE CONVERT(BIGINT, rv_atualizacao) > {desde}
           AND pac_nome IS NOT NULL
         ORDER BY CONVERT(BIGINT, rv_atualizacao)
        """;

    internal static string SqlPacientesPorCodigo(IReadOnlyList<string> codigos) => $"""
        SELECT {ColunasPaciente}
          FROM paciente
         WHERE pac_codigo IN ({ListaTexto(codigos)})
        """;

    private const string ColunasBoletim =
        """
        spa_codigo, pac_codigo, unid_codigo, spa_chegada, spa_dt_boletim,
               spa_nomesocial, spa_cartao_nsaude, spa_forma_chegada, risaco_codigo,
               CONVERT(BIGINT, rv_atualizacao) AS rv
        """;

    internal static string SqlBoletins(long desde, int top) => $"""
        SELECT TOP {top} {ColunasBoletim}
          FROM Pronto_Atendimento
         WHERE CONVERT(BIGINT, rv_atualizacao) > {desde}
           AND pac_codigo IS NOT NULL
         ORDER BY CONVERT(BIGINT, rv_atualizacao)
        """;

    internal static string SqlBoletinsPorCodigo(IReadOnlyList<string> codigos) => $"""
        SELECT {ColunasBoletim}
          FROM Pronto_Atendimento
         WHERE spa_codigo IN ({ListaTexto(codigos)})
           AND pac_codigo IS NOT NULL
        """;

    internal static string SqlEvolucoes(long desde, int top) => $"""
        SELECT TOP {top} upaevo_codigo, SPA_CODIGO, Tipo, upaevo_datahora, upaevo_descricao,
               prof_codigo, cid_codigo_primario, cid_codigo_secundario,
               CONVERT(BIGINT, rv_atualizacao) AS rv
          FROM UPA_Evolucao
         WHERE CONVERT(BIGINT, rv_atualizacao) > {desde}
           AND SPA_CODIGO IS NOT NULL
         ORDER BY CONVERT(BIGINT, rv_atualizacao)
        """;

    internal static string SqlSinaisVitais(long desde, int top) => $"""
        SELECT TOP {top} sv_codigo, spa_codigo, data, prof_codigo, pressaoarterial, pulso,
               temperatura, frequenciarespiratoria, hgt, saturacaoO2, peso,
               CONVERT(BIGINT, rv_atualizacao) AS rv
          FROM UPA_SinaisVitais
         WHERE CONVERT(BIGINT, rv_atualizacao) > {desde}
           AND spa_codigo IS NOT NULL
         ORDER BY CONVERT(BIGINT, rv_atualizacao)
        """;

    /// <summary>
    /// Lista literal para <c>IN</c>. Os códigos do Klinikos são alfanuméricos de tamanho fixo
    /// (<c>char</c>), então vão como texto — com aspas escapadas, e com um teto: o SQL Server
    /// admite 2.100 parâmetros, e um <c>IN</c> gigante quebraria a fase inteira em produção.
    /// </summary>
    internal static string ListaTexto(IReadOnlyList<string> valores)
    {
        if (valores.Count == 0) return "''";
        if (valores.Count > 1_000)
            throw new ArgumentException(
                $"Lote de {valores.Count} códigos excede o limite seguro de IN do SQL Server.", nameof(valores));
        return string.Join(",", valores.Select(v => "'" + v.Replace("'", "''", StringComparison.Ordinal) + "'"));
    }

    internal static IEnumerable<IReadOnlyList<T>> EmLotes<T>(IReadOnlyList<T> itens, int tamanho)
    {
        for (var i = 0; i < itens.Count; i += tamanho)
            yield return [.. itens.Skip(i).Take(tamanho)];
    }

    // ================= mapeamento de linha =================

    private static UnidadeLinha? MapUnidade(LinhaSql l) =>
        l.Texto("unid_codigo") is { } cod
            ? new UnidadeLinha(cod, l.Texto("unid_descricao"), l.Texto("Unid_nome_fantasia"),
                l.Texto("unid_sigla"), l.Texto("unid_codigoCNES"), l.Texto("unid_telefone"),
                l.Texto("unid_email"))
            : null;

    private static ProfissionalLinha? MapProfissional(LinhaSql l) =>
        l.Texto("PROF_CODIGO") is { } cod
            ? new ProfissionalLinha(cod, l.Texto("PROF_NOME"), l.Texto("PROF_CPF"), l.Texto("PROF_CNS"),
                l.Texto("PROF_NUMCONSELHO"), l.Texto("CBO_CODIGO"), l.Texto("PROF_ATIVO"), 0)
            : null;

    private static PacienteLinha? MapPaciente(LinhaSql l) =>
        l.Texto("pac_codigo") is { } cod
            ? new PacienteLinha(cod, l.Texto("pac_nome"), l.Texto("pac_cpf"),
                l.Texto("pac_cartao_nsaude"), l.DataHora("pac_nascimento"), l.Texto("pac_sexo"),
                l.Texto("pac_mae"), l.Texto("pac_pai"), l.Texto("pac_telefone"), l.Texto("pac_celular"),
                l.Texto("pac_email"), l.DataHora("pac_dtobito"), l.Texto("pac_responsavel"),
                l.Texto("pac_telefone_responsavel"), l.Texto("pac_raca"), l.Numero("rv") ?? 0)
            : null;

    private static BoletimLinha? MapBoletim(LinhaSql l) =>
        l.Texto("spa_codigo") is { } cod
            ? new BoletimLinha(cod, l.Texto("pac_codigo"), l.Texto("unid_codigo"),
                l.DataHora("spa_chegada"), l.DataHora("spa_dt_boletim"), l.Texto("spa_nomesocial"),
                l.Texto("spa_cartao_nsaude"), l.Texto("spa_forma_chegada"), l.Texto("risaco_codigo"),
                l.Numero("rv") ?? 0)
            : null;

    private static EvolucaoLinha? MapEvolucao(LinhaSql l) =>
        l.Numero("upaevo_codigo") is { } cod
            ? new EvolucaoLinha(cod, l.Texto("SPA_CODIGO"), l.Texto("Tipo"),
                l.DataHora("upaevo_datahora"), l.Texto("upaevo_descricao"), l.Texto("prof_codigo"),
                l.Texto("cid_codigo_primario"), l.Texto("cid_codigo_secundario"), l.Numero("rv") ?? 0)
            : null;

    private static SinaisVitaisLinha? MapSinaisVitais(LinhaSql l) =>
        l.Numero("sv_codigo") is { } cod
            ? new SinaisVitaisLinha(cod, l.Texto("spa_codigo"), l.DataHora("data"), l.Texto("prof_codigo"),
                l.Texto("pressaoarterial"), l.Texto("pulso"), l.Texto("temperatura"),
                l.Texto("frequenciarespiratoria"), l.Texto("hgt"), l.Texto("saturacaoO2"),
                l.Texto("peso"), l.Numero("rv") ?? 0)
            : null;
}
