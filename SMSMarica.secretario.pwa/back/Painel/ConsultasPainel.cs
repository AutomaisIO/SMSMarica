namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// Consultas do painel, transcritas de <c>docs/consultas-oracle.md</c> (SQL validado no
/// Oracle PROD do Salux em 24/07/2026). Períodos (:ini/:fim) viram expressões
/// <c>TRUNC(SYSDATE...)</c> montadas AQUI — constantes do código, nunca input externo
/// (não há input de usuário neste serviço). O único valor interpolado variável é o
/// código do hospital, um <see cref="int"/> vindo da config.
/// </summary>
public static class ConsultasPainel
{
    // ── Expressões de período (constantes — ver docs/consultas-oracle.md §Q2) ──
    public const string IniMesAnterior = "TRUNC(ADD_MONTHS(SYSDATE,-1),'MM')";
    public const string FimMesAnterior = "TRUNC(SYSDATE,'MM')";
    public const string IniMesAtual = "TRUNC(SYSDATE,'MM')";
    public const string FimMesAtual = "SYSDATE";
    public const string IniHoje = "TRUNC(SYSDATE)";
    public const string FimHoje = "SYSDATE";
    public const string IniOntem = "TRUNC(SYSDATE) - 1";
    public const string FimOntem = "TRUNC(SYSDATE)";

    /// <summary>Fim "dias completos" do mês atual (exclui o dia corrente, parcial).</summary>
    public const string FimDiasCompletos = "TRUNC(SYSDATE)";

    /// <summary>
    /// Unidades obstétricas do HMCML: MATERNIDADE (15), PRE PARTO (16), BERCARIO (17) e
    /// MATERNIDADE 2 (26). É por AQUI que a internação é separada — e não pelo
    /// <c>FIA.ID_INTERNACAO</c> ('U'/'E'), que no HMCML NÃO significa urgência/eletiva:
    /// investigação de 24/07/2026 mostrou que 98% dos 'E' estão na maternidade, 99,7%
    /// entraram pelo boletim da emergência, o caráter oficial do SUS é "urgência" em
    /// 100% deles e a distribuição por dia da semana inclui sábado e domingo. O 'E' é,
    /// na prática, convenção de preenchimento da recepção da maternidade.
    /// </summary>
    public const string UnidadesMaternidade = "15,16,17,26";

    /// <summary>
    /// Código da cor VERMELHO em <c>INFOSAUDE.CLASSIFICACAO_RISCO</c> (o cadastro tem
    /// duas famílias de linhas; a viva é a de nomes de cor, 7–10). Ele merece constante
    /// porque a Q6 dá ao vermelho uma regra de medição PRÓPRIA — ver lá.
    /// </summary>
    public const int CodigoVermelho = 7;

    /// <summary>
    /// Último leito ocupado de cada internação (a paciente troca de leito ao longo da
    /// estadia; o que vale para classificar é onde ela está/terminou).
    /// </summary>
    private static string LeitoAtual(int hospital) => $"""
          SELECT fl.dt_ano_fia, fl.nr_fia, fl.cd_unidade, fl.cd_quarto, fl.cd_leito,
                 ROW_NUMBER() OVER (PARTITION BY fl.dt_ano_fia, fl.nr_fia
                                    ORDER BY fl.dt_transferencia DESC) rn
            FROM infosaude.fia_leito fl
           WHERE fl.cd_hospital = {hospital}
        """;

    /// <summary>
    /// As três faixas de internação, mutuamente exclusivas e nesta ordem de precedência:
    /// <b>maternidade</b> (pela unidade do leito) → <b>até 17 anos</b> (idade na entrada)
    /// → <b>adultos</b>.
    ///
    /// A precedência importa: 114 das 172 internações de menores em julho eram de bebês
    /// com menos de 1 ano, na maternidade/berçário. Sem colocar a maternidade primeiro, a
    /// faixa "crianças" viraria um retrato de partos em vez de mostrar pediatria de fato
    /// (72 em julho, fora da maternidade). Idade sem data de nascimento cai em adultos —
    /// nenhum caso hoje (0 nascimentos nulos no mês).
    /// </summary>
    private const string EhMaternidade = "la.cd_unidade IN (15,16,17,26)";
    private const string NaoEhMaternidade = "(la.cd_unidade IS NULL OR la.cd_unidade NOT IN (15,16,17,26))";

    private static string FaixasInternacao(string dataReferencia) => $"""
               SUM(CASE WHEN {EhMaternidade} THEN 1 ELSE 0 END) AS maternidade,
               SUM(CASE WHEN {NaoEhMaternidade}
                         AND FLOOR(MONTHS_BETWEEN({dataReferencia}, p.dt_nascimento)/12) <= 17
                        THEN 1 ELSE 0 END) AS ate17,
               SUM(CASE WHEN {NaoEhMaternidade}
                         AND (p.dt_nascimento IS NULL
                              OR FLOOR(MONTHS_BETWEEN({dataReferencia}, p.dt_nascimento)/12) > 17)
                        THEN 1 ELSE 0 END) AS adultos
        """;

