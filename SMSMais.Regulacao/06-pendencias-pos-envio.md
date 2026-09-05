# 06 — Pendências pós-envio (incremento 6)

## Objetivo

Transformar o que volta dos sistemas de regulação (follow-up de "sem contato", crítica a documento, pedido de complementação) em **pendências** que a ponta e a regulação veem ao mesmo tempo. A ponta responde (novo telefone, texto de follow-up, novo arquivo); quem não é agente regulador não escreve no sistema externo — a resposta vira pendência para o agente aprovar e submeter com a credencial dele. No SISREG, onde não há recurso equivalente, o OK do agente só baixa a pendência local.

## Requisitos cobertos

- **R-12 (A10)** follow-up "sem contato" → notificação "falha de contato"; aba "Pendência de contato"; alterar número e confirmar.
- **R-13 (A11)** não-agente responde → pendência para o agente → agente submete com a credencial dele.
- **R-14 (A12)** SISREG só baixa local; documento criticado → novo arquivo na mesma caixinha com histórico; pendência geral (ponta + regulação).

## Decisões aplicadas

D-1/D-2 (a submissão usa a credencial persistida do agente; modal como fallback).

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Follow-ups lidos do SER/SERNIT | sim | `ser_evento` (`evento`, `observacao`, `usuario`, `lotacao_evento`), `ser_gatilho` tipo `NovoFollowUp`; varredura em `SMSMais.Core/Integracoes/SerWeb/Varredura/` |
| Escrita de follow-up e telefones assinada pelo operador, confirmada por releitura | sim | `SerEscritaService.RegistrarFollowUpAsync / LerContatosAsync / AlterarContatosAsync`; `SernitEscritaService` |
| Modais genéricos | sim | `shared/regulacao/ModalFollowUp.tsx`, `ModalContato.tsx` |
| Painéis de follow-up e contatos | sim | `features/ser/components/PainelFollowUpSer.tsx`, `PainelContatosSer.tsx` |
| Consumidor de gatilhos | **não** | ADR-0042 §7: a fila nasce sem consumidor de propósito; só a tela lê |
| Anexos com versão | plano 03 | `regulacao_exigencia_arquivo` (versao, substitui, situação `Criticado`) |
| Amostra para classificar | spike d | `revisoes/spike-d-followup.md` |

## Desenho

### `regulacao_pendencia`

| Coluna | Nota |
|---|---|
| id, solicitacao_id | |
| tipo | `FalhaContato=1`, `AlteracaoTelefone=2`, `DocumentoCriticado=3`, `FollowUpPonta=4`, `ComplementacaoAgente=5` |
| origem | `SistemaExterno=1`, `Ponta=2`, `Agente=3` |
| sistema_origem | enum `SistemaRegulacao` NULL |
| ser_gatilho_id / sernit_gatilho_id | FK NULL — de qual gatilho nasceu |
| chave_evento_externo | text NULL — idempotência; unique parcial `(solicitacao_id, tipo, chave_evento_externo)` |
| exigencia_id | FK NULL — caixinha criticada |
| estado | `Aberta=1`, `AguardandoAgente=2`, `Submetida=3`, `Baixada=4`, `Rejeitada=5` |
| descricao | texto do sistema externo ou do agente |
| resposta_texto, resposta_json | texto do follow-up da ponta; `{telefones: {...}}` |
| respondida_por/em, aprovada_por/em, submetida_em, baixada_por/em, motivo_baixa | |
| criado_em | |

Índice `(estado, solicitacao_id)`; `(tipo, estado)` para as abas.

### Classificação de follow-up (regex versionada, sem IA)

**Taxonomia fechada pelo spike d (05/09/2026) — são NOVE categorias, não quatro.** Medida sobre o corpus inteiro (18.904 follow-ups do SER, 312 do SERNIT); detalhe em [`revisoes/spike-d-followup.md`](revisoes/spike-d-followup.md):

