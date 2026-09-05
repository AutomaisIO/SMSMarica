# Spike c — Paridade SER × SERNIT

- Data: 04/09/2026 · Executor: Claude Opus 5 (sessão de implementação) · OK de produção: **não se aplica** (só leitura)
- Credencial: nenhuma externa. Leitura do Postgres de produção (`smsmarica.ser_catalogo_*`, `smsmarica.sernit_catalogo_*`) pela connection string do user-secrets.
- Requisições a sistema externo: **0**. CAPTCHA: não se aplica.
- Corte dos dados: SER sincronizado em 20/08/2026 19:33 UTC; SERNIT em 27/08/2026 15:45 UTC.

## Resumo em uma frase

A chave de pareamento proposta pelo plano 08 (**rótulo normalizado exato**) encontra **0 pares** — os dois catálogos não compartilham convenção de nome. Com uma chave de **conjunto de tokens + público**, fecham **23 pares** (29% do SERNIT), e a régua união é uniforme o bastante para caber em uma regra só: o dicionário de campos inteiro dos dois sistemas tem apenas **76 rótulos distintos**.

## Volumes reais (o plano estava defasado)

| | Plano 08 dizia | Medido hoje |
|---|---|---|
| SER ramo "Não" — Consulta / Exame | 120 / 83 | **136 / 98** |
| SER ramo "Sim" (AE) — Consulta / Exame | 151 / 64 | **167 / 81** |
| SER total de recursos / campos | — | **482 / 1.833** |
| SERNIT — Consulta / Exame | 43 / 35 | **43 / 35** ✔ |
| SERNIT total de recursos / campos | — | **78 / 417** |
| Moldes de formulário — SER | 21 | **20** (18 no ramo "Não" + 2 no "Sim") |
| Moldes de formulário — SERNIT | 8 | **6** |

Todos os 482 + 78 recursos estão com `campos_lidos = true`: a comparação de formulário cobre 100% do catálogo, sem buraco de sincronização.

## 1. A chave de pareamento

### Por que a chave do plano não funciona

Os três catálogos usam universos de nome disjuntos:

| Catálogo | Convenção | Exemplo |
|---|---|---|
| SER ramo "Não" | `Ambulatório 1ª vez [em\|-] <Especialidade> - <subespecialidade> (<público>)` | `Ambulatório 1ª vez - Cardiologia (Oncologia)` |
| SER ramo "Sim" (AE) | `CONSULTA EM <ESPECIALIDADE> - <SUB>`, caixa alta | `CONSULTA EM CARDIOLOGIA - PEDIATRIA` |
| SERNIT | só a especialidade, ou o nome do profissional | `Cardiologia`, `Cardiologista Pediátrico` |

O prefixo de modalidade (`Ambulatório 1ª vez`, `CONSULTA EM`) é o que mata a igualdade: ele diz *que tipo de atendimento é*, não *qual recurso é*. Medição de cada degrau:

| Chave | AMBOS | só-SER | só-SERNIT |
|---|---|---|---|
| D0 — `unaccent(upper(trim(rotulo)))` (**o que o plano 08 §A propõe**) | **3** | 215 | 75 |
| D1 — D0 sem prefixo de modalidade e sem ruído | 18 | 182 | 47 |
| D2 — conjunto de tokens de D1 | 18 | 182 | 47 |
| **D3 — conjunto de tokens + dimensão público** | **23** | 195 | 55 |

(Medido contra o ramo "Sim". Os 3 pares de D0 são acidentais — exames cujo nome coincide por sorte.)

### Qual ramo do SER pareia com o SERNIT — **o contrário do que o plano supôs**

O plano 08 diz *"o par SERNIT é com **um** dos ramos (definir no spike qual; provavelmente 'Não')"*. É o **"Sim"** (ambulatório estadual):

| Ramo do SER | AMBOS com SERNIT (chave D3) |
|---|---|
| `ambulatorio_estadual = false` ("Não") | **0** |
| `ambulatorio_estadual = true` ("Sim") | **23** |

Somar os dois ramos **não acrescenta nenhum par** (fica em 23): o que o ramo "Não" tem em comum com o SERNIT casa por *contenção*, nunca por igualdade — `Ambulatório 1ª vez em Genética Médica - Adulto` contra `Genetica`, por exemplo. Ainda assim o pareamento **deve rodar contra o SER inteiro**, porque existem recursos do SERNIT cujo único candidato está no ramo "Não" (a Genética é o caso claro), e porque o ramo do SER é um atributo de *formulário*, não de *identidade do recurso*.

