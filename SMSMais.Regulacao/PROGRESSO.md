# PROGRESSO — memória de execução do módulo Regulação → Solicitações

> Este arquivo é a **única fonte de verdade sobre o que já foi feito**. Quem for implementar (qualquer modelo, qualquer sessão) começa lendo-o e termina atualizando-o. O planejamento (README + planos 01–13) diz *o que* fazer; este arquivo diz *onde estamos* e *o que aprendemos no caminho*.

## Como usar

- Marque `[x]` só quando a tarefa estiver **feita e verificada** (build 0/0, teste passando, tela aberta). Tarefa pela metade fica `[~]` com uma linha dizendo o que falta.
- Cada entrada do **Diário** tem data, incremento, o que foi feito, commit (se houve) e o que aprendeu que o plano não previa.
- Descobriu que um plano está errado? Corrija o plano **e** anote aqui em **Desvios do plano**. Nunca implemente em silêncio algo diferente do escrito.
- Nada de produção sem OK explícito do Bernardo (deploy, migration em prod, escrita em SISREG/SER/SERNIT reais). Registre o OK aqui, com data, antes de executar.
- Sessão curta? Faça **uma** tarefa inteira, atualize este arquivo, pare.

## Estado geral

| Incremento | Estado | Observação |
|---|---|---|
| 0 — Spikes de laboratório | **parcial** | **c, d, e feitos** (04–05/09). Restam **a** e **b** — os dois escrevem em sistema real e **dependem de OK explícito do Bernardo** |
| 1 — Catálogo + busca semântica | **concluído** | backend **EM PRODUÇÃO** desde 05/09; front e 14 testes prontos (não deployados) |
| 2 — Wizard + paciente + fila local | em andamento | **2.1 a 2.8 feitas** (backend + wizard, commit `74b1fa9`). Faltam **2.9** (migrar rascunhos `ser_*`/`sernit_*`) e **2.10** (testes). Anexos em Spaces (Bernardo, 05/09) |
| 3 — Fila + agente + registro assistido + notificações por unidade | não iniciado | |
| 4 — Regras de elegibilidade | não iniciado | |
| 5 — Credenciais + envio automático SER/SERNIT | não iniciado | marco D-4 |
| 6 — Pendências pós-envio | não iniciado | |
| 7 — Escrita no SISREG | não iniciado | |
| 8 — Paridade SER × SERNIT recorrente | não iniciado | |

**Incremento em andamento:** 2 (wizard + paciente + fila local). **Próxima tarefa:** **2.9** — migrar os rascunhos legados `ser_*`/`sernit_*` para `regulacao_solicitacao`; depois **2.10** (testes do wizard). O wizard (2.8) está pronto: 5 passos, rota `regulacao/solicitacoes/nova`, item de menu e o módulo `Regulacao` (47) liberado na tela de Perfis.

> ### Para quem retomar — o que já existe e deve ser reusado, não reescrito
> - `PassoPaciente` (com `CartaoPacienteCadsus` e `InformarCpfModal`) — pronto em `features/regulacao/components/wizard/`.
> - `BuscaProcedimento` — pronto, é o passo do procedimento.
> - `UploadAnexo` em `shared/ui` e `CampoDinamico`/`CampoPaciente` em `shared/regulacao` — prontos.
> - Endpoints prontos: `POST /regulacao/solicitacoes`, `PUT {id}`, `GET {id}`, `GET formulario?procedimentoId=&fluxo=`, `GET {id}/pendencias`, `POST {id}/enviar-fila`, `POST {id}/cancelar`; e as 5 rotas de exigência/anexo.
> - O wizard cria a solicitação como **Rascunho já no passo do formulário** (os anexos precisam de dono), e a tela de revisão mostra `GET pendencias` — cada item já vem com o rótulo do campo, pronto para exibir.

> ### Estado do banco de PRODUÇÃO em 06/09/2026 — conferido, não presumido
> Última migration aplicada em prod: **`20260905130201_RemoveAgendaLocal`**. Do módulo Regulação só
> o **catálogo** está lá (`regulacao_procedimento` existe). **Duas migrations pendentes**:
> `20260906014021_ConfiguracaoDaRegulacao` e `20260906020539_SolicitacaoDaRegulacao` — as tabelas
> `regulacao_configuracao`, `regulacao_solicitacao` e `regulacao_exigencia` **ainda não existem em prod**.
> Ambas são aditivas (zero `DropTable`) e sobem sozinhas no deploy, que dispara no **push** para `main`.
>
> **Não há migration nova a criar.** `dotnet ef migrations has-pending-model-changes` responde
> *"No changes have been made to the model since the last migration"* — as três migrations do módulo
> cobrem o modelo inteiro.
>
> **Ordem obrigatória, e ela importa:** primeiro o **push** (as tabelas nascem), *depois* conceder o
> módulo `Regulacao` a um perfil. Conceder antes deixa o item de menu visível apontando para uma tela
> que chama endpoints sobre tabelas inexistentes — o usuário levaria erro 500, não uma tela vazia.
>
> **Permissão — o lado do código está fechado**, nada mais a ajustar: `ModuloPermissao.Regulacao = 47`
> existe, os 5 controllers do módulo exigem a permissão por ação (Consulta/Inclusão/Edição/Exclusão),
> `features/perfis/lib/acoes.ts` lista o módulo (sem essa linha ninguém conseguiria *conceder* a
> permissão) e o `menuConfig.ts` prende o item nela. O que falta é **dado**: marcar o módulo num perfil
> pela tela de Perfis — ação de produção, exige OK do Bernardo por ação.

**Decidido em 05/09:** os anexos vão para o **Spaces**, não `midia` em bytea (Bernardo). Motivo registrado nos planos 02 e 03: o caminho real é foto de celular, não só PDF.