    /// <summary>
    /// Junta paciente (idade) + leito atual (unidade). ATENÇÃO ao montar SQL para o
    /// sqlplus: linha em branco no meio do comando o encerra (SQLBLANKLINES off).
    /// </summary>
    private static string DeInternacaoComFaixas(int hospital) => $"""
          FROM infosaude.fia f
          JOIN infosaude.paciente p ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
          LEFT JOIN ({LeitoAtual(hospital)}) la
                 ON la.dt_ano_fia = f.dt_ano_fia AND la.nr_fia = f.nr_fia AND la.rn = 1
        """;

    // ── Marcador do atendimento médico ────────────────────────────────────────

    /// <summary>
    /// Boletins que carimbam início de atendimento médico. <c>BAA.DT_INICIO_ATEND_MED</c>
    /// é 100% vazia no HMCML, então o relógio do médico vem do primeiro documento.
    /// </summary>
    private const string ModelosBoletimMedico = "10036,10232,10014";

    /// <summary>
    /// "O médico assumiu o paciente" — EXISTS reutilizado pelas consultas do agora.
    ///
    /// <para>
    /// Só os três modelos de boletim NÃO bastam: em 24/07/2026 o painel mostrou uma
    /// paciente da pediatria "esperando há 9 horas" quando ela recebera um Receituário de
    /// Controle Especial <b>3 minutos</b> depois da classificação — receituário, atestado,
    /// laudo AIH e encaminhamento não estão na lista, e o boletim dela nunca foi aberto.
    /// Como 47% dos boletins nunca recebem <c>DT_SAIDA</c>, quem some sem o boletim ser
    /// encerrado envelhece na fila até cair da janela de 12h e contamina a média.
    /// </para>
    ///
    /// <para>
    /// A regra é a UNIÃO de três rastros: (1) os modelos de boletim, qualquer autor —
    /// 31 dos 480 de 24/07 foram lavrados por quem não é <c>MED</c>; (2) <b>qualquer</b>
    /// eDoc cujo autor seja médico (<c>cd_funcionario_inc</c> casa <c>MED</c>: os papéis
    /// são disjuntos e legíveis no código — MED, ENF, TEC, PSI, ASS…), o que exclui
    /// sozinho escalas de enfermagem, SAE e pesquisa de satisfação; (3) prescrição médica
    /// em <c>PRESCRICAO_BAA</c>. Medido em 24/07: cobertura 402 → 404 boletins e, em 83
    /// deles, o rastro é <b>16 min mais cedo</b> que o boletim (mediana da espera 23,3 →
    /// 18,8 min). Na fila viva do momento do teste, tirou 16 dos 38 fantasmas.
    /// </para>
    /// </summary>
    private static string TemAtendimentoMedico(int hospital, string alias) => $"""
        (EXISTS (SELECT 1 FROM infosaude.edoc_movimento mv
                  WHERE mv.cd_hospital = {hospital}
                    AND mv.dt_ano_baa = {alias}.dt_ano_baa AND mv.nr_baa = {alias}.nr_baa
                    AND (mv.cd_modelo IN ({ModelosBoletimMedico})
                         OR REGEXP_LIKE(mv.cd_funcionario_inc,'MED')))
             OR EXISTS (SELECT 1 FROM infosaude.prescricao_baa pb
                         WHERE pb.cd_hospital = {hospital}
                           AND pb.dt_ano_baa = {alias}.dt_ano_baa AND pb.nr_baa = {alias}.nr_baa))
        """;

    /// <summary>
    /// Primeiro rastro médico de cada boletim, na mesma união de
    /// <see cref="TemAtendimentoMedico"/> — versão datada, para as consultas de período.
    /// </summary>
    private static string PrimeiroAtendimentoMedico(int hospital, string ini, string fim) => $"""
          SELECT r.dt_ano_baa, r.nr_baa, MIN(r.dt_med) AS dt_med
            FROM (SELECT mv.dt_ano_baa, mv.nr_baa, mv.dt_inclusao AS dt_med
                    FROM infosaude.edoc_movimento mv
                   WHERE mv.cd_hospital = {hospital}
                     AND (mv.cd_modelo IN ({ModelosBoletimMedico})
                          OR REGEXP_LIKE(mv.cd_funcionario_inc,'MED'))
                     AND mv.dt_inclusao >= {ini} AND mv.dt_inclusao < {fim}
                  UNION ALL
                  SELECT pb.dt_ano_baa, pb.nr_baa, pb.dt_prescricao
                    FROM infosaude.prescricao_baa pb
                   WHERE pb.cd_hospital = {hospital}
                     AND pb.dt_prescricao >= {ini} AND pb.dt_prescricao < {fim}) r
           GROUP BY r.dt_ano_baa, r.nr_baa
        """;

