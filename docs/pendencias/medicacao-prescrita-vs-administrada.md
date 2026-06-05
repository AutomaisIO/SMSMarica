# Pendência — Medicação: prescrita vs. administrada (FHIR) no BAU

- **Status:** 🟡 Pendente — aguarda discovery no Salux antes de decidir/implementar.
- **Aberta em:** 2026-06-05
- **Origem:** discussão sobre mostrar medicamentos "dentro do BAU" (Boletim de Atendimento) em vez de por fora, na tela de Atendimentos.
- **Relaciona-se com:** [ADR-0010](../adr/0010-servico-fhir-autonomo.md) (hub FHIR), `Salux/` (engenharia reversa), memória `reference_salux_clinico_infosaude`.

## O problema

Mostrar medicamentos "dentro do BAU" exige decidir **qual** medicação: a **prescrita** (ordem médica) ou a **administrada** (o que a enfermagem efetivamente deu na unidade — inclusive em internação/FIA). São coisas clínicas diferentes, e o FHIR as separa de propósito. **Hoje só temos a prescrita fluindo** — então não dá para rotular honestamente como "administrado" sem antes resolver a fonte.

## Como o FHIR R4 trata (a régua a respeitar)

São recursos distintos, cada um com seu `status`/`intent`. Não colapsar (rigor FHIR é regra do projeto):

| Recurso | Significado | Liga-se a |
|---|---|---|
| `MedicationRequest` | **Ordem/prescrição** (médico) | `subject`→Patient, `encounter`→Encounter |
| `MedicationDispense` | **Dispensação** (farmácia liberou do estoque) | `authorizingPrescription`→MedicationRequest |
| `MedicationAdministration` | **Administração** (enfermagem deu a dose, hora/dose/via) | `request`→MedicationRequest, `context`→Encounter |
| `MedicationStatement` | "Paciente está/estava usando" (asserção/auto-relato) | `derivedFrom` |

Fluxo canônico: **Request → Dispense → Administration**. O "MAR" (o que foi administrado no internamento) = o **conjunto de `MedicationAdministration` do Encounter**, não um recurso único. Aprazamento = `MedicationRequest.dosageInstruction.timing`; **checagem** vira `MedicationAdministration`. Alinha com a RNDS/BR-Core (Prescrição = MedicationRequest, Dispensação/RDM = MedicationDispense).

⚠️ **Cheiro semântico atual:** o importador grava `MedicationRequest.status = "completed"` só porque o atendimento encerrou. `completed` ≠ "administrado" — não ler assim.

## Estado atual no projeto

- **Hub (`Automais.Fhir`):** tem `MedicationRequest` **e** `MedicationAdministration` (entidade `MedicationAdministrationRow` inclusive documenta "APRAZAMENTO do Salux"). **Não** tem `MedicationDispense`.
- **Importador (`Salux/scripts/importar_atendimentos_fhir.py`):** mapeia **só** `PRESC_BAA_OPC_PROD → MedicationRequest` (1 por item). **`MedicationAdministration` está modelado mas vazio** — nenhum script o popula.
- **Backend (`SMSMarica.server`):** `AtendimentosService` lê apenas `MedicationRequest` por Encounter (`AtendimentoDto.Medicamentos`).
- **Front:** mostra os medicamentos (prescritos) num bloco `SecaoMedicamentos` por fora do card; a impressão/PDF (`imprimirDocumento.ts`) já embute o bloco "Prescrição / Medicamentos" no documento.

## Mapeamento Salux → FHIR

| Salux (INFOSAUDE) | Recurso FHIR | Situação |
|---|---|---|
| `PRESCRICAO_BAA`/`FIA` + `PRESC_*_OPC_PROD` (`qt_material_prescrita`, `cd_via`, `cd_intervalo`, `cd_horario`) ⋈ `MATMED` | `MedicationRequest` | ✅ importado |
| Aprazamento/checagem de enfermagem — **candidatas:** `PRESCRICAO_FIA_CONDUTA` (itens c/ horário/intervalo), `PRESCRICAO_BAA_CONDUTA`; flags `in_aprazamento` na prescrição | `MedicationAdministration` | ⏳ **fonte a confirmar**, não importado |
| `MATMED_LOTE_SAIDA` (saída de material/medicamento do estoque) | `MedicationDispense` | ❌ fora de escopo por ora |

## Pergunta em aberto (o que o discovery precisa responder)

1. **Internação (FIA):** a administração/checagem é dado estruturado? Em qual tabela (`PRESCRICAO_FIA_CONDUTA`/checagem?) e com quais campos (hora real, dose, via, executante)?
2. **Urgência/ambulatorial (BAA):** a administração é estruturada ou existe **só como texto** na evolução de enfermagem (EDOC)? (Não dá pra afirmar pelos docs — precisa ver a tela real.)
3. Chave de ligação ao BAA/FIA e ao Encounter já importado.

## Próximos passos (quando retomar)

1. **Discovery no Salux** (skill `salux-capturar-tela`): abrir a tela de **checagem/administração de medicamentos** da enfermagem — um caso de **internação (FIA)** e um de **urgência (BAA)** — e capturar as queries pra cravar a(s) tabela(s)-fonte.
2. **Importador:** estender `importar_atendimentos_fhir.py` para popular `MedicationAdministration` (subject, `context`→Encounter, `request`→MedicationRequest, `effective`, `dosage.route/dose`).
3. **Backend:** incluir `MedicationAdministration` no `AtendimentoDto` (agrupado por Encounter), ao lado do prescrito.
4. **Front/BAU:** mostrar dentro do BAU, **rotulado certo** — "Administrados" (FIA/urgência) e/ou "Prescrição/Receita"; remover o bloco "por fora".
5. **ADR-0013** (opcional, recomendado): fixar a régua Request ≠ Dispense ≠ Administration e o que o BAU exibe.

## O que NÃO fazer enquanto pendente

- **Não** rotular `MedicationRequest` (prescrito) como "administrado" no BAU — viola a semântica FHIR e é incorreto clinicamente.
- **Não** colapsar prescrição/dispensação/administração num só conceito/campo.
