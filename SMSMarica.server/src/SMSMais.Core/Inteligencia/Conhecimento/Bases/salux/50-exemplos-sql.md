# Exemplos pergunta → SQL Oracle (Salux / INFOSAUDE)

Exemplos realistas baseados nas tabelas reais e nas queries capturadas em PROD.
Dialeto Oracle 12c. Lembretes: somente `SELECT`; paciente real via
`NVL(cd_paciente_unificado, cd_paciente)`; atendimento por chave composta
`(CD_HOSPITAL, DT_ANO_<X>, NR_<X>)`; top-N com `FETCH FIRST n ROWS ONLY`.

---

### P: "5 medicamentos mais usados"
```sql
SELECT m.ds_material, COUNT(*) AS qtd
FROM   (
         SELECT cd_material, cd_hospital, dt_ano_fia AS dt_ano, nr_fia AS nr, nr_prescricao
         FROM   presc_fia_opc_prod
         UNION ALL
         SELECT cd_material, cd_hospital, dt_ano_baa, nr_baa, nr_prescricao
         FROM   presc_baa_opc_prod
       ) itens
JOIN   matmed m ON m.cd_material = itens.cd_material
GROUP  BY m.ds_material
ORDER  BY qtd DESC
FETCH FIRST 5 ROWS ONLY;
```
> Para grandes volumes prefira filtrar por período juntando ao cabeçalho
> (`prescricao_fia`/`prescricao_baa`) — ver `40-prescricao-medicacao.md`.

---

### P: "Médicos mais ativos nos últimos 60 dias"
```sql
SELECT m.nm_medico, COUNT(*) AS qtd_atendimentos
FROM   (
         SELECT cd_medico, dt_atendimento AS dt FROM baa
         UNION ALL
         SELECT cd_medico, dt_baixa        AS dt FROM fia
       ) at
JOIN   medico m ON m.cd_medico = at.cd_medico
WHERE  at.dt >= TRUNC(SYSDATE) - 60
GROUP  BY m.nm_medico
ORDER  BY qtd_atendimentos DESC
FETCH FIRST 10 ROWS ONLY;
```

---

### P: "Atendimentos por unidade no mês corrente"
```sql
SELECT u.ds_unidade, COUNT(*) AS qtd        -- <!-- verificar ds_unidade -->
FROM   baa b
JOIN   unidade_hospitalar u ON u.cd_unidade = b.cd_unidade   -- <!-- verificar cd_unidade em BAA -->
WHERE  b.dt_atendimento >= TRUNC(SYSDATE,'MM')
GROUP  BY u.ds_unidade
ORDER  BY qtd DESC;
```

---

### P: "Quantos atendimentos teve o paciente X (por CPF)"
```sql
SELECT COUNT(*) AS total
FROM   (
         SELECT NVL(b.cd_paciente_unificado, b.cd_paciente) AS pac FROM baa b
         UNION ALL
         SELECT NVL(f.cd_paciente_unificado, f.cd_paciente)      FROM fia f
       ) at
WHERE  at.pac IN (
         SELECT cd_paciente FROM paciente WHERE nr_cpf = :cpf   -- <!-- verificar nr_cpf -->
       );
```

---

### P: "Atendimentos de urgência por dia na última semana"
```sql
SELECT TRUNC(b.dt_atendimento) AS dia, COUNT(*) AS qtd
FROM   baa b
WHERE  b.in_emergencia = 'S'                  -- <!-- verificar valor/flag -->
  AND  b.dt_atendimento >= TRUNC(SYSDATE) - 7
GROUP  BY TRUNC(b.dt_atendimento)
ORDER  BY dia;
```

---

### P: "Top 10 diagnósticos (CID) no ano corrente"
```sql
SELECT c.cd_cid, c.ds_cid, COUNT(*) AS qtd
FROM   baa b
JOIN   cid c ON c.cd_cid = b.cd_cid
WHERE  b.dt_atendimento >= TRUNC(SYSDATE,'YYYY')
  AND  b.cd_cid IS NOT NULL
GROUP  BY c.cd_cid, c.ds_cid
ORDER  BY qtd DESC
FETCH FIRST 10 ROWS ONLY;
```

---

### P: "Documentos eletrônicos por modelo nos últimos 12 meses (top 30)"
```sql
SELECT md.ds_modelo, COUNT(*) AS usos
FROM   edoc_movimento mo
JOIN   edoc_modelo    md ON md.cd_modelo = mo.cd_modelo
WHERE  mo.in_status = 'D'
  AND  mo.dt_inclusao >= ADD_MONTHS(TRUNC(SYSDATE),-12)
GROUP  BY md.ds_modelo
ORDER  BY usos DESC
FETCH FIRST 30 ROWS ONLY;
```

---

