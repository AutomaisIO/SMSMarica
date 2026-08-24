using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Cadastra o SQL do relatório analítico (a evidência linha a linha) de todos os indicadores
    /// que já têm motor — as 5 abas, não só a Adulto.
    ///
    /// Antes disso só o indicador 1 da aba Adulto tinha analítico, então a exportação em Excel
    /// saía com uma única planilha de evidência e o resto do arquivo era número sem lastro.
    ///
    /// Cada analítico é derivado do motor do próprio indicador e obedece ao mesmo contrato:
    /// as colunas livres que descrevem o registro, mais <c>incluido</c> (S/N) e
    /// <c>motivo_exclusao</c>. A diferença para o motor é deliberada — onde o motor
    /// <b>filtra</b> (e o registro some), o analítico <b>classifica</b> (e o registro aparece
    /// marcado com o porquê). Sem isso não há como provar o que ficou de fora da conta.
    ///
    /// Conferidos um a um contra o Oracle do Salux (hospital 1, julho/2026): para cada indicador,
    /// a contagem de <c>incluido = 'S'</c> bate com o denominador que o motor devolve, e a soma
    /// da coluna de valor bate com o numerador. As duas exceções estão comentadas junto do SQL:
    /// nas taxas de ocupação o denominador é leito-dia (outra granularidade), e nos indicadores
    /// absolutos <c>incluido</c> marca quem entrou no numerador.
    ///
    /// Idempotente e não destrutiva: só grava onde ainda não há analítico, para não passar por
    /// cima de ajuste feito na tela.
    /// </summary>
    /// <inheritdoc />
    public partial class AnaliticoTodosIndicadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Aba Adulto ----

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                             AS baa,
       ls.dt_hr_senha                                              AS chegada_senha,
       a.dt_atendimento                                            AS cadastro,
       b.dt_classifica_atual                                       AS classificacao_risco,
       ROUND((b.dt_classifica_atual - a.dt_atendimento) * 1440, 2) AS minutos,
       CASE WHEN a.dt_atendimento >= ls.dt_hr_senha
             AND b.dt_classifica_atual IS NOT NULL
             AND b.dt_classifica_atual >= a.dt_atendimento
            THEN 'S' ELSE 'N' END                                  AS incluido,
       CASE WHEN a.dt_atendimento < ls.dt_hr_senha
                 THEN 'cadastro anterior a senha (2a senha / carimbo herdado)'
            WHEN b.dt_classifica_atual IS NULL
                 THEN 'sem registro de classificacao de risco'
            WHEN b.dt_classifica_atual < a.dt_atendimento
                 THEN 'classificacao anterior ao cadastro (carimbo inconsistente)'
       END                                                         AS motivo_exclusao
  FROM infosaude.baa b
  JOIN infosaude.acolhimento a ON a.cd_acolhimento = b.cd_acolhimento
  JOIN infosaude.lista_senha ls
    ON ls.cd_hospital=a.cd_hospital_senha AND ls.cd_sala_painel=a.cd_sala_painel
   AND ls.dt_senha=a.dt_senha AND ls.cd_tipo_senha=a.cd_tipo_senha AND ls.nro_senha=a.nro_senha
 WHERE b.cd_hospital = :hospital AND b.dt_chegada >= :ini AND b.dt_chegada < :fim
   AND NVL(b.cd_setor,-1) NOT IN (38,46,50,60,71,39,48,51,67)
   AND ls.dt_hr_senha IS NOT NULL
   AND NVL(b.cd_classificacao_risco,-1) NOT IN
       (SELECT cr.cd_classificacao_risco FROM infosaude.classificacao_risco cr
         WHERE UPPER(cr.ds_classificacao_risco)='VERMELHO')
$analitico$
 WHERE aba = 1 AND numero = '2'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                            AS baa,
       cr.ds_classificacao_risco                                  AS cor,
       b.dt_classifica_atual                                      AS classificacao_risco,
       med.t_med                                                  AS primeiro_registro_medico,
       ROUND((med.t_med - b.dt_classifica_atual) * 1440, 2)       AS minutos,
       CASE WHEN b.dt_classifica_atual IS NOT NULL
             AND med.t_med IS NOT NULL
             AND med.t_med >= b.dt_classifica_atual
            THEN 'S' ELSE 'N' END                                 AS incluido,
       CASE WHEN b.dt_classifica_atual IS NULL
                 THEN 'sem registro de classificacao de risco'
            WHEN med.t_med IS NULL
                 THEN 'sem registro de atendimento medico no prontuario eletronico'
            WHEN med.t_med < b.dt_classifica_atual
                 THEN 'atendimento medico anterior a classificacao (carimbo inconsistente)'
       END                                                        AS motivo_exclusao
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr ON cr.cd_classificacao_risco = b.cd_classificacao_risco
  LEFT JOIN (SELECT m.nr_baa, m.dt_ano_baa, MIN(m.dt_inclusao) AS t_med
               FROM infosaude.edoc_movimento m
              WHERE m.cd_modelo IN (10036,10014,10232) AND m.baa_cd_hospital = :hospital
                AND m.dt_inclusao >= :ini
              GROUP BY m.nr_baa, m.dt_ano_baa) med
    ON med.nr_baa = b.nr_baa AND med.dt_ano_baa = b.dt_ano_baa
 WHERE b.cd_hospital = :hospital AND b.dt_chegada >= :ini AND b.dt_chegada < :fim
   AND NVL(b.cd_setor,-1) NOT IN (38,46,50,60,71,39,48,51,67)
   AND UPPER(cr.ds_classificacao_risco) = 'AMARELO'
$analitico$
 WHERE aba = 1 AND numero = '3.3'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                            AS baa,
       cr.ds_classificacao_risco                                  AS cor,
       b.dt_classifica_atual                                      AS classificacao_risco,
       med.t_med                                                  AS primeiro_registro_medico,
       ROUND((med.t_med - b.dt_classifica_atual) * 1440, 2)       AS minutos,
       CASE WHEN b.dt_classifica_atual IS NOT NULL
             AND med.t_med IS NOT NULL
             AND med.t_med >= b.dt_classifica_atual
            THEN 'S' ELSE 'N' END                                 AS incluido,
       CASE WHEN b.dt_classifica_atual IS NULL
                 THEN 'sem registro de classificacao de risco'
            WHEN med.t_med IS NULL
                 THEN 'sem registro de atendimento medico no prontuario eletronico'
            WHEN med.t_med < b.dt_classifica_atual
                 THEN 'atendimento medico anterior a classificacao (carimbo inconsistente)'
       END                                                        AS motivo_exclusao
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr ON cr.cd_classificacao_risco = b.cd_classificacao_risco
  LEFT JOIN (SELECT m.nr_baa, m.dt_ano_baa, MIN(m.dt_inclusao) AS t_med
               FROM infosaude.edoc_movimento m
              WHERE m.cd_modelo IN (10036,10014,10232) AND m.baa_cd_hospital = :hospital
                AND m.dt_inclusao >= :ini
              GROUP BY m.nr_baa, m.dt_ano_baa) med
    ON med.nr_baa = b.nr_baa AND med.dt_ano_baa = b.dt_ano_baa
 WHERE b.cd_hospital = :hospital AND b.dt_chegada >= :ini AND b.dt_chegada < :fim
   AND NVL(b.cd_setor,-1) NOT IN (38,46,50,60,71,39,48,51,67)
   AND UPPER(cr.ds_classificacao_risco) = 'VERDE'
$analitico$
 WHERE aba = 1 AND numero = '3.4'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                            AS baa,
       cr.ds_classificacao_risco                                  AS cor,
       b.dt_classifica_atual                                      AS classificacao_risco,
       med.t_med                                                  AS primeiro_registro_medico,
       ROUND((med.t_med - b.dt_classifica_atual) * 1440, 2)       AS minutos,
       CASE WHEN b.dt_classifica_atual IS NOT NULL
             AND med.t_med IS NOT NULL
             AND med.t_med >= b.dt_classifica_atual
            THEN 'S' ELSE 'N' END                                 AS incluido,
       CASE WHEN b.dt_classifica_atual IS NULL
                 THEN 'sem registro de classificacao de risco'
            WHEN med.t_med IS NULL
                 THEN 'sem registro de atendimento medico no prontuario eletronico'
            WHEN med.t_med < b.dt_classifica_atual
                 THEN 'atendimento medico anterior a classificacao (carimbo inconsistente)'
       END                                                        AS motivo_exclusao
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr ON cr.cd_classificacao_risco = b.cd_classificacao_risco
  LEFT JOIN (SELECT m.nr_baa, m.dt_ano_baa, MIN(m.dt_inclusao) AS t_med
               FROM infosaude.edoc_movimento m
              WHERE m.cd_modelo IN (10036,10014,10232) AND m.baa_cd_hospital = :hospital
                AND m.dt_inclusao >= :ini
              GROUP BY m.nr_baa, m.dt_ano_baa) med
    ON med.nr_baa = b.nr_baa AND med.dt_ano_baa = b.dt_ano_baa
 WHERE b.cd_hospital = :hospital AND b.dt_chegada >= :ini AND b.dt_chegada < :fim
   AND NVL(b.cd_setor,-1) NOT IN (38,46,50,60,71,39,48,51,67)
   AND UPPER(cr.ds_classificacao_risco) = 'AZUL'
