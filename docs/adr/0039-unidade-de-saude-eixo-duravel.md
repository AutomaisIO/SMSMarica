# ADR-0039 — Unidade de saúde é o eixo durável do dado clínico; o PEP é proveniência transitória

**Status:** aceito · **Data:** 2026-08-01
**Implantação:** **em produção desde 04/08/2026** — 3 Organizations criadas com o CNES medido
(Conde 2266733, UPA Inoã 7164440, Sta Rita 2266792) e 156/156 dos Encounters do primeiro ciclo
pós-deploy saíram com `serviceProvider`. A ponte do CNES foi confirmada contra a segunda base:
o Klinikos declara **7164440** para a "UPA MARICA" — mesma unidade, nome diferente.

## Contexto

O hub FHIR nasceu dirigido por PEP: cada conector (Salux hoje, Klinikos a caminho) carimba
`meta.source` e anexa identifiers da fonte ([ADR-0009](./0009-identidade-e-proveniencia-multi-pep.md)).
Isso responde bem "**de qual sistema** veio" — mas nada no hub responde "**onde** aconteceu".
Medido em produção (01/08/2026):

| Fato medido | Número | Leitura |
|---|---|---|
| `Encounter.serviceProvider` preenchido | **0** recursos | o hub não sabe a unidade de nenhum atendimento |
| Recursos `Organization` no hub | **0** (recurso inexistente) | não há o que referenciar |
| Encounters com `CD_HOSPITAL=1` (HCM Conde) | 689.950 — até 20/06/2026 | Conde **ainda opera no Salux**; migrará para o Klinikos |
| Encounters com `CD_HOSPITAL=2` (UPA Inoã) | 268.151 — parou em 07/04/2025 | cutover para o Klinikos em abr/2025 |
| Encounters com `CD_HOSPITAL=3` (Sta Rita) | 163.620 — parou em 08/04/2025 | idem |

A tabela conta a história inteira: **a unidade sobrevive ao PEP**. UPA Inoã e Santa Rita já
trocaram de sistema (Salux → Klinikos em abril/2025) sem deixar de ser as mesmas unidades; o
Conde fará o mesmo caminho. Se a unidade estivesse codificada no `meta.source` ou no slug da
base, a linha do tempo clínica de cada unidade quebraria em "era Salux" e "era Klinikos" —
exatamente o que um repositório longitudinal não pode fazer.

Dois insumos técnicos já existem e foram verificados:

1. **O mecanismo de idempotência está provado.** A auditoria do motor de sincronismo
   ([ADR-0024](./0024-sincronismo-continuo-salux.md)/[0025](./0025-internacao-salux-no-hub-fhir.md))
   em 01/08 confirmou o desenho central: upsert condicional por identifier, marcas por fase
   com `MAX` da origem, lag de 5 min, CDC do eDoc por faixa de PK. É esse mecanismo — não um
   novo — que resolve a `Organization`.
2. **O dado do backfill já está dentro do hub.** O `CD_HOSPITAL` de cada atendimento viaja
   nos identifiers `urn:salux:baa` / `urn:salux:fia` (`{slug}:{hosp}-{ano}-{nr}`). Ligar os
   Encounters existentes às suas unidades é releitura do que o hub já tem, não reimport da
   origem.

## Decisão

**A unidade de saúde passa a ser o eixo durável do dado clínico no hub; o PEP é proveniência
transitória.** Sete decisões, em ordem de importância:

1. **Dois eixos ortogonais, nunca misturados.** `meta.source` responde "de qual sistema
   veio" (ADR-0009); a **unidade** responde "onde aconteceu". Salux ontem, Klinikos hoje,
   outro amanhã — o PEP passa, a unidade fica. **Proibido** codificar unidade dentro de
   `meta.source` ou de slug de base. Um `meta.source` por unidade (ou um slug `salux-inoa`)
   parece atalho e é bomba-relógio: o próximo cutover de PEP partiria a história da unidade
   ao meio.

