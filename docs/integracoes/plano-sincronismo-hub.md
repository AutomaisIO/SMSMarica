# Plano do sincronismo com o hub — Salux hoje, Klinikos em seguida (01/08/2026)

> ## ⏱ Onde a frente está — atualizado 03/08/2026
>
> **Em produção e saudável.** 47 ciclos em 24 h, todos concluídos, 0 erros, média de 10,6 s.
> Marcas d'água com ~10 min de atraso (o esperado num ciclo de 30 min). **Zero duplicatas
> novas** — o upsert por identifier está segurando.
>
> | Entregue | Quando |
> |---|---|
> | Saneamento das duplicatas pré-identifier (2.723 Encounters, 6.494 DocRefs) | 02/08 |
> | Migration do hub + backfill (1,1 M + 2,4 M identifiers) | 02/08 |
> | Sincronismo contínuo, conciliação de identidade, Painel, escopo fail-closed, SIGTAP | 02/08 |
> | Hotfix da negativa do Hub do Desenvolvedor (`"Data Nascimento invalida"`) | 02/08 |
> | Arbitragem vira job próprio, com teto de tempo | 02/08 |
> | Fix do código FHIR na coluna de busca (`inprogress` → `in-progress`) | 02/08 |
> | **Limpeza das duplicatas clínicas do legado — 12.357 linhas** | **03/08** |
> | Reprocesso direcionado de divergências resolvidas | 03/08 |
>
> **A conciliação de identidade já pagou o investimento:** das 37 divergências arbitradas,
> **5 apontaram o HUB como correto** — a origem insistia com data de nascimento errada (uma
> delas com 3 anos de diferença) e o congelamento protegeu os cinco pacientes.
>
> **Aberto:** expurgo dos 1,7 M de tombstones (~14% de todas as tabelas, ~3,4 GB — item S9);
> reimport dos 261 pares residuais da limpeza; alocar a unidade dos 5 usuários do fail-closed.
>
> **Erro meu que vale registrar:** classifiquei o risco do legado sem identifier em
> `medication_request`/`observation` como "limitado à janela 06/06–20/06". A janela de 120 dias
> do CDC alcança **abril**, e o resultado foram ~12 mil registros clínicos duplicados. O que
> evitou o dano virar permanente foi medir antes de agir — a mesma disciplina que barrou o
> backfill no dia 02.

Documento-mestre da frente **sincronismo contínuo → hub FHIR**. Consolida três coisas
medidas em 01/08/2026: (1) o double-check completo do motor Salux→hub (ADR-0024/0025)
que está no working tree e nunca foi ao ar; (2) a entrada do **Klinikos** (2 instâncias:
`upa24h-marica-sqlserver` e `santarita-marica-sqlserver`); (3) o **censo de identidade**
nas 3 bases (hub, Salux, Klinikos UPA).

Este documento é **plano de execução com gates** — não substitui o backlog vivo do
[marco-1 §11.2](../marco-1-e-linha-de-corte.md), que continua sendo a lista canônica de
pontas soltas do repositório. A §9 daqui **complementa** aquela seção com o que é
específico desta frente; o que já está lá não é repetido aqui.

Tudo abaixo é medição (auditoria de código com arquivo:linha, builds executados, censo
via SQL read-only e cruzamento por CPF validado), não estimativa. O único braço não
medido — Santa Rita, agente offline durante toda a sessão — está dito com todas as
letras onde falta.

---

## 1. Estado real medido (produção + working tree)

### 1.1 O motor existe, funciona no papel, e nunca rodou de verdade

| Fato | Medição |
|---|---|
| Código do motor (scheduler, CDC, marcas, upsert, tela) | **100% no working tree, zero commitado** — inédito em produção |
| Migrations `smsmarica` pendentes | `20260727122701_AddPepSincronizacaoContinua` + `20260802002528_AddPepDivergenciaIdentidade` (§3.4) |
| Migration `fhir` pendente | `20260727020900_AddIdentifierClinicoELocation` — **schema `fhir` de prod parado em 16/06** (`20260616155731_AddTelefoneSearch`) |
| Última execução de importação | **20/06**, morta com `ORA-01013` (cancelamento) |
| Marcas d'água em produção | **06/06** — o hub está ~2 meses atrás da origem |
| Cursor do full-load interrompido | `cd_paciente` **182576** (de ~370 mil) — metade da base nunca teve a clínica completa importada |
| Deploy do motor é inerte | scheduler sai em `agendas.Count == 0` e a tabela de agenda nasce vazia (confirmado na auditoria de docs) |

Tradução: o hub tem ~10,5 milhões de registros clínicos (1,3M Encounter, 2,5M
DocumentReference, 4,2M Observation, 1,46M MedicationRequest, 1,09M Condition)
importados por um mecanismo antigo (POST cego, sem identifier), congelados desde junho.
O motor novo que resolveria isso está pronto e parado no disco.

### 1.2 O hub está CEGO em duas unidades desde abril/2025

O cutover do Klinikos tirou a **UPA Inoã** e o **PA Santa Rita** do Salux em
**07–08/04/2025**. Desde então, nada dessas unidades chega ao hub — o sincronismo Salux
não as vê porque elas não escrevem mais no Salux, e não existe conector Klinikos.

O tamanho do buraco foi medido no censo (base UPA, 01/08/2026):

| Medição (Klinikos UPA Maricá) | Valor |
|---|---|
| Primeiro boletim da base | **07/04/2025 13:52** — a base inteira é pós-cutover |
| Boletins acumulados (= o buraco do hub) | **178.682** |
| Atendimentos médicos acumulados | **170.391** |
| Ritmo atual (últimos 30 dias) | 12.337 boletins · 11.858 atendimentos |
| Pacientes cadastrados | 83.624 |

Santa Rita **não foi medida nesta data** (agente WSS offline durante toda a sessão);
a referência histórica de 26/07 dava 50.748 pacientes — ordem de grandeza, não censo.
O buraco cresce ~12 mil boletins/mês só na UPA. Cada mês de atraso do conector Klinikos
é um mês a mais de prontuário invisível para o hub em duas unidades de urgência.

### 1.3 Identidade de unidade: resolvível com zero configuração

`INFOSAUDE.HOSPITAL` (Salux) tem `NR_CNES` e `DS_HOSPITAL` nas 3 linhas; a tabela
`unidade` do Klinikos tem `unid_codigoCNES`. O conector resolve unidade por CNES sem
tabela de de-para manual:

| CD | Nome no Salux | CNES | Contraparte |
|---|---|---|---|
| 1 | HOSPITAL MUNICIPAL CONDE MODESTO LEAL | 2266733 | — (segue no Salux) |
| 2 | UPA 24H INOÃ | 7164440 | Klinikos "UPA MARICA" — **mesmo CNES, nome divergente** (confirmar que é a mesma unidade real antes de resolver por CNES) |
| 3 | PA 24H PS SANTA RITA | 2266792 | Klinikos Santa Rita (não medido) |

