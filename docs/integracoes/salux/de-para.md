# De-para Salux → FHIR R4 — o mapeamento REAL (derivado do código)

**Fonte da verdade:** `SaluxFhirMapper.cs` + `SaluxLinhas.cs` + SQLs da `SaluxImportacaoStrategy.cs`
(working tree, inventariado por auditoria em **01/08/2026**). Este documento descreve **o que o
código faz**, não o que os ADRs desejam — onde os dois divergem, a divergência está anotada.
Manutenção: toda mudança no mapper atualiza este arquivo no mesmo commit (candidato a teste:
todo `identifier system` e URL de extensão referenciados no mapper devem constar aqui).

Legenda das colunas de destino:

- **nativo** — campo FHIR de verdade (buscável, interoperável);
- **extras** — par chave/valor no `urn:salux:extras` (JSON string; sobra curada do de-para);
- **descartado** — lido da origem e jogado fora em silêncio;
- **nem lido** — a coluna existe na origem e o SQL nem a traz.

A seção de **perdas** é obrigatória por regra deste catálogo: um de-para que só lista sucesso
esconde exatamente o que importa saber antes de desligar o sistema de origem.

Identifiers: todo valor interno é prefixado pelo slug da base (`{slug}:{valor}`), e o
`meta.source` é sempre `https://smsmarica.saude.marica/source/salux/{slug}` (ADR-0009).

---

## Practitioner (`MedicoLinha`, 18 campos — nenhum descartado)

| Origem | Destino | Detalhe |
|---|---|---|
| cpf / cns | nativo | identifiers nacionais (`sid/cpf`, `sid/cns`) |
| crm + uf + conselho | nativo | identifier `urn:br:conselho:{sigla}:{uf}` + `qualification` |
| rg + órgão | nativo | identifier + `assigner.display` |
| cd | nativo | `urn:salux:cd_medico` prefixado |
| nome | nativo | `name.text` |
| ativo / sexo / nasc / email | nativo | `active` / `gender` / `birthDate` / `telecom` |
| especialidade | nativo | `qualification.code.text` |
| mãe, pai, órgão, categoria, cbo, especialidade (dup.) | extras | |

## Patient (`PacienteLinha`, 53 campos)

| Origem | Destino | Detalhe |
|---|---|---|
| cpf, cns, rg+órgão, pis, passaporte, rne, certidão, sgh, cem, cd_paciente | nativo | identifiers (cd_paciente = `urn:salux:cd_paciente`) |
| nome + nome social/flag | nativo | `name` official / nickname |
| ativo / sexo / nasc | nativo | `active` / `gender` / `birthDate` |
| óbito | nativo | `deceasedDateTime` |
| logr, nr, compl, bairro, cep, cidade, uf, ref | nativo | `address` (ref vira `address.text` "Ref: …") |
| estado_civil_ds | nativo | `maritalStatus.text` |
| ddd+fone / email | nativo | `telecom` (**sujeito ao merge**: confirmado é intocável — `PatientMergeFhir`) |
| mãe, pai, cônjuge, responsável+fone | nativo | `contact` MTH/FTH/SPS/GUARD |
| cd_cor, nacionalidade, país, profissão, ocupação, **peso, altura, sangue, rh**, etnia, grau_parentesco, entrada_pais, estado_civil (código), escolaridade, religião, barreira_comunicacao | extras | ⚠️ peso/altura/tipo sanguíneo em extras, **não** viram Observation; grau_parentesco não vinculado ao contact do responsável |
| dt_cadastro / dt_alteracao | consumido fora | só marca d'água |

## Encounter AMB/EMER (`BaaLinha`)

