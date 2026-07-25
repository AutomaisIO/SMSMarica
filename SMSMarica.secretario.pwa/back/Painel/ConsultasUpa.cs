namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// Consultas das UPAs (SQL Server 2014, alcançadas pelo agente WSS reverso — ver
/// ADR-0023). Servem as DUAS unidades, que rodam o mesmo HIS em instâncias separadas:
/// <c>upa24h-marica-sqlserver</c> (UPA Maricá, servidor UPA-SRV) e
/// <c>santarita-marica-sqlserver</c> (UPAM Santa Rita, servidor STARITA-SRV). Transcritas
/// de <c>docs/consultas-sqlserver-upa.md</c>, validadas contra as duas bases em 25/07/2026.
///
/// <para>
/// <b>As duas bases se chamam <c>UPA24H</c></b> e cada uma tem UMA linha em <c>Unidade</c>
/// — o que muda é o <c>unid_codigo</c> (0006 e 0007). Não dá para distinguir as unidades
/// pelo nome do banco, só pelo slug do proxy.
/// </para>
///
/// <para>
/// O desenho espelha as consultas do Conde (<see cref="ConsultasPainel"/>) para que o
/// contrato do painel seja o mesmo, mas o modelo por baixo é OUTRO. Três diferenças
/// mandam no código abaixo:
/// </para>
///
/// <list type="number">
///   <item><b>A gravidade não é uma escala global.</b> <c>risco_acolhimento.risaco_gravidade</c>
///   é relativa ao PROTOCOLO (<c>proate_codigo</c>): gravidade 5 é Laranja no protocolo
///   0002, Amarelo/Observação no 0005 e Laranja/Consultório no 0006. A cor tem que sair
///   de <c>risaco_descricao</c>, pelo join em <c>risaco_codigo</c> (que é único entre os
///   protocolos). A UPA usou dois protocolos na mesma janela (0005 e 0006).</item>
///
///   <item><b>Não há "data de saída" no boletim.</b> Quem já foi embora sem passar pelo
///   médico continua parecendo que espera. O que separa fila real de fantasma é
///   <c>UPA_Fila.DATA_CANCELAMENTO</c>: em 24/07 havia 18 boletins sem médico nas
///   últimas 12h, e os 13 com mais de 3h de espera estavam TODOS cancelados na fila.
///   Sem esse filtro o painel mostraria uma fila 5× maior do que a real.</item>
///
///   <item><b>As UPAs não internam.</b> A tabela <c>Internacao</c> parou de ser alimentada
///   em 25/01/2026 (972 linhas no total, nenhuma nos últimos 30 dias) e
///   <c>UPA_CONSOLIDACAO_PACIENTE_OBSERVACAO</c> aponta para internações dessa mesma
///   safra morta. Por isso a unidade não manda seção de internações nem de maternidade
///   — vem nula no contrato, em vez de zero (zero seria mentira).</item>
/// </list>
///
/// <para>
/// As METAS do cadastro também mudam de unidade para unidade, mesmo no mesmo protocolo:
/// no 0005, Verde é 120 min na UPA Maricá e 60 min em Santa Rita. Isso é tratado no
/// consolidado da rede, que reporta meta nula quando os alvos divergem.
/// </para>
/// </summary>
public static class ConsultasUpa
{
    /// <summary>
    /// Código da unidade dentro da base. Cada instância do HIS atende UMA unidade (a
    /// tabela <c>Unidade</c> tem uma linha só em cada uma), mas o código difere: a UPA
    /// Maricá é <c>0006</c> no servidor UPA-SRV e a UPAM Santa Rita é <c>0007</c> no
    /// STARITA-SRV — e as duas bases se chamam <c>UPA24H</c>, então não dá para
    /// identificar a unidade pelo nome do banco.
    /// </summary>
    public const string UnidadeUpaMarica = "0006";
    public const string UnidadeSantaRita = "0007";

