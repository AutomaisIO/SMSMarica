# Confronto CRECE p.36–54 (trecho C) × regras de elegibilidade em produção

Gerado em 09/10/2026, só leitura. Manual: *CRECE — Manual do Solicitante, versão 1 (30/11/2022)*, ramo **AE** do SER. Produção: exportação de hoje (`regras.json`, `ser_recursos.json`, `analise_espelho_por_procedimento.json`, `solicitacoes_por_procedimento.json`). Toda tabela foi conferida na imagem da página (PyMuPDF), não só no texto extraído.

## Resumo

- **68 linhas de tabela** confrontadas: 66 das seções 4.1.18–4.2.6 + 2 de fronteira (4.1.17 Infectologia, linhas PEDIATRIA e COINFECÇÃO, que estão na p.36 mas a seção começa na p.35 — podem aparecer também no trecho B).
- **Vereditos (68 / 66 sem a fronteira):** COBERTO 10 / 10 · PARCIAL 2 / 2 · ERRADO 5 / 5 · AUSENTE 43 / 41 · NAO_REPRESENTAVEL_HOJE 1 / 1 · SEM_RECURSO_NO_SER 7 / 7.
- **O trecho quase não tem regra.** Só 17 canônicos do trecho têm alguma regra; 10 linhas estão cobertas. As outras 41 linhas com recurso no SER não têm regra nenhuma — nem o encaminhamento. Três causas no spike: linha SEM_PAR (rótulo do manual ≠ rótulo do SER: “DIGESTÓRIO”×“DIGESTIVO”, “TIREOIDE”×“TIREOIDES”, “ORTOPEDIA GERAL”×“ORTOPEDIA (NÃO CIRURGICO)”…); linha nem extraída (layout em colunas empilhadas: Nutrição, Reumato Adolescente, Urologia Geral, Cirurgia Plástica…); ou linha pareada mas só com frases “observar o prestador” (Oftalmologia, Pneumologia) — e, pelo que os dados mostram, o encaminhamento “requisito global” só foi posto em canônico que já tinha alguma outra regra.
- **Regra ativa errada:** “Pacientes com indicação para cirurgia otorrinolaringológica” (CRECE p.40, Otorrino) está ATIVA e Bloqueia em **CONSULTA EM CIRURGIA PEDIATRICA**. A seção 4.2.2 Cirurgia Pediátrica **existe, na p.52** (a revisão de 01/10 não a achou) e não fala de otorrino. Hoje a regra está latente (o canônico não recebe pedido do SER), mas está ativa.
- **Regras órfãs (5 linhas):** Nefrologia Geral, Otorrino Geral, Endodontia, Estomatologia e Cirurgia Pediátrica têm regras com conteúdo do manual, mas presas a um canônico que NÃO recebe o pedido do SER: o recurso AE foi confirmado para o canônico com nome do SISREG, e as regras ficaram no canônico com nome do SER — que hoje só tem a origem SISREG (nomes cruzados) ou nenhuma origem. Como as regras são `sistema=SER`, não valem para ninguém.
- **Polissonografia (o mais pedido):** PARCIAL. Faltam as duas exclusões do manual (p.42): “Menores de 18 anos” — dedutível, a única regra do trecho que a análise do espelho decidiria sozinha (268 pedidos do SER hoje em “A conferir”) — e “Doenças psiquiátricas descompensada”. A inclusão é uma Sim/Não de texto corrido, não lista. O spike tem a idade invertida (`idade_max=18`).
- **Condicional / não representável hoje:** só **Urologia GERAL** (o exame que tem de vir anexado depende da condição marcada: PSA+USG, imagem ≤40 dias, exame contrastado, urocultura). **Endodontia** parece condicional (siso), mas cabe hoje como pergunta de exclusão. **Frenectomia** (“até 3 meses”) só cabe aproximada: a idade do modelo é em anos inteiros.

### Legenda

- **COBERTO** — o que o manual exige está cadastrado e ativo no canônico que recebe o pedido do SER.
- **PARCIAL** — parte está; falta requisito ou a forma está errada.
- **ERRADO** — existe regra ativa com conteúdo errado **ou** presa a um canônico que não recebe o pedido do SER (órfã). O conserto é corrigir/mover, não criar.
- **AUSENTE** — o recurso existe no SER e não há regra (nem o encaminhamento).
- **NAO_REPRESENTAVEL_HOJE** — o requisito depende de condição (documento que só vale se tal opção) e o modelo atual não expressa.
- **SEM_RECURSO_NO_SER** — a linha do manual não tem recurso correspondente no catálogo AE.
- `#xxxxxxxx` = 8 caracteres FINAIS do id da regra (os iniciais do UUIDv7 são carimbo de tempo e se repetem); `…xxxxxxxx` = final do id do canônico.
- “barra=Não” = `resposta_bloqueia=2` (barra quem responde Não / “nenhuma destas”); “barra=Sim” = exclusão. Espelho = `analise_espelho_por_procedimento.json` (todas as entradas, abertas e fechadas).

## Tabela-resumo

| # | Seção | p. | Recurso do manual | Estrutura | Recurso AE no SER | Regras ativas | Veredito |
|---|---|---|---|---|---|---|---|
| F1 | 4.1.17 Infectologia | 36 | PEDIATRIA | LISTA_BASTA_UM | CONSULTA EM INFECTOLOGIA - PEDIATRIA | 0 | **AUSENTE** |
| F2 | 4.1.17 Infectologia | 36 | COINFECÇÃO AIDS/HEPATITE C | INCLUSAO_EXCLUSAO | CONSULTA EM INFECTOLOGIA COINFECCAO HIV/HEPATITE VIRAL | 0 | **AUSENTE** |
| N1 | 4.1.18 Nefrologia | 36 | GERAL | SO_ORIENTACAO | CONSULTA EM NEFROLOGIA - GERAL | 1 | **ERRADO** |
| N2 | 4.1.18 Nefrologia | 37 | PEDIATRIA | SO_ORIENTACAO | CONSULTA EM NEFROLOGIA - PEDIATRIA | 0 | **AUSENTE** |
| U1 | 4.1.19 Nutrição | 37 | GERAL | SO_ORIENTACAO | CONSULTA EM NUTRICAO | 0 | **AUSENTE** |
| U2 | 4.1.19 Nutrição | 37 | PEDIATRIA | SO_ORIENTACAO | CONSULTA EM NUTRICAO - PEDIATRIA | 0 | **AUSENTE** |
| O1 | 4.1.20 Oftalmologia | 37 | CERATOCONE | SIMPLES | CONSULTA EM OFTALMOLOGIA - CERATOCONE | 0 | **AUSENTE** |
| O2 | 4.1.20 Oftalmologia | 38 | CIRURGIA DE CATARATA | SO_ORIENTACAO | CONSULTA EM OFTALMOLOGIA - CIRURGIA DE CATARATA | 0 | **AUSENTE** |
| O3 | 4.1.20 Oftalmologia | 38 | CÓRNEA | SO_ORIENTACAO | CONSULTA EM OFTALMOLOGIA - CORNEA | 0 | **AUSENTE** |
| O4 | 4.1.20 Oftalmologia | 38 | ESTRABISMO | SO_ORIENTACAO | CONSULTA EM OFTALMOLOGIA - ESTRABISMO | 0 | **AUSENTE** |
| O5 | 4.1.20 Oftalmologia | 38 | GERAL | SO_ORIENTACAO | CONSULTA EM OFTALMOLOGIA - GERAL | 0 | **AUSENTE** |
| O6 | 4.1.20 Oftalmologia | 38 | PEDIATRIA | SO_ORIENTACAO | CONSULTA EM OFTALMOLOGIA - PEDIATRIA | 0 | **AUSENTE** |
| O7 | 4.1.20 Oftalmologia | 38 | PLÁSTICA OCULAR | SO_ORIENTACAO | — não achado | 0 | **SEM_RECURSO_NO_SER** |
| O8 | 4.1.20 Oftalmologia | 39 | RETINA GERAL | SO_ORIENTACAO | CONSULTA EM OFTALMOLOGIA - RETINA GERAL | 0 | **AUSENTE** |
| O9 | 4.1.20 Oftalmologia | 39 | UVEÍTE | SO_ORIENTACAO | CONSULTA EM OFTALMOLOGIA - UVEITE | 0 | **AUSENTE** |
| O10 | 4.1.20 Oftalmologia | 39 | GLAUCOMA | SO_ORIENTACAO | CONSULTA EM OFTALMOLOGIA - GLAUCOMA | 0 | **AUSENTE** |
| T1 | 4.1.21 Ortopedia | 39 | ORTOPEDIA GERAL | INCLUSAO_EXCLUSAO | CONSULTA EM ORTOPEDIA (NÃO CIRURGICO) | 0 | **AUSENTE** |
| L1 | 4.1.22 Otorrinolaringologia | 39-40 | GERAL | SIMPLES | CONSULTA EM OTORRINOLARINGOLOGIA | 2 | **ERRADO** |
| L2 | 4.1.22 Otorrinolaringologia | 40 | PEDIATRIA | SIMPLES | CONSULTA EM OTORRINOLARINGOLOGIA PEDIATRICA | 0 | **AUSENTE** |
| L3 | 4.1.22 Otorrinolaringologia | 40 | CIRURGIA | SIMPLES | CONSULTA EM OTORRINOLARINGOLOGIA CIRURGICA | 0 | **AUSENTE** |
| L4 | 4.1.22 Otorrinolaringologia | 40 | CIRURGIA PEDIÁTRICA | SIMPLES | CONSULTA EM OTORRINOLARINGOLOGIA CIRURGICA - PEDIATRIA | 0 | **AUSENTE** |
| P1 | 4.1.23 Pneumologia | 40 | GERAL | SO_ORIENTACAO | CONSULTA EM PNEUMOLOGIA - GERAL | 0 | **AUSENTE** |
| P2 | 4.1.23 Pneumologia | 41 | PEDIATRIA | SO_ORIENTACAO | CONSULTA EM PNEUMOLOGIA - PEDIATRIA | 0 | **AUSENTE** |
| P3 | 4.1.23 Pneumologia | 41 | ASMA DIFÍCIL CONTROLE | SO_ORIENTACAO | — não achado | 0 | **SEM_RECURSO_NO_SER** |
| P4 | 4.1.23 Pneumologia | 41 | HIPERTENSÃO PULMONAR | SO_ORIENTACAO | CONSULTA EM PNEUMOLOGIA - HIPERTENSAO PULMONAR | 0 | **AUSENTE** |
| P5 | 4.1.23 Pneumologia | 41 | TUBERCULOSE COMPLICADA | LISTA_BASTA_UM | CONSULTA EM PNEUMOLOGIA - TUBERCULOSE COMPLICADA | 2 | **COBERTO** |
| S1 | 4.1.24 Polissonografia | 41-42 | POLISSONOGRAFIA | INCLUSAO_EXCLUSAO | CONSULTA EM POLISSONOGRAFIA | 2 | **PARCIAL** |
| Q1 | 4.1.25 Psiquiatria | 42 | PSIQUIATRIA | INCLUSAO_EXCLUSAO | — não achado | 0 | **SEM_RECURSO_NO_SER** |
| R1 | 4.1.26 Reumatologia | 42 | GERAL | LISTA_BASTA_UM | CONSULTA EM REUMATOLOGIA GERAL | 2 | **COBERTO** |
| R2 | 4.1.26 Reumatologia | 42 | PEDIATRIA | SO_ORIENTACAO | CONSULTA EM REUMATOLOGIA - PEDIATRIA | 0 | **AUSENTE** |
| R3 | 4.1.26 Reumatologia | 42-43 | ADOLESCENTE | LISTA_BASTA_UM | CONSULTA EM REUMATOLOGIA - ADOLESCENTE | 0 | **AUSENTE** |
| R4 | 4.1.26 Reumatologia | 43 | ARTRITE CRÔNICA POR CHICUNGUNYA | SO_ORIENTACAO | — não achado | 0 | **SEM_RECURSO_NO_SER** |
| V1 | 4.1.27 Urologia | 43-44 | GERAL | CONDICIONAL | CONSULTA EM UROLOGIA GERAL | 0 | **NAO_REPRESENTAVEL_HOJE** |
| V2 | 4.1.27 Urologia | 44 | PEDIÁTRICO | LISTA_BASTA_UM | CONSULTA EM UROLOGIA PEDIATRICA | 2 | **COBERTO** |
| V3 | 4.1.27 Urologia | 44 | DISFUNÇÃO SEXUAL | SO_ORIENTACAO | CONSULTA EM UROLOGIA - DISFUNCAO SEXUAL | 0 | **AUSENTE** |
| V4 | 4.1.27 Urologia | 44 | LITÍASE | SIMPLES | CONSULTA EM UROLOGIA - LITIASE | 2 | **COBERTO** |
| V5 | 4.1.27 Urologia | 44 | VASECTOMIA | SO_ORIENTACAO | CONSULTA EM UROLOGIA - VASECTOMIA | 0 | **AUSENTE** |
| V6 | 4.1.27 Urologia | 45 | DISFUNÇÃO MICCIONAL | LISTA_BASTA_UM | CONSULTA EM UROLOGIA DISFUNCAO MICCIONAL | 0 | **AUSENTE** |
| V7 | 4.1.27 Urologia | 45 | DISFUNÇÃO MICCIONAL PEDIÁTRICA | LISTA_BASTA_UM | CONSULTA EM UROLOGIA DISFUNCAO MICCIONAL - PEDIATRIA | 2 | **COBERTO** |
| V8 | 4.1.27 Urologia | 45 | GINECOLOGIA | LISTA_BASTA_UM | CONSULTA EM UROLOGIA GINECOLOGIA | 2 | **COBERTO** |
| V9 | 4.1.27 Urologia | 45-46 | RECONSTRUTORA | SO_ORIENTACAO | CONSULTA EM UROLOGIA RECONSTRUTORA | 0 | **AUSENTE** |
| D1 | 4.1.28 Odontologia | 46 | CIRURGIA BUCOMAXILOFACIAL (Traumas, fraturas, reconstruções, cistos e tumores) | LISTA_BASTA_UM | CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL | 0 | **AUSENTE** |
| D2 | 4.1.28 Odontologia | 46 | CIRURGIA BUCOMAXILOFACIAL (Cirurgia da ATM) | LISTA_BASTA_UM | sem recurso próprio | 0 | **AUSENTE** |
| D3 | 4.1.28 Odontologia | 46 | CIRURGIA ORTOGNÁTICA (Cirurgia da ATM) | SIMPLES | — não achado | 0 | **SEM_RECURSO_NO_SER** |
| D4 | 4.1.28 Odontologia | 47 | CIRURGIA ORAL MENOR | LISTA_BASTA_UM | CONSULTA EM ODONTOLOGIA - CIRURGIA ORAL MENOR | 2 | **PARCIAL** |
| D5 | 4.1.28 Odontologia | 47 | DTM (Disfunção da ATM) | LISTA_BASTA_UM | sem recurso com esse nome | 0 | **AUSENTE** |
| D6 | 4.1.28 Odontologia | 47 | ENDODONTIA | CONDICIONAL | CONSULTA EM ODONTOLOGIA - ENDODONTIA | 3 | **ERRADO** |
| D7 | 4.1.28 Odontologia | 48 | ESTOMATOLOGIA | LISTA_BASTA_UM | CONSULTA EM ODONTOLOGIA - ESTOMATOLOGIA | 2 | **ERRADO** |
| D8 | 4.1.28 Odontologia | 48 | PACIENTES PORTADORES DE COMPROMETIMENTO NEUROLÓGICO | LISTA_BASTA_UM | CONSULTA EM ODONTOLOGIA - PACIENTE COM NECESSIDADE ESPECIAL | 0 | **AUSENTE** |
| D9 | 4.1.28 Odontologia | 48-49 | PACIENTES PORTADORES DE NECESSIDADES ESPECIAIS | LISTA_BASTA_UM | CONSULTA EM ODONTOLOGIA - PACIENTE COM NECESSIDADE ESPECIAL | 0 | **AUSENTE** |
| D10 | 4.1.28 Odontologia | 49 | FRENECTOMIA LINGUAL | SIMPLES | FRENECTOMIA LINGUAL - PEDIATRIA | 0 | **AUSENTE** |
| D11 | 4.1.28 Odontologia | 49 | TOMOGRAFIA COMPUTADORIZADA CONE BEAN (MEDICINA NUCLEAR) | SIMPLES | TOMOGRAFIA CONE BEAN | 0 | **AUSENTE** |
| D12 | 4.1.28 Odontologia | 49 | OUTRAS ESPECIALIDADES (GRUPO/RADIOGRAFIA PANORÂMICA) | SIMPLES | RADIOGRAFIA PANORÂMICA - ODONTOLOGIA | 0 | **AUSENTE** |
| G1 | 4.2.1 Cirurgia Geral | 50 | APARELHO DIGESTÓRIO | LISTA_BASTA_UM | CONSULTA EM CIRURGIA GERAL - APARELHO DIGESTIVO | 0 | **AUSENTE** |
| G2 | 4.2.1 Cirurgia Geral | 50 | ESTÔMAGO | LISTA_BASTA_UM | CONSULTA EM CIRURGIA GERAL - ESTOMAGO | 0 | **AUSENTE** |
| G3 | 4.2.1 Cirurgia Geral | 50 | ESÔFAGO | LISTA_BASTA_UM | CONSULTA EM CIRURGIA GERAL - ESOFAGO | 2 | **COBERTO** |
| G4 | 4.2.1 Cirurgia Geral | 51 | FÍGADO | SIMPLES | CONSULTA EM CIRURGIA GERAL - FIGADO | 0 | **AUSENTE** |
| G5 | 4.2.1 Cirurgia Geral | 51 | HÉRNIA | LISTA_BASTA_UM | CONSULTA EM CIRURGIA GERAL - HERNIA | 0 | **AUSENTE** |
| G6 | 4.2.1 Cirurgia Geral | 51 | PÂNCREAS | SIMPLES | CONSULTA EM CIRURGIA GERAL - PANCREAS | 2 | **COBERTO** |
| G7 | 4.2.1 Cirurgia Geral | 51 | PARTES MOLES | INCLUSAO_EXCLUSAO | CONSULTA EM CIRURGIA GERAL - PARTES MOLES | 3 | **COBERTO** |
| G8 | 4.2.1 Cirurgia Geral | 52 | VESÍCULA | LISTA_BASTA_UM | CONSULTA EM CIRURGIA GERAL - VESICULA | 3 | **COBERTO** |
| G9 | 4.2.1 Cirurgia Geral | 52 | TIREOIDE | SIMPLES | CONSULTA EM CIRURGIA GERAL - TIREOIDES | 0 | **AUSENTE** |
| G10 | 4.2.1 Cirurgia Geral | 52 | GERAL | SIMPLES | — não achado | 0 | **SEM_RECURSO_NO_SER** |
| C1 | 4.2.2 Cirurgia Pediátrica | 52 | CIRURGIA PEDIÁTRICA | SIMPLES | CONSULTA EM CIRURGIA PEDIATRICA | 3 | **ERRADO** |
| K1 | 4.2.3 Cirurgia Plástica | 53 | CIRURGIA PLÁSTICA | LISTA_BASTA_UM | O SER parte a linha em 4 recursos AE: CONSULTA EM CIRURGIA PLASTICA - … | 0 | **AUSENTE** |
| K2 | 4.2.4 Cirurgia Torácica | 53 | CIRURGIA TORÁCICA | SIMPLES | CONSULTA EM CIRURGIA TORACICA | 0 | **AUSENTE** |
| K3 | 4.2.5 Cirurgia Vascular | 53 | CIRURGIA VASCULAR | SIMPLES | CONSULTA EM CIRURGIA VASCULAR - DOENCA VENOSA | 0 | **AUSENTE** |
| K4 | 4.2.6 Cirurgia Reparadora | 54 | CIRURGIA REPARADORA | SO_ORIENTACAO | — não achado | 0 | **SEM_RECURSO_NO_SER** |

