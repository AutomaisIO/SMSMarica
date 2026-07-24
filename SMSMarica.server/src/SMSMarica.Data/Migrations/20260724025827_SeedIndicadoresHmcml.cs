using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <summary>
    /// Semeia o catálogo contratual de indicadores do HMCML (ADR-0022).
    ///
    /// O conteúdo vem da planilha "Indicadores HMCML Maricá 2026": nome, memória de cálculo,
    /// fonte declarada, meta e peso. O operador e o valor numérico da meta foram extraídos das
    /// FÓRMULAS da planilha (=IF(J3&lt;=5,$G3,0)), não do texto — é a regra que o contrato aplica.
    ///
    /// Os motores (SQL) foram escritos e conferidos contra dado real do Oracle do Salux
    /// (junho/2026, hospital 1). Indicadores sem motor entram como SemMotor ou ForaDoBanco,
    /// com a ressalva explicando o porquê — nenhum indicador é omitido.
    ///
    /// Idempotente: não insere se o catálogo já tiver linhas.
    /// </summary>
    /// <inheritdoc />
    public partial class SeedIndicadoresHmcml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
IF NOT EXISTS (SELECT 1 FROM smsmarica.indicador) THEN

INSERT INTO smsmarica.indicador
    (id, aba, numero, ordem, nome, memoria_calculo, fonte_declarada, meta, meta_operador,
     meta_valor, meta_valor_maximo, pontuacao, tipo_resultado, unidade_medida,
     fator_densidade, situacao, fonte_id, sql, ressalva, ativo, criado_em)
