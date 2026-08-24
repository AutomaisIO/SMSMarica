# Solicitação de Exame — dicionário de campos

Documentação de referência dos campos de uma **Solicitação de Exame** (entidade
`SolicitacaoExame`, schema `smsmarica`). Os textos de ajuda exibidos pelo ícone **"?"**
na tela de preenchimento manual (`AjudaCampo`) são a versão resumida deste documento —
fonte única em `SMSMais.front/src/features/solicitacoes-exame/ajudaCampos.tsx`.

## As três datas (e por que são diferentes)

O ponto que mais confunde: uma solicitação tem **datas distintas** que não devem ser
misturadas. Elas existem justamente para permitir medir **tempos médios** entre as etapas
(ex.: solicitação → regulação → agendamento → execução).

| Campo | Coluna | Tipo | O que é | Origem |
|-------|--------|------|---------|--------|
| **Data da solicitação** | `data_solicitacao` | `date` | Dia em que o exame foi **pedido** pelo profissional (data da guia/pedido). | Manual ou SISREG (coluna 29 do TXT). |
| **Data da regulação** | `data_regulacao` | `date` | Dia em que a **regulação autorizou** a solicitação. Capturada só para números/estatística. | SISREG (coluna 31 do TXT). Não editável no form manual. |
| **Data/hora agendada** | `data_agendada` | `timestamptz` (UTC) | Momento em que o exame está **marcado para acontecer**. | Manual ou SISREG (colunas 6+7, hora local de Brasília → UTC). |
| **Data de cadastro** | `criado_em` | `timestamptz` (UTC) | Momento em que o **registro entrou no nosso sistema** (auditoria). Não é uma data clínica. | `DateTime.UtcNow` na criação/importação. |
| **Data do exame** | `data_estudo` | `timestamp` (wall-clock) | Data/hora **REAL de execução**, lida do DICOM (StudyDate/StudyTime) quando o estudo chega ao PACS. Fonte da verdade da data do exame/laudo. | DICOM. |
| **Detectado no PACS** | `realizado_em` | `timestamptz` (UTC) | Quando o servidor **detectou** o exame no PACS (auditoria). Fallback de exibição quando o DICOM não trouxe a data. | Servidor. |

> Na importação SISREG, `data_solicitacao`, `data_regulacao` e `data_agendada` vêm
> preenchidas automaticamente do "Arquivo Agendamento (TXT)". Ver
> `AgendaTxtParser` (índices de coluna) e `ImportacaoSisregService`.

## Demais campos

- **Paciente** — cidadão do hub FHIR (`paciente_id` → `fhir.patient`). Selecionado por busca.
- **Tipo de exame** (`tipo_exame_id`) — define modalidade DICOM, tempo estimado e o código
  SIGTAP do procedimento. Controla também se a solicitação é enviada ao worklist do PACS.
- **Unidade executora** (`unidade_id`) — onde o exame é **realizado** (ex.: CDT).
- **Unidade solicitante** (`unidade_solicitante_id`, opcional) — quem **pediu** o exame
  (ex.: a USF de origem). Distinta da executora; as duas podem diferir.
- **Solicitante** (`solicitante_nome`) — nome do profissional/unidade que pediu, como texto
  livre. Não cadastramos médico nem CRM/COREN por aqui; o nome aparece no laudo e na capa.
- **Código de Solicitação** (`codigo_solicitacao`) — nº da solicitação na regulação (SISREG);
  chave de idempotência da importação. Regra: número a partir de **9999**, ou **0000**
  (sentinela) para exame emergencial extra-SUS.
- **Chave de Confirmação** (`chave_confirmacao`) — confirma a marcação na regulação; mesma
  régua do código (≥ 9999 ou 0000).
- **Prioridade** (`prioridade`) — `Eletiva` / `Prioritária` / `Urgente`.
- **Justificativa** (`justificativa`) — hipótese diagnóstica / motivo do exame.
- **Observações** (`observacoes`) — informações adicionais para o operador do equipamento.

## Chaves DICOM (geradas pelo sistema, não editáveis)

- **AccessionNumber** (`accession_number`) — `SMS{aaaa}{seq6}`, viaja no Study (0008,0050).
- **StudyInstanceUID** (`study_instance_uid`) — UID pré-gerado do estudo.
- **WorklistItemUid** — UID do item no dcm4chee (permite cancelar/atualizar depois).
