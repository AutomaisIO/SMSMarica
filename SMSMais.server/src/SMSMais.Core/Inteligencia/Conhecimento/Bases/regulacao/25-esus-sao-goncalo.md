# ESUS de São Gonçalo (regulação EXTERNA — PPI de exame)

**ESUS** aqui é o sistema de regulação municipal de **São Gonçalo** (produto ESUS, esusmais.com.br) —
**não** é o e-SUS do governo. Maricá entra nele como unidade solicitante: a equipe de Maricá inclui o
paciente na fila de São Gonçalo e São Gonçalo agenda (retina, glaucoma, catarata, estrabismo,
pterígio, YAG, auditiva e outros exames da PPI). Nós espelhamos a fila e os agendamentos.

## `smsmarica.esussg_solicitacao` — um pedido por linha

| Coluna | Significado |
|---|---|
| `id_esussg` | Número do pedido no ESUS (`fil_id`) |
| `tipo` | 1 Consulta · 2 Exame (na prática, só exame) |
| `recurso` (texto) | Procedimento, como o ESUS escreve ("TRATAMENTO DE RETINA (PPI)") |
| `codigo_interno` | Código interno do ESUS — **não é SIGTAP** |
| `data_solicitacao` (date) | Data do pedido médico |
| `data_entrada_fila` (date) | Entrada na fila do ESUS — a régua da espera |
| `prioridade` | "A REGULAR", "URGENTE", "MAIS DE 60 ANOS", "MAIS DE 80 ANOS", "MANDADO JUDICIAL" |
| `pendencia` | "NAO", "TODAS RESOLVIDAS" ou a pendência ativa |
| `posicao_fila` | Posição regulada na fila do procedimento (só enquanto na fila) |
| `situacao` | **1 Em fila · 2 Pendente · 3 Agendada · 4 Saiu da fila** |
| `situacao_anterior`, `situacao_mudou_em` | Mudança mais recente |
| `unidade_executora`, `cnes_executora` | Onde vai ser atendido em São Gonçalo (o CNES vem no nome) |
| `data_agendada` (date), `data_hora_agendada_texto` | A PRÓXIMA sessão agendada (ou a última, se todas passaram) |
| `usuario_inclusao` | Servidor de Maricá que incluiu na fila |
| `usuario_agendamento` | Servidor de São Gonçalo que agendou |
| `notificacao_tipo`, `notificacao_resposta` | Aviso que o próprio ESUS mandou ao paciente e a resposta |
| `paciente_id` | → `fhir.patient.id` |
| `paciente_nome`, `cpf`, `cns`, `data_nascimento`, `sexo`, `nome_mae`, `telefone`, `celular`, `bairro`, `municipio_paciente` | Paciente como o ESUS mostra |
| `excluido_em` | Filtre `IS NULL` |

- "Na fila" = `situacao IN (1, 2)`. Espera = `current_date - data_entrada_fila`.
- **"Saiu da fila" (4) NÃO é cancelado**: o pedido sumiu da fila e dos agendados, e a conta de Maricá
  não vê o motivo. Nunca responda que foi cancelado.
- Um pedido pode ter várias sessões agendadas; a coluna mostra a próxima. As sessões estão na trilha.

## `smsmarica.esussg_evento` — trilha montada

Mesmas colunas do `sernit_evento`. A trilha é **montada** a partir dos marcos que o ESUS mostra
(`evento` = "Inclusão na fila", "Agendamento para dd/mm/aaaa hh:mm", "Reagendamento",
"Mudança de prioridade", "Pendência", "Saiu da fila", "Resposta do paciente (…)"). Evento percebido
entre duas leituras traz a data em que a leitura percebeu (a `observacao` diz isso).

## Catálogo

`smsmarica.esussg_catalogo_recurso` (`id, tipo, valor, rotulo, ativo`): os procedimentos que Maricá
pode pedir a São Gonçalo. `rotulo` é o mesmo texto de `esussg_solicitacao.recurso`.

## Análise automática das regras (SER, SERNIT e ESUS)

`smsmarica.regulacao_analise_espelho` (`sistema` 2 SER · 3 SERNIT · 5 ESUS SG, `espelho_id` → id da
linha no espelho, `numero_externo`, `veredito`, `resumo`, contagens, `analisado_em`). Veredito:
1 sem procedimento no catálogo · 2 sem regras · 3 apto · 4 com ressalva · 5 a conferir (depende de
pergunta/documento) · 6 bloqueado. É um parecer ao lado do pedido — não muda o pedido.
