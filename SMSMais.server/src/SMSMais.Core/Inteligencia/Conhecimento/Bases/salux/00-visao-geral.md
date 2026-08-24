# Salux / INFOSAUDE — visão geral para texto→SQL

## O que é

O **Salux HIS** é o sistema hospitalar legado (desktop PowerBuilder + Oracle) da SMS Maricá,
usado no Hospital Maternal Conde Modesto Leal (HMCML), UPA Inoã e Posto Sta Rita.
O histórico clínico mora no schema **`INFOSAUDE`** (Oracle 12c).

- Banco: **Oracle 12c** (12.2), SID `ORASX01`, em `10.50.0.18:1521`.
- Schema lógico/owner físico das tabelas: **`INFOSAUDE`** (a aplicação conecta como `SYBSA`,
  que enxerga as tabelas via synonyms públicos). Ao gerar SQL, **não prefixe o owner** —
  use os nomes de tabela diretos (`PACIENTE`, `BAA`, `FIA`, `EDOC_MOVIMENTO`...).
- Dialeto: **Oracle SQL (PL/SQL)**. Não é Postgres nem SQL Server.

## Restrição absoluta: somente leitura

Todo SQL gerado deve ser **SELECT** (ou `WITH ... SELECT`). O banco é produção viva de hospital,
acessado por conta read-only (`salux_obs`, `SELECT_CATALOG_ROLE`). **Nunca** gerar
`INSERT`/`UPDATE`/`DELETE`/`MERGE`/DDL. Preferir consultas eficientes (algumas tabelas têm
dezenas/centenas de milhões de linhas — ver avisos por tabela).

## Convenções de modelagem (regras de ouro)

### 1. Chaves compostas temporais (sem ID surrogate global)
Atendimentos não têm um ID único global. A chave é **`(CD_HOSPITAL, DT_ANO_<X>, NR_<X>)`**:
- **BAA** (ambulatório/urgência): `(CD_HOSPITAL, DT_ANO_BAA, NR_BAA)`
- **FIA** (internação): `(CD_HOSPITAL, DT_ANO_FIA, NR_FIA)`

`NR_BAA`/`NR_FIA` é sequencial **por ano e por hospital** — duas FIAs com mesmo `NR_FIA` em anos
diferentes são atendimentos distintos. Sempre junte pelos **3 campos**.

`CD_HOSPITAL` é NUMBER (1 = HMCML, conforme capturas). `DT_ANO_BAA`/`DT_ANO_FIA` é o ano (NUMBER, ex. 2024).

### 2. Paciente unificado
Pacientes duplicados são fundidos. FIA/BAA carregam `CD_PACIENTE_UNIFICADO` além de `CD_PACIENTE`.
Para identificar o paciente "real", **sempre** use:

```sql
NVL(b.cd_paciente_unificado, b.cd_paciente)
```

Ao agrupar/contar por paciente ou juntar com `PACIENTE`, junte por esse `NVL(...)`.

### 3. Internação (FIA) vs Ambulatório (BAA) — modelo dual
Quase toda tabela clínica tem versão **FIA** (internação) e **BAA** (ambulatório/urgência):
`EVOLUCAO_PACIENTE_FIA`/`_BAA`, `PRESCRICAO_FIA`/`_BAA`, etc. Para "tudo do paciente" normalmente
é preciso **`UNION ALL`** das duas. Algumas tabelas usam discriminador inline `ID_TIPO` CHAR(1):
`F` = FIA/internação, `B` = BAA/ambulatório (ex. `SINAIS_VITAIS`).

### 4. Datas
Use `TO_DATE('dd/mm/yyyy','DD/MM/YYYY')` ou `TO_DATE('yyyy-mm-dd','YYYY-MM-DD')` para literais.
Datas são `DATE` (com hora). Para "data atual" use `SYSDATE`. Para "últimos N dias":
`coluna_data >= TRUNC(SYSDATE) - 60`. Para mês corrente:
`coluna_data >= TRUNC(SYSDATE,'MM')`. Para um mês específico:
`coluna_data >= TO_DATE('2024-05-01','YYYY-MM-DD') AND coluna_data < TO_DATE('2024-06-01','YYYY-MM-DD')`.

### 5. Limitar resultados (top-N)
- Oracle 12c suporta **`FETCH FIRST n ROWS ONLY`** (preferir para clareza):
  `... ORDER BY total DESC FETCH FIRST 5 ROWS ONLY`
- Alternativa clássica com `ROWNUM` (precisa subquery para ordenar antes):
  `SELECT * FROM (SELECT ... ORDER BY total DESC) WHERE ROWNUM <= 5`

### 6. Funções úteis
- `NVL(a, b)` — coalesce de 2 args.
- `TO_CHAR(data,'YYYY-MM')` — agrupar por mês.
- `TRUNC(data)` — zera a hora.
- Concatenação: `a || ' - ' || b`.

### 7. Funções PL/SQL próprias do Salux (existem no schema, podem ser usadas em SELECT)
- `F_BUSCA_IDADE(dt_nascimento, SYSDATE, 2)` — idade do paciente.
- `F_GET_NM_FUNCIONARIO(cd_funcionario)` — nome do funcionário.
- `F_BUSCA_HOSPITAL_INTERNACAO(...)` — hospital de internação.
Quando não tiver certeza da assinatura exata, prefira juntar diretamente com a tabela de lookup
(`FUNCIONARIO`, `MEDICO`) em vez de chamar a função.

## Mapa rápido das tabelas-âncora

| Assunto | Tabela(s) | Doc |
|---|---|---|
| Identidade do paciente | `PACIENTE` | `10-paciente.md` |
| Atendimento ambulatorial/urgência | `BAA` | `20-atendimentos-baa-fia.md` |
| Internação | `FIA` | `20-atendimentos-baa-fia.md` |
| Documentos clínicos (EAV) | `EDOC_MODELO`, `EDOC_MOVIMENTO`, `EDOC_MOVIMENTO_ITEM` | `30-edoc.md` |
| Prescrição / medicação | `PRESCRICAO_FIA/_BAA`, `MATMED`, `PRESC_FIA/BAA_OPC_PROD` | `40-prescricao-medicacao.md` |
| Lookups | `MEDICO`, `FUNCIONARIO`, `CID`, `CLINICA`, `ESPECIALIDADE`, `UNIDADE_HOSPITALAR`, `PLANO_SAUDE` | vários |

> Nomes de coluna marcados com `<!-- verificar -->` não foram confirmados diretamente
> nas capturas; validar antes de confiar.
