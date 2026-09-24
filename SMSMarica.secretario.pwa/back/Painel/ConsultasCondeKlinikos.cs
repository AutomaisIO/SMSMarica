namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// Conector ADICIONAL do Conde Modesto Leal: o Klinikos (SQL Server 2014, banco
/// <c>KLINIKOSNET</c>, servidor CONDE-SRV, <c>unid_codigo='0005'</c>), alcançado pelo agente
/// WSS <c>conde-marica-sqlserver</c>. NÃO substitui o conector Salux (<see cref="ConsultasPainel"/>):
/// os dois convivem e a escolha é de configuração (<see cref="PainelOpcoes.FonteConde"/>).
///
/// <para>
/// <b>Por que existe.</b> O Conde saiu do Salux para o Klinikos em agosto/2026: o primeiro
/// dia cheio de boletins no Klinikos é 07/08/2026 (~430/dia) e o Salux do hospital 1 parou
/// em 08/08 (BAA, FIA e o livro de partos — o último BAA solto é de 21/08). Desde então a
/// aba do Conde servia número congelado.
/// </para>
///
/// <para>
/// <b>Emergência reaproveita as consultas das UPAs</b> (<see cref="ConsultasUpa"/>) — é o
/// mesmo HIS, com as mesmas tabelas (<c>Pronto_Atendimento</c>, <c>UPA_ACOLHIMENTO</c>,
/// <c>UPA_Classificacao_Risco</c>, <c>atendimento_ambulatorial</c>, <c>UPA_Fila</c>), medido
/// em 24/09/2026: 3 dias, 1.761 boletins, 100% com acolhimento, 94% classificados, 91% com
/// atendimento médico. Só entram aqui as consultas que as UPAs não têm (internação, leitos,
/// CID) e que dependem de diferenças desta instalação:
/// </para>
///
/// <list type="number">
///   <item><b>Build diferente (K.2024 × U.2025 das UPAs).</b> <c>UPA_Evolucao</c> aqui NÃO
///   tem <c>SPA_CODIGO</c>: liga-se ao boletim por <c>atendamb_codigo</c> →
///   <c>atendimento_ambulatorial.spa_codigo</c>.</item>
///   <item><b>CID vem da evolução</b> (<c>cid_codigo_primario</c>, 83% dos boletins em 7
///   dias). <c>atendimento_ambulatorial.cid_codigo</c> existe e está vazio (0 de 3.642).</item>
///   <item><b>O Conde interna pelo Klinikos</b> (<c>Internacao</c> viva: 484 em agosto, 455
///   até 24/09), ao contrário das UPAs, onde a tabela morreu em 25/01/2026.</item>
///   <item><b>Leito:</b> <c>lei_status</c> O = ocupado (bate com as internações abertas:
///   140 de 142), L = livre, I = bloqueado (5), D = desativado (65 — nenhum com paciente).
///   Capacidade = L + O.</item>
///   <item><b>Maternidade = setor OBSTETRÍCIA.</b> Os partos NÃO são registrados de forma
///   estruturada nesta base (<c>recem_nascido</c>, <c>Dados_Parto</c>, <c>FichaAdmissaoRN</c>
///   vazias; a AIH só tem 17 partos em setembro) — por isso a seção Maternidade do contrato
///   vem nula neste conector, em vez de zerada.</item>
/// </list>
/// </summary>
public static class ConsultasCondeKlinikos
{
    /// <summary>Código da unidade do Conde dentro do KLINIKOSNET (uma linha só em <c>Unidade</c>).</summary>
    public const string UnidadeConde = "0005";

    /// <summary>Setor que conta como maternidade nas faixas de internação.</summary>
    private const string EhMaternidade = "s.set_descricao LIKE 'OBSTETR%'";
    private const string NaoEhMaternidade = "(s.set_descricao IS NULL OR s.set_descricao NOT LIKE 'OBSTETR%')";

    /// <summary>Idade em anos completos numa data (DATEDIFF(year) sozinho arredonda para cima no aniversário).</summary>
    private static string Idade(string referencia) => $"""
        (DATEDIFF(year, p.pac_nascimento, {referencia})
               - CASE WHEN DATEADD(year, DATEDIFF(year, p.pac_nascimento, {referencia}), p.pac_nascimento) > {referencia}
                      THEN 1 ELSE 0 END)
        """;

