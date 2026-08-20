# SER — a tela de CRIAR solicitação (aba *Editar*)

Levantado por sonda somente-leitura em 08/08/2026 (`Automais.SER/probe_campos_dinamicos.py`).
Complementa [`docs/ser.md`](./ser.md), que cobre a leitura da fila.

> **Nada foi criado no SER.** A sonda só troca aba e combos, que apenas re-renderizam a view.
> O botão `Gravar` (`form0:j_id313`) nunca foi acionado — e a trava da sonda recusa qualquer
> parâmetro com verbo de escrita.

## 1. Como se chega lá

Mesma tela da fila (`solicitar-consulta-pesquisar.seam`), aba **Editar**. A troca de aba é
`_JSFFormSubmit` — **POST comum**, sem `AJAXREQUEST`, mandando os campos do form mais:

```
form0:editar_server_submit = form0:editar_server_submit
```

## 2. O formulário tem duas partes

Um **bloco fixo**, igual para todo pedido, e um **bloco dinâmico** (`form0:camposDinamicos`)
que muda conforme o Recurso escolhido. É o bloco dinâmico que responde à pergunta "por que
oncologia pede campos diferentes".

### 2.1 Bloco fixo

| Campo | id JSF | Tipo | Obrigatório |
|---|---|---|---|
| É Ambulatório Estadual? | `form0:comboSisReg` | select (Sim/Não) |  |
| Tipo | `form0:comboTipoRecurso` | select (CONSULTA/EXAME) | sim |
| Recurso | `form0:comboRecurso` | select (populado por Tipo) | sim |
| Recurso (autocomplete) | `form0:suggRecurso` | suggestionbox |  |
| CNS do paciente | `form0:numeroCADSUS` | text |  |
| Médico solicitante identificado? | `form0:booleanMedicoSolicitanteIdentificado_radio` | radio S/N |  |
| Médico responsável | `form0:medicoResp` | select (876 opções) |  |
| Telefone do médico | `form0:telefoneCelularMedico` | text |  |
| Especialidade do médico | `form0:especialidadeMedico` | text |  |
| Classificação de Risco | `form0:classificacao_risco` | select (Prioridade 1–4) | sim |
| Hipótese | `form0:procedimento` | **suggestionbox de CID** (não é texto — §2.1.3) | sim |
| Mandado Judicial | `form0:naturezaSolicitacaoMandato_radio` | radio S/N |  |
| Unidade de origem identificada? | `form0:unidadeDeOrigemIdentificada_radio` | radio S/N |  |
| Unidade de origem (texto livre) | `form0:unidadeNaoIdentificada` | text | sim |

Ações da aba: `form0:addMedico` (Adicionar médico), `form0:j_id299` (Anexar Arquivo) e
**`form0:j_id313` (Gravar)** — este último é escrita e está fora de qualquer uso nosso.

#### "É AMBULATÓRIO ESTADUAL?" não é um campo — é um interruptor de catálogo

Medido em 10/08/2026 (`probe_ambulatorio_estadual.py` e `probe_sisreg_detalhe.py`). Este combo é o
**primeiro** da aba e tem `onchange` A4J próprio (`form0:j_id46`). A tabela acima o listava como
um select comum, o que escondeu que ele **troca o catálogo inteiro**:

| | Não (= o default "Selecione…") | Sim |
|---|---|---|
| CONSULTA | 120 recursos | **151** — 31 que não existem no outro ramo |
| EXAME | 83 recursos | 64 (subconjunto dos 83) |

As 31 exclusivas do "Sim" são as de nome em caixa alta, estilo SISREG: urologia (11), pneumologia
(4), reumatologia (4), fonoaudiologia, alergologia, hepatologia, nutrição pediátrica, fisiatria,
polissonografia, homeopatia infantil, LECO, reabilitação em mastectomias e consulta de enfermagem.

**E o ramo muda o formulário do MESMO recurso.** O recurso 1000 pede 9 campos no "Não" (sinais e
sintomas, NYHA, laudo de ecocardiograma, estudo eletrofisiológico…) e apenas 3 no "Sim" (Queixa
Principal, Resultado de Exames, Observações).

> Consequência para a modelagem: **(tipo, valor) não identifica um recurso** — só
> (tipo, ramo, valor). É por isso que `ser_catalogo_recurso` tem `ambulatorio_estadual` na chave
> natural, e por isso a cópia do catálogo varre os dois ramos.

E a ordem dos combos é obrigatória: **ramo → tipo → recurso**. Cada troca é uma conversa Seam
acumulativa; escolher o tipo antes do ramo devolve a lista do ramo errado sem erro nenhum.

