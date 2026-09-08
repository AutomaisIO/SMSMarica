# Inventário completo do Salux — todas as camadas de material

Extração de **todo o material cadastrado e em estoque no Salux (schema `INFOSAUDE`)**, em arquivo
único, para carga em outro prontuário/ERP. Cobre os **3 hospitais** que existem no Salux
(Conde Modesto Leal, UPA 24h Inoã, PA 24h Santa Rita).

- Gerado por: `Salux/scripts/exportar_inventario_completo.py` (roda **no droplet**, via proxy SQL
  interno, somente leitura — o Oracle do hospital só é alcançável de lá).
- Saídas (em `Salux/capturas/estoque/`, gitignored):
  - `inventario_salux_completo.csv` — 66.089 linhas, todas as camadas
  - `inventario_salux_resumo.csv` — totais por camada × hospital × tipo de item
  - `inventario_salux_divergencias.csv` — onde a soma dos lotes não bate com o saldo do almoxarifado
- CSV `;` (ponto-e-vírgula), UTF-8 com BOM (abre direto no Excel pt-BR).

## 1. As camadas (coluna `ORIGEM_TABELA` + `TIPO_REGISTRO`)

A mesma unidade de material aparece em mais de uma tabela do Salux, com granularidades
diferentes. Por isso **cada linha diz de qual tabela veio** e se entra ou não no total —
a coluna `CONTA_NO_TOTAL` existe justamente para impedir dupla contagem.

| ORIGEM_TABELA | TIPO_REGISTRO | Linhas | Conta? | O que é |
|---|---|---:|:--:|---|
| `MATMED_LOTE` | `SALDO_LOTE` | 11.660 | **S** | Saldo por hospital × almoxarifado × material × **lote/validade**. É o grão mais fino. |
| `MATMED_ESTOQUE` | `SALDO_SEM_LOTE` | 1.935 | **S** | Saldo de item que **não tem controle de lote** — só aparece aqui. |
| `MATMED_ESTOQUE` | `SALDO_ESTOQUE_COM_LOTE` | 6.432 | N | Mesmo saldo já detalhado em `SALDO_LOTE`. Serve para **conferir** lote × almoxarifado. |
| `MATMED_ESTOQUE` | `CONSIGNADO` | 285 | N | Material de terceiro em consignação (está fisicamente lá, não é patrimônio próprio). |
| `MATMED_HOSPITAL` | `CONSOLIDADO_HOSPITAL` | 3.110 | N | Totalizador do próprio Salux por hospital + **custo médio oficial**. Referência de conferência. |
| `MATMED` | `CATALOGO` | 2.316 | N | **Cadastro completo de itens**, com ou sem saldo — é o que alimenta a tabela de materiais do sistema novo. |
| `EMPRESTIMO_ITEM` | `EMPRESTIMO_SALDO` | 16.115 | N | Itens movimentados em empréstimo a/de terceiros, com data e nome do terceiro. Ver armadilha (4). |
| `MATMED_LOTE_ENT` | `ENTRADA_FIFO_SALDO` | 11.395 | N | Saldo residual por **entrada** (rastreio FIFO). A tabela **não guarda hospital/almoxarifado**. |
| `INVENTARIO_ITEM` | `ULTIMA_CONTAGEM` | 12.841 | N | Última **contagem física** por hospital × almoxarifado × material, com a data. |

**Regra de ouro:** o inventário para carga é `CONTA_NO_TOTAL = 'S'` — 13.595 linhas,
1.766 materiais distintos, **R$ 7.879.419,96**. Todo o resto é catálogo, conferência ou rastreio.

## 2. Conferência contra o próprio Salux

Somando só as linhas que contam, contra o totalizador `MATMED_HOSPITAL` do Salux:

| Hospital | Camadas de saldo | Totalizador Salux | Diferença |
|---|---:|---:|---:|
| Conde Modesto Leal | 3.789.363,45 | 3.776.485,37 | +12.878,08 (+0,34%) |
| PA 24h Santa Rita | 1.355.176,89 | 1.372.508,53 | −17.331,64 (−1,26%) |
| UPA 24h Inoã | 2.734.879,62 | 2.754.734,08 | −19.854,46 (−0,72%) |

A diferença é a divergência interna do próprio Salux entre a soma dos lotes e o saldo do
almoxarifado — **46 pares item × almoxarifado, R$ 40.079,45 de impacto**, listados em
`inventario_salux_divergencias.csv`. Só contagem física resolve; nenhuma consulta desempata.

## 3. Dicionário de colunas (36)

