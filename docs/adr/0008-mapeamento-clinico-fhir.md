# ADR-0008 — Mapeamento de eventos clínicos Salux → FHIR R4

- **Status**: Aceito
- **Data**: 2026-05-30
- **Decisores**: Bruno (product/eng)
- **Relaciona-se com**: [ADR-0001](./0001-schema-isolation.md) (isolamento de schema), [ADR-0007](./0007-schema-fhir-separado.md) (schema `fhir` canônico separado), [visao.md](../visao.md) (hub FHIR R4)

## Contexto

O [ADR-0007](./0007-schema-fhir-separado.md) estabeleceu o schema `fhir.*` canônico como fonte de verdade da **identidade** clínica — hoje cobre `patient`, `practitioner`, `organization`, `consent` e 6 lookups. **Não há nenhum recurso de evento clínico** (atendimento, diagnóstico, prescrição, exame, documento preenchido).

A engenharia reversa do **Salux HIS** (sistema hospitalar legado Oracle do HCML — ver `Salux/docs/levantamento-banco.md`) revelou o modelo clínico que precisaremos espelhar/integrar:

- **Atendimento dual**: praticamente toda tabela clínica tem versão `FIA` (Ficha de Internação Ambulatorial = internação) e `BAA` (Boletim de Atendimento Ambulatorial = urgência/eletivo). Chave composta `(CD_HOSPITAL, DT_ANO, NR)`.
- **eDoc**: motor de formulários **EAV** (Entity-Attribute-Value). 333 modelos de documento; `EDOC_MOVIMENTO` (5M instâncias) + `EDOC_MOVIMENTO_ITEM` (166M respostas, todas em `DS_RESPOSTA VARCHAR2(4000)`). Tudo que é "documento" (boletim, evolução, escala, receituário, atestado, sumário de alta) é um eDoc.
- **Prescrição/medicação**: `PRESCRICAO_FIA`/`PRESCRICAO_BAA` + itens em `PRESC_*_OPC_PROD`, catálogo `MATMED`, aprazamento em `PRESCRICAO_FIA_CONDUTA`.
- **Exames/laudos**: `SOL_EXAME` + `SOL_EXAME_ITEM`, `LAUDO` + `COMPOSICAO_LAUDO`.

Antes de modelar qualquer entidade nova em `fhir.*`, é preciso fixar **como** esses conceitos se traduzem para FHIR R4. Este ADR registra esse mapeamento como decisão arquitetural. **Não cria tabelas nem migrations** — é o "norte" que cada fatia futura de implementação (cada uma com seu próprio ADR/migration) vai seguir.

**Por que ADR e não só doc de discovery**: o mapeamento fixa decisões não-óbvias e disputáveis — BAA/FIA colapsam num único `Encounter`; eDoc vira `Questionnaire`/`QuestionnaireResponse` em vez de tabelas tipadas; estratégia em camadas (QR genérico + extract seletivo). Sem o racional registrado, um futuro contribuidor poderia reverter por engano (ex.: criar `fhir.baa` e `fhir.fia` separados espelhando o Salux).

## Decisão

### 1. BAA e FIA → recurso único `Encounter` (discriminado por `class`)

O FHIR **não tem recurso separado** para internação vs ambulatório. O modelo dual do Salux colapsa num único `Encounter`, discriminado pelo campo `class` (v3-ActCode):

| Salux | `Encounter.class` |
|---|---|
| BAA eletivo | `AMB` (ambulatorial) |
| BAA urgência/emergência (`IN_EMERGENCIA='S'`) | `EMER` |
| FIA (internação) | `IMP` (inpatient) |

Mapeamento de campos:

| Salux | FHIR | Nota |
|---|---|---|
| `(CD_HOSPITAL, DT_ANO_FIA/BAA, NR_FIA/BAA)` | `Encounter.identifier` | systems `urn:salux:fia` / `urn:salux:baa` |
| `FIA.ID_INTERNACAO` (U/E) | detalhe de `class`/`type` | urgência vs eletiva |
| `BAA.IN_BAA_ATENDIDO` (N/S/E) + estados FIA | `Encounter.status` | planned → arrived → triaged → in-progress → finished |
| `BAA.ID_DESTINO` (Casa/Internação/Óbito/Transf/Encaminhado) | `Encounter.hospitalization.dischargeDisposition` | mapeia quase 1:1 |
| `FIA_LEITO` (histórico de transferências) | `Encounter.location[]` | cada troca = entry com `period` + `status` |
| `FIA_MEDICO` (médico responsável) | `Encounter.participant` → `Practitioner` | já existe em `fhir.*` |
| hospital | `Encounter.serviceProvider` → `Organization` | já existe em `fhir.*` |
| `TRIAGEM` / `CLASSIFICACAO_RISCO` (cor) | `Observation` (triage) ligado ao Encounter | cor de risco = Observation |