2. **`Organization` vira recurso de 1ª classe no hub**, resolvida por **upsert condicional
   por identifier** — idempotente, mesmo mecanismo do ADR-0024 §5. A âncora é o **id interno**
   do hub; **CNES é identifier, não chave primária** (CNES muda — recredenciamento, correção
   de cadastro). Cada PEP anexa o seu código interno como identifier próprio:
   `urn:salux:hospital|{slug}:{cd}`, `urn:klinikos:unidade|{slug}:{cd}`. É a mesma filosofia
   do Patient canônico do ADR-0009: uma âncora interna + N identifiers de fonte, e cada fonte
   nova só **anexa** o seu identifier ao recurso que já existe.

3. **Referências FHIR padrão, e só onde fazem sentido.** `Encounter.serviceProvider` →
   `Organization`; `Location.managingOrganization` → `Organization`;
   `Organization.partOf` fica disponível para hierarquia futura (rede/município), sem uso
   obrigatório agora. **`Patient` NÃO recebe unidade** — pessoa não pertence a unidade; o
   vínculo com o lugar é do **evento clínico**, nunca do cidadão.

4. **O cadastro `smsmarica.unidade` NÃO se relaciona com o schema `fhir`** (decisão
   explícita). O hub se resolve **sozinho**, a partir da ORIGEM (tabela `HOSPITAL` do Salux,
   cadastro de unidades do Klinikos) ou, na ausência de dado na origem, de um **mapa mínimo
   na configuração da base** (`IaFonte`: código da fonte → CNES/nome). Sem FK cross-sistema,
   sem chamada ao painel, sem dependência de deploy conjunto. Quando o painel precisar
   correlacionar, correlaciona por **CNES em consulta** — dependência na direção
   consumidor → hub, nunca hub → consumidor.

5. **Fail-closed.** Encerrado o período de transição/backfill, **recurso clínico novo não
   entra no hub sem unidade resolvida**. Código de unidade desconhecido na origem →
   **fila de divergência** (a exemplo do SISREG→SIGTAP e do escopo fail-closed do
   [ADR-0037](./0037-escopo-unidade-fail-closed.md)) — nunca gravação cega, nunca descarte
   silencioso.

6. **Critério verificável de "consolidado"** — três propriedades testáveis, sem juízo
   subjetivo:
   - **(a)** rodar o conector N vezes produz o **mesmo hub** (mesmos ids lógicos, mesmas
     contagens);
   - **(b)** **nenhum** recurso clínico novo sem unidade;
   - **(c)** o hub **não conhece "Maricá"** — só conhece Organizations com CNES. É a
     propriedade (c) que permite replicar o case em outra cidade ou outra instalação Salux
     sem tocar no motor.

7. **Backfill do estoque existente de Encounters (~1,3 milhão)** via **passe de reconciliação
   idempotente — modo Completo SEM purga** — lendo o `CD_HOSPITAL` que **já está** nos
   identifiers `urn:salux:baa`/`urn:salux:fia` (`{slug}:{hosp}-{ano}-{nr}`). O slug
   `salux-hcml` **não muda** — mudar o slug seria justamente violar a decisão 1
   (unidade contrabandeada para dentro da proveniência).

## Consequências

- **Ordem de fases no conector**: a resolução de `Organization` roda antes das fases
  clínicas (ou junto, com cache por run do mapa código-da-fonte → id de Organization). O
  upsert condicional garante que N conectores e N execuções convergem para a mesma
  Organization — nunca duplicam.
- **Automais.Fhir ganha o recurso `Organization`** com o mesmo ferramental dos demais:
  colunas de busca de identifier, índice único parcial e endpoint de *conditional update*
  (`PUT /fhir/Organization?identifier=system|value`), conforme ADR-0024 §5. `Location`
  (ADR-0025) passa a apontar `managingOrganization` — internações ganham dono institucional.
- **Pré-condição do passe sem purga**: a auditoria do motor (01/08) confirmou que o modo
  Completo sem purga só é seguro quando **todo** o estoque casável carrega identifier — no
  estado auditado ainda existia legado de POST cego sem identifier (~1,12 M Encounters), que
  um Completo sem purga **duplicaria**. O passe da decisão 7 lê identifiers já gravados;
  antes de rodá-lo, conferir que a cobertura de identifiers no estoque é 100% (ou tratar o
  resíduo à parte). A propósito: os três `CD_HOSPITAL` medidos somam 1.121.721 — o número
  redondo ~1,3 M é estimativa do estoque no momento da execução; medir na hora.