> ### ⚠️ O backend do incremento 1 está EM PRODUÇÃO desde 05/09/2026
> A sessão paralela pushou `22ba43e` e o deploy aplicou a migration. Conferido no banco de prod,
> não presumido: `regulacao_procedimento` e `regulacao_procedimento_origem` existem, o índice
> `ix_regulacao_proc_origem_embedding_hnsw` existe, e as duas migrations estão em
> `smsmarica.__migrations`. **O catálogo está vazio (0 linhas)** — enche na primeira vez que
> rodar um sync de catálogo do SER/SERNIT, o job diário de escalas, ou o
> `POST /regulacao/procedimentos/sincronizar`. Esse primeiro sync **gera ~560 embeddings pagos**
> na Voyage, em 5 lotes.

> ### 🔴 Armadilha achada na 1.4 — vale para todas as tarefas seguintes
> `AutoMigrate:Enabled = true` no `appsettings.json` **e o banco de dev é o de produção**.
> Subir a API na máquina local aplica migration pendente **em produção, em silêncio**. O passo 5
> do plano 01 manda fazer exatamente isso para conferir o `/docs`. **Use sempre**
> `AutoMigrate__Enabled=false dotnet run --project src/SMSMais.Api`, e valide endpoint na bancada.

**Aguardando OK do Bernardo, sem bloquear o incremento 1:** spike **a** (SER: Gravar + anexo, escrita real — pré-requisito do incremento 5) e spike **b** (SISREG: tela `marcar`, escrita real com credencial dedicada — pré-requisito do incremento 7).

> ### Restrição em vigor — D-11 (05/09/2026)
>
> **Construir tudo com a escrita externa desligada.** Nada é gravado em SISREG, SER ou SERNIT até
> o Bernardo abrir a fase de validação das gravações, que será feita em bloco no fim.
>
> - Toda escrita externa passa por **`IEscritaExternaGate`**, ponto único, lendo
>   `regulacao_configuracao.escrita_externa_habilitada` (**default false**). Service nenhum fala
>   com o provedor por fora do gate — se falar, é defeito.
> - Com o gate fechado o fluxo **vai até a borda e registra o que enviaria** (evento
>   `EnvioSimulado`: payload, anexos, credencial que usaria). É esse registro que a validação
>   futura vai conferir contra o comportamento real.
> - **"Registrar envio" manual continua valendo** — o agente digita o número gerado fora; isso
>   não é escrita externa.
> - Tarefa que dependa de captura real dos spikes a/b fica **marcada como bloqueada**, não
>   implementada por adivinhação.

~~**Pendente com o Bernardo, antes do incremento 1**~~ — **respondido em 05/09/2026: virou a decisão D-10** (README §5). O canônico é **plano**: balde e específicos são entradas distintas, a busca devolve todas, e o agente regulador troca o procedimento quando for o caso. Planos 01 e 04 já corrigidos; nasceu a tarefa **3.5b** (trocar procedimento).

## Checklist por incremento

Cada linha aponta a tarefa numerada do plano. Detalhe da tarefa fica no plano; aqui só o estado.

### Incremento 0 — Spikes (plano 13)
- [x] c — paridade SER × SERNIT (SQL local). Relatório em `revisoes/spike-c-paridade.md` — **04/09/2026**
- [x] d — classificação de follow-up. `revisoes/spike-d-followup.md` + `revisoes/spike-d-regras-followup.json` + fixture de 60 casos em `tests/SMSMais.Tests/Regulacao/Pendencias/Fixtures/` — **05/09/2026**
- [x] e — extração dos manuais CRECE/REUNI. `revisoes/spike-e-manual-regras.csv` (1.169 regras) + `revisoes/spike-e-manual-regras.md` — **04–05/09/2026**
- [ ] a — SER: Gravar + anexo numa solicitação de teste reversível. **Exige OK.** Relatório em `revisoes/spike-a-ser-gravar.md`
- [ ] b — SISREG: tela `marcar` ponta a ponta com credencial dedicada. **Exige OK.** Relatório em `revisoes/spike-b-sisreg-marcar.md`

### Incremento 1 — Catálogo + busca (plano 01)
- [x] 1.1 entidades `RegulacaoProcedimento` / `RegulacaoProcedimentoOrigem` + configurações + DbSets + migration `20260905125841_CatalogoCanonicoDeProcedimentos` (com HNSW em SQL cru) — build 0/0 — **05/09/2026**
- [x] 1.2 `RegulacaoCatalogoService.SincronizarAsync` (3 origens) + DI + disparo pós-sync nos 3 serviços — build da solução 0/0, commit `22ba43e` — **05/09/2026**
- [x] 1.3 job de embeddings (hash por modelo, lotes de 128, lote que falha não derruba o sync) — saiu embutido no sincronismo da 1.2 — **05/09/2026**
- [x] 1.4 busca híbrida (lexical + vetorial, com fallback degradado) + `CacheVetorConsulta` + `RegulacaoProcedimentosController` (7 rotas) — **05/09/2026**
- [x] 1.5 oferta interna via `sisreg_escala` (Ativa + vigente + não-ausente; grupo `000` expande os itens) e existência externa (SER / SER-AE / SERNIT) — saiu junto na 1.4 — **05/09/2026**
- [x] 1.6 `types.ts`, `api/regulacaoApi.ts`, `api/queries.ts`, `components/BuscaProcedimento.tsx`, `pages/CatalogoCuradoriaPage.tsx` + aba em `RegulacaoConfiguracaoPage` — `tsc -b` e `vite build` verdes — **05/09/2026**
- [x] 1.7 `EmbeddingsFake` + 14 testes (sync, busca, cache) — **14/14 verdes na bancada; suíte completa 1128/1129** (1 skip pré-existente) — **05/09/2026**
- [x] 1.8 `adr/0055` promovido para `docs/adr/0055-catalogo-canonico-e-embeddings.md` (+ cópia `.html`) e registrado no `CLAUDE.md` da raiz — o incremento foi para produção em 05/09 — **05/09/2026**

