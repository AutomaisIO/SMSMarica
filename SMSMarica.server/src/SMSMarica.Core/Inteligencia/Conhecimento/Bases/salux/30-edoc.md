# eDoc — documentos clínicos eletrônicos (modelo EAV)

**Tudo que é "documento" no Salux é um eDoc**: Boletim de Atendimento, evolução, receituário,
atestado, encaminhamento, escala clínica (Glasgow, Braden, Morse, Fugulin...), sumário de óbito,
SINAN, AIH, etc. São **333 modelos** ativos.

O eDoc é um **EAV (Entity-Attribute-Value)**: cada documento preenchido é 1 linha em
`EDOC_MOVIMENTO`; cada campo respondido é 1 linha em `EDOC_MOVIMENTO_ITEM`, com o valor sempre
em `DS_RESPOSTA` VARCHAR2(4000) — mesmo datas, números e booleanos viram string.

## Aviso de volume (crítico)

- `EDOC_MOVIMENTO` ≈ **5.034.386** linhas.
- `EDOC_MOVIMENTO_ITEM` ≈ **166.090.964** linhas. **Nunca varrer sem filtro.**

Sempre filtre `EDOC_MOVIMENTO_ITEM` por uma chave de movimento específica (paciente/atendimento/modelo)
e por status definitivo. Não faça `GROUP BY` global sobre os 166M.

## Tabelas — catálogo (definição dos templates)

| Tabela | Função |
|---|---|
| `EDOC_CATEGORIA` (6) | Categorias macro (Urgência, Eletivo, Médicos, Enfermagem, Multi, Administrativos) |
| `EDOC_GRUPO_MODELO` (51) | Subgrupos por unidade/especialidade (HMCML, UPA Inoã, Sta Rita, COVID, SINAN...) |
| `EDOC_MODELO` (333) | **Os 333 tipos de documento** (templates). PK `CD_MODELO`. `DS_MODELO` = nome. |
| `EDOC_DOCUMENTO` | Versões/instâncias do template. `CD_DOCUMENTO` |
| `EDOC_DOCUMENTO_ITEM` | Definição dos campos de cada documento (ordem/seq). `CD_ITEM` |
| `EDOC_ITEM` (4.272) | Catálogo global de campos reutilizáveis. `CD_ITEM`, referencia `EDOC_TIPO_ITEM` |
| `EDOC_TIPO_ITEM` (18) | Tipo de widget (Data, CheckBox, DropDown, ListBox, Texto, RichText, Numérico...) |
| `EDOC_ITEM_VALORES` | Opções predefinidas de listbox/dropdown |

## Tabelas — movimento (documentos preenchidos)

### `EDOC_MOVIMENTO` — 1 linha por documento preenchido
PK: `(CD_HOSPITAL, ANO_MOVIMENTO, ID_MOVIMENTO)`. Colunas-chave:

| Coluna | Significado |
|---|---|
| `CD_HOSPITAL`, `ANO_MOVIMENTO`, `ID_MOVIMENTO` | PK do movimento |
| `CD_MODELO` | Qual template → `EDOC_MODELO.CD_MODELO` |
| `CD_DOCUMENTO` | Versão do documento |
| `CD_PACIENTE` | Paciente |
| `FIA_CD_HOSPITAL`, `DT_ANO_FIA`, `NR_FIA` | Vínculo à internação (quando aplicável) |
| `BAA_CD_HOSPITAL`, `DT_ANO_BAA`, `NR_BAA` | Vínculo ao ambulatório (quando aplicável) |
| `CD_UNIDADE` | Unidade |
| `DT_INCLUSAO` | Data/hora de criação do documento |
| `CD_FUNCIONARIO_INC` | Funcionário que incluiu → `FUNCIONARIO` |
| `IN_STATUS` | `D`=Definitivo (97,5%), `P`=Parcial/rascunho (2,5%) |
| `IN_ATIVO` | `S`=ativo, `N`=inativado |
| `DOC_ASSINADO` (BLOB) | PDF assinado (lazy — não selecionar em listagens) |
| `HASH_ASSINATURA_DIGITAL` | Hash da assinatura |