    // ── Expressões de período (constantes — espelham ConsultasPainel) ──────────
    public const string IniMesAnterior = "DATEADD(month,-1,DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1))";
    public const string FimMesAnterior = "DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1)";
    public const string IniMesAtual = "DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1)";
    public const string FimMesAtual = "GETDATE()";
    public const string IniHoje = "CAST(GETDATE() AS date)";
    public const string FimHoje = "GETDATE()";
    public const string IniOntem = "DATEADD(day,-1,CAST(GETDATE() AS date))";
    public const string FimOntem = "CAST(GETDATE() AS date)";

    /// <summary>Fim "dias completos" do mês atual (exclui o dia corrente, parcial).</summary>
    public const string FimDiasCompletos = "CAST(GETDATE() AS date)";

    /// <summary>
    /// Boletins da UPA numa janela. <c>spa_chegada</c> é a abertura do boletim — o
    /// equivalente do <c>BAA.dt_atendimento</c> do Salux.
    /// </summary>
    private static string Boletins(string unidade, string ini, string fim) => $"""
          SELECT p.spa_codigo, p.spa_chegada
            FROM Pronto_Atendimento p
           WHERE p.unid_codigo = '{unidade}'
             AND p.spa_chegada >= {ini} AND p.spa_chegada < {fim}
        """;

    /// <summary>
    /// Primeira classificação de risco de cada boletim. A folga de 1 dia nas duas pontas
    /// cobre o boletim aberto perto da virada e triado logo depois.
    /// </summary>
    private static string PrimeiraClassificacao(string unidade, string ini, string fim) => $"""
          SELECT ac.spa_codigo, cr.risaco_codigo, cr.upaclaris_datahora,
                 ROW_NUMBER() OVER (PARTITION BY ac.spa_codigo
                                    ORDER BY cr.upaclaris_datahora, cr.upaclaris_codigo) rn
            FROM UPA_ACOLHIMENTO ac
            JOIN UPA_Classificacao_Risco cr ON cr.aco_codigo = ac.ACO_CODIGO
           WHERE ac.UNID_CODIGO = '{unidade}'
             AND ac.ACO_DATAHORA >= DATEADD(day,-1,{ini})
             AND ac.ACO_DATAHORA <  DATEADD(day, 1,{fim})
        """;

    /// <summary>
    /// Início do atendimento médico por boletim. É <c>atendimento_ambulatorial</c> que
    /// carrega o relógio (<c>atendamb_datainicio</c>/<c>atendamb_datafinal</c>) — cobre
    /// 96,2% dos boletins. <c>Atendimento_Emergencia</c>, apesar do nome promissor, NÃO
    /// serve: o join por <c>emer_codigo</c> devolve zero linha nesta base.
    /// </summary>
    private static string AtendimentoMedico(string ini, string fim) => $"""
          SELECT aa.spa_codigo,
                 MIN(aa.atendamb_datainicio) AS dt_med,
                 MAX(aa.atendamb_datafinal)  AS dt_fim
            FROM atendimento_ambulatorial aa
           WHERE aa.atendamb_datainicio >= DATEADD(day,-1,{ini})
             AND aa.atendamb_datainicio <  {fim}
           GROUP BY aa.spa_codigo
        """;

    // ── U1 — Agora (tick rápido) ───────────────────────────────────────────────

