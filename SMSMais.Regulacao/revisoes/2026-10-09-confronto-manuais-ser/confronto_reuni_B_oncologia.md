# Confronto REUNI 4.3 ONCOLOGIA (p.15–24) × regras de elegibilidade em produção

Revisão B do confronto manual × regras. Só leitura e análise; nada foi alterado em produção. Data: 09/10/2026.

- **Manual:** REUNI — Manual do Solicitante V3 (29/12/2022), rede geral do SER = ramo `NAO_AE`. Páginas 15–24 (4.3.1 a 4.3.19); a p.25 só para a remissão de tumor ósseo.
- **Produção:** export de hoje (`regras.json`, `ser_recursos.json`, `analise_espelho_por_procedimento.json`, `solicitacoes_por_procedimento.json`), sem dado de paciente.
- **Extração antiga:** `SMSMais.Regulacao/revisoes/spike-e-manual-regras.csv`.
- **Saída por linha:** `confronto_reuni_B_oncologia.jsonl` (uma linha por recurso/local, 50 linhas).

**Método e avisos de leitura.** Li cada página como imagem (renderizada a 110 dpi, PyMuPDF) e também o `reuni.txt`. O texto do `pdftotext -layout` **engana** em três tabelas — vale o visual:

1. **4.3.7 Urologia:** o texto mistura as linhas; no visual são 5 linhas separadas (RIM, BEXIGA, TESTÍCULO, PRÓSTATA, PÊNIS), cada uma com o seu requisito.
2. **4.3.13 Mastologia:** o texto põe "Informar estágio clínico da doença" na IMPALPÁVEL; no visual o bullet está na célula da **PALPÁVEL**.
3. **4.3.19 Medicina Nuclear:** o texto desloca os requisitos uma linha para baixo (dá a impressão de Gamma Knife = histopatológico + risco cirúrgico, Iodoterapia = imagem < 6 meses). No visual: **Braquiterapia** = histopatológico + risco cirúrgico para não-histerectomizadas; **Gamma Knife** = imagem há menos de 06 meses; **Iodoterapia** = só indicar o procedimento.

## Resumo

- **50 linhas** (recurso × local) em 19 subseções. Vereditos: **AUSENTE 29**, **NAO_REPRESENTAVEL_HOJE 18**, **PARCIAL 2**, **SEM_RECURSO_NO_SER 1**, COBERTO 0, ERRADO 0.
- **Só a 4.3.15 (Hematologia adulto e pediátrica) tem regras.** Os outros 29 recursos oncológicos `NAO_AE` do SER estão pareados a procedimentos canônicos com **zero** regras — nem o "requisito global" de encaminhamento. A análise do espelho dá "Sem regras" em **153 pedidos** (103 nos recursos `NAO_AE` de oncologia + 50 no AE pareado a `CONSULTA EM CIRURGIA TORACICA - ONCOLOGIA`) e "A conferir" em 13 (Hematologia (Oncologia)). Urologia (Oncologia): 5 pedidos no espelho + 3 solicitações nossas, todos sem regra — é a reclamação do usuário.
- **14 das 19 subseções usam a coluna "Local da lesão" (ou "Lesão" / "Local/tipo da lesão"), e 9 delas têm mais de um local.** Em 4 dessas os locais têm exigências **diferentes dentro do mesmo recurso do SER**: 4.3.1 Cabeça e Pescoço (9 locais), 4.3.7 Urologia (5), 4.3.10 Ginecologia (5 locais em 2 grupos), 4.3.12 Hepatobiliar (2). A 4.3.14 TOC também, mas só o melanoma difere. Isso não cabe no modelo de hoje, porque todas as regras se somam com E e não existe regra condicional. Nas outras (4.3.6, 4.3.8, 4.3.9) os locais têm requisito idêntico ou quase. Em 4.3.13 Mastologia e 4.3.19 cada local/linha **já é um recurso próprio do SER**.
- **Nada em produção está ERRADO** nas p.15–24. As 12 regras com fonte "REUNI p.20/p.22" estão no procedimento certo. Problemas: falta a OBSERVAÇÃO da leucemia aguda, a pergunta de exclusão não pergunta, há uma paráfrase interpretativa na trombocitose e 8 regras inativas são fragmentos (uma é Dedutível com sexo=F, artefato). O **ERRADO está na extração antiga** (não importada): a tabela do Tórax engoliu a de Coloproctologia, e da Urologia só saiu a PRÓSTATA.
- **4.3.17 Neurologia (Avaliação em Oncologia)** não tem recurso no catálogo do SER. Cinco recursos oncológicos do SER não têm seção na 4.3: Oncologia Geral (Adulto) (44 pedidos "Sem regras", o maior volume), Planejamento em Quimioterapia, Reconstrução de trânsito pós Oncologia, Implante de Cateter (Oncologia) e Plástica Reparadora de Mama (esta tem seção própria na p.36).
- **O SER não tem campo "local da lesão".** Os 31 recursos oncológicos têm o mesmo formulário de 9 campos (Radioterapia Infantil tem 10) e a **mesma lista de CID** (`cid_lista` 49a3a65b, assinatura `0|21|0`). O local só chega no CID do pedido e no texto livre.

## Tabela-resumo

| seção | recurso / local | recurso SER (NAO_AE) | veredito | em uma linha |
|---|---|---|---|---|
| 4.3.1 | Fossa Nasal e Seios Paranasais | Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia) | NAO_REPRESENTAVEL_HOJE | Exigência própria do local (AP e/ou imagem específica); 0 regras. |
| 4.3.1 | Glândulas salivares | Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia) | NAO_REPRESENTAVEL_HOJE | Exigência própria do local (AP e/ou imagem específica); 0 regras. |
| 4.3.1 | Massa cervical | Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia) | NAO_REPRESENTAVEL_HOJE | Exigência própria do local (AP e/ou imagem específica); 0 regras. |
| 4.3.1 | Lábio | Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia) | NAO_REPRESENTAVEL_HOJE | Exigência própria do local (AP e/ou imagem específica); 0 regras. |
| 4.3.1 | Órbita | Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia) | AUSENTE | AP OU imagem (= denominador comum dos 9 locais); 0 regras. |
| 4.3.1 | Pele | Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia) | NAO_REPRESENTAVEL_HOJE | Exigência própria do local (AP e/ou imagem específica); 0 regras. |
| 4.3.1 | Orelha | Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia) | NAO_REPRESENTAVEL_HOJE | Exigência própria do local (AP e/ou imagem específica); 0 regras. |
| 4.3.1 | Cavidade oral | Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia) | NAO_REPRESENTAVEL_HOJE | Exigência própria do local (AP e/ou imagem específica); 0 regras. |
| 4.3.1 | Faringe / laringe | Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia) | AUSENTE | AP e/ou imagem (= denominador comum dos 9 locais); 0 regras. |
| 4.3.2 | TIREOIDE | Ambulatório 1ª vez - Neoplasias da Tireoide (Oncologia) | AUSENTE | AP ou PAAF; 0 regras; cabe hoje. |
| 4.3.3 | OFTALMOLOGIA | Ambulatório 1ª vez - Oftalmologia (Oncologia) | AUSENTE | Lista de estruturas oculares + forte suspeita; 0 regras. |
| 4.3.4 | CÉREBRO | Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia (Oncologia) | AUSENTE | TC/RM com massa no SNC + exclusão de metástase; 0 regras. |
| 4.3.4 | HIPÓFISE/SELA TÚRCICA | Ambulatório 1ª vez em Neurocirurgia - Tumores da Sela Túrcica (Oncologia) | AUSENTE | TC/RM com massa no SNC + exclusão de metástase; 0 regras. |
| 4.3.5 | PELE | Ambulatório 1ª vez - Neoplasias da Pele (Oncologia) | AUSENTE | Histopatológico obrigatório; 0 regras. |
| 4.3.6 | ESÔFAGO | Ambulatório 1ª vez - Cirurgia Geral (Oncologia) | AUSENTE | EDA e/ou TC + histopatológico (idêntico nos 2 locais); 0 regras. |
| 4.3.6 | ESTÔMAGO/DUODENO | Ambulatório 1ª vez - Cirurgia Geral (Oncologia) | AUSENTE | EDA e/ou TC + histopatológico (idêntico nos 2 locais); 0 regras. |
| 4.3.7 | RIM | Ambulatório 1ª vez - Urologia (Oncologia) | NAO_REPRESENTAVEL_HOJE | Imagem + TC abdome; histo se houver; 0 regras. |
| 4.3.7 | BEXIGA | Ambulatório 1ª vez - Urologia (Oncologia) | NAO_REPRESENTAVEL_HOJE | Cistoscopia c/ biópsia e/ou massa em TC/USG; 0 regras. |
| 4.3.7 | TESTÍCULO | Ambulatório 1ª vez - Urologia (Oncologia) | NAO_REPRESENTAVEL_HOJE | USG bolsa e/ou TC; histo se houver; 0 regras. |
| 4.3.7 | PRÓSTATA | Ambulatório 1ª vez - Urologia (Oncologia) | NAO_REPRESENTAVEL_HOJE | Toque retal + PSA > 4 + biópsia; 0 regras. |
| 4.3.7 | PÊNIS | Ambulatório 1ª vez - Urologia (Oncologia) | AUSENTE | Só descrição clínica; histo se houver; 0 regras. |
| 4.3.8 | NÓDULOS TUMORAIS DO PULMÃO | Ambulatório 1ª vez - Cirurgia Torácica (Oncologia) | AUSENTE | TC tórax+abdome superior p/ todos; histopatológico se houver; 0 regras (60 pedidos sem regra). |
| 4.3.8 | TUMORES MEDIASTINO | Ambulatório 1ª vez - Cirurgia Torácica (Oncologia) | AUSENTE | TC tórax+abdome superior p/ todos; histopatológico se houver; 0 regras (60 pedidos sem regra). |
| 4.3.8 | TUMORES DA PLEURA | Ambulatório 1ª vez - Cirurgia Torácica (Oncologia) | AUSENTE | TC tórax+abdome superior p/ todos; histopatológico se houver; 0 regras (60 pedidos sem regra). |
| 4.3.9 | CÓLON | Ambulatório 1ª vez - Coloproctologia (Oncologia) | AUSENTE | Colonoscopia/TC/RNM + histopatológico; 0 regras. |
| 4.3.9 | RETO | Ambulatório 1ª vez - Coloproctologia (Oncologia) | AUSENTE | Colonoscopia/TC/RNM + histopatológico; 0 regras. |
| 4.3.10 | VULVA | Ambulatório 1ª vez - Ginecologia (Oncologia) | NAO_REPRESENTAVEL_HOJE | Grupo A histopatológico × grupo B imagem; 0 regras. |
| 4.3.10 | VAGINA | Ambulatório 1ª vez - Ginecologia (Oncologia) | NAO_REPRESENTAVEL_HOJE | Grupo A histopatológico × grupo B imagem; 0 regras. |
| 4.3.10 | COLO UTERINO E ENDOMÉTRIO | Ambulatório 1ª vez - Ginecologia (Oncologia) | NAO_REPRESENTAVEL_HOJE | Grupo A histopatológico × grupo B imagem; 0 regras. |
| 4.3.10 | TUMOR DE OVÁRIO | Ambulatório 1ª vez - Ginecologia (Oncologia) | NAO_REPRESENTAVEL_HOJE | Grupo A histopatológico × grupo B imagem; 0 regras. |
| 4.3.10 | TROMPAS E ANEXOS | Ambulatório 1ª vez - Ginecologia (Oncologia) | NAO_REPRESENTAVEL_HOJE | Grupo A histopatológico × grupo B imagem; 0 regras. |
| 4.3.11 | Doença Trofoblástica Gestacional (Mola Hidatiforme) | Ambulatório 1ª vez em Ginecologia - Doença Trofoblastica Gestacional (Mola Hidatiforme) | AUSENTE | BHCG + USG e/ou histopatológico; 0 regras. |
| 4.3.12 | FÍGADO E VIAS BILIARES | Ambulatório 1ª vez - Cirurgia Hepatobiliar (Oncologia) | AUSENTE | TC/RNM/CPRE + marcadores se possível; 0 regras. |
| 4.3.12 | PÂNCREAS | Ambulatório 1ª vez - Cirurgia Hepatobiliar (Oncologia) | NAO_REPRESENTAVEL_HOJE | Só TC de abdome; 0 regras. |
| 4.3.13 | MAMA COM LESÃO PALPÁVEL | Ambulatório 1ª vez - Mastologia (Oncologia) | AUSENTE | Recurso próprio no SER; 0 regras. |
| 4.3.13 | MAMA COM LESÃO IMPALPÁVEL | Ambulatório 1ª vez em Mastologia - Lesão Impalpável (Oncologia) | AUSENTE | Recurso próprio no SER; 0 regras. |
| 4.3.14 | CÂNCER ÓSSEO | TOC (Adulto) e TOC (Infantil) | AUSENTE | Histopatológico e/ou imagem (denominador comum); 0 regras. |
| 4.3.14 | SARCOMA DE PERNAS E BRAÇOS | TOC (Adulto) e TOC (Infantil) | AUSENTE | Histopatológico e/ou imagem (denominador comum); 0 regras. |
| 4.3.14 | TUMORES MALIGNOS DE PARTES DE MOLES | TOC (Adulto) e TOC (Infantil) | AUSENTE | Histopatológico e/ou imagem (denominador comum); 0 regras. |
| 4.3.14 | MELANOMA >2 CM | TOC (Adulto) e TOC (Infantil) | NAO_REPRESENTAVEL_HOJE | Histopatológico obrigatório (os outros: histo e/ou imagem); 0 regras. |
| 4.3.15 | ONCOLOGIA HEMATOLOGIA ADULTO | Ambulatório 1ª vez - Hematologia (Oncologia) | PARCIAL | Lista e exclusão OK; falta OBSERVAÇÃO leucemia aguda. |
| 4.3.15 | ONCOLOGIA HEMATOLOGIA PEDIÁTRICA | Ambulatório 1ª vez - Hematologia Pediátrica (Oncologia) | PARCIAL | Lista e exclusão OK; falta OBSERVAÇÃO leucemia aguda. |
| 4.3.16 | AVALIAÇÃO CARDIOLÓGICA PARA PACIENTES EM TRATAMENTO ONCOLÓGICO | Ambulatório 1ª vez - Cardiologia (Oncologia) | AUSENTE | Lista de 3 indicações; 0 regras. |
| 4.3.17 | AVALIAÇÃO EM ONCOLOGIA (NEUROLOGIA) | — (não existe) | SEM_RECURSO_NO_SER | Recurso não existe no catálogo do SER. |
| 4.3.18 | TRIAGEM EM ONCOLOGIA PEDIÁTRICA | Ambulatório 1ª vez - Triagem em Oncologia Pediátrica | AUSENTE | Só encaminhamento detalhado; 0 regras. |
| 4.3.19 | PLANEJAMENTO EM RADIOTERAPIA | Ambulatório 1ª vez - Planejamento em Radioterapia | AUSENTE | Recurso próprio no SER; 0 regras. |
| 4.3.19 | PLANEJAMENTO EM RADIOTERAPIA INFANTIL | Ambulatório 1ª vez - Planejamento em Radioterapia (Infantil) | AUSENTE | Recurso próprio no SER; 0 regras. |
| 4.3.19 | PLANEJAMENTO EM BRAQUITERAPIA | Ambulatório 1ª vez - Planejamento em Braquiterapia | AUSENTE | Recurso próprio no SER; 0 regras. |
| 4.3.19 | PLANEJAMENTO EM GAMMA KNIFE | Ambulatório 1ª vez - Radiocirurgia Gamma Knife | AUSENTE | Recurso próprio no SER; 0 regras. |
| 4.3.19 | PLANEJAMENTO EM IODOTERAPIA | Ambulatório 1ª vez - Planejamento em Iodoterapia | AUSENTE | Recurso próprio no SER; 0 regras. |

## Blocos por subseção