> Detalhe do placeholder: este combo usa a string literal `"null"` para "Selecione…", e não o
> `org.jboss.seam.ui.NoSelectionConverter` dos demais. Quem filtrar só pelo converter deixa
> "Selecione…" virar uma terceira alternativa ao lado de Sim e Não.

#### Os três radios do bloco fixo: dois bifurcam o formulário

Sondados em 10/08/2026 (`probe_radios_bloco_fixo.py`) depois que o combo de ambulatório estadual
mostrou que "select comum" não é diagnóstico. Os três disparam A4J — ou seja, **os três
re-renderizam alguma coisa** — mas só dois trocam campos:

| Radio | Sim | Não |
|---|---|---|
| `booleanMedicoSolicitanteIdentificado_radio` | `medicoResp` (select, 873), `telefoneCelularMedico`, `especialidadeMedico` **\*** | `nomeMedicoResponsavelNaoIdentificado` **\*** |
| `unidadeDeOrigemIdentificada_radio` | `suggUnidadeOrigem` (suggestionbox, `minChars:1`) | `unidadeNaoIdentificada` **\*** |
| `naturezaSolicitacaoMandato_radio` | — | — |

`*` = obrigatório no SER. **Cada caminho tem pelo menos um campo obrigatório próprio**, então
"obrigatório" aqui não é atributo do campo: depende da resposta do radio. Validação que só olhe a
lista de campos vai deixar passar pedido incompleto.

O `suggUnidadeOrigem` é um `rich:suggestionbox` — mesmo protocolo do Solicitante descrito em
[`ser.md` §4.3](./ser.md): fetch com `inputvalue` + `ajaxSingle`, depois `onselect` gravando o
índice em `form0:j_id264_selection`, que é **transitório**. O `j_id264` sai do
`Richfaces.onAvailable` da própria página, nunca chumbado.

> **Armadilha do vocabulário (custou um dia, 20/08/2026):** o combo de Tipo só aceita `CONSULTA`
> e `EXAME`. O nosso domínio chama isso de `Consulta`/`Exame` (o enum `TipoRecursoSer`, que é o
> que a tela manda). Passar o valor do nosso lado direto ao SER **não dá erro**: ele não aplica a
> troca e devolve a view intacta — o combo de recurso vem vazio, o recurso não amarra, e o
> autocomplete de CID responde "Nenhum CID encontrado" para qualquer termo, inclusive para um
> código que aquele recurso aceita. A conversão está em `SerNovaSolicitacaoService.TipoParaOSer`,
> no `PrepararAsync`, com teste de regressão.

> Estado em 20/08/2026: a tela de nova solicitação manipula **três** dos treze nomes JSF do bloco
> fixo (`classificacao_risco`, `medicoResp` e, desde hoje, `procedimento` — a Hipótese, §2.1.3).
> Os três radios e os cinco campos condicionais ainda não estão implementados — pedido montado
> hoje seria recusado por falta de médico e de unidade de origem. É pendência conhecida, anterior
> a ligar o envio.

#### 2.1.3 A Hipótese é uma caixa de CID — e a lista é do RECURSO

Medido em 20/08/2026 (`Automais.SER/probe_cid.py`), depois que a nossa tela de nova solicitação
apareceu com a Hipótese como campo de texto e sem listar CID nenhum. A tabela de §2.1 dizia
`text`; está errada. `form0:procedimento` é um `rich:suggestionbox` (o input tem
`alt="Digite o nome ou o código"`), e o SER guarda a hipótese pelo **CID escolhido**, não pelo
texto — foi por isso que a edição de 10/08/2026 respondeu "salva com sucesso" e voltou vazia.

**Sem Recurso escolhido, não existe CID nenhum.** Qualquer termo — inclusive o código exato —
devolve a linha "Nenhum CID encontrado":

| termo | sem recurso | com o recurso 1003 (Cardiologia) |
|---|---|---|
| `A09` | 0 | 1 |
| `diab` | 0 | 78 |
| `I10` | 0 | 1 |

**E a relação MUDA de recurso para recurso** — não é o CID-10 inteiro, é o que aquele
procedimento aceita:

| termo | 1003 — Cardiologia (Cardiopatia Congênita) | 1063 — Cirurgia Geral (Oncologia) |
|---|---|---|
| `diab` | 78 | **0** |
| `hipert` | 75 | **0** |
| `M54` | 10 | **0** |
| `neopl` | 500 (no teto) | 108 |
| `malig` | 395 | 21 |
| `C50` | 10 (com as subcategorias C500…C509) | 1 (só a categoria `C50`) |

