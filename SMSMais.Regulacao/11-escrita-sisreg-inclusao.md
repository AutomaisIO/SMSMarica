# 11 — Escrita no SISREG: inclusão de solicitação (incremento 7; começa pelo spike b)

## Objetivo

Incluir solicitações ambulatoriais no SISREG a partir do SMSMais por dois caminhos: (1) **Interno comum** — o próprio solicitante da UBS, no "Enviar" do wizard, com a credencial pessoal dele (D-8); (2) **NAR** — o agente regulador, no envio da fila, com a credencial de unidade (CNES + senha padrão ou específica) da unidade "em nome de" (D-9). Tudo o que hoje se sabe sobre a tela de inclusão é um mapa por GET; por isso este plano **começa pelo spike b** e só depois vira código.

## Requisitos cobertos

- **R-04 (A3)** formulário = tela de inclusão do SISREG ipsis litteris.
- **R-15 (A13)** inclusão gera número editável ~7 dias; registra operador e unidade que deram entrada; OK do agente só põe em fila.
- **R-17 (A14)** NAR autentica com a credencial da unidade.
- **R-18 (A14)** unidade de origem do usuário multi-unidade.

## Decisões aplicadas

**D-4** (SISREG depois do spike), **D-8**, **D-9**, **D-2** (modal se não houver credencial), **D-1** (credencial só com o humano logado).

## O que já existe e será reaproveitado

| Necessidade | Existe | Onde |
|---|---|---|
| Mapa por GET da inclusão | sim | `Automais.SISREG/docs/APRENDIZADOS.md` § "Subsídio para escrita" (linhas 339-367): `cadweb50?url=/cgi-bin/marcar` → `marcar` (`pa`, `cid10`, `cpfprofsol`/`nomeprofsol`, `ret`, `upsexec`) → `ProximaEtapa()`: grupo `…000` → `LST_ITENS_PA`; item → `LST_VAGAS`; gravação a partir de vagas. Outras escritas mapeadas: `gerenciador_solicitacao` (`CANCELAR_SOLICITACAO`, `REENVIAR_REGULACAO`), `cons_verificar` (`EXCLUIR_SOLICITACAO`) |
| Cliente Python do lab (login sha256(upper), cookies, sessão reaproveitada) | sim | `Automais.SISREG/sisreg/client.py` |
| Sessão .NET com login, gate por login, orçamento, detecção de CAPTCHA e sessão caída | sim | `SMSMais.Core/Integracoes/SisregWeb/SisregWebSessao.cs`, `SisregOrcamentoRequisicoes.cs` (`Registrar`, `Restante`, `EsperaAteLiberar`; `CodigoCaptcha`, `EhCaptcha`) |
| Escopo de unidade por consulta (não por login) | sim | `SisregUnidadeAtual.cs` |
| CNES das unidades | sim | `unidade.cnes` (auto-criação por CNES na importação) |
| Consulta CADSUS por CNS/CPF | sim | `ConsultaCnsService` (`cadweb50`, `etapa=DETALHAR`) |
| Catálogo de procedimentos `pa` e escalas por unidade | sim | `sisreg_procedimento_sigtap`, `sisreg_escala`, `sisreg_profissional_unidade` |
| Credenciais pessoais e por unidade | plano 07 | `usuario_credencial_integracao` (`Pessoal`, `UnidadesPadrao`, `Unidade`), `SessaoOperadorIntegracaoStore` |
| Padrão "escrita confirmada por releitura" | sim | `SerEscritaService` |

O que **não** existe: qualquer POST de escrita no SISREG pelo server (`SisregController`: "só-leitura"); captura dos campos de `LST_VAGAS`; prova de permissão de gravar; sessão SISREG por operador humano.

## Desenho

### Fase 1 — Spike b (plano 13)

Perguntas que travam o código: campos por etapa e por tipo de procedimento; permissão de gravar do perfil solicitante; janela editável (existe? dias? o que é editável? por quem?); reversão (cancelar/excluir); follow-up na tela do solicitante; seleção da unidade/CNES (login ou `marcar`/`upsexec`); credencial padrão do agente enxerga várias unidades; CAPTCHA por operador ou por IP; custo em requisições de uma inclusão. Saída: `revisoes/spike-b-sisreg-marcar.md` + HTMLs em `capturas/`. **Nenhuma linha de código de produto antes desse relatório.**

### Fase 2 — Motor (`SisregInclusaoService`, `Core/Integracoes/SisregWeb/Inclusao/`)

