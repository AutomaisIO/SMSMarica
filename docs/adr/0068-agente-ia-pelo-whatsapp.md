# ADR-0068 — Agente IA pelo WhatsApp do celular de aviso

**Status:** aceito · **Data:** 2026-10-06 · implementado em 06/10/2026 (não deployado)
**Relacionado:** [ADR-0011](./0011-modulo-ia-consulta-linguagem-natural.md) (módulo IA) ·
[ADR-0044](./0044-app-meta-unico-e-roteador-whatsapp.md) (WhatsApp via Automais.Zap) ·
[ADR-0047](./0047-posse-de-conversa-filas.md) / [ADR-0048](./0048-todas-destravada-e-historico-completo.md) (Conversas) ·
Avisos no celular (`alerta_destinatario`, lista única desde 24/09/2026)

## Contexto

O telefone cadastrado em **Sistema → Avisos no celular** recebe todo erro da plataforma (robô parado,
sincronismo que falhou, erro 500 novo). Até aqui, para agir sobre um aviso era preciso abrir o painel,
entrar no **Agente IA** (Claude Code no servidor, com o clone do repositório) e contar a história de novo.

O Bernardo pediu que o próprio WhatsApp seja o canal: quem recebe o aviso responde ali mesmo e conversa
com o agente, numa sessão que **não expira** — só recomeça quando ele pedir.

## Decisão

1. **Quem fala com o agente.** Um telefone de Avisos no celular com a chave nova **"Conversa com o Agente
   IA"** ligada e **vinculado a um usuário** (`alerta_destinatario.agente_ia` + `agente_usuario_id`;
   CHECK impede ligado sem usuário). O usuário vinculado é a identidade do agente: carimba autoria e
   decide o acesso. **Acesso total × somente leitura continua decidido só pelo motor**
   (`AIENGINE_ADMIN_USUARIO_IDS`) — o canal não cria segunda regra de autorização.
2. **Quem pode ligar.** Desligar, qualquer um com acesso à tela (é o interruptor). **Ligar, trocar o
   usuário ou o número de quem já está ligado: só o próprio usuário vinculado**, e ele precisa ter o
   módulo Agente IA com edição. Sem isso, quem só cuida dos avisos poria o próprio celular falando com o
   servidor em nome de outra pessoa.
3. **Caminho da mensagem.** Webhook → manipulador `AgenteIaWhatsAppHandler` (Ordem 1) grava um
   `agente_whatsapp_pedido` e **encerra a cadeia** (`ManipuladorContexto.Encerrado`): robô, confirmação,
   acompanhante e verificação cadastral nunca veem a mensagem — o celular pode ser também cadastro de
   paciente, e um "sim" dito ao agente não pode virar confirmação de agendamento. O webhook não espera o
   agente (o relay do Zap espera segundos; um turno leva minutos).
4. **Worker** (`AgenteWhatsAppWorker`): leva o pedido ao motor (`POST /internal/ai/whatsapp/turns`),
   acompanha os eventos do turno e responde em texto livre — a janela de 24h está aberta porque foi o
   operador quem escreveu. Andamento: "⏳ recebido" na hora, depois os textos do agente agrupados a cada
   ~30 s, e a resposta final ao terminar (o andamento que sobra é descartado, para não repetir o final).
   Uma mensagem por vez por telefone: a que chega com turno rodando espera na fila e é avisada uma vez.
   Cursor e andamento ficam na linha do pedido — restart da API retoma sem reenviar.
5. **Sessão.** Uma por telefone (`kind='whatsapp'`, `canal_ref` = telefone normalizado com nono dígito).
   O processo do Claude continua sendo descartado após 30 min ocioso e volta com `resume`; a sessão em si
   nunca é podada (a limpeza de 30 dias poupa `whatsapp`, ativas e arquivadas).
6. **Comandos** (mensagem inteira, sem acento/caixa): `parar` interrompe o turno; `reiniciar` arquiva a
   sessão e cancela o que esperava; `status` mostra a sessão. "parar o worker X" é pedido ao agente, não
   comando.
7. **Responder citando um aviso** leva o texto da mensagem citada junto do pedido, num bloco marcado
   como **dado, não instrução** (o aviso nasce de log e pode carregar texto de terceiros).
8. **Fora do módulo Conversas.** Todas as conversas e mensagens desse telefone somem de listas,
   contadores, thread e histórico do paciente. O registro fica no painel **Inteligência → Agente IA →
   aba WhatsApp**, só leitura (quem conduz é o celular).
9. **Só texto** na fase 1. Áudio, foto e documento recebem resposta pedindo texto.

## Consequências

- **Risco aceito:** com acesso total, quem tomar o WhatsApp do celular (clonagem de chip, sessão do
  WhatsApp Web esquecida) ganha o agente com acesso ao servidor de produção. PIN não ajuda numa sessão que
  não expira. Mitigações: **confirmação em duas etapas do próprio WhatsApp** no celular (recomendada na
  tela); o interruptor na tela; a regra de confirmação por ação em produção continua no prompt do agente.
- O motor do agente passa a ter uma sessão "fixa" ocupando um dos `MAX_SESSIONS` (3) quando ativa.
- A migration `AgenteIaPeloWhatsApp` só adiciona (duas colunas + uma tabela). Com a replicação DO→EVEO
  ligada, o DDL vai **primeiro no psql01**.

## Fora desta fase

- Transcrição de áudio (o Bernardo dita por voz) — precisa de um serviço de fala-para-texto.
- Repassar foto/print de erro ao agente.
