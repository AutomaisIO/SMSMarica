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

    /// <summary>Q1c: internados agora + atendimentos/internações de hoje.</summary>
    public static string Q1InternadosEHoje(int hospital) => $"""
        SELECT (SELECT COUNT(*) FROM infosaude.fia f
                 WHERE f.cd_hospital = {hospital} AND f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120)          AS internados_agora,
               (SELECT SUM(CASE WHEN f.id_internacao='U' THEN 1 ELSE 0 END) FROM infosaude.fia f
                 WHERE f.cd_hospital = {hospital} AND f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120)          AS internados_urgencia,
               (SELECT ROUND(AVG(SYSDATE - f.dt_baixa), 1) FROM infosaude.fia f
                 WHERE f.cd_hospital = {hospital} AND f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120)          AS media_dias_internado,
               (SELECT COUNT(*) FROM infosaude.baa b
                 WHERE b.cd_hospital = {hospital} AND b.dt_atendimento >= TRUNC(SYSDATE))                          AS atendimentos_hoje,
               (SELECT COUNT(*) FROM infosaude.fia f
                 WHERE f.cd_hospital = {hospital} AND f.dt_baixa >= TRUNC(SYSDATE))                                AS internacoes_hoje
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
        SELECT COUNT(*) AS total,
               SUM(CASE WHEN f.id_internacao='U' THEN 1 ELSE 0 END) AS urgencia,
               SUM(CASE WHEN f.id_internacao='E' THEN 1 ELSE 0 END) AS eletiva
          FROM infosaude.fia f
         WHERE f.cd_hospital = {hospital} AND f.dt_baixa >= {ini} AND f.dt_baixa < {fim}
        """;

    public static string Q5SerieDiariaInternacoes(int hospital) => $"""
        SELECT TRUNC(f.dt_baixa) AS dia, COUNT(*) AS qtd,
               SUM(CASE WHEN f.id_internacao='U' THEN 1 ELSE 0 END) AS urgencia,
               SUM(CASE WHEN f.id_internacao='E' THEN 1 ELSE 0 END) AS eletiva
          FROM infosaude.fia f
         WHERE f.cd_hospital = {hospital} AND f.dt_baixa >= TRUNC(SYSDATE) - 34
         GROUP BY TRUNC(f.dt_baixa) ORDER BY 1
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
