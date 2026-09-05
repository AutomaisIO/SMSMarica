# 12 — Escrita no SER e no SERNIT: envio, anexo, follow-up e telefone pelo agente (incremento 5; começa pelo spike a)

## Objetivo

Trocar o "Registrar envio" assistido do incremento 3 pelo **envio automático** ao SER/SERNIT: subir os anexos, gravar a solicitação com a credencial pessoal do agente, capturar o número e confirmar por releitura. E fechar o ciclo das pendências: follow-up e telefone respondidos pela ponta são escritos pelo agente, também com a credencial dele. O SER já tem escrita de follow-up e telefone em produção; falta o Gravar e o anexo — nunca exercitados — daí o spike a primeiro.

## Requisitos cobertos

- **R-11 (A9)** enviar ao sistema de regulação com a senha do agente; número externo.
- **R-13 (A11)** agente submete follow-up e telefone vindos da ponta.
- **R-14 (A12)** documento criticado → novo arquivo na mesma caixinha e reenvio.
- **R-15 (A13)** SER/SERNIT registram "gestor SMS Maricá"; só nós sabemos a origem.

## Decisões aplicadas

**D-4** (marco), **D-1/D-2** (credencial persistida; modal como fallback).

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Tela de criação mapeada (bloco fixo, campos dinâmicos por recurso, ids voláteis, Gravar `form0:j_id313`) | sim | `docs/ser-criar-solicitacao.md` §2.1–2.2; `SerNovaSolicitacaoService` (`ObterFormularioAsync`, `ListarRecursosAsync`, `ObterCamposDinamicosAsync`, `SugerirCidsAsync`, `PesquisarPacienteAsync`) |
| Protocolo do anexo | documentado, não exercitado | `docs/ser-criar-solicitacao.md` §2.3: form `formAnexar` (multipart), `rich:fileUpload` `formAnexar:upload:file`, botão `formAnexar:j_id320`, POST por arquivo em `solicitar-consulta-editar.seam?_richfaces_upload_uid=…&AJAXREQUEST=_viewRoot`, `maxFileBatchSize: 2`, `noDuplicate: true`; lista em `form0:anexoList`; **o arquivo sobe antes do Gravar**, em conversa Seam amarrada pela sessão |
| Escrita de follow-up e telefone com confirmação por releitura | sim, em prod | `SerEscritaService.RegistrarFollowUpAsync / LerContatosAsync / AlterarContatosAsync` (`GravarEventosNovosAsync` confirma); `SernitEscritaService` |
| Trava de somente-leitura em duas camadas com liberação nominal | sim | `SerWebSessao.cs:336-381`; bypass `SubmeterFormEscritaAsync` usado pelo follow-up |
| Sessão do operador (memória) e fábrica injetável | sim | `SerSessaoOperadorStore` (vira `SessaoOperadorIntegracaoStore`, plano 07) |
| Sondas do lab | sim | `Automais.SER/probe_criar_solicitacao.py` (modo ensaio, flag `--gravar` nunca acionada), `probe_reabrir_solicitacao.py` (menu de ações por situação), `probe_followup.py` |
| SERNIT: diferenças documentadas | sim | `SernitNovaSolicitacaoService.cs:13-29` (sem gate AE, `AJAXREQUEST=_viewRoot`, `campoDinamicoBox` plano, CID em `form0:procedimento`); `Automais.SERNIT/docs/APRENDIZADOS.md` (Editar tem action próprio `solicitar-consulta-editar.seam`; CPF obrigatório para gravar; FollowUP `j_id142`) |
| Dublê de sessão | privado | `SessaoFalsa : ISerWebSessao` em `tests/…/SerSessaoOperadorStoreTests.cs:127` → promover a `Infraestrutura/SerWebSessaoFake.cs` gravando os POSTs |
| Mapa de campos canônico → nativo | plano 02 | `regulacao_formulario_campo_mapa` |

O que **não** existe: `POST …/enviar`; acionamento do Gravar; upload; testes do SERNIT.

## Desenho

### Fase 1 — Spike a (plano 13)

