# ADR-0020 — Demografia do Paciente nativa no FHIR (aposentadoria do blob `urn:smsmarica:paciente-payload`)

- **Status**: Proposto
- **Data**: 2026-07-01
- **Decisores**: Bernardo (product/eng)
- **Relaciona-se com / estende**: [ADR-0007](./0007-schema-fhir-separado.md) (schema FHIR e régua smsmarica↔FHIR), [ADR-0008](./0008-mapeamento-clinico-fhir.md) (mapeamento clínico), [ADR-0009](./0009-identidade-e-proveniencia-multi-pep.md) (identidade/proveniência multi-PEP, chave de dedup por CPF + `cd_paciente` prefixado), [ADR-0010](./0010-servico-fhir-autonomo.md) (hub FHIR autônomo), [ADR-0014](./0014-importacao-salux-no-backend.md) (importação Salux no backend)

## Contexto

Paciente vive só no hub FHIR (ADR-0010). O `SMSMarica.server` é consumidor via `IPacienteFhirClient`. Hoje, o `PacienteFhirMapper` (server) guarda o `PacienteDto` num **record de 35 campos serializado em JSON dentro de uma extension** do `Patient` (`urn:smsmarica:paciente-payload`, doravante "o blob"). O `SaluxFhirMapper.BuildPatient` (importação) NÃO usa esse blob: escreve os mesmos dados em **elementos FHIR nativos** (name, identifier[s], address, telecom, contact, maritalStatus, extras crus em `urn:salux:extras`).

O problema **não é a leitura** — `PacienteFhirMapper.ParaDto` já é *native-first* para a demografia central (nome, CPF, CNS, RG, nascimento, sexo, filiação, endereço, telefone principal, e-mail: lê o nativo e só usa o blob como fallback). O problema é o **caminho de escrita**, que é *blob-first*:

- `ConstruirNovo`/`AplicarAtualizacao` montam o `Payload` e `AplicarPayload` (`PacienteFhirMapper.cs:288`) projeta para nativo **apenas** Name/Identifier/BirthDate/Gender. **Address/Telecom/Contact/maritalStatus/photo nunca são escritos em elemento nativo** — vivem só no blob.

Isso gera dois defeitos confirmados, **ativos em produção**:

1. **SHADOWING (edição "não pega")** — num paciente importado (Salux, que já tem endereço/telefone/e-mail nativos), editar e-mail/endereço/telefone grava só no blob; `ParaDto` lê o valor nativo antigo e sombreia a edição.
2. **PERDA DE IDENTIFICADORES** — `AplicarPayload` faz `patient.Identifier = []` (`PacienteFhirMapper.cs:301`) e repopula só CPF/CNS/RG, **apagando PIS/passaporte/RNE/certidão/SGH/CEM/`urn:salux:cd_paciente`** do importado a cada edição. Além do risco clínico (perde vínculo com prontuários legados SGH/CEM), **`urn:salux:cd_paciente` é a chave de re-dedup do incremental** (`SaluxImportacaoStrategy.PacientesDaBaseNoHubAsync`, `SaluxImportacaoStrategy.cs:389`): apagá-lo faz a importação deixar de reconhecer o paciente e **duplicar recursos clínicos**. Foi também a origem do 500 (`name.text` vazio quando o importado não tem blob).

A revisão crítica do design apontou ainda **furos estruturais** que o design original (só refatorar a edição) não fechava, e que este ADR passa a tratar como parte da decisão:

