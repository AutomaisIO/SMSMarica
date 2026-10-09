# Confronto CRECE p.24–36 (trecho B) × regras de elegibilidade em produção

Gerado em 09/10/2026, só leitura. Manual: *CRECE — Manual do Solicitante, versão 1 (30/11/2022)*, seções 4.1.11 a 4.1.17 (Gastroenterologia, Geriatria, Ginecologia, Hepatologia, Neurologia, Homeopatia, Infectologia). As duas últimas linhas da Infectologia (PEDIATRIA e COINFECÇÃO AIDS/HEPATITE C) estão na p.36 — a tabela atravessa a quebra, então entram aqui. Tabelas conferidas visualmente no PDF (páginas renderizadas), não só no texto extraído.

Fontes de produção: `regras.json` / `ser_recursos.json` / `analise_espelho_por_procedimento.json` (exportação de hoje), `spike-e-manual-regras.csv` (extração antiga) e `conversao-listas/REVISAO.md` (01/10). Ids de regra citados pelos 8 primeiros caracteres. Todas as regras do trecho têm severidade Bloqueia e filtro de sistema = SER (aliás, as 619 regras de produção são todas Bloqueia).

## Resumo

**37 linhas de tabela** no trecho: COBERTO 10, PARCIAL 2, ERRADO 5, AUSENTE 16, SEM_RECURSO_NO_SER 4, NAO_REPRESENTAVEL_HOJE 0.
Das 16 AUSENTE, 6 são linhas só de orientação em que falta apenas o encaminhamento (CIRURGIA POR VIDEOLAPAROSCOPIA, INFANTOPUERPERAL, PEDIATRIA EPILEPSIA, HOMEOPATIA, HOMEOPATIA INFANTIL, HIV/AIDS GESTANTE) — impacto baixo.

O que pesa:

1. **Regra ativa que barra quem não devia — CONSULTA EM NEUROLOGIA (Geral).** A lista `d9ad21dd` (19 condições) junta as linhas "Distúrbios do Movimento" e "Doença do Neurônio Motor" e bloqueia em "Nenhuma destas". Para a Neurologia Geral o manual só pede o encaminhamento. 6 pedidos do espelho em "A conferir".
2. **Regras do CRECE presas em procedimento homônimo de origem SISREG — Gastro Geral (+DII) e Mastologia.** O recurso AE do SER está ligado (vínculo *Confirmado*) a outro procedimento de mesmo nome, sem regra; as regras ficaram no homônimo cuja única origem viva é do SISREG e, com filtro SER, ficam inertes (a análise do espelho do SER não as alcança). Mastologia: 18 pedidos "Sem regras". Risco latente: se os homônimos forem unidos, a lista da Gastro volta a valer carregando as 3 opções da DII.
3. **Gastro Pediatria: 161 pedidos "Sem regras"** (maior volume do trecho). Os 19 critérios caíram no genérico na importação, foram desligados em 01/10 com a nota "precisam ser cadastrados lá" — e não foram.
4. **Subespecialidades vazias:** Distúrbios do Movimento (conteúdo no genérico), Parkinson, Neuro Pediatria (7 pedidos), Gastro DII e Adolescente, Gineco Cirurgia (2 pedidos) — linhas perdidas na extração ou caídas no genérico.
5. **Pareamento por nome falhou e a linha nunca foi importada:** Geriatria ("- ACIMA DE 60 ANOS"), Histeroscopia ("HITEROSCOPIA" no PDF), Videolaparoscopia, Infantopuerperal, Homeopatia (rotulada "ESCLEROSE MÚLTIPLA" pela extração), Coinfecção.

Os cacos "Observar os…/de cada prestador" **não estão** em produção para p.24–36 (os dois "Observar os" que existem são de p.19 e p.44); sobraram só cacos "Pacientes que apresentem condições de acordo com os", todos inativos. Nenhuma linha do trecho é NAO_REPRESENTAVEL_HOJE: o único ponto que pediria regra condicional (idade × item e documento só-se-litíase na Gastro Pediatria) cabe hoje no texto das opções de uma lista.

## Tabela-resumo

| # | Seção | Pág. | Recurso no manual | Estrutura | Recurso no SER (AE) | Regras ativas no recurso | Veredito |
|---|---|---|---|---|---|---|---|
| 1 | 4.1.11 Gastroenterologia | 24 | GERAL | LISTA_BASTA_UM | CONSULTA EM GASTROENTEROLOGIA | 0 (2 no homônimo SISREG, inertes) | **ERRADO** |
| 2 | 4.1.11 Gastroenterologia | 25 | ADOLESCENTE | LISTA_BASTA_UM | CONSULTA EM GASTROENTEROLOGIA - ADOLESCENTE | 0 | **AUSENTE** |
| 3 | 4.1.11 Gastroenterologia | 25 | DOENÇA INFLAMATÓRIA | INCLUSAO_EXCLUSAO | CONSULTA EM GASTROENTEROLOGIA - DOENCA INFLAMATORIA INSTESTINAL | 0 (3 opções na lista da Geral, inerte) | **ERRADO** |
| 4 | 4.1.11 Gastroenterologia | 25 | GASTROSTOMIA (INTERNADOS) | INCLUSAO_EXCLUSAO | CONSULTA EM GASTROENTEROLOGIA - GASTROSTOMIA - INTERNADOS | 5 | **COBERTO** |
| 5 | 4.1.11 Gastroenterologia | 26 | HEPATOLOGIA | LISTA_BASTA_UM | CONSULTA EM GASTROENTEROLOGIA - HEPATOLOGIA | 2 | **COBERTO** |
| 6 | 4.1.11 Gastroenterologia | 26 | PEDIATRA | CONDICIONAL | CONSULTA EM GASTROENTEROLOGIA - PEDIATRIA | 0 | **AUSENTE** |
| 7 | 4.1.12 Geriatria | 27 | GERIATRIA | SIMPLES | CONSULTA EM GERIATRIA - ACIMA DE 60 ANOS | 0 | **AUSENTE** |
| 8 | 4.1.13 Ginecologia | 27 | CIRURGIA DE BAIXO E MÉDIO RISCO | INCLUSAO_EXCLUSAO | CONSULTA EM GINECOLOGIA - CIRURGIA DE BAIXO E MEDIO RISCO | 3 | **COBERTO** |
| 9 | 4.1.13 Ginecologia | 28 | ENDOMETRIOSE | SIMPLES | CONSULTA EM GINECOLOGIA - ENDOMETRIOSE | 2 | **PARCIAL** |
| 10 | 4.1.13 Ginecologia | 28 | HISTEROSCOPIA DIAGNÓSTICA (grafado "HITEROSCOPIA" no PDF) | SIMPLES | CONSULTA EM GINECOLOGIA - HISTEROSCOPIA DIAGNOSTICA | 0 | **AUSENTE** |
| 11 | 4.1.13 Ginecologia | 28 | MASTOLOGIA | SIMPLES | CONSULTA EM MASTOLOGIA | 0 (2 no homônimo SISREG, inertes) | **ERRADO** |
| 12 | 4.1.13 Ginecologia | 28 | PATOLOGIA DA VULVA | SIMPLES | CONSULTA EM GINECOLOGIA - PATOLOGIA VULVA | 2 | **COBERTO** |
| 13 | 4.1.13 Ginecologia | 29 | UROGINECOLOGIA | SO_ORIENTACAO | CONSULTA EM GINECOLOGIA - UROGINECOLOGIA | 1 | **COBERTO** |
| 14 | 4.1.13 Ginecologia | 29 | CIRURGIA POR VIDEOLAPAROSCOPIA | SO_ORIENTACAO | CONSULTA EM GINECOLOGIA CIRURGICA - VIDEOLAPAROSCOPIA | 0 | **AUSENTE** |
| 15 | 4.1.13 Ginecologia | 29 | CIRURGIA | LISTA_BASTA_UM | CONSULTA EM GINECOLOGIA CIRURGICA | 0 | **AUSENTE** |
| 16 | 4.1.13 Ginecologia | 29 | ENDOCRINOLOGIA | LISTA_BASTA_UM | CONSULTA EM GINECOLOGIA - ENDOCRINOLOGIA | 2 | **COBERTO** |
| 17 | 4.1.13 Ginecologia | 30 | INFANTOPUERPERAL | SO_ORIENTACAO | não há "INFANTOPUERPERAL" no SER; candidato: CONSULTA EM GINECOLOGIA ENDOCRINO / INFANTO PUBERAL | 0 | **AUSENTE** |
| 18 | 4.1.13 Ginecologia | 30 | INFERTILIDADE | LISTA_BASTA_UM | CONSULTA EM GINECOLOGIA INFERTILIDADE | 2 | **COBERTO** |
| 19 | 4.1.13 Ginecologia | 30 | PATOLOGIA CERVICAL | SIMPLES | CONSULTA EM GINECOLOGIA - PATOLOGIA CERVICAL | 2 | **COBERTO** |
| 20 | 4.1.13 Ginecologia | 31 | URODINÂMICA | SIMPLES | CONSULTA EM GINECOLOGIA URODINAMICA | 4 | **COBERTO** |
| 21 | 4.1.14 Hepatologia | 31 | HEPATITE CRÔNICA C | LISTA_BASTA_UM | CONSULTA EM HEPATOLOGIA - HEPATITE CRONICA C | 2 | **COBERTO** |
| 22 | 4.1.15 Neurologia | 32 | DISTÚRBIOS DO MOVIMENTO | LISTA_BASTA_UM | CONSULTA EM NEUROLOGIA - DISTURBIO DOS MOVIMENTOS | 0 (itens na lista ativa da Geral) | **ERRADO** |
| 23 | 4.1.15 Neurologia | 32 | DOENÇA DO NEURÔNIO MOTOR | LISTA_BASTA_UM | não achado | 0 (itens na lista ativa da Geral) | **SEM_RECURSO_NO_SER** |
| 24 | 4.1.15 Neurologia | 33 | DOENÇAS NEUROMUSCULARES | SIMPLES | não há no AE; NAO_AE: Consulta em Neurologia- Doenças Neuromusculares | 0 | **AUSENTE** |
| 25 | 4.1.15 Neurologia | 33 | EPILEPSIA | SIMPLES | não achado no AE | 0 | **SEM_RECURSO_NO_SER** |
| 26 | 4.1.15 Neurologia | 33 | PEDIATRIA EPILEPSIA | SO_ORIENTACAO | CONSULTA EM NEUROLOGIA - PEDIATRIA - EPILEPSIA | 0 | **AUSENTE** |
| 27 | 4.1.15 Neurologia | 33-34 | PEDIATRIA | LISTA_BASTA_UM | CONSULTA EM NEUROLOGIA - PEDIATRIA | 0 | **AUSENTE** |
| 28 | 4.1.15 Neurologia | 34 | GERAL | SO_ORIENTACAO | CONSULTA EM NEUROLOGIA | 2 | **ERRADO** |
| 29 | 4.1.15 Neurologia | 34 | TOXINA BOTULÍNICA | SO_ORIENTACAO | não achado | 0 | **SEM_RECURSO_NO_SER** |
| 30 | 4.1.15 Neurologia | 34 | PARKINSON | SIMPLES | CONSULTA EM NEUROLOGIA - PARKINSON | 0 | **AUSENTE** |
| 31 | 4.1.15 Neurologia | 34 | ESCLEROSE MÚLTIPLA | LISTA_BASTA_UM | CONSULTA EM NEUROLOGIA - ESCLEROSE MULTIPLA | 2 | **PARCIAL** |
| 32 | 4.1.16 Homeopatia | 35 | HOMEOPATIA | SO_ORIENTACAO | CONSULTA EM HOMEOPATIA (MAIOR DE 60 ANOS) | 0 | **AUSENTE** |
| 33 | 4.1.16 Homeopatia | 35 | HOMEOPATIA INFANTIL | SO_ORIENTACAO | CONSULTA EM HOMEOPATIA INFANTIL | 0 | **AUSENTE** |
| 34 | 4.1.17 Infectologia | 35 | DOENÇAS TROPICAIS | SO_ORIENTACAO | não achado | 0 | **SEM_RECURSO_NO_SER** |
| 35 | 4.1.17 Infectologia | 35 | HIV/AIDS GESTANTE | SO_ORIENTACAO | CONSULTA EM INFECTOLOGIA - HIV/AIDS - GESTANTE | 0 | **AUSENTE** |
| 36 | 4.1.17 Infectologia | 36 (continuação da tabela) | PEDIATRIA | LISTA_BASTA_UM | CONSULTA EM INFECTOLOGIA - PEDIATRIA | 0 | **AUSENTE** |
| 37 | 4.1.17 Infectologia | 36 (continuação da tabela) | COINFECÇÃO AIDS/HEPATITE C | INCLUSAO_EXCLUSAO | CONSULTA EM INFECTOLOGIA COINFECCAO HIV/HEPATITE VIRAL | 0 | **AUSENTE** |

