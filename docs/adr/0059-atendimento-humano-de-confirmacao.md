# ADR-0059 — Atendimento humano de confirmação: posse por solicitação, filas derivadas e cancelamento em duas fases

**Status:** aceito · **Data:** 2026-09-18
**Relacionado:** [ADR-0047](./0047-posse-de-conversa-filas.md) (posse de conversa — o mesmo
modelo de claim/trilha/xmin, aplicado à solicitação) · [ADR-0057](./0057-destinatario-correto-e-contato-negado.md)
(contato negado; número errado registrado pela atendente entra pela mesma pendência) ·
[ADR-0012](./0012-agendamento-local-e-integracao-sisreg-leitura.md) (SISREG só-leitura — **mantido**
nesta fase) · [ADR-0043](./0043-instancia-por-municipio.md)

## Contexto

As atendentes da regulação confirmavam presença numa planilha Excel com a agenda de todas as
unidades: liam a próxima solicitação, ligavam ou chamavam no zap, e marcavam "confirmou" /
"não conseguiu contato" à mão. O sistema já sabia tudo que a planilha sabia — solicitações do
SISREG, comunicação automática com recibos (✓, ✓✓, lida, falha), resposta do paciente, "não sou
essa pessoa", conversa com janela de 24h — mas não tinha o **fluxo humano por cima**: quem está
com qual solicitação, confirmar/cancelar pela mão, pendências e contatos errados como filas.

Duas telas vizinhas se sobrepunham: **Confirmações** (módulo 65, criado em 17/09: fila de envio,
respostas, lote, regras) e **Notificações de Agendamento** (módulo 38: lista crua de
`comunicacao_paciente`). Nenhuma servia à atendente; nenhuma servia ao gestor.

## Decisão

### 1. Confirmações é o trabalho das atendentes; Mensageria é a gestão dos envios

- **Confirmações** (módulo 65) passa a ter quatro abas — **Não confirmados · Confirmados ·
  Contato errado · Pendentes** — em cards ordenados pelo agendamento mais próximo. Sem escopo por
  `X-Unidade-Id`: a planilha era da rede inteira, o filtro por unidade é opcional.
- **Mensageria** (módulo 38, ex-"Notificações de Agendamento", rota `/app/mensageria`, número
  do módulo mantido) recebe o que é gestão: **resumo diário** (entradas, envios, entregas,
  leituras, falhas por erro Meta, retidas, respostas, taxas), a lista de envios com paginação,
  as respostas dos pacientes, o **disparo em lote** e as **regras** — tudo que estava em
  Confirmações e não era atendimento.

### 2. As abas são DERIVADAS; a posse é a única coisa nova

Nada de estado duplicado: confirmado/cancelado continuam em `solicitacao.status_confirmacao` e
`solicitacao.status`; "número negado" continua na `pendencia_cadastro`; o envio continua em
`comunicacao_paciente`. O que entra é a **posse humana**:

- `atendimento_confirmacao` — no máximo **uma linha ativa por solicitação** (índice único
  filtrado por `encerrado_em IS NULL`), com atendente, situação (`EmAtendimento`, `Pendente`,
  `ContatoErrado` = ativas; `Confirmado`, `Cancelado`, `Liberado`, `ContatoCorrigido` =
  encerradas), motivo e `xmin` como token de concorrência.
- `atendimento_confirmacao_evento` — trilha append-only (Atendido, Assumido, Transferido,
  Confirmado, Cancelado, EnviadoPendente, ContatoErrado, Retomado, Liberado, ContatoCorrigido),
  com De/Para em snapshot.

| Aba | Regra (sobre solicitações vivas, agendadas de hoje em diante, não canceladas pela equipe; só SISREG quando a régua manda) |
|---|---|
| Não confirmados | `status_confirmacao = Pendente`, sem pendência de número errado, sem atendimento estacionado |
| Confirmados | `status_confirmacao = Confirmada`, por qualquer canal (link, botão, robô, app, presencial, atendente) |
| Contato errado | pendência aberta de número errado para o paciente **ou** atendimento em `ContatoErrado` **ou** comunicação retida em `AguardandoCorrecaoContato` |
| Pendentes | atendimento em `Pendente` (com motivo) |

Cancelados somem das quatro abas (ficam em Mensageria → Respostas e no detalhe da solicitação).

### 3. A posse é trava de ação, com "Assumir" explícito

Igual ao ADR-0047: **Atender** cria a posse (ou retoma uma estacionada); atender o que está com
outra pessoa é `409 atendimento.ja_atendido`; **Assumir** troca a posse explicitamente (evento
Assumido com De/Para); **Transferir** e **Liberar** exigem ser o dono; corrida no `xmin` → 409
legível. Confirmar/cancelar/estacionar sem ter clicado em Atender cria a posse na hora (é o mesmo
gesto, encurtado).

### 4. Depois que uma pessoa entra, o automático não tenta mais

Ao **Atender** (e em todo desfecho humano), a comunicação automática de confirmação que **ainda
não saiu** — na fila, em retry após falha, ou retida esperando identificação/correção de contato
— vira terminal: `StatusComunicacao.SubstituidaPorAtendente = 10`. O que já foi enviado fica
como está (os recibos continuam chegando). O handler da verificação cadastral e o comando do
robô respeitam o status: identificar-se depois não ressuscita o envio.

Motivo: "se não enviou, não tenta novamente; depois que o humano entra no circuito não precisa
mais enviar" (Bernardo, 18/09/2026). O risco oposto — o paciente receber a mensagem automática
minutos depois de falar com a atendente — era pior que perder um envio.

