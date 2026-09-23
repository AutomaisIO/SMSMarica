# SISCAN — como nasce uma requisição nova (mamografia)

Medido contra o SISCAN real em **22/09/2026**, versão do alvo V2.19.1-RC08, conta do
prestador **CDT DR ALBERTO LUIS MACHADO BORGES (CNES 3132358)**. Nenhuma requisição foi
criada: a sonda vai até um clique antes do **Salvar** e para ali. **Exceção documentada:**
uma única criação real, autorizada pelo Bernardo em 22/09/2026 — o resultado está na §8.

> **PRODUÇÃO FEDERAL, pacientes reais de Maricá.** O cliente nasce `somente_leitura=True`.
> A trava barra `salvar`, `encerrar`, `confirmar`, `incluir`, `liberar`, `alterar`; só a
> palavra `novo` foi liberada, e só porque **[Novo Exame] apenas renderiza a tela**.
> Criar requisição de verdade exige OK explícito do Bernardo, por ação.

Ferramentas: [`probe_nova_requisicao.py`](../probe_nova_requisicao.py) (o caminho, passo a
passo) e [`mapear_requisicao_nova.py`](../mapear_requisicao_nova.py) (varredura dos ramos
condicionais). O fluxo em si vive em [`siscan/requisicao.py`](../siscan/requisicao.py).

```bash
python probe_nova_requisicao.py --parar-em 2                      # abre a tela
python probe_nova_requisicao.py --cns <CNS>                       # + CADSUS pelo CNS
python probe_nova_requisicao.py --cns <CNS> --unidade 35 \
       --mamografia 02 --responsavel 3                            # até o Responsável
```

## 1. O caminho

| # | Onde | O que acontece |
|---|------|----------------|
| 1 | EXAME → **GERENCIAR EXAME** | tela de pesquisa (`/visao/exame/pesquisarExame.jsf`) |
| 2 | botão **Novo Exame** (`frm:botaoNovoExame`, A4J) | vai para `/visao/exame/novoExame.jsf` — **etapa 1** |
| 3 | **Cartão SUS** (`frm:cartaoSUS`, A4J do `onblur`) | o CADSUS devolve o paciente inteiro |
| 4 | **Tipo de exame** (`frm:tipoExame`, A4J) | só então Unidade Requisitante ganha opções |
| 5 | **Avançar** (`frm:botaoAvancar`) | vai para `salvarRequisicaoMamografia.jsf` — **etapa 2** |
| 6 | **Tipo de mamografia** (`frm:j_id242`, A4J) | só então o combo Responsável ganha opções |
| 7 | **Responsável** (`frm:responsavelColeta`, A4J) | o servidor deriva o **Conselho** |
| 8 | **Salvar** (`frm:btSalvar`) | **cria a requisição** e abre o modal do protocolo — ver §8 |

O `<h1>` da etapa 2 é `SOLICITAR REQUISIÇÃO DO EXAME DE MAMOGRAFIA` — o mesmo da tela de
alteração em modo editável (ver `APRENDIZADOS` §6b). A tela é a mesma; muda o caminho de
chegada.

## 2. O CNS monta tudo — e basta o `onblur`

Digitar o Cartão SUS e disparar o A4J do próprio campo (`ajaxSingle=frm:cartaoSUS`) já traz,
**sem clicar a lupa** `frm:btnPesquisarCadSUS`:

| Campo | Estado |
|-------|--------|
| `frm:cpf`, `frm:cpfPesquisado` | preenchido, **editável** |
| `frm:nome`, `frm:dataNascimento`, `frm:nacionalidade`, `frm:nomeMae`, `frm:racaCor` | preenchido, **DISABLED** |
| `frm:uf`, `frm:municipio`, `frm:tipoLogradouro`, `frm:nomeLogradouro`, `frm:numero`, `frm:bairro`, `frm:cep` | preenchido, **DISABLED** |
| `frm:sexo` | preenchido (`Feminino`), editável no HTML |
| `frm:apelido`, `frm:escolaridade`, `frm:pontoReferencia` | **vazios e editáveis** — é o que o digitador acrescenta |

