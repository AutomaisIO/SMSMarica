# 09 — Configurações do módulo (incrementos 2 e 4)

## Objetivo

Uma tabela singleton tipada, no padrão da casa, com as chaves que a transcrição pede como configuráveis e as que os outros planos precisam parametrizar sem deploy. Uma aba "Solicitações" em `RegulacaoConfiguracaoPage` (módulo 51).

## Requisitos cobertos

- **R-03 (A2)** "pode escolher Externo mesmo tendo Interno".
- **R-10 (A8)** "pode escolher a unidade ou só interno/externo".
- **R-11 (A9)** "visualizar todas as unidades ou só as do usuário" nas notificações.

## Decisões aplicadas

D-6 (rótulo da fila configurável).

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Padrão singleton tipado + service + controller | sim | `SisregConfiguracao` (`SMSMais.Core/Integracoes/Sisreg/Configuracao/`), `Instituicao` (`Core/Institucional`), `LaudoConfiguracao`, `TicketConfiguracao` |
| Página de configuração da regulação com abas | sim | `features/ser/pages/RegulacaoConfiguracaoPage.tsx` (comentário: "outras fontes entram como abas") |
| Concorrência otimista | sim | `xmin` como `RowVersion` nas configurações existentes |

Não existe chave/valor genérico (decisão da casa: cada módulo tem sua entidade).

## Desenho

**`regulacao_configuracao`** (linha única, id fixo)

| Chave | Tipo | Default | Usada por |
|---|---|---|---|
| permitir_externo_com_interno | bool | false | plano 02 passo 2 |
| ponta_pode_escolher_unidade | bool | false | reservado (R-10 cita; hoje a unidade nunca é escolhida — fica documentado como não usado) |
| ponta_pode_ver_todas_unidades | bool | false | plano 05 (`escopo=todas` sem 48) |
| exigir_cpf | bool | true | plano 10 (transição para a fila) |
| rotulo_fila | text | "Pré-regulação" | telas (D-6) |
| sisreg_prazo_edicao_dias | int | 7 | plano 04/11 (`sisreg_editavel_ate`); ajustar após o spike b |
| busca_corte_distancia | numeric | 0,45 | plano 01 (calibrar) |
| busca_score_sugestao_pareamento | numeric | 0,85 | planos 01/08 |
| regras_followup_json | jsonb | `[]` | plano 06 (regex por categoria, com versão) |
| anexo_limite_mb | int | 15 | plano 02 |
| anexo_tipos_permitidos | text[] | jpeg, png, pdf | plano 02 |
| nao_sei_padrao | enum (Ressalva/Pendencia) | Ressalva | plano 03 (default para regra nova) |
| atualizado_em/por, row_version | | | |

Service `IRegulacaoConfiguracaoService` (`ObterAsync` com cache curto de 30 s, `AtualizarAsync`); endpoints `GET/PUT /regulacao/configuracao` (51 Consulta/Edição). Leitura pública de nada — tudo interno.

Aba "Solicitações" na `RegulacaoConfiguracaoPage`: seções "Fluxo" (externo com interno, ver todas, CPF, rótulo), "SISREG" (prazo de edição), "Busca" (cortes), "Anexos" (limite, tipos), "Follow-up" (editor JSON das regras com teste inline: cola um texto, mostra a categoria).

## Tarefas

- [ ] **2.1** (inc. 2) entidade + configuração + migration `ConfiguracaoDaRegulacao` + service + endpoints + aba com as chaves de fluxo e anexos.
- [ ] **4.6** (inc. 4) chaves de busca, follow-up, `nao_sei_padrao`; editor de regras de follow-up com teste inline.

## Dependências

Nenhuma.

## Riscos

`ponta_pode_escolher_unidade` existe só porque a transcrição cita; se ninguém usar até o incremento 7, remover antes de ir a produção (não deixar chave morta).

## Testes