    // ── Q1 — Agora (tick rápido) ───────────────────────────────────────────────

    /// <summary>
    /// Q1a: aguardando médico agora, por cor (chegada nas últimas 12h, sem saída, sem
    /// rastro médico), com os DOIS tempos da jornada separados.
    ///
    /// <para>
    /// <b>T1 — chegada → classificação</b> é intervalo fechado (já aconteceu). <b>T2 —
    /// classificação → agora</b> é relógio correndo: é ele que responde "há quanto tempo
    /// essa pessoa espera o médico". A média desde a chegada continua vindo porque
    /// mistura os dois e serve de conferência.
    /// </para>
    ///
    /// <para>
    /// <b>Clamp obrigatório</b>: em 5,7% dos boletins (197 de 3.432 numa semana) a
    /// classificação é carimbada ANTES da chegada — o acolhimento/senha abre antes do
    /// BAA — e o pior caso é −184 min. Sem <c>GREATEST</c> isso vira tempo negativo na
    /// tela. Quem ainda não foi classificado entra na contagem mas fica fora das duas
    /// médias (<c>AVG</c> ignora nulo), que é o comportamento honesto.
    /// </para>
    /// </summary>
    public static string Q1AguardandoPorCor(int hospital) => $"""
        SELECT NVL(cr.ds_classificacao_risco,'SEM_CLASSIFICACAO') AS cor, COUNT(*) AS qtd,
               ROUND(AVG((SYSDATE - b.dt_chegada) * 1440), 0) AS min_medio_desde_chegada,
               ROUND(AVG(GREATEST((b.dt_classifica_atual - b.dt_chegada) * 1440, 0)), 1)
                 AS min_medio_ate_classificacao,
               ROUND(AVG((SYSDATE - GREATEST(b.dt_classifica_atual, b.dt_chegada)) * 1440), 0)
                 AS min_medio_desde_classificacao
          FROM infosaude.baa b
          LEFT JOIN infosaude.classificacao_risco cr ON cr.cd_classificacao_risco = b.cd_classificacao_risco
         WHERE b.cd_hospital = {hospital} AND b.in_emergencia = 'S'
           AND b.dt_chegada >= SYSDATE - 0.5
           AND b.dt_saida IS NULL
           AND NOT {TemAtendimentoMedico(hospital, "b")}
         GROUP BY cr.ds_classificacao_risco
        """;

    /// <summary>
    /// Q1b: em atendimento/observação (mesma janela, COM rastro médico, sem saída).
    /// Usa o MESMO marcador da Q1a — se divergirem, quem sai da fila some do painel em
    /// vez de aparecer em atendimento.
    /// </summary>
    public static string Q1EmAtendimento(int hospital) => $"""
        SELECT COUNT(*) AS em_atendimento
          FROM infosaude.baa b
         WHERE b.cd_hospital = {hospital} AND b.in_emergencia = 'S'
           AND b.dt_chegada >= SYSDATE - 0.5 AND b.dt_saida IS NULL
           AND {TemAtendimentoMedico(hospital, "b")}
        """;

    /// <summary>
    /// Q1c: internados agora (nas três faixas) + totais de hoje.
    /// As agregações ficam numa subconsulta: no Oracle, subconsulta escalar no SELECT
    /// junto de agregação sem GROUP BY dá ORA-00937.
    /// </summary>
    public static string Q1InternadosEHoje(int hospital) => $"""
        SELECT i.internados_agora, i.maternidade, i.ate17, i.adultos, i.media_dias_internado,
               (SELECT COUNT(*) FROM infosaude.baa b
                 WHERE b.cd_hospital = {hospital} AND b.dt_atendimento >= TRUNC(SYSDATE))  AS atendimentos_hoje,
               (SELECT COUNT(*) FROM infosaude.fia f2
                 WHERE f2.cd_hospital = {hospital} AND f2.dt_baixa >= TRUNC(SYSDATE))      AS internacoes_hoje
          FROM (
            SELECT COUNT(*) AS internados_agora,
            {FaixasInternacao("SYSDATE")},
                   ROUND(AVG(SYSDATE - f.dt_baixa), 1) AS media_dias_internado
            {DeInternacaoComFaixas(hospital)}
             WHERE f.cd_hospital = {hospital} AND f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120
          ) i
        """;

    // ── Q2 — Atendimentos por período (tick lento) ─────────────────────────────

    public static string Q2AtendimentosPeriodo(int hospital, string ini, string fim) => $"""
        SELECT COUNT(*) AS total FROM infosaude.baa b
         WHERE b.cd_hospital = {hospital} AND b.dt_atendimento >= {ini} AND b.dt_atendimento < {fim}
        """;

    // ── Q3 — Série diária de atendimentos (35 dias, tick lento) ────────────────

