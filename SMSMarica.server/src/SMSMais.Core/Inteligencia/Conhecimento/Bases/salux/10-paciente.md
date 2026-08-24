# Paciente — tabela `PACIENTE`

Hub de identidade do cidadão no Salux. ~366.832 linhas, ~137 colunas.
PK: **`CD_PACIENTE`** (NUMBER). Ligação de qualquer atendimento ao paciente é por `CD_PACIENTE`
(no hub FHIR vira o identifier `urn:salux:cd_paciente`).

## Colunas-chave

| Coluna | Tipo | Significado |
|---|---|---|
| `CD_PACIENTE` | NUMBER | PK — código do paciente |
| `CD_PACIENTE_UNIFICADO` | NUMBER | Auto-referência: quando o cadastro foi fundido em outro, aponta para o paciente "real". Pode ser NULL. |
| `NM_PACIENTE` | VARCHAR2 | Nome completo |
| `DT_NASCIMENTO` | DATE | Data de nascimento <!-- verificar nome exato; usado por F_BUSCA_IDADE --> |
| `NR_CPF` | VARCHAR2 | CPF <!-- verificar: pode ser CD_CPF/NU_CPF --> |
| `NR_CNS` | VARCHAR2 | Cartão Nacional de Saúde (cartão SUS) <!-- verificar nome exato --> |
| `SEXO` | CHAR | Sexo (`M`/`F`) <!-- verificar: pode ser ID_SEXO/CD_SEXO --> |
| `NM_MAE` | VARCHAR2 | Nome da mãe (filiação) <!-- verificar --> |
| `CD_UF` / `CD_CIDADE` | — | Endereço — FK para `CIDADE` (PK composta UF+cidade; nome em `DS_CIDADE`) |
| `CD_PLANO_SAUDE` / `CD_TP_PLANO` | — | Convênio — FK para `TIPO_PLANO` / `PLANO_SAUDE` |
| `IN_VIP` | CHAR | Paciente VIP (S/N) <!-- verificar --> |

> A tabela tem ~137 colunas (vários endereços, filiação, cor/pele, nacionalidade, grau de instrução,
> religião, barreira de comunicação, prontuários SGH/CEM). Para texto→SQL clínico, normalmente bastam
> nome, nascimento, CPF, CNS e a ligação por `CD_PACIENTE`.

## Identificar / filtrar paciente

Por nome (use `UPPER` + `LIKE` para busca tolerante):
```sql
SELECT cd_paciente, nm_paciente, dt_nascimento
FROM   paciente
WHERE  UPPER(nm_paciente) LIKE UPPER('%' || :nome || '%')
  AND  cd_paciente_unificado IS NULL   -- só cadastros "raiz" (não fundidos)
FETCH FIRST 50 ROWS ONLY;
```

Por CPF:
```sql
SELECT cd_paciente, nm_paciente
FROM   paciente
WHERE  nr_cpf = :cpf;   -- <!-- verificar nome da coluna de CPF -->
```

Idade do paciente (função própria do Salux):
```sql
SELECT nm_paciente, F_BUSCA_IDADE(dt_nascimento, SYSDATE, 2) AS idade
FROM   paciente
WHERE  cd_paciente = :cd;
```

## Regra do paciente unificado nos joins

Ao juntar atendimentos (`BAA`/`FIA`) com `PACIENTE`, junte pelo paciente unificado:

```sql
SELECT p.nm_paciente, COUNT(*) AS qtd_atend
FROM   baa b
JOIN   paciente p
       ON p.cd_paciente = NVL(b.cd_paciente_unificado, b.cd_paciente)
GROUP  BY p.nm_paciente;
```

Se quiser contar pacientes distintos a partir de atendimentos, conte
`DISTINCT NVL(cd_paciente_unificado, cd_paciente)` — não `cd_paciente` cru.

## Lookup rápido de nome

O Salux usa muito `SELECT nm_paciente FROM paciente WHERE cd_paciente = :1` (1,3M execs/h observado).
É barato (acesso por PK). Para enriquecer resultados com o nome do paciente, junte com `PACIENTE`
pela PK em vez de subconsultas correlacionadas repetidas.
