# SER — Sistema Estadual de Regulação (SES-RJ)

Integração com `ser.saude.rj.gov.br`. Decisão de arquitetura em
[ADR-0042](./adr/0042-ser-segunda-fonte-de-regulacao.md). O protocolo abaixo foi
levantado no laboratório `Automais.SER/` (Python) e portado para
`SMSMarica.Core/Integracoes/SerWeb/` (.NET).

> **Leitura por padrão; escrita é exceção nomeada.** Todo POST passa pela trava de
> somente-leitura (§7). Desde 18/08/2026 existem **duas** escritas liberadas, cada uma
> mapeada contra o SER real e autorizada explicitamente: **registrar FollowUP** (§9) e
> **alterar os telefones** (§10). Elas saem por uma porta separada
> (`SubmeterEscritaAsync`), que exige declarar a operação e a registra em log.
>
> **E são assinadas pelo operador, não pela credencial de sincronismo.** O SER carimba
> cada evento com o nome de quem fez; escrever com a credencial do banco faria toda ação
> do município sair no nome da mesma pessoa. A senha pessoal do operador vive **em
> memória**, amarrada à sessão dele no SMSMarica — nunca em banco.

## 1. Stack do SER

| | |
|---|---|
| Servidor | WildFly 10 / Undertow |
| Camada web | JSF 1.2 (Mojarra) + RichFaces / Ajax4JSF 3.3.3 |
| Framework | JBoss Seam (páginas `.seam`, conversas com `cid`) |
| Build observada | `2026-06-30#26-80.00` |

## 2. Sessão: três coisas andam juntas

1. **`JSESSIONID`** — cookie, path `/ser`. A sessão de verdade.
2. **`SERVERID`** — cookie do balanceador (`server2`). Perdê-lo cai em outro nó e a
   sessão "some".
3. **`cid`** — *conversation id* do Seam. Contador **por sessão**; o servidor emite
   um novo a cada conversa (entrar em módulo, abrir tela). Vem diferente toda vez —
   **nunca fixar**, sempre ler da página ou do `Location` corrente.

## 3. Fluxo obrigatório até a fila

Pular etapa dá **HTTP 500**.

| # | Requisição | Detalhe crítico |
|---|---|---|
| 1 | `GET /ser/login` | form `login`, ViewState inicial `j_id1` |
| 2 | `POST /ser/login` | `login:username`, `login:password`, `login:entrar=Entrar`, `javax.faces.ViewState`. O `action` já vem com `;jsessionid=…` — usar cru. |
| 3 | `GET /ser/home.seam` | módulos: `ambulatorial`, `internacao`, `oncologia`, `neonatal`, `ostomizados` |
| 4 | `POST /ser/home` | `{form}:goModulo`, `param1=<modulo>`, **`AJAXREQUEST={form}`**, `AJAX:EVENTS_COUNT=1`, ViewState |
| 5 | `GET /ser/pages/principal.seam?cid=N` | seguir o `Location` do passo 4 — é o que ativa o módulo na sessão |
| 6 | `GET .../solicitar-consulta-pesquisar.seam` | 500 se o passo 5 não rodou. Não precisa de `cid`. |
| 7 | `POST` no mesmo endereço | botão `form0:j_id96` (`title="Pesquisar"`) |

### 3.1 A armadilha do `AJAXREQUEST`

O Ajax4JSF **só reconhece a requisição como AJAX se o parâmetro `AJAXREQUEST`
estiver presente** (valor = id da região/form). Sem ele o WildFly trata o POST como
postback comum, re-renderiza a mesma página e **a ação nem roda** — HTTP 200, sem
erro nenhum. É a falha mais difícil de diagnosticar do SER.

Com ele, a resposta vira:

```
ajax-response: redirect
location: /ser/pages/principal.seam?cid=16273
```

### 3.2 A armadilha do ViewState por form

A home tem vários `<form>`, e o `javax.faces.ViewState` **pode diferir entre eles**.
Pegar o primeiro do documento faz o JSF restaurar a view errada — mesmo sintoma
silencioso. Sempre extrair o ViewState **de dentro do form que está sendo submetido**.

### 3.3 A armadilha do destino do POST (a pior de todas)

**Postar sempre no `action` lido do `<form>`, nunca num caminho constante.**

