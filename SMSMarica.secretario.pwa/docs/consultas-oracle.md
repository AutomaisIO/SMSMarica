# Consultas Oracle do Painel do Secretário

Todas validadas contra o Oracle PROD do Salux (`10.50.0.18:1521/ORASX01`, schema `INFOSAUDE`) em 24/07/2026, com a conta com acesso de leitura ao schema. **Execução sempre read-only**: `SET TRANSACTION READ ONLY` + guard de SQL (só SELECT/WITH), mesmo padrão do `SaluxOracleFonte` do SMSMarica.server.

Hospital fixo: `cd_hospital = 1` (HMCML). UPA Inoã (2) e PA Santa Rita (3) existem na base mas estão **sem movimento** — não incluir.

## Fatos do schema que NÃO se pode "corrigir"

1. `BAA.DT_INICIO_ATEND_MED` / `DT_FIM_ATEND_MED` estão **100% vazias** no HMCML. O marcador do início do atendimento médico é o **primeiro eDoc de boletim médico**: `EDOC_MOVIMENTO` com `CD_MODELO IN (10036, 10232, 10014)` — cobre ~92,6% dos boletins.
2. `BAA.DT_CHEGADA` = `BAA.DT_ATENDIMENTO` (ambos são a abertura do boletim na recepção).
3. ~15% dos boletins de emergência **não têm cor** (`CD_CLASSIFICACAO_RISCO NULL`). Reportar como `SEM_CLASSIFICACAO`, nunca descartar silenciosamente.
4. Cores ativas (`CLASSIFICACAO_RISCO.IN_ATIVO='S'`): 7=VERMELHO (meta 15min), 8=AMARELO (30), 9=VERDE (60), 10=AZUL (1440), 11=SALUX (ignorar na UI — ruído, meta 0, meia dúzia de casos/mês; somar em SEM_CLASSIFICACAO ou omitir).
5. `TRIAGEM` tem ~2 linhas por BAA (re-triagens + tipo P). Usar `BAA.DT_CLASSIFICA_ATUAL` como carimbo da classificação, não a TRIAGEM.
6. Chave de BAA/FIA é composta: `(CD_HOSPITAL, DT_ANO_BAA, NR_BAA)`. Joins com eDoc usam `dt_ano_baa`+`nr_baa`.
7. `SYSDATE` do banco = hora local de Brasília (confirmado: bate com o relógio).

## Cadência sugerida

- **Tick rápido (60 s)** — Q1 "agora" (3 consultas baratas, <1 s cada).
- **Tick lento (10 min)** — Q2–Q6 (a Q6 com o join no `EDOC_MOVIMENTO` de 5M de linhas leva dezenas de segundos; nunca colocar no tick rápido). Rodar as consultas **sequencialmente**, nunca em paralelo — o Oracle é produção viva de hospital.

## Q1 — Agora (tick rápido)

```sql
-- Q1a: aguardando médico agora, por cor (chegada nas últimas 12h, sem saída, sem doc médico)
SELECT NVL(cr.ds_classificacao_risco,'SEM_CLASSIFICACAO') AS cor, COUNT(*) AS qtd,
       ROUND(AVG((SYSDATE - b.dt_chegada) * 1440), 0) AS min_medio_desde_chegada
  FROM infosaude.baa b
  LEFT JOIN infosaude.classificacao_risco cr ON cr.cd_classificacao_risco = b.cd_classificacao_risco
 WHERE b.cd_hospital = 1 AND b.in_emergencia = 'S'
   AND b.dt_chegada >= SYSDATE - 0.5
   AND b.dt_saida IS NULL
   AND NOT EXISTS (SELECT 1 FROM infosaude.edoc_movimento mv
                    WHERE mv.cd_hospital = 1 AND mv.cd_modelo IN (10036,10232,10014)
                      AND mv.dt_ano_baa = b.dt_ano_baa AND mv.nr_baa = b.nr_baa)
 GROUP BY cr.ds_classificacao_risco

-- Q1b: em atendimento/observação (mesma janela, COM doc médico, sem saída)
SELECT COUNT(*) AS em_atendimento
  FROM infosaude.baa b
 WHERE b.cd_hospital = 1 AND b.in_emergencia = 'S'
   AND b.dt_chegada >= SYSDATE - 0.5 AND b.dt_saida IS NULL
   AND EXISTS (SELECT 1 FROM infosaude.edoc_movimento mv
                WHERE mv.cd_hospital = 1 AND mv.cd_modelo IN (10036,10232,10014)
                  AND mv.dt_ano_baa = b.dt_ano_baa AND mv.nr_baa = b.nr_baa)

-- Q1c: internados agora + atendimentos/internações de hoje
SELECT (SELECT COUNT(*) FROM infosaude.fia f
         WHERE f.cd_hospital = 1 AND f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120)          AS internados_agora,
       (SELECT SUM(CASE WHEN f.id_internacao='U' THEN 1 ELSE 0 END) FROM infosaude.fia f
         WHERE f.cd_hospital = 1 AND f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120)          AS internados_urgencia,
       (SELECT ROUND(AVG(SYSDATE - f.dt_baixa), 1) FROM infosaude.fia f
         WHERE f.cd_hospital = 1 AND f.dt_alta IS NULL AND f.dt_baixa >= SYSDATE - 120)          AS media_dias_internado,
       (SELECT COUNT(*) FROM infosaude.baa b
         WHERE b.cd_hospital = 1 AND b.dt_atendimento >= TRUNC(SYSDATE))                          AS atendimentos_hoje,
       (SELECT COUNT(*) FROM infosaude.fia f
         WHERE f.cd_hospital = 1 AND f.dt_baixa >= TRUNC(SYSDATE))                                AS internacoes_hoje
  FROM dual
```

