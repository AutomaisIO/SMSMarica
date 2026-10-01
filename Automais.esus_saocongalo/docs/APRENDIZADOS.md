# ESUS de São Gonçalo ("Novo Esus"): recon (laboratório `Automais.esus_saocongalo/`)

> **Nome:** é o **ESUS** (produto da esusmais.com.br), **não** o e-SUS do governo — decisão do
> Bernardo em 30/09/2026. No SMSMais ele é `SistemaRegulacao.EsusSg = 5`; o `Esus = 4` fica
> reservado ao e-SUS do governo. A integração de produção nasceu deste laboratório: ver
> [ADR-0063](../../docs/adr/0063-esus-sao-goncalo-e-analise-de-regras-dos-espelhos.md).

Mesmo método dos laboratórios `Automais.SISREG/`, `Automais.SER/` e `Automais.SERNIT/`: **medir,
nunca assumir**. Tudo abaixo foi medido contra o sistema real em **30/09/2026** (front na versão
**2.8.4**, de 25/09/2026).

> **Somente leitura.** O usuário de Maricá **tem** permissão de escrita no SG (incluir na fila,
> agendar, excluir, resolver pendência — ver §3). O laboratório não usa nenhuma: a trava de
> `esus/client.py` recusa `mutation` GraphQL, toda ação do legado que não seja de leitura e todo
> POST/PUT/DELETE no REST novo fora de uma allowlist de três caminhos (login, permissões, logoff).
> Escrita no SG só depois de mapeada, autorizada **por ação** e com um motivo de produto.

## 1. O que é o alvo

`https://saogoncalo.esusmais.com.br:8000` — "Novo Esus - São Gonçalo". **Não é o e-SUS APS/PEC
do Ministério** — é o produto ESUS. É um sistema de gestão/regulação municipal com o nome "Esus", com módulos de
cadastro, consulta, exame, internação, TFD, emergência, faturamento, etc. Maricá entra nele como
**unidade solicitante** da PPI (Programação Pactuada Integrada): coloca paciente na fila de São
Gonçalo e o SG agenda.

Três origens, mesmo host:

| porta | o que é | detalhe medido |
|---|---|---|
| **8000** | SPA **Vue 3** + element-plus (também carrega CSS do element-ui), Apollo Client | um único bundle `/assets/index-*.js` de **~14 MB** |
| **8001** | backend novo (Node; cabeçalhos de `helmet`) — REST + **`/graphql`** | CORS só para a origem `:8000` |
| **9001** | backend **legado PHP 7.4.14** atrás de nginx 1.19.10 | `/<modulo>/controller-<x>/<acao>`, sempre `POST` JSON |

Outras URLs no bundle (não exploradas): `:8001/reports`, `:9001/` (base legada) e o PEP em
`https://saogoncalo.pepmais.com.br:9080`. Há Sentry próprio (`sentry.devopsinfra.com.br`, com
certificado inválido — o navegador falha ao reportar; irrelevante para nós).

**Consequência prática:** não há HTML para raspar. O laboratório chama **as mesmas APIs JSON que o
front chama**, com `httpx`. O navegador (Playwright) só serviu para **capturar o payload exato** de
cada tela — uma vez por tela.

## 2. Login — dois backends, dois tokens

A tela pede primeiro o **Cliente** (`SGO`) e só então usuário/senha. O que acontece por baixo:

| # | requisição | detalhe |
|---|---|---|
| 1 | `GET :8001/access-control/public/validate-client-by-code?cli_codigo=SGO` | só valida o código (público) |
| 2 | `POST :8001/access-control/login-light` | `{namespaces, client, username, password}` — `client` vai **minúsculo** (`sgo`) |
| 3 | `POST :9001/niveisacesso/login-sem-permissoes` | `{usuario, senha, cliente}` — dá o **token do legado** |
| 4 | `GET :8001/access-control/refresh-token` + `GET :9001/niveisacesso/renovar-sessao` | keep-alive (o front dispara a cada navegação) |
| — | `DELETE :8001/access-control/logoff` | logoff (o legado não tem logoff; o token dele expira) |

