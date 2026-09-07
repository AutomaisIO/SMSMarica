# 04 — Fila pré-regulação e agente regulador (incremento 3)

## Objetivo

A entidade central do módulo (`regulacao_solicitacao`), sua máquina de estados com histórico completo, as permissões (solicitante × agente regulador × configuração) e as telas: fila da unidade, fila global do agente, detalhe com linha do tempo e os botões de cada perfil. No incremento 3 o envio ao sistema é **assistido** ("Registrar envio": o agente inclui no sistema como faz hoje e digita o número); os incrementos 5 e 7 trocam esse botão por "Enviar" automático sem mudar modelo nem fila.

## Requisitos cobertos

- **R-10 (A8)** fila pré-regulação; a unidade vê as suas.
- **R-11 (A9)** agente regulador vê tudo; filtros; ajusta com histórico; "Salvar mantendo pendente"; "Enviar ao sistema"; notificação ao solicitante; número externo vira chave.
- **R-15 (A13)** rastro de operador/unidade; SER/SERNIT registram "gestor SMS Maricá" — só nós sabemos a origem.
- **R-01** menu.

## Decisões aplicadas

**D-3** módulos 47/48/51; **D-6** rótulo "Pré-regulação"; **D-8** no Interno a solicitação pode chegar à fila já com número SISREG; **D-2** modal como fallback no envio.

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Escopo por unidade com cascata canônica (fail-closed) e direção Enviada/Recebida | sim | `SMSMais.Core/Common/Unidades/EscopoUnidade.cs` (`ResolverAsync`), `SolicitacaoNoEscopo.Filtrar()/Direcao()` |
| Precedente "módulo pai libera a tela, módulo irmão libera botões" | sim | `Conversas=35` + `ConversasSupervisao=36`: `ThreadMensagens.tsx:130` (`useTemConsulta('ConversasSupervisao')`), `ConversaService.cs:876` (`TemModuloAsync`) |
| Atributo de permissão com cache | sim | `SMSMais.Api/Auth/RequerPermissaoAttribute.cs` |
| Quem fez (usuário, unidade ativa, jti, IP) | sim | `IUsuarioAtualAccessor` |
| Histórico idempotente com estados como texto | sim | `SMSMais.Data/Entities/Ser/SerEvento.cs`, `SerGatilho.cs` |
| Timeline composta para a UI | sim | `SMSMais.Core/SolicitacoesExame/SolicitacaoHistoricoService.cs` |
| Número humano sequencial | sim | `Ticket.Numero` (identity) |
| Trilha de segurança | sim | `IAuditoriaService.RegistrarAsync` → `registro_auditoria` |
| Notificação ao paciente por WhatsApp (se quiser avisar o cidadão) | sim | `IComunicacaoPacienteService.EnfileirarAsync` + nova `FinalidadeComunicacao` |
| Fila com select de situação, busca e badges | sim | `features/ser/pages/SerFilaPage.tsx` (`useResumoSer`) |
| Detalhe com trilha de eventos | sim | `SerSolicitacaoDetalhePage.tsx` |
| Componentes | sim | `shared/ui/Tabs`, `Tabela`, `Modal`, `ConfirmDialog`, `StatusBadge`, `NomePacienteComResumo` |
| Os 6 pontos de um módulo novo | sim | enum → `authStore.ts` → `menuConfig.ts` → `perfis/lib/acoes.ts` → `AppRouter.tsx` → `[RequerPermissao]` (skill `sincronizar-permissoes`, corrigir os caminhos `SMSMarica.*` → `SMSMais.*` e `Sidebar.tsx` → `menuConfig.ts`) |

O que **não** existe: entidade agnóstica de destino; estados de triagem; histórico de alteração por agente; rota gateada por módulo (`<RotaComModulo>`); badge configurável na sidebar (é um `switch` em `Sidebar.tsx`).

## Desenho

### `regulacao_solicitacao`

| Coluna | Tipo / FK | Nota |
|---|---|---|
| id | uuid PK | |
| numero_local | bigint identity | "PR-000123"; some da UI depois do número externo |
| fluxo | enum `FluxoRegulacao` (Interno=1, Externo=2, Nar=3) | |
| unidade_solicitante_id | FK `unidade` NOT NULL | unidade ativa de quem abriu |
| unidade_em_nome_de_id | FK `unidade` NULL | NAR; CHECK `ck_regulacao_nar`: `(fluxo=3) = (unidade_em_nome_de_id IS NOT NULL)` |
| criado_por_usuario_id | FK `usuario` | autor real (R-15) |
| paciente_id | uuid (fhir.patient, sem FK física) | + snapshots `paciente_cpf`, `paciente_cns`, `paciente_nome` |
| procedimento_id | FK `regulacao_procedimento` | |
| sistema_destino | enum `SistemaRegulacao` NULL | definido no envio; NAR força Sisreg |
| formulario_json | jsonb | `{canonico, sisreg, ser, sernit}` |
| formulario_versao_id | FK | snapshot do catálogo |
| status | enum `StatusRegulacao` | ver máquina |
| status_motivo | text NULL | |
| agente_responsavel_id | FK `usuario` NULL | claim |
| numero_externo, enviado_em, enviado_por_usuario_id, credencial_usada_id, operador_externo_login | | trilha "com qual senha, em nome de quem" |
| solicitacao_id / ser_solicitacao_id / sernit_solicitacao_id | FK NULL ×3 | CHECK no máximo 1 (idioma de `ck_usuario_papel_unico`); plano 05 |
| sisreg_editavel_ate | timestamptz NULL | R-15 (valor em configuração) |
| origem_legado_id | uuid NULL | rascunho SER/SERNIT migrado |
| criado/atualizado/excluido_em/por, row_version (xmin) | ADR-0006 | |