Junto vem, de brinde, **o histórico de exames daquele paciente** (`frm:listaExamePacientePaginada`,
com link *Visualizar Requisição do Exame* por linha) e o botão **Atualizar Dados do Paciente**.
Isso faz da tela de Novo Exame um caminho barato para *ler* o que o SISCAN sabe de um paciente
pelo CNS — sem passar pela pesquisa com período obrigatório.

Campo `disabled` **não se reposta** (o navegador não posta, e o SISCAN os deriva do CADSUS).
Quem tentar "corrigir" nome ou endereço por POST está perdendo tempo: o servidor descarta.

## 3. A ordem é imposta pela tela, não é preferência

Dois combos nascem vazios e só são preenchidos por um A4J específico:

1. **Unidade Requisitante** (`frm:unidadeSaude2`) tem só `Selecionar` até o **tipo de exame**
   ser marcado. Depois: **37 unidades** de Maricá, com `value` = **índice posicional** e texto
   `CNES - NOME`.
2. **Responsável** (`frm:responsavelColeta`) vem `<select ...></select>`, literalmente vazio, até
   o **tipo de mamografia** ser marcado. **Não é "qualquer A4J"**: disparar o A4J de risco
   elevado (`frm:j_id91`) deixa o combo vazio do mesmo jeito. Medido nos dois sentidos.

### A lista de Responsável muda conforme diagnóstica × rastreamento

Medido na USF ELENIR UMBELINO (CNES 3055779), mesmo paciente, mesma sessão:

| Tipo de mamografia | Responsáveis oferecidos |
|--------------------|-------------------------|
| 01 Diagnóstica | 11 |
| 02 Rastreamento | 14 (os 11 + outros 3 profissionais) |

A leitura natural é que rastreamento aceita solicitante que diagnóstica não aceita
(enfermagem) — **não confirmado com a operação**, fica como hipótese.

**Armadilha séria:** o `value` do `<option>` é **posicional**, e a posição muda entre as duas
listas (o mesmo profissional é `8` na diagnóstica e `9` no rastreamento). Gravar o índice numa tabela nossa é
gravar lixo. O texto traz `NOME - CNS`: **resolver sempre pelo CNS do profissional**, como já
fazemos com unidade por CNES.

## 4. Conselho é derivado, como no laudo

Escolher o Responsável dispara A4J e o servidor devolve `frm:conselho` preenchido
(no formato `CRM - <número>`, para o profissional escolhido) e **DISABLED**. Ou seja: conselho não se digita
nem se reposta — ele é consequência do profissional. Isso fecha com o que já sabíamos da tela
de alteração (`APRENDIZADOS` §3) e com a regra de que **o responsável tem de sair certo na
criação**, porque depois não é alterável.

## 5. Armadilha nova: o radio que o A4J não devolve marcado

Clicar **Avançar** repostando só o que a tela mostrava responde:

> O campo Tipo de Exame deve ser informado.

…**mesmo com o tipo já empurrado pro bean pelo A4J**, e ainda por cima a resposta mostra o radio
marcado — o que faz parecer bug do scraper. O que acontece: o parcial do A4J não redevolve o
`<input type="radio" checked>`, então quem reposta "o que leu" manda o radio vazio e o validador
`required` reclama. No navegador não aparece porque a marca fica no DOM do cliente.

**Regra:** num submit completo, reenviar explicitamente todo valor que só foi para o bean por
A4J. É prima da armadilha da data no Gerenciar Exame (`APRENDIZADOS` §4), mas ao contrário: lá
faltava o round-trip A4J; aqui o round-trip existiu e falta o campo no POST.

Para isso ficar visível, o cliente ganhou `aplicar_a4j(base, parcial)`: a resposta A4J **não é a
tela** — é só o que está em `<meta name="Ajax-Update-Ids">`. Quem lê a resposta crua não acha
`<form id="frm">` e conclui, errado, que a ação falhou.

