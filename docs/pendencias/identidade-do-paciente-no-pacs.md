# Proposta — identidade do paciente no PACS: rastro primeiro, CPF como eixo depois

**Status:** **fases 1, 2 e 3 executadas em 2026-09-08.** O CPF é o código único do paciente no PACS.
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

### Fase 2 — Unir os 6 duplicados — **FEITA em 2026-09-08**

Pelo **merge nativo** do dcm4chee, não por `UPDATE` na mão: o merge preserva o vínculo
antigo→novo (`merge_fk`), que é justamente o que uma diligência precisa.

`POST /aets/PACS-CDT/rs/patients/{prior}/merge/{alvo}`, com os pacientes endereçados na forma
`PatientID^^^IssuerOfPatientID` — seis chamadas, todas **HTTP 204**. Backup do estado anterior
(JSON dos 6 pacientes e de todos os seus estudos) em `/root/pacs-backups/merge-cpf-20260908/antes.json`.

O **alvo** foi sempre o registro cujo nome bate com o cadastro do hub; o **prior** foi o registro
com o defeito que originou a divisão:

| Paciente | Prior (absorvido) | Alvo (sobreviveu) |
|---|---|---|
| JOAO BENTO | registro vazio, 0 estudos | o que tinha a MG de 11/08 |
| LUCIA CRISTINA | nome truncado `...CASTRO GOM` | nome completo, = cadastro |
| ANGELICA | o criado ontem pelo RX (DX 08/09) | o da MG de 28/05, com histórico |
| MATHAUS | issuer com hash de nascimento `null` | o com nascimento íntegro |
| REGINA CELIA | `PINHEIRO^REGINA CELIA` (nome de solteira) | `MALHEIROS^REGINA CELIA PINHEIRO`, = cadastro |
| SULEIDE | `SOUZA^SULEIDE^NASCIMENTO^DE` (pontuação `^`) | `SOUZA^SULEIDE NASCIMENTO DE` |

**Verificado:** cada um dos 6 CPFs devolve **1 registro** e conserva **todos** os estudos que os
dois tinham somados; a varredura do arquivo inteiro passou de **4.781 para 4.775** pacientes e
mostra **zero** CPF duplicado. Os registros absorvidos continuam no banco com `merge_fk` apontando
para o alvo — que é o rastro que a fase 1 foi feita para preservar.

> **Achado do caminho:** o segundo registro da ANGELICA foi criado **pelo próprio RX do CDT** — o
> estudo `DX` de 08/09 (prefixo `1.2.392.200036.9107`, Konica) está lá **sem accession number**.
> Como o item de worklist nunca chegou (era o 409), a recepção digitou a paciente no console. Ou
> seja: o 409 não só travava o envio, ele **causava** o órfão que depois precisa de associação
> manual.

### Fase 3 — CPF como eixo, o "C" — **FEITA em 2026-09-08**

O nome saiu da identidade: quem manda agora é o par **`PatientID` = CPF + `IssuerOfPatientID` = `CPF`**.

**O que foi medido antes de decidir** (leitura do arquivo inteiro, 4.775 pacientes):

| Forma do `PatientID` | Pacientes | Estudos | O que é |
|---|---:|---:|---|
| **CPF (11 dígitos)** | **1.660** | 1.553 | nosso backend |
| numérico curto (2–3 díg.) | 2.650 | 1.422 | acervo legado importado |
| texto / outros | 326 | 232 | digitado no aparelho |
| Guid | 5 | 0 | paciente sem CPF (ADR-0041) |

Dos 1.660: 1.469 com o hash do nome, **187 sem issuer nenhum**, 4 com `SMSMARICA`.

**O achado que decidiu o desenho:** existiam **25 pacientes de CPF sem issuer e com estudos**. Como
a coerção sempre carimba um issuer no C-STORE, isso prova que **issuer ausente funciona como
coringa** no casamento do dcm4chee — e que ele **não** preenche o nulo depois. Foi o coringa que
impediu centenas de duplicatas; só escaparam as 6 da fase 2.

