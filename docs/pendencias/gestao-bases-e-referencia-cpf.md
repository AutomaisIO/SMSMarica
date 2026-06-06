# Pendência — Gestão global de bases de dados + referência por CPF

- **Aberta em**: 2026-06-06
- **Relaciona-se com**: [ADR-0014](../adr/0014-importacao-salux-no-backend.md) (importação de PEPs),
  [ADR-0009](../adr/0009-identidade-e-proveniencia-multi-pep.md) (identidade/proveniência multi-PEP),
  [ADR-0011](../adr/0011-modulo-ia-consulta-linguagem-natural.md) (módulo IA / fontes).

Duas evoluções pedidas pelo usuário.

## 1. Promover "Bancos de Dados" a um espaço global e enriquecido

Hoje o cadastro de base (`IaFonte`) vive **dentro do módulo Inteligência** — mas a config é
**global**: serve à IA **e** à importação de PEPs (e futuros consumidores). O slug, inclusive,
é **herdado** dessa config na importação.

Proposta:
- **Seção própria** (top-level), ex.: "Bancos de Dados" / "Fontes", fora de Inteligência. A IA e a
  Sincronização PEP passam a apontar pra ela.
- **Enriquecer a tela** (não ser "só um banco numa lista"):
  - **Estado de conectividade** ao vivo (testar conexão inline; último OK/erro; latência).
  - **Estatísticas**: nº de tabelas, contagem de linhas das principais (paciente/medico/baa/edoc),
    tamanho, versão do banco — via queries leves (`COUNT`, `user_tables`, `v$version`) só-leitura.
  - **Metadados**: tipo de PEP, ambiente, **slug**, quem consome (IA / Importação), última
    sincronização (cruza com `pep_sincronizacao_execucao`).
  - Casa com o **painel de saúde da base** da pendência [qualidade-dados-importacao](./qualidade-dados-importacao.md).
- Migração suave: a entidade `IaFonte` pode continuar como está (ou ser renomeada/movida num ADR);
  o importante é a UX virar um hub de gestão de bancos.

## 2. Sistemas de negócio referenciarem por CPF (não perder a referência da realidade)

Transporte/translados, imagem (PACS/laudos/solicitações), agendamento etc. referenciam
paciente/médico pelo **id FHIR (Guid)**. Risco: se o recurso FHIR for recriado numa reimportação,
os vínculos apontam pra um id que sumiu — perde-se a "referência da realidade".

- **Mitigação já feita**: o upsert canônico de Patient/Practitioner agora é **PUT in-place**
  (dedup por CPF, merge de identifiers) — o **id FHIR é estável** entre reimportações. Logo, os
  vínculos atuais por Guid **sobrevivem** ao reimport no caminho normal.
- **Robustez desejada (próximo)**: ancorar os vínculos de negócio na **chave nacional (CPF / CNS /
  conselho)** — gravar o CPF junto do Guid, ou resolver paciente/médico por CPF na leitura. Assim,
  mesmo num delete+recreate (ou troca de hub), o vínculo com a pessoa real se mantém.
- Aplicar onde houver `*FhirId`/`MedicoId`/`PatientId` de negócio (agendamento, solicitações,
  translados, laudos…). Avaliar um resolvedor central "CPF ⇄ Patient/{id}".

## Encaminhamento
- (1) e (2) são features próprias — construir após validar a importação multi-base em produção.
- Princípio reforçado: a **identidade real** (CPF/CNS/conselho) é a âncora; o id FHIR é detalhe de
  implementação que deve permanecer estável, mas nunca ser a única amarra com a realidade.
