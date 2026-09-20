# Padrões do código do SMSMais que o módulo Ouvidoria deve seguir

> Digest técnico levantado em 20/09/2026 por agente de exploração do repositório (Claude Fable 5.1). Caminhos relativos a `SMSMais.server/src/` (backend) e `SMSMais.front/src/` (front). Verificado no código; onde diz "não existe", foi conferido por grep.

## 1. Módulo Tickets — referência de máquina de estados
- Entidades em `SMSMais.Data/Entities/`: `Ticket.cs` (`sealed`, `Guid Id` via `Guid.CreateVersion7()`, `int Numero` identity legível, enums, bloco de auditoria ADR-0006 `CriadoEm/CriadoPor/AtualizadoEm/AtualizadoPor/ExcluidoEm/ExcluidoPor`, `uint RowVersion` = xmin, coleções `Comentarios`, `Anexos`), `TicketComentario.cs`, `TicketAnexo.cs` (`MidiaId` FK para `smsmarica.midia`), `TicketConfiguracao.cs` (singleton `IdSingleton = new("77777777-0000-0000-0000-000000000001")`).
- **Configuration EF**: um arquivo `Configurations/TicketConfiguration.cs` com várias classes `internal sealed class X : IEntityTypeConfiguration<X>`. `ToTable("ticket")`, `HasColumnName("snake_case")` em **toda** propriedade, `HasMaxLength`, enums `HasConversion<int>()`, índices, `HasMany().WithOne().OnDelete(Cascade)` e:
  ```csharp
  builder.Property(t => t.RowVersion).HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
  builder.Property(t => t.Numero).HasColumnName("numero").UseIdentityByDefaultColumn();
  ```
- Core em `SMSMais.Core/Tickets/`: `ITicketService.cs`, `TicketService.cs` (primary constructor `(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual)`), `Dtos/TicketDtos.cs` (todos os `sealed record` num arquivo), `Validators/TicketValidators.cs`. Sem Mapperly: projeção manual `.Select(x => new Dto(...))` direto no `IQueryable`.
- Transições por `if` direto; estado final exige texto (`ValidacaoException("respostaFinal", …)`). **Quem faz** = sempre `_usuarioAtual.UsuarioId`. Visibilidade negada → `NaoEncontradoException` (404, não vaza existência). Soft-delete = carimbar `ExcluidoEm`, e toda query `.Where(t => t.ExcluidoEm == null)`.
- Anexos: upload separado `POST /tickets/anexos` → `IMidiasService.EnviarAsync(usuarioId, fileName, contentType, bytes, "ticket", ct)` devolve `MidiaDto`; o request carrega só `TicketAnexoRef(MidiaId, NomeArquivo)`.
- Controller `SMSMais.Api/Controllers/TicketsController.cs`: `[ApiController] [Route("tickets")]`, primary constructor com os services. **`[Authorize]` é global** (`Program.cs` ~42-52); endpoints abertos usam `[AllowAnonymous]`. Gate por **action**: `[RequerPermissao(ModuloPermissao.Ticket, AcoesPermissao.Consulta | AcoesPermissao.Edicao)]`. GET devolve o DTO direto; comandos `NoContent()`; criação `CreatedAtAction(nameof(Obter), new { id }, id)`. Upload com `[RequestSizeLimit(6 * 1024 * 1024)]`.
- Enums em `Entities/Enums/TicketEnums.cs`, valores explícitos a partir de **1**. **No banco int; no JSON string** (`Program.cs:53` `JsonStringEnumConverter`) → tipos TS são union de literais.

## 2. `ModuloPermissao` (`Entities/Enums/ModuloPermissao.cs`, termina em 70)
Últimos: 61 `AlteracoesAgenda`, 62 `Agenda`, 63 `RevelarChaveSisreg`, 64 `InteligenciaAtendimento`, 65 `Confirmacoes`, 66 `EstrategiasFila`, 67 `EstatisticaCustos`, 68 `EstatisticaSisreg`, 69 `EstatisticaSer`, 70 `EstatisticaSernit`. **Ouvidoria começa em 71.**
Padrão: cada valor com `/// <summary>` longo em pt-BR dizendo (a) o que libera, (b) **por que é módulo próprio** (`AcoesPermissao` só tem `Consulta, Inclusao, Edicao, Exclusao`), (c) quais ações são usadas. Famílias ganham `// ---- Título (data) ----` antes do primeiro membro. Números descontinuados não são reaproveitados.
Registro no backend: **só nos controllers**. `RequerPermissaoAttribute(ModuloPermissao, AcoesPermissao)` e `RequerQualquerPermissaoAttribute` em `SMSMais.Api/Auth/`. `DbSeeder.GarantirPerfilAdminAsync` concede todos os módulos ao Administrador automaticamente (`Enum.GetValues`).