VALUES
(gen_random_uuid(), 1, '1', 10, 'TEMPO MÉDIO PARA ACOLHIMENTO/CADASTRO (exceto vermelho)', 'Σ do tempo (em min) entre a chegada do usuário (totem de chegada) e o registro do boletim de atendimento no Sistema de Informação hospitalar / número de pacientes registrados.', 'PEP* ou SIH**', '≤ 5 MIN', 1, 5.0, NULL, 1.0, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_chegada - ls.dt_hr_senha) * 1440), 2) AS valor,
       COUNT(*)                                              AS denominador
  FROM infosaude.baa b
  JOIN infosaude.acolhimento a
    ON a.cd_acolhimento = b.cd_acolhimento
  JOIN infosaude.lista_senha ls
    ON ls.cd_hospital    = a.cd_hospital_senha
   AND ls.cd_sala_painel = a.cd_sala_painel
   AND ls.dt_senha       = a.dt_senha
   AND ls.cd_tipo_senha  = a.cd_tipo_senha
   AND ls.nro_senha      = a.nro_senha
 WHERE b.cd_hospital = :hospital
   AND b.dt_chegada >= :ini
   AND b.dt_chegada <  :fim
   AND NVL(b.cd_setor, -1) NOT IN (38, 46, 50, 60, 71, 39, 48, 51, 67)
   AND ls.dt_hr_senha IS NOT NULL
   AND b.dt_chegada >= ls.dt_hr_senha
   AND NVL(b.cd_classificacao_risco, -1) NOT IN
       (SELECT cr.cd_classificacao_risco FROM infosaude.classificacao_risco cr
         WHERE UPPER(cr.ds_classificacao_risco) = ''VERMELHO'')', 'FONTE DA SENHA TROCADA: o carimbo indicado na planilha (ACOLHIMENTO.DT_HR_SENHA) esta 100% NULO em TODA a tabela - 0 de 1.569.466 linhas desde 05/2021 - e BAA.DT_SENHA grava so a data (hora sempre 00:00, o que produziria uma media absurda de 823 min). A hora real da senha existe em INFOSAUDE.LISTA_SENHA.DT_HR_SENHA (100% preenchida: 11.068/11.068 em jun/2026), ligada ao boletim por BAA -> ACOLHIMENTO -> LISTA_SENHA pela chave (cd_hospital_senha, cd_sala_painel, dt_senha, cd_tipo_senha, nro_senha); o join e 1:1 (8.807 linhas para 8.807 boletins distintos, sem duplicacao). FIM DO INTERVALO: usei BAA.DT_CHEGADA (abertura do boletim) e nao DT_ATENDIMENTO - a escolha e numericamente indiferente, pois as duas sao iguais em 12.029 dos 12.034 boletins adultos (diferenca media 0,000 min); DT_CHEGADA foi preferida por ser o carimbo semantico de abertura e 100% preenchida. COBERTURA 73,2%: ficam de fora 3.228 boletins - 1.232 sem acolhimento nenhum (entrada direta/ambulancia/porta da emergencia, que de fato nao passam pelo totem, exclusao legitima) e ~1.996 com acolhimento cuja senha nao casou na LISTA_SENHA. Outliers irrelevantes: truncar em 120 min derruba a media de 12,69 para apenas 12,43 (so 9 casos acima), por isso entrego sem truncamento. Vermelhos excluidos conforme a definicao (1 unico caso tinha senha). RECORTE ADULTO = cd_setor NOT IN (38,46,50,60,71,39,48,51,67) ou nulo; conferindo pelo corte alternativo de cd_especialidade, 166 desses 12.034 (1,4%) tem especialidade pediatrica/obstetrica e 704 dos 5.371 do bloco pedi/obst (13,1%) tem especialidade de adulto - os dois cortes nao coincidem exatamente, mas a contaminacao do lado adulto e pequena.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '2', 20, 'TEMPO MÉDIO PARA CLASSIFICAÇÃO DE RISCO (exceto vermelho)', 'Σ do tempo (em min) entre o cadastro e o registro da classificação de risco pelo enfermeiro no sistema de informação hospitalar / Número de pacientes classificados.', 'PEP ou SIH', '≤10 min', 1, 10.0, NULL, 1.0, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_classifica_atual - b.dt_chegada) * 1440), 2) AS valor,
       COUNT(*)                                                     AS denominador
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_chegada >= :ini
   AND b.dt_chegada <  :fim
   AND NVL(b.cd_setor, -1) NOT IN (38, 46, 50, 60, 71, 39, 48, 51, 67)
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_classifica_atual >= b.dt_chegada
   AND NVL(b.cd_classificacao_risco, -1) NOT IN
       (SELECT cr.cd_classificacao_risco FROM infosaude.classificacao_risco cr
         WHERE UPPER(cr.ds_classificacao_risco) = ''VERMELHO'')', 'COBERTURA 88,9%: DT_CLASSIFICA_ATUAL existe em 10.802 dos 12.034 boletins adultos (89,8%); os 1.232 sem classificacao sao majoritariamente os mesmos sem acolhimento. EXCLUSAO DE 95 REGISTROS INCONSISTENTES: eram classificacoes carimbadas ANTES da chegada (media -14,8 min); investiguei e os 95 tem cor NULA e DT_CLASSIFICA_ATUAL exatamente igual a ACOLHIMENTO.DT_ATENDIMENTO - ou seja, o campo herdou o carimbo do acolhimento em vez de uma classificacao real. O filtro dt_classifica_atual >= dt_chegada os remove; sem esse filtro a media seria 6,94 min (n=10.794). Vermelhos excluidos por definicao (so 8 no bloco adulto no mes, todos classificados). Distribuicao saudavel e coerente com a operacao (mediana 5 min), sem cauda que distorca a media: truncar em 60 min daria 6,71. RECORTE ADULTO = cd_setor NOT IN (38,46,50,60,71,39,48,51,67) ou nulo; pelo corte alternativo por especialidade a divergencia e de 1,4% no lado adulto e 13,1% no lado pediatrico/obstetrico.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '3', 30, 'TEMPO MÉDIO DE ESPERA PARA ATENDIMENTO MÉDICO, SEGUNDO CLASSIFICAÇÃO DE RISCO', NULL, NULL, NULL, NULL, NULL, NULL, 2.5, 5, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '3.1', 40, 'Vermelho', 'Σ do tempo transcorrido entre a classificação de risco até atendimento médico registrado no sistema de informação hospitalar / número de pacientes atendidos', 'PEP ou SIH', '0 min', 3, 1.0, NULL, 0.5, 1, 'min', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*)                                                       AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco c
    ON c.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_chegada >= :ini
   AND b.dt_chegada <  :fim
   AND NVL(b.cd_setor, -1) NOT IN (38, 46, 50, 60, 71, 39, 48, 51, 67)
   AND UPPER(c.ds_classificacao_risco) = ''VERMELHO''
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'AMOSTRA INSUFICIENTE - o numero rodou mas nao sustenta media: n=2. No bloco adulto de jun/2026 houve so 8 classificacoes VERMELHO (em todo 2026, 83 no hospital inteiro) e apenas 2 delas tem carimbo de atendimento medico (25%). O padrao e explicavel: o paciente vermelho vai direto para a sala de emergencia e o atendimento nao passa pelo fluxo de chamada que grava DT_ATEND_PAC - ou seja, justamente os casos de meta zero sao os que menos aparecem no campo. Por isso classifiquei NaoValidado: qualquer valor mensal aqui e ruido. Nota contratual: a meta de 0 min vem da escala Manchester; a parametrizacao do proprio sistema (CLASSIFICACAO_RISCO.QT_TEMPO) define 15 min para o vermelho no HMCML. Cobertura geral do campo: DT_ATEND_PAC existe em 5.421 dos 12.034 boletins adultos (45,0%).', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '3.2', 50, 'Laranja', NULL, NULL, '≤10 min', 1, 10.0, NULL, 0.5, 1, 'min', NULL, 3, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*)                                                       AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco c
    ON c.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_chegada >= :ini
   AND b.dt_chegada <  :fim
   AND NVL(b.cd_setor, -1) NOT IN (38, 46, 50, 60, 71, 39, 48, 51, 67)
   AND UPPER(c.ds_classificacao_risco) = ''LARANJA''
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'NAO HA COR LARANJA NO HMCML. A tabela CLASSIFICACAO_RISCO tem 11 linhas, mas so 5 com in_ativo=''S'': 7 VERMELHO (qt_tempo 15), 8 AMARELO (30), 9 VERDE (60), 10 AZUL (1440) e 11 SALUX (0) - uma escala de 4 cores, sem laranja. A escala Manchester completa existe apenas como codigos legados INATIVOS (1 EMERGENCIA qt_tempo 0, 2 MUITO URGENTE=laranja qt_tempo 10, 3 URGENTE 60, 4 POUCO URGENTE 120, 5 NAO URGENTE 240, todos in_ativo=''N'') e nao tem NENHUMA ocorrencia em 2026. Observacao relevante para o contrato: as metas dos indicadores 3.1 a 3.5 (0/10/60/120/240 min) batem exatamente com o QT_TEMPO da escala legada 1-5, o que indica que a planilha foi escrita sobre o Manchester de 5 niveis enquanto o hospital opera 4 cores com tempos-alvo diferentes (15/30/60/1440). O SQL foi escrito e executado (retorna denominador 0, sem erro) e passa a funcionar automaticamente se a cor laranja for ativada; hoje nao ha o que medir. Alternativa possivel, se a Secretaria aceitar: mapear o AMARELO atual (qt_tempo 30) como equivalente do laranja - NAO fiz isso por conta propria, seria inventar equivalencia clinica.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '3.3', 60, 'Amarelo', NULL, NULL, '≤60 min', 1, 60.0, NULL, 0.5, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*)                                                       AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco c
    ON c.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_chegada >= :ini
   AND b.dt_chegada <  :fim
   AND NVL(b.cd_setor, -1) NOT IN (38, 46, 50, 60, 71, 39, 48, 51, 67)
   AND UPPER(c.ds_classificacao_risco) = ''AMARELO''
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'COBERTURA 52,9%: houve 3.186 boletins adultos classificados AMARELO no mes e 1.687 tem DT_ATEND_PAC (2 excluidos por carimbo anterior a classificacao). O campo DT_ATEND_PAC so existe em 45,0% de todos os boletins adultos - ha risco de VIES DE SELECAO, porque quem evadiu antes de ser atendido e quem foi atendido sem passar pelo carimbo de chamada ficam fora, e ambos os grupos tenderiam a puxar o tempo para CIMA. MEDIA x MEDIANA: a media (40,8) e 2,5x a mediana (16,5) por causa de uma cauda longa (p99 = 6h39); truncar em 24h muda pouco (39,1 min), por isso entrego sem truncamento, so com o filtro de sanidade dt_atend_pac >= dt_classifica_atual. Nota contratual: a meta da planilha para amarelo e 60 min (Manchester), mas o proprio sistema parametriza 30 min para essa cor (CLASSIFICACAO_RISCO.QT_TEMPO) - contra 30 min o resultado NAO seria cumprido.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '3.4', 70, 'Verde', NULL, NULL, '≤120 min', 1, 120.0, NULL, 0.5, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*)                                                       AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco c
    ON c.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_chegada >= :ini
   AND b.dt_chegada <  :fim
   AND NVL(b.cd_setor, -1) NOT IN (38, 46, 50, 60, 71, 39, 48, 51, 67)
   AND UPPER(c.ds_classificacao_risco) = ''VERDE''
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'COBERTURA 45,3%: houve 6.492 boletins adultos classificados VERDE no mes e 2.938 tem DT_ATEND_PAC. Como em todos os A3, o campo cobre so 45,0% dos boletins adultos e ha risco de VIES DE SELECAO (evasao antes do atendimento e atendimentos sem carimbo ficam fora e tenderiam a aumentar o tempo). Esta e a amostra mais robusta do grupo e a mais confiavel para acompanhamento mensal. Media (71,2) x mediana (47,2) mostram cauda moderada; truncar em 24h praticamente nao muda (70,7 min), entao entrego sem truncamento. Nota contratual: a meta da planilha para verde e 120 min (Manchester); o sistema parametriza 60 min para essa cor - contra 60 min o resultado NAO seria cumprido.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '3.5', 80, 'Azul', NULL, NULL, '≤240 min', 1, 240.0, NULL, 0.5, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*)                                                       AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco c
    ON c.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_chegada >= :ini
   AND b.dt_chegada <  :fim
   AND NVL(b.cd_setor, -1) NOT IN (38, 46, 50, 60, 71, 39, 48, 51, 67)
   AND UPPER(c.ds_classificacao_risco) = ''AZUL''
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'COBERTURA 40,7% - a menor do grupo: houve 1.018 boletins adultos classificados AZUL no mes e 414 tem DT_ATEND_PAC. Mesmo risco de vies de selecao dos demais A3 (campo cobre 45,0% dos boletins adultos). ACHADO CONTRA-INTUITIVO, registrado de proposito: o azul (59,5 min) espera MENOS que o verde (71,2 min), quando a logica da classificacao previa o contrario. Nao e erro de SQL - a distribuicao inteira do azul e mais curta (mediana 34 x 47 do verde, max 287 min x 1.524). Explicacao provavel: o azul concentra demanda simples e resolvida rapido (renovacao de receita, atestado, retorno) frequentemente atendida fora da fila de urgencia; mas isso e hipotese, nao verifiquei. Amostra pequena (414) para leitura mensal isolada. Nota contratual: meta da planilha 240 min (Manchester); o sistema parametriza 1.440 min para o azul.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '4', 90, 'TEMPO MÉDIO DE PERMANÊNCIA NA EMERGÊNCIA', 'Σ do tempo transcorrido entre a classificação de risco até atendimento médico no sistema de informação hospitalar / número de pacientes atendidos pelo médico', 'PEP ou SIH', '≤ 8h', 1, 8.0, NULL, 0.5, 1, 'h', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_saida - b.dt_chegada) * 24), 2) AS valor,
       COUNT(*)                                        AS denominador
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_chegada >= :ini
   AND b.dt_chegada <  :fim
   AND NVL(b.cd_setor, -1) NOT IN (38, 46, 50, 60, 71, 39, 48, 51, 67)
   AND b.dt_saida IS NOT NULL
   AND b.dt_saida >= b.dt_chegada
   AND (b.dt_saida - b.dt_chegada) * 24 <= 72', 'MEMORIA DE CALCULO DA PLANILHA ESTA ERRADA - ela repete a do item 3 (classificacao -> atendimento medico). Usei a definicao do NOME do indicador: DT_CHEGADA -> DT_SAIDA. Marquei NaoValidado por TRES problemas, todos medidos: (1) COBERTURA 58% - DT_SAIDA existe em so 6.981 dos 12.034 boletins adultos; os 5.053 sem saida nem entram na conta. (2) CAMPO SEMANTICAMENTE CONTAMINADO - nos boletins que tem atendimento medico, DT_SAIDA e IGUAL a DT_ATEND_PAC em 2.510 de 2.735 casos (92%): na pratica o sistema carimba a saida junto com o atendimento, de modo que para esse grupo o indicador mede chegada -> atendimento medico (media 1,47 h) e NAO a permanencia real, ignorando observacao/medicacao pos-consulta. Logo o valor SUBESTIMA a permanencia. (3) BOLETINS ABANDONADOS - 809 registros (11,6% dos que tem saida) passam de 72h, chegando a 50 dias; investiguei e TODOS os 809 nao tem atendimento medico registrado e quase todos tem destino ''C'' (alta para casa): sao boletins nunca encerrados, fechados em lote depois. Sem excluir esses, a media do mes vai a 45,73 h - numero sem qualquer sentido clinico para um pronto-atendimento cuja mediana e 1,42 h. Por isso o SQL entregue traz o corte explicito de 72h, que e ARBITRARIO (declarado no proprio WHERE para ficar auditavel); a serie so sera comparavel se o corte for mantido fixo. RECOMENDACAO: pedir ao gestor do Salux a regra de gravacao de DT_SAIDA antes de contratualizar este indicador - do jeito que o campo esta, ele nao mede permanencia na emergencia.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '5', 100, 'MÉDIA DO TEMPO PORTA-AGULHA EM PACIENTE COM SUSPEITA DE INFARTO AGUDO DO MIOCÁRDIO (elegível p/ terapia fibrinolítica)', 'Média do tempo transcorrido entre a abertura do protocolo de suspeita de IAM c/ SST, elegível a trombólise, na classificação de risco até a realização de terapia fibrinolítica', 'PEP ou SIH', '≤ 90 MIN', 1, 90.0, NULL, 1.0, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '6', 110, 'MÉDIA DO TEMPO PORTA – TOMOGRAFIA PARA PACIENTES COM SUSPEITA DE AVC', 'Média do tempo entre a hora de abertura do protocolo de suspeita ou diagnóstico de Acidente Vascular Cerebral na classificação de risco, com o tempo de realização da tomografia', 'PEP ou SIH e laudo tomografia', '≤ 60 min', 1, 60.0, NULL, 1.0, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '7', 120, 'PERCENTUAL DE PACIENTES COM DIAGNÓSTICO DE SEPSE QUE INICIARAM ANTIBIOTICOTERAPIA EM ATÉ 3 HORAS', 'Total de pacientes com antibióticos infundidos em um tempo ≤ 3 horas na SEPSE / total de pacientes com diagnóstico de SEPSE que receberam antibioticoterapia * 100. OBS: Em caso de CHOQUE SÉPTICO, tem que ser administrado ABT na primeira hora', 'PEP ou SIH com Prescrição Médica e Relatório da CCIH', '1', 2, 1.0, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '8', 130, 'ÍNDICE DE QUESTIONÁRIOS PREENCHIDOS PELOS PACIENTES DA URGÊNCIA E EMERGÊNCIA', 'Número de questionários respondidos / Total de pacientes atendidos *100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥10%', 2, 0.1, NULL, 0.25, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'WITH pesq AS (
  SELECT DISTINCT m.baa_cd_hospital AS cd_hospital,
                  m.dt_ano_baa      AS dt_ano_baa,
                  m.nr_baa          AS nr_baa
    FROM infosaude.edoc_movimento m
    JOIN infosaude.edoc_modelo md ON md.cd_modelo = m.cd_modelo
   WHERE m.cd_hospital = :hospital
     AND m.in_status = ''D''
     AND m.in_ativo = ''S''
     AND UPPER(md.ds_modelo) LIKE ''%SATISFA%''
     AND m.nr_baa IS NOT NULL
),
atend AS (
  SELECT b.cd_hospital, b.dt_ano_baa, b.nr_baa
    FROM infosaude.baa b
   WHERE b.cd_hospital = :hospital
     AND b.dt_atendimento >= :ini
     AND b.dt_atendimento <  :fim
     AND (b.cd_setor IS NULL OR b.cd_setor NOT IN (38,46,50,60,71,39,48,51,67))
)
SELECT COUNT(p.nr_baa) AS numerador, COUNT(*) AS denominador
  FROM atend a
  LEFT JOIN pesq p
    ON p.cd_hospital = a.cd_hospital
   AND p.dt_ano_baa  = a.dt_ano_baa
   AND p.nr_baa      = a.nr_baa', 'MODELOS DE PESQUISA EXISTENTES (edoc_modelo, LIKE ''%SATISFA%''/''%PESQUISA%''): 10182 Pesquisa de Satisfacao do Usuario (INATIVO); 10183 Pesquisa de Satisfacao HMDECG (INATIVO); 10192 idem 10182 (INATIVO); 10195 idem 10183 (INATIVO); 10208 Pesquisa de satisfacao do usuario - Internacao (ativo); 10209 Pesquisa de satisfacao do usuario - Ambulatorio (ativo); 10226 UPA Inoa (ativo); 10227 Sta Rita (ativo); 10228 Observacao UPA Inoa (ativo); 10242 Observacao - HMCML (INATIVO); 10304 Maternidade (ativo); 10308 upa inoa (ativo). DOCUMENTOS EM JUN/2026 NO HOSPITAL 1: SO o 10209 teve movimento -- 1.898 com in_status=''D''/in_ativo=''S'' (+8 parciais); TODOS os outros modelos = 0, inclusive o 10208 (Internacao), o 10304 (Maternidade) e o 10242 (Observacao). Serie 2026 do 10209 no hosp 1: jan 1.231, fev 536, mar 1.556, abr 1.016, mai 998, jun 1.906, jul 1.614. | DOS 1.898 DOCS DE JUNHO: 1.784 vinculados a BAA e 114 a FIA (nenhum sem vinculo). | DEDUPLICACAO: 1.784 pesquisas apontam para apenas 1.666 BAA distintos (118 duplicadas) -- por isso o motor conta ATENDIMENTOS com pesquisa (LEFT JOIN + DISTINCT), nao pesquisas. | RECORTE: a pesquisa e aplicada tambem fora do bloco adulto: adulto 1.428/12.034 = 11,87%, pediatria 234/4.443 = 5,27%, obstetricia 4/928 = 0,43%; urgencia inteira 1.666/17.405 = 9,57%. Se voce misturar numerador de todos os blocos com denominador so adulto (leitura literal do enunciado) da 1.784/12.034 = 14,82% -- numero inflado, NAO use. | ANCORAGEM: o motor conta pelo ATENDIMENTO (BAA com dt_atendimento no periodo, pesquisa de qualquer data). Em junho a variante estrita (pesquisa preenchida dentro do proprio mes) da exatamente o mesmo 1.428, entao nao ha vazamento de borda neste mes -- mas em outros meses o valor de um mes fechado pode subir se chegarem pesquisas atrasadas. | COMPARACAO: mar/2026 o mesmo motor da 1.102/13.932 = 7,91% (abaixo da meta) -- o indicador oscila muito, junho nao e patamar. | Denominador usa dt_atendimento (100% preenchido) e o recorte de bloco por cd_setor do briefing.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '9', 140, 'PERCENTUAL DE USUÁRIOS DA U&E SATISFEITOS / MUITO SATISFEITOS', 'Número de conceitos satisfeito e muito satisfeito / Total de respostas efetivas *100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥85%', 2, 0.85, NULL, 0.25, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'WITH pesq AS (
  SELECT m.cd_hospital, m.ano_movimento, m.id_movimento
    FROM infosaude.edoc_movimento m
    JOIN infosaude.edoc_modelo md ON md.cd_modelo = m.cd_modelo
   WHERE m.cd_hospital = :hospital
     AND m.in_status = ''D''
     AND m.in_ativo = ''S''
     AND UPPER(md.ds_modelo) LIKE ''%SATISFA%''
     AND m.nr_baa IS NOT NULL
     AND m.dt_inclusao >= :ini
     AND m.dt_inclusao <  :fim
)
SELECT SUM(CASE WHEN REPLACE(mi.ds_resposta,'' '','''') IN (''9-10'',''7-8'') THEN 1 ELSE 0 END) AS numerador,
       COUNT(*) AS denominador
  FROM pesq p
  JOIN infosaude.edoc_movimento_item mi
    ON mi.cd_hospital   = p.cd_hospital
   AND mi.ano_movimento = p.ano_movimento
   AND mi.id_movimento  = p.id_movimento
 WHERE mi.cd_item = 12648
   AND mi.ds_resposta IS NOT NULL', 'PERGUNTA USADA: cd_item = 12648 ''De um modo geral, como o paciente avalia o servico geral do hospital?'' -- unica pergunta de avaliacao global do formulario. A escala nao e textual: as respostas sao FAIXAS ''1 - 2'', ''3 - 4'', ''5 - 6'', ''7 - 8'', ''9 - 10'' (ha variacao de espacamento, ex. ''9 -10'', por isso o REPLACE de espacos). A legenda Muito insatisfeito/Insatisfeito/neutro/Satisfeito/Muito Satisfeito existe no formulario como itens fixos (12338-12342) sem resposta gravada -- o mapeamento faixa->rotulo (7-8 = Satisfeito, 9-10 = Muito Satisfeito) e interpretacao minha, coerente com a ordem da legenda; vale confirmar com a Ouvidoria antes de virar numero contratual. | COBERTURA: 1.898/1.898 dos documentos de junho tem essa resposta (100%); em mar/2026 idem (1.553/1.553 no 10209 e 242/242 no 10208). O item 12648 so existe a partir do documento 3 do modelo 10209 (e doc 8 do 10208) -- se trocarem a versao do formulario por uma sem essa pergunta o motor devolve denominador 0 (mostra vazio, nao numero errado). | O denominador aqui e RESPONDENTE, nao atendimento: 1.784 respostas para 17.405 atendimentos do mes (cobertura de ~10%, ver A8). | VIES: a coleta e feita pela propria equipe do hospital, 82,5% com o paciente e 17,5% com acompanhante (item 12337: Paciente 1.565 / Acompanhante 333). So 35 respostas em 1.784 ficaram abaixo de 7 -- 98% de satisfacao com distribuicao tao concentrada e tipico de viesde desejabilidade/coleta assistida, nao de leitura independente. | ALTERNATIVA DE RECORTE: se o contrato quiser so o bloco adulto, o valor e 1.542/1.545 = 99,81%; incluindo pediatria e obstetricia (entregue aqui) = 98,04%. | O periodo aqui filtra a DATA DA PESQUISA (dt_inclusao), diferente de A8 que ancora no atendimento -- em junho as duas leituras coincidem porque 100% das pesquisas de junho apontam para BAA de junho.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '10', 150, 'NET PROMOTER SCORE (NPS) DA URGÊNCIA E EMERGÊNCIA', NULL, NULL, '≥50%', 2, 0.5, NULL, 0.25, 1, '%', NULL, 3, NULL, NULL, 'NAO EXISTE PERGUNTA DE RECOMENDACAO 0-10 NO QUESTIONARIO -- verifiquei item a item o modelo 10209 (documento 3, o unico em uso em jun/2026) e o 10208 (documento 8). O que existe: (a) cd_item 12361 ''Voce indicaria este servico a um familiar, amigo ou parente?'' com resposta BINARIA Sim/Nao -- jun/2026 urgencia: 1.761 Sim e 23 Nao = 98,71% de ''Sim''; (b) cd_item 12648 ''De um modo geral, como o paciente avalia o servico geral do hospital?'' com escala 1 a 10 agrupada em FAIXAS (1-2, 3-4, 5-6, 7-8, 9-10) -- e pergunta de AVALIACAO, nao de recomendacao. Se a gestao aceitar tratar as faixas do 12648 como escala NPS (promotor 9-10, neutro 7-8, detrator <= 6), o valor medido em jun/2026 para a urgencia seria 88,57 -- MAS ISSO NAO E NPS: (i) a pergunta nao e de intencao de recomendacao; (ii) a escala comeca em 1 e nao em 0; (iii) o agrupamento em faixas impede separar 6 (detrator) de 5, e junta 9 com 10. Entregar 88,57 rotulado como NPS seria numero errado com cara de certo. Para ter NPS de verdade basta a Ouvidoria incluir no eDoc a pergunta ''De 0 a 10, quanto voce recomendaria este hospital?'' com resposta unitaria (0..10); a partir dai o motor e trivial e eu escrevo em minutos.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '11', 160, 'TAXA DE OCUPAÇÃO HOSPITALAR', 'Número de paciente-dia / Número de leito-dia operacionais*100', 'PEP ou SIH', '≥ 85%', 2, 0.85, NULL, 0.5, 1, '%', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'WITH pacientes_dia AS (
  SELECT SUM(LEAST(COALESCE(fl.dt_saida_leito, f.dt_alta, :fim), :fim)
             - GREATEST(fl.dt_transferencia, :ini)) AS dias
    FROM infosaude.fia_leito fl
    JOIN infosaude.fia f
      ON f.cd_hospital = fl.cd_hospital
     AND f.dt_ano_fia  = fl.dt_ano_fia
     AND f.nr_fia      = fl.nr_fia
   WHERE fl.cd_hospital = :hospital
     AND fl.cd_unidade NOT IN (10,13,30,31,32,33,34,15,16,17,26)
     AND fl.dt_transferencia < :fim
     AND LEAST(COALESCE(fl.dt_saida_leito, f.dt_alta, :fim), :fim) > :ini
     AND (fl.dt_saida_leito IS NOT NULL OR f.dt_alta IS NOT NULL
          OR fl.dt_transferencia >= :ini)
), leitos_dia AS (
  SELECT COUNT(*) * (:fim - :ini) AS dias
    FROM infosaude.leito l
   WHERE l.cd_hospital = :hospital
     AND l.id_condicao = ''A''
     AND l.cd_unidade NOT IN (10,13,30,31,32,33,34,15,16,17,26)
)
SELECT ROUND(p.dias, 2) AS numerador, l.dias AS denominador
  FROM pacientes_dia p CROSS JOIN leitos_dia l', 'RECORTE ADULTO - hipotese do briefing VALIDADA COM CORRECAO. Medi idade/sexo por unidade em 12 meses (jul/25-jun/26): as unidades 32 (ENFERMARIA LUA, n=394, 99,7% menores de 18a, mediana 5,5a) e 33 (ENFERMARIA SOL, n=398, 99,5% menores de 18a, mediana 0,9a) sao inequivocamente PEDIATRICAS e NAO estavam na lista do briefing. Incluí ambas no bloco pediatrico. Pediatrico final = 10,13,30,31,32,33,34 (32 e 33 sozinhas tem 792 passagens/12m, mais que 10+13+30+31 = 792 juntas - ou seja, o corte do briefing perdia METADE da pediatria). Materno = 15,16,17,26 confirmado (unidade 15 tem 35% de menores de 18 = recem-nascidos em alojamento conjunto; 84% feminino). Unidades 34 (CENTRO PEDIATRICO) e 26 (MATERNIDADE 2) tem ZERO passagens em 12 meses - mantidas na exclusao por seguranca. O flag id_internacao_obstetrica vem NULO em 734/734 FIA de junho (inutil, como o briefing ja alertava para in_unid_obstetrica).

''LEITO OPERACIONAL'' = leito.id_condicao = ''A''. Essa escolha foi VALIDADA contra o movimento real: todas as unidades cujos leitos sao 100% ''I'' (7 SALA AMARELA, 20 COVID, 21 SALA AMARELA, 111 e 112 CLINICA MEDICA FEM, 26 MATERNIDADE 2) tem ZERO passagens em 12 meses - concordancia perfeita. NAO usei qt_leito_fixo (zerado) nem id_sit_leito (F/L/O e um retrato do AGORA, nao serve para mes fechado). Adulto: 374 leitos cadastrados, 210 ativos.

POR QUE MARQUEI NaoValidado: o motor roda e o numero e coerente, mas o resultado oscila 12 pontos percentuais conforme QUAIS leitos entram no denominador, e a base nao tem flag de ''tipo de unidade'' para decidir isso objetivamente. Com todo o inventario ativo = 50,60%; so com enfermarias de internacao = 62,44%. A diferenca vem de leitos que por natureza giram vazios: pos-operatorio (11/12/25, 43 leitos, 26-63% de ocupacao), centro cirurgico (5 e 28, 19 leitos, 0% e 2,2%), hipodermia (9, 29 leitos, 35,4%) e trauma (6, 31 leitos, 45,9%). Precisa de decisao do contrato/direcao sobre o conjunto de leitos. O que NAO muda com a decisao: em qualquer variante a meta de 85% nao e alcancada. As enfermarias clinicas puras SIM rodam no alvo (CLINICA MEDICA MASC 85,1%, CLINICA MEDICA FEM 83,5%, SAUDE MENTAL 82,4%).

SANIDADE DO NUMERO: 3.188 pacientes-dia contra 437 saidas adultas x 7,85 dias de permanencia media = 3.430 dias-paciente (o valor cortado pelo periodo tem que ser um pouco menor) - bate. Cobertura de fia_leito e 100% (0 de 739 baixas de junho sem linha de leito).

LIXO REMOVIDO: existem 12 passagens de leito ABERTAS (dt_saida_leito e dt_alta ambos nulos) tocando junho, 8 delas iniciadas antes do mes - registros abandonados, incluindo um paciente ''ocupando'' leito na HIPODERMIA PEDIATRIC desde 03/02/2022 (1.608 dias) e outro na HIPODERMIA desde 24/05/2024 (767 dias). A clausula ''(dt_saida_leito IS NOT NULL OR dt_alta IS NOT NULL OR dt_transferencia >= :ini)'' descarta esses; sem ela o numerador sobe para 3.248 (+1,9%). EFEITO COLATERAL ACEITO: essa mesma clausula descarta um paciente legitimamente internado desde antes do periodo e ainda nao dado alta no momento da apuracao - irrelevante para mes fechado (a alta ja foi lancada), mas subestima levemente se o motor rodar sobre o mes corrente. Sobra ainda 1 registro suspeito nao capturado: FIA 2024/113998, TRAUMA, entrada 21/09/2024 e saida de leito 13/07/2026 (660 dias) com dt_alta nula - contribui 30 pacientes-dia (~0,9%).', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '12', 170, 'MÉDIA DE PERMANÊNCIA ATÉ ≤59 ANOS', 'Número de paciente-dia / Número de saídas /mês *100', 'PEP ou SIH', '≤ 6,5 dias', 1, 6.5, NULL, 0.5, 1, 'dias', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG(f.dt_alta - f.dt_baixa), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.fia f
  JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim
   AND FLOOR(MONTHS_BETWEEN(f.dt_baixa, p.dt_nascimento) / 12) <= 59
   AND (SELECT MAX(fl.cd_unidade) KEEP (DENSE_RANK LAST ORDER BY fl.dt_transferencia)
          FROM infosaude.fia_leito fl
         WHERE fl.cd_hospital = f.cd_hospital
           AND fl.dt_ano_fia  = f.dt_ano_fia
           AND fl.nr_fia      = f.nr_fia)
       NOT IN (10,13,30,31,32,33,34,15,16,17,26)', 'Permanencia calculada como (dt_alta - dt_baixa) em dias fracionarios - as duas colunas tem componente de hora, entao o valor ja e permanencia real, nao contagem de datas. NAO usei nr_dias_internacao: essa coluna esta 0% preenchida nas 734 altas de junho (contei antes de descartar). Nenhuma permanencia negativa (0 de 734) e nenhuma acima de 180 dias.

Idade apurada NA BAIXA (nao na alta) por MONTHS_BETWEEN/12 com FLOOR = anos completos. Nao usei infosaude.f_busca_idade porque ela retorna VARCHAR2 e quebra com ORA-01722 em contexto numerico. Cobertura de dt_nascimento: 100% (zero saidas adultas sem data de nascimento - testei com LEFT JOIN e a faixa ''SEMIDADE'' voltou vazia, por isso o INNER JOIN acima nao perde ninguem).

Classificacao adulto/pediatrico/materno pela ULTIMA unidade ocupada (unidade de alta, via MAX ... KEEP DENSE_RANK LAST). Essa escolha e pouco sensivel: das 734 altas de junho, apenas 11 (1,5%) passaram por mais de um bloco (6 materno+adulto, 5 pediatrico+adulto); 431 sao so-adulto, 208 so-materno, 84 so-pediatrico. Recorte pediatrico ja corrigido para incluir as unidades 32 e 33 (ver ressalva do A11).

A media e puxada por cauda: mediana 3,59 contra media 5,94. Se o contrato quiser resistencia a outlier, a mediana e o numero mais representativo - mas entreguei a media, que e o que a meta pede.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '13', 180, 'MÉDIA DE PERMANÊNCIA ≥60 ANOS', 'Número de paciente-dia ≥60 ano s/ Número de saídas/mês de pacientes ≥60 anos', 'PEP ou SIH', '≤ 7,4 dias', 1, 7.4, NULL, 0.5, 1, 'dias', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG(f.dt_alta - f.dt_baixa), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.fia f
  JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim
   AND FLOOR(MONTHS_BETWEEN(f.dt_baixa, p.dt_nascimento) / 12) >= 60
   AND (SELECT MAX(fl.cd_unidade) KEEP (DENSE_RANK LAST ORDER BY fl.dt_transferencia)
          FROM infosaude.fia_leito fl
         WHERE fl.cd_hospital = f.cd_hospital
           AND fl.dt_ano_fia  = f.dt_ano_fia
           AND fl.nr_fia      = f.nr_fia)
       NOT IN (10,13,30,31,32,33,34,15,16,17,26)', 'Mesmas regras e mesmas validacoes do A12 (permanencia por diferenca de datas, idade na baixa, ultima unidade, recorte pediatrico corrigido com as unidades 32 e 33). 193 + 244 = 437 saidas adultas de junho, sem sobra nem faixa ''sem idade''.

O resultado e clinicamente coerente: idoso interna mais tempo que adulto jovem (9,36 contra 5,94 dias). A media estoura a meta mas a mediana e 6,13 dias - ou seja, metade dos idosos sai em ate 6 dias e o excesso vem de uma cauda de permanencia longa (maximo 108,5 dias). Isso e informacao de gestao, nao erro de calculo: se a direcao quiser separar ''permanencia clinica'' de ''permanencia social'' (paciente com alta medica mas sem destino), o campo dt_alta_medica existiria para isso - mas esta 0% preenchido em junho/2026, entao nao da para medir esse recorte.

Serie mensal do bloco adulto (jan a jun/2026), como controle de estabilidade do motor: 8,40 / 7,17 / 8,05 / 7,85 / 7,43 / 7,85 dias de permanencia media geral. Sem salto anomalo em junho.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '14', 190, 'PROPORÇÃO DE REINTERNAÇÕES EM ATÉ 30 DIAS DA ALTA HOSPITALAR****', NULL, 'PEP ou SIH', '≤ 20%', 1, 0.2, NULL, 0.5, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT SUM(CASE WHEN r.gap IS NOT NULL AND r.gap <= 30 THEN 1 ELSE 0 END) AS numerador,
       COUNT(*) AS denominador
  FROM (SELECT (SELECT MIN(f.dt_baixa - a.dt_alta)
                  FROM infosaude.fia a
                 WHERE a.cd_hospital = f.cd_hospital
                   AND NVL(a.cd_paciente_unificado, a.cd_paciente)
                       = NVL(f.cd_paciente_unificado, f.cd_paciente)
                   AND NOT (a.dt_ano_fia = f.dt_ano_fia AND a.nr_fia = f.nr_fia)
                   AND a.dt_alta IS NOT NULL
                   AND a.dt_alta <= f.dt_baixa) AS gap
          FROM infosaude.fia f
         WHERE f.cd_hospital = :hospital
           AND f.dt_baixa >= :ini
           AND f.dt_baixa <  :fim
           AND (SELECT MIN(fl.cd_unidade) KEEP (DENSE_RANK FIRST ORDER BY fl.dt_transferencia)
                  FROM infosaude.fia_leito fl
                 WHERE fl.cd_hospital = f.cd_hospital
                   AND fl.dt_ano_fia  = f.dt_ano_fia
                   AND fl.nr_fia      = f.nr_fia)
               NOT IN (10,13,30,31,32,33,34,15,16,17,26)) r', 'DEFINICAO ADOTADA: o denominador sao as INTERNACOES do periodo (dt_baixa dentro da janela, 431 no bloco adulto) e o numerador sao aquelas que ocorreram ate 30 dias depois de uma alta anterior do MESMO paciente. Self-join em FIA por NVL(cd_paciente_unificado, cd_paciente), como pedido. Escolhi essa direcao (olhando para tras) porque a alternativa - ''das altas do periodo, quantas voltaram em 30 dias'' - exige 30 dias de janela FUTURA, que nao existe para o ultimo mes apurado e produziria um indicador sistematicamente subestimado.

Distribuicao completa do intervalo desde a alta anterior (hospital inteiro, 739 baixas de junho): 509 sem internacao anterior, 182 com intervalo maior que 30 dias, 26 entre 8 e 30 dias, 20 entre 1 e 7 dias, 2 no mesmo dia. No recorte adulto os casos de mesmo dia sao ZERO, ou seja, o numero nao esta contaminado por reabertura administrativa de ficha (que e o risco classico desse indicador).

LIMITES HONESTOS: (1) so enxerga retorno ao proprio HMCML - se o paciente reinternou na UPA Inoa, no PA Santa Rita ou em qualquer hospital de fora, o motor nao ve; (2) a internacao ANTERIOR nao foi restringida ao bloco adulto nem a unidade nenhuma (qualquer FIA do hospital 1 conta como alta previa) - isso e deliberado, uma alta da maternidade seguida de reinternacao clinica em 20 dias E uma reinternacao; (3) nao ha distincao entre reinternacao planejada (quimioterapia, cirurgia em etapas) e nao planejada, porque a base nao tem esse marcador - o indicador contratual tambem nao faz essa distincao; (4) o corte e por intervalo entre alta e nova baixa, incluindo intervalo zero, mas como visto isso nao ocorre no bloco adulto.

Classificacao do bloco pela PRIMEIRA unidade ocupada (unidade de entrada), coerente com o fato de o evento medido ser a admissao. Cobertura: 0 de 739 baixas de junho sem linha em fia_leito.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '15', 200, 'TAXA DE PARADA CARDIORRESPIRATÓRIA NA ENFERMARIA *****', 'Número de óbitos institucionais ocorridos na enfermaria / total de internações na enfermaria no período', 'PEP ou SIH', '≤1 óbito por 1.000 internações', 1, 1.0, NULL, 1.0, 2, 'obitos por 1.000 internacoes', 1000.0, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT SUM(CASE WHEN f.cd_mot_cobranca_sus IN (41,42,43) THEN 1 ELSE 0 END) AS numerador,
       COUNT(*) AS denominador
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim
   AND (SELECT MAX(fl.cd_unidade) KEEP (DENSE_RANK LAST ORDER BY fl.dt_transferencia)
          FROM infosaude.fia_leito fl
         WHERE fl.cd_hospital = f.cd_hospital
           AND fl.dt_ano_fia  = f.dt_ano_fia
           AND fl.nr_fia      = f.nr_fia)
       IN (1,9,18,19,20,22,111,112,125)', 'DIVERGENCIA GRAVE, LEIA ANTES DE PUBLICAR. O indicador se chama ''taxa de parada cardiorrespiratoria na enfermaria'' e a meta e <= 1 por 1.000 internacoes, mas a memoria de calculo da planilha conta OBITOS. Implementei obitos como instruido e o resultado e 98,64 por 1.000 - QUASE CEM VEZES a meta. Isso nao e desempenho ruim do hospital: e prova de que os dois lados do indicador medem coisas diferentes. Uma meta de 1 obito por 1.000 internacoes (0,1% de mortalidade) e clinicamente impossivel para qualquer hospital geral; ela so faz sentido para EVENTOS DE PCR EM LEITO DE ENFERMARIA (parada inesperada fora de area critica), que e um evento sentinela raro. Publicar este numero rotulado como ''PCR'' seria informacao errada com cara de certa. Recomendacao: ou o contrato passa a chamar o indicador de ''taxa de mortalidade em enfermaria'' (e a meta e renegociada), ou o hospital precisa criar registro proprio de PCR/RCP.

NAO EXISTE MOTOR PARA PCR DE VERDADE: procurei e nao ha na base nenhuma tabela de evento de parada, codigo de RCP, nem campo de time de resposta. O que existiria de mais proximo seria buscar CID de parada cardiaca (I46) em edoc_movimento_item, mas essa tabela tem 166 milhoes de linhas e, mesmo achando, registraria o diagnostico, nao o evento assistencial - continuaria nao sendo PCR na enfermaria.

DEFINICAO DE OBITO: cd_mot_cobranca_sus IN (41,42,43), confirmado contra a tabela infosaude.motivo_cobranca_sus - 41 ''OBITO COM DECLARACAO DE OBITO FORNECIDA PELO MEDICO ASSISTENTE'', 42 ''PELO IML'', 43 ''PELO SVO''. Campo 100% preenchido (734/734 altas de junho). Nao usei fia.nr_obito, que esta vazio (ver A17).

DEFINICAO DE ''ENFERMARIA'' - ESCOLHA MINHA, NAO DA BASE: a tabela unidade_hospitalar nao tem flag de tipo/criticidade de unidade, entao classifiquei pelo nome e pelo perfil de mortalidade observado. Enfermaria = 1 (CLINICA MEDICA MASC), 18/22/111/112 (CLINICA MEDICA FEM), 9 (HIPODERMIA), 19/20 (COVID), 125 (SAUDE MENTAL). Ficaram DE FORA por serem areas criticas ou cirurgicas: 6 (TRAUMA), 7/8/21 (SALA AMARELA), 14 (UPG), 11/12/25 (POS OPERATORIO), 5/28 (CENTRO CIRURGICO). Essa exclusao e o que diferencia 98,64 de 162,47 por 1.000, e ela e defensavel justamente porque a mortalidade nessas areas e altissima (TRAUMA 26 obitos em 51 saidas, SALA AMARELA 1 GRAVE 9 em 19, UPG 5 em 11) - sao unidades de doente grave, nao enfermaria. Se a direcao discordar da lista, basta trocar o IN (...) do SQL.

Denominador = saidas do periodo nessas enfermarias (mesma coorte do numerador), nao admissoes, para numerador e denominador falarem do mesmo conjunto de pacientes.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '16', 210, 'DENSIDADE DE INFECÇÃO DO TRATO URINÁRIO (ITU) ASSOCIADO A CATETER VESICAL DE DEMORA', 'Total de ITUs em pacientes com CVD / total de pacientes com CVD-dia x 1.000', 'Relatório Mensal da Comissão de Infecção Hospitalar', '≤ 3.22 ITU por 1.000 pac. c/CVD- dia', 1, 3.22, NULL, 1.0, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '17', 220, 'TAXA DE MORTALIDADE INSTITUCIONAL', 'Número de óbitos do mês de competência ocorridos após 24h de internação / Número total das saídas no mesmo período *100', 'PEP ou SIH e Comissão de Revisão de Óbitos', '≤ 10%', 1, 0.1, NULL, 0.75, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT SUM(CASE WHEN f.cd_mot_cobranca_sus IN (41,42,43)
                 AND f.dt_alta - f.dt_baixa > 1 THEN 1 ELSE 0 END) AS numerador,
       COUNT(*) AS denominador
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim
   AND (SELECT MAX(fl.cd_unidade) KEEP (DENSE_RANK LAST ORDER BY fl.dt_transferencia)
          FROM infosaude.fia_leito fl
         WHERE fl.cd_hospital = f.cd_hospital
           AND fl.dt_ano_fia  = f.dt_ano_fia
           AND fl.nr_fia      = f.nr_fia)
       NOT IN (10,13,30,31,32,33,34,15,16,17,26)', 'CONFERI nr_obito ANTES DE CONFIAR, COMO PEDIDO - E ELE NAO SERVE. fia.nr_obito esta preenchido em 0 (ZERO) das 734 altas de junho/2026, em 1 de 4.330 altas de jan a jun/2026 e em 8 de 13.182 FIA desde 2025. Idem id_obito_mulher: 0 preenchidos. Um motor baseado em nr_obito devolveria mortalidade ZERO e passaria folgado na meta - seria o pior tipo de erro possivel neste trabalho.

FONTE QUE USEI: cd_mot_cobranca_sus IN (41,42,43), validado contra infosaude.motivo_cobranca_sus (41 = obito com DO do medico assistente, 42 = do IML, 43 = do SVO). Campo 100% preenchido nas 734 altas de junho (554 alta melhorado, 75 transferencia, 71 obito, 28 evasao, resto pulverizado) - e campo de faturamento SUS, logo tem disciplina de preenchimento. CORROBORACAO INDEPENDENTE: paciente.dt_obito marca obito dentro da janela de internacao em 55 casos, e os 55 estao TODOS dentro dos 71 do motivo SUS (zero contradicao nos dois sentidos). Os 16 de diferenca sao subnotificacao do dt_obito no cadastro do paciente, nao falso positivo do motivo SUS - por isso escolhi o motivo SUS como fonte primaria.

INSTITUCIONAL = permanencia superior a 24h, implementado como dt_alta - dt_baixa > 1 (as colunas tem hora, entao e 24h reais, nao virada de data). Dos 71 obitos adultos, 4 ocorreram em menos de 24h e sao corretamente excluidos.

O NUMERO E ALTO MAS NAO E BUG. Checagens clinicas que fiz: os 71 obitos tem idade media de 72,4 anos e permanencia media de 9,05 dias - perfil de obito institucional de idoso, nao de morte na porta. A mortalidade se concentra nas unidades criticas (TRAUMA 26/51, SALA AMARELA 1 GRAVE 9/19, UPG 5/11) enquanto as enfermarias clinicas ficam em 12-13% e SAUDE MENTAL, POS OPERATORIO e COVID ficam em zero. Serie mensal do bloco adulto em 2026 para descartar anomalia de junho: 10,0% / 11,4% / 14,2% / 12,8% / 11,4% / 15,3% (jan a jun) - faixa estavel, junho e o topo de um patamar consistente, nao um pico isolado. Media do semestre ~12,5%, acima da meta em todos os seis meses.

IMPACTO DA CORRECAO DO RECORTE: se eu tivesse usado a lista pediatrica do briefing (sem as unidades 32 ENFERMARIA LUA e 33 ENFERMARIA SOL), 69 internacoes pediatricas cairiam no bloco adulto, o denominador iria de 437 para 506 e a taxa cairia de 15,33% para 13,24% - dois pontos percentuais de diluicao artificial, em cima de uma meta de 10%. E a razao pratica pela qual a correcao do corte importa.

Denominador = TODAS as saidas adultas do periodo, obitos incluidos (437). Classificacao pela ultima unidade ocupada; so 1,5% das FIA cruzam blocos.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '18', 230, 'TAXA DE PROFILAXIA DE TROMBOEMBOLISMO VENOSO', 'Número de pacientes ≥18 anos com risco trombótico não baixo que receberam tromboprofilaxia/Total de pacientes com risco trombótico não baixo internados com idade ≥ 18 anos *100', 'Relatório Mensal do Núcleo de Segurança do Paciente', '1', 2, 1.0, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '19', 240, 'INCIDÊNCIA DE ÚLCERA POR PRESSÃO (UPP)', 'Número de casos novos de pacientes com UPP/ número de pessoas expostas ao risco de adquirir UPP (pacientes internados) * 100', 'PEP ou SIH. Relatório da Comissão de Curativos e Relatório do Núcleo de Segurança do Paciente', '≤ 5%', 1, 240.0, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '20', 250, 'INCIDÊNCIA DE FLEBITE DURANTE O USO DE CIP', NULL, 'Relatório do Núcleo de Segurança do Paciente', '≤ 5%', 1, 0.05, NULL, 0.75, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '21', 260, 'DENSIDADE DE INCIDÊNCIA DE QUEDAS COM OU SEM LESÃO', 'Número de quedas com ou sem danos/ número de pacientes-dia *1000', 'Relatório do Núcleo de Segurança do Paciente e Censo Hospitalar', '≤ 2.2 por 1000 pac/dia', 1, 2.2, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '22', 270, 'PROPORÇÃO DE PACIENTES IDENTIFICADOS COM PULSEIRAS', 'Número de pacientes com pulseira de identificação / número de pacientes internados (todas as salas)', 'PEP ou SIH', '1', 2, 1.0, NULL, 0.5, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '23', 280, 'TAXA DE ADESÃO AO CHECKLIST DE CIRURGIA SEGURA', 'Número de procedimentos cirúrgicos em que o checklist de Cirurgia Segura foi completamente preenchido pela equipe cirúrgica / número de procedimentos cirúrgicos realizados *100', 'PEP ou SIH e Relatório Mensal do Núcleo de Segurança do Paciente', '1', 2, 1.0, NULL, 0.5, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '24', 290, 'ÍNDICE DE QUESTIONÁRIOS PREENCHIDOS PELOS PACIENTES INTERNADOS', 'Número de questionários respondidos / Total de pacientes internados *100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥30%', 2, 0.3, NULL, 0.25, 1, '%', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'WITH pesq AS (
  SELECT DISTINCT m.fia_cd_hospital AS cd_hospital,
                  m.dt_ano_fia      AS dt_ano_fia,
                  m.nr_fia          AS nr_fia
    FROM infosaude.edoc_movimento m
    JOIN infosaude.edoc_modelo md ON md.cd_modelo = m.cd_modelo
   WHERE m.cd_hospital = :hospital
     AND m.in_status = ''D''
     AND m.in_ativo = ''S''
     AND UPPER(md.ds_modelo) LIKE ''%SATISFA%''
     AND m.nr_fia IS NOT NULL
),
alta AS (
  SELECT f.cd_hospital, f.dt_ano_fia, f.nr_fia
    FROM infosaude.fia f
   WHERE f.cd_hospital = :hospital
     AND f.dt_alta >= :ini
     AND f.dt_alta <  :fim
)
SELECT COUNT(p.nr_fia) AS numerador, COUNT(*) AS denominador
  FROM alta a
  LEFT JOIN pesq p
    ON p.cd_hospital = a.cd_hospital
   AND p.dt_ano_fia  = a.dt_ano_fia
   AND p.nr_fia      = a.nr_fia', 'MARQUEI NaoValidado POR DEFINICAO, NAO POR EXECUCAO: o numero rodou, mas o questionario proprio de internacao ESTA FORA DE USO. O modelo 10208 ''Pesquisa de satisfacao do usuario - Internacao'' teve jan/2026 576 docs, fev 376, mar 242, abr 22 e ZERO de maio em diante (jun e jul = 0). As 114 pesquisas de junho ligadas a FIA sao todas do modelo 10209 (Ambulatorio) aplicado a pacientes internados -- ou seja, o indicador contratual de internacao esta sendo medido com o formulario do ambulatorio. Decidir com a gestao se isso vale como ''questionario do paciente internado''. | DEDUP: 114 pesquisas -> 110 FIA distintas; o motor conta INTERNACOES com pesquisa (nao pesquisas). | ANCORAGEM: motor conta pela ALTA (fia.dt_alta no periodo, pesquisa de qualquer data) = 118/734 = 16,08%; a variante estrita (pesquisa incluida dentro do mes) da 89/734 = 12,13%. A diferenca de 29 casos e real: a pesquisa de quem teve alta no fim do mes so e preenchida no mes seguinte -- por isso preferi ancorar na alta. Consequencia: o valor de um mes fechado pode subir depois. | DENOMINADOR: fia.dt_alta no periodo = 734 (baixas no mesmo mes = 739). Nao ha recorte de bloco (adulto/pediatria/maternidade) -- se o contrato quiser so adulto, e preciso entrar por infosaude.fia_leito.cd_unidade. | COMPARACAO: mar/2026, com o formulario 10208 ainda em uso, o mesmo motor da 195/769 = 25,36% -- mesmo no melhor cenario ficou abaixo da meta de 30%.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '25', 300, 'PERCENTUAL DE USUÁRIOS INTERNADOS SATISFEITOS / MUITO SATISFEITOS', 'Número de conceitos satisfeito e muito satisfeito / Total de respostas efetivas *100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥85%', 2, 0.85, NULL, 0.25, 1, '%', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'WITH pesq AS (
  SELECT m.cd_hospital, m.ano_movimento, m.id_movimento
    FROM infosaude.edoc_movimento m
    JOIN infosaude.edoc_modelo md ON md.cd_modelo = m.cd_modelo
   WHERE m.cd_hospital = :hospital
     AND m.in_status = ''D''
     AND m.in_ativo = ''S''
     AND UPPER(md.ds_modelo) LIKE ''%SATISFA%''
     AND m.nr_fia IS NOT NULL
     AND m.dt_inclusao >= :ini
     AND m.dt_inclusao <  :fim
)
SELECT SUM(CASE WHEN REPLACE(mi.ds_resposta,'' '','''') IN (''9-10'',''7-8'') THEN 1 ELSE 0 END) AS numerador,
       COUNT(*) AS denominador
  FROM pesq p
  JOIN infosaude.edoc_movimento_item mi
    ON mi.cd_hospital   = p.cd_hospital
   AND mi.ano_movimento = p.ano_movimento
   AND mi.id_movimento  = p.id_movimento
 WHERE mi.cd_item = 12648
   AND mi.ds_resposta IS NOT NULL', 'MOTOR IGUAL AO A9, so troca BAA por FIA -- mesma pergunta cd_item = 12648 e mesmo mapeamento de faixas (7-10 = satisfeito/muito satisfeito). NaoValidado por tres motivos: (1) as 114 respostas sao do formulario do AMBULATORIO (10209) aplicado a internados -- o formulario proprio de internacao (10208, que tem blocos especificos: copeira, fisioterapeuta, visita medica, etc.) esta zerado desde maio/2026; (2) amostra minuscula e nao aleatoria: 114 respostas para 734 altas (15,5%), colhidas pela propria equipe; (3) 100,00% sem uma unica resposta abaixo de 7 e resultado clinicamente implausivel como medida de satisfacao real -- e assinatura de viesde coleta, nao de qualidade perfeita. Em mar/2026, com o formulario 10208 ativo, o mesmo motor tambem deu 242/242 = 100% -- o padrao se repete, o que reforca a suspeita de viesestrutural na aplicacao do questionario. Antes de publicar como indicador contratual, recomendo revisar o processo de coleta (idealmente coleta independente da equipe assistencial, por totem/QR ou Ouvidoria).', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 1, '26', 310, 'NET PROMOTER SCORE (NPS) DA INTERNAÇÃO', NULL, 'PEP ou Relatório de Pesquisa com o Usuário', '≥50%', 2, 0.5, NULL, 0.25, 1, '%', NULL, 3, NULL, NULL, 'Mesma causa do A10: nao existe pergunta de recomendacao 0-10 -- e, no caso da internacao, o problema e ainda maior porque o formulario proprio de internacao (modelo 10208) esta sem uso desde maio/2026 (jun e jul = 0 documentos). O formulario 10208 tem duas perguntas de fidelidade, ambas binarias: cd_item 12387 ''Internacao - Voce indicaria este servico a um parente ou a um amigo?'' e 12388 ''Internacao - Voce voltaria a utilizar este servico?'' -- Sim/Nao, nao 0-10. As unicas pesquisas de internacao de jun/2026 (114, todas do formulario do ambulatorio) trazem o 12361 (Sim/Nao): 113 Sim e 1 Nao = 99,12%. Proxy pelas faixas do cd_item 12648 (promotor 9-10 = 109, neutro 7-8 = 5, detrator <= 6 = 0) daria 95,61 -- de novo, NAO e NPS (pergunta de avaliacao, escala 1-10 em faixas) e ainda com n = 114 sobre 734 altas. Recomendacao: incluir a pergunta 0-10 de recomendacao no eDoc e reativar o formulario de internacao; sem isso o indicador nao tem motor honesto.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '1', 10, 'TEMPO MÉDIO PARA ACOLHIMENTO/CADASTRO (exceto vermelho)', 'Σ do tempo (em min) entre a chegada do usuário (totem de chegada) e o registro do boletim de atendimento no Sistema de Informação hospitalar / número de pacientes registrados.', 'PEP* ou SIH**', '≤ 5 MIN', 1, 5.0, NULL, 1.0, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_chegada - a.dt_atendimento) * 1440), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.baa b
  JOIN infosaude.acolhimento a
    ON a.cd_acolhimento = b.cd_acolhimento
   AND a.cd_hospital    = b.cd_hospital
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND a.dt_atendimento IS NOT NULL
   AND b.dt_chegada >= a.dt_atendimento', 'RECORTE CONFIRMADO: BAA pediatrico jun/2026 = 4.443 atendimentos, exatamente como o briefing (38 PED CLASSIF RISCO 14 | 46 PED CONSULTORIO 3.503 | 50 PED MEDICACAO 810 | 60 PED REAVALIACAO 8 | 71 PED TRAUMA 108). Conferencia por idade: 4.365/4.443 (98,2%) com menos de 18 anos, idade media 5,9 anos - o corte por setor esta correto; 78 adultos (1,8%) passaram por setor pediatrico (contaminacao desprezivel). CARIMBO: o campo acolhimento.dt_hr_senha, que o briefing indicava como a hora do totem, esta 100% VAZIO (0 de 9.176 linhas de junho) e acolhimento.dt_senha e data sem hora (00:00:00) - baa.dt_senha tambem so existe em 2 dos 4.443 registros. Logo NAO da para medir a espera na fila do totem. O que este motor mede e acolhimento.dt_atendimento -> baa.dt_chegada, ou seja, do carimbo do acolhimento ate a abertura do boletim (cadastro). E um PISO do indicador: nao inclui o tempo de fila antes do acolhimento. Cobertura 3.970/4.443 = 89,4% (os 11% sem cd_acolhimento ficam de fora). Observacao adicional: baa.dt_chegada e baa.dt_atendimento sao o MESMO instante nos 4.443 registros (delta zero em 100%), entao os dois nomes sao intercambiaveis. Meta <= 5 min: media 8,93 nao atinge, mediana 4,63 atinge.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '2', 20, 'TEMPO MÉDIO PARA CLASSIFICAÇÃO DE RISCO (exceto vermelho)', 'Σ do tempo (em min) entre o cadastro e o registro da classificação de risco pelo enfermeiro no sistema de informação hospitalar / Número de pacientes classificados.', 'PEP ou SIH', '≤10 min', 1, 10.0, NULL, 1.0, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_classifica_atual - b.dt_chegada) * 1440), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_classifica_atual >= b.dt_chegada', 'Mede do cadastro (baa.dt_chegada) ate a classificacao de risco (baa.dt_classifica_atual). Cobertura: 4.277/4.443 (96,3%) tem dt_classifica_atual; o motor descarta 29 registros com delta negativo (classificacao carimbada antes da chegada - erro de digitacao), ficando 4.248 (95,6%). ATENCAO A DEFINICAO: se o contrato quiser contar desde a CHEGADA DO PACIENTE e nao desde o cadastro, o carimbo mais antigo disponivel e acolhimento.dt_atendimento, e ai a media sobe para 16,42 min (mediana 11,6 min, n=3.970) - ou seja, ACIMA da meta de 10 min. Com a definicao entregue (a partir do cadastro) o indicador cumpre a meta com folga. Recomendo fixar a definicao com o hospital antes de publicar, porque as duas leituras dao veredictos opostos.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '3', 30, 'TEMPO MÉDIO DE ESPERA PARA ATENDIMENTO MÉDICO, SEGUNDO CLASSIFICAÇÃO DE RISCO', NULL, NULL, NULL, NULL, NULL, NULL, 2.5, 5, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '3.1', 40, 'Vermelho', 'Σ do tempo transcorrido entre a classificação de risco até atendimento médico registrado no sistema de informação hospitalar / número de pacientes atendidos', 'PEP ou SIH', '0 min', 3, 1.0, NULL, 0.5, 1, 'min', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND b.cd_classificacao_risco = 7
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'MOTOR OK, NUMERO NAO CONFIAVEL. Duas razoes. (1) Amostra n=2: em junho inteiro houve so 2 classificacoes VERMELHO na pediatria (10 no hospital todo). Media de 2 casos nao e indicador. (2) O valor de 135,7 min contradiz a pratica clinica - paciente vermelho e atendido imediatamente; o que isso mostra e que baa.dt_atend_pac e carimbado quando o medico REGISTRA o atendimento no sistema, nao quando ele encosta no paciente, e no caso grave o registro sai depois. Nao use este numero para julgar o servico. Mapa de cores confirmado na tabela classificacao_risco: 7=VERMELHO(alvo 15min) 8=AMARELO(30) 9=VERDE(60) 10=AZUL(1440); os codigos 1-6 (EMERGENCIA/MUITO URGENTE/URGENTE/POUCO URGENTE/NAO URGENTE/RETORNO) estao inativos e sem uso em junho.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '3.2', 50, 'Laranja', NULL, NULL, '≤10 min', 1, 10.0, NULL, 0.5, 1, 'min', NULL, 3, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco c
    ON c.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND UPPER(c.ds_classificacao_risco) LIKE ''LARANJA%''
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'SEM MOTOR: a implantacao do Manchester no Salux do HMCML usa 4 cores (vermelho/amarelo/verde/azul) e nao tem LARANJA cadastrada nem usada. Nao ha como apurar este indicador sem que o hospital primeiro configure a cor e passe a classificar com ela. O unico candidato historico seria o codigo 2 = MUITO URGENTE (alvo 10 min, que e o alvo classico do laranja), mas ele esta INATIVO e teve ZERO uso em junho/2026 - reaproveita-lo seria inventar dado. O SQL entregue ja fica pronto para funcionar sozinho no dia em que a cor for cadastrada (filtra pela descricao, nao por codigo); hoje ele devolve denominador=0.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '3.3', 60, 'Amarelo', NULL, NULL, '≤60 min', 1, 60.0, NULL, 0.5, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND b.cd_classificacao_risco = 8
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'Cor com amostra robusta. COBERTURA E O PONTO FRACO: dos 1.127 amarelos pediatricos do mes, so 697 (61,8%) tem baa.dt_atend_pac preenchido - o motor mede a espera apenas nesse subconjunto (696 apos excluir 1 delta negativo). Nao ha como saber se os 38% sem carimbo esperaram mais ou menos. Na pediatria a cobertura de dt_atend_pac e 2.588/4.443 = 58,2% (melhor que os 48,7% do hospital inteiro, mas ainda longe de completa). Alvo Manchester do amarelo = 30 min; media 35,3 fica pouco acima, mediana 22,4 dentro.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '3.4', 70, 'Verde', NULL, NULL, '≤120 min', 1, 120.0, NULL, 0.5, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND b.cd_classificacao_risco = 9
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'Cor de maior volume e a mais confiavel do conjunto. Cobertura: 1.745 de 3.105 verdes pediatricos (56,2%) tem dt_atend_pac; o motor usa 1.744 (exclui 1 delta negativo). O maximo de 2.140 min (35h) e boletim carimbado tardiamente e puxa a media; a mediana de 44,7 min descreve melhor a rotina. Alvo Manchester do verde = 60 min: media 62,4 fica no limite, mediana bem dentro.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '3.5', 80, 'Azul', NULL, NULL, '≤240 min', 1, 240.0, NULL, 0.5, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND b.cd_classificacao_risco = 10
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'AMOSTRA MINUSCULA: so 14 classificacoes azuis na pediatria em junho inteiro e apenas 5 delas com dt_atend_pac. O motor esta correto e o valor (53,5 min, alvo do azul = 1.440 min) e clinicamente coerente, mas com n=5 o indicador oscila violentamente de mes para mes - qualquer leitura mensal isolada e ruido. Sugiro reportar acumulado trimestral para esta cor. O azul praticamente nao e usado na pediatria (0,3% das classificacoes).', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '4', 90, 'ÍNDICE DE QUESTIONÁRIOS PREENCHIDOS PELOS PACIENTES DA U&E DO CENTRO PEDIÁTRICO', 'Número de questionários respondidos / Total de pacientes atendidos *100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥10%', 2, 0.1, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '5', 100, 'PERCENTUAL DE USUÁRIOS DA U&E DO CENTRO PEDIÁTRICO SATISFEITOS / MUITO SATISFEITOS', 'Número de conceitos satisfeito e muito satisfeito / Total de respostas efetivas *100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥85%', 2, 0.85, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '6', 110, 'NET PROMOTER SCORE (NPS) DA U&E DO CENTRO PEDIÁTRICO', NULL, NULL, '≥50%', 2, 0.5, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '7', 120, 'TEMPO MÉDIO DE PERMANÊNCIA NA EMERGÊNCIA', 'Σ do tempo transcorrido entre a classificação de risco até atendimento médico no sistema de informação hospitalar / número de pacientes atendidos pelo médico', 'PEP ou SIH', '≤ 8h', 1, 8.0, NULL, 0.5, 1, 'h', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_saida - b.dt_chegada) * 24), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (38,46,50,60,71)
   AND b.dt_saida IS NOT NULL
   AND b.dt_saida > b.dt_chegada', 'NUMERO DENTRO DA META (<=8h) MAS DISTORCIDO POR CAUDA. A mediana e 53 minutos e o p95 e 4,79h - a rotina da emergencia pediatrica e curta. A media de 4,29h vem de 44 boletins (1,1%) com mais de 24h e 21 (0,5%) com mais de 7 DIAS, que sao boletins nunca encerrados, nao permanencia real. Cortes medidos: <=24h -> 1,39h | <=72h -> 1,54h | <=168h -> 1,83h | sem corte -> 4,29h. Entreguei SEM corte arbitrario (so a sanidade dt_saida > dt_chegada) para nao embutir regra de negocio nao pactuada; se o hospital aceitar descartar boletim nao encerrado, basta acrescentar AND b.dt_saida <= b.dt_chegada + 7 e o indicador cai para 1,83h. Cobertura: 3.978/4.443 = 89,5% (10,5% dos boletins nao tem dt_saida e ficam fora do denominador).', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '8', 130, 'TAXA DE OCUPAÇÃO HOSPITALAR', 'Número de paciente-dia / Número de leito-dia operacionais*100', 'PEP ou SIH', '≥ 85%', 2, 0.85, NULL, 1.0, 1, '%', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'WITH pacientes_dia AS (
  SELECT SUM(LEAST(NVL(fl.dt_saida_leito, :fim), :fim)
             - GREATEST(fl.dt_transferencia, :ini)) AS dias
    FROM infosaude.fia_leito fl
   WHERE fl.cd_hospital = :hospital
     AND fl.cd_unidade IN (10,13,30,31,32,33,34)
     AND fl.dt_transferencia < :fim
     AND NVL(fl.dt_saida_leito, :fim) > :ini
),
leitos_dia AS (
  SELECT COUNT(*) * (:fim - :ini) AS dias
    FROM infosaude.leito l
   WHERE l.cd_hospital = :hospital
     AND l.cd_unidade IN (10,13,30,31,32,33,34)
)
SELECT ROUND(p.dias, 2) AS numerador,
       c.dias          AS denominador
  FROM pacientes_dia p CROSS JOIN leitos_dia c', 'NUMERADOR CONFIAVEL, DENOMINADOR NAO. Os 297,39 pacientes-dia batem com a checagem independente (89 altas x 3,2 dias de permanencia media) e o metodo passou no teste de sanidade. O problema e a capacidade: infosaude.leito e um retrato ATUAL do cadastro, nao a capacidade operante do mes, e mistura leito de internacao com posicao de tratamento do PS - a unidade 10 HIPODERMIA PEDIATRIC (7 dos 31 ''leitos'') e area de medicacao/hidratacao, com permanencia media de 1,26 dia, e a unidade 13 PEDIATRIA tem 6 leitos cadastrados e UMA unica internacao no mes (esta desativada na pratica), sozinha derrubando o indicador em ~19%. PROVA DE QUE O DENOMINADOR ESTA INFLADO: aplicando o MESMO motor ao hospital inteiro da 4.240,8 pacientes-dia / (499 leitos x 30) = 28,3% - um hospital que faz 17,4 mil atendimentos de urgencia por mes nao opera a 28% de ocupacao; o cadastro de 499 leitos (73 deles em unidades marcadas como INATIVAS) superestima a capacidade real, que pelo censo medio (141 pacientes/dia) deve girar em torno de 165 leitos. Enquanto o hospital nao informar a capacidade operante por unidade, este indicador NAO deve ser publicado contra a meta de 85%: ele vai reprovar sempre por erro de denominador, nao por vazio de leito. Se a capacidade pediatrica real for ~12 leitos, a ocupacao seria ~83%. Recorte usado: ver ressalva de P9.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '9', 140, 'MÉDIA DE PERMANÊNCIA NA INTERNAÇÃO HOSPITALAR', 'Número de paciente-dia / Número de saídas /mês *100', 'PEP ou SIH', '≤ 4,7 dias', 1, 4.7, NULL, 1.0, 1, 'dias', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG(f.dt_alta - f.dt_baixa), 2) AS valor,
       COUNT(*) AS denominador
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim
   AND f.dt_alta >= f.dt_baixa
   AND EXISTS (SELECT 1
                 FROM infosaude.fia_leito fl
                WHERE fl.cd_hospital = f.cd_hospital
                  AND fl.dt_ano_fia  = f.dt_ano_fia
                  AND fl.nr_fia      = f.nr_fia
                  AND fl.cd_unidade IN (10,13,30,31,32,33,34))', 'RECORTE DE INTERNACAO - DIVERGENCIA COM O BRIEFING, LEIA ANTES DE USAR. A hipotese do briefing (cd_unidade 10,13,30,31,34) captura 82 altas em junho; a lista que estou entregando (10,13,30,31,32,33,34) captura 89. Duas correcoes medidas: (a) a unidade 34 CENTRO PEDIATRICO nao tem NENHUM leito cadastrado e ZERO movimento em todo o 2026 - existe so no cadastro (mantida na lista por ser inofensiva); (b) as unidades 32 ENFERMARIA LUA e 33 ENFERMARIA SOL sao pediatricas de fato e o briefing as jogava no bloco adulto (''o resto'') - em 2026 elas tiveram 191 e 175 internacoes com 190/191 e 174/175 pacientes menores de 18 anos e idade media 6,0 e 1,9 anos; juntas concentram 62% dos pacientes-dia pediatricos de junho. ALERTA DE CONSISTENCIA PARA O AGENTE ADULTO: se o motor adulto usar ''todas as unidades exceto (10,13,30,31,34)'', as unidades 32 e 33 serao contadas NOS DOIS BLOCOS. Precisa reconciliar. Validacao por idade do recorte entregue: 88 das 89 altas de junho tem menos de 18 anos, idade media 4,1 anos. Sobre o motor em si: FIA.nr_dias_internacao esta 100% NULO (89/89), por isso a permanencia e calculada por dt_alta - dt_baixa. Volume: 89 altas/mes e amostra pequena porem suficiente para uma media; 3,20 dias e clinicamente coerente e cumpre a meta de 4,7.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '10', 150, 'PROPORÇÃO DE REINTERNAÇÕES EM ATÉ 30 DIAS DA ALTA HOSPITALAR****', NULL, 'PEP ou SIH', '≤ 20%', 1, 0.2, NULL, 1.0, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT SUM(reinternacao) AS numerador,
       COUNT(*)          AS denominador
  FROM (
    SELECT CASE WHEN (SELECT COUNT(*)
                        FROM infosaude.fia p
                       WHERE p.cd_hospital = f.cd_hospital
                         AND NVL(p.cd_paciente_unificado, p.cd_paciente)
                             = NVL(f.cd_paciente_unificado, f.cd_paciente)
                         AND (p.dt_ano_fia <> f.dt_ano_fia OR p.nr_fia <> f.nr_fia)
                         AND p.dt_alta IS NOT NULL
                         AND p.dt_alta <= f.dt_baixa
                         AND p.dt_alta >= f.dt_baixa - 30) > 0
                THEN 1 ELSE 0 END AS reinternacao
      FROM infosaude.fia f
     WHERE f.cd_hospital = :hospital
       AND f.dt_baixa >= :ini
       AND f.dt_baixa <  :fim
       AND EXISTS (SELECT 1
                     FROM infosaude.fia_leito fl
                    WHERE fl.cd_hospital = f.cd_hospital
                      AND fl.dt_ano_fia  = f.dt_ano_fia
                      AND fl.nr_fia      = f.nr_fia
                      AND fl.cd_unidade IN (10,13,30,31,32,33,34))
  )', 'Definicao adotada: denominador = internacoes pediatricas com BAIXA no periodo (88); numerador = aquelas cuja baixa ocorreu ate 30 dias apos a alta de uma internacao anterior do mesmo paciente no mesmo hospital (olhando para tras, sem depender de dado futuro). Paciente identificado por NVL(cd_paciente_unificado, cd_paciente). VOLUME BAIXO: com 88 internacoes/mes cada caso vale 1,14 ponto percentual - a serie mensal vai balancar muito e um mes com 18 reinternacoes ja estoura a meta de 20%. Recomendo acompanhar em janela trimestral. Se o contrato quiser a leitura ''alta seguida de reinternacao em 30 dias'' (olhando para frente a partir da alta), o motor muda e o mes so fecha 30 dias depois - avisar antes de trocar. Recorte de unidades: ver ressalva de P9.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '11', 160, 'TAXA DE PARADA CARDIORRESPIRATÓRIA NA ENFERMARIA *****', 'Número de óbitos institucionais ocorridos na enfermaria / total de internações na enfermaria no período', 'PEP ou SIH', '≤1 óbito por 1.000 internações', 1, 1.0, NULL, 1.0, 2, '/1000 pacientes-dia', 1000.0, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'WITH obitos AS (
  SELECT COUNT(*) AS qt
    FROM infosaude.fia f
   WHERE f.cd_hospital = :hospital
     AND f.dt_alta >= :ini
     AND f.dt_alta <  :fim
     AND f.cd_mot_cobranca_sus IN (41,42,43)
     AND EXISTS (SELECT 1
                   FROM infosaude.fia_leito fl
                  WHERE fl.cd_hospital = f.cd_hospital
                    AND fl.dt_ano_fia  = f.dt_ano_fia
                    AND fl.nr_fia      = f.nr_fia
                    AND fl.cd_unidade IN (10,13,30,31,32,33,34))
),
pacientes_dia AS (
  SELECT SUM(LEAST(NVL(fl.dt_saida_leito, :fim), :fim)
             - GREATEST(fl.dt_transferencia, :ini)) AS dias
    FROM infosaude.fia_leito fl
   WHERE fl.cd_hospital = :hospital
     AND fl.cd_unidade IN (10,13,30,31,32,33,34)
     AND fl.dt_transferencia < :fim
     AND NVL(fl.dt_saida_leito, :fim) > :ini
)
SELECT o.qt              AS numerador,
       ROUND(p.dias, 2)  AS denominador
  FROM obitos o CROSS JOIN pacientes_dia p', 'PROXY, NAO O EVENTO. Parada cardiorrespiratoria NAO e registrada em lugar nenhum da base (nao ha campo nem tabela de PCR); conforme a memoria de calculo recebida, o numerador usa OBITO. Onde o obito mora: descoberto que FIA.nr_obito esta 100% NULO e nao serve; o registro real e FIA.cd_mot_cobranca_sus (motivo de saida SUS) nos codigos 41 (DO pelo medico assistente), 42 (IML) e 43 (SVO) - o campo esta preenchido em 100% das saidas pediatricas e capta 71 obitos no hospital inteiro em junho, entao e confiavel. DENOMINADOR: adotei pacientes-dia (densidade de incidencia, padrao para evento em enfermaria); se a intencao contratual for ''1 por 1.000 saidas'', trocar o CTE pacientes_dia por COUNT das saidas - o resultado pediatrico e 0 nos dois casos. SINAL NULO NO BLOCO PEDIATRICO: zero obitos em 6 meses e 388 saidas. Nao e falha de registro - e o perfil do servico: 52 das 388 saidas (13,4%) sao TRANSFERENCIA PARA OUTRO ESTABELECIMENTO (codigo 31), ou seja, a crianca grave sai daqui viva e o desfecho acontece fora. O indicador vai ler 0 quase sempre e nao distingue servico bom de servico sem casos graves.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '12', 170, 'TAXA DE MORTALIDADE INSTITUCIONAL', 'Número de óbitos do mês de competência ocorridos após 24h de internação / Número total das saídas no mesmo período *100', 'PEP ou SIH e Comissão de Revisão de Óbitos', '≤ 3,5%', 1, 0.035, NULL, 0.5, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT SUM(CASE WHEN f.cd_mot_cobranca_sus IN (41,42,43)
                 AND f.dt_alta - f.dt_baixa >= 1
                THEN 1 ELSE 0 END) AS numerador,
       COUNT(*)                    AS denominador
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim
   AND EXISTS (SELECT 1
                 FROM infosaude.fia_leito fl
                WHERE fl.cd_hospital = f.cd_hospital
                  AND fl.dt_ano_fia  = f.dt_ano_fia
                  AND fl.nr_fia      = f.nr_fia
                  AND fl.cd_unidade IN (10,13,30,31,32,33,34))', 'Definicao adotada: mortalidade INSTITUCIONAL = obito apos 24h de internacao (por isso o AND f.dt_alta - f.dt_baixa >= 1); removendo essa linha vira mortalidade geral - na pediatria as duas dao 0, entao a escolha nao muda o numero, mas precisa ser a MESMA usada no bloco adulto para os dois serem comparaveis. Fonte do obito: FIA.cd_mot_cobranca_sus IN (41,42,43), preenchido em 100% das saidas pediatricas (FIA.nr_obito esta 100% nulo e nao serve). VOLUME PEQUENO DEMAIS PARA O INDICADOR TER SIGNIFICADO: com 89 saidas/mes, 1 obito ja seria 1,1%, 3 obitos 3,4% e 4 obitos estouram a meta de 3,5% - o indicador salta de 0% a reprovado com quatro casos, e nao ha nenhum obito pediatrico ha 6 meses para calibrar. Sugiro publicar acumulado no ano e sempre com o numerador absoluto ao lado do percentual. UMA INCONSISTENCIA ENCONTRADA (nao afeta o resultado): entre os 388 internados pediatricos de 2026 ha 1 paciente com PACIENTE.DT_OBITO preenchida (04/03/2026), mas a data e ANTERIOR a propria baixa (05/03) e a alta foi ''alta melhorado'' - e erro de cadastro no prontuario do paciente, nao obito nao registrado.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '13', 180, 'INCIDÊNCIA DE ÚLCERA POR PRESSÃO (UPP)', 'Número de casos novos de pacientes com UPP/ número de pessoas expostas ao risco de adquirir UPP (pacientes internados) * 100', 'PEP ou SIH. Relatório da Comissão de Curativos e Relatório do Núcleo de Segurança do Paciente', '≤ 5%', 1, 0.05, NULL, 0.5, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '14', 190, 'INCIDÊNCIA DE FLEBITE DURANTE O USO DE CIP', NULL, 'Relatório do Núcleo de Segurança do Paciente', '≤ 5%', 1, 0.05, NULL, 0.5, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '15', 200, 'DENSIDADE DE INCIDÊNCIA DE QUEDAS COM OU SEM LESÃO', 'Número de quedas com ou sem danos/ número de pacientes-dia *1000', 'Relatório do Núcleo de Segurança do Paciente e Censo Hospitalar', '≤ 2.2 por 1000 pac/dia', 1, 2.2, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '16', 210, 'PROPORÇÃO DE PACIENTES IDENTIFICADOS COM PULSEIRAS', 'Número de pacientes com pulseira de identificação / número de pacientes internados (todas as salas)', 'PEP ou SIH', '1', 2, 1.0, NULL, 1.0, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '17', 220, 'ÍNDICE DE QUESTIONÁRIOS PREENCHIDOS PELOS PACIENTES INTERNADOS NO CENTRO PEDIÁTRICO', 'Número de questionários respondidos / Total de pacientes internados *100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥30%', 2, 0.3, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '18', 230, 'PERCENTUAL DE USUÁRIOS INTERNADOS NO CENTRO PEDIÁTRICO SATISFEITOS / MUITO SATISFEITOS', 'Número de conceitos satisfeito e muito satisfeito / Total de respostas efetivas *100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥85%', 2, 0.85, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 2, '19', 240, 'NET PROMOTER SCORE (NPS) DA INTERNAÇÃO DO CENTRO PEDIÁTRICO', NULL, 'PEP ou Relatório de Pesquisa com o Usuário', '≥50%', 2, 0.5, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '1', 10, 'TEMPO MÉDIO PARA ACOLHIMENTO/CADASTRO (exceto vermelho)', 'Σ do tempo (em min) entre a chegada do usuário (totem de chegada) e o registro do boletim de atendimento no Sistema de Informação hospitalar / número de pacientes registrados.', 'PEP ou SIH', '≤ 5 MIN', 1, 5.0, NULL, 1.0, 1, 'min', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_chegada - a.dt_atendimento) * 1440), 2) AS valor,
       COUNT(*)                                                AS denominador
  FROM infosaude.baa b
  JOIN infosaude.acolhimento a
    ON a.cd_acolhimento = b.cd_acolhimento
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (39, 48, 51, 67)
   AND a.dt_atendimento IS NOT NULL
   AND b.dt_chegada >= a.dt_atendimento
   AND (b.dt_chegada - a.dt_atendimento) * 1440 <= 1440', 'NAO EXISTE nesta base o carimbo da chegada fisica do paciente. Medido: acolhimento.dt_hr_senha vem 0% preenchido no recorte (0 de 727 joins) e acolhimento.dt_senha (64 de 727 = 8,8%) e data truncada — o intervalo dt_senha->acolhimento da media de 777 min (12,9 h), min 347 e max 1346, ou seja e lixo. Tambem medido: baa.dt_chegada = baa.dt_atendimento em 100% dos 928 BAA (diferenca zero, max zero), logo dt_chegada NAO e a chegada e sim a abertura do boletim. O unico par mensuravel e acolhimento.dt_atendimento -> baa.dt_chegada, que mede ''do acolhimento ate o cadastro do boletim''. Cobertura: 727 de 928 BAA obstetricos (78,3%); os 201 restantes nao tem cd_acolhimento. Filtros de sanidade: descartadas diferencas negativas (nenhuma em jun/2026, neg=0) e acima de 24 h (nenhuma em jun/2026) — nao alteram o resultado do mes, so protegem o indicador de lixo futuro. NaoValidado porque nao ha como confirmar que este e o par de carimbos que o contrato quer: se a meta de <=5 min se refere a chegada->acolhimento, o dado simplesmente nao existe e o indicador e SemMotor.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '2', 20, 'TEMPO MÉDIO PARA CLASSIFICAÇÃO DE RISCO (exceto vermelho)', 'Σ do tempo (em min) entre o cadastro e o registro da classificação de risco pelo enfermeiro no sistema de informação hospitalar / Número de pacientes classificados.', 'PEP ou SIH', '≤ 10 min', 1, 10.0, NULL, 1.0, 1, 'min', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_classifica_atual - a.dt_atendimento) * 1440), 2) AS valor,
       COUNT(*)                                                         AS denominador
  FROM infosaude.baa b
  JOIN infosaude.acolhimento a
    ON a.cd_acolhimento = b.cd_acolhimento
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (39, 48, 51, 67)
   AND b.dt_classifica_atual IS NOT NULL
   AND a.dt_atendimento IS NOT NULL
   AND b.dt_classifica_atual >= a.dt_atendimento
   AND (b.dt_classifica_atual - a.dt_atendimento) * 1440 <= 1440', 'O numero bate a meta (<=10 min) mas por construcao do sistema, nao por desempenho: 665 dos 727 (91,5%) tem diferenca EXATAMENTE zero entre acolhimento.dt_atendimento e baa.dt_classifica_atual — os dois carimbos sao gravados na mesma transacao da tela de acolhimento/classificacao. So 62 casos (8,5%) tem diferenca > 0. Trate como indicador de registro, nao de espera real. Atencao ao sinal, porque o fluxo do HMCML e invertido em relacao ao esperado: baa.dt_classifica_atual e ANTERIOR a baa.dt_chegada em 665 de 731 casos (media -28,5 min, mediana -17,4) — a classificacao acontece ANTES da abertura do boletim, entao usar dt_chegada como T0 produziria tempo negativo. Alternativas descartadas por medicao: triagem.dt_atendimento vem 28,2 min DEPOIS do acolhimento (e ~igual a abertura do boletim, nao e a hora da triagem) e os campos Manchester/EMERGES da TRIAGEM (dt_inicio_clas_emerges, dt_fim_clas_emerges, nr_tempoespera_emerges) vem 0% preenchidos nas 790 triagens do recorte. Cobertura: 727 de 928 (78,3%).', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '3', 30, 'TEMPO MÉDIO DE ESPERA PARA ATENDIMENTO MÉDICO, SEGUNDO CLASSIFICAÇÃO DE RISCO', NULL, NULL, NULL, NULL, NULL, NULL, 2.5, 5, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '3.1', 40, 'Vermelho', 'Σ do tempo transcorrido entre a classificação de risco até atendimento médico registrado no sistema de informação hospitalar / número de pacientes atendidos', 'PEP ou SIH', '0 min', 3, 1.0, NULL, 0.5, 1, 'min', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*)                                                       AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr
    ON cr.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (39, 48, 51, 67)
   AND UPPER(TRIM(cr.ds_classificacao_risco)) = ''VERMELHO''
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'Motor roda e esta correto, mas sem amostra em jun/2026 (n=0) — nao ha o que validar. A cor VERMELHO existe e esta ativa no cadastro (cd=7, in_ativo=''S'', qt_tempo=15 min), so nao foi usada no recorte obstetrico do mes. RESSALVA COMUM A TODA A FAMILIA M3: apenas 81 dos 928 BAA obstetricos (8,7%) tem classificacao de risco preenchida. Verifiquei os tres candidatos e nenhum melhora isso — COALESCE(baa.cd_classificacao_risco, triagem.cd_classificacao_risco, acolhimento.cd_classificacao_risco) continua dando 81. Ou seja, 91,3% do bloco materno-infantil de urgencia nao entra em nenhuma das cinco linhas. O denominador de espera usa dt_classifica_atual -> dt_atend_pac, e dt_atend_pac so existe em 464 de 928 (50,0%) — sem sinal negativo (neg=0), o que da consistencia ao par. Se a base voltar a escala Manchester (cd 1..6: EMERGENCIA/MUITO URGENTE/URGENTE/POUCO URGENTE/NAO URGENTE, hoje toda com in_ativo=''N''), o filtro por descricao ''VERMELHO'' para de casar — nesse cenario trocar por cd_classificacao_risco IN (7,1).', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '3.2', 50, 'Laranja', NULL, NULL, '≤10 min', 1, 10.0, NULL, 0.5, 1, 'min', NULL, 3, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*)                                                       AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr
    ON cr.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (39, 48, 51, 67)
   AND UPPER(TRIM(cr.ds_classificacao_risco)) = ''LARANJA''
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'SemMotor por ausencia da categoria, nao por ausencia de dado. Listei infosaude.classificacao_risco inteira: a escala ATIVA do HMCML tem 4 cores + 1 tecnica — cd=7 VERMELHO (15 min), cd=8 AMARELO (30 min), cd=9 VERDE (60 min), cd=10 AZUL (1440 min), cd=11 SALUX; e a escala Manchester de 6 niveis (cd=1 EMERGENCIA, 2 MUITO URGENTE, 3 URGENTE, 4 POUCO URGENTE, 5 NAO URGENTE, 6 RETORNO) esta toda com in_ativo=''N'' e nao foi usada em junho. NAO EXISTE LARANJA em nenhuma das duas. Se o contrato exige as 5 cores do Manchester, o equivalente de LARANJA e ''MUITO URGENTE'' (cd=2), que esta inativo e com zero uso — o SQL entregue ficaria pronto trocando o filtro por cd_classificacao_risco = 2, mas hoje retornaria sempre n=0. Recomendo pactuar com a gestao a escala de 4 cores em vez de forcar 5 linhas.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '3.3', 60, 'Amarelo', NULL, NULL, '≤60 min', 1, 60.0, NULL, 0.5, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*)                                                       AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr
    ON cr.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (39, 48, 51, 67)
   AND UPPER(TRIM(cr.ds_classificacao_risco)) = ''AMARELO''
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'Amostra minuscula: 15 casos de 928 BAA obstetricos do mes. A media (92 min) e quatro vezes a mediana (22,8 min) porque um unico caso de 734,6 min puxa tudo — com n=15 o indicador e instavel mes a mes; recomendo publicar a mediana junto. O alvo cadastrado para AMARELO no proprio Salux e qt_tempo=30 min, entao a media estoura o alvo e a mediana cumpre. Cobertura de cor no bloco: so 81 de 928 BAA (8,7%) tem classificacao de risco preenchida — COALESCE com triagem e acolhimento nao melhora (continua 81). Dos 22 amarelos, 7 nao tem dt_atend_pac (campo presente em apenas 50,0% dos BAA do recorte) e ficam de fora. Par de carimbos consistente: nenhum caso com dt_atend_pac anterior a dt_classifica_atual (neg=0).', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '3.4', 70, 'Verde', NULL, NULL, '≤120 min', 1, 120.0, NULL, 0.5, 1, 'min', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*)                                                       AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr
    ON cr.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (39, 48, 51, 67)
   AND UPPER(TRIM(cr.ds_classificacao_risco)) = ''VERDE''
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'Maior amostra da familia M3 (34 casos) e o numero e clinicamente plausivel: verde espera mais que amarelo (113 vs 92 min de media; 70 vs 23 de mediana), o que e a ordenacao esperada da classificacao de risco. Ainda assim a media e inflada por cauda longa (max 922,6 min = 15 h) — mediana 69,6 min bate quase exatamente o alvo cadastrado no Salux para VERDE (qt_tempo=60 min). Cobertura de cor no bloco: 81 de 928 (8,7%). Dos 56 verdes, 22 nao tem dt_atend_pac e ficam de fora (campo presente em 50,0% dos BAA do recorte). Sem inversao de carimbo (neg=0).', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '3.5', 80, 'Azul', NULL, NULL, '≤240 min', 1, 240.0, NULL, 0.5, 1, 'min', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG((b.dt_atend_pac - b.dt_classifica_atual) * 1440), 2) AS valor,
       COUNT(*)                                                       AS denominador
  FROM infosaude.baa b
  JOIN infosaude.classificacao_risco cr
    ON cr.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
   AND b.cd_setor IN (39, 48, 51, 67)
   AND UPPER(TRIM(cr.ds_classificacao_risco)) = ''AZUL''
   AND b.dt_classifica_atual IS NOT NULL
   AND b.dt_atend_pac IS NOT NULL
   AND b.dt_atend_pac >= b.dt_classifica_atual', 'Motor correto, sem amostra util. A cor existe e esta ativa (cd=10, qt_tempo=1440 min) e teve 3 casos no recorte obstetrico de junho, mas os 3 estao sem dt_atend_pac — campo que so existe em 50,0% dos BAA do recorte — entao o numerador nao fecha para nenhum. Com n=3 no melhor cenario, esta linha nunca sera estatisticamente util neste bloco. Mesma ressalva de cobertura da familia M3: so 81 de 928 BAA obstetricos (8,7%) tem cor.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '4', 90, 'ÍNDICE DE QUESTIONÁRIOS PREENCHIDOS PELAS PACIENTES DA U&E DO CENTRO MATERNO INFANTIL', 'Número de questionários respondidos / Total de atendimentos médicos na U&E do Centro Materno Infantil *100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥10%', 2, 0.1, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '5', 100, 'PERCENTUAL DE SATISFAÇÃO PACIENTES DA U&E DO CENTRO MATERNO INFANTIL SATISFEITAS/ MUITO SATISFEITAS RELAÇÃO AO ATENDIMENTO NA MATERNIDADE', 'Número de questionários respondidos / Total de atendimentos médicos na U&E do Centro Materno Infantil*100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥ 85%', 2, 0.85, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '6', 110, 'NET PROMOTER SCORE (NPS)DA U&E DO CENTRO MATERNO', NULL, 'PEP ou Relatório de Pesquisa com o Usuário', '≥50%', 2, 0.5, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '7', 120, 'TAXA DE OCUPAÇÃO HOSPITALAR', 'Número de paciente-dia / Número de leito-dia *100', 'PEP ou SIH', '80 a 85%', NULL, NULL, NULL, 0.5, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT (SELECT ROUND(SUM(LEAST(NVL(fl.dt_saida_leito, :fim), :fim)
                       - GREATEST(fl.dt_transferencia, :ini)), 2)
          FROM infosaude.fia_leito fl
         WHERE fl.cd_hospital = :hospital
           AND fl.cd_unidade IN (15, 16, 17, 26)
           AND fl.dt_transferencia < :fim
           AND NVL(fl.dt_saida_leito, :fim) > :ini)  AS numerador,
       (SELECT COUNT(*)
          FROM infosaude.leito l
         WHERE l.cd_hospital = :hospital
           AND l.cd_unidade IN (15, 16, 17, 26)
           AND l.id_condicao = ''A'') * (:fim - :ini)  AS denominador
  FROM dual', 'ESCOLHA DO RECORTE: usei fia_leito.cd_unidade IN (15,16,17,26) porque fia.id_internacao_obstetrica esta 100% NULO — 0 preenchidos nas 739 internacoes de junho e 0 nas 4.350 internacoes do HMCML em 2026 inteiro. O flag e campo morto nesta base; o leito e o unico recorte com dado real. METODO: numerador = soma do tempo real em leito (sobreposicao com o periodo), nao contagem de cabecas. Conferi por um segundo metodo independente — censo diario a meia-noite somou 669 pacientes-dia (media 22,3/dia, max 30, min 15) contra 695,45 do metodo de tempo, diferenca de 3,8%: os dois convergem e o numero e solido. Se a gestao preferir a definicao oficial de paciente-dia do MS (que soma tambem quem entrou e saiu no mesmo dia), a contagem por presenca em qualquer momento do dia daria 32,7/dia = ~981 pac-dias = 48,1% — ainda muito abaixo da meta. DENOMINADOR: 68 leitos com id_condicao=''A'' de um cadastro de 94 leitos maternos (a MATERNIDADE sozinha tem 79 cadastrados). Excluindo tambem os com id_sit_leito=''F'' sobrariam 61 leitos (37,9%). Nao confie em unidade_hospitalar.qt_leito_fixo nem em in_unid_obstetrica (''N'' para todas). LIMITACAO TEMPORAL: infosaude.leito nao tem historico — o denominador usa a foto ATUAL do cadastro aplicada retroativamente ao mes apurado. LEITURA DE GESTAO: 34,1% contra meta de 80-85% e uma ociosidade real, com pico de 46 leitos ocupados simultaneamente contra 68 ativos; a unidade 26 (MATERNIDADE 2) teve zero internacoes e seus 2 leitos estao inativos. O BERCARIO(17) esta incluido por ser bloco materno-infantil (sao internacoes de RN, nao das maes); excluindo-o a taxa fica praticamente igual (33,4%).', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '8', 130, 'TEMPO MÉDIO DE PERMANÊNCIA', 'Número de paciente-dia / Número de saídas', 'PEP ou SIH', '≤ 2,5 dias', 1, 2.5, NULL, 0.5, 1, 'dias', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT ROUND(AVG(f.dt_alta - f.dt_baixa), 2) AS valor,
       COUNT(*)                              AS denominador
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim
   AND f.dt_alta >= f.dt_baixa
   AND EXISTS (SELECT 1
                 FROM infosaude.fia_leito fl
                WHERE fl.cd_hospital = f.cd_hospital
                  AND fl.dt_ano_fia  = f.dt_ano_fia
                  AND fl.nr_fia      = f.nr_fia
                  AND fl.cd_unidade IN (15, 16, 17, 26))', 'Numero clinicamente coerente para maternidade (84% das altas entre 1 e 3 dias, o padrao de alta em 48 h pos-parto normal e 72 h pos-cesarea) e dentro da meta de <=2,5 dias, mas por margem estreita — o mesmo calculo ancorado em dt_baixa (coorte de ENTRADAS do mes, 225 internacoes) da 2,55 dias e estoura a meta. Entreguei ancorado em dt_alta porque tempo medio de permanencia se calcula sobre as SAIDAS do periodo; registre essa convencao no contrato, ela decide se o indicador passa ou nao. NAO use fia.nr_dias_internacao: vem 100% nulo (0 de 226). Recorte por fia_leito.cd_unidade IN (15,16,17,26) porque fia.id_internacao_obstetrica esta 100% nulo em 2026 (0 de 4.350). O EXISTS pega a internacao que passou por QUALQUER leito materno, entao uma paciente que passou pelo centro cirurgico e depois pela maternidade conta — correto, mas o tempo medido e a internacao inteira, nao so o trecho no leito materno. Inclui o BERCARIO (RN internados), coerente com bloco materno-infantil. Denominador cobre 214 das 226 internacoes maternas (94,7%): 1 sem dt_alta (ainda internada) e 11 com alta fora do mes.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '9', 140, 'PROPORÇÃO DE PARTO CESÁREO', 'Número de partos cesáreos/ total de partos realizados*100', 'PEP ou SIH', '≤ 35%', 2, 0.35, NULL, 1.0, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT COUNT(DISTINCT CASE WHEN n.id_tp_parto = ''C''
                           THEN n.dt_ano_fia || ''/'' || n.nr_fia END) AS numerador,
       COUNT(DISTINCT n.dt_ano_fia || ''/'' || n.nr_fia)               AS denominador
  FROM infosaude.nascimento n
 WHERE n.fia_cd_hospital = :hospital
   AND n.dt_parto >= :ini
   AND n.dt_parto <  :fim', 'CAMINHO ENCONTRADO (respondendo a pergunta do briefing): o parto NAO esta em fia.cd_procedimento — esse campo vem 100% NULO nas 226 internacoes maternas de junho. Tambem descartei tu_bpa_unificada (zero registros dos codigos SIGTAP de parto 0310010039/0411010034/0411010026/0411010042/0310010047/0310010055/0310010012 em junho: o BPA e ambulatorial e nao registra parto) e infosaude.aih (nao possui NENHUMA coluna de procedimento; so datas e chaves de FIA). FIA_CLINICA tambem nao tem procedimento. O parto mora em infosaude.NASCIMENTO, ligada a FIA da MAE por (fia_cd_hospital, dt_ano_fia, nr_fia), com dt_parto e id_tp_parto. Foram 94 PARTOS em junho/2026 por esse caminho. VALIDACAO DE QUE ''C''=CESAREA: (a) a permanencia media da mae e maior nos ''C'' (2,45 dias) do que nos ''V'' (1,96 dias) em 2026, exatamente o esperado clinicamente; (b) a serie de 19 meses (jan/2025 a jul/2026) fica estavel entre 51,3% e 68,3%, sem salto — jun/2026 com 63,8% esta dentro da faixa. O campo texto tipo_parto (VARCHAR2 40) vem 100% nulo, so id_tp_parto (CHAR 1) serve. Uso COUNT(DISTINCT FIA) para que um parto gemelar conte 1 parto e nao 2 (em junho nao houve gemelar, mas em 2025/2026 aparecem 2 registros com id_tp_parto fora de C/V — entram no denominador como parto e nao no numerador, o que e o comportamento correto). O indicador estoura folgadamente a meta de <=35%, e isso e consistente com a serie historica inteira: nao e artefato do mes.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '10', 150, 'ESCORE DE BOLOGNA', NULL, NULL, NULL, NULL, NULL, NULL, 2.5, 5, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '10.1', 160, 'PROPORÇÃO DE GESTANTES COM PARTOGRAMA', 'Número de partos realizados com registro de partograma no prontuário / Total de partos no mês *100', 'PEP ou SIH', '≥ 80%', 2, 0.8, NULL, 0.5, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '10.2', 170, 'PROPORÇÃO DE GESTANTES COM ACOMPANHAMENTO NA SALA', 'Número de partos realizados com registro da presença do acompanhante no prontuário / total de partos mês *100', 'PEP ou SIH', '≥ 80%', 2, 0.8, NULL, 0.5, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '10.3', 180, 'PROPORÇÃO DE PARTOS NA POSIÇÃO VERTICALIZADA (NÃO SUPINA)', 'Número de partos normais realizados em posição não supina / total de partos normais mês *100', 'PEP ou SIH', '≥ 20%', 2, 0.2, NULL, 0.5, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '10.4', 190, 'PROPORÇÃO DE PARTURIENTES COM CONTATO PELE A PELE COM O RECEM NASCIDO POR PELO MENOS 30 MIN', 'Número de partos realizados com registro de contato pele a pele / total de partos mês *100', 'PEP ou SIH', '≥ 80%', 2, 0.8, NULL, 0.5, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '10.5', 200, 'PROPORÇÃO DE GESTANTES COM ESTIMULAÇÃO DO PARTO NORMAL', 'Número de gestantes com estimulação do trabalho de parto / total de partos normais no mês*100', 'PEP ou SIH', '≤ 20%', 1, 0.2, NULL, 0.5, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '11', 210, 'PROPORÇÃO DE PARTO NORMAL ASSISTIDO POR ENFERMEIRO OBSTÉTRICO', 'Número de partos normais assistidos por enfermeira obstétrica / total de partos normais no mês*100', 'PEP ou SIH', '≥ 60%', 2, 0.6, NULL, 1.0, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '12', 220, 'PROPORÇÃO DO USO DE MÉTODOS NÃO FARMACOLÓGICOS PARA ALÍVIO DA DOR', 'Número de partos normais com registro de métodos não farmacológicos de alívio da dor / total de partos normais no mês *100', 'PEP ou SIH', '≥ 60%', 2, 0.6, NULL, 1.0, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '13', 230, 'PERCENTUAL DE NASCIDOS VIVOS COM APGAR MENOR QUE 7 NO 5º MINUTO DE VIDA', 'Número de nascidos vivos com Apgar < 7 no 5º minuto de vida / Número total de recém‐nascidos mês * 100', 'PEP ou SIH', '≤1%', 1, 0.01, NULL, 1.0, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT SUM(CASE WHEN TO_NUMBER(TRIM(n.apgar_5_min)) < 7 THEN 1 ELSE 0 END) AS numerador,
       COUNT(*)                                                            AS denominador
  FROM infosaude.nascimento n
 WHERE n.fia_cd_hospital = :hospital
   AND n.dt_parto >= :ini
   AND n.dt_parto <  :fim
   AND TRIM(n.id_condicao_nascimento) = ''V''
   AND REGEXP_LIKE(TRIM(n.apgar_5_min), ''^[0-9]+$'')', 'DESCOBERTA: o Apgar E colunado, nao precisou de eDoc. Mora em infosaude.NASCIMENTO.APGAR_5_MIN e APGAR_1_MINUTO (CHAR(4), guardam o numero como texto, por isso o TRIM/TO_NUMBER e o REGEXP_LIKE de guarda contra valor nao numerico). Tabela ligada a FIA da mae por (fia_cd_hospital, dt_ano_fia, nr_fia). Existe tambem NASCIMENTO_BKP (backup, ignorada) e ESUS_ATEND_PUERICULTURA (atencao basica, fora do escopo hospitalar). COBERTURA EXCELENTE, rara nesta base: 94 de 94 nascimentos de junho tem apgar_5_min preenchido (0 nulos, 0 nao numericos) e os 94 tem FIA materna em leito materno. Denominador limitado a id_condicao_nascimento=''V'' (nascido VIVO) — em junho os 94 sao ''V'', nao houve natimorto registrado, entao o filtro nao alterou o resultado do mes, mas mantem a definicao correta do indicador. O indicador cumpre a meta de <=1% por pouco (1,06% e tecnicamente acima); com n=94 um unico caso vale 1,06 ponto percentual, entao ele oscila muito — abril/2026 chegou a 6,2% com 6 casos. Publicar sempre com o n.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '14', 240, 'PROPORÇÃO DE RECÉM-NASCIDOS AMAMENTADOS NA PRIMEIRA HORA DE VIDA', 'Número de recém-nascidos amamentados na primeira hora/total de recém-nascidos *100', 'PEP ou SIH', '≥ 85%', 2, 0.85, NULL, 1.0, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '15', 250, 'DENSIDADE DE INFECÇÃO DE SÍTIO CIRURGICO (PARTOS OPERATÓRIOS)', 'Número casos de ISC que ocorreram em até 30 dias / total de partos operatórios * 100', 'PEP ou SIH e Relatório Mensal da Comissão de Infecção Hospitalar', '≤1%', 1, 0.01, NULL, 1.0, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '16', 260, 'PROPORÇÃO DE ÓBITOS FETAIS E INFANTIS INVESTIGADOS', 'Número de óbitos fetais e infantis investigados / Número total de óbitos fetais e infantis*100', 'Censo Hospitalar / Relatório Mensal da Comissão de Revisão de Óbitos', '1', 2, 1.0, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '17', 270, 'NÚMERO DE ÓBITOS MATERNOS (até 42 dias após o parto)', 'Número absoluto', 'Censo Hospitalar e Relatório mensal da Comissão de Revisão de Óbitos', '0', 5, 0.0, NULL, 1.0, 3, NULL, NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT COUNT(*) AS valor
  FROM infosaude.fia f
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim
   AND (f.id_obito_mulher IS NOT NULL OR f.nr_obito IS NOT NULL)
   AND EXISTS (SELECT 1
                 FROM infosaude.fia_leito fl
                WHERE fl.cd_hospital = f.cd_hospital
                  AND fl.dt_ano_fia  = f.dt_ano_fia
                  AND fl.nr_fia      = f.nr_fia
                  AND fl.cd_unidade IN (15, 16, 17, 26))', 'O zero e apurado, mas NAO e confiavel como prova de ausencia — e por isso que nao marquei Validado. Os campos que sustentam o indicador estao praticamente mortos: fia.id_obito_mulher vem 100% NULO nas 1.590 internacoes obstetricas de 2026 e fia.nr_obito tambem (0 de 226 em junho); no HMCML inteiro, nas 4.350 internacoes de 2026, nr_obito aparece preenchido UMA unica vez (alta de 14/03/2026) e id_obito_mulher NENHUMA. Com esse nivel de subpreenchimento o motor nao distingue ''nao houve obito materno'' de ''houve e ninguem registrou''. A convergencia dos quatro caminhos em zero e um indicio favoravel, nao uma prova. RECORTE ''ATE 42 DIAS'': conforme alertado no briefing, o indicador so enxerga o que morreu DENTRO do episodio de internacao obstetrica no HMCML. Obito materno tardio (puerperio em casa, em outra unidade, na UPA de Inoa, no PA Santa Rita ou em hospital de outro municipio) e invisivel para esta base — o cruzamento correto seria com o SIM/DO municipal, que nao existe no Salux. Recorte de internacao por fia_leito.cd_unidade IN (15,16,17,26), ja que fia.id_internacao_obstetrica esta 100% nulo. Recomendacao operacional: antes de publicar este indicador, pactuar com a assistencia o preenchimento obrigatorio de id_obito_mulher na alta por obito, senao ele sera zero para sempre por construcao.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '18', 280, 'ÍNDICE DE QUESTIONÁRIOS PREENCHIDOS PELAS PACIENTES INTERNADAS NO CENTRO MATERNO INFANTIL', 'Número de questionários respondidos / Total de internações no Centro Materno Infantil*100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥ 30%', 2, 0.3, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '19', 290, 'PERCENTUAL DE SATISFAÇÃO DAS PACIENTES SATISFEITAS / MUITO SATISFEITAS RELAÇÃO AO ATENDIMENTO NA INTERNAÇÃO DO CENTRO MATERNO INFANTIL', 'Número de questionários respondidos / Total de internações no Centro Materno Infantil *100', 'PEP ou Relatório de Pesquisa com o Usuário', '≥ 85%', 1, 0.85, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 3, '20', 300, 'NET PROMOTER SCORE (NPS) DA INTERNAÇÃO DO CENTRO MATERNO INFANTIL', NULL, 'PEP ou Relatório de Pesquisa com o Usuário', '≥ 50%', 2, 0.5, NULL, 0.25, 1, NULL, NULL, 3, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '1', 10, 'TAXA DE ABSENTEÍSMO DA ENFERMAGEM', 'Total de dias perdidos pela equipe de enfermagem/ total de dias da equipe de enfermagem *100.', 'Relatório do Ponto Biométrico', '≤10%', 1, 0.1, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '2', 20, 'TEMPO MÉDIO DE SUBSTITUIÇÃO DE CARGOS EM VACÂNCIA', 'Σ dos dias do total de cargos em vacância/ número de cargos em vacância', 'Relatório do Ponto Biométrico e Relatório de Recursos Humanos', '≤10 dias', 1, 10.0, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '3', 30, 'ÍNDICE ROTATIVIDADE DE RECURSOS HUMANOS', '[(Número de contratados + número de demitidos) /2] / total de funcionários ativos', 'Relatório do Ponto Biométrico', '≤ 5%', 1, 0.05, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '4', 40, 'ÍNDICE RELATIVO DE ACIDENTES DE TRABALHO', 'Número de acidentes de trabalho ocorridos no mês / número de funcionários ativos no mês *100', 'Relatório SESMT / Relatório Ponto Biométrico', '≤ 1%', 1, 0.01, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '5', 50, 'TAXA DE TREINAMENTO HORA-PROFISSIONAL', 'Total de horas profissional treinados no mês /Número de profissionais ativos no período', 'Relatório do NEP / Relatório do Ponto Biométrico', '≥ 1,5 horas', 2, 1.5, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '6', 60, 'PROPORÇÃO DE COMPRAS COM PESQUISA DE PREÇO EM PLATAFORMA ELETRÔNICA DE BASE NACIONAL', 'Número de aquisições realizadas com pesquisa em plataformas eletrônicas / número total de aquisições no período', 'Relatório Plataforma de Compras', '≥80%', 2, 0.8, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '7', 70, 'TEMPO DE EQUIPAMENTO PARADO', 'Total de horas de equipamento sem funcionamento / total de horas úteis dos equipamentos', 'Relatório do Sistema de Ordem de Serviço', '≤ 72 horas', 1, 72.0, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '8', 80, 'CUSTO MÉDIO DO ATENDIMENTO DE URGÊNCIA E EMERGÊNCIA', 'Gasto total alocado no centro de custos U&E / número de atendimentos SUS', 'TABNET referente ao mês de competência / PEP', 'Custo médio', NULL, NULL, NULL, 0.5, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '9', 90, 'CUSTO MÉDIO DA DIÁRIA HOSPITALAR POR BLOCO (adulto, pediátrico e maternidade)', 'Gasto total alocado no centro de custos enfermarias / Número de pacientes-dia do grupo correspondente no período', 'TABNET referente ao mês de competência / PEP', 'Custo médio internação adulto', NULL, NULL, NULL, 0.5, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '10', 100, 'FATURAMENTO SUS', 'Número de atendimentos faturados / número de atendimentos realizados *100', 'Relatório DATASUS / PEP', '1', 2, 1.0, NULL, 1.0, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'WITH baa_per AS (
  SELECT b.dt_ano_baa, b.nr_baa
    FROM infosaude.baa b
   WHERE b.cd_hospital = :hospital
     AND b.dt_atendimento >= :ini
     AND b.dt_atendimento <  :fim
), bpa_per AS (
  SELECT DISTINCT u.dt_ano_baa, u.nr_baa
    FROM infosaude.tu_bpa_unificada u
   WHERE u.cd_hospital = :hospital
     AND u.baa_cd_hospital = :hospital
     AND u.dt_atend >= :ini
     AND u.dt_atend <  :fim
     AND u.dt_cmpt_bpa_unif >= TRUNC(:ini,''MM'')
     AND u.dt_cmpt_bpa_unif <  ADD_MONTHS(TRUNC(:fim,''MM''), 3)
     AND u.id_situacao = ''L''
     AND u.dt_geracao IS NOT NULL
)
SELECT SUM(CASE WHEN p.nr_baa IS NULL THEN 0 ELSE 1 END) AS numerador,
       COUNT(*) AS denominador
  FROM baa_per b
  LEFT JOIN bpa_per p
    ON p.dt_ano_baa = b.dt_ano_baa
   AND p.nr_baa    = b.nr_baa', 'COMO O HOSPITAL FATURA: predominantemente BPA CONSOLIDADO. Em jun/2026 a tu_bpa_unificada tem 70.857 linhas para o hospital 1 — 67.416 com TIPO_BPA=''C'' (consolidado) e 3.441 com ''I'' (individualizado). No nivel de atendimento: 15.061 BAA so com BPA-C, 2.343 com C e I simultaneos, e NENHUM so com I. Ou seja, todo atendimento entra no consolidado e ~13,5% recebem tambem registro individualizado (procedimentos que exigem BPA-I). COMPETENCIA: DT_CMPT_BPA_UNIF e DATE (primeiro dia do mes), nao numero. Nao ha faturamento retroativo — 100% das linhas que faturam atendimento de junho estao na propria competencia 06/2026 (lag zero, medido). DEFINICAO DE ''FATURADO'': ID_SITUACAO=''L'', que o dicionario do banco descreve como ''Movimentacao entregue, arquivo gerado'' (demais: D=digitado, O=critica executada OK, N=critica com inconsistencia). ARMADILHA CRITICA — SO RODAR COM A COMPETENCIA FECHADA: em 23/07/2026 a competencia 07/2026 tinha 31.693 linhas e NENHUMA com status ''L'' (10.496 ''D'' + 20.908 ''O'' + 289 ''N''), porque o arquivo BPA-magnetico so e gerado no inicio do mes seguinte. Rodar o indicador sobre um mes ainda aberto devolve 0%. O INDICADOR SATURA: ele fica em ~99,99% todo mes porque o Salux gera automaticamente a linha de BPA-C para cada atendimento do BAA; ele mede ''o atendimento entrou no arquivo de producao do SIA'', e NAO mede aprovacao/pagamento pelo gestor — glosa do SIA nao e visivel nesta base. Se o contrato quiser medir faturamento efetivamente remunerado, este motor nao serve. ESCOPO: cobre so a producao ambulatorial/urgencia (SIA/BPA). Internacao e faturada por AIH/SIH e esta no I11 — inclusive os 644 BAA de junho com id_destino=''I'' geram BPA da urgencia normalmente (todos os 644 tem BPA gerado). Denominador = BAA por dt_atendimento, campo 100% preenchido; nenhuma perda por campo nulo.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '11', 110, 'ALIMENTAÇÃO DO SIH/SUS', 'Número de AIH apresentada no mês / número de Internações realizadas no mês x 100', 'Relatório DATASUS', '1', 2, 1.0, NULL, 1.0, 1, '%', NULL, 2, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'WITH fia_per AS (
  SELECT f.dt_ano_fia, f.nr_fia
    FROM infosaude.fia f
    LEFT JOIN infosaude.paciente p
      ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
   WHERE f.cd_hospital = :hospital
     AND f.dt_baixa >= :ini
     AND f.dt_baixa <  :fim
     AND NOT (f.id_internacao = ''E''
              AND p.dt_nascimento IS NOT NULL
              AND TRUNC(f.dt_baixa) - TRUNC(p.dt_nascimento) <= 1)
), aih_ok AS (
  SELECT DISTINCT a.dt_ano_fia, a.nr_fia
    FROM infosaude.aih a
   WHERE a.cd_hospital = :hospital
     AND a.id_situacao_aih = ''L''
     AND a.nr_aih IS NOT NULL
)
SELECT SUM(CASE WHEN x.nr_fia IS NULL THEN 0 ELSE 1 END) AS numerador,
       COUNT(*) AS denominador
  FROM fia_per f
  LEFT JOIN aih_ok x
    ON x.dt_ano_fia = f.dt_ano_fia
   AND x.nr_fia    = f.nr_fia', 'MARQUEI NaoValidado NAO por defeito do SQL — ele roda e o numero esta certo — mas porque o valor de junho apurado hoje NAO e comparavel com a meta de 100%. (1) MATURACAO, a maior ressalva: a AIH so nasce depois da alta e e apresentada na competencia seguinte. Em 23/07/2026, das 644 internacoes de junho, 481 tem AIH liberada, 114 estao na competencia 202608 AINDA EM DIGITACAO (92 ''O'' + 16 ''N'' + 6 ''F'') e 56 estao criticadas/pendentes na 202607 ja fechada. Se as 114 forem liberadas, junho vai a ~92,4%. O gradiente descendente da serie jan(95,15%)->jun(74,69%) e maturacao, nao piora de desempenho. RECOMENDACAO: so medir o mes depois de fechada a 2a competencia seguinte (~60 dias). (2) COMPETENCIA E NUMERO, NAO DATA: AIH.DT_APRESENTACAO e NUMBER no formato AAAAMM (ex.: 202606). O motor deliberadamente NAO filtra por competencia — usa coorte de internacao (qualquer AIH liberada daquela FIA, em qualquer competencia), porque medi e comprovei que competencia e mes de internacao sao coortes DIFERENTES: das 610 AIH liberadas na competencia 202606, 609 sao de internacoes com ALTA EM MAIO e ZERO tem baixa em junho. Fazer ''AIH da competencia X / internacoes com baixa no mes X'' seria numerador e denominador de populacoes distintas — numero bonito e errado. (3) EXCLUSAO DE RECEM-NASCIDO — PRECISA DE RATIFICACAO DO FATURAMENTO: excluo do denominador FIA com ID_INTERNACAO=''E'' e idade do paciente na baixa <= 1 dia. Sao RN em alojamento conjunto, faturados dentro da AIH da mae. Evidencia: jan-jun/2026 foram 649 registros assim e EXATAMENTE ZERO tem AIH propria; entram todos pela unidade 15 MATERNIDADE. Grupo de controle que valida a regra: os 87 RN de ate 28 dias internados como URGENCIA (UTI neo/pediatria) continuam no denominador e 76 deles (87%) tem AIH. As demais eletivas nao-RN (12 casos) tambem ficam, e 8 tem AIH. Sem essa exclusao junho cai para 65,09% por motivo estrutural, nao por falha de faturamento. (4) ''APRESENTADA'' = ID_SITUACAO_AIH=''L'' + NR_AIH preenchido (dicionario do banco: A=aberta, F=fechada, B=bloqueada, R=reaberta, N=nao OK, O=OK, C=classificada, L=liberada). Na competencia 202606, as 610 linhas ''L'' tinham 100% de NR_AIH, SQ_AIH_LOTE e DT_FATURAMENTO — o status e confiavel. Assim como o I10, mede alimentacao/apresentacao ao SIH, nao aprovacao nem pagamento. (5) NR_DIAS_INTERNACAO vem 0 em 100% das FIA — campo morto, nao usar. (6) ALTERNATIVA MAIS ESTAVEL: ancorar a coorte em dt_alta em vez de dt_baixa (a AIH so existe apos a alta) — junho por alta da 583/734 = 79,43% (sem exclusao de RN); basta trocar dt_baixa por dt_alta no CTE fia_per. Escolher dt_baixa foi instrucao do briefing, mas dt_alta e tecnicamente mais coerente com o ciclo do SIH.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 5, '12', 120, 'RESOLUBILIDADE DE OUVIDORIAS', NULL, 'Relatório de Ouvidoria', '≥ 90%', 2, 0.9, NULL, 1.0, 1, NULL, NULL, 4, NULL, NULL, NULL, TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '1', 10, 'DISTRIBUIÇÃO DOS ATENDIMENTOS DE U&E SEGUNDO MOTIVO DOS ATENDIMENTOS', 'Possibilita a análise da relação entre o perfil de morbidade da demanda e a necessidade de complexidade da oferta de serviços — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico, que apresente a distribuição dos atendimentos, segundo o MOTIVO.', NULL, NULL, NULL, NULL, NULL, NULL, 4, 'atendimentos', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT NVL(LTRIM(m.ds_mot_atendimento, ''. ''), ''NAO INFORMADO'') AS rotulo,
       COUNT(*) AS quantidade
  FROM infosaude.baa b
  LEFT JOIN infosaude.motivo_atendimento m
    ON m.cd_mot_atendimento = b.cd_mot_atendimento
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
 GROUP BY NVL(LTRIM(m.ds_mot_atendimento, ''. ''), ''NAO INFORMADO'')
 ORDER BY COUNT(*) DESC', 'cd_mot_atendimento e preenchido em 100% dos 17.405 BAA e a soma da distribuicao fecha exatamente com o total do periodo — o motor esta correto. O problema e semantico, nao tecnico: no Salux esse campo funciona como TIPO de atendimento (emergencia x ambulatorial x causa externa), nao como motivo clinico/queixa. 97,1% cai num unico rotulo, entao o indicador tem baixissimo poder discriminante. Se o contrato quiser ''motivo'' no sentido clinico, a fonte correta seria BAA.CD_CID (diagnostico) ou a queixa da triagem — nao este campo; isso precisa ser pactuado antes de publicar. Observar tambem que as 3 categorias de causa externa (acidente de trabalho 29, agressao 14, PAF 5) somam so 48 casos, o que e implausivelmente baixo para 17,4 mil atendimentos de urgencia e sugere subregistro no campo (a tabela motivo_atendimento tem os flags in_acidente_trabalho / in_agressao / in_acidente_transito para consolidar por natureza, se desejado). Os acentos aparecem como ''?'' na minha captura por charset do cliente sqlplus (banco e WE8ISO8859P1); o dado gravado tem acento correto.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '2', 20, 'DISTRIBUIÇÃO DOS ATENDIMENTOS DE U&E SEGUNDO CLASSIFICAÇÃO DE RISCO', 'Possibilita a análise da relação entre o perfil de morbidade da demanda e a necessidade de complexidade da oferta de serviços — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico, que apresente a distribuição dos atendimentos, segundo a CLASSIFICAÇÃO DE RISCO.', NULL, NULL, NULL, NULL, NULL, NULL, 4, 'atendimentos', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT NVL(UPPER(r.ds_classificacao_risco), ''SEM CLASSIFICACAO DE RISCO'') AS rotulo,
       COUNT(*) AS quantidade
  FROM infosaude.baa b
  LEFT JOIN infosaude.classificacao_risco r
    ON r.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
 GROUP BY NVL(UPPER(r.ds_classificacao_risco), ''SEM CLASSIFICACAO DE RISCO'')
 ORDER BY CASE NVL(UPPER(r.ds_classificacao_risco), ''SEM CLASSIFICACAO DE RISCO'')
            WHEN ''VERMELHO'' THEN 1
            WHEN ''LARANJA''  THEN 2
            WHEN ''AMARELO''  THEN 3
            WHEN ''VERDE''    THEN 4
            WHEN ''AZUL''     THEN 5
            WHEN ''SEM CLASSIFICACAO DE RISCO'' THEN 9
            ELSE 8
          END', 'Tres alertas honestos. (1) 13,6% (2.369) dos atendimentos NAO tem cor — mantive como rotulo proprio para a distribuicao fechar com o total; se o contrato exigir base so de classificados, use denominador 15.036. Esses sem-cor nao estao aleatoriamente espalhados: concentram-se em CONS OBSTETRICIA (813), TRAUMA (446), ORTOPEDIA ADULTO (211) e CONS CLINICA MEDICA (227) — sao fluxos que entram direto na especialidade sem passar pelo acolhimento, nao e falha de digitacao pontual. (2) Nao existe LARANJA nesta base: a tabela classificacao_risco do HMCML so tem VERMELHO(7)/AMARELO(8)/VERDE(9)/AZUL(10), escala de 4 cores, e in_triagem_manchester = ''N'' em todas — ou seja, NAO e Manchester de 5 niveis. Se o contrato cobra a escala Manchester, isso e uma divergencia de processo a reportar, nao um bug do SQL. (3) VERMELHO com 10 casos no mes (0,06%) esta bem abaixo do esperado (0,5%-2%); a leitura mais provavel e que o paciente critico vai direto para sala vermelha/trauma sem registro de triagem e cai no balde ''sem classificacao'' (ha 109 TRAUMA sem cor que terminaram em internacao). O rotulo ''SALUX'' (cd 11, 3 casos) e lixo de configuracao do fornecedor — mantido por transparencia, pode ser suprimido se o cliente preferir.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '3', 30, 'DISTRIBUIÇÃO DOS ATENDIMENTOS DE U&E SEGUNDO SEXO E FAIXA ETÁRIA', 'Possibilita a análise do perfil demográfico da demanda, relacionando com a cobertura e capacidade de atendimento pela rede atenção primária à saúde segundo o local de origem do paciente (pacientes azuis. — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico, que apresente a distribuição dos atendimentos, segundo SEXO e FAIXA ETÁRIA. OBS: Se o sistema de prontuário não possuir relatório que apresente os 2 atriburos (sexo e faixa etária), pode enviar os 2 relatórios separados. As faixas etárias adotadas são: 0 a <1 ano; 1 - 4; 5 - 9; 10 -12; 13 - 15; 16 -19; 20-29; 30- 39; 40-49; 50-59; 60 e mais.', NULL, NULL, NULL, NULL, NULL, NULL, 4, 'atendimentos', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT base.sexo || '' · '' || DECODE(base.ix,
          1,  ''0 a <1 ano'',
          2,  ''1-4'',
          3,  ''5-9'',
          4,  ''10-12'',
          5,  ''13-15'',
          6,  ''16-19'',
          7,  ''20-29'',
          8,  ''30-39'',
          9,  ''40-49'',
          10, ''50-59'',
          11, ''60 e mais'',
          ''IDADE IGNORADA'') AS rotulo,
       COUNT(*) AS quantidade
  FROM (SELECT CASE WHEN p.sexo = ''M'' THEN ''M''
                    WHEN p.sexo = ''F'' THEN ''F''
                    ELSE ''NI'' END AS sexo,
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
           AND b.dt_atendimento >= :ini
           AND b.dt_atendimento <  :fim) base
 GROUP BY base.sexo, base.ix
 ORDER BY base.sexo, base.ix', 'Cobertura perfeita: paciente.dt_nascimento e paciente.sexo estao preenchidos em 100% dos 17.405 atendimentos (join por NVL(cd_paciente_unificado, cd_paciente)). Idade calculada na data do atendimento (nao na data de hoje) com MONTHS_BETWEEN, o que resolve corretamente o corte de <1 ano. Ressalvas: (a) o sexo vem de paciente.SEXO (a coluna se chama SEXO, nao id_sexo) e so assume ''M''/''F'' — nao ha registro de sexo ignorado/intersexo, entao o rotulo ''NI'' existe no motor por defesa mas nunca aparece; (b) 1 unico paciente sai com 140 anos (erro de cadastro de data de nascimento) e cai em ''60 e mais'', impacto desprezivel, mas e sinal de que nao ha validacao de nascimento no cadastro; (c) o separador do rotulo e o ponto medio U+00B7 exigido pelo contrato — ele saiu como ''??'' na minha captura porque o cliente sqlplus da sessao converte mal caracteres nao-ASCII (banco e WE8ISO8859P1, onde U+00B7 existe como byte 0xB7); pelo driver do backend o rotulo sai correto, e as contagens sao identicas de todo jeito porque o separador e constante; (d) o predominio feminino (57,7%) e o peso de 60+ (17,5%) sao compativeis com perfil de urgencia municipal, sem sinal de erro.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '4', 40, 'DISTRIBUIÇÃO DOS ATENDIMENTOS SEGUNDO DIAS DA SEMANA E HORÁRIOS DE MAIOR PICO DE ATENDIMENTOS', 'Possibilita analisar a dinâmica dos atendimentos no serviço e eventual necessidade de adequações — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico, que apresente a distribuição dos atendimentos, segundo DIAS DA SEMANA e HORÁRIOS de maior pico.', NULL, NULL, NULL, NULL, NULL, NULL, 4, 'atendimentos', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT DECODE(TRUNC(b.dt_atendimento) - TRUNC(b.dt_atendimento, ''IW''),
              0, ''SEG'', 1, ''TER'', 2, ''QUA'', 3, ''QUI'', 4, ''SEX'', 5, ''SAB'', 6, ''DOM'')
       || '' '' || TO_CHAR(b.dt_atendimento, ''HH24'') || ''h'' AS rotulo,
       COUNT(*) AS quantidade
  FROM infosaude.baa b
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
 GROUP BY TRUNC(b.dt_atendimento) - TRUNC(b.dt_atendimento, ''IW''),
          TO_CHAR(b.dt_atendimento, ''HH24'')
 ORDER BY COUNT(*) DESC,
          TRUNC(b.dt_atendimento) - TRUNC(b.dt_atendimento, ''IW''),
          TO_CHAR(b.dt_atendimento, ''HH24'')', 'Motor exato: 100% de dt_atendimento preenchido, 168 celulas, soma fecha. Duas observacoes. (1) Usei dt_atendimento e nao dt_chegada de proposito, mas medi antes: as duas datas caem na MESMA hora em 17.405/17.405 dos casos (mediana e p90 da diferenca = 0 min), ou seja, no HMCML dt_atendimento e o carimbo de abertura do boletim/chegada — o indicador mede demanda de PORTA, nao inicio do atendimento medico. Se o contrato quiser pico do atendimento MEDICO, a coluna seria dt_atend_pac, que so tem 48,7% de preenchimento e daria um retrato enviesado. (2) O dia da semana e derivado de TRUNC(dt)-TRUNC(dt,''IW''), que ancora a semana na segunda-feira independentemente de NLS_TERRITORY — nao usei TO_CHAR(dt,''D''), que mudaria de significado conforme a sessao. Leitura clinica: pico em segunda e terca de manha (8h-11h) com vale de madrugada e o padrao classico de porta de urgencia que absorve demanda represada do fim de semana e demanda ambulatorial nao atendida na rede basica — coerente, sem sinal de bug. Como junho/2026 tem 4 segundas e 5 tercas, comparar celula com celula entre dias diferentes tem um vies de contagem de dias; para ranking de pico dentro do mesmo dia da semana isso nao afeta.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '5', 50, 'DISTRIBUIÇÃO DOS ATENDIMENTOS SEGUNDO ORIGEM DO PACIENTE (DISTRITO SANITÁRIO, BAIRRO, MUNICÍPIO)', 'Possibilita análise da distribuição geográfica dos atendimentos via a vis cobertura assistencial em atenção primária à saúde — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico, que apresente a distribuição dos atendimentos, segundo o BAIRRO', NULL, NULL, NULL, NULL, NULL, NULL, 4, 'atendimentos', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT CASE
         WHEN p.cd_cidade = 330270 THEN NVL(UPPER(TRIM(p.bairro)), ''BAIRRO NAO INFORMADO'')
         ELSE NVL(UPPER(TRIM(p.bairro)), ''BAIRRO NAO INFORMADO'')
              || '' / '' || NVL(UPPER(c.ds_cidade), ''MUNICIPIO NAO INFORMADO'')
       END AS rotulo,
       COUNT(*) AS quantidade
  FROM infosaude.baa b
  LEFT JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(b.cd_paciente_unificado, b.cd_paciente)
  LEFT JOIN infosaude.cidade c
    ON c.cd_uf = p.cd_uf AND c.cd_cidade = p.cd_cidade
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
 GROUP BY CASE
            WHEN p.cd_cidade = 330270 THEN NVL(UPPER(TRIM(p.bairro)), ''BAIRRO NAO INFORMADO'')
            ELSE NVL(UPPER(TRIM(p.bairro)), ''BAIRRO NAO INFORMADO'')
                 || '' / '' || NVL(UPPER(c.ds_cidade), ''MUNICIPIO NAO INFORMADO'')
          END
 ORDER BY COUNT(*) DESC', 'Coluna usada: infosaude.PACIENTE.BAIRRO (VARCHAR2(100), texto livre) — foi a unica candidata de bairro na tabela paciente alem de BAIRRO_TRAB (bairro do TRABALHO, descartada) e CD_BAIRRO_SA04 (codigo de logradouro do padrao SA04, sem tabela de dominio util aqui). Preenchimento: 17.405/17.405 = 100%. Decisao de projeto que precisa ser conhecida: para paciente de fora de Marica eu ACRESCENTO '' / MUNICIPIO'' ao rotulo. Sem isso a distribuicao mentiria: RIO DO OURO existe em Marica e em Sao Goncalo e apareceria fundido em 221 quando na verdade e 20 de Marica + 201 de Sao Goncalo; CENTRO cairia de 1.567 (Marica) para 1.581 misturando centros de outras cidades. Limitacoes reais: (a) o campo e texto digitado, sem tabela de dominio — 483 rotulos distintos para ~90 bairros oficiais de Marica indica variacao de grafia/abreviacao e cauda longa de digitacao livre; para uso epidemiologico serio isso exige um de-para de normalizacao, que NAO fiz aqui (entregar bairro normalizado por chute seria inventar dado); (b) muitos rotulos ja trazem o distrito entre parenteses no proprio cadastro (''CORDEIRINHO (PONTA NEGRA)''), padrao herdado do CEP, o que pode ser usado para agregar por distrito depois; (c) uso o cadastro ATUAL do paciente, entao mudanca de endereco posterior reescreve retroativamente o bairro do atendimento passado.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '6', 60, 'PROPORÇÃO DOS ATENDIMENTOS DE U&E DE NÃO RESIDENTES DO MUNICÍPIO DE MARICÁ', 'Análise da sobrecarga da rede municipal por população não residente — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico, que apresente a distribuição dos atendimentos de NÃO RESISDENTES do Município de Maricá.', NULL, NULL, NULL, NULL, NULL, NULL, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT COUNT(CASE WHEN p.cd_cidade IS NOT NULL AND p.cd_cidade <> 330270 THEN 1 END) AS numerador,
       COUNT(CASE WHEN p.cd_cidade IS NOT NULL THEN 1 END) AS denominador
  FROM infosaude.baa b
  LEFT JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(b.cd_paciente_unificado, b.cd_paciente)
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim', 'Grafia do municipio nao foi problema porque NAO comparei texto: usei o codigo IBGE. Verifiquei em infosaude.cidade que existe exatamente UMA linha com cd_cidade = 330270 e uma unica linha cujo nome casa com ''%MARIC%'' (RJ / 330270 / ''MARICA'', sem acento na base) — logo nao ha homonimo nem duplicata de cadastro para escapar, e o codigo IBGE ja carrega o prefixo de UF (33 = RJ), o que o torna unico nacionalmente. Fonte escolhida: cd_cidade do CADASTRO do paciente, que tem cobertura quase total (so 2 atendimentos em 17.405 sem municipio, excluidos do denominador = 0,01%), contra 135 sem municipio (0,78%) se eu usasse o endereco copiado no BAA. Ressalvas: (a) BAA e cadastro divergem em 150 atendimentos (0,86%) — endereco atualizado depois do atendimento; a diferenca no resultado final e de 0,01 ponto percentual, irrelevante; (b) ''nao residente'' aqui e endereco declarado no cadastro, sem comprovacao, e o cadastro atual sobrescreve o endereco da epoca — quem se mudou para Marica depois de junho conta como residente retroativamente; (c) 5,83% de invasao para um hospital de porta aberta na divisa com Sao Goncalo/Itaborai/Niteroi e um numero plausivel, nao inflado.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '7', 70, 'DISTRIBUIÇÃO DOS ATENDIMENTOS SEGUNDO PROCEDIMENTOS REALIZADOS (CÓDIGO SUS)', 'Análise da complexidade dos atendimentos — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico ou do sistema de informação ambulatorial (SIA), que apresente a distribuição dos atendimentos segundo PROCEDIMENTOS REALIZADOS (código SIGTAP)', NULL, NULL, NULL, NULL, NULL, NULL, 4, 'atendimentos', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT TO_CHAR(t.cd_proced_sia) || '' - '' || NVL(ps.ds_proced_sia, ''PROCEDIMENTO NAO CADASTRADO'') AS rotulo,
       COUNT(DISTINCT b.dt_ano_baa || ''/'' || b.nr_baa) AS quantidade
  FROM infosaude.baa b
  JOIN infosaude.tu_bpa_unificada t
    ON t.baa_cd_hospital = b.cd_hospital
   AND t.dt_ano_baa      = b.dt_ano_baa
   AND t.nr_baa          = b.nr_baa
  LEFT JOIN infosaude.proced_sia ps
    ON ps.cd_proced_sia = t.cd_proced_sia
 WHERE b.cd_hospital = :hospital
   AND b.dt_atendimento >= :ini
   AND b.dt_atendimento <  :fim
 GROUP BY t.cd_proced_sia, ps.ds_proced_sia
 ORDER BY COUNT(DISTINCT b.dt_ano_baa || ''/'' || b.nr_baa) DESC', 'ESCOLHA DA FONTE (o ponto que o contrato pede para explicar): usei tu_bpa_unificada, NAO BAA.CD_PROCED_SIA. Motivo medido, nao opiniao: BAA.CD_PROCED_SIA so tem 12.088/17.405 preenchidos (69,5%) e, pior, 12.079 desses 12.088 carregam o MESMO codigo 301060061 — sobram 9 atendimentos distribuidos em 3 outros codigos. Ou seja, aquele campo nao e uma distribuicao, e uma constante com buraco; se eu o usasse, o indicador diria ''um unico procedimento em 99,9% dos casos''. Ja o BPA cobre 17.404/17.405 atendimentos (99,99%) e discrimina 241 procedimentos reais. Puxei do BAA para o BPA pelo indice IX_TU_BPA_UNIFICADA_BAA (baa_cd_hospital, dt_ano_baa, nr_baa) — assim o recorte de periodo e de hospital vem do BAA e a base fica identica a dos demais indicadores do grupo (filtrar tu_bpa direto por dt_atend faz full scan de 5,4M linhas e estoura o tempo). Ressalvas de leitura: (a) e uma distribuicao MULTI-RESPOSTA — a soma (70.816) e maior que o total de atendimentos (17.405) porque um atendimento gera varios procedimentos; percentual deve ser lido sobre 17.405 (''em X% dos atendimentos foi feito o procedimento Y''), nunca como fatia de pizza que soma 100%; (b) quantidade = atendimentos DISTINTOS com aquele procedimento, nao quantidade faturada (se quiserem volume faturado, troque por SUM(t.qt_proced_sia): 72.723 no mes); (c) o BPA e base de FATURAMENTO, entao mede procedimento faturado, que pode ser subconjunto do realizado (o que nao entra na producao SUS nao aparece) e esta sujeito a reprocessamento de competencia — reprocessar um mes ja fechado pode mudar o numero retroativamente; (d) contraprova de coerencia clinica: os 15.020 acolhimentos com classificacao de risco faturados batem com os 15.036 BAA com cor preenchida do P2 (99,9%), o que confirma que as duas tabelas estao contando o mesmo mundo.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '8', 80, 'DISTRIBUIÇÃO DAS INTERNAÇÕES HOSPITALARES SEGUNDO A ORIGEM DO PACIENTE (BAIRRO E MUNICÍPIO)', 'Análise do perfil de utilização do serviço segundo região geográfica; análise de acessibilidade ao serviço de saúde. — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico, que apresente a distribuição das internações, segundo o BAIRRO e MUNICÍPIO', NULL, NULL, NULL, NULL, NULL, NULL, 4, NULL, NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT NVL(UPPER(TRIM(ci.ds_cidade)), ''MUNICIPIO NAO INFORMADO'')
       || '' '' || CHR(183) || '' '' ||
       NVL(UPPER(TRIM(p.bairro)), ''BAIRRO NAO INFORMADO'') AS rotulo,
       COUNT(*) AS quantidade
  FROM infosaude.fia f
  LEFT JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
  LEFT JOIN infosaude.cidade ci
    ON ci.cd_uf = p.cd_uf
   AND ci.cd_cidade = p.cd_cidade
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim
 GROUP BY NVL(UPPER(TRIM(ci.ds_cidade)), ''MUNICIPIO NAO INFORMADO''),
          NVL(UPPER(TRIM(p.bairro)), ''BAIRRO NAO INFORMADO'')
 ORDER BY quantidade DESC, rotulo', 'VOLUME: 734 altas (dt_alta) no hospital 1 em jun/2026 - volume normal, nao e mes atipico (serie mensal jan/2025 a jun/2026 varia de 589 a 808; jul/2026 aparece com 565 so porque o mes esta incompleto na base). O indicador tem significado. COLUNA DE BAIRRO USADA: infosaude.paciente.BAIRRO (VARCHAR2 100) - e a unica coluna de bairro do endereco residencial; existe tambem BAIRRO_TRAB (endereco de trabalho, nao usada) e CD_BAIRRO_SA04 (codigo do cadastro SA04, sem tabela de dominio util). Municipio vem de paciente.cd_uf + paciente.cd_cidade -> infosaude.cidade (Marica = cd_uf ''RJ'', cd_cidade 330270, confirmado). COBERTURA: 734/734 (100%) com paciente resolvido por NVL(cd_paciente_unificado, cd_paciente), bairro preenchido e municipio preenchido - nao ha buraco em junho. LIMITACAO REAL: o endereco vem do CADASTRO ATUAL do paciente, nao do endereco vigente na data da internacao; se o paciente mudar de endereco, a serie historica muda retroativamente. O FIA guarda um snapshot proprio (fia.cd_uf/cd_cidade) que diverge do cadastro em 3 de 730 casos, mas NAO tem bairro - por isso o bairro obriga a usar o cadastro. QUALIDADE DO TEXTO: bairro e campo livre digitado, sem tabela de dominio; em junho veio limpo (caixa alta, sem acento, 101 valores distintos) com o distrito entre parenteses (''INOA (INOA)'', ''JARDIM ATLANTICO LESTE (ITAIPUACU)''). Consolidar bairro/distrito exigiria um de-para manual - nao inventei um. SEPARADOR: usei CHR(183) e nao o literal ''·'' porque o banco e WE8ISO8859P1 e o literal em UTF-8 chega corrompido (DUMP do literal = bytes 191,191; DUMP de CHR(183) = byte 183); CHR(183) devolve exatamente U+00B7 em qualquer cliente.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '9', 90, 'PROPORÇÃO DE INTERNAÇÕES DE NÃO RESIDENTES EM MARICÁ', 'Análise da relação entre oferta e demanda e a necessidade de leitos — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico, que apresente a distribuição das internações de NÃO RESISDENTES do Município de Maricá.', NULL, NULL, NULL, NULL, NULL, NULL, 1, '%', NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT COUNT(CASE WHEN p.cd_uf IS NOT NULL
                   AND p.cd_cidade IS NOT NULL
                   AND NOT (p.cd_uf = ''RJ'' AND p.cd_cidade = 330270)
                  THEN 1 END) AS numerador,
       COUNT(CASE WHEN p.cd_uf IS NOT NULL
                   AND p.cd_cidade IS NOT NULL
                  THEN 1 END) AS denominador
  FROM infosaude.fia f
  LEFT JOIN infosaude.paciente p
    ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim', 'VOLUME: 734 altas no hospital 1 em jun/2026 (volume normal da serie). 7,08% de nao residentes e plausivel para hospital municipal de porta aberta em municipio litoraneo vizinho de Sao Goncalo/Niteroi. RESIDENCIA definida por cd_uf=''RJ'' AND cd_cidade=330270 (MARICA, confirmado em infosaude.cidade); todos os 734 pacientes de junho sao do RJ, nenhum de outro estado. DENOMINADOR: contei apenas internacoes com municipio identificado, para nao diluir o percentual com desconhecidos. Em junho isso nao muda nada (734/734 tem municipio), mas em mes com falha de cadastro o indicador nao vai mentir para baixo. FONTE DO ENDERECO: cadastro atual do paciente (mesma fonte do P8, para os dois indicadores conversarem). DIVERGENCIA MEDIDA E NAO ESCONDIDA: se em vez do cadastro eu usasse o snapshot da internacao (fia.cd_uf/cd_cidade), dariam 54 nao residentes em 730 internacoes com municipio = 7,40%. Diferenca de 0,3 p.p., mas existe - escolhi o cadastro do paciente por coerencia com o P8 e porque o snapshot do FIA tem 4 registros sem municipio. Se a gestao preferir o snapshot (mais fiel a serie historica), e trocar p.cd_uf/p.cd_cidade por f.cd_uf/f.cd_cidade - decisao de definicao, nao de tecnica.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '10', 100, 'DISTRIBUIÇÃO DAS INTERNAÇÕES HOSPITALARES POR SEXO E FAIXA ETÁRIA', 'Análise do perfil etário da demanda e suas implicações nos indicadores operacionais de tempo de permanência e mortalidade institucional. — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico, que apresente a distribuição das internações, segundo SEXO e FAIXA ETÁRIA. OBS: Se o sistema de prontuário não possuir relatório que apresente os 2 atriburos (sexo e faixa etária), pode enviar os 2 relatórios separados. As faixas etárias adotadas são: 0 a <1 ano; 1 - 4; 5 - 9; 10 -12; 13 - 15; 16 -19; 20-29; 30- 39; 40-49; 50-59; 60 e mais.', NULL, NULL, NULL, NULL, NULL, NULL, 4, NULL, NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT s.sexo || '' '' || CHR(183) || '' '' ||
       DECODE(s.ix,  1, ''0 a <1'',
                     2, ''1-4'',
                     3, ''5-9'',
                     4, ''10-12'',
                     5, ''13-15'',
                     6, ''16-19'',
                     7, ''20-29'',
                     8, ''30-39'',
                     9, ''40-49'',
                    10, ''50-59'',
                    11, ''60+'',
                        ''IDADE IGNORADA'') AS rotulo,
       COUNT(*) AS quantidade
  FROM (SELECT CASE WHEN UPPER(p.sexo) = ''F'' THEN ''F''
                    WHEN UPPER(p.sexo) = ''M'' THEN ''M''
                    ELSE ''NI'' END AS sexo,
               CASE
                 WHEN p.dt_nascimento IS NULL
                   OR p.dt_nascimento > f.dt_alta                                    THEN 99
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) <  1     THEN 1
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) <  5     THEN 2
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 10     THEN 3
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 13     THEN 4
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 16     THEN 5
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 20     THEN 6
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 30     THEN 7
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 40     THEN 8
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 50     THEN 9
                 WHEN FLOOR(MONTHS_BETWEEN(f.dt_alta, p.dt_nascimento) / 12) < 60     THEN 10
                 ELSE 11
               END AS ix
          FROM infosaude.fia f
          LEFT JOIN infosaude.paciente p
            ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
         WHERE f.cd_hospital = :hospital
           AND f.dt_alta >= :ini
           AND f.dt_alta <  :fim) s
 GROUP BY s.sexo, s.ix
 ORDER BY s.sexo, s.ix', 'VOLUME: 734 altas no hospital 1 em jun/2026 (volume normal). COBERTURA: 734/734 (100%) com sexo e data de nascimento preenchidos; em junho so existem ''F'' (409) e ''M'' (325) - o SQL mapeia qualquer outro valor/nulo para ''NI'', que nao apareceu. Atencao: a coluna e infosaude.paciente.SEXO (CHAR 1), NAO ''id_sexo'' como consta no briefing - id_sexo nao existe na tabela. IDADE: anos completos na data da ALTA, via FLOOR(MONTHS_BETWEEN(dt_alta, dt_nascimento)/12). Conferida contra a funcao do proprio Salux infosaude.f_busca_idade(dt_nascimento, dt_alta, 2): 734/734 identicas no componente de anos (a funcao devolve texto tipo ''0a 4m 1d'', por isso a comparacao foi feita so no componente de anos). DEFINICAO PENDENTE COM A GESTAO (impacta a leitura): a faixa ''0 a <1'' tem 139 casos (18,9% do total) e e dominada por RECEM-NASCIDOS - 92 dos 139 nasceram durante a propria internacao (dt_nascimento >= data da baixa) e 89 tem CID Z37/Z38 (registro de nascimento). Ou seja, o RN abre FIA proprio e conta como internacao. Se o contrato quiser perfil de MORBIDADE, os RN saudaveis precisam ser excluidos - nao excluí por conta propria porque isso muda o denominador de varios outros indicadores. A assimetria F 20-29 (74) x M 20-29 (20) nao e erro: e obstetricia. SEPARADOR: CHR(183) em vez do literal ''·'' pelo motivo de charset explicado no P8.', TRUE, NOW() AT TIME ZONE 'utc'),