- **`namespaces`** é a lista de permissões que o front quer conhecer (215 itens, em
  `esus/namespaces.json`, capturada do front). A resposta devolve a árvore de permissões
  **só desses namespaces** — não é segredo, é o "quais telas me interessam".
- **Token é um UUID, sem prefixo `Bearer`.** Vai cru no cabeçalho `authorization`.
- **Cabeçalhos de toda chamada autenticada:** `authorization: <token>` + `unithealth: <uns_id>`.
  No `:9001` o `authorization` é o **token do legado**, não o do novo — trocar dá sessão expirada.
- Sessão: `loginInfo.expireTime = 3600` s.
- A resposta do login traz: `client` (`cli_id=1`, "Sao Goncalo"), `user` (`usu_id`,
  `usu_id_unidades_saude_padrao`, `usu_id_setor_padrao`), `unitHealths`, `sectors`,
  `permissions[uns][setor][modulo][submodulo][operacao] = id`, e flags
  (`cadsus=true`, `basicAttentionIntegration=true`, `recordNumberRequired=true`).

`probe_login.py` faz 2→3→4→logoff sem navegador e prova a trava. **Não imprime token nem senha.**

## 3. O usuário de Maricá dentro do SG

- **Uma unidade só: `uns_id=39` — "MUNICÍPIO DE MARICÁ - 0000001"**, setor `248`.
- É um **cadastro-placeholder**: CNES `0000001`, endereço "AV RUI BARBOSA S/N, Flamengo, Rio de
  Janeiro". Não tente casar essa "unidade" com CNES real de Maricá.
- Módulos liberados (na tela: **Cadastro, Exame, Consulta** — submódulo "Regulação"):

| módulo.submódulo | operações |
|---|---|
| `cadastro.pessoa` / `funcionario` / `prontuario` | incluir, alterar, excluir, exibir, … `historicoGeralPaciente` |
| `consulta.filaConsulta` | incluir, alterar, excluir, visualizar, mudarFila, resolverPendencia, `visualizarUnidadeSolicitante` |
| `consulta.buscaPacientesAgendadosFilaConsulta` / `pacientesExcluidosFilaConsulta` | exibir |
| `exame2.filaExame` | incluir, alterar, excluir, **agendar**, agendarPacienteForaTopoFila, agendarEmLote, mudarFila, pendências… |
| `exame2.buscaPacientesAgendadosFilaExames` | exibir |
| `exame2.agendamentoPorDia` / `agendamentoPorPeriodo` / `escala` / `examesEscala` | exibir |

## 4. Telas medidas

Payloads exatos em `esus/telas.py`. Todas no legado (`:9001`), `POST {"arrFormData": {...},
"toPrint": false, "toCsv": false, "toExcel": false}`.

| tela (rota do front) | ação do legado | resultado em 30/09/2026 |
|---|---|---|
| Fila de Regulação — consulta (`/appointment/schedule/queue`) | `consultas/controller-fila-consulta/buscar` | **0** |
| Fila de Regulação — exame (`/exam/schedule/queue`) | `exames2/controller-fila-exame/buscar` | **655** |
| Agendados pela fila — exame (`/exam/schedule/patient-schedule`) | `exames2/controller-paciente-agendado-fila-exames/buscar` | **64** (agendamento 31/08–29/11/2026) |
| Agendados pela fila — consulta (`/appointment/schedule/patient-schedule`) | `consultas/controller-paciente-agendado-fila-consulta/buscar` | **0** |

**Maricá só usa a PPI de EXAME no SG.** Consulta está vazia nas duas telas.

### 4.1 Fila de exame (655)

- Procedimentos (7): tratamento de **retina** 318, **glaucoma** 209, estrabismo 38, pterígio 33,
  **modalidade auditiva** 27, YAG laser 21, catarata 1 olho 9 — é oftalmologia + auditiva.
