# 01 — Catálogo canônico de procedimentos e busca semântica (incremento 1)

## Objetivo

Um único lugar para responder "que procedimento é esse, em quais sistemas existe e quem executa internamente", alimentado pelos três catálogos que já são sincronizados (SISREG, SER, SERNIT), com busca por texto livre que combina similaridade vetorial (pgvector) e busca lexical. É o passo 1 do wizard e a base do cadastro de regras.

## Requisitos cobertos

- **R-02 (A2)** — busca vetorizada no Postgres; a consulta vira vetor; resultado lista unidades internas e existência externa.
- **R-05 (A4)**, parcialmente — os recursos SER e SERNIT passam a ter identidade comum (o pareamento em si é o plano 08).

## Decisões aplicadas

**D-10 (05/09/2026)** — o catálogo canônico é **plano**. O balde do SERNIT (`Endocrinologia`) e as
subespecialidades do SER (`ENDOCRINOLOGIA - DIABETES`) são canônicos **distintos**; a busca devolve
todos, e quem desempata é o agente regulador, que pode trocar o procedimento na triagem. Regra
operacional que sai daí, medida no spike c: **só fundem em um canônico as origens que casam pela
chave D3** (igualdade de tokens + público, ver [`revisoes/spike-c-paridade.md` §1](revisoes/spike-c-paridade.md));
**contenção nunca funde** — os 17 casos de `Endocrinologia ⊃ ENDOCRINOLOGIA - DIABETES` viram, no
máximo, sugestão de curadoria, e a busca **não** deduplica um contra o outro.

Escolhas próprias deste plano: embedding na **origem** (por sistema), não no canônico; pareamento entre sistemas é **sugerido e confirmado**, nunca automático (mesmo idioma de `SugeridoSigtapId`/`ConfirmadoEm` em `sisreg_procedimento_sigtap`).

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Extensão pgvector, `UseVector()`, tipo `vector` no schema `smsmarica` | sim | `SMSMais.Data/SmsMaisDbContext.cs:256`, `SMSMais.Data/DependencyInjection.cs:46`, migration `20260605015019_AddInteligenciaIa` |
| Serviço de embeddings (Voyage `voyage-3`, 1024 dims, token cifrado no banco) | sim | `SMSMais.Core/Inteligencia/Provedores/IServicoEmbeddings.cs`, `VoyageEmbeddings.cs` (`EmbeddarAsync`, `EmbeddarLoteAsync`); token em `IaConfiguracao.TokenEmbeddingsCifrado` |
| Busca por distância via EF | sim | `SMSMais.Core/Inteligencia/Conhecimento/RecuperadorContexto.cs:115` (`L2Distance`); aqui usar `CosineDistance` |
| `unaccent` | sim | `ConversaService.cs:338` usa `EF.Functions.Unaccent` + `ILike` |
| Catálogo SISREG (pa 7 dígitos → SIGTAP, nome, grupo) | sim | `SMSMais.Data/Entities/Sisreg/SisregProcedimentoSigtap.cs` |
| Oferta interna (unidade × procedimento × vigência × vagas) | sim | `sisreg_escala` (`SMSMais.Data/Entities/Sisreg/`), sincronizada por `EscalasSincronizacaoService`; também `sisreg_profissional_unidade` × `sisreg_procedimento_profissional` |
| Catálogo SER (tipo + valor + ramo "ambulatório estadual" + rótulo) | sim | `SMSMais.Data/Entities/Ser/SerCatalogo.cs` (`SerCatalogoRecurso`), `SerCatalogoSyncService` |
| Catálogo SERNIT (tipo + valor + rótulo) | sim | `SMSMais.Data/Entities/Sernit/`, `SernitCatalogoSyncService` |
| Heurística de similaridade textual e a regra "só exato auto-confirma" | sim | `SMSMais.Core/Integracoes/SisregWeb/Varredura/Sigtap/SugestaoSigtap.cs` + testes |
| Fixture de teste com pgvector | sim | `tests/SMSMais.Tests/Infraestrutura/PostgresFixture.cs` (imagem `pgvector/pgvector:pg16`) |

O que **não** existe: coluna vetorial em qualquer catálogo; índice HNSW; tabela que una os três universos; `pg_trgm` (não é necessário).

## Desenho

### Tabelas (schema `smsmarica`)

**`regulacao_procedimento`** — o procedimento canônico (o que o usuário vê como "um" procedimento).