Com os **mesmos campos, os mesmos headers e o mesmo ViewState**, postar numa URL fixa
devolve um conjunto de resultados **diferente** do que a tela mostra — registros que
existem, e que são encontráveis por `Id Solicitação`, simplesmente **não aparecem na
listagem**. Não há erro, não há aviso: a resposta é uma listagem plausível, só que
incompleta.

Medição de 06/08/2026, filtro `Solicitante=GESTOR SMS MARICA` + `AGENDADA` + `CONSULTA`:

| Destino do POST | 1º registro | Solicitação 2727024 presente? |
|---|---|---|
| caminho constante | 2875441 (01/06/2020) | **não** |
| **`action` do form** | **2727024 (06/01/2020)** | **sim** |

É a mesma regra que o cliente do SISREG já documentava (*"o action vem com
`;jsessionid` — usar cru"*), e ignorá-la aqui custou uma carga inicial inteira e três
diagnósticos errados (suspeita de vazamento de outro município, de corte silencioso da
listagem e de base inteira não-confiável — nenhum procedia).

### 3.4 A armadilha da resposta parcial

O submit do datascroller responde `ajax-response: true` com a grade nova mas **sem
`<form id="form0">`**. Reaproveitar essa resposta como base do próximo submit quebra.
O motor guarda dois HTMLs: o último **completo** (fonte dos campos) e o último
**recebido** (fonte das linhas).

## 4. Tela de pesquisa (`form0`)

| Campo | Id JSF | Valores |
|---|---|---|
| Situação | `form0:j_id75` | `EM_FILA`, `PENDENTE`, `AGENDADA`, `CHEGADA_NAO_CONFIRMADA`, `CHEGADA_CONFIRMADA`, `CANCELADA`, `ALTA` |
| Tipo | `form0:comboTipoRecurso` | `CONSULTA`, `EXAME` |
| CPF / Nome / CNS | `form0:cpf`, `form0:nome`, `form0:cns` | texto |
| Id Solicitação | `form0:idSolicitacao` | texto |
| Datas (3 pares) | `form0:dt{Inicial,Final}{Solicitacao,ConsultaExame,Agendamento}InputDate` | `dd/mm/aaaa` |
| **Pesquisar** | `form0:j_id96` | `A4J.AJAX.Submit('form0', …)` |
| **Exportar** | `form0:btnExport` | `jsfcljs` (Mojarra) — **POST comum, sem `AJAXREQUEST`**. É por aqui que ALTA é lida (§4.4) |

**Situação é obrigatória** — buscar sem ela devolve zero.

Grade `form0:listagem`, 14 colunas: ID, Tipo, Recurso, Data da Solicitação, Paciente,
Idade, CPF, CNS, CID, Solicitante, Município Solicitante, Agendado para, Situação,
Ação.

### 4.1 Paginação e o teto de 100

`rich:datascroller` `form0:sc1`: POST com `ajaxSingle=form0:sc1` + `form0:sc1=<n>`.
**20 por página, 5 páginas.** E corta **em silêncio**: não há aviso nenhum de que
existe mais coisa além da 5ª página.

> **Esta tela deixou de ser o caminho da varredura.** Desde 06/08/2026 a grade é lida
> pela tela de Histórico (§4.3), que devolve 500 por lote e **avisa** quando trunca.
> A tela de Solicitação continua sendo usada para duas coisas que só ela faz: a
> situação **ALTA** e o **Histórico da Solicitação** (§5).
>
> **E desde 08/08/2026 nem a ALTA passa por esta paginação** — ela é lida pelo *Exportar*
> desta mesma tela (§4.4). A paginação sobrou só para a consulta direta de diagnóstico.

#### A paginação perdia registro em silêncio

Medido em produção em 07/08/2026: a varredura terminou **`fatias_truncadas = 0`** — afirmando
cobertura completa — com **853 registros de ALTA faltando** (2.454 lidos contra 3.307 no SER).
A detecção do teto funcionava (a Alta acusa as 5 páginas corretamente); quem perdia era o
trajeto de página em página, que tem dois caminhos de perda calada:

1. o laço interrompe **sem avisar** quando uma página volta vazia (`if (linhas.Count == 0) break`);
2. a contagem de páginas devolve **zero** quando o paginador não é achado no HTML, e aí a leitura
   fica só na primeira página — 20 de 100.

Por isso a leitura passou a ser **sempre por arquivo**, nas duas telas.

### 4.2 O menu *Opções* muda conforme a situação

| Situação | Itens |
|---|---|
| Em fila | Visualizar, Editar, **Histórico**, Cancelar, Registrar FollowUP |
| Cancelada | Visualizar, **Histórico**, Registrar FollowUP |
| **Alta** | **nenhuma ação — não tem Histórico** |

Os ids são `form0:listagem:<i>:j_idNN`, com `<i>` 0-based **na página corrente** (não
é o ID da solicitação). O motor localiza o item pelo **texto** e falha explicitamente
quando não existe — chutar um `j_id` fixo faz o SER responder **página vazia sem
erro**, e o chamador acha que leu o histórico de alguém.

### 4.3 Tela de Histórico de Consulta/Exame — a que exporta 500

`/ser/pages/historico/consulta-exame/solicitacao/historico-pesquisar.seam`. Também tem
`form0` e também tem botão *Pesquisar*, então serve de "página de formulário" (§3.4).

| Campo | Id/name JSF | Observação |
|---|---|---|
| Situação | `form0:j_id57` | **6 valores, sem `ALTA`** — localizar pelo `<select>` que oferece `EM_FILA`, não pelo `j_id` |
| Tipo | `form0:tipo` | `CONSULTA`, `EXAME` |
| Data da Solicitação | `form0:data{Inicial,Final}InputDate` | é o eixo do fatiamento |
| Data do Agendamento | `form0:dataAgendamento{Inicial,Final}InputDate` | **não usar** — é mutável |
| Unidade solicitante | `form0:suggUnidadeSol` | **exige a amarração pelo autocomplete** — texto puro é decorativo (ver abaixo) |
| Município do Paciente | `form0:municipio` | **não usar** — paciente de outro município pode ter solicitação aberta por Maricá |
| Pesquisar | `form0:btnSearch` | `A4J.AJAX.Submit` (precisa de `AJAXREQUEST`) |
| **Exportar** | `form0:btnExport` | `jsfcljs` (Mojarra) — **POST comum, sem `AJAXREQUEST`** |

Diferenças que importam em relação à tela de Solicitação:

- **Avisa quando trunca**, em `form0:messages`: *"Consulta muito ampla, retorno limitado
  em 500 resultados"*. É esse aviso — e não contagem de linhas — que autoriza afirmar
  "este lote está completo".
- Traz **Unidade executora**, que a outra não tem.
- **Não** traz CPF, Solicitante nem Município solicitante.
- **Não** oferece "Histórico da Solicitação" no menu da linha.
- Sem Situação preenchida, exige nome/código/CNS/CPF/ID do paciente.

#### A armadilha da busca que não devolve o resultado

**O POST do `btnSearch` responde ~267 bytes**, e não a grade:

```xml
<meta name="Ajax-Response" content="redirect" />
<meta name="Location" content="/ser/pages/.../historico-pesquisar.seam?cid=25509" />
```

É o **mesmo redirect A4J embutido no corpo** que a tela de histórico usa (§5), apontando
para a própria tela com o `?cid` da conversa Seam. **O resultado só existe na página de
destino.** Quem parseia os 267 bytes encontra grade vazia, nenhuma mensagem e nenhum aviso
de corte — e conclui *"recorte vazio, cobertura completa"*. Custou uma noite em 06/08/2026,
com três hipóteses erradas pelo caminho (data, tipo e unidade solicitante).

#### O filtro de Solicitante NÃO funciona por texto — o protocolo da amarração (resolvido 07/08/2026)

**O SER amarra a unidade solicitante no servidor, durante a ida-e-volta do autocomplete.**
Mandar o texto no campo do submit não filtra nada — o texto é decorativo.

Prova, no mesmo navegador, mesma tela, mesmo valor visível no campo:

| como `form0:suggUnidadeSol` foi preenchido | 1º registro |
|---|---|
| digitado + **clique na sugestão** | **2727024** (06/01/2020) — recorte de Maricá |
| atribuído por código (= o que o motor fazia) | **2875441** (01/06/2020) — **sem filtro** |

**O mecanismo** (lido no `ui.pack.js`/`framework.pack.js` que o próprio SER serve — não foi
preciso DevTools): o `rich:suggestionbox` faz **duas** requisições A4J, e a segunda é a que
importa.

| # | Requisição | Parâmetros além dos campos do form |
|---|---|---|
| 1 | **fetch de sugestões** (ao digitar) | `inputvalue=<texto>` (param default do RichFaces — o init não o sobrescreve), `<box>=<box>`, `ajaxSingle=<box>` |
| 2 | **onselect** (ao clicar na sugestão) | `<box>:<support>=<box>:<support>`, `ajaxSingle=<box>`, **`<box>_selection=<índice da linha escolhida>`** |

Em ambas, `AJAXREQUEST=_viewRoot` — o init do componente não passa `containerId` e o
`A4J.Query` cai no default `A4J.AJAX.VIEW_ROOT_ID`, não no id do form.

O servidor guarda a lista de sugestões do fetch na conversa Seam e resolve o índice do
`_selection` contra ela no onselect — só então a unidade fica amarrada e a busca seguinte sai
recortada.

**Por que o `_selection` parecia sempre vazio:** o `selectEntry` do RichFaces escreve o índice
no hidden, dispara o submit do onselect **e limpa o campo em seguida** (`A.value=this.index; …;
A.value=""`). Olhar o DOM depois do clique mostra vazio; o valor só existe DENTRO do request.
Observar o DOM após o fato não diz o que viajou — ler o JS do componente resolveu em minutos o
que a dedução por tentativa não resolveu em uma noite.

Prova de reprodução headless (sonda `probe_solicitante.py`, 07/08/2026, 3 rodadas):

| rodada | 1º registro | conjunto |
|---|---|---|
| A — com amarração | **2727024** (DAIANA, recorte de Maricá) | idêntico ao de B |
| B — com amarração (repetição) | **2727024** | **idêntico ao de A** |
| C — só texto (o motor de antes) | 2790533 (outro recorte) | **variou entre execuções** |

O export da rodada A veio com 500 linhas abrindo em 2727024 — grade e planilha do mesmo
recorte. A instabilidade "aleatória" que se via na plataforma era isto: sem amarração, a
consulta é do **Estado inteiro** e o SER não a devolve em ordem estável.

> **No motor:** `SerExportLeitor.AmarrarSolicitanteAsync` reproduz as duas requisições antes de
> toda busca com solicitante; os ids voláteis (`j_id37`/`j_id42`) são extraídos do script de
> init da página a cada tela (`SerHtmlParser.SuggestionBoxDoCampo`), nunca fixados. Sem sugestão
> que case, a leitura **falha alto** (`ser.solicitante_nao_resolvido`) — degradar para busca sem
> filtro leria o Estado inteiro, com PII de outros municípios. A credencial GESTOR SMS MARICA
> **não** escopa sozinha esta tela; foi medido.

#### O filtro de Tipo não filtra (é do SER)

Medido no navegador: com `Tipo = CONSULTA` a própria tela do SER devolve linhas **EXAME**
(Cateterismo Cardíaco). Não é defeito do motor — é da tela. Não confiar nesse combo para
fatiar nada.

#### E a armadilha de reusar a página de resultado

**Cada busca exige um GET novo da tela.** Medido em 07/08/2026: três buscas idênticas, cada
uma precedida de GET, devolvem conjunto e ordem **idênticos**; encadeadas — usando a página
de resultado como base do submit seguinte, e portanto o `?cid` da conversa anterior — o SER
passa a devolver **conjuntos instáveis** entre chamadas.

É a mesma família do §3.3: **o que muda o resultado não é o filtro, é de onde se posta.** A
página de resultado serve para UMA coisa — o POST do Exportar, que precisa do `action` com o
`cid` que produziu aquele lote — e morre ali. Nunca vira base da busca seguinte.

#### O arquivo

O `btnExport` responde `historico-pesquisar.xls`: **BIFF8 dentro de OLE2** (assinatura
`D0 CF 11 E0 A1 B1 1A E1`), `Content-Type: application/vnd.ms-excel`. Medido em 06/08/2026:

| recorte | bytes | linhas | aviso de corte |
|---|---|---|---|
| EM_FILA, 01/07–31/07/2026 | 130.560 | 1 + **310** | **não** (completo) |
| EM_FILA, sem data | 179.200 | 1 + **500** | **sim** |

Aba única `Sheet1`, **12 colunas, nesta ordem**: Tipo, Recurso, ID Solicitação, Data da
Solicitação, CNS, Paciente, Idade, CID, Unidade executora, Data do agendamento, Situação,
Ação. (Mapear por NOME mesmo assim — a ordem é observação, não contrato.)

**Tipos das células, que não são os óbvios:**

- **Data vem como número de série do Excel**: `46204.37281195602` = 01/07/2026 08:56. Não é
  texto nem data formatada. Sem converter, a solicitação entra no espelho **sem data de
  solicitação** — o eixo de todo o fatiamento — e sem erro nenhum.
- **ID e demais numéricos vêm como `double`**: `8000763.0`. Formatar com `ToString()` puro
  produziria notação científica.
- CNS, Idade, CID e Situação vêm como texto.
- Converter serial só nas COLUNAS DE DATA: o ID também é número, e virar data seria pior
  que ficar nulo.

Duas armadilhas do transporte:

1. **Ler a resposta como texto destrói o arquivo.** O transporte devolve `byte[]`; quem
   decodifica UTF-8 num BIFF8 troca os bytes por caracteres de substituição e a planilha
   deixa de abrir — sem erro em lugar nenhum.
2. **Reconhecer a planilha pelo conteúdo, não pelo `Content-Type`.** Quando a sessão cai
   (outro login do mesmo operador), o SER responde a tela de login com HTTP 200 no lugar
   do arquivo.

**Varredura:** janela adaptativa sobre `Data da Solicitação`, da esquerda para a direita —
tenta a maior janela, encolhe pela metade enquanto vier o aviso de corte, aplica o lote e
avança. Dia único que ainda passa de 500 é fatiado por Tipo; o que nem assim couber vira
fatia truncada declarada.

> **Por que não pular para `max(data)` do lote:** só seria correto se o corte de 500 fosse
> aplicado *depois* de ordenar por data da solicitação, e isso **nunca foi verificado**. Se
> o SER cortar em qualquer outra ordem, o salto passa por cima das solicitações antigas que
> ficaram de fora do lote — em silêncio. É a mesma classe de perda invisível que custou
> ~35% da carga inicial. Se um dia a ordenação for confirmada, é trocar só o laço.

### 4.4 O export da tela de Solicitação — o caminho da ALTA (medido 08/08/2026)

A tela de Solicitação também tem `form0:btnExport`, e é assim que **ALTA** é varrida: ela é a
única situação que o combo da tela de Histórico não oferece.

**O teto de 100 vale para o arquivo também** — o export *não* escapa dele. Medido:

| recorte | linhas no arquivo | registros com ID |
|---|---|---|
| ALTA, sem data | 300 | **100** |
| ALTA, `Tipo=CONSULTA` | 300 | **100** |
| EM_FILA, sem data | 100 | **100** |

> **A contagem crua do arquivo mente.** As 300 linhas da ALTA são 100 registros: o export quebra
> a coluna *Agendado para* — que na tela é `data - UNIDADE` — em várias linhas. É a regra de
> sempre: **nunca inferir por contagem de linhas**.

#### A coluna *Agendado para* deste export é inútil — e por quê (incidente de 08/08/2026)

**O layout não é "registro seguido dos seus fragmentos".** Os **100 registros ocupam as 100
primeiras linhas**; os 200 fragmentos vêm **todos depois**, num bloco. A coluna forma um *fluxo*
que não se alinha com as linhas de registro: só o 1º registro fica com o próprio valor correto,
e do 2º em diante a célula da linha pertence a outro agendamento.

E não dá para remontar contando, porque o **número de linhas por registro é variável**: 1 quando
não há agendamento (o EM_FILA devolveu 100 linhas para 100 registros) e 3 quando há data +
unidade. A informação de qual fragmento é de quem simplesmente **não está no arquivo**.

> **O que custou aprender isso:** a primeira versão do parser tentou remontar grudando os
> fragmentos no registro anterior. Em produção isso concatenou vários agendamentos num único
> registro, estourou o `varchar(300)` de `agendado_para_texto` e **derrubou a varredura inteira**
> no `SaveChanges` (`22001: value too long`). A queda ainda acionou dois defeitos conhecidos em
> cadeia: o `finally` tentou salvar com o change tracker envenenado, o `Status=Erro` não
> persistiu, e a execução ficou **presa em `EmExecucao`**, travando o scheduler.
>
> **O erro de método:** inferi o layout do arquivo a partir de uma amostra de três linhas, tendo
> o arquivo inteiro salvo na sonda. É a versão estrutural do mesmo "nunca inferir por contagem".

**Por isso o motor descarta essa coluna:** `SerExportSolicitacaoLeitor` zera `AgendadoPara` das
linhas que vêm daqui. Dado errado é pior que dado ausente. Quem preenche a coluna é a tela de
Histórico, que traz *Data do agendamento* em campo próprio e alinhado — e o merge usa `??` para
que uma releitura por ALTA **não apague** o que a outra tela já sabia.

**Por que trocar paginação por arquivo se o teto é o mesmo:** o ganho não é cobertura, é **uma
requisição no lugar de cinco**, sem os dois caminhos de perda silenciosa da paginação (§4.1).

Colunas (14): ID, Tipo, Recurso, Data da Solicitação, Paciente, Idade, **CPF**, CNS, CID,
**Solicitante**, **Município Solicitante**, Agendado para, Situação, Ação. As três em negrito a
tela de Histórico **não** tem — e foram elas que provaram o escopo: 100 de 100 linhas de ALTA
vieram com `GESTOR SMS MARICA` / `MARICA`. **Esta tela é escopada pela credencial do operador**,
então aqui não há amarração de autocomplete a fazer (ela nem tem campo de solicitante).

Duas diferenças de comportamento em relação à tela de Histórico:

- **A busca devolve a grade no próprio corpo** — sem o redirect A4J. O motor ainda segue o
  redirect se ele aparecer, para o dia em que a SES-RJ mudar isso.
- **O filtro de Tipo FUNCIONA aqui.** Medido: ALTA sem filtro traz 37 CONSULTA + 63 EXAME; com
  `Tipo=CONSULTA` traz 100 CONSULTA, conjunto diferente. Na tela de Histórico o mesmo combo é
  decorativo (§4.3) — e é isso que torna o corte por Tipo confiável como último recurso da
  bisecção **apenas aqui**.

**Sem aviso escrito de corte.** A caixa `form0:messages` volta vazia mesmo com a ALTA estourando.
O único sinal é o lote voltar **cheio**, e o motor trata lote cheio como cortado: é inferência,
mas na direção segura — no máximo fatia à toa. Concluir "completo" de um lote cheio é que seria
a perda invisível de novo.

## 5. Tela de histórico

Chega por redirect A4J embutido no corpo (não no header):

```html
<meta name="Location" content="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-historico.seam?cid=16453" />
```

Dois blocos:

- **Dados do paciente** — inputs *readonly* com id volátil. Parsear pelo `<label>` do
  mesmo `<td>`. Campos: ID Solicitação, Paciente, CNS, CPF, **Nome Mãe**, Sexo,
  **Data Nascimento**, Etnia, CEP, UF, Município, Bairro, Tipo Logradouro,
  Logradouro, Número, Complemento, **Telefone Residencial, Telefone WhatsApp,
  Telefone Contato**.
- **Trilha de eventos** — tabela `form0:historicoList`: Data, Evento, Estado
  Anterior, Estado Atual, Central regulação, Unidade Executora, Usuário, Lotação
  Evento, **IP**, Observação. Eventos vistos: `Solicitar`, `FollowUP`, `Pendenciar`,
  `Cancelar`.

O `tbody` **repete o cabeçalho** e traz linhas-tooltip só com a observação — filtrar
por `Data` casando `dd/mm/aaaa`.

## 6. Custo medido (05/08/2026, produção)

| Operação | Requisições | Custo |
|---|---|---|
| Varredura por faixa de data | 1 busca + N páginas | ~15 reg/s |
| EM_FILA 2022→hoje (1.484 registros) | 238 | 100 s |
| Histórico, ciclo otimizado | 2 | 0,617 s / solicitação |
| Histórico de 5.000 solicitações | — | ≈ 50 min |

O ciclo otimizado do histórico é **pesquisar por `ID Solicitação` reusando o form +
abrir o histórico**. Duas requisições é o piso: abrir o histórico descarta o
resultado da busca (a aba "Pesquisar" volta com a grade vazia).

## 7. Trava de somente-leitura

Duas camadas, porque uma só não cobre JSF:

1. **Nome do parâmetro** — pega ids falantes (`form0:btnSalvar`, `…:agendarLink`).
2. **Rótulo visível do componente clicado** — indispensável: o botão *Gravar* do modal
   de cancelamento é `j_id189:j_id195` e **Registrar FollowUP** é `j_id169`. O verbo
   só existe no `value`/texto do elemento, nunca no nome.

Estado verificado no menu *Opções*: `Editar`, `Cancelar` e `Registrar FollowUP`
**bloqueados**; `Visualizar` e `Histórico da Solicitação` permitidos.

## 8. A tela de CRIAR solicitação (aba *Editar*)

Levantada em 08/08/2026 e documentada à parte, por volume:
[`docs/ser-criar-solicitacao.md`](./ser-criar-solicitacao.md).

Resumo do que importa saber daqui: o formulário de pedido tem um **bloco fixo** (tipo, recurso,
CNS, médico, classificação de risco, hipótese, unidade de origem) e um **bloco dinâmico**
(`form0:camposDinamicos`) que **muda conforme o Recurso escolhido** — 203 recursos produzem
**21 formulários diferentes**, com 163 campos únicos. Oncologia, por exemplo, acrescenta
peso/altura/IMC obrigatórios, "já realizou cirurgia oncológica?" e as datas da biópsia.

> **Continua valendo a trava de somente-leitura.** Abrir a aba e trocar combos só re-renderiza a
> view; o botão *Gravar* (`form0:j_id313`) nunca é acionado por nada nosso.

## 9. Registrar FollowUP — a primeira escrita mapeada (18/08/2026)

Mapeada no navegador e **exercitada de verdade** na solicitação 8196837 (Agendada). É a
escrita mais simples do SER, e a única de que precisamos hoje: acrescenta uma observação ao
histórico **sem mudar a situação**.

**O FollowUP não mora no `form0`.** Clicar em *Registrar FollowUP* no menu da linha abre um
modal com **form próprio**, e o Gravar dele é um **POST comum** — sem `AJAXREQUEST`, sem
`ajaxSingle`, sem o ViewState do `form0`. A página inteira navega e volta com *"FollowUp
registrado!"*.

| Passo | Requisição |
|---|---|
| 1. Pesquisar por `form0:idSolicitacao` | A4J normal (`AJAXREQUEST=form0`) |
| 2. Item *Registrar FollowUP* do menu da linha | A4J (`AJAXREQUEST=_viewRoot`) — abre o modal |
| 3. Gravar do modal | **POST comum**, no `action` do form do modal |

O POST do passo 3 tem **cinco campos**:

```
<form>                = <form>          # marcador do form, como sempre no JSF
<form>:<textarea>     = <observação>
<form>:<gravar>       = Gravar
autoScroll            =
javax.faces.ViewState = <o ViewState DE DENTRO desse form>
```

Medido em 18/08/2026: form `j_id175`, textarea `j_id175:j_id183`, Gravar `j_id175:j_id185`,
Cancelar `j_id175:j_id186`, item do menu `form0:listagem:0:j_id169`. **Todos posicionais** — o
item do menu se acha pelo texto, o form pelo textarea + botão *Gravar* que ele contém.

**"FollowUp registrado!" não é prova** — é a lição de 10/08/2026 com a Hipótese, que respondeu
sucesso e não gravou nada. A conferência é pelo **Histórico**, e o evento tem esta cara:

```
18/08/2026 13:03:51 · FollowUP · Agendada -> Agendada
CREG-METROPOLITANA II · HOSPITAL UNIVERSITARIO ANTONIO PEDRO (UFF HUAP)
BERNARDO DOS SANTOS LEITE ALMEIDA · Gestor: GESTOR SMS MARICA · <IP>
"Registramos que o contato com a paciente já foi realizado, e a mesma informa que poderá comparecer."
```

> **Correção a uma regra registrada.** `docs/ser-continuacao.md §3` diz *"FollowUP é Em fila ->
> Em fila"*. O certo é que o FollowUP **preserva a situação, qualquer que ela seja** — o caso
> medido foi `Agendada -> Agendada`. A consequência prática não muda: FollowUP **nunca** aparece
> num diff de grade, então só a leitura do histórico o revela.

**O evento carrega o usuário logado** (nome completo) e a lotação (`Gestor: GESTOR SMS MARICA`).
Como a integração usa uma credencial única, todo FollowUP nosso vai sair com esse nome — quem
de fato pediu tem de estar no TEXTO, ou o histórico do Estado perde a autoria real.

> **A trava de somente-leitura continua barrando isto no motor .NET** — `registrar` e `followup`
> estão no regex, e o rótulo do botão é *Gravar*. Ligar o FollowUP é decisão explícita: exige
> liberar nominalmente esses componentes, como já foi feito para a troca de aba. Enquanto isso
> não acontecer, o caminho é a sonda `Automais.SER/probe_followup.py` (ensaio por padrão).

## 10. Alterar os telefones (aba *Editar*)

A segunda escrita. Serve ao caso real da regulação: liga-se para o paciente, o número está errado,
e corrigir exigia abrir a tela do Estado.

| Passo | Requisição |
|---|---|
| 1. Pesquisar por `form0:idSolicitacao` | A4J normal (`AJAXREQUEST=form0`) |
| 2. Item *Editar* do menu da linha | A4J (`AJAXREQUEST=_viewRoot`) — a aba volta preenchida |
| 3. Gravar (`<a title="Gravar">` do `form0`) | A4J, na região que o próprio `onclick` declara |

**Os três telefones se resolvem pelo RÓTULO, nunca pelo id.** Dois deles não têm `id`, só `name`
posicional (`form0:j_id173` residencial e `form0:j_id178` WhatsApp, medidos em 10/08/2026);
`form0:telefoneContato` é o único estável. Chumbar aqueles números grava no campo errado na
próxima recompilação da SES-RJ — e o SER responde "salvo com sucesso" do mesmo jeito.

**Trava invertida.** A trava de somente-leitura não vale nesta operação (ela *é* escrita, e o
botão chama-se *Gravar*). O que protege é comparar o POST contra o que a tela renderizou e
recusar se qualquer campo além dos telefones divergir: um POST que mexesse em recurso, médico,
risco ou CID passaria despercebido, porque o SER aceita e confirma.

**Campo `disabled` não vai no POST.** O SER trava a identidade do paciente (nome, CPF, CNS, mãe,
raça) com `disabled`, e o navegador não envia esses campos — então gravar não os zera. O motor
faz o mesmo **só na escrita** (`CamposDoForm(..., comoNavegador: true)`); nas leituras o corpo
segue como sempre foi, porque nesta tela o que muda o resultado costuma ser o que se manda no
POST (§3.3) e trocar isso "para arrumar" seria trocar comportamento provado por suposto.

**Situação terminal não oferece *Editar*.** Cancelada e Alta só têm Visualizar, Histórico e
Registrar FollowUP — a tela mostra os números e esconde o botão, em vez de deixar digitar para
recusar no fim.

**A conferência é reabrir a edição do zero** e comparar número a número, **só pelos dígitos**: o
SER aplica máscara ao gravar (`21987654321` volta `(21) 98765-4321`), e comparar o texto cru faria
"formatou" parecer "ignorou".

> **Isto NÃO altera o hub FHIR** (decisão de 18/08/2026). Telefone no FHIR tem regras próprias —
> o marcador de verificado por OTP é a fonte única, e contato só ACUMULA (o incidente de 10/08
> apagou 8.634 números num merge). O que a tela grava é o cadastro do SER e o espelho
> `ser_solicitacao`. Efeito colateral consciente: como espelho e SER passam a bater, a varredura
> seguinte não vê mudança e não dispara conciliação — a alteração fica no Estado e no espelho.

## 11. Laboratório

`Automais.SER/` (Python) continua como bancada de recon — é onde se investiga tela
nova antes de portar. Não roda em produção. `.env`, `credenciais_ser.txt` e
`capturas/` são gitignored (credencial e PII).
