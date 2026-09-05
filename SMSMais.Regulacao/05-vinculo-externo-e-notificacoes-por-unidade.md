# 05 — Vínculo com o registro externo e notificações por unidade solicitante (incremento 3)

## Objetivo

Depois que a solicitação ganha número no SISREG/SER/SERNIT, casá-la com o espelho que as varreduras e importações já mantêm (`solicitacao`, `ser_solicitacao`, `sernit_solicitacao`). Com esse vínculo, o SMSMais passa a saber a **unidade solicitante real** de cada registro externo — inclusive no SER/SERNIT, onde o Estado só vê "gestor SMS Maricá" — e as notificações podem ser filtradas por "minha unidade" (padrão) ou "todas".

## Requisitos cobertos

- **R-11 (A9)** número externo vira a chave; número interno fica como rastro; notificação de "entrou na fila de verdade"; filtro "só minha unidade" (padrão) / "todas" (configurável).
- **R-15 (A13)** só o nosso sistema sabe a unidade e o usuário de origem.

## Decisões aplicadas

Nenhuma D-n específica. Escolha própria: FK **unidirecional** `regulacao_solicitacao → espelho` (nunca o inverso), preservando ADR-0021 e ADR-0042 (o espelho continua fiel ao sistema externo).

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Espelho SISREG (marcações importadas) | sim | `smsmarica.solicitacao` (`CodigoSolicitacao`, `UnidadeSolicitanteId`, `ProcedimentoCodigoSisreg`), criada por `ImportacaoSisregService` e varredura de agenda |
| Espelho SER / SERNIT | sim | `ser_solicitacao` (`id_ser` único, `PacienteId` conciliado, `SolicitanteNome` texto), `sernit_solicitacao` |
| Gatilhos e notificações por sistema | sim | `ser_gatilho`/`sernit_gatilho`; `SerNotificacaoService` (`ResumoAsync`, `ListarAsync`, `MarcarVistaAsync`, `MarcarVistasDaSolicitacaoAsync`); `SerNotificacoesPage.tsx`; endpoints `/regulacao/ser/notificacoes/*` (`SerController.cs:176-215`) |
| Escopo por unidade | sim | `EscopoUnidade.ResolverAsync` |
| Configuração de "ver todas" | plano 09 | `regulacao_configuracao.ponta_pode_ver_todas_unidades` |

O que **não** existe: coluna ligando espelho a pedido local; notificação com unidade solicitante; qualquer gatilho do SISREG.

## Desenho

### Vínculo

Colunas em `regulacao_solicitacao` (plano 04): `solicitacao_id`, `ser_solicitacao_id`, `sernit_solicitacao_id` (FK NULL, CHECK ≤ 1 setada, unique parcial em cada uma).

**Conciliação** (`RegulacaoConciliacaoService`), chamada em três pontos já existentes, logo após gravar o espelho:
1. `SerSincronizacaoService` / `SernitSincronizacaoService` — quando insere ou atualiza `ser_solicitacao`: procurar `regulacao_solicitacao` com `sistema_destino = Ser` e `numero_externo = id_ser` sem FK; setar FK; transição `EnviadaAoSistema → EmFilaExterna` (ou o estado que a situação externa indicar); evento `SituacaoExterna`.
2. `ImportacaoSisregService` / varredura — idem com `solicitacao.CodigoSolicitacao`.
3. Botão do agente "Conciliar agora" (48) para não esperar a próxima varredura.

Mapeamento de situação externa → estado local (nunca regride além de `EmFilaExterna`): SER `EM_FILA`→EmFilaExterna, `AGENDADA`/`CHEGADA CONFIRMADA`→Agendada, `ALTA`→Concluida, `CANCELADA`→Cancelada; SISREG conforme `StatusSolicitacao` da importação. Tabela de mapeamento em código, com teste.

Chave natural: `(sistema, numero_externo)`; IDs do SERNIT são curtos e colidem com SER/SISREG — por isso o sistema faz parte da chave.