Convenções: **[G]** = Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (o mesmo texto do requisito global que já existe em outros procedimentos). **[M]** = Pergunta Sim/Não, Bloqueia, "Sim" bloqueia: "A lesão é suspeita de metástase (tumor primário em outro órgão)?", com motivo "Inserir de acordo com o sítio primário". "Pergunta de LISTA" = a lista "basta uma" de hoje: "nenhuma destas" = Não, e Não bloqueia. A opção marcada fica gravada em `OpcoesMarcadasJson` e o regulador vê. **CID sugerido** é sugestão minha (CID-10), não está no manual.

**NAO_REPRESENTAVEL_HOJE** quer dizer: além de ausente, o requisito deste local é mais estrito que o denominador comum do recurso do SER, ou diferente dele. Hoje ele só entra como texto de opção de lista ou informativa, e o sistema não exige o documento específico do local.

### 4.3.1 — Consulta de primeira vez em Oncologia - CABEÇA E PESCOÇO (p.15)

- **estrutura:** CONDICIONAL (eixo: local da lesão / sítio primário — 9 locais como bullets numa célula única 'CABEÇA E PESCOÇO')
- **recurso_ser:** `Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia) (NAO_AE tipo1 v1108)`
- **recurso AE parecido:** AE 'CONSULTA EM CIRURGIA DA CABECA E PESCOCO' (não oncológico; regido pelo CRECE)
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia): 2 pedidos 'Sem regras'
- **extração antiga (CSV):** Nenhuma linha (seção 4.3.1 inteira ausente do CSV).
- **problemas comuns:**
  - Recurso do SER sem nenhuma regra: a análise automática dá 'Sem regras' (2 pedidos no espelho).
  - Extração antiga (spike-e) não gerou NENHUMA linha para 4.3.1 — a tabela inteira (p.15) se perdeu.
  - **o que se perde:** O documento específico deste local não é exigido (a caixinha é genérica); 'E' (anatomopatológico E imagem) vira declaração na opção. Pedido que chega do SER (sem local) cai sempre em 'A conferir'.

#### 4.3.1 · Fossa Nasal e Seios Paranasais — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Diagnóstico confirmado de neoplasia com sítio primário em cabeça e pescoço (cabeçalho da seção) — *inclusão*; **obrigatório**
- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo anatomopatológico — *laudo histopatológico (anatomopatológico)*; **obrigatório**
- Exame de imagem: TC e/ou RNM de crânio e face — *documento/exame a anexar*; **obrigatório (E com o anatomopatológico)**
- CID sugerido (não literal): C30, C31

Problemas:

- O requisito deste local é mais estrito/diferente do denominador comum dos 9 locais ('anatomopatológico e/ou imagem'); no modelo de hoje só entra como texto de opção de lista ou informativa — o sistema não exige o documento específico deste local.

Proposta:

- (a) modelo atual: [G] + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'O paciente tem DIAGNÓSTICO CONFIRMADO DE NEOPLASIA com sítio primário em cabeça e pescoço?' + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Local da lesão (sítio primário)' com 9 opções, cada uma com o requisito literal do local (esta: 'Fossa Nasal e Seios Paranasais — Laudo anatomopatológico; Exame de imagem: TC e/ou RNM de crânio e face') + Documento/Bloqueia genérico 'Laudo anatomopatológico e/ou exame de imagem exigidos para o local marcado'.
- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (9 opções). Filhas que só valem para a opção 'Fossa Nasal e Seios Paranasais': Documento/Bloqueia 'Laudo anatomopatológico'; Documento/Bloqueia 'Exame de imagem: TC e/ou RNM de crânio e face'. Chave alternativa para pedido do espelho: CID do pedido começa com C30, C31 (sugestão, não está no manual).

#### 4.3.1 · Glândulas salivares — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Diagnóstico confirmado de neoplasia com sítio primário em cabeça e pescoço (cabeçalho da seção) — *inclusão*; **obrigatório**
- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo anatomopatológico — *laudo histopatológico (anatomopatológico)*; **obrigatório**
- Exame de imagem: USG ou TC com contraste das glândulas salivares — *documento/exame a anexar*; **obrigatório (E com o anatomopatológico)**
- CID sugerido (não literal): C07, C08

Problemas:

- O requisito deste local é mais estrito/diferente do denominador comum dos 9 locais ('anatomopatológico e/ou imagem'); no modelo de hoje só entra como texto de opção de lista ou informativa — o sistema não exige o documento específico deste local.

Proposta:

- (a) modelo atual: [G] + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'O paciente tem DIAGNÓSTICO CONFIRMADO DE NEOPLASIA com sítio primário em cabeça e pescoço?' + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Local da lesão (sítio primário)' com 9 opções, cada uma com o requisito literal do local (esta: 'Glândulas salivares — Laudo anatomopatológico; Exame de imagem: USG ou TC com contraste das glândulas salivares') + Documento/Bloqueia genérico 'Laudo anatomopatológico e/ou exame de imagem exigidos para o local marcado'.
- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (9 opções). Filhas que só valem para a opção 'Glândulas salivares': Documento/Bloqueia 'Laudo anatomopatológico'; Documento/Bloqueia 'Exame de imagem: USG ou TC com contraste das glândulas salivares'. Chave alternativa para pedido do espelho: CID do pedido começa com C07, C08 (sugestão, não está no manual).

#### 4.3.1 · Massa cervical — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Diagnóstico confirmado de neoplasia com sítio primário em cabeça e pescoço (cabeçalho da seção) — *inclusão*; **obrigatório**
- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo anatomopatológico — *laudo histopatológico (anatomopatológico)*; **obrigatório**
- TC de pescoço e tórax e face evidenciando sítio primário em via aérea superior (rinofaringe, traqueia, laringe) — *documento/exame a anexar + conteúdo do laudo*; **obrigatório (E)**
- CID sugerido (não literal): C77.0, C76.0

Problemas:

- O requisito deste local é mais estrito/diferente do denominador comum dos 9 locais ('anatomopatológico e/ou imagem'); no modelo de hoje só entra como texto de opção de lista ou informativa — o sistema não exige o documento específico deste local.

Proposta:

- (a) modelo atual: [G] + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'O paciente tem DIAGNÓSTICO CONFIRMADO DE NEOPLASIA com sítio primário em cabeça e pescoço?' + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Local da lesão (sítio primário)' com 9 opções, cada uma com o requisito literal do local (esta: 'Massa cervical — Laudo anatomopatológico; TC de pescoço e tórax e face evidenciando sítio primário em via aérea superior (rinofaringe, traqueia, laringe)') + Documento/Bloqueia genérico 'Laudo anatomopatológico e/ou exame de imagem exigidos para o local marcado'.
- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (9 opções). Filhas que só valem para a opção 'Massa cervical': Documento/Bloqueia 'Laudo anatomopatológico'; Documento/Bloqueia 'TC de pescoço e tórax e face evidenciando sítio primário em via aérea superior (rinofaringe, traqueia, laringe)'. Chave alternativa para pedido do espelho: CID do pedido começa com C77.0, C76.0 (sugestão, não está no manual).

#### 4.3.1 · Lábio — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Diagnóstico confirmado de neoplasia com sítio primário em cabeça e pescoço (cabeçalho da seção) — *inclusão*; **obrigatório**
- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo anatomopatológico confirmando o sítio primário — *laudo histopatológico (anatomopatológico)*; **obrigatório**
- CID sugerido (não literal): C00

Problemas:

- O requisito deste local é mais estrito/diferente do denominador comum dos 9 locais ('anatomopatológico e/ou imagem'); no modelo de hoje só entra como texto de opção de lista ou informativa — o sistema não exige o documento específico deste local.

Proposta:

- (a) modelo atual: [G] + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'O paciente tem DIAGNÓSTICO CONFIRMADO DE NEOPLASIA com sítio primário em cabeça e pescoço?' + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Local da lesão (sítio primário)' com 9 opções, cada uma com o requisito literal do local (esta: 'Lábio — Laudo anatomopatológico confirmando o sítio primário') + Documento/Bloqueia genérico 'Laudo anatomopatológico e/ou exame de imagem exigidos para o local marcado'.
- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (9 opções). Filhas que só valem para a opção 'Lábio': Documento/Bloqueia 'Laudo anatomopatológico confirmando o sítio primário'. Chave alternativa para pedido do espelho: CID do pedido começa com C00 (sugestão, não está no manual).

#### 4.3.1 · Órbita — **AUSENTE** (representável hoje: sim (genérico))

Requisitos:

- Diagnóstico confirmado de neoplasia com sítio primário em cabeça e pescoço (cabeçalho da seção) — *inclusão*; **obrigatório**
- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo anatomopatológico OU exame de imagem (TC e/ou RNM de crânio e face) — *documento/exame a anexar (alternativo)*; **obrigatório um dos dois ('ou')**
- CID sugerido (não literal): C69.6

Problemas:

- O requisito deste local coincide com o denominador comum ('anatomopatológico e/ou imagem') — cabe num documento genérico, desde que os outros locais não imponham mais.
- Conflito interno do manual: o cabeçalho exige 'DIAGNÓSTICO CONFIRMADO DE NEOPLASIA', mas este local aceita exame de imagem sem anatomopatológico ('ou' / 'e/ou').
- Fronteira com 4.3.3 (Oftalmologia: conjuntiva, córnea, íris, retina e/ou coroide) — órbita fica em Cabeça e Pescoço.

Proposta:

- (a) modelo atual: [G] + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'O paciente tem DIAGNÓSTICO CONFIRMADO DE NEOPLASIA com sítio primário em cabeça e pescoço?' + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Local da lesão (sítio primário)' com 9 opções, cada uma com o requisito literal do local (esta: 'Órbita — Laudo anatomopatológico OU exame de imagem (TC e/ou RNM de crânio e face)') + Documento/Bloqueia genérico 'Laudo anatomopatológico e/ou exame de imagem exigidos para o local marcado'.
- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (9 opções). Filhas que só valem para a opção 'Órbita': Documento/Bloqueia 'Laudo anatomopatológico OU exame de imagem (TC e/ou RNM de crânio e face)'. Chave alternativa para pedido do espelho: CID do pedido começa com C69.6 (sugestão, não está no manual).

#### 4.3.1 · Pele — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Diagnóstico confirmado de neoplasia com sítio primário em cabeça e pescoço (cabeçalho da seção) — *inclusão*; **obrigatório**
- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo anatomopatológico confirmando o sítio primário — *laudo histopatológico (anatomopatológico)*; **obrigatório**
- CID sugerido (não literal): C43, C44 (cabeça/pescoço)

Problemas:

- O requisito deste local é mais estrito/diferente do denominador comum dos 9 locais ('anatomopatológico e/ou imagem'); no modelo de hoje só entra como texto de opção de lista ou informativa — o sistema não exige o documento específico deste local.
- Sobreposição com 4.3.5 (Neoplasias de Pele, recurso próprio no SER): o manual não diz se pele de cabeça e pescoço vai para Cabeça e Pescoço ou para Neoplasias da Pele.

Proposta:

- (a) modelo atual: [G] + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'O paciente tem DIAGNÓSTICO CONFIRMADO DE NEOPLASIA com sítio primário em cabeça e pescoço?' + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Local da lesão (sítio primário)' com 9 opções, cada uma com o requisito literal do local (esta: 'Pele — Laudo anatomopatológico confirmando o sítio primário') + Documento/Bloqueia genérico 'Laudo anatomopatológico e/ou exame de imagem exigidos para o local marcado'.
- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (9 opções). Filhas que só valem para a opção 'Pele': Documento/Bloqueia 'Laudo anatomopatológico confirmando o sítio primário'. Chave alternativa para pedido do espelho: CID do pedido começa com C43, C44 (cabeça/pescoço) (sugestão, não está no manual).

#### 4.3.1 · Orelha — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Diagnóstico confirmado de neoplasia com sítio primário em cabeça e pescoço (cabeçalho da seção) — *inclusão*; **obrigatório**
- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo anatomopatológico confirmando o sítio primário — *laudo histopatológico (anatomopatológico)*; **obrigatório**
- CID sugerido (não literal): C44.2, C30.1

Problemas:

- O requisito deste local é mais estrito/diferente do denominador comum dos 9 locais ('anatomopatológico e/ou imagem'); no modelo de hoje só entra como texto de opção de lista ou informativa — o sistema não exige o documento específico deste local.

Proposta:

- (a) modelo atual: [G] + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'O paciente tem DIAGNÓSTICO CONFIRMADO DE NEOPLASIA com sítio primário em cabeça e pescoço?' + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Local da lesão (sítio primário)' com 9 opções, cada uma com o requisito literal do local (esta: 'Orelha — Laudo anatomopatológico confirmando o sítio primário') + Documento/Bloqueia genérico 'Laudo anatomopatológico e/ou exame de imagem exigidos para o local marcado'.
- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (9 opções). Filhas que só valem para a opção 'Orelha': Documento/Bloqueia 'Laudo anatomopatológico confirmando o sítio primário'. Chave alternativa para pedido do espelho: CID do pedido começa com C44.2, C30.1 (sugestão, não está no manual).

#### 4.3.1 · Cavidade oral — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Diagnóstico confirmado de neoplasia com sítio primário em cabeça e pescoço (cabeçalho da seção) — *inclusão*; **obrigatório**
- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo anatomopatológico confirmando o sítio primário — *laudo histopatológico (anatomopatológico)*; **obrigatório**
- CID sugerido (não literal): C02–C06

Problemas:

- O requisito deste local é mais estrito/diferente do denominador comum dos 9 locais ('anatomopatológico e/ou imagem'); no modelo de hoje só entra como texto de opção de lista ou informativa — o sistema não exige o documento específico deste local.

Proposta:

- (a) modelo atual: [G] + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'O paciente tem DIAGNÓSTICO CONFIRMADO DE NEOPLASIA com sítio primário em cabeça e pescoço?' + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Local da lesão (sítio primário)' com 9 opções, cada uma com o requisito literal do local (esta: 'Cavidade oral — Laudo anatomopatológico confirmando o sítio primário') + Documento/Bloqueia genérico 'Laudo anatomopatológico e/ou exame de imagem exigidos para o local marcado'.
- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (9 opções). Filhas que só valem para a opção 'Cavidade oral': Documento/Bloqueia 'Laudo anatomopatológico confirmando o sítio primário'. Chave alternativa para pedido do espelho: CID do pedido começa com C02–C06 (sugestão, não está no manual).

#### 4.3.1 · Faringe / laringe — **AUSENTE** (representável hoje: sim (genérico))

Requisitos:

- Diagnóstico confirmado de neoplasia com sítio primário em cabeça e pescoço (cabeçalho da seção) — *inclusão*; **obrigatório**
- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Anatomopatológico e/ou exame de imagem (TC de pescoço e tórax e face ou laringoscopia) com indicativo do sítio primário em rinofaringe, traqueia, laringe — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um ('e/ou')**
- CID sugerido (não literal): C09–C14, C32, C33

Problemas:

- O requisito deste local coincide com o denominador comum ('anatomopatológico e/ou imagem') — cabe num documento genérico, desde que os outros locais não imponham mais.
- Conflito interno do manual: o cabeçalho exige 'DIAGNÓSTICO CONFIRMADO DE NEOPLASIA', mas este local aceita exame de imagem sem anatomopatológico ('ou' / 'e/ou').

Proposta:

- (a) modelo atual: [G] + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'O paciente tem DIAGNÓSTICO CONFIRMADO DE NEOPLASIA com sítio primário em cabeça e pescoço?' + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Local da lesão (sítio primário)' com 9 opções, cada uma com o requisito literal do local (esta: 'Faringe / laringe — Anatomopatológico e/ou exame de imagem (TC de pescoço e tórax e face ou laringoscopia) com indicativo do sítio primário em rinofaringe, traqueia, laringe') + Documento/Bloqueia genérico 'Laudo anatomopatológico e/ou exame de imagem exigidos para o local marcado'.
- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (9 opções). Filhas que só valem para a opção 'Faringe / laringe': Documento/Bloqueia 'Anatomopatológico e/ou exame de imagem (TC de pescoço e tórax e face ou laringoscopia) com indicativo do sítio primário em rinofaringe, traqueia, laringe'. Chave alternativa para pedido do espelho: CID do pedido começa com C09–C14, C32, C33 (sugestão, não está no manual).

### 4.3.2 — Consulta de primeira vez em Oncologia – TIREOIDE (p.16)

