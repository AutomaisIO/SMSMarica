# Correção de identidade de exame — o que cada opção faz, ponta a ponta

Documento operacional: para cada uma das três opções, **o que muda no nosso banco, o que acontece
no PACS e o que a técnica precisa fazer no equipamento**. Escrito para que ninguém precise adivinhar
o estado do sistema depois de apertar o botão.

Base técnica: spike executado em 2026-08-11 contra o dcm4chee de produção (ver
`~/.claude/plans/o-que-poderemos-fazer-pure-twilight.md`).

## Princípio adotado

**Integridade real, sem máscara.** O objeto DICOM armazenado passa a conter os dados corretos —
não basta o índice apontar para a pessoa certa enquanto o arquivo carrega o nome de outra.

O spike provou que a via barata (`POST /studies/{uid}/patient` + `PUT /studies/{uid}`) corrige
`PatientID` e `AccessionNumber` mas **não** o `PatientName` dentro do objeto. Por isso a correção
usa a **reescrita completa**:

```
1. baixa as instâncias do estudo         (WADO-RS)
2. reescreve os atributos de identidade  (pydicom)
3. gera SOP Instance UIDs e StudyInstanceUID NOVOS
4. re-armazena                            (STOW-RS)
5. rejeita o original com 113038^DCM     (IOCM "Incorrect Modality Worklist Entry")
6. apaga o original                       (DELETE /studies/{uid})
```

O passo 5 não é burocracia: `113038^DCM` é o código DICOM padrão para *"item de worklist errado"* —
exatamente este erro — e a instalação já tem o AE `IOCM_WRONG_MWL` provisionado para enxergar o que
foi rejeitado por esse motivo. É a trilha de auditoria do próprio PACS.

> **Por que UIDs novos.** O UID antigo fica colado ao estudo errado. Reaproveitá-lo faria o objeto
> corrigido colidir com o que estamos apagando. Com UID novo, o antes e o depois são objetos
> distintos, e a rejeição documenta a relação entre eles.

---

## Como o item volta para a worklist (vale para as opções 1 e 2)

Esta é a parte que precisa estar clara, porque não existe "empurrar" para o equipamento.

**A Modality Worklist é PULL.** O equipamento consulta (C-FIND); o servidor não tem como avisá-lo.
Então "devolver à worklist" significa: recriar o item no dcm4chee e a técnica **atualizar a consulta
de worklist na estação** — o item reaparece na lista dela.

O que o sistema faz, automaticamente:

| Passo | Efeito |
|---|---|
| `Status` → *Solicitada* | volta a ser candidato da fila de envio |
| `StudyInstanceUID` → **novo** pré-gerado | o UID anterior está queimado no PACS |
| `WorklistItemUid`, `RealizadoEm`, `DataEstudo` → nulos | o exame deixa de constar como feito |
| `ProximaTentativaEm` → agora | o worker pega no próximo ciclo |

O `EnviadorWorklistService` roda a cada **15 segundos** e cria o item novo
(`POST /mwlitems`). **Não é preciso reautorizar na recepção** — a fila só exige
`Status ∈ {Solicitada, Enviada}` + `ProximaTentativaEm` vencido; a autorização vive na
`Solicitacao` e governa apenas o enfileiramento inicial.

> **Na prática, para a técnica:** aguardar ~15 segundos e atualizar a worklist na estação.
> O paciente reaparece na lista como se nunca tivesse sido feito.

Único caso em que não volta sozinho: se o `TipoExame` estiver com `EnviarParaWorklist` desligado —
aí não vai à worklist e nem erro aparece. A tela avisa antes de confirmar.

---

## Opção 1 — Descartar estudo

**Quando:** as imagens não servem para ninguém (aquisição abortada, teste, duplicata sem valor).

| Onde | O que acontece |
|---|---|
| **PACS** | Estudo rejeitado com `113038^DCM` e **apagado**. Some do `PACS-CDT`. |
| **Nosso banco** | O exame que o consumiu volta ao estado anterior e **retorna à worklist** (ver acima). |
| **Laudos** | Rascunhos do estudo são descartados. Havendo laudo **assinado**, a operação é bloqueada. |
| **Paciente** | Link já enviado é revogado; aviso pendente é cancelado. |
| **Equipamento** | Item reaparece em ~15 s. **O exame precisa ser refeito.** |

## Opção 2 — Alterar destino

**Quando:** o estudo é de outra pessoa, e o dono do registro errado **ainda não fez** o exame dele.