    public static string Q3SerieDiariaAtendimentos(int hospital) => $"""
        SELECT TRUNC(b.dt_atendimento) AS dia, COUNT(*) AS qtd
          FROM infosaude.baa b
         WHERE b.cd_hospital = {hospital} AND b.dt_atendimento >= TRUNC(SYSDATE) - 34
         GROUP BY TRUNC(b.dt_atendimento) ORDER BY 1
        """;

    // ── Q4 — Atendimentos de hoje por hora (tick lento) ────────────────────────

    public static string Q4PorHoraHoje(int hospital) => $"""
        SELECT TO_NUMBER(TO_CHAR(b.dt_atendimento,'HH24')) AS hora, COUNT(*) AS qtd
          FROM infosaude.baa b
         WHERE b.cd_hospital = {hospital} AND b.dt_atendimento >= TRUNC(SYSDATE)
         GROUP BY TO_NUMBER(TO_CHAR(b.dt_atendimento,'HH24')) ORDER BY 1
        """;

    // ── Q5 — Internações por período + série (tick lento) ──────────────────────

    public static string Q5InternacoesPeriodo(int hospital, string ini, string fim) => $"""
        SELECT COUNT(*) AS total,
        {FaixasInternacao("f.dt_baixa")}
        {DeInternacaoComFaixas(hospital)}
         WHERE f.cd_hospital = {hospital} AND f.dt_baixa >= {ini} AND f.dt_baixa < {fim}
        """;

    public static string Q5SerieDiariaInternacoes(int hospital) => $"""
        SELECT TRUNC(f.dt_baixa) AS dia, COUNT(*) AS qtd,
        {FaixasInternacao("f.dt_baixa")}
        {DeInternacaoComFaixas(hospital)}
         WHERE f.cd_hospital = {hospital} AND f.dt_baixa >= TRUNC(SYSDATE) - 34
         GROUP BY TRUNC(f.dt_baixa) ORDER BY 1
        """;

    // ── Q7 — Maternidade: partos (tick lento) ──────────────────────────────────

    /// <summary>
    /// Nascimentos registrados em <c>INFOSAUDE.NASCIMENTO</c> (o livro de partos).
    /// Registro consistente: 82–137 partos/mês nos últimos 12 meses, sem buracos, com
    /// peso/prematuridade/APGAR/sexo preenchidos em 100% do mês corrente.
    /// ATENÇÃO: <c>PESO</c> está em QUILOS (2,165–4,365), não em gramas — baixo peso é
    /// &lt; 2,5. O tipo do parto vale por <c>ID_TP_PARTO</c> ('C'/'V'); a coluna textual
    /// <c>TIPO_PARTO</c> está sempre nula.
    /// </summary>
    public static string Q7Maternidade(string ini, string fim) => $"""
        SELECT COUNT(*)                                                          AS partos,
               SUM(CASE WHEN n.id_tp_parto='C' THEN 1 ELSE 0 END)                AS cesareas,
               SUM(CASE WHEN n.id_tp_parto='V' THEN 1 ELSE 0 END)                AS vaginais,
               SUM(CASE WHEN n.in_prematuro='S' THEN 1 ELSE 0 END)               AS prematuros,
               SUM(CASE WHEN n.peso > 0 AND n.peso < 2.5 THEN 1 ELSE 0 END)      AS baixo_peso,
               ROUND(AVG(CASE WHEN n.peso > 0 THEN n.peso END), 3)               AS peso_medio_kg,
               SUM(CASE WHEN TO_NUMBER(REGEXP_SUBSTR(n.apgar_5_min,'^\d+')) < 7
                        THEN 1 ELSE 0 END)                                       AS apgar5_abaixo7,
               SUM(CASE WHEN n.sexo='F' THEN 1 ELSE 0 END)                       AS meninas,
               SUM(CASE WHEN n.sexo='M' THEN 1 ELSE 0 END)                       AS meninos,
               -- ── enriquecimento da aba Maternidade ──
               SUM(CASE WHEN n.id_condicao_nascimento='M' THEN 1 ELSE 0 END)     AS natimortos,
               SUM(CASE WHEN n.id_malformacao='S' THEN 1 ELSE 0 END)             AS com_malformacao,
               SUM(CASE WHEN n.id_malformacao IS NULL THEN 1 ELSE 0 END)         AS malformacao_sem_info,
               SUM(CASE WHEN n.id_tmp_gestacao='5' THEN 1 ELSE 0 END)            AS a_termo,
               SUM(CASE WHEN n.id_tmp_gestacao='4' THEN 1 ELSE 0 END)            AS prematuro_tardio,
               SUM(CASE WHEN n.id_tmp_gestacao='6' THEN 1 ELSE 0 END)            AS pos_termo,
               SUM(CASE WHEN n.id_tmp_gestacao IS NULL THEN 1 ELSE 0 END)        AS gestacao_sem_info,
               SUM(CASE WHEN n.id_tp_gravidez='U' THEN 1 ELSE 0 END)             AS gravidez_unica,
               SUM(CASE WHEN n.id_tp_gravidez IS NOT NULL AND n.id_tp_gravidez<>'U'
                        THEN 1 ELSE 0 END)                                       AS gravidez_multipla,
               SUM(CASE WHEN TO_NUMBER(REGEXP_SUBSTR(n.apgar_1_minuto,'^\d+')) < 7
                        THEN 1 ELSE 0 END)                                       AS apgar1_abaixo7,
               ROUND(AVG(CASE WHEN n.estatura > 0 THEN n.estatura END), 1)       AS estatura_media,
               ROUND(AVG(CASE WHEN n.perimetro_cefalico > 0 THEN n.perimetro_cefalico END), 1) AS pc_medio,
               ROUND(AVG(FLOOR(MONTHS_BETWEEN(n.dt_parto, p.dt_nascimento)/12)), 1) AS idade_media_mae,
               SUM(CASE WHEN FLOOR(MONTHS_BETWEEN(n.dt_parto,p.dt_nascimento)/12) <= 17
                        THEN 1 ELSE 0 END)                                       AS mae_ate17,
               SUM(CASE WHEN FLOOR(MONTHS_BETWEEN(n.dt_parto,p.dt_nascimento)/12) < 20
                        THEN 1 ELSE 0 END)                                       AS mae_menor20,
               SUM(CASE WHEN FLOOR(MONTHS_BETWEEN(n.dt_parto,p.dt_nascimento)/12) >= 35
                        THEN 1 ELSE 0 END)                                       AS mae_35_mais
          FROM infosaude.nascimento n
          LEFT JOIN infosaude.fia f
                 ON f.cd_hospital = n.cd_hospital AND f.dt_ano_fia = n.dt_ano_fia AND f.nr_fia = n.nr_fia
          LEFT JOIN infosaude.paciente p ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
         WHERE n.dt_parto >= {ini} AND n.dt_parto < {fim}
        """;