## Regras de produção do trecho fora do lugar

Todas as regras com fonte CRECE p.36–54 estão em 17 canônicos. Conteúdo literal confere em todas, **exceto a primeira**. O problema das outras é onde estão.

| Regra | Canônico onde está | Problema |
|---|---|---|
| #9a8c51b1 ATIVA Pergunta/Bloqueia “Pacientes com indicação para cirurgia otorrinolaringológica” (CRECE p.40) | CONSULTA EM CIRURGIA PEDIATRICA (…8d90883a6d71) | **Procedimento errado.** É da Otorrino (linhas PEDIATRIA/CIRURGIA PEDIÁTRICA, p.40). Origem: spike pareou “OTORRINOLARINGOLOGIA - CIRURGIA PEDIÁTRICA” → “CONSULTA EM CIRURGIA PEDIATRICA”. Desligar. |
| #d744dd0b, #9c1ba9c0 (Cirurgia Pediátrica) | CONSULTA EM CIRURGIA PEDIATRICA (…8d90883a6d71) | Órfãs: origem SER inativa; o recurso v1057 está confirmado em “CONSULTA EM CIRURGIA GERAL - PEDIATRIA” (…524e1b4d), sem regras. |
| #ac459843 encaminhamento (+2 inativas) | CONSULTA EM NEFROLOGIA - GERAL (…506a8547f05b) | Canônico **sem nenhuma origem**; o recurso v1118 está em “CONSULTA EM NEFROLOGIA” (…ff43253e). |
| #a6b97d9e, #adefd850 (Otorrino Geral) | CONSULTA EM OTORRINOLARINGOLOGIA (…345751ca189b) | Homônimo do canônico que recebe o SER (…ab934a60); este só tem origem SISREG e as regras são sistema=SER. |
| #a0cc26ae, #05de0ac4, #66ba2ac3 (Endodontia) | CONSULTA EM ODONTOLOGIA - ENDODONTIA (…1a8e442eed52) | Nomes cruzados: este tem a origem SISREG “CONSULTA ODONTOLOGIA - ENDODONTIA”; o recurso SER foi para “CONSULTA ODONTOLOGIA - ENDODONTIA” (…32c6bf0d). |
| #a7f6e021, #d81f5d58 (Estomatologia) | CONSULTA EM ODONTOLOGIA - ESTOMATOLOGIA (…de59687e78a0) | Idem Endodontia; o SER vai para …f21b9160. |
| Encaminhamento “requisito global” em Oral Menor (#7c2f01e6), Endodontia, Estomatologia | Odontologia | Texto errado para Odontologia: o manual pede encaminhamento do **Cirurgião-Dentista** + **exames radiográficos anteriores**. |

A coluna `procedimento_origem_id` das regras não foi usada (a REVISAO de 01/10 já registrou que está suja; o avaliador do espelho não a lê).

## Recursos AE do SER nessas especialidades sem linha no manual

CONSULTA EM UROLOGIA CIRURGICA (v1181 → “CONSULTA EM CIRURGIA UROLOGICA”, 7 pedidos) · LITOTRIPSIA EXTRACORPOREA (LECO) (v1182) · CONSULTA EM CIRURGIA GERAL - SUPRA RENAL (v1059) · CONSULTA EM REUMATOLOGIA - DOENÇA AUTOIMUNE (v1178) · CONSULTA EM OFTALMOLOGIA - CIRURGIA DE CATARATA PEDIATRICA (v1139) · CONSULTA EM OFTALMOLOGIA - PEDIATRIA - ESTRABISMO (v1143, 34 pedidos) · OFTALMOLOGIA TRATAMENTO CIRURGICO DE PTERIGIO (v1144) · CONSULTA EM INFECTOLOGIA-PEDIATRIA-HIV/AIDS (v1078) · CONSULTA EM ODONTOLOGIA - ODONTOPEDIATRIA (v1131) · - PERIODONTIA (v1138) · - DOR ORO-FACIAL (v1137, provável par da DTM) · FOTOCOAGULACAO ODONTOLOGICA (v1072) · RADIOGRAFIA PERIAPICAL (v1074) · TELERRADIOGRAFIA (v1102) · TC DE ARTICULACOES TEMPORO-MANDIBULARES (v1068) · TC DE FACE - PEDIATRIA (v1067) · CIRURGIA PLASTICA - TUMOR DE PELE (v1060, 16 pedidos; ver K1).

## Pendências da REVISAO de 01/10 (conversao-listas/REVISAO.md) neste trecho

- **CONSULTA EM CIRURGIA PEDIATRICA** — “Não achei a seção no PDF … não mexi”. **Resolvida aqui:** a seção é a 4.2.2, p.52. As duas regras NÃO são alternativas: a de otorrino não pertence a esta seção. Ação: desligar #9a8c51b1 (ver C1).
- **Polissonografia** — não aparece na REVISAO: era uma regra só, então a conversão em listas não a pegou, e as exclusões nunca foram importadas.
- O que a REVISAO aplicou neste trecho (listas de Esôfago, Vesícula, Oral Menor, Estomatologia, TB, Reumato Geral, Uro Pediátrica, DM Pediátrica, Uro Ginecologia; Litíase v2; siso da Endodontia como Informativa; cacos de Nefrologia/Pâncreas/Partes Moles) **confere com o PDF**. Mas Nefrologia, Endodontia e Estomatologia foram corrigidas em canônicos órfãos — a correção não chega ao pedido do SER.

## Surpresas

- O SER reaproveitou o valor **1118**: hoje é CONSULTA EM NEFROLOGIA - GERAL; antes era CONSULTA EM NEUROLOGIA/AVC (sincronizado pela última vez em 30/09, origem inativa). A chave `{tipo}|{valor}|{ramo}` colide.
- Dois canônicos com o mesmo nome “CONSULTA EM OTORRINOLARINGOLOGIA”.
- O espelho guarda análise velha: TB Complicada e Polissonografia têm 1 pedido cada ainda “Sem regras” apesar de regras ativas.
- Esquisitices do próprio manual: marcador vazio em Glaucoma (p.39); Psiquiatria exclui “Doenças psiquiátricas descompensada” (cópia da Polissonografia?); cabeçalho “Local da lesão” em Reumatologia; “Cone Bean (Medicina Nuclear)”; “Cirurgia Ortognática (Cirurgia da ATM)”.
- O spike juntou Bucomaxilofacial, ATM e Ortognática num recurso fantasma “ODONTOLOGIA - RECONSTRUTORA” (27 linhas) e criou “ORTOPEDIA - OTORRINOLAR / INGOLOGIA”: cortes de layout.

## Por recurso

### F1 · 4.1.17 Infectologia (fronteira — seção começa na p.35) — PEDIATRIA (p.36)

1. **Seção / página / recurso:** 4.1.17 Infectologia (fronteira — seção começa na p.35) · p.36 · PEDIATRIA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Critério (lista ABERTA — “como”): “Pacientes que apresentem Doenças Infecciosas e Parasitárias, como:” Recém-nascido/criança exposta ao HIV · Investigação de adenomegalia · Investigação de hepatomegalia e/ou esplenomegalia · Toxoplasmose congênita ou adquirida · Citomegalovirose congênita · Infecção pelo Zika Vírus · Investigação de Febre de Origem Obscura · Piodermite de repetição · Infecções de repetição · Tuberculose · Sífilis Congênita e Adquirida · Vítima de Abuso sexual.
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM INFECTOLOGIA - PEDIATRIA (AE tipo1 v1076). Vizinho sem linha no manual: CONSULTA EM INFECTOLOGIA-PEDIATRIA-HIV/AIDS (v1078).
5. **Nosso:** CONSULTA EM INFECTOLOGIA - PEDIATRIA (…e1b6b76c, vínculo confirmado) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra. No spike, a linha não foi extraída como recurso próprio (só “COINFECÇÃO”, SEM_PAR).
8. **Proposta:** Pergunta de lista (basta uma), rb=Não, Bloqueia: as 12 opções literais + “Outra doença infecciosa ou parasitária (descrever no encaminhamento)” (o manual diz “como”). Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.36) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### F2 · 4.1.17 Infectologia (fronteira — seção começa na p.35) — COINFECÇÃO AIDS/HEPATITE C (p.36)