> Consequência direta: **não existe UMA lista de CID a servir**. Uma tabela de CID-10 baixada de
> fora ofereceria ao operador um código que o SER recusa na hora de gravar — e ele recusa em
> silêncio. Por isso o recurso e o ramo fazem parte da pergunta em
> `GET /regulacao/ser/configuracao/nova-solicitacao/cids`.

#### Mas são só DUAS listas — e por isso dá para espelhar

Medido em 20/08/2026 (`Automais.SER/probe_cid_grupos.py`), assinando cada recurso com três buscas
(`diab`, `malig`, `Z9`) e agrupando:

| ramo | recursos | listas distintas |
|---|---|---|
| Não | 204 (121 consultas + 83 exames) | **2** |
| Sim | 218 (154 consultas + 64 exames) | **1** |

| lista | quem usa | tamanho |
|---|---|---|
| ampla | 390 recursos | **14.226 CID** — o CID-10 inteiro |
| restrita | 32 recursos, todos com "(Oncologia)" no nome | **136 CID** |

A restrita é **subconjunto perfeito** da ampla (interseção 136, nada fora). E duas varreduras
completas de recursos sem nada em comum — Cardiologia (1003) e Cranio Maxilo Facial (1018) —
devolveram conjuntos **idênticos**: 14.226 = 14.226, zero de diferença dos dois lados.

> É isso que torna a cópia viável: varre-se uma vez por **lista** (260 buscas por prefixo, ~25s),
> não uma vez por recurso. Espelhar por recurso custaria 422 varreduras.

O espelho mora em `ser_catalogo_cid_lista` + `ser_catalogo_cid`, e cada recurso guarda a
assinatura medida (`cid_assinatura`) e a lista a que pertence (`cid_lista_id`). A cópia roda junto
com o sync de catálogo, em duas passadas — medir todos os recursos numa conversa Seam só, depois
copiar uma lista por assinatura nova. Intercalar as duas coisas destruiria o estado da conversa da
medição, e as assinaturas seguintes sairiam do recurso errado.

**Recurso sem lista copiada cai no autocomplete ao vivo** — a fonte continua sendo o SER, o
espelho é só a comodidade.

#### O mesmo nome, duas listas: quem decide é o RAMO

Medido em 20/08/2026 (`Automais.SER/probe_cid_mastologia.py`), a partir de um caso real de
operação. "Mastologia" existe nos dois ramos, com listas opostas:

| ramo | recurso | `I64` | `Acidente` | `C50` | campo vazio |
|---|---|---|---|---|---|
| Não | `1059 Ambulatório 1ª vez - Mastologia (Oncologia)` | **0** | **0** | 1 (`C50`) | 136 |
| Não | `1069 Ambulatório 1ª vez em Mastologia - Lesão Impalpável (Oncologia)` | **0** | **0** | 1 | 136 |
| Sim | `1026 CONSULTA EM MASTOLOGIA` | 1 | 378 | 10 (com C500…C509) | 500 (teto) |

> Ou seja: "não acho o I64 em mastologia" pode ser o SER funcionando como deve. No ramo "Não" o
> recurso é o oncológico e a lista tem 136 códigos de neoplasia; no "Sim" é a consulta comum
> (nome em caixa alta, estilo SISREG — uma das 31 exclusivas daquele ramo) e a lista é o CID-10
> inteiro. **Antes de tratar como defeito, conferir em que ramo o operador está.**

#### Campo vazio lista tudo

Apagar o texto não é "sem filtro proibido", é o comportamento do próprio SER: `inputvalue` vazio
devolve a lista inteira até o teto — 136 no oncológico, 500 no amplo. A nossa tela faz o mesmo ao
abrir a caixa, e por isso não há mínimo de caracteres.

> Detalhe: a busca do SER é `contém` na string inteira. Colar `"I64 Acidente Vascular"` devolve
> **zero** lá, porque essa string não existe nem no código nem na descrição. No espelho a coluna
> `busca` é `código + descrição`, então o mesmo texto colado encontra — melhor que o SER, sem
> oferecer nada que ele não aceite.

**A busca casa código e descrição**, sem exigir acento: `hipert` traz `E05`, `G932`, `I10`…
O SER corta em **500 linhas** por busca (um termo de uma letra volta com exatamente 500); a nossa
resposta marca `truncado` para a tela pedir um termo mais específico em vez de fingir que aquilo é
tudo. É também o que obriga a cópia a enumerar por prefixo de dois caracteres, que cabe folgado
(`A0` → 68, `M5` → 32, `Z9` → 90) — e a refinar com um terceiro caractere qualquer prefixo que
volte no teto, em vez de aceitar uma lista incompleta com cara de completa.