| Coluna | Tipo | Nota |
|---|---|---|
| id | uuid PK | |
| nome_canonico | text NOT NULL | primeiro rótulo visto; editável na curadoria |
| nome_normalizado | text NOT NULL, índice | `unaccent(upper(trim()))`, para a busca lexical |
| tipo | enum `TipoProcedimentoRegulacao` (Consulta=1, Exame=2, Cirurgia=3, Outro=9) | derivado da origem |
| procedimento_sigtap_id | FK `procedimento_sigtap` NULL | quando alguma origem SISREG traz |
| ativo | bool | falso quando nenhuma origem ativa aponta para ele |
| criado_em/por, atualizado_em/por | ADR-0006 | |

**`regulacao_procedimento_origem`** — uma linha por (sistema, recurso). É aqui que mora o embedding.

| Coluna | Tipo | Nota |
|---|---|---|
| id | uuid PK | |
| procedimento_id | FK `regulacao_procedimento` NOT NULL | |
| sistema | enum `SistemaRegulacao` (Sisreg=1, Ser=2, Sernit=3, Esus=4) | |
| chave_externa | text NOT NULL | SISREG: `pa` 7 dígitos; SER: `tipo|valor|ramo`; SERNIT: `tipo|valor`. **Unique (sistema, chave_externa)** |
| rotulo_externo | text NOT NULL | como o sistema chama |
| ramo | text NULL | SER: "ambulatório estadual" S/N |
| sisreg_procedimento_sigtap_id / ser_catalogo_recurso_id / sernit_catalogo_recurso_id | FK NULL | ponte para o catálogo de cada sistema |
| embedding | `vector(1024)` NULL | texto embedado = `rotulo_externo` + tipo + ramo |
| embedding_hash | text NULL | sha256 de `modelo|texto`; se igual, não re-embeda |
| embedding_em | timestamptz NULL | |
| vinculo | enum (Automatico=1, Confirmado=2) | como esta origem chegou ao canônico |
| sugerido_procedimento_id, sugerido_score | FK NULL, float NULL | sugestão de pareamento pendente de curadoria |
| confirmado_em/por | | |
| ativo | bool | some do catálogo de origem → falso |

Índices: HNSW em `embedding` (`vector_cosine_ops`) criado por `migrationBuilder.Sql(...)` — o EF não gera; `(sistema, ativo)`; `(procedimento_id)`.

Por que o embedding fica na origem: a busca precisa recordar "AMBULATÓRIO 1ª VEZ EM CARDIOLOGIA" (SER) e "CONSULTA EM CARDIOLOGIA" (SISREG) de forma independente; o agrupamento por sistema é o que a tela mostra.

### Sincronização (`RegulacaoCatalogoService.SincronizarAsync`)

Em `SMSMais.Core/Regulacao/Catalogo/`. Disparada ao fim de `EscalasSincronizacaoService`, `SerCatalogoSyncService` e `SernitCatalogoSyncService` (chamada direta, não evento), e por botão na tela de curadoria.

1. Upsert das origens a partir de `sisreg_procedimento_sigtap` (+ nomes distintos de `sisreg_escala` que não estejam no catálogo), `ser_catalogo_recurso`, `sernit_catalogo_recurso`. Origem que sumiu do catálogo de sistema → `ativo=false` (não apaga).
2. Origem nova sem canônico: cria canônico 1:1 (nome = rótulo). Depois, se existir origem **de outro sistema** com cosine ≥ 0,85, grava `sugerido_procedimento_id`/`sugerido_score`. Só a curadoria confirma.
   O casamento automático usa a **chave D3** do spike c (não o rótulo cru, que acha 3 pares em 482×78);
   o cosine só entra para o que a chave não fechou. Corpus de aceitação pronto: os **17 casos de
   contenção** do spike c §3 devem aparecer como sugestão, e os **2 falsos** (`Cirurgia Plástica
   Pediátrica` → `CIRURGIA PLASTICA - ORELHA`, `Ortopedia Pediátrica` → `ORTOPEDIA (NÃO CIRURGICO)`)
   **não** devem — nos dois a dimensão público é o que os separa.
3. Recalcula `embedding_hash`; o que mudou vai para `IServicoEmbeddings.EmbeddarLoteAsync` em lotes de 128. Falha do provedor **não derruba** o sync: a origem fica com `embedding=null` e só entra na busca pelo caminho lexical. Log e contador de "sem embedding".