Gravar + anexo numa solicitação reversível, com credencial pessoal, uma vez, tudo capturado. Saída: `revisoes/spike-a-ser-gravar.md` (ids do Gravar e do upload, resposta do Gravar — redirect com `cid`? —, formato do número, se Cancelar está disponível na situação inicial, e se o anexo pode ser adicionado **depois** do Gravar pela aba Editar — necessário para o documento criticado).

### Fase 2 — `RegulacaoEnvioSerService` / `RegulacaoEnvioSernitService` (`Core/Regulacao/Envio/`)

`EnviarAsync(RegulacaoSolicitacao s, ISerWebSessao sessaoDoAgente)`:
1. Traduz `formulario_json.canonico` → campos nativos do SER (ou SERNIT) pelo `regulacao_formulario_campo_mapa`; valores múltiplos via `SerValorMultiplo`; datas dd/MM/yyyy; CID = `valor` da lista do recurso.
2. Abre a aba Editar (`form0:editar_server_submit`), aplica o "ambulatório estadual" do ramo da origem, tipo, recurso; pesquisa o paciente por CNS/CPF (`numeroCADSUS`) — SERNIT exige CPF.
3. **Anexos antes do Gravar**: para cada arquivo `Atual` das caixinhas, POST multipart conforme §2.3; conferir em `form0:anexoList` que apareceu; gravar `enviado_ao_sistema_em` no arquivo.
4. Preenche bloco fixo + dinâmicos; aciona Gravar (id lido do form, nunca fixo).
5. Captura o número (da resposta ou da tela seguinte); **confirmação por releitura** do histórico da solicitação ("Registrado!" não é prova); falha na confirmação → `FalhaEnvio` sem gravar número.
6. Devolve número; o service da fila grava `numero_externo`, `enviado_por`, `credencial_usada_id`, `operador_externo_login`, estado `EnviadaAoSistema`, eventos.

Liberação nominal na trava de `SerWebSessao` para os parâmetros do Gravar e do upload (a trava continua barrando tudo o mais). Mesmo para o SERNIT.

### Fase 3 — Pendências (plano 06) pelo agente

- Telefone: `AlterarContatosAsync` com a sessão do agente; releitura confirma.
- Follow-up: `RegistrarFollowUpAsync`; o texto inclui a identificação da ponta ("UBS X, operador Y: …") porque no SER só aparece o nome do agente — quem pediu tem de estar no texto (regra já registrada em `docs/ser.md` §9).
- Documento criticado: nova versão na caixinha → upload pela aba Editar da solicitação existente (se o spike a confirmar que é possível após o Gravar); senão, follow-up avisando que o documento está disponível e caminho alternativo combinado com a central.

### Endpoint

`POST /regulacao/solicitacoes/{id}/enviar-sistema` body `{sistema: Ser|Sernit}` (48 Edição): valida destino permitido (ressalva), `Exigir(sistema)` (plano 07; `credencial.ausente` → modal com "salvar no meu perfil"), trava `EnviandoAoSistema`, chama o service, devolve número ou erro legível. Retentativa após `FalhaEnvio` só depois de "Conferir duplicidade" (busca no SER por CNS + recurso + data pela `SerConsultaDiretaService`).

## Tarefas

- [ ] **5.5** Spike a executado e relatório lido; este plano revisado.
- [ ] **5.6** `RegulacaoEnvioSerService` + `RegulacaoEnvioSernitService` (tradução, anexos antes, Gravar, captura, releitura) + liberação nominal + endpoint + trava + conferência de duplicidade; "Registrar envio" continua disponível como alternativa manual.
- [ ] **5.7** Aprovação de pendências pelo agente (plano 06) usando `SerEscritaService`/`SernitEscritaService` com a sessão do agente; texto de follow-up com a identificação da ponta; nova versão de documento.
- [ ] **5.8** `SerWebSessaoFake` promovido para `tests/SMSMais.Tests/Infraestrutura/` (grava os POSTs recebidos, devolve HTML de fixture); testes: ordem anexo → Gravar; releitura obrigatória; falha silenciosa (200 sem número) não grava local; SERNIT sem CPF recusa antes de tocar o sistema. Primeiros testes do SERNIT (`SernitHtmlParserTests`, `SernitNovaSolicitacaoTests`).
- [ ] **5.9** Promover `adr/0053` e `adr/0054`.

