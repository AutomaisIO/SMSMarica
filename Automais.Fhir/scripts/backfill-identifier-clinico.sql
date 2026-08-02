-- =====================================================================================
-- Backfill dos identifiers clínicos no hub FHIR — PRÉ-REQUISITO do upsert condicional
-- (ADR-0024 §5). Rodar DEPOIS da migration 20260727020900_AddIdentifierClinicoELocation
-- e ANTES de ligar a agenda do sincronismo contínuo.
--
-- POR QUE ISTO EXISTE
-- A migration só cria colunas nullable + índices; não faz backfill. O upsert do hub busca
-- EXCLUSIVAMENTE pelas colunas (identifier_system/identifier_value), sem fallback ao JSONB.
-- Sem este script, todos os registros criados antes dela ficam INVISÍVEIS ao upsert:
--   * BAA editado que reentra na janela do incremental → Encounter e Condition DUPLICADOS
--     (o índice único não impede: a linha legada tem identifier_system NULL, fora do filtro);
--   * pior, no CDC de eDoc o lookup do Encounter legado volta vazio e o código faz return
--     silencioso com a marca do log avançando → a edição é PERDIDA, sem trilha.
--
-- SEGURANÇA
--   * Idempotente: todo UPDATE tem WHERE identifier_system IS NULL. Re-executar não faz nada.
--   * Não destrutivo: só preenche coluna nova; nenhum DELETE, nenhum content alterado.
--   * Interrompível: em lotes com COMMIT; parar no meio deixa o resto NULL (estado válido).
--   * Rodar com psql, NÃO pelo MigrateAsync do startup (timeout de 30s derruba o serviço).
--
-- COBERTURA MEDIDA EM PRODUÇÃO (01/08/2026, linhas vivas):
--   encounter            1.121.721 — 100% com identifier no content  → backfill direto
--   document_reference   2.461.573 — 100% com identifier no content  → backfill direto
--   condition            1.087.183 —   0% no content → derivado do Encounter (chave + ':cond')
--   medication_request   1.451.803 —   0% no content → NÃO derivável (ver §4)
--   observation          4.175.030 —   0% no content → NÃO derivável (ver §4)
-- =====================================================================================

\timing on
\set ON_ERROR_STOP on

-- -------------------------------------------------------------------------------------
-- 0. Antes: retrato do que será preenchido (guardar a saída para conferir no fim)
-- -------------------------------------------------------------------------------------
SELECT 'ANTES' AS momento, 'encounter' AS tabela,
       count(*) FILTER (WHERE identifier_system IS NULL) AS sem_identifier,
       count(*) AS vivos
FROM fhir.encounter WHERE NOT is_deleted
UNION ALL SELECT 'ANTES', 'document_reference',
       count(*) FILTER (WHERE identifier_system IS NULL), count(*)
FROM fhir.document_reference WHERE NOT is_deleted
UNION ALL SELECT 'ANTES', 'condition',
       count(*) FILTER (WHERE identifier_system IS NULL), count(*)
FROM fhir.condition WHERE NOT is_deleted;

-- -------------------------------------------------------------------------------------
-- 1. ENCOUNTER e DOCUMENT_REFERENCE — identifier já está no content (identifier[0])
--    Em lotes, para não segurar lock nem inflar WAL numa transação só.
-- -------------------------------------------------------------------------------------
DO $$
DECLARE
  afetadas bigint;
  total    bigint := 0;