$analitico$
 WHERE aba = 1 AND numero = '3.5'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                  AS baa,
       b.dt_chegada                                     AS chegada,
       b.dt_saida                                       AS saida,
       ROUND((b.dt_saida - b.dt_chegada) * 24, 2)       AS horas,
       CASE WHEN b.dt_saida IS NOT NULL
             AND b.dt_saida >= b.dt_chegada
             AND (b.dt_saida - b.dt_chegada) * 24 <= 72
            THEN 'S' ELSE 'N' END                       AS incluido,
       CASE WHEN b.dt_saida IS NULL
                 THEN 'sem registro de saida (boletim em aberto)'
            WHEN b.dt_saida < b.dt_chegada
                 THEN 'saida anterior a chegada (carimbo inconsistente)'
            WHEN (b.dt_saida - b.dt_chegada) * 24 > 72
                 THEN 'permanencia acima de 72h  -  boletim nao encerrado no ato da saida'
       END                                              AS motivo_exclusao
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital AND b.dt_chegada >= :ini AND b.dt_chegada < :fim
   AND NVL(b.cd_setor,-1) NOT IN (38,46,50,60,71,39,48,51,67)
$analitico$
 WHERE aba = 1 AND numero = '4'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
WITH pesq AS (
  SELECT DISTINCT m.baa_cd_hospital AS cd_hospital, m.dt_ano_baa, m.nr_baa
    FROM infosaude.edoc_movimento m
    JOIN infosaude.edoc_modelo md ON md.cd_modelo = m.cd_modelo
   WHERE m.cd_hospital = :hospital AND m.in_status = 'D' AND m.in_ativo = 'S'
     AND UPPER(md.ds_modelo) LIKE '%SATISFA%' AND m.nr_baa IS NOT NULL
)
SELECT b.nr_baa || '/' || b.dt_ano_baa                             AS baa,
       b.dt_chegada                                                AS chegada,
       b.dt_atendimento                                            AS atendimento_medico,
       CASE WHEN p.nr_baa IS NULL THEN 'N' ELSE 'S' END            AS respondeu_questionario,
       'S'                                                         AS incluido,
       CAST(NULL AS VARCHAR2(120))                                 AS motivo_exclusao
  FROM infosaude.baa b
  LEFT JOIN pesq p
    ON p.cd_hospital = b.cd_hospital AND p.dt_ano_baa = b.dt_ano_baa AND p.nr_baa = b.nr_baa
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
   AND (b.cd_setor IS NULL OR b.cd_setor NOT IN (38,46,50,60,71,39,48,51,67))
$analitico$
 WHERE aba = 1 AND numero = '8'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
WITH pesq AS (
  SELECT m.cd_hospital, m.ano_movimento, m.id_movimento, m.nr_baa, m.dt_ano_baa, m.dt_inclusao
    FROM infosaude.edoc_movimento m
    JOIN infosaude.edoc_modelo md ON md.cd_modelo = m.cd_modelo
   WHERE m.cd_hospital = :hospital AND m.in_status = 'D' AND m.in_ativo = 'S'
     AND UPPER(md.ds_modelo) LIKE '%SATISFA%' AND m.nr_baa IS NOT NULL
     AND m.dt_inclusao >= :ini AND m.dt_inclusao < :fim
)
SELECT p.nr_baa || '/' || p.dt_ano_baa                                          AS baa,
       p.dt_inclusao                                                            AS respondido_em,
       mi.ds_resposta                                                           AS resposta,
       CASE WHEN REPLACE(mi.ds_resposta,' ','') IN ('9-10','7-8')
            THEN 'S' ELSE 'N' END                                               AS satisfeito,
       CASE WHEN mi.ds_resposta IS NOT NULL THEN 'S' ELSE 'N' END               AS incluido,
       CASE WHEN mi.ds_resposta IS NULL
                 THEN 'questionario entregue sem resposta na pergunta de satisfacao'
       END                                                                      AS motivo_exclusao
  FROM pesq p
  JOIN infosaude.edoc_movimento_item mi
    ON mi.cd_hospital = p.cd_hospital AND mi.ano_movimento = p.ano_movimento
   AND mi.id_movimento = p.id_movimento
 WHERE mi.cd_item = 12648
$analitico$
 WHERE aba = 1 AND numero = '9'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia                                       AS fia,
       fl.cd_unidade                                                         AS unidade,
       fl.cd_leito                                                           AS leito,
       fl.dt_transferencia                                                   AS entrada_no_leito,
       fl.dt_saida_leito                                                     AS saida_do_leito,
       f.dt_alta                                                             AS alta,
       ROUND(LEAST(COALESCE(fl.dt_saida_leito, f.dt_alta, :fim), :fim)
             - GREATEST(fl.dt_transferencia, :ini), 3)                       AS dias_no_periodo,
       CASE WHEN fl.dt_saida_leito IS NOT NULL OR f.dt_alta IS NOT NULL
                 OR fl.dt_transferencia >= :ini
            THEN 'S' ELSE 'N' END                                            AS incluido,
       CASE WHEN fl.dt_saida_leito IS NULL AND f.dt_alta IS NULL
                 AND fl.dt_transferencia < :ini
                 THEN 'leito em aberto desde antes do periodo, sem alta nem saida  -  ocupacao fantasma'
       END                                                                   AS motivo_exclusao
  FROM infosaude.fia_leito fl
  JOIN infosaude.fia f
    ON f.cd_hospital = fl.cd_hospital AND f.dt_ano_fia = fl.dt_ano_fia AND f.nr_fia = fl.nr_fia
 WHERE fl.cd_hospital = :hospital
   AND fl.cd_unidade NOT IN (10,13,30,31,32,33,34,15,16,17,26)
   AND fl.dt_transferencia < :fim
   AND LEAST(COALESCE(fl.dt_saida_leito, f.dt_alta, :fim), :fim) > :ini
$analitico$
 WHERE aba = 1 AND numero = '11'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia                            AS fia,
       p.nm_paciente                                              AS paciente,
       f.dt_baixa                                                 AS internacao,
       f.dt_alta                                                  AS alta,
       FLOOR(MONTHS_BETWEEN(f.dt_baixa, p.dt_nascimento) / 12)    AS idade,
       ROUND(f.dt_alta - f.dt_baixa, 2)                           AS dias_de_permanencia,
       ult.cd_unidade                                             AS unidade_de_saida,
       CASE WHEN ult.cd_unidade NOT IN (10,13,30,31,32,33,34,15,16,17,26)
             AND FLOOR(MONTHS_BETWEEN(f.dt_baixa, p.dt_nascimento) / 12) <= 59
            THEN 'S' ELSE 'N' END                                 AS incluido,
       CASE WHEN ult.cd_unidade IN (10,13,30,31,32,33,34,15,16,17,26)
                 THEN 'saida por unidade pediatrica/materno-infantil  -  fora do bloco adulto'
            WHEN f.dt_baixa IS NULL OR p.dt_nascimento IS NULL
                 THEN 'sem data de internacao ou de nascimento  -  idade nao calculavel'
            WHEN NOT (FLOOR(MONTHS_BETWEEN(f.dt_baixa, p.dt_nascimento) / 12) <= 59)
                 THEN '60 anos ou mais  -  conta no indicador da faixa idosa'
       END                                                        AS motivo_exclusao
  FROM infosaude.fia f
  JOIN infosaude.paciente p ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
  LEFT JOIN (SELECT fl.cd_hospital, fl.dt_ano_fia, fl.nr_fia,
                    MAX(fl.cd_unidade) KEEP (DENSE_RANK LAST ORDER BY fl.dt_transferencia) AS cd_unidade
               FROM infosaude.fia_leito fl
              GROUP BY fl.cd_hospital, fl.dt_ano_fia, fl.nr_fia) ult
    ON ult.cd_hospital = f.cd_hospital AND ult.dt_ano_fia = f.dt_ano_fia AND ult.nr_fia = f.nr_fia
 WHERE f.cd_hospital = :hospital AND f.dt_alta >= :ini AND f.dt_alta < :fim