---

## 2. Resultado do double-check

Quatro auditorias independentes (motor, hub, front, docs×código) + builds. Placar das
alegações: **motor** 6 confirmadas / 2 parciais (de 8); **hub** 5 confirmadas / **1
refutada** (a refutada é a mais grave — ver achado nº 1); **front** 5 confirmadas / 1
parcial (de 6); **docs** majoritariamente fiel, com o ADR-0008 como exceção grande.

### 2.1 O que foi confirmado que funciona (o desenho é sólido)

- Disparo agendado é sempre Incremental/Tudo/ApagarAntes=false (`PepSincronizacaoService.cs:163-180`).
- Marca d'água por fase com MAX da origem + lag de 5 min; conversões centralizadas em `SaluxTempo` (exceção deliberada: marca de médicos é UtcNow, comentada).
- CDC de eDoc ancora sem reprocessar o passado, poll por faixa de PK, tombstone sempre escopado pelos DOIS `meta.source`.
- Merge de telefone confirmado é idempotente no retry; ciclo If-Match→409→re-merge limitado a **3 tentativas** — não loopa.
- Scheduler inerte com agenda vazia; anti-sobreposição; backoff exponencial teto 4h; janela que cruza meia-noite; re-scan de médicos por envelhecimento.
- Paciente novo com atendimento novo entra sozinho (UNION BAA+eDoc+FIA na origem).
- FIA: internação em curso re-lida a cada ciclo; status in-progress/finished/unknown; óbito → `dischargeDisposition=exp`; marca por GREATEST(dt_baixa, dt_alta).
- Verificado explicitamente: **não** há SemaphoreSlim aninhado; CancellationToken propagado a todas as fases longas.
- Hub: upsert condicional `PUT ?identifier=` nos 5 clínicos + Location, preservando id lógico; Patient/Practitioner corretamente fora (merge canônico no importador); Encounter com period_end + If-Match/ETag; índice único parcial fecha a corrida create-create.
- Front: polling 1s/15s; 9 endpoints todos com `[RequerPermissao]` (SincronizacaoPep=27); tela mostra próximo ciclo, falhas consecutivas (alerta ≥5), coluna Disparo, diagnóstico origem×hub.
- Docs: §11.2 do marco-1 é fiel (migrations existem com os nomes exatos; "inerte no deploy" confere). ADR-0024: 6 de 7 decisões batem com o código. ADR-0025 B1: status/priority/óbito/Location 3 níveis/leito atual — tudo confere.

### 2.2 Achados, do mais grave ao menos grave

**CRÍTICOS — bloqueiam o deploy:**

| # | Achado | Onde | Consequência |
|---|---|---|---|
| 1 | **Sem backfill dos identifiers no hub**: a migration `AddIdentifierClinicoELocation` só cria colunas nullable + índices — **zero** `migrationBuilder.Sql()`. A busca do upsert é 100% pelas colunas, sem fallback ao JSONB. | `Automais.Fhir.Data/Migrations/20260727020900_AddIdentifierClinicoELocation.cs:12` | Os ~10,5M registros de prod ficam com `identifier_system=NULL`, **invisíveis ao upsert**. BAA editado que reentra na janela → Encounter + Condition **duplicados vivos** (o índice único não impede: a linha legada está fora do predicado). Pior: no CDC de eDoc o lookup do Encounter legado volta vazio → `return` silencioso com a marca avançando → **edição perdida para sempre, sem trilha**. É o item nº 1 do plano (§4.1). |
| 2 | **ORA-01795 trava o CDC de eDoc permanentemente**: `ProcessarEdocLogAsync` monta `IN` com até 5.000 cds sem lotear (limite Oracle = 1.000). | `SaluxImportacaoStrategy.cs:516` | Agenda pausada ~1 dia (~7k ops/dia no log) → primeiro poll estoura → run inteiro morre → retry relê **o mesmo lote** → falha idêntica para sempre; arrasta o run agendado junto. |

**ALTOS:**

| # | Achado | Onde | Consequência |
|---|---|---|---|
| 3 | `MigrateAsync` no startup com CommandTimeout default (30s) e **catch que engole a falha** | `Automais.Fhir.Api/Program.cs:49` | CREATE INDEX não-concorrente em observation (4,2M) tende a estourar 30s → rollback da migration inteira → serviço sobe com modelo EF desalinhado → **500 em todos os 5 recursos clínicos**, erro só no log de startup. |
| 4 | `ApagarAntes` + cursor/Limitado **purga a base inteira e reescreve só uma parte** | `SaluxImportacaoStrategy.cs:61` | Operador retoma o full-load do cursor 182576 com ApagarAntes → todos os pacientes acima do cursor ficam sem clínica, e o incremental nunca repõe. `IniciarAsync` não bloqueia a combinação. |
| 5 | **"Parar importação" não segura o motor** — cancelamento é tratado como sucesso e o scheduler religa no próximo intervalo | `PepSincronizacaoService.cs:484` | Em incidente (hub gravando lixo), o operador acha que parou; com agenda de 30 min a importação renasce sozinha em até 30 min. `PausadoAte` existe na pilha inteira (entidade/DTO/request/type TS) mas **não tem nenhum controle na UI**. |
| 6 | **PUT /agenda destrutivo**: o front envia payload mínimo e o back sobrescreve `JanelaInicioLocal`/`JanelaFimLocal`/`PausadoAte` com null | `PepSincronizacaoService.cs:229` | Qualquer "Salvar agenda" pela tela apaga janela e pausa configuradas por fora, sem aviso — e ainda zera `ProximoRunEm`, disparando run no tick seguinte. |
| 7 | **ADR-0008 "Aceito" descreve um design que nunca existiu**: eDoc deveria virar Questionnaire/QuestionnaireResponse SDC; o código gera DocumentReference com HTML achatado | `docs/adr/0008-mapeamento-clinico-fhir.md:66` | Perdem-se: pares EAV estruturados, tipos de widget, itens não respondidos, status P/D, autor, escalas (Glasgow/Braden/Morse) e — o mais grave — o **PDF assinado (`DOC_ASSINADO`) nunca é lido**: o documento legal não chega ao hub. Vira decisão consciente na §7. |

**MÉDIOS** (não bloqueiam, mas entram na mesma janela de correção — §4.2):