## 3. Escopo por unidade
`Core/Common/Unidades/EscopoUnidade.cs`: `static Task<EscopoUnidadeResultado> ResolverAsync(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual, ct)`; `record EscopoUnidadeResultado(bool VeTudo, Guid[] Unidades, Guid? Referencia)` com `Tudo`, `Nada`, `SemAcesso`, `Uma(id)`, `UnidadeUnica`. Sem usuário (job) ⇒ Tudo; sem vínculo ⇒ Nada (fail-closed). O **service aplica o `.Where`**.
`Core/Identidade/IUsuarioAtualAccessor.cs`: `Guid? UsuarioId`, `Guid? UnidadeAtivaId` (header `X-Unidade-Id`, não validado), `string? Ip`, `string? SessaoId`.

## 4. Auditoria
`Core/Auditoria/IAuditoriaService.cs`: `Task RegistrarAsync(string entidade, string entidadeId, string acao, string? valorAnterior, string? valorNovo, ct)`. Resolve quem/IP sozinho. **Não existe classe base de auditoria**; cada POCO repete os 6 campos e cada Configuration mapeia `criado_em/criado_por/atualizado_em/atualizado_por/excluido_em/excluido_por`. Sem query filter global.

## 5. Exceções (`Core/Common/Excecoes/`)
`NaoEncontradoException(string recurso, object identificador)` → 404; `ConflitoException(string codigo, string mensagem)` → 409 (código vira `type`, minúsculo com ponto: `"ouvidoria.complementacao_unica"`); `ValidacaoException(string campo, string mensagem)` ou `(IDictionary<string,string[]>)` → 400. Middleware `Api/Middleware/ExceptionHandlingMiddleware.cs`.

## 6. WhatsApp / Mensageria
- Envio: `Core/Notificacoes/WhatsApp/IWhatsAppCliente.cs`
  ```csharp
  Task<EnvioWhatsAppResultado> EnviarTextoAsync(string telefone, string texto, Guid? pacienteId = null, CancellationToken ct = default, OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico);
  Task<EnvioWhatsAppResultado> EnviarTemplateAsync(string telefone, string template, string idiomaBcp47, IReadOnlyList<string> parametros, Guid? pacienteId = null, string? conteudoLegivel = null, CancellationToken ct = default, OrigemEnvioWhatsApp origem = …);
  // + EnviarTemplateComBotoesAsync, EnviarInterativoBotoesAsync, EnviarTemplateAutenticacaoAsync, ListarTemplatesAsync
  ```
  `record EnvioWhatsAppResultado(bool Ok, string? WaMessageId, string? Erro)`; `enum OrigemEnvioWhatsApp { Automatico=1, Resposta=2, Humano=3 }`. Fora da janela de 24 h só template aprovado; dentro, texto livre.
- **ADR-0057 (destinatário correto) já é aplicado dentro de `WhatsAppCliente`** (`BloqueioAsync` → `IContatoNegadoService.BloqueadoAsync`), só para `Automatico`. Usar `IWhatsAppCliente` = regra de graça.
- Fila com retentativa (`Core/Notificacoes/Comunicacao/ComunicacaoPacienteService.cs`) é amarrada a `Solicitacao` (exame) — não reutilizar para ouvidoria.
- Recepção: `IWhatsAppWebhookService.ProcessarAsync(rawJson)`; para reagir a mensagem de entrada, implementar `IManipuladorMensagemWhatsApp { int Ordem; Task TratarAsync(ManipuladorContexto ctx, ct) }` (`Core/Notificacoes/WhatsApp/Manipuladores/`), sem `SaveChanges`, marcar `ctx.Consumido = true`; registrar com `services.AddScoped<IManipuladorMensagemWhatsApp, XHandler>()`.

## 7. `PesquisasSatisfacao` — não reaproveitável
Chave `(PatientId, EncounterId)` com índice único em `EncounterId`; não guarda resposta (redirect externo para AvanteSocial). Copiar só o **padrão de token escopado** (`Id` Guid na URL pública, `ExpiraEm`, `RespondidaEm`).

## 8. Institucional
`Core/Institucional/IInstituicaoService.ObterAsync()` (nunca lança; cache). Entidade `Instituicao` singleton com `Nome`, `NomeSecretaria`, `NomeCurto`, `Sigla`, `Uf`, `Telefone`, `EmailContato`, `EmailDpo`, `WhatsAppNumeroPublico`, `UrlPainel/UrlApp/UrlArquivos`, logos/cores. Público: `GET /publico/instituicao`.

## 9. Validators e DTOs
FluentValidation em `Core/<X>/Validators/<X>Validators.cs`, várias classes num arquivo, `AbstractValidator<Request>`; opcionais com `When(...)`. Registro automático (`AddValidatorsFromAssembly`). DTOs `sealed record` posicionais com `/// <summary>`.