    public static string Q7SerieDiariaPartos() => """
        SELECT TRUNC(n.dt_parto) AS dia, COUNT(*) AS qtd,
               SUM(CASE WHEN n.id_tp_parto='C' THEN 1 ELSE 0 END) AS cesareas
          FROM infosaude.nascimento n
         WHERE n.dt_parto >= TRUNC(SYSDATE) - 34
         GROUP BY TRUNC(n.dt_parto) ORDER BY 1
        """;

    // ── Q6 — Espera por cor (tick lento — a consulta PESADA) ───────────────────

    /// <summary>
    /// Rodar 1× por período (hoje / mês atual / mês anterior). <paramref name="fimDoc"/> =
    /// fim + 3 dias — o doc médico pode ser lavrado depois do fim do período.
    ///
    /// <para>
    /// Usa o mesmo marcador ampliado do agora (<see cref="PrimeiroAtendimentoMedico"/>),
    /// senão o histórico e a fila viva contariam populações diferentes.
    /// </para>
    ///
    /// <para>
    /// <b>O VERMELHO tem regra de medição PRÓPRIA</b> (decisão do usuário, 25/07). Ali o
    /// médico costuma assistir o paciente primeiro e só depois se lavram classificação,
    /// boletim e prescrição — medir "classificação → documento" mede o papel, não o
    /// cuidado. Então no vermelho a espera é <b>da chegada até a primeira interação de
    /// qualquer natureza</b> (a própria classificação já conta), e todo vermelho
    /// classificado é atendido por definição. Efeito nos dois casos de 24/07: 63,9 e
    /// 156,5 min viram <b>1,9 e 5,4 min</b> — o que o painel exibia como "1h50 de espera
    /// no vermelho" era atraso de REGISTRO, não de atendimento.
    /// </para>
    ///
    /// <para>
    /// A meta vem de <see cref="MetasTriagem"/>, não do cadastro das bases — as três
    /// discordam entre si e a Santa Rita discorda de si mesma. Meta zero (vermelho) sai
    /// com <c>pct_na_meta</c> nulo: alvo imediato não tem percentual que informe.
    /// </para>
    ///
    /// <para>
    /// <b>Clamp em vez de descarte</b>: a versão anterior jogava fora o paciente cujo
    /// rastro médico antecedia a classificação (<c>d.dt_med >= dt_classifica_atual</c>).
    /// Com o marcador ampliado esses casos são 9 em 3.432 (0,3%) — e descartar some com o
    /// paciente da conta inteira, inclusive do denominador da meta. <c>GREATEST(...,0)</c>
    /// mantém o paciente com espera zero, que é o que de fato aconteceu: foi atendido
    /// antes de o carimbo da triagem sair. Efeito na semana medida: média 40,3 → 40,1 min.
    /// </para>
    /// </summary>
    public static string Q6EsperaPorCor(int hospital, string ini, string fim, string fimDoc) => $"""
        WITH b AS (
          SELECT b.dt_ano_baa, b.nr_baa, b.cd_classificacao_risco,
                 b.dt_chegada, b.dt_classifica_atual
            FROM infosaude.baa b
           WHERE b.cd_hospital = {hospital}
             AND b.dt_atendimento >= {ini} AND b.dt_atendimento < {fim}
             AND b.in_emergencia = 'S'
        ), d AS (
        {PrimeiroAtendimentoMedico(hospital, ini, fimDoc)}
        ), e AS (
          SELECT NVL(cr.ds_classificacao_risco,'SEM_CLASSIFICACAO')                   AS cor,
                 cr.cd_classificacao_risco                                            AS ordem,
                 {MetasTriagem.CaseSalux("cr.cd_classificacao_risco")}                AS meta,
                 CASE WHEN {CodigoVermelho} = cr.cd_classificacao_risco THEN 1
                      WHEN d.dt_med IS NOT NULL THEN 1 ELSE 0 END                     AS atendido,
                 GREATEST((b.dt_classifica_atual - b.dt_chegada) * 1440, 0)           AS ate_triagem,
                 CASE WHEN {CodigoVermelho} = cr.cd_classificacao_risco
                      THEN GREATEST((LEAST(b.dt_classifica_atual,
                                           NVL(d.dt_med, b.dt_classifica_atual))
                                     - b.dt_chegada) * 1440, 0)
                      ELSE GREATEST((d.dt_med - b.dt_classifica_atual) * 1440, 0)
                 END                                                                  AS espera
            FROM b
            LEFT JOIN d ON d.dt_ano_baa = b.dt_ano_baa AND d.nr_baa = b.nr_baa
            LEFT JOIN infosaude.classificacao_risco cr
                   ON cr.cd_classificacao_risco = b.cd_classificacao_risco
           WHERE b.dt_classifica_atual IS NOT NULL
        )
        SELECT cor                                                                    AS cor,
               COUNT(*)                                                               AS pacientes,
               SUM(atendido)                                                          AS com_atendimento,
               ROUND(AVG(ate_triagem), 1)                                             AS media_ate_triagem,
               ROUND(AVG(espera), 1)                                                  AS media_espera,
               ROUND(MEDIAN(espera), 1)                                               AS mediana_espera,
               ROUND(PERCENTILE_CONT(0.9) WITHIN GROUP (ORDER BY espera), 1)          AS p90_espera,
               MAX(meta)                                                              AS meta_min,
               CASE WHEN MAX(meta) > 0
                    THEN ROUND(100 * SUM(CASE WHEN espera <= meta THEN 1 ELSE 0 END)
                               / NULLIF(SUM(atendido),0), 1)
               END                                                                    AS pct_na_meta
          FROM e
         GROUP BY cor, ordem
         ORDER BY ordem
        """;