Service com cache: mudar e reler; `PUT` sem 51 → 403; validação de JSON das regras de follow-up.

## Fora de escopo

Configurações institucionais (`instituicao`) e credenciais.

---

## Especificação para execução

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `SMSMais.Data/Entities/Regulacao/RegulacaoConfiguracao.cs` + `Enums/NaoSeiViraRegulacao.cs` (se ainda não existir pelo plano 03) + `Configurations/Regulacao/RegulacaoConfiguracaoConfiguration.cs` | criar |
| `SMSMais.Data/SmsMaisDbContext.cs` | DbSet `RegulacaoConfiguracoes` |
| `SMSMais.Data/Migrations/<ts>_ConfiguracaoDaRegulacao.cs` | gerar; **não** inserir linha na migration (regra 9 do CLAUDE.md) — o service cria a linha default no primeiro `ObterAsync` |
| `SMSMais.Core/Regulacao/Configuracao/IRegulacaoConfiguracaoService.cs`, `RegulacaoConfiguracaoService.cs`, `Dtos/RegulacaoConfiguracaoDtos.cs`, `Validators/AtualizarRegulacaoConfiguracaoValidator.cs` | criar |
| `SMSMais.Core/DependencyInjection.cs` | `AddScoped<IRegulacaoConfiguracaoService, RegulacaoConfiguracaoService>()` |
| `SMSMais.Api/Controllers/RegulacaoConfiguracaoController.cs` | criar |
| `SMSMais.front/src/features/regulacao/components/configuracao/AbaConfiguracaoSolicitacoes.tsx` + registro como aba "Solicitações" em `features/ser/pages/RegulacaoConfiguracaoPage.tsx` | criar |
| `tests/SMSMais.Tests/Regulacao/Configuracao/RegulacaoConfiguracaoServiceTests.cs` | criar |

### B. Entidade

```csharp
public sealed class RegulacaoConfiguracao
{
    public static readonly Guid IdUnico = new("00000000-0000-0000-0000-00000000c052");   // mesmo idioma de outras singletons (conferir TicketConfiguracao)
    public Guid Id { get; set; } = IdUnico;
    public bool PermitirExternoComInterno { get; set; }               // permitir_externo_com_interno, default false
    public bool PontaPodeEscolherUnidade { get; set; }                // ponta_pode_escolher_unidade, default false (reservado)
    public bool PontaPodeVerTodasUnidades { get; set; }               // ponta_pode_ver_todas_unidades, default false
    public bool ExigirCpf { get; set; } = true;                        // exigir_cpf
    public string RotuloFila { get; set; } = "Pré-regulação";         // rotulo_fila, max 60
    public int SisregPrazoEdicaoDias { get; set; } = 7;               // sisreg_prazo_edicao_dias
    public decimal BuscaCorteDistancia { get; set; } = 0.45m;         // busca_corte_distancia numeric(4,3)
    public decimal BuscaScoreSugestaoPareamento { get; set; } = 0.85m; // busca_score_sugestao_pareamento numeric(4,3)
    public string RegrasFollowupJson { get; set; } = "[]";           // regras_followup_json jsonb: RegraFollowUp[]
    public int AnexoLimiteMb { get; set; } = 15;                      // anexo_limite_mb
    public string[] AnexoTiposPermitidos { get; set; } = ["image/jpeg", "image/png", "application/pdf"];  // anexo_tipos_permitidos text[]
    public NaoSeiViraRegulacao NaoSeiPadrao { get; set; } = NaoSeiViraRegulacao.Ressalva;   // nao_sei_padrao
    public DateTime? AtualizadoEm { get; set; }  public Guid? AtualizadoPor { get; set; }
    public uint RowVersion { get; set; }                               // xmin
}
```

### C. Serviço, DTOs, endpoints