- `ISisregSessaoFactory.ParaOperador(login, senha, cnes?)` (plano 07): sessão própria, gate e orçamento por login; **nunca** a sessão institucional.
- `IncluirAsync(RegulacaoSolicitacao s, CredencialResolvida c)`:
  1. abre sessão (login; se o spike mostrar seleção de unidade pós-login, seleciona o CNES);
  2. `cadweb50?url=/cgi-bin/marcar` com CNS/CPF do paciente → contexto do paciente;
  3. `marcar` com `pa` (da origem SISREG do procedimento canônico), `cid10`, profissional solicitante (`cpfprofsol`/`nomeprofsol` do formulário), `ret`, e os campos que o spike revelar; para grupo → `LST_ITENS_PA` e escolha do item; → `LST_VAGAS`;
  4. gravação conforme a tela de vagas (o spike diz se há "solicitar sem vaga" / fila de regulação — é o que queremos: **não** marcar vaga direto, e sim solicitar para regulação; se a tela só permitir marcar vaga, isso é decisão a levar ao Bernardo);
  5. captura do número (`co_solicitacao`) da resposta;
  6. **confirmação por releitura**: consultar a solicitação (`cons_verificar`/`gerenciador_solicitacao`) e conferir paciente + procedimento;
  7. logout explícito quando a credencial é de unidade (NAR).
- Parsers dos HTMLs de cada etapa com testes sobre as capturas do spike (mesmo idioma de `CadsusHtmlParser` + `CadsusHtmlParserTests`).
- Orçamento: cada inclusão custa N requisições (medido no spike); `SisregOrcamentoRequisicoes` por login; CAPTCHA → `bloqueada_ate` na credencial (plano 07), `FalhaEnvio(captcha)`, instrução ao usuário; se o spike mostrar CAPTCHA por IP, isso bloqueia todos e vira pausa global com aviso ao operador (`INotificadorSincronismo`).
- **Idempotência**: antes de re-tentar após `FalhaEnvio`, conferir no SISREG (por CPF do paciente + `pa` + data) se a solicitação já existe; a inclusão não tem chave natural antes do número.
- Formulário Interno passa a ser gerado do mapa real (`regulacao_formulario_versao` esquema `sisreg.inclusao`), cumprindo R-04.

### Fase 3 — Os dois chamadores

**Interno comum (D-8)** — no `enviar-fila` do plano 04, quando `fluxo = Interno`:
1. `SessaoOperadorIntegracaoStore.Exigir(Sisreg)` → credencial `Pessoal` do solicitante (D-2: sem credencial → modal → opção de salvar);
2. aviso "vamos entrar no SISREG como LOGIN; sua sessão no navegador cairá" (uma vez por sessão);
3. `IncluirAsync`; sucesso → `numero_externo`, `sisreg_editavel_ate = agora + sisreg_prazo_edicao_dias`, `operador_externo_login`, `credencial_usada_id`, estado `PendenteRegulacao`, eventos `EnvioSistema` + `NumeroExterno`; falha → continua `Rascunho`, evento `FalhaEnvio` com motivo legível.
4. Na fila, o agente vê "já no SISREG nº X, editável até dd/mm". O **OK do agente** faz o que o spike b indicar (só local, ou `REENVIAR_REGULACAO`/confirmação); ajustes do agente dentro da janela → edita no SISREG com a credencial de unidade dele para aquela unidade (se existir; se não, vira pendência `ComplementacaoAgente` para a ponta aplicar com a credencial própria).

**NAR (D-9)** — no `enviar-sistema` do agente, quando `fluxo = Nar`:
1. `Exigir(Sisreg, unidadeId = unidade_em_nome_de)` → `Unidade` (se `usar_padrao = false`) ou `UnidadesPadrao` com o CNES da unidade; ausente → erro "cadastre a credencial desta unidade em Minhas credenciais";
2. aviso de sessão; `IncluirAsync`; logout ao fim;
3. sucesso → `EnviadaAoSistema`, `numero_externo`, trilha completa (origem: unidade e usuário do NAR; em nome de; agente; login usado) — R-17.

### Segurança de escrita

`SisregWebSessao` hoje é somente-leitura por construção (o controller diz "o SISREG não aceita escrita por esta API"). A escrita entra por uma classe separada (`SisregSessaoOperador`) que só a fábrica por operador cria — a sessão institucional continua sem método de POST de escrita. Liberação nominal por URL (`marcar`, `LST_ITENS_PA`, `LST_VAGAS`, `gerenciador_solicitacao`) e teste que garante que a sessão institucional não consegue chamá-las.