    // ── L1..L3 — Leitos e permanência (tick lento) ─────────────────────────────

    /// <summary>
    /// Internados AGORA (a mesma população da Q1c). Fica numa constante porque as três
    /// consultas de leito precisam exatamente do mesmo recorte — se divergirem, o painel
    /// mostra ocupação que não bate com o número de internados exibido ao lado.
    /// </summary>
    private static string InternadosAgora(int hospital) => $"""
           WHERE f.cd_hospital = {hospital} AND f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120
        """;

    /// <summary>
    /// L1: ocupação por setor. Só unidades ATIVAS (<c>id_condicao_unidade = 'A'</c>) —
    /// uma enfermaria desativada tem leito cadastrado mas não é capacidade, e incluí-la
    /// afundaria a taxa de ocupação com 73 leitos fantasmas.
    ///
    /// <b>Nem todo registro em <c>LEITO</c> é leito de internação.</b> Contar a tabela
    /// inteira (o que esta consulta fazia até 25/07) inflava o Conde para 426 "leitos
    /// cadastrados" e 219 "livres", quando a capacidade real são 211 e as vagas 83. Os
    /// dois campos que qualificam vêm comentados no próprio dicionário do Salux:
    ///
    /// <list type="bullet">
    ///   <item><c>ID_LEITO</c> — <c>I</c>: Internação · <c>E</c>: <b>Extra</b> ·
    ///   <c>O</c>: Observação · <c>C</c>: Cirurgia · <c>R</c>: Recuperação ·
    ///   <c>V</c>: <b>Virtual</b> (não existe fisicamente).</item>
    ///   <item><c>ID_CONDICAO</c> — <c>A</c>: ativo · <c>I</c>: desativado.</item>
    /// </list>
    ///
    /// Então <b>capacidade = <c>I</c> + ativo + não fechado</b>. Extra é contingência,
    /// não leito; virtual é registro de fluxo (a maternidade tem 37); desativado saiu de
    /// operação. Os três são devolvidos à parte, porque o volume deles é informação de
    /// gestão — mas nenhum entra no denominador.
    ///
    /// <c>fora_capacidade</c> conta os internados que estão num leito que NÃO é de
    /// capacidade (12 em extra, 9 em virtual em 25/07). Eles seguem no numerador — é o
    /// que faz a taxa passar de 100% num setor lotado, e é justamente o que o painel
    /// precisa mostrar. Sem isso a Saúde Mental aparecia com 100% em vez dos 150% reais.
    /// </summary>
    public static string L1OcupacaoPorSetor(int hospital) => $"""
        WITH la AS (
        {LeitoAtual(hospital)}
        ), pac AS (
          SELECT la.cd_unidade, COUNT(*) AS ocupados,
                 SUM(CASE WHEN l.id_leito = 'I' AND l.id_condicao = 'A'
                          THEN 0 ELSE 1 END) AS fora_capacidade
            FROM infosaude.fia f
            JOIN la ON la.dt_ano_fia = f.dt_ano_fia AND la.nr_fia = f.nr_fia AND la.rn = 1
            LEFT JOIN infosaude.leito l
                   ON l.cd_hospital = {hospital} AND l.cd_unidade = la.cd_unidade
                  AND l.cd_quarto = la.cd_quarto AND l.cd_leito = la.cd_leito
        {InternadosAgora(hospital)}
           GROUP BY la.cd_unidade
        ), lei AS (
          SELECT l.cd_unidade,
                 SUM(CASE WHEN l.id_leito = 'I' AND l.id_condicao = 'A'
                           AND NVL(l.id_sit_leito,'?') <> 'F' THEN 1 ELSE 0 END) AS capacidade,
                 SUM(CASE WHEN l.id_leito = 'I' AND l.id_condicao = 'A'
                           AND l.id_sit_leito = 'F' THEN 1 ELSE 0 END) AS bloqueados,
                 SUM(CASE WHEN l.id_leito = 'E' THEN 1 ELSE 0 END) AS extras,
                 SUM(CASE WHEN l.id_leito = 'V' THEN 1 ELSE 0 END) AS virtuais,
                 SUM(CASE WHEN l.id_leito = 'I' AND l.id_condicao = 'I'
                          THEN 1 ELSE 0 END) AS desativados
            FROM infosaude.leito l
            JOIN infosaude.unidade_hospitalar u
              ON u.cd_hospital = l.cd_hospital AND u.cd_unidade = l.cd_unidade
           WHERE l.cd_hospital = {hospital} AND u.id_condicao_unidade = 'A'
           GROUP BY l.cd_unidade
        )
        SELECT u.sc_unidade AS setor, NVL(lei.capacidade,0) AS capacidade,
               NVL(lei.bloqueados,0) AS bloqueados, NVL(pac.ocupados,0) AS ocupados,
               NVL(pac.fora_capacidade,0) AS fora_capacidade,
               NVL(lei.extras,0) AS extras, NVL(lei.virtuais,0) AS virtuais,
               NVL(lei.desativados,0) AS desativados
          FROM infosaude.unidade_hospitalar u
          LEFT JOIN lei ON lei.cd_unidade = u.cd_unidade
          LEFT JOIN pac ON pac.cd_unidade = u.cd_unidade
         WHERE u.cd_hospital = {hospital} AND u.id_condicao_unidade = 'A'
           AND (NVL(lei.capacidade,0) > 0 OR NVL(pac.ocupados,0) > 0
                OR NVL(lei.extras,0) > 0 OR NVL(lei.virtuais,0) > 0
                OR NVL(lei.desativados,0) > 0)
         ORDER BY NVL(pac.ocupados,0) DESC, NVL(lei.capacidade,0) DESC
        """;