| Categoria | SER | SERNIT | Vira pendência? |
|---|---:|---:|---|
| `FalhaContato` | 39,8% | 0% | **sim → pendência de contato** |
| `SemVaga` | 21,8% | 1,9% | não (é estado) |
| `Outro` | 13,1% | 14,1% | sim → `ComplementacaoAgente` |
| `ContatoRealizado` | 12,7% | 1,0% | não — **mas ver o sub-desfecho abaixo** |
| `ReclassificacaoRisco` | 9,2% | **80,8%** | não |
| `OrientacaoAoPaciente` | 1,9% | 0% | **não** |
| `Agendamento` | 0,7% | 1,3% | não → `SituacaoExterna` |
| `CancelamentoOuReagendamento` | 0,5% | 0,6% | não |
| `SolicitacaoAoSolicitante` | 0,3% | 0,3% | **sim → pendência de documento/informação** |

Três consequências que este plano não previa:

1. **`OrientacaoAoPaciente` ≠ `SolicitacaoAoSolicitante`.** *"Favor informar ao paciente … os documentos de identificação pessoal"* (364 casos) manda a unidade **avisar o paciente o que levar** — não há nada a corrigir. *"Favor anexar laudo de biópsia"* (66 casos) é pedido **à unidade**. Tratá-las como uma só faria esta fila nascer com **5,5× mais itens do que o real**, quase todos falsos.
2. **`ContatoRealizado` tem sub-desfecho, e `NaoAguarda` (347 casos: não quer mais, já fez, particular, faleceu) precisa de caminho próprio** — avisar a unidade de que a solicitação provavelmente deve ser cancelada. É vaga que volta para a fila.
3. **O SERNIT não gera uma única falha de contato** (0 em 312) e é 81% reclassificação de risco. Não esperar simetria entre os sistemas.

Regras em `regulacao_configuracao.regras_followup_json` (plano 09), com versão — semente pronta em [`revisoes/spike-d-regras-followup.json`](revisoes/spike-d-regras-followup.json), **8 regras ordenadas em que a ordem importa** (a primeira que casa vence). O classificador é função pura testada contra o fixture do spike (60 casos, precisão ≥ 80% em todas as classes).

### Fluxos

**1. Externo → pendência geral.** Consumidor `RegulacaoPendenciaConsumidor` (background, fila cap-1 como os motores existentes) lê `ser_gatilho`/`sernit_gatilho` com `tipo = NovoFollowUp` e `ProcessadoEm IS NULL` cujas solicitações tenham `regulacao_solicitacao` vinculada (plano 05): classifica o texto de `ser_evento.observacao`, cria a pendência (`origem = SistemaExterno`, `estado = Aberta`), grava evento `PendenciaAberta`, carimba `ProcessadoEm/ProcessadoPor = "regulacao"` no gatilho. Gatilho sem pedido local → não toca (segue só na tela do SER).

**2. Ponta responde.** Na aba Pendências do detalhe (47 Edição): `FalhaContato` → "Informar novo telefone" (`ModalContato`) e/ou "Registrar follow-up" (`ModalFollowUp`); `DocumentoCriticado` → "Anexar novo arquivo" na **mesma caixinha** (nova versão; a anterior fica `Criticado`). Ao responder: `estado = AguardandoAgente`, evento `PendenciaRespondida`. **Nada é escrito no sistema externo.** A ponta também pode abrir pendência por conta própria (`FollowUpPonta`, `AlteracaoTelefone`) sem esperar o sistema.

**3. Agente aprova e submete** (48 Edição): vê a fila de pendências `AguardandoAgente` (todas as unidades); abre, confere, e "Aprovar e submeter":
- SER/SERNIT: `SerEscritaService.AlterarContatosAsync` / `RegistrarFollowUpAsync` com a **sessão do agente** (plano 07; `SessaoOperadorIntegracaoStore.Exigir(Ser)`); documento criticado → upload da nova versão pela rotina de anexo do plano 12 (se a solicitação já está no SER, o anexo vai pela aba Editar — confirmar no spike a); sucesso confirmado por releitura → `estado = Submetida`, `enviado_ao_sistema_em` no arquivo, evento `PendenciaSubmetida`.
- SISREG: sem recurso → `estado = Baixada` com `motivo_baixa = "SISREG não tem este recurso; registrado localmente"`, evento `PendenciaBaixada`. Se o spike b mostrar follow-up no SISREG, trocar por escrita.
- "Rejeitar" com motivo → `Rejeitada`; a ponta vê e pode responder de novo (nova pendência).