| # | Achado | Onde |
|---|---|---|
| 8 | Retry de POST em 502/504/408/429 pode **duplicar Patient/Practitioner** (contradiz o próprio doc-comment "só 503") | `EscritorComRetentativa.cs:82` |
| 9 | Corrida de dedup por CPF: dois cds com o mesmo CPF no mesmo chunk → dois Patients canônicos (o Salux tem 5.656 cadastros redundantes por CPF — §3) | `SaluxImportacaoStrategy.cs:404` |
| 10 | Watermark avança por cima de registros que falharam na escrita; trilha durável engole erro de persistência; **reimport por `CdsPacientes` não tem nenhum produtor** (código morto sem chamador) | `SaluxImportacaoStrategy.cs:317` |
| 11 | eDoc novo anexado a BAA antigo perdido **em silêncio nos dois caminhos** (fluxo dt_inclusao e CDC) | `SaluxImportacaoStrategy.cs:364` |
| 12 | Escopo Limitado envenena marcas (médicos = UtcNow sem guard; clínicas avançam com varredura parcial) — o guard `!limitado` existe para cursor/PacienteEm, só não cobre o resto | `SaluxImportacaoStrategy.cs:123` |
| 13 | Hub: `period_end` sem backfill — recortes por "altas do período" ignoram os 1,3M encounters pré-migration | migration fhir `:62` |
| 14 | Hub: catch amplo de `DbUpdateException` mascara erros reais como 404; update-update concorrente perde escrita (VersionId não é concurrency token) | `EncounterService.cs:79` |
| 15 | Front: datas no fuso do browser (viola a regra Brasília fixo; util central `shared/lib/datas.ts` existe e não é usado) | `PepSincronizacaoPage.tsx:25` |
| 16 | Front: race no useEffect da agenda (poll de 30s sobrescreve edição não salva; troca de base mantém form sujo) | `PepSincronizacaoPage.tsx:126` |
| 17 | Docs: rede de segurança LOG_BAA (pendência A3) e LOG_FIA não implementadas; alerta de falhas consecutivas é LogError, **não** gera ERRO-XXXXXX como o ADR-0024 promete; admitSource=emd ausente | `SaluxImportacaoStrategy.cs:893` · `PepSincronizacaoService.cs:481` |

**BAIXOS/INFO** (registrados, não bloqueiam): corrida List&lt;T&gt; no endpoint de status;
ParaCada abandona tasks em voo no cancelamento; READ ONLY + reconexão silenciosa
(ORA-01555 provável em Completo de 12,8h; 1555 não é transitório); janela de corrida no
anti-overlap; SQL 100% literal (sem injeção evidenciada, mas hard parse contínuo no
Oracle vivo); `_estatisticas` roda ~10 COUNTs full-scan sem cache e sem auth; conditional
update sempre 200 (nunca 201) e POST duplicado vira 500 genérico; tombstones se
multiplicam a cada Completo sem expurgo físico; `location.part_of_id` sem FK.

### 2.3 Sobre a alegação "trocar `purgarPorPaciente` para false é seguro"

**Não é — hoje.** Os danos da purga são reais (churn de id lógico a cada Completo,
janela sem dado se o run morrer entre purga e reescrita), mas a purga ainda é o único
mecanismo que: (a) alcança o legado pré-identifier — ~1,12M Encounters de POST cego que
o upsert não casa e **duplicaria**; (b) remove exclusões da origem de BAA/prescrição/FIA
(o CDC cobre só eDoc). Pré-condição para desligar a purga: **backfill do identifier no
hub** (§4.1) + aceitar retenção de exclusões da origem até LOG_BAA/LOG_FIA existirem.

### 2.5 ✅ Achado novo (01/08) e SANEADO (02/08): duplicatas que já estavam em produção

> **Executado em produção em 02/08/2026, com autorização explícita.** Backup em
> `backup_20260802_dedup` (52 MB, inclui o mapa dos 47.011 filhos antes do re-apontamento).
> Resultado: **2.723** Encounters unificados · **2.345** Conditions e **6.494**
> DocumentReferences deduplicados · gates **0/0/0** · **0 órfãos criados**. Auditoria em
> `fhir.saneamento_duplicata_20260801`. Em seguida a migration
> `20260727020900_AddIdentifierClinicoELocation` foi aplicada (23,8 s). O texto abaixo é o
> diagnóstico original, mantido como registro do que foi encontrado.


O pré-check do backfill — leitura pura, antes de qualquer escrita — revelou um problema
que **ninguém sabia que existia e que já está em produção hoje**, independente do
sincronismo novo:

| Tabela | Chaves duplicadas | Cópias por chave | Linhas excedentes |
|---|---|---|---|
| `fhir.encounter` | **2.723** | exatamente 2 | 2.723 |
| `fhir.document_reference` | **6.494** | exatamente 2 | 6.494 |
| `fhir.observation` | 0 | — | — |

**2.258 pacientes** têm o **mesmo atendimento duas vezes** na linha do tempo — e o pior:
os filhos clínicos ficaram **divididos entre as duas cópias** (2.689 das 2.723 chaves têm
filhos nas duas). Estão pendurados nas cópias: 4.890 `Condition`, 22.010 `Observation`,
7.709 `MedicationRequest`, 12.402 `DocumentReference`. Ou seja: abrir o atendimento pela
cópia "errada" mostra um prontuário **incompleto**.

Origem identificada com precisão: as duas cópias são **idênticas em conteúdo** (amostra de
200 chaves: 200 idênticas) e foram criadas **no mesmo run** (amostra de 500: 500 com menos
de 1 h de diferença), entre **07/06 e 20/06/2026** — o run que morreu com `ORA-01013`. É a
assinatura exata do bug corrigido em código no commit `88175e9` (busca do hub capada em
200/500 sem paginação + `CriarAsync` como POST cego): **o código foi consertado, os dados
duplicados ficaram.**

Consequências:

1. **Bloqueia o backfill** (§4.1-0): o índice único parcial rejeitaria a segunda cópia — o
   backfill falharia no primeiro lote com duplicata.
2. **É um problema clínico de hoje**, não do sincronismo: 2.258 pacientes com prontuário
   partido ao meio no hub, agora, em produção.
3. Depois de juntar os filhos, **2.345 chaves** ficariam com 2 `Condition` idênticas — hoje
   nenhum encounter tem mais de uma. O saneamento precisa deduplicar isso também, senão o
   backfill de `condition` (derivado `:cond`) colide.

Isto **não estava no plano** e muda o caminho crítico: entra um passo 0 antes do backfill.
O achado é, por si só, a justificativa do pré-check — e do gate de bancada da §5 passo 2.

### 2.4 Veredito dos builds

| Build | Resultado | Conforme a régua da casa (0/0)? |
|---|---|---|
| `SMSMarica.server` — dotnet build | 0 erros, 0 warnings (2m00s) | **sim** |
| `Automais.Fhir` — dotnet build | 0 erros, **2 warnings CS8604** (`UpsertPorIdentifierTests.cs:77` e `:123`) | **não** — corrigir antes do commit |
| `SMSMarica.front` — npm run build | tsc 0 erros; warnings não-bloqueantes (chunk 5,3 MB, imports mistos) | sim (com ressalva do chunk) |