### Incremento 2 — Wizard + paciente + fila local (planos 02, 10, 09)
- [x] 2.1 `regulacao_configuracao` singleton + service (cache 30 s, `RowVersion`) + validador + controller (3 rotas) + aba "Solicitações" no front + migration `20260906014021_ConfiguracaoDaRegulacao` — **40 testes verdes**; a busca deixou de usar constante e lê o corte daqui — **05/09/2026**
- [x] 2.2 as 5 entidades + 5 enums (`FluxoRegulacao`, `StatusRegulacao`, `SituacaoExigenciaRegulacao`, `SituacaoArquivoExigencia`, `OrigemArquivoExigencia`) + configurações + DbSets + migration `20260906020539_SolicitacaoDaRegulacao` — validada contra Postgres real na bancada (5 tabelas, 18 índices, 2 parciais) — **05/09/2026**
- [x] 2.3 `shared/regulacao/` com `tiposCampo.ts`, `CampoDinamico.tsx` e `CampoPaciente.tsx`; SER e SERNIT passaram a usar. **−404 linhas, +6** nas duas páginas. `tsc -b` e `vite build` verdes — **05/09/2026**
- [x] 2.4 `IArquivoExigenciaStore` + `ArquivoExigenciaStoreSpaces` + `RegulacaoExigenciaService` + `RegulacaoExigenciasController` (5 rotas) + `shared/ui/UploadAnexo.tsx`. **Armazenamento: Spaces** (decidido pelo Bernardo). **49 testes verdes** (9 novos) — **05/09/2026**
- [x] 2.5 `RegulacaoPacienteService` (local → CADSUS → criar) + `RegulacaoPacientesController` + `GET pacientes/por-cns/{cns}` + `PassoPaciente`/`CartaoPacienteCadsus`/`InformarCpfModal`. **60 testes verdes** (11 novos) — **06/09/2026**
- [x] 2.6 `RegulacaoFormularioService`: união SER ∪ SERNIT por slug de rótulo + tabela de sinônimos, obrigatoriedade por OU, conflito de tipo vira dois campos sufixados, versão reusada por hash, `TraduzirAsync` com de-para de opções e conversão de data. **70 testes verdes** (10 novos) — **06/09/2026**
- [x] 2.7 esquema `sisreg.inclusao` (5 campos do mapa por GET documentado no `APRENDIZADOS.md`) — saiu no mesmo serviço da 2.6. **Explicitamente provisório**: os campos reais da tela `marcar` só se conhecem no spike b — **06/09/2026**
- [x] 2.8 wizard completo (5 passos) + rota `/app/regulacao/solicitacoes/nova` + item de menu + módulo 47 na tela de Perfis. Backend commitado em `6e3c793`; front verde. **A tela abre e funciona** — **06/09/2026**
  <details><summary>backend (detalhe)</summary> `RegulacaoSolicitacaoService` (criar/obter/atualizar/pendências/enviar-fila/cancelar, escopo fail-closed) + `RegulacaoSolicitacoesController` (7 rotas). **81 testes verdes** (11 novos).</details>
- [ ] 2.9 migração dos rascunhos `ser_*`/`sernit_*` para `regulacao_solicitacao`; telas antigas viram somente-leitura
- [ ] 2.10 testes

### Incremento 3 — Fila + agente (planos 04, 05)
- [ ] 3.1 `RegulacaoEvento` + `RegulacaoSolicitacaoDestino` + migration; máquina de estados no service
- [ ] 3.2 permissões: docs do enum 47/48/51; `acoes.ts`; `authStore.ts`; `menuConfig.ts`; `AppRouter.tsx`; `<RotaComModulo>`
- [ ] 3.3 controller `RegulacaoSolicitacoesController` com o mapa endpoint × módulo do plano 04
- [ ] 3.4 fila da unidade (escopo via `EscopoUnidade`) e fila global do agente (filtros)
- [ ] 3.5 detalhe com timeline; assumir / ajustar (diff) / devolver / recusar
- [ ] 3.5b trocar procedimento (D-10) — regera formulário, preserva respostas compatíveis, reavalia elegibilidade
- [ ] 3.6 "Registrar envio" (número externo digitado) + trava anti-duplo-envio
- [ ] 3.7 FKs para os espelhos + conciliação por número nas varreduras/importação (plano 05)
- [ ] 3.8 notificações por unidade (`regulacao_evento_visto`, filtro minha/todas) + badge da sidebar
- [ ] 3.9 notificação ao solicitante ao registrar envio
- [ ] 3.10 testes
- [ ] 3.11 promover `adr/0052`

### Incremento 4 — Regras (planos 03, 09)
- [ ] 4.1 `RegulacaoRegra` + `RegulacaoSolicitacaoRespostaRegra` + migration
- [ ] 4.2 motor puro `AvaliadorElegibilidade` + testes unitários
- [ ] 4.3 integração no wizard (questionário, caixinhas, ressalva por destino, bloqueio)
- [ ] 4.4 exames internos (`ICidadaoClinicoService`) na caixinha documental
- [ ] 4.5 tela de cadastro de regras (51) + importação do CSV do spike e
- [ ] 4.6 configurações restantes (plano 09)