### Busca (`GET /regulacao/procedimentos/buscar?q=&tipo=`)

Híbrida, sem `pg_trgm`:

1. **Lexical**: `EF.Functions.ILike(EF.Functions.Unaccent(nome_normalizado), %q%)` sobre canônico e origens → score 1,0 (exatos e prefixos primeiro).
2. **Vetorial**: `EmbeddarAsync(q)` com **cache LRU em memória por texto normalizado** (a UI chama a cada pausa de digitação, não a cada tecla; o cache evita pagar a Voyage duas vezes pela mesma consulta) → `OrderBy(o => o.Embedding.CosineDistance(v)).Take(30)`, corte por distância (**calibrar** com 20 consultas reais no incremento 1; ponto de partida 0,45).
3. União por `procedimento_id`, filtro `ativo`, ordenação (lexical primeiro, depois por distância). Se a Voyage falhar (`VoyageEmbeddings` lança `ConflitoException`), a busca devolve só o lexical e sinaliza `degradada=true` — **a busca nunca morre por causa do provedor**.

Resposta por procedimento: `{ id, nome, tipo, origens: [{sistema, rotulo, ramo}], executantesInternos: [...], existeExterno: {ser: bool, sernit: bool} }`.

### Oferta interna e lado externo

- **Executantes internos**: `sisreg_escala` onde `procedimento_codigo` = chave da origem SISREG (para grupo `…000`, expandir para os itens do grupo via `sisreg_procedimento_sigtap.Grupo`/prefixo), `status = Ativa`, `vigencia_fim >= hoje`, `ausente = false` → agrupar por `unidade_id`: nome, CNES, soma de `vagas_total`, próxima vigência. Sem escala vigente → "sem oferta interna no momento".
- **Lado externo**: existência de origem `Ser` (com ramo AE / não-AE) e `Sernit`. Informativo opcional: `distinct ser_solicitacao.unidade_executora` para o recurso ("onde já foi atendido"). **Nenhuma unidade escolhível** (R-03).

### Curadoria (tela, módulo 51)

Em Regulação → Configuração → aba "Catálogo de procedimentos": lista de sugestões de pareamento (origem, canônico sugerido, score) com Confirmar / Rejeitar / Escolher outro; renomear canônico; botão "Sincronizar agora"; contador de origens sem embedding.

## Tarefas

- [ ] **1.1** Entidades `RegulacaoProcedimento`, `RegulacaoProcedimentoOrigem` em `SMSMais.Data/Entities/Regulacao/`; enums `SistemaRegulacao`, `TipoProcedimentoRegulacao`; configurações EF (`HasColumnType("vector(1024)")`); `DbSet`s; migration `CatalogoCanonicoDeProcedimentos` com o `CREATE INDEX ... USING hnsw` em SQL cru.
- [ ] **1.2** `RegulacaoCatalogoService.SincronizarAsync` + chamada ao fim dos três syncs existentes + endpoint `POST /regulacao/procedimentos/sincronizar` (51).
- [ ] **1.3** Embeddings: hash, lotes, tolerância a falha, contador; reusar `IServicoEmbeddings` sem alterá-lo.
- [ ] **1.4** `RegulacaoProcedimentoBuscaService` (lexical ∪ vetorial, cache LRU, `degradada`) + `GET /regulacao/procedimentos/buscar` (47 Consulta).
- [ ] **1.5** Oferta interna a partir de `sisreg_escala` (grupo expandido) e lado externo; incluir na resposta da busca.
- [ ] **1.6** Front: feature `SMSMais.front/src/features/regulacao/` com `api/`, `components/BuscaProcedimento.tsx` (debounce 300 ms, lista com dois blocos: interno com unidades, externo com sistemas) e página de curadoria; item de menu só no incremento 3 (por ora a busca é usada dentro do wizard).
- [ ] **1.7** Testes: `EmbeddingsFake : IServicoEmbeddings` (hash determinístico → vetor); sync a partir de seeds locais dos três catálogos; hash impede re-embed; busca híbrida no Testcontainers; executantes só ativas/vigentes; grupo `…000` expande; falha do provedor degrada sem lançar.
- [ ] **1.8** Promover `adr/0055-catalogo-canonico-e-embeddings.md` para `docs/adr/` quando o incremento for para produção (renumerar se 0055 já tiver sido usado).

## Dependências

Token da Voyage já configurado em prod (módulo IA). Nenhum outro plano.