**4. Visibilidade.** Toda pendência aparece para a unidade solicitante (aba "Pendências" da fila + badge) e para o agente (fila global). Aba dedicada **"Pendência de contato"** na fila da unidade lista `FalhaContato` abertas com o telefone atual e o botão de resposta.

### Telas

- Fila da unidade: aba "Pendência de contato" e aba "Pendências" (todas); linha com tipo, origem, texto, idade.
- Fila do agente: aba "Pendências aguardando" com filtro por tipo/sistema/unidade; ação Aprovar e submeter / Rejeitar.
- Detalhe: aba Pendências com a conversa (descrição externa → resposta da ponta → decisão do agente → confirmação).

## Tarefas

- [ ] **6.1** Entidade `RegulacaoPendencia` + enums + configuração + migration `PendenciasDeRegulacao`.
- [ ] **6.2** `ClassificadorFollowUp` (função pura sobre `regras_followup_json`) + teste contra o fixture do spike d.
  O fixture já existe: `tests/SMSMais.Tests/Regulacao/Pendencias/Fixtures/followups-rotulados.csv` (60 casos, sem PII). **`SMSMais.Tests.csproj` não tem regra de cópia para a saída e nenhum teste hoje lê arquivo** — acrescentar `<None Update="**\Fixtures\**" CopyToOutputDirectory="PreserveNewest" />`, senão o teste falha por arquivo não encontrado.
  Os 8 últimos casos do fixture são **regressões de defeitos reais** do classificador (spike d §4) e não devem ser removidos: `AO?` não casa "o"; "sem êxito" ≠ "sem sucesso"; "feito contato" é sucesso, não falha; `CONTATO TELEFONIC` também aparece em pedido à unidade; "sem disponibilidade de vaga"; "telefone é de outra pessoa".
- [ ] **6.3** `RegulacaoPendenciaConsumidor` (background) + idempotência por gatilho + carimbo `ProcessadoPor`.
- [ ] **6.4** Endpoints: `GET /regulacao/pendencias` (47; 48 = todas), `POST /regulacao/pendencias/{id}/responder` (47), `POST …/aprovar`, `POST …/rejeitar` (48); telas (abas, modais reaproveitados).
- [ ] **6.5** Documento criticado: nova versão na caixinha + reenvio ao SER/SERNIT (depende do spike a) ou baixa local (SISREG).
- [ ] **6.6** Testes: ciclo Aberta → AguardandoAgente → Submetida com `SerWebSessaoFake`; SISREG baixa local; gatilho sem pedido local não vira pendência; dois gatilhos iguais não duplicam.

## Dependências

Planos 04, 05 (vínculo), 07 (credencial do agente), 12 (escrita SER/SERNIT), spike d.

## Riscos e pontos a confirmar

- A varredura do SER hoje não roda sozinha em prod (scheduler desligado); sem varredura não há gatilho novo. Ligar a varredura é decisão operacional fora deste plano — registrar como pré-requisito do incremento 6.
- Texto de follow-up é livre; a taxonomia vai errar. `Outro` sempre vira pendência para o agente, nunca some.
- Follow-up no SER "preserva a situação" (docs/ser.md §9); não assumir mudança de estado ao submeter.

## Testes

Tarefa 6.6 + manual: seed de `ser_evento` com "SEM CONTATO: DIVERSAS TENTATIVAS" ligado a um pedido local → pendência `FalhaContato` na UBS certa; UBS informa telefone; agente aprova; `SerWebSessaoFake` recebeu o POST de contatos; pendência `Submetida`.

## Fora de escopo

Gatilhos do SISREG (não existem); envio de mensagem ao paciente por causa da pendência (pode reaproveitar `IComunicacaoPacienteService` depois).

---