- **A importação também faz replace-all.** `UpsertCanonicoAsync` (`SaluxImportacaoStrategy.cs:316`) faz `PUT` do `Patient` recém-construído por `BuildPatient`, preservando só os identifiers unidos (`UnirIdentifiers`). `BuildPatient` **não carrega o blob** → cada reimport **apaga o blob inteiro** e sobrescreve toda a demografia nativa com os valores do Oracle. Como o import roda por **dias**, qualquer edição promovida a nativo é clobberada, e todo clínico ainda no blob (altura/peso/alergias/medicamentos/comorbidades/foto/observações/planoSaúde/contatoEmergência) é **destruído**. Corrigir só a edição não basta.
- **Não há concorrência otimista** em lugar nenhum. O hub só faz `VersionId+1` (`PatientService.cs:48`); o `PUT` "substitui" sem `If-Match`; `PacienteFhirClient`/`HubFhirEscritor` mandam `PUT` puro. Backfill + edição de usuário + import multi-dia concorrentes = **lost-update** clássico. Todo o fluxo `ObterAsync → mutar → AtualizarAsync` é não-atômico.
- **Read-modify-write amplifica a perda:** `AtualizarContatoAsync`/`AtualizarFotoAsync` (`PacientesService.cs:150-170`) reconstroem o request inteiro a partir do DTO (cujos clínicos vêm do blob via `RequestCompletoDeDto`). Se um reimport apagou o blob logo antes, regrava clínico vazio — **perda silenciosa mesmo sem import concorrente**.
- **Campos com origem/destino frágeis:** Latitude/Longitude são **write-dead hoje** (`ConstruirNovo` grava `0,0` fixo em `PacienteFhirMapper.cs:71`; `AplicarAtualizacao` nunca seta; `ParaDto` lê só `pl.Latitude/pl.Longitude`) → **todo** paciente (criado ou importado) fica `0,0` e intransportável. `EstadoCivil` é gravado pelo Salux só como `maritalStatus.text` livre em pt-BR (`SaluxFhirMapper.cs:243`), sem coding. Telefone único do Salux é gravado como `use=home` sem `rank` (`SaluxFhirMapper.cs:247`). Raça-cor/escolaridade/peso/altura/sangue/Rh têm **duas origens redundantes**: blob (criado) e `urn:salux:extras` (importado).

A padronização = tornar o **nativo a única fonte de verdade** para tudo que tem casa nativa, com escrita por **MERGE/UPSERT** (nunca replace-all) em **ambos** os caminhos (edição e importação), espelhando o `SaluxFhirMapper.BuildPatient`. Régua respeitada (ADR-0007/0009): identidade/clínico canônico → hub FHIR; Laudo/SolicitacaoExame/transporte permanecem no `smsmarica`.

## Decisão

### 1. Nativo é a única fonte de verdade; escrita por MERGE/UPSERT em todos os caminhos
Todo campo com casa nativa no `Patient` R4 passa a ser **lido e escrito no mesmo elemento nativo**. As três superfícies de escrita — edição (`PacienteFhirMapper`), criação (`ConstruirNovo`) e **importação** (`SaluxImportacaoStrategy.UpsertCanonicoAsync`) — passam a **MERGE aditivo idempotente**, jamais replace-all:
- **Identifier**: UPSERT por `system` (CPF/CNS/RG); **nunca `Identifier = []`**. Secundários (PIS/passaporte/RNE/certidão/SGH/CEM/`urn:salux:cd_paciente`) são preservados sempre.
- **Name**: UPSERT por `use` (official/nickname), preservando os demais; **nunca emitir `HumanName` com `text` vazio**; se o request vier vazio, preservar o nome existente.
- **Address[0]**: gerenciado (Line[0]=logradouro, número via extension `iso21090-ADXP-houseNumber` no elemento da linha para round-trip perfeito do campo `Numero` do `EnderecoDto`, Line[1]=complemento, District/City/State/PostalCode/Text).
- **Telecom**: UPSERT por `(system, use)`, preservando telecoms não gerenciados.
- **Contact**: UPSERT por `relationship` (MTH/FTH/GUARD/SPS + emergência).
- **maritalStatus/photo**: gerenciados.
- **Regra de merge**: chave estável por elemento; se um campo do request vier vazio, **preservar o nativo** (não zerar).

### 2. Modelo único de telecom (decidido)
`rank=1` marca o **principal**; `use=mobile`/`use=home` marcam celular/residencial; o principal é **excluído** dos baldes por `use` para não duplicar. Leitura: principal = telecom `rank=1`, senão o **primeiro** telefone. Compat Salux: como `BuildPatient` grava um único telefone `use=home` sem rank, ele passa a ler como **principal** (paridade com o comportamento atual do `ParaDto`, que devolve o primeiro telefone como principal), e residencial/celular só surgem quando há telefone **distinto**. `BuildPatient` passa a marcar `Rank=1` no telefone único (desambiguação futura).