## Tarefas

- [ ] **7.1** Spike b executado (plano 13) e relatório lido; README §9 questões 1, 2, 3, 6, 7 respondidas; este plano revisado com o que o spike mostrou (registrar em "Desvios do plano" se algo mudar).
- [ ] **7.2** `ISisregSessaoFactory.ParaOperador` + `SisregSessaoOperador` (POST de escrita liberado por URL) + orçamento por login + CAPTCHA por credencial + logout.
- [ ] **7.3** `SisregInclusaoService` + parsers + testes com os HTMLs capturados (fixtures sem PII).
- [ ] **7.4** Chamador Interno (D-8) no `enviar-fila`; formulário Interno gerado do mapa real; tela do agente com "já no SISREG"; "OK interno" conforme spike.
- [ ] **7.5** Chamador NAR (D-9) no `enviar-sistema`; resolução de credencial por unidade; trilha completa.
- [ ] **7.6** Ajustes do agente na janela editável (conforme spike) ou pendência para a ponta.
- [ ] **7.7** Aviso de sessão derrubada; idempotência por conferência antes de re-tentar; testes de que a sessão institucional não escreve.

## Dependências

Planos 04 (estados), 07 (credenciais e sessão por operador), 02 (formulário Interno), spike b. **OK explícito do Bernardo** para o spike e para a primeira inclusão real.

## Riscos e pontos a confirmar

- **Sessão única por operador**: ao incluir com a credencial do solicitante, o SISREG derruba a sessão dele no navegador (e vice-versa). Aviso obrigatório; não relogar em ping-pong.
- **CAPTCHA**: ~700 requisições por operador travam 24h. Uma inclusão custa poucas dezenas; mas um posto grande pode incluir dezenas por dia com a mesma credencial — medir no spike e projetar. Se o CAPTCHA for por IP (túnel WireGuard único), o risco é da rede inteira.
- **A tela de vagas pode marcar direto** em vez de "solicitar para regulação"; se for assim, o comportamento do Interno muda (a UBS estaria agendando, não solicitando) — decisão do Bernardo.
- **"Editável por 7 dias"** é lembrança, não fato. Se não existir, D-8 continua válida (a solicitação nasce no SISREG e o OK do agente é só local), mas "ajustes do agente na janela" some.
- Permissão de gravar do perfil solicitante pode não existir para a credencial dedicada do spike; pedir uma com o perfil certo.

## Testes

Parsers com HTML capturado; `IncluirAsync` contra um `SisregSessaoFake` que devolve as páginas do spike na ordem; releitura confirma; CAPTCHA no meio → `FalhaEnvio(captcha)` e credencial bloqueada; sessão institucional chamando URL de escrita → exceção.

## Fora de escopo

Cancelamento/exclusão pelo SMSMais (só no spike, para reverter); marcação de vaga; follow-up no SISREG (só se o spike mostrar que existe — aí entra no plano 06).

---

## Especificação para execução

> **Pré-condição absoluta:** `revisoes/spike-b-sisreg-marcar.md` existe e responde às perguntas 3–8 do plano 13. Sem ele, só as tarefas 7.2 (fábrica de sessão) e a estrutura do parser (sem HTML real) podem começar. Ao ler o relatório, ajustar esta especificação e registrar em `PROGRESSO.md` → Desvios.

### A. Arquivos

