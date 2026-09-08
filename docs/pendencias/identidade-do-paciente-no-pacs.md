# Proposta — identidade do paciente no PACS: rastro primeiro, CPF como eixo depois

**Status:** direção aprovada em 2026-09-08; fases 1 e 2 a executar, fase 3 a detalhar.
**Relacionado:** [`pacs.md`](../pacs.md) · [ADR-0041](../adr/0041-identidade-incompleta-no-hub.md)
(paciente sem CPF entra marcado) · `CorrecaoIdentidadeExameService` · `PacsReescritorEstudoClient`

## O que motivou

A radiografia `260908005` não chegou ao aparelho: `dcm4chee respondeu 409 ao registrar o paciente`.
A paciente (real, com mamografia laudada em junho) tinha **dois registros** no dcm4chee com o mesmo
CPF e `IssuerOfPatientID` diferentes — o `POST /patients` ficou ambíguo e o PACS recusou.

A causa está numa coerção configurada no próprio dcm4chee:

```
IssuerOfPatientID = DCM4CHEE.{PatientName,hash}.{PatientBirthDate,hash}
```

Como o backend **não envia** issuer, o PACS fabrica um a partir do **nome** e do **nascimento**.
Consequência: **o nome virou parte da identidade** — muda o nome, vira outra pessoa.

### Medição (08/09/2026)

| Forma do `PatientID` no PACS | Quantos | Origem |
|---|---:|---|
| ID curto (`063`, `094`) | 2.651 | acervo legado importado |
| **CPF** | **1.666** | nosso backend, com CPF |
| outro (`23072026`, `LUCINEA DE ALMEIDA`) | 369 | **digitado no aparelho** |
| **Guid** | **95** | nosso backend, **sem** CPF |

E o fallback sem CPF não é teórico: **77 em agosto, 12 em setembro**. O
`ConstrutorMwlItem.PatientId` manda o CPF quando existe e o **Guid do paciente** quando não existe.

**Duplicatas por CPF: 6** — e cada uma por um motivo banal, o que mostra a fragilidade da regra:

| CPF | O que mudou |
|---|---|
| ANGELICA | nome idêntico hoje; foi editado depois (cadastro atualizado em 25/08) |
| JOAO BENTO | nome idêntico, mesmo dia; um registro ficou com 0 estudos |
| MATHAUS | segundo registro com hash de nascimento `null` — chegou sem data |
| REGINA CELIA | `PINHEIRO^REGINA CELIA` × `MALHEIROS^REGINA CELIA PINHEIRO` (sobrenome de casada) |
| LUCIA CRISTINA | `...CASTRO GOMES DOS` × `...CASTRO GOM` (nome truncado) |
| SULEIDE | `SOUZA^SULEIDE NASCIMENTO DE` × `SOUZA^SULEIDE^NASCIMENTO^DE` (**só a pontuação `^`**) |

> ⚠️ Os 254 `pat_id` com mais de um issuer **não** são 254 problemas. A maioria é acervo legado com
> IDs curtos, onde há **14 pessoas diferentes** dividindo o mesmo ID — ali o issuer está fazendo o
> trabalho certo e mexer nele seria o erro. O problema real são os 6 acima.

## O que o dcm4chee oferece de rastro (e o que está desligado)

| Recurso | Estado hoje |
|---|---|
| **Merge nativo** (`patient.merge_fk`) — preserva o registro antigo apontando para o novo | **0 usos** |
| **Rejeição IOCM com motivo** — usamos `113038^DCM` *Incorrect Modality Worklist Entry* | usado, **mas apagamos logo depois** |
| **Revoke Rejection** — desfaz a rejeição e restaura o estudo | disponível, nunca usado |
| **Job de expurgo de rejeitados** (`dcmDeleteRejectedPollingInterval: PT5M`) | ativo |
| **Audit Trail DICOM** (`audit.log`) | **0 bytes desde ago/2024** — nunca ligado |
| Histórico de atributos | **não existe** — `dicomattrs` guarda o estado atual |

