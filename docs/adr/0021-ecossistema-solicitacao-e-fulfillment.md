# ADR-0021 — Ecossistema de Solicitação (regulação) e fulfillment por categoria

- **Status:** Proposto (aguardando aprovação)
- **Data:** 2026-07-15
- **Relacionados:** ADR-0012 (agendamento local + SISREG só-leitura), ADR-0013 (agenda multi-recurso), ADR-0016 (conciliação de estudo), [[project_visao_fhir_hub]], [[feedback_fhir_rigor_padrao]], [[feedback_regua_smsmarica_vs_fhir]], [[project_sisreg_mapeamento_sigtap]], [[project_sisreg_solicitacao_idempotencia]]

> Substitui a proposta anterior deste número (consulta como agregado irmão de exame). Aquela
> resolvia consulta×exame, mas não enxergava a fusão **regulação × execução** nem o exame
> laboratorial. Este ADR ataca a doença raiz.

## Contexto

A extração de agendamentos do SISREG traz, **misturadas na mesma linha-a-linha de um único
arquivo por unidade executante**, quatro naturezas: exame de imagem, exame laboratorial (futuro),
consulta e cirurgia/OCI. Medição da leva atual (~40 arquivos): ~metade consulta, ~metade
exame/procedimento; unidades grandes (ex.: Ernesto Che Guevara) trazem 5 naturezas no mesmo
arquivo. **Roteamento tem de ser por linha (natureza), não por arquivo.**

Hoje o importador só sabe criar `SolicitacaoExame`, e trava quando o SIGTAP não casa com um
`TipoExame` (ex.: ECG `0211020036`, registro `673378648`). Além disso, **todas** as consultas
colapsam no mesmo SIGTAP `0301010072` — a especialidade só existe no texto —, então `SIGTAP →
TipoExame` é estruturalmente incapaz de representá-las.

### A doença raiz: `SolicitacaoExame` funde duas frentes

O modelo atual é *ao mesmo tempo*:

- **Regulação** (o pedido/autorização): código SISREG, solicitante, unidades, datas de
  solicitação/regulação, confirmação do paciente. — genérico a qualquer natureza.
- **Execução de imagem** (o artefato produzido): AccessionNumber, StudyInstanceUID, worklist,
  retry PACS, preparo de imagem, DataEstudo, autorização da recepção. — específico de imagem/DICOM.

Essa fusão é por que consulta não cabe (herda 25 campos DICOM mortos), por que exame laboratorial
(que **não** tem PACS — vem de integração com laboratório) também não caberia, e por que a régua
FHIR fica confusa. São de fato **duas frentes**:

| Frente | Conceito | FHIR |
|---|---|---|
| **Regulação** | O pedido/autorização (SISREG) | `ServiceRequest` |
| **Recepção/execução** | O que ocorre quando o paciente é atendido + o resultado | `Encounter` + `ImagingStudy`/`DiagnosticReport`/`Procedure` |

### Fato que reduz o risco do split

`Laudo` **não tem FK** para `SolicitacaoExame` — liga-se por `StudyInstanceUID` (string, sem FK
local). Logo, separar a tabela **não afeta o laudo**: ele continua resolvendo o exame pelo StudyUID,
que permanece no satélite de imagem. Laudos reais ([[feedback_laudos_reais_nunca_apagar]]) não são
tocados pela migração estrutural.

## Decisão

Modelar um **ecossistema** que separa regulação de fulfillment, tudo em `smsmarica` (pt-BR domínio,
naming FHIR-inspirado; **não** projetado ao hub FHIR ainda, conforme [[feedback_regua_smsmarica_vs_fhir]]).

### Espinha — `Solicitacao` (regulação / ServiceRequest)

Uma por marcação. É o que a importação SISREG cria **sempre**, para qualquer natureza.