## Por recurso

### 1. 4.1.11 Gastroenterologia — GERAL (p.24)

1. **Seção / página / recurso:** 4.1.11 Gastroenterologia · p.24 · GERAL
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (literal):**
   - Critério clínico, lista (basta um) — "Pacientes que apresentam as seguintes alterações": Epigastralgia refratária ao uso de inibidor de bomba de prótons; Úlcera péptica que persiste após controle endoscópico (realizado 8 a 12 semanas após início do tratamento) e/ou complicada (passado de hemorragia e/ou estenose); DRGE com manifestações típicas e/ou associada à hérnia hiatal e refratária ao tratamento otimizado por 3 meses; DRGE com esofagite grau C ou D de Los Angeles e/ou complicada com úlcera; Estenose péptica e/ou esôfago de Barrett; Queixas otorrinolaringológicas (pigarro, rouquidão, globus cervical, faringite de repetição) com investigação inicial sugestiva de DRGE e de difícil tratamento; Queixas respiratórias (tosse crônica, asma de início recente, pneumonias de repetição, fibrose pulmonar idiopática) cuja investigação inicial sugira DRGE; Esofagites infeciosas; Esofagites secundárias a doenças dermatológicas; Pancreatite crônica; Lesões nodulares ou císticas do pâncreas; Fibrose cística; Diarréia crônica (> 4 semanas com exame parasitológico negativo); Constipação sem melhora após 12 semanas de tratamento na Atenção Primária; Doença celíaca; Colite microscópica; Pólipos de cólon, exceto: pólipos hiperplásicos < 10 mm no reto ou sigmoide - 1 a 2 adenomas tubulares < 10 mm; Tumores neuroendócrinos gastrointestinais; Sangramento gastrointestinal/anemia ferropriva de origem obscura - Doenças do peritônio (19 itens).
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GASTROENTEROLOGIA (AE tipo1 v1098) → procedimento homônimo 01a07a08-8122-7ef0 (vínculo Confirmado), 0 regras
5. **Nosso:** procedimento CONSULTA EM GASTROENTEROLOGIA 01a07a08-813a-7468 (onde estão as regras; única origem viva = SISREG) × 01a07a08-8122-7ef0 (o do recurso AE, sem regras)
   - Ativas: b87349a6 Pergunta lista (basta uma) "Critérios de inclusão — Geral ou Doença Inflamatória Intestinal (basta um)" — 22 opções: as 19 da Geral + 3 "DII, maiores de 18 anos — Crohn (K50.0 e K50.1) / RCU (K51.1) / Colite Indeterminada (K50.8)"; Bloqueia em "Nenhuma"; filtro sistema=SER; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global") — ambas no 813a-7468
   - Inativas: 20 da seção Geral no 813a-7468 (1 cabeçalho "Pacientes que apresentam as seguintes alterações" + 19 itens soltos, ex.: 1c6e9571, 5db30938, df27d9db…), desligadas na conversão de 01/10
6. **Veredito:** ERRADO
7. **Problemas:**
   - As regras estão no procedimento ERRADO: o recurso AE do SER está ligado (vínculo Confirmado) ao homônimo 8122-7ef0, que não tem regra — pedido do SER cai em "Sem regras". As regras ficaram no 813a-7468, cuja única origem viva é do SISREG, e têm filtro sistema=SER: não disparam na análise dos pedidos do SER e, sem origem SER viva, também não num envio ao SER (inertes). Padrão compatível com o reparo posicional do catálogo de 01/10 (devolve a origem ao procedimento original; a regra fica no procedimento dela).
   - A lista ativa mistura a seção DII (3 opções) — a DII tem recurso próprio no SER.
   - Risco latente: se alguém confirmar o pareamento SISREG↔SER ou unir os homônimos, a lista passa a valer com as opções da DII; se o filtro de sistema for tirado, critério estadual passa a barrar pedido SISREG de Gastro.
8. **Proposta:** No procedimento do recurso AE (8122-7ef0) — ou religando a origem AE ao 813a-7468 —: Pergunta de lista "basta uma" só com as 19 alterações da Geral (sem as 3 da DII), Bloqueia em "Nenhuma destas"; + Documento encaminhamento. Depois desativar b87349a6 no 813a-7468. Representável hoje.

### 2. 4.1.11 Gastroenterologia — ADOLESCENTE (p.25)

1. **Seção / página / recurso:** 4.1.11 Gastroenterologia · p.25 · ADOLESCENTE
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (literal):**
   - Critério clínico, lista (basta um) — "Pacientes que apresentam as seguintes alterações": Adolescentes com hepatopatia aguda ou crônica; Ascite; Doenças inflamatórias intestinais em investigação ou diagnosticadas; Esofagite; Suspeita de Neoplasia do TGI; Constipação grave.
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra (inclusive faixa etária: o manual não define a idade de "adolescente")
4. **Recurso no SER:** CONSULTA EM GASTROENTEROLOGIA - ADOLESCENTE (AE tipo1 v1102) → 01a07a08-8137-7320, 0 regras
5. **Nosso:** procedimento CONSULTA EM GASTROENTEROLOGIA - ADOLESCENTE 01a07a08-8137-7320
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra, nem inativa: a linha não existe na extração antiga (spike-e-manual-regras.csv) — perdida antes do pareamento.
   - Faixa etária "adolescente" não está no manual nem no rótulo do SER.