$analitico$
 WHERE aba = 1 AND numero = '12'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia                            AS fia,
       p.nm_paciente                                              AS paciente,
       f.dt_baixa                                                 AS internacao,
       f.dt_alta                                                  AS alta,
       FLOOR(MONTHS_BETWEEN(f.dt_baixa, p.dt_nascimento) / 12)    AS idade,
       ROUND(f.dt_alta - f.dt_baixa, 2)                           AS dias_de_permanencia,
       ult.cd_unidade                                             AS unidade_de_saida,
       CASE WHEN ult.cd_unidade NOT IN (10,13,30,31,32,33,34,15,16,17,26)
             AND FLOOR(MONTHS_BETWEEN(f.dt_baixa, p.dt_nascimento) / 12) >= 60
            THEN 'S' ELSE 'N' END                                 AS incluido,
       CASE WHEN ult.cd_unidade IN (10,13,30,31,32,33,34,15,16,17,26)
                 THEN 'saida por unidade pediatrica/materno-infantil  -  fora do bloco adulto'
            WHEN f.dt_baixa IS NULL OR p.dt_nascimento IS NULL
                 THEN 'sem data de internacao ou de nascimento  -  idade nao calculavel'
            WHEN NOT (FLOOR(MONTHS_BETWEEN(f.dt_baixa, p.dt_nascimento) / 12) >= 60)
                 THEN 'menos de 60 anos  -  conta no indicador da outra faixa'
       END                                                        AS motivo_exclusao
  FROM infosaude.fia f
  JOIN infosaude.paciente p ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
  LEFT JOIN (SELECT fl.cd_hospital, fl.dt_ano_fia, fl.nr_fia,
                    MAX(fl.cd_unidade) KEEP (DENSE_RANK LAST ORDER BY fl.dt_transferencia) AS cd_unidade
               FROM infosaude.fia_leito fl
              GROUP BY fl.cd_hospital, fl.dt_ano_fia, fl.nr_fia) ult
    ON ult.cd_hospital = f.cd_hospital AND ult.dt_ano_fia = f.dt_ano_fia AND ult.nr_fia = f.nr_fia
 WHERE f.cd_hospital = :hospital AND f.dt_alta >= :ini AND f.dt_alta < :fim
$analitico$
 WHERE aba = 1 AND numero = '13'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia                         AS fia,
       f.dt_baixa                                              AS internacao,
       pri.cd_unidade                                          AS unidade_de_entrada,
       (SELECT MIN(f.dt_baixa - a.dt_alta)
          FROM infosaude.fia a
         WHERE a.cd_hospital = f.cd_hospital
           AND NVL(a.cd_paciente_unificado, a.cd_paciente)
               = NVL(f.cd_paciente_unificado, f.cd_paciente)
           AND NOT (a.dt_ano_fia = f.dt_ano_fia AND a.nr_fia = f.nr_fia)
           AND a.dt_alta IS NOT NULL
           AND a.dt_alta <= f.dt_baixa)                         AS dias_desde_a_alta_anterior,
       CASE WHEN (SELECT MIN(f.dt_baixa - a.dt_alta)
                    FROM infosaude.fia a
                   WHERE a.cd_hospital = f.cd_hospital
                     AND NVL(a.cd_paciente_unificado, a.cd_paciente)
                         = NVL(f.cd_paciente_unificado, f.cd_paciente)
                     AND NOT (a.dt_ano_fia = f.dt_ano_fia AND a.nr_fia = f.nr_fia)
                     AND a.dt_alta IS NOT NULL
                     AND a.dt_alta <= f.dt_baixa) <= 30
            THEN 'S' ELSE 'N' END                               AS reinternacao_ate_30d,
       CASE WHEN pri.cd_unidade NOT IN (10,13,30,31,32,33,34,15,16,17,26)
            THEN 'S' ELSE 'N' END                               AS incluido,
       CASE WHEN pri.cd_unidade IN (10,13,30,31,32,33,34,15,16,17,26)
                 THEN 'entrada por unidade pediatrica/materno-infantil  -  fora do bloco adulto'
            WHEN pri.cd_unidade IS NULL
                 THEN 'internacao sem registro de leito  -  bloco nao identificavel'
       END                                                      AS motivo_exclusao
  FROM infosaude.fia f
  LEFT JOIN (SELECT fl.cd_hospital, fl.dt_ano_fia, fl.nr_fia,
                    MIN(fl.cd_unidade) KEEP (DENSE_RANK FIRST ORDER BY fl.dt_transferencia) AS cd_unidade
               FROM infosaude.fia_leito fl
              GROUP BY fl.cd_hospital, fl.dt_ano_fia, fl.nr_fia) pri
    ON pri.cd_hospital = f.cd_hospital AND pri.dt_ano_fia = f.dt_ano_fia AND pri.nr_fia = f.nr_fia
 WHERE f.cd_hospital = :hospital AND f.dt_baixa >= :ini AND f.dt_baixa < :fim
$analitico$
 WHERE aba = 1 AND numero = '14'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia                              AS fia,
       f.dt_baixa                                                   AS internacao,
       f.dt_alta                                                    AS alta,
       ROUND(f.dt_alta - f.dt_baixa, 2)                             AS dias_de_permanencia,
       f.cd_mot_cobranca_sus                                        AS motivo_de_saida,
       ult.cd_unidade                                               AS unidade_de_saida,
       CASE WHEN f.cd_mot_cobranca_sus IN (41,42,43) AND f.dt_alta - f.dt_baixa > 1
            THEN 'S' ELSE 'N' END                                   AS obito_institucional,
       CASE WHEN ult.cd_unidade NOT IN (10,13,30,31,32,33,34,15,16,17,26)
            THEN 'S' ELSE 'N' END                                   AS incluido,
       CASE WHEN ult.cd_unidade IN (10,13,30,31,32,33,34,15,16,17,26)
                 THEN 'saida por unidade pediatrica/materno-infantil  -  fora do bloco adulto'
            WHEN ult.cd_unidade IS NULL
                 THEN 'internacao sem registro de leito  -  bloco nao identificavel'
       END                                                          AS motivo_exclusao
  FROM infosaude.fia f
  LEFT JOIN (SELECT fl.cd_hospital, fl.dt_ano_fia, fl.nr_fia,
                    MAX(fl.cd_unidade) KEEP (DENSE_RANK LAST ORDER BY fl.dt_transferencia) AS cd_unidade
               FROM infosaude.fia_leito fl
              GROUP BY fl.cd_hospital, fl.dt_ano_fia, fl.nr_fia) ult
    ON ult.cd_hospital = f.cd_hospital AND ult.dt_ano_fia = f.dt_ano_fia AND ult.nr_fia = f.nr_fia
 WHERE f.cd_hospital = :hospital AND f.dt_alta >= :ini AND f.dt_alta < :fim
$analitico$
 WHERE aba = 1 AND numero = '17'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            // ---- Aba Pediátrico ----

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                             AS baa,
       a.dt_atendimento                                            AS cadastro,
       b.dt_classifica_atual                                       AS classificacao_risco,
       ROUND((b.dt_classifica_atual - a.dt_atendimento) * 1440, 2) AS minutos,
       CASE WHEN a.dt_atendimento IS NOT NULL
             AND b.dt_classifica_atual IS NOT NULL
             AND b.dt_classifica_atual >= a.dt_atendimento
            THEN 'S' ELSE 'N' END                                  AS incluido,
       CASE WHEN a.dt_atendimento IS NULL
                 THEN 'sem registro de cadastro/acolhimento'
            WHEN b.dt_classifica_atual IS NULL
                 THEN 'sem registro de classificacao de risco'
            WHEN b.dt_classifica_atual < a.dt_atendimento
                 THEN 'classificacao anterior ao cadastro (carimbo inconsistente)'
       END                                                         AS motivo_exclusao
  FROM infosaude.baa b
  JOIN infosaude.acolhimento a ON a.cd_acolhimento = b.cd_acolhimento
 WHERE b.cd_hospital = :hospital AND b.dt_chegada >= :ini AND b.dt_chegada < :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND NVL(b.cd_classificacao_risco,-1) NOT IN
       (SELECT cr.cd_classificacao_risco FROM infosaude.classificacao_risco cr
         WHERE UPPER(cr.ds_classificacao_risco)='VERMELHO')