Índices: `(status, unidade_solicitante_id)`, `(status, fluxo)`, `(paciente_id)`, `(procedimento_id)`, `(agente_responsavel_id)`, unique parcial `(sistema_destino, numero_externo) WHERE numero_externo IS NOT NULL`, unique parcial em cada FK de espelho.

### `regulacao_evento` (append-only)

solicitacao_id, tipo (enum: `Criacao`, `Edicao`, `Anexo`, `RespostaRegra`, `EnvioFila`, `Assumida`, `Ajuste`, `Devolucao`, `EnvioSistema`, `FalhaEnvio`, `NumeroExterno`, `RessalvaDestino`, `PendenciaAberta`, `PendenciaRespondida`, `PendenciaSubmetida`, `PendenciaBaixada`, `SituacaoExterna`, `Cancelamento`, `Recusa`), status_anterior/novo, usuario_id NULL (= sistema), usuario_nome, papel (`Solicitante`/`Agente`/`Sistema`), unidade_ativa_id, ip, sessao_id (jti), diff_json (`{campo: {de, para}}`), detalhe_json, criado_em. Índice `(solicitacao_id, criado_em)`. `registro_auditoria` recebe só o que é de segurança (uso de credencial).

### Máquina de estados

`Rascunho(1)` → `PendenteRegulacao(2)` → `EmAnalise(3)` → `EnviandoAoSistema(5)` → `EnviadaAoSistema(6)` → `EmFilaExterna(7)` → `Agendada(8)` → `Concluida(9)`. Laterais: `Devolvida(4)`, `FalhaEnvio(12)`, `Cancelada(10)`, `Recusada(11)`.

| De → Para | Quem | Pré-condições (no service) | Evento |
|---|---|---|---|
| ∅ → Rascunho | Solicitante (47 Inclusão) | procedimento + fluxo; NAR exige `em_nome_de`; Externo só se permitido | `Criacao` |
| Rascunho → Rascunho | Solicitante / Agente | edição, anexos, respostas | `Edicao` (diff), `Anexo`, `RespostaRegra` |
| Rascunho → PendenteRegulacao | Solicitante (47 Edição) | CPF; obrigatórios; documentais obrigatórias atendidas; dedutíveis avaliadas (todas bloqueiam ⇒ recusa; parte ⇒ grava destinos). **Interno (D-8, inc. 7):** inclusão no SISREG com a credencial do solicitante antes da transição; sucesso grava `numero_externo` | `EnvioFila`, `RessalvaDestino`, (`NumeroExterno`) |
| PendenteRegulacao → EmAnalise | Agente (48 Edição) | claim atômico por RowVersion | `Assumida` |
| EmAnalise → EmAnalise | Agente | "Salvar mantendo pendente" | `Ajuste` (diff) |
| EmAnalise → Devolvida | Agente | pendência `ComplementacaoAgente` aberta (plano 06) | `Devolucao`, `PendenciaAberta` |
| Devolvida → PendenteRegulacao | Solicitante | pendência respondida | `PendenciaRespondida`, `EnvioFila` |
| EmAnalise → EnviandoAoSistema | Agente (48 Edição) | `sistema_destino` ∈ destinos Elegível/ComRessalva (NAR ⇒ Sisreg); credencial resolvida (plano 07) e não bloqueada; `UPDATE … WHERE status = 3` (trava anti-duplo-envio) | `EnvioSistema` (credencial_id, login, unidade efetiva) |
| EnviandoAoSistema → EnviadaAoSistema | Sistema | número capturado; anexos subidos (SER/SERNIT antes do Gravar) | `NumeroExterno`; notifica solicitante |
| EnviandoAoSistema → FalhaEnvio | Sistema | erro / CAPTCHA / sessão derrubada | `FalhaEnvio` (motivo) |
| FalhaEnvio → EnviandoAoSistema / EmAnalise | Agente | retentar só após conferência de duplicidade | `EnvioSistema` / `Ajuste` |
| EmAnalise → EnviadaAoSistema (**assistido**, inc. 3) | Agente (48 Edição) | "Registrar envio": digita `numero_externo` + sistema; unique parcial impede repetir | `NumeroExterno` (detalhe `assistido=true`) |
| EnviadaAoSistema → EmFilaExterna | Sistema (plano 05) | varredura/importação casou `numero_externo` ⇒ seta FK do espelho | `SituacaoExterna` |
| EmFilaExterna ↔ Agendada → Concluida / Cancelada | Sistema | mapeamento das situações externas; nunca regride além de EmFilaExterna | `SituacaoExterna` |
| Rascunho / PendenteRegulacao → Cancelada | Solicitante (47 Exclusão) | antes de EmAnalise | `Cancelamento` |
| EmAnalise / Devolvida → Recusada | Agente (48 Exclusão) | motivo obrigatório | `Recusa` |
| PendenteRegulacao (Interno já no SISREG) → EmFilaExterna | Agente | "OK" do agente: ação local; ação no SISREG conforme spike b | `Assumida`, `SituacaoExterna` |

### Permissões (endpoint × módulo × ação)

| Endpoint | Módulo · ação | Escopo |
|---|---|---|
| `GET /regulacao/solicitacoes`, `GET …/{id}`, `GET …/{id}/eventos` | 47 Consulta | `unidade_solicitante ∈ EscopoUnidade`; com 48 ⇒ tudo + filtros (fluxo, procedimento, unidade solicitante, unidade executora, sistema, status, agente) |
| `POST /regulacao/solicitacoes`, `PUT …/{id}`, anexos, respostas | 47 Inclusão / Edição | escopo |
| `POST …/{id}/enviar-fila`, `DELETE …/{id}` | 47 Edição / Exclusão | escopo |
| `POST …/{id}/assumir`, `…/ajustar`, `…/devolver`, `…/registrar-envio`, `…/enviar-sistema`, `…/ok-interno`, `…/trocar-procedimento` | 48 Edição | global |
| `POST …/{id}/recusar` | 48 Exclusão | global |
| Configuração, regras, catálogo | 51 | — |