### 3. Modelo único de EstadoCivil (decidido)
Escrita smsmarica: enum → coding `v3-MaritalStatus` (`Solteiro→S`, `Casado→M`, `UniaoEstavel→T`, `Divorciado→D`, `Viuvo→W`, `Separado→L`, `NaoInformado→UNK`) + `.text` humano. Leitura: prioriza o `coding`; para importados (que têm só `.text` pt-BR do Salux), aplica um **normalizador de texto** (uppercase, remove `(A)`/acentos): `CASADO→Casado`, `SOLTEIRO→Solteiro`, `UNIAO ESTAVEL/CONVIVENTE→UniaoEstavel`, `DIVORCIADO→Divorciado`, `VIUVO→Viuvo`, `SEPARADO*→Separado`, resto `NaoInformado`. **Pré-requisito**: rodar `SELECT DISTINCT ds_est_civil FROM estado_civil` (read-only) e congelar o mapa antes de Fase A; `BuildPatient` passa a emitir também o `coding` para novos imports.

### 4. Latitude/Longitude deixam de ser write-dead antes de qualquer migração
Fase A adiciona **geocodificação no save** (reusa `Geo.IGeocodificadorService`, já em DI) e grava lat/long na extension `http://hl7.org/fhir/StructureDefinition/geolocation` do `address[0]`; `ParaDto` lê de lá (blob como fallback de transição). Isso corrige o `0,0` universal que hoje deixa **todos** os pacientes intransportáveis. (Alternativa `smsmarica.localizacao` avaliada em "Alternativas".)

### 5. Concorrência otimista é pré-requisito, não melhoria futura
Hub: `PUT /fhir/Patient/{id}` passa a honrar `If-Match: W/"<versionId>"`; versão stale → **409**; GET/PUT/POST devolvem `ETag`. Clientes (`PacienteFhirClient`, `HubFhirEscritor`) enviam `If-Match` e fazem **re-read-and-retry** em conflito. Sem isso, backfill + edição + import se sobrescrevem.

### 6. Importação e backfill são mutuamente exclusivos
O job de promoção **recusa iniciar** (ou aguarda) enquanto houver run de importação PEP ativo (checa `PepSincronizacaoEstadoVivo`), e vice-versa. Alinha-se à regra operacional já vigente ("deploy só após o run atual terminar").

### 7. Blob preservado até a Fase C; aposentadoria por campo, validada por presença no destino
Nas Fases A/B o blob **permanece intacto** como rede de segurança e casa temporária dos clínicos; o import passa a **preservá-lo** no merge. A remoção (Fase C) é por campo e só ocorre quando o recurso-destino **já existe e está populado** no hub — validado por **presença no destino**, não por "divergência zero" (que mascara ausência-em-ambos).

### 8. Régua mantida (ADR-0007/0009)
Demografia e clínico canônico → hub FHIR (Patient nativo + Observation/AllergyIntolerance/Condition/MedicationStatement/Coverage). Laudo/SolicitacaoExame/transporte permanecem no `smsmarica`. `urn:salux:extras` (proveniência crua) **permanece** — não é o blob e está fora do escopo de aposentadoria.

## Consequências

### Positivas
- Elimina **por construção** o shadowing (ler e escrever no mesmo elemento) e a perda de identificadores secundários (fim do `Identifier = []`), fechando um risco de **duplicação clínica em produção**.
- Paciente **criado** pelo smsmarica passa a ter telecom/endereço nativos → **pesquisável por telefone** no hub (hoje não é) e simétrico ao importado.
- Lat/Long deixam de ser `0,0` universais → pacientes voltam a ser transportáveis (destrava `GeradorDeTransladoService`/`FaturamentoService`).
- Importação vira merge/preserve → reimport de dias deixa de destruir edições e clínico.
- `If-Match` torna toda escrita determinística e auditável; conflitos viram 409, não clobber silencioso.
- Fase A é **transparente ao front** (shape do `PacienteDto`/`PacienteListItemDto` inalterado; só muda a fonte interna).

### Negativas / custos
- Introduz complexidade real: motor de merge compartilhado, concorrência otimista ponta-a-ponta, job de backfill com gate e relatório de divergências.
- Fase B agrega recursos clínicos no `ObterPorId` → risco de **N+1** no hub (mitigável com batch/_include/endpoints dedicados).
- Fase B depende de recursos ainda inexistentes no hub (**AllergyIntolerance**, **Coverage**) e de **fixar URLs canônicas RNDS/eSUS**.
- Reversibilidade só é real enquanto o blob estiver intacto **e** o import não o tocar — por isso o import-merge e o gate são pré-requisitos, e o hub não expõe `_history` (sem backup não há undo).