### `EDOC_MOVIMENTO_ITEM` — 1 linha por campo respondido (EAV, 166M)
PK: `(CD_HOSPITAL, ANO_MOVIMENTO, ID_MOVIMENTO, SEQ_DOCTO, CD_MODELO, CD_DOCUMENTO, CD_ITEM)`.

| Coluna | Significado |
|---|---|
| chaves acima | identificam o movimento + o campo |
| `CD_ITEM` | Qual campo → `EDOC_ITEM` / `EDOC_DOCUMENTO_ITEM` (label em `DS_ITEM` <!-- verificar -->) |
| `DS_RESPOSTA` | **O valor preenchido** (string genérica VARCHAR2 4000) |
| `CD_ITEM_GRUPO` | Grupo do item |

### `EDOC_MOVIMENTO_ITEM_LISTBOX`
Respostas de campos do tipo ListBox (quando uma resposta é multi-valor).

## Como consultar respostas

### Listar documentos de um paciente (definitivos), sem tocar nos itens
```sql
SELECT mo.dt_inclusao,
       md.ds_modelo,
       mo.id_movimento,
       mo.ano_movimento
FROM   edoc_movimento mo
JOIN   edoc_modelo    md ON md.cd_modelo = mo.cd_modelo
WHERE  mo.cd_paciente = :cd_paciente
  AND  mo.in_status = 'D'
  AND  mo.in_ativo  = 'S'
ORDER  BY mo.dt_inclusao DESC;
```

### Ler as respostas de UM documento específico (chave completa)
```sql
SELECT i.cd_item, i.ds_resposta
FROM   edoc_movimento_item i
WHERE  i.cd_hospital   = :cd_hospital
  AND  i.ano_movimento = :ano
  AND  i.id_movimento  = :id
ORDER  BY i.seq_docto, i.cd_item;
```

### Documentos de um modelo específico num período (ex.: Atestados — modelo 10046)
```sql
SELECT mo.dt_inclusao, mo.cd_paciente, mo.id_movimento
FROM   edoc_movimento mo
WHERE  mo.cd_modelo = 10046           -- Atestado Médico - HMCML
  AND  mo.in_status = 'D'
  AND  mo.dt_inclusao >= TRUNC(SYSDATE,'MM')
ORDER  BY mo.dt_inclusao DESC;
```

### Pivotar respostas-chave de um documento (EAV → colunas)
Para escalas como Glasgow, os campos vêm como linhas; use `MAX(CASE ...)` por `cd_item`:
```sql
SELECT mo.id_movimento, mo.dt_inclusao,
       MAX(CASE WHEN i.cd_item = 775 THEN i.ds_resposta END) AS total_glasgow,
       MAX(CASE WHEN i.cd_item = 776 THEN i.ds_resposta END) AS resultado
FROM   edoc_movimento mo
JOIN   edoc_movimento_item i
       ON  i.cd_hospital   = mo.cd_hospital
       AND i.ano_movimento = mo.ano_movimento
       AND i.id_movimento  = mo.id_movimento
WHERE  mo.cd_modelo = 10029           -- Escala de Glasgow
  AND  mo.in_status = 'D'
  AND  mo.cd_paciente = :cd_paciente
GROUP  BY mo.id_movimento, mo.dt_inclusao
ORDER  BY mo.dt_inclusao DESC;
```
> `cd_item` muda por modelo. Os números (775, 776) acima são do modelo 10029 (Glasgow); para outros
> modelos, descubra os `cd_item` via `EDOC_DOCUMENTO_ITEM` do documento ou marque `<!-- verificar -->`.

## Modelos mais usados (úteis como exemplos de `cd_modelo`)

| CD_MODELO | Modelo |
|---:|---|
| 10036 | Boletim de Atendimento de Urgência - HMCML |
| 10037 | Receituário Médico Simples - HMCML |
| 10046 | Atestado Médico - HMCML |
| 10029 | Escala de Glasgow |
| 10146 | Escala de Braden - HMCML |
| 10145 | Escala de Morse - HMCML |
| 10039 | Laudo AIH |
| 10047 | Resumo de Alta |
| 10277 | Sumário de Óbito - HMCML |
| 10222 | SINAN - Ficha de notificação |

Para contar uso de modelos, conte em `EDOC_MOVIMENTO` (5M, viável), **não** em `EDOC_MOVIMENTO_ITEM`:
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
