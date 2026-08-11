# Incidente 2026-08-11 — Troca de identidade em mamografia (worklist errada)

> **Classificação:** segurança do paciente (identidade trocada em exame de imagem) + exposição de
> dado de saúde (LGPD).
> **Status:** vazamento **contido**; correção do vínculo **pendente**.
> **Levantamento:** 2026-08-11, só-leitura no Postgres de PROD + QIDO no dcm4chee.

## 1. O que aconteceu

No mamógrafo do CDT, em 11/08/2026, a técnica selecionou o item de Modality Worklist do paciente
**JOÃO BENTO RIBEIRO** e adquiriu as imagens da paciente **PATRICIA FERNANDA DE ALMEIDA BANKS**.

O equipamento copiou do item de worklist o `PatientName`, o `PatientID` e o `AccessionNumber` do
João. As imagens da Patricia chegaram ao PACS **internamente coerentes** com a identidade do João —
por isso a conciliação automática do SMSMarica aceitou o estudo sem qualquer sinal de erro, promoveu
o exame do João a *Realizada* e disparou o WhatsApp de "exame liberado" **para o João**.

Os dois exames eram MAMOGRAFIA BILATERAL e estavam na worklist da mesma estação — o que explica
mecanicamente a confusão.

## 2. Os envolvidos

| Papel | Paciente | CPF | Exame | Accession | Status hoje |
|---|---|---|---|---|---|
| **A** — dono errado do registro | JOÃO BENTO RIBEIRO (86a) | 010.745.887-03 | `019fcc69-9540…` | `260804114` | **Realizada** (com as imagens da Patricia) |
| **B** — dona real das imagens | PATRICIA FERNANDA DE ALMEIDA BANKS | 045.288.227-33 | `019fcc69-8fd0…` | `260804109` | **Recebida** — item ainda na worklist |

`fhir.patient`: João `5f331a20-c0f4-410f-b796-7158316787cc` · Patricia `54002b3d-b040-45d9-9917-f5c9828c6413`.

## 3. Os três estudos no PACS

```
CONTAMINADO  (imagens da PATRICIA, carimbadas JOÃO)
  StudyUID  2.25.326124362417603501160858399380646188215      ← UID PRÉ-GERADO por nós
  Accession 260804114        PatientID 01074588703 (CPF do João)
  Nome      RIBEIRO^JOAO BENTO
  11/08/2026 09:40:31    MG    4 séries / 4 instâncias

REFEITO      (imagens REAIS do JOÃO)
  StudyUID  1.2.392.200036.9125.2.24823725282117250.65139678144.8116424   ← UID da MÁQUINA (Fuji)
  Accession (VAZIO)          PatientID 123456789
  Nome      JOAO^BENTO^RIBEIRO
  11/08/2026 11:02:24    MG    4 séries / 4 instâncias         → ÓRFÃO no PACS

PATRICIA     — nenhum estudo sob o CPF nem sob o nome dela. As imagens dela são o CONTAMINADO.
```

> **Correção de premissa:** o `123456789` combinado como contorno foi digitado no campo
> **PatientID (0010,0020)**, não no AccessionNumber — que ficou **vazio**. Não muda o desfecho
> (o estudo é órfão de qualquer forma), mas muda o caminho da conciliação.

## 4. Extensão do estrago

| Camada | Situação |
|---|---|
| PACS | Estudo da Patricia arquivado sob a identidade do João |
| Vínculo local | **Nenhuma linha em `exame_associacao`** — o vínculo é **implícito/estrutural** |
| Laudo | **Nenhum laudo** no estudo contaminado. Nada a descartar; nada assinado |
| Comunicação | WhatsApp **ENVIADO E ENTREGUE** ao João em 11/08 12:51:33 |
| Sessão do app | **Nenhuma** — o link nunca foi usado |
| Worklist | Item do João consumido; item da Patricia **ainda pendente** |

### 4.1 A confirmação técnica que importa

Não existe linha em `exame_associacao` para o estudo contaminado. Isso confirma empiricamente o
diagnóstico: quando o estudo volta com o `StudyInstanceUID` **pré-gerado**, a conciliação
(`ExameAssociacaoService.cs:363`) o trata como *worklist genuíno* e cria um vínculo **implícito**,
gravado na coluna `exame_imagem.study_instance_uid` (UNIQUE).

**Consequência operacional: nenhuma ferramenta existente hoje alcança este caso.** Não há o que
"desassociar" — o botão da tela de PACS não vê esse vínculo.