Regra de implementação: endpoint exige o módulo base; o **service** amplia o escopo quando `TemModuloAsync(usuario, RegulacaoTriagem)` (precedente `ConversaService.cs:876`). Agente recebe 47 **e** 48 no perfil.

Enum: manter os nomes `Regulacao`, `RegulacaoTriagem`, `RegulacaoConfiguracao`; reescrever os comentários XML (o bloco cita um "ADR-0024" que não existe em `docs/adr/` — apontar para o ADR-0052). Rótulo na matriz de Perfis: "Regulação — Solicitações (unidade)", "Regulação — Agente regulador", "Regulação — Configuração".

### Front

- Menu: em `menuConfig.ts`, grupo `regulacao`, item **Solicitações** (`/app/regulacao/solicitacoes`, módulo `Regulacao`) com subitens: Nova solicitação · Minha fila · Fila da regulação (só com 48) · Pendências (plano 06) · Notificações (plano 05). Badge "pendências da minha unidade" via novo caso em `contadorBadge()` + hook `useRegulacaoBadges()`.
- `<RotaComModulo modulo="RegulacaoTriagem">` em `app/router/` (renderiza 403 amigável) — as rotas hoje não são gateadas; criar e usar nas rotas do agente e da configuração.
- `MinhaFilaPage` (unidade): tabs por status (Rascunho / Pré-regulação / Devolvidas / Enviadas / Em fila / Agendadas), busca, tabela com paciente, procedimento, fluxo, destino, agente, atualizado em.
- `FilaRegulacaoPage` (agente): mesmas tabs + filtros; ação em lote "Assumir".
- `SolicitacaoDetalhePage`: cabeçalho (número local/externo, fluxo, unidade, em nome de, autor), abas Formulário · Regras/Destinos · Anexos (caixinhas) · Linha do tempo · Pendências; botões por perfil: solicitante (Editar, Enviar, Cancelar); agente (Assumir, Salvar mantendo pendente, Devolver, Recusar, Registrar envio / Enviar ao sistema, OK interno).
- Aviso no Enviar do agente (plano 07): "vamos entrar no SISREG como LOGIN; sua sessão no navegador cairá".

## Tarefas

- [ ] **3.1** Entidades `RegulacaoEvento`, `RegulacaoSolicitacaoDestino` (se não vieram no 2.2); `StatusRegulacao`; `RegulacaoSolicitacaoService` com a máquina de estados (transições como métodos, cada uma grava evento e valida pré-condições; `ConflitoException` em transição inválida); migration `FilaPreRegulacao`.
- [ ] **3.2** Permissões: XML doc do enum; `authStore.ts` (47/48 já declarados); `menuConfig.ts`; `perfis/lib/acoes.ts` (reativar 47, rótulos); `AppRouter.tsx`; `<RotaComModulo>`; corrigir a skill `sincronizar-permissoes`.
- [ ] **3.3** `RegulacaoSolicitacoesController` com o mapa acima; `EscopoUnidade` no service; ampliação por 48.
- [ ] **3.4** `MinhaFilaPage`, `FilaRegulacaoPage` (+ `api/regulacaoApi.ts`, `useResumoRegulacao`).
- [ ] **3.5** `SolicitacaoDetalhePage` com timeline (eventos + pendências + comunicações) e ações do agente (assumir com RowVersion, ajustar com diff, devolver, recusar).
- [ ] **3.5b** **Trocar o procedimento** (`POST …/{id}/trocar-procedimento`, evento `TrocaProcedimento`) — exigido por **D-10**: o canônico é plano, o solicitante pode ter escolhido o balde ou um específico, e é o agente quem desempata (nos dois sentidos). Não é um `Ajuste` comum: trocar o procedimento **muda o formulário e muda as regras**, então o service tem de (a) regerar `formulario_versao`, (b) **preservar as respostas dos campos cuja chave canônica sobrevive** e descartar o resto registrando o que caiu no `diff_json`, (c) reavaliar a elegibilidade e regravar `regulacao_solicitacao_destino`, (d) recusar a troca se a solicitação já tiver número externo (aí é cancelar e refazer). Também vale para trocar entre destinos com oferta diferente — 38 recursos do SERNIT não existem no SER (spike c §2).
- [ ] **3.6** "Registrar envio" assistido + trava; "OK interno" (ação local) para o caso D-8.
- [ ] **3.7** (plano 05) FKs de espelho e conciliação por número.
- [ ] **3.8** (plano 05) notificações por unidade + badge.
- [ ] **3.9** Notificação ao solicitante ao registrar envio (evento `NumeroExterno` vira notificação; opcional WhatsApp ao paciente por nova `FinalidadeComunicacao` — decidir com o Bernardo).
- [ ] **3.10** Testes: escopo fail-closed; agente vê tudo; claim concorrente (um ganha, outro `ConflitoException`); cada ajuste gera evento com diff; transições inválidas recusadas; registrar envio duplicado recusado pelo unique.
- [ ] **3.11** Promover `adr/0052`.

## Dependências

Plano 02 (entidade nasce junto). Plano 07 só no incremento 5.

## Riscos e pontos a confirmar

- **O que o OK do agente faz no SISREG** para uma solicitação interna incluída pelo solicitante (D-8): só local até o spike b responder (README §9 q.6).
- `EscopoUnidade` fail-closed: agente sem vínculo de unidade mas com 48 precisa ver tudo — a ampliação por 48 deve vir **antes** da cascata.
- Diff de ajuste sobre `formulario_json`: comparar chave a chave do bloco `canonico`; blocos nativos são derivados.
- Não confundir com `smsmarica.solicitacao` (ADR-0021, marcação importada): prefixo `regulacao_` em tudo.

