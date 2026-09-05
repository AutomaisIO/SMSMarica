# Spike e — Extração dos manuais de elegibilidade (CRECE e REUNI)

- Data: 04–05/09/2026 · Executor: Claude Opus 5 (sessão de implementação) · OK de produção: **não se aplica** (só arquivos locais e leitura do banco)
- Requisições a sistema externo: **0**. Sem OCR e sem IA — é extração de texto sobre a geometria do PDF.
- Saída: [`spike-e-manual-regras.csv`](spike-e-manual-regras.csv) — **1.169 regras**, 15 colunas, `;` como separador, UTF-8 com BOM.
- Fontes: `Automais.SER/documentacao/CRECE_MANUAL DO SOLICITANTE_Versão1 30.11.2022.pdf` (54 pág.) e `REUNI_MANUAL DO SOLICITANTE_V3 29.12.20222 - Copia.pdf` (44 pág.).

## Resumo em uma frase

Os manuais cobrem **204 recursos** com **1.169 regras**, mas só **19% do catálogo SER** tem regra escrita — e a regra é, em 83% dos casos, **texto clínico que vira pergunta**, não critério dedutível. Dedutível de verdade (idade/sexo) são **27 regras**; documental, **168**.

## 1. Os dois manuais mapeiam os dois ramos do SER

Achado que fecha uma lacuna do spike c:

| Manual | Capa | Ramo do SER |
|---|---|---|
| **CRECE** — Central de Regulação Estadual de Consultas e Exames | Volume 3, *"Ambulatório Estadual - AE"* | `ambulatorio_estadual = true` |
| **REUNI** — Central Unificada de Regulação | Volume 2 | `ambulatorio_estadual = false` |

Isso confirma por outra via a correção do spike c: o ramo "Sim" **é** o ambulatório estadual, e cada ramo tem manual próprio. O CSV carrega a coluna `ramo_ser` já resolvida.

## 2. Números

| | CRECE (AE) | REUNI (rede geral) | Total |
|---|---|---|---|
| Recursos com regra escrita | 100 | 104 | **204** |
| Regras extraídas | 875 | 294 | **1.169** |
| Casaram com `ser_catalogo_recurso` | — | — | **102 (50%)** |

Cobertura pelo lado do catálogo — **é o número que importa para o plano 03**:

| | Recursos | Com regra escrita |
|---|---|---|
| SER ramo "Sim" (AE / CRECE) | 248 | **66 (27%)** |
| SER ramo "Não" (rede geral / REUNI) | 234 | **25 (11%)** |
| **SER inteiro** | **482** | **91 (19%)** |

Classificação das 1.169 regras:

| Tipo | Regras | % | Leitura |
|---|---|---|---|
| **NaoDedutivel** | 974 | 83% | vira pergunta ao solicitante |
| **Documental** | 168 | 14% | vira caixinha de anexo |
| **Dedutivel** | 27 | 2% | idade e/ou sexo, avaliável pelo cadastro |

Por seção: 874 de inclusão, 295 de exclusão. Além disso, **179 ocorrências** da frase *"Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso"* foram descartadas como **boilerplate** — ela aparece em quase todo recurso e é requisito global, não regra do procedimento. **Deve virar uma regra documental única, aplicada a todos**, não 179 linhas.

## 3. O que o plano 13 não previa sobre a extração

O plano manda `pdftotext -layout` + "quebrar por título de recurso (linhas em caixa alta)". **Isso não funciona**, por quatro razões descobertas medindo:

1. **As duas colunas se intercalam na mesma linha de texto.** O nome do recurso é uma célula mesclada centralizada verticalmente, então `-layout` produz `CONSULTA DE        Indivíduos com lesão do pé diabético…` — nome e critério grudados. Pior: a célula do nome **começa depois** do texto de critério ao lado dela, então nem a ordem das linhas ajuda.
   → Solução: ler com **PyMuPDF pela geometria da tabela** — as réguas horizontais desenhadas delimitam a linha, e a régua vertical separa as colunas.