$analitico$
 WHERE aba = 2 AND numero = '2'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                      AS baa,
       cr.ds_classificacao_risco                            AS cor,
       b.dt_classifica_atual                                AS classificacao_risco,
       med.t_med                                            AS primeiro_registro_medico,
       ROUND((med.t_med - b.dt_classifica_atual) * 1440, 2) AS minutos,
       CASE WHEN b.dt_classifica_atual IS NOT NULL
             AND med.t_med IS NOT NULL
             AND med.t_med >= b.dt_classifica_atual
            THEN 'S' ELSE 'N' END                           AS incluido,
       CASE WHEN b.dt_classifica_atual IS NULL
                 THEN 'sem registro de classificacao de risco'
            WHEN med.t_med IS NULL
                 THEN 'sem registro de atendimento medico no prontuario eletronico'
            WHEN med.t_med < b.dt_classifica_atual
                 THEN 'atendimento medico anterior a classificacao (carimbo inconsistente)'
       END                                                  AS motivo_exclusao
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr ON cr.cd_classificacao_risco = b.cd_classificacao_risco
  LEFT JOIN (SELECT m.nr_baa, m.dt_ano_baa, MIN(m.dt_inclusao) AS t_med
               FROM infosaude.edoc_movimento m
              WHERE m.cd_modelo IN (10036,10014,10232) AND m.baa_cd_hospital = :hospital
                AND m.dt_inclusao >= :ini
              GROUP BY m.nr_baa, m.dt_ano_baa) med
    ON med.nr_baa = b.nr_baa AND med.dt_ano_baa = b.dt_ano_baa
 WHERE b.cd_hospital = :hospital AND b.dt_chegada >= :ini AND b.dt_chegada < :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND UPPER(cr.ds_classificacao_risco) = 'AMARELO'
$analitico$
 WHERE aba = 2 AND numero = '3.3'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                      AS baa,
       cr.ds_classificacao_risco                            AS cor,
       b.dt_classifica_atual                                AS classificacao_risco,
       med.t_med                                            AS primeiro_registro_medico,
       ROUND((med.t_med - b.dt_classifica_atual) * 1440, 2) AS minutos,
       CASE WHEN b.dt_classifica_atual IS NOT NULL
             AND med.t_med IS NOT NULL
             AND med.t_med >= b.dt_classifica_atual
            THEN 'S' ELSE 'N' END                           AS incluido,
       CASE WHEN b.dt_classifica_atual IS NULL
                 THEN 'sem registro de classificacao de risco'
            WHEN med.t_med IS NULL
                 THEN 'sem registro de atendimento medico no prontuario eletronico'
            WHEN med.t_med < b.dt_classifica_atual
                 THEN 'atendimento medico anterior a classificacao (carimbo inconsistente)'
       END                                                  AS motivo_exclusao
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr ON cr.cd_classificacao_risco = b.cd_classificacao_risco
  LEFT JOIN (SELECT m.nr_baa, m.dt_ano_baa, MIN(m.dt_inclusao) AS t_med
               FROM infosaude.edoc_movimento m
              WHERE m.cd_modelo IN (10036,10014,10232) AND m.baa_cd_hospital = :hospital
                AND m.dt_inclusao >= :ini
              GROUP BY m.nr_baa, m.dt_ano_baa) med
    ON med.nr_baa = b.nr_baa AND med.dt_ano_baa = b.dt_ano_baa
 WHERE b.cd_hospital = :hospital AND b.dt_chegada >= :ini AND b.dt_chegada < :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND UPPER(cr.ds_classificacao_risco) = 'VERDE'
$analitico$
 WHERE aba = 2 AND numero = '3.4'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                      AS baa,
       cr.ds_classificacao_risco                            AS cor,
       b.dt_classifica_atual                                AS classificacao_risco,
       med.t_med                                            AS primeiro_registro_medico,
       ROUND((med.t_med - b.dt_classifica_atual) * 1440, 2) AS minutos,
       CASE WHEN b.dt_classifica_atual IS NOT NULL
             AND med.t_med IS NOT NULL
             AND med.t_med >= b.dt_classifica_atual
            THEN 'S' ELSE 'N' END                           AS incluido,
       CASE WHEN b.dt_classifica_atual IS NULL
                 THEN 'sem registro de classificacao de risco'
            WHEN med.t_med IS NULL
                 THEN 'sem registro de atendimento medico no prontuario eletronico'
            WHEN med.t_med < b.dt_classifica_atual
                 THEN 'atendimento medico anterior a classificacao (carimbo inconsistente)'
       END                                                  AS motivo_exclusao
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr ON cr.cd_classificacao_risco = b.cd_classificacao_risco
  LEFT JOIN (SELECT m.nr_baa, m.dt_ano_baa, MIN(m.dt_inclusao) AS t_med
               FROM infosaude.edoc_movimento m
              WHERE m.cd_modelo IN (10036,10014,10232) AND m.baa_cd_hospital = :hospital
                AND m.dt_inclusao >= :ini
              GROUP BY m.nr_baa, m.dt_ano_baa) med
    ON med.nr_baa = b.nr_baa AND med.dt_ano_baa = b.dt_ano_baa
 WHERE b.cd_hospital = :hospital AND b.dt_chegada >= :ini AND b.dt_chegada < :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND UPPER(cr.ds_classificacao_risco) = 'AZUL'
$analitico$
 WHERE aba = 2 AND numero = '3.5'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa            AS baa,
       b.dt_chegada                               AS chegada,
       b.dt_atendimento                           AS atendimento_medico,
       b.dt_saida                                 AS saida,
       ROUND((b.dt_saida - b.dt_chegada) * 24, 2) AS horas,
       CASE WHEN b.dt_saida IS NOT NULL AND b.dt_saida > b.dt_chegada
            THEN 'S' ELSE 'N' END                 AS incluido,
       CASE WHEN b.dt_saida IS NULL
                 THEN 'sem registro de saida (boletim em aberto)'
            WHEN b.dt_saida <= b.dt_chegada
                 THEN 'saida igual ou anterior a chegada (carimbo inconsistente)'
       END                                        AS motivo_exclusao
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
   AND b.cd_setor IN (38,46,50,60,71)
$analitico$
 WHERE aba = 2 AND numero = '7'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT fl.nr_fia || '/' || fl.dt_ano_fia                   AS fia,
       fl.cd_unidade                                       AS unidade,
       fl.cd_leito                                         AS leito,
       fl.dt_transferencia                                 AS entrada_no_leito,
       fl.dt_saida_leito                                   AS saida_do_leito,
       ROUND(LEAST(NVL(fl.dt_saida_leito, :fim), :fim)
             - GREATEST(fl.dt_transferencia, :ini), 3)     AS dias_no_periodo,
       'S'                                                 AS incluido,
       CAST(NULL AS VARCHAR2(120))                         AS motivo_exclusao
  FROM infosaude.fia_leito fl
 WHERE fl.cd_hospital = :hospital
   AND fl.cd_unidade IN (10,13,30,31,32,33,34)
   AND fl.dt_transferencia < :fim
   AND NVL(fl.dt_saida_leito, :fim) > :ini
$analitico$
 WHERE aba = 2 AND numero = '8'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia   AS fia,
       f.dt_baixa                        AS internacao,
       f.dt_alta                         AS alta,
       ROUND(f.dt_alta - f.dt_baixa, 2)  AS dias_de_permanencia,
       CASE WHEN f.dt_alta >= f.dt_baixa THEN 'S' ELSE 'N' END AS incluido,
       CASE WHEN f.dt_baixa IS NULL
                 THEN 'sem data de internacao registrada'
            WHEN f.dt_alta < f.dt_baixa
                 THEN 'alta anterior a internacao (carimbo inconsistente)'
       END                               AS motivo_exclusao
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital AND f.dt_alta >= :ini AND f.dt_alta < :fim
   AND EXISTS (SELECT 1 FROM infosaude.fia_leito fl
                WHERE fl.cd_hospital = f.cd_hospital AND fl.dt_ano_fia = f.dt_ano_fia
                  AND fl.nr_fia = f.nr_fia AND fl.cd_unidade IN (10,13,30,31,32,33,34))
$analitico$
 WHERE aba = 2 AND numero = '9'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia   AS fia,
       f.dt_baixa                        AS internacao,
       (SELECT MIN(f.dt_baixa - p.dt_alta)
          FROM infosaude.fia p
         WHERE p.cd_hospital = f.cd_hospital
           AND NVL(p.cd_paciente_unificado, p.cd_paciente)
               = NVL(f.cd_paciente_unificado, f.cd_paciente)
           AND (p.dt_ano_fia <> f.dt_ano_fia OR p.nr_fia <> f.nr_fia)
           AND p.dt_alta IS NOT NULL AND p.dt_alta <= f.dt_baixa)   AS dias_desde_a_alta_anterior,
       CASE WHEN (SELECT COUNT(*)
                    FROM infosaude.fia p
                   WHERE p.cd_hospital = f.cd_hospital
                     AND NVL(p.cd_paciente_unificado, p.cd_paciente)
                         = NVL(f.cd_paciente_unificado, f.cd_paciente)
                     AND (p.dt_ano_fia <> f.dt_ano_fia OR p.nr_fia <> f.nr_fia)
                     AND p.dt_alta IS NOT NULL AND p.dt_alta <= f.dt_baixa
                     AND p.dt_alta >= f.dt_baixa - 30) > 0
            THEN 'S' ELSE 'N' END                                   AS reinternacao_ate_30d,
       'S'                                                          AS incluido,
       CAST(NULL AS VARCHAR2(120))                                  AS motivo_exclusao
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital AND f.dt_baixa >= :ini AND f.dt_baixa < :fim
   AND EXISTS (SELECT 1 FROM infosaude.fia_leito fl
                WHERE fl.cd_hospital = f.cd_hospital AND fl.dt_ano_fia = f.dt_ano_fia
                  AND fl.nr_fia = f.nr_fia AND fl.cd_unidade IN (10,13,30,31,32,33,34))