> O dado do SER é irregular no acento — "Diarréia" tem, "Hipertensao" não. O espelho guarda uma
> coluna `busca` sem acento e normaliza o termo digitado do mesmo jeito (`SerCidBusca`), para que
> "hipertensão" ache "Hipertensao". Isso não amplia o que é oferecido: o conjunto continua sendo
> exatamente o que o SER devolveu.

**A linha da tabela tem três células, e a que importa é a primeira**, que vem com
`style="display: none"`:

```html
<tr class="rich-sb-int richfaces_suggestionEntry">
  <td style="display: none;">(A09 ) Diarréia e gastroenterite de origem infecciosa presumível</td>
  <td class="rich-sb-cell-padding">A09</td>
  <td class="rich-sb-cell-padding">Diarréia e gastroenterite de origem infecciosa presumível</td>
</tr>
<tr id="form0:j_id226:0NothingLabel" style="display: none;"><td>Nenhum CID encontrado</td></tr>
```

É a coluna oculta — `(A09 ) …`, código preenchido com espaço até 4 caracteres, entre parênteses,
mais a descrição — que o navegador escreve no campo, e é ela que o pedido leva de volta em
`form0:procedimento`. Guardar só o código, ou só a descrição, dá pedido sem hipótese.
E a `NothingLabel` vem escondida em **toda** resposta, inclusive nas que têm resultado: descarte
pela forma da linha (uma célula só), nunca pelo texto da mensagem.

> Para o envio, vale a regra do §2.1 do `probe_criar_solicitacao.py`: são **as duas coisas** —
> a amarração A4J (fetch + `onselect` com o índice no `_selection`) **e** o texto no campo. O
> `onselect` responde `Ajax-Update-Ids` vazio; a escolha mora só na conversa Seam. Nossa tela hoje
> faz apenas o fetch (é consulta); a amarração entra junto com o envio.

### 2.2 Bloco dinâmico

Cada campo é um par `form0:container_dinamico_id_<N>` (rótulo) + `form0:dinamico_id_<N>`
(o campo). Trocar o Recurso dispara A4J (`form0:j_id57`, com `ajaxSingle=form0:comboRecurso`)
e o SER devolve o bloco re-renderizado. O `oncomplete` chama `verificarPetCt()` — o PET-CT tem
regra própria, e de fato é o único recurso com formulário exclusivo (§4).

> **A resposta é PARCIAL**: não traz `<form id="form0">`. Vale a regra de sempre — usar a
> última página completa como fonte dos campos e o parcial só como fonte do conteúdo novo.

#### Os cinco tipos de campo, e por onde cada um posta

Capturado em 10/08/2026 (`probe_radio_dinamico.py`, CONSULTA 995 e 1007). O nome que o SER lê
**nem sempre é o id base do container** — foram dois defeitos em produção por causa disso.

| Tipo | Marcação no SER | Nome que recebe o valor |
|---|---|---|
| `text` / `textarea` / `select` | `<input>`, `<textarea>`, `<select>` | `form0:dinamico_id_<N>` |
| `date` | `rich:calendar` | **`form0:dinamico_id_<N>InputDate`** |
| `radio` | `<table class="radioButton">` | `form0:dinamico_id_<N>` |
| `checkbox` | `<table class="checkBox">` | `form0:dinamico_id_<N>`, **repetido** |

**Data** (§ regressão 10/08): o `rich:calendar` embute um `<script>` com a localização inteira do
calendário *dentro* do container — ler o texto cru transforma o rótulo em
`"Data da coleta da biópsia://<![CDATA[ Richfaces.Calendar.addLocale('pt'…"`. E o valor não viaja
no id base: vai no input irmão terminado em `InputDate`. `InputCurrentDate` existe no mesmo
componente e **não** é o campo.

**Escolha** (radio e checkbox): não são `<select>` — são uma tabela com **um `<input>` por opção**,
todos com o mesmo `name`, e o texto de cada opção num `<label for="<id da opção>">`:

```html
<span><label>Grupo Sanguineo:</label></span>          <!-- rótulo do CAMPO: sem `for` -->
<table class="radioButton" id="form0:dinamico_id_379">
  <td><input id="form0:dinamico_id_379:0" name="form0:dinamico_id_379" type="radio" value="Tipo A"/>
      <label for="form0:dinamico_id_379:0">Tipo A</label></td>   <!-- rótulo da OPÇÃO: com `for` -->
```