2. **O separador de coluna não tem x fixo** — varia de 177 a 198 entre as páginas. Fixar uma janela fez a página 28 do CRECE cair no fallback e o texto de requisitos vazar para dentro do nome do recurso (`ENDOMETRIOSE Crit I o A e O Cr O`). A regra certa é geométrica: das verticais que atravessam a tabela, a menor é a borda esquerda, a maior a direita, e a que sobra no meio é o separador.
3. **O nome do recurso é `seção + célula`.** O manual põe a especialidade no título numerado da seção (`4.1.5. ENDOCRINOLOGIA`) e deixa só a subespecialidade na célula (`DIABETES GESTACIONAL`); o catálogo escreve `CONSULTA EM ENDOCRINOLOGIA - DIABETES GESTACIONAL`. Ignorar o título derruba o pareamento de **50% para 9%**.
4. **Nem todo título numerado é uma especialidade, e nem todo é caixa alta.** `4. RECURSOS REUNI – PROTOCOLOS DE SOLICITAÇÃO…` e `3. SITUAÇÃO NO SISTEMA` são títulos de capítulo e colariam na frente de todo recurso; e `4.1.1. CARDIOLOGIA - ALTA COMPLEXIDADE (ambulatório 1ª vez)` era rejeitado por exigir caixa alta na linha inteira. O título tem de ser **reduzido à cabeça** (`CARDIOLOGIA`) e os de capítulo, filtrados.

Sujeira removida com filtro explícito, registrada para quem reprocessar: **35 células** que eram fragmento da coluna de requisitos (reconhecíveis por não estarem em caixa alta — todo nome de recurso do manual é caixa alta) e **37 títulos truncados** pela régua da última linha da página (`GINECOLOG`, `OTORRINOLAR`), reconhecíveis por serem prefixo estrito de outro nome.

## 4. Regras de classificação usadas

- **Documental** — o item tem verbo de anexo (`inserir|anexar|apresentar|encaminhar|enviar`). Exigir também o *nome* do exame perdia 44 itens, porque o vocabulário de exame do manual é aberto (EEG, RNM, curva enzimática, "formulário de Alto Custo"). Como o boilerplate universal já saiu antes, o verbo sozinho não gera ruído.
- **Sub-lista herdada** — item terminado em `:` abre uma lista de documentos; os itens seguintes herdam `Documental`. Sem isso, `Anexar:` ficava documental e os documentos em si viravam perguntas.
- **Sub-item de segundo nível** — o manual usa a letra `o` como marcador aninhado. Sem quebrar nisso, `Refluxo gastroesofágico: o Todos os pacientes maiores de 2 anos. o Pacientes menores de 2 anos…` virava **uma** regra e a idade extraída (a do primeiro sub-item) valia para os dois. Depois da quebra, viram duas regras com faixas etárias opostas e corretas. A quebra só ocorre antes de maiúscula ou dígito, para não cortar o artigo "o" no meio de frase.
- **Idade** — faixas fechadas testadas **antes** das abertas: `a partir de 12 anos até 17 anos` casaria com o padrão aberto `a partir de X` e perderia o limite superior.
- **Sexo** — `mulher|feminino|gestante|grávida|puérpera` → `F`; `homem|masculino` → `M`. Conferido item a item: a única marcação `M` do corpus está correta (*"somente usuários adultos do sexo masculino"*).
- **Tudo o que não classificar** → `NaoDedutivel` com `pergunta = texto_original`, como o plano manda, para revisão humana.

## 5. Colunas do CSV

`recurso; sistema; manual; ramo_ser; recurso_catalogo; pareamento; secao; tipo; texto_original; idade_min; idade_max; sexo; pergunta; documento; fonte`

Três colunas a mais do que o plano 13 pedia, e a razão de cada uma:

- **`manual`** (CRECE/REUNI) e **`ramo_ser`** — o plano previa só `sistema`, mas "SER" não basta: a mesma especialidade tem regra diferente nos dois ramos, e é o ramo que decide qual vale.
- **`recurso_catalogo`** + **`pareamento`** (`composto` | `celula` | `secao` | `SEM_PAR`) — o rótulo do catálogo com que a regra casou e por qual nível. Sem isso a importação da tarefa 4.5 teria de refazer o pareamento, e o revisor não teria como ver o que ficou solto.
- **`secao`** (inclusao/exclusao) — o plano não distinguia, mas critério de **exclusão** é a negação da regra e importar os dois juntos inverteria 295 regras.

## 6. O que não coube