Isso **descartou** a alternativa "deixar todo mundo sem issuer": aparelho que manda issuer próprio
— e existem três no acervo (`SMSMARICA`, `HOSPITAL`, `SENDING_FAC`) — criaria um segundo registro, e
o coringa casaria com os dois, trazendo o 409 de volta. O único desenho que garante um registro por
CPF **aconteça o que acontecer no aparelho** é carimbar um valor constante, incondicionalmente.

#### O que foi feito

**1. Normalização dos registros existentes.** `PUT /aets/PACS-CDT/rs/patients/{pid}^^^{issuer}` com
`IssuerOfPatientID=CPF` no corpo — **1.472 em 22 segundos**, 1 erro (o registro-canária, que já
estava em `CPF`). Backup dos 1.660 com atributos completos em
`/root/pacs-backups/fase3-issuer-cpf-20260908/antes-pacientes-cpf.json`.

> Os **187 sem issuer ficaram como estão**, e isso é limite do dcm4chee, não esquecimento: ele
> **recusa** trocar a identidade a partir de "sem issuer" — `Previous Patient ID "X" matches new
> Patient ID "X^^^CPF" and change patient id tracking is enabled`. A saída seria desligar o
> *change patient id tracking*, que é justamente a proteção contra troca ambígua de identidade —
> não vale o preço. E não precisa: como coringa, eles casam com qualquer issuer do mesmo CPF.

> **Bônus de rastro:** a troca de identidade no dcm4chee é implementada como **merge** (cria a
> linha nova e aponta a antiga com `merge_fk`). Cada uma das 1.472 mudanças ficou registrada.

**2. Coerção.** As 13 regras existentes (`SupplementIssuerOfPatientID` em 3 AEs de C-STORE +
`...OnMPPS` em 10) ganharam a condição `PatientID!=^[0-9]{11}$`, e nasceram 13 irmãs
`SupplementIssuerCPF*` com `PatientID=^[0-9]{11}$` ⇒ `IssuerOfPatientID=CPF`. As novas são
**incondicionais** — de propósito: sobrescrevem até o issuer que o aparelho mandar. Aplicado por
`ldapmodify` (nunca por `PUT /devices`, que descarta `dcmMWLWorklistLabel` em silêncio) + `ctrl/reload`.
Backup em `device-antes.ldif`; verificado depois que os 16 AEs respondem e que os 6 labels de
worklist continuam de pé.

**3. Backend.** `ConstrutorMwlItem` passou a emitir `(0010,0021)` no item de MWL e no `POST /patients`;
`IdentidadeDicom` carrega o issuer e a reescrita o grava no objeto. **Só quando o `PatientID` é o
CPF** — paciente sem CPF entra com o Guid e **sem** issuer, porque para esse formato a regra antiga
continua valendo e um issuer nosso faria o registro deixar de casar com a imagem devolvida.
Travado por `tests/SMSMais.Tests/Worklist/IssuerOfPatientIdTests.cs`.

#### Estado final

`CPF`: **1.473** · coringa (sem issuer): **187** · com hash do nome: **0**. Acervo legado e
digitados no aparelho **intocados**. Mesma contagem de pacientes (4.775) e de estudos de antes —
nada se perdeu.

#### O que ficou de fora, de propósito

Os 2.650 do legado (lá o issuer separa **14 pessoas diferentes** que dividem o mesmo ID curto) e os
326 digitados no aparelho — esses só têm conserto pela associação, que é onde a fase 1 age.

### O que NÃO fazer

- **Não mexer na coerção do dcm4chee.** Ela é o que segura o legado de pé.
- **Não simetrizar com o CNS.** Ver [[feedback_cadsus_guarda_identidade_assimetrica]].

## Verificação

Associar um estudo e conferir que o original aparece em `IOCM_WRONG_MWL` (e não sumiu); desfazer a
associação e ver o original restaurado; conferir que `rejected_instance` deixa de ficar zerado;
depois do merge, os 6 CPFs passam a ter um registro só e o `260908005` chega à worklist.
