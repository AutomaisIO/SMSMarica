-- =====================================================================================
-- SANEAMENTO das duplicatas pré-identifier no hub FHIR
-- PRÉ-REQUISITO do backfill (backfill-identifier-clinico.sql), que por sua vez é
-- pré-requisito de ligar o sincronismo contínuo.
--
-- *** NÃO EXECUTAR SEM APROVAÇÃO EXPLÍCITA E SEM BACKUP. ***
-- Este script faz soft-delete de recursos clínicos e re-aponta filhos. É reversível
-- (soft-delete + tabela de auditoria abaixo), mas mexe em dado clínico de produção.
--
-- -------------------------------------------------------------------------------------
-- O QUE FOI DESCOBERTO (medido em produção, 01/08/2026 — leitura apenas)
--
--   fhir.encounter          2.723 chaves com EXATAMENTE 2 cópias vivas  → 2.723 excedentes
--   fhir.document_reference 6.494 chaves com EXATAMENTE 2 cópias vivas  → 6.494 excedentes
--   fhir.observation        0 duplicatas (não foi afetado)
--
--   * 2.258 PACIENTES afetados;
--   * as duas cópias são IDÊNTICAS em conteúdo (amostra de 200 chaves: 200 idênticas);
--   * as duas cópias foram criadas no MESMO run (amostra de 500: 500 com diferença < 1h);
--   * criadas entre 07/06 e 20/06/2026 — bate com o run que morreu em 20/06 (ORA-01013);
--   * atendimentos de 01/10/2022 a 03/06/2026 (a duplicação é do RUN, não do período).
--
--   Assinatura do bug conhecido (corrigido em código no commit 88175e9, mas os dados
--   já duplicados ficaram): a busca do hub era capada em 200/500 sem paginação e o
--   CriarAsync era POST cego — a purga parava no 1º teto e o reimport duplicava.
--
-- IMPACTO HOJE, JÁ EM PRODUÇÃO (independe do sincronismo novo): 2.258 pacientes têm o
-- MESMO atendimento aparecendo duas vezes na linha do tempo, com os filhos clínicos
-- DIVIDIDOS entre as duas cópias — 2.689 das 2.723 chaves têm filhos nas duas.
-- Filhos vivos pendurados nas cópias: 4.890 Condition · 22.010 Observation ·
-- 7.709 MedicationRequest · 12.402 DocumentReference.
--
-- POR QUE BLOQUEIA O BACKFILL: o índice único parcial
-- (identifier_system, identifier_value) WHERE identifier_system IS NOT NULL AND NOT
-- is_deleted rejeitaria a segunda cópia. O backfill falharia no primeiro lote com
-- duplicata — e é por isso que ele NÃO deve ser executado antes deste saneamento.
--
-- -------------------------------------------------------------------------------------
-- ESTRATÉGIA (3 passos, nesta ordem)
--   1. Eleger a cópia SOBREVIVENTE por chave: a de menor id (estável e determinístico;
--      como o conteúdo é idêntico, qualquer critério serve — o que importa é ser o mesmo
--      em todas as consultas do script).
--   2. RE-APONTAR os filhos da perdedora para a sobrevivente. Sem isso, ~47 mil recursos
--      clínicos ficariam pendurados num Encounter soft-deletado — pior que a duplicata.
--   3. SOFT-DELETE da perdedora.
--   4. Deduplicar as Conditions resultantes: hoje NENHUM encounter tem >1 condition viva,
--      mas depois do merge 2.345 chaves teriam 2 (uma de cada cópia, idênticas). Sem isto,
--      o backfill de condition (derivado ':cond') colidiria no índice único.
-- =====================================================================================

\timing on
\set ON_ERROR_STOP on

-- -------------------------------------------------------------------------------------
-- 0. Auditoria: guarda o que foi mexido (permite reverter e prestar contas)
-- -------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS fhir.saneamento_duplicata_20260801 (
    id               bigserial PRIMARY KEY,
    tabela           text        NOT NULL,
    chave            text        NOT NULL,
    id_sobrevivente  uuid        NOT NULL,
    id_removido      uuid        NOT NULL,
    filhos_movidos   jsonb,
    executado_em     timestamptz NOT NULL DEFAULT now()
);

