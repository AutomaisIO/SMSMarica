# ADR-0047 — Posse de conversa: fila da unidade × lista pessoal do atendente

**Status:** aceito · **Data:** 2026-08-24
**Relacionado:** [ADR-0044](./0044-app-meta-unico-e-roteador-whatsapp.md) (roteador de WhatsApp),
[ADR-0043](./0043-instancia-por-municipio.md) (instância por município — o multi-tenant do módulo
continua sendo *unidade*, nunca `TenantId`).

## Contexto

A Central de Atendimento (módulo Conversas) nasceu com o modelo de dados de posse pronto —
`conversa.operador_responsavel_id`, `conversa.unidade_id`, a trilha `conversa_evento` com
De/Para e o token de concorrência `xmin` — mas sem a mecânica: nenhum endpoint mudava a posse,
a aba "Da unidade" era um superconjunto que incluía as conversas com dono, responder não tornava
ninguém responsável, e abrir a thread zerava o contador de não-lidas **para todos os operadores**
(o front chamava `POST /{id}/lida` num `useEffect`).

Na prática: não existia separação entre "o que é meu" e "o que ninguém pegou", qualquer operador
via (e apagava o badge de) conversas de qualquer colega, e a posse — quando existia — era um
rótulo de listagem, não uma trava.

## Decisão

**Conversa tem no máximo um responsável, e as listas são disjuntas:**

- **Com dono** (`operador_responsavel_id` setado): existe só na lista pessoal do dono
  (aba *Minhas*) e na visão da supervisão (aba *Todas*, `ConversasSupervisao`).
- **Sem dono, com unidade**: fila da unidade — visível aos vinculados àquela unidade.
- **Sem dono e sem unidade** (número desconhecido): triagem geral, visível a todo operador do
  módulo até alguém puxar.
- A antiga aba `NaoAtribuidas` vira **alias** da fila (compatibilidade); o recorte da fila segue
  sendo **todas as unidades vinculadas** do operador — `X-Unidade-Id` continua fora deste módulo.

**A posse muda por exatamente cinco caminhos**, todos gravados na trilha `conversa_evento`:

| Ação | Como | Evento |
|---|---|---|
| Claim implícito | responder (texto ou template) conversa **sem dono** | `Assumida` |
| Claim explícito | `POST /conversas/{id}/assumir` (o botão "Marcar como lida" da fila) | `Assumida` |
| Devolver | `POST /{id}/devolver` — limpa o dono, **mantém a unidade** | `Devolvida` (novo, =9) |
| Encaminhar a colega | `POST /{id}/encaminhar` — alvo ativo, com o módulo, vinculado à unidade | `Transferida` |
| Transferir de unidade | `POST /{id}/transferir` — entra na fila de lá **sem dono** | `EncaminhadaUnidade` |

**Responder conversa de terceiro NÃO rouba a posse** — a autoria da mensagem já registra quem
falou; reatribuição é sempre ação explícita. Agir sobre conversa de terceiro (devolver /
encaminhar / transferir) exige `ConversasSupervisao`.

**`POST /{id}/lida` mantém a semântica antiga** (zera o contador, nunca dá claim). Motivo de
transição que virou garantia: o front anterior chamava esse endpoint ao abrir a thread — se ele
desse claim, o deploy do backend na frente do front faria *abrir a conversa* roubar a posse em
silêncio. O claim explícito tem endpoint próprio (`/assumir`).

**A posse é trava de acesso, não só filtro.** Todo acesso por id (ler, mensagens, responder,
marcar lida e as ações novas) passa pelo escopo: supervisão ∨ dono ∨ triagem geral ∨ unidade
vinculada — fora disso, **404** (não vaza a existência). Colega da mesma unidade continua
podendo abrir e responder a conversa do outro (sem virar dono) — é o plantão cobrindo o colega.

**Concorrência pelo `xmin` que já existia:** dois atendentes puxando a mesma conversa — o
segundo recebe `409 conversa.ja_assumida` com o nome de quem ganhou. Exceção deliberada:
`EnviarTexto` nunca falha **depois** que a mensagem já foi ao cidadão; perdeu a corrida do
claim, a posse fica com o vencedor e só os efeitos da mensagem são reaplicados.

**Contador de não-lidas continua um inteiro por conversa** (não por usuário). Com as listas
disjuntas ele é suficiente: conversa com dono só aparece para o dono, então o badge é dele; na
fila, o primeiro que puxar assume a pendência. O sino/badge global sai do novo
`GET /conversas/resumo` (minhas + fila) — antes o total era somado da lista carregada, e só na
janela solta do chat (o sino da janela principal vivia em zero).

**SignalR espelha as listas** (`Grupos()` ↔ `ListarAsync` — mudou um, muda o outro): conversa
com dono notifica só `usuario:{dono}` + supervisão + thread aberta (a unidade inteira não bipa
mais); sem dono notifica a unidade, ou `conversas:geral` na triagem. Mudança de posse/unidade
usa `ConversaMovidaAsync`, que soma a audiência **antiga** à nova — quem perdeu a conversa
recebe o evento que a tira da lista dele.

## Consequências

- Sem migration: campos, índices e eventos já existiam; só `Devolvida = 9` entrou no enum.
- O deploy é em duas fases *por design*: fase 1 (backend aditivo, abas intactas) é inofensiva ao
  front antigo; fase 2 (abas disjuntas + `Grupos()` + UI) é atômica back+front.
- Conversa assumida por operador **sem unidade principal** (vários vínculos, nenhum marcado)
  fica com dono e sem unidade — visível só a ele e à supervisão. É comportamento documentado;
  o alerta de cadastro (vínculo sem principal) fica como melhoria futura.
- A supervisão zera badge de conversa alheia ao marcar lida (contador é global). Aceito.
- Mensagens de automação (confirmação de agendamento etc.) continuam **fora** da thread
  (`WhatsAppCliente.NovaMensagem` não vincula `conversa_id`) — gap conhecido, independente de
  posse, registrado para frente própria.
- Fica proibido dar claim em `POST /{id}/lida` — quem quiser "abrir = assumir" precisa de novo
  ADR (e de pensar no front velho em cache).
