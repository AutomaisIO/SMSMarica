# ADR-0010 — Serviço FHIR autônomo (Firely + JSONB)

- **Status**: Aceito
- **Data**: 2026-05-30
- **Decisores**: Bruno (product/eng)
- **Relaciona-se com**: [ADR-0001](./0001-schema-isolation.md), [ADR-0004](./0004-arquitetura-tres-projetos.md) (supersede parcial), [ADR-0007](./0007-schema-fhir-separado.md) (supersede parcial), [ADR-0008](./0008-mapeamento-clinico-fhir.md), [ADR-0009](./0009-identidade-e-proveniencia-multi-pep.md), [visao.md](../visao.md)

## Contexto

[ADR-0007](./0007-schema-fhir-separado.md) colocou o modelo FHIR num schema `fhir` **embutido** no mesmo `SmsMaricaDbContext` do backend de transporte, com FKs cross-schema `smsmarica → fhir`, e deixou explícito que "não há servidor FHIR separado por ora — fica para quando aparecer 2º consumidor". Os ADRs [0008](./0008-mapeamento-clinico-fhir.md) e [0009](./0009-identidade-e-proveniencia-multi-pep.md) ampliaram a ambição do hub: dezenas de recursos clínicos (Encounter, Observation, Questionnaire, MedicationRequest, DiagnosticReport, Provenance...) e identidade multi-PEP com MPI.

Com essa ambição, o hub FHIR deixa de ser "um schema do backend de transporte" e passa a ser **o produto central** — um repositório canônico e eterno que múltiplos sistemas (transporte, futuros PEPs, indicadores) consomem. Manter isso embutido no `SMSMarica.server` acopla o hub ao ciclo de vida de um consumidor específico e impede reuso por outros projetos.

Decisão: extrair o FHIR para um **serviço .NET autônomo e replicável**, com o qual os demais sistemas falam **via API**.

## Decisão

### 1. Serviço autônomo, solução própria — namespace `Automais.Fhir`

O serviço é um **produto portável**, reutilizável por qualquer prefeitura — não é específico de Maricá. Por isso o namespace/branding é **`Automais.Fhir`** (empresa), não `SMSMarica.Fhir`. Vive no monorepo do SMSMarica por ora (mesmo GitHub/secrets), mas é desenhado para ser extraído e levado a outro município sem reescrita.

Nova solução `Automais.Fhir/Automais.Fhir.slnx`, espelhando o padrão 3-camadas + testes do backend ([ADR-0004](./0004-arquitetura-tres-projetos.md)):

- `Automais.Fhir.Data` — EF Core + Npgsql; dono do schema `fhir`.
- `Automais.Fhir.Core` — serviços de domínio, validação, regras de consistência.
- `Automais.Fhir.Api` — REST FHIR (`/fhir/Patient`...), OperationOutcome, OpenAPI/Scalar.
- `Automais.Fhir.Tests`.

Dependências: `Data ← nada` · `Core ← Data` · `Api ← Core + Data` · `Tests ← os três`. Mora dentro do mesmo repositório/GitHub do SMSMarica (monorepo), mas é **deployável e versionável de forma independente**.

### 2. Firely SDK (`Hl7.Fhir.R4`) como modelo canônico

Os recursos FHIR são representados pelos modelos oficiais do **Firely SDK** (`Hl7.Fhir.R4`, v6.2.0) — serialização JSON/XML conforme a spec e validação contra perfis (incluindo perfis **RNDS/BR**) sem reimplementar nada. Brasilidades (CNS, CPF, raça/cor, etnia, nacionalidade, município IBGE) entram via `identifier.system` canônicos e `extension` dos perfis nacionais — a RNDS já **é** FHIR R4. Usa-se o **SDK (biblioteca)**, não o produto "Firely Server".

### 3. Persistência JSONB (document-store)

Cada recurso é guardado **inteiro** numa coluna `jsonb` (do jeito que o Firely serializa), em **uma tabela por tipo** (`fhir.patient`, `fhir.practitioner`, ...). Ao lado do documento, cada tabela carrega:

- **metadados comuns**: `id`, `version_id`, `last_updated`, `meta_source` (NOT NULL, [ADR-0009](./0009-identidade-e-proveniencia-multi-pep.md)), `is_deleted` (DELETE FHIR é lógico).
- **search params extraídos**: poucas colunas indexadas (ex.: `cpf`, `cns`, `nome`, `nascimento` no Patient) — cópias indexadas de campos do documento, só para busca sem abrir o JSON.

GET devolve o documento inteiro; PUT/POST gravam o documento inteiro — sem montar/desmontar. Adicionar um recurso novo = "mais uma tabelinha igual", sem explosão de modelagem. **Aposenta as 25 tabelas tipadas** do Lado A (ver Consequências).

### 4. Mesmo Postgres, schema próprio, serviço 100% separado

O serviço usa o **mesmo cluster Postgres** (DigitalOcean `defaultdb`) para não criar custo de banco novo, mas em **schema `fhir` próprio** e com **DbContext próprio** (`FhirDbContext`, `HasDefaultSchema("fhir")`). Co-habitam apenas no banco físico. Connection string é config isolada do serviço — trocar/mover o banco depois é só mudar a string, sem tocar em código.

### 5. Escopo: REST FHIR + camada de consistência