- **estrutura:** SIMPLES (um local; documento com alternativa 'ou')
- **recurso_ser:** `Ambulatório 1ª vez - Neoplasias da Tireoide (Oncologia) (NAO_AE tipo1 v1114)`
- **recurso AE parecido:** AE 'CONSULTA EM CIRURGIA GERAL - TIREOIDES' e 'CONSULTA EM ENDOCRINOLOGIA - TIREOIDE' (não oncológicos)
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Neoplasias da Tireoide (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Neoplasias da Tireoide (Oncologia): 0 pedidos no espelho
- **extração antiga (CSV):** Nenhuma linha.
- **problemas comuns:**
  - Sem nenhuma regra.
  - Extração antiga não pegou a tabela.
- **proposta (a), melhor no modelo atual:** [G] + Documento/Bloqueia 'Laudo anatomopatológico ou citopatológico (PAAF) confirmando o sítio primário'.
  - **o que se perde:** Nada relevante.
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.2 · TIREOIDE — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Exame anatomopatológico OU citopatológico (PAAF) confirmando o sítio primário — *laudo histopatológico/citopatológico (alternativo)*; **obrigatório um dos dois ('ou')**

### 4.3.3 — Consulta de primeira vez em Oncologia – OFTALMOLOGIA (p.16)

- **estrutura:** SIMPLES (inclusão por sítio: lista 'e/ou' de estruturas oculares)
- **recurso_ser:** `Ambulatório 1ª vez - Oftalmologia (Oncologia) (NAO_AE tipo1 v1115)`
- **recurso AE parecido:** AE 'CONSULTA EM OFTALMOLOGIA - RETINA GERAL' / '- CORNEA' (não oncológicos)
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Oftalmologia (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Oftalmologia (Oncologia): 4 pedidos 'Sem regras'
- **extração antiga (CSV):** Nenhuma linha.
- **problemas comuns:**
  - Sem nenhuma regra (4 pedidos 'Sem regras').
  - Nenhum exame é exigido — só a forte suspeita no encaminhamento.
- **proposta (a), melhor no modelo atual:** [G] + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Onde está a lesão suspeita?' opções: conjuntiva | córnea | íris | retina | coroide + Informativa 'O encaminhamento deve indicar a forte suspeita clínica.'
  - **o que se perde:** Nada relevante.
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.3 · OFTALMOLOGIA — **AUSENTE** (representável hoje: sim)

Requisitos:

- Lesão suspeita em conjuntiva, córnea, íris, retina e/ou coroide — *inclusão (lista, basta uma)*; **obrigatório**
- Encaminhamento médico indicando a forte suspeita clínica — *conteúdo obrigatório do encaminhamento*; **obrigatório**

### 4.3.4 — Consulta de primeira vez em Oncologia – NEUROCIRURGIA (p.16)

- **estrutura:** SIMPLES + exclusão (metástase → sítio primário); o local único do manual se divide em 2 recursos do SER
- **recurso_ser:** `Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia (Oncologia) (NAO_AE tipo1 v1103)` · `Ambulatório 1ª vez em Neurocirurgia - Tumores da Sela Túrcica (Oncologia) (NAO_AE tipo1 v1117)`
- **recurso AE parecido:** nenhum oncológico (NAO_AE 'Neurocirurgia - Neurocirurgia Adulto (Exceto Coluna)' não é oncológico)
- **nosso (procedimento canônico):** `Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia (Oncologia)` · `Ambulatório 1ª vez em Neurocirurgia - Tumores da Sela Túrcica (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia (Oncologia): 0 pedidos no espelho | Ambulatório 1ª vez em Neurocirurgia - Tumores da Sela Túrcica (Oncologia): 0 pedidos no espelho
- **extração antiga (CSV):** Nenhuma linha.
- **problemas comuns:**
  - Sem nenhuma regra.
  - O manual tem uma linha 'CÉREBRO/HIPÓFISE/SELA TÚRCICA'; o SER tem dois recursos (Neurocirurgia (Oncologia) e Tumores da Sela Túrcica (Oncologia)) — as mesmas regras valem para os dois.
- **proposta (a), melhor no modelo atual:** [G] + Documento/Bloqueia 'Laudo de TC e/ou RM de crânio com nódulo ou massa em SNC sugestivo de sítio primário' + Pergunta Sim/Não, Bloqueia, 'Sim' bloqueia: 'A lesão é suspeita de metástase (tumor primário em outro órgão)?' — motivo: 'Inserir de acordo com o sítio primário' (literal do manual). Repetir nos dois procedimentos.
  - **o que se perde:** Nada relevante.
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.4 · CÉREBRO — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento indicando presença de nódulo ou massa em SNC, sugestivo de sítio primário — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- TC e/ou RM de crânio que diagnosticou o nódulo/massa — *documento/exame a anexar*; **obrigatório**
- Suspeita de metástase em SNC → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**

#### 4.3.4 · HIPÓFISE/SELA TÚRCICA — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento indicando presença de nódulo ou massa em SNC, sugestivo de sítio primário — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- TC e/ou RM de crânio que diagnosticou o nódulo/massa — *documento/exame a anexar*; **obrigatório**
- Suspeita de metástase em SNC → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**

### 4.3.5 — Consulta de primeira vez em Oncologia – NEOPLASIAS DE PELE (p.16)

- **estrutura:** SIMPLES
- **recurso_ser:** `Ambulatório 1ª vez - Neoplasias da Pele (Oncologia) (NAO_AE tipo1 v1120)`
- **recurso AE parecido:** AE 'CONSULTA EM CIRURGIA PLASTICA - TUMOR DE PELE' e 'CONSULTA EM DERMATOLOGIA - BIOPSIA DE PELE'
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Neoplasias da Pele (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Neoplasias da Pele (Oncologia): 3 pedidos 'Sem regras'
- **extração antiga (CSV):** Nenhuma linha.
- **problemas comuns:**
  - Sem nenhuma regra (3 pedidos 'Sem regras').
  - Remissões que o manual não amarra: 'Pele' também aparece em 4.3.1 (Cabeça e Pescoço) e 'MELANOMA >2 CM' em 4.3.14 (TOC).
- **proposta (a), melhor no modelo atual:** [G] + Documento/Bloqueia 'Laudo histopatológico confirmando o sítio primário' + Informativa 'O encaminhamento deve indicar a forte suspeita clínica.'
  - **o que se perde:** Nada.
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.5 · PELE — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento indicando a forte suspeita clínica — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Exame histopatológico confirmando o sítio primário — *laudo histopatológico*; **obrigatório**

### 4.3.6 — Consulta de primeira vez em Oncologia – CIRURGIA GERAL (p.17)

- **estrutura:** CONDICIONAL só na forma (2 locais com requisitos IDÊNTICOS) → efetivamente SIMPLES
- **recurso_ser:** `Ambulatório 1ª vez - Cirurgia Geral (Oncologia) (NAO_AE tipo1 v1106)`
- **recurso AE parecido:** nenhum oncológico
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Cirurgia Geral (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Cirurgia Geral (Oncologia): 2 pedidos 'Sem regras'
- **extração antiga (CSV):** Uma linha 'ONCOLOGIA - ESÔFAGO' SEM_PAR, só com o documento (EDA e/ou TC + histopatológico); a linha do encaminhamento se perdeu. Não foi importada. / Uma linha 'ONCOLOGIA - ESTÔMAGO/DUODENO' SEM_PAR, só com o documento (EDA e/ou TC + histopatológico); a linha do encaminhamento se perdeu. Não foi importada.
- **problemas comuns:**
  - Sem nenhuma regra (2 pedidos 'Sem regras').
  - Ambiguidade de leitura: 'com lesão suspeita e laudo histopatológico' — lido como laudo histopatológico OBRIGATÓRIO (não há 'se houver').
- **proposta (a), melhor no modelo atual:** [G] + Documento/Bloqueia 'Resultado de EDA e/ou TC de abdome e tórax com lesão suspeita' + Documento/Bloqueia 'Laudo histopatológico'. (Opcional: pergunta de lista 'Local: Esôfago | Estômago/Duodeno' só para registro.)
  - **o que se perde:** Nada.
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.6 · ESÔFAGO — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Resultado de EDA e/ou TC de abdome e tórax com lesão suspeita — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- Laudo histopatológico — *laudo histopatológico*; **obrigatório (leitura: '...com lesão suspeita E laudo histopatológico')**
- CID sugerido (não literal): C15

#### 4.3.6 · ESTÔMAGO/DUODENO — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Resultado de EDA e/ou TC de abdome e tórax com lesão suspeita — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- Laudo histopatológico — *laudo histopatológico*; **obrigatório (leitura: '...com lesão suspeita E laudo histopatológico')**
- CID sugerido (não literal): C16, C17.0

### 4.3.7 — Consulta de primeira vez em Oncologia – UROLOGIA (p.17)

- **estrutura:** CONDICIONAL (eixo: local da lesão — 5 linhas, cada uma com exigência própria)
- **recurso_ser:** `Ambulatório 1ª vez - Urologia (Oncologia) (NAO_AE tipo1 v1104)`
- **recurso AE parecido:** AE 'CONSULTA EM UROLOGIA GERAL' / 'CONSULTA EM UROLOGIA CIRURGICA' (não oncológicos); exame AE 'BIOPSIA DE PROSTATA GUIADA POR ULTRASSOM TRANSRETAL'
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Urologia (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Urologia (Oncologia): 5 pedidos 'Sem regras' + 3 solicitações nossas
- **extração antiga (CSV):** Nenhuma linha (da tabela só PRÓSTATA saiu). / 'ONCOLOGIA - PRÓSTATA' SEM_PAR (1 linha Documental com o texto todo).
- **problemas comuns:**
  - O procedimento não tem NENHUMA regra: a análise do espelho dá 'Sem regras' (5 pedidos) e as 3 solicitações nossas para este procedimento passaram sem conferência — é a reclamação do usuário.
  - O formulário do SER (9 campos) não tem 'local da lesão'; o pedido que chega do SER só traz CID e texto livre.
- **proposta (a), melhor no modelo atual:** [G] + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Local da lesão — marque o local; marcar é declarar que o requisito dele está atendido', opções literais: 'RIM — imagem com lesão sólida suspeita e TC de abdome' | 'BEXIGA — cistoscopia com biópsia positiva e/ou massa vesical em TC de abdome e pelve ou USG' | 'TESTÍCULO — massa testicular ao exame clínico + USG de bolsa escrotal e/ou TC abdome/pelve' | 'PRÓSTATA — toque retal alterado e PSA > 4 ng/ml, com diagnóstico confirmado por biópsia' | 'PÊNIS — lesão peniana ao exame clínico com suspeita de neoplasia' + Documento/Bloqueia genérico 'Exame(s) exigido(s) para o local marcado' + Documento NÃO obrigatório 'Laudo histopatológico (se houver; para próstata, a biópsia é obrigatória)'.
  - **o que se perde:** Não se exige o documento específico do local (ex.: biópsia e PSA só para próstata); 'se houver' (rim/testículo/pênis) × 'obrigatório' (próstata) fica só no texto; sexo não é conferido; PSA > 4 vira declaração; pedidos do espelho ficam 'A conferir'. A opção marcada fica gravada (OpcoesMarcadasJson), então o regulador vê o local.

#### 4.3.7 · RIM — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento indicando forte suspeita clínica — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Exame(s) de imagem com lesão sólida suspeita — *documento/exame a anexar*; **obrigatório**
- Tomografia Computadorizada de Abdome — *documento/exame a anexar*; **obrigatório (E)**
- Laudo histopatológico — *laudo histopatológico*; **se houver (opcional)**
- CID sugerido (não literal): C64, C65

Problemas:

- Os 5 locais têm exigências diferentes e não há denominador comum além do encaminhamento: a exigência deste local só cabe hoje como texto de opção/informativa.

Proposta:

- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (RIM | BEXIGA | TESTÍCULO | PRÓSTATA | PÊNIS). Filhas de 'RIM': Documento/Bloqueia 'Exame(s) de imagem com lesão sólida suspeita'; Documento/Bloqueia 'Tomografia Computadorizada de Abdome'; Documento NÃO obrigatório 'Laudo histopatológico'. Chave alternativa para pedido do espelho: CID do pedido começa com C64, C65 (sugestão, não está no manual).

#### 4.3.7 · BEXIGA — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Cistoscopia ('Citoscopia' no manual) com biópsia positiva e/ou massa vesical em TC de abdome e pelve ou USG — *documento/exame a anexar (alternativas 'e/ou' / 'ou')*; **obrigatório ao menos um**
- CID sugerido (não literal): C67

Problemas:

- Os 5 locais têm exigências diferentes e não há denominador comum além do encaminhamento: a exigência deste local só cabe hoje como texto de opção/informativa.
- Grafia do manual: 'Citoscopia' (= cistoscopia). A precedência de 'e/ou ... ou ... ou' é ambígua; leitura adotada: basta um dos achados (cistoscopia com biópsia positiva; massa vesical em TC de abdome e pelve; massa vesical em USG).

Proposta:

- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (RIM | BEXIGA | TESTÍCULO | PRÓSTATA | PÊNIS). Filhas de 'BEXIGA': Documento/Bloqueia 'Cistoscopia ('Citoscopia' no manual) com biópsia positiva e/ou massa vesical em TC de abdome e pelve ou USG'. Chave alternativa para pedido do espelho: CID do pedido começa com C67 (sugestão, não está no manual).

#### 4.3.7 · TESTÍCULO — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento com descrição de massa testicular ao exame clínico com suspeita de neoplasia — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- USG de bolsa escrotal e/ou TC abdome e/ou pelve mostrando massa testicular suspeita — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- Laudo histopatológico — *laudo histopatológico*; **se houver (opcional)**
- CID sugerido (não literal): C62

Problemas:

- Os 5 locais têm exigências diferentes e não há denominador comum além do encaminhamento: a exigência deste local só cabe hoje como texto de opção/informativa.
- Sexo masculino é implícito, mas NÃO está escrito no manual; uma Dedutível de sexo valeria para o recurso inteiro e barraria RIM/BEXIGA de mulheres — impossível sem condicional.

Proposta:

- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (RIM | BEXIGA | TESTÍCULO | PRÓSTATA | PÊNIS). Filhas de 'TESTÍCULO': Documento/Bloqueia 'USG de bolsa escrotal e/ou TC abdome e/ou pelve mostrando massa testicular suspeita'; Documento NÃO obrigatório 'Laudo histopatológico'. Opcional: Dedutível sexo=M com severidade Ressalva (implícito, não literal). Chave alternativa para pedido do espelho: CID do pedido começa com C62 (sugestão, não está no manual).

#### 4.3.7 · PRÓSTATA — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Toque retal alterado — *achado de exame clínico (declaração no encaminhamento)*; **obrigatório**
- PSA > 4 ng/ml — *valor laboratorial (documento + limiar)*; **obrigatório**
- Laudo de biópsia confirmando o diagnóstico — *laudo histopatológico (biópsia)*; **obrigatório**
- CID sugerido (não literal): C61

Problemas:

- Os 5 locais têm exigências diferentes e não há denominador comum além do encaminhamento: a exigência deste local só cabe hoje como texto de opção/informativa.
- Extração antiga: só esta linha saiu ('ONCOLOGIA - PRÓSTATA', SEM_PAR, Documental com o texto inteiro); RIM/BEXIGA/TESTÍCULO/PÊNIS se perderam. Nada foi importado.
- PSA > 4 ng/ml é limiar laboratorial: o modelo não lê valor de exame — vira pergunta declaratória + documento.
- Sexo masculino é implícito, mas NÃO está escrito no manual; uma Dedutível de sexo valeria para o recurso inteiro e barraria RIM/BEXIGA de mulheres — impossível sem condicional.

Proposta:

- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (RIM | BEXIGA | TESTÍCULO | PRÓSTATA | PÊNIS). Filhas de 'PRÓSTATA': Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'Toque retal alterado?'; Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'PSA > 4 ng/ml?' + Documento/Bloqueia 'Resultado do PSA'; Documento/Bloqueia 'Laudo de biópsia confirmando o diagnóstico'. Opcional: Dedutível sexo=M com severidade Ressalva (implícito, não literal). Chave alternativa para pedido do espelho: CID do pedido começa com C61 (sugestão, não está no manual).

#### 4.3.7 · PÊNIS — **AUSENTE** (representável hoje: sim (genérico))

Requisitos:

- Encaminhamento com descrição de lesão peniana ao exame clínico com suspeita de neoplasia — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico — *laudo histopatológico*; **se houver (opcional)**
- CID sugerido (não literal): C60

Problemas:

- Este é o local menos exigente (só encaminhamento + histopatológico se houver): cabe inteiro no denominador comum — desde que as exigências dos outros locais NÃO sejam impostas a todos.
- Sexo masculino é implícito, mas NÃO está escrito no manual; uma Dedutível de sexo valeria para o recurso inteiro e barraria RIM/BEXIGA de mulheres — impossível sem condicional.

Proposta:

- (b) com condicional: Pai: Pergunta de escolha ÚNICA 'Local da lesão' (RIM | BEXIGA | TESTÍCULO | PRÓSTATA | PÊNIS). Filhas de 'PÊNIS': Documento NÃO obrigatório 'Laudo histopatológico'. Opcional: Dedutível sexo=M com severidade Ressalva (implícito, não literal). Chave alternativa para pedido do espelho: CID do pedido começa com C60 (sugestão, não está no manual).

### 4.3.8 — Consulta de primeira vez em Oncologia – TÓRAX (p.17)

- **estrutura:** CONDICIONAL só na forma (3 locais, requisito em célula MESCLADA = idêntico) → SIMPLES + exclusão (metástase pulmonar → sítio primário)
- **recurso_ser:** `Ambulatório 1ª vez - Cirurgia Torácica (Oncologia) (NAO_AE tipo1 v1107)`
- **recurso AE parecido:** AE 'CONSULTA EM CIRURGIA TORACICA' (pareado ao nosso canônico 'CONSULTA EM CIRURGIA TORACICA - ONCOLOGIA', 0 regras, 50 pedidos 'Sem regras'; o ramo AE é regido pelo CRECE, não pelo REUNI)
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Cirurgia Torácica (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Cirurgia Torácica (Oncologia): 10 pedidos 'Sem regras' (+50 no AE pareado a 'CONSULTA EM CIRURGIA TORACICA - ONCOLOGIA', também 'Sem regras')
- **extração antiga (CSV):** Recurso 'ONCOLOGIA - NÓDULOS TUMORAIS PULMÃO TUMORES MEDIASTI TUMORES DA PLEU' (SEM_PAR) com 6 linhas: só 1 é do Tórax ('Todos os pacientes ... TC do tórax e abdome superior'); as outras 5 são da 4.3.9 Coloproctologia engolida ('NO RA Local da lesão Requisitos necessários CÓLON', Colonoscopia..., 'Inserir laudo histopatológico RETO', Colonoscopia (ou Retossigmoidoscopia)..., 'Inserir laudo histopatológico'). O 1º bullet (forte suspeita / sítio primário / histopatológico se houver) se perdeu. Nada foi importado.
- **problemas comuns:**
  - Sem nenhuma regra: 10 pedidos NAO_AE + 50 do recurso AE parecido com 'Sem regras'.
  - A extração antiga misturou Tórax com Coloproctologia — se alguém reaproveitar aquele CSV, Colonoscopia vira exigência da Cirurgia Torácica (ERRADO no CSV, não em produção).
- **proposta (a), melhor no modelo atual:** [G] + Documento/Bloqueia 'TC do tórax e abdome superior' + Documento NÃO obrigatório 'Laudo histopatológico (se houver)' + Informativa 'O encaminhamento deve indicar forte suspeita clínica e informar o sítio primário.' + Pergunta Sim/Não, Bloqueia, 'Sim' bloqueia: 'A lesão é suspeita de metástase (tumor primário em outro órgão)?' — motivo: 'Inserir de acordo com o sítio primário' (literal do manual).
  - **o que se perde:** Nada (requisito igual para os 3 locais).
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.8 · NÓDULOS TUMORAIS DO PULMÃO — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento para o Serviço de Cirurgia Torácica com indicação de forte suspeita clínica, informando o sítio primário — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico — *laudo histopatológico*; **se houver (opcional)**
- TC do tórax e abdome superior ('Todos os pacientes encaminhados') — *documento/exame a anexar*; **obrigatório**
- Suspeita de metástase pulmonar → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**
- CID sugerido (não literal): C34

#### 4.3.8 · TUMORES MEDIASTINO — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento para o Serviço de Cirurgia Torácica com indicação de forte suspeita clínica, informando o sítio primário — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico — *laudo histopatológico*; **se houver (opcional)**
- TC do tórax e abdome superior ('Todos os pacientes encaminhados') — *documento/exame a anexar*; **obrigatório**
- Suspeita de metástase pulmonar → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**
- CID sugerido (não literal): C37, C38.1–C38.3

#### 4.3.8 · TUMORES DA PLEURA — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento para o Serviço de Cirurgia Torácica com indicação de forte suspeita clínica, informando o sítio primário — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico — *laudo histopatológico*; **se houver (opcional)**
- TC do tórax e abdome superior ('Todos os pacientes encaminhados') — *documento/exame a anexar*; **obrigatório**
- Suspeita de metástase pulmonar → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**
- CID sugerido (não literal): C38.4, C45.0

### 4.3.9 — Consulta de primeira vez em Oncologia – COLOPROCTOLOGIA (p.18)

- **estrutura:** CONDICIONAL só na forma (2 locais quase idênticos; só o reto aceita retossigmoidoscopia) → SIMPLES
- **recurso_ser:** `Ambulatório 1ª vez - Coloproctologia (Oncologia) (NAO_AE tipo1 v1105)`
- **recurso AE parecido:** AE 'CONSULTA EM COLOPROCTOLOGIA' (tem regra CRECE p.16 ativa 'Neoplasias anais e colorretais (benignas, malignas e de comportamento incerto)')
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Coloproctologia (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Coloproctologia (Oncologia): 5 pedidos 'Sem regras'
- **extração antiga (CSV):** Engolida pelo recurso do Tórax (ver 4.3.8): as linhas de Colonoscopia e 'Inserir laudo histopatológico' ficaram sob 'ONCOLOGIA - NÓDULOS TUMORAIS PULMÃO...', com fonte 'REUNI p.17' (a seção é da p.18).
- **problemas comuns:**
  - Sem nenhuma regra (5 pedidos 'Sem regras').
  - A extração antiga atribuiu esta tabela ao Tórax.
- **proposta (a), melhor no modelo atual:** [G] + Documento/Bloqueia 'Colonoscopia (ou retossigmoidoscopia, para reto) e/ou TC de abdome total e pelve com contraste e/ou RNM de pelve' + Documento/Bloqueia 'Laudo histopatológico'.
  - **o que se perde:** Retossigmoidoscopia passaria também para cólon (diferença mínima).
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.9 · CÓLON — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Colonoscopia e/ou TC de abdome total e pelve com contraste e/ou RNM de pelve — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- Laudo histopatológico — *laudo histopatológico*; **obrigatório**
- CID sugerido (não literal): C18, C19

#### 4.3.9 · RETO — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Colonoscopia (ou Retossigmoidoscopia) e/ou TC de abdome total e pelve com contraste e/ou RNM de pelve — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- Laudo histopatológico — *laudo histopatológico*; **obrigatório**
- CID sugerido (não literal): C20

### 4.3.10 — Consulta de primeira vez em Oncologia – GINECOLOGIA (p.18)

- **estrutura:** CONDICIONAL (eixo: local da lesão — 2 grupos: vulva/vagina/colo-endométrio = histopatológico; ovário/trompas-anexos = imagem + marcadores se possível)
- **recurso_ser:** `Ambulatório 1ª vez - Ginecologia (Oncologia) (NAO_AE tipo1 v1109)`
- **recurso AE parecido:** AE 'CONSULTA EM GINECOLOGIA - PATOLOGIA CERVICAL' / '- PATOLOGIA VULVA' (não oncológicos; CRECE)
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Ginecologia (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Ginecologia (Oncologia): 1 pedido 'Sem regras'
- **extração antiga (CSV):** Nenhuma linha.
- **problemas comuns:**
  - Sem nenhuma regra.
  - Os dois grupos não têm exigência comum além do encaminhamento: histopatológico obrigatório (grupo A) × imagem (grupo B) — não dá para impor um só a um grupo.
  - Sexo feminino é implícito, não está escrito.
- **proposta (a), melhor no modelo atual:** [G] + Pergunta de LISTA Bloqueia 'Local da lesão' (5 opções com o requisito literal: 'VULVA — laudo histopatológico' ... 'TUMOR DE OVÁRIO — imagem RNM, TC e/ou USG; marcadores tumorais se possível') + Documento/Bloqueia genérico 'Laudo histopatológico (vulva, vagina, colo uterino e endométrio) OU exame de imagem RNM/TC/USG (ovário, trompas e anexos)' + Documento NÃO obrigatório 'Marcadores tumorais (se possível)'.
  - **o que se perde:** O sistema não impõe histopatológico ao grupo A nem imagem ao grupo B; fica declaração na opção.

#### 4.3.10 · VULVA — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico — *laudo histopatológico*; **obrigatório**
- CID sugerido (não literal): C51

Proposta:

- (b) com condicional: Pai: escolha ÚNICA 'Local da lesão'. Filhas do grupo A ('VULVA'): Documento/Bloqueia 'Laudo histopatológico'. Chave alternativa: CID C51 (sugestão).

#### 4.3.10 · VAGINA — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico — *laudo histopatológico*; **obrigatório**
- CID sugerido (não literal): C52

Proposta:

- (b) com condicional: Pai: escolha ÚNICA 'Local da lesão'. Filhas do grupo A ('VAGINA'): Documento/Bloqueia 'Laudo histopatológico'. Chave alternativa: CID C52 (sugestão).

#### 4.3.10 · COLO UTERINO E ENDOMÉTRIO — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento médico com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico — *laudo histopatológico*; **obrigatório**
- CID sugerido (não literal): C53, C54

Proposta:

- (b) com condicional: Pai: escolha ÚNICA 'Local da lesão'. Filhas do grupo A ('COLO UTERINO E ENDOMÉTRIO'): Documento/Bloqueia 'Laudo histopatológico'. Chave alternativa: CID C53, C54 (sugestão).

#### 4.3.10 · TUMOR DE OVÁRIO — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento indicando forte suspeita clínica — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Exame de imagem RNM, TC e/ou USG — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- Marcadores tumorais — *valor laboratorial / documento*; **se possível (opcional)**
- CID sugerido (não literal): C56

Proposta:

- (b) com condicional: Pai: escolha ÚNICA 'Local da lesão'. Filhas do grupo B ('TUMOR DE OVÁRIO'): Documento/Bloqueia 'Exame de imagem RNM, TC e/ou USG'; Documento NÃO obrigatório 'Marcadores tumorais'. Chave alternativa: CID C56 (sugestão).

#### 4.3.10 · TROMPAS E ANEXOS — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento indicando forte suspeita clínica — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Exame de imagem RNM, TC e/ou USG — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- Marcadores tumorais — *valor laboratorial / documento*; **se possível (opcional)**
- CID sugerido (não literal): C57

Proposta:

- (b) com condicional: Pai: escolha ÚNICA 'Local da lesão'. Filhas do grupo B ('TROMPAS E ANEXOS'): Documento/Bloqueia 'Exame de imagem RNM, TC e/ou USG'; Documento NÃO obrigatório 'Marcadores tumorais'. Chave alternativa: CID C57 (sugestão).

### 4.3.11 — Consulta de primeira vez em Oncologia – GINECOLOGIA (Doença Trofoblástica Gestacional (Mola Hidatiforme)) (p.18)

- **estrutura:** SIMPLES
- **recurso_ser:** `Ambulatório 1ª vez em Ginecologia - Doença Trofoblastica Gestacional (Mola Hidatiforme) (NAO_AE tipo1 v1111)`
- **recurso AE parecido:** nenhum
- **nosso (procedimento canônico):** `Ambulatório 1ª vez em Ginecologia - Doença Trofoblastica Gestacional (Mola Hidatiforme)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez em Ginecologia - Doença Trofoblastica Gestacional (Mola Hidatiforme): 0 pedidos no espelho
- **extração antiga (CSV):** Nenhuma linha.
- **problemas comuns:**
  - Sem nenhuma regra.
  - Precedência ambígua de 'BHCG plasmático, exame de USG ... e /ou histopatológico': leitura adotada = BHCG obrigatório + (USG e/ou histopatológico).
- **proposta (a), melhor no modelo atual:** [G] + Documento/Bloqueia 'Resultado de BHCG plasmático' + Documento/Bloqueia 'USG pélvica ou transvaginal e/ou histopatológico indicando o diagnóstico'.
  - **o que se perde:** Nada (se a leitura for confirmada).
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.11 · Doença Trofoblástica Gestacional (Mola Hidatiforme) — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento com descrição detalhada do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- BHCG plasmático — *valor laboratorial / documento*; **obrigatório (leitura adotada)**
- USG pélvica ou transvaginal e/ou histopatológico indicando o diagnóstico — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- CID sugerido (não literal): O01, D39.2, C58

### 4.3.12 — Consulta de primeira vez em Oncologia – CIRURGIA HEPATOBILIAR (p.19)

- **estrutura:** CONDICIONAL (eixo: local da lesão — 2 locais) + exclusão (metástase hepática → sítio primário)
- **recurso_ser:** `Ambulatório 1ª vez - Cirurgia Hepatobiliar (Oncologia) (NAO_AE tipo1 v1110)`
- **recurso AE parecido:** AE 'CONSULTA EM GASTROENTEROLOGIA - HEPATOLOGIA' (não oncológico). Atenção: NAO_AE 'Ambulatório 1ª Vez - Cirurgia Hepato Biliar (Infantil)' (v1151) NÃO é o oncológico.
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Cirurgia Hepatobiliar (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Cirurgia Hepatobiliar (Oncologia): 3 pedidos 'Sem regras'
- **extração antiga (CSV):** Nenhuma linha.
- **problemas comuns:**
  - Sem nenhuma regra (3 pedidos 'Sem regras').
- **proposta (a), melhor no modelo atual:** [G] + Pergunta de LISTA Bloqueia 'Local da lesão' (FÍGADO E VIAS BILIARES — TC/RNM abdome ou CPRE; PÂNCREAS — TC de abdome com massa sólida, mista ou cística) + Documento/Bloqueia 'Exame de imagem: TC de abdome (pâncreas) ou TC/RNM de abdome ou CPRE (fígado e vias biliares)' + Documento NÃO obrigatório 'Marcadores tumorais (se possível)' + Pergunta Sim/Não, Bloqueia, 'Sim' bloqueia: 'A lesão é suspeita de metástase (tumor primário em outro órgão)?' — motivo: 'Inserir de acordo com o sítio primário' (literal do manual).
  - **o que se perde:** A TC obrigatória só para pâncreas não é imposta.

#### 4.3.12 · FÍGADO E VIAS BILIARES — **AUSENTE** (representável hoje: sim (genérico))

Requisitos:

- Encaminhamento indicando forte suspeita clínica de tumor primário — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- TC abdome / RNM abdome / CPRE — *documento/exame a anexar (alternativo '/')*; **obrigatório um deles**
- Marcadores tumorais — *valor laboratorial / documento*; **se possível (opcional)**
- Suspeita de metástase hepática → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**
- CID sugerido (não literal): C22, C23, C24

Problemas:

- O requisito deste local é o mais largo (TC, RNM ou CPRE) e cabe num documento genérico.

Proposta:

- (b) com condicional: Filhas de 'FÍGADO E VIAS BILIARES': Documento/Bloqueia 'TC abdome / RNM abdome / CPRE'; Documento NÃO obrigatório 'Marcadores tumorais'. Chave alternativa: CID C22, C23, C24 (sugestão).

#### 4.3.12 · PÂNCREAS — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento indicando forte suspeita clínica — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- TC de abdome com massa sólida ou mista ou cística — *documento/exame a anexar*; **obrigatório (só TC)**
- Suspeita de metástase hepática → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**
- CID sugerido (não literal): C25

Problemas:

- Pâncreas aceita só TC; um documento genérico 'TC/RNM/CPRE' deixaria passar pâncreas só com RNM.

Proposta:

- (b) com condicional: Filhas de 'PÂNCREAS': Documento/Bloqueia 'TC de abdome com massa sólida ou mista ou cística'. Chave alternativa: CID C25 (sugestão).

### 4.3.13 — Consulta de primeira vez em Oncologia – MASTOLOGIA (p.19)

- **estrutura:** CONDICIONAL (eixo: local/tipo da lesão) — mas cada tipo tem recurso próprio no SER → SIMPLES por recurso
- **recurso_ser:** `Ambulatório 1ª vez - Mastologia (Oncologia) (NAO_AE tipo1 v1102) — inferência: o recurso genérico é o da lesão palpável, porque a impalpável tem recurso próprio (conferir com a regulação)` · `Ambulatório 1ª vez em Mastologia - Lesão Impalpável (Oncologia) (NAO_AE tipo1 v1112)`
- **recurso AE parecido:** AE 'CONSULTA EM MASTOLOGIA' (tem regra CRECE p.28 ativa 'Pacientes que apresentem exames de mamografia com BI-RADS 4 ou BI-RADS 5')
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Mastologia (Oncologia)` · `Ambulatório 1ª vez em Mastologia - Lesão Impalpável (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Mastologia (Oncologia): 1 pedido 'Sem regras' | Ambulatório 1ª vez em Mastologia - Lesão Impalpável (Oncologia): 0 pedidos no espelho
- **extração antiga (CSV):** Nenhuma linha.
- **problemas comuns:**
  - Sem nenhuma regra.
  - **o que se perde:** Nada.

#### 4.3.13 · MAMA COM LESÃO PALPÁVEL — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico — *laudo histopatológico*; **obrigatório**
- Estágio clínico da doença — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- CID sugerido (não literal): C50

Problemas:

- O texto do pdftotext põe 'Informar estágio clínico da doença.' junto da IMPALPÁVEL; na leitura visual o bullet está na célula da PALPÁVEL.

Proposta:

- (a) modelo atual: [G] + Documento/Bloqueia 'Laudo histopatológico' + Informativa 'O encaminhamento deve informar o estágio clínico da doença.'
- (b) com condicional: Não precisa (o SER já separa os recursos).

#### 4.3.13 · MAMA COM LESÃO IMPALPÁVEL — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento indicando forte suspeita clínica — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- USG e Mamografia com categoria 4 e 5 de BI-RADS — *documento/exame a anexar (os dois)*; **obrigatório**
- CID sugerido (não literal): C50, D05, N63

Problemas:

- 'categoria 4 e 5' deve ser lido como BI-RADS 4 OU 5 (como no CRECE).

Proposta:

- (a) modelo atual: [G] + Documento/Bloqueia 'USG mamária' + Documento/Bloqueia 'Mamografia' + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'Os exames têm categoria BI-RADS 4 ou 5?'
- (b) com condicional: Não precisa.

### 4.3.14 — Consulta de primeira vez em Oncologia – Tumor de Tecido Ósseo-Conectivo (TOC) ADULTO / INFANTIL (p.19)

- **estrutura:** CONDICIONAL (eixo: local da lesão — 4 linhas; só MELANOMA >2 CM exige histopatológico sem alternativa) + exclusão (metástase óssea → sítio primário); 2 recursos no SER (Adulto/Infantil) com os mesmos requisitos
- **recurso_ser:** `Ambulatório 1ª Vez - Tumores do Tecido Ósseo e Conectivo (Adulto) (NAO_AE tipo1 v1119) e Ambulatório 1ª Vez - Tumores do Tecido Ósseo e Conectivo (Infantil) (NAO_AE tipo1 v1118)`
- **recurso AE parecido:** nenhum oncológico (AE 'CONSULTA EM CIRURGIA GERAL - PARTES MOLES' EXCLUI lesões malignas, CRECE p.51)
- **nosso (procedimento canônico):** `Ambulatório 1ª Vez - Tumores do Tecido Ósseo e Conectivo (Adulto) / Ambulatório 1ª Vez - Tumores do Tecido Ósseo e Conectivo (Infantil)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª Vez - Tumores do Tecido Ósseo e Conectivo (Adulto): 0 pedidos no espelho; Ambulatório 1ª Vez - Tumores do Tecido Ósseo e Conectivo (Infantil): 0 pedidos no espelho
- **extração antiga (CSV):** Nenhuma linha.
- **problemas comuns:**
  - Sem nenhuma regra.
  - Remissão da p.25 (4.4 Ortopedia): 'Pacientes com diagnóstico de TUMOR ÓSSEO deverão ser encaminhados à fila de Ambulatório de 1ªvez – Tumores do Tecido Ósseo-Conectivo.'
  - O manual não define o corte de idade entre Adulto e Infantil.
- **proposta (a), melhor no modelo atual:** [G] + Pergunta de LISTA Bloqueia 'Local da lesão' (4 opções literais; 'MELANOMA >2 CM — laudo histopatológico obrigatório') + Documento/Bloqueia 'Laudo histopatológico e/ou exame de imagem (melanoma >2 cm: histopatológico)' + Pergunta Sim/Não, Bloqueia, 'Sim' bloqueia: 'A lesão é suspeita de metástase (tumor primário em outro órgão)?' — motivo: 'Inserir de acordo com o sítio primário' (literal do manual). Repetir em Adulto e Infantil.
  - **o que se perde:** O histopatológico obrigatório só para melanoma não é imposto.

#### 4.3.14 · CÂNCER ÓSSEO — **AUSENTE** (representável hoje: sim (genérico))

Requisitos:

- Encaminhamento indicando forte suspeita clínica — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico e/ou exame de imagem — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- Suspeita de metástase óssea → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**
- CID sugerido (não literal): C40, C41

Proposta:

- (b) com condicional: Filhas de 'CÂNCER ÓSSEO': Documento/Bloqueia 'Laudo histopatológico e/ou exame de imagem'. Chave alternativa: CID C40, C41 (sugestão).

#### 4.3.14 · SARCOMA DE PERNAS E BRAÇOS — **AUSENTE** (representável hoje: sim (genérico))

Requisitos:

- Encaminhamento indicando forte suspeita clínica — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico e/ou exame de imagem — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- Suspeita de metástase óssea → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**
- CID sugerido (não literal): C40, C49

Proposta:

- (b) com condicional: Filhas de 'SARCOMA DE PERNAS E BRAÇOS': Documento/Bloqueia 'Laudo histopatológico e/ou exame de imagem'. Chave alternativa: CID C40, C49 (sugestão).

#### 4.3.14 · TUMORES MALIGNOS DE PARTES DE MOLES — **AUSENTE** (representável hoje: sim (genérico))

Requisitos:

- Encaminhamento com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico e/ou exame de imagem — *documento/exame a anexar (e/ou)*; **obrigatório ao menos um**
- Suspeita de metástase óssea → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**
- CID sugerido (não literal): C49

Proposta:

- (b) com condicional: Filhas de 'TUMORES MALIGNOS DE PARTES DE MOLES': Documento/Bloqueia 'Laudo histopatológico e/ou exame de imagem'. Chave alternativa: CID C49 (sugestão).

#### 4.3.14 · MELANOMA >2 CM — **NAO_REPRESENTAVEL_HOJE** (representável hoje: não)

Requisitos:

- Encaminhamento com a descrição clínica do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Laudo histopatológico — *laudo histopatológico*; **obrigatório**
- Suspeita de metástase óssea → inserir de acordo com o sítio primário — *exclusão (redirecionamento)*; **exclui**
- CID sugerido (não literal): C43

Problemas:

- Melanoma >2 cm exige histopatológico (sem 'e/ou imagem'); um documento genérico 'histopatológico e/ou imagem' deixaria passar melanoma só com imagem.
- Melanoma está em TOC e não em 4.3.5 (Pele) — o manual não remete de uma seção à outra.

Proposta:

- (b) com condicional: Filhas de 'MELANOMA >2 CM': Documento/Bloqueia 'Laudo histopatológico'. Chave alternativa: CID C43 (sugestão).

### 4.3.15 — Consulta de primeira vez em Oncologia – HEMATOLOGIA (p.20-21/22)

- **estrutura:** INCLUSAO_EXCLUSAO (inclusão = LISTA_BASTA_UM; exclusões a–d; OBSERVAÇÃO de fluxo)
- **recurso_ser:** `Ambulatório 1ª vez - Hematologia (Oncologia) (NAO_AE tipo1 v1100)` · `Ambulatório 1ª vez - Hematologia Pediátrica (Oncologia) (NAO_AE tipo1 v1116)`
- **recurso AE parecido:** NAO_AE 'Ambulatório 1ª vez - Hematologia (Adulto)' é a hematologia NÃO oncológica (REUNI p.33)
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Hematologia (Oncologia)` · `Ambulatório 1ª vez - Hematologia Pediátrica (Oncologia)` — regras: 3 ativas, 4 inativas
  - *ONCOLOGIA HEMATOLOGIA ADULTO* — ativas:
    - [ATIVA v1 Documento/Bloqueia] 'Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.' (fonte: CRECE/REUNI — requisito global)
    - [ATIVA v1 Pergunta Sim/Não/Bloqueia, 'Sim' bloqueia] 'a. Adenomegalias reacionais a viroses agudas. b. Atipias linfocitárias em presença de viroses agudas. c. Trombocitose em hemograma único. d. Pacientes com adenomegalias suspeitas sem biópsia' (REUNI p.20)
    - [ATIVA v1 Pergunta de LISTA/Bloqueia, 'nenhuma' bloqueia] 'O paciente se enquadra em ao menos um dos critérios abaixo?' — 10 opções: Dor óssea + eletroforese com gamopatia monoclonal | Bicitopenia após afastar causas clínicas | Citopenias + manifestações suspeitas de leucemia aguda | Citopenias + linfonodomegalia e esplenomegalia não explicada | Blastos/promielócitos no sangue periférico | Febre + neutropenia (< 1500 /μL) | Bicitopenia/pancitopenia graves (Hb < 7, neutrófilos < 500, plaquetas < 50 mil) | Trombocitose (d/e fundidos, com 'excluir ... causas secundárias') | Leucocitose com desvio à esquerda (f.1–f.3) | Policitemia (g.1–g.2) (REUNI p.20)
    - inativas:
      - [inativa v1 Pergunta] 'Neutrófilos < 500 cels/μL; e/ou' (fragmento de c.4)
      - [inativa v1 Pergunta] '/ Agendamento: a. Dor óssea ... c.4. Bicitopenia/pancitopenia com alterações hematológicas graves' (fragmento)
      - [inativa v1 Pergunta] 'Hemoglobina < 7 g/dl; e/ou' (fragmento de c.4)
      - [inativa v1 Dedutível com sexo=F] 'Plaquetas < 50 mil cels/mm³. d. Trombocitose ... OBSERVAÇÃO: PACIENTES COM SUSPEITA DE LEUCEMIA AGUDA NÃO SÃO REGULADOS...' — artefato: o sexo F veio de 'hb > 16,0g/dl em mulheres'
  - *ONCOLOGIA HEMATOLOGIA PEDIÁTRICA* — ativas:
    - [ATIVA v1 Documento/Bloqueia] 'Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.' (requisito global)
    - [ATIVA v1 Pergunta Sim/Não/Bloqueia, 'Sim' bloqueia] mesmo texto de exclusão a–d (REUNI p.22)
    - [ATIVA v1 Pergunta de LISTA/Bloqueia, 'nenhuma' bloqueia] 'O paciente se enquadra em ao menos um dos critérios abaixo?' — 8 opções: Bicitopenia | Citopenias + leucemia aguda | Citopenias + linfonodomegalia/esplenomegalia | Blastos/promielócitos | Febre + neutropenia (< 1500 /μL) | Bicitopenia/pancitopenia graves | Trombocitose (c.1–c.4 + excluir causas secundárias) | Leucocitose com desvio à esquerda (d.1–d.2) (REUNI p.22)
    - inativas:
      - [inativa v1 Pergunta] '/ Agendamento: a. Bicitopenia ... b.4. ...' (fragmento)
      - [inativa v1 Pergunta] 'Hemoglobina < 7 g/dl; e/ou'
      - [inativa v1 Pergunta] 'Plaquetas < 50 mil cels/mm³. c. Trombocitose ... OBSERVAÇÃO ...' (fragmento)
      - [inativa v1 Pergunta] 'Neutrófilos < 500 cels/μL; e/ou'
- **espelho:** Ambulatório 1ª vez - Hematologia (Oncologia): 13 pedidos 'A conferir' | Ambulatório 1ª vez - Hematologia Pediátrica (Oncologia): 0 pedidos no espelho
- **extração antiga (CSV):** 'ONCOLOGIA - ONCOLOGIA HEMATOLOGIA ADULTO' pareado 'composto' com o recurso: 1 linha de exclusão + 4 de inclusão (fragmentadas). Foi a única parte da 4.3 importada (e depois reescrita como lista). / 'ONCOLOGIA - ONCOLOGIA HEMATOLOGIA PEDIÁTRICA' pareado 'composto' com o recurso: 1 linha de exclusão + 4 de inclusão (fragmentadas). Foi a única parte da 4.3 importada (e depois reescrita como lista).
- **problemas comuns:**
  - Falta a OBSERVAÇÃO (literal: 'PACIENTES COM SUSPEITA DE LEUCEMIA AGUDA NÃO SÃO REGULADOS AMBULATORIALMENTE. NECESSÁRIO ESTAR INTERNADO E INSERIDO NO SISTEMA SER PARA TRANSFERÊNCIA HOSPITALAR.'). Nenhuma regra ATIVA a carrega (só aparece dentro de uma regra inativa). Pior: a opção 'Citopenias + manifestações clínicas suspeitas de leucemia aguda' (no manual, com 'VIDE OBSERVAÇÃO') é critério de INCLUSÃO na lista — marcá-la faz o pedido passar, quando o manual manda internar e pedir transferência hospitalar.
  - A pergunta de exclusão não pergunta nada: o texto exibido é a própria lista 'a. ... d.'; deveria ser 'Algum destes critérios de exclusão se aplica ao paciente?' (como na Bariátrica).
  - Na opção Trombocitose, o 'Excluir: trombocitose persistente após exclusão de causas secundárias' do manual (literalmente contraditório) foi reescrito como 'excluir trombocitose persistente por causas secundárias' — interpretação razoável, mas é paráfrase; conferir com a regulação.
  - 4 regras inativas são fragmentos da extração (inofensivas, mas poluem a tela de regras).
- **proposta (a), melhor no modelo atual:** Manter as 3 ativas. Acrescentar: Informativa (literal) 'OBSERVAÇÃO: PACIENTES COM SUSPEITA DE LEUCEMIA AGUDA NÃO SÃO REGULADOS AMBULATORIALMENTE. NECESSÁRIO ESTAR INTERNADO E INSERIDO NO SISTEMA SER PARA TRANSFERÊNCIA HOSPITALAR.'; reescrever a pergunta de exclusão como 'Algum destes critérios de exclusão se aplica ao paciente?' (opções a–d, 'Sim' bloqueia); e uma Pergunta Sim/Não com severidade Ressalva ('Sim' faz ressalva) 'Há suspeita de leucemia aguda?' — motivo: 'Não é regulado ambulatorialmente; internar e inserir no SER para transferência hospitalar.' (Ressalva, não Bloqueia, porque o próprio manual lista o critério c como inclusão com 'VIDE OBSERVAÇÃO'.) Corrigir a fonte para 'REUNI p.20–21'. Excluir as 4 inativas-fragmento.
  - **o que se perde:** Nada essencial.
- **proposta (b), com regra condicional:** A OBSERVAÇÃO viraria filha das opções 'Citopenias + leucemia aguda' / 'Blastos' / 'Leucocitose ...': se marcadas → Ressalva com o texto literal.

#### 4.3.15 · ONCOLOGIA HEMATOLOGIA ADULTO — **PARCIAL** (representável hoje: sim)

Requisitos:

- Critérios de exclusão a–d (adenomegalias reacionais a viroses agudas; atipias linfocitárias em viroses agudas; trombocitose em hemograma único; adenomegalias suspeitas sem biópsia) — *exclusão*; **exclui**
- Critérios de inclusão/agendamento (a–g, basta um) — *inclusão (lista, basta uma)*; **obrigatório ao menos um**
- Excluir: trombocitose persistente após exclusão de causas secundárias (texto literal ambíguo) — *exclusão dentro do critério de trombocitose*; **exclui**
- OBSERVAÇÃO: PACIENTES COM SUSPEITA DE LEUCEMIA AGUDA NÃO SÃO REGULADOS AMBULATORIALMENTE. NECESSÁRIO ESTAR INTERNADO E INSERIDO NO SISTEMA SER PARA TRANSFERÊNCIA HOSPITALAR. — *orientação / exclusão de fluxo (leucemia aguda → internação + SER hospitalar)*; **exclui do ambulatório**

Problemas:

- A fonte diz 'REUNI p.20', mas os critérios e, f, g e a OBSERVAÇÃO estão na p.21.
- Acertos da produção: o manual repete 'Trombocitose' em d e e (texto igual) e a produção fundiu numa opção; 'neurogenia' (c.3) foi corrigido para 'neutropenia'.

#### 4.3.15 · ONCOLOGIA HEMATOLOGIA PEDIÁTRICA — **PARCIAL** (representável hoje: sim)

Requisitos:

- Critérios de exclusão a–d (adenomegalias reacionais a viroses agudas; atipias linfocitárias em viroses agudas; trombocitose em hemograma único; adenomegalias suspeitas sem biópsia) — *exclusão*; **exclui**
- Critérios de inclusão/agendamento (a–d, basta um) — *inclusão (lista, basta uma)*; **obrigatório ao menos um**
- Excluir: trombocitose persistente após exclusão de causas secundárias (texto literal ambíguo) — *exclusão dentro do critério de trombocitose*; **exclui**
- OBSERVAÇÃO: PACIENTES COM SUSPEITA DE LEUCEMIA AGUDA NÃO SÃO REGULADOS AMBULATORIALMENTE. NECESSÁRIO ESTAR INTERNADO E INSERIDO NO SISTEMA SER PARA TRANSFERÊNCIA HOSPITALAR. — *orientação / exclusão de fluxo (leucemia aguda → internação + SER hospitalar)*; **exclui do ambulatório**

### 4.3.16 — Consulta de primeira vez em Oncologia – CARDIOLOGIA (p.23)

- **estrutura:** LISTA_BASTA_UM (Indicação: 3 itens) + encaminhamento
- **recurso_ser:** `Ambulatório 1ª vez - Cardiologia (Oncologia) (NAO_AE tipo1 v1121)`
- **recurso AE parecido:** AE 'CONSULTA EM CARDIOLOGIA' (não oncológico)
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Cardiologia (Oncologia)` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Cardiologia (Oncologia): 0 pedidos no espelho
- **extração antiga (CSV):** 'ONCOLOGIA - AVALIAÇÃO CARDIOLÓGICA PARA PACIENTES EM TRATAMENTO ONCOLÓGICO' SEM_PAR: 4 linhas NaoDedutivel (uma é só o rótulo 'Indicação'); não importadas.
- **problemas comuns:**
  - Sem nenhuma regra.
  - A extração antiga não pareou com 'Cardiologia (Oncologia)' (nome diferente) e por isso nada foi importado.
- **proposta (a), melhor no modelo atual:** [G] + Pergunta de LISTA Bloqueia ('nenhuma destas' bloqueia) 'Indicação' com as 3 opções literais.
  - **o que se perde:** Nada.
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.16 · AVALIAÇÃO CARDIOLÓGICA PARA PACIENTES EM TRATAMENTO ONCOLÓGICO — **AUSENTE** (representável hoje: sim)

Requisitos:

- Indicação (basta uma): complicações cardiológicas do tratamento radioterápico ou quimioterápico; doença cardíaca com necessidade de avaliação pré-quimioterapia; neoplasia primária do coração — *inclusão (lista, basta uma)*; **obrigatório ao menos um**
- Encaminhamento médico com descrição do caso — *conteúdo obrigatório do encaminhamento*; **obrigatório**

### 4.3.17 — Consulta de primeira vez em Oncologia – NEUROLOGIA (p.23)

- **estrutura:** INCLUSAO_EXCLUSAO (idade acima de 18; inclusão LISTA_BASTA_UM de 3; exclusão de 6)
- **recurso_ser:** NÃO EXISTE no catálogo do SER exportado (423 recursos) — não há 'Avaliação em Oncologia (Neurologia)' nem 'Neurologia (Oncologia)'
- **recurso AE parecido:** AE 'CONSULTA EM NEUROLOGIA' (não oncológico; regras CRECE p.32)
- **nosso (procedimento canônico):** `nenhum` — regras: nenhuma
- **espelho:** —
- **extração antiga (CSV):** 'ONCOLOGIA - AVALIAÇÃO EM ONCOLOGIA (NEUROLOGIA)' SEM_PAR: 1 Dedutível (idade_min 18) + 3 inclusão + 6 exclusão; a última linha engoliu o cabeçalho da 4.3.18 ('... Recurso Requisitos necessários TRIAGEM EM ONCOLOGIA PEDIÁTRICA').
- **problemas comuns:**
  - Sem recurso no SER: nada onde pendurar a regra. Pode existir em perfil que Maricá não enxerga.
  - 'acima de 18 anos' × exclusão 'menos de 18 anos': quem tem exatamente 18 fica ambíguo; leitura adotada: idade mínima 18.
- **proposta (a), melhor no modelo atual:** Deixar especificado e não cadastrar até o recurso aparecer no catálogo. Quando aparecer: Dedutível idade mínima 18 (Bloqueia) + Pergunta de LISTA de inclusão (3 opções) + Pergunta de exclusão (5 itens, 'Sim' bloqueia) + [G].
  - **o que se perde:** —
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.17 · AVALIAÇÃO EM ONCOLOGIA (NEUROLOGIA) — **SEM_RECURSO_NO_SER** (representável hoje: sim (se o recurso existir))

Requisitos:

- Idade: acima de 18 anos (e exclusão 'Indivíduos com menos de 18 anos') — *idade (Dedutível)*; **obrigatório**
- Complicações neurológicas de QT/imunoterapia/RT; complicações neurológicas de TMO para doenças hemato-oncológicas; suspeita de encefalite paraneoplásica com neoplasia já identificada — *inclusão (lista, basta uma)*; **obrigatório ao menos um**
- Tratamento primário de doenças oncológicas/hemato-oncológicas/neoplasias do SNC ou periférico; neuro-oncologia cirúrgica; cuidados paliativos; doenças infecciosas, dores, cefaleias, transtornos emocionais ou psiquiátricos; pacientes graves e instáveis — *exclusão*; **exclui**

### 4.3.18 — Consulta de primeira vez em Oncologia - PEDIATRIA (p.24)

- **estrutura:** SIMPLES (só conteúdo do encaminhamento)
- **recurso_ser:** `Ambulatório 1ª vez - Triagem em Oncologia Pediátrica (NAO_AE tipo1 v1113)`
- **recurso AE parecido:** nenhum
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Triagem em Oncologia Pediátrica` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Triagem em Oncologia Pediátrica: 0 pedidos no espelho
- **extração antiga (CSV):** Nenhuma linha própria (o cabeçalho foi engolido pela última linha da 4.3.17).
- **problemas comuns:**
  - Sem nenhuma regra (nem o encaminhamento).
  - O manual não dá idade máxima de 'infantil'.
- **proposta (a), melhor no modelo atual:** Documento/Bloqueia 'Encaminhamento médico com descrição detalhada do caso suspeito de câncer infantil'.
  - **o que se perde:** Nada.
- **proposta (b), com regra condicional:** Não precisa.

#### 4.3.18 · TRIAGEM EM ONCOLOGIA PEDIÁTRICA — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento com descrição detalhada do caso suspeito de câncer infantil — *conteúdo obrigatório do encaminhamento*; **obrigatório**

### 4.3.19 — Consulta de primeira vez em Oncologia – MEDICINA NUCLEAR (p.24)

- **estrutura:** SIMPLES (cada linha da tabela é um recurso próprio do SER)
- **recurso_ser:** `Ambulatório 1ª vez - Planejamento em Radioterapia (NAO_AE tipo1 v1126)` · `Ambulatório 1ª vez - Planejamento em Radioterapia (Infantil) (NAO_AE tipo1 v1128)` · `Ambulatório 1ª vez - Planejamento em Braquiterapia (NAO_AE tipo1 v1127)` · `Ambulatório 1ª vez - Radiocirurgia Gamma Knife (NAO_AE tipo1 v1129)` · `Ambulatório 1ª vez - Planejamento em Iodoterapia (NAO_AE tipo1 v1125)`
- **recurso AE parecido:** nenhum
- **nosso (procedimento canônico):** `Ambulatório 1ª vez - Planejamento em Radioterapia` · `Ambulatório 1ª vez - Planejamento em Radioterapia (Infantil)` · `Ambulatório 1ª vez - Planejamento em Braquiterapia` · `Ambulatório 1ª vez - Radiocirurgia Gamma Knife` · `Ambulatório 1ª vez - Planejamento em Iodoterapia` — regras: nenhuma (o procedimento canônico existe e está pareado ao recurso do SER, mas tem 0 regras — nem o 'requisito global' de encaminhamento)
- **espelho:** Ambulatório 1ª vez - Planejamento em Radioterapia: 13 pedidos 'Sem regras' | Ambulatório 1ª vez - Planejamento em Radioterapia (Infantil): 1 pedido 'Sem regras' | Ambulatório 1ª vez - Planejamento em Braquiterapia: 0 pedidos no espelho | Ambulatório 1ª vez - Radiocirurgia Gamma Knife: 1 pedido 'Sem regras' | Ambulatório 1ª vez - Planejamento em Iodoterapia: 7 pedidos 'Sem regras'
- **extração antiga (CSV):** Nenhuma linha.
- **problemas comuns:**
  - Sem nenhuma regra.

#### 4.3.19 · PLANEJAMENTO EM RADIOTERAPIA — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento claro e detalhado indicando o sítio a ser irradiado — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Exame histopatológico — *laudo histopatológico*; **obrigatório**
- Peso do paciente — *dado (já é campo obrigatório do formulário do SER)*; **obrigatório**

Proposta:

- (a) modelo atual: [G] + Documento/Bloqueia 'Exame histopatológico' + Informativa 'Indicar o sítio a ser irradiado.' (O peso já é campo obrigatório do formulário do SER: 'Peso do Paciente (gramas)'.)
  - perde: Nada.
- (b) com condicional: Não precisa.

#### 4.3.19 · PLANEJAMENTO EM RADIOTERAPIA INFANTIL — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento claro e detalhado indicando o sítio a ser irradiado — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Exame histopatológico — *laudo histopatológico*; **obrigatório**

Proposta:

- (a) modelo atual: [G] + Documento/Bloqueia 'Exame histopatológico' + Informativa 'Indicar o sítio a ser irradiado.' (O formulário do SER deste recurso já tem 'Resultado do histopatológico' obrigatório — 10 campos, diferente dos outros 30.)
  - perde: Nada.
- (b) com condicional: Não precisa.

#### 4.3.19 · PLANEJAMENTO EM BRAQUITERAPIA — **AUSENTE** (representável hoje: parcial)

Requisitos:

- Encaminhamento claro e detalhado indicando o sítio a ser irradiado — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Exame histopatológico — *laudo histopatológico*; **obrigatório**
- Risco cirúrgico — *documento/exame a anexar*; **condicional: só para pacientes NÃO histerectomizadas**

Problemas:

- CUIDADO: o pdftotext -layout embaralha esta tabela (põe 'histopatológico ... risco cirúrgico' no Gamma Knife e 'imagem há menos de 06 meses' na Iodoterapia). A leitura visual da p.24 é a que vale: Braquiterapia = sítio + histopatológico + risco cirúrgico (não-histerectomizadas).
- Cláusula condicional interna (não-histerectomizadas) — sem condicional, só como pergunta 'se aplicável' ou documento não obrigatório.

Proposta:

- (a) modelo atual: [G] + Documento/Bloqueia 'Exame histopatológico' + Pergunta Sim/Não Bloqueia ('Não' bloqueia) 'Se a paciente NÃO é histerectomizada, o risco cirúrgico foi anexado? (responda Sim se não se aplica)' + Documento NÃO obrigatório 'Risco cirúrgico (obrigatório para não-histerectomizadas)'.
  - perde: A exigência de risco cirúrgico depende de declaração.
- (b) com condicional: Pai: Pergunta Sim/Não 'A paciente é histerectomizada?'; filha quando 'Não': Documento/Bloqueia 'Risco cirúrgico'.

#### 4.3.19 · PLANEJAMENTO EM GAMMA KNIFE — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento claro e detalhado indicando o procedimento — *conteúdo obrigatório do encaminhamento*; **obrigatório**
- Exame de imagem há menos de 06 meses — *documento/exame a anexar com validade*; **obrigatório (validade 180 dias)**

Problemas:

- CUIDADO: o pdftotext -layout embaralha esta tabela (põe 'histopatológico ... risco cirúrgico' no Gamma Knife e 'imagem há menos de 06 meses' na Iodoterapia). A leitura visual da p.24 é a que vale: Gamma Knife = procedimento + imagem < 6 meses.

Proposta:

- (a) modelo atual: [G] + Documento/Bloqueia 'Exame de imagem' com validade_dias = 180 (o modelo já tem validade no documento).
  - perde: Nada.
- (b) com condicional: Não precisa.

#### 4.3.19 · PLANEJAMENTO EM IODOTERAPIA — **AUSENTE** (representável hoje: sim)

Requisitos:

- Encaminhamento claro e detalhado indicando o procedimento — *conteúdo obrigatório do encaminhamento*; **obrigatório**

Problemas:

- CUIDADO: o pdftotext -layout embaralha esta tabela (põe 'histopatológico ... risco cirúrgico' no Gamma Knife e 'imagem há menos de 06 meses' na Iodoterapia). A leitura visual da p.24 é a que vale: Iodoterapia = só 'indicando o procedimento'.

Proposta:

- (a) modelo atual: [G] (só isso — o manual não exige exame).
  - perde: Nada.
- (b) com condicional: Não precisa.

## Regras de produção com fonte "REUNI p.15" a "p.24"

São 12, todas sob a fonte p.20 ou p.22, todas no procedimento certo e todas com `sistema = SER`. Nenhuma caiu em procedimento errado. Nenhuma regra de produção cita as p.15–19, 23 ou 24.

| procedimento | estado | tipo | texto (resumo) | avaliação |
|---|---|---|---|---|
| Hematologia (Oncologia) | ATIVA | Pergunta Sim/Não, "Sim" bloqueia | "a. Adenomegalias reacionais ... d. Pacientes com adenomegalias suspeitas sem biópsia" | Conteúdo correto. O texto exibido não é pergunta (falta "Algum destes critérios de exclusão se aplica?"). |
| Hematologia (Oncologia) | ATIVA | Pergunta de LISTA (10 opções) | "O paciente se enquadra em ao menos um dos critérios abaixo?" | Completa (a–g; d/e fundidos; "neurogenia"→"neutropenia"). Falta a OBSERVAÇÃO. A fonte devia ser "p.20–21". |
| Hematologia (Oncologia) | inativa | Pergunta | "Neutrófilos < 500 cels/μL; e/ou" | Fragmento. Pode excluir. |
| Hematologia (Oncologia) | inativa | Pergunta | "/ Agendamento: a. Dor óssea ..." | Fragmento. Pode excluir. |
| Hematologia (Oncologia) | inativa | Pergunta | "Hemoglobina < 7 g/dl; e/ou" | Fragmento. Pode excluir. |
| Hematologia (Oncologia) | inativa | **Dedutível sexo=F** | "Plaquetas < 50 mil ... OBSERVAÇÃO ..." | **Artefato**: o sexo F saiu de "hb > 16,0g/dl em mulheres". Inativa (não causa dano), mas é a única regra com conteúdo que não corresponde ao manual. Excluir. |
| Hematologia Pediátrica (Oncologia) | ATIVA | Pergunta Sim/Não, "Sim" bloqueia | exclusão a–d | Mesma observação do adulto. |
| Hematologia Pediátrica (Oncologia) | ATIVA | Pergunta de LISTA (8 opções) | "O paciente se enquadra em ao menos um dos critérios abaixo?" | Completa (a, b, b.1–b.4, c, d). Falta a OBSERVAÇÃO. |
| Hematologia Pediátrica (Oncologia) | inativa ×4 | Pergunta | fragmentos ("/ Agendamento", "Hemoglobina < 7", "Plaquetas < 50 mil ... OBSERVAÇÃO", "Neutrófilos < 500") | Fragmentos. Podem ser excluídos. |

As duas também têm a regra ativa "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global"), que **não existe em nenhum dos outros 29 procedimentos oncológicos**.

Sobre a 4.3.15, uma nota: na opção "Trombocitose" a produção escreveu "excluir trombocitose persistente por causas secundárias". O manual diz, literalmente, "Excluir: trombocitose persistente após exclusão de causas secundárias" — tomado ao pé da letra, isso excluiria justamente a trombocitose primária. A leitura da produção é a sensata, mas é uma interpretação: vale confirmar com a regulação.

## Recursos oncológicos do SER sem seção na 4.3

| recurso SER (NAO_AE) | v | regras | espelho | onde está no REUNI |
|---|---|---|---|---|
| Ambulatório 1ª vez - Oncologia Geral (Adulto) | 1101 | 0 | **44 "Sem regras"** | não aparece no manual |
| Ambulatório 1ª vez - Planejamento em Quimioterapia | 1122 | 0 | 0 | 4.3.19 lista Radioterapia/Braquiterapia/Gamma Knife/Iodoterapia, **não** Quimioterapia |
| Ambulatório 1ª Vez - Cirurgia Geral - Reconstrução de trânsito pós Oncologia | 1124 | 0 | 1 "Sem regras" | não aparece |
| Ambulatório 1ª vez em Cirurgia Pediátrica - Implante de Cateter (Oncologia) | 1123 | 0 | 0 | não aparece |
| Ambulatório 1ª vez em Cirurgia Plástica Reparadora - Mama (Oncologia) | 1063 | 0 | 0 | seção própria na **p.36** (fora deste trecho) |

Proposta para os quatro sem manual: pôr só [G], que é requisito geral do SER. Assim eles saem de "Sem regras" e passam a cobrar pelo menos o encaminhamento.

Visto de passagem, fora do escopo: o recurso SER "Ambulatório 1ª vez em Cardiologia - Arritimias (Infantil)" tem origem **inativa** para o canônico "Hematologia (Infantil)"; "Amiloidose Cardíaca" tem origem inativa para "Hematologia (Adulto)"; "Cardiopatia Congênita (Adulto)" tem origem inativa para "Cirurgia Bariátrica (Adulto)". Isso faz o `ser_catalogo_resumo.txt` mostrar "regras ativas: 10" na Amiloidose. Confirmar que a contagem e a análise ignoram origem inativa.

## O que a extração antiga (spike-e CSV) fez com a 4.3

Das 1.169 linhas, 33 têm fonte REUNI p.15–24. Só a Hematologia foi pareada ("composto"); o resto ficou `SEM_PAR` e não entrou em produção.

| recurso no CSV | linhas | pareamento | o que aconteceu |
|---|---|---|---|
| ONCOLOGIA - ONCOLOGIA HEMATOLOGIA ADULTO | 5 | composto | fragmentado; virou as regras de p.20, depois reescritas como lista |
| ONCOLOGIA - ONCOLOGIA HEMATOLOGIA PEDIÁTRICA | 5 | composto | idem p.22 |
| ONCOLOGIA - AVALIAÇÃO EM ONCOLOGIA (NEUROLOGIA) | 10 | SEM_PAR | completo (idade 18 + 3 inclusões + 6 exclusões), mas sem recurso no SER; a última linha engoliu "Recurso Requisitos necessários TRIAGEM EM ONCOLOGIA PEDIÁTRICA" |
| ONCOLOGIA - AVALIAÇÃO CARDIOLÓGICA PARA PACIENTES EM TRATAMENTO ONCOLÓGICO | 4 | SEM_PAR | 3 indicações + uma linha que é só o rótulo "Indicação"; o nome não bateu com "Cardiologia (Oncologia)" |
| ONCOLOGIA - NÓDULOS TUMORAIS PULMÃO TUMORES MEDIASTI TUMORES DA PLEU | 6 | SEM_PAR | **ERRADO**: só 1 linha é do Tórax; 5 são da 4.3.9 Coloproctologia (Cólon/Reto) e saíram com fonte p.17; o 1º bullet do Tórax se perdeu |
| ONCOLOGIA - ESÔFAGO / ESTÔMAGO/DUODENO | 1 + 1 | SEM_PAR | só o documento; o encaminhamento se perdeu |
| ONCOLOGIA - PRÓSTATA | 1 | SEM_PAR | **só PRÓSTATA saiu da Urologia**; RIM, BEXIGA, TESTÍCULO e PÊNIS sumiram |
| (nada) | 0 | — | 4.3.1, 4.3.2, 4.3.3, 4.3.4, 4.3.5, 4.3.10, 4.3.11, 4.3.12, 4.3.13, 4.3.14, 4.3.18, 4.3.19 **não geraram nenhuma linha** |

Conclusão: a tabela "Local da lesão" (bullets numa célula, ou várias linhas com a 1ª coluna mesclada) derrotou a extração. O CSV não serve de base para a 4.3. A base é o inventário abaixo.

## Recomendação de modelagem

**1. Já, no modelo atual (sem código novo):**

- Pôr [G] nos 29 procedimentos oncológicos sem regra e nos 4 recursos sem seção. Isso tira 153 pedidos de "Sem regras" e passa a cobrar o encaminhamento.
- Cadastrar como regras comuns as subseções **SIMPLES** e as de local idêntico. São documentos e listas simples: 4.3.2, 4.3.3, 4.3.4 (×2), 4.3.5, 4.3.6, 4.3.8, 4.3.9, 4.3.11, 4.3.13 (×2), 4.3.16, 4.3.18 e 4.3.19 (×5, com Gamma Knife usando `validade_dias = 180`). Exclusão de metástase com [M] em 4.3.4, 4.3.8, 4.3.12 e 4.3.14. "Laudo histopatológico se houver" vira **Documento com `obrigatorio = false`**.
- Hematologia: acrescentar a OBSERVAÇÃO (Informativa literal + Pergunta Ressalva "Há suspeita de leucemia aguda?"), reescrever a pergunta de exclusão e limpar as 8 inativas-fragmento.
- Nas 4 tabelas condicionais (4.3.1, 4.3.7, 4.3.10, 4.3.12) e no melanoma da 4.3.14: Pergunta de LISTA "Local da lesão" com o requisito literal **dentro da opção** + Documento genérico + histopatológico não obrigatório. O que se perde: o sistema não exige o documento específico do local, não confere sexo nem limiar (PSA > 4), e o pedido do espelho fica "A conferir". **Não** recomendo transformar cada local num procedimento canônico próprio. O pedido do SER chega só com o recurso, então a análise do espelho não saberia qual escolher. E isso fere o catálogo plano do ADR-0055.

**2. Regra condicional (precisa de ADR, porque mexe no motor):** usar o `expressao_json`, hoje reservado, para que uma regra só valha quando uma condição for verdadeira. O inventário abaixo pede **dois tipos de condição**:

- `{"quando": {"regra": <id da pergunta pai>, "opcao": "<id da opção>"}}`: a regra filha vale quando aquela opção foi marcada. Para isso o pai precisa de **escolha única** (radio), não "basta uma". Hoje a lista é multi-marcação.
- `{"quando": {"cid_prefixo": ["C61"]}}`: a mesma filha vale quando o CID do pedido começa com o prefixo. Isso serve para o **pedido do espelho**, que não tem "local" mas tem CID. Os 31 recursos oncológicos usam a mesma lista de CID no SER, então o CID não restringe o recurso, mas identifica o sítio.
- Semântica sugerida: a filha só entra na soma E quando a condição vale. Se a condição depende de pergunta ainda não respondida, a filha fica pendente e não trava. Pai sem resposta continua travando como hoje. Uma condição não pode depender de outra filha (só um nível), o que mantém o catálogo plano.
- Com isso, as 18 linhas NAO_REPRESENTAVEL_HOJE ficam representáveis. Bônus: a cláusula "não-histerectomizadas → risco cirúrgico" (Braquiterapia) e a OBSERVAÇÃO da leucemia (filha das opções de suspeita de leucemia aguda) usam o mesmo mecanismo.

Ordem sugerida: (a) [G] em todos + as simples (só cadastro, com OK); (b) Hematologia; (c) listas "Local da lesão" provisórias na Urologia e na Cabeça e Pescoço, que respondem à reclamação; (d) ADR da condicional; (e) migrar as listas provisórias para pai + filhas.

## Inventário das tabelas por Local da lesão (literal)

Texto copiado do manual com a grafia original (inclusive "Citoscopia", "PARTES DE MOLES", "relacionadasao"). "CID sugerido" é meu, não literal. Cada bloco vira uma especificação: **pai** = pergunta de escolha única com os locais; **filhas** = os requisitos de cada local.

### T1 — 4.3.1 Cabeça e Pescoço (p.15) · recurso SER: "Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço - Exceto Tireóide (Oncologia)"

Forma: coluna "Local da lesão" com uma célula só ("CABEÇA E PESCOÇO"); os locais são bullets na coluna de requisitos. Cabeçalho comum: "Destina-se aos pacientes com DIAGNÓSTICO CONFIRMADO DE NEOPLASIA, com sítio primário em cabeça e pescoço."

| local | requisito literal | decomposição | CID sugerido |
|---|---|---|---|
| Fossa Nasal e Seios Paranasais | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultados dos exames pertinentes (anatomopatológico e exame de imagem - TC e/ou RNM de Crânio e face)." | AP **e** imagem (TC e/ou RNM crânio e face) | C30, C31 |
| Glândulas salivares | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultados dos exames pertinentes (anatomopatológico e exame de imagem – USG ou TC com contraste das glândulas salivares)." | AP **e** imagem (USG ou TC com contraste) | C07, C08 |
| Massa cervical | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultados do exame anatomopatológico e exame de imagem -TC de pescoço e tórax e face, evidenciando sítio primário em região de via aérea superior (rinofaringe, traqueia, laringe)." | AP **e** TC pescoço+tórax+face evidenciando sítio primário em VAS | C77.0, C76.0 |
| Lábio | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultados do exame anatomopatológico, confirmando o sítio primário." | AP confirmando sítio primário | C00 |
| Órbita | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultados dos exames pertinentes (anatomopatológico ou exame de imagem - TC e/ou RNM Crânio e face)." | AP **ou** imagem (TC e/ou RNM crânio e face) | C69.6 |
| Pele | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultados do exame anatomopatológico, confirmando o sítio primário." | AP confirmando sítio primário | C43, C44 (cabeça/pescoço) |
| Orelha | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultados do exame anatomopatológico, confirmando o sítio primário." | AP confirmando sítio primário | C44.2, C30.1 |
| Cavidade oral | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultados do exame anatomopatológico, confirmando o sítio primário." | AP confirmando sítio primário | C02–C06 |
| Faringe / laringe | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultados dos exames pertinentes, com indicativo do sítio primário em rinofaringe, traqueia, laringe (anatomopatológico e/ou exame de imagem - TC de pescoço e tórax e face ou laringoscopia)." | AP **e/ou** imagem (TC pescoço+tórax+face ou laringoscopia) | C09–C14, C32, C33 |

### T2 — 4.3.4 Neurocirurgia (p.16) · recursos SER: "Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia (Oncologia)" e "... - Tumores da Sela Túrcica (Oncologia)"

Cabeçalho: "Pacientes com suspeita de metástase em SNC deverão ser inseridos de acordo com o sítio primário."

| local | requisito literal |
|---|---|
| CÉREBRO/HIPÓFISE/SELA TÚRCICA | "Inserir no SER o encaminhamento médico indicando presença de Nódulo ou Massa em Sistema Nervoso Central (SNC) diagnosticado por Tomografia Computadorizada e/ou Ressonância Magnética de Crânio, sugestivo de sítio primário." |

Uma linha só no manual, dois recursos no SER. Não é condicional: as mesmas regras valem nos dois.

### T3 — 4.3.6 Cirurgia Geral (p.17) · recurso SER: "Ambulatório 1ª vez - Cirurgia Geral (Oncologia)"

| local | requisito literal |
|---|---|
| ESÔFAGO | "• Inserir no SER o encaminhamento médico com a descrição clínica do caso. • Inserir o resultado do exame de Endoscopia Digestiva Alta (EDA) e/ou Tomografia Computadorizada de Abdome e Tórax com lesão suspeita e laudo histopatológico." |
| ESTÔMAGO/DUODENO | (idêntico ao de ESÔFAGO) |

Locais idênticos. Não precisa de condicional.

### T4 — 4.3.7 Urologia (p.17) · recurso SER: "Ambulatório 1ª vez - Urologia (Oncologia)"

Forma: 5 linhas, cada uma com célula própria.

| local | requisito literal | decomposição | CID sugerido |
|---|---|---|---|
| RIM | "Inserir no SER o encaminhamento médico indicando forte suspeita clínica e resultados de exames de imagem com lesão sólida suspeita e Tomografia Computadorizada de Abdome. Laudo histopatológico se houver." | forte suspeita; imagem com lesão sólida suspeita **e** TC de abdome (obrig.); histopatológico **se houver** | C64, C65 |
| BEXIGA | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultado de exame de Citoscopia com biopsia positiva e/ou massa vesical ou Tomografia Computadorizada de Abdome e Pelve ou USG." | descrição clínica; basta um: cistoscopia com biópsia positiva / massa vesical em TC de abdome e pelve / massa vesical em USG | C67 |
| TESTÍCULO | "Inserir no SER o encaminhamento médico com descrição de massa testicular ao exame clínico com suspeita de neoplasia. Inserir USG de bolsa escrotal e/ou TC abdome e/ou pelve mostrando massa testicular com suspeita de neoplasia. Laudo histopatológico, se houver." | massa testicular ao exame clínico; USG de bolsa escrotal e/ou TC abdome e/ou pelve (obrig., ao menos um); histopatológico **se houver** | C62 |
| PRÓSTATA | "Inserir no SER o encaminhamento com a descrição clínica do caso e resultados dos exames de toque retal alterado e PSA > 4 ng/ml, com diagnóstico confirmado por biópsia." | descrição clínica; toque retal alterado **e** PSA > 4 ng/ml **e** biópsia confirmando (todos obrig.) | C61 |
| PÊNIS | "Inserir no SER o encaminhamento médico com descrição de lesão peniana ao exame clínico com suspeita de neoplasia. Laudo histopatológico, se houver." | lesão peniana ao exame clínico; histopatológico **se houver** | C60 |

### T5 — 4.3.8 Tórax (p.17) · recurso SER: "Ambulatório 1ª vez - Cirurgia Torácica (Oncologia)"

Cabeçalho: "Pacientes com suspeita de metástase pulmonar deverão ser inseridos de acordo com o sítio primário." Forma: 3 linhas de local com a coluna de requisitos em **célula mesclada** (vale para os três).

| locais | requisito literal (comum) |
|---|---|
| NÓDULOS TUMORAIS DO PULMÃO · TUMORES MEDIASTINO · TUMORES DA PLEURA | "• Inserir no SER o encaminhamento médico para o Serviço de Cirurgia Torácica, com indicação médica de forte suspeita clínica, informando o sítio primário. Laudo histopatológico se houver. • Todos os pacientes encaminhados deverão ter um exame Tomografia Computadorizada do tórax e abdome superior." |

Requisito igual para os três. Não precisa de condicional. CID sugerido: C34 / C37, C38.1–C38.3 / C38.4, C45.0.

### T6 — 4.3.9 Coloproctologia (p.18) · recurso SER: "Ambulatório 1ª vez - Coloproctologia (Oncologia)"

| local | requisito literal |
|---|---|
| CÓLON | "• Inserir no SER o encaminhamento médico com a descrição clínica do caso. • Inserir o resultado do exame de Colonoscopia e/ou Tomografia Computadorizada de Abdome Total e Pelve com contraste e/ou RNM de Pelve. • Inserir laudo histopatológico" |
| RETO | "• Inserir no SER o encaminhamento médico com a descrição clínica do caso. • Inserir o resultado do exame de Colonoscopia (ou Retossigmoidoscopia) e/ou Tomografia Computadorizada de Abdome Total e Pelve com contraste e/ou RNM de pelve. • Inserir laudo histopatológico" |

A única diferença: o reto aceita retossigmoidoscopia. Dá para unificar.

### T7 — 4.3.10 Ginecologia (p.18) · recurso SER: "Ambulatório 1ª vez - Ginecologia (Oncologia)"

| local | requisito literal | grupo | CID sugerido |
|---|---|---|---|
| VULVA | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e o Laudo Histopatológico." | A — histopatológico obrig. | C51 |
| VAGINA | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e o Laudo Histopatológico." | A | C52 |
| COLO UTERINO E ENDOMÉTRIO | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e o Laudo Histopatológico." | A | C53, C54 |
| TUMOR DE OVÁRIO | "Inserir no SER o encaminhamento médico indicando forte suspeita clínica e o exame de Imagem RNM, TC e/ou USG e Marcadores Tumorais (se possível)." | B — imagem obrig.; marcadores **se possível** | C56 |
| TROMPAS E ANEXOS | "Inserir no SER o encaminhamento médico indicando forte suspeita clínica e o exame de Imagem RNM, TC e/ou USG e Marcadores Tumorais (se possível)." | B | C57 |

### T8 — 4.3.11 Ginecologia, Mola (p.18) · recurso SER: "Ambulatório 1ª vez em Ginecologia - Doença Trofoblastica Gestacional (Mola Hidatiforme)"

| lesão | requisito literal |
|---|---|
| Doença Trofoblástica Gestacional (Mola Hidatiforme) | "Inserir no SER o encaminhamento médico com descrição detalhada do caso e resultado de BHCG plasmático, exame de USG pélvica ou transvaginal e /ou histopatológico indicando o diagnóstico." |

Linha única, não condicional.

### T9 — 4.3.12 Cirurgia Hepatobiliar (p.19) · recurso SER: "Ambulatório 1ª vez - Cirurgia Hepatobiliar (Oncologia)"

Cabeçalho: "Pacientes com suspeita de metástase hepática deverão ser inseridos de acordo com o sítio primário."

| local | requisito literal | decomposição | CID sugerido |
|---|---|---|---|
| FÍGADO E VIAS BILIARES | "Inserir no SER o encaminhamento médico indicando forte suspeita clínica de tumor primário e resultado de exame de TC abdome / RNM abdome / Colangiopancreatografia Retrógrada Endoscópica (CPRE) e Marcadores Tumorais (se possível)." | um de TC / RNM / CPRE (obrig.); marcadores **se possível** | C22, C23, C24 |
| PÂNCREAS | "Inserir no SER o encaminhamento médico indicando forte suspeita clínica e resultado do exame de Tomografia Computadorizada de abdome com massa sólida ou mista ou cística." | **só** TC de abdome com massa sólida, mista ou cística | C25 |

### T10 — 4.3.13 Mastologia (p.19) · cada local tem recurso próprio no SER

| local/tipo da lesão | requisito literal | recurso SER |
|---|---|---|
| MAMA COM LESÃO PALPÁVEL | "• Inserir no SER o encaminhamento médico com a descrição clínica do caso e anexar o laudo Histopatológico. • Informar estágio clínico da doença." | "Ambulatório 1ª vez - Mastologia (Oncologia)" (inferência: o genérico é o da palpável) |
| MAMA COM LESÃO IMPALPÁVEL | "• Inserir no SER o encaminhamento médico indicando forte suspeita clínica e anexar os exames de USG e Mamografia com categoria 4 e 5 de BI - RADS." | "Ambulatório 1ª vez em Mastologia - Lesão Impalpável (Oncologia)" |

O SER já resolve a condição: não precisa de condicional.

### T11 — 4.3.14 Tumor de Tecido Ósseo-Conectivo (TOC) ADULTO / INFANTIL (p.19) · recursos SER: "Ambulatório 1ª Vez - Tumores do Tecido Ósseo e Conectivo (Adulto)" e "(Infantil)"

Cabeçalho: "Pacientes com suspeita de metástase óssea deverão ser inseridos de acordo com o sítio primário." Remissão na p.25 (4.4 Ortopedia): "Pacientes com diagnóstico de TUMOR ÓSSEO deverão ser encaminhados à fila de Ambulatório de 1ªvez – Tumores do Tecido Ósseo-Conectivo."

| local | requisito literal | decomposição | CID sugerido |
|---|---|---|---|
| CÂNCER ÓSSEO | "Inserir no SER o encaminhamento médico indicando forte suspeita clínica e anexar o laudo histopatológico e/ou exame de imagem." | histopatológico **e/ou** imagem | C40, C41 |
| SARCOMA DE PERNAS E BRAÇOS | "Inserir no SER o encaminhamento médico indicando forte suspeita clínica e anexar o laudo histopatológico e/ou exame de imagem." | histopatológico **e/ou** imagem | C40, C49 |
| TUMORES MALIGNOS DE PARTES DE MOLES | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e anexar o laudo histopatológico e/ou exame de imagem." | histopatológico **e/ou** imagem | C49 |
| MELANOMA >2 CM | "Inserir no SER o encaminhamento médico com a descrição clínica do caso e anexar o laudo histopatológico." | **histopatológico obrigatório** | C43 |

### T12 — 4.3.19 Medicina Nuclear (p.24) · coluna "Recurso"; cada linha é um recurso do SER

| recurso (manual) | requisito literal (leitura VISUAL) | recurso SER |
|---|---|---|
| PLANEJAMENTO EM RADIOTERAPIA | "Inserir no SER o encaminhamento médico descrevendo de forma clara e detalhada, indicando o sítio a ser irradiado e apresentando o exame histopatológico. Também, informar o peso do paciente." | "Ambulatório 1ª vez - Planejamento em Radioterapia" |
| PLANEJAMENTO EM RADIOTERAPIA INFANTIL | "Inserir no SER o encaminhamento médico descrevendo de forma clara e detalhada, indicando o sítio a ser irradiado e apresentando o exame histopatológico." | "Ambulatório 1ª vez - Planejamento em Radioterapia (Infantil)" |
| PLANEJAMENTO EM BRAQUITERAPIA | "Inserir no SER o encaminhamento médico descrevendo de forma clara e detalhada, indicando o sítio a ser irradiado e apresentando o exame histopatológico. Pacientes não-histerectomizadas deverão anexar risco cirúrgico." | "Ambulatório 1ª vez - Planejamento em Braquiterapia" — **cláusula condicional** (histerectomia) |
| PLANEJAMENTO EM GAMMA KNIFE | "Inserir no SER o encaminhamento médico descrevendo de forma clara e detalhada, indicando o procedimento e apresentando exame de imagem há menos de 06 meses." | "Ambulatório 1ª vez - Radiocirurgia Gamma Knife" |
| PLANEJAMENTO EM IODOTERAPIA | "Inserir no SER o encaminhamento médico descrevendo de forma clara e detalhada, indicando o procedimento." | "Ambulatório 1ª vez - Planejamento em Iodoterapia" |

### Tabelas sem "Local" que completam a especificação (literal)

- **4.3.2 TIREOIDE (p.16):** "Inserir no SER o encaminhamento médico com a descrição clínica do caso e resultados do exame anatomopatológico ou citopatológico (PAAF), confirmando o sítio primário."
- **4.3.3 OFTALMOLOGIA (p.16):** "• Destina-se aos pacientes com lesão suspeita em conjuntiva, córnea, íris, retina e/ou coroide. • Inserir no SER o encaminhamento médico indicando a forte suspeita clínica."
- **4.3.5 PELE (p.16):** "Inserir no SER o encaminhamento médico indicando a forte suspeita clínica e resultado do exame histopatológico, confirmando o sítio primário."
- **4.3.16 AVALIAÇÃO CARDIOLÓGICA PARA PACIENTES EM TRATAMENTO ONCOLÓGICO (p.23):** "Indicação: • Pacientes Oncológicos apresentando complicações cardiológicas relacionadasao tratamento radioterápico ou quimioterápico. • Pacientes com doença cardíaca necessitando de avaliação pré-quimioterapia. • Pacientes com neoplasia primária do coração. • Inserir no SER o encaminhamento médico com descrição do caso."
- **4.3.18 TRIAGEM EM ONCOLOGIA PEDIÁTRICA (p.24):** "• Inserir no SER o encaminhamento médico com descrição detalhada do caso suspeito de câncer infantil."

### 4.3.15 HEMATOLOGIA — texto literal (p.20–22)

**Oncologia – HEMATOLOGIA ADULTO (p.20–21).** Critérios de Exclusão: a. Adenomegalias reacionais a viroses agudas. b. Atipias linfocitárias em presença de viroses agudas. c. Trombocitose em hemograma único. d. Pacientes com adenomegalias suspeitas sem biópsia.

Critérios de Inclusão/ Agendamento:

- a. Dor óssea, com eletroforese de proteínas séricas, evidenciando Gamopatia Monoclonal (casos de Mieloma múltiplo).
- b. Bicitopenia após afastar todas as causas clínicas (hipotireoidismo, doenças hepáticas, uso de medicamentos, esplenomegalia com doença hepática).
- c. Citopenias + manifestações clínicas suspeitas de leucemia aguda (fadiga generalizada, fraqueza, palidez, equimose, petéquias, sangramentos, infecções recorrentes) – VIDE OBSERVAÇÃO.
  - c.1. Citopenias + linfonodomegalia e esplenomegalia não explicada por quadroinfeccioso agudo; ou
  - c.2. Presença de blastos/ promielócitos no sangue periférico; ou
  - c.3. Paciente com febre + neurogenia (< 1500 /μL); ou
  - c.4. Bicitopenia/pancitopenia com alterações hematológicas graves: • Hemoglobina < 7 g/dl; e/ou • Neutrófilos < 500 cels/μL; e/ou • Plaquetas < 50 mil cels/mm³.
- d. Trombocitose:
  - d.1. Isolada > 600.000/mm³ em 3 exames c/ intervalo <1m; ou
  - d.2. Acompanhada de leucocitose e aumento do hematócrito; ou
  - d.3. Associada a sintomas vasomotores, sangramento ou trombose (após avaliação em serviço de urgência / emergência); ou
  - d.4. Associada a leucocitose ou policitemia.
  - Excluir: trombocitose persistente após exclusão de causas secundárias (quadro infeccioso atual, anemia ferropriva, esplenectomia /asplenia, trauma/cirurgia recente).
- e. Trombocitose (p.21): e.1–e.4 e "Excluir" com o mesmo texto de d.
- f. Leucocitose com desvio à esquerda em ausência de infecção:
  - f.1. Leucocitose + manifestações clínicas suspeitas de leucemia aguda (fadiga generalizada, fraqueza, palidez, equimose, petéquias, sangramentos, infecçõesrecorrentes); ou
  - f.2. Presença de blastos e promielócitos no sangue periférico; ou
  - f.3. Leucostase (presença de sintomas respiratórios, neurológicos, priapismo empessoas com hiperleucocitose) ou leucócitos > 100 mil cels. /mm³.
- g. Policitemia:
  - g.1. Suspeita de Policitemia vera (hb > 16,0g/dl em mulheres e > 16,5g/dl em homens), em pessoas com sintomas sugestivos: prurido após o banho, gota,trombose venosa ou arterial prévia, sangramento, esplenomegalia; ou
  - g.2. Policitemia persistente (hb > 16,0g/dl em mulheres e > 16,5g/ dl em homens) após repetição do hemograma em 1 mês e exclusão de causas secundárias (DPOC, tabagismo, hepatocarcinoma, carcinoma renal).

OBSERVAÇÃO: PACIENTES COM SUSPEITA DE LEUCEMIA AGUDA NÃO SÃO REGULADOS AMBULATORIALMENTE. NECESSÁRIO ESTAR INTERNADO E INSERIDO NO SISTEMA SER PARA TRANSFERÊNCIA HOSPITALAR.

**Oncologia – HEMATOLOGIA PEDIÁTRICA (p.22).** Critérios de Exclusão: os mesmos a–d do adulto.

Critérios de Inclusão/ Agendamento:

- a. Bicitopenia após afastar todas as causas clínicas (hipotireoidismo, doenças hepáticas, uso de medicamentos,esplenomegalia com doença hepática); ou
- b. Citopenias com manifestações clínicas suspeitas de leucemia aguda (fadiga generalizada, fraqueza, palidez, equimose, petéquias, sangramentos, infecções recorrentes); ou
  - b.1. Citopenias com linfonodomegalia e esplenomegalia não explicada por quadroinfeccioso agudo; ou
  - b.2. Presença de blastos/ promielócitos no sangue periférico; ou
  - b.3. Paciente com febre + neutropenia (< 1500 /μL); ou
  - b.4. Bicitopenia/pancitopenia com alterações hematológicas graves: • Hemoglobina < 7 g/dl; e/ou • Neutrófilos < 500 cels/μL; e/ou • Plaquetas < 50 mil cels/mm³.
- c. Trombocitose:
  - c.1. Isolada > 600.000/mm³ em 3 exames c/ intervalo <1m; ou
  - c.2. Acompanhada de leucocitose e aumento do hematócrito.
  - c.3. Associada a sintomas vasomotores, sangramento ou trombose (após avaliaçãoem serviço de urgência / emergência); ou
  - c.4. Associada a leucocitose ou policitemia;
  - Excluir: trombocitose persistente após exclusão de causas secundárias (quadro infeccioso atual, anemia ferropriva, esplenectomia /asplenia, trauma/cirurgia recente).
- d. Leucocitose com desvio à esquerda em ausência de infecção:
  - d.1. Leucocitose + manifestações clínicas suspeitas de leucemia aguda (fadiga generalizada, fraqueza, palidez, equimose, petéquias, sangramentos, infecções recorrentes); ou
  - d.2. Presença de blastos e promielócitos no sangue periférico.

OBSERVAÇÃO: o mesmo texto do adulto.

### 4.3.17 NEUROLOGIA — texto literal (p.23) · sem recurso no SER

Recurso: AVALIAÇÃO EM ONCOLOGIA (NEUROLOGIA).

Critérios de inclusão:

- Idade: acima de 18 anos.
- Complicações neurológicas de Quimioterapia, Imunoterapia e Radioterapia para Doenças Oncológicas, tais como mielopatias, plexopatias, radiculopatias, miopatias, doenças do neurônio motor, doenças da junção neuromuscular, distúrbios de movimento, demências e prejuízos na cognição, epilepsia e encefalopatias decorrentes dos tratamentos.
- Complicações neurológicas de Transplante de medula óssea para Doenças Hemato-Oncológicas, tais como mielopatias, plexopatias, radiculopatias, miopatias, doenças do neurônio motor, doenças da junção neuromuscular, distúrbios de movimento, demências e prejuízosda cognição, epilepsia e encefalopatias decorrentes do tratamento.
- Suspeita de Encefalite Paraneoplásica, na presença de neoplasia já identificada.

Critério de exclusão:

- Tratamento Primário de Doenças Oncológicas, Hemato-Oncológicas, Neoplasias do Sistema Nervoso Central ou Periférico. O ambulatório atende apenas complicações do tratamento destas doenças e não as doenças propriamente ditas.
- Encaminhamentos para Neuro-oncologia Cirúrgica. Os atendimentos do ambulatório são apenas clínicos por neurologista.
- Encaminhamento para Cuidados Paliativos. Não é o foco de atendimento deste ambulatório e já existem serviços na rede com este perfil.
- Encaminhamento para tratamento de Doenças Infecciosas, Dores, Cefaleias, Transtornos emocionais ou psiquiátricos, mesmo que decorrente de tratamentos oncológicos. Não são foco deste ambulatório e já existem serviços na rede com este perfil.
- Pacientes graves e instáveis, por se tratar de atendimento ambulatorial.
- Indivíduos com menos de 18 anos.