### 2. Encounter é a espinha dorsal

Todo recurso clínico referencia o atendimento via `.encounter`; todo recurso referencia o cidadão via `.subject` → `Patient`. Isso dá dois eixos de consulta:

- **Prontuário longitudinal**: tudo onde `subject = Patient`.
- **Uma visita**: filtra por `encounter`.

```
Patient ──subject──┐
                   ↓
              Encounter (BAA/FIA)
                   ↑ .encounter
  ┌──────────┬─────┼──────────┬─────────────────────┐
Observation Condition MedicationRequest ServiceRequest QuestionnaireResponse
                                              ↓ .basedOn
                                         DiagnosticReport (LAUDO)
```

### 3. eDoc → `Questionnaire` / `QuestionnaireResponse` (módulo SDC)

O eDoc é um motor de formulários data-driven. O par `Questionnaire` (template) + `QuestionnaireResponse` (instância preenchida) é o **par canônico** do FHIR para exatamente esse caso (módulo SDC — Structured Data Capture). **Não é forçar a barra** — é o uso textbook.

| Salux eDoc | FHIR |
|---|---|
| `EDOC_DOCUMENTO` + `EDOC_DOCUMENTO_ITEM` + `EDOC_ITEM` + `EDOC_TIPO_ITEM` (18 widgets) | `Questionnaire` (`.item.type`: boolean/decimal/integer/date/dateTime/time/string/text/choice/open-choice mapeia os 18 tipos) |
| `EDOC_ITEM_VALORES` (opções de listbox) | `Questionnaire.item.answerOption` |
| `EDOC_REGRA_CONDICAO` (lógica se-então) | `Questionnaire.item.enableWhen` |
| `EDOC_GRUPO_ITEM` (aninhamento) | `Questionnaire.item` aninhado |
| `EDOC_MOVIMENTO` + `EDOC_MOVIMENTO_ITEM` (`DS_RESPOSTA`) | `QuestionnaireResponse` (`.item.answer.value[x]`) |
| `EDOC_MOVIMENTO.IN_STATUS` (P/D) | `QuestionnaireResponse.status` (in-progress/completed) |
| autor (`CD_FUNCIONARIO_INC`) | `QuestionnaireResponse.author` → `Practitioner` |
| paciente / atendimento | `.subject` → `Patient` / `.encounter` → `Encounter` |

### 4. Camadas, não recurso-por-formulário

A modelagem é em camadas — **não** se cria um recurso tipado para cada um dos 333 modelos:

1. **Todo** eDoc → `QuestionnaireResponse` (lossless, genérico — resolve os 333 de uma vez).
2. **Os de alto valor** projetam *também* para recurso tipado, via padrão SDC "extract":

   | Salux | FHIR tipado |
   |---|---|
   | Escalas Glasgow/Braden/Morse (eDoc) | `Observation` (com código LOINC) |
   | `SINAIS_VITAIS` (tabela própria, 115 col — não é eDoc) | `Observation` perfil vital-signs |
   | CID em FIA/BAA | `Condition` |
   | `PACIENTE_X_ALERGIA` | `AllergyIntolerance` |
   | Resumo de alta / evolução | `Composition` |

3. **PDF assinado** (`EDOC_MOVIMENTO.DOC_ASSINADO` BLOB + `HASH_ASSINATURA_DIGITAL`) → `DocumentReference` + `Binary`.

### 5. Medicação / prescrição

Módulo de medicação do FHIR, encaixe limpo:

| Salux | FHIR | Papel |
|---|---|---|
| `MATMED` (catálogo) | `Medication` | o medicamento (código ANVISA/CATMAT/CMED) |
| `PRESCRICAO_FIA/BAA` + `PRESC_*_OPC_PROD` | `MedicationRequest` | a **ordem** prescrita |
| `PRESCRICAO_FIA_CONDUTA` (aprazamento/checagem) | `MedicationAdministration` | o que foi **administrado** |
| `REQUISICAO` (dispensação farmácia) | `MedicationDispense` (opcional) | dispensação |

Campos da linha de prescrição → `MedicationRequest.dosageInstruction` (datatype `Dosage`): `cd_via`→`route`, `cd_horario`/`cd_intervalo`→`timing`, `qt_material`→`doseAndRate`, `in_criterio_medico`/ACM→`asNeeded`, `in_urgencia`→`priority`, `in_controlado`/`in_alto_risco`→extensões. `ID_TIPO_PRESCRICAO` (M/F/O/L) → `category` (inpatient/outpatient) + papel do requester.

### 6. Exames / laudos

| Salux | FHIR |
|---|---|
| `SOL_EXAME` + `SOL_EXAME_ITEM` | `ServiceRequest` (pedido) |
| `LAUDO` + `COMPOSICAO_LAUDO` | `DiagnosticReport` (`.result` → `Observation`; `.presentedForm` → PDF via `laudo_url`) |