## 6. A etapa 2 por dentro

Derivados e travados: `cartaoSUS`, `paciente`, `sexoFeminino`, `cnesPrestador` (3132358),
`prestador`, `cnesUnidade`, `unidade`.

Editáveis: `frm:prontuario` (Nº do Prontuário), `frm:localizacaoNodulo` (checkbox — `01` Sim
Mama Direita, `02` Sim Mama Esquerda, `04` Não), `frm:mamasExaminadas` (`01` Sim, `02` Nunca
foram examinadas anteriormente, `03` Não Sabe), e os condicionais já mapeados na tela de
alteração, com os mesmos ids: `j_id91` risco elevado, `j_id105` fez mamografia, `j_id121`
radioterapia (→ localização → ano por lado), `j_id151` cirurgia (26 campos de ano),
`j_id242` tipo de mamografia.

Obrigatórios marcados com `*` na etapa 2: **Data da Solicitação**, **Responsável**, **Conselho**.

Rastreamento (`frm:tipoMamografiaRastreamento` — um dos três controles que **não** chamam o
servidor, junto com `mamasExaminadas` e `localizacaoNodulo`):
`01` População alvo · `02` População de risco elevado (história familiar) · `03` Paciente já
tratado de câncer de mama.

Diagnóstica abre 6 checkboxes: `achadosExameClinico`, `controleRadiologicoLesao`,
`lesaoDiagnosticoCancer`, `avaliacaoRespostaQuimioterapia`, `revisaoMamografia`, `controleLesao`
(cada um com um irmão `...Hidden` que o SISCAN manda com o valor `teste` — lixo de
desenvolvimento do próprio SISCAN, não nosso).

Depois do Salvar existe um modal `formModalExameSalvo` com **PROTOCOLO DA REQUISIÇÃO** e os
botões *Novo Exame*, *Inserir Resultado*, *Imprimir*, *Voltar* — é de lá que sai o número do
protocolo. Não foi exercitado.

## 6b. Ramos condicionais da tela de CRIAÇÃO (varridos, sem gravar)

Levantado por [`mapear_requisicao_nova.py`](../mapear_requisicao_nova.py), ligando um valor por
vez a partir do mesmo estado base e comparando os campos presentes no form. Bate com o que já
tinha sido medido na tela de alteração — a tela é a mesma.

| Pergunta | Resposta | Efeito |
|----------|----------|--------|
| Apresenta risco elevado (`j_id91`) | 01 / 02 / 03 | não abre nem fecha nada, apesar de disparar A4J |
| Mamas já examinadas (`mamasExaminadas`) | 01 / 02 / 03 | **não chama o servidor** — só JS no cliente |
| Achado no exame clínico (`localizacaoNodulo`) | 01 / 02 / 04 | **não chama o servidor** |
| Fez mamografia alguma vez (`j_id105`) | Sim 01 | abre `anoUltimaMamografia` |
| Fez radioterapia (`j_id121`) | Sim 01 | abre a localização (`j_id128`, 3 radios) |
| ↳ Localização (`j_id128`) | 01 Esquerda | abre `anoRadioterapiaEsquerda` |
| | 02 Direita | abre `anoRadioterapiaDireita` |
| | 03 Ambas | abre os dois |
| Fez cirurgia de mama (`j_id151`) | Sim S | abre **26** campos de ano (13 tipos × 2 lados) |
| Tipo de mamografia (`j_id242`) | 01 Diagnóstica | abre 6 checkboxes de indicação (+ 6 irmãos `...Hidden`) |
| | 02 Rastreamento | abre os 3 radios `tipoMamografiaRastreamento` |

**Armadilha do próprio método** (quase publiquei um mapa errado): `client.campos()` imita o
navegador e **ignora radio/checkbox não marcado**. Com ele, "radioterapia = Sim" aparecia como
*não abre nada* — porque o que abre é um grupo de radios desmarcados. O mapeador compara
**presença** (`name=value` para radio/checkbox), não submissibilidade. Quem for escrever outra
sonda de varredura: o mesmo vale para qualquer tela do SISCAN.