$analitico$
 WHERE aba = 2 AND numero = '10'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia   AS fia,
       f.dt_baixa                        AS internacao,
       f.dt_alta                         AS alta,
       ROUND(f.dt_alta - f.dt_baixa, 2)  AS dias_de_permanencia,
       f.cd_mot_cobranca_sus             AS motivo_de_saida,
       CASE WHEN f.cd_mot_cobranca_sus IN (41,42,43) AND f.dt_alta - f.dt_baixa > 1
            THEN 'S' ELSE 'N' END        AS obito_institucional,
       'S'                               AS incluido,
       CAST(NULL AS VARCHAR2(120))       AS motivo_exclusao
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital AND f.dt_alta >= :ini AND f.dt_alta < :fim
   AND EXISTS (SELECT 1 FROM infosaude.fia_leito fl
                WHERE fl.cd_hospital = f.cd_hospital AND fl.dt_ano_fia = f.dt_ano_fia
                  AND fl.nr_fia = f.nr_fia AND fl.cd_unidade IN (10,13,30,31,32,33,34))
$analitico$
 WHERE aba = 2 AND numero = '12'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            // ---- Aba Materno-Infantil ----

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                       AS baa,
       c.cor                                                 AS cor,
       a.dt_atendimento                                      AS cadastro,
       c.t_class                                             AS classificacao_risco,
       ROUND((c.t_class - a.dt_atendimento) * 1440, 2)       AS minutos,
       CASE WHEN a.dt_atendimento IS NOT NULL
             AND c.t_class >= a.dt_atendimento
            THEN 'S' ELSE 'N' END                            AS incluido,
       CASE WHEN a.dt_atendimento IS NULL
                 THEN 'sem registro de cadastro/acolhimento'
            WHEN c.t_class < a.dt_atendimento
                 THEN 'classificacao anterior ao cadastro (carimbo inconsistente)'
       END                                                   AS motivo_exclusao
  FROM 
  (SELECT nr_baa, dt_ano_baa, t_class, cor FROM (
      SELECT mv.nr_baa, mv.dt_ano_baa, mv.dt_inclusao AS t_class, mi.ds_resposta AS cor,
             ROW_NUMBER() OVER (PARTITION BY mv.nr_baa, mv.dt_ano_baa ORDER BY mv.dt_inclusao) rn
        FROM infosaude.edoc_movimento mv
        JOIN infosaude.edoc_movimento_item mi
          ON mi.cd_hospital=mv.cd_hospital AND mi.ano_movimento=mv.ano_movimento
         AND mi.id_movimento=mv.id_movimento AND mi.cd_modelo=mv.cd_modelo
         AND mi.cd_documento=mv.cd_documento
        JOIN infosaude.edoc_item it ON it.cd_item=mi.cd_item
         AND it.ds_item='Classificação de Risco'
       WHERE mv.cd_modelo=10043 AND mv.cd_hospital=:hospital
         AND mv.dt_inclusao >= :ini AND mv.dt_inclusao < :fim
    ) WHERE rn=1)
 c
  JOIN infosaude.baa b
    ON b.nr_baa=c.nr_baa AND b.dt_ano_baa=c.dt_ano_baa AND b.cd_hospital=:hospital
  JOIN infosaude.acolhimento a ON a.cd_acolhimento=b.cd_acolhimento
 WHERE b.cd_setor IN (39,48,51,67) AND c.cor NOT LIKE 'Vermelho%'
$analitico$
 WHERE aba = 3 AND numero = '2'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT c.nr_baa || '/' || c.dt_ano_baa                 AS baa,
       c.cor                                           AS cor,
       c.t_class                                       AS classificacao_risco,
       med.t_med                                       AS primeiro_registro_medico,
       ROUND((med.t_med - c.t_class) * 1440, 2)        AS minutos,
       CASE WHEN med.t_med IS NOT NULL AND med.t_med >= c.t_class
            THEN 'S' ELSE 'N' END                      AS incluido,
       CASE WHEN med.t_med IS NULL
                 THEN 'sem registro de atendimento medico no prontuario eletronico'
            WHEN med.t_med < c.t_class
                 THEN 'atendimento medico anterior a classificacao (carimbo inconsistente)'
       END                                             AS motivo_exclusao
  FROM 
  (SELECT nr_baa, dt_ano_baa, t_class, cor FROM (
      SELECT mv.nr_baa, mv.dt_ano_baa, mv.dt_inclusao AS t_class, mi.ds_resposta AS cor,
             ROW_NUMBER() OVER (PARTITION BY mv.nr_baa, mv.dt_ano_baa ORDER BY mv.dt_inclusao) rn
        FROM infosaude.edoc_movimento mv
        JOIN infosaude.edoc_movimento_item mi
          ON mi.cd_hospital=mv.cd_hospital AND mi.ano_movimento=mv.ano_movimento
         AND mi.id_movimento=mv.id_movimento AND mi.cd_modelo=mv.cd_modelo
         AND mi.cd_documento=mv.cd_documento
        JOIN infosaude.edoc_item it ON it.cd_item=mi.cd_item
         AND it.ds_item='Classificação de Risco'
       WHERE mv.cd_modelo=10043 AND mv.cd_hospital=:hospital
         AND mv.dt_inclusao >= :ini AND mv.dt_inclusao < :fim
    ) WHERE rn=1)
 c
  LEFT JOIN (SELECT nr_baa, dt_ano_baa, MIN(dt_inclusao) AS t_med
               FROM infosaude.edoc_movimento
              WHERE cd_modelo IN (10036,10014,10232) AND baa_cd_hospital=:hospital
                AND dt_inclusao >= :ini
              GROUP BY nr_baa, dt_ano_baa) med
    ON med.nr_baa=c.nr_baa AND med.dt_ano_baa=c.dt_ano_baa
 WHERE c.cor LIKE 'Laranja%'
$analitico$
 WHERE aba = 3 AND numero = '3.2'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT c.nr_baa || '/' || c.dt_ano_baa                 AS baa,
       c.cor                                           AS cor,
       c.t_class                                       AS classificacao_risco,
       med.t_med                                       AS primeiro_registro_medico,
       ROUND((med.t_med - c.t_class) * 1440, 2)        AS minutos,
       CASE WHEN med.t_med IS NOT NULL AND med.t_med >= c.t_class
            THEN 'S' ELSE 'N' END                      AS incluido,
       CASE WHEN med.t_med IS NULL
                 THEN 'sem registro de atendimento medico no prontuario eletronico'
            WHEN med.t_med < c.t_class
                 THEN 'atendimento medico anterior a classificacao (carimbo inconsistente)'
       END                                             AS motivo_exclusao
  FROM 
  (SELECT nr_baa, dt_ano_baa, t_class, cor FROM (
      SELECT mv.nr_baa, mv.dt_ano_baa, mv.dt_inclusao AS t_class, mi.ds_resposta AS cor,
             ROW_NUMBER() OVER (PARTITION BY mv.nr_baa, mv.dt_ano_baa ORDER BY mv.dt_inclusao) rn
        FROM infosaude.edoc_movimento mv
        JOIN infosaude.edoc_movimento_item mi
          ON mi.cd_hospital=mv.cd_hospital AND mi.ano_movimento=mv.ano_movimento
         AND mi.id_movimento=mv.id_movimento AND mi.cd_modelo=mv.cd_modelo
         AND mi.cd_documento=mv.cd_documento
        JOIN infosaude.edoc_item it ON it.cd_item=mi.cd_item
         AND it.ds_item='Classificação de Risco'
       WHERE mv.cd_modelo=10043 AND mv.cd_hospital=:hospital
         AND mv.dt_inclusao >= :ini AND mv.dt_inclusao < :fim
    ) WHERE rn=1)
 c
  LEFT JOIN (SELECT nr_baa, dt_ano_baa, MIN(dt_inclusao) AS t_med
               FROM infosaude.edoc_movimento
              WHERE cd_modelo IN (10036,10014,10232) AND baa_cd_hospital=:hospital
                AND dt_inclusao >= :ini
              GROUP BY nr_baa, dt_ano_baa) med
    ON med.nr_baa=c.nr_baa AND med.dt_ano_baa=c.dt_ano_baa
 WHERE c.cor LIKE 'Amarelo%'