## Plano faseado

### Fase A — Demografia 100% nativa (MERGE/UPSERT) + pré-requisitos estruturais
Inclui os pré-requisitos P0 (podem/devem ir a prod antes do restante, por serem correções de bug ativo):
- **P0.1** UPSERT de identifiers (mata o `Identifier = []`) — hotfix isolado.
- **P0.2** Concorrência otimista (`If-Match`/ETag) hub + clientes + retry.
- **P0.3** Importação vira merge/preserve (preserva o blob e as casas nativas geridas pelo smsmarica; nunca replace-all).
- Refator `PacienteFhirMapper` para native-first total (name official+nickname, identifiers upsert, birthDate, gender, address[0]+número+geolocation, telecom por use+rank, email, contact MTH/FTH/GUARD/SPS/emergência, maritalStatus com coding v3, photo).
- Geocoder-on-save (lat/long reais).
- Blob **encolhe para os clínicos** na leitura (fonte), mas **continua sendo escrito por completo** como dual-write (reversibilidade máxima).
- Backfill de **promoção** blob→nativo (hub paginado + job smsmarica-side reusando o merge), idempotente, com validação round-trip contra golden **pré-deploy**.
- Front intocado.
- **Risco**: Baixo/Médio — aditivo e reversível; risco concentrado no merge de identifiers/telecom (coberto por testes de não-regressão de perda de dados).

### Fase B — Extensões canônicas + clínicos em recursos próprios
- Extensões RNDS/eSUS: raça-cor, escolaridade, ocupação (CBO), naturalidade (`patient-birthPlace`), nacionalidade. **Fixar URLs de profile (BRIndividuo) antes de gravar.**
- Clínicos → recursos FHIR próprios: altura (LOINC 8302-2), peso (29463-7), ABO (883-9), Rh (10331-7); Condition (CID-10, reusa `LimparCodigoCid`); MedicationStatement (auto-relato) vs MedicationRequest (decisão); AllergyIntolerance e Coverage (**exigem implementar no hub**).
- **Reconciliação das duas origens** (blob vs `urn:salux:extras`) definida por campo, para não duplicar Observation.
- `ParaDto` passa a **agregar** esses recursos mantendo o shape do DTO (front intocado), aceitando custo de leitura extra.
- **Risco**: Médio/Alto — recursos ausentes no hub + N+1 + semântica clínica (vital-signs vs dado cadastral).

### Fase C — Aposentar o blob (por campo, com gate)
- Remove a extension `urn:smsmarica:paciente-payload` **preservando `urn:salux:extras`**; remove `Payload`/`LerPayload` e o ramo de blob em `AplicarNome`; retira os fallbacks `?? pl.X` do `ParaDto`.
- **Gate por campo**: só remove os campos cujo recurso-destino **já existe e está populado** (AllergyIntolerance/Coverage bloqueiam alergias/planoSaúde). `Observacoes` (sem casa limpa no R4) é o **último** e exige destino definido antes.
- Disparado só após soak (dias) com leitura 100% nativa e relatório de divergências zerado; exige backup do `fhir.patient`.
- **Risco**: Baixo se A/B validados, mas **irreversível** (perde a rede de segurança).

## Migração / backfill

- **Local**: job **smsmarica-side** reusando o `PacienteFhirMapper` (fonte única das regras de mapeamento) + o caminho `If-Match`; requer **adicionar paginação keyset ao hub** (`_count` + cursor + `Bundle.Link[next]`) — desejável de qualquer modo (hoje `BuscarAsync` capa em 50 sem offset).
- **Idempotência**: promove só campo nativo **ausente ou divergente**; rodar 2x é no-op; **nunca remove o blob** nas Fases A/B.
- **Reversibilidade**: com blob intacto + import-merge + dual-write, rollback = redeploy do mapper anterior, dados preservados.
- **Validação round-trip (alvo fixo)**: capturar snapshot do `ParaDto` com o mapper **ANTIGO (pré-deploy)** em tabela/arquivo (golden); após deploy + promoção, comparar `ParaDto` **NOVO** ao golden campo a campo; divergências vão para a trilha de qualidade (reusa `pep_sincronizacao_falha`/`RegistradorFalhasPep` ou tabela `paciente_promocao_divergencia`) **sem abortar o lote**. Comparar novo-contra-novo não prova nada.
- **Segurança**: backup/dump do `fhir.patient` antes do lote; rodar primeiro em cópia/treinamento.
- **Ordem/gate**: Fase A só demografia; Fase B só depois que os recursos filhos existirem no hub; **backfill nunca roda com import ativo** (§6).
- **Interação delete×reimport**: paciente soft-deleted não é promovido nem tem blob removido; um reimport que o "ressuscite" cria duplicata sem blob. Mitigação: mapear `ReativarAsync`/undelete no hub OU tratar soft-deleted explicitamente no backfill e no import (item de Fase A/B a resolver).

