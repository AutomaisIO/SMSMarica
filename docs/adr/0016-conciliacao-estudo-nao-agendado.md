# ADR-0016 — Conciliação de estudo não-agendado (associar estudo "voando" a paciente/solicitação)

- **Status:** Aceito — **coerção implementada em 2026-08-11, por reescrita** (ver adendo no fim)
- **Data:** 2026-06-13
- **Relacionados:** ADR-0012 (agendamento/solicitação), `docs/pacs.md`,
  `docs/pacs-correcao-identidade.md`, [[project_worklist_ups_vs_mwl]]

## Contexto

O fluxo feliz da Solicitação de Exame é: SMSMarica cria a solicitação → grava um item de
worklist no dcm4chee → o equipamento puxa a worklist → adquire as imagens **já com o paciente,
AccessionNumber e StudyInstanceUID corretos** → o estudo chega no PACS associado à solicitação.

Na prática hoje **não há equipamento cooperando com a worklist** (o aparelho real é MWL e nós
gravamos UPS — ver [[project_worklist_ups_vs_mwl]]). Resultado: o técnico adquire as imagens
**sem puxar a worklist**, então o estudo chega no PACS:

- com **PatientName/PatientID** digitados à mão (ou vazios), que **não casam** com nenhum paciente
  nosso (`fhir.patient`);
- **sem AccessionNumber** (ou com um qualquer);
- com um **StudyInstanceUID próprio da máquina**, diferente do pré-gerado pela solicitação.

Ou seja, o estudo fica "voando" — sem vínculo com a solicitação/paciente. A solicitação nunca
sai de `Agendada` (o `SincronizadorExamesService` busca por AccessionNumber e não acha) e não dá
pra visualizar/laudar a partir dela.

É o problema clássico de PACS de **conciliação de estudo não-agendado** (*unscheduled /
unidentified study reconciliation*).

## Decisão

Criar no SMSMarica uma função de **Conciliação ("Associar exame")** que corrige o estudo no
próprio dcm4chee-arc (coerção de paciente + AccessionNumber) e religa a solicitação, em vez de
exigir que o operador faça isso na arc-ui crua do PACS.

### Fluxo

1. **Listar estudos voando** — estudos do PACS sem solicitação correspondente. Heurística:
   AccessionNumber vazio/sem `SMS…`, **ou** AccessionNumber que não casa com nenhuma
   `SolicitacaoExame`, **ou** PatientID que não casa com nenhum paciente nosso.
2. **Operador escolhe o estudo + a solicitação** (que carrega `PacienteId`, `AccessionNumber` e
   o paciente resolvido no FHIR).
3. **Backend coage o estudo no dcm4chee** (ver endpoints abaixo):
   a. Garante o paciente no dcm4chee (PatientID = `PacienteId`, PatientName do FHIR).
   b. **Move Study to Patient** — associa o estudo voador ao paciente certo.
   c. **Update Study** — grava `AccessionNumber` (0008,0050), `StudyDescription` e atributos do
      pedido (ReferencedRequestSequence) batendo com a solicitação.
4. **Backend religa a solicitação**: grava o **StudyInstanceUID REAL** (o da máquina) em
   `SolicitacaoExame.StudyInstanceUID`, seta `RealizadoEm` e `Status = Realizada`.
5. A partir daí **Visualizar/Laudar** funcionam normalmente (o estudo está associado).

### Auto-conciliação (quando possível)

Se o técnico digitar o **AccessionNumber** (`SMS…`) ou o **CPF** no aparelho, o
`SincronizadorExamesService` pode conciliar **sozinho** por AccessionNumber — sem operador. A tela
manual cobre o caso em que nada foi digitado.

### Endpoints dcm4chee-arc 5.32 usados (confirmados)

| Operação | Método + caminho (`…/aets/{aet}/rs`) |
|---|---|
| Update Study (accession, descrição, request attrs) | `PUT /studies/{studyUID}` (corpo DICOM+JSON) |
| Move Study to Patient | `POST /studies/{studyUID}/patient` |
| Create Patient (se faltar) | `POST /patients` |
| Change Patient ID | `POST /patients/{prior}/changeid/{new}` |
| Reject (IOCM, se precisar desfazer) | `POST /studies/{studyUID}/reject/{code}^{designator}` |

> **Descartado:** "Link Instances with MWL Entry"
> (`POST /mwlitems/{study}/{sps}/move/…`) — só vincula a item **MWL**, e nós gravamos **UPS**
> (não MWL). A coerção direta (Move + Update) não depende de MWL e é mais robusta no nosso caso.

### Identidade do paciente no PACS

Mantém a régua do `ConstrutorWorkitemUps`: **PatientID (0010,0020) = `PacienteId`** (Guid do
`fhir.patient`); **PatientName (0010,0010)** resolvido no hub FHIR. Assim o estudo conciliado fica
consistente com o que a worklist teria gravado.

### O StudyInstanceUID divergente