## Dependências

Planos 04, 07, spike a. **OK explícito** para o spike e para o primeiro envio real.

## Riscos e pontos a confirmar

- O SER recusou o Gravar via robô em 08/2026 ("bloqueado até no navegador"): pode ser validação de negócio (paciente, unidade solicitante) e não protocolo. O spike a captura `form0:messages` e para.
- O anexo é "duas conversas Seam amarradas pela sessão" — o passo mais provável de falhar em silêncio. Verificar `form0:anexoList` **sempre**.
- Ids `j_id…` mudam quando a SES recompila: localizar por rótulo/estrutura, nunca fixar (lição de `SerFormDeModuloTests`).
- `SerWebSessao` em prod com aviso pendente na home derruba a integração (incidente 28/08): o service precisa devolver a mensagem "há aviso pendente no SER; alguém precisa lê-lo" em vez de falha genérica.
- Múltiplas sessões simultâneas são OK no SER (medido 08/08) — não há a arbitragem do SISREG.

## Testes

Tarefa 5.8 + manual (com OK): enviar uma solicitação de teste ao SER; conferir número, anexo e histórico no SER pelo navegador; conferir `regulacao_evento` e a notificação da UBS.

## Fora de escopo

SISREG (11); cancelamento pelo SMSMais; edição de solicitação já enviada além de telefone/follow-up/anexo.

---

## Especificação para execução

> **Pré-condição:** `revisoes/spike-a-ser-gravar.md` existe. Até lá só a estrutura (interfaces, tradução, fake) pode ser escrita; o acionamento do Gravar/upload depende dos ids e do comportamento reais.

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `Automais.SER/probe_criar_solicitacao.py` | estender: `--anexo <arquivo>` (upload pelo `formAnexar` antes do Gravar) e `--gravar` (já existe, nunca acionado); salvar todas as respostas em `capturas/gravar/` |
| `SMSMais.Core/Integracoes/SerWeb/SerWebSessao.cs` | liberação nominal na trava (§7 de `docs/ser.md`): operações `"gravar_solicitacao"` e `"anexar_arquivo"`; novo método `Task<RespostaSer> EnviarArquivoAsync(string caminhoUpload, string nomeCampo, string nomeArquivo, string contentType, byte[] conteudo, CancellationToken ct)` (multipart, `AJAXREQUEST=_viewRoot`, cookies da mesma sessão) |
| `SMSMais.Core/Integracoes/SerWeb/SerHtmlParser.cs` | `BotaoGravar(html)`, `FormAnexar(html)` (ids voláteis por estrutura/rótulo), `ListaAnexos(html)`, `NumeroGerado(html)` |
| `SMSMais.Core/Integracoes/SernitWeb/SernitWebSessao.cs`, `SernitHtmlParser.cs` | espelho (action próprio `solicitar-consulta-editar.seam`; `campoDinamicoBox` plano) |
| `SMSMais.Core/Regulacao/Envio/IRegulacaoEnvioSistema.cs` (interface comum), `RegulacaoEnvioSerService.cs`, `RegulacaoEnvioSernitService.cs`, `Modelos/EnvioModelos.cs` | criar |
| `SMSMais.Core/Regulacao/Solicitacoes/RegulacaoSolicitacaoService.cs` | `EnviarAoSistemaAsync(id, sistema)` resolve `IRegulacaoEnvioSistema` por `sistema` (`IEnumerable<IRegulacaoEnvioSistema>` + `Sistema` property) |
| `SMSMais.Core/Regulacao/Pendencias/RegulacaoPendenciaService.cs` | `AprovarESubmeterAsync` usa `ISerEscritaService`/`ISernitEscritaService` com a sessão do agente e `IRegulacaoEnvioSistema.AnexarEmSolicitacaoExistenteAsync` |
| `SMSMais.Core/DependencyInjection.cs` | registrar os dois serviços de envio |
| `tests/SMSMais.Tests/Infraestrutura/SerWebSessaoFake.cs` (promovido de `SessaoFalsa` em `SerSessaoOperadorStoreTests.cs:127`), `SernitWebSessaoFake.cs` | criar |
| `tests/SMSMais.Tests/Regulacao/Envio/*.cs`, `tests/SMSMais.Tests/Integracoes/Sernit/*.cs` | criar |