| Coluna | Conteúdo |
|---|---|
| `ORIGEM_TABELA` / `TIPO_REGISTRO` / `CONTA_NO_TOTAL` | Procedência da linha e se ela entra no total |
| `CD_HOSPITAL` / `DS_HOSPITAL` | Hospital (1 Conde · 2 UPA Inoã · 3 Santa Rita) |
| `CD_ESTOQUE` / `DS_ESTOQUE` | Almoxarifado/farmácia (61 no Conde, 17 na UPA, 9 em Santa Rita) |
| `CODIGO` / `DESCRICAO` | `CD_MATERIAL` e `DS_MATERIAL` do catálogo |
| `UNIDADE` | Unidade de medida (COMP, UND, FR…) |
| `TIPO_ITEM` | Classificação normalizada: `MEDICAMENTO`, `MEDICAMENTO_CONTROLADO`, `MEDICAMENTO_ANTIRRETROVIRAL`, `MATERIAL_MEDICO_HOSPITALAR`, `OPME`, `MATERIAL_ODONTOLOGICO`, `DIETA_NUTRICAO`, `MATERIAL_EXPEDIENTE`, `HIGIENE_LIMPEZA`, `EQUIPAMENTO_MANUTENCAO`, … |
| `CLASSIFICACAO` / `SUBGRUPO` / `GRUPO_COMPRA` | Grupo de estocagem, subgrupo e grupo de compra, como estão no Salux |
| `LOTE` / `VALIDADE` / `SITUACAO_VALIDADE` | Lote, validade e situação: `OK`, `VENCE_EM_ATE_90D`, `VENCIDO`, `SEM_VALIDADE` |
| `QUANTIDADE` | Saldo na unidade do item |
| `VALOR_UNITARIO` | **Custo médio do hospital** (`MATMED_HOSPITAL`), com fallback no custo médio do catálogo |
| `VALOR_TOTAL` | `QUANTIDADE × VALOR_UNITARIO`, em reais |
| `VALOR_AQUISICAO_UNIT` | Custo de aquisição (última compra) |
| `MARCA` / `LABORATORIO` | Só nas camadas que têm lote |
| `LOCALIZACAO` | Sala / estante / prateleira, quando cadastrado |
| `IN_MEDICAMENTO`, `IN_CONTROLADO`, `IN_ANTIBIOTICO`, `IN_PATRIMONIO`, `IN_CONSIGNADO`, `IN_KIT`, `IN_ATIVO_ITEM` | Flags do cadastro (S/N) |
| `REGISTRO_MS` / `CD_DCB` | Registro ANVISA e código DCB do princípio ativo |
| `REFERENCIA` | Contexto da linha (nº do empréstimo + terceiro, saldo do sistema na contagem, descrição completa no catálogo…) |
| `DATA_REF` | Data que dá sentido à linha (movimento do empréstimo, data da contagem, cadastro do item…) |
| `EXTRAIDO_EM` | Carimbo único da extração (todas as camadas leem o mesmo instante lógico) |

## 4. Armadilhas (lidas na marra)

1. **Nunca somar todas as linhas.** `SALDO_ESTOQUE_COM_LOTE`, `CONSOLIDADO_HOSPITAL` e
   `ENTRADA_FIFO_SALDO` repetem o mesmo material já contado em `SALDO_LOTE`. Filtrar
   `CONTA_NO_TOTAL = 'S'`.
2. **Vencidos entram na extração** — 2.504 linhas, R$ 744.997,78. Estão marcados em
   `SITUACAO_VALIDADE`; quem decide se migram é a operação, não a consulta. Outros R$ 959.329,51
   vencem em até 90 dias.
3. **`IN_CONTROLAR_LOTE_VALIDADE` é NULL no banco inteiro** — não serve de filtro. Quem não tem
   lote é material/OPME/expediente na prática.
4. **`EMPRESTIMO_ITEM.QT_SALDO` não é saldo em aberto**: ele é praticamente igual a `QT_MATERIAL`
   (o movimento), inclusive em empréstimos de 2021. Trate como histórico, use `DATA_REF`.
5. **`MATMED_LOTE_ENT` não tem hospital nem almoxarifado** (as colunas existem mas são nulas em
   ~99% das linhas) — por isso a camada sai agregada por material × lote × validade.
6. **UPA Inoã e Santa Rita saíram do Salux para o Klinikos em ~abr/2025.** O saldo delas aqui está
   congelado nessa época — a última contagem física dessas duas é de jun/2025, contra ago/2026 no
   Conde. Conferir antes de migrar.
7. O `TIPO_ITEM` vem do **grupo de estocagem** do Salux, que tem erros de digitação de origem
   (`EQUPAMENTOS MANUTENCAO`) e grupos vazios; a coluna normalizada corrige isso sem alterar
   `CLASSIFICACAO`, que preserva o texto original.

## 5. Reproduzir

```bash
ssh root@smsmarica.online
python3 /root/exportar_inventario_completo.py /root/inventario_salux_completo.csv \
                                              /root/inventario_salux_resumo.csv
```

Leva ~1 min. Só leitura: passa pelo proxy SQL interno (`salux-hcml`), que tem guard read-only e
corta em 5.000 linhas por consulta — o script pagina de 4.000 em 4.000. Ponto e vírgula dentro de
literal de texto no SQL é **bloqueado pelo guard** (lê como múltiplos statements).
