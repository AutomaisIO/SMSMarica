# Confronto dos manuais do SER (CRECE + REUNI) com as regras de produção — 09/10/2026

**Origem:** reclamação sobre a Urologia (Oncologia). O manual REUNI (4.3.7, p.17) define o requisito
**por "Local da lesão"** (rim, bexiga, testículo, próstata, pênis), e o nosso cadastro não tem nada disso.
O pedido foi repassar os manuais inteiros contra o que está implementado para o SER.

**Como foi feito:** exportei de produção, só leitura e sem dado de paciente, as 619 regras, os 423
recursos do catálogo do SER com os campos do formulário e as contagens da análise automática. Sete
agentes leram os dois PDFs página a página, conferindo as tabelas na imagem, porque o texto extraído
mistura colunas e células mescladas. Os achados mais graves foram reconferidos no export. O detalhe de
cada recurso (requisitos literais, regra por regra, proposta de cadastro) está em
[`2026-10-09-confronto-manuais-ser/`](./2026-10-09-confronto-manuais-ser/).

## 1. Números

| | |
|---|---|
| Linhas de recurso nos dois manuais (cada "local"/indicação conta como linha) | ≈ 315 (CRECE 146, REUNI 171) |
| **Cobertas** como o manual escreve | **38 (12%)** |
| Parciais | 19 |
| **Ausentes** (o SER tem o recurso, e nós não temos regra nenhuma) | **156** |
| **Erradas** (há regra ativa que cobra o que não devia ou barra quem não devia) | **22** |
| **Não representáveis hoje** (o requisito depende de algo escolhido: local, indicação) | **46** |
| Sem recurso correspondente no catálogo do SER | 29 |
| Só orientação (preparo, glossário) | 7 |

E do lado da produção:

- **62 dos 422 recursos do SER** têm alguma regra ativa (AE 42/214, não-AE 20/209).
- **232 regras ativas, todas com severidade "Bloqueia".** Ressalva e Aviso nunca foram usadas.
- **27 das 232 ativas não valem para ninguém** (ver §3).
- Análise automática dos pedidos espelhados: **3.353 "Sem regras"** (SER 2.120, ESUS SG 666,
  SERNIT 567), **640 "A conferir"**, 89 sem procedimento. **Zero "Apto" e zero "Bloqueado".** Qualquer
  regra do tipo pergunta já deixa o pedido em "A conferir", porque o espelho não tem como respondê-la.

## 2. A reclamação: Urologia (Oncologia) e as tabelas por "Local da lesão"

**Situação em produção:** "Ambulatório 1ª vez - Urologia (Oncologia)" tem **zero regras**, nem a do
encaminhamento. São 5 pedidos do espelho em "Sem regras" e **3 solicitações nossas**, a 2ª consulta
mais pedida do módulo, que passaram sem conferência nenhuma. Das ~31 consultas de Oncologia do SER,
**só as duas de Hematologia têm regra**. As outras 29 dão 153 pedidos "Sem regras", entre eles
Oncologia Geral com 44 e a Cirurgia Torácica do ramo AE com 50.

**Por que ficou assim:** a extração de 07/09 leu cada linha da tabela "Local da lesão" como se fosse
um recurso ("ONCOLOGIA - PRÓSTATA", "ONCOLOGIA - NÓDULOS TUMORAIS PULMÃO…"). Nenhuma casou com o
catálogo, e todas foram descartadas. Da Urologia só a próstata chegou a ser extraída; rim, bexiga,
testículo e pênis se perderam. A tabela do Tórax engoliu a de Cólon/Reto, e doze subseções não
geraram linha nenhuma.

**O formulário do SER não tem campo "local da lesão".** Os 31 recursos oncológicos usam os mesmos 9
campos: peso, altura, IMC, datas da biópsia, "já fez cirurgia oncológica?", queixa, resultado de
exames e observações. O pedido que chega pelo espelho traz só o CID e texto livre.