### A chave D3, especificada

1. `unaccent`, `UPPER`, colapsar espaço. `ª`→`a`, `º`→`o`.
2. Abrir parênteses e colchetes em vez de descartar — **`Cardiologia (Oncologia)` é cardio-oncologia, recurso distinto de `Cardiologia`**. Descartar o parêntese funde os dois.
3. Remover **um** prefixo de modalidade, o primeiro que casar: `AMBULATORIO DE 1A VEZ [EM|DE|-]`, `AMBULATORIO 1A VEZ [EM|DE|-]`, `AMBULATORIO [EM|DE|-]`, `CONSULTA [EM|DE|-]`, `CONSULTA `, `ATENDIMENTO [EM|DE|-]`, `EXAME [DE|-]`.
4. Sinônimos profissional→especialidade: `ALERGISTA`→`ALERGIA`, `ALERGOLOGIA`→`ALERGIA`, `<X>OLOGISTA`→`<X>OLOGIA`, `<X>IATRA`→`<X>IATRIA`.
5. Separadores (`- / , ; : .`) viram espaço.
6. Descartar tokens de ruído: `AMBULATORIAL VEZ 1A RETORNO SOLICITACAO E DE DA DO EM P OU A O AS OS ETC`. **`COM` e `SEM` ficam de fora do ruído** — ver armadilha 3 abaixo.
7. Descartar os marcadores de público (`INFANTIL PEDIATRIA PEDIATRICO PEDIATRICA PEDIATRICOS CRIANCA CRIANCAS ADULTO ADULTOS`) — eles viram a dimensão separada do passo 9.
8. **Se sobrar vazio, refazer o passo 7 sem descartar público.** O rótulo `Pediátria` do SERNIT *é* a especialidade Pediatria; sem esta guarda a chave fica vazia e casa com qualquer outro rótulo que também esvazie.
9. Dimensão **público**: `PEDIATRICO` se o rótulo cru casar `PEDIATRIC|PEDIATRIA|INFANTIL|CRIANC|ORELHINHA|0 A 3 ANOS`; senão `GERAL`. *"Adulto" e a ausência de marcador caem no mesmo balde de propósito*: o SERNIT nunca escreve "adulto" e o SER escreve — exigir igualdade literal quebraria todo par.
10. Chave final = `(tipo, frozenset(tokens), publico)`. Conjunto, não string: ordem e repetição não importam.

Validação da chave: **zero colisão** dos dois lados — nenhuma chave junta dois rótulos diferentes nem no SER (482 recursos → 422 chaves, todas as fusões são rótulos idênticos) nem no SERNIT (78 → 78).

### Três armadilhas encontradas construindo a chave

1. **`unaccent` mora no schema `smsmarica`, não no `public`.** O SQL do plano 08 §A falha com `function unaccent(text) does not exist`. Qualificar: `smsmarica.unaccent(...)`.
2. **Descartar parênteses funde recursos distintos** — `Cardiologia (Oncologia)` viraria `Cardiologia`.
3. **`COM`/`SEM` como ruído inverte sentido.** Tratados como preposição, `Eletroencefalograma Pediátrico com Sedação` e `... sem Sedação` colapsam na mesma chave. Foi a única colisão do SERNIT e é uma fusão de recursos opostos.

## 2. As quatro classes

Chave D3, SER inteiro (482) × SERNIT (78):

| Classe | Chaves | Observação |
|---|---|---|
| **AMBOS** | **23** | 19 consultas + 4 exames |
| **SÓ-SER** | 399 | o SER é ~5× maior; a maior parte é subespecialidade que o SERNIT não tem |
| **SÓ-SERNIT** | 55 | 17 têm candidato por contenção (§3); **38 não têm candidato nenhum** |

### Os 23 pares