1. **Seção / página / recurso:** 4.1.17 Infectologia (fronteira — seção começa na p.35) · p.36 · COINFECÇÃO AIDS/HEPATITE C
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério: “Pacientes que apresentem infecções pelo vírus da Hepatite C e HIV.” (as duas — coinfecção)
   - Orientação: “Observar critérios de faixa etária de inclusão de cada prestador.”
   - Exclusão: “Não atendem pacientes com Hepatite B.”
   - Exclusão: “Não atendem pacientes com infecção isolada por Hepatite C ou por HIV.” (é o avesso do critério de inclusão)
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM INFECTOLOGIA COINFECCAO HIV/HEPATITE VIRAL (AE tipo1 v1077) — o SER diz “Hepatite VIRAL” (inclui B); o manual exclui Hepatite B.
5. **Nosso:** CONSULTA EM INFECTOLOGIA COINFECCAO HIV/HEPATITE VIRAL (…a8519bf5) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (spike: SEM_PAR). 1 pedido do SER no espelho em “Sem regras”.
8. **Proposta:** Pergunta Sim/Não “O paciente tem coinfecção HIV e Hepatite C (as duas)?” rb=Não, Bloqueia (cobre também a exclusão “infecção isolada”, que não deve virar segunda regra); Pergunta “O paciente tem Hepatite B?” rb=Sim, Bloqueia; Documento encaminhamento. CID (B20–B24 + B18.2) só como Ressalva, nunca Bloqueia: o pedido traz um CID só.

### N1 · 4.1.18 Nefrologia — GERAL (p.36)

1. **Seção / página / recurso:** 4.1.18 Nefrologia · p.36 · GERAL
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Pacientes que apresentem condições de acordo com os critérios de inclusão de cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM NEFROLOGIA - GERAL (AE tipo1 v1118). Obs.: o valor 1118 é o mesmo do recurso “CONSULTA EM NEUROLOGIA/AVC”, que sumiu do SER em 30/09.
5. **Nosso:** Recurso ligado (confirmado) a “CONSULTA EM NEFROLOGIA” (…ff43253e) — 0 regras. As regras estão em OUTRO canônico, “CONSULTA EM NEFROLOGIA - GERAL” (…506a8547f05b), que não tem NENHUMA origem.
   - Ativas:
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #ac459843
   - Inativas (2):
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem condições de acordo com os (CRECE p.36) #99bc27a4
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem condições de acordo com os (CRECE p.36) #87a26193
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - Regra órfã: o encaminhamento está ativo num canônico sem origem, então nunca é avaliado; o pedido do SER cai em “CONSULTA EM NEFROLOGIA”, sem regra.
   - As 2 inativas (“Pacientes que apresentem condições de acordo com os”) são cacos — desligadas certo (REVISAO 01/10).
8. **Proposta:** Recriar o encaminhamento no canônico “CONSULTA EM NEFROLOGIA” e desligar o órfão (ou fundir os dois canônicos, decisão da curadoria). Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.36) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### N2 · 4.1.18 Nefrologia — PEDIATRIA (p.37)

1. **Seção / página / recurso:** 4.1.18 Nefrologia · p.37 · PEDIATRIA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Pacientes que apresentem condições de acordo com os critérios de inclusão de cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM NEFROLOGIA - PEDIATRIA (AE tipo1 v1119)
5. **Nosso:** CONSULTA EM NEFROLOGIA - PEDIATRICA (…0dcfe818) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; 22 pedidos do SER no espelho em “Sem regras”. Nenhuma idade no manual — não inventar Dedutível de idade.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.37) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### U1 · 4.1.19 Nutrição — GERAL (p.37)

1. **Seção / página / recurso:** 4.1.19 Nutrição · p.37 · GERAL
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Pacientes que apresentem condições de acordo com os critérios de inclusão de cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM NUTRICAO (AE tipo1 v1157)
5. **Nosso:** CONSULTA EM NUTRIÇÃO (…eaa871ff) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento).
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.37) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### U2 · 4.1.19 Nutrição — PEDIATRIA (p.37)

1. **Seção / página / recurso:** 4.1.19 Nutrição · p.37 · PEDIATRIA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Pacientes que apresentem condições de acordo com os critérios de inclusão de cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM NUTRICAO - PEDIATRIA (AE tipo1 v1161)
5. **Nosso:** CONSULTA EM NUTRICAO - PEDIATRIA (…f52682ec) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.37) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### O1 · 4.1.20 Oftalmologia — CERATOCONE (p.37)

1. **Seção / página / recurso:** 4.1.20 Oftalmologia · p.37 · CERATOCONE
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Critério: “Pacientes com diagnóstico de Ceratocone para realizar o procedimento de Crosslinking.” (diagnóstico + finalidade)
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OFTALMOLOGIA - CERATOCONE (AE tipo1 v1145)
5. **Nosso:** CONSULTA EM OFTALMOLOGIA - CERATOCONE (…3b78a767) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; 3 pedidos do SER em “Sem regras”. O spike não extraiu esta linha.
8. **Proposta:** Pergunta Sim/Não “O paciente tem diagnóstico de Ceratocone e o encaminhamento é para Crosslinking?” rb=Não, Bloqueia; Documento encaminhamento. CID H18.6 no máximo como Ressalva.

### O2 · 4.1.20 Oftalmologia — CIRURGIA DE CATARATA (p.38)

1. **Seção / página / recurso:** 4.1.20 Oftalmologia · p.38 · CIRURGIA DE CATARATA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OFTALMOLOGIA - CIRURGIA DE CATARATA (AE tipo1 v1146). O SER também tem “… - CIRURGIA DE CATARATA PEDIATRICA” (v1139), sem linha no manual.
5. **Nosso:** CONSULTA EM OFTALMOLOGIA - CIRURGIA DE CATARATA (…db912bc8) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). 1 pedido do SER em “Sem regras”.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.38) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### O3 · 4.1.20 Oftalmologia — CÓRNEA (p.38)

1. **Seção / página / recurso:** 4.1.20 Oftalmologia · p.38 · CÓRNEA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OFTALMOLOGIA - CORNEA (AE tipo1 v1141)
5. **Nosso:** CONSULTA EM OFTALMOLOGIA - CORNEA (…19d2deb0) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). 21 pedidos do SER em “Sem regras”.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.38) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### O4 · 4.1.20 Oftalmologia — ESTRABISMO (p.38)

1. **Seção / página / recurso:** 4.1.20 Oftalmologia · p.38 · ESTRABISMO
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OFTALMOLOGIA - ESTRABISMO (AE tipo1 v1140). O SER também tem “… - PEDIATRIA - ESTRABISMO” (v1143), sem linha no manual (34 pedidos no espelho).
5. **Nosso:** CONSULTA EM OFTALMOLOGIA - ESTRABISMO (…370766a6) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). 20 pedidos do SER em “Sem regras”.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.38) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### O5 · 4.1.20 Oftalmologia — GERAL (p.38)

1. **Seção / página / recurso:** 4.1.20 Oftalmologia · p.38 · GERAL
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OFTALMOLOGIA - GERAL (AE tipo1 v1147)
5. **Nosso:** CONSULTA EM OFTALMOLOGIA - GERAL (…b6330725) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). Nenhuma regra.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.38) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### O6 · 4.1.20 Oftalmologia — PEDIATRIA (p.38)

1. **Seção / página / recurso:** 4.1.20 Oftalmologia · p.38 · PEDIATRIA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OFTALMOLOGIA - PEDIATRIA (AE tipo1 v1148)
5. **Nosso:** CONSULTA EM OFTALMOLOGIA - PEDIATRA (…5f4feb59; nome do SISREG) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). 31 pedidos do SER em “Sem regras”.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.38) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### O7 · 4.1.20 Oftalmologia — PLÁSTICA OCULAR (p.38)

1. **Seção / página / recurso:** 4.1.20 Oftalmologia · p.38 · PLÁSTICA OCULAR
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** não achado. Candidato fraco: OFTALMOLOGIA - EXERESE DE CALAZIO E OUTRAS PEQUENAS LESOES DA PALPEBRA E SUPERCILIOS (AE tipo2 v1079 — procedimento, não consulta).
5. **Nosso:** —
   - Ativas: nenhuma.
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Não há consulta de plástica ocular no catálogo AE do SER.
8. **Proposta:** Nada a cadastrar até existir o recurso; se a curadoria parear com a exérese de calázio, só o Documento encaminhamento.

### O8 · 4.1.20 Oftalmologia — RETINA GERAL (p.39)

1. **Seção / página / recurso:** 4.1.20 Oftalmologia · p.39 · RETINA GERAL
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OFTALMOLOGIA - RETINA GERAL (AE tipo1 v1142)
5. **Nosso:** CONSULTA EM OFTALMOLOGIA - RETINA GERAL (…6856f4b7) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). 19 pedidos do SER em “Sem regras”.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.39) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### O9 · 4.1.20 Oftalmologia — UVEÍTE (p.39)

1. **Seção / página / recurso:** 4.1.20 Oftalmologia · p.39 · UVEÍTE
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OFTALMOLOGIA - UVEITE (AE tipo1 v1149)
5. **Nosso:** CONSULTA EM OFTALMOLOGIA - UVEITE (…e075defb) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). Nenhuma regra.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.39) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### O10 · 4.1.20 Oftalmologia — GLAUCOMA (p.39)

1. **Seção / página / recurso:** 4.1.20 Oftalmologia · p.39 · GLAUCOMA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OFTALMOLOGIA - GLAUCOMA (AE tipo1 v1150)
5. **Nosso:** CONSULTA EM OFTALMOLOGIA - GLAUCOMA (…68c963e7) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). 5 pedidos do SER em “Sem regras”. O próprio manual tem um marcador VAZIO (“•”) logo abaixo de “Critérios de inclusão:” (p.39) — critério possivelmente apagado; perguntar à regulação.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.39) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### T1 · 4.1.21 Ortopedia — ORTOPEDIA GERAL (p.39)

1. **Seção / página / recurso:** 4.1.21 Ortopedia · p.39 · ORTOPEDIA GERAL
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (manual, literal):**
   - Critério: “Pacientes para consultas em Ortopedia Clínica.”
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Documento com validade: “Apresentar Exame de Imagem recente (até 180 dias).”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão: “Não realizam Cirurgia Ortopédica.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM ORTOPEDIA (NÃO CIRURGICO) (AE tipo1 v1151)
5. **Nosso:** CONSULTA EM ORTOPEDIA (NÃO CIRURGICO) (…534ff818) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra. O spike extraiu certo (inclusive o exame de imagem como Documental), mas ficou SEM_PAR e não foi importado.
   - É o único recurso do trecho com prazo explícito de exame (180 dias).
8. **Proposta:** Documento/Bloqueia “Exame de Imagem recente (até 180 dias)” com validade_dias=180; Pergunta de exclusão “O encaminhamento é para avaliação ou indicação de cirurgia ortopédica?” rb=Sim (Bloqueia, ou Ressalva se a regulação preferir); Documento encaminhamento.

### L1 · 4.1.22 Otorrinolaringologia — GERAL (p.39-40)

1. **Seção / página / recurso:** 4.1.22 Otorrinolaringologia · p.39-40 · GERAL
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Critério: “Pacientes com indicação para consulta e/ou cirurgia otorrinolaringológica.”
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OTORRINOLARINGOLOGIA (AE tipo1 v1152)
5. **Nosso:** Recurso ligado (confirmado) a “CONSULTA EM OTORRINOLARINGOLOGIA” (…ab934a60) — 0 regras. As regras estão num SEGUNDO canônico de mesmo nome (…345751ca189b), cuja única origem é o SISREG; as regras são sistema=SER.
   - Ativas:
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #a6b97d9e
     - [Pergunta/Bloqueia, barra=Não] Pacientes com indicação para consulta e/ou cirurgia otorrinolaringológica (CRECE p.39) #adefd850
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - Regras órfãs: no canônico do SISREG, filtradas para o SER — não valem para ninguém. Pedido do SER cai no homônimo sem regras.
   - Dois canônicos com o MESMO nome “CONSULTA EM OTORRINOLARINGOLOGIA”.
   - O critério é quase tautológico para uma consulta (“indicação para consulta e/ou cirurgia”).
8. **Proposta:** Recriar no …ab934a60 (ou fundir os homônimos) e desligar as órfãs. Critério como Informativa (ou Pergunta com Ressalva); Documento encaminhamento.

### L2 · 4.1.22 Otorrinolaringologia — PEDIATRIA (p.40)

