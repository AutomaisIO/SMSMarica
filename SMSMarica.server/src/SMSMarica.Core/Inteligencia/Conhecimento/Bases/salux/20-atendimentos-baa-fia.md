# Atendimentos — `BAA` (ambulatório/urgência) e `FIA` (internação)

São os dois nós centrais do atendimento. **BAA** = Boletim de Atendimento Ambulatorial
(urgência/eletivo). **FIA** = Ficha de Internação Ambulatorial (internação completa).
Modelo é dual: muitas perguntas de "atendimentos" precisam de `UNION ALL` das duas.

## Chaves

- **BAA**: PK `(CD_HOSPITAL, DT_ANO_BAA, NR_BAA)`. ~1,7M linhas, ~138 colunas.
- **FIA**: PK `(CD_HOSPITAL, DT_ANO_FIA, NR_FIA)`. ~42K linhas, ~128 colunas.

`NR_BAA`/`NR_FIA` é sequencial por ano por hospital. **Sempre junte pelos 3 campos da chave.**

## BAA — colunas-chave

| Coluna | Significado |
|---|---|
| `CD_HOSPITAL`, `DT_ANO_BAA`, `NR_BAA` | PK composta |
| `CD_PACIENTE`, `CD_PACIENTE_UNIFICADO` | Paciente (use `NVL(cd_paciente_unificado, cd_paciente)`) |
| `CD_MEDICO` | Médico responsável → `MEDICO.CD_MEDICO` |
| `CD_CID` | Diagnóstico CID-10 → `CID` |
| `CD_ESPECIALIDADE` | Especialidade → `ESPECIALIDADE` |
| `DT_CHEGADA` / `DT_ATENDIMENTO` / `DT_SAIDA` | Marcos temporais do atendimento <!-- verificar nomes exatos --> |
| `CD_CLASSIFICACAO_RISCO` | Cor da triagem (Manchester) → `CLASSIFICACAO_RISCO` |
| `IN_EMERGENCIA` | Flag de emergência |
| `IN_BAA_ATENDIDO` | `N`=espera, `S`=atendido, `E`=em atendimento |
| `ID_DESTINO` | Destino pós-atendimento: `C`=casa/alta, `E`=encaminhado, `I`=internação, `D`=óbito, `T`=transferência |
| `CD_PLANO_SAUDE` / `CD_TP_PLANO` | Convênio |
| `TX_LAUDO` | Laudo/observação (texto) <!-- verificar --> |

## FIA — colunas-chave

| Coluna | Significado |
|---|---|
| `CD_HOSPITAL`, `DT_ANO_FIA`, `NR_FIA` | PK composta |
| `CD_PACIENTE`, `CD_PACIENTE_UNIFICADO` | Paciente |
| `CD_CID` (+ `CD_CID2`, `CD_CID3`) | Diagnósticos múltiplos <!-- verificar sufixos --> |
| `ID_INTERNACAO` | `U`=urgência (83%), `E`=eletiva (17%) |
| `DT_BAIXA` / `DT_ALTA` | Entrada/alta da internação <!-- verificar: doc menciona dt_baixa→dt_alta --> |
| `DT_PREVISAO_ALTA` | Previsão de alta |
| `CD_PROCEDIMENTO` | Procedimento principal |
| `NR_OBITO` | Número de óbito (preenchido se houve óbito) <!-- verificar --> |

Tabelas satélite da FIA: `FIA_MEDICO` (médico responsável ao longo do tempo, pegue o último por
`MAX(dt_inicio_atend)`), `FIA_LEITO` (histórico de leitos/transferências por `MAX(dt_transferencia)`),
`FIA_TIPO_PLANO` (plano vigente).

## Lookups relacionados

| Tabela | PK | Coluna de nome |
|---|---|---|
| `MEDICO` | `CD_MEDICO` | `NM_MEDICO` <!-- verificar --> (CRM em coluna própria, `NR_CRM`/`CD_CONSELHO`) |
| `FUNCIONARIO` | `CD_FUNCIONARIO` (VARCHAR2 8, alfanumérico) | `NM_FUNCIONARIO` |
| `CID` | `CD_CID` | `DS_CID` <!-- verificar --> (catálogo CID-10) |
| `ESPECIALIDADE` | `CD_ESPECIALIDADE` | `DS_ESPECIALIDADE` <!-- verificar --> |
| `UNIDADE_HOSPITALAR` | — | unidade funcional |
| `CLINICA` | `CD_CLINICA` | clínica/unidade funcional |
| `PLANO_SAUDE` / `TIPO_PLANO` | — | convênios |
| `CLASSIFICACAO_RISCO` | `CD_COR_RISCO` | cor da triagem |

## Exemplos

### Atendimentos ambulatoriais de um paciente, mais recentes primeiro
```sql
SELECT b.dt_atendimento,
       b.nr_baa || '/' || b.dt_ano_baa AS atendimento,
       c.ds_cid                        AS diagnostico,
       m.nm_medico
FROM   baa b
LEFT   JOIN cid c    ON c.cd_cid    = b.cd_cid
LEFT   JOIN medico m ON m.cd_medico = b.cd_medico
WHERE  NVL(b.cd_paciente_unificado, b.cd_paciente) = :cd_paciente
ORDER  BY b.dt_atendimento DESC;
```

### Atendimentos (BAA + FIA) por período — visão unificada
```sql
SELECT 'AMB' AS tipo, b.dt_atendimento AS dt, b.cd_medico,
       NVL(b.cd_paciente_unificado, b.cd_paciente) AS cd_paciente
FROM   baa b
WHERE  b.dt_atendimento >= TO_DATE('2024-05-01','YYYY-MM-DD')
  AND  b.dt_atendimento <  TO_DATE('2024-06-01','YYYY-MM-DD')
UNION ALL
SELECT 'INT' AS tipo, f.dt_baixa AS dt, f.cd_medico,
       NVL(f.cd_paciente_unificado, f.cd_paciente)
FROM   fia f
WHERE  f.dt_baixa >= TO_DATE('2024-05-01','YYYY-MM-DD')
  AND  f.dt_baixa <  TO_DATE('2024-06-01','YYYY-MM-DD');
```

### Quantidade de atendimentos ambulatoriais por médico, últimos 60 dias
```sql
SELECT m.nm_medico, COUNT(*) AS qtd
FROM   baa b
JOIN   medico m ON m.cd_medico = b.cd_medico
WHERE  b.dt_atendimento >= TRUNC(SYSDATE) - 60
GROUP  BY m.nm_medico
ORDER  BY qtd DESC;
```

### Atendimentos por mês no ano corrente
```sql
SELECT TO_CHAR(b.dt_atendimento,'YYYY-MM') AS mes, COUNT(*) AS qtd
FROM   baa b
WHERE  b.dt_atendimento >= TRUNC(SYSDATE,'YYYY')
GROUP  BY TO_CHAR(b.dt_atendimento,'YYYY-MM')
ORDER  BY mes;
```