(gen_random_uuid(), 4, '11', 110, 'DISTRIBUIÇÃO DAS INTERNAÇÕES POR CID-10 PRINCIPAL DE SAÍDA (ADULTOS; CRIANÇAS E MULHER/GESTANTE)', 'Análise das causas de internação da demanda espontânea; análise do grau de adequação da oferta de leitos e nível de complexidade — Forma de apresentacao: Envio de relatório do sistema de prontuário eletrônico, que apresente a distribuição das internações por CID-10 de saída.', NULL, NULL, NULL, NULL, NULL, NULL, 4, NULL, NULL, 1, (SELECT id FROM smsmarica.ia_fonte WHERE slug = 'salux-hcml' AND excluido_em IS NULL LIMIT 1), 'SELECT CASE WHEN f.cd_cid IS NULL THEN ''SEM CID REGISTRADO''
            ELSE TRIM(f.cd_cid) || '' '' || CHR(183) || '' '' ||
                 NVL(TRIM(c.ds_cid), ''DESCRICAO NAO CADASTRADA'')
       END AS rotulo,
       COUNT(*) AS quantidade
  FROM infosaude.fia f
  LEFT JOIN infosaude.cid c
    ON c.cd_cid = f.cd_cid
 WHERE f.cd_hospital = :hospital
   AND f.dt_alta >= :ini
   AND f.dt_alta <  :fim
 GROUP BY CASE WHEN f.cd_cid IS NULL THEN ''SEM CID REGISTRADO''
               ELSE TRIM(f.cd_cid) || '' '' || CHR(183) || '' '' ||
                    NVL(TRIM(c.ds_cid), ''DESCRICAO NAO CADASTRADA'')
          END
 ORDER BY quantidade DESC, rotulo', 'VOLUME: 734 altas no hospital 1 em jun/2026 (volume normal da serie). COBERTURA: fia.cd_cid preenchido em 734/734 (100%) e ZERO codigos sem correspondencia em infosaude.cid - o join e limpo. Os dois lados sao CHAR(6) e o codigo ja vem com o ponto (''A41.9 ''), por isso basta TRIM, sem formatacao artificial. E MESMO O CID DE SAIDA? Evidencia a favor: fia.dt_cd_cid (data em que o CID principal foi carimbado) e igual a data da ALTA em 658/734 (89,6%) e igual a data da baixa em 27; 76 tem outra data intermediaria. Nao existe no FIA uma coluna separada de ''CID de admissao'' x ''CID de alta'' - cd_cid e o unico CID principal, sobrescrito ao longo da internacao (ha ainda cid2_cd_cid, cid3_cd_cid e cd_cid_secundario, que sao secundarios e nao entram aqui). CAUDA MUITO LONGA: 292 rotulos para 734 altas, sendo 179 deles com um unico caso; o top-20 cobre apenas 293/734 (40%). Nao apliquei corte no SQL para nao perder informacao, mas na apresentacao recomendo top-N + ''OUTROS'', senao a tabela fica com 292 linhas ilegiveis. ARMADILHA DE LEITURA (importante): o 1o colocado, Z37.0, com 89 casos (12,1%), NAO e morbidade - e o registro de nascimento do recem-nascido, que abre FIA proprio. Quem ler a tabela como ''principais causas de internacao'' vai errar. Precisa de nota de rodape, ou de uma decisao da gestao para excluir capitulo Z (Z37/Z38) do indicador. CAIXA DO TEXTO: ds_cid esta gravado em CAIXA ALTA no Salux, entao o rotulo sai ''A41.9 · SEPTICEMIA NAO ESPECIFICADA'' e nao ''Septicemia nao especificada'' como no exemplo. Nao converti para caixa de sentenca no SQL de proposito: isso quebraria siglas (HIV, AIDS, SIDA, DPOC). Se a formatacao for exigida, faca na camada de apresentacao, onde da para proteger as siglas. SEPARADOR: CHR(183) pelo motivo de charset explicado no P8.', TRUE, NOW() AT TIME ZONE 'utc');

-- Amarra os subindicadores ao agrupador (3.1..3.5 -> 3), como o SUM() da planilha.
UPDATE smsmarica.indicador f
   SET indicador_pai_id = p.id
  FROM smsmarica.indicador p
 WHERE f.aba = p.aba
   AND POSITION('.' IN f.numero) > 0
   AND p.numero = SPLIT_PART(f.numero, '.', 1)
   AND f.indicador_pai_id IS NULL;

-- Primeira versao do SQL de cada motor, para o historico nascer completo.
INSERT INTO smsmarica.indicador_versao (id, indicador_id, numero, sql, nota, criado_em)
SELECT gen_random_uuid(), i.id, 1, i.sql,
       'Motor inicial - escrito e conferido contra dado real de junho/2026',
       NOW() AT TIME ZONE 'utc'
  FROM smsmarica.indicador i
 WHERE i.sql IS NOT NULL;


END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove só o catálogo semeado (execuções e versões caem por cascata).
            migrationBuilder.Sql("DELETE FROM smsmarica.indicador;");
        }
    }
}
