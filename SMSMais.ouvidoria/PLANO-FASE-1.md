# Ouvidoria — plano da fase 1 (backend + painel)

> **Fonte da intenção:** [`README.md`](./README.md) §0 (premissas do Bernardo) e §4 (requisitos R1–R33). **Decisão arquitetural:** [ADR-0060](../docs/adr/0060-modulo-ouvidoria.md). **Padrões de código a seguir:** [`referencias/05-padroes-do-codigo.md`](./referencias/05-padroes-do-codigo.md). **Andamento:** [`PROGRESSO.md`](./PROGRESSO.md).
> Escrito em 20/09/2026. Este arquivo é o **contrato** entre as tarefas: quem implementa o front trabalha contra os endpoints e DTOs daqui sem esperar o backend.

## Decisões assumidas para a fase 1 (D-4..D-7 do README)

| | Decisão | Base |
|---|---|---|
| D-4 | Cidadão 30 + 30 (uma prorrogação). Área: **20 dias** (Normal), **10** (Alta), **2 dias úteis** (Urgente); configuráveis por instância. Complementação 20 dias, suspende uma vez. | Lei 13.460 art. 16; PN CGU 116 arts. 25–27; SES-RJ |
| D-5 | Anonimato **só em denúncia** (vira comunicação de irregularidade, sem código de acesso). Solicitação e informação nunca sigilosas/anônimas. | Manual MS 2014; Fala.BR |
| D-6 | Unidade apuratória = ponto de resposta do tipo `Apuracao`, cadastrado pelo ouvidor (corregedoria, comissão de ética, vigilância…). | PN 116 art. 33–35 |
| D-7 | Manifestação-solicitação **não abre** pedido de regulação; só se **vincula** a um existente (`RegulacaoSolicitacaoId`) e a Regulação é um ponto de resposta do tipo `AreaCentral`. | R8; ouvidoria não é fila |

Fora da fase 1 (fase 2/3): site público `ouvidoria.<domínio>`, app do cidadão, pesquisa pós-resposta, captação por WhatsApp, painéis completos, relatório trimestral/anual PDF, exportação Fala.BR, IA, OuvidorSUS. **Mas os endpoints públicos mínimos já entram na fase 1** (registrar e acompanhar por protocolo + código) para o site público nascer sem mexer no backend.

---

## 1. Modelo de dados (`SMSMais.Data`, schema `smsmarica`, tabelas `ouvidoria_*`)

### 1.1 Enums — `Entities/Enums/OuvidoriaEnums.cs` (int no banco, string no JSON; valores explícitos a partir de 1)

```csharp
public enum OuvidoriaTipo { Solicitacao = 1, Reclamacao = 2, Denuncia = 3, Sugestao = 4, Elogio = 5, Informacao = 6 }
public enum OuvidoriaIdentificacao { Identificada = 1, Sigilosa = 2, Anonima = 3 }
public enum OuvidoriaCanal { Painel = 1, SitePublico = 2, AppCidadao = 3, WhatsApp = 4, Presencial = 5, Telefone = 6, Email = 7, Carta = 8, Urna = 9, BuscaAtiva = 10, Disque136 = 11, FalaBr = 12, OuvidoriaGeral = 13, Outro = 99 }
public enum OuvidoriaOrigem { Cidadao = 1, OuvidoriaAtiva = 2, DeOficio = 3, Coletiva = 4 }
public enum OuvidoriaPrioridade { Normal = 1, Alta = 2, Urgente = 3 }
public enum OuvidoriaStatus { Registrada = 1, EmTriagem = 2, Encaminhada = 3, AguardandoComplementacao = 4, RespondidaPelaArea = 5, EmValidacao = 6, Respondida = 7, EmRecurso = 8, Concluida = 9, Arquivada = 10, EncaminhadaOutroOrgao = 11 }
public enum OuvidoriaResolutividade { Resolvida = 1, NaoResolvida = 2 }
public enum OuvidoriaSituacaoFinal { Atendida = 1, NaoAtendida = 2, NaoLocalizado = 3, Faleceu = 4, Procede = 5, NaoProcede = 6, Inconclusiva = 7 }
public enum OuvidoriaMotivoNaoAtendimento { FaltaRecursos = 1, NaoCobertoSus = 2, FezParticular = 3, VagasInsuficientes = 4, NaoCompareceu = 5, Outro = 99 }
public enum OuvidoriaMotivoArquivamento { Duplicidade = 1, TextoIncompreensivel = 2, FaltaUrbanidade = 3, Impropria = 4, CopiaConhecimento = 5, PerdaObjeto = 6, SemComplementacao = 7, SemElementosMinimos = 8, Outro = 99 }
public enum OuvidoriaTipoEvento { Registro = 1, Triagem = 2, Reclassificacao = 3, Encaminhamento = 4, PedidoComplementacao = 5, Complementacao = 6, RespostaArea = 7, DevolucaoParaReanalise = 8, RespostaIntermediaria = 9, RespostaConclusiva = 10, Prorrogacao = 11, Cobranca = 12, Escalonamento = 13, Recurso = 14, Conclusao = 15, Arquivamento = 16, EncaminhamentoExterno = 17, Anotacao = 18, Habilitacao = 19, Reabertura = 20, AcessoIdentidade = 21 }
public enum OuvidoriaTipoPontoResposta { Unidade = 1, AreaCentral = 2, Apuracao = 3 }
```

### 1.2 Entidades — `Entities/Ouvidoria/*.cs` (uma classe por arquivo, `sealed`, XML doc pt-BR, Guid v7)