## Riscos e pontos a confirmar

- Custo: catálogo inteiro (~5 mil rótulos × ~12 tokens) é centavos; a consulta é ~10 tokens. O risco é chamar por tecla — daí o debounce + cache.
- Dimensão travada em 1024 pelo tipo da coluna; trocar de modelo exige migration. Manter o mesmo modelo do módulo IA.
- Sem índice, a busca vetorial é scan sequencial; com HNSW e filtro `ativo` pode ser que o planner ignore o índice — medir com `EXPLAIN` no incremento 1.
- O nome do procedimento no SISREG é o eixo (memória do projeto: o "SIGTAP" exportado é defasado); não amarrar o canônico ao SIGTAP.

## Testes

Listados na tarefa 1.7. Verificação manual: buscar "implante capilar" (não existe) → vazio sem erro; buscar "cardiologia" → canônicos com origens SISREG/SER/SERNIT, unidades internas com vagas; desligar o token da Voyage → busca continua (só lexical, `degradada=true`).

## Fora de escopo

Pareamento SER × SERNIT em massa (plano 08); regras (plano 03); qualquer escrita nos sistemas.

---

## Especificação para execução

> Escrita para ser seguida sem decisões. Onde houver dúvida, o precedente citado vence. Convenções: entidades em `SMSMais.Data/Entities/Regulacao/`, configurações em `SMSMais.Data/Configurations/Regulacao/` (`internal sealed class XConfiguration : IEntityTypeConfiguration<X>`, `builder.ToTable("regulacao_…")`, colunas `snake_case` via `HasColumnName`, copiar o estilo de `SerSolicitacaoRascunhoConfiguration`), serviços em `SMSMais.Core/Regulacao/Catalogo/`, controller em `SMSMais.Api/Controllers/RegulacaoProcedimentosController.cs`, front em `SMSMais.front/src/features/regulacao/`.

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `SMSMais.Data/Entities/Regulacao/RegulacaoProcedimento.cs`, `RegulacaoProcedimentoOrigem.cs` | criar |
| `SMSMais.Data/Entities/Enums/SistemaRegulacao.cs`, `TipoProcedimentoRegulacao.cs`, `VinculoOrigemRegulacao.cs` | criar |
| `SMSMais.Data/Configurations/Regulacao/RegulacaoProcedimentoConfiguration.cs`, `RegulacaoProcedimentoOrigemConfiguration.cs` | criar |
| `SMSMais.Data/SmsMaisDbContext.cs` | adicionar `DbSet<RegulacaoProcedimento> RegulacaoProcedimentos` e `DbSet<RegulacaoProcedimentoOrigem> RegulacaoProcedimentoOrigens` |
| `SMSMais.Data/Migrations/<ts>_CatalogoCanonicoDeProcedimentos.cs` | gerar; editar `Up()`/`Down()` para o índice HNSW em SQL cru |
| `SMSMais.Core/Regulacao/Catalogo/IRegulacaoCatalogoService.cs`, `RegulacaoCatalogoService.cs` | criar (sync + embeddings + curadoria) |
| `SMSMais.Core/Regulacao/Catalogo/IRegulacaoProcedimentoBuscaService.cs`, `RegulacaoProcedimentoBuscaService.cs`, `CacheVetorConsulta.cs` | criar (busca híbrida + cache) |
| `SMSMais.Core/Regulacao/Catalogo/Dtos/RegulacaoProcedimentoDtos.cs` | criar |
| `SMSMais.Core/DependencyInjection.cs` | bloco `// ---- REGULAÇÃO → SOLICITAÇÕES (ADR-0052)`: `AddScoped` dos dois serviços; `AddSingleton<Regulacao.Catalogo.CacheVetorConsulta>` |
| `SMSMais.Core/Integracoes/SisregWeb/Escalas/EscalasSincronizacaoService.cs`, `SMSMais.Core/Ser/SerCatalogoSyncService.cs`, `SMSMais.Core/Sernit/SernitCatalogoSyncService.cs` | ao fim do sync, `await catalogo.SincronizarAsync(ct)` dentro de `try/catch` que só loga (o sync de origem nunca falha por causa do canônico) |
| `SMSMais.Api/Controllers/RegulacaoProcedimentosController.cs` | criar |
| `SMSMais.front/src/features/regulacao/types.ts`, `api/regulacaoApi.ts`, `api/queries.ts` | criar |
| `SMSMais.front/src/features/regulacao/components/BuscaProcedimento.tsx` | criar |
| `SMSMais.front/src/features/regulacao/pages/CatalogoCuradoriaPage.tsx` | criar (vira aba em `features/ser/pages/RegulacaoConfiguracaoPage.tsx`) |
| `tests/SMSMais.Tests/Regulacao/Catalogo/*.cs`, `tests/SMSMais.Tests/Infraestrutura/EmbeddingsFake.cs` | criar |