## Especificação para execução

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `SMSMais.Data/Entities/Regulacao/RegulacaoPendencia.cs` + `Enums/{TipoPendenciaRegulacao,OrigemPendenciaRegulacao,EstadoPendenciaRegulacao}.cs` + configuração | criar |
| `SMSMais.Data/SmsMaisDbContext.cs` | DbSet `RegulacaoPendencias` |
| `SMSMais.Data/Migrations/<ts>_PendenciasDeRegulacao.cs` | gerar |
| `SMSMais.Core/Regulacao/Pendencias/ClassificadorFollowUp.cs` (puro), `IRegulacaoPendenciaService.cs`, `RegulacaoPendenciaService.cs`, `Dtos/RegulacaoPendenciaDtos.cs` | criar |
| `SMSMais.Core/Regulacao/Pendencias/Background/RegulacaoPendenciaConsumidor.cs` (`BackgroundService`, ciclo a cada 5 min, cap 1 — copiar o esqueleto de `SerConciliacaoPacienteRunner`) | criar |
| `SMSMais.Core/DependencyInjection.cs` | registrar service + `AddHostedService<RegulacaoPendenciaConsumidor>()` |
| `SMSMais.Api/Controllers/RegulacaoPendenciasController.cs` | criar |
| `SMSMais.front/src/features/regulacao/pages/PendenciasPage.tsx`, `components/pendencias/{ListaPendencias,ResponderPendencia,AprovarPendencia}.tsx`, aba "Pendências" no detalhe | criar |
| `tests/SMSMais.Tests/Regulacao/Pendencias/*.cs` + fixture `Fixtures/followups-rotulados.csv` (do spike d, sem PII) | criar |

### B. Entidade

```csharp
public sealed class RegulacaoPendencia
{
    public Guid Id { get; set; }
    public Guid SolicitacaoId { get; set; }                 // Cascade
    public TipoPendenciaRegulacao Tipo { get; set; }        // FalhaContato=1, AlteracaoTelefone=2, DocumentoCriticado=3, FollowUpPonta=4, ComplementacaoAgente=5
    public OrigemPendenciaRegulacao Origem { get; set; }    // SistemaExterno=1, Ponta=2, Agente=3
    public SistemaRegulacao? SistemaOrigem { get; set; }
    public Guid? SerGatilhoId { get; set; }  public Guid? SernitGatilhoId { get; set; }   // FKs SetNull
    public string? ChaveEventoExterno { get; set; }        // max 200; unique parcial (solicitacao_id, tipo, chave_evento_externo) WHERE not null
    public Guid? ExigenciaId { get; set; }                  // FK regulacao_solicitacao_exigencia SetNull
    public EstadoPendenciaRegulacao Estado { get; set; } = EstadoPendenciaRegulacao.Aberta;  // Aberta=1, AguardandoAgente=2, Submetida=3, Baixada=4, Rejeitada=5
    public string Descricao { get; set; } = string.Empty;  // max 4000
    public string? RespostaTexto { get; set; }              // max 4000
    public string? RespostaJson { get; set; }               // jsonb: {telefones: {residencial, whatsapp, contato}} | {arquivoId}
    public Guid? RespondidaPor { get; set; }  public DateTime? RespondidaEm { get; set; }
    public Guid? AprovadaPor { get; set; }  public DateTime? AprovadaEm { get; set; }
    public DateTime? SubmetidaEm { get; set; }  public string? ResultadoSubmissao { get; set; }   // max 2000
    public Guid? BaixadaPor { get; set; }  public DateTime? BaixadaEm { get; set; }  public string? MotivoBaixa { get; set; }
    public DateTime CriadoEm { get; set; }
}
// índices ix_regulacao_pendencia_estado (estado, solicitacao_id), ix_regulacao_pendencia_tipo_estado (tipo, estado)
```

### C. Classificador

```csharp
public sealed record RegraFollowUp(string Categoria, string Regex, int Ordem, string? ViraPendencia);
// categorias (spike d): ReclassificacaoRisco | FalhaContato | ContatoRealizado | CancelamentoOuReagendamento
//                      | SolicitacaoAoSolicitante | OrientacaoAoPaciente | SemVaga | Agendamento | Outro
// ViraPendencia: "contato" (FalhaContato) | "documento" (SolicitacaoAoSolicitante) | null
public static class ClassificadorFollowUp
{
    public static string Classificar(string texto, IReadOnlyList<RegraFollowUp> regras);   // normaliza (unaccent, upper), aplica por prioridade, default "Outro"
}
```
Regras vêm de `regulacao_configuracao.regras_followup_json` (plano 09). **A semente escrita à mão aqui foi substituída** pela medida do spike d: usar [`revisoes/spike-d-regras-followup.json`](revisoes/spike-d-regras-followup.json) verbatim. Normalização obrigatória antes de casar: NFKD sem acento, colapso de espaço, `UPPER` — sem isso "êxito" e "EXITO" não casam. **A ordem das regras é significativa**: `FalhaContato` vem antes de `ContatoRealizado` porque o texto de falha quase sempre cita "contato".