    /// <summary>
    /// Internação + paciente + setor ONDE ESTÁ (o local do leito; na falta, o local atual
    /// da internação). É o setor que separa a maternidade — a mesma régua do Salux, que
    /// classificava pela unidade do leito atual.
    /// </summary>
    private const string DeInternacaoComSetor = """
          FROM Internacao i
          JOIN paciente p ON p.pac_codigo = i.pac_codigo
          LEFT JOIN Local_Atendimento la
                 ON la.locatend_codigo = COALESCE(i.locatend_leito, i.locatend_codigo_atual, i.locatend_codigo)
          LEFT JOIN setor s ON s.set_codigo = la.set_codigo
        """;

    /// <summary>
    /// Internados agora. A janela de 120 dias é a mesma do Salux: internação esquecida
    /// aberta há meses não é paciente no leito.
    /// </summary>
    private const string InternadosAgora =
        "i.inter_dtalta IS NULL AND i.inter_datainter >= DATEADD(day,-120,GETDATE())";

    private static string FaixasInternacao(string referencia) => $"""
               SUM(CASE WHEN {EhMaternidade} THEN 1 ELSE 0 END) AS maternidade,
               SUM(CASE WHEN {NaoEhMaternidade} AND {Idade(referencia)} <= 17 THEN 1 ELSE 0 END) AS ate17,
               SUM(CASE WHEN {NaoEhMaternidade}
                         AND (p.pac_nascimento IS NULL OR {Idade(referencia)} > 17) THEN 1 ELSE 0 END) AS adultos
        """;

    // ── K1c — internados agora + totais de hoje (tick rápido) ─────────────────

    /// <summary>
    /// Mesmo shape da <c>ConsultasPainel.Q1InternadosEHoje</c>: total, maternidade, ate17,
    /// adultos, média de dias internado, atendimentos de hoje, internações de hoje.
    /// </summary>
    public static string K1InternadosEHoje() => $"""
        SELECT i2.internados_agora, i2.maternidade, i2.ate17, i2.adultos, i2.media_dias_internado,
               (SELECT COUNT(*) FROM Pronto_Atendimento pa
                 WHERE pa.unid_codigo = '{UnidadeConde}'
                   AND pa.spa_chegada >= CAST(GETDATE() AS date))                    AS atendimentos_hoje,
               (SELECT COUNT(*) FROM Internacao ih
                 WHERE ih.inter_datainter >= CAST(GETDATE() AS date))                AS internacoes_hoje
          FROM (
            SELECT COUNT(*) AS internados_agora,
            {FaixasInternacao("GETDATE()")},
                   ROUND(AVG(CAST(DATEDIFF(minute, i.inter_datainter, GETDATE()) AS float) / 1440.0), 1)
                                                                                     AS media_dias_internado
            {DeInternacaoComSetor}
             WHERE {InternadosAgora}
          ) i2
        """;

    // ── K5 — internações por período + série (tick lento) ─────────────────────

    /// <summary>Entradas na internação no período (<c>inter_datainter</c>), nas três faixas.</summary>
    public static string K5InternacoesPeriodo(string ini, string fim) => $"""
        SELECT COUNT(*) AS total,
        {FaixasInternacao("i.inter_datainter")}
        {DeInternacaoComSetor}
         WHERE i.inter_datainter >= {ini} AND i.inter_datainter < {fim}
        """;

    public static string K5SerieDiariaInternacoes() => $"""
        SELECT CAST(i.inter_datainter AS date) AS dia, COUNT(*) AS qtd,
        {FaixasInternacao("i.inter_datainter")}
        {DeInternacaoComSetor}
         WHERE i.inter_datainter >= DATEADD(day,-34,CAST(GETDATE() AS date))
         GROUP BY CAST(i.inter_datainter AS date) ORDER BY 1
        """;

    // ── K-L1..L3 — leitos, perfil e permanência (tick lento) ──────────────────

