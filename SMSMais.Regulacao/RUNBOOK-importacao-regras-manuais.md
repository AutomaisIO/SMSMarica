# RUNBOOK — importar as regras dos manuais CRECE e REUNI

Como levar as **1.169 regras** extraídas dos manuais do SER para dentro do módulo, o que esperar de cada número e o que **não** importar. Conferido em 07/09/2026 contra os PDFs originais e contra o catálogo de produção.

## 1. As fontes

| Arquivo | Páginas | MD5 | Ramo do SER |
|---|---|---|---|
| `documantacao/CRECE_MANUAL DO SOLICITANTE_Versão1 30.11.2022.pdf` | 54 | `80ea05b9…` | Ambulatório Estadual (AE) |
| `documantacao/REUNI_MANUAL DO SOLICITANTE_V3 29.12.20222 - Copia.pdf` | 44 | `40766116…` | rede geral (não-AE) |

São **bit a bit os mesmos** que o spike e leu (as cópias em `Automais.SER/documentacao/` têm o mesmo MD5). **Não é preciso reextrair**: o CSV `revisoes/spike-e-manual-regras.csv` já é a saída deles.

> O script extrator **não foi guardado** no repositório — só o método está descrito em `revisoes/spike-e-manual-regras.md`. Se um dia os manuais forem atualizados, a extração terá de ser reescrita a partir daquele texto. Enquanto os PDFs forem estes, o CSV basta.

## 2. Fidelidade da extração (conferida linha a linha)

Das 1.169 linhas, comparando o texto com o texto real dos PDFs:

| Situação | Linhas | Leitura |
|---|---|---|
| Texto bate com o PDF | 1.121 | fiel |
| Prefixo de subtítulo acrescentado (`HIPÓFISE:`, `Pacientes apresentando:`, `Icterícia:`) | 35 | **enriquecimento proposital** — sem ele a regra fica solta ("Feocromocitoma" sozinho não diz que é critério da supra-renal) |
| **Texto corrompido na origem** | **13** | ver abaixo |

**A atribuição de manual está correta**: nenhuma regra marcada CRECE foi encontrada só no REUNI, nem o inverso.

### As 13 linhas corrompidas

Sobra do defeito de colunas intercaladas que o spike e descreve como resolvido — sobreviveu em cinco páginas do REUNI (12, 17, 23, 31, 37 e 38), onde o nome do recurso quebrou em cima da régua que separa as colunas. Sintoma: fragmentos do nome vazam para o critério (`SOS O TEIRO`, `MÃO OU RIAL)`, `CIA R`, `AL ICA - AL)`) e o nome do recurso fica truncado (`CINTILOGRAFIA DE OS` deveria ser `DE OSSOS`; `CORPO INT`, `CORPO INTEIRO`).

**Só 2 das 13 entrariam** (as outras 11 não têm par no catálogo e são puladas):

- `REUNI p.12` — `"Recurso Requisitos necessários ECOCARDIOGRAMA TRANSESOF…"`
- `REUNI p.31` — `"Bolsa rota ACONSELHAMENTO EM MALFORMAÇÃO FETAL"`

→ **Excluir as duas pela tela de curadoria** depois de importar. Elas nascem inativas, então não afetam ninguém enquanto ficarem lá; mas são texto sem sentido e não devem sobreviver à revisão.

## 3. Por que não se importa o CSV cru

Conferindo o CSV contra os PDFs, **288 das 790 linhas que casam com o catálogo não são regra**:

| O que é | Linhas |
|---|---|
| Cacos de *"Observar os critérios de inclusão do paciente de acordo com cada prestador"* | **280** |
| Cabeçalho da tabela (`Recursos / Requisitos necessários`) | 3 |
| Boilerplate do encaminhamento que escapou do filtro do spike | 3 |
| Número de seção do manual (`4.1.7. DERMATOLOGIA`) | 2 |

Os 280 são **uma frase só**, que fecha quase todo recurso do manual e que o extrator partiu em três:

```
'Observar os'
'do prestador (unidade executante)'
'de cada prestador e faixa etária para atendimento'
```

Nenhum dos três é regra — e a frase inteira também não é: ela manda *o avaliador* olhar os critérios do prestador; não é requisito de quem solicita. Importados, virariam 280 perguntas sem sentido na tela da unidade.

Por isso a importação é feita por `importar_regras_manuais.py`, e não pela tela: o importador da tela lê o CSV como ele veio e não tem como distinguir caco de regra.

## 4. O que a importação vai produzir

Medido contra o catálogo de produção de 07/09/2026 (999 procedimentos, 560 origens SER/SERNIT):