BEGIN
  LOOP
    WITH alvo AS (
      SELECT id FROM fhir.encounter
      WHERE identifier_system IS NULL
        AND NOT is_deleted
        AND content #>> '{identifier,0,system}' IS NOT NULL
      LIMIT 20000
      FOR UPDATE SKIP LOCKED
    )
    UPDATE fhir.encounter e
       SET identifier_system = left(e.content #>> '{identifier,0,system}', 100),
           identifier_value  = left(e.content #>> '{identifier,0,value}', 200)
      FROM alvo WHERE e.id = alvo.id;
    GET DIAGNOSTICS afetadas = ROW_COUNT;
    total := total + afetadas;
    EXIT WHEN afetadas = 0;
    RAISE NOTICE 'encounter: % linhas (acumulado %)', afetadas, total;
    COMMIT;
  END LOOP;
  RAISE NOTICE 'encounter CONCLUÍDO: % linhas', total;
END $$;

DO $$
DECLARE
  afetadas bigint;
  total    bigint := 0;
BEGIN
  LOOP
    WITH alvo AS (
      SELECT id FROM fhir.document_reference
      WHERE identifier_system IS NULL
        AND NOT is_deleted
        AND content #>> '{identifier,0,system}' IS NOT NULL
      LIMIT 20000
      FOR UPDATE SKIP LOCKED
    )
    UPDATE fhir.document_reference d
       SET identifier_system = left(d.content #>> '{identifier,0,system}', 100),
           identifier_value  = left(d.content #>> '{identifier,0,value}', 200)
      FROM alvo WHERE d.id = alvo.id;
    GET DIAGNOSTICS afetadas = ROW_COUNT;
    total := total + afetadas;
    EXIT WHEN afetadas = 0;
    RAISE NOTICE 'document_reference: % linhas (acumulado %)', afetadas, total;
    COMMIT;
  END LOOP;
  RAISE NOTICE 'document_reference CONCLUÍDO: % linhas', total;
END $$;

-- -------------------------------------------------------------------------------------
-- 2. ENCOUNTER.period_end — mesma lacuna (recortes por "altas do período" ignoravam o legado)
-- -------------------------------------------------------------------------------------
DO $$
DECLARE
  afetadas bigint;
  total    bigint := 0;
BEGIN
  LOOP
    WITH alvo AS (
      SELECT id FROM fhir.encounter
      WHERE period_end IS NULL
        AND NOT is_deleted
        AND content #>> '{period,end}' IS NOT NULL
      LIMIT 20000
      FOR UPDATE SKIP LOCKED
    )
    UPDATE fhir.encounter e
       SET period_end = (e.content #>> '{period,end}')::timestamptz
      FROM alvo WHERE e.id = alvo.id;
    GET DIAGNOSTICS afetadas = ROW_COUNT;
    total := total + afetadas;
    EXIT WHEN afetadas = 0;
    RAISE NOTICE 'encounter.period_end: % linhas (acumulado %)', afetadas, total;
    COMMIT;
  END LOOP;
  RAISE NOTICE 'period_end CONCLUÍDO: % linhas', total;
END $$;

-- -------------------------------------------------------------------------------------
-- 3. CONDITION — derivado do Encounter: o importador usa identifier
--    'urn:salux:baa|{slug}:{h}-{ano}-{nr}' + sufixo ':cond' (mesma regra para :fia).
--    Só condições LIGADAS a um encounter que JÁ tem identifier são deriváveis; as demais
--    ficam NULL (relatadas no passo 5) e serão recriadas pelo sincronismo.
-- -------------------------------------------------------------------------------------
DO $$
DECLARE
  afetadas bigint;
  total    bigint := 0;
BEGIN
  LOOP
    WITH alvo AS (
      SELECT c.id,
             e.identifier_system AS sys,
             e.identifier_value  AS val
        FROM fhir.condition c
        JOIN fhir.encounter e ON e.id = c.encounter_id
       WHERE c.identifier_system IS NULL
         AND NOT c.is_deleted
         AND e.identifier_system IS NOT NULL
       LIMIT 20000
       FOR UPDATE OF c SKIP LOCKED
    )
    UPDATE fhir.condition c
       SET identifier_system = alvo.sys,
           identifier_value  = left(alvo.val || ':cond', 200)
      FROM alvo WHERE c.id = alvo.id;
    GET DIAGNOSTICS afetadas = ROW_COUNT;
    total := total + afetadas;
    EXIT WHEN afetadas = 0;
    RAISE NOTICE 'condition: % linhas (acumulado %)', afetadas, total;
    COMMIT;
  END LOOP;
  RAISE NOTICE 'condition CONCLUÍDO: % linhas', total;
END $$;

-- Colisão possível: 2 Conditions vivas no mesmo Encounter derivariam o MESMO ':cond' e o
-- índice único rejeitaria a segunda. O laço acima falharia alto (ON_ERROR_STOP). Diagnóstico:
SELECT e.identifier_value AS encounter_identifier, count(*) AS conditions_vivas
FROM fhir.condition c JOIN fhir.encounter e ON e.id = c.encounter_id
WHERE NOT c.is_deleted AND e.identifier_system IS NOT NULL
GROUP BY 1 HAVING count(*) > 1
ORDER BY 2 DESC LIMIT 20;

-- -------------------------------------------------------------------------------------
-- 4. MEDICATION_REQUEST e OBSERVATION — NÃO são backfilláveis. Decisão registrada.
--
--    O identifier deles depende de dados que não estão no recurso:
--      medication_request → 'urn:salux:presc|{slug}:{chaveBaa}-{nr_prescricao}-{seq_item}'
--                           (nr_prescricao/seq_item não existem no content);
--      observation        → 'urn:salux:edoc|{slug}:{h}-{ano}-{idm}:{tipo}'
--                           (a chave do documento não existe no content; só o encounter).
--
--    RISCO REAL, e por que é aceitável: o incremental só reprocessa BAA dentro da janela de
--    120 dias (ClausulaBaaNovoOuEditado). O legado do hub vai até 20/06/2026 e as marcas
--    estão em 06/06 — ou seja, a sobreposição "legado sem identifier × reprocessado" é o
--    intervalo 06/06–20/06. Fora dele, nada é re-tocado e nada duplica.
--
--    Detecção de duplicata (rodar DEPOIS dos primeiros ciclos; esperado: 0 ou poucos):
--      -- MedicationRequest: mesmo encounter + mesmo medicamento, um com e um sem identifier
--      SELECT encounter_id, count(*) FILTER (WHERE identifier_system IS NULL) AS legado,
--             count(*) FILTER (WHERE identifier_system IS NOT NULL) AS novo
--      FROM fhir.medication_request WHERE NOT is_deleted
--      GROUP BY 1 HAVING count(*) FILTER (WHERE identifier_system IS NULL) > 0
--                   AND count(*) FILTER (WHERE identifier_system IS NOT NULL) > 0;
--
--    Limpeza, se aparecerem: soft-delete das linhas legadas (identifier_system IS NULL) do
--    encounter que já ganhou a versão nova — a versão com identifier é a corrente.
-- -------------------------------------------------------------------------------------

-- -------------------------------------------------------------------------------------
-- 5. Depois: conferência. O gate do deploy é ZERO em 'sem_identifier' para as 3 primeiras.
-- -------------------------------------------------------------------------------------
SELECT 'DEPOIS' AS momento, 'encounter' AS tabela,
       count(*) FILTER (WHERE identifier_system IS NULL) AS sem_identifier,
       count(*) AS vivos
FROM fhir.encounter WHERE NOT is_deleted
UNION ALL SELECT 'DEPOIS', 'document_reference',
       count(*) FILTER (WHERE identifier_system IS NULL), count(*)
FROM fhir.document_reference WHERE NOT is_deleted
UNION ALL SELECT 'DEPOIS', 'condition',
       count(*) FILTER (WHERE identifier_system IS NULL), count(*)
FROM fhir.condition WHERE NOT is_deleted
UNION ALL SELECT 'DEPOIS (esperado > 0 — ver §4)', 'medication_request',
       count(*) FILTER (WHERE identifier_system IS NULL), count(*)
FROM fhir.medication_request WHERE NOT is_deleted
UNION ALL SELECT 'DEPOIS (esperado > 0 — ver §4)', 'observation',
       count(*) FILTER (WHERE identifier_system IS NULL), count(*)
FROM fhir.observation WHERE NOT is_deleted;

-- Smoke do upsert: cada identifier preenchido tem que ser único entre os vivos.
SELECT 'encounter' AS tabela, identifier_system, identifier_value, count(*)
FROM fhir.encounter WHERE NOT is_deleted AND identifier_system IS NOT NULL
GROUP BY 1,2,3 HAVING count(*) > 1 LIMIT 10;