**102 recursos do manual (50%) não casaram com o catálogo.** Depois de corrigidos os quatro defeitos de extração, o que resta é divergência real de nome, não falha de parse:

- **20** casariam por **contenção** — o mesmo problema de hierarquia do spike c (o manual detalha, o combo agrupa, ou o contrário).
- **82** divergem de nome entre manual e combo. O padrão mais comum é a **especialidade trocada**: o manual diz `CIRURGIA CARDIOVASCULAR - IMPLANTE DE MARCAPASSO`, o combo diz `Cardiologia - Implante de Marcapasso`; o manual diz `ONCOLOGIA - ESÔFAGO`, o combo usa outra árvore. Há também linhas de serviço inteiras que o manual protocola e o combo não expõe como recurso: `ODONTOLOGIA` (6 itens), `HOMEOPATIA`, `FISIOTERAPIA`, `PSIQUIATRIA`, `CONSULTA DE ENFERMAGEM` (3).

Esses 102 **não** entram como regra órfã: ficam no CSV com `pareamento = SEM_PAR` e `recurso_catalogo` vazio, para a curadoria da tela da tarefa 4.5 resolver. Importar por adivinhação de nome ligaria regra clínica ao procedimento errado, que é o pior defeito possível neste módulo.

## 7. Ressalvas

1. **Os manuais são de 2022 e o catálogo é de 2026.** Parte dos 102 sem-par pode ser recurso que deixou de existir ou mudou de nome nesses quatro anos. O plano 03 já prevê `versao` e `fonte` por regra — a coluna `fonte` do CSV traz manual e página.
2. **83% das regras são não dedutíveis**, ou seja, viram pergunta. Um recurso com 20 critérios de inclusão vira um questionário de 20 perguntas, o que ninguém responde. **A tela da tarefa 4.5 precisa permitir marcar regra como "informativa"** (mostrada como texto de apoio, sem virar pergunta) — o plano 03 hoje só tem os três tipos e não previu isso.
3. **Critério de exclusão não é regra invertida automaticamente.** *"Indivíduos com lesão do pé diabético ESTÁGIOS B, C e D"* como exclusão não vira "pergunta com resposta esperada não" sem revisão — algumas exclusões são condições que o solicitante não tem como afirmar.
4. **Não classifiquei CID.** O plano cita CID entre os dedutíveis; os manuais praticamente não citam código CID, e sim o nome da doença. Regra por CID terá de ser cadastrada à mão ou derivada do `ser_catalogo_cid` do recurso, que já existe no banco.

## 8. O que muda nos planos

| Plano | § | Mudança |
|---|---|---|
| 13 | Spike e — ferramenta | `pdftotext -layout` **não serve**. A extração é por geometria (PyMuPDF): réguas da tabela + separador de coluna detectado por página + título de seção reduzido à cabeça. Quatro defeitos documentados em §3. |
| 13 | Spike e — critério de saída | Cobertura medida: 204 recursos, 1.169 regras, 19% do catálogo, 50% de pareamento. |
| 03 | `TipoRegraRegulacao` | Falta um quarto valor, **`Informativa`**: 83% do corpus é texto clínico que não deve virar pergunta obrigatória sob pena de o questionário ficar impraticável (um recurso com 20 critérios viraria 20 perguntas). Não dá para resolver com `Severidade = Aviso`, que já existe: um `NaoDedutivel` com severidade Aviso **continua sendo uma pergunta**, só não bloqueia. O que falta é o estado "mostra como texto de apoio e não pergunta nada". |
| 03 | 4.5 (importação) | A coluna `secao` do CSV é o que decide **`resposta_bloqueia`**: `exclusao` → `Sim` bloqueia; `inclusao` → `Nao` bloqueia. Importar sem olhar `secao` inverteria o sentido de 295 regras. (A entidade do plano já modela isso; é o importador que precisa da regra.) |
| 03 | 4.5 (importação) | O CSV tem `pareamento = SEM_PAR` em 102 recursos: a importação **não** deve adivinhar; tem de cair na curadoria. E o boilerplate (179 ocorrências) vira **uma** regra global, não 179. |
| 08 / spike c | ramo × manual | Confirmado por outra via: CRECE = ramo "Sim" (AE), REUNI = ramo "Não". |