## 6c. De-para com uma ficha do SISREG — o que dá para preencher sozinho

Exercitado com uma solicitação real de MAMOGRAFIA BILATERAL (validado contra a tela, sem gravar).
A ficha do SISREG (`smsmarica.solicitacao`, campo `raw_sisreg`, 38 posições separadas por `;`)
resolve a identificação inteira; **a anamnese não vem de lugar nenhum**.

| Campo do SISCAN | De onde sai |
|-----------------|-------------|
| `frm:cartaoSUS` | CNS da ficha → e o CADSUS monta nome, nascimento, mãe, raça/cor e endereço |
| Tipo de exame | procedimento da ficha (SIGTAP `0204030030` / SISREG `1305007` → Mamografia `01`) |
| Unidade Requisitante | **CNES** da unidade solicitante da ficha → `unidade_por_cnes()`, nunca o índice |
| Tipo de mamografia | **pela idade** — ver a régua abaixo. O CID da ficha (`Z12.3`) sugere, mas quem decide é a idade |
| Responsável | operador solicitante da ficha; casar por **CNS do profissional** (`responsavel_por_cns()`) |
| Data da Solicitação | data da solicitação da ficha |
| Conselho | derivado pelo SISCAN a partir do Responsável |

### A régua do tipo de mamografia é a IDADE

Régua do CDT Maricá (Bernardo, 22/09/2026), em `tipo_mamografia_por_idade()`:

| Idade na data de referência | Tipo |
|---|---|
| **>= 36 anos** | `02` Rastreamento |
| **< 36 anos** | `01` Diagnóstica |

Não é o critério do INCA (50–69 para rastreamento populacional) — é a régua operacional daqui.
Consequência que não é óbvia: como a lista do combo **Responsável muda** entre diagnóstica e
rastreamento, **a idade da paciente também decide quem pode assinar a requisição**.

**O que a ficha do SISREG NÃO responde — e são 5 obrigatórios:** tem nódulo ou caroço na mama;
apresenta risco elevado para câncer de mama; antes desta consulta teve as mamas examinadas por
profissional; fez mamografia alguma vez (+ ano); fez radioterapia na mama ou no plastrão (+ lado
+ ano); fez cirurgia de mama (+ até 26 anos). Mais o Nº do Prontuário (opcional) e, no
rastreamento, a população-alvo.

É o mesmo diagnóstico que o de-para do laudo já tinha dado por outro caminho: **são perguntas da
paciente**, e nenhuma ficha de regulação as carrega. Qualquer promessa de "gerar a requisição do
SISCAN a partir do SISREG" tem de dizer quem responde essas seis — ou nasce com um formulário
para alguém preencher.

**De brinde:** se o paciente não tem exame no SISCAN, o painel do CNS volta **sem** a tabela
`listaExamePacientePaginada`. A ausência da tabela é, portanto, uma leitura barata de "esse CNS
ainda não tem exame no SISCAN" — sem passar pela pesquisa com período obrigatório.

## 6d. O que a NOSSA anamnese já responde

Confrontado com `SMSMais.front/src/features/anamnese` (questionário `mamografia` v1, CDT Maricá —
o formulário de papel digitalizado). Das 6 perguntas obrigatórias do SISCAN:

| Pergunta obrigatória do SISCAN | Na nossa anamnese |
|---|---|
| TEM NÓDULO OU CAROÇO NA MAMA? (por lado) | **temos, e melhor**: `queixas.sintomas.noduloPalpavel` é por mama (direita/esquerda), mais o diagrama com marcações e `avaliacaoClinica.alteracoesPalpaveis` |
| APRESENTA RISCO ELEVADO PARA CÂNCER DE MAMA? | **temos, com régua diferente**: seção 5 tem 4 critérios objetivos + `classificacao` Baixo/Moderado/Alto. O SISCAN quer Sim/Não/Não sabe — **falta decidir o de-para do "Moderado"** |
| TEVE AS MAMAS EXAMINADAS POR PROFISSIONAL ANTES DESTA CONSULTA? | **não temos** — nada equivalente |
| FEZ MAMOGRAFIA ALGUMA VEZ? (+ **ano** da última) | temos o Sim/Não (`historicoClinico.jaRealizouMamografia`); o **ano** só se alguém escreveu na observação livre — não é campo |
| FEZ RADIOTERAPIA NA MAMA OU NO PLASTRÃO? (+ lado + ano) | **não temos** — "radioterapia" só existe no cadastro de tipos de tratamento (TFD), nada na anamnese |
| FEZ CIRURGIA DE MAMA? (+ 13 tipos × 2 lados, com ano) | temos o Sim/Não (`jaRealizouCirurgiaMamaria`) e `possuiProteseMamaria` (→ inclusão de implantes); faltam **tipo, lado e ano** estruturados (o traço no diagrama dá o lado) |
| População-alvo do rastreamento (01/02/03) | **derivável** da seção 5: familiar 1º grau ou câncer antes dos 50 na família → `02`; histórico pessoal de câncer → `03`; senão `01` |

**Placar: 2 respondidas, 2 parciais (falta o ano/estrutura), 2 ausentes.**

**Resolvido em 22/09/2026 — anamnese v2.** O questionário `mamografia` ganhou a versão 2, com o
bloco `siscan`: mamas já examinadas antes, radioterapia (resposta → lado → ano por lado), ano da
última mamografia e as cirurgias como lista de `tipo × lado × ano`. As chaves dos 13 tipos
espelham o nome do campo deles (`biopsiaCirurgicaIncisional` → `frm:anoBiopsiaCirurgicaIncisionalDireita`),
para o de-para ser conferível a olho. O backend não mudou: ele guarda o conteúdo como JSON opaco
e só valida que é JSON com `versao >= 1`. Anamnese v1 continua abrindo — o merge sobre o shape
vazio deixa as perguntas novas em branco.

E o contrário também vale — **a nossa anamnese já responde o que o SISCAN pergunta no LAUDO**:
`aindaMenstrua` + data da última menstruação, `fazUsoHormonios` e `estaGestante` são exatamente
as três perguntas que o `APRENDIZADOS` §3 apontou como estando "na tela errada" do SISCAN
(ficam no laudo, preenchidas depois que a paciente foi embora). Nós já as colhemos com a
paciente na frente.

Colhemos ainda, e o SISCAN não pede: histórico familiar de câncer de **ovário**, ultrassonografia
mamária prévia, tabagismo, anticoncepcional, nº de filhos, e os sintomas por mama (dor, secreção
mamilar, alteração na pele, vermelhidão, retração, edema).

**Conclusão para o produto:** para gerar requisição no SISCAN a partir da nossa anamnese faltam
**três campos** — "mamas já examinadas antes", "radioterapia na mama/plastrão (lado + ano)" e o
**ano** da última mamografia/cirurgia —, mais uma decisão de negócio sobre como traduzir
"Moderado" para o Sim/Não/Não sabe do risco elevado.

## 7. O que isso responde, e o que não responde

**Responde:** criar requisição pela nossa plataforma é viável por HTTP, e o caro (identidade do
paciente) sai de graça do CNS.

**Não responde:** a pergunta nº 1 do handoff — *quem digita no SISCAN as requisições das USF*.
Agora ela tem uma pista forte: o combo Responsável é **por unidade requisitante**, e a conta do
CDT enxerga as 37 unidades e os profissionais de cada uma. Ou seja, **o CDT consegue criar
requisição em nome de qualquer USF** — o que explicaria requisições que a USF não consegue
alterar depois. Confirmar com a operação antes de prometer qualquer coisa.

**Também em aberto:** se `Salvar` exige o Nº do Prontuário; o que o protocolo devolve; e se
existe caminho de exclusão/refazimento quando o responsável sai errado.


## 8. O Salvar, medido (uma criação real — 22/09/2026)