- Prioridade: A REGULAR 377, URGENTE 146, MAIS DE 60 ANOS 117, MAIS DE 80 ANOS 15.
- Pendência: NAO 624, TODAS RESOLVIDAS 31.
- Município do endereço do paciente: MARICÁ 611, **SÃO GONÇALO 39, RIO DE JANEIRO 5** — há
  paciente na fila de Maricá com endereço de outro município (vale conferir antes de cruzar).
- Espera: entrada mais antiga **16/03/2020**; **mediana 924 dias**; **492 de 655 com mais de 1 ano**.
- Cobertura de identificador: CNS 653/655, **CPF 517/655**, telefone 636/655.
- Campos úteis por linha: `fil_id` (id da fila), `pes_id`/`pac_id`, `pep_cpf_numero`,
  `pep_cartaosus`, `pep_nascimento`, `pep_sexo`, `pep_mae`, `telefone`/`nop_celular`,
  `codigo_procedimento` (**código interno do ESUS — NÃO é SIGTAP**: o mesmo `2675` aparece em pterígio, glaucoma, retina e YAG), `fle_nome_procedimento` (o campo `nome` da linha também é o PROCEDIMENTO; o paciente é `pes_nome`), `subprocedimentos`, `pfi_nome`
  (prioridade), `fil_data` (entrada), `fil_data_pedido`, `ordem_regulada`, `pendencia`,
  `profissional_solicitante`, `nomeRegulador`.

### 4.2 Agendados de exame (64 no período)

- Destino: **ABRAE 2297523** 54 (auditiva), **OFTALMOCLÍNICA SÃO GONÇALO 2291525** 6,
  **VISATTO UNIDADE RODOSHOPPING** 4 (retina). O nome da unidade de destino **carrega o CNES
  no texto** — é por ele que se cruza com a nossa `Unidade`.
- Local sempre "ÚNICO"; comprovante impresso 64/64 "SIM"; TFD 64/64 "NÃO".
- **O SG notifica o paciente e guarda a resposta**: `not_tipo` (WHATSAPP 2, SMS 2, vazio 60),
  `not_delivered_status` (answered, expired), `not_resposta` (CONFIRMADO 1, NÃO RESPONDIDO 1,
  AGUARDANDO 62). Ou seja: quase nenhum agendamento de Maricá recebeu aviso pelo SG.
- CPF 64/64, telefone 62/64.
- Campos úteis: `fil_id` (**mesmo id da fila** — é a chave para seguir o paciente fila→agendado),
  `data_agendada`, `eha_data_exame`, `data_hora_formatada`, `unidadeAgendamento`, `set_nome`,
  `lca_nome`, `stp_novo_nome_procedimento`, `cpf_numero`, `telefone`, `data_saida_fila`,
  `data_cadastro_agendamento`, `not_*`.

## 5. Armadilhas (cada uma já custou uma medição errada)

1. **Duas formas de paginação no legado.** Fila: `dados = [[linhas], "total"]`. Agendados:
   `dados = {"recordSet": [...], "total": "N"}`. A primeira versão do parser só conhecia a
   primeira e devolvia `0` para a segunda — **61 agendados "sumiram"** e pareceu que não havia
   nenhum, mesmo buscando a rede inteira. Hoje `linhas_e_total()` **levanta erro** em forma
   desconhecida, em vez de devolver vazio.
2. **Chave desconhecida no `arrFormData` não dá erro — dá 0 linhas.** Copie o payload da tela
   (`esus/telas.py`); não monte de cabeça.
3. **Agendados sem período devolvem 0.** O período (`periodoInicial`/`periodoFinal`) é a data do
   agendamento e é obrigatório de fato.
4. **Datas são `dd/mm/aaaa`** (é o que a máscara manda). As respostas também vêm em `dd/mm/aaaa`
   em vários campos — ordenar como texto embaralha os meses.
5. **Paginação:** `limiteInicio` = offset, `limiteFim` = **tamanho da página** (não é "fim").
   `paginar()` exige lido = declarado (a régua do `consultar_canceladas.py` do SISREG).