> Medido em 22 containers reais: **o rótulo do campo nunca tem `for`; o das opções sempre tem.**
> É essa assimetria que separa os dois — por isso o `<label for>` só é descartado em grupo de
> marcação, nunca num campo comum.

`value` é o texto em si ("Tipo A", "Diabetes"), não um código. E **checkbox é múltipla escolha**:
o JSF posta o mesmo nome uma vez por marcado
(`…id_594=Diabetes&…id_594=Depressão`). Como o rascunho guarda par nome→valor, os valores viajam
juntos separados por `\n` e se desdobram no envio — contrato em `SerValorMultiplo`.

## 2.3 Anexar arquivo — como o upload funciona

Levantado em 08/08/2026 lendo o `ui.pack.js` do próprio SER. **Nunca exercitado**: subir arquivo
é escrita, e a sonda para antes disso. O que está aqui é o protocolo, não uma prova de execução.

O botão *Anexar Arquivo* (`form0:j_id299`) é um A4J cujo `oncomplete` abre o modal
`modalAnexarArquivo`. Dentro do modal vive **outro form**, separado do `form0`:

| | |
|---|---|
| Form | `formAnexar`, `enctype="multipart/form-data"` |
| Componente | `rich:fileUpload` (`formAnexar:upload`) |
| Campo do arquivo | `formAnexar:upload:file` |
| Botões | `formAnexar:j_id320` (**Anexar** — escrita) e `formAnexar:j_id319` (Cancelar) |
| Limites declarados | `maxFileBatchSize: 2`, `noDuplicate: true` |

**O envio dos bytes não usa o `action` do form.** O RichFaces reescreve o `action` na hora e
submete num iframe escondido — um POST `multipart/form-data` por arquivo, para:

```
/ser/pages/consultas-exames/solicitacao/solicitar-consulta-editar.seam
  ?_richfaces_upload_uid=<uid aleatório>
  &formAnexar:upload=formAnexar:upload
  &_richfaces_upload_file_indicator=true
  &AJAXREQUEST=_viewRoot
```

Antes de submeter, o componente **desabilita todos os outros `input[type=file]`** do form, para
que vá exatamente um arquivo por requisição. O progresso e o cancelamento andam por fora, num A4J
paralelo com `_richfaces_file_upload_action` + `_richfaces_upload_uid`.

Há um caminho alternativo por **Flash** (`FileUploadComponent.swf`), que monta a mesma URL mas
embute `;jsessionid=` no caminho e acrescenta `_richfaces_size` e `_richfaces_send_http_error`.
Para isso, **o `JSESSIONID` é impresso em texto claro no HTML da página**, como argumento do
construtor do componente — o plugin não enxerga cookie. É observação de segurança do alvo, não
algo que a gente use.

Os anexos já enviados aparecem em `form0:anexoList`, com as colunas **Data, Nome do Arquivo,
Usuário e Ação**.

> **Para uma futura integração de escrita**, o anexo é o passo mais delicado: são duas conversas
> distintas (o `form0` do pedido e o `formAnexar` do arquivo) amarradas pela mesma sessão Seam,
> e o arquivo sobe *antes* de o pedido ser gravado.

## 3. O catálogo medido

**203 recursos** (120 consultas + 83 exames)
produzem **21 formulários distintos**, com **163 campos dinâmicos únicos**
(90 obrigatórios).

O formulário padrão — **Queixa Principal, Resultado de Exames, Observações**, todos
obrigatórios — cobre 138 dos 203 recursos. O resto é especialidade pedindo dado clínico.


## 4. CONSULTA — 120 recursos, 15 formulários

### 63 recurso(s) · 3 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Queixa Principal | `textarea` |
| **obrig.** | Resultado de Exames | `textarea` |
| **obrig.** | Observações | `textarea` |

- Ambulatório 1ª vez em Cardiologia - Cardiopatia Congênita (Adulto)
- Ambulatório 1ª vez em Cardiologia - Cirurgia Cardíaca Pediátrica
- Ambulatório 1ª vez em Cardiologia - Hipertensão Arterial Resistente (Adulto)
- Avaliação de Cardiopatia Congênita Pediátrica (Internados)
- Reabilitação Cardíaca
- Ambulatório 1ª vez em Cardiologia - Doenças Neuromusculares
- *… e mais 57*

### 30 recurso(s) · 9 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Peso do Paciente (gramas) | `text` |
| **obrig.** | Altura do Paciente (cm) | `text` |
| **obrig.** | IMC do Paciente | `text` |
|  | Paciente já realizou cirurgia oncológica? Sim Não | `radio` |
|  | Data da coleta da biópsia | `text` |
|  | Data do resultado da biópsia | `text` |
| **obrig.** | Queixa Principal | `textarea` |
| **obrig.** | Resultado de Exames | `textarea` |
| **obrig.** | Observações | `textarea` |