| | Linhas |
|---|---|
| Lidas do CSV | 1.169 |
| Puladas — sem par no catálogo | 379 |
| Descartadas — não são regra (§3) | 288 |
| Regras do manual | 502 |
| Exigência do encaminhamento, uma por procedimento | 72 |
| **Gravadas, todas INATIVAS** | **574** |
| Procedimentos atingidos | 72 |

As 502 do manual são 421 `NaoDedutivel` (viram pergunta), 61 `Documental` (viram caixinha de anexo) e 20 `Dedutivel` (idade/sexo).

### Por que nem as dedutíveis entram ativas

O `AvaliadorElegibilidade` soma as regras com **E**: o pedido precisa passar em todas as ativas do procedimento, e cada falha barra. O manual, porém, lista faixas etárias como critérios **alternativos** — `CONSULTA EM ALERGOLOGIA - PEDIATRIA` tem três (2 a 12 anos, 2 a 12 anos e até 2 anos). Ativadas juntas, **nenhuma criança passa nas três** e o procedimento fica intransitável. Há ainda sexo inferido errado: a regra de plaquetas da hematologia (`Plaquetas < 50 mil cels/mm³`) veio marcada como feminina e barraria todos os homens.

Faixa alternativa deve ser cadastrada como **pergunta**, não como dedutível. O formulário da tela avisa isso.

### Não existe regra global

`regulacao_regra.procedimento_id` é obrigatório. A exigência do encaminhamento médico — que os manuais repetem **218 vezes** — entra como uma regra por procedimento. Tornar a coluna anulável é o certo, mas é migration mais mudança no avaliador; fica como pendência, não improviso.

## 5. Como importar

```bash
cd SMSMais.Regulacao
python importar_regras_manuais.py            # simula, não grava
python importar_regras_manuais.py --gravar   # grava, em transação única
```

A simulação deve terminar em **574 a gravar (0 ativas), 72 procedimentos**. Número diferente significa que o catálogo mudou — conferir antes de seguir.

> **A importação não é idempotente.** O script **recusa gravar** se a tabela já tiver regras. Para reimportar, apagar as anteriores primeiro — e lembrar que apagar uma regra apaga o motivo pelo qual pedidos antigos foram barrados.

Depois, a curadoria: entrar com o módulo **Regulação — Configuração (51)**, menu **Regulação → Regras de elegibilidade**, buscar o procedimento e **ativar só o que realmente barra**. O botão **Nova regra** cadastra à mão o que o manual não cobre — e o que é cadastrado à mão nasce ativo.

### Quem consegue chegar na tela (conferido em 07/09/2026)

| Perfil | Usuários | 47 (solicitante) | 48 (agente) | 51 (configuração) |
|---|---|---|---|---|
| Administrador | 8 | sim | sim | **sim** |
| Regulacao | 29 | **não** | sim | **sim** |
| Tecnico Regulacao | 16 | **não** | sim | não |

O perfil `Regulacao` enxerga a tela de regras mas **não consegue abrir solicitação** (falta o 47); o `Tecnico Regulacao` não vê a tela de regras.

## 6. O que ainda falta, e é decisão de vocês

- **A curadoria.** Nada fica valendo por ser importado: tudo entra inativo, e enquanto nenhuma regra estiver ativa o passo de regras do wizard passa direto. O extrator classificou **974 linhas como pergunta**; ativar todas transformaria a abertura de solicitação num interrogatório. O trabalho é escolher as poucas que de fato impedem o encaminhamento, deixar as demais como texto informativo e descartar o resto — decisão clínica, não automatizável.
- **A regra do encaminhamento já entra**, uma por procedimento (72), inativa. O certo seria uma só para todos; ver §4.
- **Os 379 recursos sem par.** Metade do manual não casa com o catálogo porque o SER escreve o mesmo procedimento de dois jeitos. Isso melhora sozinho conforme o pareamento do catálogo (ADR-0055) avança; reimportar depois traz mais regras — mas veja a nota sobre duplicação acima.

## 7. Defeito corrigido antes de qualquer importação

O importador partia as linhas com `Split(';')`, sem olhar aspas. **98 linhas do manual vêm entre aspas**, porque o texto clínico usa ponto e vírgula:

```
"Imunodeficiência primária- suspeita se tiver: Pneumonia de repetição (mais de 2 no último ano); infecções de repetição (mais de 8 no último ano)"
```

Dessas, **56 seriam importadas** — e entrariam com o critério cortado no primeiro `;`, com uma aspa solta na frente, e com **o pedaço final do texto clínico ocupando a coluna da fonte**. A fonte é a linha que a unidade solicitante lê para saber de onde veio a exigência: ela leria *"infecções de repetição (mais de 8 no último ano)""* no lugar de *"CRECE p.12"*.

Corrigido em 07/09/2026 com um separador que honra RFC 4180 (aspas, `;` dentro de aspas, `""` literal e quebra de linha dentro do campo), com dois testes que prendem o caso.