**`OuvidoriaManifestacao`** — tabela `ouvidoria_manifestacao`
- `Id`; `Protocolo` (string 11, único, `AAAA-NNNNNN`; sequência Postgres `ouvidoria_protocolo_seq` declarada com `modelBuilder.HasSequence<long>`); `CodigoAcessoHash` (string? 64 — SHA-256 do código de 8 caracteres; **null quando anônima**).
- Classificação: `Tipo`, `Identificacao`, `Canal`, `Origem`, `Prioridade` (default Normal), `Status` (default Registrada), `AssuntoId?`, `SubassuntoId?` (ambos FK para `ouvidoria_assunto`), `Resumo?` (200), `Teor` (text), `TeorPseudonimizado?` (text — o que vai à apuração em denúncia).
- Contexto: `UnidadeId?` (FK `unidade`), `PontoRespostaId?` (FK), `RegulacaoSolicitacaoId?` (FK `regulacao_solicitacao`), `ProtocoloExterno?` (60), `SistemaExterno?` (40), `DataFato?` (DateOnly), `LocalFato?` (200).
- Manifestante (**nunca no DTO do ponto de resposta**): `ManifestanteNome?` (200), `ManifestanteCpf?` (11), `ManifestanteTelefone?` (20), `ManifestanteEmail?` (200), `ManifestantePatientId?` (Guid, sem FK física).
- Referido (paciente em favor de quem): `ReferidoPatientId?`, `ReferidoNome?`, `ReferidoCpf?`, `ReferidoCns?`.
- Envolvido: `EnvolvidoPractitionerId?`, `EnvolvidoDescricao?` (300).
- Relógios: `RegistradaEm`, `PrazoRespostaEm` (DateOnly), `ProrrogadoEm?`, `ProrrogacaoJustificativa?`, `PrazoAreaEm?` (DateOnly), `EncaminhadaEm?`, `ComplementacaoSolicitadaEm?`, `ComplementacaoUsada` (bool), `SuspensaEm?`, `DiasSuspensos` (int, default 0), `RespondidaEm?`, `ConcluidaEm?`, `DiasAteResposta?`, `DiasAtraso?`, `UltimaAtividadeEm`.
- Conclusão: `Resolutividade?`, `SituacaoFinal?`, `MotivoNaoAtendimento?`, `MotivoArquivamento?`, `RespostaConclusiva?` (text).
- Denúncia: `HabilitadaEm?`, `HabilitadaPor?`.
- Trabalho: `ResponsavelId?` (técnico da ouvidoria atribuído).
- Auditoria ADR-0006 (6 campos) + `uint RowVersion` (xmin).
- Navegações: `Eventos`, `Anexos`, `Marcadores` (N:N via `OuvidoriaManifestacaoMarcador`).
- Índices: `Protocolo` único; `Status`; `UnidadeId`; `PontoRespostaId`; `PrazoRespostaEm`; `ManifestanteCpf`; `(Tipo, RegistradaEm)`.

**`OuvidoriaEvento`** — `ouvidoria_evento` (append-only; nunca UPDATE/DELETE)
- `Id`, `ManifestacaoId`, `Tipo` (OuvidoriaTipoEvento), `StatusAnterior?`, `StatusNovo?`, `AutorId?`, `AutorNome?` (desnormalizado, 200), `PontoRespostaId?`, `Texto?` (text), `VisivelAoCidadao` (bool), `CriadoEm`. Índice `(ManifestacaoId, CriadoEm)`.

**`OuvidoriaAnexo`** — `ouvidoria_anexo`: `Id`, `ManifestacaoId`, `EventoId?`, `MidiaId` (FK `midia`), `NomeArquivo` (300), `VisivelAoCidadao`, `CriadoEm`.

**`OuvidoriaAssunto`** — `ouvidoria_assunto`: `Id`, `PaiId?` (self), `Nome` (200), `CodigoOuvidorSus?` (20), `Ordem` (int), `Ativo`. Índice `(PaiId, Nome)` único.

**`OuvidoriaMarcador`** — `ouvidoria_marcador`: `Id`, `Nome` (80, único), `Ativo`. **`OuvidoriaManifestacaoMarcador`** — `ouvidoria_manifestacao_marcador`: PK composta `(ManifestacaoId, MarcadorId)`.

**`OuvidoriaPontoResposta`** — `ouvidoria_ponto_resposta`: `Id`, `Nome` (200), `Tipo` (OuvidoriaTipoPontoResposta), `UnidadeId?` (FK; obrigatório quando `Tipo = Unidade`; único por unidade), `PrazoDias?` (sobrepõe a configuração), `Ativo`, auditoria (6 campos). Navegação `Membros`.
**`OuvidoriaPontoRespostaMembro`** — `ouvidoria_ponto_resposta_membro`: `Id`, `PontoRespostaId`, `UsuarioId` (FK `usuario`), `Titular` (bool), `CriadoEm`. Único `(PontoRespostaId, UsuarioId)`.

**`OuvidoriaAcessoIdentidade`** — `ouvidoria_acesso_identidade` (Decreto 10.153 art. 6º §3º): `Id`, `ManifestacaoId`, `UsuarioId`, `Justificativa` (500), `Ip?` (45), `CriadoEm`. Índice `(ManifestacaoId, CriadoEm)`.

**`OuvidoriaConfiguracao`** — `ouvidoria_configuracao`, singleton `IdSingleton = new("77777777-0000-0000-0000-000000000002")`: `PrazoCidadaoDias` = 30, `ProrrogacaoDias` = 30, `PrazoAreaDias` = 20, `PrazoAreaAltaDias` = 10, `PrazoAreaUrgenteDiasUteis` = 2, `ComplementacaoDias` = 20, `ArquivamentoAutomaticoDias` = 30, `NotificarPorWhatsApp` = true, `TextoRecibo?` (text — mensagem enviada com o protocolo; `{protocolo}` e `{prazo}` são substituídos), `AtualizadoEm?`, `AtualizadoPor?`.