- Ambulatório 1ª vez em Cirurgia Plástica Reparadora - Mama (Oncologia)
- Ambulatório 1ª vez - Hematologia (Oncologia)
- Ambulatório 1ª vez - Oncologia Geral (Adulto)
- Ambulatório 1ª vez - Mastologia (Oncologia)
- Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia (Oncologia)
- Ambulatório 1ª vez - Urologia (Oncologia)
- *… e mais 24*

### 4 recurso(s) · 5 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Descreva o Tratamento Conservador realizado, se houver | `textarea` |
| **obrig.** | Quanto tempo durou o tratamento? | `text` |
| **obrig.** | Observações | `textarea` |
| **obrig.** | Queixa Principal | `textarea` |
| **obrig.** | Resultado de Exames | `textarea` |

- Ambulatório 1ª vez - Cranio Maxilo Facial (Infantil)
- Ambulatório 1ª vez - Cranio Maxilo Facial (Adulto)
- Ambulatório 1ª vez - Microcirurgia Reconstrutora (Adulto)
- Ambulatório 1ª Vez em Ortopedia - Reconstrução e Alongamento Ósseo (Fixador Externo)

### 4 recurso(s) · 8 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Peso do Paciente (gramas) | `text` |
| **obrig.** | IMC do Paciente | `text` |
| **obrig.** | Comorbidades: Diabetes Hipertensão Doenças articulares Doenças vasculares Depressão | `checkbox` |
|  | Exames Complementares (Data e Laudo) | `textarea` |
|  | Medicação em Uso (especificar Droga, Dosagem, e Tempo de Uso) | `textarea` |
|  | Laudo/Anamnese | `textarea` |
| **obrig.** | Altura do Paciente (cm) | `text` |
|  | Outras comorbidades | `textarea` |

- Readequação Corporal Pós-Cirurgia Bariátrica
- Ambulatório 1ª vez - Cirurgia Bariátrica (Adulto)
- Ambulatório 1ª vez - Cirurgia Bariátrica - Superobesidade (IMC acima 55)
- Ambulatório 1ª Vez em Gestação pós cirurgia bariátrica

### 4 recurso(s) · 4 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |
| **obrig.** | Condições que Justificam a Internação | `textarea` |
|  | Principais Resultados de Provas Diagnósticas (Resultados de Exames Realizados) | `textarea` |
|  | Observações | `textarea` |

- Avaliação diagnóstica infecção congênita Zika/Storch/Oropuche
- Ambulatório de 1ª Vez - Transplante de Fígado (Infantil)
- Ambulatório de 1ª Vez - Transplante Renal (Adulto)
- Ambulatório de 1ª Vez - Transplante de Fígado (Adulto)

### 3 recurso(s) · 13 campos

| | Campo | Tipo |
|---|---|---|
|  | Medicação em uso | `textarea` |
|  | Rx de Tórax | `textarea` |
|  | Hemograma Completo | `textarea` |
|  | Coagulograma | `textarea` |
|  | Glicose | `text` |
|  | Ureia | `text` |
|  | Creatinina | `text` |
|  | Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB | `radio` |
|  | Fator RH | `text` |
|  | Descrição do Laudo do Ultrasson Doppler Arterial | `textarea` |
|  | Descrição do Laudo da Arteriografia | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cirurgia Vascular - Vasculopatia Arterial Periférica
- Ambulatório 1ª vez em Cirurgia Vascular - Pé diabético
- Ambulatório 1ª vez em Cirurgia Vascular - Vasculopatia Carotídea

### 2 recurso(s) · 6 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Medicação em uso | `textarea` |
| **obrig.** | Resultado de exames pré-operatórios de rotina (hemograma completo e coagulograma) | `textarea` |
| **obrig.** | Descrição do laudo do ECG | `textarea` |
| **obrig.** | Telefone de Contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cardiologia - Arritimias (Infantil)
- Ambulatório 1ª vez em Cardiologia Estudo Eletrofisiológico / Ablação

### 2 recurso(s) · 13 campos

| | Campo | Tipo |
|---|---|---|
|  | Classificação funcional da New York Heart Associaton (NYHA): I Atividade física comum como | `select` |
|  | Medicação em uso | `textarea` |
|  | Rx de Tórax | `textarea` |
|  | Hemograma Completo | `textarea` |
|  | Coagulograma | `textarea` |
|  | Glicose | `text` |
|  | Ureia | `text` |
|  | Creatinina | `text` |
|  | Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB | `radio` |
|  | Fator RH | `text` |
|  | Descrição do laudo do ECG e ou Holter | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cardiologia - Implante de Marcapasso
- Ambulatório 1ª vez em Cardiologia - Implante de Ressincronizador Cardíaco

