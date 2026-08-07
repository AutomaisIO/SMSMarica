# SER — Sistema Estadual de Regulação (SES-RJ)

Integração de **leitura** com `ser.saude.rj.gov.br`. Decisão de arquitetura em
[ADR-0042](./adr/0042-ser-segunda-fonte-de-regulacao.md). O protocolo abaixo foi
levantado no laboratório `Automais.SER/` (Python) e portado para
`SMSMarica.Core/Integracoes/SerWeb/` (.NET).

> **Somente leitura.** O motor nunca escreve no SER. A trava está em
> `SerWebSessao` e é descrita no fim deste documento.

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
| Exportar Excel | `form0:btnExport` | não avaliado |

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
| Unidade solicitante | `form0:suggUnidadeSol` | **texto puro** `GESTOR SMS MARICA` |
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

#### O filtro de Solicitante NÃO funciona por texto (medido 07/08/2026)

**O SER amarra a unidade solicitante no servidor, durante a ida-e-volta do autocomplete.**
Mandar o texto no campo do submit não filtra nada — o texto é decorativo.

Prova, no mesmo navegador, mesma tela, mesmo valor visível no campo:

| como `form0:suggUnidadeSol` foi preenchido | 1º registro |
|---|---|
| digitado + **clique na sugestão** | **2727024** (06/01/2020) — recorte de Maricá |
| atribuído por código (= o que o motor faz) | **2875441** (01/06/2020) — **sem filtro** |

O hidden `form0:j_id37_selection` **permanece vazio** nos dois casos, então não é por ele que
o servidor sabe: a ligação acontece na requisição A4J do `rich:suggestionbox`.

> **CONSEQUÊNCIA GRAVE:** enquanto isso não for reproduzido, o export lê a **fila do Estado
> inteiro**, não a de Maricá — inclusive PII de pacientes de outros municípios. Não recarregar
> a base até resolver. Reproduzir o request do suggestionbox pelos parâmetros declarados na
> página (`{'form0:j_id37':'form0:j_id37','ajaxSingle':'form0:j_id37'}`) **não bastou** —
> pegar o request real no DevTools (*Copy as cURL*) é o caminho.

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

## 8. Laboratório

`Automais.SER/` (Python) continua como bancada de recon — é onde se investiga tela
nova antes de portar. Não roda em produção. `.env`, `credenciais_ser.txt` e
`capturas/` são gitignored (credencial e PII).
