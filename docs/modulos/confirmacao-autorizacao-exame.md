# Confirmação de agendamento + autorização presencial + comunicações ao paciente

> Estado: **em produção** desde 2026-07-05. Migrations: `20260704012933_ConfirmacaoAgendamentoWhatsApp`, `20260705011223_AutorizacaoPresencialSolicitacao`, `20260705022530_RenomearComunicacaoPaciente`.

## Comunicações ao paciente (`comunicacao_paciente`)

Tabela ÚNICA de comunicação WhatsApp (renomeada de `agendamento_notificacao`), uma linha por
**solicitação × finalidade**, ciclo completo: fila → envio → entrega → leitura → **visualização**
→ falha (+tentativas/motivo). Finalidades e gatilhos:

| Finalidade | Gatilho | Destino do magic link | Chave de envio (config `ComunicacaoPaciente`) |
|---|---|---|---|
| ConfirmacaoAgendamento (1) | import com DataAgendada futura | `/agendados/exames` | `EnviarConfirmacaoAgendamento` (ON) |
| ExameLiberado (2) | solicitação marcada **Realizada** | `/exames` | `EnviarExameLiberado` (**OFF** até a Meta aprovar) |
| LaudoPronto (3) | laudo **ASSINADO** digitalmente | `/laudos` | `EnviarLaudoPronto` (**OFF** até a Meta aprovar) |

Chave desligada = a fila **acumula** (o gatilho enfileira normal) e flui sozinha ao ligar.
`VisualizadoEm` é estampado quando o paciente usa o magic link da comunicação **ou** abre o
recurso no app (imagens-pdf / laudo-pdf).

### Checks (estilo WhatsApp) na lista de solicitações

Chips ao lado da Situação, por finalidade (ExameLiberado quando Realizada; LaudoPronto quando
Laudada): **✓ cinza** enviado · **✓✓ cinza** entregue · **✓✓ AZUL** lida/visualizada ·
**⚠** falha/sem número (tooltip com o motivo) · relógio = na fila.

### Histórico + contatos manuais

No detalhe da solicitação, a seção "Comunicação com o paciente" mostra a timeline de cada
comunicação (fila/enviada/entregue/lida/visualizada + erros) e os **contatos manuais**
(`contato_registro`, append-only): botão "Registrar contato" (meio: ligação/WhatsApp/presencial;
resultado: atendeu/não atendeu/caixa postal/número inválido; observação).
Endpoints: `GET /solicitacoes-exame/{id}/historico`, `POST /solicitacoes-exame/{id}/contatos`.
Gestão geral em `/comunicacoes-paciente` (rota renomeada de agendamento-notificacoes).

## Visão geral

Do agendamento (import SISREG hoje; API futura) até o PACS, a solicitação de exame passa por
**duas camadas independentes** do status operacional (`StatusSolicitacaoExame`):

1. **Confirmação do paciente** (`SolicitacaoExame.StatusConfirmacao`): a resposta do cidadão à
   notificação — Pendente / Confirmada / Cancelada + canal + motivo. **Não** altera o status
   operacional; a equipe decide o cancelamento real.
2. **Autorização presencial** (`AutorizadoEm`/`AutorizadoPor` + `ChaveConfirmacao`): a recepção,
   com o paciente na frente, entra com a chave de confirmação do SISREG. **Só a autorização
   libera o envio ao PACS** — import/criação nunca enfileiram o envio (regra desde 2026-07-05;
   antes o import auto-enviava).

## Fluxo

```
Import SISREG ──> AgendamentoNotificacao (fila) ──> worker envia template WhatsApp
   (DataAgendada futura + celular BR válido)         (magic link + quick reply)
                                                            │
              ┌──────────────── 3 canais de resposta ───────┤
              ▼                        ▼                    ▼
        clicou no link          quick reply no zap     card no app
        (whatsapp-link)        (whatsapp-quickreply)      (app)
              └────────────► StatusConfirmacao ◄────────────┘
                                     │
Paciente chega na recepção ──> AUTORIZAR (chave SISREG; exige telefone verificado)
                                     │  • confirma "presencial" se pendente
                                     │  • REVIVE se o paciente tinha cancelado
                                     ▼
                          ProximaTentativaEm = now ──> EnviadorWorklistService ──> PACS
```

## Regras

- **Notificação**: só exames com `DataAgendada` futura; telefone escolhido = contato validado
  (por CPF) > celular do cadastro; sem celular BR válido → `SemTelefoneValido` (nem tenta).
  Recibos (`value.statuses`) promovem Enviada→Entregue→Lida; `failed` retentável re-enfileira
  com magic link novo; erros permanentes da Meta (131026/131030) são terminais.
- **Magic link**: uso único **atômico**. Token gasto nunca re-autentica: com sessão no aparelho
  abre o destino direto; sem sessão cai no login.
- **Autorização** (`POST /solicitacoes-exame/{id}/autorizar`):
  - exige paciente com **número verificado** (marcador no telecom do Patient FHIR) **ou uma
    dispensa de verificação registrada** (ver seção abaixo) — o botão "Verificar" no Resumo do
    Paciente faz o OTP com número editável e grava direto no FHIR;
  - chave segue a régua da regulação (`0000` emergencial ou ≥ 9999);
  - presença física **vence** qualquer resposta anterior: pendente → Confirmada `presencial`;
    **cancelada → Confirmada `presencial`** (limpa motivo/cancelamento — "revive");
  - só então enfileira o envio ao PACS (se o `TipoExame.EnviarParaWorklist`).
  - `reenviar-worklist` respeita o mesmo gate quando a solicitação nunca foi enviada
    (`Solicitada` sem `AutorizadoEm` → 409 `solicitacaoExame.nao_autorizada`). Em
    `Enviada`/`Recebida` é manutenção de item já no PACS e segue liberado.