### D. Serviço

```csharp
public interface IRegulacaoPendenciaService
{
    Task<PaginaDto<RegulacaoPendenciaDto>> ListarAsync(RegulacaoPendenciaFiltro filtro, CancellationToken ct);  // escopo 47; 48 vê todas
    Task<RegulacaoPendenciaDto> AbrirAsync(Guid solicitacaoId, TipoPendenciaRegulacao tipo, OrigemPendenciaRegulacao origem, string descricao, Guid? exigenciaId, string? chaveExterna, SistemaRegulacao? sistema, Guid? serGatilhoId, Guid? sernitGatilhoId, CancellationToken ct);
    Task<RegulacaoPendenciaDto> ResponderAsync(Guid pendenciaId, ResponderPendenciaRequest req, CancellationToken ct);     // ponta (47): Aberta → AguardandoAgente
    Task<RegulacaoPendenciaDto> AprovarESubmeterAsync(Guid pendenciaId, CancellationToken ct);                          // agente (48): AguardandoAgente → Submetida | Baixada
    Task<RegulacaoPendenciaDto> RejeitarAsync(Guid pendenciaId, string motivo, CancellationToken ct);                    // agente: → Rejeitada
    Task<RegulacaoPendenciaDto> BaixarAsync(Guid pendenciaId, string motivo, CancellationToken ct);                      // agente: → Baixada (sem submeter)
    Task<int> ProcessarGatilhosAsync(CancellationToken ct);   // usado pelo consumidor
}
public sealed record RegulacaoPendenciaFiltro(EstadoPendenciaRegulacao[]? Estados, TipoPendenciaRegulacao[]? Tipos, SistemaRegulacao? Sistema, Guid? UnidadeSolicitanteId, Guid? SolicitacaoId, int Pagina = 1, int Tamanho = 25);
public sealed record ResponderPendenciaRequest(string? Texto, TelefonesRequest? Telefones, Guid? ArquivoId);
public sealed record TelefonesRequest(string? Residencial, string? Whatsapp, string? Contato);
public sealed record RegulacaoPendenciaDto(Guid Id, Guid SolicitacaoId, long NumeroLocal, string? NumeroExterno, SistemaRegulacao? Sistema, TipoPendenciaRegulacao Tipo, OrigemPendenciaRegulacao Origem, EstadoPendenciaRegulacao Estado, string Descricao, string? RespostaTexto, JsonElement? RespostaJson, string PacienteNome, string Procedimento, Guid UnidadeSolicitanteId, string UnidadeSolicitante, Guid? ExigenciaId, string? ExigenciaTitulo, DateTime CriadoEm, DateTime? RespondidaEm, DateTime? AprovadaEm, DateTime? SubmetidaEm, string? ResultadoSubmissao, string? MotivoBaixa);
```
`ProcessarGatilhosAsync`: `db.SerGatilhos.Where(g => g.Tipo == TipoGatilhoSer.NovoFollowUp && g.ProcessadoEm == null && db.RegulacaoSolicitacoes.Any(r => r.SerSolicitacaoId == g.SerSolicitacaoId)).Take(200)` → para cada, texto = último `SerEvento` FollowUP daquela solicitação (`Observacao`), `categoria = ClassificadorFollowUp.Classificar(...)`; `FalhaContato`/`DocumentoCriticado` → `AbrirAsync(..., chaveExterna = g.ChaveEvento)`; `Outro` → `ComplementacaoAgente` (para o agente decidir); `Agendamento` → nada; sempre `g.ProcessadoEm = now; g.ProcessadoPor = "regulacao"`. Idem SERNIT. Unique parcial evita duplicar em reprocessamento.