8. **Proposta:** Pergunta de lista "basta uma" com as 6 alterações (Bloqueia em "Nenhuma") + Documento encaminhamento. Não cadastrar Dedutível de idade sem a regulação dizer a faixa (12–18 seria inferência); no máximo Aviso.

### 3. 4.1.11 Gastroenterologia — DOENÇA INFLAMATÓRIA (p.25)

1. **Seção / página / recurso:** 4.1.11 Gastroenterologia · p.25 · DOENÇA INFLAMATÓRIA
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - Idade — "Critério Atendimento exclusivamente para maiores de 18 anos" (Dedutível; "maiores de 18" = ≥18 ou >18 a confirmar).
   - Documento — "É obrigatório encaminhamento médico preenchido, assinado e carimbado pelo médico solicitante".
   - Critério/CID, lista (basta um): Doença de Crohn (CID K50.0 e K50.1); Retocolite Ulcerativa (CID K51.1); Colite Indeterminada (CID K50.8).
   - Exclusão — "Quadro sindrômicos SEM diagnóstico de Doença Inflamatório Intestinal (DII), outras formas de colite e outras doenças do Trato Intestinal (TI) alto e baixo".
   - Exclusão — "Pacientes ainda em investigação de quadros sindrômicos, NÃO diagnosticados com Doença II".
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GASTROENTEROLOGIA - DOENCA INFLAMATORIA INSTESTINAL (AE tipo1 v1100; grafado "INSTESTINAL" no SER) → 01a07a08-813a-7869, 0 regras
5. **Nosso:** procedimento CONSULTA EM GASTROENTEROLOGIA - DOENCA INFLAMATORIA INSTESTINAL 01a07a08-813a-7869 (vazio); conteúdo no homônimo de Gastro Geral 813a-7468
   - Ativas: No procedimento certo: nenhuma.; No 813a-7468 (inerte): 3 opções "DII, maiores de 18 anos — …" dentro da lista b87349a6 da Gastro Geral
   - Inativas: No 813a-7468: 296b48c1 Dedutível idade ≥18; 450ed419 e d8a83269 exclusões (Sim bloqueia); ce80fc47 (Crohn), cab6a4e5 (RCU), a37e2d9a (Colite indeterminada) como perguntas soltas; 62f705e8 "encaminhamento preenchido, assinado e carimbado"; 857e76bb cabeçalho
6. **Veredito:** ERRADO
7. **Problemas:**
   - A seção caiu no procedimento da Gastro Geral. A conversão de 01/10 dobrou a idade para dentro do texto da opção e desligou as exclusões porque barravam a Geral — certo lá, mas a DII tem recurso próprio no SER (a REVISAO.md já dizia "se a DII também tiver recurso próprio, o mesmo vale") e nada foi recriado nele.
   - Os CIDs do manual são estreitos e não batem com a CID-10 (K51.1 é só uma subcategoria da retocolite; "colite indeterminada" não é K50.8): um Dedutível de CID com esses códigos BARRARIA K50.9, K51.0, K51.9 etc.
8. **Proposta:** No 813a-7869: (a) Dedutível idade_min 18, Bloqueia; (b) Documento "Encaminhamento médico preenchido, assinado e carimbado"; (c) Pergunta de lista Crohn | Retocolite ulcerativa | Colite indeterminada, Bloqueia em "Nenhuma"; (d) Pergunta de exclusão "Quadro sindrômico ainda em investigação, sem diagnóstico de DII (ou outra colite/doença do TI)?" Sim bloqueia; (e) opcional: Dedutível CID permitidos por PREFIXO K50/K51 (e K52) com severidade Ressalva — nunca Bloqueia com os subcódigos literais. Tudo representável hoje.

### 4. 4.1.11 Gastroenterologia — GASTROSTOMIA (INTERNADOS) (p.25)

1. **Seção / página / recurso:** 4.1.11 Gastroenterologia · p.25 · GASTROSTOMIA (INTERNADOS)
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico preenchido, assinado e carimbado.
   - Critério — risco cirúrgico liberado para o procedimento.
   - Documento/valor — exames laboratoriais dos últimos 15 dias: hemograma completo, "creatina" [sic], TAP e PTT.
   - Critério — obrigatoriamente internado em outra unidade e encaminhado por ambulância com médico.
   - Exclusão — pacientes que já possuem gastrostomia.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GASTROENTEROLOGIA - GASTROSTOMIA - INTERNADOS (AE tipo2 v1062) → 01a07a08-8133-768b (duas origens AE, uma inativa)
5. **Nosso:** procedimento CONSULTA EM GASTROENTEROLOGIA - GASTROSTOMIA - INTERNADOS 01a07a08-8133-768b
   - Ativas: 38b8e11a Pergunta "Pacientes que já possuem gastrostomia" — Sim bloqueia; 534ec47e Pergunta risco cirúrgico liberado — Não bloqueia; ef8b4b26 Pergunta exames dos últimos 15 dias — Não bloqueia; 1fd7d4c9 Pergunta internado + ambulância com médico — Não bloqueia; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: 542b3f6e "É obrigatório encaminhamento médico preenchido, assinado e carimbado" (duplicata)
6. **Veredito:** COBERTO
7. **Problemas:**
   - Menores: exames de 15 dias e risco cirúrgico são Pergunta (não pedem anexo); "assinado e carimbado" sumiu ao desligar a duplicata. O catálogo mostra "10 regras ativas" porque o recurso aponta duas vezes para o mesmo procedimento.
8. **Proposta:** Opcional: trocar ef8b4b26 por Documento "Hemograma completo, creatinina, TAP e PTT" com validade_dias=15; Documento "Risco cirúrgico liberado"; ajustar o rótulo do encaminhamento para "preenchido, assinado e carimbado".

### 5. 4.1.11 Gastroenterologia — HEPATOLOGIA (p.26)

1. **Seção / página / recurso:** 4.1.11 Gastroenterologia · p.26 · HEPATOLOGIA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (literal):**
   - Critério clínico, lista (basta um): Esquistossomose hepatoesplênica; Hepatite autoimune; Hepatite B Crônica; Doença gordurosa não alcoólica-hepática; Síndromes colestáticas crônicas; Hepatopatias a esclarecer; Hepatites agudas.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra (esta linha não repete o "Inserir no SER o encaminhamento"; a regra global cobre)
4. **Recurso no SER:** CONSULTA EM GASTROENTEROLOGIA - HEPATOLOGIA (AE tipo1 v1099) → 01a07a08-8135-7868 (Confirmado)
5. **Nosso:** procedimento CONSULTA EM GASTROENTEROLOGIA - HEPATOLOGIA 01a07a08-8135-7868
   - Ativas: e2994758 Pergunta lista 7 opções — Bloqueia em "Nenhuma"; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: 7 itens soltos + 1 cabeçalho (fa0f8cd9, 0f066e52, 39a79863, ceb1693e, b69792ae, a6b0bb8f, e9433aac, 11eab52b)
6. **Veredito:** COBERTO
7. **Problemas:**
   - Nenhum. A extração em texto colou "HEPATOLOGIA PEDIATRA", mas no PDF são duas linhas e a lista desta caiu no recurso certo. 56 pedidos do espelho em "A conferir".
8. **Proposta:** Manter.

### 6. 4.1.11 Gastroenterologia — PEDIATRA (p.26)

1. **Seção / página / recurso:** 4.1.11 Gastroenterologia · p.26 · PEDIATRA
2. **Estrutura:** CONDICIONAL (eixo idade, dentro de uma LISTA_BASTA_UM)
3. **Requisitos (literal):**
   - Critério clínico, lista (basta um): suspeita de alergia alimentar com sintomas digestivos (vômitos, diarreia, sangramento nas fezes, etc.); baixa estatura desde que previamente avaliada pela endocrinologia; constipação crônica não responsiva ao tratamento inicial do Pediatra Geral; encoprese; diarreia > 01 mês com parasitológico negativo; dor abdominal > 02 meses e/ou ≥ 3 episódios em 02 meses, com parasitológico negativo; dor epigástrica ou gastrite não responsiva a antagonista H2 e com parasitológico negativo; fibrose cística com comprometimento do trato digestivo; hemorragia digestiva alta ou baixa; hepatites desde que IgM negativo para Hepatite A ou IgM positivo por > 2 meses; hepatoesplenomegalia; hipertensão porta.
   - Itens com eixo de IDADE: Icterícia — "Todos os pacientes com aumento de bilirrubina direta" / "Criancas > - 2 anos com aumento de bilirrubina indireta"; Refluxo gastroesofágico — "Todos os pacientes maiores de 2 anos" / "menores de 2 anos que apresentam algum dos sintomas: anemia não responsiva a reposição de ferro, déficit ponderal, irritabilidade, hemorragia digestiva, sintomas respiratórios recorrentes, recusa alimentar, vômitos persistentes > 10 dias e/ou vômitos recorrentes".
   - Documento CONDICIONAL — "Litíase biliar (Apresentar ultrassonografia abdominal" (o USG só é exigido se o motivo for litíase).
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GASTROENTEROLOGIA - PEDIATRIA (AE tipo1 v1101) → 01a07a08-8134-7ad0, 0 regras — 161 pedidos do espelho em "Sem regras" (maior volume do trecho)
5. **Nosso:** procedimento CONSULTA EM GASTROENTEROLOGIA - PEDIATRIA 01a07a08-8134-7ad0 (vazio); conteúdo inativo no homônimo de Gastro Geral 813a-7468
   - Ativas: nenhuma
   - Inativas: 19 no 813a-7468 (fonte CRECE p.26): 2e549c6d, 6e943ef4, af36cdd7, 979d5665, 3709b54c, 6aea6ff7, 93b8cd58, ab1e18e4, 8d1ead79, a2f52c56, 4df122f2, 69e7c89f, 1f6b275e (cabeçalho Icterícia), 30cdd810, 836a12df, 82f5b3d4 (Documento litíase), c5e4057a (cabeçalho Refluxo), 3dc13720 (Dedutível idade ≥2), 9cf760e2 (Dedutível idade ≤2)
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Achado confirmado: a extração pareou a seção com "CONSULTA EM GASTROENTEROLOGIA" (genérico). Somadas com E, as Dedutíveis ≥2 e ≤2 (mais a ≥18 da DII) não deixavam ninguém passar.
   - A conversão de 01/10 desligou tudo e anotou "os 17 critérios precisam ser cadastrados lá" — não foi feito: o recurso certo tem 0 regras e 161 pedidos sem análise.