### 7. Ordem sugerida de implementação futura

Cada bloco será uma fatia própria (ADR-0009+ se introduzir nova regra dura; senão, só entidade + migration):

1. **`Encounter`** — destrava todo o resto (é a espinha).
2. **`Condition` + `Observation`** — diagnóstico, sinais vitais e escalas.
3. **`Questionnaire` / `QuestionnaireResponse`** — substrato genérico dos 333 eDocs.
4. **`Medication` / `MedicationRequest` / `MedicationAdministration`**.
5. **`ServiceRequest` / `DiagnosticReport`** — exames e laudos.
6. **`DocumentReference` / `Composition`** — PDFs assinados e documentos estruturados.

### 8. Este ADR não implementa nada

O escopo é "como mapeia", não "crie as tabelas". Nenhuma entidade, configuration ou migration é criada por este ADR.

## Alternativas consideradas

### A. Modelar cada um dos 333 modelos de eDoc como recurso FHIR tipado próprio

**Prós:** dados discretos imediatamente consultáveis sem parser de QR.
**Contras:** explosão de recursos e configurations; impraticável de manter; muitos formulários não têm recurso FHIR correspondente (seriam distorções). É o caso clássico de "forçar a barra". **Rejeitada** — usa-se `QuestionnaireResponse` como substrato universal e extrai-se tipado só onde há valor (decisão 4).

### B. Manter BAA e FIA como dois recursos distintos espelhando o Salux

**Prós:** tradução 1:1 do legado, menos pensamento de modelagem.
**Contras:** o FHIR unifica atendimento em `Encounter` com discriminador `class`; criar `fhir.baa`/`fhir.fia` separados quebraria interoperabilidade (todo integrador FHIR espera `Encounter`) e duplicaria toda a árvore de recursos clínicos. **Rejeitada** (decisão 1).

### C. Ignorar `QuestionnaireResponse` e só extrair dados tipados

**Prós:** modelo final mais "limpo" (só recursos tipados).
**Contras:** perda lossless — a maioria dos 333 formulários não tem recurso tipado adequado, e a fidelidade do documento original (campos, ordem, respostas livres, assinatura) se perderia. Inviável para prontuário legal. **Rejeitada** — QR genérico preserva tudo; extract é complementar.

## Consequências

### Positivas

- Interoperabilidade: quando Salux/eSUS/RNDS pedirem `Encounter`, `MedicationRequest` ou `DiagnosticReport`, o schema já está no formato canônico.
- `Encounter` como espinha dá um grafo clínico claro e navegável (longitudinal por Patient, por-visita por Encounter).
- `QuestionnaireResponse` resolve os 333 eDocs de uma vez sem explosão de tabelas, preservando fidelidade total (lossless).
- Extract seletivo dá o melhor dos dois mundos: documento íntegro + dados discretos consultáveis (escalas, sinais, CID, alergias).

### Negativas

- Volume de recursos a modelar é grande (≥ 12 recursos FHIR novos ao longo das fatias). Mitigado pela ordem priorizada (decisão 7).
- O padrão extract **duplica** o dado entre `QuestionnaireResponse` e o recurso tipado (ex.: escore de Glasgow vive na QR e numa Observation). É duplicação intencional — a QR é o documento, a Observation é o dado consultável. Disciplina de geração (a QR é truth source; o tipado é projeção) precisa ficar clara na implementação.
- `Encounter` com chave composta do Salux exige `identifier` com system dedicado e cuidado na deduplicação (paciente unificado via `CD_PACIENTE_UNIFICADO` — usar `NVL(cd_paciente_unificado, cd_paciente)`).

### Condições para revisitar

- Se a integração com o Salux for **bidirecional** (gravar de volta), reavaliar como `Encounter.status`/`hospitalization` se reconciliam com os triggers de auditoria do Salux (ver `Salux/capturas/investig_triggers_audit.txt`).
- Se aparecer um perfil nacional (RNDS/IPS-BR) que dite codificação específica para escalas/sinais, alinhar os códigos LOINC/SNOMED das Observations a ele.

## Enforcement

- Todas as entidades clínicas novas vivem em `fhir.*` (inglês snake_case); FK cross-schema só na direção `smsmarica → fhir` ([ADR-0007](./0007-schema-fhir-separado.md)).
- `Encounter.identifier` usa systems `urn:salux:fia` / `urn:salux:baa`; um único `SmsMaricaDbContext`.
- BAA/FIA **não** geram recursos separados — sempre `Encounter` com `class`. Code review rejeita `fhir.baa`/`fhir.fia`.
- eDoc genérico **sempre** gera `QuestionnaireResponse`; recurso tipado é projeção complementar, nunca substituto da QR.
- Cada fatia de implementação referencia este ADR; mudanças de regra dura exigem ADR próprio (0009+).