### B. Entidades

```csharp
namespace SMSMais.Data.Entities.Regulacao;

public sealed class RegulacaoProcedimento
{
    public Guid Id { get; set; }
    public string NomeCanonico { get; set; } = string.Empty;      // nome_canonico, max 300
    public string NomeNormalizado { get; set; } = string.Empty;   // nome_normalizado, max 300, índice
    public TipoProcedimentoRegulacao Tipo { get; set; }           // tipo
    public Guid? ProcedimentoSigtapId { get; set; }               // procedimento_sigtap_id (FK procedimento_sigtap, SetNull)
    public bool Ativo { get; set; } = true;                        // ativo
    public DateTime CriadoEm { get; set; }  public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }  public Guid? AtualizadoPor { get; set; }
    public ICollection<RegulacaoProcedimentoOrigem> Origens { get; set; } = [];
}

public sealed class RegulacaoProcedimentoOrigem
{
    public Guid Id { get; set; }
    public Guid ProcedimentoId { get; set; }  public RegulacaoProcedimento? Procedimento { get; set; }
    public SistemaRegulacao Sistema { get; set; }                  // sistema
    public string ChaveExterna { get; set; } = string.Empty;       // chave_externa, max 120
    public string RotuloExterno { get; set; } = string.Empty;      // rotulo_externo, max 300
    public string? Ramo { get; set; }                               // ramo, max 20 ("AE" | "NAO_AE"), só SER
    public Guid? SisregProcedimentoSigtapId { get; set; }          // FK sisreg_procedimento_sigtap, SetNull
    public Guid? SerCatalogoRecursoId { get; set; }                // FK ser_catalogo_recurso, SetNull
    public Guid? SernitCatalogoRecursoId { get; set; }             // FK sernit_catalogo_recurso, SetNull
    public Pgvector.Vector? Embedding { get; set; }                // embedding vector(1024)
    public string? EmbeddingHash { get; set; }                     // embedding_hash, max 64
    public DateTime? EmbeddingEm { get; set; }                     // embedding_em
    public VinculoOrigemRegulacao Vinculo { get; set; }            // vinculo
    public Guid? SugeridoProcedimentoId { get; set; }              // sugerido_procedimento_id
    public double? SugeridoScore { get; set; }                     // sugerido_score
    public DateTime? ConfirmadoEm { get; set; }  public Guid? ConfirmadoPor { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }  public DateTime? AtualizadoEm { get; set; }
}
```

Enums: `SistemaRegulacao { Sisreg = 1, Ser = 2, Sernit = 3, Esus = 4 }` · `TipoProcedimentoRegulacao { Consulta = 1, Exame = 2, Cirurgia = 3, Outro = 9 }` · `VinculoOrigemRegulacao { Automatico = 1, Confirmado = 2 }`.

Configuração: tabelas `regulacao_procedimento` e `regulacao_procedimento_origem`; `Embedding` com `.HasColumnType("vector(1024)")` (igual a `IaChunkConhecimentoConfiguration`); índices `ix_regulacao_procedimento_nome_normalizado`, `ix_regulacao_proc_origem_procedimento`, `ix_regulacao_proc_origem_sistema_ativo (sistema, ativo)`; unique `ux_regulacao_proc_origem_sistema_chave (sistema, chave_externa)`. Na migration, após os `CreateIndex`:

```csharp
migrationBuilder.Sql("CREATE INDEX ix_regulacao_proc_origem_embedding_hnsw ON smsmarica.regulacao_procedimento_origem USING hnsw (embedding vector_cosine_ops);");
// Down(): migrationBuilder.Sql("DROP INDEX IF EXISTS smsmarica.ix_regulacao_proc_origem_embedding_hnsw;");
```

Chave externa: SISREG = `pa` (7 dígitos, `SisregProcedimentoSigtap.Codigo`); SER = `$"{tipo}|{valor}|{(ambulatorioEstadual ? "AE" : "NAO_AE")}"`; SERNIT = `$"{tipo}|{valor}"`.

