# Conversas e mensagens

## `smsmarica.conversa` — uma conversa por contato em atendimento

Tabela liberada inteira. ~20 mil conversas desde 07/2026 (quando a Central de Atendimento começou).

| Coluna | Significado |
|---|---|
| `telefone_canonical` | Telefone do cidadão (só dígitos, com DDI) |
| `paciente_id` | → `fhir.patient.id` (preenchido quando identificado; ~70%) |
| `nome_contato` | Nome que aparece no WhatsApp |
| `status` | **1 Aberta** (em atendimento) · **2 Pendente** (aviso enviado, esperando o cidadão responder) · 3 Resolvida · 4 Fechada |
| `unidade_id` | Unidade dona da fila da conversa → `smsmarica.unidade` |
| `operador_responsavel_id` | Atendente com a conversa → `smsmarica.usuario.id` (nulo = na fila, sem dono) |
| `ultima_mensagem_em`, `ultima_mensagem_direcao` (1 saída, 2 entrada), `ultima_mensagem_preview` | Última mensagem |
| `nao_lidas` | Mensagens do cidadão ainda não lidas pela equipe |
| `janela_expira_em` | Fim da janela de 24h do WhatsApp (depois dela só se responde com template) |
| `primeiro_contato_em`, `criado_em` | Início |
| `assunto` | Pouco usado (quase sempre nulo): 1 TFD · 2 Marcação de consulta · 3 Dúvida · 4 Atendente · 99 Outro |
| `robo_assunto_id`, `robo_bloqueado`, `robo_interacoes_na_janela` | Robô (ver `20-robo-e-avisos.md`) |
| `excluido_em` | Filtre `IS NULL` |

"Sem resposta da equipe" = `status = 1 AND ultima_mensagem_direcao = 2` (a última foi do cidadão);
há quanto tempo = `now() - ultima_mensagem_em`.

## `smsmarica.whatsapp_mensagem` — cada mensagem

Tabela liberada inteira. ~94 mil mensagens (há histórico antigo desde 2017, do transporte TFD, sem conversa).

| Coluna | Significado |
|---|---|
| `conversa_id` | → `conversa.id` (nulo nas mensagens antigas do TFD) |
| `paciente_id`, `telefone` | Cidadão |
| `direcao` | **1 saída** (nós → cidadão) · **2 entrada** (cidadão → nós) |
| `tipo_mensagem` | 1 Texto · 2 Imagem · 3 Documento · 4 Áudio · 5 Vídeo · 6 Template (aviso padronizado) · 7 Nota interna (só a equipe vê) · 8 Sistema · 9 Robô · nulo = antiga |
| `conteudo` | **Texto da mensagem** (em mídia, a legenda ou vazio) |
| `template` | Nome do template, quando é aviso padronizado |
| `autor_nome_exibicao`, `autor_usuario_id` | Quem da equipe escreveu (nas de saída) |
| `status` | 1 Enviada · 2 Entregue · 3 Lida · 4 Falha · 5 Recebida |
| `erro_meta` | Motivo da falha de envio |
| `ocorrido_em` | Quando (UTC) |

- Conversa em ordem: `WHERE conversa_id = '...' ORDER BY ocorrido_em`.
- O que os cidadãos escrevem: `direcao = 2 AND tipo_mensagem = 1`.
- O que a equipe respondeu: `direcao = 1 AND tipo_mensagem = 1` (humano) ou `9` (robô).

## `smsmarica.conversa_evento` — trilha da conversa

Tabela liberada inteira. `tipo`: 1 Criada · 2 Atribuída · 3 Assumida (um atendente pegou) ·
4 Transferida (para colega) · 5 Encaminhada para outra unidade · 6 Resolvida · 7 Reaberta · 8 Fechada ·
9 Devolvida à fila · 10 Devolvida ao robô. `ator_usuario_id`, `de_usuario_id`, `para_usuario_id`
(→ `usuario`), `de_unidade_id`, `para_unidade_id` (→ `unidade`), `observacao`, `ocorrido_em`.

Tempo até o primeiro atendente assumir = primeiro evento 3 − `conversa.criado_em`.

## Exemplos

```sql
-- Conversas abertas sem resposta há mais de 2 horas, por unidade
SELECT u.nome, count(*) FROM smsmarica.conversa c
LEFT JOIN smsmarica.unidade u ON u.id = c.unidade_id
WHERE c.excluido_em IS NULL AND c.status = 1 AND c.ultima_mensagem_direcao = 2
  AND c.ultima_mensagem_em < now() - interval '2 hours'
GROUP BY 1 ORDER BY 2 DESC;

-- Mensagens de cidadãos que falam de "remarcar" nesta semana
SELECT count(*) FROM smsmarica.whatsapp_mensagem
WHERE direcao = 2 AND tipo_mensagem = 1
  AND ocorrido_em >= date_trunc('week', now())
  AND smsmarica.unaccent(lower(conteudo)) LIKE '%remarc%';

-- Atendimentos resolvidos por atendente no mês
SELECT us.nome_completo, count(*) FROM smsmarica.conversa_evento e
JOIN smsmarica.usuario us ON us.id = e.ator_usuario_id
WHERE e.tipo = 6 AND e.ocorrido_em >= date_trunc('month', now())
GROUP BY 1 ORDER BY 2 DESC LIMIT 20;
```
