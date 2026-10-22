# ADR-0075 — O aparelho decide o que acontece com o exame depois que chega: onde é laudado, quando o paciente recebe e se usa anamnese

**Status:** aceito · **Data:** 2026-10-20 · **Implementação:** branch `feat/envio-exame-por-equipamento`
**Relacionado:** [ADR-0056](./0056-escopo-de-exame-por-unidade.md) (tipo × unidade e aparelho de destino) ·
[ADR-0061](./0061-modos-assinatura-e-selo-do-laudo.md) (assinar, conferir e aprovar o laudo) ·
[ADR-0066](./0066-acervo-do-paciente-e-midia-do-whatsapp.md) (acervo do paciente) ·
[ADR-0074](./0074-pedido-varios-procedimentos-um-exame-por-codigo.md) (campos da worklist por aparelho)

## Contexto

Até aqui, todo exame realizado ia ao paciente **assim que o estudo chegava ao PACS**: aparecia no app do
cidadão, com o PDF das imagens, e saía o WhatsApp "Exame liberado". Laudo assinado gerava, depois, o
"Laudo pronto". Não havia regra por aparelho: todo exame entrava na fila de laudo e na estatística
"aguardando laudo", e a anamnese aparecia para todos.

Isso não serve a três situações reais (pedido do Bernardo em 20/10/2026):