### P: "Atestados médicos emitidos neste mês"
```sql
SELECT mo.dt_inclusao, p.nm_paciente, F_GET_NM_FUNCIONARIO(mo.cd_funcionario_inc) AS profissional
FROM   edoc_movimento mo
JOIN   paciente p ON p.cd_paciente = mo.cd_paciente
WHERE  mo.cd_modelo = 10046          -- Atestado Médico - HMCML
  AND  mo.in_status = 'D'
  AND  mo.dt_inclusao >= TRUNC(SYSDATE,'MM')
ORDER  BY mo.dt_inclusao DESC;
```

---

### P: "Internações com óbito no ano corrente"
```sql
SELECT f.dt_baixa, p.nm_paciente, c.ds_cid
FROM   fia f
JOIN   paciente p ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
LEFT   JOIN cid c ON c.cd_cid = f.cd_cid
WHERE  f.nr_obito IS NOT NULL         -- <!-- verificar nr_obito -->
  AND  f.dt_baixa >= TRUNC(SYSDATE,'YYYY')
ORDER  BY f.dt_baixa DESC;
```

---

### P: "Pacientes internados atualmente (sem alta)"
```sql
SELECT p.nm_paciente, f.dt_baixa, f.nr_fia, f.dt_ano_fia
FROM   fia f
JOIN   paciente p ON p.cd_paciente = NVL(f.cd_paciente_unificado, f.cd_paciente)
WHERE  f.dt_alta IS NULL              -- <!-- verificar dt_alta -->
ORDER  BY f.dt_baixa DESC;
```

---

### P: "Tempo médio de espera no ambulatório por dia (se houver coluna de minutos)"
> A query da lista de pacientes por setor calcula `qt_minutos_espera`/`qt_minutos_triagem`.
> Se houver coluna persistida, agrupe por dia; senão, derive de `dt_chegada`/`dt_atendimento`:
```sql
SELECT TRUNC(b.dt_chegada) AS dia,
       ROUND(AVG((b.dt_atendimento - b.dt_chegada) * 24 * 60), 1) AS media_min_espera
FROM   baa b
WHERE  b.dt_chegada >= TRUNC(SYSDATE) - 30
  AND  b.dt_atendimento IS NOT NULL
GROUP  BY TRUNC(b.dt_chegada)
ORDER  BY dia;
```
> Subtração de `DATE` em Oracle dá dias; multiplicar por `24*60` converte para minutos.
> Nomes `dt_chegada`/`dt_atendimento` marcados para `<!-- verificar -->`.

---

### P: "Solicitações de exame de um paciente, com situação"
```sql
SELECT se.dt_compromisso, sei.cd_procedimento, pr.ds_procedimento, sei.id_situacao_exame
FROM   sol_exame se
JOIN   sol_exame_item sei ON sei.... = se....     -- <!-- verificar chave de junção SOL_EXAME ⋈ SOL_EXAME_ITEM -->
LEFT   JOIN procedimento pr ON pr.cd_procedimento = sei.cd_procedimento
WHERE  se.cd_paciente = :cd_paciente             -- <!-- verificar cd_paciente em SOL_EXAME -->
ORDER  BY se.dt_compromisso DESC;
```
> `id_situacao_exame` ∈ {SO=solicitado, CO=concluído, AE/IE...} conforme captura. Junção
> `SOL_EXAME ⋈ SOL_EXAME_ITEM` precisa verificação do nome da chave.

---

### P: "Evolução clínica de uma internação"
```sql
SELECT epf.dt_evolucao,
       F_GET_NM_FUNCIONARIO(epf.cd_funcionario) AS profissional,
       epf.tx_evolucao
FROM   evolucao_paciente_fia epf
WHERE  epf.cd_hospital = :cd_hospital
  AND  epf.dt_ano_fia  = :ano
  AND  epf.nr_fia      = :nr_fia
ORDER  BY epf.dt_evolucao DESC;
```
> `tx_evolucao` é LONG (texto). Pode conter RTF/HTML (campos RichText do PowerBuilder).

---

## Checklist antes de entregar um SELECT

1. É só `SELECT`/`WITH` (nada de escrita)?
2. Junções a `BAA`/`FIA` usam os **3 campos** da chave composta?
3. Paciente resolvido por `NVL(cd_paciente_unificado, cd_paciente)`?
4. Datas com `TO_DATE`/`TRUNC`/`SYSDATE` corretos?
5. Top-N com `FETCH FIRST` (ou `ROWNUM` em subquery ordenada)?
6. Evitou varrer `EDOC_MOVIMENTO_ITEM` (166M) sem filtro de chave?
7. Documentos eDoc filtrados por `IN_STATUS='D'` e `IN_ATIVO='S'` quando se quer "vigentes"?