    /// <summary>
    /// Ocupação por setor, no MESMO shape da <c>ConsultasPainel.L1OcupacaoPorSetor</c>
    /// (setor, capacidade, bloqueados, ocupados, fora_capacidade, extras, virtuais,
    /// desativados). Ocupados são PACIENTES (internação aberta), não o flag do leito — a
    /// mesma regra do Salux, para casar com o "internados agora". Esta base não tem leito
    /// extra nem virtual: as duas colunas vêm zeradas. Paciente em leito desativado ou
    /// bloqueado conta como fora da capacidade (segue no numerador).
    /// </summary>
    public static string L1OcupacaoPorSetor() => $"""
        WITH lei AS (
          SELECT ISNULL(la.set_codigo,'?') AS set_codigo,
                 SUM(CASE WHEN l.lei_status IN ('L','O') THEN 1 ELSE 0 END) AS capacidade,
                 SUM(CASE WHEN l.lei_status = 'I' THEN 1 ELSE 0 END)        AS bloqueados,
                 SUM(CASE WHEN l.lei_status = 'D' THEN 1 ELSE 0 END)        AS desativados
            FROM Leito l
            LEFT JOIN Local_Atendimento la ON la.locatend_codigo = l.locatend_codigo
           GROUP BY ISNULL(la.set_codigo,'?')
        ), pac AS (
          SELECT ISNULL(la.set_codigo,'?') AS set_codigo, COUNT(*) AS ocupados,
                 SUM(CASE WHEN l.lei_status IN ('L','O') THEN 0 ELSE 1 END) AS fora_capacidade
            FROM Internacao i
            LEFT JOIN Leito l ON l.lei_codigo = i.lei_codigo AND l.locatend_codigo = i.locatend_leito
            LEFT JOIN Local_Atendimento la
                   ON la.locatend_codigo = COALESCE(i.locatend_leito, i.locatend_codigo_atual, i.locatend_codigo)
           WHERE {InternadosAgora}
           GROUP BY ISNULL(la.set_codigo,'?')
        ), k AS (
          SELECT set_codigo FROM lei UNION SELECT set_codigo FROM pac
        )
        SELECT ISNULL(s.set_descricao,'Sem setor')  AS setor,
               ISNULL(lei.capacidade,0)             AS capacidade,
               ISNULL(lei.bloqueados,0)             AS bloqueados,
               ISNULL(pac.ocupados,0)               AS ocupados,
               ISNULL(pac.fora_capacidade,0)        AS fora_capacidade,
               0                                    AS extras,
               0                                    AS virtuais,
               ISNULL(lei.desativados,0)            AS desativados
          FROM k
          LEFT JOIN lei ON lei.set_codigo = k.set_codigo
          LEFT JOIN pac ON pac.set_codigo = k.set_codigo
          LEFT JOIN setor s ON s.set_codigo = k.set_codigo
         WHERE ISNULL(lei.capacidade,0) > 0 OR ISNULL(pac.ocupados,0) > 0 OR ISNULL(lei.desativados,0) > 0
         ORDER BY ISNULL(pac.ocupados,0) DESC, ISNULL(lei.capacidade,0) DESC
        """;

    /// <summary>Perfil de quem está internado agora — shape da <c>ConsultasPainel.L2PerfilInternados</c>.</summary>
    public static string L2PerfilInternados() => $"""
        SELECT COUNT(*) AS internados,
               SUM(CASE WHEN p.pac_sexo = 'M' THEN 1 ELSE 0 END) AS homens,
               SUM(CASE WHEN p.pac_sexo = 'F' THEN 1 ELSE 0 END) AS mulheres,
               SUM(CASE WHEN p.pac_sexo IS NULL OR p.pac_sexo NOT IN ('M','F') THEN 1 ELSE 0 END) AS sem_sexo,
               SUM(CASE WHEN {Idade("GETDATE()")} <= 17 THEN 1 ELSE 0 END) AS ate17,
               SUM(CASE WHEN {Idade("GETDATE()")} BETWEEN 18 AND 59 THEN 1 ELSE 0 END) AS adultos,
               SUM(CASE WHEN {Idade("GETDATE()")} >= 60 THEN 1 ELSE 0 END) AS idosos,
               ROUND(AVG(CAST({Idade("GETDATE()")} AS float)), 1) AS idade_media,
               ROUND(AVG(CAST(DATEDIFF(minute, i.inter_datainter, GETDATE()) AS float) / 1440.0), 1) AS dias_medios
          FROM Internacao i
          JOIN paciente p ON p.pac_codigo = i.pac_codigo
         WHERE {InternadosAgora}
        """;