## Testes

Tarefa 3.10 + manual: usuário de UBS abre e envia; agente (outro usuário) assume, ajusta um campo, registra número; UBS vê o número e o histórico com o diff; usuário de outra UBS não vê a solicitação.

## Fora de escopo

Envio automático (05/07 → planos 12 e 11), pendências externas (06), credenciais (07).

---

## Especificação para execução

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `SMSMais.Data/Entities/Regulacao/RegulacaoSolicitacao.cs`, `RegulacaoEvento.cs`, `RegulacaoSolicitacaoDestino.cs` | criar |
| `SMSMais.Data/Entities/Enums/StatusRegulacao.cs`, `TipoEventoRegulacao.cs`, `PapelEventoRegulacao.cs`, `SituacaoDestinoRegulacao.cs` | criar |
| `SMSMais.Data/Configurations/Regulacao/RegulacaoSolicitacaoConfiguration.cs`, `RegulacaoEventoConfiguration.cs`, `RegulacaoSolicitacaoDestinoConfiguration.cs` | criar |
| `SMSMais.Data/SmsMaisDbContext.cs` | DbSets `RegulacaoSolicitacoes`, `RegulacaoEventos`, `RegulacaoSolicitacaoDestinos` |
| `SMSMais.Data/Migrations/<ts>_SolicitacaoDeRegulacao.cs` (plano 02) e `<ts>_FilaPreRegulacao.cs` (se `RegulacaoEvento`/`Destino` vierem depois) | gerar |
| `SMSMais.Data/Entities/Enums/ModuloPermissao.cs` | reescrever os comentários XML de 47, 48, 51 (ver §C) |
| `SMSMais.Core/Regulacao/Solicitacoes/IRegulacaoSolicitacaoService.cs`, `RegulacaoSolicitacaoService.cs`, `MaquinaDeEstadosRegulacao.cs`, `Dtos/RegulacaoSolicitacaoDtos.cs`, `Validators/CriarRegulacaoSolicitacaoValidator.cs` | criar |
| `SMSMais.Core/Regulacao/Solicitacoes/IRegulacaoEventoService.cs`, `RegulacaoEventoService.cs` | criar (grava evento + diff) |
| `SMSMais.Core/Regulacao/Comum/RegulacaoEscopo.cs` | criar (`EscopoUnidade` + ampliação por 48) |
| `SMSMais.Core/DependencyInjection.cs` | registrar |
| `SMSMais.Api/Controllers/RegulacaoSolicitacoesController.cs` | criar |
| `SMSMais.front/src/shared/auth/authStore.ts` | conferir que `'Regulacao' | 'RegulacaoTriagem' | 'RegulacaoConfiguracao'` estão na union (já estão) |
| `SMSMais.front/src/features/perfis/lib/acoes.ts` | inserir `{ id: 'Regulacao', rotulo: 'Regulação — Solicitações (unidade solicitante)' }`; trocar o rótulo de `RegulacaoTriagem` para `'Regulação — Agente regulador (vê todas as unidades, envia aos sistemas)'`; `RegulacaoConfiguracao` → `'Regulação — Configuração (regras, catálogo, credenciais e motor)'`; apagar o comentário "continua fora" |
| `SMSMais.front/src/app/layout/menuConfig.ts` | item **Solicitações** no grupo `regulacao` (ver §E) |
| `SMSMais.front/src/app/layout/Sidebar.tsx` | `contadorBadge`: `if (item.badge === 'regulacaoPendencias') return regulacaoBadges.pendenciasMinhaUnidade;` + hook |
| `SMSMais.front/src/app/router/RotaComModulo.tsx` | criar |
| `SMSMais.front/src/app/router/AppRouter.tsx` | rotas (ver §E) |
| `SMSMais.front/src/features/regulacao/pages/MinhaFilaPage.tsx`, `FilaRegulacaoPage.tsx`, `SolicitacaoDetalhePage.tsx`, `components/{TabelaSolicitacoes,LinhaDoTempo,AcoesAgente,AcoesSolicitante,ModalRegistrarEnvio,StatusRegulacaoBadge}.tsx`, `api/regulacaoApi.ts` (+), `api/queries.ts` (+), `lib/badges.ts` | criar |
| `.claude/skills/sincronizar-permissoes/SKILL.md` | corrigir caminhos `SMSMarica.*` → `SMSMais.*`, `Sidebar.tsx` → `menuConfig.ts` |
| `tests/SMSMais.Tests/Regulacao/Solicitacoes/*.cs` | criar |

### B. Entidades