### 1.3 Configurations
Um arquivo `Configurations/OuvidoriaConfiguration.cs` com uma classe `internal sealed class XConfiguration : IEntityTypeConfiguration<X>` por entidade (molde `TicketConfiguration.cs`): `ToTable("ouvidoria_...")`, **todas** as colunas `HasColumnName("snake_case")`, enums `HasConversion<int>()`, `xmin` como no Ticket, FKs com `OnDelete(Restrict)` exceto `Evento/Anexo/Marcador N:N/Membro/AcessoIdentidade → Cascade`. Sequência: em `OuvidoriaManifestacaoConfiguration` **não**; declarar em `SmsMaisDbContext.OnModelCreating` via `modelBuilder.HasSequence<long>("ouvidoria_protocolo_seq", SchemaPadrao).StartsAt(1).IncrementsBy(1);` (comentário `// Ouvidoria (ADR-0060)`).

### 1.4 DbSets em `SmsMaisDbContext` (bloco comentado `// ---- Ouvidoria (ADR-0060) ----`)
`OuvidoriaManifestacoes`, `OuvidoriaEventos`, `OuvidoriaAnexos`, `OuvidoriaAssuntos`, `OuvidoriaMarcadores`, `OuvidoriaManifestacaoMarcadores`, `OuvidoriaPontosResposta`, `OuvidoriaPontoRespostaMembros`, `OuvidoriaAcessosIdentidade`, `OuvidoriaConfiguracoes`.

### 1.5 Permissões — `Enums/ModuloPermissao.cs`, bloco `// ---- Ouvidoria (ADR-0060, 20/09/2026) ----`
| # | Nome | Consulta | Inclusao | Edicao | Exclusao |
|---|---|---|---|---|---|
| 71 | `Ouvidoria` | ver fila e detalhe (manifestante mascarado se sigilosa) | registrar manifestação | triar, encaminhar, pedir complementação, validar, responder ao cidadão, prorrogar, cobrar, recurso, concluir | arquivar |
| 72 | `OuvidoriaGestao` | painel/indicadores, configuração | criar pontos de resposta, assuntos, marcadores | editar os mesmos + configuração + escalonar | desativar |
| 73 | `OuvidoriaSigilo` | ver denúncias e revelar identidade em sigilosas (**cada revelação exige justificativa e é logada**) | habilitar denúncia | editar teor pseudonimizado | — |
| 74 | `OuvidoriaPontoResposta` | ver manifestações encaminhadas aos pontos de que é membro, **sem dados do manifestante** | — | responder pela área | — |

Justificativa no XML doc: `AcoesPermissao` só tem 4 flags; sigilo e ponto de resposta são **eixos ortogonais** ao trabalho da ouvidoria (quem vê identidade × quem responde pela unidade), então não cabem como ação de `Ouvidoria`.

### 1.6 Seed (dentro do service, idempotente, na primeira leitura de assuntos — **não em migration**)
Os 22 assuntos do Manual do MS 2014 (§A.5 de `referencias/02`), sem subassuntos, `CodigoOuvidorSus` nulo. Configuração singleton criada sob demanda com os defaults. Nenhum ponto de resposta seedado.

### 1.7 Migration
`dotnet ef migrations add Ouvidoria --project src/SMSMais.Data --startup-project src/SMSMais.Api`. **Conferir o `Up()`**: deve conter só `ouvidoria_*` + a sequência. Se aparecer `DropTable`/`DropColumn` de outra coisa, **parar** e avisar (snapshot compartilhado — ver `referencias/05`).

---

## 2. Regras de negócio (`SMSMais.Core/Ouvidoria/`)

Arquivos: `IOuvidoriaManifestacaoService.cs` + `OuvidoriaManifestacaoService.cs`; `IOuvidoriaCatalogoService.cs` + `OuvidoriaCatalogoService.cs` (assuntos, marcadores, pontos de resposta, configuração); `IOuvidoriaPublicoService.cs` + `OuvidoriaPublicoService.cs` (protocolo + código); `OuvidoriaPrazos.cs` (estático puro: cálculo de prazos, dias úteis sem feriados, atraso); `OuvidoriaProtocolo.cs` (formato + geração do código de acesso e hash); `OuvidoriaNotificador.cs` (WhatsApp best-effort); `Dtos/OuvidoriaDtos.cs`; `Validators/OuvidoriaValidators.cs`. Registro em `DependencyInjection.cs` com comentário `// Ouvidoria (ADR-0060)`.

### 2.1 Registro (`RegistrarAsync`)
1. Validar identificação × tipo: `Solicitacao`/`Informacao` exigem `Identificada` (nome + CPF **ou** telefone; CPF basta — Lei 13.460 art. 10-A); `Anonima` só em `Denuncia`; `Sigilosa` permitida em Reclamação/Denúncia/Sugestão/Elogio. Violação → `ValidacaoException("identificacao", …)`.
2. Nunca recusar por falta de campo além de `Tipo`, `Identificacao`, `Teor` (mín. 10 caracteres), `Canal`. Sem campo "motivo".
3. `Protocolo = $"{ano}-{nextval:D6}"`; código de acesso = 8 caracteres `[A-HJ-NP-Z2-9]` (sem 0/O/1/I) gerado com `RandomNumberGenerator`; salvar só o hash SHA-256; **devolver o código uma única vez** no retorno. Anônima: sem código.
4. `PrazoRespostaEm = hoje + PrazoCidadaoDias`; `Status = Registrada`; `UltimaAtividadeEm = agora`.
5. Evento `Registro` (visível ao cidadão) com o canal.
6. Se `ManifestantePatientId` nulo e CPF informado, tentar resolver pelo `IPacienteResolver` (best-effort; não falha).
7. Notificar recibo (§2.8).