1. **Seção / página / recurso:** 4.1.22 Otorrinolaringologia · p.40 · PEDIATRIA
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Critério: “Pacientes com indicação para cirurgia otorrinolaringológica.” (cirurgia — não “consulta e/ou cirurgia” como no GERAL)
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OTORRINOLARINGOLOGIA PEDIATRICA (AE tipo1 v1153)
5. **Nosso:** CONSULTA EM OTORRINOLARINGOLOGIA - CIRURGIA PEDIATRIA (…743e1dc2, confirmado) — 0 regras. É o MESMO canônico do recurso v1155 (Cirurgia Pediátrica).
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; 21 pedidos do SER em “Sem regras” (somando v1153 e v1155).
   - Dois recursos do SER num canônico só — aqui inofensivo, porque o manual dá o mesmo texto às duas linhas.
8. **Proposta:** No …743e1dc2 (serve às duas linhas): Pergunta Sim/Não “O paciente tem indicação de cirurgia otorrinolaringológica?” rb=Não, Bloqueia; Documento encaminhamento.

### L3 · 4.1.22 Otorrinolaringologia — CIRURGIA (p.40)

1. **Seção / página / recurso:** 4.1.22 Otorrinolaringologia · p.40 · CIRURGIA
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Critério: “Pacientes com indicação para cirurgia otorrinolaringológica.”
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OTORRINOLARINGOLOGIA CIRURGICA (AE tipo1 v1154)
5. **Nosso:** CONSULTA EM CIRURGIA OTORRINOLARINGOLOGIA (…f1032484) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; 42 pedidos do SER em “Sem regras”.
8. **Proposta:** Pergunta Sim/Não “O paciente tem indicação de cirurgia otorrinolaringológica?” rb=Não, Bloqueia; Documento encaminhamento.

### L4 · 4.1.22 Otorrinolaringologia — CIRURGIA PEDIÁTRICA (p.40)

1. **Seção / página / recurso:** 4.1.22 Otorrinolaringologia · p.40 · CIRURGIA PEDIÁTRICA
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Critério: “Pacientes com indicação para cirurgia otorrinolaringológica.”
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM OTORRINOLARINGOLOGIA CIRURGICA - PEDIATRIA (AE tipo1 v1155)
5. **Nosso:** CONSULTA EM OTORRINOLARINGOLOGIA - CIRURGIA PEDIATRIA (…743e1dc2) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - A regra DESTA linha (CRECE p.40) foi gravada em “CONSULTA EM CIRURGIA PEDIATRICA” (regra #9a8c51b1) — o spike pareou “OTORRINOLARINGOLOGIA - CIRURGIA PEDIÁTRICA” com “CONSULTA EM CIRURGIA PEDIATRICA”. Ver C1.
8. **Proposta:** Mesma regra de L2, no …743e1dc2 (um cadastro atende L2 e L4).

### P1 · 4.1.23 Pneumologia — GERAL (p.40)

1. **Seção / página / recurso:** 4.1.23 Pneumologia · p.40 · GERAL
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM PNEUMOLOGIA - GERAL (AE tipo1 v1173)
5. **Nosso:** CONSULTA EM PNEUMOLOGIA - GERAL (…71e826a3) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). 2 pedidos do SER em “Sem regras”.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.40) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### P2 · 4.1.23 Pneumologia — PEDIATRIA (p.41)

1. **Seção / página / recurso:** 4.1.23 Pneumologia · p.41 · PEDIATRIA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM PNEUMOLOGIA - PEDIATRIA (AE tipo1 v1174)
5. **Nosso:** CONSULTA EM PNEUMOLOGIA - PEDIATRIA (…efc0fb63) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). 6 pedidos do SER em “Sem regras”.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.41) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### P3 · 4.1.23 Pneumologia — ASMA DIFÍCIL CONTROLE (p.41)

1. **Seção / página / recurso:** 4.1.23 Pneumologia · p.41 · ASMA DIFÍCIL CONTROLE
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** não achado (nenhum recurso AE nem não-AE com “ASMA”).
5. **Nosso:** —
   - Ativas: nenhuma.
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Recurso do manual sem par no catálogo do SER.
8. **Proposta:** Nada a cadastrar até o recurso existir.

### P4 · 4.1.23 Pneumologia — HIPERTENSÃO PULMONAR (p.41)

1. **Seção / página / recurso:** 4.1.23 Pneumologia · p.41 · HIPERTENSÃO PULMONAR
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM PNEUMOLOGIA - HIPERTENSAO PULMONAR (AE tipo1 v1175)
5. **Nosso:** CONSULTA EM PNEUMOLOGIA - HIPERTENSAO PULMONAR (…cbb5e590) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento). Nenhuma regra.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.41) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### P5 · 4.1.23 Pneumologia — TUBERCULOSE COMPLICADA (p.41)

1. **Seção / página / recurso:** 4.1.23 Pneumologia · p.41 · TUBERCULOSE COMPLICADA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Critério (lista FECHADA — “os seguintes casos”, sem “como”/“entre outras”): Tuberculose Extrapulmonar · Tuberculose com intolerância ao esquema básico com indicação para tratamento alternativo · Tuberculose com evolução desfavorável ao esquema básico · Micobactéria Não-tuberculosa.
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM PNEUMOLOGIA - TUBERCULOSE COMPLICADA (AE tipo1 v1176)
5. **Nosso:** CONSULTA EM PNEUMOLOGIA - TUBERCULOSE COMPLICADA (…3d1c7e67d29a)
   - Ativas:
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #57e38dff
     - [Pergunta/Bloqueia, barra=Não, lista 4 opções] Pacientes apresentando os seguintes casos (CRECE p.41) #1e083c2e — opções: Tuberculose Extrapulmonar | Tuberculose com intolerância ao esquema básico com indicação para tratamento alternativo | Tuberculose com evolução desfavorável ao esquema básico | Micobactéria Não-tuberculosa
   - Inativas (5):
     - [Pergunta/Bloqueia, barra=Não] Pacientes apresentando os seguintes casos: Micobactéria Não-tuberculosa (CRECE p.41) #f77e44d0
     - [Pergunta/Bloqueia, barra=Não] Pacientes apresentando os seguintes casos: Tuberculose com intolerância ao esquema básico com indicação para t… (CRECE p.41) #75ea725a
     - [Pergunta/Bloqueia, barra=Não] Pacientes apresentando os seguintes casos (CRECE p.41) #7ffa8367
     - [Pergunta/Bloqueia, barra=Não] Pacientes apresentando os seguintes casos: Tuberculose com evolução desfavorável ao esquema básico (CRECE p.41) #eeabd88b
     - [Pergunta/Bloqueia, barra=Não] Pacientes apresentando os seguintes casos: Tuberculose Extrapulmonar (CRECE p.41) #f04415f5
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nada no conteúdo: 4/4 opções literais, sem “Outra” (correto, a lista é fechada).
   - 1 pedido do SER no espelho ainda marcado “Sem regras” — análise anterior às regras, não refeita.
8. **Proposta:** Manter.

### S1 · 4.1.24 Polissonografia — POLISSONOGRAFIA (p.41-42)

1. **Seção / página / recurso:** 4.1.24 Polissonografia · p.41-42 · POLISSONOGRAFIA
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério (lista FECHADA de 7 — “Pacientes que apresentem:”, sem “como”/“entre outras”): queixa de insônia; movimentos anormais durante o sono; parassonia; narcolepsia; sonolência excessiva diurna; distúrbios do ritmo circadiano; transtorno comportamental do sono REM.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão (p.42, continuação da mesma tabela): “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento:” o Doenças psiquiátricas descompensada. o Menores de 18 anos.
4. **Recurso no SER:** CONSULTA EM POLISSONOGRAFIA (AE tipo1 v1170)
5. **Nosso:** CONSULTA EM POLISSONOGRAFIA (…d16ce664ddba) — o procedimento mais pedido no nosso módulo (6 solicitações) e 268 pedidos do SER no espelho em “A conferir”.
   - Ativas:
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem: queixa de insônia; movimentos anormais durante o sono; parassonia; narcolepsia; sonolência excessiva diurna; distúrbios do ritmo circadiano; transtorno comportamental do sono REM (CRECE p.41) #b24cbfc9
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #1b292469
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - FALTA a exclusão “Menores de 18 anos” — é Dedutível (idade do cadastro) e é a única regra do trecho que a análise do espelho conseguiria decidir SOZINHA (Bloqueado), em vez de “A conferir”.
   - FALTA a exclusão “Doenças psiquiátricas descompensada”.
   - A inclusão é uma Pergunta Sim/Não com as 7 condições num texto corrido (pergunta = o próprio texto do manual): não diz se basta uma; a conversão em listas de 01/10 não pegou (era uma regra só).
   - O spike (spike-e-manual-regras.csv) gravou “Menores de 18 anos” como idade_max=18 — INVERTIDO (deixaria passar só quem tem até 18). Não importar de lá.
   - Para a regulação confirmar: a lista literal não cita ronco/suspeita de apneia do sono; pedido só por apneia, sem sonolência diurna, seria barrado se respondido ao pé da letra.
   - As 6 solicitações já abertas no nosso módulo nunca foram checadas para idade nem para doença psiquiátrica.
8. **Proposta:** (1) Nova versão da inclusão como Pergunta de lista (basta uma), rb=Não, Bloqueia, 7 opções literais e SEM “Outra” (lista fechada no manual). (2) Dedutível idade_min_anos=18, Bloqueia, descrição “Menores de 18 anos”, fonte CRECE p.42. (3) Pergunta “O paciente tem doença psiquiátrica descompensada?” rb=Sim, Bloqueia, fonte CRECE p.42. (4) Manter o Documento encaminhamento. Tudo cabe no modelo atual; nada condicional.

### Q1 · 4.1.25 Psiquiatria — PSIQUIATRIA (p.42)

1. **Seção / página / recurso:** 4.1.25 Psiquiatria · p.42 · PSIQUIATRIA
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento:” o Doenças psiquiátricas descompensada. o Menores de 18 anos.
4. **Recurso no SER:** não achado (nenhum recurso AE nem não-AE com “PSIQ” no catálogo de 423).
5. **Nosso:** —
   - Ativas: nenhuma.
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem recurso no SER.
   - O manual exclui “Doenças psiquiátricas descompensada” de uma consulta de PSIQUIATRIA — texto idêntico ao da Polissonografia (provável cópia); confirmar antes de cadastrar.
8. **Proposta:** Se o recurso aparecer: Dedutível idade_min_anos=18 Bloqueia + Pergunta de exclusão (após a regulação confirmar) + Documento encaminhamento.

### R1 · 4.1.26 Reumatologia — GERAL (p.42)

1. **Seção / página / recurso:** 4.1.26 Reumatologia · p.42 · GERAL
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério (lista ABERTA — “como … Entre outras condições”): Doenças Autoimunes (Suspeita ou Confirmadas) · Lupus Sistêmico · Colagenoses · Artrite Reumatoide · Dermatomiosite · Esclerodermia · Síndrome de Sjogren.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM REUMATOLOGIA GERAL (AE tipo1 v1179)
5. **Nosso:** CONSULTA EM REUMATOLOGIA GERAL (…7d8bf4d9d626)
   - Ativas:
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #563d3e8f
     - [Pergunta/Bloqueia, barra=Não, lista 8 opções] Pacientes que apresentem determinadas patologias, como (CRECE p.42) #96866969 — opções: Doenças Autoimunes (Suspeita ou Confirmadas) | Lupus Sistêmico | Colagenoses | Artrite Reumatoide | Dermatomiosite | Esclerodermia | Síndrome de Sjogren | Outra condição do mesmo grupo (descrever no encaminhamento)
   - Inativas (9):
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Colagenoses; (CRECE p.42) #128349ea
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como (CRECE p.42) #b27fd88e
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Doenças Autoimunes (Suspeita ou Confirmadas); (CRECE p.42) #c4461940
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Entre outras condições (CRECE p.42) #53cdde89
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Síndrome de Sjogren; (CRECE p.42) #b2955358
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Esclerodermia; (CRECE p.42) #866a0e90
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Dermatomiosite; (CRECE p.42) #b918bdd6
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Artrite Reumatoide; (CRECE p.42) #d33adda9
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Lupus Sistêmico; (CRECE p.42) #96b9e8cf
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nada: 7/7 opções literais + “Outra condição do mesmo grupo” (correto, lista aberta). 2 pedidos do SER em “A conferir”.
8. **Proposta:** Manter.

### R2 · 4.1.26 Reumatologia — PEDIATRIA (p.42)

1. **Seção / página / recurso:** 4.1.26 Reumatologia · p.42 · PEDIATRIA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM REUMATOLOGIA - PEDIATRIA (AE tipo1 v1180)
5. **Nosso:** CONSULTA EM REUMATOLOGIA - PEDIATRIA (…9f6919ff) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; 8 pedidos do SER em “Sem regras”.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.42) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### R3 · 4.1.26 Reumatologia — ADOLESCENTE (p.42-43)

1. **Seção / página / recurso:** 4.1.26 Reumatologia · p.42-43 · ADOLESCENTE
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério (p.43; lista com “como”, SEM “entre outras”): Queixas músculo-esqueléticas apresentando sinais e sintomas sugestivos de Doença Reumática · Febre por mais de 1 mês de duração · Artrite acima de 1 semana de duração · Serosites · Vasculites · Ulcerações orais e/ou genitais de repetição · Alterações renais acompanhadas de artrite · Exantema malar · Úlceras · Convulsão · Alterações hematológicas · Fraqueza muscular proximal e simétrica progressiva, principalmente, quando acompanhada de lesões cutâneas.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM REUMATOLOGIA - ADOLESCENTE (AE tipo1 v1177)
5. **Nosso:** CONSULTA EM REUMATOLOGIA - ADOLESCENTE (…c7106c0b) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra. A lista (12 itens, p.43) não aparece no spike — a linha nem foi extraída.
8. **Proposta:** Pergunta de lista (basta uma), rb=Não, Bloqueia: 12 opções literais. Por causa do “como”, acrescentar “Outra condição sugestiva de doença reumática (descrever no encaminhamento)” — para a regulação confirmar (não há “entre outras”). Documento encaminhamento.