```csharp
public sealed class RegulacaoSolicitacao
{
    public Guid Id { get; set; }
    public long NumeroLocal { get; set; }                       // numero_local bigint identity (ValueGeneratedOnAdd, UseIdentityAlwaysColumn)
    public FluxoRegulacao Fluxo { get; set; }
    public Guid UnidadeSolicitanteId { get; set; }  public Unidade? UnidadeSolicitante { get; set; }
    public Guid? UnidadeEmNomeDeId { get; set; }    public Unidade? UnidadeEmNomeDe { get; set; }
    public Guid CriadoPorUsuarioId { get; set; }
    public Guid PacienteId { get; set; }                         // fhir.patient, sem FK física
    public string? PacienteCpf { get; set; }  public string? PacienteCns { get; set; }  public string PacienteNome { get; set; } = string.Empty;
    public Guid ProcedimentoId { get; set; }  public RegulacaoProcedimento? Procedimento { get; set; }
    public SistemaRegulacao? SistemaDestino { get; set; }
    public string FormularioJson { get; set; } = "{}";          // jsonb {canonico, sisreg, ser, sernit}
    public Guid? FormularioVersaoId { get; set; }
    public StatusRegulacao Status { get; set; } = StatusRegulacao.Rascunho;
    public string? StatusMotivo { get; set; }                    // max 2000
    public Guid? AgenteResponsavelId { get; set; }
    public string? NumeroExterno { get; set; }                   // max 40
    public DateTime? EnviadoEm { get; set; }  public Guid? EnviadoPorUsuarioId { get; set; }
    public Guid? CredencialUsadaId { get; set; }  public string? OperadorExternoLogin { get; set; }  // max 120
    public bool EnvioAssistido { get; set; }                     // envio_assistido: número digitado pelo agente (inc. 3)
    public Guid? SolicitacaoId { get; set; }  public Guid? SerSolicitacaoId { get; set; }  public Guid? SernitSolicitacaoId { get; set; }
    public DateTime? SisregEditavelAte { get; set; }
    public Guid? OrigemLegadoId { get; set; }
    public string? Observacoes { get; set; }                     // max 4000
    public DateTime CriadoEm { get; set; }  public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }  public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }  public Guid? ExcluidoPor { get; set; }
    public uint RowVersion { get; set; }                         // xmin: builder.Property(x => x.RowVersion).IsRowVersion() com HasColumnName("xmin") — copiar de DocumentoExameConfiguration
    public ICollection<RegulacaoEvento> Eventos { get; set; } = [];
    public ICollection<RegulacaoSolicitacaoDestino> Destinos { get; set; } = [];
    public ICollection<RegulacaoSolicitacaoExigencia> Exigencias { get; set; } = [];
}

public sealed class RegulacaoEvento
{
    public Guid Id { get; set; }
    public Guid SolicitacaoId { get; set; }
    public TipoEventoRegulacao Tipo { get; set; }
    public StatusRegulacao? StatusAnterior { get; set; }  public StatusRegulacao? StatusNovo { get; set; }
    public Guid? UsuarioId { get; set; }  public string? UsuarioNome { get; set; }   // max 200; null = sistema
    public PapelEventoRegulacao Papel { get; set; }
    public Guid? UnidadeAtivaId { get; set; }  public string? Ip { get; set; }  public string? SessaoId { get; set; }  // max 64 / 64
    public string? DiffJson { get; set; }      // jsonb {campo: {de, para}}
    public string? DetalheJson { get; set; }   // jsonb livre (motivo, número, credencial_id, login…)
    public DateTime CriadoEm { get; set; }
}
// índice ix_regulacao_evento_solicitacao (solicitacao_id, criado_em)

public sealed class RegulacaoSolicitacaoDestino
{
    public Guid Id { get; set; }
    public Guid SolicitacaoId { get; set; }
    public SistemaRegulacao Sistema { get; set; }
    public SituacaoDestinoRegulacao Situacao { get; set; }   // Elegivel=1, Bloqueado=2, ComRessalva=3
    public string? Motivo { get; set; }                       // max 2000
    public DateTime AvaliadoEm { get; set; }
}
// unique ux_regulacao_destino (solicitacao_id, sistema)
```

Configuração de `regulacao_solicitacao`: check constraints via `builder.ToTable("regulacao_solicitacao", t => { t.HasCheckConstraint("ck_regulacao_solicitacao_nar", "(fluxo = 3) = (unidade_em_nome_de_id IS NOT NULL)"); t.HasCheckConstraint("ck_regulacao_solicitacao_um_espelho", "((solicitacao_id IS NOT NULL)::int + (ser_solicitacao_id IS NOT NULL)::int + (sernit_solicitacao_id IS NOT NULL)::int) <= 1"); })`; FKs `unidade_solicitante_id`/`unidade_em_nome_de_id` → `unidade` (Restrict); `solicitacao_id` → `solicitacao`, `ser_solicitacao_id` → `ser_solicitacao`, `sernit_solicitacao_id` → `sernit_solicitacao` (SetNull); índices `ix_regulacao_solicitacao_status_unidade (status, unidade_solicitante_id)`, `ix_…_status_fluxo`, `ix_…_paciente`, `ix_…_procedimento`, `ix_…_agente`; uniques parciais `ux_regulacao_solicitacao_externo (sistema_destino, numero_externo) WHERE numero_externo IS NOT NULL`, `ux_…_solicitacao_id WHERE solicitacao_id IS NOT NULL` (idem SER/SERNIT); `HasQueryFilter(x => x.ExcluidoEm == null)`.

Enums: `StatusRegulacao { Rascunho = 1, PendenteRegulacao = 2, EmAnalise = 3, Devolvida = 4, EnviandoAoSistema = 5, EnviadaAoSistema = 6, EmFilaExterna = 7, Agendada = 8, Concluida = 9, Cancelada = 10, Recusada = 11, FalhaEnvio = 12 }` · `TipoEventoRegulacao { Criacao = 1, Edicao, Anexo, RespostaRegra, EnvioFila, Assumida, Ajuste, Devolucao, EnvioSistema, FalhaEnvio, NumeroExterno, RessalvaDestino, PendenciaAberta, PendenciaRespondida, PendenciaSubmetida, PendenciaBaixada, SituacaoExterna, Cancelamento, Recusa, OkInterno }` · `PapelEventoRegulacao { Solicitante = 1, Agente = 2, Sistema = 3 }` · `SituacaoDestinoRegulacao { Elegivel = 1, Bloqueado = 2, ComRessalva = 3 }`.

### C. Enum de permissões — comentários a reescrever

