# ADR-0009 — Identidade e proveniência multi-PEP no hub FHIR

- **Status**: Aceito
- **Data**: 2026-05-30
- **Decisores**: Bruno (product/eng)
- **Relaciona-se com**: [ADR-0007](./0007-schema-fhir-separado.md) (schema `fhir` canônico), [ADR-0008](./0008-mapeamento-clinico-fhir.md) (mapeamento clínico Salux → FHIR), [visao.md](../visao.md) (hub FHIR R4 como repositório eterno)

## Contexto

A [visão](../visao.md) posiciona o SMSMarica como **repositório canônico e eterno** da SMS Maricá: uma cópia padronizada, longitudinal, dos dados clínicos que hoje vivem em **vários sistemas operacionais distintos** — Salux HIS (HCML), eSUS APS, o PACS próprio (dcm4chee, ver [pacs.md](../pacs.md)), e futuros PEPs das unidades. Os sistemas operacionais continuam fora do FHIR ([ADR-0008](./0008-mapeamento-clinico-fhir.md): "estado operacional fora, desfecho clínico projetado pra dentro").

Isso levanta duas perguntas que precisam de resposta arquitetural antes de modelar qualquer recurso clínico:

1. **Proveniência**: dado que o hub é réplica multi-fonte, como saber **a qualquer momento, de qual sistema veio** cada dado? Sem isso, indicadores não conseguem segmentar por fonte, auditoria não rastreia, e o "repositório eterno" vira um amontoado sem linhagem.

2. **Identidade do Patient**: o `Patient` é o dado **central** e parte dele vem de fontes diferentes (nome no Salux, endereço no eSUS, prontuário no SGH). Um mesmo cidadão existe como cadastro distinto em cada PEP, e dentro de um mesmo PEP pode ter duplicatas (Salux: `CD_PACIENTE_UNIFICADO`). Como organizar isso sem cair em rastreamento campo-a-campo (inviável) nem em sobrescrita silenciosa que apaga divergências?

Este ADR fixa as duas decisões. Não cria tabelas/migrations — é o "como", que as fatias de implementação seguem.

## Decisão

### 1. Proveniência por **recurso**, nunca por campo

A granularidade de proveniência é o **recurso FHIR**, não o elemento. Não se rastreia "este telefone veio do eSUS, este endereço veio do Salux" — element-level provenance (via extensão) é peso desproporcional e quase nunca consultado. Cada recurso clínico é projetado a partir de **um** sistema-fonte e carrega a origem desse sistema.

### 2. Três mecanismos combinados (do leve ao completo)

| Mecanismo | Campo/recurso | Papel |
|---|---|---|
| **`meta.source`** | coluna `meta_source` (URI) **em toda tabela `fhir.*`** | Carimbo de origem sempre-presente, custo-zero de consulta. Resposta ao "de qual sistema veio". Ex.: `https://smsmarica.saude.marica/source/salux`, `.../source/esus`, `.../source/pacs` |
| **`Identifier.system`** | tabelas `*_identifier` (já existem) | Traceback fino até a **linha exata** no sistema-fonte. Ex.: `urn:salux:cd_paciente\|34948`. Cada recurso (não só Patient) mantém o id nativo da fonte no `identifier[]` |
| **`Provenance`** | nova tabela `fhir.provenance` | Trilha de auditoria rica: **quando** entrou, por **qual** carga/pipeline, a partir de **qual** registro-fonte, qual **atividade** (create/transform/merge) |

**`meta.source` é obrigatório** em todo recurso `fhir.*`. `Provenance` é gerado por **evento de sincronização** (uma Provenance por carga, com `target[]` = recursos gravados, `agent.who` = `Device`/`Organization` do sistema-fonte + pipeline de ETL, `entity[]` = referência ao registro-fonte, `recorded` = timestamp da ingestão, `activity` = create/transform/merge).

URIs de origem (`meta.source`) centralizados em constantes, como os CodeSystems do [ADR-0007](./0007-schema-fhir-separado.md) — não duplicar strings.

### 3. Patient organizado como **registros-fonte + master** (não golden-record mesclado)

Cada cidadão é representado por:

- **Um `Patient` por PEP** — fiel à sua fonte, `meta.source` único e trivial (o recurso inteiro veio de um sistema só). Re-sync do Salux mexe só no Patient-Salux; não contamina o Patient-eSUS. Conflitos entre fontes (Salux diz nascido em 1943, eSUS diz 1944) ficam **visíveis**, não sobrescritos.
- **Um `Patient` "master"** (índice de paciente / MPI) que consolida e é o que o app do cidadão e os indicadores leem. `meta.source` do master = o **processo de MPI do hub**, não um PEP.

Os registros-fonte e o master se amarram por `Patient.link` (decisão 4). A consolidação (qual nome/endereço "vale" no master) é trabalho explícito do MPI — registrado em `Provenance` cujo `entity[]` lista **todos** os registros-fonte que contribuíram.

**Consequência prática para proveniência**: como cada Patient-fonte é mono-fonte, nunca surge a pergunta "qual campo veio de onde". Se um dia for preciso saber a origem de um campo específico do cidadão, basta ir ao Patient-fonte correspondente. Resource-level resolve tudo.

### 4. `Patient.link` é o mecanismo canônico da teia de identidade

`Patient.link` é elemento padrão do FHIR R4 e **já está modelado** (`fhir.patient_link` + enum `LinkType`). Os 4 tipos e seus usos:

| `type` | Significado | Uso no hub |
|---|---|---|
| `seealso` | Ambos válidos, mesma pessoa em lugares diferentes | **Patient-fonte ↔ master** (cidadão existe no Salux *e* no eSUS) |
| `replaced-by` | Registro saiu de uso; aponta o que o substituiu | Lado **aposentado** de uma fusão de duplicatas |
| `replaces` | Registro substitui outro (sobrevivente) | Lado **sobrevivente** da fusão |
| `refer` | Em uso, mas outro tem mais dados | Sistema referencia outro mais completo |

**Mapeamento do Salux**: o `CD_PACIENTE_UNIFICADO` (fusão de duplicatas, lógica `NVL(cd_paciente_unificado, cd_paciente)`) é um par `replaced-by`/`replaces` — o cadastro duplicado aponta `replaced-by` para o unificado, e o unificado `replaces` de volta. Cross-sistema ("mesma pessoa no Salux e no eSUS") é `seealso` entre cada Patient-fonte e o master.

`Patient.identifier[]` carrega **um identifier por sistema** (já modelado), cada um com seu `system` → permite saber em quais PEPs o cidadão existe sem percorrer os links.

## Alternativas consideradas

### A. Golden record mesclado (um único Patient com campos consolidados de todas as fontes)

**Prós:** consumo trivial (um Patient por pessoa, já mesclado); menos recursos.
**Contras:** o recurso tem dados multi-fonte → proveniência só resolvível em element-level (pesado). Conflitos entre fontes são resolvidos **silenciosamente** (perde a divergência, que às vezes é clinicamente relevante). Re-sync de uma fonte arrisca sobrescrever dado de outra. **Rejeitada** — mantém-se o master como *visão consolidada derivada*, mas os registros-fonte mono-fonte são preservados (decisão 3).

### B. Element-level provenance (rastrear origem campo a campo)

**Prós:** máxima granularidade.
**Contras:** complexidade e volume desproporcionais; raramente consultado; o FHIR desencoraja para réplica. **Rejeitada** — granularidade por recurso (decisão 1) cobre as necessidades reais de indicador/auditoria.

### C. Só `meta.source`, sem `Provenance`

**Prós:** mínimo esforço.
**Contras:** `meta.source` diz *de onde*, mas não *quando entrou*, *de qual carga*, nem *qual registro-fonte exato* — insuficiente para auditoria de ingestão e para reconstruir merges. **Rejeitada** — `meta.source` é o piso (sempre presente), `Provenance` é a trilha completa por sync.

## Consequências

### Positivas

- Origem de qualquer dado é consultável a qualquer momento via `meta.source` (custo-zero) + traceback fino via `identifier.system`.
- Patient-fonte mono-fonte mantém proveniência trivial e preserva fidelidade ao PEP de origem; conflitos ficam visíveis.
- Indicadores podem segmentar por fonte (`WHERE meta_source = ...`); auditoria de ingestão reconstrói cargas via `Provenance`.
- `Patient.link` (padrão FHIR, já modelado) cobre tanto fusão de duplicatas quanto a teia cross-sistema, mapeando direto no `CD_PACIENTE_UNIFICADO` do Salux.

### Negativas

- **N Patient-recursos por pessoa** (um por PEP + master). Mais linhas, e o app/consultas precisam saber ler o master (não os fontes). Mitigado: o master é o ponto de entrada único para consumo.
- O **MPI (matching/linkage)** vira um componente real a construir — decidir quando dois Patient-fonte são a mesma pessoa (CNS/CPF como âncora forte; nome+nascimento+mãe como heurística). Não trivial; fica como peça de roadmap própria.
- `meta_source` em toda tabela `fhir.*` é uma coluna a mais em cada entidade — incluir no template de modelagem desde a primeira (Encounter), senão vira migration retroativa.
- `Provenance` é mais um recurso/tabela a modelar e popular em todo pipeline de ingestão.

### Condições para revisitar

- Se o hub passar a ser **fonte primária** (algum dado nasce no SMSMarica, não replicado), `meta.source` desse recurso aponta para o próprio SMSMarica e a Provenance é create, não transform.
- Se um perfil nacional (RNDS) exigir Provenance com estrutura/assinatura específica, alinhar a tabela ao perfil.
- Se o volume de Patient-fonte se mostrar problemático, reavaliar colapsar para golden-record com Provenance.entity preservando linhagem (trade-off da alternativa A).

## Enforcement

- Toda tabela `fhir.*` carrega `meta_source` (URI, NOT NULL). Code review rejeita entidade FHIR nova sem ele.
- Todo recurso projetado de um PEP mantém o id nativo da fonte em `identifier[]` com `system` dedicado ([ADR-0007](./0007-schema-fhir-separado.md) §5; novos: `urn:esus:...`, `urn:salux:fia/baa` etc.).
- Pipelines de ingestão geram uma `Provenance` por evento de sync, com `agent` = sistema-fonte + ETL e `entity` = registro-fonte.
- Patient: **um recurso por PEP + master**, nunca um golden-record mesclado sem linhagem. Consolidação registrada em `Provenance`. Consumo (app cidadão, indicadores) lê o **master**.
- `Patient.link`: `seealso` para teia cross-sistema; `replaced-by`/`replaces` para fusão (= `CD_PACIENTE_UNIFICADO`).
- URIs de `meta.source` centralizados em constantes — não duplicar strings na codebase.