    /// <summary>
    /// L2: quem está internado agora — sexo, faixas etárias e tempo já decorrido. Faixas
    /// pela idade HOJE (não na entrada): a pergunta aqui é quem ocupa o leito neste
    /// momento, não como ele entrou.
    /// </summary>
    public static string L2PerfilInternados(int hospital) => $"""
        SELECT COUNT(*) AS internados,
               SUM(CASE WHEN p.sexo = 'M' THEN 1 ELSE 0 END) AS homens,
               SUM(CASE WHEN p.sexo = 'F' THEN 1 ELSE 0 END) AS mulheres,
               SUM(CASE WHEN p.sexo IS NULL OR p.sexo NOT IN ('M','F') THEN 1 ELSE 0 END) AS sem_sexo,
               SUM(CASE WHEN FLOOR(MONTHS_BETWEEN(SYSDATE, p.dt_nascimento)/12) <= 17 THEN 1 ELSE 0 END) AS ate17,
               SUM(CASE WHEN FLOOR(MONTHS_BETWEEN(SYSDATE, p.dt_nascimento)/12) BETWEEN 18 AND 59 THEN 1 ELSE 0 END) AS adultos,
               SUM(CASE WHEN FLOOR(MONTHS_BETWEEN(SYSDATE, p.dt_nascimento)/12) >= 60 THEN 1 ELSE 0 END) AS idosos,
               ROUND(AVG(FLOOR(MONTHS_BETWEEN(SYSDATE, p.dt_nascimento)/12)), 1) AS idade_media,
               ROUND(AVG(SYSDATE - f.dt_baixa), 1) AS dias_medios
          FROM infosaude.fia f
          JOIN infosaude.paciente p ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
        {InternadosAgora(hospital)}
        """;