8. **Proposta:** No 8134-7ad0: Pergunta de lista "basta uma" com o eixo de idade escrito na opção (quem responde é gente): "Icterícia com aumento de bilirrubina direta (qualquer idade)", "Icterícia com bilirrubina indireta aumentada em criança ≥ 2 anos", "Refluxo gastroesofágico em maior de 2 anos", "Refluxo em menor de 2 anos com [sintomas]", "Litíase biliar (com USG abdominal)" + os demais itens; Bloqueia em "Nenhuma"; + Documento encaminhamento. NÃO recriar as Dedutíveis de idade soltas (barram com E). Exigiria regra condicional: a máquina deduzir idade × opção e o "Documento USG abdominal obrigatório só se litíase" — hoje, no máximo uma Informativa "Se litíase biliar, anexar USG abdominal".

### 7. 4.1.12 Geriatria — GERIATRIA (p.27)

1. **Seção / página / recurso:** 4.1.12 Geriatria · p.27 · GERIATRIA
2. **Estrutura:** SIMPLES (idade) + LISTA_BASTA_UM (somadas com E)
3. **Requisitos (literal):**
   - Idade — "Pacientes acima de 60 anos".
   - Critério clínico, lista (basta um) — "Pacientes que apresentem": Alzheimer; Parkinson; AVC Recente; Depressão e demência; Desequilíbrio e quedas frequentes.
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GERIATRIA - ACIMA DE 60 ANOS (AE tipo1 v1103) → 01a07a08-813d-7ca4, 0 regras
5. **Nosso:** procedimento CONSULTA EM GERIATRIA - ACIMA DE 60 ANOS 01a07a08-813d-7ca4
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nunca importada: a extração antiga tinha 11 linhas (inclusive Dedutível idade 60) marcadas SEM_PAR — o rótulo do SER tem o sufixo "- ACIMA DE 60 ANOS" e o pareamento por nome falhou.
   - "Acima de 60" é >60 ou ≥60? O modelo só tem anos inteiros (idade_min 60 aceita quem tem 60).
8. **Proposta:** Dedutível idade_min_anos=60, Bloqueia (confirmar ≥60 × 61); Pergunta de lista com as 5 condições, Bloqueia em "Nenhuma" (ou Ressalva se a regulação ler a lista como exemplos); + encaminhamento. Representável hoje — o E entre idade e lista é o que o manual diz (na Fisiatria a REVISAO.md fez o contrário, idade virou opção; confirmar com a regulação).

### 8. 4.1.13 Ginecologia — CIRURGIA DE BAIXO E MÉDIO RISCO (p.27)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.27 · CIRURGIA DE BAIXO E MÉDIO RISCO
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - Critério, lista (basta um) — "Pacientes com indicação cirúrgica para resolução das seguintes patologias": Miomatose uterina; Massas anexiais benignas; Sangramento uterino anormal; Adenomiose.
   - Exclusão — "Pacientes portadores de Tumores Malignos Ginecológicos, Pólipos Endometriais, Endometriose Pélvica ou de Parede, Patologias da Vulva ou Cervicais, Distúrbios do Assoalho Pélvico e/ou Incontinência Urinária".
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA - CIRURGIA DE BAIXO E MEDIO RISCO (AE tipo1 v1104) → 01a07a08-8131-78a4
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA - CIRURGIA DE BAIXO E MEDIO RISCO 01a07a08-8131-78a4
   - Ativas: a8a24f7a Pergunta lista 4 opções — Bloqueia em "Nenhuma"; eef4a61c Pergunta de exclusão (texto literal) — Sim bloqueia; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: 4 itens soltos + 1 cabeçalho (2abc044e, 55b27c80, 93ed8878, b5cb47ac, 72090ea2)
6. **Veredito:** COBERTO
7. **Problemas:**
   - Menor: o texto da pergunta da lista ("O paciente tem ao menos uma das condições abaixo?") perde "com indicação cirúrgica" — fica só no título.
8. **Proposta:** Nova versão do texto: "Há indicação cirúrgica para uma destas patologias?".

### 9. 4.1.13 Ginecologia — ENDOMETRIOSE (p.28)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.28 · ENDOMETRIOSE
2. **Estrutura:** SIMPLES (documentos + conteúdo do encaminhamento)
3. **Requisitos (literal):**
   - Documentos a anexar no SER — "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso e os resultados dos exames de RNM pélvica, USG transvaginal e Preventivo".
   - Orientação para o dia da consulta + conteúdo do encaminhamento — "Apresentar na consulta os exames de RNM pélvica, USG transvaginal, Preventivo e encaminhamento médico com o diagnóstico de endometriose".
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA - ENDOMETRIOSE (AE tipo1 v1106) → 01a07a08-813d-7457
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA - ENDOMETRIOSE 01a07a08-813d-7457
   - Ativas: eae8065f Documento "Apresentar na consulta os exames de RNM pélvica, USG transvaginal, Preventivo e encaminhamento médico com o diagnóstico de endometriose"; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: nenhuma
6. **Veredito:** PARCIAL
7. **Problemas:**
   - A regra ativa usa o texto da orientação do dia da consulta e junta três exames + encaminhamento numa caixinha só; o requisito de ANEXAR no SER (1ª linha) não virou regra própria por exame.
   - "Diagnóstico de endometriose" no encaminhamento não é perguntado. 11 pedidos do espelho em "A conferir".
8. **Proposta:** 3 Documentos (RNM pélvica; USG transvaginal; Preventivo/colpocitologia), Bloqueia; Pergunta "O encaminhamento traz o diagnóstico de endometriose?" Não bloqueia; Informativa "Levar à consulta RNM, USG TV, Preventivo e encaminhamento"; desativar eae8065f.

### 10. 4.1.13 Ginecologia — HISTEROSCOPIA DIAGNÓSTICA (grafado "HITEROSCOPIA" no PDF) (p.28)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.28 · HISTEROSCOPIA DIAGNÓSTICA (grafado "HITEROSCOPIA" no PDF)
2. **Estrutura:** SIMPLES (documentos com validade) + LISTA_BASTA_UM
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Documento com validade — "Colpocitologia oncótica (Preventivo) com validade de 12 meses".
   - Documento com validade — "USG transvaginal com validade de 6 meses".
   - Critério clínico — "Pacientes que apresentem queixas de sangramento, de dor pélvica e infertilidade" ("e" literal; leitura clínica é "ou").
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA - HISTEROSCOPIA DIAGNOSTICA (AE tipo1 v1109), 0 regras — já há 1 solicitação nossa neste procedimento
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA - HISTEROSCOPIA DIAGNOSTICA
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nunca importada: SEM_PAR na extração antiga por causa da grafia "HITEROSCOPIA" do próprio PDF.
8. **Proposta:** Documento "Colpocitologia oncótica (Preventivo)" validade_dias=365; Documento "USG transvaginal" validade_dias=180; Pergunta de lista sangramento | dor pélvica | infertilidade (confirmar com a regulação que é "basta uma"), Bloqueia em "Nenhuma"; + encaminhamento. Representável hoje (validade_dias existe no modelo).

### 11. 4.1.13 Ginecologia — MASTOLOGIA (p.28)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.28 · MASTOLOGIA
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Critério/exame — "Pacientes que apresentem exames de mamografia com BI-RADS 4 ou BI-RADS 5".
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM MASTOLOGIA (AE tipo1 v1067) → 01a07a08-8121-7b9c (vínculo Confirmado, ativo), 0 regras; a ligação com 01a07a08-813a-7977 está INATIVA
5. **Nosso:** procedimento CONSULTA EM MASTOLOGIA 01a07a08-813a-7977 (onde está a regra; origem SISREG ativa + origem AE inativa) × 01a07a08-8121-7b9c (o do recurso AE, sem regras)
   - Ativas: 1f57d0e0 Pergunta "Pacientes que apresentem exames de mamografia com BI-RADS 4 ou BI-RADS 5" — Não bloqueia; filtro sistema=SER; no 813a-7977; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global") — no 813a-7977
   - Inativas: nenhuma