- **Mamografia e densitometria** são laudadas aqui, e a médica avalia a imagem **depois** do exame — a
  imagem pode mudar (estudo refeito, imagem rejeitada; ver o ticket #169). Mandar a imagem antes é
  entregar ao paciente o que ainda não está fechado. Imagem e laudo têm de ir **juntos**.
- **Ultrassom** é laudado na hora, **em outra plataforma**: não há laudo nosso, e a imagem pode ir já.
- **MAPA e Holter** (CardioNet) voltam com **imagem e laudo no mesmo PDF**, assinado fora: não há laudo
  nem assinatura nossa, nem anamnese.

O que foi medido na produção em 20/10/2026, por aparelho (exames realizados):

| Aparelho | Exames | Com laudo aqui | Assinados |
|---|---:|---:|---:|
| Mamógrafo (MG) | 1.439 | 1.224 | 1.217 |
| Densitometria (OT) | 483 | 419 | 376 |
| Raio-X (DX) | 173 | 153 | 147 |
| 4 ultrassons (US) | 1.359 | **0** | 0 |
| CardioNet Holter/ECG, MAPAS | 0 | — | — |

O raio-X também é laudado e assinado aqui. Nenhum ultrassom jamais teve laudo na plataforma. O CardioNet
ainda não devolveu estudo nenhum ao PACS (é só MWL SCU, sem C-STORE). 78 exames não tinham aparelho
gravado — todos de ultrassom, em unidades com dois ultrassons.

## Decisão

### 1. Três campos no **equipamento**, e não no tipo de exame

O mesmo procedimento pode ser laudado aqui num aparelho e fora em outro; quem decide se a imagem pode ir
antes do laudo é quem lauda. Os campos:

- **`origem_laudo`** (`OrigemLaudo`) — escolha única: `Plataforma` (1), `JuntoComExame` (2),
  `ForaDaPlataforma` (3). Duas caixas ("habilitar laudo" + "laudo vem com a imagem") deixariam marcar as
  duas, e isso não existe.
- **`liberacao_paciente`** (`LiberacaoExamePaciente`) — `ComLaudoAssinado` (1) ou `AoChegar` (2).
- **`usa_anamnese`** (bool).

**Trava:** `ComLaudoAssinado` só com `Plataforma` (`ck_equipamento_liberacao_com_laudo`, validador e
serviço). Com o laudo feito fora, não haveria laudo nosso para soltar a imagem — o exame ficaria retido
para sempre.

### 2. A liberação é gravada no exame, no momento em que acontece

`exame_imagem.liberado_paciente_em` (null = retido) e `liberado_paciente_por` (null = automático).
Mudar a regra do aparelho depois **não esconde nem solta** exame antigo. Três pontos gravam:

1. **Chegada** (`SolicitacoesService.MarcarComoRealizadaAsync`, o único caminho para "Realizada"):
   `AoChegar` libera e enfileira o "Exame liberado"; `ComLaudoAssinado` grava null e **não** enfileira.
   A decisão é refeita a cada realização — o estudo que chega depois de uma correção de identidade não
   herda a liberação do anterior.
2. **Laudo aprovado** (`LaudoAssinaturaService`, ao enfileirar o "Laudo pronto"): libera, e o "Exame
   liberado" automático que ainda não saiu fica `Dispensada` — **uma mensagem só**, o "Laudo pronto",
   cujo link abre o exame com imagem e laudo.
3. **Pessoa** ("Enviar exame" no detalhe, que no exame retido vira **"Liberar antes do laudo"**):
   libera e grava quem. O "Reenviar" do histórico **não** libera: recusa explicando o caminho.

### 3. O que o paciente vê segue a liberação

App do cidadão (`/cidadao/exames`, PDF das imagens, acervo em "Documentos") mostra só o exame liberado
**ou** com laudo assinado — o laudo assinado também libera na leitura, para cobrir a aprovação cuja
gravação falhou (ela é best-effort). Os usos de dentro (regulação, acervo visto pela equipe) continuam
vendo tudo, e cada chamada declara para quem é (`visaoDaEquipe`, `ListarParaPacienteAsync`).

A fila do WhatsApp confere na hora do envio: "Exame liberado" automático de exame não liberado vira
`Dispensada` ("Retido até o laudo…"). É isso que segura o que já estava na fila antes da regra.

### 4. Qual aparelho, quando o exame não gravou

`RegrasDoExameService`, numa ordem só para todo o sistema: o aparelho do exame → o aparelho fixo do
escopo (tipo × unidade) → os aparelhos ativos da unidade na modalidade. Vários candidatos: se
concordam, vale a regra deles; se discordam, **retém** (soltar antes da hora não tem volta), e laudo e
anamnese valem se algum usa. Sem candidato: retém, laudo na plataforma, com anamnese.

### 5. Laudo e anamnese

- `OrigemLaudo != Plataforma`: o PACS não oferece "Criar laudo" e o servidor recusa
  (`laudo.fora_da_plataforma`); "aguardando laudo" e "% laudados" nos relatórios só contam aparelho que
  lauda aqui.
- `usa_anamnese = false`: some o botão (Solicitações e PACS) e o laudo não a cobra.

### 6. Implantação (migration `LiberacaoExamePorEquipamento`)

- Aparelhos existentes: `Plataforma + ComLaudoAssinado + anamnese`; **ultrassom** (modalidade 4) vira
  `ForaDaPlataforma + AoChegar`. Instância nova não tem aparelho quando a migration roda.
- Exames já realizados: liberados se o "Exame liberado" já tinha saído (não se tira do app o que foi
  mandado), se têm laudo assinado ou se o aparelho libera ao chegar. **O resto fica retido** — decisão de
  20/10/2026: reter também o que ainda não tinha sido avisado. Prévia na produção: de 3.456 realizados,
  **69 retidos** (53 MG, 13 DO, 3 DX), todos sem laudo e sem aviso enviado.
- "Exame liberado" ainda na fila de solicitação que já tem "Laudo pronto": `Dispensada`.
- MAPA e Holter (`JuntoComExame + AoChegar`, sem anamnese) se configuram **pela tela**, depois do
  deploy: são cadastros de Maricá e não entram em migration (CLAUDE.md, regra 9c).
- Os defaults ficam só na migration, não no modelo: com default no banco o EF omite do INSERT o valor
  igual ao default do CLR — o `false` de `usa_anamnese` — e o aparelho cadastrado sem anamnese
  nasceria com ela.

## Consequências

- Mamografia, densitometria e raio-X deixam de aparecer no app antes do laudo; quem recebe é avisado uma
  vez só, com tudo.
- Ultrassom segue como era, e sai da fila de laudo e da estatística, onde nunca deveria ter entrado.
- Cadastro de aparelho novo exige pensar nos três campos — a tela sugere pela modalidade (ultrassom: fora
  e ao chegar; o resto: aqui e com o laudo).

## Pendências

- **"Laudo vem junto" ainda não lê o PDF de dentro do estudo.** O PDF das imagens rasteriza instâncias
  por WADO `/rendered`; um PDF encapsulado (SOP 1.2.840.10008.5.1.4.1.1.104.1) não vira página. Só dá
  para fazer com um resultado real do CardioNet chegando ao PACS — hoje ele não devolve estudo.
- O app do cidadão (PWA e Flutter) simplesmente não lista o exame retido. Um card "aguardando laudo",
  sem imagem, seria mais claro para o paciente — fica para quando o Bernardo quiser.

## Alternativas descartadas

- **Flag no tipo de exame.** Foi o primeiro pedido; descartado pelo próprio Bernardo — a regra é de
  quem lauda, e o tipo é do município inteiro (o mesmo problema que o ADR-0056 resolveu para a worklist).
- **Liberação calculada na leitura, sem gravar.** Menos pontos de escrita, mas mudar a regra do aparelho
  esconderia exames já entregues (ou soltaria retidos sem aviso). Gravar fixa a decisão no momento.
- **Card do exame retido sem imagem no app.** Mais informativo, mas muda o PWA e o app nativo; o pedido
  era segurar imagem e laudo.