### Notificações do módulo

Derivadas de `regulacao_evento` (tipos `NumeroExterno`, `SituacaoExterna`, `PendenciaAberta`, `Devolucao`), sem tabela nova de notificação. Leitura marcada em **`regulacao_evento_visto`** (evento_id, usuario_id, visto_em) — mesmo idioma do "vista" do SER.

- `GET /regulacao/notificacoes?escopo=minha|todas&tipo=&desde=` (47 Consulta): escopo `minha` = eventos de solicitações cuja `unidade_solicitante_id` ∈ `EscopoUnidade`; `todas` só se o usuário tem 48 **ou** `ponta_pode_ver_todas_unidades = true`.
- `GET /regulacao/notificacoes/resumo` (contadores por tipo, para o badge).
- `POST /regulacao/notificacoes/{eventoId}/vista`, `POST /regulacao/solicitacoes/{id}/vistas`.

### Efeito colateral nas telas SER/SERNIT

`SerNotificacaoDto` ganha `unidadeSolicitanteId/Nome` quando `ser_solicitacao` tem `regulacao_solicitacao` apontando para ela (join reverso). `SerNotificacoesPage` e `SernitNotificacoesPage` ganham o mesmo filtro "minha unidade / todas". Registros antigos (sem pedido local) continuam sem unidade — aparecem só em "todas".

### O número interno vira rastro

Depois de `numero_externo`, listagens e detalhe mostram o número externo com o sistema ("SER 8000763"); `numero_local` fica no cabeçalho em fonte menor e no histórico.

## Tarefas

- [ ] **3.7a** Migration `VinculoRegulacaoComEspelhos` (3 FKs + CHECK + uniques parciais) e `regulacao_evento_visto`.
- [ ] **3.7b** `RegulacaoConciliacaoService` + chamadas nos três pontos + botão "Conciliar agora"; tabela de mapeamento de situações com teste.
- [ ] **3.8a** `RegulacaoNotificacaoService` (listar/resumo/vista) + endpoints + regra de escopo (`minha`/`todas`).
- [ ] **3.8b** Página Notificações da regulação (feature `regulacao`) + filtro nas páginas SER/SERNIT existentes + `unidadeSolicitante` no DTO do SER/SERNIT.
- [ ] **3.8c** Badge da sidebar (`contadorBadge()` + `useRegulacaoBadges`).
- [ ] **3.9** Notificação ao solicitante (evento `NumeroExterno`) — já coberta pelo mecanismo acima; WhatsApp ao paciente opcional (decidir).
- [ ] Testes: conciliação casa por `(sistema, numero)`; não casa SERNIT 1742 com SER 1742; regressão de estado bloqueada; `minha` não vaza outra unidade; `todas` exige 48 ou config.

## Dependências

Plano 04. As varreduras SER/SERNIT e a importação SISREG existentes.

## Riscos e pontos a confirmar

- A varredura do SER pode demorar horas para ver a solicitação nova (janela adaptativa, scheduler desligado em prod segundo a memória do projeto). O botão "Conciliar agora" faz uma leitura direta pelo `SerLeitorService` por ID quando existir; senão espera.
- `ser_solicitacao` guarda `SolicitanteNome` do Estado; **não** sobrescrever com a nossa unidade — o vínculo é a única fonte da origem real.

## Testes

Seed: `regulacao_solicitacao` `EnviadaAoSistema` com `numero_externo = 8000763`, `Ser`; inserir `ser_solicitacao` com `id_ser = 8000763` → FK setada, estado `EmFilaExterna`, evento gravado, notificação visível só para a unidade solicitante.

## Fora de escopo

Classificação de follow-up (06); gatilhos do SISREG (não existem; ficam para depois do incremento 7).

---