### Incremento 5 — Credenciais + envio SER/SERNIT (planos 07, 12)
- [ ] 5.1 `UsuarioCofre` + `UsuarioCredencialIntegracao` + migration; `ICofreUsuarioService`; login abre o cofre; troca re-embrulha; reset invalida
- [ ] 5.2 endpoints `/identidade/me/credenciais-integracao/*` (write-only) + auditoria
- [ ] 5.3 tela "Minhas credenciais" em `MeuPerfilPage` com o aviso D-7; unidades SISREG só para 48
- [ ] 5.4 `SessaoOperadorIntegracaoStore` (substitui `SerSessaoOperadorStore`/`SernitSessaoOperadorStore`); modal como fallback (D-2)
- [ ] 5.5 spike a concluído e relatório lido
- [ ] 5.6 `POST /regulacao/solicitacoes/{id}/enviar-sistema` para SER e SERNIT (anexos antes do Gravar; releitura confirma)
- [ ] 5.7 follow-up/telefone da ponta aprovados e escritos pelo agente
- [ ] 5.8 `SerWebSessaoFake` promovido; testes; primeiros testes do SERNIT
- [ ] 5.9 promover `adr/0053` e `adr/0054`

### Incremento 6 — Pendências (plano 06)
- [ ] 6.1 `RegulacaoPendencia` + migration
- [ ] 6.2 classificador de follow-up (regex versionada) a partir do spike d
- [ ] 6.3 consumidor de `ser_gatilho`/`sernit_gatilho` → pendências
- [ ] 6.4 telas: aba "Pendência de contato", responder (ponta), aprovar/submeter (agente)
- [ ] 6.5 documento criticado → nova versão na caixinha + reenvio
- [ ] 6.6 testes

### Incremento 7 — Escrita SISREG (plano 11)
- [ ] 7.1 spike b concluído e relatório lido; questões abertas 1, 2, 3, 6, 7 respondidas
- [ ] 7.2 `ISisregSessaoFactory.ParaOperador` (sessão, gate e orçamento por login); CAPTCHA por credencial
- [ ] 7.3 `SisregInclusaoService` (parser das etapas do `marcar`) + testes com HTML capturado
- [ ] 7.4 "Enviar" do Interno inclui no SISREG com a credencial do solicitante (D-8)
- [ ] 7.5 NAR: agente inclui com a credencial da unidade (D-9)
- [ ] 7.6 ajustes do agente na janela editável (conforme spike b)
- [ ] 7.7 aviso de sessão derrubada; logout ao fim da rajada

### Incremento 8 — Paridade recorrente (plano 08)
- [ ] 8.1 job pós-sync de paridade + tabela de divergências
- [ ] 8.2 tela de divergências (51)

## OKs de produção registrados

| Data | O quê | Quem autorizou | Resultado |
|---|---|---|---|
| — | — | — | — |

## Desvios do plano