```csharp
public interface IRegulacaoConfiguracaoService
{
    Task<RegulacaoConfiguracaoDto> ObterAsync(CancellationToken ct);                  // cria a linha default se não existir; cache IMemoryCache 30 s ("regulacao-configuracao")
    Task<RegulacaoConfiguracaoFluxoDto> ObterFluxoAsync(CancellationToken ct);         // subconjunto para qualquer usuário com 47
    Task<RegulacaoConfiguracaoDto> AtualizarAsync(AtualizarRegulacaoConfiguracaoRequest req, CancellationToken ct);   // invalida o cache
    Task<string> ClassificarFollowUpTesteAsync(string texto, CancellationToken ct);   // usa ClassificadorFollowUp com as regras atuais
}
public sealed record RegulacaoConfiguracaoDto(bool PermitirExternoComInterno, bool PontaPodeEscolherUnidade, bool PontaPodeVerTodasUnidades, bool ExigirCpf, string RotuloFila, int SisregPrazoEdicaoDias, decimal BuscaCorteDistancia, decimal BuscaScoreSugestaoPareamento, JsonElement RegrasFollowup, int AnexoLimiteMb, string[] AnexoTiposPermitidos, NaoSeiViraRegulacao NaoSeiPadrao, DateTime? AtualizadoEm, string? AtualizadoPorNome, uint RowVersion);
public sealed record RegulacaoConfiguracaoFluxoDto(bool PermitirExternoComInterno, bool PontaPodeVerTodasUnidades, bool ExigirCpf, string RotuloFila, int AnexoLimiteMb, string[] AnexoTiposPermitidos);
public sealed record AtualizarRegulacaoConfiguracaoRequest(/* mesmos campos editáveis + */ uint RowVersion);
```
Validador: `RotuloFila` 1–60; `SisregPrazoEdicaoDias` 0–60; cortes entre 0 e 1; `AnexoLimiteMb` 1–50; tipos ∈ lista permitida (`image/jpeg`, `image/png`, `image/webp`, `application/pdf`); `RegrasFollowup` é array de `{categoria, regex, prioridade}` com regex compilável e categoria ∈ {FalhaContato, DocumentoCriticado, Agendamento, Outro}.

`RegulacaoConfiguracaoController`, `[Route("regulacao/configuracao")]`: `GET` (`RegulacaoConfiguracao`, Consulta) · `PUT` (`RegulacaoConfiguracao`, Edicao; `RowVersion` divergente → 409) · `GET fluxo` (`Regulacao`, Consulta) · `POST followup/testar {texto}` (`RegulacaoConfiguracao`, Consulta) → `{ categoria }`.

### D. Front

`AbaConfiguracaoSolicitacoes.tsx`: seções "Fluxo" (3 toggles + CPF + rótulo), "SISREG" (prazo), "Busca" (dois sliders numéricos com 3 casas), "Anexos" (limite + checkboxes de tipos), "Follow-up" (editor JSON com validação no cliente + caixa "testar texto" → categoria). Botão Salvar com `rowVersion`; 409 → "alguém salvou antes; recarregue". Hooks `useConfiguracaoRegulacao()`, `useAtualizarConfiguracaoRegulacao()`, `useConfiguracaoFluxo()` (key `['regulacao','configuracao']`).

### E. Testes

`RegulacaoConfiguracaoServiceTests`: `Primeiro_obter_cria_default`; `Atualizar_invalida_cache`; `RowVersion_divergente_conflita`; `Regex_invalida_e_recusada`; `Fluxo_nao_expoe_chaves_de_configuracao`.

### F. Passo a passo

1. Entidade/config/DbSet → build → migration `ConfiguracaoDaRegulacao` → build.
2. Service + validador + controller → testes.
3. Aba no front → `npm run build`.
4. Incremento 4: acrescentar as chaves de busca/follow-up/`nao_sei_padrao` à aba (as colunas já existem desde a migration).
5. `PROGRESSO.md` 2.1 e 4.6.