6. **Busca de agendados sem filtro de unidade levou 73 s** (rede inteira do SG). Sempre com
   `uns_solicitante`.
7. **No bundle, muitas URLs do legado vêm sem a barra inicial** (`"exames2/controller-…"`).
   O catálogo que só procurava `"/…"` achou 164 ações; com a barra opcional, **736**.
8. **`obter-comprovante-*` não é leitura inocente.** A tela de agendados filtra por
   "comprovante impresso" — gerar o comprovante pode carimbar esse flag no SG. A trava recusa.

### 5.1 Medido depois (30/09/2026, tarde) — muda como se pagina e se modela

9. **O "total" declarado conta registros ÚNICOS; o offset/limite conta linhas BRUTAS.** A lista de
   agendados repete linhas (junção interna): 2019 inteiro veio com **1.405 linhas para 1.352
   declaradas**, e as 1.352 únicas batem com o declarado. Paginar até "offset ≥ total" **perdeu 3
   agendamentos** de mar/2019 (a página de 100 veio cheia de repetição). Regra: paginar até uma página
   vir INCOMPLETA e conferir **únicos = declarado**. `esus/client.py::paginar` faz isso.
10. **Página grande funciona e é rápida:** `limiteFim=1000` traz a fila inteira (648) em 1,4 s e o ano
    de 2019 de agendados em 4,3 s (a consulta por ano com página pequena levava 45–60 s).
11. **Uma linha de agendados é um AGENDAMENTO, não um pedido:** o mesmo `fil_id` pode ter várias
    sessões (`eap_id` e `data_hora_formatada` diferentes). mar/2019: 100 linhas, 94 pedidos, 97
    agendamentos. Chave do agendamento = (`fil_id`, `eap_id`, `data_hora_formatada`).
12. **Catálogo do que Maricá pode pedir:** `exames2/controller-fila-exame/combo-box-procedimentos-regulaveis-por-solicitante`
    com corpo `{"idUnidadeSolicitante": 39}` (sem `arrFormData`) → 14 procedimentos (`data` = id,
    `nome`). Todo pedido da fila casa com ele pelo NOME.
13. **Excluídos de exame: sem permissão** para a conta ("Usuário não possui permissão", Lumen). Saída da
    fila só se descobre por diferença entre duas leituras.
14. **Os `namespaces` do login-light não mudam o acesso a dado** (lista vazia = lista mínima = 215).

### 5.2 Comparecimento: o ESUS SABE (01/10/2026)

O que dizíamos até aqui ("o ESUS não informa comparecimento") estava errado — valia só para as duas
listas que lemos. A informação existe na tela **Histórico de Atendimentos do Paciente**, a que a conta
de Maricá tem acesso (`cadastro.prontuario.historicoGeralPaciente`). `probe_historico_paciente.py`,
somente leitura, 10 pacientes:

1. `pacientes/controller-paciente/buscar-historico-geral-paciente` — corpo
   `{"arrFiltro": {pes_id, mod_id: null, periodoInicial, periodoFinal, rdg_regulacao: null,
   limiteInicio, limiteFim}}` (**`arrFiltro`, não `arrFormData`**). Uma linha por pedido
   (`id_fila`, `id` = id(s) do exame, às vezes `"78090,78093"`), módulo 33 = EXAMES. Não traz o status.
2. `pacientes/controller-paciente/buscar-detalhes-historico-exame-paciente` — `{"idExame": id}`, sem
   `arrFormData`. Devolve a **trilha do exame**, uma linha por evento (`tlg_nome`: AGENDADO, ALTERADO,
   EXCLUÍDO, Transferência, **EFETIVADO**, **NÃO EFETIVADO**, ALTERADO NA EFETIVAÇÃO, MODIFICADO PARA EM
   ABERTO), com `efl_id_exames_efetivacao` (**2 = efetivado, 3 = não efetivado, 1 = em aberto** — os
   mesmos três estados do SISREG), `data_efetivacao` e `motivo_nao_efetivacao` ("Não Compareceu").
   Também traz `fil_id` — é por ele que se casa com o espelho `esussg_solicitacao`.