| Origem | Destino | Detalhe |
|---|---|---|
| emerg (`in_emergencia`) | nativo | `class` AMB/EMER |
| dt_cheg\|dt_atend → dt_saida | nativo | `period` |
| chave (h-ano-nr) | nativo | identifier `urn:salux:baa` — **carrega o CD_HOSPITAL** (eixo unidade, ADR-0039) |
| médico | nativo (raso) | `participant.individual.display` — **SÓ display**, sem referência a Practitioner |
| cid / cid_ds | derivado | `Condition` própria (identifier `:cond`); CID limpo de †/‡/*/+ (`LimparCodigoCid`), original preservado em `code.text` |
| risco_ds | derivado | `Observation` survey `urn:salux:classificacao-risco` (`:risco`) |
| status | fixo | **sempre `finished`** (divergência do ADR-0008, que previa in-progress) |

## MedicationRequest (`PrescricaoLinha`)

| Origem | Destino | Detalhe |
|---|---|---|
| cd_mat + mat | nativo | `medicationCodeableConcept` `urn:salux:matmed` |
| urg = 'S' | nativo | `priority` urgent |
| médico | nativo (raso) | `requester.display` |
| chave_baa + nr_prescricao + seq_item | nativo | identifier `urn:salux:presc` |
| qt / horário / obs | **achatado** | `dosageInstruction[0].text` ("Qtd: X — horário — obs") — o ADR-0008 §5 previa Dosage estruturado |
| status / intent | fixo | completed / order |

## DocumentReference (`EdocLinha`)

| Origem | Destino | Detalhe |
|---|---|---|
| modelo | nativo | `type.text` + `attachment.title` |
| dt (dt_episodio) | nativo | `date` |
| chave_doc (h-ano-idm) | nativo | identifier `urn:salux:edoc` |
| baa | nativo | `context.encounter` |
| itens EAV | **achatado** | `attachment.data` = HTML `text/html` renderizado (ADR-0008 mandava Questionnaire/QuestionnaireResponse SDC — nunca implementado) |
| dt_incl | consumido fora | marca d'água do CDC |

## Observation de vitais (derivadas do eDoc, `EdocItemLinha`)

Heurística por rótulo do item (pressão / pulso / freq. cardíaca / freq. respiratória /
sat O2 / temperatura) → **5 tipos**: identifier `urn:salux:edoc` `{chave}:{pa|fc|fr|temp|spo2}`,
LOINC 85354-9 (+8480-6/8462-4), 8867-4, 9279-1, 8310-5, 2708-6, unidades UCUM.
Só o **1º valor por tipo** (preferência ao "acolhimento").

## Encounter IMP (`FiaLinha`) — ADR-0025 B1

| Origem | Destino | Detalhe |
|---|---|---|
| dt_baixa / dt_alta | nativo | `period` |
| (derivado) | nativo | `status` finished / in-progress / unknown (janela 120d) |
| carater | nativo | `priority` EL/UR (lookup SUS) |
| nr_obito (presença) | nativo | `hospitalization.dischargeDisposition = exp` |
| chave (h-ano-nr) | nativo | identifier `urn:salux:fia` — carrega o CD_HOSPITAL |
| cid / cid_ds | derivado | `Condition` `:cond` |
| dt_previsao_alta, dt_alta_medica, carater, carater_ds | extras | |

## Encounter.location (`FiaLeitoLinha`) e Location (wa/ro/bd)

Tudo usado: `dt_transferencia`/`dt_saida_leito` → `period` (active/completed); leito → referência
`Location` resolvida no hub. Unidade (`wa`): nome + condição→status. Quarto (`ro`): nome; extras
`in_isolamento`, `sexo`. Leito (`bd`): condição→status (I→inactive, F→suspended); extras `id_leito`.

## EDOC_MOVIMENTO_LOG (`EdocLogLinha`)

`IN_OPERACAO` é lido e **deliberadamente ignorado** — o CDC relê a origem e decide
(existe → upsert; sumiu → tombstone escopado por `meta.source`). Documentado no código.

---

## ⚠️ O QUE SE PERDE (a seção que importa)

### Descartado no mapper (lido e jogado fora)

| Item | Consequência |
|---|---|
| **Tipo do widget e itens não-vitais do eDoc como dado discreto** | ficam SÓ no HTML — nenhuma escala (Glasgow, Braden, Morse…) vira dado consultável |
| Itens do eDoc com resposta NULL | filtrados no SQL — "não respondido" é indistinguível de "não perguntado" |
| Vitais além do 1º valor por tipo | série temporal dentro do mesmo documento se perde |
| seq_docto, cd_item_grupo, cd_item | só ORDER BY |
| O **valor** de nr_obito (FIA) | só a presença é usada |

### Nem lido do Oracle (o SQL não traz)

| Item | Consequência |
|---|---|
| **`EDOC_MOVIMENTO.DOC_ASSINADO` + `HASH_ASSINATURA_DIGITAL`** | **o PDF assinado — documento legal — nunca chega ao hub.** Recuperável só com o Oracle vivo (plano §7) |
| `EDOC_MOVIMENTO.IN_STATUS` (P/D) | rascunho e definitivo entram iguais |
| `CD_FUNCIONARIO_INC` (autor do eDoc) | documento sem autor no hub |
| `cd_modelo` (código do modelo) | só o nome descritivo vai |
| BAA: `in_baa_atendido`, `id_destino`, `cd_medico` (como FK), triagem | destino da urgência (ex.: internação, `id_destino='I'` → `admitSource=emd` do ADR-0025) não mapeado |
| Prescrição: `cd_via`, `in_criterio_medico`, `in_controlado`, `in_alto_risco`, `id_tipo_prescricao` | via de administração e flags clínicas fora do hub |
| FIA: `id_internacao` (deliberado — convenção de recepção), `fia_medico` | médico responsável pela internação fora |

### Divergências código × ADR (anotadas, decisão pendente)

1. eDoc → `DocumentReference` HTML em vez de `Questionnaire`/`QuestionnaireResponse` (ADR-0008 §3) — decisão fazer×adiar no plano §7.
2. `MedicationRequest.dosageInstruction` texto livre em vez de Dosage estruturado (ADR-0008 §5).
3. `Encounter.diagnosis` com use AD/DD (ADR-0025) substituído por Condition avulsa sem `use`.
4. `admitSource=emd` + chave do BAA de origem em extras (ADR-0025 B1) não implementados.
5. Médico como `display` em participant/requester — sem resolução para o Practitioner canônico.