6. **Veredito:** ERRADO
7. **Problemas:**
   - Regra presa num procedimento que o SER não alcança mais (mesmo padrão da Gastro Geral): 18 pedidos do espelho em "Sem regras" (1 "A conferir", provavelmente de antes do religamento). Com o filtro SER, também não dispara no SISREG — inerte.
   - O manual pede a mamografia BI-RADS 4/5 mas não diz "anexar o laudo" — não há Documento.
8. **Proposta:** Recriar a Pergunta (Sim/Não, Não bloqueia) e o encaminhamento no 8121-7b9c (ou mover as regras) e desativar no 813a-7977. Opcional: Documento "Laudo da mamografia (BI-RADS 4 ou 5)" como Ressalva — vai além do literal.

### 12. 4.1.13 Ginecologia — PATOLOGIA DA VULVA (p.28)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.28 · PATOLOGIA DA VULVA
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Critério clínico — "lesões na vulva, verruga vulvar, úlceras vulvares, hipercromia, hipocromia, suspeita de líquen, suspeita de câncer vulvar, etc." (lista aberta).
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA - PATOLOGIA VULVA (AE tipo1 v1107) → 01a07a08-8137-7479
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA - PATOLOGIA VULVA 01a07a08-8137-7479
   - Ativas: c60184f7 Pergunta Sim/Não com o texto literal — Não bloqueia; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: nenhuma
6. **Veredito:** COBERTO
7. **Problemas:**
   - Nenhum (lista aberta "etc." corretamente como pergunta única).
8. **Proposta:** Manter.

### 13. 4.1.13 Ginecologia — UROGINECOLOGIA (p.29)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.29 · UROGINECOLOGIA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - "Pacientes que apresentem condições de acordo com os critérios de inclusão de cada prestador" — orientação.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA - UROGINECOLOGIA (AE tipo1 v1108) → 01a07a08-8131-7ec4
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA - UROGINECOLOGIA 01a07a08-8131-7ec4
   - Ativas: Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: 093dcdc1 caco "Pacientes que apresentem condições de acordo com os"
6. **Veredito:** COBERTO
7. **Problemas:**
   - Nenhum. 4 pedidos em "A conferir" (o Documento pede gente).
8. **Proposta:** Manter.

### 14. 4.1.13 Ginecologia — CIRURGIA POR VIDEOLAPAROSCOPIA (p.29)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.29 · CIRURGIA POR VIDEOLAPAROSCOPIA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - "condições de acordo com os critérios de inclusão de cada prestador" — orientação.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA CIRURGICA - VIDEOLAPAROSCOPIA (AE tipo1 v1116), 0 regras
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA CIRURGICA - VIDEOLAPAROSCOPIA
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Só falta o encaminhamento (impacto baixo). SEM_PAR na extração antiga (nome diferente no SER).
8. **Proposta:** Documento encaminhamento.

### 15. 4.1.13 Ginecologia — CIRURGIA (p.29)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.29 · CIRURGIA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Critério, lista (basta um): "Pacientes portadores de doenças benignas do trato genital para tratamento cirúrgico"; "Pacientes portadores de doenças benignas da pelve para tratamento cirúrgico".
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA CIRURGICA (AE tipo1 v1117) → procedimento "CONSULTA EM CIRURGIA GINECOLOGICA" 01a07a08-811b-7535 (Confirmado), 0 regras — 2 pedidos "Sem regras"
5. **Nosso:** procedimento CONSULTA EM CIRURGIA GINECOLOGICA 01a07a08-811b-7535
   - Ativas: nenhuma
   - Inativas: Nenhuma aqui. O MESMO texto está, por erro do PDF, na linha TOXINA BOTULÍNICA (p.34) e virou 2 regras inativas em CONSULTA EM NEUROLOGIA (015bb6f5, 2980c292).
6. **Veredito:** AUSENTE
7. **Problemas:**
   - A linha não existe na extração antiga — perdida.
8. **Proposta:** Pergunta de lista trato genital | pelve ("doença benigna com indicação de tratamento cirúrgico"), Bloqueia em "Nenhuma"; + encaminhamento.

### 16. 4.1.13 Ginecologia — ENDOCRINOLOGIA (p.29)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.29 · ENDOCRINOLOGIA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Critério, lista (basta um): Síndrome do Ovário Policístico; Puberdade precoce e tardia; Hiperprolactinemia/Galactorréia; Síndrome do climatério; Menopausa precoce e tardia; Ginecologia da adolescência (a partir de 11 anos de idade); Irregularidade menstrual.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA - ENDOCRINOLOGIA (AE tipo1 v1114) → 01a07a08-8139-7785
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA - ENDOCRINOLOGIA 01a07a08-8139-7785
   - Ativas: c72af97c Pergunta lista 7 opções — Bloqueia em "Nenhuma"; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: 6 itens + 1 cabeçalho; e5078cd3 Dedutível idade ≥11 (corretamente inativa: barrava menor de 11 com outra patologia)
6. **Veredito:** COBERTO
7. **Problemas:**
   - Nenhum; a REVISAO.md deixou para a regulação confirmar a idade 11 como opção.
8. **Proposta:** Manter.

### 17. 4.1.13 Ginecologia — INFANTOPUERPERAL (p.30)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.30 · INFANTOPUERPERAL
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - "condições de acordo com os critérios de inclusão de cada prestador" — orientação.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** não há "INFANTOPUERPERAL" no SER; candidato: CONSULTA EM GINECOLOGIA ENDOCRINO / INFANTO PUBERAL (AE tipo1 v1105), 0 regras
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA ENDOCRINO / INFANTO PUBERAL (se confirmado o par)
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Só falta o encaminhamento. Nome diverge ("puerperal" × "puberal" — provável erro do manual); SEM_PAR na extração antiga.
8. **Proposta:** Confirmar o par e cadastrar o Documento encaminhamento.

### 18. 4.1.13 Ginecologia — INFERTILIDADE (p.30)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.30 · INFERTILIDADE
2. **Estrutura:** LISTA_BASTA_UM (item 2 com eixo idade no próprio texto)
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Critério, lista (basta um): "Casais com diagnóstico de infertilidade conjugal para investigação da etiologia ou para proposta de tratamentos com base em diagnósticos previamente realizados"; "História de infertilidade (primária ou secundária) há mais de 1 ano se mulher com menos de 35 anos, ou 6 meses se mais de 35 anos".
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA INFERTILIDADE (AE tipo1 v1112) → 01a07a08-813e-75ba
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA INFERTILIDADE 01a07a08-813e-75ba
   - Ativas: 440dfd66 Pergunta lista 2 opções — Bloqueia em "Nenhuma"; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: 92d44812 cabeçalho; e7f6aab3 item; 32b7330d Dedutível sexo F (corretamente inativa: o critério é do casal)
6. **Veredito:** COBERTO
7. **Problemas:**
   - O prazo por idade (1 ano se < 35 / 6 meses se > 35) fica no texto da opção — a máquina não deduz (exigiria condicional), mas quem responde é gente: aceitável. 20 pedidos em "A conferir".
8. **Proposta:** Manter.

### 19. 4.1.13 Ginecologia — PATOLOGIA CERVICAL (p.30)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.30 · PATOLOGIA CERVICAL
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Critério — "alterações citológicas de acordo com as diretrizes para rastreamento do câncer do colo do útero MS/INCA 2011".
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA - PATOLOGIA CERVICAL (AE tipo1 v1110) → 01a07a08-813b-714e
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA - PATOLOGIA CERVICAL 01a07a08-813b-714e
   - Ativas: 17a0bc59 Pergunta Sim/Não literal — Não bloqueia; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: nenhuma
6. **Veredito:** COBERTO
7. **Problemas:**
   - Nenhum.
8. **Proposta:** Manter.

### 20. 4.1.13 Ginecologia — URODINÂMICA (p.31)

1. **Seção / página / recurso:** 4.1.13 Ginecologia · p.31 · URODINÂMICA
2. **Estrutura:** SIMPLES (2 critérios somados) + LISTA_BASTA_UM
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Critério — "sintomas urinários (incontinência urinária, frequência, urgência, nictúria, enurese) avaliadas pelo Ginecologista Geral com indicação de Estudo Urodinâmico".
   - Documento/exame — "Possuir exame de urina (EAS+Urinocultura) negativo para infecção urinária com intervalo máximo de acordo com o prestador".
   - Lista (basta uma) "Indicações do Estudo Urodinâmico": antes de qualquer tratamento cirúrgico para IUE; insucesso em cirurgia prévia para IUE; insucesso no tratamento medicamentoso da incontinência urinária.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM GINECOLOGIA URODINAMICA (AE tipo1 v1113) → 01a07a08-813f-77dc