### 2 recurso(s) · 14 campos

| | Campo | Tipo |
|---|---|---|
|  | Classificação de Stanford: Tipo A) Dissecções em que há o comprometimento da aorta ascende | `select` |
|  | Medicação em uso | `textarea` |
|  | Rx de Tórax | `textarea` |
|  | Hemograma Completo | `textarea` |
|  | Coagulograma | `textarea` |
|  | Glicose | `text` |
|  | Ureia | `text` |
|  | Creatinina | `text` |
|  | Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB | `radio` |
|  | Fator RH | `text` |
|  | Descrição do Laudo do Cateterismo | `textarea` |
|  | Descrição do Laudo da TC ou Ultrasson detalhado com medidas do diâmetro da Aorta | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cirurgia Cardiovascular - Aneurisma / Dissecção de Aorta Torácica
- Ambulatório 1ª vez em Cirurgia Vascular - Aneurisma / Dissecção de Aorta Abdominal

### 1 recurso(s) · 14 campos

| | Campo | Tipo |
|---|---|---|
|  | Classificação funcional da New York Heart Associaton (NYHA): I Atividade física comum como | `select` |
|  | Medicação em uso | `textarea` |
|  | Rx de Tórax | `textarea` |
|  | Hemograma Completo | `textarea` |
|  | Coagulograma | `textarea` |
|  | Glicose | `text` |
|  | Ureia | `text` |
|  | Creatinina | `text` |
|  | Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB | `radio` |
|  | Fator RH | `text` |
|  | Descrição do laudo do Cateterismo | `textarea` |
|  | Descrição do laudo do Ecocardiograma | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cirurgia Cardiovascular - Cirurgia Orovalvar

### 1 recurso(s) · 15 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Classificação funcional da Sociedade Canadense de Cardiologia ( CSCC ): I Paciente cardiop | `select` |
| **obrig.** | Classificação funcional da New York Heart Associaton (NYHA): I Atividade física comum como | `select` |
| **obrig.** | Medicação em uso | `textarea` |
|  | Rx de Tórax | `textarea` |
| **obrig.** | Hemograma Completo | `textarea` |
| **obrig.** | Coagulograma | `textarea` |
| **obrig.** | Glicose | `text` |
| **obrig.** | Ureia | `text` |
| **obrig.** | Creatinina | `text` |
|  | Grupo Sanguineo: Tipo A Tipo B Tipo O Tipo AB | `radio` |
|  | Fator RH | `text` |
| **obrig.** | Descrição do laudo do Cateterismo | `textarea` |
| **obrig.** | Descrição do laudo do Ecocardiograma | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª vez em Cardiologia - Cirurgia de Revascularização do Miocárdio

