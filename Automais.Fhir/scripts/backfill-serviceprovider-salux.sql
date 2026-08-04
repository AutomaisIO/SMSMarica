-- =====================================================================================
-- Backfill de Encounter.serviceProvider nos registros LEGADOS do Salux (ADR-0039)
--
-- POR QUE ISTO EXISTE
-- O ADR-0039 pôs a unidade de saúde como eixo durável do dado clínico, e o conector passou
-- a emitir `serviceProvider` em 04/08/2026. Mas ele só carimba o que reprocessa: 1,15 milhão
-- de encounters anteriores ficaram sem unidade — o hub sabia de QUAL SISTEMA veio cada
-- atendimento (meta.source) e continuava sem saber ONDE aconteceu.
--
-- POR QUE NÃO PRECISA LER O SALUX
-- O identifier do encounter JÁ carrega o hospital: `salux-hcml:{H}-{ano}-{nr}` (a chave é
-- montada em SaluxLinhas.BaaLinha.Chave / FiaLinha.Chave). O mapeamento H → unidade é
-- determinístico e as três Organizations já existem no hub. Então isto é um UPDATE local —
-- minutos — e NÃO um "Completo" do conector, que levaria dias e não sobreviveria ao ritmo
-- de deploy atual.
--
--   H=1 → 2266733  HOSPITAL MUNICIPAL CONDE MODESTO LEAL
--   H=2 → 7164440  UPA 24H INOÃ        (hoje operada pelo Klinikos — a unidade é a mesma)
--   H=3 → 2266792  PA 24H SANTA RITA   (idem)
--
-- SEGURANÇA
--   * Idempotente: todo UPDATE tem `content->'serviceProvider' IS NULL`. Re-executar não faz nada.
--   * Não destrutivo: só ACRESCENTA uma chave ao jsonb. Nenhum DELETE, nada é sobrescrito.
--   * Interrompível: em lotes com COMMIT; parar no meio deixa o resto sem serviceProvider
--     (estado válido — é exatamente o estado de antes).
--   * A unidade é resolvida por CNES, não por id fixo: se a Organization for recriada, o
--     script continua correto.
--
-- DECISÃO DELIBERADA: version_id e last_updated NÃO são bumpados.
--   Bumpar 1,15 milhão de linhas faria todo consumidor que acompanha `_lastUpdated` re-puxar
--   a base inteira, e destruiria a ordenação "mais recentes primeiro" da busca — tudo passaria
--   a ter o mesmo carimbo. Isto é preenchimento de um campo que deveria existir desde sempre,
--   não uma revisão clínica do recurso. O conteúdo segue coerente com a versão que ele declara.
--
-- COBERTURA MEDIDA EM PRODUÇÃO (04/08/2026, linhas vivas):
--   urn:salux:baa   1.151.841 encounters —    552 já com serviceProvider
--   urn:salux:fia       6.850 encounters —    139 já com serviceProvider
--   (urn:klinikos:boletim já nasce com serviceProvider — fora do escopo)
-- =====================================================================================

\timing on
\set ON_ERROR_STOP on

-- -------------------------------------------------------------------------------------
-- 0. Antes: retrato (guardar a saída para conferir no fim)
-- -------------------------------------------------------------------------------------
SELECT 'ANTES' AS momento,
       split_part(split_part(identifier_value, ':', 2), '-', 1) AS hospital,
       count(*) AS vivos,
       count(*) FILTER (WHERE content->'serviceProvider' IS NULL) AS sem_unidade
FROM fhir.encounter
WHERE NOT is_deleted AND identifier_system IN ('urn:salux:baa', 'urn:salux:fia')
GROUP BY 1, 2
ORDER BY 2;

-- -------------------------------------------------------------------------------------
-- 1. Guarda: as três unidades TÊM de existir antes de qualquer escrita.
--    Sem isto, um hub sem Organization gravaria referência para o vazio.
-- -------------------------------------------------------------------------------------
DO $$
DECLARE faltando text;
BEGIN
    SELECT string_agg(c.cnes, ', ') INTO faltando
    FROM (VALUES ('2266733'), ('7164440'), ('2266792')) AS c(cnes)
    WHERE NOT EXISTS (
        SELECT 1 FROM fhir.organization o WHERE o.cnes = c.cnes AND NOT o.is_deleted);

    IF faltando IS NOT NULL THEN
        RAISE EXCEPTION 'Organization ausente para o(s) CNES: %. Rode o conector uma vez antes.', faltando;
    END IF;
END $$;

-- -------------------------------------------------------------------------------------
-- 2. Backfill em lotes (o LIMIT evita transação gigante e lock longo).
--    Repetir o bloco até "UPDATE 0" — ou usar o runner em Python, que faz o laço.
-- -------------------------------------------------------------------------------------
WITH mapa AS (
    SELECT h, o.id AS org_id
    FROM (VALUES ('1', '2266733'), ('2', '7164440'), ('3', '2266792')) AS m(h, cnes)
    JOIN fhir.organization o ON o.cnes = m.cnes AND NOT o.is_deleted
),
alvo AS (
    SELECT e.ctid, mapa.org_id
    FROM fhir.encounter e
    JOIN mapa ON mapa.h = split_part(split_part(e.identifier_value, ':', 2), '-', 1)
    WHERE NOT e.is_deleted
      AND e.identifier_system IN ('urn:salux:baa', 'urn:salux:fia')
      AND e.content->'serviceProvider' IS NULL
    LIMIT 50000
)
UPDATE fhir.encounter e
   SET content = jsonb_set(
           e.content, '{serviceProvider}',
           jsonb_build_object('reference', 'Organization/' || alvo.org_id::text), true)
  FROM alvo
 WHERE e.ctid = alvo.ctid;

-- -------------------------------------------------------------------------------------
-- 3. Depois: conferência. `sem_unidade` tem de ser 0 nas três linhas.
-- -------------------------------------------------------------------------------------
SELECT 'DEPOIS' AS momento,
       split_part(split_part(identifier_value, ':', 2), '-', 1) AS hospital,
       count(*) AS vivos,
       count(*) FILTER (WHERE content->'serviceProvider' IS NULL) AS sem_unidade
FROM fhir.encounter
WHERE NOT is_deleted AND identifier_system IN ('urn:salux:baa', 'urn:salux:fia')
GROUP BY 1, 2
ORDER BY 2;

-- Sanidade: nenhum encounter pode apontar para Organization inexistente.
SELECT count(*) AS referencias_orfas
FROM fhir.encounter e
WHERE NOT e.is_deleted
  AND e.content->'serviceProvider'->>'reference' IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM fhir.organization o
      WHERE 'Organization/' || o.id::text = e.content->'serviceProvider'->>'reference');