$analitico$
 WHERE aba = 3 AND numero = '3.3'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT c.nr_baa || '/' || c.dt_ano_baa                 AS baa,
       c.cor                                           AS cor,
       c.t_class                                       AS classificacao_risco,
       med.t_med                                       AS primeiro_registro_medico,
       ROUND((med.t_med - c.t_class) * 1440, 2)        AS minutos,
       CASE WHEN med.t_med IS NOT NULL AND med.t_med >= c.t_class
            THEN 'S' ELSE 'N' END                      AS incluido,
       CASE WHEN med.t_med IS NULL
                 THEN 'sem registro de atendimento medico no prontuario eletronico'
            WHEN med.t_med < c.t_class
                 THEN 'atendimento medico anterior a classificacao (carimbo inconsistente)'
       END                                             AS motivo_exclusao
  FROM 
  (SELECT nr_baa, dt_ano_baa, t_class, cor FROM (
      SELECT mv.nr_baa, mv.dt_ano_baa, mv.dt_inclusao AS t_class, mi.ds_resposta AS cor,
             ROW_NUMBER() OVER (PARTITION BY mv.nr_baa, mv.dt_ano_baa ORDER BY mv.dt_inclusao) rn
        FROM infosaude.edoc_movimento mv
        JOIN infosaude.edoc_movimento_item mi
          ON mi.cd_hospital=mv.cd_hospital AND mi.ano_movimento=mv.ano_movimento
         AND mi.id_movimento=mv.id_movimento AND mi.cd_modelo=mv.cd_modelo
         AND mi.cd_documento=mv.cd_documento
        JOIN infosaude.edoc_item it ON it.cd_item=mi.cd_item
         AND it.ds_item='Classificação de Risco'
       WHERE mv.cd_modelo=10043 AND mv.cd_hospital=:hospital
         AND mv.dt_inclusao >= :ini AND mv.dt_inclusao < :fim
    ) WHERE rn=1)
 c
  LEFT JOIN (SELECT nr_baa, dt_ano_baa, MIN(dt_inclusao) AS t_med
               FROM infosaude.edoc_movimento
              WHERE cd_modelo IN (10036,10014,10232) AND baa_cd_hospital=:hospital
                AND dt_inclusao >= :ini
              GROUP BY nr_baa, dt_ano_baa) med
    ON med.nr_baa=c.nr_baa AND med.dt_ano_baa=c.dt_ano_baa
 WHERE c.cor LIKE 'Verde%'
$analitico$
 WHERE aba = 3 AND numero = '3.4'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT c.nr_baa || '/' || c.dt_ano_baa                 AS baa,
       c.cor                                           AS cor,
       c.t_class                                       AS classificacao_risco,
       med.t_med                                       AS primeiro_registro_medico,
       ROUND((med.t_med - c.t_class) * 1440, 2)        AS minutos,
       CASE WHEN med.t_med IS NOT NULL AND med.t_med >= c.t_class
            THEN 'S' ELSE 'N' END                      AS incluido,
       CASE WHEN med.t_med IS NULL
                 THEN 'sem registro de atendimento medico no prontuario eletronico'
            WHEN med.t_med < c.t_class
                 THEN 'atendimento medico anterior a classificacao (carimbo inconsistente)'
       END                                             AS motivo_exclusao
  FROM 
  (SELECT nr_baa, dt_ano_baa, t_class, cor FROM (
      SELECT mv.nr_baa, mv.dt_ano_baa, mv.dt_inclusao AS t_class, mi.ds_resposta AS cor,
             ROW_NUMBER() OVER (PARTITION BY mv.nr_baa, mv.dt_ano_baa ORDER BY mv.dt_inclusao) rn
        FROM infosaude.edoc_movimento mv
        JOIN infosaude.edoc_movimento_item mi
          ON mi.cd_hospital=mv.cd_hospital AND mi.ano_movimento=mv.ano_movimento
         AND mi.id_movimento=mv.id_movimento AND mi.cd_modelo=mv.cd_modelo
         AND mi.cd_documento=mv.cd_documento
        JOIN infosaude.edoc_item it ON it.cd_item=mi.cd_item
         AND it.ds_item='Classificação de Risco'
       WHERE mv.cd_modelo=10043 AND mv.cd_hospital=:hospital
         AND mv.dt_inclusao >= :ini AND mv.dt_inclusao < :fim
    ) WHERE rn=1)
 c
  LEFT JOIN (SELECT nr_baa, dt_ano_baa, MIN(dt_inclusao) AS t_med
               FROM infosaude.edoc_movimento
              WHERE cd_modelo IN (10036,10014,10232) AND baa_cd_hospital=:hospital
                AND dt_inclusao >= :ini
              GROUP BY nr_baa, dt_ano_baa) med
    ON med.nr_baa=c.nr_baa AND med.dt_ano_baa=c.dt_ano_baa
 WHERE c.cor LIKE 'Azul%'
$analitico$
 WHERE aba = 3 AND numero = '3.5'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT fl.nr_fia || '/' || fl.dt_ano_fia                   AS fia,
       fl.cd_unidade                                       AS unidade,
       fl.cd_leito                                         AS leito,
       fl.dt_transferencia                                 AS entrada_no_leito,
       fl.dt_saida_leito                                   AS saida_do_leito,
       ROUND(LEAST(NVL(fl.dt_saida_leito, :fim), :fim)
             - GREATEST(fl.dt_transferencia, :ini), 3)     AS dias_no_periodo,
       'S'                                                 AS incluido,
       CAST(NULL AS VARCHAR2(120))                         AS motivo_exclusao
  FROM infosaude.fia_leito fl
 WHERE fl.cd_hospital = :hospital
   AND fl.cd_unidade IN (15,16,17,26)
   AND fl.dt_transferencia < :fim
   AND NVL(fl.dt_saida_leito, :fim) > :ini
$analitico$
 WHERE aba = 3 AND numero = '7'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia   AS fia,
       f.dt_baixa                        AS internacao,
       f.dt_alta                         AS alta,
       ROUND(f.dt_alta - f.dt_baixa, 2)  AS dias_de_permanencia,
       CASE WHEN f.dt_alta >= f.dt_baixa THEN 'S' ELSE 'N' END AS incluido,
       CASE WHEN f.dt_baixa IS NULL
                 THEN 'sem data de internacao registrada'
            WHEN f.dt_alta < f.dt_baixa
                 THEN 'alta anterior a internacao (carimbo inconsistente)'
       END                               AS motivo_exclusao
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital AND f.dt_alta >= :ini AND f.dt_alta < :fim
   AND EXISTS (SELECT 1 FROM infosaude.fia_leito fl
                WHERE fl.cd_hospital = f.cd_hospital AND fl.dt_ano_fia = f.dt_ano_fia
                  AND fl.nr_fia = f.nr_fia AND fl.cd_unidade IN (15,16,17,26))
$analitico$
 WHERE aba = 3 AND numero = '8'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT n.dt_ano_fia || '/' || n.nr_fia                                    AS fia,
       MIN(n.dt_parto)                                                    AS data_do_parto,
       COUNT(*)                                                           AS nascidos,
       MAX(CASE WHEN n.id_tp_parto = 'C' THEN 'Cesareo' ELSE 'Normal' END) AS tipo_de_parto,
       MAX(CASE WHEN n.id_tp_parto = 'C' THEN 'S' ELSE 'N' END)           AS parto_cesareo,
       'S'                                                                AS incluido,
       CAST(NULL AS VARCHAR2(120))                                        AS motivo_exclusao
  FROM infosaude.nascimento n
 WHERE n.fia_cd_hospital = :hospital
   AND n.dt_parto >= :ini AND n.dt_parto < :fim
 GROUP BY n.dt_ano_fia || '/' || n.nr_fia