### 2.2 Triagem (`TriarAsync`) — `Registrada | EmTriagem → EmTriagem` (ou permanece)
Permite reclassificar tipo (respeitando §2.1.1; reclassificar denúncia → outro tipo gera evento `Reclassificacao` visível), assunto/subassunto, prioridade, unidade, marcadores, resumo, responsável, vínculo `RegulacaoSolicitacaoId`. Prioridade recalcula `PrazoAreaEm` **só se ainda não encaminhada**. Se a manifestação é duplicada (mesmo `ManifestanteCpf` + mesmo `AssuntoId` + mesma `UnidadeId` nos últimos 90 dias, não arquivada), o DTO de detalhe expõe `possiveisDuplicatas` (lista de protocolos) — a decisão de arquivar é humana.

### 2.3 Encaminhar (`EncaminharAsync`) — `Registrada | EmTriagem | RespondidaPelaArea | EmValidacao | EmRecurso → Encaminhada`
- Exige `PontoRespostaId` ativo. Denúncia exige `HabilitadaEm` não nulo e ponto do tipo `Apuracao`, e **exige `TeorPseudonimizado`** preenchido (o técnico com `OuvidoriaSigilo` edita antes).
- `PrazoAreaEm = OuvidoriaPrazos.PrazoArea(hoje, prioridade, config, ponto.PrazoDias)`; `EncaminhadaEm = agora`.
- Evento `Encaminhamento` visível ao cidadão **sem nomear pessoas** ("Encaminhada à unidade/área responsável em dd/mm").
- Notificar cidadão (§2.8).

### 2.4 Complementação
- `PedirComplementacaoAsync` — qualquer status ativo → `AguardandoComplementacao`. **Só uma vez** (`ComplementacaoUsada`), senão `ConflitoException("ouvidoria.complementacao_unica", …)`. Marca `SuspensaEm = agora`, `ComplementacaoSolicitadaEm`. Evento visível. Anônima: não permitido (`ConflitoException("ouvidoria.anonima_sem_contato")`).
- `ComplementarAsync` (técnico registra o que chegou, ou o cidadão pelo endpoint público) — `AguardandoComplementacao → EmTriagem`. Retoma o relógio: `DiasSuspensos += (hoje − SuspensaEm)`, `PrazoRespostaEm += dias suspensos`, `SuspensaEm = null`.
- Job (fase 1 mínimo: método `ArquivarSemComplementacaoAsync()` chamado por um `BackgroundService` diário `OuvidoriaRotinasService`): `AguardandoComplementacao` há mais de `ComplementacaoDias` → `Arquivada` com `MotivoArquivamento = SemComplementacao`, evento visível.

### 2.5 Resposta da área e validação
- `ResponderAreaAsync` (membro do ponto **ou** técnico da ouvidoria) — `Encaminhada → RespondidaPelaArea`; evento `RespostaArea` **não visível** ao cidadão; anexos opcionais.
- `DevolverParaReanaliseAsync` — `RespondidaPelaArea | EmValidacao → Encaminhada` com novo `PrazoAreaEm` (metade do prazo original, mín. 2 dias). Evento não visível.
- `IniciarValidacaoAsync` — `RespondidaPelaArea → EmValidacao` (implícito ao abrir para responder; pode ser só o próprio `ResponderCidadaoAsync`).

### 2.6 Resposta ao cidadão (`ResponderCidadaoAsync`) — `EmTriagem | Encaminhada | RespondidaPelaArea | EmValidacao | EmRecurso → Respondida`
- `Conclusiva = false` → evento `RespostaIntermediaria` visível; status **não muda**.
- `Conclusiva = true` → exige `Resolutividade` e `SituacaoFinal` coerente com o tipo: Solicitação → `Atendida | NaoAtendida (+ MotivoNaoAtendimento) | NaoLocalizado | Faleceu`; Reclamação/Denúncia → `Procede | NaoProcede | Inconclusiva`; Sugestão/Elogio/Informação → `Atendida`. Texto mín. 20 caracteres (conteúdo mínimo do art. 29 fica como **ajuda de campo** no front, não como validação). `RespondidaEm = agora`, `DiasAteResposta = dias(RegistradaEm → agora) − DiasSuspensos`, `DiasAtraso = max(0, hoje − PrazoRespostaEm)`. Evento `RespostaConclusiva` visível. Notificar.
- Não permitido em `Anonima` responder ao cidadão? **Permitido**: a resposta fica no acompanhamento… anônima não tem acesso. Regra: em anônima, `ResponderCidadaoAsync` conclusiva só registra e já **conclui** (`Status = Concluida`), sem notificação.

### 2.7 Prorrogar, cobrar, escalonar, recurso, concluir, arquivar, externo, habilitar, anotar
- `ProrrogarAsync` — status ativo, **uma vez** (`ProrrogadoEm` nulo), exige `Justificativa` (mín. 20), **proibido em `EncaminhadaOutroOrgao`**. `PrazoRespostaEm += ProrrogacaoDias`. Evento visível. Notificar.
- `CobrarAsync` — `Encaminhada` com `PrazoAreaEm < hoje` (ou qualquer hora, com texto); evento `Cobranca` não visível; notifica membros do ponto (fase 1: só evento; e-mail/WhatsApp interno fica para fase 2).
- `EscalonarAsync` (`OuvidoriaGestao`) — evento `Escalonamento` não visível, texto obrigatório.
- `RegistrarRecursoAsync` — `Respondida → EmRecurso`, **uma vez** (não pode existir evento `Recurso` anterior); texto obrigatório; evento visível.
- `ConcluirAsync` — `Respondida → Concluida` (manual) e automático pelo `OuvidoriaRotinasService` após `ArquivamentoAutomaticoDias` sem recurso. `ConcluidaEm = agora`. Evento visível.
- `ArquivarAsync` (`Ouvidoria.Exclusao`) — qualquer status não final → `Arquivada`; exige `MotivoArquivamento`; `Duplicidade` exige `Texto` com o protocolo original. Evento visível (texto genérico "arquivada: motivo").
- `EncaminharExternoAsync` — → `EncaminhadaOutroOrgao`; `SistemaExterno` + `ProtocoloExterno?` + texto; evento visível; **bloqueia prorrogação**.
- `HabilitarDenunciaAsync` (`OuvidoriaSigilo.Inclusao`) — tipo `Denuncia`, `HabilitadaEm = agora`; texto (autoria/materialidade/competência) no evento `Habilitacao` não visível.
- `AnotarAsync` — evento `Anotacao` não visível, qualquer status.
- `AtualizarTeorPseudonimizadoAsync` (`OuvidoriaSigilo.Edicao`).