| Data | Plano | O que mudou e por quê |
|---|---|---|
| 04/09/2026 | 08 §Decisões | **Chave de pareamento trocada.** A planejada (rótulo normalizado exato) acha 3 pares acidentais — os catálogos têm convenções de nome disjuntas. Nova chave D3 = tipo + conjunto de tokens sem prefixo de modalidade + dimensão público → 23 pares, zero colisão. Especificação em `revisoes/spike-c-paridade.md` §1. |
| 04/09/2026 | 08 §Riscos | **Ramo do SER invertido.** O plano supôs que o SERNIT pareia com o ramo "Não"; é o **"Sim"** (23 pares × 0). O pareamento roda contra o SER inteiro mesmo assim. |
| 04/09/2026 | 08 §A | **SQL do spike descartado.** `unaccent` mora no schema `smsmarica` (falha sem qualificar) e a chave D3 não é expressável em SQL legível. A lógica vai para C# na tarefa 8.1, não para uma query. |
| 04/09/2026 | 08 §C | **Entidade `RegulacaoParidadeDivergencia` remodelada.** `ProcedimentoId` pressupõe 1 origem por sistema; o SER tem 30 rótulos por ramo com dois `valor` e a hierarquia cria 1:N. Passa a apontar o par de origens; unique muda. Somada a tabela de sinônimos de rótulo de campo. |
| 04/09/2026 | 08 §Medições | Contagens do `APRENDIZADOS.md` defasadas (SER 482 recursos, não ~418). Atualizadas. |
| 04/09/2026 | 08 §F | Seis casos de teste novos, todos regressão de bug real do normalizador achado no spike. |
| 04/09/2026 | **01** | **Questão de modelagem aberta, não resolvida:** o canônico não pode ser 1:1 com origem (`Endocrinologia` do SERNIT é pai de N recursos do SER). Três saídas em `revisoes/spike-c-paridade.md` §7; **bloqueia a tarefa 1.1**. |
| 04/09/2026 | 03 | 38 recursos do SERNIT não existem no SER → ressalva de destino também por **ausência de oferta**, não só por regra clínica. Mesma marcação. |
| 05/09/2026 | 13 §spike e | **Ferramenta trocada.** `pdftotext -layout` não serve: intercala as duas colunas da tabela e o nome do recurso é célula mesclada. Extração passa a ser por geometria (PyMuPDF). Quatro defeitos documentados no relatório. |
| 05/09/2026 | 13 §spike e | O nome do recurso no manual é **título da seção + célula**; sem o título o pareamento cai de 50% para 9%. O plano supunha que o nome estava só na célula. |
| 05/09/2026 | 03 §tabela `tipo` | **Quarto valor no enum: `Informativa=4`.** 83% das regras dos manuais é texto clínico; virando pergunta, um recurso com 20 critérios pediria 20 respostas. `Severidade=Aviso` não resolve — continua perguntando. |
| 05/09/2026 | 03 §4.5 | Três regras novas para o importador: `secao` decide `resposta_bloqueia` (senão inverte 295 regras); 102 recursos vêm `SEM_PAR` e vão para curadoria, nunca para adivinhação; o boilerplate (179 ocorrências) vira **uma** regra global. |
| 06/09/2026 | 02 §D-5 | **O wizard tem 5 passos, não 7.** O passo de **regras de elegibilidade** não existe porque o motor é do incremento 4 — encaixa entre "Paciente" e "Formulário" quando entrar. E o passo do **NAR** ("unidade em nome de") está na tela como aviso, não como campo: o seletor de unidade depende da fila do agente (incremento 3). O backend já recusa NAR sem a unidade, então não há como criar pela metade. |
| 06/09/2026 | 04 §A | **Bloqueio achado ao ligar o menu:** o módulo `Regulacao` (47) não estava em `features/perfis/lib/acoes.ts` — com o comentário "continua fora: não tem endpoint nem tela". Deixou de ser verdade hoje. Sem essa linha, ninguém consegue **conceder** a permissão, e o item de menu não apareceria para pessoa nenhuma. Corrigido. |
| 06/09/2026 | 02 §C | **Defeito meu, pego relendo:** o de-para de opções que escrevi mapeava `valor → valor`, ou seja, não traduzia nada. O canônico guarda o valor do **primeiro** sistema; a tradução tem de ir dele para o valor do sistema de destino. Sem isso o valor viajaria com o código do sistema errado e o destino recusaria com o campo aparentemente preenchido. |
| 06/09/2026 | 02 §2.4 | **Pendência que deixei em aberto:** o plano manda `RemoverArquivoAsync` recusar também quando a solicitação não está em `Rascunho`/`PendenteRegulacao`/`Devolvida`. Só implementei a trava por `EnviadoAoSistemaEm`; a checagem de status entra na 2.8, quando as transições passarem a existir. |
| 06/09/2026 | 10 §B | `InformarCpfAsync` **reusa `IPacientesService.DefinirCpfAsync`** (que já tem DV e recusa de troca de identidade) em vez de revalidar. O que o serviço da regulação acrescenta é só o que `DefinirCpfAsync` deliberadamente não faz: olhar se o CPF é de **outro** cadastro e devolver o id dele para a tela oferecer a troca. |
| 06/09/2026 | 10 §B | **Custo a melhorar depois:** o resumo do paciente sai de `ObterPorIdAsync`, que carrega o retrato FHIR **inteiro** (37 campos, incluindo `FotoBase64`) só para exibir nome/CPF/CNS/nascimento. Correto, mas pesado para uma tela de busca. Uma leitura estreita em `IPacientesService` resolveria — não fiz agora para não mexer em interface usada por todo o sistema. |
| 05/09/2026 | 02 §2.4 | **Regra de versionamento decidida na implementação**, porque o plano não distinguia: caixinha de **regra** versiona (novo arquivo vira `v2` e marca o anterior `Substituido`, apontando com `SubstituiArquivoId`); caixinha **"Anexos gerais" acumula**, porque ali são documentos diferentes e não versões do mesmo. Sem isso, anexar a segunda foto apagaria a primeira da tela. |
| 05/09/2026 | 02 §2.4 | Arquivo **já enviado ao sistema** não se remove (`ConflitoException`) — é prova do que foi mandado. Remoção normal é `Removido` na trilha + exclusão do conteúdo no Spaces, porque é dado de paciente. |
| 05/09/2026 | 02 §2.3 | O plano fala em extrair "componentes"; na verdade `CampoDinamico`/`CampoPaciente` eram **funções locais dentro das duas páginas**, mais os tipos em cada `types.ts`. As duas cópias eram **idênticas byte a byte**, diferindo só no nome do sistema dentro de tipos e comentários — daí o `sistema` ter virado prop, usado só nas mensagens ao operador. |
| 05/09/2026 | 02 §B / 04 §B | `RegulacaoSolicitacao` nasceu **sem** as coleções `Eventos` e `Destinos`: essas entidades são da tarefa 3.1 e entram com a migration do incremento 3. A `Exigencias` ficou, porque as caixinhas são desta tarefa. |
| 05/09/2026 | 02 §B | `RegulacaoSolicitacaoExigencia.RegraId` ficou **sem FK física**: `regulacao_regra` só existe no incremento 4. A FK entra junto com a tabela, lá. |
| 05/09/2026 | 09 §C | **Categorias de follow-up do validador trocadas.** O plano listava as quatro antigas (`FalhaContato, DocumentoCriticado, Agendamento, Outro`); valem as **nove** medidas no spike d. Uma configuração salva com categoria fora da lista viraria classificador mudo em produção. |
| 05/09/2026 | 09 §C | `ClassificarFollowUpTesteAsync` **não** entrou na 2.1: depende do `ClassificadorFollowUp`, que é da tarefa 6.2. Fica para o incremento 6, junto com a seção de follow-up da aba. |
| 05/09/2026 | 09 §B | A coluna `nao_sei_padrao` (e o enum `NaoSeiViraRegulacao`) nasceram agora, embora só o incremento 4 as use — é o que o próprio plano pede em §F, para o motor de regras não exigir uma segunda migration. |
| 05/09/2026 | 01 §G passo 5 | **Passo perigoso.** "Subir a API e conferir o `/docs`" aplica migration em produção (`AutoMigrate` ligado + banco de dev = prod). Trocado por: rodar com `AutoMigrate__Enabled=false` e validar endpoint na bancada. |
| 05/09/2026 | 01 §C | **O texto embedado deixa de levar sistema e ramo.** O plano manda embedar `"{rótulo} ({Sistema} {Ramo})"`; medido, dois recursos de nome idêntico em sistemas diferentes ficam em cosseno **0,851** contra corte de 0,85 — o par que a sugestão existe para achar fica pendurado na fronteira. O nome do sistema é ruído no eixo que se está comparando. Agora embeda só o rótulo; sistema e ramo seguem como colunas de filtro. Pego pelo teste `Sugestao_so_entre_sistemas_diferentes_e_acima_do_corte`. |
| 05/09/2026 | 01 §C | `IRegulacaoConfiguracaoService` (corte de distância da busca) só nasce na tarefa 2.1. A 1.4 usa constante `CorteDistanciaPadrao = 0.45` e passa a ler do singleton quando o plano 09 entrar. |
| 05/09/2026 | **README §5** | **Nova decisão D-11** (instrução do Bernardo): construir tudo com a escrita externa **desligada**, validar as gravações depois em bloco. Exige `IEscritaExternaGate` como ponto único + evento `EnvioSimulado`. Afeta 04, 06, 07, 09, 11, 12. |
| 05/09/2026 | 01 §A | **Gancho pós-sync do SISREG**: o plano manda pendurar no `EscalasSincronizacaoService`, mas **não é ali que nascem as origens SISREG** — quem popula `sisreg_procedimento_sigtap` é o `MapeadorSigtapSisreg`, na importação. O gancho ficou nas escalas mesmo, por **cadência** (é o job diário de rede inteira) e porque o sincronismo é idempotente; a justificativa do plano é que estava errada, não o lugar. |
| 05/09/2026 | 01 §C | `CalcularSugestoesAsync` como especificado dava **três idas ao banco por origem** (~1.700 num catálogo de 560). Reescrito para carregar as candidatas rastreadas de uma vez: sobra uma consulta por candidata, a do vizinho mais próximo — que é para o que o HNSW existe. |
| 05/09/2026 | **README §5** | **Nova decisão D-10** (respondendo a questão aberta do spike c): canônico plano, sem hierarquia; contenção nunca funde; o agente troca o procedimento. |
| 05/09/2026 | 01 | Explicitado que o casamento automático usa a **chave D3**, não o rótulo cru, e que a busca **não** deduplica balde contra específico. Corpus de aceitação do cosine: os 17 casos de contenção devem virar sugestão, os 2 falsos não. |
| 05/09/2026 | 04 | **Tarefa 3.5b nova**: `POST …/{id}/trocar-procedimento`. Não é `Ajuste` — muda formulário e regras, então regera a versão do formulário, preserva as respostas de chave sobrevivente, reavalia elegibilidade e recusa se já houver número externo. |
| 05/09/2026 | 13 §spike d | **Taxonomia trocada: de 4 para 9 categorias.** As duas maiores reais (`SemVaga` 22%, `ReclassificacaoRisco` 9% no SER e 81% no SERNIT) não estavam previstas; `Agendamento` é residual porque `Agendar` é evento próprio. |
| 05/09/2026 | 13 §spike d | **Método de medição trocado.** Rotular 100 linhas aleatórias mede mal (40% cairiam numa classe só); precisão se mede por amostra estratificada da classe predita. |
| 05/09/2026 | 13 §spike d | **Anonimização automática não é confiável neste corpus** — deixa passar nome entre parênteses e destrói texto em CAIXA ALTA. O fixture é montado só com textos repetidos ≥ 3× (template = sem nome), com filtro de telefone e conferência linha a linha. |
| 05/09/2026 | 06 §classificador | Só **duas** das nove categorias viram pendência. **`OrientacaoAoPaciente` separada de `SolicitacaoAoSolicitante`**: juntas, a fila nasceria com 5,5× itens falsos. |
| 05/09/2026 | 06 | **Caminho novo**: `ContatoRealizado` com sub-desfecho `NaoAguarda` (347 casos) tem de avisar a unidade — é vaga que volta para a fila. O plano não previa. |
| 05/09/2026 | 06 §6.2 | `SMSMais.Tests.csproj` **não copia fixture para a saída** e nenhum teste hoje lê arquivo: o teste do classificador precisa acrescentar a regra `None Update`, senão falha por arquivo não encontrado. |
| 05/09/2026 | 09 | `regras_followup_json` inicial pronto e medido: `revisoes/spike-d-regras-followup.json`. A semente escrita à mão no plano 06 foi substituída. |
| 05/09/2026 | 13 §spike e | CSV com **5 colunas a mais** que o previsto (`manual`, `ramo_ser`, `recurso_catalogo`, `pareamento`, `secao`) — sem elas a importação teria de refazer o pareamento e não distinguiria os ramos do SER. |