5. **Nosso:** procedimento CONSULTA EM GINECOLOGIA URODINAMICA 01a07a08-813f-77dc
   - Ativas: 3afd4e59 Pergunta sintomas avaliados pelo ginecologista — Não bloqueia; 5b48735a Pergunta EAS+urinocultura negativo — Não bloqueia; 0f275bcd Pergunta lista 3 indicações — Bloqueia em "Nenhuma"; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: 3 itens soltos + 1 cabeçalho "do Estudo Urodinâmico"
6. **Veredito:** COBERTO
7. **Problemas:**
   - Menor: o exame de urina é Pergunta (sem anexo); a validade é "de acordo com o prestador", então não cabe validade_dias.
8. **Proposta:** Opcional: Documento "EAS + Urinocultura negativos" (sem validade).

### 21. 4.1.14 Hepatologia — HEPATITE CRÔNICA C (p.31)

1. **Seção / página / recurso:** 4.1.14 Hepatologia · p.31 · HEPATITE CRÔNICA C
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (literal):**
   - Critério, lista (basta um): "Pacientes portadores de Hepatite C Crônica, sem cirrose hepática descompensada"; "Hepatite C Aguda".
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM HEPATOLOGIA - HEPATITE CRONICA C (AE tipo1 v1158) → 01a07a08-8136-7058
5. **Nosso:** procedimento CONSULTA EM HEPATOLOGIA - HEPATITE CRONICA C 01a07a08-8136-7058
   - Ativas: dfc62b3c Pergunta lista 2 opções — Bloqueia em "Nenhuma"; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: 3c360ca4, 42b4eab6 (itens soltos)
6. **Veredito:** COBERTO
7. **Problemas:**
   - Nenhum ("Hepatite C Aguda" num recurso "crônica C" é literal do manual).
8. **Proposta:** Manter.

### 22. 4.1.15 Neurologia — DISTÚRBIOS DO MOVIMENTO (p.32)

1. **Seção / página / recurso:** 4.1.15 Neurologia · p.32 · DISTÚRBIOS DO MOVIMENTO
2. **Estrutura:** LISTA_BASTA_UM (exemplificativa: "tais como")
3. **Requisitos (literal):**
   - Critério clínico — "problemas neurológicos caracterizados por movimentos involuntários que podem ocorrer de maneira contínua ou episódica, tais como": Doença de Parkinson (CID G20); Tremor essencial; Síndrome Corticobasal (SCB); Coreias; Doença de Huntington (DHQ); Ataxia; Tique; Mioclonia; Paralisia supranuclear progressiva.
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM NEUROLOGIA - DISTURBIO DOS MOVIMENTOS (AE tipo1 v1127), 0 regras
5. **Nosso:** procedimento CONSULTA EM NEUROLOGIA - DISTURBIO DOS MOVIMENTOS (vazio); conteúdo em CONSULTA EM NEUROLOGIA 01a07a08-8134-7130
   - Ativas: No procedimento certo: nenhuma.; Os 9 itens são opções de d9ad21dd Pergunta lista 19 opções em CONSULTA EM NEUROLOGIA (8134-7130) — Bloqueia em "Nenhuma", misturados com os 11 da "Doença do neurônio motor"
   - Inativas: No 8134-7130: itens soltos (19fd6c11, c7b71a24, c6713b14, 0c3ec964, 8f4cd46a, 53f86686…) e cabeçalho af6fc840
6. **Veredito:** ERRADO
7. **Problemas:**
   - Achado confirmado: a subespecialidade foi jogada no recurso genérico; o recurso próprio do SER está "Sem regras".
   - "tais como" = lista exemplificativa; a lista não tem opção "outro".
8. **Proposta:** No procedimento de DISTURBIO DOS MOVIMENTOS: Pergunta de lista com os 9 itens + "Outro movimento involuntário contínuo ou episódico (descrever)", Bloqueia em "Nenhuma"; + encaminhamento. Retirar esses itens da lista da Geral (ver GERAL).

### 23. 4.1.15 Neurologia — DOENÇA DO NEURÔNIO MOTOR (p.32)

1. **Seção / página / recurso:** 4.1.15 Neurologia · p.32 · DOENÇA DO NEURÔNIO MOTOR
2. **Estrutura:** LISTA_BASTA_UM (exemplificativa: "tais como")
3. **Requisitos (literal):**
   - Critério clínico — "síndromes clínicas com características próprias, tais como": Cefaléia; Epilepsia; Desmaios; Doença de Parkinson; Distonias; Paralisia facial periférica; Cisticercose; Miastenia gravis.e outras miopatias; Hidrocefalia; Síndromes demenciais; Esclerose múltipla. (Esta linha não traz o "Inserir no SER o encaminhamento".)
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** não achado (nenhum "NEURÔNIO MOTOR" no catálogo). Candidatos: CONSULTA EM NEUROLOGIA (geral); - CEFALEIA; - DEMENCIA; - PEDIATRIA - EPILEPSIA
5. **Nosso:** procedimento — (os itens estão em CONSULTA EM NEUROLOGIA 01a07a08-8134-7130)
   - Ativas: Os 11 itens são opções de d9ad21dd Pergunta lista 19 opções em CONSULTA EM NEUROLOGIA (8134-7130) — Bloqueia em "Nenhuma"
   - Inativas: No 8134-7130: itens soltos (e73d050d, 7b0d58bc, 80fe7d45, f761ee66, f1b28a83, c62972fa, fc1fe96b, 883d8fde, 183e09b2) e cabeçalho f7ea3095
6. **Veredito:** SEM_RECURSO_NO_SER
7. **Problemas:**
   - O conteúdo não tem relação com doença do neurônio motor (ELA nem aparece): parece a lista genérica de neurologia de algum prestador, rotulada errado no PDF.
   - A importação usou essa lista para barrar o recurso Geral (ver GERAL).
8. **Proposta:** Não cadastrar como regra bloqueante em recurso nenhum. Perguntar à regulação a que recurso essa lista pertence; se for a Geral, no máximo Pergunta de lista com "Outra condição neurológica (descrever)" ou Informativa.

### 24. 4.1.15 Neurologia — DOENÇAS NEUROMUSCULARES (p.33)

1. **Seção / página / recurso:** 4.1.15 Neurologia · p.33 · DOENÇAS NEUROMUSCULARES
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - Critério — "Pacientes que apresentem doenças do nervo periférico e muscular".
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** não há no ramo AE; existe NAO_AE "Consulta em Neurologia- Doenças Neuromusculares" (tipo1 v1093) → 01a07a08-8138-7b37, 0 regras
5. **Nosso:** procedimento Consulta em Neurologia- Doenças Neuromusculares 01a07a08-8138-7b37
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - A extração antiga pareou com o recurso NAO_AE, mas a regra não chegou a produção.
   - O SER põe o recurso no ramo NAO_AE, embora o CRECE o liste: conferir se o REUNI também tem critérios para ele (as regras de dois manuais somariam com E).
8. **Proposta:** Pergunta Sim/Não "Doença do nervo periférico ou muscular?" Não bloqueia + encaminhamento no 8138-7b37 — depois de confirmar qual manual rege o recurso.

### 25. 4.1.15 Neurologia — EPILEPSIA (p.33)

1. **Seção / página / recurso:** 4.1.15 Neurologia · p.33 · EPILEPSIA
2. **Estrutura:** SIMPLES (definição)
3. **Requisitos (literal):**
   - Critério — "Pacientes que apresentem alteração temporária e reversível do funcionamento do cérebro".
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** não achado no AE (só "CONSULTA EM NEUROLOGIA - PEDIATRIA - EPILEPSIA" e o NAO_AE "Ambulatório 1ª vez em Neurocirurgia - Epilepsia Refratária ou Fármaco-Resistente (Adulto)", que é outra coisa)
5. **Nosso:** procedimento —
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** SEM_RECURSO_NO_SER
7. **Problemas:**
   - Linha também perdida na extração antiga.
8. **Proposta:** Nada até existir recurso; se a regulação disser que epilepsia adulto vai pela CONSULTA EM NEUROLOGIA, no máximo Informativa.

### 26. 4.1.15 Neurologia — PEDIATRIA EPILEPSIA (p.33)

1. **Seção / página / recurso:** 4.1.15 Neurologia · p.33 · PEDIATRIA EPILEPSIA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM NEUROLOGIA - PEDIATRIA - EPILEPSIA (AE tipo1 v1124), 0 regras
5. **Nosso:** procedimento CONSULTA EM NEUROLOGIA - PEDIATRIA - EPILEPSIA
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Só falta o encaminhamento (a extração antiga só trouxe cacos).
8. **Proposta:** Documento encaminhamento.

### 27. 4.1.15 Neurologia — PEDIATRIA (p.33-34)