Autorizada por ação. Caso: ficha SISREG de uma mamografia bilateral cuja anamnese já existia no
nosso sistema (preenchida no dia do exame), unidade requisitante = a USF da ficha, responsável =
a profissional que assinou a ficha. Ferramenta: [`criar_requisicao.py`](../criar_requisicao.py),
que **por padrão é ensaio** — imprime o POST exato e não envia; só `--confirmar` clica Salvar, e
só então a trava libera a palavra `salvar`.

### O que o Salvar devolve

Modal `formModalExameSalvo`, com o texto:

> PROTOCOLO DA REQUISIÇÃO — Registro salvo com sucesso! O número do protocolo gerado é
> **`00000141043026`**

E os botões *Imprimir*, *Novo Exame*, *Inserir Resultado*, *Voltar*.

Três coisas que só a medição mostra:

1. **O protocolo vem com zeros à esquerda, 14 posições** (`00000141043026`), enquanto a grade do
   Gerenciar Exame mostra o mesmo número **sem** eles (`141043026`). Quem guardar a string do modal
   e comparar com a da grade não vai casar nada. Normalizar: guardar sem zeros, ou sempre com.
2. **O Nº do Exame NÃO vem no modal.** Ele é outro número (`141108550` neste caso) e só aparece
   relendo a grade, embutido no id das ações. Se o carimbo precisa dos dois — e precisa, porque a
   tela deles pesquisa por ambos —, **a releitura é obrigatória**, não é zelo.
3. **A resposta do POST não traz erro nenhum quando dá certo**, e "Registro salvo com sucesso"
   está no HTML antes mesmo de existir requisição. A prova é a releitura.

### A releitura pelo nosso próprio número funciona

Gravamos o `AccessionNumber` no **Nº do Prontuário** e a pesquisa por `Nº Prontuário` devolveu
exatamente 1 linha, com paciente, CNS, protocolo, unidade requisitante e status `Requisitado`.
A ponte de volta está de pé — é ela que resolve idempotência e reconciliação.

### Quem cria, altera

A requisição recém-criada, aberta pela ação *Alterar/Visualizar*, veio com
`<h1>SOLICITAR REQUISIÇÃO DO EXAME DE MAMOGRAFIA</h1>` e botão **Salvar** — ou seja, **modo
editável**. É evidência forte para a hipótese que estava aberta desde 07/08/2026: o que decide
editável × travada não é o prazo, é **ser de quem criou**. Não é prova definitiva (só um caso, e
recém-criado), mas é a primeira medição do lado "criado por nós".

Todas as 8 respostas voltaram exatamente como enviadas, o prontuário persistiu e o conselho
(`CRM - …`) veio derivado e disabled.

### Outras coisas medidas de graça

- **Data da Solicitação retroativa é aceita**: gravamos a data da ficha do SISREG, três meses
  atrás, sem reclamação. Útil, porque o caso real é justamente registrar no SISCAN um exame que
  já aconteceu.
- **Do modal dá para ir direto a *Inserir Resultado***, que é o caminho da médica para o laudo.
- **Não sabemos se o Nº do Prontuário é obrigatório** — mandamos preenchido. Não testar isso em
  produção só para saber.


## 9. O SISCAN não tem sessão única (medido em 22/09/2026)

No SISREG e no SER, um login novo derruba a sessão anterior daquele operador — inclusive a do
humano que está trabalhando. Era a suposição natural aqui também, e é **falsa**.

`probe_sessao_unica.py` abre duas sessões com a mesma credencial e faz a primeira ler de novo
depois que a segunda entrou. As duas continuaram vivas, com o menu completo.

**Consequência:** o painel pode autenticar no SISCAN sem derrubar a atendente que está com a tela
dela aberta. E as sondas do laboratório podem rodar sem atrapalhar ninguém — o que, aliás, já
vinha acontecendo: cada sonda faz um login novo.

É a segunda vez que a analogia entre os sistemas engana (a "sessão única herdada do SISREG" também
era falsa no SER). **Medir, não deduzir.**
