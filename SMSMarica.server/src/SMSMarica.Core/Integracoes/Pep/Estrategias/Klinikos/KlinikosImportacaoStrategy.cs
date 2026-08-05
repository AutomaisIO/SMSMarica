using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Pep.Fhir;
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
/// <para><b>Identidade passa pelo canônico</b> (<see cref="UpsertCanonicoPep"/>): paciente e
/// profissional com CPF são amarrados pela chave nacional — a mesma pessoa vista pelo Salux e
/// pelo Klinikos converge para UM recurso, com merge que preserva telefone verificado e
/// conciliação de nascimento divergente. Sem CPF, o paciente entra pela chave local, marcado
/// (ADR-0041), e NUNCA entra no merge — dois registros sabidamente separados valem mais que um
/// unificado no chute. A primeira versão deste conector usou o atalho dos recursos clínicos
/// (<c>PUT ?identifier=</c>) e o hub respondeu 405 em 606 mil upserts de paciente: aquele
/// caminho é bloqueado para identidade DE PROPÓSITO.</para>
///
/// <para><b>A marca d'água é fail-closed</b> (<see cref="FasePonteiro"/>): escrita que falha
/// segura o ponteiro da fase no registro anterior à falha. No incidente de 04/08 o ponteiro
/// avançou até o fim da base com ZERO registros gravados — 1,43 milhão de linhas ficaram
/// invisíveis ao incremental. Re-varrer é barato (upsert é idempotente); pular dado clínico em
/// silêncio é permanente.</para>
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

    private const string TipoInicioAtendimento = "INICIO DO ATENDIMENTO MEDICO";

    /// <summary>Boletim resolvido no hub: as referências que todo recurso clínico precisa.</summary>
    private readonly record struct Atendimento(string EncRef, string PacRef, bool TeveAtendimento);

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

        void Falhou(string chave, long cd, Exception ex)
        {
            var msg = ex.Message.Split('\n')[0];
            p.RegistrarFalha(cd, $"{chave}: {msg}");
            ctx.Falhas?.Registrar(cd, $"{chave}: {msg}");
        }

        // Opções que este conector ainda NÃO implementa são REJEITADAS, não ignoradas: um
        // "apagar antes" silenciosamente pulado deixaria o operador certo de que purgou — e o
        // reimport por códigos assumiria formato de cd que esta base não tem (char com zeros à
        // esquerda). Falhar alto aqui é o que evita a próxima surpresa em produção.
        if (ctx.Opcoes.ApagarAntes)
            throw new ValidacaoException("pep.opcao_nao_suportada",
                "\"Apagar antes\" ainda não é suportado para bases Klinikos.");
        // Reimport DIRECIONADO: processa exatamente estes pacientes. É o caminho pelo qual a
        // arbitragem de identidade devolve ao hub um paciente cuja origem foi declarada correta.
        // O código vem como TEXTO porque aqui ele é `char` com zeros à esquerda — tratá-lo como
        // número perderia os zeros e apontaria para outro paciente (ou para nenhum).
        var direcionado = ctx.Opcoes.CodigosPacientes is { Count: > 0 }
            ? [.. ctx.Opcoes.CodigosPacientes]
            : new List<string>();
        if (ctx.Opcoes.CdsPacientes is { Count: > 0 })
            throw new ValidacaoException("pep.codigo_numerico",
                "Esta base identifica paciente por código de texto (com zeros à esquerda). "
                + "Use CodigosPacientes — CdsPacientes numérico apontaria para outro paciente.");

        // Escopo LIMITADO = ensaio: importa até N pacientes e SÓ o clínico deles, e não move
        // NENHUM ponteiro — um teste não pode deixar marca que faça o incremental pular dado.
        var limitado = ctx.Opcoes.Escopo == EscopoSincronizacao.Limitado;
        var maxPacientes = limitado ? Math.Max(ctx.Opcoes.MaxPacientes ?? 0, 0) : int.MaxValue;
        var maxMedicos = limitado ? Math.Max(ctx.Opcoes.MaxMedicos ?? 0, 0) : int.MaxValue;
        if (limitado && maxPacientes == 0 && maxMedicos == 0)
            throw new ValidacaoException("pep.limites",
                "No escopo Limitado informe ao menos um limite (médicos e/ou pacientes).");

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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Falhou($"unidade {u.Codigo}", Cd(u.Codigo), ex);
            }
        }
        logger.LogInformation("Klinikos {Slug}: {N} unidade(s) resolvida(s).", slug, orgPorUnidade.Count);

        // ---------- 2. Profissionais ----------
        // Varredura INTEGRAL todo ciclo: `profissional` é a única das tabelas de interesse que
        // NÃO tem `rv_atualizacao` — medido, e a razão pela qual "rowversion em quase toda
        // tabela" não vira "em toda tabela" sem conferir. São 495 linhas.
        //
        // Profissional SEM CPF não entra — mesma régua do conector do Salux: Practitioner é
        // canônico por chave nacional, e sem ela não há como afirmar que o "João" de uma base
        // é o da outra. São 31 de 495 na UPA; nenhum recurso da Fase 1 os referencia.
        p.FaseAtual = "profissionais…";
        var profSemCpf = 0;
        foreach (var linha in await leitor.ConsultarAsync(SqlProfissionais(), ct))
        {
            if (p.Medicos >= maxMedicos) break;
            if (MapProfissional(linha) is not { } pr) continue;
            var cpfProf = Digitos(pr.Cpf);
            if (pr.Nome is null || !CpfPep.Valido(cpfProf)) { profSemCpf++; continue; }
            try
            {
                await UpsertCanonicoPep.UpsertAsync(
                    ctx, "Practitioner", UpsertCanonicoPep.SysCpf, cpfProf,
                    mapper.BuildPractitioner(pr), KlinikosFhirMapper.IdentProfissional, ct);
                p.Medicos++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Falhou($"profissional {pr.Codigo}", Cd(pr.Codigo), ex);
            }
        }
        if (profSemCpf > 0)
            logger.LogInformation("Klinikos {Slug}: {N} profissional(is) sem CPF ficaram fora (regra canônica).", slug, profSemCpf);

        // ---------- 3. Pacientes ----------
        p.FaseAtual = "pacientes…";
        if (direcionado.Count > 0)
        {
            // Lista explícita: nenhum ponteiro se move — é reparo pontual, fora do fluxo do CDC.
            await GarantirPacientesAsync(ctx, mapper, leitor, direcionado, pacPorCodigo, Falhou, ct);
            p.Pacientes += pacPorCodigo.Count;

            p.FaseAtual = "atendimentos…";
            foreach (var lote in EmLotes(direcionado, TamanhoLote))
            {
                var achados = (await leitor.ConsultarAsync(SqlBoletinsDePacientes(lote), ct))
                    .Select(MapBoletim).OfType<BoletimLinha>().ToList();
                var comAtd = await CarregarFlagsAtendimentoAsync(leitor, achados.Select(b => b.Codigo), ct);
                foreach (var b in achados)
                {
                    try
                    {
                        if (await UpsertBoletimAsync(ctx, mapper, b, comAtd.Contains(b.Codigo),
                                orgPorUnidade, pacPorCodigo, ct) is { } a)
                        {
                            atendPorBoletim[b.Codigo] = a;
                            p.Encounters++;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Falhou($"boletim {b.Codigo}", Cd(b.PacCodigo), ex);
                    }
                }
            }

            p.FaseAtual = "concluído";
            return;
        }

        await PaginarAsync(leitor, FasePaciente, ctx, incremental, SqlPacientes, async (linhas, fase) =>
        {
            foreach (var linha in linhas)
            {
                if (p.Pacientes >= maxPacientes) break;
                if (MapPaciente(linha) is not { } pac) continue;
                fase.Visto(pac.Rv);
                try
                {
                    pacPorCodigo[pac.Codigo] = await UpsertPacienteAsync(ctx, mapper, pac, ct);
                    p.Pacientes++;
                    if (!CpfPep.Valido(pac.Cpf)) p.PacientesIdentidadeIncompleta++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    fase.Falhou(pac.Rv);
                    Falhou($"paciente {pac.Codigo}", Cd(pac.Codigo), ex);
                }
            }
        }, ct, persistirPonteiro: !limitado, pararQuando: () => p.Pacientes >= maxPacientes);

        // ---------- 4. Boletins → Encounter ----------
        p.FaseAtual = "atendimentos…";
        await PaginarAsync(leitor, FaseAtendimento, ctx, incremental, SqlBoletins, async (linhas, fase) =>
        {
            var boletins = linhas.Select(MapBoletim).OfType<BoletimLinha>().ToList();
            if (limitado)
            {
                // Ensaio: só o clínico dos pacientes que o ensaio importou — os demais NÃO são
                // falha, estão fora do escopo; e o ponteiro não anda, então nada é pulado.
                boletins = [.. boletins.Where(b => b.PacCodigo is { } pc && pacPorCodigo.ContainsKey(pc))];
            }
            var comAtendimento = await CarregarFlagsAtendimentoAsync(
                leitor, boletins.Select(b => b.Codigo), ct);
            if (!limitado)
            {
                await GarantirPacientesAsync(ctx, mapper, leitor, boletins.Select(b => b.PacCodigo),
                    pacPorCodigo, Falhou, ct);
            }

            foreach (var b in boletins)
            {
                fase.Visto(b.Rv);
                try
                {
                    if (await UpsertBoletimAsync(ctx, mapper, b, comAtendimento.Contains(b.Codigo),
                            orgPorUnidade, pacPorCodigo, ct) is { } a)
                    {
                        atendPorBoletim[b.Codigo] = a;
                        p.Encounters++;
                    }
                    else
                    {
                        fase.Falhou(b.Rv);
                        Falhou($"boletim {b.Codigo}", Cd(b.PacCodigo),
                            new InvalidOperationException($"paciente {b.PacCodigo ?? "(nulo)"} não resolvido no hub"));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    fase.Falhou(b.Rv);
                    Falhou($"boletim {b.Codigo}", Cd(b.PacCodigo), ex);
                }
            }
        }, ct, persistirPonteiro: !limitado);

        // ---------- 5. Evoluções → Condition / DocumentReference / MedicationRequest ----------
        p.FaseAtual = "evoluções…";
        // CID por boletim: a evolução clinicamente mais RECENTE (datahora) vence. Sem esta
        // guarda, a EDIÇÃO de uma evolução antiga (rowversion novo, datahora velha) regrediria
        // o diagnóstico já revisado.
        var cidDataPorBoletim = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await PaginarAsync(leitor, FaseEvolucao, ctx, incremental, SqlEvolucoes, async (linhas, fase) =>
        {
            var evolucoes = linhas.Select(MapEvolucao).OfType<EvolucaoLinha>().ToList();
            if (limitado)
                evolucoes = [.. evolucoes.Where(ev => ev.SpaCodigo is { } sp && atendPorBoletim.ContainsKey(sp))];

            // Evolução que chega agora pode pertencer a um boletim importado num ciclo
            // ANTERIOR como "não atendido" (o paciente ainda esperava quando o boletim subiu).
            // Evict do cache força o re-fetch abaixo, que recalcula a flag — agora verdadeira —
            // e re-upserta o Encounter como atendido. Sem isso, o status errado seria permanente.
            foreach (var e in evolucoes)
            {
                if (e.TipoNorm != "ESTORNO" && e.SpaCodigo is { } spa
                    && atendPorBoletim.TryGetValue(spa, out var atd) && !atd.TeveAtendimento)
                {
                    atendPorBoletim.Remove(spa);
                }
            }

            if (!limitado)
            {
                await GarantirAtendimentosAsync(ctx, mapper, leitor, evolucoes.Select(e => e.SpaCodigo),
                    orgPorUnidade, pacPorCodigo, atendPorBoletim, Falhou, ct);
            }

            foreach (var e in evolucoes)
            {
                fase.Visto(e.Rv);
                if (e.SpaCodigo is null || !atendPorBoletim.TryGetValue(e.SpaCodigo, out var a))
                {
                    fase.Falhou(e.Rv);
                    Falhou($"evolução {e.Codigo}", e.Codigo,
                        new InvalidOperationException($"boletim {e.SpaCodigo ?? "(nulo)"} não resolvido no hub"));
                    continue;
                }
                try { await ProcessarEvolucaoAsync(ctx, mapper, e, a, cidDataPorBoletim, ct); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    fase.Falhou(e.Rv);
                    Falhou($"evolução {e.Codigo}", e.Codigo, ex);
                }
            }
        }, ct, persistirPonteiro: !limitado);

        // ---------- 6. Sinais vitais → Observation ----------
        p.FaseAtual = "sinais vitais…";
        await PaginarAsync(leitor, FaseSinais, ctx, incremental, SqlSinaisVitais, async (linhas, fase) =>
        {
            var vitais = linhas.Select(MapSinaisVitais).OfType<SinaisVitaisLinha>().ToList();
            if (limitado)
            {
                vitais = [.. vitais.Where(v => v.SpaCodigo is { } sp && atendPorBoletim.ContainsKey(sp))];
            }
            else
            {
                await GarantirAtendimentosAsync(ctx, mapper, leitor, vitais.Select(v => v.SpaCodigo),
                    orgPorUnidade, pacPorCodigo, atendPorBoletim, Falhou, ct);
            }

            foreach (var sv in vitais)
            {
                fase.Visto(sv.Rv);
                if (sv.SpaCodigo is null || !atendPorBoletim.TryGetValue(sv.SpaCodigo, out var a))
                {
                    // Sem boletim resolvido o ponteiro NÃO passa por cima: o boletim pode
                    // chegar no próximo ciclo, e a medida tem de vir junto.
                    fase.Falhou(sv.Rv);
                    Falhou($"sinal vital {sv.Codigo}", sv.Codigo,
                        new InvalidOperationException($"boletim {sv.SpaCodigo ?? "(nulo)"} não resolvido no hub"));
                    continue;
                }
                foreach (var (chave, obs) in mapper.BuildObservacoesVitais(sv, a.PacRef, a.EncRef))
                {
                    try
                    {
                        await ctx.Escritor.UpsertPorIdentifierAsync(
                            obs, KlinikosFhirMapper.IdentSinais, chave, ct);
                        p.Observations++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        fase.Falhou(sv.Rv);
                        Falhou($"sinal vital {sv.Codigo}", sv.Codigo, ex);
                    }
                }
            }
        }, ct, persistirPonteiro: !limitado);

        p.FaseAtual = "concluído";
    }

    // ================= ponteiro fail-closed =================

    /// <summary>
    /// Ponteiro de UMA fase durante o run. A regra que o incidente de 04/08 tornou inegociável:
    /// <b>escrita que falhou segura a marca</b>. O ponteiro persistido é
    /// <c>min(menor rv que falhou − 1, maior rv visto)</c> — a fase re-varre a partir da
    /// primeira falha no próximo ciclo, e re-varrer é barato porque todo upsert é idempotente.
    ///
    /// <para>Um registro permanentemente quebrado ("veneno") trava o ponteiro da fase e força
    /// re-varredura a cada ciclo. É o comportamento CERTO: barulhento, visível na trilha de
    /// falhas, e ninguém perde dado — o oposto do run que "concluiu" pulando 1,43 milhão de
    /// linhas em silêncio.</para>
    /// </summary>
    internal sealed class FasePonteiro
    {
        public long MaxVisto { get; private set; }
        public long? MenorRvFalho { get; private set; }

        public void Visto(long rv) { if (rv > MaxVisto) MaxVisto = rv; }

        public void Falhou(long rv) { if (MenorRvFalho is null || rv < MenorRvFalho) MenorRvFalho = rv; }

        /// <summary>Até onde é SEGURO afirmar "tudo processado": nunca além de uma falha.</summary>
        public long PonteiroSeguro => MenorRvFalho is { } f ? Math.Min(f - 1, MaxVisto) : MaxVisto;
    }

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
        Func<IReadOnlyList<LinhaSql>, FasePonteiro, Task> processar,
        CancellationToken ct,
        bool persistirPonteiro = true,
        Func<bool>? pararQuando = null)
    {
        var desde = incremental ? ctx.Marca.Ponteiro(fase) : 0;
        var ponteiro = new FasePonteiro();
        var paginas = 0;

        // Rowversion tem uma corrida clássica: transação ABERTA na origem já consumiu um rv
        // MENOR que o máximo que vamos ler, mas ainda não é visível; se o ponteiro passar do
        // rv dela, o commit posterior fica para trás do corte — invisível para sempre.
        // MIN_ACTIVE_ROWVERSION() é o teto seguro: nada abaixo dele está em voo.
        var capSeguro = long.MaxValue;
        var capLinhas = await leitor.ConsultarAsync(SqlCapRowversion(), ct);
        if (capLinhas.Count > 0 && capLinhas[0].Numero("cap") is { } cap && cap > 0)
            capSeguro = cap;

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

            await processar(linhas, ponteiro);
            paginas++;

            // Ponteiro salvo A CADA PÁGINA, não só ao fim da fase. É seguro justamente porque
            // a paginação é POR ele: PonteiroSeguro nunca passa de uma falha, então o que ele
            // marca como feito está feito. E é o que torna a interrupção barata — em 05/08 três
            // cargas longas morreram por deploy, e cada uma recomeçava a fase do zero: 78
            // minutos de evoluções relidos porque a fase não tinha chegado ao fim.
            //
            // (O conector do Salux salva por fase porque lá a paginação é por cd, não pela
            // marca — uma marca parcial pularia registros de blocos não processados. Aqui não.)
            if (persistirPonteiro && ctx.SalvarMarca is not null && ponteiro.PonteiroSeguro > desde)
            {
                ctx.Marca.AvancarPonteiro(fase, ponteiro.PonteiroSeguro);
                await ctx.SalvarMarca(ctx.Marca, ct);
            }

            // A PAGINAÇÃO navega pelo MaxVisto — uma falha não pode travar o laço dentro do
            // run; ela só segura o ponteiro PERSISTIDO. Origem sem avanço = fim (evita laço
            // infinito quando a página inteira não tem rv maior).
            if (pararQuando?.Invoke() == true) break;
            if (ponteiro.MaxVisto <= desde) break;
            desde = ponteiro.MaxVisto;
            if (linhas.Count < TamanhoPagina) break;
        }

        ct.ThrowIfCancellationRequested();
        // Ensaio (escopo Limitado) NÃO move ponteiro: um teste que avançasse a marca faria o
        // incremental seguinte pular tudo que o ensaio não importou.
        if (!persistirPonteiro) return;
        var alvo = Math.Min(ponteiro.PonteiroSeguro, capSeguro);
        if (incremental)
        {
            ctx.Marca.AvancarPonteiro(fase, alvo);
        }
        else
        {
            // COMPLETO é re-varredura integral: falha abaixo do ponteiro guardado RECUA a
            // marca — senão o registro falho ficaria atrás do corte, invisível para sempre.
            ctx.Marca.DefinirPonteiro(fase, alvo);
        }
        if (ctx.SalvarMarca is not null) await ctx.SalvarMarca(ctx.Marca, ct);

        if (ponteiro.MenorRvFalho is { } falho)
        {
            logger.LogWarning(
                "Klinikos: fase '{Fase}' teve falha de escrita — ponteiro segurado em {Seguro} "
                + "(viu até {Max}); o próximo ciclo re-varre a partir da falha.",
                fase, ponteiro.PonteiroSeguro, ponteiro.MaxVisto);
        }
        else
        {
            logger.LogInformation("Klinikos: fase '{Fase}' em {N} página(s); ponteiro {Rv}.",
                fase, paginas, ponteiro.MaxVisto);
        }
    }

    // ================= paciente =================

    /// <summary>
    /// Paciente pelo caminho CANÔNICO (<see cref="UpsertCanonicoPep"/>). Com CPF, a âncora é a
    /// chave nacional — é o que amarra a mesma pessoa entre Salux e Klinikos num recurso só,
    /// preserva telefone verificado e congela nascimento divergente. Sem CPF, a âncora é a
    /// chave local e o recurso já vem marcado pelo mapper (ADR-0041) — upsert idempotente da
    /// própria base, nunca merge com as outras.
    /// </summary>
    private static async Task<string> UpsertPacienteAsync(
        ContextoImportacaoPep ctx, KlinikosFhirMapper mapper, PacienteLinha pac, CancellationToken ct)
    {
        var recurso = mapper.BuildPatient(pac);
        // Mesma régua do mapper: só CPF VÁLIDO ancora — "00000000000" fundiria duas pessoas.
        var cpf = CpfPep.Valido(pac.Cpf) ? pac.CpfDigitos : string.Empty;
        var id = cpf.Length == 11
            ? await UpsertCanonicoPep.UpsertAsync(
                ctx, "Patient", UpsertCanonicoPep.SysCpf, cpf, recurso,
                KlinikosFhirMapper.IdentPaciente, ct)
            : await UpsertCanonicoPep.UpsertAsync(
                ctx, "Patient", KlinikosFhirMapper.IdentPaciente, mapper.Pref(pac.Codigo), recurso,
                KlinikosFhirMapper.IdentPaciente, ct);
        return $"Patient/{id}";
    }

    /// <summary>
    /// Traz da origem os pacientes citados mas ainda não resolvidos neste run. No incremental um
    /// boletim novo é quase sempre de um paciente ANTIGO — cuja linha de cadastro não mudou, e
    /// portanto não veio na fase de pacientes.
    /// </summary>
    private static async Task GarantirPacientesAsync(
        ContextoImportacaoPep ctx, KlinikosFhirMapper mapper, LeitorAgenteSql leitor,
        IEnumerable<string?> codigos, Dictionary<string, string> cache,
        Action<string, long, Exception> falhou, CancellationToken ct)
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
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    falhou($"paciente {pac.Codigo}", Cd(pac.Codigo), ex);
                }
            }
        }
    }

    // ================= boletim =================

    /// <summary>
    /// Quais destes boletins tiveram atendimento médico iniciado? Uma consulta em LOTE por
    /// página (<c>IN</c> de até 500) — e não um <c>EXISTS</c> correlacionado por linha, que
    /// dependeria de um índice em <c>UPA_Evolucao(SPA_CODIGO)</c> cuja existência não dá para
    /// verificar com o agente offline. O lote tem custo previsível: uma passada por consulta.
    /// </summary>
    private static async Task<HashSet<string>> CarregarFlagsAtendimentoAsync(
        LeitorAgenteSql leitor, IEnumerable<string> boletins, CancellationToken ct)
    {
        var todos = boletins.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var com = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lote in EmLotes(todos, TamanhoLote))
        {
            foreach (var linha in await leitor.ConsultarAsync(SqlFlagsAtendimento(lote), ct))
            {
                if (linha.Texto("SPA_CODIGO") is { } spa) com.Add(spa);
            }
        }
        return com;
    }

    private static async Task<Atendimento?> UpsertBoletimAsync(
        ContextoImportacaoPep ctx, KlinikosFhirMapper mapper, BoletimLinha b, bool teveAtendimento,
        IReadOnlyDictionary<string, string> orgPorUnidade,
        IReadOnlyDictionary<string, string> pacPorCodigo,
        CancellationToken ct)
    {
        if (b.PacCodigo is null || !pacPorCodigo.TryGetValue(b.PacCodigo, out var pacRef)) return null;

        var orgRef = b.UnidCodigo is not null ? orgPorUnidade.GetValueOrDefault(b.UnidCodigo) : null;

        // Todo boletim vira Encounter, inclusive o de quem desistiu antes de ser atendido
        // (7,7% na UPA): a pessoa esteve na unidade, e isso é informação clínica. O que muda
        // é o status — nunca a existência.
        var enc = mapper.BuildEncounter(b, pacRef, orgRef, teveAtendimento);
        var salvo = await ctx.Escritor.UpsertPorIdentifierAsync(
            enc, KlinikosFhirMapper.IdentBoletim, mapper.Pref(b.Codigo), ct);

        return new Atendimento($"Encounter/{salvo.Id}", pacRef, teveAtendimento);
    }

    /// <summary>
    /// Resolve boletins citados por evolução/sinal vital que ainda não estão no cache do run —
    /// é o caso normal no incremental: uma reavaliação de hoje pendura num boletim de ontem.
    /// O re-fetch recalcula a flag de atendimento, então também é o caminho que CURA o status
    /// de um boletim importado antes de o atendimento começar.
    /// </summary>
    private static async Task GarantirAtendimentosAsync(
        ContextoImportacaoPep ctx, KlinikosFhirMapper mapper, LeitorAgenteSql leitor,
        IEnumerable<string?> boletins,
        IReadOnlyDictionary<string, string> orgPorUnidade,
        Dictionary<string, string> pacPorCodigo,
        Dictionary<string, Atendimento> cache,
        Action<string, long, Exception> falhou, CancellationToken ct)
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
            var comAtendimento = await CarregarFlagsAtendimentoAsync(
                leitor, achados.Select(b => b.Codigo), ct);

            await GarantirPacientesAsync(ctx, mapper, leitor, achados.Select(b => b.PacCodigo),
                pacPorCodigo, falhou, ct);

            foreach (var b in achados)
            {
                try
                {
                    if (await UpsertBoletimAsync(ctx, mapper, b, comAtendimento.Contains(b.Codigo),
                            orgPorUnidade, pacPorCodigo, ct) is { } a)
                        cache[b.Codigo] = a;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    falhou($"boletim {b.Codigo}", Cd(b.PacCodigo), ex);
                }
            }
        }
    }

    // ================= evolução =================

    private static async Task ProcessarEvolucaoAsync(
        ContextoImportacaoPep ctx, KlinikosFhirMapper mapper, EvolucaoLinha e, Atendimento a,
        Dictionary<string, string> cidDataPorBoletim, CancellationToken ct)
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
            // Só grava se esta evolução é clinicamente mais recente que a última que já gravou
            // CID para o boletim — rowversion ordena EDIÇÕES, não o curso clínico.
            var quando = e.DataHora ?? string.Empty;
            if (!cidDataPorBoletim.TryGetValue(boletim, out var ultima)
                || string.CompareOrdinal(quando, ultima) >= 0)
            {
                await ctx.Escritor.UpsertPorIdentifierAsync(
                    cond, KlinikosFhirMapper.IdentBoletim, mapper.Pref(boletim) + ":cond", ct);
                cidDataPorBoletim[boletim] = quando;
                p.Conditions++;
            }
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

            // Marcos de jornada, não documentos: o INÍCIO é texto constante ("Início do
            // Atendimento") e as entradas de sala não têm narrativa. O que eles carregam já
            // foi extraído acima (o CID); uma DocumentReference de texto vazio ou boilerplate
            // só sujaria o prontuário — seriam 165 mil iguais.
            case TipoInicioAtendimento:
            case "ENTRADA NA SALA AMARELA":
            case "ENTRADA NA SALA VERMELHA":
                break;

            default:
                if (string.IsNullOrWhiteSpace(e.Descricao)) break; // sem texto não há documento
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

    /// <summary>Boletins DE uma lista de pacientes (reimport direcionado).</summary>
    internal static string SqlBoletinsDePacientes(IReadOnlyList<string> pacientes) => $"""
        SELECT {ColunasBoletim}
          FROM Pronto_Atendimento
         WHERE pac_codigo IN ({ListaTexto(pacientes)})
        """;

    internal static string SqlBoletinsPorCodigo(IReadOnlyList<string> codigos) => $"""
        SELECT {ColunasBoletim}
          FROM Pronto_Atendimento
         WHERE spa_codigo IN ({ListaTexto(codigos)})
           AND pac_codigo IS NOT NULL
        """;

    /// <summary>
    /// Quais boletins tiveram ALGUM atendimento — qualquer evolução que não seja ESTORNO.
    /// A régua anterior (só "INÍCIO DO ATENDIMENTO MÉDICO") rotularia como "não atendido"
    /// os ~2.200 boletins atendidos apenas pela enfermagem — status clínico FALSO no hub.
    /// "Não atendido" de verdade = boletim sem evolução nenhuma (evasão antes de tudo).
    /// O <c>%</c> no fim do LIKE blinda contra padding de <c>char</c>/espaço à direita.
    /// </summary>
    internal static string SqlFlagsAtendimento(IReadOnlyList<string> boletins) => $"""
        SELECT DISTINCT SPA_CODIGO
          FROM UPA_Evolucao
         WHERE SPA_CODIGO IN ({ListaTexto(boletins)})
           AND Tipo NOT LIKE 'ESTORNO%'
        """;

    /// <summary>Teto seguro do rowversion: nada abaixo dele pertence a transacao em voo.</summary>
    internal static string SqlCapRowversion() =>
        "SELECT CONVERT(BIGINT, MIN_ACTIVE_ROWVERSION()) - 1 AS cap";

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

    // ================= helpers =================

    /// <summary>Código da origem → número para a trilha de falhas (0 quando não numérico).</summary>
    private static long Cd(string? codigo)
    {
        var d = Digitos(codigo);
        return d.Length is > 0 and <= 18 && long.TryParse(d, out var n) ? n : 0;
    }

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

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