### 2.8 Notificações (`OuvidoriaNotificador`)
Best-effort via `IWhatsAppCliente.EnviarTextoAsync(telefone, texto, pacienteId: ManifestantePatientId, origem: Automatico)`; falha só loga (`LogWarning`, **não** `LogError` — não é falha de plataforma). Condições: `config.NotificarPorWhatsApp`, `Identificacao != Anonima`, `ManifestanteTelefone` presente. **Em `Sigilosa`, o texto nunca inclui teor nem assunto**, só protocolo e etapa. Momentos: registro (recibo: protocolo + código + prazo + link `instituicao.UrlApp`-like — usar `TextoRecibo` da configuração ou padrão), encaminhamento, pedido de complementação, prorrogação, resposta conclusiva, arquivamento. ADR-0057 já é aplicado dentro do cliente.

### 2.9 Visibilidade e escopo (`ListarAsync`/`ObterAsync`)
- Quem tem `Ouvidoria.Consulta`: vê tudo, **exceto** denúncias e sigilosas, cujos campos de manifestante vêm mascarados (`manifestante: null`, `identidadeRestrita: true`) a menos que tenha `OuvidoriaSigilo.Consulta`. Denúncias inteiras só aparecem na lista para quem tem `OuvidoriaSigilo`.
- Quem tem só `OuvidoriaPontoResposta`: vê apenas manifestações com `PontoRespostaId` em pontos de que é membro, status `Encaminhada | RespondidaPelaArea | EmValidacao | Respondida | Concluida`, com **`manifestante` sempre nulo**, `Teor` substituído por `TeorPseudonimizado` quando denúncia, eventos internos filtrados para os do próprio ponto.
- `RevelarIdentidadeAsync(id, justificativa)` — grava `OuvidoriaAcessoIdentidade` + evento `AcessoIdentidade` (não visível) + `IAuditoriaService.RegistrarAsync("OuvidoriaManifestacao", id, "RevelarIdentidade", null, justificativa)`. Exige `OuvidoriaSigilo.Consulta` e justificativa (mín. 15).
- Escopo por unidade (`EscopoUnidade`) **não** se aplica à ouvidoria central (ela é transversal). Aplica-se ao ponto de resposta por pertencimento.

### 2.10 Prazos (`OuvidoriaPrazos`, estático, testável)
`PrazoCidadao(DateOnly registro, int dias)`, `PrazoArea(DateOnly hoje, OuvidoriaPrioridade p, OuvidoriaConfiguracao c, int? prazoDoPonto)` (Urgente = dias úteis seg–sex), `DiasAtraso(DateOnly prazo, DateOnly hoje)`, `FaixaPrazo(int dias) → "ate30" | "31a60" | "mais60"`. Fuso: **regra única do repo** (`reference_fuso_datas_regra` — usar o helper de data existente para "hoje" no fuso da instância).

---

## 3. API (`SMSMais.Api/Controllers/`)

### 3.1 `OuvidoriaController` — `[Route("ouvidoria")]` (JWT global; `[RequerPermissao]` por action)