| tipo | público | chave | SER | SERNIT |
|---|---|---|---|---|
| Consulta | Pediátrico | ALERGIA | CONSULTA EM ALERGOLOGIA - PEDIATRIA | Alergista Pediátrico |
| Consulta | Geral | CARDIOLOGIA | CONSULTA EM CARDIOLOGIA ⚠ | Cardiologia |
| Consulta | Pediátrico | CARDIOLOGIA | CONSULTA EM CARDIOLOGIA - PEDIATRIA ⚠ | Cardiologista Pediátrico |
| Consulta | Pediátrico | CIRURGIA | CONSULTA EM CIRURGIA PEDIATRICA ⚠ | Cirurgia Pediátrica |
| Consulta | Geral | CIRURGIA TORACICA | CONSULTA EM CIRURGIA TORACICA | Cirurgia Torácica |
| Consulta | Geral | DERMATOLOGIA | CONSULTA EM DERMATOLOGIA | Dermatologia |
| Consulta | Pediátrico | DERMATOLOGIA | CONSULTA EM DERMATOLOGIA - PEDIATRIA | Dermatologia Pediátrica |
| Consulta | Pediátrico | ENDOCRINOLOGIA | CONSULTA EM ENDOCRINOLOGIA - PEDIATRICA | Endocrinologia Pediátrica |
| Consulta | Geral | GASTROENTEROLOGIA | CONSULTA EM GASTROENTEROLOGIA | Gastroenterologia |
| Consulta | Pediátrico | GASTROENTEROLOGIA | CONSULTA EM GASTROENTEROLOGIA - PEDIATRIA | Gastroenterologista Pediatrico |
| Consulta | Pediátrico | INFECTOLOGIA | CONSULTA EM INFECTOLOGIA - PEDIATRIA | Infectologia Pediátrica |
| Consulta | Geral | MASTOLOGIA | CONSULTA EM MASTOLOGIA | Mastologia |
| Consulta | Pediátrico | NEFROLOGIA | CONSULTA EM NEFROLOGIA - PEDIATRIA | Nefrologia Pediátrica |
| Consulta | Geral | NEUROLOGIA | CONSULTA EM NEUROLOGIA | Neurologia |
| Consulta | Pediátrico | NEUROLOGIA | CONSULTA EM NEUROLOGIA - PEDIATRIA | Neurologia Pediátrica |
| Consulta | Geral | OTORRINOLARINGOLOGIA | CONSULTA EM OTORRINOLARINGOLOGIA | Otorrinolaringologia |
| Consulta | Pediátrico | OTORRINOLARINGOLOGIA | CONSULTA EM OTORRINOLARINGOLOGIA PEDIATRICA | Otorrinolaringologia Pediátrico |
| Consulta | Pediátrico | PNEUMOLOGIA | CONSULTA EM PNEUMOLOGIA - PEDIATRIA | Pneumologia Pediátrica |
| Consulta | Pediátrico | REUMATOLOGIA | CONSULTA EM REUMATOLOGIA - PEDIATRIA | Reumatologia Pediátrica |
| Exame | Geral | BIOPSIA RENAL | BIÓPSIA RENAL | Biópsia Renal |
| Exame | Pediátrico | BRONCOSCOPIA | BRONCOSCOPIA PEDIATRICA | Broncoscopia Pediátrica |
| Exame | Geral | ESPIROMETRIA | ESPIROMETRIA | Espirometria |
| Exame | Geral | VIDEOLARINGOSCOPIA | VIDEOLARINGOSCOPIA - ADULTO | Videolaringoscopia |

⚠ = o lado SER tem **dois `valor` diferentes** para o mesmo rótulo (ver §5).

### Os 38 recursos só-SERNIT sem candidato nenhum

Nem por igualdade, nem por contenção, nem por token compartilhado com qualquer recurso do SER (conferido contra os dois ramos):