**4.3.7 Urologia, literal do manual (p.17):**

| Local | Requisitos necessários |
|---|---|
| RIM | Encaminhamento indicando forte suspeita clínica e resultados de exames de imagem com lesão sólida suspeita e TC de Abdome. Laudo histopatológico **se houver**. |
| BEXIGA | Encaminhamento com descrição clínica e resultado de Citoscopia com biópsia positiva **e/ou** massa vesical **ou** TC de Abdome e Pelve **ou** USG. |
| TESTÍCULO | Encaminhamento com massa testicular ao exame clínico com suspeita de neoplasia. USG de bolsa escrotal **e/ou** TC abdome e/ou pelve com massa suspeita. Histopatológico **se houver**. |
| PRÓSTATA | Encaminhamento com descrição clínica e **toque retal alterado e PSA > 4 ng/ml, com diagnóstico confirmado por biópsia**. |
| PÊNIS | Encaminhamento com lesão peniana ao exame clínico com suspeita de neoplasia. Histopatológico **se houver**. |

**Onde mais o requisito muda conforme algo escolhido** (as 46 linhas não representáveis):

| Manual / seção | Eixo | Ramos |
|---|---|---|
| REUNI 4.3.1 Onco Cabeça e Pescoço | local da lesão | 9 (fossa nasal/seios, salivares, massa cervical, lábio, órbita, pele, orelha, cavidade oral, faringe/laringe) |
| REUNI 4.3.7 Onco Urologia | local da lesão | 5 |
| REUNI 4.3.10 Onco Ginecologia | local da lesão | 5 (vulva, vagina e colo/endométrio exigem histopatológico; ovário e trompas exigem imagem) |
| REUNI 4.3.12 Onco Hepatobiliar | local da lesão | fígado/vias biliares × pâncreas |
| REUNI 4.3.14 Onco Tecido Ósseo-Conectivo | local | só o melanoma > 2 cm difere |
| REUNI 4.4.1–4.4.5 Ortopedia (joelho, mão, coluna, quadril, ombro/cotovelo) | indicação | 3 a 5 por fila, cada uma com exame obrigatório próprio |
| REUNI 4.8.1 / 4.8.2 Hematologia adulto e pediátrica | tipo de alteração | anemia × leucopenia × hemorrágico |
| CRECE 4.1.27 Urologia Geral | condição | PSA+USG, imagem < 40 dias, exame contrastado, urocultura |

A Coluna Adulto (401 pedidos) e o Joelho Adulto (104) estão nessa lista e são as maiores filas "Sem
regras" do SER.

Também são condicionais só na forma, com o mesmo requisito para todos os locais: Tórax (4.3.8, célula
mesclada), Cirurgia Geral (4.3.6) e Coloproctologia (4.3.9). Esses cabem hoje.

## 3. Regras que não valem para ninguém (gêmeos do catálogo)

São 10 procedimentos e 27 regras ativas. As regras curadas ficaram num procedimento canônico cuja única
origem viva é o SISREG. O recurso do SER foi ligado, com vínculo confirmado, a **outro** canônico de mesmo
nome, sem regra. Como essas regras têm filtro `sistema = SER`, não disparam no SISREG nem são alcançadas
pelo pedido do SER. A causa é o reparo posicional de 01/10, que devolveu as origens aos canônicos sem
levar as regras junto. Os nomes "DE DOR"/"DA DOR" cruzados entre SISREG e SER pioraram o pareamento.