    /// <summary>
    /// Permanência das ALTAS do período — shape da <c>ConsultasPainel.L3Permanencia</c>
    /// (uma linha por segmento, TOTAL primeiro). SQL Server 2014 não tem MEDIAN nem
    /// PERCENTILE_CONT agregado: mediana e p90 saem da forma analítica (OVER ()).
    /// </summary>
    public static string L3Permanencia(string ini, string fim) => $"""
        WITH altas AS (
          SELECT CAST(DATEDIFF(minute, i.inter_datainter, i.inter_dtalta) AS float) / 1440.0 AS dias,
                 p.pac_sexo AS sexo,
                 {Idade("i.inter_datainter")} AS idade
            FROM Internacao i
            JOIN paciente p ON p.pac_codigo = i.pac_codigo
           WHERE i.inter_dtalta >= {ini} AND i.inter_dtalta < {fim}
             AND i.inter_dtalta >= i.inter_datainter
        ), q AS (
          SELECT TOP 1
                 PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY dias) OVER () AS mediana,
                 PERCENTILE_CONT(0.9) WITHIN GROUP (ORDER BY dias) OVER () AS p90
            FROM altas
        )
        SELECT 'TOTAL' AS segmento, COUNT(*) AS altas, ROUND(AVG(dias),1) AS media,
               (SELECT ROUND(mediana,1) FROM q) AS mediana,
               (SELECT ROUND(p90,1) FROM q) AS p90
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

    // ── K8 — Diagnósticos mais frequentes por cor (tick lento) ────────────────

    /// <summary>
    /// Top CIDs por cor — shape da <c>ConsultasPainel.Q8CidPorCor</c> (cor, cid, descrição,
    /// qtd, total_cor). O CID é o PRIMEIRO primário lavrado em evolução do boletim; a cor é
    /// a da primeira classificação. Boletim sem CID fica fora (o total é de quem tem CID,
    /// como no Salux).
    /// </summary>
    public static string K8CidPorCor(string ini, string fim, int topN) => $"""
        WITH pa AS (
          SELECT p.spa_codigo FROM Pronto_Atendimento p
           WHERE p.unid_codigo = '{UnidadeConde}'
             AND p.spa_chegada >= {ini} AND p.spa_chegada < {fim}
        ), cl AS (
          SELECT ac.spa_codigo, cr.risaco_codigo,
                 ROW_NUMBER() OVER (PARTITION BY ac.spa_codigo
                                    ORDER BY cr.upaclaris_datahora, cr.upaclaris_codigo) rn
            FROM UPA_ACOLHIMENTO ac
            JOIN UPA_Classificacao_Risco cr ON cr.aco_codigo = ac.ACO_CODIGO
           WHERE ac.UNID_CODIGO = '{UnidadeConde}'
             AND ac.ACO_DATAHORA >= DATEADD(day,-1,{ini}) AND ac.ACO_DATAHORA < DATEADD(day,1,{fim})
        ), ev AS (
          SELECT aa.spa_codigo, RTRIM(e.cid_codigo_primario) AS cid,
                 ROW_NUMBER() OVER (PARTITION BY aa.spa_codigo
                                    ORDER BY e.upaevo_datahora, e.upaevo_codigo) rn
            FROM UPA_Evolucao e
            JOIN atendimento_ambulatorial aa ON aa.atendamb_codigo = e.atendamb_codigo
           WHERE e.upaevo_datahora >= DATEADD(day,-1,{ini}) AND e.upaevo_datahora < DATEADD(day,3,{fim})
             AND NULLIF(LTRIM(RTRIM(e.cid_codigo_primario)),'') IS NOT NULL
        ), b AS (
          SELECT ISNULL(ra.risaco_descricao,'SEM_CLASSIFICACAO') AS cor, ev.cid
            FROM pa
            JOIN ev ON ev.spa_codigo = pa.spa_codigo AND ev.rn = 1
            LEFT JOIN cl ON cl.spa_codigo = pa.spa_codigo AND cl.rn = 1
            LEFT JOIN {ConsultasUpa.RiscoAcolhimento} ra ON ra.risaco_codigo = cl.risaco_codigo
        ), g AS (
          SELECT cor, cid, COUNT(*) AS qtd,
                 SUM(COUNT(*)) OVER (PARTITION BY cor) AS total_cor,
                 ROW_NUMBER() OVER (PARTITION BY cor ORDER BY COUNT(*) DESC, cid) AS rn
            FROM b GROUP BY cor, cid
        )
        SELECT g.cor, g.cid, ISNULL(c.NO_CID, g.cid) AS ds_cid, g.qtd, g.total_cor
          FROM g
          LEFT JOIN TB_CID c ON c.CO_CID = g.cid
         WHERE g.rn <= {topN}
         ORDER BY g.cor, g.qtd DESC
        """;
}
