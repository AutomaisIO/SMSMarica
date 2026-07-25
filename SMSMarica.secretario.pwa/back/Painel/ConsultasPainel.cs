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
    /// Último leito ocupado de cada internação (a paciente troca de leito ao longo da
    /// estadia; o que vale para classificar é onde ela está/terminou).
    /// </summary>
    private static string LeitoAtual(int hospital) => $"""
          SELECT fl.dt_ano_fia, fl.nr_fia, fl.cd_unidade,
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

    // ── Q1 — Agora (tick rápido) ───────────────────────────────────────────────

    /// <summary>Q1a: aguardando médico agora, por cor (chegada nas últimas 12h, sem saída, sem doc médico).</summary>
    public static string Q1AguardandoPorCor(int hospital) => $"""
        SELECT NVL(cr.ds_classificacao_risco,'SEM_CLASSIFICACAO') AS cor, COUNT(*) AS qtd,
               ROUND(AVG((SYSDATE - b.dt_chegada) * 1440), 0) AS min_medio_desde_chegada
          FROM infosaude.baa b
          LEFT JOIN infosaude.classificacao_risco cr ON cr.cd_classificacao_risco = b.cd_classificacao_risco
         WHERE b.cd_hospital = {hospital} AND b.in_emergencia = 'S'
           AND b.dt_chegada >= SYSDATE - 0.5
           AND b.dt_saida IS NULL
           AND NOT EXISTS (SELECT 1 FROM infosaude.edoc_movimento mv
                            WHERE mv.cd_hospital = {hospital} AND mv.cd_modelo IN (10036,10232,10014)
                              AND mv.dt_ano_baa = b.dt_ano_baa AND mv.nr_baa = b.nr_baa)
         GROUP BY cr.ds_classificacao_risco
        """;

    /// <summary>Q1b: em atendimento/observação (mesma janela, COM doc médico, sem saída).</summary>
    public static string Q1EmAtendimento(int hospital) => $"""
        SELECT COUNT(*) AS em_atendimento
          FROM infosaude.baa b
         WHERE b.cd_hospital = {hospital} AND b.in_emergencia = 'S'
           AND b.dt_chegada >= SYSDATE - 0.5 AND b.dt_saida IS NULL
           AND EXISTS (SELECT 1 FROM infosaude.edoc_movimento mv
                        WHERE mv.cd_hospital = {hospital} AND mv.cd_modelo IN (10036,10232,10014)
                          AND mv.dt_ano_baa = b.dt_ano_baa AND mv.nr_baa = b.nr_baa)
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
          SELECT mv.dt_ano_baa, mv.nr_baa, MIN(mv.dt_inclusao) AS dt_med
            FROM infosaude.edoc_movimento mv
           WHERE mv.cd_hospital = {hospital}
             AND mv.cd_modelo IN (10036, 10232, 10014)
             AND mv.dt_inclusao >= {ini} AND mv.dt_inclusao < {fimDoc}
           GROUP BY mv.dt_ano_baa, mv.nr_baa
        )
        SELECT NVL(cr.ds_classificacao_risco,'SEM_CLASSIFICACAO')                     AS cor,
               COUNT(*)                                                              AS pacientes,
               SUM(CASE WHEN d.dt_med IS NOT NULL THEN 1 ELSE 0 END)                 AS com_atendimento,
               ROUND(AVG((b.dt_classifica_atual - b.dt_chegada) * 1440), 1)          AS media_ate_triagem,
               ROUND(AVG((d.dt_med - b.dt_classifica_atual) * 1440), 1)              AS media_espera,
               ROUND(MEDIAN((d.dt_med - b.dt_classifica_atual) * 1440), 1)           AS mediana_espera,
               ROUND(PERCENTILE_CONT(0.9) WITHIN GROUP
                     (ORDER BY (d.dt_med - b.dt_classifica_atual) * 1440), 1)        AS p90_espera,
               MAX(cr.qt_tempo)                                                      AS meta_min,
               ROUND(100 * SUM(CASE WHEN (d.dt_med - b.dt_classifica_atual) * 1440
                                         <= cr.qt_tempo THEN 1 ELSE 0 END)
                     / NULLIF(SUM(CASE WHEN d.dt_med IS NOT NULL THEN 1 ELSE 0 END),0), 1) AS pct_na_meta
          FROM b
          LEFT JOIN d ON d.dt_ano_baa = b.dt_ano_baa AND d.nr_baa = b.nr_baa
          LEFT JOIN infosaude.classificacao_risco cr
                 ON cr.cd_classificacao_risco = b.cd_classificacao_risco
         WHERE b.dt_classifica_atual IS NOT NULL
           AND (d.dt_med IS NULL OR d.dt_med >= b.dt_classifica_atual)
         GROUP BY cr.ds_classificacao_risco, cr.cd_classificacao_risco
         ORDER BY cr.cd_classificacao_risco
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
    /// Ocupados = pacientes reais no leito atual; bloqueados = <c>id_sit_leito = 'F'</c>.
    /// </summary>
    public static string L1OcupacaoPorSetor(int hospital) => $"""
        WITH la AS (
        {LeitoAtual(hospital)}
        ), pac AS (
          SELECT la.cd_unidade, COUNT(*) AS ocupados
            FROM infosaude.fia f
            JOIN la ON la.dt_ano_fia = f.dt_ano_fia AND la.nr_fia = f.nr_fia AND la.rn = 1
        {InternadosAgora(hospital)}
           GROUP BY la.cd_unidade
        ), lei AS (
          SELECT l.cd_unidade, COUNT(*) AS leitos,
                 SUM(CASE WHEN l.id_sit_leito = 'F' THEN 1 ELSE 0 END) AS bloqueados
            FROM infosaude.leito l
            JOIN infosaude.unidade_hospitalar u
              ON u.cd_hospital = l.cd_hospital AND u.cd_unidade = l.cd_unidade
           WHERE l.cd_hospital = {hospital} AND u.id_condicao_unidade = 'A'
           GROUP BY l.cd_unidade
        )
        SELECT u.sc_unidade AS setor, NVL(lei.leitos,0) AS leitos,
               NVL(lei.bloqueados,0) AS bloqueados, NVL(pac.ocupados,0) AS ocupados
          FROM infosaude.unidade_hospitalar u
          LEFT JOIN lei ON lei.cd_unidade = u.cd_unidade
          LEFT JOIN pac ON pac.cd_unidade = u.cd_unidade
         WHERE u.cd_hospital = {hospital} AND u.id_condicao_unidade = 'A'
           AND (NVL(lei.leitos,0) > 0 OR NVL(pac.ocupados,0) > 0)
         ORDER BY NVL(pac.ocupados,0) DESC, NVL(lei.leitos,0) DESC
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