E do nosso lado, `exame_associacao` já guarda `AccessionNumberDicomOriginal`,
`StatusSolicitacaoAnterior`, a origem (manual × automática) e quem/quando — **mas não** o
`PatientID`, o nome nem o `StudyInstanceUID` anteriores.

## O apagamento que ninguém escolhe

Em `PacsReescritorEstudoClient.ReescreverIdentidadeAsync`, quando alguém **associa** um estudo:

1. baixa cada instância, reescreve com a identidade correta e re-armazena (estudo **novo**, UID novo);
2. confirma que o novo existe — boa guarda: se o STOW falhar, aborta e preserva o original;
3. **chama `DescartarAsync(uid_original)`, que rejeita _e_ apaga.**

Não se perde imagem (os pixels foram para o estudo novo). Perde-se **a prova de como aquilo
chegou**: sob que identidade e sob que UID. É por isso que `rejected_instance` está zerado.

O outro caminho que apaga — `DescartarEstudoAsync` — é **deliberado**: exige motivo e recusa com
laudo assinado. Esse está protegido. O problema é o apagamento **automático** dentro da associação.

## Decisão

### Fase 1 — Rastro (primeiro, e é o que torna o resto reversível)

1. **`exame_associacao` guarda a identidade anterior**: `PatientIdDicomOriginal`,
   `NomePacienteDicomOriginal`, `StudyInstanceUidOriginal`. Migration pequena, sem tocar em dado
   existente.
2. **A reescrita para de apagar** — só rejeita (`113038`). O original fica no acervo, visível pelo
   AE `IOCM_WRONG_MWL` e recuperável por *Revoke Rejection*.
3. **Expurgo em 90 dias**, pela retenção **nativa** do dcm4chee (não por rotina nossa). Cobre o caso
   real — associação errada é percebida em dias ou semanas — sem crescer para sempre.
4. **Desfazer associação** passa a ser possível: restaura o original e remove o reescrito.

> **Custo, medido no próprio código:** a mamografia do CDT é gravada **sem compressão, ~56 MB por
> instância, ~224 MB o estudo**. Guardar o original dobra isso por associação, **durante 90 dias**.
> O storage é S3 (DigitalOcean Spaces): é custo mensal, não parede.

### Fase 2 — Unir os 6 duplicados

Pelo **merge nativo** do dcm4chee, não por `UPDATE` na mão: o merge preserva o vínculo
antigo→novo (`merge_fk`), que é justamente o que uma diligência precisa. Destrava o `260908005`.

### Fase 3 — CPF como eixo (o "C")

O backend passa a **enviar** `IssuerOfPatientID` derivado de algo estável, tirando o nome da
identidade. **Não pode ir sozinha:** os 1.666 registros de CPF existentes têm o issuer velho; mudar
só o envio criaria 1.666 pares novos — a duplicação que se quer eliminar, em escala. A fase exige
normalizar os existentes, com simulação, contagem antes/depois e reversibilidade, como foi feito no
backfill do escopo.

**Fica fora do alcance da fase 3, de propósito:** os 2.651 do legado (issuer está certo lá, separa
pessoas distintas) e os 369 digitados no aparelho — esses só têm conserto pela associação, que é
onde a fase 1 age.

### O que NÃO fazer

- **Não mexer na coerção do dcm4chee.** Ela é o que segura o legado de pé.
- **Não simetrizar com o CNS.** Ver [[feedback_cadsus_guarda_identidade_assimetrica]].

## Verificação

Associar um estudo e conferir que o original aparece em `IOCM_WRONG_MWL` (e não sumiu); desfazer a
associação e ver o original restaurado; conferir que `rejected_instance` deixa de ficar zerado;
depois do merge, os 6 CPFs passam a ter um registro só e o `260908005` chega à worklist.