## Diário

### 06/09/2026 — conferência de migration e de permissão (nenhum código mudou)

- **Pedido:** "faça migration e ajuste a questão de perfil que precisar". **Resposta medida: não havia nem uma nem outra a fazer.**
- `dotnet ef migrations has-pending-model-changes` → *"No changes have been made to the model since the last migration"*. As três migrations do módulo já cobrem o modelo; criar uma quarta geraria migration vazia.
- **Fui ao banco de produção conferir em vez de presumir** — e o estado não era o que eu tinha registrado. Prod está em `RemoveAgendaLocal`; `regulacao_procedimento` existe (o catálogo subiu no push da sessão paralela), mas `regulacao_configuracao`, `regulacao_solicitacao` e `regulacao_exigencia` **não existem**. As duas migrations pendentes sobem no próximo push.
- **Detalhe que quase me fez consultar a tabela errada:** neste repositório a tabela de histórico do EF chama-se `smsmarica.__migrations`, não `__EFMigrationsHistory` (esse nome existe, mas é do schema `fhir`, de outro serviço). Quem for conferir migration aplicada precisa saber disso.
- **Permissão:** o lado do código já estava inteiro (enum 47, os 5 controllers por ação, `acoes.ts`, `menuConfig.ts`). Falta só o dado — marcar o módulo num perfil pela tela, ação de produção.
- **Ordem que registrei no Estado geral porque ela importa:** push primeiro, concessão do perfil depois. Ao contrário, o menu aparece e a tela responde 500 sobre tabela inexistente.