1. **Seção / página / recurso:** 4.1.15 Neurologia · p.33-34 · PEDIATRIA
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos (literal):**
   - Critério clínico, lista (basta um) — "Pacientes que apresentem as seguintes condições": Transtorno do déficit de atenção e hiperatividade; Transtorno do Espectro Autista; Suspeita de doença neurológica de base (sequela hipóxica–isquêmica, TORSCH, etc.); Retardo mental; Convulsão febril; Cefaleias; Doenças Neuromusculares; Neuropatias Periféricas; Ataxias; Movimentos involuntários; Paralisia cerebral com/sem Epilepsia; Encefalopatias.
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER (continuação na p.34)
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM NEUROLOGIA - PEDIATRIA (AE tipo1 v1123), 0 regras — 7 pedidos "Sem regras"
5. **Nosso:** procedimento CONSULTA EM NEUROLOGIA - PEDIATRIA
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Linha perdida na extração antiga (a lista atravessa a quebra da p.33 para a p.34).
8. **Proposta:** Pergunta de lista com as 12 condições, Bloqueia em "Nenhuma"; + encaminhamento. Sem Dedutível de idade (o manual não define).

### 28. 4.1.15 Neurologia — GERAL (p.34)

1. **Seção / página / recurso:** 4.1.15 Neurologia · p.34 · GERAL
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - "Pacientes que apresentem condições de acordo com os critérios de inclusão de cada prestador" — orientação.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM NEUROLOGIA (AE tipo1 v1120) → 01a07a08-8134-7130
5. **Nosso:** procedimento CONSULTA EM NEUROLOGIA 01a07a08-8134-7130
   - Ativas: d9ad21dd Pergunta lista 19 opções em CONSULTA EM NEUROLOGIA (8134-7130) — Bloqueia em "Nenhuma": Parkinson (CID G20), PSP, SCB, Tremor essencial, Distonias, Mioclonia, Huntington, Epilepsia, Esclerose múltipla, Miastenia, Paralisia facial periférica, Hidrocefalia, Cisticercose, Síndromes demenciais, Coreias, Ataxia, Tique, Cefaléia, Desmaios; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: 15 itens/cabeçalhos das linhas Distúrbios do Movimento e Neurônio Motor; 4 da linha Toxina Botulínica (015bb6f5, 2980c292, 33887969, 6cb19252)
6. **Veredito:** ERRADO
7. **Problemas:**
   - Regra ATIVA que barra quem não devia: para a Neurologia Geral o manual só pede encaminhamento. A lista ativa (vinda de duas OUTRAS linhas) faz pedido de neuropatia, vertigem, sequela de AVC, neuralgia etc. cair em "Nenhuma destas" e ser BLOQUEADO. 6 pedidos do espelho em "A conferir".
   - A REVISAO.md de 01/10 juntou as duas seções aqui e deixou "para a regulação confirmar" — pendente.
8. **Proposta:** Desativar d9ad21dd (ou rebaixar a Informativa) e deixar só o encaminhamento; levar os itens de Distúrbios do Movimento para o recurso próprio.

### 29. 4.1.15 Neurologia — TOXINA BOTULÍNICA (p.34)

1. **Seção / página / recurso:** 4.1.15 Neurologia · p.34 · TOXINA BOTULÍNICA
2. **Estrutura:** SO_ORIENTACAO (o texto literal do PDF não se aplica)
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Texto do PDF: "Pacientes portadores de doenças benignas do trato genital para tratamento cirúrgico"; "…doenças benignas da pelve para tratamento cirúrgico"; "Observar as condições apresentadas pelos pacientes de acordo com os critérios de inclusão de cada prestador" — cópia literal da linha Ginecologia CIRURGIA (p.29): erro do próprio manual.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** não achado (nenhum "TOXINA"/"BOTUL" no catálogo do SER)
5. **Nosso:** procedimento — (cacos em CONSULTA EM NEUROLOGIA 01a07a08-8134-7130)
   - Ativas: nenhuma
   - Inativas: 015bb6f5 (trato genital), 2980c292 (pelve), 33887969 e 6cb19252 (cacos "…de acordo com os") — fonte CRECE p.34
6. **Veredito:** SEM_RECURSO_NO_SER
7. **Problemas:**
   - A REVISAO.md descreveu como "regra da GINECOLOGIA que caiu na Neurologia"; na verdade o PDF traz esse texto na linha da Toxina — erro de origem do CRECE, não da extração.
8. **Proposta:** Manter inativas; não cadastrar; reportar o erro do manual ao CRECE.

### 30. 4.1.15 Neurologia — PARKINSON (p.34)

1. **Seção / página / recurso:** 4.1.15 Neurologia · p.34 · PARKINSON
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Critério — "Pacientes que apresentem diagnóstico de Parkinson".
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM NEUROLOGIA - PARKINSON (AE tipo1 v1122), 0 regras
5. **Nosso:** procedimento CONSULTA EM NEUROLOGIA - PARKINSON
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Linha perdida na extração antiga; o tema só aparece como opção da lista da Geral.
8. **Proposta:** Pergunta Sim/Não "Diagnóstico de Doença de Parkinson?" Não bloqueia + encaminhamento. Opcional: Dedutível CID permitido G20 com severidade Ressalva (Bloqueia barraria G21/G22 e CID genérico).

### 31. 4.1.15 Neurologia — ESCLEROSE MÚLTIPLA (p.34)

1. **Seção / página / recurso:** 4.1.15 Neurologia · p.34 · ESCLEROSE MÚLTIPLA
2. **Estrutura:** LISTA_BASTA_UM (exemplificativa: "suspeita de … como")
3. **Requisitos (literal):**
   - Critério clínico — "Pacientes que apresentem suspeita de patologia desmielinizante, como": Esclerose múltipla; Neuromielite Óptica (NMO); Encefalomielite aguda disseminada (ADEM).
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - "condições de acordo com os critérios de inclusão de cada prestador" — orientação.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM NEUROLOGIA - ESCLEROSE MULTIPLA (AE tipo1 v1125) → 01a07a08-813b-7b88
5. **Nosso:** procedimento CONSULTA EM NEUROLOGIA - ESCLEROSE MULTIPLA 01a07a08-813b-7b88
   - Ativas: 0ad3aecf Pergunta lista 3 opções, texto "O paciente tem ao menos uma das condições abaixo?" — Bloqueia em "Nenhuma"; Documento/Bloqueia "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." (fonte "CRECE/REUNI — requisito global")
   - Inativas: 3 itens soltos, cabeçalho f342fb49, caco 5026e7a8
6. **Veredito:** PARCIAL
7. **Problemas:**
   - A pergunta perde "suspeita de": paciente só com suspeita pode ser respondido "Nenhuma" e barrado.
   - "como" = exemplificativa, sem opção "outra".
8. **Proposta:** Nova versão: "Há suspeita (ou diagnóstico) de patologia desmielinizante?" com EM | NMO | ADEM | "Outra patologia desmielinizante (descrever)".

### 32. 4.1.16 Homeopatia — HOMEOPATIA (p.35)

1. **Seção / página / recurso:** 4.1.16 Homeopatia · p.35 · HOMEOPATIA
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - "condições de acordo com os critérios de inclusão de cada prestador" — orientação.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM HOMEOPATIA (MAIOR DE 60 ANOS) (AE tipo1 v1159), 0 regras
5. **Nosso:** procedimento CONSULTA EM HOMEOPATIA (MAIOR DE 60 ANOS)
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Só falta o encaminhamento. O rótulo do SER limita idade (> 60) e o manual não fala de idade.
   - A extração antiga rotulou a seção como "HOMEOPATIA - ESCLEROSE MÚLTIPLA" (vazou o nome da linha anterior) e ficou SEM_PAR.
8. **Proposta:** Documento encaminhamento. Idade 60 só se a regulação confirmar, e como Aviso (fonte: rótulo do SER, não o manual).

### 33. 4.1.16 Homeopatia — HOMEOPATIA INFANTIL (p.35)

1. **Seção / página / recurso:** 4.1.16 Homeopatia · p.35 · HOMEOPATIA INFANTIL
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - "condições de acordo com os critérios de inclusão de cada prestador" — orientação.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM HOMEOPATIA INFANTIL (AE tipo1 v1172), 0 regras
5. **Nosso:** procedimento CONSULTA EM HOMEOPATIA INFANTIL
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Só falta o encaminhamento; a extração fundiu esta linha com a anterior.
8. **Proposta:** Documento encaminhamento.

### 34. 4.1.17 Infectologia — DOENÇAS TROPICAIS (p.35)

1. **Seção / página / recurso:** 4.1.17 Infectologia · p.35 · DOENÇAS TROPICAIS
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - "condições de acordo com os critérios de inclusão de cada prestador" — orientação.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** não achado (nenhum "TROPICA" no catálogo; "PUNÇAO LOMBAR (DOENÇAS INFECTOPARASITARIAS)" é exame, não serve)
5. **Nosso:** procedimento —
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** SEM_RECURSO_NO_SER
7. **Problemas:**
   - Sem recurso correspondente no SER.
8. **Proposta:** Nada.

### 35. 4.1.17 Infectologia — HIV/AIDS GESTANTE (p.35)

1. **Seção / página / recurso:** 4.1.17 Infectologia · p.35 · HIV/AIDS GESTANTE
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - "condições de acordo com os critérios de inclusão de cada prestador" — orientação.
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM INFECTOLOGIA - HIV/AIDS - GESTANTE (AE tipo1 v1075), 0 regras
5. **Nosso:** procedimento CONSULTA EM INFECTOLOGIA - HIV/AIDS - GESTANTE
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Só falta o encaminhamento; linha perdida na extração antiga.
8. **Proposta:** Documento encaminhamento. Opcional: Dedutível sexo F como Aviso (vem do nome do recurso, não do texto do manual).