| Canônico com as regras (só SISREG) | Regras | Efeito no SER |
|---|---|---|
| CONSULTA EM ANGIOLOGIA | 3 | Sem regras |
| CONSULTA EM CARDIOLOGIA - PEDIATRIA | 6 | Sem regras ("Eco se houver" está como obrigatório: corrigir ao mover) |
| CONSULTA EM CIRURGIA PEDIATRICA | 3 | inclui uma regra **errada**, da Otorrino (ver §4) |
| CONSULTA EM CLINICA MEDICA - CLINICA DE DOR | 3 | Sem regras |
| CONSULTA EM GASTROENTEROLOGIA | 2 | Sem regras (a lista ainda leva 3 opções da DII) |
| CONSULTA EM MASTOLOGIA | 2 | 18 pedidos Sem regras |
| CONSULTA EM NEFROLOGIA - GERAL | 1 | canônico sem origem nenhuma (o valor 1118 do SER foi reaproveitado de NEUROLOGIA/AVC) |
| CONSULTA EM ODONTOLOGIA - ENDODONTIA | 3 | Sem regras |
| CONSULTA EM ODONTOLOGIA - ESTOMATOLOGIA | 2 | Sem regras |
| CONSULTA EM OTORRINOLARINGOLOGIA | 2 | Sem regras (existem dois canônicos com esse nome) |

As correções de 01/10 em Nefrologia, Endodontia e Estomatologia caíram justamente nesses canônicos.

## 4. Regras ativas erradas (desligar ou corrigir: é curadoria, não código)

| Procedimento | Regra | O que está errado | Efeito hoje |
|---|---|---|---|
| Hematologia (Adulto) | `cc8f8ccf` | "Teste do Pezinho…" (Documento/Bloqueia) é da tabela **pediátrica** (p.35); a extração juntou as p.33 e 35 | todo pedido adulto cobra triagem neonatal |
| Hematologia (Adulto) | as 10 ativas | os exames dos 3 ramos (anemia, leucopenia, hemorrágico) estão somados com E, todos obrigatórios | leucopenia cobra 7 caixas a mais; condicional (§2) |
| Hematologia (Infantil) | — | **nenhuma regra**; o conteúdo dela foi para o adulto | Sem regras |
| CONSULTA EM NEUROLOGIA (geral) | `d9ad21dd` | lista de 19 condições juntando "Distúrbios do Movimento" e "Neurônio Motor", com "Nenhuma" bloqueando; para a geral o manual pede **só o encaminhamento** | barra neuropatia, vertigem, sequela de AVC…; 6 "A conferir" |
| CONSULTA EM DERMATOLOGIA (geral) | `794da64f`, `180c4302`, `f6ef4e99` + opções em `c8be1629` | exclusões e opções da **Hanseníase** e dos **Hemangiomas** (p.17) no recurso geral | barra psoríase ou vitiligo estável |
| Estudo Eletrofisiológico / Ablação | `9c34827a`, `e8423576`, `4d5af645` | são do **Ecocardiograma** (p.12); `9c34827a` ainda é um caco ("…Transtorácico. ECOCARDIOGRAMA DE ESTRESSE") | 12 "A conferir"; os requisitos do próprio recurso faltam |
| Cirurgia Bariátrica (Adulto) | `1c6bf33e` | critérios a–d num Sim/Não só, **cortado em 500 caracteres** | 107 "A conferir" |
| Cirurgia Bariátrica (Adulto) | `5c339564` | começa com o caco "acima."; a idade 16–18 está preenchida, mas o avaliador **ignora idade em Documento** | caixinha das epífises aparece para todos |
| Pré-Natal Alto Risco | `4afd33b1` | laudo de USG **gemelar** obrigatório para toda gestante | — |
| Epilepsia Refratária (Infantil) | `0823e1f3` | "EEG e RNM (**se possível**)" virou obrigatório que bloqueia | — |
| CONSULTA EM CIRURGIA PEDIATRICA | `df97d1c5` | "indicação para cirurgia otorrinolaringológica" é da **Otorrino** (p.40); a seção 4.2.2 existe (p.52) e não fala disso | inerte hoje (gêmeo); barra se o canônico voltar a receber pedidos |
| Cardiopatia Congênita Pediátrica | `9d289a2b` | "menores de 18" cadastrado com máximo 18 | deixa passar quem tem 18 |
| Odontologia Endodontia / Estomatologia | documento | diz "encaminhamento **médico**"; o manual pede o do **Cirurgião-Dentista** + exames radiográficos | — |
| Alergologia Pediatria | 5 exclusões | barram opções da própria lista de inclusão; **a contradição está no manual** | 34 "A conferir"; decisão da regulação (pendente desde 01/10) |