| Método | Rota | Permissão | Body → Retorno |
|---|---|---|---|
| GET | `manifestacoes` | `Ouvidoria.Consulta` **ou** `OuvidoriaPontoResposta.Consulta` (`[RequerQualquerPermissao]`) | query `status[]`, `tipo`, `unidadeId`, `pontoRespostaId`, `prioridade`, `atrasadas` (bool), `aguardandoValidacao` (bool), `busca` (protocolo/nome/CPF), `de`, `ate`, `pagina`, `tamanho` → `PaginaDto<ManifestacaoListaDto>` |
| GET | `manifestacoes/resumo` | idem | → `OuvidoriaResumoDto` |
| GET | `manifestacoes/{id}` | idem | → `ManifestacaoDetalheDto` |
| POST | `manifestacoes` | `Ouvidoria.Inclusao` | `RegistrarManifestacaoRequest` → `201 ManifestacaoCriadaDto` |
| POST | `manifestacoes/{id}/triagem` | `Ouvidoria.Edicao` | `TriarRequest` → 204 |
| POST | `manifestacoes/{id}/encaminhar` | `Ouvidoria.Edicao` | `EncaminharRequest` → 204 |
| POST | `manifestacoes/{id}/pedir-complementacao` | `Ouvidoria.Edicao` | `TextoRequest` → 204 |
| POST | `manifestacoes/{id}/complementar` | `Ouvidoria.Edicao` | `TextoComAnexosRequest` → 204 |
| POST | `manifestacoes/{id}/responder-area` | `OuvidoriaPontoResposta.Edicao` **ou** `Ouvidoria.Edicao` | `TextoComAnexosRequest` → 204 |
| POST | `manifestacoes/{id}/devolver-area` | `Ouvidoria.Edicao` | `TextoRequest` → 204 |
| POST | `manifestacoes/{id}/responder-cidadao` | `Ouvidoria.Edicao` | `ResponderCidadaoRequest` → 204 |
| POST | `manifestacoes/{id}/prorrogar` | `Ouvidoria.Edicao` | `TextoRequest` (justificativa) → 204 |
| POST | `manifestacoes/{id}/cobrar` | `Ouvidoria.Edicao` | `TextoRequest?` → 204 |
| POST | `manifestacoes/{id}/escalonar` | `OuvidoriaGestao.Edicao` | `TextoRequest` → 204 |
| POST | `manifestacoes/{id}/recurso` | `Ouvidoria.Edicao` | `TextoRequest` → 204 |
| POST | `manifestacoes/{id}/concluir` | `Ouvidoria.Edicao` | — → 204 |
| POST | `manifestacoes/{id}/arquivar` | `Ouvidoria.Exclusao` | `ArquivarRequest` → 204 |
| POST | `manifestacoes/{id}/encaminhar-externo` | `Ouvidoria.Edicao` | `EncaminharExternoRequest` → 204 |
| POST | `manifestacoes/{id}/habilitar` | `OuvidoriaSigilo.Inclusao` | `TextoRequest` → 204 |
| PUT | `manifestacoes/{id}/teor-pseudonimizado` | `OuvidoriaSigilo.Edicao` | `TextoRequest` → 204 |
| POST | `manifestacoes/{id}/anotar` | `Ouvidoria.Edicao` | `TextoRequest` → 204 |
| POST | `manifestacoes/{id}/identidade` | `OuvidoriaSigilo.Consulta` | `TextoRequest` (justificativa) → `ManifestanteDto` |
| POST | `anexos` | `Ouvidoria.Inclusao` ou `OuvidoriaPontoResposta.Edicao` | multipart ≤ 6 MB → `MidiaDto` (via `IMidiasService.EnviarAsync(..., "ouvidoria", ct)`) |
| GET/POST/PUT | `pontos-resposta`, `pontos-resposta/{id}` | GET: `Ouvidoria.Consulta`; escrita: `OuvidoriaGestao.Inclusao/Edicao` | `PontoRespostaDto`, `SalvarPontoRespostaRequest` (nome, tipo, unidadeId?, prazoDias?, ativo, membros: `[{usuarioId, titular}]`) |
| GET/POST/PUT | `assuntos`, `assuntos/{id}` | GET: qualquer autenticado; escrita: `OuvidoriaGestao` | `AssuntoDto` (árvore plana com `paiId`), `SalvarAssuntoRequest` |
| GET/POST/PUT | `marcadores`, `marcadores/{id}` | idem | `MarcadorDto` |
| GET/PUT | `configuracao` | GET: `Ouvidoria.Consulta`; PUT: `OuvidoriaGestao.Edicao` | `OuvidoriaConfiguracaoDto` |
| GET | `painel` | `OuvidoriaGestao.Consulta` | query `de`, `ate`, `unidadeId?` → `OuvidoriaPainelDto` |

### 3.2 `OuvidoriaPublicoController` — `[Route("publico/ouvidoria")] [AllowAnonymous]`
| Método | Rota | Body → Retorno |
|---|---|---|
| GET | `assuntos` | → `AssuntoPublicoDto[]` (só ativos, id/nome/paiId) |
| GET | `unidades` | → `UnidadePublicaDto[]` (id, nome; unidades ativas não externas) |
| POST | `manifestacoes` | `RegistrarManifestacaoPublicaRequest` (sem canal/origem — fixos `SitePublico`/`Cidadao`) → `201 ManifestacaoCriadaDto` |
| GET | `manifestacoes/{protocolo}?codigo=` | → `AcompanhamentoDto` (status, tipo, prazo, prorrogado, eventos visíveis, resposta conclusiva, `podeComplementar`, `podeRecorrer`) — 404 se protocolo/código não batem (não distinguir) |
| POST | `manifestacoes/{protocolo}/complementar?codigo=` | `TextoRequest` → 204 |
| POST | `manifestacoes/{protocolo}/recurso?codigo=` | `TextoRequest` → 204 |
Proteção: limitar por IP com o rate limiter já existente no `Program.cs` se houver política nomeada; senão, aplicar `[EnableRateLimiting]` com política nova `ouvidoria-publico` (20 req/min por IP) registrada no `Program.cs`. Comparação do código em tempo constante (`CryptographicOperations.FixedTimeEquals`).