- `PacienteId` (FHIR) + snapshot nome/CNS
- **`Categoria`** (tipo clínico, **positivo**, derivado do subgrupo SIGTAP — NÃO é "vai ao PACS
  ou não"): `Consulta` · `Imagem` · `Laboratorio` · `GraficoFuncional` · `Endoscopia` · `Cirurgia`
  · `Outro`. Categoria = *intenção* (listar/filtrar/rotear módulo); é distinta de qual satélite de
  execução existe (ver abaixo).
- `ProcedimentoSigtapCodigo` (cru) + `ProcedimentoTexto`
- `EspecialidadeTexto` (nullable — só consulta; ponte futura p/ `Especialidade` do módulo Agendamento)
- `UnidadeSolicitanteId` + `UnidadeExecutanteId`
- Solicitante: `SolicitanteNome`, `SolicitanteCpf` (interno), `SolicitanteNumConselho`, `SolicitanteUfConselho`, `SolicitanteConselho`, `SolicitanteUsuarioId?`
- `CodigoSolicitacao` (nº SISREG) + `ChaveConfirmacao` + `RawSisreg` — **índice único parcial** (régua [[project_sisreg_solicitacao_idempotencia]]: `IS NOT NULL AND <> '0000' AND excluido_em IS NULL`)
- Datas: `DataSolicitacao`, `DataRegulacao`, `DataAgendada`
- `Status` (regulação): `Solicitada` · `Agendada` · `Realizada` · `Cancelada`
- `Prioridade`, `Justificativa`, `Observacoes`
- Confirmação do paciente (é sobre a marcação → regulação): `StatusConfirmacao`, `ConfirmadoEm`, `ConfirmadoCanal`, `ConfirmacaoCanceladaEm`, `MotivoCancelamentoPaciente`
- Cancelamento + auditoria ADR-0006 + `RowVersion`

### Satélites — fulfillment por categoria (1 : 0..1, FK `SolicitacaoId`)

Criados só onde/quando a execução existe. **A recepção do paciente é o evento que abre o
fulfillment** (a chave da recepção autoriza; a presença é confirmada; então o exame vai ao PACS /
a consulta vira atendimento).

> **"Não vai ao PACS" NÃO é uma categoria — é a ausência do satélite `ExameImagem`.** A estrutura
> já diz isso: um exame gráfico/funcional (ECG, audiometria) ou laboratorial simplesmente não tem
> `ExameImagem`. Criar uma categoria "não-PACS" seria (a) definição negativa/lixeira, (b)
> duplicação do que `TipoExame.EnviarParaWorklist`/`ModalidadeDicom` já expressam, e (c)
> enrijecer uma fronteira móvel (trocar o equipamento por um DICOM-capaz não deve virar migração
> de linha entre tabelas). O eixo real é *onde o resultado mora*, positivo e realizado por
> presença de satélite.

- **`ExameImagem`** (o que hoje é a metade DICOM do `SolicitacaoExame`): `SolicitacaoId`,
  `TipoExameId?` (modalidade DICOM; **nullable** = mapeamento pendente), `AccessionNumber`,
  `StudyInstanceUID`, `WorklistItemUid`, retry (`TentativasEnvio`/`Ultima…`/`ProximaTentativaEm`/`ErroIntegracaoPacs`),
  `IniciadoEm`, `RealizadoEm`, `DataEstudo`, `ImagensPreparadas…`, autorização recepção
  (`AutorizadoEm`/`AutorizadoPor`). Laudo continua ligado por `StudyInstanceUID`.
- **`ExameLaboratorial`** *(futuro — só esboço agora)*: `SolicitacaoId`, ordem no laboratório,
  `DiagnosticReport`/resultado, coleta/resultado, fonte da integração de lab.
- **`Atendimento`** (consulta) e **`Procedimento`** (cirurgia) *(futuro/fino)*: por ora consulta =
  `Solicitacao` com `Categoria=Consulta` **sem satélite**; ganha `Atendimento`/`Encounter` quando o
  módulo de atendimento existir.

**Regra anti-YAGNI:** na Fase 1 **só `ExameImagem` é construído**. Categorias `Laboratorio`,
`GraficoFuncional`, `Endoscopia`, `Consulta`, `Cirurgia`, `Outro` entram como **espinha sem
satélite** — o resultado, quando houver, é anexo digitalizado (a PWA de arquivos já faz isso). Nada
de tabelas de satélite vazias antecipando integrações que ainda não existem.

### Roteamento por natureza (determinístico — categoriza, não chuta código)

No importador, pelo **subgrupo SIGTAP** (os 4 primeiros dígitos) — é ele, não o grupo, que separa
imagem de gráfico/lab dentro do `02`:

| Subgrupo SIGTAP | Categoria | Cria `ExameImagem`? |
|---|---|---|
| `0301`, `0302` | `Consulta` | não |
| `0204` (RX/mamo/densito), `0205` (US), `0206` (TC/RM) | `Imagem` | **sim** |
| `0202` (lab clínico), `0203` (patologia) | `Laboratorio` | não (satélite futuro) |
| `0211` (ECG, EEG, audiometria, espirometria, Holter/MAPA, fundoscopia) | `GraficoFuncional` | não |
| `0209` (endoscopia) | `Endoscopia` | não |
| `04…` | `Cirurgia` | não |
| demais (`09…` OCI, `07…`, `03xx` clínicos, etc.) | `Outro` | não |

O satélite `ExameImagem` nasce **só** para `Imagem`. Quem decide de fato se vai à worklist é o
`TipoExame` mapeado (`EnviarParaWorklist`); a categoria só roteia a criação do satélite e a UI.
Subgrupo desconhecido/ambíguo cai em `Outro` (espinha simples) — nunca inventa satélite.

## Identidade pública (id exposto pela API) — decisão do cutover

**Para exames, o id público continua sendo o `ExameImagem.Id` (= o `solicitacao_exame.id` antigo,
preservado na migração).** A espinha `Solicitacao.Id` (novo GUID) é interna. Racional: zero quebra
de front, deep-links (`?exame={id}`), anexos, download tokens e associação — todos seguem com os
mesmos ids; o sync exame↔laudo é por `StudyInstanceUID` (intocado). **Consultas** (sem satélite)
expõem o próprio `Solicitacao.Id`. `DTO.Id = ExameImagem?.Id ?? Solicitacao.Id`.

Consequência: dependentes ancoradas na espinha (comunicação/contato/declaração/loginlink) exigem
uma tradução `id público (ExameImagem) → Solicitacao.Id` nos poucos pontos que as tocam. As
ancoradas no satélite (anexo/associação/anamnese/documento) usam o id público direto.

## Migração segura (janela noturna pós-17h; downtime OK, dados inegociáveis)

O split é uma **migração de dados** sobre tabela de produção. Downtime após as 17h é aceitável; a
prioridade é **não perder nem corromper nada**. Estratégia: *expand → migrate → verify → contract*,
com backup e rollback.

**Pré-requisitos de segurança:**
1. **Backup** do schema `smsmarica` (pg_dump) antes de tocar — guardar em `~/SMSMarica-secrets`
   ([[reference_dataprotection_backup]]). Este é o rollback de última instância.
2. Rodar tudo numa **transação** (DDL do Postgres é transacional) — falhou, `ROLLBACK`, nada mudou.
3. Aplicar DDL **manualmente e conferir `smsmarica.__migrations`** — o AutoMigrate do startup NÃO
   aplica ([[reference_automigrate_startup_nao_aplica]]).

**Passos (dentro da janela, app parado):**
1. **Expand** — criar `solicitacao` (espinha) e `exame_imagem` (satélite) vazias. Não mexer ainda em
   `solicitacao_exame`.
2. **Migrate** — para cada linha de `solicitacao_exame`: inserir 1 `solicitacao` (copiando as
   colunas de regulação; `Categoria` derivada do subgrupo SIGTAP do registro — o legado é quase
   todo mamografia/US → `Imagem`) e 1 `exame_imagem` (copiando as colunas de execução,
   `SolicitacaoId` apontando pra espinha, mesmo `Id`/StudyUID). Repontar `exame_associacao` e demais
   FKs de `solicitacao_exame_id` → `exame_imagem_id`.
3. **Verify (gate obrigatório antes de seguir):**
   - `count(solicitacao WHERE categoria=ExameImagem) == count(exame_imagem) == count(solicitacao_exame antigo)`
   - Toda `exame_imagem.solicitacao_id` resolve; toda FK repontada resolve.
   - Todo `StudyInstanceUID` distinto preservado (laudos acham o exame). Spot-check de registros
     com laudo finalizado/assinado.
   - Nº SISREG: idempotência preservada (mesmos códigos, sem duplicar).
4. **Contract** — só após verificação verde: `DROP TABLE solicitacao_exame` (o backup + as duas
   tabelas novas já cobrem tudo). Se algo falhar antes disso, `ROLLBACK` volta ao estado íntegro.

**Rollback:** enquanto `solicitacao_exame` existir, é reverter o app e dropar as tabelas novas. Após
o contract, restaurar do backup. Deploy do app novo só depois do verify verde.

## Plano de implementação (ordem)

**Fase 1 — Fundação (backend):**
1. Entidades `Solicitacao` + `ExameImagem` + enum `Categoria`; `Configuration`s (índice único
   parcial no código; FK `ExameImagem→Solicitacao`); `DbSet`s.
2. Migration estrutural + **script de dados** do split (seção acima), imutável.
3. Reapontar os consumidores por frente (137 arquivos, mecânico mas revisado):
   - **Execução → `ExameImagem`**: worklist (`EnviadorWorklistService`, `ConstrutorMwlItem`),
     preparo/PDF de imagem, conciliação (`ExameAssociacao`), sincronizador PACS.
   - **Regulação → `Solicitacao`**: confirmação WhatsApp (`ComunicacaoPaciente`/handler),
     idempotência, datas, solicitante, listagem.
4. Importador vira **roteador**: passos comuns (paciente CNS→CPF, unidade, idempotência) criam
   `Solicitacao`; `Categoria` vem do subgrupo SIGTAP; só `Imagem` cria `ExameImagem`, demais são só
   espinha. Import **não-bloqueante**: exame de imagem sem `TipoExame` mapeado cria o `ExameImagem`
   com `TipoExameId` null = pendente (não trava a entrada).

**Fase 2 — Superfícies:**
5. Tela de **mapeamento** (ex-"divergências"): vincular/criar `TipoExame` por SIGTAP → backfill dos
   pendentes (os 407 ECG entram de uma vez).
6. Front: listagem **"Solicitações"** com filtro por categoria (a espinha unificada que o modelo
   agora dá de graça) + a tela rica de imagem lendo o satélite. Permissões (`sincronizar-permissoes`).

**Fase 3 — Futuro (fora desta leva):** satélites `ExameLaboratorial` (integração lab),
`Atendimento`/`Encounter` (consulta), `Procedimento` (cirurgia); vínculo `EspecialidadeTexto → Especialidade`.

## Consequências

- ✅ Regulação e execução deixam de brigar: imagem (PACS), laboratório (integração) e consulta
  (atendimento) têm cada um seu satélite — ou nenhum — sem campo morto.
- ✅ Importa a leva inteira, qualquer natureza, sem travar em mapeamento.
- ✅ Listagem unificada "Solicitações" sai de graça (é a espinha) + módulos filtrados por categoria.
- ✅ Alinhado ao norte FHIR (ServiceRequest × Encounter/resultados) sem projetar ao hub ainda.
- ✅ Laudo intacto (liga por StudyUID, sem FK).
- ⚠️ Refator amplo (137 arquivos) + migração de dados em produção — mitigado por janela noturna,
  transação, backup e gate de verificação.
- ⚠️ Duas+ tabelas onde havia uma: queries de execução ganham join com a espinha (custo pequeno).

## Alternativas consideradas

- **Manter fundido (`SolicitacaoExame` faz tudo)** — a doença atual: consulta não cabe, lab não
  cabe, régua FHIR confusa. Descartado.
- **Tabelas irmãs sem espinha (consulta ⟂ exame)** — resolvia consulta×exame, mas repetia a fusão
  regulação/execução dentro de cada uma e não dava lar ao exame laboratorial. Descartado (era o
  0021 anterior).
- **Tabela única `Solicitacao` + `tipo` achatada** — overlap de campos baixo (25 de ~37 são
  exame-imagem-only) → mar de NULLs e guards `if tipo` por toda query de PACS/worklist/laudo.
  Descartado: single-table só serve com alto overlap.
- **Categoria "PACS vs não-PACS"** — descartada por ser (a) definição *negativa* (lixeira que junta
  ECG, lab, endoscopia, consulta só por "não ser imagem"), (b) duplicação de
  `TipoExame.EnviarParaWorklist`/`ModalidadeDicom` que já expressam isso, e (c) enrijecimento de
  fronteira móvel (equipamento novo DICOM-capaz viraria migração de linha). O eixo correto é *onde o
  resultado mora* (positivo), realizado pela presença do satélite `ExameImagem` — a ausência do
  satélite já É "não vai ao PACS".
- **Split incremental "strangler" (ligar, não mover)** — menor risco, mas deixa a fusão meio-a-meio
  por fases. Com janela de downtime + backup, o split completo é seguro e entrega o end-state limpo
  de uma vez. Preterido a favor do split completo.