### 06/09/2026 — incremento 2, tarefas 2.1 a 2.8 (backend)

- Commits **`d746f81`** (2.1–2.5 + todo o incremento 1) e **`6e3c793`** (2.6–2.8 backend). **Nenhum pushado.** Suíte completa **1198/1199** na bancada, build 0/0 nos dois lados.
- **O spike c virou código** na união do formulário: obrigatoriedade por OU, tabela de sinônimos de rótulo, e conflito de tipo gerando dois campos em vez de escolha automática — cada regra com o número medido no comentário e no teste.
- **Dois defeitos meus, pegos antes de rodar:** o de-para de opções mapeava `valor → valor` (não traduzia nada), e o dublê do formulário nos testes devolvia um `versaoId` inexistente contra uma FK real.
- **Onde parei e por quê:** o contexto da sessão chegou perto do ponto de resumo. Parar aqui, com o backend fechado e commitado, é melhor do que atravessar o wizard do front pela metade.

### 05/09/2026 — incremento 1 CONCLUÍDO (tarefas 1.4 a 1.8)

- **1.4/1.5** busca híbrida + `CacheVetorConsulta` + `RegulacaoProcedimentosController` (7 rotas), com oferta interna pelas escalas e existência externa. **1.6** front completo (`features/regulacao/`: types, api, queries, `BuscaProcedimento`, aba de curadoria). **1.7** `EmbeddingsFake` + 14 testes. **1.8** ADR-0055 promovido.
- **Verificação:** build da solução 0/0; `tsc -b` + `vite build` verdes; **suíte completa 1128/1129 na bancada** (1 skip pré-existente).
- **O teste achou um defeito de projeto, não de código.** `Sugestao_so_entre_sistemas_diferentes_e_acima_do_corte` falhou, e a causa era o plano: embedar `"{rótulo} ({Sistema} {Ramo})"` deixa dois recursos de nome **idêntico** em sistemas diferentes com cosseno **0,851** contra corte de 0,85. O par que a sugestão existe para encontrar ficava pendurado na fronteira e sumia com qualquer variação de grafia. Corrigido: embeda só o rótulo.
- **Armadilha de infraestrutura achada:** `AutoMigrate:Enabled = true` + banco de dev = produção. O passo 5 do plano 01 (subir a API para ver o `/docs`) aplicaria migration em prod — inclusive os 6 `DropTable` da outra frente. Não executei; corrigi o plano e avisei no farol.
- O `npm run lint` do repositório continua quebrado (sem `eslint.config.js`, eslint fora do `node_modules`) — pré-existente; o gate real é o `npm run build`.

### 05/09/2026 — incremento 1, tarefa 1.2 (sincronismo do catálogo canônico)

- `IRegulacaoCatalogoService` + `RegulacaoCatalogoService` + DTOs + registro no DI + gancho pós-sync no SER, no SERNIT e nas escalas do SISREG. `src/` compila 0 erros / 0 warnings.
- **Não fechei o gate de build da solução**: o projeto de testes não conseguiu relinkar porque outro `testhost` (frente paralela, rodando a suíte de escalas) está com `SMSMais.Core.dll` aberto. Não matei o processo. Ficou `[~]`, com 10 avisos do projeto de testes por conferir.
- Precisei ajustar `EscalasSincronizacaoTests`: ele constrói o serviço à mão e o parâmetro novo quebrou a chamada. Entrou um dublê `CatalogoRegulacaoNulo` — o catálogo não é o que está sob teste ali, e o de verdade precisaria do provedor de embeddings.
- **Duas correções de plano** (ver Desvios): o gancho do SISREG estava justificado no serviço errado, e o cálculo de sugestões como especificado fazia ~1.700 idas ao banco por sincronismo.
- Detalhe que vale para quem seguir: o hash do embedding inclui o **modelo** (`voyage-3`, lido de `ia_configuracao`). Sem isso, trocar de modelo deixaria metade do catálogo num espaço vetorial e metade em outro, e a busca viraria loteria — sem ninguém perceber.

### 05/09/2026 — incremento 1, tarefa 1.1 (entidades do catálogo canônico)

- `RegulacaoProcedimento` + `RegulacaoProcedimentoOrigem` + `RegulacaoEnums` + as duas configurações + DbSets + migration `20260905125841_CatalogoCanonicoDeProcedimentos`. Build 0 erros / 0 warnings. **Migration não aplicada em lugar nenhum** — é só código.
- **Desvio do plano:** a FK `procedimento_sigtap_id` — eu tinha escrito `Restrict` e o plano pede `SetNull`. Voltei para o plano, que está certo: o correlato SIGTAP é informativo, e `Restrict` faria a tabela do SIGTAP virar refém do catálogo.
- O índice HNSW é o **primeiro do repositório** — o módulo de IA usa `vector(1024)` mas não tem índice de vetor, faz varredura sequencial. Escrito em SQL cru no `Up()`/`Down()` porque o EF não gera índice de vetor.
- **Atenção para quem pegar a próxima tarefa:** o working tree está sendo usado ao mesmo tempo por outra frente (refactor Agenda/SISREG — módulos `Agendamentos` e `Especialidades` deletados no index, `AgendaSisreg/` novo). Durante esta tarefa o build ficou vermelho por alguns minutos por causa de um `StatusAgendamento` deletado por eles e ainda referenciado; voltou sozinho. Conferi que o meu hunk no `SmsMaisDbContextModelSnapshot.cs` é **puramente aditivo** (210 inserções, 0 remoções) — na hora de commitar, só o próprio hunk.

### 05/09/2026 — spike d (classificação de follow-up)