    /// <summary>
    /// U1a: aguardando médico agora, por cor. Janela de 12h (a mesma do Conde), sem
    /// atendimento médico iniciado e AINDA NA FILA (não cancelada) — ver a armadilha 2
    /// na documentação da classe.
    /// </summary>
    public static string U1AguardandoPorCor(string unidade) => $"""
        WITH pa AS (
        {Boletins(unidade, "DATEADD(hour,-12,GETDATE())", "GETDATE()")}
        ), cl AS (
        {PrimeiraClassificacao(unidade, "DATEADD(hour,-36,GETDATE())", "GETDATE()")}
        ), am AS (
        {AtendimentoMedico("DATEADD(day,-2,GETDATE())", "GETDATE()")}
        )
        SELECT ISNULL(ra.risaco_descricao,'SEM_CLASSIFICACAO')            AS cor,
               COUNT(*)                                                   AS qtd,
               AVG(DATEDIFF(minute, pa.spa_chegada, GETDATE()))           AS min_medio_desde_chegada
          FROM pa
          LEFT JOIN cl ON cl.spa_codigo = pa.spa_codigo AND cl.rn = 1
          LEFT JOIN risco_acolhimento ra ON ra.risaco_codigo = cl.risaco_codigo
          LEFT JOIN am ON am.spa_codigo = pa.spa_codigo
         WHERE am.dt_med IS NULL
           -- EXISTS, não JOIN: a UPA_Fila tem reentradas do mesmo boletim (514 linhas
           -- para 474 boletins) e um join duplicaria o paciente na contagem da fila.
           AND EXISTS (SELECT 1 FROM UPA_Fila f
                        WHERE f.SPA_CODIGO = pa.spa_codigo AND f.DATA_CANCELAMENTO IS NULL)
         GROUP BY ra.risaco_descricao
        """;

    /// <summary>
    /// U1b: em atendimento (médico já iniciou, ainda sem encerrar) + atendimentos do dia.
    /// As duas contas vêm juntas para economizar ida ao agente da UPA.
    /// </summary>
    public static string U1EmAtendimentoEHoje(string unidade) => $"""
        WITH pa AS (
        {Boletins(unidade, "DATEADD(hour,-12,GETDATE())", "GETDATE()")}
        ), am AS (
        {AtendimentoMedico("DATEADD(day,-2,GETDATE())", "GETDATE()")}
        )
        SELECT (SELECT COUNT(*)
                  FROM pa
                  JOIN am ON am.spa_codigo = pa.spa_codigo
                 WHERE am.dt_med IS NOT NULL AND am.dt_fim IS NULL)          AS em_atendimento,
               (SELECT COUNT(*) FROM Pronto_Atendimento p
                 WHERE p.unid_codigo = '{unidade}'
                   AND p.spa_chegada >= CAST(GETDATE() AS date))            AS atendimentos_hoje
        """;

    // ── U2 — Atendimentos por período (tick lento) ─────────────────────────────

    public static string U2AtendimentosPeriodo(string unidade, string ini, string fim) => $"""
        SELECT COUNT(*) AS total FROM Pronto_Atendimento p
         WHERE p.unid_codigo = '{unidade}' AND p.spa_chegada >= {ini} AND p.spa_chegada < {fim}
        """;

    // ── U3 — Série diária de atendimentos (35 dias, tick lento) ────────────────

    public static string U3SerieDiariaAtendimentos(string unidade) => $"""
        SELECT CAST(p.spa_chegada AS date) AS dia, COUNT(*) AS qtd
          FROM Pronto_Atendimento p
         WHERE p.unid_codigo = '{unidade}'
           AND p.spa_chegada >= DATEADD(day,-34,CAST(GETDATE() AS date))
         GROUP BY CAST(p.spa_chegada AS date) ORDER BY 1
        """;

    // ── U4 — Atendimentos de hoje por hora (tick lento) ────────────────────────

    public static string U4PorHoraHoje(string unidade) => $"""
        SELECT DATEPART(hour, p.spa_chegada) AS hora, COUNT(*) AS qtd
          FROM Pronto_Atendimento p
         WHERE p.unid_codigo = '{unidade}' AND p.spa_chegada >= CAST(GETDATE() AS date)
         GROUP BY DATEPART(hour, p.spa_chegada) ORDER BY 1
        """;

    // ── U6 — Espera por cor (tick lento) ───────────────────────────────────────