---

## 3. Censo de identidade nas 3 bases

Executado em 01/08/2026, tudo read-only. Hub via conexão direta ao Postgres; Salux e
Klinikos via proxy SQL interno no droplet. **Extratos com PII ficaram fora do repo**
(scratchpad local para o hub; `/root/tmp/censo/` no droplet para as origens).

### 3.1 Números por base

| | Hub (`fhir.patient` vivos) | Salux (INFOSAUDE.PACIENTE) | Klinikos UPA | Klinikos Sta Rita |
|---|---|---|---|---|
| Cadastros | **252.766** | **370.431** | **83.624** | **não coletado** (agente offline) |
| Com CPF | 252.766 (**100%**) | 307.105 (82,9%) | 69.479 (83,1%) | ref. 26/07: ~82% |
| Com CNS | 175.201 (69,3%) | 226.246 (61,1%) | **130 (0,16%)** no cadastro | ref. 26/07: 389 (0,77%) |
| Com ambos | 175.201 | 213.422 (57,6%) | 117 | — |
| **Sem nenhum** | **0** | **50.502 (13,6%)** | **14.132 (16,9%)** | — |
| Duplicatas internas por CPF | **60 CPFs × 2 = 120 patients** | 5.656 cadastros redundantes (307.105 com CPF ÷ 301.449 distintos) | — | — |
| Duplicatas por CNS | 0 | 226.245 distintos (≈0) | — | — |

Notas medidas:

- **Hub**: 99,77% dos patients vêm do Salux HCML (252.196); 570 nativos do smsmarica.
  Todo patient com CNS também tem CPF; **nenhum** sem ambos. Colunas de busca cpf/cns
  estão **100% sincronizadas** com o JSONB (a pendência conhecida de backfill de colunas
  não se manifesta nesses dois campos).
- **Hub, 60 CPFs duplicados**: violação direta da promessa 1 pessoa = 1 Patient. Máximo
  2 repetições por CPF. É exatamente o padrão que o achado nº 9 (§2.2) produz — e a razão
  de corrigi-lo antes de qualquer run novo. Cruzar os 570 nativos com esses 60 pares.
- **Klinikos**: a premissa "CNS mora em `Paciente_CNS`" é **falsa** — aquela tabela é o
  dossiê CadSUS (PIS, certidão, CTPS) **sem** número de CNS; o CNS mora em
  `paciente.pac_cartao_nsaude` (130 preenchidos) e há 17.324 CNS **provisórios** em
  `pac_cartao_nsaude_provisorio`, não incluídos no extrato — decidir se valem para
  cruzamento.
- **Salux**: extrato final (319.929 linhas esperadas) ainda estava **em geração** no
  droplet ao fechar esta medição (`grep FIM /root/tmp/censo/salux-hcml.log` confirma o
  término).

### 3.2 Cruzamento entre bases — EXECUTADO em 01/08 (parcial: **sem Santa Rita**)

Cruzamento por **CPF válido** (dígito verificador conferido; zero CPFs sintaticamente
inválidos nas 3 fontes) sobre os extratos completos: hub 252.766 linhas · Salux
**319.930** (os 370.431 cadastros menos os 50.502 sem CPF nem CNS) · UPA 69.492.
Santa Rita segue pendente (agente WSS offline) — os números abaixo são **de 2 bases +
hub**, e a tripla só fecha com ela (S1/S2 da §9).

**Ocorrências do mesmo paciente (a pergunta central):**

| Medição | Valor | Leitura |
|---|---|---|
| CPFs distintos válidos | hub 252.706 · Salux 301.450 · UPA 69.478 | Salux: 307.106 cadastros com CPF → 301.450 pessoas (5.656 redundantes intra-base) |
| **Salux ∩ UPA (mesma pessoa nas 2 bases)** | **50.539 CPFs** | **72,7% dos pacientes da UPA já passaram pelo Salux** — o cutover partiu a história clínica de 50 mil pessoas ao meio; só o hub reunifica |
| Pessoas únicas na união (2 bases) | 320.389 | vs 389.422 cadastros brutos — ~69 mil registros são a mesma pessoa em outra base |
| Interseção por CNS (entre os sem-CPF) | 1 | CNS não ajuda no cruzamento hoje (Klinikos quase não tem CNS de cadastro) |

**Cobertura do hub (quem está faltando):**

| Medição | Valor | Leitura |
|---|---|---|
| CPFs do Salux presentes no hub | **83,7%** (48.989 fora) | a cauda do full-load interrompido (cursor 182576) medida com precisão |
| CPFs da UPA presentes no hub | **56,5%** (30.213 fora) | o buraco do cutover em pessoas |
| UPA fora do hub **e** fora do Salux | **18.907** | pessoas 100% invisíveis ao hub — só o conector Klinikos as traz |

**Qualidade/risco (o sinal sério — mesmo CPF, nascimento divergente):**

| Par | Divergências | Com ANO diferente |
|---|---|---|
| Salux × UPA | 1.099 | **371** |
| hub × UPA | 941 | 317 |
| hub × Salux | 142 | 81 |

Divergência de dia/mês costuma ser digitação; **ano diferente** no mesmo CPF é candidato
a cadastro trocado (CPF digitado errado numa das bases → risco de fundir a história de
duas pessoas). Consequência de desenho: os **371** pares Salux×UPA **não podem ser
mesclados às cegas pelo conector Klinikos** — o merge por CPF precisa de um guarda de
plausibilidade (nascimento a mais de N anos de distância → fila de divergência, não
merge automático). Isso vira requisito do conector (§6.2) e reforça a regra fail-closed
do ADR-0039. Exemplos mascarados ficaram no relatório da sessão; os extratos com PII
**não** entram no repo (§8.3).

### 3.4 Conciliação de identidade no motor — IMPLEMENTADO em 01/08

O achado acima deixou de ser relatório e virou comportamento do motor. Três partes:

**1. Detecção, no upsert canônico do Patient.** Quando a origem traz um nascimento
diferente do que o hub já tem para o mesmo CPF, o hub **prevalece** (o campo é congelado)
e a divergência é registrada na tabela nova `smsmarica.pep_sincronizacao_divergencia`
(migration `20260802002528_AddPepDivergenciaIdentidade` — só `CREATE TABLE` + índices).
Uma linha viva por (base, CPF, tipo): re-detecção soma ocorrência, nunca duplica. O sink é
o mesmo padrão durável da trilha de falhas (Channel + `IDbContextFactory`, sobrevive a
crash) e o conjunto do que já é conhecido é carregado **uma vez por run** — zero I/O no
caminho quente do upsert.

**2. Arbitragem pela consulta oficial de CPF.** O árbitro não somos nós: a consulta de CPF
(Hub do Desenvolvedor, com CADSUS/SISREG como fallback — a cadeia de `proxy_motor` que já
está em produção) **só valida quando o par CPF + nascimento confere**. Então perguntar
pelas duas datas em disputa resolve o conflito:

| Resultado da consulta | Veredicto | Efeito no motor |
|---|---|---|
| a data da **origem** valida | `OrigemCorreta` | descongela — o próximo run corrige o hub sozinho |
| a data do **hub** valida | `HubCorreto` | congelamento **permanente**: a origem está errada |
| nenhuma valida | `AmbosNegados` | CPF suspeito (provável troca de titular) — congelado, exige olho humano |
| ninguém respondeu | `NaoConclusiva` | volta para a fila da próxima rodada |

Economia deliberada, porque cada consulta custa saldo: a origem é perguntada primeiro e,
validando, a segunda consulta é dispensada (a Receita casa data exata — duas datas
diferentes não podem validar as duas); há teto por rodada (`Pep:Divergencias:MaxPorRun`,
padrão 50); e 3 indisponibilidades seguidas abortam a rodada em vez de queimar a fila.
A arbitragem roda como **fase final de cada sincronização** e também sob demanda pela tela.

**3. Relatório.** Seção "Divergências de identidade" na tela de Sincronização, com
contadores (pendentes, **congelados agora**, origem correta, hub correto, CPF suspeito),
a tabela caso a caso (CPF mascarado, os dois valores, veredicto, valor correto, nome
oficial devolvido pela consulta) e as ações *Arbitrar pendentes* e *ignorar* (falso
positivo — descongela). Endpoints: `GET /pep-sincronizacao/divergencias`,
`GET …/divergencias/resumo`, `POST …/divergencias/verificar`,
`POST …/divergencias/{id}/ignorar` — todos sob `SincronizacaoPep`.

A lógica de arbitragem é **pura e testada** (`ArbitroIdentidade` + 11 testes, sem banco,
sem rede, sem Docker — rodam nesta máquina), no mesmo padrão do `DecididorAgendaPep`.

### 3.3 A regra de dedup que rege tudo isso (fixada em 26/07)

Já decidida e registrada em [`docs/klinikos/mapeamento-fhir.md`](../klinikos/mapeamento-fhir.md):

1. **Buscar por CPF e/ou CNS no hub antes de criar** qualquer Patient (CPF primeiro,
   cair para CNS).
2. Se existir, **não criar** — anexar identifiers que faltam (merge existente:
   `UnirIdentifiers` + `PreservarDoExistente` + PUT com If-Match).
3. `meta.source` obrigatório + código interno prefixado por slug (ADR-0009).
4. **Sem CPF e sem CNS → fica fora**, com **contador exposto por run** (métrica de
   cobertura, não silêncio). A exclusão se autocorrige quando o cadastro ganha documento
   na origem.

Aplicada aos números do censo: ficam fora 50.502 do Salux (13,6%) e 14.132 da UPA
(16,9%) — e é por isso que o contador exposto importa: são ~65 mil pessoas cuja entrada
depende da origem completar o cadastro, e alguém precisa enxergar esse número subir ou
descer.

---

## 4. Correções ANTES do deploy

> **Status em 01/08 (fim do dia): 6 dos 8 bloqueantes APLICADOS no working tree.**
> Builds nas três soluções em **0 erros / 0 warnings**; 60 testes unitários verdes.
> Resta o item 1 (backfill — script pronto, falta **ensaiar na bancada**) e o item 3
> (Organization no hub). Detalhe do que foi feito:
>
> | # | Correção | Estado |
> |---|---|---|
> | 1 | Backfill do identifier no hub | **script pronto** (`Automais.Fhir/scripts/backfill-identifier-clinico.sql`), não executado |
> | 2 | Migration do hub fora do startup + fail-fast | ✅ `Program.cs`: timeout de 30 min configurável + `throw` (era log-e-segue) |
> | 3 | Organization + `serviceProvider` | ⏳ pendente |
> | 4 | ORA-01795: lotear os cds no CDC | ✅ lote de 500 + `ListaInt` blindado (falha alto acima de 1.000) |
> | 5 | Purga por paciente → reconciliação | ⏳ depende do item 1 (desligar antes duplicaria o legado) |
> | 6 | Pausar motor / parar-que-pausa | ✅ `POST /agenda/pausar`, `cancelar?pausarHoras=`, botão **Pausar/Retomar motor** e aviso "PAUSADO até…" na tela |
> | 7 | `PUT /agenda` não-destrutivo | ✅ janela e pausa viraram PATCH — salvar pela tela não apaga mais o que foi configurado por fora |
> | 8 | Guard `ApagarAntes` × cursor/Limitado | ✅ `ValidacaoException` nas duas combinações |
>
> Extra da mesma janela: os 2 warnings CS8604 do `Automais.Fhir` saíram (régua 0/0 restaurada).

### 4.1 Bloqueantes (críticos + altos) — nesta ordem