### C. Serviços e DTOs

```csharp
public interface IRegulacaoCatalogoService
{
    Task<RegulacaoCatalogoSyncResultadoDto> SincronizarAsync(CancellationToken ct);
    Task<IReadOnlyList<RegulacaoSugestaoPareamentoDto>> ListarSugestoesAsync(CancellationToken ct);
    Task ConfirmarPareamentoAsync(Guid origemId, Guid procedimentoId, CancellationToken ct); // move a origem, Vinculo=Confirmado, desativa canônico que ficou órfão
    Task RejeitarPareamentoAsync(Guid origemId, CancellationToken ct);                         // limpa sugestão
    Task RenomearCanonicoAsync(Guid procedimentoId, string nome, CancellationToken ct);
}
public interface IRegulacaoProcedimentoBuscaService
{
    Task<RegulacaoBuscaResultadoDto> BuscarAsync(string termo, TipoProcedimentoRegulacao? tipo, int limite, CancellationToken ct);
    Task<RegulacaoProcedimentoDetalheDto> ObterAsync(Guid procedimentoId, CancellationToken ct);
}

public sealed record RegulacaoCatalogoSyncResultadoDto(int OrigensNovas, int OrigensDesativadas, int CanonicosNovos, int EmbeddingsGerados, int SemEmbedding, int Sugestoes);
public sealed record RegulacaoOrigemDto(Guid Id, SistemaRegulacao Sistema, string Rotulo, string? Ramo, string ChaveExterna);
public sealed record ExecutanteInternoDto(Guid UnidadeId, string Nome, string? Cnes, int VagasTotal, DateOnly? ProximaVigencia);
public sealed record ExisteExternoDto(bool Ser, bool SerAmbulatorioEstadual, bool Sernit);
public sealed record RegulacaoProcedimentoItemDto(Guid Id, string Nome, TipoProcedimentoRegulacao Tipo, double Score, IReadOnlyList<RegulacaoOrigemDto> Origens, IReadOnlyList<ExecutanteInternoDto> ExecutantesInternos, ExisteExternoDto ExisteExterno);
public sealed record RegulacaoBuscaResultadoDto(IReadOnlyList<RegulacaoProcedimentoItemDto> Itens, bool Degradada);
public sealed record RegulacaoProcedimentoDetalheDto(Guid Id, string Nome, TipoProcedimentoRegulacao Tipo, string? CodigoSigtap, IReadOnlyList<RegulacaoOrigemDto> Origens, IReadOnlyList<ExecutanteInternoDto> ExecutantesInternos, ExisteExternoDto ExisteExterno);
public sealed record RegulacaoSugestaoPareamentoDto(Guid OrigemId, SistemaRegulacao Sistema, string Rotulo, Guid CanonicoAtualId, string CanonicoAtual, Guid SugeridoId, string SugeridoNome, double Score);
```

`RegulacaoCatalogoService(SmsMaisDbContext db, IServicoEmbeddings embeddings, IUsuarioAtualAccessor usuarioAtual, ILogger<RegulacaoCatalogoService> log)` — `SincronizarAsync`:
1. `db.SisregProcedimentosSigtap` → origens `Sisreg`; `db.SerCatalogoRecursos` → `Ser` (ramo pelo `AmbulatorioEstadual`); `db.SernitCatalogoRecursos` → `Sernit`. Upsert por `(Sistema, ChaveExterna)`; rótulo mudou → atualiza.
2. Origem que não veio → `Ativo = false` (nunca apaga).
3. Origem nova sem canônico → cria `RegulacaoProcedimento` (`NomeCanonico = RotuloExterno`; `NomeNormalizado` pelo mesmo normalizador de `MapeadorSigtapSisreg`/`NormalizacaoNomeProcedimento`; `Tipo`: SER/SERNIT por `TipoRecursoSer` → Consulta/Exame; SISREG por subgrupo SIGTAP se houver, senão `Outro`).
4. Embeddings: `texto = $"{RotuloExterno} ({Sistema}{(Ramo is null ? "" : " " + Ramo)})"`, `hash = SHA256($"{modelo}|{texto}")`; só onde `EmbeddingHash != hash`; lotes de 128 em `EmbeddarLoteAsync`; `catch` por lote → log + contador `SemEmbedding`, segue.
5. Sugestões: para origem sem `ConfirmadoEm`, menor `CosineDistance` contra origens de **outro** sistema; se `1 - d >= 0.85` e canônico diferente → grava `SugeridoProcedimentoId/Score`.