```csharp
/// <summary>Regulação → Solicitações, lado da UNIDADE SOLICITANTE: abrir, editar, anexar, responder
/// regras e pendências, enviar para a fila pré-regulação, cancelar; ver a própria fila e notificações.
/// Escopado por unidade (EscopoUnidade). ADR-0052.</summary>
Regulacao = 47,
/// <summary>Agente regulador: vê a fila de TODAS as unidades, assume, ajusta (com histórico), devolve,
/// recusa, registra/envia ao sistema de regulação e aprova pendências vindas da ponta. Recebe 47 também. ADR-0052.</summary>
RegulacaoTriagem = 48,
/// <summary>Reservado (não implementado).</summary>
RegulacaoMedica = 49,
/// <summary>Reservado (não implementado).</summary>
RegulacaoAgendamento = 50,
/// <summary>Configuração da regulação: regras de elegibilidade, catálogo canônico, credenciais/motores
/// do SER/SERNIT, configurações do módulo. ADR-0052.</summary>
RegulacaoConfiguracao = 51,
```

### D. Serviço, máquina de estados e DTOs

```csharp
public interface IRegulacaoSolicitacaoService
{
    Task<PaginaDto<RegulacaoSolicitacaoListaDto>> ListarAsync(RegulacaoSolicitacaoFiltro filtro, CancellationToken ct);  // aplica escopo; 48 amplia
    Task<RegulacaoResumoFilaDto> ResumoAsync(CancellationToken ct);            // contagens por status para as abas/badge
    Task<RegulacaoSolicitacaoDetalheDto> ObterAsync(Guid id, CancellationToken ct);
    Task<RegulacaoSolicitacaoDetalheDto> CriarAsync(CriarRegulacaoSolicitacaoRequest req, CancellationToken ct);
    Task<RegulacaoSolicitacaoDetalheDto> AtualizarAsync(Guid id, AtualizarRegulacaoSolicitacaoRequest req, CancellationToken ct); // Rascunho/Devolvida (solicitante) ou EmAnalise (agente → evento Ajuste com diff)
    Task<RegulacaoSolicitacaoDetalheDto> EnviarParaFilaAsync(Guid id, CancellationToken ct);   // valida pendências; Interno chama IRegulacaoInclusaoSisreg (plano 11) quando existir
    Task CancelarAsync(Guid id, string motivo, CancellationToken ct);
    // agente (48)
    Task<RegulacaoSolicitacaoDetalheDto> AssumirAsync(Guid id, CancellationToken ct);
    Task<RegulacaoSolicitacaoDetalheDto> DevolverAsync(Guid id, string motivo, CancellationToken ct);
    Task RecusarAsync(Guid id, string motivo, CancellationToken ct);
    Task<RegulacaoSolicitacaoDetalheDto> RegistrarEnvioAsync(Guid id, RegistrarEnvioRequest req, CancellationToken ct);  // assistido
    Task<RegulacaoSolicitacaoDetalheDto> ConfirmarOkInternoAsync(Guid id, CancellationToken ct);                        // D-8
    Task<RegulacaoSolicitacaoDetalheDto> EnviarAoSistemaAsync(Guid id, SistemaRegulacao sistema, CancellationToken ct); // inc. 5/7: SER/SERNIT → IRegulacaoEnvioSistema (plano 12); NAR → IRegulacaoInclusaoSisreg (plano 11)
    Task<IReadOnlyList<RegulacaoEventoDto>> EventosAsync(Guid id, CancellationToken ct);
    Task<RegulacaoSolicitacao> ObterNoEscopoAsync(Guid id, CancellationToken ct);   // usado pelos outros serviços do módulo
}

public sealed record RegulacaoSolicitacaoFiltro(StatusRegulacao[]? Status, FluxoRegulacao? Fluxo, SistemaRegulacao? Sistema, Guid? ProcedimentoId, Guid? UnidadeSolicitanteId, Guid? AgenteId, string? Busca, bool SoMinhas, int Pagina = 1, int Tamanho = 25);
public sealed record CriarRegulacaoSolicitacaoRequest(FluxoRegulacao Fluxo, Guid? UnidadeEmNomeDeId, Guid ProcedimentoId, Guid PacienteId, Guid? FormularioVersaoId, JsonElement? FormularioCanonico, string? Observacoes);
public sealed record AtualizarRegulacaoSolicitacaoRequest(Guid? FormularioVersaoId, JsonElement FormularioCanonico, string? Observacoes, SistemaRegulacao? SistemaDestino);
public sealed record RegistrarEnvioRequest(SistemaRegulacao Sistema, string NumeroExterno, DateTime? EnviadoEm);
public sealed record RegulacaoSolicitacaoListaDto(Guid Id, long NumeroLocal, string? NumeroExterno, SistemaRegulacao? SistemaDestino, FluxoRegulacao Fluxo, StatusRegulacao Status, string PacienteNome, string? PacienteCpf, string Procedimento, Guid UnidadeSolicitanteId, string UnidadeSolicitante, string? UnidadeEmNomeDe, string? AgenteNome, int PendenciasAbertas, DateTime CriadoEm, DateTime? AtualizadoEm);
public sealed record RegulacaoSolicitacaoDetalheDto(/* lista + */ Guid PacienteId, string? PacienteCns, Guid ProcedimentoId, Guid? FormularioVersaoId, JsonElement Formulario, string? StatusMotivo, Guid? AgenteResponsavelId, DateTime? EnviadoEm, string? OperadorExternoLogin, bool EnvioAssistido, DateTime? SisregEditavelAte, IReadOnlyList<RegulacaoDestinoDto> Destinos, string? Observacoes, uint RowVersion);
public sealed record RegulacaoDestinoDto(SistemaRegulacao Sistema, SituacaoDestinoRegulacao Situacao, string? Motivo);
public sealed record RegulacaoEventoDto(Guid Id, TipoEventoRegulacao Tipo, StatusRegulacao? De, StatusRegulacao? Para, string? UsuarioNome, PapelEventoRegulacao Papel, JsonElement? Diff, JsonElement? Detalhe, DateTime CriadoEm);
public sealed record RegulacaoResumoFilaDto(IReadOnlyDictionary<StatusRegulacao, int> PorStatus, int PendenciasAbertas);
```

