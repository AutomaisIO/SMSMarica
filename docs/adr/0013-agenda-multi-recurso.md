# ADR-0013 — Agenda multi-recurso: especialidade (pool), médico e equipamento

- **Status**: Aceito
- **Data**: 2026-06-05
- **Decisores**: Bernardo (product/eng)
- **Relaciona-se com / estende**: [ADR-0012](./0012-agendamento-local-e-integracao-sisreg-leitura.md) (agendamento local), [ADR-0007](./0007-schema-fhir-separado.md) (FK nullable + CHECK p/ papel único), [ADR-0010](./0010-servico-fhir-autonomo.md) (FHIR autônomo)

## Contexto

A agenda do [ADR-0012](./0012-agendamento-local-e-integracao-sisreg-leitura.md) só agenda **médico**. O fluxo real exige mais:

- **Exames de imagem** se agendam contra **equipamentos** (US, mamógrafo, tomógrafo…), não médicos. Precisamos cadastrar os equipamentos das unidades e ter agenda de equipamento.
- **Consultas** têm dois casos: a **comum** vai para **qualquer médico da especialidade** (pool), e a **revisão/retorno** tem que ser com o **mesmo médico** da 1ª vez.

## Decisão

### 1. A agenda generaliza o "alvo agendado" — pool, médico ou equipamento são só Agendas distintas

Não há flag de retorno nem worker de pool computado. Existem **três tipos de Agenda**, distinguidos por quais FKs estão setadas (padrão de FK nullable + CHECK do [ADR-0007](./0007-schema-fhir-separado.md)):

| Tipo de agenda | `Finalidade` | FKs setadas | Uso |
|---|---|---|---|
| **Especialidade (pool)** | Consulta | `EspecialidadeId` (médico null) | consulta comum — qualquer médico da especialidade |
| **Médico específico** | Consulta | `EspecialidadeId` + `MedicoId` (FHIR Practitioner) | **retorno/revisão** — mesmo médico |
| **Equipamento** | Exame | `EquipamentoId` | exame de imagem |

CHECK em `agenda`:
`(finalidade=Consulta AND especialidade_id IS NOT NULL AND equipamento_id IS NULL) OR (finalidade=Exame AND equipamento_id IS NOT NULL AND especialidade_id IS NULL AND medico_id IS NULL)`.

**O retorno é garantido MANUALMENTE**: o operador marca o retorno na agenda do médico específico. Sem vínculo formal retorno→consulta-de-origem (follow-up futuro).

A **lógica de slots** (`CalculadoraSlots`, recorrências, avulsos, bloqueios) é **idêntica** para os três tipos — só muda o que a Agenda referencia. `DuracaoConsultaMinutos` vira `DuracaoSlotMinutos` (serve consulta e exame).

### 2. `Equipamento` é entidade `smsmarica`

`smsmarica.equipamento`: nome, unidade (FK), `ModalidadeDicom` (reusa o enum), identificador DICOM opcional (AE Title/Station, p/ worklist futuro), ativo, auditoria/soft-delete. Canônico FHIR seria `Device`; fica no `smsmarica` como infra operacional (régua: identidade clínica é FHIR; agenda/equipamento são negócio). Projeta pro FHIR só se a interoperabilidade pedir.

### 3. Agendamento de exame guarda o `TipoExame`, sem ligar à `SolicitacaoExame`

`Agendamento.TipoExameId?` registra o exame marcado. Sem vínculo com `SolicitacaoExame` por ora — concilia depois. Reusa `Especialidade` e `TipoExame` existentes.

### 4. Agenda permanece não-FHIR

Confirmado: agendamento (consulta/exame) é regra de negócio em `smsmarica`; **não** vira `Schedule`/`Slot`/`Appointment` no hub. Médico/paciente continuam referenciados por id FHIR (sem FK). Reavaliar só se surgir necessidade de interoperar agenda com sistemas externos.

### 5. Permissão

`ModuloPermissao.Equipamentos = 26` (append-only). Agenda/agendamento seguem em `Agendamentos`.

## Consequências

- **Positivas:** modelo unificado (um motor de slots p/ médico, pool e equipamento); reuso de Especialidade/TipoExame/Unidade; sem complexidade de pool computado; tabelas de agenda ainda vazias em prod → refactor sem migração de dados.
- **A vigiar:** o CHECK precisa cobrir as combinações válidas; o retorno depende de disciplina do operador (manual); conciliação exame↔SolicitacaoExame e vínculo retorno→origem ficam como follow-up.

## Alternativas descartadas

- **Flag `EhRetorno` + vínculo de origem + pool computado entre médicos**: mais máquinas; o usuário preferiu "agendas distintas" (pool é uma agenda própria), o que é mais simples e idiomático.
- **Equipamento como FHIR `Device`**: desnecessário agora; é infra operacional local.
- **Agenda como FHIR Appointment/Schedule**: só se houver interoperabilidade externa (não há).
