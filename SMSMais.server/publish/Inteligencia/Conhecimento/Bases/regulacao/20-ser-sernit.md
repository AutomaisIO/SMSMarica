# SER e SERNIT (regulação EXTERNA)

**SER** = Sistema Estadual de Regulação (SES-RJ). **SERNIT** = SER de Niterói. Quem regula e executa
é outro ente; nós espelhamos a fila e o histórico de cada pedido de Maricá. As duas tabelas têm o
**mesmo formato**; troque `ser_` por `sernit_` e `id_ser` por `id_sernit`.

## `smsmarica.ser_solicitacao` / `smsmarica.sernit_solicitacao`

Liberadas inteiras. ~26 mil pedidos no SER (desde 2015) e ~1,3 mil no SERNIT.

| Coluna | Significado |
|---|---|
| `id_ser` / `id_sernit` | Número do pedido no sistema externo |
| `tipo` | 1 Consulta · 2 Exame |
| `recurso` (texto) | O que foi pedido (especialidade/exame), no vocabulário do sistema externo |
| `data_solicitacao` (date) | Entrada do pedido |
| `situacao` | **1 Em fila · 2 Pendente · 3 Agendada · 4 Chegada não confirmada · 5 Chegada confirmada · 6 Cancelada · 7 Alta** |
| `situacao_anterior`, `situacao_mudou_em` | Mudança mais recente |
| `agendado_para_texto` (texto) | Data/local marcado, como o sistema externo escreve (texto livre, não é data) |
| `unidade_executora` (texto) | Onde vai ser atendido (fora de Maricá, em geral) |
| `cid`, `solicitante_nome`, `municipio_solicitante` | Clínico e origem (`municipio_solicitante` pode vir nulo) |
| `paciente_id` | → `fhir.patient.id` (preenchido em ~99,9%) |
| `paciente_nome`, `cpf`, `cns`, `data_nascimento`, `sexo`, `nome_mae`, `telefone_*`, `bairro`, `municipio_paciente` | Paciente como o sistema externo mostra (endereço/telefone só depois de lido o histórico) |
| `ultimo_evento_em`, `eventos_count` | Histórico |
| `excluido_em` | Filtre `IS NULL` |

- "Na fila externa" = `situacao IN (1, 2)` (em fila ou pendente). Agendados = 3. Atendidos = 5 ou 7.
- Espera na fila = `current_date - data_solicitacao` com `situacao IN (1,2)`.

## `smsmarica.ser_evento` / `smsmarica.sernit_evento` — histórico de cada pedido

Colunas liberadas: `id, ser_solicitacao_id (sernit_solicitacao_id), data_evento, evento,
estado_anterior, estado_atual, central_regulacao, unidade_executora, usuario, lotacao_evento,
observacao, capturado_em`. Um evento por movimentação (inclusive **FollowUP** = tentativa de contato
registrada no sistema externo; `evento ILIKE '%follow%'`). Ordene por `data_evento`.

## Catálogo externo

`smsmarica.ser_catalogo_recurso` (`id, tipo, valor, rotulo, ambulatorio_estadual`) e
`smsmarica.sernit_catalogo_recurso` (`id, tipo, valor, rotulo`): os recursos que cada sistema oferece.
`rotulo` é o mesmo texto de `ser_solicitacao.recurso`.