`MaquinaDeEstadosRegulacao` (estática, pura): `static bool PodeTransitar(StatusRegulacao de, StatusRegulacao para, PapelEventoRegulacao papel)` com a tabela do §Máquina; `static TipoEventoRegulacao? EventoDe(StatusRegulacao de, StatusRegulacao para, PapelEventoRegulacao papel)` — **o papel entra também aqui** (implementado em 06/09): o mesmo par de estados com ator diferente é outra transição, e `PendenteRegulacao → EmAnalise` existe para o agente e só para ele. Mais `EhTerminal(status)` e `DestinosDe(de, papel)`, que a tela usa para decidir botões. O service chama `Transitar(sol, para, papel, detalhe)`: valida, grava `StatusAnterior/Novo`, chama `IRegulacaoEventoService.RegistrarAsync`. Transição inválida → `ConflitoException` com código `regulacao.transicao_invalida` (usar o construtor existente em `Common/Excecoes/ConflitoException.cs`).

Regras de `EnviarParaFilaAsync`: `config.ExigirCpf && string.IsNullOrEmpty(sol.PacienteCpf)` → pendência "CPF do paciente"; `formulario.ObrigatoriosFaltando(...)` → pendências por rótulo; exigências `Obrigatoria && Situacao == Pendente` → pendência por título; regras (plano 03) `PerguntasPendentes`/bloqueios; qualquer pendência → `ValidacaoException(new Dictionary<string,string[]>{["pendencias"] = lista})` (o front lê `errors.pendencias`). Sem pendência: se `Fluxo == Interno` e `IRegulacaoInclusaoSisreg` estiver registrado (plano 11) → inclui; senão segue para `PendenteRegulacao` (`EnvioAssistido` decidirá depois). Grava destinos (`RessalvaDestino` quando houver bloqueio parcial).

`AssumirAsync`: `UPDATE regulacao_solicitacao SET status=3, agente_responsavel_id=@u, atualizado_em=now() WHERE id=@id AND status=2` via `ExecuteUpdateAsync` (retorno 0 → `ConflitoException("regulacao.ja_assumida")`); depois recarregar (**não** `ChangeTracker.Clear()` — ver memória "ExecuteUpdateAsync mente pro change tracker"). `AtualizarAsync` pelo agente: diff = comparação chave a chave de `Formulario.canonico` (antes/depois) → `DiffJson`.

`RegistrarEnvioAsync`: `EmAnalise → EnviadaAoSistema`, `EnvioAssistido = true`, `NumeroExterno`, `SistemaDestino`, `EnviadoPorUsuarioId`, `EnviadoEm`; unique parcial garante não repetir; evento `NumeroExterno` com `Detalhe = {assistido: true}`; dispara notificação (evento já é a notificação, plano 05).

`RegulacaoEscopo.ResolverAsync(db, usuarioAtual, identidade)`: se `await identidade.TemModuloAsync(usuarioId, ModuloPermissao.RegulacaoTriagem)` → `EscopoUnidadeResultado.Tudo`; senão `EscopoUnidade.ResolverAsync(...)` (precedente `ConversaService.cs:876`). Listagem: `VeTudo ? q : q.Where(s => escopo.Unidades.Contains(s.UnidadeSolicitanteId))`; `SemAcesso` → lista vazia. **NAR**: a validação de `UnidadeEmNomeDeId` é `db.Unidades.AnyAsync(u => u.Id == id && u.Ativo)`, fora do escopo.

### E. Endpoints — `RegulacaoSolicitacoesController`, `[Route("regulacao/solicitacoes")]`

| Verbo | Rota | `[RequerPermissao]` | Entrada | Saída |
|---|---|---|---|---|
| GET | `` | `Regulacao`, `Consulta` | `RegulacaoSolicitacaoFiltro` (query) | `PaginaDto<RegulacaoSolicitacaoListaDto>` |
| GET | `resumo` | `Regulacao`, `Consulta` | | `RegulacaoResumoFilaDto` |
| GET | `{id:guid}` · `{id:guid}/eventos` | `Regulacao`, `Consulta` | | detalhe · eventos |
| POST | `` | `Regulacao`, `Inclusao` | `CriarRegulacaoSolicitacaoRequest` | detalhe (201) |
| PUT | `{id:guid}` | `Regulacao`, `Edicao` | `AtualizarRegulacaoSolicitacaoRequest` | detalhe |
| POST | `{id:guid}/enviar-fila` | `Regulacao`, `Edicao` | | detalhe |
| DELETE | `{id:guid}` body `{motivo}` | `Regulacao`, `Exclusao` | | 204 |
| POST | `{id:guid}/assumir` · `devolver {motivo}` · `registrar-envio` · `ok-interno` · `enviar-sistema {sistema}` | `RegulacaoTriagem`, `Edicao` | | detalhe |
| POST | `{id:guid}/recusar` body `{motivo}` | `RegulacaoTriagem`, `Exclusao` | | 204 |

### F. Front