## Especificação para execução

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `SMSMais.Data/Entities/Regulacao/RegulacaoEventoVisto.cs` + configuração | criar (as FKs de espelho já estão em `RegulacaoSolicitacao`, plano 04) |
| `SMSMais.Data/SmsMaisDbContext.cs` | DbSet `RegulacaoEventosVistos` |
| `SMSMais.Data/Migrations/<ts>_VinculoRegulacaoComEspelhos.cs` | gerar só se as FKs/`regulacao_evento_visto` não vieram na migration do plano 04 |
| `SMSMais.Core/Regulacao/Conciliacao/IRegulacaoConciliacaoService.cs`, `RegulacaoConciliacaoService.cs`, `MapaSituacaoExterna.cs` | criar |
| `SMSMais.Core/Integracoes/SerWeb/Varredura/SerSincronizacaoService.cs`, `SernitWeb/Varredura/SernitSincronizacaoService.cs` | após persistir o lote de `ser_solicitacao`: `await conciliacao.ConciliarSerAsync(idsSerDoLote, ct)` em `try/catch` (só loga) |
| `SMSMais.Core/Integracoes/SisregWeb/Importacao/ImportacaoSisregService.cs` (e o caminho da varredura de agenda que cria `Solicitacao`) | após criar/atualizar `Solicitacao` com `CodigoSolicitacao`: `await conciliacao.ConciliarSisregAsync(codigos, ct)` |
| `SMSMais.Core/Regulacao/Notificacoes/IRegulacaoNotificacaoService.cs`, `RegulacaoNotificacaoService.cs`, `Dtos/RegulacaoNotificacaoDtos.cs` | criar |
| `SMSMais.Core/Ser/SerNotificacaoService.cs`, `SMSMais.Core/Sernit/SernitNotificacaoService.cs` | join reverso para preencher `UnidadeSolicitanteId/Nome`; parâmetro `escopo` (`minha`/`todas`) |
| `SMSMais.Core/Ser/Dtos/SerDtos.cs` (`SerNotificacaoDto`), `Sernit/Dtos/SernitNotificacaoDtos.cs` | acrescentar `Guid? UnidadeSolicitanteId, string? UnidadeSolicitanteNome` |
| `SMSMais.Api/Controllers/RegulacaoNotificacoesController.cs` | criar |
| `SMSMais.Api/Controllers/SerController.cs` (`SerNotificacaoController`), `SernitController.cs` | query `escopo` nos `GET` de notificações |
| `SMSMais.front/src/features/regulacao/pages/NotificacoesRegulacaoPage.tsx`, `components/FiltroEscopoUnidade.tsx` | criar |
| `SMSMais.front/src/features/ser/pages/SerNotificacoesPage.tsx`, `sernit/pages/SernitNotificacoesPage.tsx` | usar `FiltroEscopoUnidade` + coluna "Unidade solicitante" |
| `tests/SMSMais.Tests/Regulacao/Conciliacao/*.cs`, `Regulacao/Notificacoes/*.cs` | criar |

### B. Entidade

```csharp
public sealed class RegulacaoEventoVisto
{
    public Guid EventoId { get; set; }   // PK composta (evento_id, usuario_id); FK regulacao_evento Cascade
    public Guid UsuarioId { get; set; }
    public DateTime VistoEm { get; set; }
}
```

### C. Conciliação