| # | Correção | O que fazer | Esforço |
|---|---|---|---|
| **0** | 🔴 **SANEAR as duplicatas pré-identifier** (achado novo de 01/08 — ver §2.5) | Sem isto o backfill **falha no primeiro lote**: o índice único rejeita a segunda cópia. Script pronto: `Automais.Fhir/scripts/sanear-duplicatas-pre-identifier.sql` (elege sobrevivente → re-aponta ~47 mil filhos → soft-delete da perdedora → dedup das Conditions resultantes), com tabela de auditoria e reversão. **Exige backup e aprovação explícita.** | ½ dia + ensaio |
| 1 | **Backfill do identifier no hub** (achado nº 1) | Script SQL controlado (não no startup): `encounter` e `document_reference` são backfilláveis direto do JSONB (o importador em prod já gravava o identifier no content — `content#>>'{identifier,0,system}'`), **com dedup prévio** para não violar o índice único; `condition` derivável via `encounter_id` → chave do BAA + `:cond`; `medication_request`/`observation` legados **não têm** identifier no content → decisão formal: derivação onde possível ou um único Completo com purga como passo de cutover documentado (ciente do churn de ids). Incluir no mesmo script o backfill de `period_end` (achado nº 13). | 1–2 dias (script + ensaio na bancada com contagens antes/depois) |
| 2 | **Migration do hub fora do startup + fail-fast** (achado nº 3) | Aplicar via psql/bundle EF com statement_timeout desligado; os 6 índices como `CREATE INDEX CONCURRENTLY`; no `Program.cs`, falha de migration → **abort do startup**, nunca logar-e-seguir. | ½ dia |
| 3 | **Organization + `serviceProvider` na MESMA migration do hub — E no conector** | Lado hub: a migration `AddIdentifierClinicoELocation` **nunca foi aplicada** — janela única para incluir a tabela `organization` (hoje zero ocorrências no Automais.Fhir) e a coluna de `serviceProvider` no Encounter sem custo extra de deploy; aproveitar para a FK de `location.part_of_id`. Lado conector (sem isso o passo 7 da §5 **não** backfilla o estoque): resolução de `Organization` no **início do run** (upsert por identifier CNES + `urn:salux:hospital`, cache por run do mapa `CD_HOSPITAL` → id) e `SaluxFhirMapper` emitindo `serviceProvider` em **todo** Encounter (BAA e FIA). O de-para já está resolvido por CNES com zero configuração (§1.3). | 1,5–2 dias (entidade + service + controller + migration editada antes de aplicar + fase de Organizations e serviceProvider no conector) |
| 4 | **ORA-01795: lotear os cds no CDC** (achado nº 2) | `EmLotes(cds, TamanhoLote)` antes de `SqlPacientes` (padrão já usado nas linhas 174-177) + blindar `ListaInt` para nunca emitir lista >1000. | horas |
| 5 | **Purga por paciente → reconciliação sem purga** (achado §2.3; linha ~67 da strategy) | Depois do backfill (item 1): trocar `purgarPorPaciente` para reconciliação por upsert; exclusões da origem ficam explicitamente retidas até LOG_BAA/LOG_FIA (registrado na §9). Enquanto o item 1 não rodar, **a purga fica como está** — desligá-la antes duplica o legado. | 1 dia |
| 6 | **Botão Pausar motor + parar-que-pausa** (achado nº 5) | `PausadoAte` já existe em entidade/DTO/request/type TS. UI: expor pausa de um clique; no modal do "Parar", oferecer (ou aplicar por padrão) "parar E pausar por X horas". Mínimo aceitável: `CancelarAsync` seta `PausadoAte` curto quando a agenda está ativa. | ½ dia |
| 7 | **PUT /agenda não-destrutivo** (achado nº 6) | Janela/PausadoAte só sobrescrevem quando informados (mesmo padrão do `MedicoRescanHoras`, linha 231) — ou o front ecoa os valores do GET. | horas |
| 8 | **Guard ApagarAntes × cursor/Limitado** (achado nº 4) | `ValidacaoException` em `IniciarAsync` quando `ApagarAntes=true` com `CursorPacienteInicial != null` ou `Escopo=Limitado`. | horas |

### 4.2 Recomendadas na mesma janela (médios que custam pouco)

| Correção | Esforço |
|---|---|
| Retry de POST restrito a 503 explícito (ou If-None-Exist no hub) — evita duplicar Patient (nº 8) | horas |
| Dedup por CPF dentro do chunk (processar 1 cd por CPF, ou striped lock por chave) (nº 9) | ½ dia |
| CDC: `encId` nulo → pendência re-processável na trilha (não `return` silencioso); `falhou(l.CdPaciente)` em vez de 0; expor disparo com `CdsPacientes` na API (hoje é promessa sem consumidor) (nº 10/11) | 1 dia |
| Guard `!limitado` para `MedicoEm`/`BaaEm`/`EdocEm`/`FiaEm` (nº 12) | horas |
| Catch do upsert restrito a 23505; considerar VersionId como concurrency token (nº 14) | horas |
| Front: datas via `shared/lib/datas.ts` (Brasília fixo); dirty-state no form da agenda; invalidar histórico na transição emExecucao true→false (nº 15/16) | ½ dia |
| Alerta de falhas consecutivas via `IRegistroErroService` (ERRO-XXXXXX, como o ADR-0024 promete) (nº 17) | horas |
| 2 warnings CS8604 nos testes do Automais.Fhir (régua 0/0) | minutos |

Total honesto da §4: **~5 a 7 dias úteis** de trabalho focado antes do primeiro deploy,
sendo o caminho crítico o item 1 (backfill) + item 3 (Organization na mesma migration).

---

## 5. Ordem de entrega com gates verificáveis

Método que funcionou no ADR-0038: worktree limpo → ensaio na bancada → deploy →
verificação por id. Regras da casa em vigor em todos os passos: **migration é passo
manual** (o deploy não aplica — já custou caro 3 vezes), **conferir
`smsmarica.__migrations` / `fhir."__EFMigrationsHistory"` depois de todo deploy**, e
**produção só com OK explícito por ação**.

| Passo | O quê | Gate para avançar |
|---|---|---|
| 0 | Correções §4.1 aplicadas no working tree | builds 0 erros / 0 warnings nas duas soluções; testes de upsert verdes |
| 1 | **Commit fatiado por frente** (motor server / hub / front), sem deploy | cuidado com o snapshot do EF compartilhado entre frentes — commitar só o próprio hunk; `git status` limpo para esta frente |
| 2 | **Bancada Maestro**: aplicar as 2 migrations + backfill em schemas com cópia de dados; medir o tempo dos CREATE INDEX; ensaiar rollback | contagens antes/depois batendo; **0 linhas vivas com identifier NULL em encounter, document_reference E condition** (a dupla do achado nº 1: BAA editado duplica Encounter **e** a Condition `:cond`); **decisão formal registrada** para o legado de medication_request/observation (derivação impossível pelo content → ou cutover documentado com purga ou convivência com órfãos); tempo de índice conhecido |
| 3 | **Deploy hub (Automais.Fhir)**: migration manual via psql/bundle (índices CONCURRENTLY), depois o backfill, depois o serviço | `fhir."__EFMigrationsHistory"` tem a `20260727020900`; smoke: `PUT ?identifier=` sobre registro legado **atualiza** (não cria); `GET ?identifier=` devolve no máximo 1 |
| 4 | **Deploy server (SMSMarica.server)**: migration `20260727122701` manual; serviço sobe com o motor **inerte** (agenda vazia) | `smsmarica.__migrations` confere; tabela de agenda vazia; zero runs espontâneos por 24h |
| 5 | **Ligar a agenda com janela** (ex.: madrugada, intervalo 30 min) — OK explícito | primeiro run agendado completo sem Erro; diagnóstico origem×hub coerente; query de duplicatas (identifier com >1 linha viva) = **0** |
| 6 | **Observar** N ciclos (sugestão: 3 dias) | falhas consecutivas = 0; marcas avançando; sem crescimento anômalo de tombstones |
| 7 | **Reconciliação noturna**: um Completo controlado (sem ApagarAntes) para fechar o gap 06/06→hoje e a metade da base acima do cursor 182576 — **é este passe que backfilla a unidade no estoque**: com a §4.1-3 no lugar, cada Encounter reescrito por upsert sai com `serviceProvider`, sem passo extra | contagens origem×hub batendo no diagnóstico; **3 Organizations vivas no hub** (criadas pelo próprio run); amostra de Encounters antigos reescritos carrega `serviceProvider`; duração medida (última referência: 12,8h — atenção ao ORA-01555, achado baixo da §2.2) |
| 8 | **Verificação do eixo unidade** (fecha o ADR-0039 decisão 7) | cobertura de `serviceProvider` no estoque Salux ≈ 100% dos Encounters vivos; Encounter novo do incremental sai com serviceProvider; contagem por Organization bate com a distribuição por `CD_HOSPITAL` (§1 da conversa: 689.950 / 268.151 / 163.620 + o delta do gap) |