### R4 · 4.1.26 Reumatologia — ARTRITE CRÔNICA POR CHICUNGUNYA (p.43)

1. **Seção / página / recurso:** 4.1.26 Reumatologia · p.43 · ARTRITE CRÔNICA POR CHICUNGUNYA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** não achado. Candidatos: CONSULTA EM REUMATOLOGIA GERAL (v1179); CONSULTA EM REUMATOLOGIA - DOENÇA AUTOIMUNE (v1178, que por sua vez não tem linha própria no manual).
5. **Nosso:** —
   - Ativas: nenhuma.
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem recurso próprio no SER.
8. **Proposta:** Nada a cadastrar; o pedido vai pelo recurso geral, cujas regras já valem.

### V1 · 4.1.27 Urologia — GERAL (p.43-44)

1. **Seção / página / recurso:** 4.1.27 Urologia · p.43-44 · GERAL
2. **Estrutura:** CONDICIONAL (eixo: qual condição clínica → qual exame tem de vir anexado)
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério (lista ABERTA — “como … Entre outras condições”), e vários itens trazem o exame exigido embutido: (1) Dificuldades miccionais COM dosagem de PSA e USG de próstata e vias urinárias recentes; (2) urolitíase documentada por tomografia, urografia excretora ou USG com MENOS DE 40 DIAS; (3) lesões que comprometem pênis ou bolsa testicular; (4) patologias cirúrgicas de bolsa testicular sem indicação de tratamento cirúrgico de urgência; (5) achados radiológicos de massas renais, vesicais, testiculares ou falhas de enchimento vesicais, ureterais e de pelve renal documentados em EXAME CONTRASTADO; (6) história de hematúria macroscópica com exclusão de ITU POR UROCULTURA; (7) ITU recorrente com tratamentos adequados e documentados por UROCULTURAS SEQUENCIAIS; (8) patologias prepuciais com dificuldade ou impossibilidade de exposição da glande.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM UROLOGIA GERAL (AE tipo1 v1183)
5. **Nosso:** CONSULTA EM UROLOGIA - GERAL (…3d5fc059) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Nenhuma regra (o spike não extraiu a linha GERAL de Urologia).
   - O documento obrigatório DEPENDE da opção marcada (PSA+USG / imagem ≤40 dias / exame contrastado / urocultura) — o modelo soma documentos com E e não sabe qual opção foi marcada.
8. **Proposta:** HOJE: Pergunta de lista (basta uma), rb=Não, Bloqueia, com os 8 itens literais (cada opção carregando o exame no próprio texto) + “Outra condição urológica (descrever no encaminhamento)”; documentos como caixinhas NÃO obrigatórias (obrigatorio=false): “PSA e USG de próstata e vias urinárias recentes”, “Tomografia, urografia excretora ou USG (menos de 40 dias)” com validade_dias=40, “Exame contrastado”, “Urocultura(s)”; Documento encaminhamento. COM REGRA CONDICIONAL: opção (1)→PSA+USG obrigatórios; (2)→imagem ≤40 dias; (5)→exame contrastado; (6)/(7)→urocultura(s).

### V2 · 4.1.27 Urologia — PEDIÁTRICO (p.44)

1. **Seção / página / recurso:** 4.1.27 Urologia · p.44 · PEDIÁTRICO
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério (lista ABERTA): Anomalias congênitas urogenitais · Patologias obstrutivas das vias urinárias · Criptorquidia · Enurese noturna · Distúrbios de diferenciação sexual (genitália ambígua) · Bexiga neurogênica · Incontinência urinária · Entre outras condições.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM UROLOGIA PEDIATRICA (AE tipo1 v1186)
5. **Nosso:** CONSULTA EM UROLOGIA PEDIATRICA (…f76d293a155e)
   - Ativas:
     - [Pergunta/Bloqueia, barra=Não, lista 8 opções] Pacientes que apresentem determinadas patologias, como (CRECE p.44) #e51a6582 — opções: Anomalias congênitas urogenitais | Patologias obstrutivas das vias urinárias | Criptorquidia | Enurese noturna | Distúrbios de diferenciação sexual (genitália ambígua) | Bexiga neurogênica | Incontinência urinária | Outra condição do mesmo grupo (descrever no encaminhamento)
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #b887a2d5
   - Inativas (9):
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como (CRECE p.44) #15716a49
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Anomalias congênitas urogenitais; (CRECE p.44) #54c0b099
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Patologias obstrutivas das vias urinárias; (CRECE p.44) #5f5039c8
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Enurese noturna; (CRECE p.44) #526040d8
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Incontinência urinária; (CRECE p.44) #355b0df0
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Distúrbios de diferenciação sexual (genitália ambígua)… (CRECE p.44) #4c3f78de
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Bexiga neurogênica; (CRECE p.44) #55ebb33a
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Criptorquidia; (CRECE p.44) #999a4a65
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Entre outras condições (CRECE p.44) #f61476d9
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nada: 7/7 opções literais + “Outra”. 12 pedidos do SER em “A conferir”.
8. **Proposta:** Manter.

### V3 · 4.1.27 Urologia — DISFUNÇÃO SEXUAL (p.44)

1. **Seção / página / recurso:** 4.1.27 Urologia · p.44 · DISFUNÇÃO SEXUAL
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM UROLOGIA - DISFUNCAO SEXUAL (AE tipo1 v1184)
5. **Nosso:** CONSULTA EM UROLOGIA - DISFUNCAO SEXUAL (…e66bd125) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (nem o encaminhamento).
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.44) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### V4 · 4.1.27 Urologia — LITÍASE (p.44)

1. **Seção / página / recurso:** 4.1.27 Urologia · p.44 · LITÍASE
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério: “Pacientes que apresentem cálculo renal: observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM UROLOGIA - LITIASE (AE tipo1 v1189)
5. **Nosso:** CONSULTA EM UROLOGIA - LITIASE (…2fccfb76b2e8)
   - Ativas:
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #64337da3
     - [Pergunta/Bloqueia, barra=Não, v2] Pacientes que apresentem cálculo renal (CRECE p.44) #24ffc0af
   - Inativas (1):
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem cálculo renal: observar os (CRECE p.44) #c8c158e7
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nada: v2 corrigiu o texto cortado. 2 pedidos do SER em “A conferir”. (O prazo de 40 dias da imagem está só na linha GERAL, não aqui.)
8. **Proposta:** Manter.

### V5 · 4.1.27 Urologia — VASECTOMIA (p.44)

1. **Seção / página / recurso:** 4.1.27 Urologia · p.44 · VASECTOMIA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM UROLOGIA - VASECTOMIA (AE tipo1 v1191)
5. **Nosso:** CONSULTA EM UROLOGIA - VASECTOMIA (…915369c0) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra. O manual NÃO traz sexo, idade nem nº de filhos — não inventar Dedutível.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.44) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### V6 · 4.1.27 Urologia — DISFUNÇÃO MICCIONAL (p.45)

1. **Seção / página / recurso:** 4.1.27 Urologia · p.45 · DISFUNÇÃO MICCIONAL
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério (lista ABERTA): Bexiga neurogênica · Disfunção neurogênica do trato urinário inferior com alguma doença congênita ou adquirida associada (Esclerose Múltipla, Parkinson, AVC, Trauma Raquimedular, Espinha Bífida) · Entre outras condições.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM UROLOGIA DISFUNCAO MICCIONAL (AE tipo1 v1188)
5. **Nosso:** CONSULTA EM UROLOGIA DISFUNCAO MICCIONAL (…82d1726a) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (o spike só extraiu a versão pediátrica); 3 pedidos do SER em “Sem regras”.
8. **Proposta:** Pergunta de lista (basta uma), rb=Não, Bloqueia: as 2 opções literais + “Outra condição (descrever no encaminhamento)”; Documento encaminhamento.

### V7 · 4.1.27 Urologia — DISFUNÇÃO MICCIONAL PEDIÁTRICA (p.45)

1. **Seção / página / recurso:** 4.1.27 Urologia · p.45 · DISFUNÇÃO MICCIONAL PEDIÁTRICA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério (lista ABERTA): Bexiga neurogênica · Quadro suspeito ou confirmado de enurese, incontinência urinária, infecção urinária de repetição, sem resposta aos tratamentos convencionais · Entre outras condições.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM UROLOGIA DISFUNCAO MICCIONAL - PEDIATRIA (AE tipo1 v1187)
5. **Nosso:** CONSULTA EM UROLOGIA DISFUNCAO MICCIONAL - PEDIATRIA (…4f2577a112b8)
   - Ativas:
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #f9d59f6b
     - [Pergunta/Bloqueia, barra=Não, lista 3 opções] Pacientes que apresentem determinadas patologias, como (CRECE p.45) #e0167c80 — opções: Bexiga neurogênica | Quadro suspeito ou confirmado de enurese, incontinência urinária, infecção urinária de repetição, sem resposta aos tratamentos convencionais | Outra condição do mesmo grupo (descrever no encaminhamento)
   - Inativas (4):
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como (CRECE p.45) #bf05acd6
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Entre outras condições (CRECE p.45) #7e3dde63
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Quadro suspeito ou confirmado de enurese, incontinênci… (CRECE p.45) #9d24086c
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Bexiga neurogênica; (CRECE p.45) #4bd14a7c
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nada: 2/2 opções literais + “Outra”.
8. **Proposta:** Manter.

### V8 · 4.1.27 Urologia — GINECOLOGIA (p.45)

1. **Seção / página / recurso:** 4.1.27 Urologia · p.45 · GINECOLOGIA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério (lista ABERTA): Incontinência urinária · Prolapso vaginal · Prolapso genital · Entre outras condições.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM UROLOGIA GINECOLOGIA (AE tipo1 v1185)
5. **Nosso:** CONSULTA EM UROLOGIA GINECOLOGIA (…92b37d675f96)
   - Ativas:
     - [Pergunta/Bloqueia, barra=Não, lista 4 opções] Pacientes que apresentem determinadas patologias, como (CRECE p.45) #dc649e60 — opções: Incontinência urinária | Prolapso vaginal | Prolapso genital | Outra condição do mesmo grupo (descrever no encaminhamento)
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #0c73baa1
   - Inativas (5):
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Entre outras condições (CRECE p.45) #618e1981
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Prolapso genital; (CRECE p.45) #affbccfe
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Prolapso vaginal; (CRECE p.45) #3589c4ad
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Incontinência urinária; (CRECE p.45) #d613d5ad
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como (CRECE p.45) #b5ca6a78
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nada: 3/3 opções literais + “Outra”. 12 pedidos do SER em “A conferir”.
8. **Proposta:** Manter.

### V9 · 4.1.27 Urologia — RECONSTRUTORA (p.45-46)

1. **Seção / página / recurso:** 4.1.27 Urologia · p.45-46 · RECONSTRUTORA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM UROLOGIA RECONSTRUTORA (AE tipo1 v1190)
5. **Nosso:** CONSULTA EM UROLOGIA RECONSTRUTORA (…f4e66094) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; 12 pedidos do SER em “Sem regras”.
8. **Proposta:** Documento/Bloqueia “Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.” (fonte CRECE p.45) no canônico ligado ao recurso do SER. Frases “Observar os critérios … de cada prestador” não viram regra. Atenção: um Documento/Bloqueia tira os pedidos do espelho de “Sem regras” e os põe em “A conferir” — se a regulação não quiser isso em recurso só-orientação, cadastrar como Informativa.

### D1 · 4.1.28 Odontologia — CIRURGIA BUCOMAXILOFACIAL (Traumas, fraturas, reconstruções, cistos e tumores) (p.46)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.46 · CIRURGIA BUCOMAXILOFACIAL (Traumas, fraturas, reconstruções, cistos e tumores)
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso e exames radiográficos anteriores.” (dois anexos: encaminhamento do CD + exames radiográficos anteriores)
   - Critério (lista FECHADA — “determinadas necessidades cirúrgicas:”, sem “como”/“entre outras”): Traumatologia complexa maxilofacial para tratamento de traumas e fraturas através da reconstrução dos ossos da face · Cirurgia para patologias dos maxilares para tratamento de cistos e tumores diagnosticados e com indicação cirúrgica · Cirurgias reconstrutivas com utilização de enxertos autógenos e placas de reconstrução · Tratamento de sequelas em geral (Trauma, tumores, deformidades) · Tratamento de infecção oral e maxilofacial severa · Cirurgia pré-protética avançada.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL (AE tipo1 v1135)
5. **Nosso:** CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL (…c2cda374) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra. O spike juntou as três primeiras linhas da Odontologia num falso recurso “ODONTOLOGIA - RECONSTRUTORA” (27 linhas, SEM_PAR) — por isso nada entrou.
   - 1 solicitação no nosso módulo e 2 pedidos do SER em “Sem regras”.
8. **Proposta:** Pergunta de lista (basta uma), rb=Não, Bloqueia, 6 opções literais (sem “Outra”); se a curadoria confirmar que D2 vai para o MESMO recurso, as 2 opções da ATM entram nesta MESMA lista (duas listas somariam com E). Documento “Encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso” + Documento “Exames radiográficos anteriores”. NÃO usar o “encaminhamento médico” global.