Menu (`menuConfig.ts`, grupo `regulacao`, **primeiro** item):
```ts
{
  rotulo: 'Solicitações', to: '/app/regulacao/solicitacoes', icone: ClipboardList, modulo: 'Regulacao',
  descricao: 'Abertura, fila pré-regulação e acompanhamento das solicitações de regulação (SISREG, SER, SERNIT).',
  subItens: [
    { rotulo: 'Nova solicitação', to: '/app/regulacao/solicitacoes/nova', icone: FilePlus2, modulo: 'Regulacao' },
    { rotulo: 'Minha fila', to: '/app/regulacao/solicitacoes', icone: ClipboardList, modulo: 'Regulacao', end: true, badge: 'regulacaoPendencias' },
    { rotulo: 'Fila da regulação', to: '/app/regulacao/solicitacoes/regulacao', icone: ClipboardCheck, modulo: 'RegulacaoTriagem' },
    { rotulo: 'Pendências', to: '/app/regulacao/solicitacoes/pendencias', icone: BellRing, modulo: 'Regulacao' },      // plano 06
    { rotulo: 'Notificações', to: '/app/regulacao/solicitacoes/notificacoes', icone: BellRing, modulo: 'Regulacao' },  // plano 05
  ],
}
```
(`ItemMenu.badge` ganha o literal `'regulacaoPendencias'` no tipo.)

`RotaComModulo.tsx`: `({ modulo, children }) => useTemConsulta(modulo) ? children : <SemPermissaoPage />` (página simples com o texto "Você não tem acesso a esta tela" e link para o hub). Rotas em `AppRouter.tsx`:
```tsx
<Route path="regulacao/solicitacoes" element={<MinhaFilaPage />} />
<Route path="regulacao/solicitacoes/nova" element={<NovaSolicitacaoPage />} />
<Route path="regulacao/solicitacoes/regulacao" element={<RotaComModulo modulo="RegulacaoTriagem"><FilaRegulacaoPage /></RotaComModulo>} />
<Route path="regulacao/solicitacoes/:id" element={<SolicitacaoDetalhePage />} />
```

Páginas: `MinhaFilaPage` (`SoMinhas = true`; `Tabs` por status: Rascunhos · Pré-regulação · Devolvidas · Enviadas · Em fila · Agendadas · Canceladas/Recusadas, com badge de `ResumoAsync`; busca; `TabelaSolicitacoes`); `FilaRegulacaoPage` (mesmas abas + filtros fluxo/sistema/procedimento/unidade/agente; ação em lote "Assumir selecionadas"); `SolicitacaoDetalhePage` (cabeçalho: número externo grande ou `PR-{numeroLocal}`, fluxo, unidade, em nome de, autor, agente; `Tabs`: Formulário · Regras e destinos · Anexos · Linha do tempo · Pendências; `AcoesSolicitante` (Editar, Enviar, Cancelar) ou `AcoesAgente` (Assumir, Salvar mantendo pendente, Devolver, Recusar, Registrar envio → `ModalRegistrarEnvio`, OK interno, Enviar ao sistema) decidido por `useTemConsulta('RegulacaoTriagem')`).

`api/queries.ts`: `useSolicitacoes(filtro)` key `['regulacao','solicitacoes', filtro]`; `useResumoRegulacao()`; `useSolicitacao(id)`; `useEventosSolicitacao(id)`; mutations `useCriarSolicitacao`, `useAtualizarSolicitacao`, `useEnviarParaFila`, `useCancelarSolicitacao`, `useAssumir`, `useDevolver`, `useRecusar`, `useRegistrarEnvio`, `useOkInterno`, `useEnviarAoSistema` — todas invalidam `['regulacao']`. `lib/badges.ts`: `useRegulacaoBadges()` = `useResumoRegulacao` → `{ pendenciasMinhaUnidade: resumo.pendenciasAbertas }`, com `enabled: useTemConsulta('Regulacao')`, `refetchInterval: 60_000`.

### G. Testes (`tests/SMSMais.Tests/Regulacao/Solicitacoes/`)

- `MaquinaDeEstadosRegulacaoTests` (puro): `Solicitante_nao_assume`; `Agente_nao_envia_fila`; `Rascunho_para_em_fila_externa_e_invalido`; `Falha_envio_volta_para_enviando_ou_em_analise`.
- `RegulacaoSolicitacaoServiceTests` (fixture): `Usuario_sem_vinculo_nao_ve_nada`; `Usuario_de_outra_unidade_nao_ve_a_solicitacao`; `Agente_ve_todas`; `Nar_exige_unidade_em_nome_de`; `Nar_aceita_unidade_fora_dos_vinculos`; `Enviar_fila_sem_cpf_devolve_pendencias`; `Assumir_concorrente_um_ganha_outro_conflito`; `Ajuste_do_agente_grava_diff`; `Registrar_envio_duplicado_e_recusado`; `Cancelar_depois_de_em_analise_e_recusado`.
- `RegulacaoEscopoTests`: `Modulo_48_amplia_para_tudo_mesmo_sem_vinculo`.

### H. Passo a passo

1. Enums + entidades + configurações + DbSets (com as do plano 02) → build → migration `SolicitacaoDeRegulacao` → build.
2. Comentários do enum 47/48/51 → `acoes.ts` → `authStore.ts` (conferir) → `npm run build`.
3. `MaquinaDeEstadosRegulacao` + testes puros.
4. `IRegulacaoEventoService`, `RegulacaoEscopo`, `IRegulacaoSolicitacaoService` + DI + testes com fixture.
5. Controller → build → `/docs`.
6. `RotaComModulo`, menu, rotas, badge → páginas → `npm run build`.
7. Skill `sincronizar-permissoes` corrigida.
8. `PROGRESSO.md` 3.1–3.6, 3.9, 3.10.

### I. Critério de pronto

Usuário A (UBS X) abre e envia; usuário B (UBS Y) não a vê; agente C assume, ajusta um campo (evento com diff visível na linha do tempo), registra número "SER 123"; A vê o número e recebe a notificação; deep-link de A para `/regulacao/solicitacoes/regulacao` mostra a página de sem permissão.
