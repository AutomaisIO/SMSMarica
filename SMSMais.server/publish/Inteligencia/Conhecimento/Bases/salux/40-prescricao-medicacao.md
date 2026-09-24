# Prescrição e medicação

Prescrição também é dual (FIA = internação, BAA = ambulatório). O **catálogo** de
material/medicamento é `MATMED`. Os itens prescritos ficam em tabelas `PRESC_*_OPC_PROD`
ligadas ao cabeçalho da prescrição e ao `MATMED`.

## Tabelas

| Tabela | Linhas (aprox) | Função |
|---|---:|---|
| `PRESCRICAO_FIA` | 752.140 | Cabeçalho de prescrição em internação |
| `PRESCRICAO_BAA` | 1.800.309 | Cabeçalho de prescrição ambulatorial |
| `PRESC_FIA_OPC_PROD` | — | Itens (produtos/medicamentos) da prescrição FIA |
| `PRESC_BAA_OPC_PROD` | — | Itens da prescrição BAA |
| `MATMED` | — | **Catálogo de material/medicamento** (PK `CD_MATERIAL`) |
| `PRESCRICAO_FIA_CONDUTA` | 2.511.874 | Itens com horário/intervalo (internação) |
| `PRESCRICAO_BAA_CONDUTA` | 242.327 | Condutas BAA |
| `MODELO_PRESCRICAO` | 2.562 | Templates de prescrição |

## Chaves

- Cabeçalho da prescrição FIA: `(CD_HOSPITAL, DT_ANO_FIA, NR_FIA, NR_PRESCRICAO)`.
- Cabeçalho da prescrição BAA: `(CD_HOSPITAL, DT_ANO_BAA, NR_BAA, NR_PRESCRICAO)` <!-- verificar -->.
- Itens (`PRESC_*_OPC_PROD`) juntam ao cabeçalho por essas chaves e ao `MATMED` por `CD_MATERIAL`.

## `ID_TIPO_PRESCRICAO` (em PRESCRICAO_FIA / PRESCRICAO_BAA)

| Código | Significado |
|:---:|---|
| `M` | Médica (maioria) |
| `F` | Farmacêutica/Enfermagem |
| `O` | Odontológica (conselho CRO) |
| `L` | Multiprofissional/Livre (residual) |

## Colunas-chave dos itens (`PRESC_FIA_OPC_PROD` / `PRESC_BAA_OPC_PROD`)

- `CD_MATERIAL` → `MATMED.CD_MATERIAL`
- `DS_MATERIAL` — descrição do medicamento (pode estar no `MATMED`)
- `QT_MATERIAL_PRESCRITA`, `QT_MATERIAL_SOLICITADA`
- `CD_VIA` (via de administração), `CD_DILUICAO`, `CD_INTERVALO`, `CD_HORARIO`, `CD_UNIDADE_MEDIDA`
- `HORA_INICIAL`, `HORA_FINAL`, `QT_VEZES`
- `DT_INICIO_APRAZ`, `DS_HORARIO`
- `IN_URGENCIA`, `OBSERVACAO`, `IN_CRITERIO_MEDICO`, `IN_OPCIONAL`

Do `MATMED`: `IN_CONTROLADO`, `IN_ALTO_RISCO`, `IN_FRACIONADO` etc.

## Exemplos

### 5 medicamentos mais prescritos (últimos 12 meses, internação + ambulatório)
```sql
SELECT m.cd_material, m.ds_material, COUNT(*) AS qtd_prescricoes
FROM   (
         SELECT opc.cd_material
         FROM   presc_fia_opc_prod opc
         JOIN   prescricao_fia pf
                ON  pf.cd_hospital   = opc.cd_hospital
                AND pf.dt_ano_fia    = opc.dt_ano_fia
                AND pf.nr_fia        = opc.nr_fia
                AND pf.nr_prescricao = opc.nr_prescricao
         WHERE  pf.dt_prescricao >= ADD_MONTHS(TRUNC(SYSDATE),-12)   -- <!-- verificar nome dt_prescricao -->
         UNION ALL
         SELECT opc.cd_material
         FROM   presc_baa_opc_prod opc
         JOIN   prescricao_baa pb
                ON  pb.cd_hospital   = opc.cd_hospital
                AND pb.dt_ano_baa    = opc.dt_ano_baa
                AND pb.nr_baa        = opc.nr_baa
                AND pb.nr_prescricao = opc.nr_prescricao
         WHERE  pb.dt_prescricao >= ADD_MONTHS(TRUNC(SYSDATE),-12)
       ) itens
JOIN   matmed m ON m.cd_material = itens.cd_material
GROUP  BY m.cd_material, m.ds_material
ORDER  BY qtd_prescricoes DESC
FETCH FIRST 5 ROWS ONLY;
```

### Medicamentos prescritos para um atendimento (internação) específico
```sql
SELECT m.ds_material, opc.qt_material_prescrita, opc.ds_horario
FROM   presc_fia_opc_prod opc
JOIN   prescricao_fia pf
       ON  pf.cd_hospital   = opc.cd_hospital
       AND pf.dt_ano_fia    = opc.dt_ano_fia
       AND pf.nr_fia        = opc.nr_fia
       AND pf.nr_prescricao = opc.nr_prescricao
JOIN   matmed m ON m.cd_material = opc.cd_material
WHERE  pf.cd_hospital = :cd_hospital
  AND  pf.dt_ano_fia  = :ano
  AND  pf.nr_fia      = :nr_fia
ORDER  BY pf.nr_prescricao;
```

### Medicamentos controlados mais prescritos (ambulatório, últimos 30 dias)
```sql
SELECT m.ds_material, COUNT(*) AS qtd
FROM   presc_baa_opc_prod opc
JOIN   prescricao_baa pb
       ON  pb.cd_hospital   = opc.cd_hospital
       AND pb.dt_ano_baa    = opc.dt_ano_baa
       AND pb.nr_baa        = opc.nr_baa
       AND pb.nr_prescricao = opc.nr_prescricao
JOIN   matmed m ON m.cd_material = opc.cd_material
WHERE  m.in_controlado = 'S'
  AND  pb.dt_prescricao >= TRUNC(SYSDATE) - 30
GROUP  BY m.ds_material
ORDER  BY qtd DESC
FETCH FIRST 10 ROWS ONLY;
```

> Vários nomes de coluna de data/quantidade no cabeçalho (`DT_PRESCRICAO`, `NR_PRESCRICAO`) e nos
> itens são inferidos das capturas — onde marcado `<!-- verificar -->`, confirmar antes de confiar.