## 10. DI, DbContext, migrations
- `Core/DependencyInjection.cs` → `AddCore(...)`: `services.AddScoped<Tickets.ITicketService, Tickets.TicketService>();` (uma linha por service). `BackgroundService` registram-se com `services.AddHostedService<X>()` (procurar exemplos existentes, ex. `PesquisaSatisfacaoScheduler`).
- `SMSMais.Data/SmsMaisDbContext.cs`: `public sealed class SmsMaisDbContext(DbContextOptions<SmsMaisDbContext> options)`, `const string SchemaPadrao = "smsmarica"`, DbSets expression-bodied `public DbSet<X> Xs => Set<X>();`, `OnModelCreating` aplica `ApplyConfigurationsFromAssembly` → configurations novas **não** precisam de registro.
- Migration: `cd SMSMais.server && dotnet ef migrations add <Nome> --project src/SMSMais.Data --startup-project src/SMSMais.Api`. **Snapshot é compartilhado entre sessões paralelas**: conferir o `Up()` gerado — se houver `DropTable`/`DropColumn` de coisa alheia, parar. Migration aplicada é imutável.

## 11. Regulação, Unidade, FHIR
- `Entities/Regulacao/RegulacaoSolicitacao.cs`, PK `Guid Id`, DbSet `RegulacaoSolicitacoes`.
- `Entities/Unidade.cs`: `Guid Id`, `Nome`, `string? Cnes`, `bool Externa`, `bool Ativo`. DbSet `Unidades`.
- Paciente FHIR: `Guid` **sem FK física** (hub autônomo); convenção: desnormalizar `Cpf/Cns/Nome` na abertura. Resolução por `Core/Pacientes/Fhir/IPacienteResolver` / `IPacientesService`. Profissional: `Guid? PractitionerId`.

## 12. Testes
`tests/SMSMais.Tests/` (xUnit + Testcontainers). `Infraestrutura/PostgresFixture.cs` (`[Collection(nameof(PostgresCollection))]`, `fixture.CriarDbContext()`; usa bancada se env `SMSMARICA_TESTS_CONNECTION` existir), `UsuarioAtualAccessorFake(usuarioId, unidadeAtivaId, ip, sessaoId)`. Molde: `tests/SMSMais.Tests/Common/EscopoUnidadeTests.cs`. Nomes em pt-BR com underscores; `/// <summary>` dizendo qual regressão o teste impede.

## 13. Front — feature e os 4 registros
Estrutura de `features/tickets/`: `types.ts`, `api/ticketsApi.ts` (axios puro), `api/queries.ts` (TanStack Query, `ticketsKeys`), `components/*`, `pages/*`. Alias `@/`. Gate por permissão em componente: `useTemConsulta(...)` de `@/shared/auth/authStore`.
1. Tipo: `shared/auth/authStore.ts` → union `ModuloPermissao` (última: `'EstatisticaSernit'`).
2. Menu: `app/layout/menuConfig.ts` → `SECOES: SecaoMenu[]`, item `{ rotulo, to, icone (lucide), modulo, descricao }`.
3. Rotas: `app/router/AppRouter.tsx` → `<Route element={<RotaComModulo modulo="X" rotulo="…" />}><Route path="…" element={<Page />} /></Route>`.
4. Perfis: `features/perfis/lib/acoes.ts` → lista `{ id, rotulo }` (ordem do menu) **e** dicionário de apelidos por ação `{ Consulta, Inclusao, Edicao, Exclusao }`.
A skill `sincronizar-permissoes` confere os 4 + `[RequerPermissao]`.

## 14. HTTP e bibliotecas do front
`shared/api/httpClient.ts`: axios `http` com `baseURL = VITE_API_BASE_URL` (build de produção **exige** a env), injeta `Authorization` e `X-Unidade-Id`; 401 → logout; ≥500 → toast global; **4xx tratados inline** na tela. Helpers `extrairMensagemDeErro(erro)`, `extrairCodigoReferencia(erro)`, tipo `ProblemaApi`. Upload: `shared/api/midiaApi.ts`.
Libs: React 18, Vite, TS, `@tanstack/react-query` 5, `zustand` 5, `react-hook-form` 7 + `zod` 3, `react-router-dom` 6, Tailwind + `cn` (`shared/lib/cn.ts`), `lucide-react`, `recharts`. **Tabela própria** `shared/ui/Tabela.tsx`. UI base em `shared/ui/`: `Button`, `Input`, `Select`, `Campo`, `Modal`, `ConfirmDialog`, `Tabs`, `Paginacao`, `StatusBadge`, `CounterBadge`, `Notificacoes` (`notificar(msg,'erro')`), `UploadAnexo`, `BuscaPaciente`, `AjudaCampo`, `EditorRichText/`.

## 15. PWA do cidadão e rotas públicas
`SMSMais.cidadao.pwa/src/lib/httpClient.ts` (axios próprio, `Authorization`, sem `X-Unidade-Id`, `classificarErro`), `lib/api.ts`, `store/auth.ts`. Rota pública por token: `SMSMais.arquivos.pwa` + `AnexosController` (`GET/POST /anexos/sessao/{token}` `[AllowAnonymous]`). `PublicoController` `[Route("publico")] [AllowAnonymous]` com `GET /publico/instituicao` etc.

## Não existe (verificado)
Código de Ouvidoria; classe base de auditoria; `tests/SMSMais.Tests/Tickets/`; lista de módulos no backend além do enum; biblioteca de tabela de terceiros; Mapperly em Tickets.