O serviço entrega **ambos** desde já: superfície REST FHIR conforme (para consumidores externos) **e** as regras de consistência do hub (validação, `meta.source`/`Provenance`, `Patient.link`/MPI). A consistência não é opcional — é a razão de o hub existir.

### 6. Identidade própria; sem front (API-only)

Sendo autônomo, o serviço tem **identidade própria** (usuários + JWT do FHIR, **não** os do `smsmarica`) — fatia futura, `Authentication.JwtBearer` previsto.

**Não tem front próprio: o hub é API-only.** O papel de front (uso operacional — consultar/cadastrar paciente) é do **`smsmarica.online`** e futuros PEPs, que consomem a API FHIR. Construir UI de paciente no FHIR duplicaria o consumidor. Um eventual **console de admin do hub** (MPI/`Patient.link`, proveniência, consentimento LGPD, terminologia) — que não é uso operacional — poderia justificar um front no futuro, mas está fora de escopo agora. *(Revisão 2026-05-30: a ideia inicial de front próprio foi descartada e o scaffold `Automais.Fhir.front` removido.)*

### 7. Sem FK navegável cross-serviço

O `smsmarica` deixa de ter FK navegável para `fhir`. As referências (`tratamento.patient_id`, `usuario.patient_id`/`practitioner_id`, `laudo.*`, `solicitacao_exame.patient_id`) viram **id lógico** resolvido **via API FHIR**. Isso reverte parte das Fatias 1–4 ([ADR-0007](./0007-schema-fhir-separado.md)) — feito conscientemente.

## Alternativas consideradas

### A. Manter FHIR embutido no SMSMarica.server (status quo do ADR-0007)
**Prós:** DbContext único, transação/navegação atômica cross-schema, menos infra. **Contras:** acopla o hub ao backend de transporte; impede reuso por outros projetos; não escala para o hub multi-PEP de 0008/0009. **Rejeitada** — o hub virou produto central, não detalhe de um consumidor.

### B. Persistência relacional tipada (reusar as 25 tabelas do Lado A)
**Prós:** SQL puro vê tudo; reaproveita o modelado. **Contras:** N tabelas + mapeamento bidirecional Firely↔POCOs por recurso; insustentável para os ≥12 recursos de 0008. **Rejeitada** — JSONB resolve crescimento com custo marginal ~zero (decisão 3).

### C. Servidor FHIR pronto (Firely Server / Spark / HAPI)
**Prós:** REST/validação de fábrica. **Contras:** injetar regras próprias (MPI, proveniência multi-PEP) é difícil; foge do "serviço .NET próprio" controlável. **Rejeitada** — usa-se o SDK e constrói-se a casca REST enxuta.

### D. Banco físico separado
**Prós:** isolamento total. **Contras:** custo de mais um banco gerenciado. **Rejeitada** — mesmo cluster, schema próprio, connection string isolada dá 95% do isolamento a custo zero (decisão 4).

## Consequências

### Positivas
- Hub reusável e deployável por qualquer projeto que fale FHIR.
- Crescer para Encounter/Observation/etc. (0008) custa ~uma tabela jsonb cada.
- Conformidade RNDS via Firely sem reimplementar serialização/validação.
- Proveniência (0009) nativa: `meta_source` em toda tabela, `Provenance` como recurso.

### Negativas
- **Reverter Fatias 1–4**: dropar as 25 tabelas tipadas (vazias → risco zero) e trocar as FKs cross-schema por referência lógica resolvida via API. Trabalho real, assumido agora para não doer mais tarde.
- Consumo do `smsmarica` passa a depender de chamada HTTP ao FHIR (latência/resiliência a tratar — cache/retry).
- Dois deployables a operar (backend + FHIR), cada um com seu CI/secret.

### Cutover (coordenação obrigatória)
Ambos os lados miram o **mesmo schema físico `fhir`** no `defaultdb`. Portanto:
1. Enquanto se constrói o serviço FHIR, **nada é aplicado** ao banco (migration gerada, não executada).
2. No cutover: (a) `smsmarica` dropa as 25 tabelas tipadas + remove entities/configs/FKs cross-schema; (b) o serviço FHIR aplica sua migration `InicialFhirSchema` (cria `fhir.patient` jsonb etc.); (c) `smsmarica` passa a resolver Patient/Practitioner via API FHIR.
3. Lookups (`municipio_ibge`, `cbo_ocupacao`...) — decisão à parte: viram `CodeSystem`/`ValueSet` FHIR ou tabelas de referência. Não bloqueiam.

### Condições para revisitar
- Se a latência da resolução via API doer no `smsmarica`, avaliar cache de leitura/réplica local de identificadores.
- Se surgir necessidade de banco fisicamente separado (compliance, escala), a connection string isolada (decisão 4) torna a mudança barata.

## Enforcement
- Toda entidade FHIR vive no serviço `Automais.Fhir.*`, schema `fhir`, modelo JSONB (documento + search params). Code review rejeita tabela tipada "explodida" por campo.
- `meta_source` NOT NULL em toda tabela (`ResourceRow`); systems de identifier centralizados em `FhirSystems`; URIs de origem em `MetaSources`.
- `smsmarica` **não** cria FK para `fhir`; referência é id lógico via API. Code review rejeita FK cross-schema nova.
- Recursos serializados/parseados **só** via Firely SDK (`FhirJson`); erros viram `OperationOutcome`.
- Migrations do serviço FHIR só são aplicadas após o cutover (drop das tabelas tipadas no smsmarica).