### 3.3 DTOs (`sealed record`, `Dtos/OuvidoriaDtos.cs`)
```csharp
record ManifestanteDto(string? Nome, string? Cpf, string? Telefone, string? Email, Guid? PatientId);
record ReferidoDto(Guid? PatientId, string? Nome, string? Cpf, string? Cns);
record ManifestacaoCriadaDto(Guid Id, string Protocolo, string? CodigoAcesso, DateOnly PrazoRespostaEm);
record ManifestacaoListaDto(Guid Id, string Protocolo, OuvidoriaTipo Tipo, OuvidoriaStatus Status, OuvidoriaPrioridade Prioridade,
    OuvidoriaIdentificacao Identificacao, OuvidoriaCanal Canal, string? Resumo, string? AssuntoNome, string? UnidadeNome,
    string? PontoRespostaNome, string? ManifestanteNome /* null se restrito */, DateTime RegistradaEm, DateOnly PrazoRespostaEm,
    DateOnly? PrazoAreaEm, int DiasAtraso, bool Atrasada, bool AreaAtrasada, DateTime UltimaAtividadeEm, string? ResponsavelNome);
record EventoDto(Guid Id, OuvidoriaTipoEvento Tipo, OuvidoriaStatus? StatusAnterior, OuvidoriaStatus? StatusNovo, string? AutorNome,
    string? PontoRespostaNome, string? Texto, bool VisivelAoCidadao, DateTime CriadoEm, IReadOnlyList<AnexoDto> Anexos);
record AnexoDto(Guid Id, Guid MidiaId, string NomeArquivo, bool VisivelAoCidadao, DateTime CriadoEm);
record ManifestacaoDetalheDto(/* tudo da lista */ ..., string Teor, string? TeorPseudonimizado, ManifestanteDto? Manifestante,
    bool IdentidadeRestrita, ReferidoDto? Referido, Guid? EnvolvidoPractitionerId, string? EnvolvidoDescricao, Guid? AssuntoId,
    Guid? SubassuntoId, Guid? UnidadeId, Guid? PontoRespostaId, Guid? RegulacaoSolicitacaoId, string? ProtocoloExterno,
    string? SistemaExterno, DateOnly? DataFato, string? LocalFato, OuvidoriaOrigem Origem, DateTime? ProrrogadoEm,
    string? ProrrogacaoJustificativa, bool ComplementacaoUsada, DateTime? EncaminhadaEm, DateTime? RespondidaEm, DateTime? ConcluidaEm,
    OuvidoriaResolutividade? Resolutividade, OuvidoriaSituacaoFinal? SituacaoFinal, OuvidoriaMotivoNaoAtendimento? MotivoNaoAtendimento,
    OuvidoriaMotivoArquivamento? MotivoArquivamento, string? RespostaConclusiva, DateTime? HabilitadaEm, Guid? ResponsavelId,
    IReadOnlyList<Guid> MarcadorIds, IReadOnlyList<string> PossiveisDuplicatas, IReadOnlyList<EventoDto> Eventos,
    IReadOnlyList<AnexoDto> Anexos, IReadOnlyList<string> AcoesPermitidas /* nomes das ações válidas para o status+perfil */);
record RegistrarManifestacaoRequest(OuvidoriaTipo Tipo, OuvidoriaIdentificacao Identificacao, OuvidoriaCanal Canal, OuvidoriaOrigem Origem,
    string Teor, string? Resumo, Guid? AssuntoId, Guid? SubassuntoId, Guid? UnidadeId, DateOnly? DataFato, string? LocalFato,
    ManifestanteDto? Manifestante, ReferidoDto? Referido, string? EnvolvidoDescricao, string? ProtocoloExterno, string? SistemaExterno,
    Guid? RegulacaoSolicitacaoId, IReadOnlyList<AnexoRef> Anexos);
record AnexoRef(Guid MidiaId, string NomeArquivo);
record RegistrarManifestacaoPublicaRequest(OuvidoriaTipo Tipo, OuvidoriaIdentificacao Identificacao, string Teor, Guid? AssuntoId,
    Guid? UnidadeId, DateOnly? DataFato, string? LocalFato, ManifestanteDto? Manifestante, ReferidoDto? Referido, string? EnvolvidoDescricao);
record TriarRequest(OuvidoriaTipo? Tipo, Guid? AssuntoId, Guid? SubassuntoId, OuvidoriaPrioridade? Prioridade, Guid? UnidadeId,
    string? Resumo, Guid? ResponsavelId, Guid? RegulacaoSolicitacaoId, IReadOnlyList<Guid>? MarcadorIds);
record EncaminharRequest(Guid PontoRespostaId, int? PrazoDias, string? Texto, string? TeorPseudonimizado);
record TextoRequest(string Texto);
record TextoComAnexosRequest(string Texto, IReadOnlyList<AnexoRef> Anexos);
record ResponderCidadaoRequest(string Texto, bool Conclusiva, OuvidoriaResolutividade? Resolutividade,
    OuvidoriaSituacaoFinal? SituacaoFinal, OuvidoriaMotivoNaoAtendimento? MotivoNaoAtendimento);
record ArquivarRequest(OuvidoriaMotivoArquivamento Motivo, string? Texto);
record EncaminharExternoRequest(string SistemaExterno, string? ProtocoloExterno, string Texto);
record OuvidoriaResumoDto(int Registradas, int EmTriagem, int Encaminhadas, int AguardandoComplementacao, int AguardandoValidacao,
    int Atrasadas, int AreaAtrasadas, int EmRecurso, int MeuPonto /* encaminhadas aos pontos do usuário */);
record PontoRespostaDto(Guid Id, string Nome, OuvidoriaTipoPontoResposta Tipo, Guid? UnidadeId, string? UnidadeNome, int? PrazoDias,
    bool Ativo, IReadOnlyList<PontoRespostaMembroDto> Membros, int Pendentes /* encaminhadas em aberto */);
record PontoRespostaMembroDto(Guid UsuarioId, string Nome, bool Titular);
record SalvarPontoRespostaRequest(string Nome, OuvidoriaTipoPontoResposta Tipo, Guid? UnidadeId, int? PrazoDias, bool Ativo,
    IReadOnlyList<SalvarMembroRequest> Membros);
record SalvarMembroRequest(Guid UsuarioId, bool Titular);
record AssuntoDto(Guid Id, Guid? PaiId, string Nome, string? CodigoOuvidorSus, int Ordem, bool Ativo);
record SalvarAssuntoRequest(Guid? PaiId, string Nome, string? CodigoOuvidorSus, int Ordem, bool Ativo);
record MarcadorDto(Guid Id, string Nome, bool Ativo);  record SalvarMarcadorRequest(string Nome, bool Ativo);
record OuvidoriaConfiguracaoDto(int PrazoCidadaoDias, int ProrrogacaoDias, int PrazoAreaDias, int PrazoAreaAltaDias,
    int PrazoAreaUrgenteDiasUteis, int ComplementacaoDias, int ArquivamentoAutomaticoDias, bool NotificarPorWhatsApp, string? TextoRecibo);
record OuvidoriaPainelDto(int Total, IReadOnlyList<ContagemDto> PorTipo, IReadOnlyList<ContagemDto> PorStatus, IReadOnlyList<ContagemDto> PorCanal,
    IReadOnlyList<ContagemDto> PorAssunto, IReadOnlyList<ContagemDto> PorUnidade, int Respondidas, int NoPrazo, int ForaPrazo,
    double? TempoMedioDias, double? TempoMedioAreaDias, int Estoque, int Resolvidas, int NaoResolvidas,
    IReadOnlyList<ContagemDto> FaixasPrazo /* ate30 | 31a60 | mais60 */);
record ContagemDto(string Chave, string Rotulo, int Quantidade);
record AcompanhamentoDto(string Protocolo, OuvidoriaTipo Tipo, OuvidoriaStatus Status, DateTime RegistradaEm, DateOnly PrazoRespostaEm,
    bool Prorrogada, string? RespostaConclusiva, OuvidoriaResolutividade? Resolutividade, bool PodeComplementar, bool PodeRecorrer,
    IReadOnlyList<EventoPublicoDto> Eventos);
record EventoPublicoDto(OuvidoriaTipoEvento Tipo, string? Texto, DateTime CriadoEm);
```
`PaginaDto<T>`: usar o genérico de paginação já existente no repo se houver; senão `record PaginaDto<T>(IReadOnlyList<T> Itens, int Total, int Pagina, int Tamanho)`.