## 5. Ausências de maior volume

Pedidos do SER hoje em "Sem regras" **cujo manual tem requisito**:

| Pedidos | Recurso | Observação |
|---|---|---|
| 401 | Patologia Cirúrgica da Coluna Vertebral (Adulto) | condicional por indicação (§2) |
| 161 | CONSULTA EM GASTROENTEROLOGIA - PEDIATRIA | 19 critérios estão inativos no genérico; recadastrar no recurso próprio (pendente de 01/10) |
| 153 | Oncologia (29 recursos) | §2 |
| 104 | Ortopedia Joelho (Adulto) | condicional |
| ≈155 | EEG adulto/infantil, Cintilografia do miocárdio, Elastografia, PET-CT… (REUNI p.38–44) | nada desta parte do manual está em produção; a Elastografia esconde no "preparo" barreiras reais (≥ 18 anos, só doença hepática crônica, gravidez…) |
| 38 + 27 | Readequação pós-bariátrica; Bariátrica Superobesidade | |
| 19 / 13 / 5 | Dermatologia Infantil; Alergologia; Biópsia de pele | linhas perdidas na extração ou pelo pareamento por nome ("DIABETE" × "DIABETES", "HITEROSCOPIA") |

Na **Polissonografia**, a consulta mais pedida do nosso módulo, a regra está parcial: há 268 "A
conferir" e faltam as duas exclusões da p.42. Uma delas, **menores de 18 anos**, o sistema decide
sozinho pelo cadastro.

Recursos com muito volume que **os manuais não mencionam** e, portanto, não têm de onde tirar regra:
Campimetria (250), RM de Mama (105), RM com sedação (68), RM Cardíaca (43), Capsulotomia YAG (18).
Esses dependem de a regulação definir o critério.

Fora isso, o **encaminhamento global** só foi replicado onde a importação já tinha deixado alguma
regra. Por isso quase toda ausência não tem nem ele.

## 6. O que o modelo de regras não deixa fazer

1. **Regra condicional.** Não existe "se o local for Próstata, exija PSA e biópsia". Todas as regras
   ativas se somam com E, e `expressao_json` está reservado e sempre nulo. Isso responde pelas 46
   linhas do §2 e pela Hematologia adulto somada.
2. **Regra não distingue o ramo AE/NAO_AE.** O avaliador não usa `procedimento_origem_id`. Hoje
   nenhum canônico tem origem nos dois ramos, mas o risco é real se o pareamento unir os dois.
3. **Não há regra global.** O encaminhamento existe como 1 regra por procedimento, e os dados
   cadastrais obrigatórios (1.2) não têm onde morar.
4. **Idade só em anos inteiros.** Não cabem "após o 2º mês de vida" (Teste do suor) nem "até 3 meses"
   (Frenectomia).
5. **Idade em regra do tipo Documento é ignorada** pelo avaliador.
6. **`validade_dias` e `tipo_exame_id` nunca foram usados.** Todo "< 6 meses", "< 40 dias" e "< 1 ano"
   existe só no texto.
7. **Regra Informativa não conta na análise automática.** Recurso só com informativa aparece como
   "Sem regras".

## 7. Contradições do próprio manual (a regulação decide)

- **Alergologia Pediatria:** as exclusões negam inclusões.
- **Toxina Botulínica:** o texto é cópia do da Gineco Cirurgia.
- **CIDs da DII** (K51.1; K50.8 como "colite indeterminada"): não batem com a CID-10.
- **Tendinites** vão para o ambulatório local, mas a Mão aceita tendinopatias.
- **Tumor ósseo** tem fila própria, mas a Coluna tem a linha "TUMOR".
- **Cardiologia adulto:** "risco cirúrgico" e "insuficiência cardíaca" aparecem como inclusão e como
  exclusão.