$analitico$
 WHERE aba = 3 AND numero = '9'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT n.dt_ano_fia || '/' || n.nr_fia                       AS fia,
       n.dt_parto                                            AS data_do_parto,
       n.apgar_5_min                                         AS apgar_5o_minuto,
       CASE WHEN REGEXP_LIKE(TRIM(n.apgar_5_min), '^[0-9]+$')
             AND TO_NUMBER(TRIM(n.apgar_5_min)) < 7
            THEN 'S' ELSE 'N' END                            AS apgar_menor_que_7,
       CASE WHEN REGEXP_LIKE(TRIM(n.apgar_5_min), '^[0-9]+$')
            THEN 'S' ELSE 'N' END                            AS incluido,
       CASE WHEN n.apgar_5_min IS NULL
                 THEN 'apgar do 5o minuto nao registrado'
            WHEN NOT REGEXP_LIKE(TRIM(n.apgar_5_min), '^[0-9]+$')
                 THEN 'apgar do 5o minuto preenchido com texto nao numerico'
       END                                                   AS motivo_exclusao
  FROM infosaude.nascimento n
 WHERE n.fia_cd_hospital = :hospital
   AND n.dt_parto >= :ini AND n.dt_parto < :fim
   AND TRIM(n.id_condicao_nascimento) = 'V'
$analitico$
 WHERE aba = 3 AND numero = '13'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia   AS fia,
       f.dt_baixa                        AS internacao,
       f.dt_alta                         AS alta,
       f.cd_mot_cobranca_sus             AS motivo_de_saida,
       CASE WHEN f.cd_mot_cobranca_sus IN (41,42,43) THEN 'S' ELSE 'N' END AS incluido,
       CASE WHEN f.cd_mot_cobranca_sus NOT IN (41,42,43)
                 OR f.cd_mot_cobranca_sus IS NULL
            THEN 'saida por motivo diferente de obito' END                 AS motivo_exclusao
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital AND f.dt_alta >= :ini AND f.dt_alta < :fim
   AND EXISTS (SELECT 1 FROM infosaude.nascimento n
                WHERE n.fia_cd_hospital=f.cd_hospital AND n.dt_ano_fia=f.dt_ano_fia
                  AND n.nr_fia=f.nr_fia)
$analitico$
 WHERE aba = 3 AND numero = '17'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            // ---- Aba Perfil Epidemiológico ----

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                           AS baa,
       b.dt_atendimento                                          AS atendimento,
       NVL(LTRIM(m.ds_mot_atendimento, '. '), 'NAO INFORMADO')   AS categoria,
       'S'                                                       AS incluido,
       CAST(NULL AS VARCHAR2(120))                               AS motivo_exclusao
  FROM infosaude.baa b
  LEFT JOIN infosaude.motivo_atendimento m
    ON m.cd_mot_atendimento = b.cd_mot_atendimento
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
$analitico$
 WHERE aba = 4 AND numero = '1'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                                     AS baa,
       b.dt_atendimento                                                    AS atendimento,
       NVL(UPPER(r.ds_classificacao_risco), 'SEM CLASSIFICACAO DE RISCO')  AS categoria,
       'S'                                                                 AS incluido,
       CAST(NULL AS VARCHAR2(120))                                         AS motivo_exclusao
  FROM infosaude.baa b
  LEFT JOIN infosaude.classificacao_risco r
    ON r.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
$analitico$
 WHERE aba = 4 AND numero = '2'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT base.baa                                     AS baa,
       base.atendimento                             AS atendimento,
       base.dt_nascimento                           AS nascimento,
       base.sexo || ' ' || CHR(183) || ' ' || DECODE(base.ix,
          1,'0 a <1 ano', 2,'1-4', 3,'5-9', 4,'10-12', 5,'13-15', 6,'16-19',
          7,'20-29', 8,'30-39', 9,'40-49', 10,'50-59', 11,'60 e mais',
          'IDADE IGNORADA')                         AS categoria,
       'S'                                          AS incluido,
       CAST(NULL AS VARCHAR2(120))                  AS motivo_exclusao
  FROM (SELECT b.nr_baa || '/' || b.dt_ano_baa AS baa,
               b.dt_atendimento                AS atendimento,
               p.dt_nascimento                 AS dt_nascimento,
               CASE WHEN p.sexo = 'M' THEN 'M'
                    WHEN p.sexo = 'F' THEN 'F'
                    ELSE 'NI' END AS sexo,
               CASE
                 WHEN p.dt_nascimento IS NULL THEN 99
                 WHEN MONTHS_BETWEEN(b.dt_atendimento, p.dt_nascimento) <  12 THEN 1
                 WHEN MONTHS_BETWEEN(b.dt_atendimento, p.dt_nascimento) <  60 THEN 2
                 WHEN MONTHS_BETWEEN(b.dt_atendimento, p.dt_nascimento) < 120 THEN 3
                 WHEN MONTHS_BETWEEN(b.dt_atendimento, p.dt_nascimento) < 156 THEN 4
                 WHEN MONTHS_BETWEEN(b.dt_atendimento, p.dt_nascimento) < 192 THEN 5
                 WHEN MONTHS_BETWEEN(b.dt_atendimento, p.dt_nascimento) < 240 THEN 6
                 WHEN MONTHS_BETWEEN(b.dt_atendimento, p.dt_nascimento) < 360 THEN 7
                 WHEN MONTHS_BETWEEN(b.dt_atendimento, p.dt_nascimento) < 480 THEN 8
                 WHEN MONTHS_BETWEEN(b.dt_atendimento, p.dt_nascimento) < 600 THEN 9
                 WHEN MONTHS_BETWEEN(b.dt_atendimento, p.dt_nascimento) < 720 THEN 10
                 ELSE 11
               END AS ix
          FROM infosaude.baa b
          LEFT JOIN infosaude.paciente p
            ON p.cd_paciente = NVL(b.cd_paciente_unificado, b.cd_paciente)
         WHERE b.cd_hospital = :hospital
           AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim) base
$analitico$
 WHERE aba = 4 AND numero = '3'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa            AS baa,
       b.dt_atendimento                           AS atendimento,
       DECODE(TRUNC(b.dt_atendimento) - TRUNC(b.dt_atendimento, 'IW'),
              0,'SEG', 1,'TER', 2,'QUA', 3,'QUI', 4,'SEX', 5,'SAB', 6,'DOM')
       || ' ' || TO_CHAR(b.dt_atendimento, 'HH24') || 'h'  AS categoria,
       'S'                                        AS incluido,
       CAST(NULL AS VARCHAR2(120))                AS motivo_exclusao
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
$analitico$
 WHERE aba = 4 AND numero = '4'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa            AS baa,
       b.dt_atendimento                           AS atendimento,
       CASE WHEN p.cd_cidade = 330270
            THEN NVL(UPPER(TRIM(p.bairro)), 'BAIRRO NAO INFORMADO')
            ELSE NVL(UPPER(TRIM(p.bairro)), 'BAIRRO NAO INFORMADO')
                 || ' / ' || NVL(UPPER(c.ds_cidade), 'MUNICIPIO NAO INFORMADO')
       END                                        AS categoria,
       'S'                                        AS incluido,
       CAST(NULL AS VARCHAR2(120))                AS motivo_exclusao
  FROM infosaude.baa b
  LEFT JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(b.cd_paciente_unificado, b.cd_paciente)
  LEFT JOIN infosaude.cidade c
    ON c.cd_uf = p.cd_uf AND c.cd_cidade = p.cd_cidade
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
$analitico$
 WHERE aba = 4 AND numero = '5'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.nr_baa || '/' || b.dt_ano_baa                                AS baa,
       b.dt_atendimento                                               AS atendimento,
       NVL(UPPER(c.ds_cidade), '(sem municipio)')                     AS municipio_de_residencia,
       CASE WHEN p.cd_cidade IS NOT NULL AND p.cd_cidade <> 330270
            THEN 'S' ELSE 'N' END                                     AS nao_residente,
       CASE WHEN p.cd_cidade IS NOT NULL THEN 'S' ELSE 'N' END        AS incluido,
       CASE WHEN p.cd_cidade IS NULL
            THEN 'paciente sem municipio de residencia cadastrado'
       END                                                            AS motivo_exclusao
  FROM infosaude.baa b
  LEFT JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(b.cd_paciente_unificado, b.cd_paciente)
  LEFT JOIN infosaude.cidade c
    ON c.cd_uf = p.cd_uf AND c.cd_cidade = p.cd_cidade
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
$analitico$
 WHERE aba = 4 AND numero = '6'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT b.dt_ano_baa || '/' || b.nr_baa                     AS baa,
       b.dt_atendimento                                    AS atendimento,
       TO_CHAR(t.cd_proced_sia) || ' - '
       || NVL(ps.ds_proced_sia, 'PROCEDIMENTO NAO CADASTRADO')  AS categoria,
       'S'                                                 AS incluido,
       CAST(NULL AS VARCHAR2(120))                         AS motivo_exclusao
  FROM infosaude.baa b
  JOIN infosaude.tu_bpa_unificada t
    ON t.baa_cd_hospital = b.cd_hospital
   AND t.dt_ano_baa = b.dt_ano_baa AND t.nr_baa = b.nr_baa
  LEFT JOIN infosaude.proced_sia ps ON ps.cd_proced_sia = t.cd_proced_sia
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
 GROUP BY b.dt_ano_baa || '/' || b.nr_baa, b.dt_atendimento,
          TO_CHAR(t.cd_proced_sia) || ' - '
          || NVL(ps.ds_proced_sia, 'PROCEDIMENTO NAO CADASTRADO')
