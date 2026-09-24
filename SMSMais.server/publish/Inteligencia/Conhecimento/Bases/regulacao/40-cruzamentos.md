# Cruzamentos e exemplos de SQL (Postgres)

## Fila interna de um procedimento, com espera e risco

```sql
SELECT count(*) AS na_fila,
       percentile_cont(0.5) WITHIN GROUP (ORDER BY current_date - data_solicitacao) AS espera_mediana_dias,
       max(current_date - data_solicitacao) AS espera_maxima_dias,
       count(*) FILTER (WHERE risco = 0) AS vermelhos
FROM smsmarica.sisreg_fila_pendente
WHERE saiu_em IS NULL
  AND smsmarica.unaccent(lower(procedimento_nome)) LIKE '%ecocardiograma%';
```

## Fila externa (SER + SERNIT) do mesmo assunto

```sql
SELECT 'SER' AS sistema, count(*) AS na_fila,
       percentile_cont(0.5) WITHIN GROUP (ORDER BY current_date - data_solicitacao) AS espera_mediana_dias
FROM smsmarica.ser_solicitacao
WHERE excluido_em IS NULL AND situacao IN (1, 2)
  AND smsmarica.unaccent(lower(recurso)) LIKE '%cardiolog%'
UNION ALL
SELECT 'SERNIT', count(*),
       percentile_cont(0.5) WITHIN GROUP (ORDER BY current_date - data_solicitacao)
FROM smsmarica.sernit_solicitacao
WHERE excluido_em IS NULL AND situacao IN (1, 2)
  AND smsmarica.unaccent(lower(recurso)) LIKE '%cardiolog%';
```

## Mesmo paciente na fila interna e na externa

A fila do SISREG só tem CNS; SER/SERNIT têm `paciente_id` (cadastro único).

```sql
SELECT count(DISTINCT p.id) AS pacientes_nas_duas
FROM smsmarica.sisreg_fila_pendente f
JOIN fhir.patient p ON p.cns = f.cns
JOIN smsmarica.ser_solicitacao s ON s.paciente_id = p.id AND s.excluido_em IS NULL AND s.situacao IN (1, 2)
WHERE f.saiu_em IS NULL;
```

## Histórico completo de regulação de um paciente (interno + externo)

Localize o paciente primeiro (`fhir.patient` por CPF ou CNS, `LIMIT 5`), depois:

```sql
SELECT 'SISREG agendado' AS origem, s.procedimento_texto AS o_que, s.data_solicitacao,
       (s.data_agendada AT TIME ZONE 'America/Sao_Paulo') AS quando, s.status::text AS situacao
FROM smsmarica.solicitacao s WHERE s.paciente_id = :id AND s.excluido_em IS NULL
UNION ALL
SELECT 'SISREG fila', f.procedimento_nome, f.data_solicitacao, NULL, f.situacao
FROM smsmarica.sisreg_fila_pendente f
JOIN fhir.patient p ON p.cns = f.cns WHERE p.id = :id AND f.saiu_em IS NULL
UNION ALL
SELECT 'SER', r.recurso, r.data_solicitacao, NULL, r.situacao::text
FROM smsmarica.ser_solicitacao r WHERE r.paciente_id = :id AND r.excluido_em IS NULL
UNION ALL
SELECT 'SERNIT', n.recurso, n.data_solicitacao, NULL, n.situacao::text
FROM smsmarica.sernit_solicitacao n WHERE n.paciente_id = :id AND n.excluido_em IS NULL
ORDER BY 3 DESC
LIMIT 100;
```
(Substitua `:id` pelo `fhir.patient.id` encontrado — a ferramenta não aceita parâmetros.)

## Agendamentos por unidade executante no mês

```sql
SELECT u.nome, count(*) AS agendamentos
FROM smsmarica.solicitacao s
JOIN smsmarica.unidade u ON u.id = s.unidade_executante_id
WHERE s.excluido_em IS NULL AND s.status IN (2, 3)
  AND (s.data_agendada AT TIME ZONE 'America/Sao_Paulo')::date
      BETWEEN date_trunc('month', current_date)::date AND (date_trunc('month', current_date) + interval '1 month - 1 day')::date
GROUP BY u.nome ORDER BY 2 DESC LIMIT 30;
```

## Tempo médio entre o pedido e a data marcada (SISREG), por procedimento

```sql
SELECT s.procedimento_texto,
       count(*) AS agendamentos,
       percentile_cont(0.5) WITHIN GROUP (
         ORDER BY (s.data_agendada AT TIME ZONE 'America/Sao_Paulo')::date - s.data_solicitacao) AS dias_mediana
FROM smsmarica.solicitacao s
WHERE s.excluido_em IS NULL AND s.data_solicitacao IS NOT NULL AND s.data_agendada IS NOT NULL
  AND s.data_agendada >= now() - interval '180 days'
GROUP BY 1 HAVING count(*) >= 20 ORDER BY 3 DESC LIMIT 20;
```

## Vagas de regulação vigentes por procedimento (escalas)

```sql
SELECT procedimento_nome, sum(vagas_primeira_vez + vagas_reserva) AS vagas_por_semana,
       count(DISTINCT unidade_id) AS unidades
FROM smsmarica.sisreg_escala
WHERE status = 1 AND NOT ausente AND NOT agenda_local
  AND current_date BETWEEN vigencia_inicio AND vigencia_fim
GROUP BY 1 ORDER BY 2 DESC LIMIT 30;
```

## Quanto da fila saiu agendada (últimos 30 dias)

```sql
SELECT saiu_para, count(*) FROM smsmarica.sisreg_fila_pendente
WHERE saiu_em >= now() - interval '30 days' GROUP BY 1;
-- 1 = agendada, 2 = saiu sem agendar
```