- **Psiquiatria** exclui "doenças psiquiátricas descompensadas".
- **Fisioterapia uroginecológica:** "somente adultos do sexo masculino".
- **Homeopatia:** o SER diz "(MAIOR DE 60 ANOS)" e o manual não fala de idade.
- **Coinfecção:** no SER é "HIV/HEPATITE VIRAL", e o manual exclui Hepatite B.

## 8. Proposta

**Etapa 1: curadoria, sem código.** Cada gravação em produção precisa de OK.

1. ✅ **Feito em 09/10/2026 (lote 1).** Desligar ou corrigir as regras do §4, criando nova versão,
   nunca apagando. Ficaram de fora a Alergologia Pediatria e os exames somados da Hematologia adulto,
   que dependem da regulação ou da regra condicional.
2. ✅ **Feito em 09/10/2026 (lote 2).** Levar as regras do §3 para o canônico que o SER alcança
   (26 movidas; a da Otorrino foi desligada no lote 1), corrigindo no caminho o "Eco se houver", as
   opções da DII na Gastro e o documento da Odontologia. Resultado: 45/45 chamadas, ativas
   232 → 222, recursos do SER com regra 62 → 72. Plano executado em
   [`2026-10-09-confronto-manuais-ser/plano_lotes12.md`](./2026-10-09-confronto-manuais-ser/plano_lotes12.md);
   registro em `PROGRESSO.md` ("OKs de produção").
3. ✅ **Feito em 09/10/2026 (lote 3).** Pôr a regra do encaminhamento em todo recurso do SER que esteja sem ela
   (337 canônicos; os 422 recursos do SER passam a ter regra).
4. ✅ **Feito em 09/10/2026 (lote 3), na parte listada abaixo.** Cadastrar as subseções **simples**, por ordem de volume: Polissonografia (< 18 dedutível),
   Gastro Pediatria, Oncologia de requisito único (Tórax, Cólon/Reto, Cirurgia Geral, Tireoide,
   Oftalmo, Neuro, Pele, Mastologia), EEG, Cintilografia, Elastografia.
5. ✅ **Feito em 09/10/2026 (lote 3).** **Urologia (Oncologia), provisório:** uma pergunta de lista "Local da lesão", com o requisito literal
   de cada local dentro da opção e "nenhuma destas" bloqueando, mais um documento genérico "Exame(s)
   exigido(s) para o local marcado" e o histopatológico como documento não obrigatório. O local marcado
   fica gravado e o regulador vê. **O que se perde:** a caixinha específica por local (PSA e biópsia só
   para próstata), e o PSA > 4 vira declaração.

**Etapa 2: regra condicional (código + ADR).** Uma pergunta de **escolha única** (local, indicação,
tipo de alteração) e regras filhas que só entram na soma quando a opção correspondente foi marcada.
Um nível só. Isso encaixa no ciclo atual do assistente (responder → reavaliar → aparecem as caixinhas
do local) e cobre as 46 linhas do §2. Para o pedido que chega do espelho, que não traz o local, a
pergunta fica pendente ("A conferir"). Opcionalmente, o CID do pedido sugere o local (C61 → Próstata),
mas como **sugestão**: essa correspondência não está no manual. Junto, corrigir os itens 5 e 7 do §6 e
decidir sobre regra global e idade em meses.

**Não fazer:** reimportar o `spike-e-manual-regras.csv`. Ele traz armadilhas que nunca chegaram à
produção porque ficaram sem par:

- "lesão ligamentar a partir de 50 anos" virou idade mínima 50 e barraria todo o Joelho;
- idade mínima 18 no preparo do Teste do Suor;
- as indicações do PET-CT como anexos obrigatórios;
- o Transplante Renal adulto perdido.