### B. Interface comum de envio

```csharp
public sealed record EnvioResultado(string NumeroExterno, DateTime EnviadoEm, string OperadorLogin, IReadOnlyList<Guid> ArquivosEnviados, string ConfirmacaoResumo);
public interface IRegulacaoEnvioSistema
{
    SistemaRegulacao Sistema { get; }
    Task<EnvioResultado> EnviarAsync(RegulacaoSolicitacao s, object sessaoOperador, CancellationToken ct);            // sessaoOperador: ISerWebSessao | ISernitWebSessao
    Task<bool> AnexarEmSolicitacaoExistenteAsync(RegulacaoSolicitacao s, RegulacaoExigenciaArquivo arquivo, object sessaoOperador, CancellationToken ct);  // false se o sistema não permite após o Gravar (spike a decide)
    Task<IReadOnlyList<string>> ProcurarDuplicidadeAsync(RegulacaoSolicitacao s, object sessaoOperador, CancellationToken ct);   // números encontrados por CNS + recurso + data
}
```

### C. `RegulacaoEnvioSerService.EnviarAsync` — sequência

1. `formulario = IRegulacaoFormularioService.TraduzirAsync(s.FormularioVersaoId, Ser, s.Formulario.canonico)` → dicionário `nomeNativo → valor` (bloco fixo + dinâmicos; `SerValorMultiplo` já é aplicado pela transformação `multiplo_quebra_linha`).
2. `html = sessao.AbrirTelaPesquisaAsync` → `SubmeterFormAsync(html, "form0", {"form0:editar_server_submit": "…"})` (aba Editar); aplicar "ambulatório estadual" (`form0:comboSisReg`) conforme o `Ramo` da origem SER do canônico; `comboTipoRecurso`, `comboRecurso`/`suggRecurso` (o `valor` da origem) — cada mudança é um A4J como em `SerNovaSolicitacaoService` (reusar os métodos privados; promover a `internal`).
3. Paciente: `numeroCADSUS = s.PacienteCns ?? s.PacienteCpf` → botão Pesquisar; conferir nome retornado ≈ `s.PacienteNome` (normalizado); divergência → `ValidacaoException("ser.paciente_divergente", …)`.
4. **Anexos antes do Gravar**: para cada `RegulacaoExigenciaArquivo` com `Situacao == Atual` e `EnviadoAoSistemaEm == null`: `sessao.EnviarArquivoAsync(caminhoUpload, "formAnexar:upload:file", nome, contentType, bytes)` (bytes de `IArquivoExigenciaStore.LerAsync`); reler a tela e confirmar em `SerHtmlParser.ListaAnexos` pelo nome; sucesso → `EnviadoAoSistemaEm = now`; falha → aborta o envio (`ser.anexo_nao_confirmado`) **antes** de gravar.
5. Preencher campos; `SubmeterEscritaAsync(html, "form0", campos + {BotaoGravar: "Gravar"}, viewState, "gravar_solicitacao")`.
6. `numero = SerHtmlParser.NumeroGerado(resposta)` (ou da tela seguinte após o redirect com `cid`, armadilha já conhecida da busca); nulo → `ser.gravar_sem_numero` com `form0:messages` na mensagem.
7. **Releitura**: `ISerLeitorService` lê a solicitação por número (histórico) e confere evento "Solicitar" + paciente; divergência → `ser.confirmacao_divergente`.
8. Devolve `EnvioResultado`. O `RegulacaoSolicitacaoService` grava número/estado/eventos e chama `RegistrarUsoAsync`.