---

## 4. Front (`SMSMais.front/src/features/ouvidoria/`)

Estrutura: `types.ts` (espelha §3.3, enums como union de strings), `api/ouvidoriaApi.ts`, `api/queries.ts` (`ouvidoriaKeys`), `lib/rotulos.ts` (rótulos pt-BR de todos os enums + cores de status), `lib/acoes.ts` (quais ações por status), `components/` (`StatusManifestacaoBadge`, `TipoBadge`, `PrazoChip` (verde/amarelo/vermelho), `LinhaDoTempo`, `RegistrarManifestacaoForm`, `AcoesManifestacao` (drawer com o formulário da ação escolhida), `ManifestanteCard` (com botão "Revelar identidade" que pede justificativa), `SeletorAssunto` (dois níveis), `SeletorPontoResposta`, `SeletorUnidade`), `pages/` (`OuvidoriaFilaPage` (abas: Triagem · Em andamento · Aguardando validação · Atrasadas · Recurso · Concluídas · **Meu ponto**), `RegistrarManifestacaoPage`, `ManifestacaoDetalhePage`, `PontosRespostaPage`, `AssuntosPage`, `ConfiguracaoOuvidoriaPage`, `PainelOuvidoriaPage` (cards + `recharts` simples)).

Registro nos 4 pontos (ver `referencias/05` §13): `authStore.ts` (4 literais), `menuConfig.ts` (seção **Ouvidoria** com ícone `Megaphone`: Fila `/app/ouvidoria`, Registrar `/app/ouvidoria/registrar`, Meu ponto `/app/ouvidoria/meu-ponto` (módulo `OuvidoriaPontoResposta`), Pontos de resposta, Assuntos, Painel, Configuração (módulo `OuvidoriaGestao`)), `AppRouter.tsx` (rotas sob `RotaComModulo`), `perfis/lib/acoes.ts` (4 módulos com rótulo + apelidos por ação da tabela §1.5). Rodar a skill `sincronizar-permissoes` no fim para conferir.

Comportamentos obrigatórios da UI: **nunca** mostrar dados do manifestante quando `identidadeRestrita`; botão "Revelar identidade" só com `OuvidoriaSigilo`, exige justificativa e avisa "este acesso fica registrado"; formulário de registro impõe as regras de identificação × tipo em tempo real; resposta conclusiva mostra a **ajuda de campo** com o conteúdo mínimo por tipo (art. 29); ao registrar, exibir o protocolo e o **código de acesso uma única vez** com botão copiar e aviso; erros 4xx inline com `extrairMensagemDeErro`.

---

## 5. Testes (`tests/SMSMais.Tests/Ouvidoria/`)

`OuvidoriaPrazosTests` (puros): prazo cidadão; prazo área por prioridade; urgente pula fim de semana; atraso; faixa. `OuvidoriaManifestacaoServiceTests` (PostgresFixture): registrar gera protocolo `AAAA-NNNNNN` e código de 8 caracteres, hash salvo e código não salvo; solicitação anônima → `ValidacaoException`; denúncia anônima sem código; encaminhar exige ponto ativo e calcula `PrazoAreaEm`; denúncia sem habilitar não encaminha; complementação suspende e retoma o prazo, segunda vez → `ConflitoException`; prorrogar só uma vez e proibido após externo; responder conclusiva exige resolutividade e grava `DiasAteResposta`; recurso só uma vez; arquivar duplicidade exige texto; revelar identidade grava `OuvidoriaAcessoIdentidade` e auditoria; ponto de resposta não recebe manifestante no DTO; público: código errado → `NaoEncontradoException`.

---

## 6. Tarefas e ordem

| # | Tarefa | Depende de | Verificação |
|---|---|---|---|
| T1 | Data: enums, entidades, configurations, DbSets, sequência, `ModuloPermissao` 71–74, migration `Ouvidoria` | — | `dotnet build` (só o erro pré-existente de `WhatsAppCliente.cs` pode restar); `Up()` da migration só com `ouvidoria_*` + sequência |
| T2 | Core: services, DTOs, validators, prazos, protocolo, notificador, rotinas, DI | T1 | `dotnet build` |
| T3 | Api: `OuvidoriaController`, `OuvidoriaPublicoController`, rate limit | T2 | `dotnet build`; `/openapi/v1.json` lista as rotas |
| T4 | Front: feature completa + 4 registros | contrato (§3) | `VITE_API_BASE_URL=x npm run build`; `npm run lint` |
| T5 | Testes | T2 | `dotnet test --filter Ouvidoria` (bancada ou Docker) |
| T6 | Docs: `docs/architecture.md` (módulo), `SMSMais.ouvidoria/PROGRESSO.md`, `Aprendizados e Scratchpads/INDICE.md` se criar ferramenta | T1–T5 | — |

Nada é commitado sem OK do Bernardo. O snapshot do EF e a migration ficam para um commit separado.