### 36. 4.1.17 Infectologia — PEDIATRIA (p.36 (continuação da tabela))

1. **Seção / página / recurso:** 4.1.17 Infectologia · p.36 (continuação da tabela) · PEDIATRIA
2. **Estrutura:** LISTA_BASTA_UM (exemplificativa: "como")
3. **Requisitos (literal):**
   - Critério clínico — "Doenças Infecciosas e Parasitárias, como": Recém-nascido/criança exposta ao HIV; Investigação de adenomegalia; Investigação de hepatomegalia e/ou esplenomegalia; Toxoplasmose congênita ou adquirida; Citomegalovirose congênita; Infecção pelo Zika Vírus; Investigação de Febre de Origem Obscura; Piodermite de repetição; Infecções de repetição; Tuberculose; Sífilis Congênita e Adquirida; Vítima de Abuso sexual.
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Orientação "Observar os critérios de inclusão/exclusão e faixa etária de cada prestador" — não vira regra
4. **Recurso no SER:** CONSULTA EM INFECTOLOGIA - PEDIATRIA (AE tipo1 v1076), 0 regras (existe também "CONSULTA EM INFECTOLOGIA-PEDIATRIA-HIV/AIDS", sem linha no manual)
5. **Nosso:** procedimento CONSULTA EM INFECTOLOGIA - PEDIATRIA
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Linha perdida na extração antiga. "Criança exposta ao HIV" talvez vá pelo recurso -PEDIATRIA-HIV/AIDS.
8. **Proposta:** Pergunta de lista com os 12 itens + "Outra doença infecciosa ou parasitária (descrever)", Bloqueia em "Nenhuma"; + encaminhamento.

### 37. 4.1.17 Infectologia — COINFECÇÃO AIDS/HEPATITE C (p.36 (continuação da tabela))

1. **Seção / página / recurso:** 4.1.17 Infectologia · p.36 (continuação da tabela) · COINFECÇÃO AIDS/HEPATITE C
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - Documento — encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER
   - Critério — "Pacientes que apresentem infecções pelo vírus da Hepatite C e HIV" (as duas).
   - Orientação — "Observar critérios de faixa etária de inclusão de cada prestador".
   - Exclusão — "Não atendem pacientes com Hepatite B".
   - Exclusão — "Não atendem pacientes com infecção isolada por Hepatite C ou por HIV".
4. **Recurso no SER:** CONSULTA EM INFECTOLOGIA COINFECCAO HIV/HEPATITE VIRAL (AE tipo1 v1077), 0 regras — 1 pedido "Sem regras"
5. **Nosso:** procedimento CONSULTA EM INFECTOLOGIA COINFECCAO HIV/HEPATITE VIRAL
   - Ativas: nenhuma
   - Inativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - A extração antiga tinha as 3 linhas, mas SEM_PAR (nome diferente) — nunca importadas.
   - O SER fala em "HEPATITE VIRAL" (inclui B?) e o manual EXCLUI Hepatite B — confirmar com a regulação.
8. **Proposta:** Pergunta Sim/Não "Coinfecção HIV + Hepatite C confirmada?" Não bloqueia; Pergunta "Tem Hepatite B?" Sim bloqueia; + encaminhamento. CID não serve (o pedido traz um CID só; coinfecção são dois).

## Regras de produção com fonte CRECE p.24–p.36 fora do lugar ou sem correspondência

| Procedimento (id) | Regras | Situação |
|---|---|---|
| CONSULTA EM GASTROENTEROLOGIA (813a-7468) — única origem viva: SISREG | `b87349a6` lista ATIVA (Geral + 3 opções DII) + encaminhamento ATIVO; 47 inativas: 20 da Geral (p.24), 8 da DII (p.25), 19 da **Gastro Pediatria** (p.26) | Procedimento errado. Filtro SER num procedimento que o SER não alcança → inertes. Pediatria e DII têm recurso próprio no SER. |
| CONSULTA EM MASTOLOGIA (813a-7977) — origem AE inativa | `1f57d0e0` BI-RADS 4/5 ATIVA + encaminhamento ATIVO | Inerte: o recurso AE foi religado (Confirmado) a 8121-7b9c, sem regras. |
| CONSULTA EM NEUROLOGIA (8134-7130) | `d9ad21dd` lista ATIVA de 19 itens (Distúrbios do Movimento + Neurônio Motor) | Barra a Neurologia Geral, que no manual é só orientação. |
| CONSULTA EM NEUROLOGIA (8134-7130) | `015bb6f5`, `2980c292` ("doenças benignas do trato genital/da pelve"), `33887969`, `6cb19252` (cacos) — inativas | Vieram da linha TOXINA BOTULÍNICA, cujo texto no PDF é cópia da Gineco CIRURGIA (erro do manual). Inativas: certo. |
| Cacos "Pacientes que apresentem condições de acordo com os" | `093dcdc1` (Uroginecologia), `5026e7a8` (Esclerose Múltipla), `6cb19252`, `33887969` (Neurologia) — inativas | Certo ficarem inativas. |
| Dedutíveis inativas | `e5078cd3` idade ≥11 (Gineco Endócrino), `32b7330d` sexo F (Infertilidade) | Certo ficarem inativas. |
| Dedutível inativa que deve voltar em outro lugar | `296b48c1` idade ≥18 (DII), hoje no homônimo de Gastro Geral | Recriar no procedimento da DII (813a-7869). |
| CONSULTA EM NEFROLOGIA - GERAL (8135-789e) — fora do trecho (4.1.18, p.36) | `fc73d1e6`, `2bc53223` cacos inativos | Procedimento sem recurso do SER ligado (o recurso "CONSULTA EM NEFROLOGIA - GERAL" aponta para "CONSULTA EM NEFROLOGIA"). Fica para o trecho seguinte. |

## Pendências da REVISAO.md (01/10) neste trecho

- **Gastro Pediatria** — "os 17 critérios dela saem daqui e precisam ser cadastrados lá": **não feito** (0 regras; 161 pedidos sem análise).
- **Gastro DII** — "Se a DII também tiver recurso próprio, o mesmo vale para ela": tem (`… DOENCA INFLAMATORIA INSTESTINAL`); **não feito**.
- **Gastro Geral** — a lista convertida está no homônimo de origem SISREG (não alcançado pelo SER) — situação posterior à conversão, não prevista na revisão.
- **Neurologia** — "Duas seções viraram uma lista… Para a regulação confirmar": pendente, e a premissa não se sustenta — Distúrbios do Movimento tem recurso próprio e a linha GERAL do manual não tem lista.
- **Neurologia** — "As duas regras de cirurgia ginecológica que estavam aqui saem": feito (inativas); a explicação está imprecisa (é erro do PDF na linha Toxina, não da extração).
- **Gineco Endocrinologia** (idade 11 como opção) e **Urodinâmica** (requisitos somados): ficaram para a regulação confirmar — sem mudança.
- **Fora da revisão** (nunca importadas, por isso nem aparecem lá): Gastro Adolescente, Geriatria, Histeroscopia Diagnóstica, Gineco Cirurgia/Videolaparoscopia/Infantopuerperal, Neuro Pediatria/Parkinson/Pediatria Epilepsia/Neuromusculares, Homeopatia (2), Infectologia (HIV Gestante, Pediatria, Coinfecção). Mastologia também não aparece (regra única, sem conversão).

## Recursos AE do SER desta área sem linha no manual (p.24–36)

CONSULTA EM GINECOLOGIA - HISTEROSCOPIA CIRURGICA; - LAQUEADURA; CONSULTA EM HEPATOLOGIA - CIRROSE (1 pedido "Sem regras"); - HEPATITE CRONICA B (1 pedido); CONSULTA EM INFECTOLOGIA-PEDIATRIA-HIV/AIDS; CONSULTA EM NEUROLOGIA - ADOLESCENTE; NEUROLOGIA/AVC; - CEFALEIA; - DEMENCIA. Sem regra é o certo enquanto não houver critério no manual (podem estar em outra seção).

## Erros do próprio PDF (não da extração)

- Neurologia **TOXINA BOTULÍNICA** (p.34): critérios copiados da Gineco CIRURGIA ("doenças benignas do trato genital/da pelve para tratamento cirúrgico").
- Neurologia **DOENÇA DO NEURÔNIO MOTOR** (p.32): lista genérica de neurologia (cefaleia, epilepsia, desmaios…), sem ELA.
- Gineco **HITEROSCOPIA** (p.28) e **INFANTOPUERPERAL** (p.30; no SER é "INFANTO PUBERAL"); Gastro DII com CIDs que não batem com a CID-10 (K51.1, K50.8 como "colite indeterminada").
- Gastro Pediatra: "Criancas > - 2 anos" (provável ≥ 2) e "Litíase biliar (Apresentar ultrassonografia abdominal" com parêntese aberto.