- **Reabilitação e órtese/prótese (11)** — `Próteses, Órteses, Adaptações`; `Cadeira de Rodas Manuais…`; `Cadeira de Rodas Motorizada…`; `Óculos de Visão Subnormal…`; `Proteses Oculares, Lente Escleral`; `Reabilitação Física` / `Intelectual` / `de Deficiência Visual` / `Estimulação Precoce - 0 a 3 anos`. É uma linha de serviço que o SER simplesmente não oferece por esse canal.
- **Consultas (5)** — `Cirurgia Urológica`, `Genetica`\*, `Hematologia Pediátrica / Anemia Falcifome`, `Imunologia Pediátrica`, `Pediátria`. (\*`Genetica` tem candidato por contenção no ramo "Não": `Genética Médica - Adulto` / `- Pediatria`.)
- **Exames (22)** — entre eles `Eletrocardiograma`, `Retossigmoidoscopia`, `Urofluxometria`, `Uretrocistografia`, `Traqueoscopia`, `Vectonistagmografia`, `Litotripsia Extracorporea`, `Imitanciometria`, `Logoaudiometria`, `Transito e Morfologia do Delgado`, `Esofago Hiato Estomago e Duodeno (SEED)`, `Histeroscopia Cirurgica` / `Diagnóstica`, `Avaliação Urodinamica Completa`, a família de `Eletroencefalograma` e a de `Endoscopia Digestiva`. Verificado token a token: `ELETROCARDIOGRAMA`, `RETOSSIGMOIDOSCOPIA` e `UROFLUXOMETRIA` **não aparecem em nenhum rótulo do SER**, nos dois ramos. É ausência de oferta, não falha de pareamento.

**Consequência para o produto:** para esses 38 o fluxo Externo tem **um destino só** (SERNIT). Isso é exatamente a *ressalva de destino* do plano 03, mas por ausência de oferta em vez de regra clínica — vale ligar as duas coisas na mesma marcação da solicitação.

## 3. O que a contenção revela: não é par, é hierarquia

17 recursos do SERNIT não fecham por igualdade mas são **contidos** por um ou mais do SER. O padrão é sempre o mesmo — **o SER quebra a especialidade em subespecialidades; o SERNIT tem um balde só**:

| SERNIT (1) | SER (N) |
|---|---|
| `Endocrinologia` | `CONSULTA EM ENDOCRINOLOGIA - ANDROLOGIA`, `- DIABETES`, … |
| `Ginecologia` | `CONSULTA EM GINECOLOGIA CIRURGICA`, `- ENDOCRINOLOGIA`, … |
| `Pneumologia` | `CONSULTA EM PNEUMOLOGIA - GERAL`, `- HIPERTENSAO PULMONAR`, … |
| `Reumatologia` | `CONSULTA EM REUMATOLOGIA GERAL`, `- ADOLESCENTE`, … |
| `Nefrologia` | `CONSULTA EM NEFROLOGIA - GERAL` |
| `Geriatria` | `CONSULTA EM GERIATRIA - ACIMA DE 60 ANOS` |
| `Alergia e Imunologia` | `CONSULTA EM ALERGOLOGIA` |
| `Audiometria Comportamental` + `Audiometria Tonal Limiar Via Aérea / Óssea` | `AUDIOMETRIA` (1 do SER cobre 2 do SERNIT — **hierarquia invertida**) |
| `Colonoscopia (COLOSCOPIA)` | `COLONOSCOPIA` (só o sinônimo entre parênteses estorva) |
| `Broncoscopia (Broncofibroscopia)` | `BRONCOSCOPIA` (idem) |
| `Biópsia de Próstata` | `BIOPSIA DE PROSTATA GUIADA POR ULTRASSOM TRANSRETAL` |
| `Monitorização Ambulatorial de Pressão Arterial` | `MONITORAMENTO AMBULATORIAL DE PRESSAO ARTERIAL (MAPA)` |
| `Teste de Esforço / Teste Ergométrico` | `TESTE DE ESFORCO OU TESTE ERGOMETRICO 2` |

Dois candidatos são **falsos** e mostram por que contenção não pode virar par automático: `Cirurgia Plástica Pediátrica` → `CIRURGIA PLASTICA - ORELHA` (o do SER é geral, não pediátrico) e `Ortopedia Pediátrica` → `ORTOPEDIA (NÃO CIRURGICO)` (idem). Nos dois a dimensão público não bate — a guarda funcionou, mas só porque ela existe.

**Decisão:** contenção **não** vira par. Vira **sugestão para curadoria** (`sugerido_procedimento_id` + `sugerido_score` do plano 01), e o de-para 1:N precisa de um vínculo que o modelo do plano 01 hoje não tem — ver §6.

Três subclasses merecem tratamento próprio, porque são pareamento perdido por **notação**, não por hierarquia, e um dicionário de ~10 linhas resolve:

- **sinônimo entre parênteses** — `Colonoscopia (COLOSCOPIA)`, `Broncoscopia (Broncofibroscopia)`, `Eletroneuromiograma (ENMG)`, `Endoscopia Digestiva (Esofagogastroduodenoscopia)`;
- **`MONITORAMENTO` × `MONITORIZAÇÃO`** — mesma palavra, duas formas;
- **sufixo numérico solto** — `TESTE ERGOMETRICO 2`.

## 4. Os formulários: a régua união

Comparação campo a campo dos 23 pares, chave = rótulo normalizado (o `numero` e o `campo` são ids internos de cada instância e não servem):

| Classe | Ocorrências |
|---|---|
| IGUAL | 33 |
| SÓ-SERNIT | 83 |
| SÓ-SER | 25 |
| OBRIGATORIEDADE DIFERENTE | 11 |
| **TIPO DIFERENTE** | **0** |
| **OPÇÕES DIFERENTES** | **0** |

**11 dos 23 pares têm formulário idêntico** — todos o molde de 3 campos. Os 12 restantes seguem um padrão único:

- o lado **SER** é sempre o mesmo molde de 3 campos: `Queixa Principal`, `Resultado de Exames`, `Observações` (todos obrigatórios);
- o lado **SERNIT** é mais rico: 7 campos (clínico: peso/altura/IMC + sinais e sintomas + condições que justificam + provas diagnósticas) ou 12 (cirúrgico: + risco cirúrgico, coagulograma, hemograma, glicemia, ECG, RX tórax, tomografia, ultrassonografia);
- a **única** divergência de obrigatoriedade em todos os pares é `Observações`: obrigatório no SER, opcional no SERNIT. As 11 ocorrências são esse mesmo campo.

O vocabulário inteiro é minúsculo — **SER 67 rótulos distintos, SERNIT 23, 14 em comum, 76 na união**, para 26 moldes de formulário. Isso muda a escala do trabalho: a união **não** é engenharia por par, é um **dicionário canônico de ~76 campos** e um conjunto por recurso.

### Régua união decidida

| Aspecto | Regra | Justificativa medida |
|---|---|---|
| Conjunto de campos | união dos dois lados | 83 + 25 campos existem de um lado só |
| Chave do campo | rótulo normalizado (`unaccent`+`upper`+colapso de espaço) **+ tabela de sinônimos** | o par `VIDEOLARINGOSCOPIA` tem 0 campo em comum só porque o SER escreve `Observações` e o SERNIT `Observação` |
| Obrigatoriedade | **OR** — obrigatório se for obrigatório em qualquer lado | a única divergência é `Observações` (SER exige); exigir é o lado seguro, e o SERNIT aceita preenchido |
| Tipo | igualdade; conflito **não tem regra**, vira divergência para curadoria | 0 conflitos hoje — inventar regra agora seria adivinhação |
| Opções | igualdade; conflito vira divergência (`TraduzirOpcoes`) | 0 conflitos hoje |
| Ordem | SER primeiro, depois os só-SERNIT, por `ordem` de origem | o molde de 3 campos do SER é o que o solicitante já conhece |

Pior caso da união: 14 campos (`CIRURGIA` pediátrica e `CIRURGIA TORACICA` — SER 3 ∪ SERNIT 12).

### Tabela de sinônimos de rótulo de campo (semente)

`OBSERVACAO` ↔ `OBSERVACOES`. É a única encontrada nos 23 pares; entra como tabela porque o mecanismo é necessário, não porque a lista seja grande.

## 5. Achados que não eram a pergunta do spike

1. **O rótulo não é único nem dentro do SER.** 30 rótulos por ramo têm **dois `valor`** diferentes (`CONSULTA EM CARDIOLOGIA` → 988 e 1003; `- PEDIATRIA` → 989 e 1004; `CIRURGIA PEDIATRICA` → 1001 e 1016). O par sempre difere de 15 e os **formulários dos irmãos são idênticos** (conferido: 0 dos 23 pares tem irmãos divergentes). A chave natural da tabela é `(tipo, ramo, valor)`, então as duas linhas convivem legitimamente. Consequência: o de-para canônico↔origem é **1:N do lado SER**, e qualquer "escolher o valor para enviar" precisa de desempate explícito.
2. **`Eletrocardiograma` no SERNIT tem um campo com o nome técnico vazado como rótulo** — `campo='form0:dinamico_id_1009'`, `rotulo='OBS_CONSULTA_EXAME_REDE'`, textarea obrigatório. É o nome da coluna do banco deles aparecendo na tela. Caso de teste pronto para o `SernitCatalogoParserTests` que o plano 08 pede.
3. **O formulário do `Videolaringoscopia` no SERNIT é de biópsia de próstata** — traz `PSA/Outros`, `Grau Histopatológico`, `Laudo Histopatológico/Exame Imagem`. É erro de catálogo **deles**, não do nosso parser (o mesmo molde aparece no recurso de biópsia). Não corrigir do nosso lado; registrar, porque a união vai herdar o campo estranho.
4. **`smsmarica.unaccent` e `smsmarica.vector`** — as duas extensões estão instaladas no schema `smsmarica`, não no `public`. Vale para o plano 01 (embeddings) também.