| Arquivo | Ação |
|---|---|
| `Automais.SISREG/probe_marcar.py` | criar (spike b; sobre `sisreg/client.py`; salva cada HTML em `capturas/marcar/<etapa>-<procedimento>.html`) |
| `SMSMais.Core/Integracoes/SisregWeb/Operador/ISisregSessaoFactory.cs`, `SisregSessaoFactory.cs`, `ISisregSessaoOperador.cs`, `SisregSessaoOperador.cs` | criar (sessão por login; POST de escrita liberado por URL; orçamento por login) |
| `SMSMais.Core/Integracoes/SisregWeb/SisregWebSessao.cs` | extrair o login/cookie jar/gate para uma base reutilizável **sem** expor POST de escrita na sessão institucional |
| `SMSMais.Core/Integracoes/SisregWeb/SisregOrcamentoRequisicoes.cs` | ganhar `SisregOrcamentoPorLogin` (dicionário login → instância) mantendo a global |
| `SMSMais.Core/Integracoes/SisregWeb/Inclusao/ISisregInclusaoService.cs`, `SisregInclusaoService.cs`, `Parsers/{MarcarPaginaParser,ListaItensPaParser,ListaVagasParser,ConfirmacaoInclusaoParser}.cs`, `Modelos/SisregInclusaoModelos.cs` | criar |
| `SMSMais.Core/Regulacao/Envio/IRegulacaoInclusaoSisreg.cs`, `RegulacaoInclusaoSisreg.cs` | criar (adapta a solicitação local ao motor: credencial, aviso, eventos) |
| `SMSMais.Core/Regulacao/Solicitacoes/RegulacaoSolicitacaoService.cs` | `EnviarParaFilaAsync` (Interno → `IRegulacaoInclusaoSisreg.IncluirComoSolicitanteAsync`), `EnviarAoSistemaAsync` (Nar → `IncluirEmNomeDeUnidadeAsync`), `ConfirmarOkInternoAsync` (ação no SISREG conforme spike) |
| `SMSMais.Core/Regulacao/Formularios/RegulacaoFormularioService.cs` | esquema `sisreg.inclusao` gerado do mapa real do spike |
| `SMSMais.Core/DependencyInjection.cs` | registrar fábrica (singleton), inclusão (scoped), adaptador (scoped) |
| `SMSMais.Api/Controllers/RegulacaoSolicitacoesController.cs` | nenhuma rota nova (usa `enviar-fila` / `enviar-sistema` / `ok-interno`); `POST regulacao/sessoes/sisreg/aviso-confirmado` (guarda por jti que o aviso de sessão foi lido) |
| `SMSMais.front/src/features/regulacao/components/AvisoSessaoSisreg.tsx` | criar (modal de confirmação única por sessão) |
| `tests/SMSMais.Tests/Integracoes/Sisreg/Inclusao/*.cs` + `Fixtures/sisreg-marcar/*.html` (capturas anonimizadas) | criar |

### B. Spike b — roteiro do `probe_marcar.py`

1. Login com a credencial dedicada (`SISREG_SPIKE_USUARIO/SENHA` no `.env` do lab; nunca a do robô).
2. `GET /cgi-bin/cadweb50?url=/cgi-bin/marcar` → salvar; POST `nu_cns` do paciente de teste → salvar a resposta (contexto do paciente).
3. `GET /cgi-bin/marcar` (com o contexto) → salvar; extrair `<form>`, campos (`pa`, `cid10`, `cpfprofsol`, `nomeprofsol`, `ret`, `upsexec`, outros), selects e o JS `ProximaEtapa()`.
4. Para cada procedimento (grupo `…000`, item de consulta, exame): POST do `marcar` → se grupo, salvar `LST_ITENS_PA` e escolher o item; salvar `LST_VAGAS` (campos, botões: "Solicitar", "Marcar", "Fila de regulação"?).
5. Executar a gravação **uma vez** por procedimento; salvar a resposta; extrair o número.
6. `GET gerenciador_solicitacao` / `cons_verificar` da solicitação criada: salvar; anotar ações disponíveis (Editar? até quando? Cancelar? Excluir? Follow-up?).
7. Reverter (`CANCELAR_SOLICITACAO` ou `EXCLUIR_SOLICITACAO`); salvar; confirmar por releitura.
8. Contar requisições (o `client.py` já conta); anotar se em algum momento apareceu `recaptcha`.
9. Testar a seleção de unidade: com a credencial padrão do agente (se fornecida), verificar se o login pede UPS/CNES ou se `upsexec`/outro campo define a unidade solicitante; anotar.
10. Escrever `revisoes/spike-b-sisreg-marcar.md` (template: perguntas 1–8 numeradas, evidência = nome do arquivo de captura, custo total, recomendações para o plano 11 e para o README §9).

### C. Sessão por operador