`AprovarESubmeterAsync`: carrega solicitação; `sessao = await store.Exigir(provedorDoSistema, null)` (plano 07; sem credencial → `credencial.ausente`); por tipo: `AlteracaoTelefone`/`FalhaContato` com telefones → `ISerEscritaService.AlterarContatosAsync(serSolicitacaoId, req)` (o service já confirma relendo) e, se houver texto, `RegistrarFollowUpAsync` com prefixo `"[{UnidadeSolicitante} — {RespondidaPorNome}] "`; `FollowUpPonta` → só follow-up; `DocumentoCriticado` → plano 12 (`IRegulacaoEnvioSistema.AnexarEmSolicitacaoExistenteAsync` do sistema correspondente) e, se não suportado, follow-up "novo documento disponível no SMSMais"; SISREG → `Baixada` com `MotivoBaixa = "SISREG não tem este recurso; registrado localmente"`. Sucesso → `Submetida`, `ResultadoSubmissao`, evento `PendenciaSubmetida`; falha → permanece `AguardandoAgente`, `ResultadoSubmissao = erro`, `FalhaEnvio` no histórico.

`ResponderAsync` (ponta): valida escopo; `Estado ∈ {Aberta, Rejeitada}`; grava resposta; `AguardandoAgente`; evento `PendenciaRespondida`. Para `DocumentoCriticado`: o front primeiro anexa a nova versão pela rota de arquivos (plano 02) e passa `ArquivoId`.

### E. Endpoints — `RegulacaoPendenciasController`, `[Route("regulacao/pendencias")]`

`GET` (filtro; `Regulacao`, Consulta) · `POST` body `{solicitacaoId, tipo, descricao, exigenciaId?}` (ponta abre `FollowUpPonta`/`AlteracaoTelefone`; `Regulacao`, Inclusao) · `POST {id}/responder` (`Regulacao`, Edicao) · `POST {id}/aprovar` · `POST {id}/rejeitar {motivo}` · `POST {id}/baixar {motivo}` (`RegulacaoTriagem`, Edicao) · `POST processar-gatilhos` (`RegulacaoConfiguracao`, Edicao — manual).

### F. Front

- `PendenciasPage.tsx` (rota `regulacao/solicitacoes/pendencias`): `Tabs`: "Pendência de contato" (tipo FalhaContato, estados Aberta/Rejeitada) · "Minhas pendências" (todas as abertas da unidade) · "Aguardando regulação" (só 48, `AguardandoAgente`) · "Resolvidas"; `ListaPendencias` com tipo, origem, descrição, idade, solicitação (link).
- `ResponderPendencia.tsx`: por tipo — `FalhaContato`: `ModalContato` (telefones) + textarea opcional; `DocumentoCriticado`: `UploadAnexo` na caixinha + confirmar; `FollowUpPonta`: `ModalFollowUp`.
- `AprovarPendencia.tsx` (48): mostra descrição externa, resposta da ponta, botões Aprovar e submeter / Rejeitar / Baixar; usa `useSessaoIntegracaoObrigatoria(provedor)` (plano 07) para pedir a senha quando `credencial.ausente`.
- `queries.ts`: `usePendencias(filtro)` (`refetchInterval: 60_000`), `useAbrirPendencia`, `useResponderPendencia`, `useAprovarPendencia`, `useRejeitarPendencia`, `useBaixarPendencia`.

### G. Testes

`ClassificadorFollowUpTests`: uma linha por regra da semente + `Precisao_na_amostra_rotulada_acima_de_80_por_cento` (lê `Fixtures/followups-rotulados.csv`). `RegulacaoPendenciaServiceTests` (fixture + `SerWebSessaoFake`): `Gatilho_sem_pedido_local_nao_vira_pendencia`; `Gatilho_repetido_nao_duplica`; `Ciclo_aberta_aguardando_submetida`; `Sisreg_baixa_local`; `Ponta_nao_escreve_no_ser`; `Falha_na_submissao_mantem_aguardando`.

### H. Passo a passo

1. Entidade/enums/config/DbSet → build → migration `PendenciasDeRegulacao` → build.
2. `ClassificadorFollowUp` + testes (com a amostra do spike d).
3. Service + consumidor + DI + testes.
4. Controller → `/docs`.
5. Front → `npm run build`.
6. `PROGRESSO.md` 6.1–6.6.

### I. Critério de pronto

Seed `ser_evento` FollowUP "SEM CONTATO: DIVERSAS TENTATIVAS" ligado a pedido local → pendência `FalhaContato` na UBS certa; UBS informa telefone; agente aprova; `SerWebSessaoFake` recebeu o POST de contatos; pendência `Submetida`; solicitação SISREG análoga fica `Baixada` com motivo.