`RegulacaoProcedimentoBuscaService(SmsMaisDbContext db, IServicoEmbeddings embeddings, CacheVetorConsulta cache, IRegulacaoConfiguracaoService config)` — `BuscarAsync`:
- `termo.Trim().Length < 3` → `ValidacaoException("q", "Informe ao menos 3 caracteres.")`.
- lexical: `db.RegulacaoProcedimentos.Where(p => p.Ativo && EF.Functions.ILike(EF.Functions.Unaccent(p.NomeNormalizado), $"%{n}%"))` ∪ `db.RegulacaoProcedimentoOrigens.Where(o => o.Ativo && EF.Functions.ILike(EF.Functions.Unaccent(o.RotuloExterno), $"%{n}%")).Select(o => o.ProcedimentoId)`; score 1,0.
- vetorial: `var v = await cache.ObterOuEmbedarAsync(n, embeddings, ct)` (LRU 500 entradas); `db.RegulacaoProcedimentoOrigens.Where(o => o.Ativo && o.Embedding != null).Select(o => new { o.ProcedimentoId, D = o.Embedding!.CosineDistance(v) }).OrderBy(x => x.D).Take(30)`; manter `D <= config.BuscaCorteDistancia`; score `1 - D`.
- `try/catch` em torno do vetorial: `ConflitoException`/`HttpRequestException`/`TaskCanceledException` → `Degradada = true`.
- monta itens por `ProcedimentoId` (lexical primeiro, depois por score); para cada item: `Origens`, `ExisteExterno` (há origem Ser/Sernit ativa; `SerAmbulatorioEstadual` = há origem Ser com `Ramo == "AE"`), `ExecutantesInternos` = `db.SisregEscalas.Where(e => codigosPa.Contains(e.ProcedimentoCodigo) && e.Status == StatusAtiva && e.VigenciaFim >= hoje && !e.Ausente).GroupBy(e => new { e.UnidadeId, e.UnidadeNomeSisreg, e.Cnes })` → soma de `VagasTotal`, mínima `VigenciaInicio` futura. `codigosPa` = chaves das origens SISREG; para código terminado em `000`, acrescentar os itens do grupo (mesma regra de grupo usada em `SisregProcedimentoSigtap.Grupo`).
- `Take(limite)` (default 20, máx 50).

`CacheVetorConsulta`: singleton; `ConcurrentDictionary<string, float[]>` + fila de ordem para despejo (cap 500); `Task<float[]> ObterOuEmbedarAsync(string termoNormalizado, IServicoEmbeddings emb, CancellationToken ct)`.

### D. Endpoints — `RegulacaoProcedimentosController`, `[ApiController] [Route("regulacao/procedimentos")]`

| Verbo | Rota | `[RequerPermissao]` | Entrada | Saída |
|---|---|---|---|---|
| GET | `buscar?q=&tipo=&limite=20` | `Regulacao`, `Consulta` | query | `RegulacaoBuscaResultadoDto` |
| GET | `{id:guid}` | `Regulacao`, `Consulta` | — | `RegulacaoProcedimentoDetalheDto` |
| POST | `sincronizar` | `RegulacaoConfiguracao`, `Edicao` | — | `RegulacaoCatalogoSyncResultadoDto` |
| GET | `sugestoes` | `RegulacaoConfiguracao`, `Consulta` | — | `RegulacaoSugestaoPareamentoDto[]` |
| POST | `origens/{origemId:guid}/confirmar` | `RegulacaoConfiguracao`, `Edicao` | `{ procedimentoId }` | 204 |
| POST | `origens/{origemId:guid}/rejeitar` | `RegulacaoConfiguracao`, `Edicao` | — | 204 |
| PUT | `{id:guid}/nome` | `RegulacaoConfiguracao`, `Edicao` | `{ nome }` | 204 |

### E. Front