$analitico$
 WHERE aba = 4 AND numero = '7'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia            AS fia,
       f.dt_alta                                  AS alta,
       NVL(UPPER(TRIM(ci.ds_cidade)), 'MUNICIPIO NAO INFORMADO')
       || ' ' || CHR(183) || ' ' ||
       NVL(UPPER(TRIM(p.bairro)), 'BAIRRO NAO INFORMADO')       AS categoria,
       'S'                                        AS incluido,
       CAST(NULL AS VARCHAR2(120))                AS motivo_exclusao
  FROM infosaude.fia f
  LEFT JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
  LEFT JOIN infosaude.cidade ci
    ON ci.cd_uf = p.cd_uf AND ci.cd_cidade = p.cd_cidade
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini AND f.dt_alta < :fim
$analitico$
 WHERE aba = 4 AND numero = '8'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia                                     AS fia,
       f.dt_alta                                                           AS alta,
       NVL(UPPER(ci.ds_cidade), '(sem municipio)')                         AS municipio_de_residencia,
       CASE WHEN p.cd_uf IS NOT NULL AND p.cd_cidade IS NOT NULL
             AND NOT (p.cd_uf = 'RJ' AND p.cd_cidade = 330270)
            THEN 'S' ELSE 'N' END                                          AS nao_residente,
       CASE WHEN p.cd_uf IS NOT NULL AND p.cd_cidade IS NOT NULL
            THEN 'S' ELSE 'N' END                                          AS incluido,
       CASE WHEN p.cd_uf IS NULL OR p.cd_cidade IS NULL
            THEN 'paciente sem municipio/UF de residencia cadastrado'
       END                                                                 AS motivo_exclusao
  FROM infosaude.fia f
  LEFT JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
  LEFT JOIN infosaude.cidade ci
    ON ci.cd_uf = p.cd_uf AND ci.cd_cidade = p.cd_cidade
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini AND f.dt_alta < :fim
$analitico$
 WHERE aba = 4 AND numero = '9'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT s.fia                                       AS fia,
       s.alta                                      AS alta,
       s.dt_nascimento                             AS nascimento,
       s.sexo || ' ' || CHR(183) || ' ' ||
       DECODE(s.ix, 1,'0 a <1', 2,'1-4', 3,'5-9', 4,'10-12', 5,'13-15', 6,'16-19',
                    7,'20-29', 8,'30-39', 9,'40-49', 10,'50-59', 11,'60+',
                    'IDADE IGNORADA')              AS categoria,
       'S'                                         AS incluido,
       CAST(NULL AS VARCHAR2(120))                 AS motivo_exclusao
  FROM (SELECT f.nr_fia || '/' || f.dt_ano_fia AS fia,
               f.dt_alta                       AS alta,
               p.dt_nascimento                 AS dt_nascimento,
               CASE WHEN UPPER(p.sexo) = 'F' THEN 'F'
                    WHEN UPPER(p.sexo) = 'M' THEN 'M'
                    ELSE 'NI' END AS sexo,
               CASE
                 WHEN p.dt_nascimento IS NULL OR p.dt_nascimento > f.dt_alta        THEN 99
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) <  1   THEN 1
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) <  5   THEN 2
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 10   THEN 3
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 13   THEN 4
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 16   THEN 5
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 20   THEN 6
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 30   THEN 7
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 40   THEN 8
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 50   THEN 9
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 60   THEN 10
                 ELSE 11
               END AS ix
          FROM infosaude.fia f
          LEFT JOIN infosaude.paciente p
            ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
         WHERE f.cd_hospital = :hospital
           AND f.dt_alta >= :ini AND f.dt_alta < :fim) s
$analitico$
 WHERE aba = 4 AND numero = '10'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
SELECT f.nr_fia || '/' || f.dt_ano_fia            AS fia,
       f.dt_alta                                  AS alta,
       CASE WHEN f.cd_cid IS NULL THEN 'SEM CID REGISTRADO'
            ELSE TRIM(f.cd_cid) || ' ' || CHR(183) || ' ' ||
                 NVL(TRIM(c.ds_cid), 'DESCRICAO NAO CADASTRADA')
       END                                        AS categoria,
       'S'                                        AS incluido,
       CAST(NULL AS VARCHAR2(120))                AS motivo_exclusao
  FROM infosaude.fia f
  LEFT JOIN infosaude.cid c ON c.cd_cid = f.cd_cid
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini AND f.dt_alta < :fim
$analitico$
 WHERE aba = 4 AND numero = '11'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            // ---- Aba Institucional ----

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
WITH bpa_per AS (
  SELECT DISTINCT u.dt_ano_baa, u.nr_baa
    FROM infosaude.tu_bpa_unificada u
   WHERE u.cd_hospital = :hospital AND u.baa_cd_hospital = :hospital
     AND u.dt_atend >= :ini AND u.dt_atend < :fim
     AND u.dt_cmpt_bpa_unif >= TRUNC(:ini,'MM')
     AND u.dt_cmpt_bpa_unif <  ADD_MONTHS(TRUNC(:fim,'MM'), 3)
     AND u.id_situacao = 'L'
     AND u.dt_geracao IS NOT NULL
)
SELECT b.nr_baa || '/' || b.dt_ano_baa                            AS baa,
       b.dt_atendimento                                           AS atendimento,
       CASE WHEN p.nr_baa IS NULL THEN 'N' ELSE 'S' END            AS faturado_no_bpa,
       'S'                                                        AS incluido,
       CAST(NULL AS VARCHAR2(120))                                AS motivo_exclusao
  FROM infosaude.baa b
  LEFT JOIN bpa_per p
    ON p.dt_ano_baa = b.dt_ano_baa AND p.nr_baa = b.nr_baa
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
$analitico$
 WHERE aba = 5 AND numero = '10'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = $analitico$
WITH aih_ok AS (
  SELECT DISTINCT a.dt_ano_fia, a.nr_fia
    FROM infosaude.aih a
   WHERE a.cd_hospital = :hospital AND a.id_situacao_aih = 'L' AND a.nr_aih IS NOT NULL
)
SELECT f.nr_fia || '/' || f.dt_ano_fia                            AS fia,
       f.dt_baixa                                                 AS internacao,
       f.id_internacao                                            AS tipo_de_internacao,
       CASE WHEN x.nr_fia IS NULL THEN 'N' ELSE 'S' END            AS tem_aih_aprovada,
       CASE WHEN f.id_internacao = 'E' AND p.dt_nascimento IS NOT NULL
                 AND TRUNC(f.dt_baixa) - TRUNC(p.dt_nascimento) <= 1
            THEN 'N' ELSE 'S' END                                  AS incluido,
       CASE WHEN f.id_internacao = 'E' AND p.dt_nascimento IS NOT NULL
                 AND TRUNC(f.dt_baixa) - TRUNC(p.dt_nascimento) <= 1
            THEN 'recem-nascido internado no parto  -  nao gera AIH propria'
       END                                                         AS motivo_exclusao
  FROM infosaude.fia f
  LEFT JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
  LEFT JOIN aih_ok x
    ON x.dt_ano_fia = f.dt_ano_fia AND x.nr_fia = f.nr_fia
 WHERE f.cd_hospital = :hospital
   AND f.dt_baixa >= :ini AND f.dt_baixa < :fim
$analitico$
 WHERE aba = 5 AND numero = '11'
   AND excluido_em IS NULL AND sql_analitico IS NULL;
");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET sql_analitico = NULL
 WHERE excluido_em IS NULL
   AND (aba, numero) IN ((1, '2'), (1, '3.3'), (1, '3.4'), (1, '3.5'), (1, '4'), (1, '8'), (1, '9'), (1, '11'), (1, '12'), (1, '13'), (1, '14'), (1, '17'), (2, '2'), (2, '3.3'), (2, '3.4'), (2, '3.5'), (2, '7'), (2, '8'), (2, '9'), (2, '10'), (2, '12'), (3, '2'), (3, '3.2'), (3, '3.3'), (3, '3.4'), (3, '3.5'), (3, '7'), (3, '8'), (3, '9'), (3, '13'), (3, '17'), (4, '1'), (4, '2'), (4, '3'), (4, '4'), (4, '5'), (4, '6'), (4, '7'), (4, '8'), (4, '9'), (4, '10'), (4, '11'), (5, '10'), (5, '11'));
");
        }
    }
}