- Leitura do corpus inteiro de produção (18.904 follow-ups do SER, 312 do SERNIT), 0 requisição externa, nenhuma escrita, nada commitado. Saídas: `revisoes/spike-d-followup.md`, `revisoes/spike-d-regras-followup.json` e o fixture de 60 casos em `tests/SMSMais.Tests/Regulacao/Pendencias/Fixtures/`.
- **Resposta à pergunta do spike: sim, dá para classificar sem IA** — as regex deixam só 13% em `Outro`, e todas as classes ficaram com precisão ≥ 90% (a maioria 100%) sobre ~130 itens lidos à mão.
- **A taxonomia do plano estava errada.** As duas maiores classes reais não estavam nela. E o achado que mais muda o produto: o template que parecia "documento criticado" é **orientação ao paciente** (avisar o que levar no dia), que não pede ação nenhuma — se as duas ficassem juntas, a fila de pendências nasceria com 5,5× itens falsos.
- **O SERNIT é outro animal:** 81% reclassificação de risco e **zero** falha de contato em 312 eventos.
- **Seis defeitos meus, achados medindo**, todos virados em caso de regressão no fixture — entre eles um sutil: `AO?` numa regex exige o "A" literal e nunca casa só "o", então "informar **o** paciente" escapava do filtro e viraria pendência falsa.
- **PII:** escrevi um anonimizador automático e o descartei — ele deixa passar nome entre parênteses e destrói texto em CAIXA ALTA (transformou `PACIENTE AGENDADO PARA O HOSPITAL` em `PACIENTE <NOME> O HOSPITAL`). O fixture veio de textos repetidos ≥ 3× e conferência linha a linha.

### 05/09/2026 — spike e (extração dos manuais CRECE e REUNI)

- Só arquivos locais + leitura do catálogo já dumpado no spike c. 0 requisição externa, nada commitado, nada deployado. Saída: `revisoes/spike-e-manual-regras.csv` (1.169 regras) e `revisoes/spike-e-manual-regras.md`.
- **Resultado:** 204 recursos com regra escrita; 83% NaoDedutivel, 14% Documental, 2% Dedutivel. Só **19% do catálogo SER** tem regra no manual (27% no ramo AE, 11% no outro). Metade dos recursos do manual não casa com o combo por divergência real de nome.
- **Confirmou o spike c por outra via:** CRECE é o Volume 3 "Ambulatório Estadual - AE" (ramo `ae=true`) e REUNI é o Volume 2 da rede geral (ramo `ae=false`). Cada ramo tem manual próprio.
- **O que o plano não previa:** a ferramenta indicada (`pdftotext -layout`) não funciona — a tabela tem duas colunas que se intercalam no texto corrido e o nome do recurso é célula mesclada centralizada, que começa *depois* do critério ao lado. Foi preciso ler pela geometria (réguas da tabela no PyMuPDF).
- **Quatro defeitos meus, achados medindo:** separador de coluna com x variável (177–198) fazia o texto de requisitos vazar para dentro do nome; o nome do recurso é seção + célula, não só célula; título de capítulo (`4. RECURSOS REUNI – PROTOCOLOS…`) colava na frente de todo recurso; e `4.1.1. CARDIOLOGIA - ALTA COMPLEXIDADE (ambulatório 1ª vez)` era rejeitado por eu exigir caixa alta na linha inteira. Cada um está documentado no relatório §3 para quem reprocessar.
- **Descoberta que muda o produto:** com 83% de regras não dedutíveis, o questionário do plano 03 fica impraticável sem um estado "informativa". Corrigido no plano.

### 04/09/2026 — spike c (paridade SER × SERNIT)

- Executado por leitura do Postgres de produção (0 requisição a sistema externo, nenhuma escrita). Relatório: `revisoes/spike-c-paridade.md`. Nada commitado, nada deployado.
- **Resultado:** 23 pares, 399 chaves só-SER, 55 só-SERNIT (38 delas sem candidato nenhum no SER). 11 dos 23 pares têm formulário idêntico; nos outros 12 o padrão é uniforme. Zero conflito de tipo e de opções. Régua união decidida.
- **O que o plano não previa:**
  - a chave planejada não funciona — os catálogos não compartilham convenção de nome, e o prefixo de modalidade (`Ambulatório 1ª vez`, `CONSULTA EM`) mata toda igualdade;
  - `unaccent` e `vector` estão no schema `smsmarica`, não no `public` (vale para o plano 01 também);
  - o rótulo não é chave única nem **dentro** do SER: 30 rótulos por ramo têm dois `valor`;
  - o vocabulário de campo é minúsculo (76 rótulos distintos, 26 moldes) — a união é um dicionário canônico, não engenharia por par, e é bem menor do que o plano fazia supor;
  - a contenção entre catálogos é **hierarquia**, não par — e isso abriu uma questão de modelagem do canônico que **bloqueia o incremento 1**.
- **Três bugs meus, achados medindo:** descartar parênteses funde `Cardiologia (Oncologia)` com `Cardiologia`; tratar `COM`/`SEM` como ruído funde `com Sedação` com `sem Sedação`; descartar o marcador de público esvazia a chave de `Pediátria`. Os três viraram caso de teste no plano 08 §F.

### 04/09/2026 — planejamento
- Sessão de planejamento (Claude Fable 5.1). Escritos README, PROGRESSO, planos 01–13, rascunhos de ADR 0052–0055, workflows `regulacao-revisar-planos` e `regulacao-executar-plano`.
- Decisões D-1 a D-9 tomadas com o Bernardo (ver README §5).
- Cada plano 01–13 terminou com a seção **"Especificação para execução"**: arquivos exatos, entidades com colunas e tipos, enums, interfaces e DTOs em C#, rotas com permissão, componentes/hooks do front, nomes de teste, passo a passo com comandos e critério de pronto. É por essa seção que a implementação deve ser guiada; a parte de cima do plano é o porquê.
- Nada implementado.