- `types.ts`: `SistemaRegulacao = 'Sisreg' | 'Ser' | 'Sernit' | 'Esus'` (enum viaja como string — ver memória "Enum viaja como STRING"), `TipoProcedimentoRegulacao`, `RegulacaoOrigem`, `ExecutanteInterno`, `ExisteExterno`, `RegulacaoProcedimentoItem`, `BuscaProcedimentoResultado`, `RegulacaoProcedimentoDetalhe`, `SugestaoPareamento`, `CatalogoSyncResultado`.
- `api/regulacaoApi.ts`: `buscarProcedimentos(q, tipo?, limite?)`, `obterProcedimento(id)`, `sincronizarCatalogo()`, `listarSugestoesPareamento()`, `confirmarPareamento(origemId, procedimentoId)`, `rejeitarPareamento(origemId)`, `renomearCanonico(id, nome)` — `http` de `@/shared/api/httpClient`, estilo de `features/ser/api/serApi.ts`.
- `api/queries.ts`: `useBuscaProcedimentos(q, tipo)` (`enabled: q.trim().length >= 3`, `staleTime: 60_000`, key `['regulacao', 'procedimentos', 'busca', q, tipo]`), `useProcedimento(id)`, `useSugestoesPareamento()`, mutations `useSincronizarCatalogo`, `useConfirmarPareamento`, `useRejeitarPareamento`, `useRenomearCanonico` (invalidam `['regulacao', 'procedimentos']`).
- `components/BuscaProcedimento.tsx`: props `{ value: RegulacaoProcedimentoItem | null; onChange: (item: RegulacaoProcedimentoItem | null) => void; tipo?: TipoProcedimentoRegulacao; autoFocus?: boolean }`; input com debounce de 300 ms; lista: nome, chips por sistema, bloco "Interno" (unidades + vagas + vigência) e bloco "Externo" (SER / SER-AE / SERNIT); aviso discreto quando `degradada`; teclado (setas/enter).
- `pages/CatalogoCuradoriaPage.tsx`: tabela de sugestões (Confirmar · Rejeitar · Escolher outro via `BuscaProcedimento`), renomear canônico inline, botão "Sincronizar agora" com o resultado, contador "sem embedding". Registrar como aba "Catálogo de procedimentos" em `RegulacaoConfiguracaoPage.tsx` (padrão `Aba{id,rotulo,conteudo}` de `shared/ui/Tabs`).

### F. Testes (`tests/SMSMais.Tests/Regulacao/Catalogo/`)

- `Infraestrutura/EmbeddingsFake.cs`: `IServicoEmbeddings` determinístico (bag de trigramas do texto normalizado hashados nas 1024 posições, normalizado L2) — textos iguais → iguais; textos parecidos → cosseno alto.
- `RegulacaoCatalogoSyncTests` (com `PostgresFixture`): `Origem_nova_cria_canonico_um_para_um`; `Origem_que_sumiu_fica_inativa_e_nao_e_apagada`; `Hash_igual_nao_reembeda`; `Falha_do_provedor_nao_derruba_o_sync_e_conta_sem_embedding`; `Sugestao_so_entre_sistemas_diferentes_e_acima_do_corte`.
- `RegulacaoProcedimentoBuscaTests`: `Lexical_exato_vem_antes_do_vetorial`; `Provedor_fora_devolve_degradada_com_lexical`; `Executantes_internos_so_escala_ativa_e_vigente`; `Grupo_000_expande_itens`; `Termo_curto_e_recusado`.
- `CacheVetorConsultaTests`: `Mesmo_termo_normalizado_nao_chama_o_provedor_duas_vezes`.

### G. Passo a passo

1. Entidades + enums + configurações + DbSets → `cd SMSMais.server && dotnet build` (0 erros / 0 warnings).
2. `dotnet ef migrations add CatalogoCanonicoDeProcedimentos --project src/SMSMais.Data --startup-project src/SMSMais.Api` → editar `Up/Down` com o HNSW → `dotnet build`.
3. DTOs + serviços + cache + DI → build.
4. Chamadas pós-sync nos três serviços existentes → build → `dotnet test --filter "Escalas|Catalogo"` (os que já existem continuam verdes).
5. Controller → build → `dotnet run --project src/SMSMais.Api` → `/docs` mostra `regulacao/procedimentos/*`.
6. Front: types → api → queries → componente → página/aba → `cd SMSMais.front && npm run build`.
7. Testes novos → `dotnet test --filter Regulacao`.
8. `PROGRESSO.md`: marcar 1.1–1.7; anotar a calibração de `busca_corte_distancia` com 20 termos reais.

### H. Critério de pronto

Buscar "cardiologia" devolve canônicos com origens dos três sistemas e unidades internas com vagas; buscar "xyzw" devolve vazio sem erro; com o token da Voyage inválido a busca continua e mostra "degradada"; `dotnet test --filter Regulacao` verde; `npm run build` verde.
