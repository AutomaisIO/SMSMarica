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

    /// <summary>Q1c: internados agora (com split por maternidade) + totais de hoje.</summary>
    public static string Q1InternadosEHoje(int hospital) => $"""
        WITH leito_atual AS (
        {LeitoAtual(hospital)}
        ), internados AS (
          SELECT NVL(la.cd_unidade,0) AS cd_unidade, f.dt_baixa
            FROM infosaude.fia f
            LEFT JOIN leito_atual la ON la.dt_ano_fia = f.dt_ano_fia AND la.nr_fia = f.nr_fia AND la.rn = 1
           WHERE f.cd_hospital = {hospital} AND f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120
        )
        SELECT (SELECT COUNT(*) FROM internados)                                                     AS internados_agora,
               (SELECT SUM(CASE WHEN cd_unidade IN ({UnidadesMaternidade}) THEN 1 ELSE 0 END)
                  FROM internados)                                                                   AS internados_maternidade,
               (SELECT ROUND(AVG(SYSDATE - dt_baixa), 1) FROM internados)                            AS media_dias_internado,
               (SELECT COUNT(*) FROM infosaude.baa b
                 WHERE b.cd_hospital = {hospital} AND b.dt_atendimento >= TRUNC(SYSDATE))            AS atendimentos_hoje,
               (SELECT COUNT(*) FROM infosaude.fia f
                 WHERE f.cd_hospital = {hospital} AND f.dt_baixa >= TRUNC(SYSDATE))                  AS internacoes_hoje
          FROM dual
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
        WITH leito_atual AS (
        {LeitoAtual(hospital)}
        )
        SELECT COUNT(*) AS total,
               SUM(CASE WHEN la.cd_unidade IN ({UnidadesMaternidade}) THEN 1 ELSE 0 END) AS maternidade,
               SUM(CASE WHEN la.cd_unidade IS NULL
                          OR la.cd_unidade NOT IN ({UnidadesMaternidade}) THEN 1 ELSE 0 END) AS demais
          FROM infosaude.fia f
          LEFT JOIN leito_atual la ON la.dt_ano_fia = f.dt_ano_fia AND la.nr_fia = f.nr_fia AND la.rn = 1
         WHERE f.cd_hospital = {hospital} AND f.dt_baixa >= {ini} AND f.dt_baixa < {fim}
        """;

    public static string Q5SerieDiariaInternacoes(int hospital) => $"""
        WITH leito_atual AS (
        {LeitoAtual(hospital)}
        )
        SELECT TRUNC(f.dt_baixa) AS dia, COUNT(*) AS qtd,
               SUM(CASE WHEN la.cd_unidade IN ({UnidadesMaternidade}) THEN 1 ELSE 0 END) AS maternidade,
               SUM(CASE WHEN la.cd_unidade IS NULL
                          OR la.cd_unidade NOT IN ({UnidadesMaternidade}) THEN 1 ELSE 0 END) AS demais
          FROM infosaude.fia f
          LEFT JOIN leito_atual la ON la.dt_ano_fia = f.dt_ano_fia AND la.nr_fia = f.nr_fia AND la.rn = 1
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
               SUM(CASE WHEN n.sexo='M' THEN 1 ELSE 0 END)                       AS meninos
          FROM infosaude.nascimento n
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
}