### D2 · 4.1.28 Odontologia — CIRURGIA BUCOMAXILOFACIAL (Cirurgia da ATM) (p.46)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.46 · CIRURGIA BUCOMAXILOFACIAL (Cirurgia da ATM)
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso e exames radiográficos anteriores.” (dois anexos: encaminhamento do CD + exames radiográficos anteriores)
   - Critério (lista FECHADA): “Pacientes que apresentam determinadas necessidades cirúrgicas na Articulação Temporomandibular (ATM):” Neuropatias Orofaciais · Reconstrução protética total da ATM.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** sem recurso próprio. Prováveis: CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL (v1135); CONSULTA EM ODONTOLOGIA - DOR ORO-FACIAL (v1137) para “Neuropatias Orofaciais”.
5. **Nosso:** (depende do par) — nenhum dos dois candidatos tem regra.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; par com o SER indefinido.
8. **Proposta:** Curadoria decide o recurso. Se for o BUCO-MAXILO: somar as 2 opções à lista de D1 (uma lista só). Documentos como em D1.

### D3 · 4.1.28 Odontologia — CIRURGIA ORTOGNÁTICA (Cirurgia da ATM) (p.46)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.46 · CIRURGIA ORTOGNÁTICA (Cirurgia da ATM)
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso e exames radiográficos anteriores.” (dois anexos: encaminhamento do CD + exames radiográficos anteriores)
   - Critério: “Pacientes que apresentam alterações congênitas de crescimento facial com necessidade de tratamento cirúrgico de deformidades maxilofaciais.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** não achado. Candidatos: CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL (v1135); TELERRADIOGRAFIA SEM/ TRAÇADO (tipo2 v1102, exame do planejamento).
5. **Nosso:** —
   - Ativas: nenhuma.
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem consulta de ortognática no SER. O rótulo do manual diz “(Cirurgia da ATM)”, mas o conteúdo é deformidade de crescimento facial.
8. **Proposta:** Se a curadoria parear com o BUCO-MAXILO: o critério vira mais uma opção da lista de D1. Senão, nada.

### D4 · 4.1.28 Odontologia — CIRURGIA ORAL MENOR (p.47)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.47 · CIRURGIA ORAL MENOR
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso e exames radiográficos anteriores.” (dois anexos: encaminhamento do CD + exames radiográficos anteriores)
   - Critério (lista FECHADA — “seguintes condições”): Dentes inclusos, retidos ou impactados · Cirurgia de sisos · Dentes supranumerários · Hiperplasias ou regularização de rebordo · Excisão de cálculo salivar · Remoção de cistos e corpos estranhos · Frenectomias.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM ODONTOLOGIA - CIRURGIA ORAL MENOR (AE tipo1 v1136)
5. **Nosso:** CONSULTA EM ODONTOLOGIA - CIRURGIA ORAL MENOR (…f608caef60f1)
   - Ativas:
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #7c2f01e6
     - [Pergunta/Bloqueia, barra=Não, lista 7 opções] Pacientes que apresentam necessidade cirúrgica para tratamento das seguintes condições (CRECE p.47) #69b2d1bf — opções: Cirurgia de sisos | Dentes inclusos, retidos ou impactados | Dentes supranumerários | Remoção de cistos e corpos estranhos | Excisão de cálculo salivar | Hiperplasias ou regularização de rebordo | Frenectomias
   - Inativas (8):
     - [Pergunta/Bloqueia, barra=Não] Frenectomias (CRECE p.47) #9e27b469
     - [Pergunta/Bloqueia, barra=Não] Remoção de cistos e corpos estranhos; (CRECE p.47) #c38b9c29
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentam necessidade cirúrgica para tratamento das seguintes condições (CRECE p.47) #791ddd51
     - [Pergunta/Bloqueia, barra=Não] Dentes inclusos, retidos ou impactados; (CRECE p.47) #a113989f
     - [Pergunta/Bloqueia, barra=Não] Cirurgia de sisos; (CRECE p.47) #d40a2b7e
     - [Pergunta/Bloqueia, barra=Não] Dentes supranumerários; (CRECE p.47) #f4a1711b
     - [Pergunta/Bloqueia, barra=Não] Hiperplasias ou regularização de rebordo; (CRECE p.47) #42d50734
     - [Pergunta/Bloqueia, barra=Não] Excisão de cálculo salivar; (CRECE p.47) #c9bc7686
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - Lista certa (7/7, sem “Outra” — correto).
   - O documento ativo diz “Encaminhamento MÉDICO …” — o manual pede encaminhamento do CIRURGIÃO-DENTISTA e “exames radiográficos anteriores”, que faltam.
8. **Proposta:** Nova versão do documento: “Encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso”; novo Documento “Exames radiográficos anteriores”.

