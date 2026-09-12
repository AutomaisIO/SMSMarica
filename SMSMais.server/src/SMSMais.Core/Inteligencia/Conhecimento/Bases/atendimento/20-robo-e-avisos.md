# Robô de atendimento e avisos ao paciente

## Robô

`smsmarica.robo_assunto` (`id, nome, descricao, ativo, padrao`) — os assuntos que o robô atende.

`smsmarica.robo_tarefa` — uma por mensagem que o robô processou. Colunas liberadas: `id, conversa_id,
mensagem_whatsapp_id, paciente_id, robo_assunto_id, status, confianca_ultima, tentativas, erro,
criado_em, atualizado_em, custo_usd, tokens_entrada, tokens_saida`.
`status`: 1 Pendente · 2 Processando · 3 **Concluída** (o robô respondeu) · 4 Falha · 5 **Hand-off**
(passou para um humano).

`smsmarica.robo_acao` — cada ferramenta que o robô usou (liberada inteira). `comando`:
1 consultar situação do agendamento · 2 confirmar presença · 3 iniciar cancelamento · 4 registrar
número errado · 5 encaminhar para humano · 6 informar horário · 7 responder ao cidadão ·
8 consultar exame/laudo recente · 9 consultar posição na regulação · 10 verificar cadastro ·
11 consultar cadastro · 12 (outros). `sucesso` (bool), `entrada_json`/`resultado_json`, `ocorrido_em`.

"O robô resolve sozinho" ≈ tarefas `status = 3` sem `status = 5` na mesma conversa; hand-off = 5.

## Avisos automáticos — `smsmarica.comunicacao_paciente`

Colunas liberadas: `id, tipo, finalidade, solicitacao_id, paciente_id, telefone, status,
motivo_falha, mensagem_whatsapp_id, tentativas, enviado_em, entregue_em, lido_em, visualizado_em,
criado_em, origem, enviado_por`.

- `finalidade`: 1 confirmação de agendamento · 2 exame disponível · 3 laudo pronto.
- `status`: 1 Pendente · 2 Enviada · 3 Entregue · 4 Lida · 5 Falha · 6 Sem telefone válido ·
  7 retida (contato não verificado — dado clínico) · 8 retida (aguardando o cidadão confirmar o CPF) ·
  9 retida (número marcado como errado).
- `origem`: 0 automática · 1 manual (um operador reenviou).
- `solicitacao_id` → `smsmarica.solicitacao`.

## Agendamento — `smsmarica.solicitacao` (só estas colunas)

`id, paciente_id, codigo_solicitacao, procedimento_texto, categoria, status, data_agendada,
status_confirmacao, confirmado_em, confirmado_canal, unidade_executante_id, cancelado_em, excluido_em`.
`status_confirmacao`: 1 Pendente · 2 o paciente confirmou · 3 o paciente cancelou.
`data_agendada` em UTC (`AT TIME ZONE 'America/Sao_Paulo'`).

```sql
-- Das confirmações enviadas no mês, quantas o paciente respondeu confirmando ou cancelando
SELECT s.status_confirmacao, count(*)
FROM smsmarica.comunicacao_paciente c
JOIN smsmarica.solicitacao s ON s.id = c.solicitacao_id
WHERE c.finalidade = 1 AND c.enviado_em >= date_trunc('month', now())
GROUP BY 1;
```
