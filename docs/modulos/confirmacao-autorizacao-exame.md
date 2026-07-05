# Confirmação de agendamento + autorização presencial de exames

> Estado: **em produção** desde 2026-07-05. Migrations: `20260704012933_ConfirmacaoAgendamentoWhatsApp`, `20260705011223_AutorizacaoPresencialSolicitacao`.

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
  - exige paciente com **número verificado** (`contato_validado` por CPF) — o botão "Verificar"
    no Resumo do Paciente faz o OTP com número editável e grava direto no FHIR;
  - chave segue a régua da regulação (`0000` emergencial ou ≥ 9999);
  - presença física **vence** qualquer resposta anterior: pendente → Confirmada `presencial`;
    **cancelada → Confirmada `presencial`** (limpa motivo/cancelamento — "revive");
  - só então enfileira o envio ao PACS (se o `TipoExame.EnviarParaWorklist`).
  - `reenviar-worklist` respeita o mesmo gate quando a solicitação nunca foi enviada
    (`Solicitada` sem `AutorizadoEm` → 409 `solicitacaoExame.nao_autorizada`). Em
    `Enviada`/`Recebida` é manutenção de item já no PACS e segue liberado.
- **Estoque legado**: solicitações anteriores à regra que nunca foram enviadas ficam
  "Aguardando" e seguem o fluxo novo (autoriza quando o paciente vier). Decisão de 2026-07-05.

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

`tests/SMSMarica.Tests` — `AutorizacaoSolicitacaoTests` (gate + régua + revive),
`WhatsAppWebhookStatusesTests` (recibos, monotonicidade, retentável/terminal),
`ConfirmacaoAgendamentoHandlerTests` (máquina de estados do cancelamento + não-sequestro do chat).
São de **integração** (PostgresFixture/Testcontainers) — exigem Docker.

## Pendências conhecidas

- Template `confirma_exame`/`confirma_agendamento` com botões aguardando aprovação na Meta —
  até lá o sandbox (`/app/sandbox`, módulo 39) testa por texto livre na janela de 24h.
- QR do ticket no PWA ainda carrega UUID aleatório (conteúdo real a definir — ex.: token de
  check-in).
- Consultas usarão o mesmo desenho quando forem importadas (entidades já são genéricas).