SERNIT: mesma sequência com `ISernitWebSessao`; sem `comboSisReg`; CPF obrigatório (`s.PacienteCpf` nulo → `ValidacaoException` **antes** de abrir a sessão); action próprio da aba Editar.

### D. Ganchos no service da fila

`EnviarAoSistemaAsync(id, sistema)`: solicitação `EmAnalise`; `sistema ∈ DestinosPermitidos ∪ DestinosComRessalva`; `sessao = await store.ExigirSerAsync()` (ou Sernit); transição `EnviandoAoSistema` (`ExecuteUpdateAsync … WHERE status = 3`); `envio.EnviarAsync`; sucesso → `EnviadaAoSistema` + `NumeroExterno` + `EnviadoPorUsuarioId` + `OperadorExternoLogin` + `CredencialUsadaId`; exceção → `FalhaEnvio` com `StatusMotivo` e evento; sempre `RegistrarUsoAsync`. "Conferir duplicidade" → `ProcurarDuplicidadeAsync` → evento com os números achados; se achou, o agente pode "Registrar envio" com o número existente em vez de reenviar.

### E. Front

Botão "Enviar ao sistema" no `AcoesAgente`: `Select` do sistema (só os permitidos; ressalva em amarelo), confirmação, `useSessaoIntegracaoObrigatoria(provedor)`; progresso ("subindo anexos… gravando… confirmando…") via estado do botão; erro exibe o código traduzido (`ser.anexo_nao_confirmado`, `ser.gravar_sem_numero`, `ser.confirmacao_divergente`, `ser.paciente_divergente`, `sernit.cpf_obrigatorio`, `ser.aviso_pendente_na_home` — este último quando `SerWebSessao` detectar a home sem `goModulo`, incidente 28/08). Estado `FalhaEnvio`: botões "Conferir duplicidade" e "Tentar de novo" (habilitado só após conferência).

### F. Testes

`SerWebSessaoFake`: recebe uma fila de respostas HTML (fixtures de `docs/ser-criar-solicitacao.md` e das capturas do spike a) e grava `(formId, campos, operacao)` de cada `SubmeterEscritaAsync`/`EnviarArquivoAsync`. `RegulacaoEnvioSerServiceTests`: `Anexos_sobem_antes_do_gravar`; `Anexo_nao_confirmado_aborta_antes_de_gravar`; `Gravar_sem_numero_falha`; `Releitura_divergente_falha_e_nao_grava_numero`; `Traducao_usa_nomes_nativos`; `Paciente_divergente_para`. `RegulacaoEnvioSernitServiceTests`: `Sem_cpf_recusa_antes_de_abrir_sessao`; `Action_da_aba_editar_e_o_proprio`. `RegulacaoSolicitacaoServiceEnvioTests`: `Duplo_clique_nao_envia_duas_vezes`; `Falha_exige_conferencia`; `Ressalva_permite_so_o_destino_elegivel`. `SernitHtmlParserTests`, `SernitNovaSolicitacaoTests` (primeiros testes do SERNIT).

### G. Passo a passo

1. Spike a (com OK) → relatório → ajustar §C (ids, redirect, anexo pós-Gravar).
2. `SerWebSessaoFake`/`SernitWebSessaoFake` promovidos → testes existentes continuam verdes.
3. Liberação nominal + `EnviarArquivoAsync` + parsers → testes com fixtures.
4. `IRegulacaoEnvioSistema` + SER + SERNIT + DI → testes.
5. Ganchos no service da fila + pendências (plano 06) → testes.
6. Front → `npm run build`.
7. Primeiro envio real com OK, uma solicitação de teste, conferida no SER pelo navegador.
8. `PROGRESSO.md` 5.5–5.8.

### H. Critério de pronto

Envio de teste ao SER: número capturado, anexo na lista do SER, histórico relido, evento local e notificação da UBS; duplo clique não duplica; SERNIT sem CPF recusa antes de tocar o sistema; pendência de telefone aprovada pelo agente aparece no SER com o prefixo da UBS.