```csharp
public interface ISisregSessaoFactory
{
    ISisregSessaoOperador ParaOperador(string login, string senha, string? cnes);   // reusa instância viva por (login upper, cnes) — sessões compartilhadas por login
    Task<TestarConexaoSisregResultado> TestarAsync(string login, string senha, string? cnes, CancellationToken ct);
    void Encerrar(string login, string? cnes);
}
public interface ISisregSessaoOperador : IDisposable
{
    string Login { get; }  string? Cnes { get; }
    Task<string> GetAsync(string caminho, CancellationToken ct);                                   // mesmas regras de sessão caída/CAPTCHA da sessão institucional
    Task<string> PostEscritaAsync(string caminho, IReadOnlyDictionary<string, string> campos, string operacao, CancellationToken ct);  // só caminhos em UrlsDeEscritaLiberadas
    Task LogoutAsync(CancellationToken ct);                                                         // GET ?logout=1 (confirmar no spike)
    SisregOrcamentoRequisicoes Orcamento { get; }                                                   // por login
}
```
`UrlsDeEscritaLiberadas = { "/cgi-bin/marcar", "/cgi-bin/LST_ITENS_PA", "/cgi-bin/LST_VAGAS", "/cgi-bin/gerenciador_solicitacao" }` (ajustar pelo spike). Qualquer outro caminho em `PostEscritaAsync` → `InvalidOperationException` (teste garante). A sessão institucional (`SisregWebSessao`) **não** ganha `PostEscritaAsync`. Gate `SemaphoreSlim(1,1)` por instância. Sem relogin automático dentro de uma operação: `SessaoInvalida` → exceção com código `sisreg.sessao_derrubada`. CAPTCHA → exceção `sisreg.captcha_exigido` (já existe `CodigoCaptcha`).

### D. Motor de inclusão

```csharp
public sealed record SisregInclusaoPedido(string PacienteCns, string? PacienteCpf, string CodigoPa, string? Cid10, string? ProfissionalSolicitanteCpf, string? ProfissionalSolicitanteNome, bool Retorno, string? Observacao, IReadOnlyDictionary<string, string> CamposExtras);
public sealed record SisregInclusaoResultado(string NumeroSolicitacao, DateTime IncluidaEm, string OperadorLogin, string? UnidadeSolicitanteCnes, DateTime? EditavelAte, int RequisicoesGastas, string PaginaConfirmacaoResumo);
public interface ISisregInclusaoService
{
    Task<SisregInclusaoResultado> IncluirAsync(ISisregSessaoOperador sessao, SisregInclusaoPedido pedido, CancellationToken ct);
    Task<SisregSolicitacaoConsultaDto?> ConsultarAsync(ISisregSessaoOperador sessao, string numeroSolicitacao, CancellationToken ct);   // releitura de confirmação
    Task<IReadOnlyList<SisregSolicitacaoConsultaDto>> ProcurarDuplicidadeAsync(ISisregSessaoOperador sessao, string cns, string codigoPa, DateOnly desde, CancellationToken ct);
    Task<SisregOkInternoResultado> ConfirmarAsync(ISisregSessaoOperador sessao, string numeroSolicitacao, CancellationToken ct);   // ação do OK do agente, se o spike mostrar que existe; senão lança NotSupportedException
}
```
`IncluirAsync` (sequência; cada passo registra no orçamento): (1) `cadweb50?url=/cgi-bin/marcar` + POST `nu_cns` → `MarcarPaginaParser.ContextoPaciente`; (2) `GET marcar` → parser lê o form e os campos; (3) POST `marcar` com o pedido; (4) se `CodigoPa` termina em `000` → `ListaItensPaParser` → POST com o item (o item vem de `CamposExtras["item_pa"]`, escolhido no wizard); (5) `ListaVagasParser` → identificar a ação "solicitar para regulação" (nunca marcar vaga; se a página só oferecer marcação, lançar `sisreg.somente_marcacao_disponivel` e **parar**); (6) POST da gravação → `ConfirmacaoInclusaoParser.Numero`; (7) `ConsultarAsync(numero)` confirma paciente + `pa`; divergência → `sisreg.confirmacao_divergente`; (8) devolve resultado com `EditavelAte = IncluidaEm + prazo` (prazo real do spike ou `config.SisregPrazoEdicaoDias`).

### E. Adaptador do módulo