    /// <summary>
    /// Tempo da classificação de risco até o primeiro atendimento médico, por cor —
    /// o mesmo indicador da Q6 do Conde. <paramref name="fimDoc"/> = fim + 3 dias, porque
    /// o atendimento pode ser lavrado depois do fim do período.
    ///
    /// <para>
    /// Boletim sem classificação entra como <c>SEM_CLASSIFICACAO</c> com os tempos nulos
    /// (8,9% do movimento): sem triagem não há de onde contar, mas o paciente existiu e
    /// não é descartado.
    /// </para>
    ///
    /// <para>
    /// <b>A meta é por VARIANTE, não por cor.</b> O cadastro quebra Amarelo e Laranja em
    /// Consultório (meta em minutos: Azul 240, Verde 120, Amarelo 60, Laranja 10) e
    /// Observação (sem meta) — e o painel mostra uma linha por cor. Colapsar com
    /// <c>MAX(meta)</c> produz número errado: em junho/2026, 2.038 dos 2.039 amarelos
    /// foram Observação (sem meta) e UM único foi Consultório, e esse um puxava a meta de
    /// 60 min para o grupo inteiro, jogando o "% na meta" para 0. Por isso a meta
    /// reportada é a da variante PREDOMINANTE (<c>rn = 1</c> em <c>modo</c>) e o
    /// percentual só considera os pacientes que caíram nessa variante. Quando a
    /// predominante não tem meta cadastrada, os dois campos vêm nulos — a pulseira
    /// mostra "sem meta definida" em vez de um zero que ninguém sabe de onde veio.
    /// </para>
    /// </summary>
    public static string U6EsperaPorCor(string unidade, string ini, string fim, string fimDoc) => $"""
        WITH pa AS (
        {Boletins(unidade, ini, fim)}
        ), cl AS (
        {PrimeiraClassificacao(unidade, ini, fim)}
        ), am AS (
        {AtendimentoMedico(ini, fimDoc)}
        ), e AS (
          SELECT ISNULL(ra.risaco_descricao,'SEM_CLASSIFICACAO') AS cor,
                 ra.risaco_Tempo_Espera                          AS meta,
                 CASE WHEN cl.upaclaris_datahora >= pa.spa_chegada
                      THEN DATEDIFF(minute, pa.spa_chegada, cl.upaclaris_datahora) END AS ate_triagem,
                 CASE WHEN am.dt_med >= cl.upaclaris_datahora
                      THEN DATEDIFF(minute, cl.upaclaris_datahora, am.dt_med) END      AS espera
            FROM pa
            LEFT JOIN cl ON cl.spa_codigo = pa.spa_codigo AND cl.rn = 1
            LEFT JOIN risco_acolhimento ra ON ra.risaco_codigo = cl.risaco_codigo
            LEFT JOIN am ON am.spa_codigo = pa.spa_codigo
        ), modo AS (
          -- O desempate por meta é só para o resultado não oscilar entre um ciclo e
          -- outro quando duas variantes empatam em volume.
          SELECT cor, meta,
                 ROW_NUMBER() OVER (PARTITION BY cor ORDER BY COUNT(*) DESC, meta) AS rn
            FROM e GROUP BY cor, meta
        ), p AS (
          SELECT e.cor, e.ate_triagem, e.espera,
                 modo.meta AS meta_predominante,
                 CASE WHEN e.meta = modo.meta THEN e.espera END AS espera_com_meta,
                 PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY e.espera) OVER (PARTITION BY e.cor) AS p50,
                 PERCENTILE_CONT(0.9) WITHIN GROUP (ORDER BY e.espera) OVER (PARTITION BY e.cor) AS p90
            FROM e JOIN modo ON modo.cor = e.cor AND modo.rn = 1
        )
        SELECT cor,
               COUNT(*)                                                     AS pacientes,
               SUM(CASE WHEN espera IS NOT NULL THEN 1 ELSE 0 END)          AS com_atendimento,
               ROUND(AVG(CAST(ate_triagem AS float)), 1)                    AS media_ate_triagem,
               ROUND(AVG(CAST(espera AS float)), 1)                         AS media_espera,
               ROUND(MAX(p50), 1)                                           AS mediana_espera,
               ROUND(MAX(p90), 1)                                           AS p90_espera,
               MAX(meta_predominante)                                       AS meta_min,
               CASE WHEN MAX(meta_predominante) IS NULL THEN NULL ELSE
                 ROUND(100.0 * SUM(CASE WHEN espera_com_meta <= meta_predominante THEN 1 ELSE 0 END)
                       / NULLIF(SUM(CASE WHEN espera_com_meta IS NOT NULL THEN 1 ELSE 0 END),0), 1) END AS pct_na_meta
          FROM p
         GROUP BY cor
        """;