    /// <summary>
    /// L3: permanência das ALTAS do período — o indicador clássico de tempo médio de
    /// permanência, que é diferente da média dos internados atuais (essa está no perfil).
    /// Uma olha para quem já saiu, a outra para quem ainda está lá; misturar as duas é o
    /// erro clássico do indicador.
    ///
    /// Devolve uma linha por segmento (<c>TOTAL</c> primeiro), para o front não precisar
    /// recompor médias ponderadas — cada segmento já vem medido no banco.
    /// </summary>
    public static string L3Permanencia(int hospital, string ini, string fim) => $"""
        WITH altas AS (
          SELECT f.dt_alta - f.dt_baixa AS dias, p.sexo,
                 FLOOR(MONTHS_BETWEEN(f.dt_baixa, p.dt_nascimento)/12) AS idade
            FROM infosaude.fia f
            JOIN infosaude.paciente p ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
           WHERE f.cd_hospital = {hospital}
             AND f.dt_alta >= {ini} AND f.dt_alta < {fim}
             AND f.dt_alta >= f.dt_baixa
        )
        SELECT 'TOTAL' AS segmento, COUNT(*) AS altas, ROUND(AVG(dias),1) AS media,
               ROUND(MEDIAN(dias),1) AS mediana,
               ROUND(PERCENTILE_CONT(0.9) WITHIN GROUP (ORDER BY dias),1) AS p90
          FROM altas
        UNION ALL
        SELECT 'HOMENS', COUNT(*), ROUND(AVG(dias),1), NULL, NULL FROM altas WHERE sexo = 'M'
        UNION ALL
        SELECT 'MULHERES', COUNT(*), ROUND(AVG(dias),1), NULL, NULL FROM altas WHERE sexo = 'F'
        UNION ALL
        SELECT 'ATE17', COUNT(*), ROUND(AVG(dias),1), NULL, NULL FROM altas WHERE idade <= 17
        UNION ALL
        SELECT 'ADULTOS', COUNT(*), ROUND(AVG(dias),1), NULL, NULL FROM altas WHERE idade BETWEEN 18 AND 59
        UNION ALL
        SELECT 'IDOSOS', COUNT(*), ROUND(AVG(dias),1), NULL, NULL FROM altas WHERE idade >= 60
        """;

    // ── Q8 — Diagnósticos mais frequentes por cor (tick lento) ─────────────────

    /// <summary>
    /// Os CIDs mais registrados em cada cor de triagem. <c>BAA.CD_CID</c> é preenchido em
    /// 94–98% dos boletins de emergência (Amarelo: 98,1%), então o ranking é sólido — ao
    /// contrário das UPAs, onde o CID da classificação é ZERO e o que existe é queixa em
    /// texto livre, fragmentada demais para ranquear ("DOR DE DENTE", "DENTISTA" e
    /// "AVALIAÇÃO ODONTOLOGICA" são a mesma coisa em três linhas). Por isso esta consulta
    /// só existe do lado do Conde.
    ///
    /// Devolve o total da cor junto (janela sobre o GROUP BY) para o front mostrar a
    /// participação de cada CID sem precisar de uma segunda consulta.
    /// </summary>
    public static string Q8CidPorCor(int hospital, string ini, string fim, int topN) => $"""
        SELECT cor, cd_cid, ds_cid, qtd, total_cor FROM (
          SELECT NVL(cr.ds_classificacao_risco,'SEM_CLASSIFICACAO')            AS cor,
                 b.cd_cid, NVL(c.ds_cid, b.cd_cid)                             AS ds_cid,
                 COUNT(*)                                                      AS qtd,
                 SUM(COUNT(*)) OVER (PARTITION BY NVL(cr.ds_classificacao_risco,'SEM_CLASSIFICACAO')) AS total_cor,
                 ROW_NUMBER() OVER (PARTITION BY NVL(cr.ds_classificacao_risco,'SEM_CLASSIFICACAO')
                                    ORDER BY COUNT(*) DESC, b.cd_cid)          AS rn
            FROM infosaude.baa b
            LEFT JOIN infosaude.classificacao_risco cr
                   ON cr.cd_classificacao_risco = b.cd_classificacao_risco
            LEFT JOIN infosaude.cid c ON c.cd_cid = b.cd_cid
           WHERE b.cd_hospital = {hospital} AND b.in_emergencia = 'S'
             AND b.cd_cid IS NOT NULL
             AND b.dt_atendimento >= {ini} AND b.dt_atendimento < {fim}
           GROUP BY cr.ds_classificacao_risco, b.cd_cid, c.ds_cid
        ) WHERE rn <= {topN}
        ORDER BY cor, qtd DESC
        """;
}