-- -------------------------------------------------------------------------------------
-- 1+2+3. ENCOUNTER: eleger, re-apontar filhos, soft-delete da perdedora
-- -------------------------------------------------------------------------------------
DO $$
DECLARE
  r            record;
  n_cond       bigint; n_obs bigint; n_med bigint; n_doc bigint;
  processadas  bigint := 0;
BEGIN
  FOR r IN
    WITH dup AS (
      SELECT content #>> '{identifier,0,value}' AS v
        FROM fhir.encounter
       WHERE NOT is_deleted AND content #>> '{identifier,0,system}' IS NOT NULL
       GROUP BY 1 HAVING count(*) > 1)
    SELECT dup.v AS chave,
           min(e.id) AS fica,
           array_agg(e.id) FILTER (WHERE e.id <> (SELECT min(e2.id) FROM fhir.encounter e2
                WHERE NOT e2.is_deleted
                  AND e2.content #>> '{identifier,0,value}' = dup.v)) AS sai
      FROM dup
      JOIN fhir.encounter e ON e.content #>> '{identifier,0,value}' = dup.v AND NOT e.is_deleted
     GROUP BY dup.v
  LOOP
    UPDATE fhir.condition          SET encounter_id = r.fica WHERE encounter_id = ANY(r.sai) AND NOT is_deleted;
    GET DIAGNOSTICS n_cond = ROW_COUNT;
    UPDATE fhir.observation        SET encounter_id = r.fica WHERE encounter_id = ANY(r.sai) AND NOT is_deleted;
    GET DIAGNOSTICS n_obs = ROW_COUNT;
    UPDATE fhir.medication_request SET encounter_id = r.fica WHERE encounter_id = ANY(r.sai) AND NOT is_deleted;
    GET DIAGNOSTICS n_med = ROW_COUNT;
    UPDATE fhir.document_reference SET encounter_id = r.fica WHERE encounter_id = ANY(r.sai) AND NOT is_deleted;
    GET DIAGNOSTICS n_doc = ROW_COUNT;

    UPDATE fhir.encounter
       SET is_deleted = true, last_updated = now(), version_id = version_id + 1
     WHERE id = ANY(r.sai);

    INSERT INTO fhir.saneamento_duplicata_20260801 (tabela, chave, id_sobrevivente, id_removido, filhos_movidos)
    SELECT 'encounter', r.chave, r.fica, x,
           jsonb_build_object('condition', n_cond, 'observation', n_obs,
                              'medication_request', n_med, 'document_reference', n_doc)
      FROM unnest(r.sai) AS x;

    processadas := processadas + 1;
    IF processadas % 200 = 0 THEN
      RAISE NOTICE 'encounter: % chaves saneadas', processadas;
      COMMIT;
    END IF;
  END LOOP;
  COMMIT;
  RAISE NOTICE 'ENCOUNTER CONCLUÍDO: % chaves', processadas;
END $$;

-- -------------------------------------------------------------------------------------
-- 4. CONDITION: depois do merge, cada encounter pode ter 2 conditions idênticas
--    (uma de cada cópia). Mantém a de menor id; soft-delete das demais.
-- -------------------------------------------------------------------------------------
DO $$
DECLARE
  afetadas bigint; total bigint := 0;
BEGIN
  LOOP
    WITH grupos AS (
      SELECT encounter_id, min(id) AS fica
        FROM fhir.condition
       WHERE NOT is_deleted AND encounter_id IS NOT NULL
       GROUP BY encounter_id HAVING count(*) > 1
       LIMIT 2000),
    alvo AS (
      SELECT c.id, g.fica FROM fhir.condition c
        JOIN grupos g ON g.encounter_id = c.encounter_id
       WHERE NOT c.is_deleted AND c.id <> g.fica)
    UPDATE fhir.condition c
       SET is_deleted = true, last_updated = now(), version_id = c.version_id + 1
      FROM alvo WHERE c.id = alvo.id;
    GET DIAGNOSTICS afetadas = ROW_COUNT;
    total := total + afetadas;
    EXIT WHEN afetadas = 0;
    RAISE NOTICE 'condition: % soft-deletadas (acumulado %)', afetadas, total;
    COMMIT;
  END LOOP;
  RAISE NOTICE 'CONDITION CONCLUÍDO: % soft-deletadas', total;
END $$;

-- -------------------------------------------------------------------------------------
-- 5. DOCUMENT_REFERENCE: mesma lógica, sem filhos para re-apontar.
--    (As Observations de vitais do eDoc NÃO duplicaram — medido: 0.)
-- -------------------------------------------------------------------------------------
DO $$
DECLARE
  afetadas bigint; total bigint := 0;
BEGIN
  LOOP
    WITH grupos AS (
      SELECT content #>> '{identifier,0,value}' AS v, min(id) AS fica
        FROM fhir.document_reference
       WHERE NOT is_deleted AND content #>> '{identifier,0,system}' IS NOT NULL
       GROUP BY 1 HAVING count(*) > 1
       LIMIT 2000),
    alvo AS (
      SELECT d.id, g.fica, g.v FROM fhir.document_reference d
        JOIN grupos g ON g.v = d.content #>> '{identifier,0,value}'
       WHERE NOT d.is_deleted AND d.id <> g.fica)
    UPDATE fhir.document_reference d
       SET is_deleted = true, last_updated = now(), version_id = d.version_id + 1
      FROM alvo WHERE d.id = alvo.id;
    GET DIAGNOSTICS afetadas = ROW_COUNT;
    total := total + afetadas;
    EXIT WHEN afetadas = 0;
    RAISE NOTICE 'document_reference: % soft-deletadas (acumulado %)', afetadas, total;
    COMMIT;
  END LOOP;
  RAISE NOTICE 'DOCUMENT_REFERENCE CONCLUÍDO: % soft-deletadas', total;
END $$;

-- -------------------------------------------------------------------------------------
-- 6. GATE: tem de dar ZERO nas três. Só então o backfill pode rodar.
-- -------------------------------------------------------------------------------------
SELECT 'encounter' AS tabela, count(*) AS chaves_ainda_duplicadas FROM (
  SELECT 1 FROM fhir.encounter WHERE NOT is_deleted AND content #>> '{identifier,0,system}' IS NOT NULL
   GROUP BY content #>> '{identifier,0,value}' HAVING count(*) > 1) a
UNION ALL
SELECT 'document_reference', count(*) FROM (
  SELECT 1 FROM fhir.document_reference WHERE NOT is_deleted AND content #>> '{identifier,0,system}' IS NOT NULL
   GROUP BY content #>> '{identifier,0,value}' HAVING count(*) > 1) b
UNION ALL
SELECT 'condition (>1 por encounter)', count(*) FROM (
  SELECT 1 FROM fhir.condition WHERE NOT is_deleted AND encounter_id IS NOT NULL
   GROUP BY encounter_id HAVING count(*) > 1) c;

-- Nenhum filho pode ter ficado apontando para Encounter soft-deletado:
SELECT 'orfaos' AS check,
  (SELECT count(*) FROM fhir.condition x JOIN fhir.encounter e ON e.id = x.encounter_id
    WHERE NOT x.is_deleted AND e.is_deleted) AS cond,
  (SELECT count(*) FROM fhir.observation x JOIN fhir.encounter e ON e.id = x.encounter_id
    WHERE NOT x.is_deleted AND e.is_deleted) AS obs,
  (SELECT count(*) FROM fhir.medication_request x JOIN fhir.encounter e ON e.id = x.encounter_id
    WHERE NOT x.is_deleted AND e.is_deleted) AS med,
  (SELECT count(*) FROM fhir.document_reference x JOIN fhir.encounter e ON e.id = x.encounter_id
    WHERE NOT x.is_deleted AND e.is_deleted) AS doc;

-- -------------------------------------------------------------------------------------
-- REVERSÃO (se necessário, antes do backfill): a auditoria tem o de-para.
--   UPDATE fhir.encounter SET is_deleted = false
--    WHERE id IN (SELECT id_removido FROM fhir.saneamento_duplicata_20260801 WHERE tabela='encounter');
--   (os filhos re-apontados NÃO voltam sozinhos — a distribuição original está perdida,
--    mas era arbitrária: as cópias eram idênticas e a divisão foi acidente do bug.)
-- =====================================================================================