Backout por passo: ver §8.4.

---

## 6. Klinikos: o que reusa, o que é novo, o que o hub precisa crescer

Base de análise: [`docs/klinikos/mapeamento-fhir.md`](../klinikos/mapeamento-fhir.md)
(26/07/2026). Síntese de lá que continua valendo: **o Klinikos é mais compatível com
FHIR do que o Salux** em prescrição (Dosage estruturado), sinais vitais (colunados),
administração de medicamento (`Item_Aprazamento`) e jornada (statusHistory de graça).

### 6.1 O motor reusa (sem tocar)

- **Scheduler + agenda + fila + decisor** (`PepSincronizacaoScheduler`/`DecididorAgendaPep`) — já são por fonte (`fonteId`), multi-base por construção.
- **Marcas d'água por fase** com lag — o mecanismo; os *campos* precisam generalizar (§9).
- **Upsert por identifier** no hub (`PUT ?identifier=`) e **merge canônico** de Patient/Practitioner (`UpsertCanonicoAsync` + `PatientMergeFhir`) — idênticos; muda só o system (`urn:klinikos:*`).
- **Tela de Sincronização PEP** + trilha de falhas + diagnóstico origem×hub — a UI já lista bases.
- **Canal de leitura**: o proxy/agente WSS para SQL Server já existe (é o mesmo do módulo IA — as duas instâncias já são fontes cadastradas).

### 6.2 O que é novo (escrever)

- **`KlinikosImportacaoStrategy`** — estratégia própria por instância (2 slugs:
  `upa24h-marica-sqlserver`, `santarita-marica-sqlserver`), unidade resolvida por CNES.
- **CDC próprio, com redundância tripla já identificada na origem**: `rowversion` +
  `pac_dt_ultima_alteracao` + `historico_paciente` — melhor sinal de mudança do que o
  Salux oferece; escolher a âncora e usar as outras duas como verificação.
- **Mapper próprio** (`KlinikosFhirMapper`): Encounter EMER com statusHistory,
  Observation colunada (inclui HGT/glicemia e Glasgow estruturado), MedicationRequest
  com Dosage estruturado, CNS do boletim complementando o cadastro.
- **Contador de excluídos por run** (regra de dedup §3.3, item 4) — novo no motor,
  exposto na tela.
- **Conciliação de identidade — quase pronta para reuso.** O mecanismo do §3.4 (detecção,
  congelamento, arbitragem pela consulta oficial, relatório) é **agnóstico de base**: a
  tabela é por `fonte_id` e o árbitro é lógica pura. O conector Klinikos só precisa chamar
  a mesma conciliação no seu upsert de Patient. São **371** casos já medidos entre Salux e
  UPA esperando por isso — fundir às cegas mistura a história clínica de duas pessoas, o
  desastre exato que este projeto não pode cometer.

### 6.3 O que o HUB precisa crescer (recursos que o Klinikos traz e não existem)

Hoje o Automais.Fhir tem 9 DbSets. O Klinikos entrega, e o hub **não tem onde guardar**:

| Recurso | Origem no Klinikos | Situação no hub |
|---|---|---|
| **AllergyIntolerance** | `Atendimento_Alergia` (texto livre, 85% negação) | tabela **não existe** |
| **Procedure** (SIGTAP) | `Producao_Spa`/`procedimento_ambulatorial_real` | não existe |
| **ServiceRequest / DiagnosticReport** | `resultado_exame_radiologico` (radiologia; lab interno morto) | não existe — e é o mesmo recurso que o F7 do Salux exigiria |
| MedicationAdministration | `Item_Aprazamento` (dose_adm, checagem) | tabela existe **vazia** — o Klinikos é quem a destrava (pendência conhecida do BAU) |

Decidir cedo: esses recursos entram numa migration própria do hub **antes** do conector
Klinikos, no mesmo padrão (colunas de identifier já nascendo com backfill vazio — sem
repetir o erro do achado nº 1).

### 6.4 O risco da internação — pergunta a fazer AGORA

O módulo `Internacao` do Klinikos está **morto desde 25/01/2026** (medido no
levantamento de 26/07). O Conde (HCML) **interna** — é a única unidade com Encounter
IMP, recém-implementado no ADR-0025 via FIA do Salux — **e vai migrar para o Klinikos**.

Se a migração do Conde for para o Klinikos *como ele roda hoje nas UPAs*, o hub perde a
internação no dia do cutover: FIA morre no Salux e não nasce equivalente no Klinikos.

**Pergunta a levar ao projeto da migração agora, não no cutover:** o módulo de
internação do Klinikos será reativado/parametrizado para o Conde? Com quais tabelas
(a `Internacao` atual ou outra)? Haverá equivalente de FIA/leito/transferência que o
conector possa mapear para Encounter IMP + Location? A resposta muda o desenho do
conector (e o prazo da §7).

---

## 7. Salux com prazo de validade

A migração do Conde para o Klinikos põe **data de morte no Oracle do Salux** (ainda sem
dia marcado, mas certa). Duas consequências práticas:

1. **A extração final vira deadline.** O último Completo + reconciliação contra o Salux
   precisa acontecer **antes do desligamento**, com o gap zerado e verificado
   (contagens origem×hub por fase). Depois disso o hub é a única cópia. Tratar como
   marco com dono e data assim que o projeto da migração der o cronograma.
2. **O que o achatamento perdeu só é recuperável com o Oracle vivo.** O ADR-0008 mandava
   eDoc → QuestionnaireResponse (lossless); o código gera HTML achatado (achado nº 7).
   Ficam presos na origem: os pares EAV estruturados, os tipos de widget, os itens não
   respondidos, o autor, o status P/D, as escalas como dado discreto e — o item legal —
   o **PDF assinado (`DOC_ASSINADO` + `HASH_ASSINATURA_DIGITAL`), que nunca foi lido
   pelo importador**. Morreu o Oracle, morreu a chance.

Decisão consciente a tomar (uma das duas, registrada em ADR):

| Opção | O que significa |
|---|---|
| **Fazer antes do desligamento** | Implementar a re-extração estruturada (QR SDC, ou no mínimo: EAV bruto + PDFs assinados arquivados em storage próprio com hash) e rodá-la na extração final. Custo: frente nova de trabalho com deadline externo. |
| **Adiar assumidamente** | Aceitar que o hub fica com o HTML renderizado como registro definitivo do eDoc e que o documento assinado permanece apenas no backup final do Oracle (que então precisa ser **preservável e legível a longo prazo** — decisão de infra, não de código). Rebaixar as decisões 3/4 do ADR-0008 formalmente. |