- **Estoque legado**: solicitações anteriores à regra que nunca foram enviadas ficam
  "Aguardando" e seguem o fluxo novo (autoriza quando o paciente vier). Decisão de 2026-07-05.

## Dispensa de verificação do contato (`dispensa_verificacao_contato`)

> Migration `20260729152102_DispensaVerificacaoContato`.

O gate de número verificado travava o balcão: quem **não tem celular**, não tem WhatsApp ou não
consegue ler o código ficava parado, sem ninguém poder autorizar o exame. A dispensa é o
registro de que o paciente **consentiu em não validar** — com o motivo, o operador e a data.

- **Escopo: por PACIENTE, até revogar.** Uma ativa por vez (índice único filtrado em
  `revogado_em IS NULL`). Trocar o motivo **revoga e cria outra** — append-only, a trilha nunca
  é sobrescrita.
- **Cai sozinha** quando o contato é verificado por OTP (`"Contato verificado por código"`) ou
  quando o telefone principal muda de fato (`"Telefone principal alterado"`) — número novo
  merece uma tentativa nova de verificar.
- **Ciência do paciente é obrigatória** (`paciente_ciente`): o operador afirma que informou que
  o paciente não receberá avisos/resultado/laudo por WhatsApp e que ele concordou. Sem o
  checkbox, 400 `dispensa.sem_ciencia`.
- Vive em `smsmarica`, **não** no hub FHIR: é ato administrativo do balcão (quem/quando/porquê),
  não atributo de identidade do cidadão. O **verificado** continua sendo só o marcador no
  telecom do Patient FHIR.

### Motivos e o que cada um libera

Toda dispensa libera a **autorização presencial**. O que varia é o **envio de dado clínico**
(`DispensaContatoRegras.PermiteEnvio`): só sai quando o motivo indica que existe um número
utilizável e consentido. `Outro` é conservador de propósito.

| Motivo | Resultado/laudo por WhatsApp |
|---|---|
| Não possui celular | ❌ entrega presencial |
| Possui celular, mas sem WhatsApp | ❌ entrega presencial |
| Número é de terceiro (familiar / cuidador / responsável) | ✅ vai para o número do cadastro |
| Não consegue ler ou informar o código (idoso, def. visual, baixa alfabetização) | ✅ |
| Sem sinal / sem internet no momento | ✅ |
| Paciente recusa informar ou validar o número | ❌ entrega presencial |
| Outro (descrição obrigatória, ≥ 5 caracteres) | ❌ entrega presencial |

Ao registrar, as comunicações já retidas em `AguardandoTelefoneVerificado` são acertadas: motivo
"com canal" **solta a fila** (com `ignorar_verificacao_telefone`); motivo "sem canal" mantém a
retenção mas **reetiqueta o motivo** para "entrega presencial" — antes a equipe ficava esperando
uma verificação que nunca ia acontecer.

### Endpoints e tela

`GET /telefones/dispensa/motivos` (opções + consequência de cada uma) ·
`GET /telefones/dispensa/{pacienteId}` (ativa; 204 quando não há) ·
`POST /telefones/dispensa` · `POST /telefones/dispensa/{pacienteId}/revogar`.
Permissão: `SolicitacoesExame` (Consulta para ler, Edição para registrar/revogar) — dispensar é
ato da recepção que autoriza, não edição de cadastro.

No painel: botão **"Não vai validar"** ao lado do "Verificar" (Resumo do Paciente e card de
Autorização), modal com os motivos + consequência visível + checkbox de ciência. Com dispensa
ativa, o botão some e entra o selo âmbar com o motivo, o "desfazer" e — quando é o caso — o
aviso de **entrega presencial**.

## Situação "de fora" (lista de Solicitações)

Derivada no front (`features/solicitacoes-exame/components/SituacaoSolicitacao.tsx`), na ordem:

| Situação | Cor | Regra |
|---|---|---|
| (operacional) | — | Recebida/EmExecucao/Realizada/Laudada/Cancelada usam o badge operacional |
| Cancelado | cinza + linha esmaecida | paciente avisou que não vai |
| Falha | vermelho | `ErroIntegracaoPacs` preenchido |
| Autorizado | laranja | `AutorizadoEm` preenchido |
| Falta | roxo | passou das **18h de Brasília** do dia do exame **e** 1h do horário (a 2ª condição evita "Falta" antes da hora em exame noturno) |
| Atrasado | azul | 1h após o horário agendado |
| Confirmado | amarelo | paciente confirmou |
| Aguardando | cinza | estado inicial |

Todos os cortes de tempo são calculados em **UTC-3 fixo** (regra única de fuso — nunca o fuso
do navegador).

## Testes

`tests/SMSMarica.Tests` — `AutorizacaoSolicitacaoTests` (gate + régua + revive + dispensa
libera / dispensa revogada volta a travar), `DispensaContatoRegrasTests` (quais motivos deixam
dado clínico sair; **sem banco** — roda sem Docker),
`WhatsAppWebhookStatusesTests` (recibos, monotonicidade, retentável/terminal),
`ConfirmacaoAgendamentoHandlerTests` (máquina de estados do cancelamento + não-sequestro do chat).
São de **integração** (PostgresFixture/Testcontainers) — exigem Docker.

## Pendências conhecidas

- Template `confirma_exame`/`confirma_agendamento` com botões aguardando aprovação na Meta —
  até lá o sandbox (`/app/sandbox`, módulo 39) testa por texto livre na janela de 24h.
- QR do ticket no PWA ainda carrega UUID aleatório (conteúdo real a definir — ex.: token de
  check-in).
- Consultas usarão o mesmo desenho quando forem importadas (entidades já são genéricas).