## 6. O que muda nos planos

| Plano | § | Mudança |
|---|---|---|
| 08 | §A (SQL) | `unaccent` → `smsmarica.unaccent`. |
| 08 | Decisões aplicadas | Chave de pareamento deixa de ser "rótulo normalizado" e passa a ser **D3 (conjunto de tokens + público)**, especificada em §1 deste relatório. A chave antiga acha 0 pares. |
| 08 | Riscos | A suposição *"o par SERNIT é com o ramo 'Não'"* está **errada**: é o ramo **"Sim"**. O pareamento roda contra o SER inteiro mesmo assim. |
| 08 | §C (entidade) | `RegulacaoParidadeDivergencia.ProcedimentoId` pressupõe 1 origem por sistema. O SER tem **1:N** (30 rótulos com 2 `valor`) e a contenção cria **1:N canônico↔SERNIT**. Precisa de origem-por-linha, não procedimento-por-linha. |
| 08 | Desenho / régua união | Régua união fechada (§4), com **tabela de sinônimos de rótulo de campo** — que o plano não previa e sem a qual o par `VIDEOLARINGOSCOPIA` tem 0 campo em comum. |
| 08 | Medições | Contagens atualizadas (§ "Volumes reais"). |
| 01 | catálogo canônico | O canônico **não pode ser 1:1 com origem**: `Endocrinologia` (SERNIT) é pai de N recursos do SER. Ou o canônico é a folha (e o SERNIT aponta N vezes), ou existe hierarquia. **Questão para o Bernardo** — ver §7. |
| 01 | pareamento por cosine | Os 17 casos de contenção de §3 são o corpus de aceitação do pareamento sugerido: o cosine tem de propor esses e **não** propor os 2 falsos (`Cirurgia Plástica Pediátrica`, `Ortopedia Pediátrica`). |
| 02 | 2.6 (união) | A união é um dicionário de ~76 campos, não engenharia por par. Pior caso 14 campos. |
| 03 | ressalva de destino | 38 recursos do SERNIT **não existem no SER**: ressalva "só pode ir para SERNIT" por ausência de oferta, não por regra clínica. Mesma marcação, origem diferente. |

## 7. Pergunta que sobrou para o Bernardo

**Quando o SERNIT tem um balde e o SER tem N subespecialidades (`Endocrinologia` × `ENDOCRINOLOGIA - ANDROLOGIA` / `- DIABETES`), o que o solicitante escolhe na busca?**

Três saídas possíveis, e a escolha muda o modelo do plano 01:

- **(a)** o canônico é a folha do SER; ao mandar para o SERNIT, todas as folhas caem no balde. Busca fina, envio grosso. Exige de-para N:1 no envio.
- **(b)** o canônico é o balde; ao mandar para o SER, o agente escolhe a subespecialidade na triagem. Busca grossa, decisão na regulação — que é onde está o conhecimento.
- **(c)** dois níveis no canônico (grupo + item), como o SISREG já faz com o código terminado em `000`.

Recomendo **(b)**: é o que menos mente para o solicitante (ele não sabe se o caso é Andrologia ou Diabetes) e põe a decisão em quem regula. Mas o custo é que o envio ao SER passa a exigir um passo de escolha na triagem, o que o plano 04 não modelou.

## Anexos

Sem PII (catálogo de procedimentos não tem dado de paciente). Scripts de apuração ficaram no scratchpad da sessão; as regras de normalização estão especificadas em §1 para porte a C#.