### 5. Confirmar e cancelar pela mão gravam o canal `atendente`

`solicitacao.confirmado_canal = "atendente"` (string livre, como os demais canais) +
`contato_registro` (meio/resultado/observação) em cada desfecho. O **presencial continua
vencendo tudo** (autorizar na recepção não foi tocado).

### 6. Cancelamento em DUAS fases — nesta, o SISREG não é tocado

**Fase 1 (esta ADR):** `Cancelar` grava `solicitacao.status = Cancelada`, `cancelado_em`,
`cancelado_por`, `motivo_cancelamento`, `status_confirmacao = Cancelada` (canal `atendente`),
espelha o satélite de imagem, encerra as comunicações pendentes, **revoga os magic links** e
registra o contato. A vaga volta a contar **por derivação** (`OfertasSisregService` filtra
`cancelado_em IS NULL`) — não existe "recalcular vagas". A resposta traz `orientacaoSisreg =
true` e a tela manda cancelar também no SISREG pelo navegador: a extensão Chrome já observa
`cons_verificar · EXCLUIR_SOLICITACAO` e concilia (`ProcessadorCapturasSisreg`).

**Fase 2 (plano próprio, fora desta ADR):** modal de senha + escrita no SISREG. Pré-requisitos
decididos em 18/09/2026: (a) spike do formulário de cancelamento em PROD **com OK explícito por
ação** (hoje só se conhece `codigo_solicitacao` + `justificativa`, pela observação da extensão);
(b) `ISisregSessaoOperador.PostEscritaAsync` com allowlist de URLs de escrita; (c) **credencial
do operador só em memória por sessão**, no modelo do `SerSessaoOperadorStore` (senha digitada
uma vez por login no painel, validada no SISREG, morre no logout) — **sem cofre no banco**;
(d) reconciliação "cancelou mesmo?" relendo a solicitação; (e) aviso de que a sessão do
navegador da atendente **cai** (o SISREG derruba a sessão anterior do mesmo login).

Por que não fazer tudo de uma vez: escrever no SISREG sem o spike é chute em produção; e o
efeito colateral de sessão única (robô derruba humano e vice-versa, ADR-0012 §CAPTCHA) precisa
de desenho de operação, não só de código.

### 7. Contato errado pela mão da atendente entra pela mesma pendência do robô

"Contato errado" no card chama `IPendenciaCadastroService.RegistrarNumeroErradoAsync` — a mesma
pendência `NumeroErrado` + carimbo FHIR de contato negado do ADR-0057. Corrigir = a recepção
grava/verifica o número novo (OTP, `ModalOtpTelefone`) e clica em **Corrigir contato**: as
pendências abertas do paciente são resolvidas (o que solta as comunicações retidas), a comunicação
substituída por atendente é rearmada, e a solicitação volta à fila automática.

### 8. Botão do zap abre a conversa quando a janela está aberta

`GET /conversas/situacao?pacienteId=&telefone=` responde se há conversa viva com janela de 24h
aberta. `BotaoWhatsAppPaciente` (em qualquer tela) abre a **thread direto, com histórico**, em
vez do diálogo de template. Sem janela, o caminho antigo.

### 9. Lote manual: reenviar para já avisados e já confirmados

O disparo em lote (agora em Mensageria) ganha `incluirJaAvisados` (rearma a comunicação: links
revogados, recibos zerados, envio novo) e `incluirJaConfirmados` (a resposta anterior vai para
`contato_registro` e `status_confirmacao` volta a Pendente — senão o worker mata o envio como
"já respondeu"). Opções discretas, tela de admin. Assunção registrada: "reenviar para quem
confirmou" = **pedir nova confirmação**.

### 10. Custos são módulo próprio

`ModuloPermissao.EstatisticaCustos = 67`. Sem ele, `GET /estatisticas/whatsapp` devolve o
consumo do robô **sem tokens e sem custo** (tokens revelam custo) e sem a estimativa Meta — o
backend omite, o front só esconde. A **estimativa de custo Meta** usa tarifas por categoria
(utility/marketing/authentication, USD) e um mapa template → categoria guardados em
`mensageria_configuracao` (singleton editado em Mensageria → Regras; **nada seedado em
migration**, regra 9). Utility com janela de 24h aberta e texto de sessão não custam; é
estimativa, a fatura é da Meta.

## Consequências

- Migration única `20260918233414_AtendimentoConfirmacaoEMensageriaConfiguracao`: três tabelas,
  três índices, nenhuma alteração em tabela existente. Enums novos são ints.
- Rotas antigas de Confirmações (`/confirmacoes/fila*`, `/respostas`, `/lote*`,
  `/configuracao`, `/regras/*`) continuam existindo e aceitam **Confirmações OU Mensageria**
  (`RequerQualquerPermissao`). A rota de front `/app/notificacoes-agendamento` redireciona para a
  Mensageria e passa a ter guarda de módulo (antes não tinha).
- Permissões: `Confirmacoes` ganha **Exclusao** = cancelar agendamento; `NotificacoesAgendamento`
  vira "Mensageria" no rótulo (número mantido, sem migração de perfis); `EstatisticaCustos` novo.
- Polling de 30 s cobre a posse de terceiros nesta fase (sem SignalR).
- O que **não** muda: nada escreve no SISREG; presencial vence; ADR-0057 continua a régua de LGPD.

## Pendências

- Fase 2 do cancelamento (spike + sessão por operador em memória + escrita com allowlist).
- Tempo real da posse por SignalR, se o polling incomodar.
- Tarifas Meta puxadas automaticamente (hoje tabela editada à mão).