### 1 recurso(s) · 9 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Medicação em uso | `textarea` |
| **obrig.** | Classificação ;funcional da New York Heart Associaton (NYHA): I Atividade física comum com | `select` |
| **obrig.** | Resultado de exames pré-operatórios de rotina (Rx de tórax, hemograma completo, coagulogra | `textarea` |
| **obrig.** | Descrição do laudo do Ecocardiograma, com função de VE [colocando o % da fração de ejeção  | `textarea` |
| **obrig.** | Descrição do laudo do ECG e ou Holter, com a duração do QRS | `textarea` |
| **obrig.** | Resultado do estudo eletrofisiológico | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Ambulatório 1ª Vez em Cardiologia - Implante de Cardiodesfibrilador (CDI)

### 1 recurso(s) · 9 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Classificação funcional da Sociedade Canadense de Cardiologia (CSCC), para angina: I Pacie | `radio` |
| **obrig.** | Classificação funcional da New York Heart Associaton (NYHA), para insuficiência cardíaca:  | `radio` |
| **obrig.** | Medicação em uso | `textarea` |
| **obrig.** | Laudo da Ergometria, Cintilografia, Ecocardiograma com Dobutamina ou ECG e Enzimas | `textarea` |
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |
| **obrig.** | Peso do paciente (gramas) | `text` |
| **obrig.** | Altura do Paciente (cm) | `text` |

- Ambulatório 1ª vez em Cardiologia - Pré Angioplastia Coronariana

### 1 recurso(s) · 6 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |
| **obrig.** | Condições que Justificam a Internação | `textarea` |
|  | Principais Resultados de Provas Diagnósticas (Resultados de Exames Realizados) | `textarea` |
|  | Observações | `textarea` |
|  | Descrição do laudo da TC | `textarea` |
|  | Descrição do laudo da Angiografia Cerebral | `textarea` |

- Ambulatório 1ª vez em Neurocirurgia - Neurovascular

### 1 recurso(s) · 10 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Resultado do histopatológico | `textarea` |
| **obrig.** | Exames complementares | `textarea` |
|  | Observações | `textarea` |
| **obrig.** | Peso do Paciente (gramas) | `text` |
| **obrig.** | Altura do Paciente (cm) | `text` |
| **obrig.** | IMC do Paciente | `text` |
|  | Paciente já realizou cirurgia oncológica? Sim Não | `radio` |
|  | Data da coleta da biópsia | `text` |
|  | Data do resultado da biópsia | `text` |

- Ambulatório 1ª vez - Planejamento em Radioterapia (Infantil)


## 5. EXAME — 83 recursos, 6 formulários

### 75 recurso(s) · 3 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Queixa Principal | `textarea` |
| **obrig.** | Resultado de Exames | `textarea` |
| **obrig.** | Observações | `textarea` |

- Ecocardiograma Transesofágico (ambulatorial)
- Ecocardiograma de Estresse
- Toracocentese
- Biópsia de Pleura
- Biópsia de Gânglio
- Escarro Induzido
- *… e mais 69*

### 3 recurso(s) · 9 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Classificação funcional da Sociedade Canadense de Cardiologia (CSCC), para angina: I Pacie | `radio` |
| **obrig.** | Classificação funcional da New York Heart Associaton (NYHA), para insuficiência cardíaca:  | `radio` |
| **obrig.** | Medicação em uso | `textarea` |
| **obrig.** | Laudo da Ergometria, Cintilografia, Ecocardiograma com Dobutamina ou ECG e Enzimas | `textarea` |
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |
| **obrig.** | Peso do paciente (gramas) | `text` |
| **obrig.** | Altura do Paciente (cm) | `text` |

- Cateterismo Cardíaco (Internados)
- Cateterismo Cardíaco (Ambulatorial)
- Cateterismo Cardíaco Pediatrico (Ambulatorial)

### 2 recurso(s) · 5 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | História clínica | `textarea` |
| **obrig.** | Medicação em uso | `textarea` |
| **obrig.** | Descrição do Laudo do Ultrasson Doppler Arterial e/ou TC | `textarea` |
| **obrig.** | Telefone de contato | `text` |
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |

- Arteriografia Periférica (Ambulatorial)
- Arteriografia Periférica (Internados)

### 1 recurso(s) · 10 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Presença de massa ou neoformação pulmonar? SIM NÃO | `radio` |
| **obrig.** | Necessita de biópsia brônquica ou transbrônquica? SIM NÃO | `radio` |
| **obrig.** | Paciente com hemoptise ou escarro hemoptoico? SIM NÃO | `radio` |
| **obrig.** | Suspeita de Estenose de traquéia/estridor/cornagem? SIM NÃO | `radio` |
| **obrig.** | Paciente intubado? SIM NÃO | `radio` |
| **obrig.** | Paciente traqueostomizado? SIM NÃO | `radio` |
| **obrig.** | Paciente com idade menor ou igual a 16 anos? SIM NÃO | `radio` |
|  | Observações | `textarea` |
|  | Hipótese diagnóstica | `textarea` |
|  | Quadro Clínico | `textarea` |

- Broncoscopia (Internados)

### 1 recurso(s) · 4 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Principais Sinais e Sintomas Clínicos | `textarea` |
| **obrig.** | Condições que Justificam a Internação | `textarea` |
|  | Principais Resultados de Provas Diagnósticas (Resultados de Exames Realizados) | `textarea` |
|  | Observações | `textarea` |

- Cintilografias (Internados)

### 1 recurso(s) · 10 campos

| | Campo | Tipo |
|---|---|---|
| **obrig.** | Laudo Histopatológico/Exame Imagem | `textarea` |
| **obrig.** | Grau Histopatológico Gx G1 G2 G3 G4 | `select` |
| **obrig.** | PSA/Outros | `text` |
|  | Observação | `textarea` |
| **obrig.** | Peso do Paciente (gramas) | `text` |
| **obrig.** | Altura do Paciente (cm) | `text` |
| **obrig.** | IMC do Paciente | `text` |
|  | Paciente já realizou cirurgia oncológica? Sim Não | `radio` |
|  | Data da coleta da biópsia | `text` |
|  | Data do resultado da biópsia | `text` |

- Tomografia por Emissão de Pósitrons (PET-CT)