| Onde | O que acontece |
|---|---|
| **PACS** | Estudo **reescrito** com a identidade do destino (PatientID, PatientName, AccessionNumber) e re-armazenado com UIDs novos; o original é rejeitado com `113038^DCM` e apagado. |
| **Nosso banco** | O exame **destino** adota o novo StudyUID e vira *Realizada*, com a data real do DICOM; se ele tinha item de worklist, o item é removido. O exame **origem** volta à worklist. |
| **Laudos** | Rascunhos descartados; assinado bloqueia. |
| **Paciente** | Link do destinatário errado revogado. O aviso correto só sai depois da correção. |
| **Equipamento** | O item da **origem** reaparece (precisa ser feito); o do **destino** some (já está feito). |

## Opção 3 — Trocar um pelo outro

**Quando:** o exame de destino **já tem estudo vinculado**. Enquanto o outro estudo for apenas
órfão, não é caso de opção 3 — resolve-se com a opção 2 seguida da **associação manual**, que o
fluxo atual já cobre.

Faz o mesmo da opção 2 para o estudo contaminado **e**, no mesmo movimento, traz o estudo do destino
para a origem. Ao informar a solicitação de destino, a tela **sugere** o estudo a trazer de volta
(vinculado ao destino, ou órfão cujo accession/PatientID aponte para ele) — sugestão marcada, que o
ADM pode desmarcar.

| Onde | O que acontece |
|---|---|
| **PACS** | **Os dois** estudos são reescritos com a identidade correta e re-armazenados; os dois originais são rejeitados com `113038^DCM` e apagados. |
| **Nosso banco** | Cada exame adota o UID do seu estudo novo e fica *Realizada*. |
| **Worklist** | **Ninguém volta para a worklist** — os dois exames foram feitos. Itens pendentes de ambos são removidos. |
| **Equipamento** | Nada a fazer. Nenhum exame precisa ser refeito. |

---

## O caso Patricia / João Bento é a opção 2, em dois tempos

Decisão do operador: **alterar destino** para o estudo contaminado e, **depois**, resolver o estudo
do João pela **associação manual** que já existe. (Se a associação viesse primeiro, o exame do João
já teria estudo e o caso viraria opção 3.)

| Tempo | Ação | Resultado |
|---|---|---|
| 1 | **Alterar destino** do estudo `2.25.3261…` para o exame `260804109` | Estudo reescrito para a Patricia; ela vira *Realizada* e sai da worklist. O exame do João é liberado e volta à worklist. |
| 2 | **Associar** o estudo órfão `1.2.392…8116424` ao exame `260804114` | O exame do João vira *Realizada* e sai da worklist. |

**Efeito transitório esperado:** entre os tempos 1 e 2, o João reaparece na worklist da estação
(~15 s após o passo 1) e some quando a associação é feita. Se a técnica estiver olhando a lista
nesse intervalo, verá um paciente que não deve ser examinado. Fazer os dois passos em sequência.

O aviso de "exame liberado" sai para a Patricia depois da correção; o link entregue ao João já está
revogado desde 11/08 14:44 UTC.

**A origem não volta à worklist neste caso.** O "alterar destino" ganha a opção
**"a origem já fez o exame"**: libera o exame do João sem recriar o item de worklist, porque ele
será resolvido pela associação no passo 2. Sem isso, ele reapareceria na estação por alguns minutos
e a técnica veria um paciente que não deve ser examinado.

## Estudo "voando": a associação manual passa a corrigir o DICOM

O estudo que chega sem worklist (chaves digitadas à mão no equipamento) carrega identificadores
inventados. Até aqui a associação resolvia isso **só do nosso lado** — o objeto no PACS permanecia
como veio, e a regra era explicitamente "nunca reescreve o DICOM".

**Isso muda.** Ao associar, o objeto passa a ser reescrito com a identidade do pedido —
`PatientID`, `AccessionNumber` e `PatientName` — pela mesma máquina da opção 2 (reescreve,
re-armazena com UIDs novos, rejeita e apaga o original).

Vale para todo estudo voando, não só para este caso: é o que fecha a lacuna entre "o sistema mostra
certo" e "o arquivo está certo". No caso do João, o estudo sai de
`PatientID 123456789 / accession vazio` para `PatientID = CPF / accession 260804114`.

---

## O que fica registrado

- **No PACS:** o par rejeição + objeto novo. O que foi rejeitado por worklist errada é consultável
  pelo AE `IOCM_WRONG_MWL`.
- **No nosso banco:** entrada em `registro_auditoria` com estado antes e depois, motivo digitado e
  quem executou — hoje associar/desassociar nem entram nessa trilha; esta operação entra sempre.
- **No incidente:** o `ExameIncidenteIdentidade` que abriu a quarentena é fechado como *Resolvido*,
  com a nota da correção.