    // ── L4/L5 — Leitos de observação (tick lento) ──────────────────────────────

    /// <summary>
    /// Abaixo disso o cadastro de leitos não descreve a unidade, descreve o abandono do
    /// cadastro: Santa Rita tem DOIS leitos registrados para 262 atendimentos/dia, e o
    /// <c>rowversion</c> da tabela está ~13,6 milhões de modificações atrás do banco
    /// (contra alguns milhares nas tabelas vivas). Nesse caso o painel diz que o dado
    /// falta, em vez de publicar "0 de 2" e sugerir unidade vazia.
    ///
    /// É limiar, não lista fixa: no dia em que a unidade cadastrar os leitos, o painel
    /// volta a mostrar ocupação sozinho.
    /// </summary>
    public const int MinimoLeitosCadastrados = 5;

    /// <summary>
    /// L4: leitos de observação por setor. As UPAs não internam — o que existe aqui é
    /// observação, e o cadastro é raso: um único setor ("URGÊNCIA/OBSERVAÇÃO"), sem nome
    /// de enfermaria e sem tipo de leito preenchido. Agrupar pelo setor é o máximo de
    /// detalhe honesto; quebrar pelos <c>locatend_codigo</c> (0004..0007) daria quatro
    /// linhas numeradas que não dizem nada a quem lê.
    ///
    /// <c>lei_status</c>: L = livre, O = ocupado. Não há status de bloqueio nestas bases.
    /// </summary>
    public static string L4LeitosObservacao() => """
        SELECT ISNULL(s.set_descricao, 'Observação')                     AS setor,
               COUNT(*)                                                  AS leitos,
               SUM(CASE WHEN l.lei_status = 'O' THEN 1 ELSE 0 END)       AS ocupados
          FROM Leito l
          LEFT JOIN Local_Atendimento la ON la.locatend_codigo = l.locatend_codigo
          LEFT JOIN setor s ON s.set_codigo = la.set_codigo
         GROUP BY s.set_descricao
         ORDER BY 2 DESC
        """;

    /// <summary>
    /// L5: quantos passaram pela observação no período. Sai da SUBDESCRIÇÃO da
    /// classificação de risco — o cadastro separa Amarelo/Laranja em "Consultório" e
    /// "Observação", e é essa escolha da triagem que encaminha o paciente ao leito.
    /// É medida de FLUXO (quantos foram), não de ocupação (quantos estão) — as duas
    /// aparecem juntas na tela justamente porque respondem perguntas diferentes.
    /// </summary>
    public static string L5FluxoObservacao(string unidade, string ini, string fim) => $"""
        SELECT COUNT(*)                                                          AS classificados,
               SUM(CASE WHEN ra.risaco_subdescricao LIKE '%bserva%' THEN 1 ELSE 0 END) AS encaminhados
          FROM Pronto_Atendimento pa
          JOIN UPA_ACOLHIMENTO ac ON ac.spa_codigo = pa.spa_codigo AND ac.UNID_CODIGO = pa.unid_codigo
          JOIN UPA_Classificacao_Risco cr ON cr.aco_codigo = ac.ACO_CODIGO
          JOIN risco_acolhimento ra ON ra.risaco_codigo = cr.risaco_codigo
         WHERE pa.unid_codigo = '{unidade}'
           AND pa.spa_chegada >= {ini} AND pa.spa_chegada < {fim}
        """;
}
