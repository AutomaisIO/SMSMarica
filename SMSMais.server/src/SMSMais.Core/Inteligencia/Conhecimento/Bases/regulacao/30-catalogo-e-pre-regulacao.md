# Catálogo único de procedimentos e pré-regulação

## Catálogo canônico — o que liga SISREG, SER e SERNIT

Cada sistema chama o mesmo procedimento de um jeito. O catálogo canônico junta os nomes:

`smsmarica.regulacao_procedimento` (`id, nome_canonico, tipo, procedimento_sigtap_id, ativo`) — o
procedimento "de verdade". `tipo`: 1 Consulta · 2 Exame · 3 Cirurgia · 9 Outro.

`smsmarica.regulacao_procedimento_origem` (`id, procedimento_id, sistema, chave_externa,
rotulo_externo, ramo, vinculo, ativo`) — como cada sistema chama aquele procedimento.
`sistema`: **1 SISREG · 2 SER · 3 SERNIT**. `vinculo`: 1 sugerido pela máquina · 2 confirmado por pessoa.

Como casar (medido em 12/09/2026):

| Sistema | Casa por | Cobertura |
|---|---|---|
| SER | `ser_solicitacao.recurso = o.rotulo_externo` (sistema 2) | ~85% dos recursos |
| SERNIT | `sernit_solicitacao.recurso = o.rotulo_externo` (sistema 3) | ~75% |
| SISREG agendamentos | `solicitacao.procedimento_codigo_sisreg = o.chave_externa` (sistema 1) | parcial (~20% dos códigos) |
| SISREG fila | `sisreg_fila_pendente.procedimento_nome = o.rotulo_externo` (sistema 1) | parcial |

Dois registros com o mesmo `procedimento_id` são o **mesmo procedimento** em sistemas diferentes.
Quando o catálogo não casa, compare por nome (`ILIKE`/`unaccent`) e **diga que foi aproximado** —
e, se o nome for ambíguo, pergunte ao operador qual procedimento ele quer.

## Pré-regulação do SMSMais (ainda com pouco uso)

`smsmarica.regulacao_solicitacao` — pedidos abertos pelas unidades no SMSMais antes de ir para o
sistema de regulação. Colunas liberadas: `id, numero_local, fluxo, unidade_solicitante_id,
unidade_em_nome_de_id, paciente_id, paciente_cpf, paciente_cns, paciente_nome, procedimento_id,
sistema_destino, status, status_motivo, numero_externo, enviado_em, solicitacao_id,
ser_solicitacao_id, sernit_solicitacao_id, observacoes, criado_em, atualizado_em, excluido_em`.

- `fluxo`: 1 Interno (vai ao SISREG) · 2 Externo (SER/SERNIT) · 3 Agendamento indireto (SISREG em nome de outra unidade).
- `sistema_destino`: 1 SISREG · 2 SER · 3 SERNIT.
- `status`: 1 Rascunho · 2 Pendente de regulação · 3 Em análise · 4 Devolvida · 5 Enviando · 6 Enviada ·
  7 Em fila externa · 8 Agendada · 9 Concluída · 10 Cancelada · 11 Recusada · 12 Falha no envio.
- `numero_externo` = número no sistema de destino; `solicitacao_id` / `ser_solicitacao_id` /
  `sernit_solicitacao_id` ligam ao espelho do sistema.
- `procedimento_id` → `regulacao_procedimento.id`.

`smsmarica.regulacao_evento` (`id, solicitacao_id, tipo, status_anterior, status_novo, usuario_nome,
papel, criado_em`) — trilha de cada pedido; `papel` 1 solicitante · 2 agente regulador · 3 sistema.
`smsmarica.regulacao_solicitacao_destino` — para quais sistemas o pedido pode ir
(`situacao` 1 elegível · 2 bloqueado · 3 com ressalva).

Hoje há poucas linhas aqui: quase toda a regulação ainda está nas tabelas do SISREG/SER/SERNIT.