3. `buscar-detalhes-historico-exame-efetivacao-paciente` voltou **vazio** nos casos testados; não usar.

Medido: VISATTO (retina) efetiva com regularidade (efetivado no dia ou em até 2 semanas; um "Não
Compareceu"); Oftalmoclínica efetivou 5 dias depois; os agendamentos de 2019 no CMDI de SG nunca foram
efetivados (campo vazio = a unidade nunca apontou, como o "em aberto" do SISREG). A efetivação chega
com atraso, então releitura dos últimos ~31 dias vale aqui também.

**Custo para produção:** 1 requisição por paciente (histórico) + 1 por exame. Os agendados de Maricá no
SG são poucos (64 num trimestre em 30/09), então reler os que passaram nos últimos 31 dias custa
dezenas de requisições por noite. Não implementado — a ficha segue mostrando "Sem registro de chegada"
para o ESUS SG até a varredura passar a ler esse detalhe.

## 6. Trava de somente-leitura

Três portas, três regras (código em `esus/client.py`):

- **GraphQL:** documento com `mutation`/`subscription` é recusado (comentários e strings removidos
  antes de olhar).
- **Legado:** o último segmento do caminho é a ação. Só passa se **começar** por verbo de leitura
  (`buscar|listar|pesquisar|consultar|obter|carregar|exibir|visualizar|verificar|combo…`) **e não
  contiver** verbo de escrita (`salvar|alterar|excluir|agendar|regular|comprovante|gerar|…`).
  Conferido contra o catálogo inteiro: das 736 ações, **369 passam** (todas de leitura) e 367 são
  recusadas.
- **REST novo:** GET livre; o resto só em `login-light`, `load-module-permissions-by-unit-id` e
  `logoff`.

## 7. Ferramentas

| script | o que faz |
|---|---|
| `esus/client.py` | `EsusSession`: login nos dois backends, `rest()`, `gql()`, `legado()`, `paginar()`, trava |
| `esus/telas.py` | payloads exatos das telas de fila e agendados |
| `probe_login.py` | sonda 0 — login sem navegador, perfil, permissões, keep-alive, prova da trava, logoff |
| `probe_fila.py` | sonda 1 — fila de exame (padrão) ou `--consulta`; agregados + lista nominal em `capturas/` |
| `probe_agendados.py` | sonda 2 — agendados pela fila (`--de/--ate`, `--consulta`); agendamentos únicos = declarado, e quantos pedidos; agregados + lista em `capturas/` |
| `catalogar_endpoints.py` | lê o bundle público e gera `docs/ENDPOINTS.md` (736 ações do legado, REST, 312 queries, 108 mutations); `--filtro <regex>` para procurar |
| `probe_historico_paciente.py` | sonda 3 — comparecimento pelo histórico do paciente (§5.2); `--pes <json>` com `[{pes_id, unidade, data}]`, senão amostra da captura de 2019 |

As listas nominais (nome, CPF, CNS, telefone) vão para `capturas/` (gitignored) ou para `--saida`
fora da árvore — **nunca** para o repositório.

## 8. Em aberto (próximas sondas)

- **Excluídos da fila** — `exames2/controller-paciente-excluidos-fila-exames/buscar` (o usuário
  tem `pacientesExcluidosFilaConsulta.exibir`; para exame, conferir).
- ~~**Histórico por pessoa**~~ — respondido em §5.2 (01/10/2026): sim, pelo detalhe do exame no
  histórico do paciente.
- **Agendamento por dia / escala** (`exame2.agendamentoPorDia`, `escala`) — capacidade ofertada
  a Maricá por unidade executante.
- **Cruzamento com o hub**: CPF (517/655) e CNS (653/655) contra `fhir.patient`, e o CNES no nome
  da unidade de destino contra `smsmarica.unidade`. Nada disso foi feito — o laboratório só lê o SG.