### D5 · 4.1.28 Odontologia — DTM (Disfunção da ATM) (p.47)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.47 · DTM (Disfunção da ATM)
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso e exames radiográficos anteriores.” (dois anexos: encaminhamento do CD + exames radiográficos anteriores)
   - Critério (lista FECHADA — “seguintes condições”): Limitação na abertura de boca ao bocejar; mastigar, falar ou ao usar os maxilares · Dor crônica na região da ATM, ruídos como estalos, crepitações e/ou zumbido na região do ouvido ou próximo às articulações da mandíbula ou musculatura da face cansada, rígida ou tensa · Mandíbula travada, presa ou com a sensação de que a mandíbula “cai” (luxações recorrentes da ATM, dor nas orelhas, têmporas, bochechas ou atrás dos olhos · Dores na cabeça (pressão), com dor tipo choque ou queimação.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** sem recurso com esse nome. Provável: CONSULTA EM ODONTOLOGIA - DOR ORO-FACIAL (AE tipo1 v1137) — confirmar.
5. **Nosso:** CONSULTA EM ODONTOLOGIA - DOR ORO-FACIAL (…759bff7a) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; 1 pedido do SER (DOR ORO-FACIAL) em “Sem regras”. O spike não extraiu esta linha.
8. **Proposta:** Após confirmar o par: Pergunta de lista (basta uma), rb=Não, Bloqueia, 4 opções literais; documentos do Cirurgião-Dentista como em D4.

### D6 · 4.1.28 Odontologia — ENDODONTIA (p.47)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.47 · ENDODONTIA
2. **Estrutura:** CONDICIONAL (eixo: o dente é 3º molar?)
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso e exames radiográficos anteriores.” (dois anexos: encaminhamento do CD + exames radiográficos anteriores)
   - Critério: “Pacientes que apresentam enfermidades da polpa dentária e infecção radicular com necessidade de tratamento endodôntico (Canal) em dentes permanentes com estrutura coronária que permita isolamento absoluto e posterior restauração do elemento dentário.”
   - Condicional: “Tratamentos endodônticos em 3ºs molares (sisos) serão realizados apenas nos casos em que o siso seja pilar para Prótese Parcial Removível ou que tenha migrado e esteja em função com o antagonista.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM ODONTOLOGIA - ENDODONTIA (AE tipo1 v1134)
5. **Nosso:** Recurso ligado (confirmado) a “CONSULTA ODONTOLOGIA - ENDODONTIA” (…32c6bf0d, nome do SISREG) — 0 regras. As regras estão em “CONSULTA EM ODONTOLOGIA - ENDODONTIA” (…1a8e442eed52), cuja única origem é o SISREG “CONSULTA ODONTOLOGIA - ENDODONTIA” — nomes CRUZADOS; regras sistema=SER.
   - Ativas:
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentam enfermidades da polpa dentária e infecção radicular com necessidade de tratamento endodôntico (Canal) em dentes permanentes com estrutura coronária que permita isolamento absoluto e posterior restauração do elemento dentário (CRECE p.47) #a0cc26ae
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #05de0ac4
     - [Informativa/Bloqueia, v2] Tratamentos endodônticos em 3ºs molares (sisos) serão realizados apenas nos casos em que o siso seja pilar para Prótese Parcial Removível ou que tenha migrado e esteja em função com o antagonista (CRECE p.47) #66ba2ac3
   - Inativas (1):
     - [Pergunta/Bloqueia, barra=Não] Tratamentos endodônticos em 3ºs molares (sisos) serão realizados apenas nos casos em que o siso seja pilar par… (CRECE p.47) #46667cc3
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - Regras órfãs: o pedido do SER cai no canônico sem regras; no canônico com regras só entra SISREG, que o filtro sistema=SER descarta.
   - Documento diz “Encaminhamento MÉDICO”; faltam “Cirurgião-Dentista” e “exames radiográficos anteriores”.
   - A cláusula do siso virou Informativa (v2) — certo como paliativo, mas dá para fazer melhor sem condicional (ver proposta).
8. **Proposta:** Recriar no …32c6bf0d e desligar as órfãs. Inclusão como Pergunta Sim/Não rb=Não. A cláusula do siso CABE hoje como EXCLUSÃO: Pergunta “O tratamento pedido é em 3º molar (siso) que NÃO é pilar de Prótese Parcial Removível e NÃO migrou/está em função com o antagonista?” rb=Sim, Bloqueia. Documentos do Cirurgião-Dentista como em D4.

### D7 · 4.1.28 Odontologia — ESTOMATOLOGIA (p.48)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.48 · ESTOMATOLOGIA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso e exames radiográficos anteriores.” (dois anexos: encaminhamento do CD + exames radiográficos anteriores)
   - Critério (lista FECHADA — “as seguintes lesões na região maxilofacial”): Lesões que não cicatrizam em 15 dias na cavidade oral · Tumefação, massa ou tumoração não especificadas na cavidade oral · Doenças da língua, lábio e da mucosa oral · Líquen plano, Estomatites e lesões correlatas · Doença de glândulas salivares · Cistos da região bucal · Halitose · Hiperplasia irritativa da mucosa oral · Leucoplasia e outras afecções do epitélio oral · Xerostomia, queimação e ardência bucal.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM ODONTOLOGIA - ESTOMATOLOGIA (AE tipo1 v1133)
5. **Nosso:** Recurso ligado (confirmado) a “CONSULTA ODONTOLOGIA - ESTOMATOLOGIA” (…f21b9160) — 0 regras. Regras em “CONSULTA EM ODONTOLOGIA - ESTOMATOLOGIA” (…de59687e78a0), origem só SISREG — nomes CRUZADOS; regras sistema=SER.
   - Ativas:
     - [Pergunta/Bloqueia, barra=Não, lista 10 opções] Pacientes que apresentam necessidade de diagnóstico e biópsia para as seguintes lesões na região maxilofacial (CRECE p.48) #a7f6e021 — opções: Lesões que não cicatrizam em 15 dias na cavidade oral | Doenças da língua, lábio e da mucosa oral | Leucoplasia e outras afecções do epitélio oral | Líquen plano, Estomatites e lesões correlatas | Hiperplasia irritativa da mucosa oral | Cistos da região bucal | Tumefação, massa ou tumoração não especificadas na cavidade oral | Doença de glândulas salivares | Xerostomia, queimação e ardência bucal | Halitose
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #d81f5d58
   - Inativas (11):
     - [Pergunta/Bloqueia, barra=Não] Líquen plano, Estomatites e lesões correlatas; (CRECE p.48) #f9914562
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentam necessidade de diagnóstico e biópsia para as seguintes lesões na região maxilofacial (CRECE p.48) #e8964205
     - [Pergunta/Bloqueia, barra=Não] Lesões que não cicatrizam em 15 dias na cavidade oral; (CRECE p.48) #ef45bfd2
     - [Pergunta/Bloqueia, barra=Não] Tumefação, massa ou tumoração não especificadas na cavidade oral; (CRECE p.48) #63a5ead4
     - [Pergunta/Bloqueia, barra=Não] Doenças da língua, lábio e da mucosa oral; (CRECE p.48) #55e6ef22
     - [Pergunta/Bloqueia, barra=Não] Cistos da região bucal; (CRECE p.48) #fbd104fc
     - [Pergunta/Bloqueia, barra=Não] Doença de glândulas salivares; (CRECE p.48) #48873c6d
     - [Pergunta/Bloqueia, barra=Não] Halitose; (CRECE p.48) #5266d386
     - [Pergunta/Bloqueia, barra=Não] Hiperplasia irritativa da mucosa oral; (CRECE p.48) #8677bf3a
     - [Pergunta/Bloqueia, barra=Não] Leucoplasia e outras afecções do epitélio oral; (CRECE p.48) #83c96039
     - [Pergunta/Bloqueia, barra=Não] Xerostomia, queimação e ardência bucal (CRECE p.48) #275b1d5f
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - Regras órfãs (mesmo padrão de D6). A lista em si está certa: 10/10 opções literais, sem “Outra”.
   - Documento diz “Encaminhamento MÉDICO”; faltam “Cirurgião-Dentista” e “exames radiográficos anteriores”.
8. **Proposta:** Recriar a lista no …f21b9160 e desligar as órfãs; documentos do Cirurgião-Dentista como em D4.

### D8 · 4.1.28 Odontologia — PACIENTES PORTADORES DE COMPROMETIMENTO NEUROLÓGICO (p.48)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.48 · PACIENTES PORTADORES DE COMPROMETIMENTO NEUROLÓGICO
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso e exames radiográficos anteriores.” (dois anexos: encaminhamento do CD + exames radiográficos anteriores)
   - Critério (lista ABERTA — “como”): Deficiência mental · Autismo · Esquizofrênica · Transtorno psicótico e neuróptico · Transtorno bipolar, depressivo, fobia, pânico · TOD · Hiperatividade · Paralisia cerebral · Síndrome de Down, West, Rett · Epidermólise bolhose · Eplepsia · Parkinson · Alzheimer · Microcefalia · Hidrocefalia · Encefalopatia · Síndrome Congênita do Zika Vírus.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM ODONTOLOGIA - PACIENTE COM NECESSIDADE ESPECIAL (AE tipo1 v1132) — um recurso só no SER para D8 e D9.
5. **Nosso:** CONSULTA EM ODONTOLOGIA - PACIENTE COM NECESSIDADE ESPECIAL (…096f0dad) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (spike: SEM_PAR).
8. **Proposta:** UMA Pergunta de lista (basta uma) no …096f0dad com a UNIÃO das opções de D8 (17) e D9 (13) + “Outra condição (descrever no encaminhamento)”, rb=Não, Bloqueia. Duas listas separadas somariam com E e exigiriam as duas condições. Documentos do Cirurgião-Dentista como em D4.

### D9 · 4.1.28 Odontologia — PACIENTES PORTADORES DE NECESSIDADES ESPECIAIS (p.48-49)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.48-49 · PACIENTES PORTADORES DE NECESSIDADES ESPECIAIS
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento do Cirurgião-Dentista com a descrição clara e detalhada do caso e exames radiográficos anteriores.” (dois anexos: encaminhamento do CD + exames radiográficos anteriores)
   - Critério (lista ABERTA — “como”, p.49): Pacientes com doenças sistêmicas · Cardiopatias · Doenças pulmonares crônicas · Nefropatias · HIV · Doenças reumatológicas · Antecedente de acidente vascular cerebral há menos de 6 meses · Insuficiência hepática/renal · Transplantados · Irradiados de cabeça e pescoço e/ou complicação de quimioterapia · Problemas hematológicos · Transtornos alimentares (anorexia – bulimia) · Transtorno por uso de drogas.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM ODONTOLOGIA - PACIENTE COM NECESSIDADE ESPECIAL (AE tipo1 v1132) — o mesmo de D8.
5. **Nosso:** CONSULTA EM ODONTOLOGIA - PACIENTE COM NECESSIDADE ESPECIAL (…096f0dad) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (spike: SEM_PAR).
8. **Proposta:** Ver D8 (lista única com a união das duas linhas).

### D10 · 4.1.28 Odontologia — FRENECTOMIA LINGUAL (p.49)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.49 · FRENECTOMIA LINGUAL
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento com a descrição do caso e o resultado do Teste realizado pelo Fonoaudiólogo, Pediatra ou Cirurgião-Dentista.” (dois anexos: encaminhamento + resultado do Teste)
   - Idade: “Pacientes, com até 3 meses de idade, …”
   - Critério: “… que apresentam diagnóstico de anquiloglossia com dificuldade na amamentação.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** FRENECTOMIA LINGUAL - PEDIATRIA (AE tipo2 v1104)
5. **Nosso:** FRENECTOMIA LINGUAL - PEDIATRIA (…2cf8760a) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (spike: SEM_PAR).
   - “Até 3 meses” não cabe em idade_min/max_anos (anos inteiros); idade_max_anos=0 só barra a partir de 1 ano.
8. **Proposta:** Dedutível idade_max_anos=0, Bloqueia (aproximação: barra quem já fez 1 ano); Pergunta Sim/Não “O bebê tem até 3 meses, diagnóstico de anquiloglossia e dificuldade na amamentação?” rb=Não, Bloqueia (cobre o mês); Documento “Resultado do Teste realizado pelo Fonoaudiólogo, Pediatra ou Cirurgião-Dentista”; Documento “Encaminhamento com a descrição do caso”. Idade em meses exata exigiria campo novo.

### D11 · 4.1.28 Odontologia — TOMOGRAFIA COMPUTADORIZADA CONE BEAN (MEDICINA NUCLEAR) (p.49)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.49 · TOMOGRAFIA COMPUTADORIZADA CONE BEAN (MEDICINA NUCLEAR)
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER Pacientes com solicitação/pedido, assinado pelo Cirurgião-Dentista, de Tomografia Computadorizada; devendo conter no pedido a região a ser pesquisada e a finalidade do exame.”
   - Orientação ao paciente: “Paciente deve apresentar o pedido no dia do exame.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** TOMOGRAFIA CONE BEAN (AE tipo2 v1073). Vizinhos sem linha no manual: TOMOGRAFIA COMPUTADORIZADA DE ARTICULACOES TEMPORO-MANDIBULARES (v1068), TOMOGRAFIA COMPUTADORIZADA DE FACE - PEDIATRIA (v1067).
5. **Nosso:** TOMOGRAFIA CONE BEAN (…96f467bf) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (spike: SEM_PAR). Aqui NÃO há “encaminhamento médico” — o documento é o pedido do Cirurgião-Dentista; o encaminhamento global estaria errado.
8. **Proposta:** Documento/Bloqueia “Pedido de Tomografia Computadorizada assinado pelo Cirurgião-Dentista, com a região a ser pesquisada e a finalidade do exame”; Informativa “Paciente deve apresentar o pedido no dia do exame.”

### D12 · 4.1.28 Odontologia — OUTRAS ESPECIALIDADES (GRUPO/RADIOGRAFIA PANORÂMICA) (p.49)

1. **Seção / página / recurso:** 4.1.28 Odontologia · p.49 · OUTRAS ESPECIALIDADES (GRUPO/RADIOGRAFIA PANORÂMICA)
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER Pacientes com solicitação/pedido, assinado pelo Cirurgião-Dentista, de Radiografia Panorâmica de face; devendo conter no pedido a região a ser pesquisada e a finalidade do exame.”
   - Orientação ao paciente: “Paciente deve apresentar o pedido no dia.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** RADIOGRAFIA PANORÂMICA - ODONTOLOGIA (AE tipo2 v1071). “OUTRAS ESPECIALIDADES (GRUPO/…)” pode abranger também RADIOGRAFIA PERIAPICAL - ODONTOLOGIA (v1074) e TELERRADIOGRAFIA (v1102), mas o texto só fala da panorâmica.
5. **Nosso:** RADIOGRAFIA PANORÂMICA - ODONTOLOGIA (…912f9a2a) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (spike: SEM_PAR).
8. **Proposta:** Documento/Bloqueia “Pedido de Radiografia Panorâmica de face assinado pelo Cirurgião-Dentista, com a região a ser pesquisada e a finalidade do exame”; Informativa “Paciente deve apresentar o pedido no dia.”

### G1 · 4.2.1 Cirurgia Geral — APARELHO DIGESTÓRIO (p.50)

1. **Seção / página / recurso:** 4.2.1 Cirurgia Geral · p.50 · APARELHO DIGESTÓRIO
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação: “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador.”
   - Critério (lista ABERTA — “como … Entre outras condições”): Câncer de estômago · Úlcera de estômago e tumores benignos · Patologias do intestino delgado · Câncer de cólon · Doença diverticular do cólon · Megacólon e Doença de Crohn.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA GERAL - APARELHO DIGESTIVO (AE tipo1 v1051)
5. **Nosso:** CONSULTA EM CIRURGIA GERAL - APARELHO DIGESTIVO (…d4757ba3) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra: o spike extraiu (13 linhas) mas ficou SEM_PAR — o SER escreve “DIGESTIVO”, o manual “DIGESTÓRIO”. 6 pedidos do SER em “Sem regras”.
8. **Proposta:** Pergunta de lista (basta uma), rb=Não, Bloqueia: 6 opções literais + “Outra condição (descrever no encaminhamento)”; Documento encaminhamento; “Observar se há necessidade de exames … do prestador” no máximo Informativa.

### G2 · 4.2.1 Cirurgia Geral — ESTÔMAGO (p.50)

1. **Seção / página / recurso:** 4.2.1 Cirurgia Geral · p.50 · ESTÔMAGO
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação: “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador.”
   - Critério (lista ABERTA): Doença do refluxo gastroesofágico · Úlceras, Acalasia e Divertículos · Manifestações atípicas com refluxo devidamente comprovado · Pacientes cirúrgicos com complicações · Entre outras condições.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA GERAL - ESTOMAGO (AE tipo1 v1054)
5. **Nosso:** CONSULTA EM CIRURGIA GERAL - ESTOMAGO (…4c85d667) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; a linha não aparece no spike.
8. **Proposta:** Pergunta de lista (basta uma), rb=Não, Bloqueia: 4 opções literais + “Outra”; Documento encaminhamento.

### G3 · 4.2.1 Cirurgia Geral — ESÔFAGO (p.50)

1. **Seção / página / recurso:** 4.2.1 Cirurgia Geral · p.50 · ESÔFAGO
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação: “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador.”
   - Critério (lista ABERTA): Doença do refluxo gastroesofágico · Úlceras, Acalasia e Divertículos · Não respondem satisfatoriamente ao tratamento clínico, inclusive, aqueles com manifestações atípicas cujo refluxo foi devidamente comprovado · Esôfago de barret, Esteatose, Úlcera e sangramento esofágico · Entre outras condições.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA GERAL - ESOFAGO (AE tipo1 v1053)
5. **Nosso:** CONSULTA EM CIRURGIA GERAL - ESOFAGO (…a89282e36407)
   - Ativas:
     - [Pergunta/Bloqueia, barra=Não, lista 5 opções] Pacientes que apresentem determinadas patologias, como (CRECE p.50) #118819f9 — opções: Doença do refluxo gastroesofágico | Úlceras, Acalasia e Divertículos | Não respondem satisfatoriamente ao tratamento clínico, inclusive, aqueles com manifestações atípicas cujo refluxo foi devidamente comprovado | Esôfago de barret, Esteatose, Úlcera e sangramento esofágico | Outra condição do mesmo grupo (descrever no encaminhamento)
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #a6276b5e
   - Inativas (7):
     - [Pergunta/Bloqueia, barra=Não] Observar se há necessidade de apresentação de exames complementares definidos pelo prestador (CRECE p.50) #7ef4fccd
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como (CRECE p.50) #3012ad0e
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Doença do refluxo gastroesofágico; (CRECE p.50) #ca023013
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Úlceras, Acalasia e Divertículos; (CRECE p.50) #de0765a8
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Não respondem satisfatoriamente ao tratamento clínico,… (CRECE p.50) #f9f15df4
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Esôfago de barret, Esteatose, Úlcera e sangramento eso… (CRECE p.50) #f5cf3684
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Entre outras condições (CRECE p.50) #dd035993
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nada: 4/4 + “Outra”. 1 pedido do SER em “A conferir”.
8. **Proposta:** Manter.

### G4 · 4.2.1 Cirurgia Geral — FÍGADO (p.51)

1. **Seção / página / recurso:** 4.2.1 Cirurgia Geral · p.51 · FÍGADO
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério: “As vagas ofertadas são exclusivamente para pacientes com patologias cirúrgicas de fígado.”
   - Orientação: “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA GERAL - FIGADO (AE tipo1 v1050)
5. **Nosso:** CONSULTA EM CIRURGIA GERAL - FIGADO (…2a23f992) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; a linha não aparece no spike. 1 pedido do SER em “Sem regras”.
8. **Proposta:** Pergunta Sim/Não “O paciente tem patologia cirúrgica de fígado?” rb=Não, Bloqueia (igual ao Pâncreas); Documento encaminhamento.

### G5 · 4.2.1 Cirurgia Geral — HÉRNIA (p.51)

1. **Seção / página / recurso:** 4.2.1 Cirurgia Geral · p.51 · HÉRNIA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação: “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador.”
   - Critério: “Pacientes que apresentam hérnia de parede abdominal:” Hérnia umbilical, inguinal, incisional e/ou epigástrica · Outras hérnias · Diástese associada à hérnia epigástrica e umbilical.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA GERAL - HERNIA (AE tipo1 v1048)
5. **Nosso:** CONSULTA EM CIRURGIA GERAL - HERNIA (…4c10223a) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra; a linha não aparece no spike.
8. **Proposta:** Pergunta de lista (basta uma), rb=Não, Bloqueia, 3 opções literais (o próprio manual já traz “Outras hérnias” — não acrescentar “Outra”); Documento encaminhamento.

### G6 · 4.2.1 Cirurgia Geral — PÂNCREAS (p.51)

1. **Seção / página / recurso:** 4.2.1 Cirurgia Geral · p.51 · PÂNCREAS
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério: “As vagas ofertadas são exclusivamente para pacientes com patologias cirúrgicas de pâncreas.”
   - Orientação: “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA GERAL - PANCREAS (AE tipo1 v1055)
5. **Nosso:** CONSULTA EM CIRURGIA GERAL - PANCREAS (…a7e2858a7593)
   - Ativas:
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #617a00cb
     - [Pergunta/Bloqueia, barra=Não] As vagas ofertadas são exclusivamente para pacientes com patologias cirúrgicas de pâncreas (CRECE p.51) #1521cf9b
   - Inativas (1):
     - [Pergunta/Bloqueia, barra=Não] Observar se há necessidade de apresentação de exames complementares definidos pelo prestador (CRECE p.51) #d25c5d43
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nada.
8. **Proposta:** Manter.

### G7 · 4.2.1 Cirurgia Geral — PARTES MOLES (p.51)

1. **Seção / página / recurso:** 4.2.1 Cirurgia Geral · p.51 · PARTES MOLES
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Orientação: “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador.”
   - Critério: “Pacientes que apresentam lesões de pele benignas.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão: “Lesões malignas.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA GERAL - PARTES MOLES (AE tipo1 v1052)
5. **Nosso:** CONSULTA EM CIRURGIA GERAL - PARTES MOLES (…a1033aa0f62b)
   - Ativas:
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentam lesões de pele benignas (CRECE p.51) #7831ffea
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #a72486f4
     - [Pergunta/Bloqueia, barra=Sim] Lesões malignas (CRECE p.51) #75029544
   - Inativas (1):
     - [Pergunta/Bloqueia, barra=Não] Observar se há necessidade de apresentação de exames complementares definidos pelo prestador (CRECE p.51) #4176caca
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Inclusão (rb=Não) e exclusão (rb=Sim) com o sentido certo. Só a redação da pergunta de exclusão é seca (“Lesões malignas”).
8. **Proposta:** Manter; opcional: nova versão da pergunta como “A lesão é maligna?”.

### G8 · 4.2.1 Cirurgia Geral — VESÍCULA (p.52)

1. **Seção / página / recurso:** 4.2.1 Cirurgia Geral · p.52 · VESÍCULA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério: “As vagas ofertadas são exclusivamente para pacientes com patologias cirúrgicas de vesícula.”
   - Orientação: “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador.”
   - Critério (lista ABERTA — “como”): Colelitíase na vigência de sintomas ou assintomáticos com história prévia de complicações (colecistite, colangite e/ou pancreatite) · Coledocolitíase · Cisto de Colédoco · Pólipo de vesícula.
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA GERAL - VESICULA (AE tipo1 v1049)
5. **Nosso:** CONSULTA EM CIRURGIA GERAL - VESICULA (…3ce0fdcb47cf)
   - Ativas:
     - [Pergunta/Bloqueia, barra=Não, lista 5 opções] Pacientes que apresentem determinadas patologias, como (CRECE p.52) #ed8b7d3a — opções: Colelitíase na vigência de sintomas ou assintomáticos com história prévia de complicações (colecistite, colangite e/ou pancreatite) | Coledocolitíase | Cisto de Colédoco | Pólipo de vesícula | Outra patologia cirúrgica de vesícula (descrever no encaminhamento)
     - [Pergunta/Bloqueia, barra=Não] As vagas ofertadas são exclusivamente para pacientes com patologias cirúrgicas de vesícula (CRECE p.52) #afe22503
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #3220186d
   - Inativas (6):
     - [Pergunta/Bloqueia, barra=Não] Observar se há necessidade de apresentação de exames complementares definidos pelo prestador (CRECE p.52) #af291d6d
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como (CRECE p.52) #a033438e
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Colelitíase na vigência de sintomas ou assintomáticos… (CRECE p.52) #f71b4925
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Coledocolitíase; (CRECE p.52) #7ddf0ce3
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Cisto de Colédoco; (CRECE p.52) #b110a13b
     - [Pergunta/Bloqueia, barra=Não] Pacientes que apresentem determinadas patologias, como: Pólipo de vesícula (CRECE p.52) #70f0155e
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nada: 4/4 + “Outra patologia cirúrgica de vesícula”, e a regra “exclusivamente” separada.
8. **Proposta:** Manter.

### G9 · 4.2.1 Cirurgia Geral — TIREOIDE (p.52)

1. **Seção / página / recurso:** 4.2.1 Cirurgia Geral · p.52 · TIREOIDE
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério: “As vagas ofertadas são exclusivamente para pacientes com patologias cirúrgicas de tireoide.”
   - Orientação: “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador.”
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA GERAL - TIREOIDES (AE tipo1 v1056)
5. **Nosso:** CONSULTA EM CIRURGIA GERAL - TIREOIDES (…77a70f89) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra: spike SEM_PAR (“TIREOIDE” × “TIREOIDES”). 1 pedido do SER em “Sem regras”.
8. **Proposta:** Pergunta Sim/Não “O paciente tem patologia cirúrgica de tireoide?” rb=Não, Bloqueia; Documento encaminhamento.

### G10 · 4.2.1 Cirurgia Geral — GERAL (p.52)

1. **Seção / página / recurso:** 4.2.1 Cirurgia Geral · p.52 · GERAL
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso.”
   - Critério: “As vagas ofertadas são exclusivamente para pacientes com patologias cirúrgicas, critérios de inclusão do paciente de acordo com cada prestador.”
   - Orientação: “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** não achado (não há “CONSULTA EM CIRURGIA GERAL” sem sufixo no ramo AE). O SER tem “CONSULTA EM CIRURGIA GERAL - SUPRA RENAL” (v1059), que não tem linha no manual.
5. **Nosso:** —
   - Ativas: nenhuma.
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Linha do manual sem recurso no SER.
8. **Proposta:** Nada a cadastrar até o recurso existir.

### C1 · 4.2.2 Cirurgia Pediátrica — CIRURGIA PEDIÁTRICA (p.52)

1. **Seção / página / recurso:** 4.2.2 Cirurgia Pediátrica · p.52 · CIRURGIA PEDIÁTRICA
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clínica do caso e anexar os resultados dos exames complementares realizados.” (dois anexos: encaminhamento + resultados dos exames complementares realizados)
   - Critério: “As vagas ofertadas são exclusivamente para pacientes com patologias cirúrgicas, critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento”.
4. **Recurso no SER:** CONSULTA EM CIRURGIA PEDIATRICA (AE tipo1 v1057)
5. **Nosso:** Origem ATIVA (confirmada) do recurso aponta para “CONSULTA EM CIRURGIA GERAL - PEDIATRIA” (…524e1b4d, nome do SISREG) — 0 regras. As regras estão em “CONSULTA EM CIRURGIA PEDIATRICA” (…8d90883a6d71): a origem SER dele está INATIVA e a ativa é o SISREG “CONSULTA EM CIRURGIA GERAL - PEDIATRIA” — nomes CRUZADOS; regras sistema=SER.
   - Ativas:
     - [Pergunta/Bloqueia, barra=Não] As vagas ofertadas são exclusivamente para pacientes com patologias cirúrgicas, (CRECE p.52) #d744dd0b
     - [Pergunta/Bloqueia, barra=Não] Pacientes com indicação para cirurgia otorrinolaringológica (CRECE p.40) #9a8c51b1
     - [Documento/Bloqueia] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (CRECE/REUNI — requisito global) #9c1ba9c0
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - A SEÇÃO EXISTE: 4.2.2, p.52 (a revisão de 01/10 não a achou). Ela não fala de otorrino.
   - Regra ATIVA, Bloqueia: “Pacientes com indicação para cirurgia otorrinolaringológica” (fonte CRECE p.40) — é da Otorrino / CIRURGIA PEDIÁTRICA (L4), não daqui. Somada com E, barraria toda cirurgia pediátrica que não seja otorrino. Causa: o spike pareou “OTORRINOLARINGOLOGIA - CIRURGIA PEDIÁTRICA” com “CONSULTA EM CIRURGIA PEDIATRICA” (pareamento “celula”).
   - Hoje está LATENTE para o SER (canônico órfão), mas ativa: se a origem for reativada ou os canônicos fundidos, passa a barrar.
   - “As vagas … patologias cirúrgicas,” ficou com o texto cortado na vírgula.
   - Falta o anexo “resultados dos exames complementares realizados”; o encaminhamento cadastrado é o genérico “descrição clara e detalhada” (o manual diz “descrição clínica”).
8. **Proposta:** Desligar #9a8c51b1 (otorrino). No canônico que recebe o SER (…524e1b4d): Pergunta Sim/Não “O paciente tem patologia cirúrgica?” rb=Não, Bloqueia, texto literal sem o corte; Documento “Encaminhamento médico com a descrição clínica do caso”; Documento “Resultados dos exames complementares realizados” (Bloqueia ou Ressalva — regulação decide). Desligar as órfãs do …8d90883a6d71.

### K1 · 4.2.3 Cirurgia Plástica — CIRURGIA PLÁSTICA (p.53)

1. **Seção / página / recurso:** 4.2.3 Cirurgia Plástica · p.53 · CIRURGIA PLÁSTICA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clínica do caso e anexar os resultados dos exames complementares realizados.” (dois anexos: encaminhamento + resultados dos exames complementares realizados)
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Critério (lista com “como”, sem “entre outras”): Fendas Labiais e Palatinas com anomalias congênitas craniofaciais, fissura labiopalatina, tumores benignos e malignos · Deformidades congênitas e adquiridas das orelhas · Pálpebras (Blefaroplastia) · Queimados (Tratamento de sequelas iniciais e tardias, reconstrução com expansores, revisão de cicatrizes traumáticas e queloides, etc.).
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** O SER parte a linha em 4 recursos AE: CONSULTA EM CIRURGIA PLASTICA - FENDAS LABIAIS E PALATINAS (v1063) · - ORELHA (v1061) · - QUEIMADOS (v1062) · - TUMOR DE PELE (v1060). “Pálpebras (Blefaroplastia)” não tem recurso.
5. **Nosso:** 4 canônicos homônimos (…392d1b34, …d646624c, …07c7970d, …75fa4e83) — 0 regras em todos.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (a linha não aparece no spike). 16 pedidos do SER (TUMOR DE PELE) em “Sem regras”.
   - “Tumor de pele” não é item do manual: “tumores benignos e malignos” aparece dentro do item de Fendas — para a regulação confirmar se o recurso do SER está coberto.
8. **Proposta:** Como cada item virou um recurso, a lista vira uma Pergunta Sim/Não por canônico, com o texto do item (Fendas / Orelhas / Queimados), rb=Não, Bloqueia. TUMOR DE PELE: só documentos até a regulação decidir. Em todos: Documento “Encaminhamento médico com a descrição clínica do caso” + Documento “Resultados dos exames complementares realizados”.

### K2 · 4.2.4 Cirurgia Torácica — CIRURGIA TORÁCICA (p.53)

1. **Seção / página / recurso:** 4.2.4 Cirurgia Torácica · p.53 · CIRURGIA TORÁCICA
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clínica do caso e anexar os resultados dos exames complementares realizados.” (dois anexos: encaminhamento + resultados dos exames complementares realizados)
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA TORACICA (AE tipo1 v1064)
5. **Nosso:** Origem ativa (confirmada) aponta para “CONSULTA EM CIRURGIA TORACICA - ONCOLOGIA” (…6170a34e) — 0 regras. O canônico “CONSULTA EM CIRURGIA TORACICA” (…e44ad0ab) só tem a origem inativa.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (o spike pareou, mas só extraiu cacos).
   - Par suspeito: consulta de baixa/média complexidade do CRECE confirmada num canônico “- ONCOLOGIA”. 50 pedidos do SER contados como oncologia; se um dia entrarem regras de oncologia (REUNI) nesse canônico, vão cair em pedido do CRECE. 1 solicitação nossa em “CONSULTA EM CIRURGIA TORACICA”.
8. **Proposta:** Rever o par na curadoria. Depois: Documento “Encaminhamento médico com a descrição clínica do caso” + Documento “Resultados dos exames complementares realizados”.

### K3 · 4.2.5 Cirurgia Vascular — CIRURGIA VASCULAR (p.53)

1. **Seção / página / recurso:** 4.2.5 Cirurgia Vascular · p.53 · CIRURGIA VASCULAR
2. **Estrutura:** SIMPLES
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clínica do caso e anexar os resultados dos exames complementares realizados.” (dois anexos: encaminhamento + resultados dos exames complementares realizados)
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Critério (“como”, um item só): “Doenças vasculares sem feridas (Varizes sintomáticas refratárias ao tratamento conservador, tromboses) e Fístulas Arteriovenosas (FAV)” — “sem feridas” funciona como exclusão.
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** CONSULTA EM CIRURGIA VASCULAR - DOENCA VENOSA (AE tipo1 v1066); também CONSULTA CIRURGIA VASCULAR - TRATAMENTO DE VARIZES COM ESPUMA NÃO ESTÉTICO (v1065).
5. **Nosso:** CONSULTA EM CIRURGIA VASCULAR - DOENCA VENOSA (…1090bddb) e CONSULTA CIRURGIA VASCULAR - TRATAMENTO DE VARIZES COM ESPUMA NÃO ESTÉTICO (…06cf5acd) — 0 regras.
   - Ativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (spike: SEM_PAR). 2 pedidos do SER em “Sem regras”.
8. **Proposta:** Pergunta de lista (basta uma), rb=Não: “Varizes sintomáticas refratárias ao tratamento conservador” | “Tromboses” | “Fístula Arteriovenosa (FAV)” | “Outra doença vascular (descrever)”; Pergunta de exclusão “O paciente tem ferida/úlcera vascular?” rb=Sim, Bloqueia; documentos como em K2.

### K4 · 4.2.6 Cirurgia Reparadora — CIRURGIA REPARADORA (p.54)

1. **Seção / página / recurso:** 4.2.6 Cirurgia Reparadora · p.54 · CIRURGIA REPARADORA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual, literal):**
   - Documento: “Inserir no SER o encaminhamento médico com a descrição clínica do caso e anexar os resultados dos exames complementares realizados.” (dois anexos: encaminhamento + resultados dos exames complementares realizados)
   - Orientação (não é requisito): “Observar os critérios de inclusão do paciente de acordo com cada prestador.”
   - Informativo: “Realizado triagem para verificar se o paciente atende aos critérios mínimos para realização de cirurgia reparadora.”
   - Exclusão só remissiva: “Observar os critérios de exclusão de cada prestador e faixa etária para atendimento.” (o manual não fixa idade)
4. **Recurso no SER:** não achado no ramo AE. Candidatos de outro manual/especialidade: Ambulatório 1ª vez em Cirurgia Plástica Reparadora - Mama (Oncologia) (NAO_AE/REUNI); CONSULTA EM UROLOGIA RECONSTRUTORA (v1190).
5. **Nosso:** —
   - Ativas: nenhuma.
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Linha do manual sem recurso AE.
8. **Proposta:** Nada a cadastrar; a triagem seria Informativa.