Como a máquina gera o próprio UID, **a solicitação adota o UID real** do estudo (não o
pré-gerado). Mudar o UID do estudo no PACS (reject + re-store) é caro e arriscado — não
compensa. O vínculo solicitação↔estudo passa a ser pelo UID real após a conciliação.

## Componentes (a construir)

**Backend:**
- `IPacsReconciliacaoClient` — cliente tipado para as operações de escrita do dcm4chee (Move/Update/
  Create Patient), no padrão dos clients existentes. (O `PacsProxyService.EncaminharAsync` já
  encaminha PUT/POST, mas um client tipado é mais claro e testável.)
- `IConciliacaoExameService` — `ListarEstudosNaoAssociadosAsync`, `ConciliarAsync(studyUID,
  solicitacaoId)`. Coage no PACS + religa a solicitação (transação lógica; idempotente).
- Endpoints: `GET /conciliacao/estudos-voando`, `POST /conciliacao/{studyUID}` (body: solicitacaoId).
  Permissão `SolicitacoesExame` (Edição).

**Front:**
- Tela "Conciliação" (ou aba/filtro em Exames de Imagem) que lista os estudos voando e, por estudo,
  um botão **"Associar"** → modal: busca/seleciona a solicitação → confirma. Mostra o que vai mudar
  (paciente, accession) antes de aplicar.

## Consequências

- ✅ Estudos voadores deixam de travar o fluxo; operador associa em segundos sem entrar na arc-ui.
- ✅ Auto-conciliação por AccessionNumber/CPF reduz trabalho manual quando o técnico digita o id.
- ⚠️ Escrita no PACS: o deploy do dcm4chee é **unsecure** (sem auth REST) — as operações de coerção
  ficam acessíveis a quem alcança a porta 8080. Já é o caso hoje (QIDO/WADO/reject); manter o PACS
  atrás de rede/firewall. Auditar quem conciliou (registrar usuário + antes/depois) no SMSMarica.
- ⚠️ Operação destrutiva-ish (muda o paciente do estudo): exigir confirmação e logar; usar Reject/
  IOCM para reverter se associar errado.
- ➡️ Não resolve a causa-raiz (máquina não puxa worklist). O caminho definitivo continua sendo
  **escrever MWL** para o aparelho real enxergar ([[project_worklist_ups_vs_mwl]]); a conciliação
  é a rede de segurança para o que escapar.

## Alternativas consideradas

- **Operador concilia na arc-ui do dcm4chee** — funciona, mas é fora do SMSMarica, sem nossa régua
  FHIR, sem auditoria nossa, e exige treinar operador no PACS cru. Descartado como fluxo primário.
- **Mudar o StudyInstanceUID do estudo para o pré-gerado** — exige reject+re-store; caro e frágil.
  Descartado: a solicitação adota o UID real.
- **Link a item MWL** — não usamos MWL (gravamos UPS); endpoint não se aplica.

---

## Adendo 2026-08-11 — o que a implementação corrigiu desta ADR

A coerção prevista aqui foi implementada, mas o spike contra o dcm4chee de produção mostrou que
três coisas descritas acima **não são assim**:

1. **`POST /studies/{uid}/patient` quer o PatientID na QUERY STRING**, não no path nem no corpo:
   `POST /studies/{uid}/patient?PatientID={id}` → 204. Com o ID no path devolve 404; no corpo,
   400 `"Missing Patient ID in query filters"`.

2. **`PUT /studies/{uid}` NÃO troca o paciente.** Ele só atualiza atributos do estudo e recusa
   corpo com outro paciente: *"Patient found using patient identifiers sent in request payload
   does not match"*. Mover e coagir são **duas** chamadas, nesta ordem.

3. **A coerção não basta.** Ela propaga `PatientID` e `AccessionNumber` para o que o WADO-RS
   devolve, mas o **`PatientName` continua o antigo** dentro do objeto recuperado — e nem
   `updatePolicy=OVERWRITE` nem `REPLACE` mudam isso. Ou seja, coagir deixaria o nome de outra
   pessoa no arquivo, visível em exportação DICOM e gravação de CD.

Por (3), a implementação **não usa coerção**: usa **reescrita** — baixa as instâncias, troca a
identidade, gera UIDs novos, re-armazena por STOW-RS e então rejeita o original com
`113038^DCM` ("Incorrect Modality Worklist Entry", o código IOCM deste erro; a instalação já tem
o AE `IOCM_WRONG_MWL`) e o apaga.

Também **cai a premissa** de que "mudar UID no PACS é caro e arriscado, então a solicitação adota
o UID real": com reescrita, é o contrário — o UID **precisa** ser novo, porque o antigo está
colado ao objeto que está sendo removido.

Detalhes operacionais e as três opções de correção: `docs/pacs-correcao-identidade.md`.
Código: `SMSMarica.Core/Pacs/PacsReescritorEstudoClient.cs`.