Resultado real 24/07 16h20: aguardando VERDE 46 / AZUL 10 / AMARELO 3 / SEM_CLASSIFICACAO 11; em atendimento 131; internados 164 (155 urgência, média 7,6 dias).

## Q2 — Atendimentos por período (tick lento)

Períodos: mês anterior `[TRUNC(ADD_MONTHS(SYSDATE,-1),'MM'), TRUNC(SYSDATE,'MM'))`, mês atual `[TRUNC(SYSDATE,'MM'), SYSDATE)`, hoje `[TRUNC(SYSDATE), SYSDATE)`.

```sql
SELECT COUNT(*) AS total FROM infosaude.baa b
 WHERE b.cd_hospital = 1 AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
```

**Média diária do mês atual = só dias completos**: total com `dt_atendimento < TRUNC(SYSDATE)` ÷ (dias corridos − 1). Mês anterior: total ÷ nº de dias do mês. Validado: jun 17.405 (580,2/dia); jul 01–23 13.117 (570,3/dia).

## Q3 — Série diária de atendimentos (35 dias, tick lento)

```sql
SELECT TRUNC(b.dt_atendimento) AS dia, COUNT(*) AS qtd
  FROM infosaude.baa b
 WHERE b.cd_hospital = 1 AND b.dt_atendimento >= TRUNC(SYSDATE) - 34
 GROUP BY TRUNC(b.dt_atendimento) ORDER BY 1
```

## Q4 — Atendimentos de hoje por hora (tick lento)

```sql
SELECT TO_NUMBER(TO_CHAR(b.dt_atendimento,'HH24')) AS hora, COUNT(*) AS qtd
  FROM infosaude.baa b
 WHERE b.cd_hospital = 1 AND b.dt_atendimento >= TRUNC(SYSDATE)
 GROUP BY TO_NUMBER(TO_CHAR(b.dt_atendimento,'HH24')) ORDER BY 1
```

## Q5 — Internações por período + série (tick lento)

```sql
-- por período (mesmos :ini/:fim da Q2, sobre dt_baixa)
SELECT COUNT(*) AS total,
       SUM(CASE WHEN f.id_internacao='U' THEN 1 ELSE 0 END) AS urgencia,
       SUM(CASE WHEN f.id_internacao='E' THEN 1 ELSE 0 END) AS eletiva
  FROM infosaude.fia f
 WHERE f.cd_hospital = 1 AND f.dt_baixa >= :ini AND f.dt_baixa < :fim

-- série diária 35 dias
SELECT TRUNC(f.dt_baixa) AS dia, COUNT(*) AS qtd
  FROM infosaude.fia f
 WHERE f.cd_hospital = 1 AND f.dt_baixa >= TRUNC(SYSDATE) - 34
 GROUP BY TRUNC(f.dt_baixa) ORDER BY 1
```

Validado: jun 739 (642 U / 97 E, 24,6/dia); jul 604 (522/82, 25,2/dia).

## Q6 — Espera por cor (tick lento — a consulta PESADA)

Rodar 1× por período (hoje / mês atual / mês anterior), substituindo `:ini`/`:fim` como na Q2. `:fim_doc` = `:fim + 3` (doc pode ser lavrado depois do fim do período).

```sql
WITH b AS (
  SELECT b.dt_ano_baa, b.nr_baa, b.cd_classificacao_risco,
         b.dt_chegada, b.dt_classifica_atual
    FROM infosaude.baa b
   WHERE b.cd_hospital = 1
     AND b.dt_atendimento >= :ini AND b.dt_atendimento < :fim
     AND b.in_emergencia = 'S'
), d AS (
  SELECT mv.dt_ano_baa, mv.nr_baa, MIN(mv.dt_inclusao) AS dt_med
    FROM infosaude.edoc_movimento mv
   WHERE mv.cd_hospital = 1
     AND mv.cd_modelo IN (10036, 10232, 10014)
     AND mv.dt_inclusao >= :ini AND mv.dt_inclusao < :fim_doc
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
```

**Observações**: cor SALUX (cd 11) — somar em SEM_CLASSIFICACAO ou omitir; N do VERMELHO é minúsculo (10–20/mês), o front deve exibir o N junto. Boletins sem `dt_classifica_atual` ficam fora (o contrato reporta só quem foi classificado; SEM_CLASSIFICACAO cobre quem tem carimbo de classificação sem cor).

## Substituição de parâmetros

Como no motor de indicadores do server (ADR-0022): `:ini`/`:fim` viram literais `DATE 'yyyy-mm-dd'` / expressões `TRUNC(SYSDATE...)` montadas pelo código — **nunca** texto vindo de fora. Não há input de usuário neste serviço.