## Alternativas consideradas

- **Manter o blob (não fazer nada)**: rejeitada. O shadowing e o `Identifier = []` são bugs ativos que corrompem dados reais e podem duplicar clínico em produção; o blob também impede busca por telefone de pacientes criados e mantém lat/long `0,0`.
- **Corrigir só o caminho de edição (design original)**: rejeitada. A importação continuaria replace-all, clobberando as promoções por dias e destruindo clínico — quebra idempotência/reversibilidade. Import-merge é bloqueante.
- **Backfill hub-side (job interno do Automais.Fhir sobre a tabela)**: viável, mas duplicaria as regras de mapeamento no hub (que não deve depender do `smsmarica.Core`). Preferimos paginar o hub e rodar o backfill no server reusando o `PacienteFhirMapper`.
- **Lat/long em `smsmarica.localizacao`** (em vez de `address.geolocation`): pela régua é dado operacional de transporte; porém geocodificar no save e guardar no endereço mantém o dado FHIR-clean e coeso com o endereço que o originou. Mantida como alternativa se surgir necessidade operacional (ex.: correção manual de pino sem alterar endereço).
- **Foto inline (`Patient.photo` base64)**: correto por FHIR, mas infla o `Content` JSONB e faz todo `GET Patient` trafegar a imagem. Avaliar `Binary` + `Attachment.url` (decisão aberta; não bloqueia Fase A).
- **Peso/altura como Observation vital-signs (pipeline Salux)**: semanticamente enganoso para dado **cadastral auto-relatado** (vital-signs pressupõe medição em atendimento com `effective`/`encounter`). Decisão: em Fase B usar `category=social-history`/sem `encounter` para o dado cadastral, distinto do vital medido do Salux.

## Riscos e mitigação

| Risco | Mitigação |
|---|---|
| Perda de identifiers secundários / quebra de re-dedup / duplicação clínica (bug ativo) | **P0.1** UPSERT por system + teste de não-regressão de perda de dados; enviar como hotfix isolado antes do refactor. |
| Reimport multi-dia clobbera edições e clínico | **P0.3** import vira merge/preserve (preserva blob + casas smsmarica); nunca PUT-replace. |
| Lost-update (backfill × edição × import) | **P0.2** `If-Match`/ETag + 409 + re-read-and-retry; **§6** gate import↔backfill. |
| Round-trip validando alvo móvel | Capturar golden do `ParaDto` **pré-deploy**; comparar novo↔golden. |
| Importados perdem EstadoCivil/telefonePrincipal na leitura | **§2/§3** modelo único de telecom (rank+use) e mapa `ds_est_civil`→enum congelado por discovery antes de Fase A. |
| Lat/long `0,0` para todos | **§4** geocoder-on-save + `address.geolocation` (blob fallback). |
| Fase C perde alergias/planoSaúde (destino inexistente) | **§7** gate por presença no destino; AllergyIntolerance/Coverage no hub são pré-requisito de remover esses campos. |
| N+1 no `ObterPorId` (Fase B) | Batch/_include ou endpoints clínicos dedicados; medir antes de generalizar. |
| `ObterPorTelefone` + invariante `contato_validado` (número único) passam a colidir onde antes não indexava | Revisar `FirstOrDefault` em telefone de família; alinhar com a regra de "número único por CPF" (validação de telefone) antes/junto do backfill de telecom. |
| Soft-deleted ressuscitado por reimport vira duplicata sem blob | Tratar soft-deleted no backfill e no import; mapear undelete no hub. |
| Reconciliação blob × `urn:salux:extras` duplica Observation (Fase B) | Definir vencedor por campo antes de criar recursos filhos. |
| Sem `_history` no hub (sem undo) | Backup do `fhir.patient` antes do lote; rodar em cópia/treinamento primeiro. |