O que não é aceitável é a terceira opção implícita: não decidir e deixar o desligamento
do Conde escolher por nós.

---

## 8. Riscos e salvaguardas

### 8.1 Telefone verificado do paciente (3 camadas, confirmadas na auditoria)

1. `PatientMergeFhir.PreservarDoExistente` remove qualquer confirmado injetado por
   tentativa anterior e reinjeta clones do hub — **idempotente no retry**
   (`PatientMergeFhir.cs:423-431`).
2. Confirmado é **único rank 1**; o número equivalente vindo do Oracle é demovido
   (tolerância a DDI).
3. Escrita com **If-Match**: 409 → re-lê → re-mergeia, máximo 3 tentativas, depois vira
   falha do paciente na trilha — nunca sobrescreve às cegas.

### 8.2 Oracle de produção read-only (3 barreiras)

1. `SqlReadOnlyGuard` valida todo SQL do importador (só leitura).
2. Cada conexão abre `SET TRANSACTION READ ONLY` (`LeitorOracleHis.cs:68`) — com a
   ressalva conhecida do ORA-01555 em Completo longo (achado baixo, §2.2).
3. Regra não-negociável do projeto (`Salux/CLAUDE.md`): PROD Oracle é live de hospital,
   só SELECT — vale para humanos e para o motor.

### 8.3 PII

- Extratos do censo **nunca entram no repo**: hub no scratchpad local da sessão; Salux e
  Klinikos em `/root/tmp/censo/` no droplet. Este documento e os JSONs de auditoria não
  carregam nenhum CPF/CNS/nome.
- Vale a regra permanente: nunca `git add -A`, nunca commitar dump/captura.

### 8.4 Backout por passo do plano (§5)

| Passo | Backout |
|---|---|
| Migration hub | AddColumn nullable + CreateTable novos = **não destrutivo**; índices CONCURRENTLY que falharem são dropados e re-tentados; `Down()` existe. Antes de tudo: ensaio + rollback na bancada (passo 2). |
| Backfill hub | UPDATE idempotente (`WHERE identifier_system IS NULL`) — re-executável; erro no meio não corrompe (colunas continuam NULL onde não passou). |
| Deploy server | Motor **inerte por construção** (agenda vazia) — o backout é não ligar a agenda. |
| Agenda ligada | Desativar a agenda + **Pausar motor** (novo controle da §4.1-6). O "Parar" sozinho **não** é backout (achado nº 5) até a correção entrar. |
| Completo de reconciliação | Sem ApagarAntes, nunca (guard da §4.1-8). Se morrer no meio: as marcas por fase só avançam ao fim da fase — re-rodar continua de onde parou; o risco de janela-sem-dado da purga desaparece quando a §4.1-5 entrar. |
| Conector Klinikos (futuro) | Mesmo padrão: fonte nova nasce com agenda desativada; slug próprio permite tombstone escopado por `meta.source` sem tocar o que veio do Salux. |

---

## 9. Backlog vivo desta frente

Complementa o [marco-1 §11.2](../marco-1-e-linha-de-corte.md) — o que já está lá
(commit fatiado, Docker/Testcontainers, governança de ADR) não se repete; abaixo só o
específico do sincronismo. Ao resolver, mover a linha para "resolvido" com data, como lá.

| # | Item | Contexto / gatilho |
|---|---|---|
| S1 | ~~Cruzamento das 3 bases~~ — **executado em 01/08 para hub+Salux+UPA (§3.2)**; falta SÓ a perna Santa Rita para fechar a tripla | gatilho: agente WSS de Santa Rita voltar → S2 → re-rodar `cruzar.py` (scratchpad da sessão) com o 4º extrato |
| S2 | **Censo Santa Rita** — agente WSS offline em 01/08 | quando o agente voltar: `python3 /root/tmp/censo/extrair_censo.py santarita-marica-sqlserver sqlserver` + mesmas queries da UPA |
| S3 | **Merge dos 60 CPFs duplicados do hub** (120 patients) | fazer **antes** de ligar a agenda, ou o merge canônico alterna entre os dois; cruzar com os 570 nativos smsmarica |
| S4 | **Enriquecimento de CNS** — 77.565 patients do hub (30,7%) sem CNS | avaliar via cadweb50 (mesmo caminho da resolução SISREG CNS→CPF) |
| S5 | **CNS provisório do Klinikos** — 17.324 em `pac_cartao_nsaude_provisorio` | decidir se vale para cruzamento/dedup antes do conector |
| S6 | **CNES 7164440 com nome divergente** (UPA 24H INOÃ × UPA MARICA) | confirmar mesma unidade real antes de resolver unidade por CNES no conector |
| S7 | ~~`docs/integracoes/salux/de-para.md`~~ — **feito em 01/08** ([o documento](./salux/de-para.md), com a seção obrigatória "o que se perde") | resta o candidato a teste: identifier systems/extensões do mapper ⊆ de-para |
| S8 | **LOG_BAA rede de segurança** (pendência A3 do ADR-0024) + **LOG_FIA** (correção retroativa de internação) | hoje existem só em comentário de código; edição de BAA >120d e FIA corrigida/excluída ficam invisíveis |
| S9 | **Tombstone de BAA: expurgo físico** — cada Completo com purga soma ~1,3M tombstones | DELETE de tombstones antigos + VACUUM; some da pauta se a §4.1-5 (reconciliação sem purga) entrar antes do próximo Completo |
| S10 | **MarcaDagua genérica por fase nomeada** — hoje os campos chamam `BaaEm`/`EdocEm`/`FiaEm` (nomes Salux) | endurecer **antes** da 2ª base (Klinikos), senão a estratégia nova herda campos com nome errado |
| S11 | **ADR-0039** — sincronismo multi-PEP (Salux + Klinikos) com campo `Implantação:` desde o nascimento | consolida ADR-0024/0025 + as decisões deste plano (backfill, reconciliação sem purga, regra de dedup §3.3); rebaixa formalmente as decisões 3/4 do ADR-0008 (§7) |
| S12 | **Recursos novos do hub para o Klinikos** (AllergyIntolerance, Procedure, ServiceRequest/DiagnosticReport) | migration própria, com lição do achado nº 1 aprendida: identifier + backfill pensados juntos |
| S13 | **Pergunta da internação ao projeto de migração do Conde** (§6.4) | fazer agora; a resposta dimensiona o conector e o prazo da §7 |
| S14 | **`_estatisticas` sem cache/auth** e listagens sem índice de ordenação no hub | cache curto ou `pg_class.reltuples`; garantir 5081 fechada para fora |

---

*Medições de 01/08/2026 (auditorias de código no working tree, builds, censo read-only).*
*Nenhum número deste documento é estimativa, exceto os esforços da §4 — e estes estão
marcados como tal.*