```csharp
public interface IRegulacaoInclusaoSisreg
{
    Task<SisregInclusaoResultado> IncluirComoSolicitanteAsync(RegulacaoSolicitacao s, CancellationToken ct);   // D-8: credencial Pessoal do usuário atual
    Task<SisregInclusaoResultado> IncluirEmNomeDeUnidadeAsync(RegulacaoSolicitacao s, CancellationToken ct);   // D-9: credencial Unidade/UnidadesPadrao do agente para s.UnidadeEmNomeDeId
    Task<bool> ConfirmarOkInternoAsync(RegulacaoSolicitacao s, CancellationToken ct);
}
```
Passos comuns: `store.ExigirSisregAsync(unidadeId?)` (plano 07: cofre → credencial → sessão pela fábrica; `credencial.ausente`/`cofre.fechado` sobem para o front); aviso de sessão confirmado por jti (senão `ConflitoException("sisreg.aviso_pendente")` → front abre `AvisoSessaoSisreg` e repete); `pedido = Traduzir(s.FormularioJson, "sisreg")` via `IRegulacaoFormularioService.TraduzirAsync`; `IncluirAsync`; sucesso → `s.NumeroExterno`, `SistemaDestino = Sisreg`, `OperadorExternoLogin`, `CredencialUsadaId`, `SisregEditavelAte`, eventos `EnvioSistema` + `NumeroExterno` (`Detalhe = {login, cnes, requisicoes}`); `RegistrarUsoAsync(sucesso)`; CAPTCHA → `RegistrarUsoAsync(false, captcha: true)` (bloqueia a credencial 24h) + `FalhaEnvio(captcha)` + `INotificadorSincronismo` avisa o operador; NAR → `LogoutAsync` ao fim (sucesso ou falha). Retentativa: `EnviarParaFilaAsync`/`EnviarAoSistemaAsync` recusam se `Status == FalhaEnvio` sem `Detalhe.duplicidadeConferida = true` — botão "Conferir duplicidade" chama `ProcurarDuplicidadeAsync` e grava o evento.

### F. Front

`AvisoSessaoSisreg.tsx`: "O SMSMais vai entrar no SISREG como **{login}**{unidade ? ` (unidade {unidade})` : ''}. Se você estiver com o SISREG aberto no navegador, aquela sessão cairá. Continuar?" → `POST regulacao/sessoes/sisreg/aviso-confirmado` → repete a ação. Detalhe da solicitação (agente): bloco "SISREG nº X — editável até dd/mm" + botão "OK interno"; solicitante: no `PassoRevisao`, o botão "Enviar" do Interno passa a dizer "Enviar e incluir no SISREG" e trata os códigos `cofre.fechado`, `credencial.ausente`, `sisreg.aviso_pendente`, `sisreg.captcha_exigido`, `sisreg.sessao_derrubada`, `sisreg.somente_marcacao_disponivel` com mensagens próprias.

### G. Testes (`tests/SMSMais.Tests/Integracoes/Sisreg/Inclusao/`)

Parsers com as capturas do spike (anonimizadas): `MarcarPaginaParserTests`, `ListaItensPaParserTests`, `ListaVagasParserTests`, `ConfirmacaoInclusaoParserTests`. `SisregInclusaoServiceTests` com `SisregSessaoOperadorFake` (devolve as páginas na ordem; grava os POSTs): `Fluxo_item_nao_passa_por_lista_de_itens`; `Grupo_000_passa`; `Pagina_so_com_marcacao_para_sem_gravar`; `Numero_capturado_e_confirmado_por_releitura`; `Confirmacao_divergente_falha`; `Captcha_no_meio_lanca_codigo`; `Url_fora_da_lista_e_recusada`; `Sessao_institucional_nao_tem_post_de_escrita` (reflexão/compilação). `RegulacaoInclusaoSisregTests`: `Interno_usa_credencial_pessoal`; `Nar_usa_credencial_da_unidade_e_faz_logout`; `Sem_aviso_confirmado_pede_aviso`; `Falha_envio_exige_conferencia_antes_de_retentar`.

### H. Passo a passo

1. Spike b (com OK) → relatório → revisar esta especificação → `PROGRESSO.md` (desvios).
2. Fábrica/sessão por operador + orçamento por login + testes → build.
3. Parsers com fixtures → testes.
4. `ISisregInclusaoService` + testes com fake.
5. Adaptador + ganchos no `RegulacaoSolicitacaoService` + esquema `sisreg.inclusao` real + endpoint do aviso → testes.
6. Front (aviso, mensagens, bloco do agente) → `npm run build`.
7. Primeira inclusão real em homologação/prod **com OK registrado**, uma solicitação, conferida no SISREG pelo navegador; só então liberar para as UBS.
8. `PROGRESSO.md` 7.1–7.7.

### I. Critério de pronto

Solicitante com credencial SISREG salva envia Interno → aviso → solicitação nasce no SISREG com número e aparece na fila local como "já no SISREG"; agente NAR envia em nome da UBS X com a credencial de unidade → SISREG registra a UBS X; CAPTCHA simulado bloqueia só aquela credencial; sessão institucional continua sem escrita (teste).