## 5. Contenção executada

**2026-08-11 14:44 UTC — magic-link revogado** (autorizado explicitamente pelo operador).

```
cidadao_login_link  019ff0e0-b14d-75f3-b2da-5e1c431a448d
  destino    /exames?exame=019fcc69-9540-7dfd-80fe-e9c0827beeac
  expira_em  14/08 12:51  →  11/08 14:44   (antecipado = revogado)
  usado_em   NULL  (antes e depois)
```

Único link da solicitação; após o UPDATE, `ativo = false`.

**Não houve vazamento consumado.** A mensagem foi entregue ao celular do João, mas o link nunca foi
aberto e nenhuma sessão foi criada. A revogação fechou a janela antes do clique.

> **O que teria acontecido se ele tivesse clicado:** o link é um *magic-link que autentica*. Um
> clique abriria a sessão do prontuário do João já apontando para o exame — exibindo a mamografia da
> Patricia. Ver Fase 3 do plano (gate de CPF).

Nada mais foi escrito: exame, status, worklist e PACS estão exatamente como estavam.

## 6. O que falta corrigir

Três peças, e a do meio só destrava depois da primeira:

1. **Estudo contaminado → Patricia.** Coagir no dcm4chee (`PatientID` 04528822733, nome
   `BANKS^PATRICIA FERNANDA DE ALMEIDA`, `AccessionNumber` 260804109), liberar o
   `study_instance_uid` do exame do João e atribuí-lo ao exame da Patricia (*Realizada*, data do
   estudo 11/08 09:40). Remover o item dela da worklist.
2. **Estudo refeito → João.** Associação explícita do UID `1.2.392.200036…8116424` ao exame
   `019fcc69-9540…`, guardando o PatientID DICOM original (`123456789`) como snapshot.
3. **Comunicações.** Só depois de 1 e 2 os avisos corretos devem sair — para a Patricia (exame
   liberado) e, se aplicável, para o João sobre o exame refeito.

É exatamente a **opção 3 (trocar um pelo outro)** do plano de correção — com a ressalva de que o
estudo que volta para o João **não tem accession** e tem UID de máquina.

## 7. Por que passou

- A conciliação é determinística e confia no `AccessionNumber`/`StudyInstanceUID` que voltam do
  equipamento. Nesse erro, **todos eles são coerentes — e todos estão errados**. Não havia sinal.
- Dois itens de MAMOGRAFIA BILATERAL na worklist da mesma estação, no mesmo turno.
- Nenhuma checagem cruzada entre "quem passou pela recepção" e "de quem chegou estudo".

**O detector proposto teria pego:** ao fim do dia, Patricia estava autorizada, com item pendente na
worklist e **sem estudo**; João tinha estudo *Realizada*. Esse par é a assinatura do erro.

**A implausibilidade clínica também sinalizava:** mamografia bilateral em paciente do sexo masculino,
86 anos.

## 8. Ações

| # | Ação | Estado |
|---|---|---|
| 1 | Revogar o magic-link entregue ao paciente errado | **FEITO** 11/08 14:44 UTC |
| 2 | Gate de CPF nas 3 portas (link, download público, cache do app) | Implementado, não deployado |
| 3 | Quarentena de exame acionável por qualquer usuário | Implementado, não deployado |
| 4 | Motor de correção de identidade (3 opções, só ADM) | Implementado, não deployado |
| 5 | Corrigir as duas peças deste caso | **PENDENTE** — depende do deploy |
| 6 | Detector automático (par órfão da recepção) | Implementado, não deployado |
| 7 | Varredura retroativa por outros casos | **PENDENTE** — rodar em modo relatório |

**Como corrigir este caso quando a ferramenta estiver no ar** (decisão do operador — opção 2 em
dois tempos, ver `docs/pacs-correcao-identidade.md`):

1. Em *Exames de Imagem → Corrigir Identidade*, carregar o estudo `2.25.3261…` com destino
   `260804109` (Patricia). Ação **Alterar destino**, marcando **"o paciente que estava errado já
   fez o exame dele"** — assim o João não reaparece na lista do aparelho.
2. Na tela de exames, **Associar** o estudo `1.2.392…8116424` ao pedido `260804114` (João). A
   associação agora também corrige o DICOM, então o `PatientID 123456789` e o accession vazio
   viram os dados certos dele.

Plano completo: `~/.claude/plans/o-que-poderemos-fazer-pure-twilight.md`.