```csharp
public interface IRegulacaoConciliacaoService
{
    Task<int> ConciliarSerAsync(IReadOnlyCollection<string> idsSer, CancellationToken ct);
    Task<int> ConciliarSernitAsync(IReadOnlyCollection<string> idsSernit, CancellationToken ct);
    Task<int> ConciliarSisregAsync(IReadOnlyCollection<string> codigosSolicitacao, CancellationToken ct);
    Task<bool> ConciliarAgoraAsync(Guid regulacaoSolicitacaoId, CancellationToken ct);   // botão do agente: tenta as três, e para SER/SERNIT lê direto por ID se o espelho não tem
}
```
Algoritmo (`ConciliarSerAsync`): `db.RegulacaoSolicitacoes.Where(s => s.SistemaDestino == SistemaRegulacao.Ser && s.NumeroExterno != null && idsSer.Contains(s.NumeroExterno) && s.SerSolicitacaoId == null)` → para cada, `ser = db.SerSolicitacoes.First(x => x.IdSer == s.NumeroExterno)`; seta `SerSolicitacaoId`; estado novo = `MapaSituacaoExterna.DeSer(ser.Situacao)`; se `MaquinaDeEstadosRegulacao.PodeTransitar(s.Status, novo, Sistema)` e `novo` não regride (ordem `EmFilaExterna < Agendada < Concluida`; `Cancelada` sempre pode) → transita com evento `SituacaoExterna` (`Detalhe = {sistema, situacaoExterna, idExterno}`). Idem SERNIT e SISREG (`Solicitacao.CodigoSolicitacao`, `MapaSituacaoExterna.DeSisreg(StatusSolicitacao)`).

`MapaSituacaoExterna` (estático, com teste): SER `SituacaoSer.EmFila → EmFilaExterna`, `Agendada → Agendada`, `ChegadaConfirmada → Agendada`, `Alta → Concluida`, `Cancelada → Cancelada`, `Pendente → EmFilaExterna` (pendência tratada no plano 06), demais → `null` (não transita). SISREG: `StatusSolicitacao.Solicitada/Regulada → EmFilaExterna`, `Agendada/Autorizada → Agendada`, `Realizada → Concluida`, `Cancelada → Cancelada` (conferir os nomes reais do enum `StatusSolicitacao` em `SMSMais.Data/Entities/Enums/`).

`ConciliarAgoraAsync`: se `SistemaDestino == Ser` e não há espelho → `ISerLeitorService` lê a solicitação por ID (método existente de histórico/detalhe) e grava o espelho pelo mesmo caminho da varredura (reusar o "upsert de uma solicitação" de `SerSincronizacaoService`; se não existir método público, extrair um) → depois concilia. Custo baixo (SER não tem orçamento).

### D. Notificações

```csharp
public interface IRegulacaoNotificacaoService
{
    Task<PaginaDto<RegulacaoNotificacaoDto>> ListarAsync(RegulacaoNotificacaoFiltro filtro, CancellationToken ct);
    Task<RegulacaoNotificacaoResumoDto> ResumoAsync(string escopo, CancellationToken ct);
    Task MarcarVistaAsync(Guid eventoId, CancellationToken ct);
    Task MarcarVistasDaSolicitacaoAsync(Guid solicitacaoId, CancellationToken ct);
}
public sealed record RegulacaoNotificacaoFiltro(string Escopo /* "minha" | "todas" */, TipoEventoRegulacao[]? Tipos, bool SoNaoVistas, DateTime? Desde, int Pagina = 1, int Tamanho = 25);
public sealed record RegulacaoNotificacaoDto(Guid EventoId, Guid SolicitacaoId, long NumeroLocal, string? NumeroExterno, SistemaRegulacao? Sistema, TipoEventoRegulacao Tipo, StatusRegulacao? De, StatusRegulacao? Para, string PacienteNome, string Procedimento, Guid UnidadeSolicitanteId, string UnidadeSolicitante, DateTime CriadoEm, bool Vista);
public sealed record RegulacaoNotificacaoResumoDto(int NaoVistas, IReadOnlyDictionary<TipoEventoRegulacao, int> PorTipo);
```
Tipos que viram notificação: `NumeroExterno`, `SituacaoExterna`, `PendenciaAberta`, `Devolucao`, `Recusa`, `FalhaEnvio`. Escopo: `minha` = `RegulacaoEscopo` (unidades do usuário); `todas` só se módulo 48 **ou** `config.PontaPodeVerTodasUnidades`, senão `ValidacaoException("escopo", "…")`. `Vista` = `LEFT JOIN regulacao_evento_visto ON evento_id AND usuario_id`.