- **O contrato do conector muda**: depois da transição, "unidade não resolvida" deixa de ser
  ignorável e vira item de fila de divergência com gestão (quem resolve, como resolve —
  tipicamente completando o mapa mínimo da `IaFonte` ou corrigindo a origem). O fim do
  período de transição é uma **chave explícita**, não um comportamento implícito.
- **Portabilidade vira propriedade de projeto**: a checagem (c) — nenhum conhecimento
  municipal hardcoded no hub — passa a ser critério de code review para qualquer código novo
  do conector e do hub.
- **`smsmarica.unidade` segue intocado** no seu papel (cadastro de gestão, executante ×
  solicitante, CNES para SISREG). Nada neste ADR cria FK, view ou sync entre os dois lados.

### Pendências que este ADR abre (não fazem parte desta entrega)

1. **CNES na origem — MEDIDO em 01/08**: a tabela `INFOSAUDE.HOSPITAL` do Salux **carrega**
   CNES e nome oficial (`NR_CNES`, `DS_HOSPITAL`): `1` → HOSPITAL MUNICIPAL CONDE MODESTO
   LEAL (CNES **2266733**); `2` → UPA 24H INOÃ (CNES **7164440**); `3` → PRONTO ATENDIMENTO
   24H DO POSTO DE SAÚDE SANTA RITA (CNES **2266792**). O conector Salux resolve unidades
   com **zero configuração**; o mapa mínimo na `IaFonte` fica como fallback para origens
   sem esse dado. Resta: **(a)** validar os 3 códigos contra o CNES/DataSUS; **(b)** tratar
   a divergência de nome no CNES 7164440 — o cadastro do Klinikos chama a mesma unidade de
   "UPA MARICA" enquanto o Salux diz "UPA 24H INOÃ" (mesmo CNES ⇒ mesma Organization; o
   nome oficial vem do CNES, os demais viram alias).
2. **Klinikos**: identificar onde vive o código interno de unidade no Klinikos e o seu
   formato, antes do conector existir (a análise em `docs/klinikos/` ainda não cobriu isso).
3. **Forma da fila de divergência**: reusar a pendência de importação de 1ª classe
   ([ADR-0035](./0035-pendencia-importacao-primeira-classe.md)) ou fila própria — decidir na
   implementação.
4. **Formato do mapa mínimo na `IaFonte`** (schema da configuração) e a chave que encerra o
   período de transição do fail-closed.

## Alternativas rejeitadas

- **Unidade dentro do `meta.source` (ou um slug de base por unidade).** Mistura os dois
  eixos: `meta.source` é proveniência de **sistema** (ADR-0009) e a unidade é **lugar**. O
  caso concreto já aconteceu — Inoã e Santa Rita saíram do Salux em abr/2025: com unidade no
  source, a mesma UPA teria duas identidades ("salux/inoa" e "klinikos/inoa") e a linha do
  tempo dela quebraria a cada troca de PEP. Além disso o tombstone escopado por `meta.source`
  (CDC do eDoc) passaria a ter N sources por base, multiplicando a superfície de erro.
- **FK do hub para `smsmarica.unidade`.** Acopla o repositório canônico ao sistema de
  gestão: são bancos e serviços distintos (Automais.Fhir é autônomo,
  [ADR-0010](./0010-servico-fhir-autonomo.md)), então a "FK" viraria dependência de API e de
  deploy coordenado — e destruiria a propriedade (c): replicar o hub em outra cidade exigiria
  levar o painel junto. A direção certa é o painel consumir o hub, nunca o hub depender do
  painel.
- **CNES como chave primária da `Organization`.** CNES **muda** (recredenciamento, correção
  de cadastro) e unidade pode operar antes de ter CNES definitivo. Chave natural externa como
  PK contaminaria toda referência (`Encounter.serviceProvider`, `Location.managingOrganization`)
  com um dado mutável. CNES é identifier de 1ª classe — buscável, exibível — mas a âncora é o
  id interno, exatamente como CPF/CNS no Patient.