Nos serviços do SER/SERNIT: `SerNotificacaoDto` ganha `UnidadeSolicitanteId/Nome` por `LEFT JOIN regulacao_solicitacao r ON r.ser_solicitacao_id = ser_solicitacao.id` + `unidade`; `ListarAsync(filtro)` ganha `Escopo`: `minha` filtra `r.UnidadeSolicitanteId ∈ escopo` (registros sem `r` ficam de fora de `minha`), `todas` sem filtro (regra de permissão igual à acima).

### E. Endpoints

`RegulacaoNotificacoesController`, `[Route("regulacao/notificacoes")]`: `GET ?escopo=minha&tipos=&soNaoVistas=&desde=&pagina=` (`Regulacao`, Consulta) · `GET resumo?escopo=` · `POST {eventoId:guid}/vista` (`Regulacao`, Edicao) · `POST solicitacao/{id:guid}/vistas`. No `RegulacaoSolicitacoesController`: `POST {id:guid}/conciliar-agora` (`RegulacaoTriagem`, Edicao) → `{ conciliada: bool }`. `SerNotificacaoController`/`SernitNotificacaoController`: `GET` aceitam `escopo` (default `minha`).

### F. Front

- `FiltroEscopoUnidade.tsx`: segmento "Minha unidade | Todas as unidades"; "Todas" só renderiza se `useTemConsulta('RegulacaoTriagem')` ou `configFluxo.pontaPodeVerTodasUnidades`; persiste a escolha em `localStorage['smsmarica.regulacao.escopoNotificacoes']`.
- `NotificacoesRegulacaoPage.tsx`: filtro de escopo + tipo + só não vistas; tabela (data, tipo com `StatusRegulacaoBadge`, paciente, procedimento, unidade solicitante, número externo); clique abre o detalhe e marca vistas da solicitação; botão "marcar como vista" por linha.
- `SerNotificacoesPage.tsx`/`SernitNotificacoesPage.tsx`: mesmo filtro; coluna "Unidade solicitante" (vazio = "não originada no SMSMais").
- `queries.ts`: `useNotificacoesRegulacao(filtro)` (`refetchInterval: 60_000`), `useResumoNotificacoesRegulacao(escopo)`, `useMarcarVista`, `useMarcarVistasSolicitacao`, `useConciliarAgora`.

### G. Testes

`MapaSituacaoExternaTests` (puro): uma linha por situação. `RegulacaoConciliacaoServiceTests` (fixture): `Concilia_por_sistema_e_numero`; `Nao_casa_sernit_1742_com_ser_1742`; `Nao_regride_de_agendada_para_em_fila`; `Cancelada_externa_cancela_local`; `Evento_situacao_externa_gravado`. `RegulacaoNotificacaoServiceTests`: `Minha_nao_vaza_outra_unidade`; `Todas_exige_48_ou_config`; `Vista_por_usuario`; `Notificacao_do_ser_traz_unidade_solicitante_quando_vinculada`.

### H. Passo a passo

1. `RegulacaoEventoVisto` + DbSet (+ migration se necessário) → build.
2. `MapaSituacaoExterna` + testes puros.
3. `IRegulacaoConciliacaoService` + chamadas nos três pontos + botão → testes.
4. `IRegulacaoNotificacaoService` + controller + ajustes SER/SERNIT → testes.
5. Front: filtro de escopo, página, ajustes nas páginas SER/SERNIT, badge (`useRegulacaoBadges` soma `naoVistas`) → `npm run build`.
6. `PROGRESSO.md` 3.7, 3.8, 3.9.

### I. Critério de pronto

Seed: solicitação `EnviadaAoSistema` com `Ser` + `8000763`; inserir `ser_solicitacao` `IdSer = 8000763`, `Situacao = EmFila` → FK setada, estado `EmFilaExterna`, notificação `SituacaoExterna` visível só para a UBS de origem em "Minha unidade"; usuário sem 48 e sem config não consegue "Todas" (400).
