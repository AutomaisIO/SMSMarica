# Confronto REUNI (rede geral do SER, ramo NAO_AE) p.25–34 × regras de elegibilidade em produção — parte C

Data: 09/10/2026 · Só leitura. Manual: `SMSMais.Regulacao/documantacao/REUNI_MANUAL DO SOLICITANTE_V3 29.12.20222 - Copia.pdf`, p.25–34 (4.4 Ortopedia, 4.5 Neurocirurgia, 4.6 Pré-natal de alto risco, 4.7 Microcirurgias, 4.8 Hematologia). A tabela da Hematologia Pediátrica (4.8.2) começa na p.34 e fica inteira na p.35, por isso a p.35 entrou. As tabelas foram conferidas na imagem das páginas (renderizadas com PyMuPDF), não só no texto extraído.

Produção: `regras.json` / `ser_recursos.json` / `analise_espelho_por_procedimento.json` exportados hoje. Volumes de pedidos = pedidos SER no espelho já analisados.

## Resumo

- **55 linhas/recursos do manual** confrontados. Por veredito: COBERTO 1, PARCIAL 2, AUSENTE 24, ERRADO 3, NAO_REPRESENTAVEL_HOJE 25.
- **Ortopedia inteira (4.4, 37 linhas, 13 filas do SER) não tem uma regra sequer**, nem ativa nem inativa. A extração antiga rotulou essas tabelas como "ONCOLOGIA - JOELHO/ESCOLIOSE/HÉRNIAS DE DISCO" com pareamento SEM_PAR, e nada foi importado. É o maior buraco do trecho em volume: **Coluna Adulto, 401 pedidos "Sem regras"**; Joelho Adulto, 104; Quadril, 12; Ombro/Cotovelo, 10.
- **Hematologia Adulto está ERRADA**: as 10 regras ativas somam com E os exames dos três ramos (anemia, leucopenia, hemorrágico) como documentos obrigatórios que bloqueiam. E incluem o **"Teste do Pezinho"**, que é da Hematologia **Pediátrica** (p.35). Já a **Hematologia Infantil não tem nada**: o conteúdo dela foi parar no adulto, porque a extração antiga juntou as páginas 33 e 35.
- **Pré-natal de alto risco** está quase certo (lista de 18 condições + 3 exclusões). O erro é o laudo de USG **gemelar**, obrigatório para toda gestante. **Aconselhamento em malformação fetal** está AUSENTE: o conteúdo dele caiu no Pré-natal (fonte "p.31", é da p.32) e foi desativado lá, sem ser recriado no procedimento certo.
- **Neurocirurgia**: só o Neurovascular está COBERTO. A Epilepsia Infantil tem "EEG e RNM (se possível)" como documento obrigatório que bloqueia, e a Epilepsia Adulto ficou sem nada (o pareamento ligou a linha "adulto e infantil" só ao infantil). Adulto/Infantil exceto coluna, Plexo braquial/Nervos periféricos e Parkinson: sem regra.
- **Condicionais**: Joelho Adulto, Mão Adulto, Coluna, Quadril, Ombro/Cotovelo e os dois de Hematologia são filas do SER com várias indicações, e cada indicação pede um exame diferente. Sem regra condicional, a saída é: (1) uma caixa de imagem obrigatória e genérica, (2) uma pergunta de lista "basta uma" em que cada opção traz a indicação **com o exame exigido no texto**, e (3) os exames específicos como documento opcional. Assim não se cobra a mais nem se perde a conferência humana.
- **Armadilha evitada, mas viva no CSV antigo**: o spike-e transformou "lesão ligamentar/menisco a partir de 50 anos" em Dedutível idade_min=50. Se for importado, barra da fila do Joelho todo paciente com menos de 50 anos.

Convenção usada: **AUSENTE** = cabe no modelo atual e não está cadastrado; **NAO_REPRESENTAVEL_HOJE** = a linha é uma indicação dentro de uma fila com várias, e o exame obrigatório dela só fica fiel com regra condicional (há aproximação, descrita na proposta); **ERRADO** = existe regra ativa que cobra o que o manual não cobra (ou de outro procedimento); **PARCIAL** = existe regra e o conteúdo está certo, mas há obrigatoriedade/severidade errada ou falta parte.

## Tabela-resumo

| # | Seção | Recurso (manual) | Recurso SER (NAO_AE) | Veredito | Em uma linha |
|---|---|---|---|---|---|
| 1 | 4.4 Ortopedia (regras gerais da seção) | Ortopedia — abertura da seção + "ATENÇÃO" + "Orientações gerais" | Todas as 11 filas NAO_AE de ortopedia: Ambulatório 1ª vez em Ortopedia - Joelho (Adulto); Ambulatório 1ª Vez em Ortopedia - Joelho (Infan… | **AUSENTE** | Nenhum dos 11 procedimentos de ortopedia (nem os 2 de coluna) tem regra — ativa ou inativa. A análise do espelho dá "Sem regras": Joelho Adulto 104 pedidos, Quadril 12, Ombro/Cotovelo 10, Reconstrução 4, Pé 2, Mão Adulto 1, Mão… |
| 2 | 4.4.1 Ortopedia – Joelho | LESÕES LIGAMENTARES/ MENISCO EM PACIENTES COM MENOS DE 50 ANOS | Ambulatório 1ª vez em Ortopedia - Joelho (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Procedimento sem nenhuma regra (104 pedidos "Sem regras"). |
| 3 | 4.4.1 Ortopedia – Joelho | LESÕES LIGAMENTARES/ MENISCO EM PACIENTES A PARTIR DE 50 ANOS | Ambulatório 1ª vez em Ortopedia - Joelho (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra. ARMADILHA: a extração antiga transformou a linha em Dedutível idade_min=50 ("Geralmente, para pacientes a partir de 50 anos…", spike-e, REUNI p.25). Se importada, barra todo paciente com menos de 50 anos da fila do J… |
| 4 | 4.4.1 Ortopedia – Joelho | ARTROPLASTIA TOTAL DE JOELHO | Ambulatório 1ª vez em Ortopedia - Joelho (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra. O RX com carga é exigido aqui e no ≥ 50 anos/osteotomia, mas não na lesão < 50 nem na instabilidade femoropatelar. |
| 5 | 4.4.1 Ortopedia – Joelho | OSTEOTOMIA/ DEFORMIDADES ANGULARES | Ambulatório 1ª vez em Ortopedia - Joelho (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra. |
| 6 | 4.4.1 Ortopedia – Joelho | INSTABILIDADE FEMOROPATELAR | Ambulatório 1ª vez em Ortopedia - Joelho (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra. |
| 7 | 4.4.1 Ortopedia – Joelho | JOELHO (INFANTIL – ATÉ 15 ANOS) | Ambulatório 1ª Vez em Ortopedia - Joelho (Infantil) | **AUSENTE** | Sem regra. A extração antiga rotulou como "ONCOLOGIA - JOELHO (INFANTIL – ATÉ 15 ANOS)" (SEM_PAR) e só pegou os dois extras condicionais, não a base. |
| 8 | 4.4.2 Ortopedia – Mão | SÍNDROME DO TÚNEL DO CARPO | Ambulatório 1ª vez em Ortopedia - Mão (Adulto) | **AUSENTE** | Sem regra. Nada além do encaminhamento é obrigatório: o exame pode ser substituído por exame clínico detalhado — cadastrar USG/ENMG como obrigatório seria erro. |
| 9 | 4.4.2 Ortopedia – Mão | CISTO SINOVIAL E DEDO EM GATILHO | Ambulatório 1ª vez em Ortopedia - Mão (Adulto) | **AUSENTE** | Sem regra. |
| 10 | 4.4.2 Ortopedia – Mão | TENDINOPATIAS | Ambulatório 1ª vez em Ortopedia - Mão (Adulto) | **AUSENTE** | Sem regra. Conflita com a abertura da seção 4.4 ("tendinites… deverão ser tratadas nos ambulatórios locais"). Na Mão, a tabela prevalece. |
| 11 | 4.4.2 Ortopedia – Mão | FRATURAS DE ESCAFOIDE NÃO CONSOLIDADA | Ambulatório 1ª vez em Ortopedia - Mão (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra. Exame obrigatório só para esta lesão, numa fila em que três lesões têm exame apenas "se possível". |
| 12 | 4.4.2 Ortopedia – Mão | NECROSE DE OSSOS DO PUNHO, INSTABILIDADES CÁRPICAS | Ambulatório 1ª vez em Ortopedia - Mão (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra. |
| 13 | 4.4.2 Ortopedia – Mão | FRATURAS NÃO CONSOLIDADAS, VICIOSAS, PSEUDOARTROSE, ARTROSES DO PUNHO | Ambulatório 1ª vez em Ortopedia - Mão (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra. |
| 14 | 4.4.2 Ortopedia – Mão | MÃO (INFANTIL) | Ambulatório 1ª vez em Ortopedia - Mão (Infantil) | **AUSENTE** | Sem regra (1 pedido "Sem regras"). |
| 15 | 4.4.3 Ortopedia – Coluna (regra da seção) | Coluna — "Todos os encaminhamentos médicos deverão ser encaminhados por Neurocirurgião ou Ortopedista." | Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Inf… | **AUSENTE** | Nenhuma regra nas duas filas — Coluna Adulto é o MAIOR volume do trecho: 401 pedidos no espelho, todos "Sem regras". |
| 16 | 4.4.3 Ortopedia – Coluna | HÉRNIAS DE DISCO | Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Inf… | **NAO_REPRESENTAVEL_HOJE** | Sem regra (Coluna Adulto: 401 pedidos "Sem regras"). A extração antiga gravou o "Inclui:" inteiro como uma pergunta única (rótulo "ONCOLOGIA - HÉRNIAS DE DISCO", SEM_PAR) — a lista a–d é "basta um", não um bloco Sim/Não. |
| 17 | 4.4.3 Ortopedia – Coluna | ESCOLIOSE | Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Inf… | **NAO_REPRESENTAVEL_HOJE** | Sem regra (Coluna Adulto: 401 pedidos "Sem regras"). A extração antiga ("ONCOLOGIA - ESCOLIOSE", SEM_PAR) pegou só o documento, sem o "indicar se há patologia primária". |
| 18 | 4.4.3 Ortopedia – Coluna | TUMOR | Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Inf… | **NAO_REPRESENTAVEL_HOJE** | Sem regra (Coluna Adulto: 401 pedidos "Sem regras"). Choca com a abertura da 4.4 (tumor ósseo → Tumores do Tecido Ósseo-Conectivo): a exclusão por CID C40/C41/D16 não pode valer na Coluna. |
| 19 | 4.4.3 Ortopedia – Coluna | ESPONDILOARTROSE | Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Inf… | **AUSENTE** | Sem regra (Coluna Adulto: 401 pedidos "Sem regras"). |
| 20 | 4.4.3 Ortopedia – Coluna | ESPONDILÓLISE | Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Inf… | **NAO_REPRESENTAVEL_HOJE** | Sem regra (Coluna Adulto: 401 pedidos "Sem regras"). |
| 21 | 4.4.3 Ortopedia – Coluna | ESPONDILOLISTESE | Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Inf… | **AUSENTE** | Sem regra (Coluna Adulto: 401 pedidos "Sem regras"). |
| 22 | 4.4.3 Ortopedia – Coluna | FRATURAS | Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Inf… | **AUSENTE** | Sem regra (Coluna Adulto: 401 pedidos "Sem regras"). |
| 23 | 4.4.3 Ortopedia – Coluna | PATOLOGIA CIRÚRGICA DA COLUNA VERTEBRAL (ADULTO E INFANTIL) | Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Inf… | **NAO_REPRESENTAVEL_HOJE** | Sem regra (Coluna Adulto: 401 pedidos "Sem regras"). A linha tem o mesmo nome das duas filas do SER; ambígua: pode ser a regra das "demais" patologias ou um resumo. O "da fratura" no texto é provável erro do manual. |
| 24 | 4.4.4 Ortopedia – Quadril Adulto | COXARTROSE | Ambulatório 1ª vez em Ortopedia - Quadril (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (12 pedidos "Sem regras"). O exame exigido muda por indicação (RX × RNM × ambos); só "algum exame de imagem do quadril" é comum a todas. |
| 25 | 4.4.4 Ortopedia – Quadril Adulto | OSTEONECROSE | Ambulatório 1ª vez em Ortopedia - Quadril (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (12 pedidos "Sem regras"). O exame exigido muda por indicação (RX × RNM × ambos); só "algum exame de imagem do quadril" é comum a todas. |
| 26 | 4.4.4 Ortopedia – Quadril Adulto | IMPACTO FEMORO-ACETABULAR | Ambulatório 1ª vez em Ortopedia - Quadril (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (12 pedidos "Sem regras"). O exame exigido muda por indicação (RX × RNM × ambos); só "algum exame de imagem do quadril" é comum a todas. |
| 27 | 4.4.4 Ortopedia – Quadril Adulto | REVISÃO DE ARTROPLASTIA TOTAL DO QUADRIL | Ambulatório 1ª vez em Ortopedia - Quadril (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (12 pedidos "Sem regras"). O exame exigido muda por indicação (RX × RNM × ambos); só "algum exame de imagem do quadril" é comum a todas. |
| 28 | 4.4.5 Ortopedia – Ombro/Cotovelo | LESÃO LIGAMENTARES OU DO MANGUITO ROTADOR | Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (10 pedidos "Sem regras"). A fila do SER é "Ombro / Cotovelo", mas o manual não tem nenhuma linha de cotovelo. |
| 29 | 4.4.5 Ortopedia – Ombro/Cotovelo | INSTABILIDADE DO OMBRO | Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (10 pedidos "Sem regras"). A fila do SER é "Ombro / Cotovelo", mas o manual não tem nenhuma linha de cotovelo. |
| 30 | 4.4.5 Ortopedia – Ombro/Cotovelo | LUXAÇÃO ACRÔMIO-CLAVICULAR | Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (10 pedidos "Sem regras"). A fila do SER é "Ombro / Cotovelo", mas o manual não tem nenhuma linha de cotovelo. |
| 31 | 4.4.5 Ortopedia – Ombro/Cotovelo | FRATURA | Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (10 pedidos "Sem regras"). A fila do SER é "Ombro / Cotovelo", mas o manual não tem nenhuma linha de cotovelo. |
| 32 | 4.4.5 Ortopedia – Ombro/Cotovelo | ARTROSE | Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (10 pedidos "Sem regras"). A fila do SER é "Ombro / Cotovelo", mas o manual não tem nenhuma linha de cotovelo. |
| 33 | 4.4.6 Ortopedia – Pediatria (até 15 anos) | ACOMETIMENTO ORTOPÉDICO COM INDICAÇÃO CIRÚRGICA | Ambulatório 1ª Vez em Ortopedia - Ortopedia Pediátrica (exceto coluna) | **AUSENTE** | Sem regra (1 pedido SER "Sem regras"). A fila do SER é "(exceto coluna)": coluna infantil vai para Patologia Cirúrgica da Coluna (Infantil). |
| 34 | 4.4.7 Outros Ambulatórios Ortopédicos | PÉ / TORNOZELO | Ambulatório 1ª vez em Ortopedia - Pé & Tornozelo (Adulto) | **AUSENTE** | Sem regra (2 pedidos "Sem regras"). |
| 35 | 4.4.7 Outros Ambulatórios Ortopédicos | TRAUMAS DE MÉDIA COMPLEXIDADE (FRATURAS OCORRIDAS EM ATÉ 21 DIAS) | Ambulatório 1ª vez em Ortopedia - Trauma Ortopédico de Média Complexidade | **AUSENTE** | Sem regra. Os 21 dias não são dedutíveis: o espelho não tem a data do evento. |
| 36 | 4.4.7 Outros Ambulatórios Ortopédicos | SEQUELAS PÓS-TRAUMÁTICAS (FRATURAS CONSOLIDADAS DE FORMA VICIOSA) | Ambulatório 1ª vez em Ortopedia - Sequelas Pós Traumáticas (Adulto) | **AUSENTE** | Sem regra. |
| 37 | 4.4.7 Outros Ambulatórios Ortopédicos | RECONSTRUÇÃO E ALONGAMENTO ÓSSEO (FIXADOR EXTERNO) | Ambulatório 1ª Vez em Ortopedia - Reconstrução e Alongamento Ósseo (Fixador Externo) | **AUSENTE** | Sem regra (4 pedidos "Sem regras"). O formulário do SER desta fila pede ainda "Descreva o Tratamento Conservador realizado, se houver" e "Quanto tempo durou o tratamento?" — o manual não cita. |
| 38 | 4.5 Neurocirurgia | NEUROCIRURGIA - ADULTO E INFANTIL (EXCETO COLUNA) | Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia Adulto (Exceto Coluna) ; Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia Infantil… | **AUSENTE** | Nenhuma das duas filas tem regra (Infantil: 2 pedidos "Sem regras"). |
| 39 | 4.5 Neurocirurgia | EPILEPSIA REFRATÁRIA OU FÁRMACO RESISTENTE (ADULTO E INFANTIL) — fila Adulto | Ambulatório 1ª vez em Neurocirurgia - Epilepsia Refrataria ou Fármaco-Resistente (Adulto) (rótulo do SER sem acento em "Refrataria") | **AUSENTE** | Sem regra: o pareamento "composto" da extração antiga ligou a linha "(ADULTO E INFANTIL)" só à fila Infantil. |
| 40 | 4.5 Neurocirurgia | EPILEPSIA REFRATÁRIA OU FÁRMACO RESISTENTE (ADULTO E INFANTIL) — fila Infantil | Ambulatório 1ª vez em Neurocirurgia - Epilepsia Refratária ou Fármaco-Resistente (Infantil) | **PARCIAL** | O "(se possível)" virou Documento OBRIGATÓRIO que Bloqueia: todo pedido sem EEG+RNM fica "A conferir documento" (e trava o envio no assistente). |
| 41 | 4.5 Neurocirurgia | PLEXO BRAQUIAL/NERVOS PERIFÉRICOS | Ambulatório 1ª vez em Neurocirurgia - Plexo Braquial ; Ambulatório 1ª vez em Neurocirurgia - Nervos Periféricos | **AUSENTE** | Nenhuma das duas filas tem regra; a extração antiga deixou a linha SEM_PAR. |
| 42 | 4.5 Neurocirurgia | NEUROVASCULAR | Ambulatório 1ª vez em Neurocirurgia - Neurovascular | **COBERTO** | Só cosmético: o rótulo do encaminhamento é o global ("descrição clara e detalhada do caso") e não cita tratamento/medicações/tempo de lesão. |
| 43 | 4.5 Neurocirurgia | PARKINSON/ MOVIMENTOS INVOLUNTÁRIOS | Ambulatório 1ª vez em Neurocirurgia - Parkinson / Movimentos Involuntários | **AUSENTE** | Sem regra (nem o encaminhamento global). |
| 44 | 4.6 Obstetrícia – Pré-natal de alto risco estratégico | PRÉ-NATAL DE ALTO RISCO ESTRATÉGICO | Ambulatório 1ª vez - Pré Natal de Alto Risco Estratégico | **PARCIAL** | A regra "Para casos de gemelaridade: Laudo de USG…" é Documento OBRIGATÓRIO que Bloqueia para TODA gestante — o manual só exige em gemelar. Toda gestação única fica "A conferir documento" e o assistente pede um laudo que não ex… |
| 45 | 4.6 Obstetrícia – Pré-natal de alto risco estratégico | ACONSELHAMENTO EM MALFORMAÇÃO FETAL | Ambulatório 1ª vez - Aconselhamento em malformação fetal | **AUSENTE** | Sem regra ativa nem inativa no procedimento certo (2 pedidos "Sem regras"). O conteúdo FOI extraído, mas caiu no Pré-Natal de Alto Risco com fonte "REUNI p.31" (é da p.32) e depois foi desativado lá — nunca foi recriado aqui. |
| 46 | 4.7 Microcirurgias | MICROCIRURGIA RECONSTRUTORA (INFANTIL) | Ambulatório 1ª vez - Microcirurgia Reconstrutora (Infantil) | **AUSENTE** | Sem regra. O SER tem filas próprias "Microcirurgia - Lesão de Plexo Braquial (Adulto/Infantil)", sem linha no manual; o critério "lesão de plexo braquial" desta linha pode estar desatualizado (o manual é de 2022). |
| 47 | 4.7 Microcirurgias | MICROCIRURGIA RECONSTRUTORA (ADULTO) | Ambulatório 1ª vez - Microcirurgia Reconstrutora (Adulto) | **AUSENTE** | Sem regra. O SER tem filas próprias "Microcirurgia - Lesão de Plexo Braquial (Adulto/Infantil)", sem linha no manual; o critério "lesão de plexo braquial" desta linha pode estar desatualizado (o manual é de 2022). |
| 48 | 4.8.1 Hematologia Adulto (seção) | Hematologia Adulto — Critérios de exclusão + "Necessita informar" | Ambulatório 1ª vez - Hematologia (Adulto) | **AUSENTE** | Nenhuma das 4 exclusões está cadastrada (nem ativa nem inativa). |
| 49 | 4.8.1 Hematologia Adulto | ANEMIA | Ambulatório 1ª vez - Hematologia (Adulto) | **ERRADO** | Teste do Pezinho (pediátrico) ativo no adulto; Os exames da anemia estão todos lá, mas o caso de anemia também é cobrado por "02 hemogramas recentes (intervalo mínimo de 1 mês)" (leucopenia), "Coagulograma e Plaquetas" (hemorrá… |
| 50 | 4.8.1 Hematologia Adulto | DISTÚRBIOS DOS LEUCÓCITOS (LEUCOPENIA) | Ambulatório 1ª vez - Hematologia (Adulto) | **ERRADO** | Teste do Pezinho (pediátrico) ativo no adulto; A leucopenia exige 2 caixas no manual (encaminhamento + 2 hemogramas ≥ 1 mês). Em produção ela recebe mais 7 caixas obrigatórias: sorologias, FAN/função hepática e renal, eletrofor… |
| 51 | 4.8.1 Hematologia Adulto | DISTÚRBIOS HEMORRÁGICOS | Ambulatório 1ª vez - Hematologia (Adulto) | **ERRADO** | Teste do Pezinho (pediátrico) ativo no adulto; O distúrbio hemorrágico recebe 7 caixas obrigatórias que não são dele (anemia, leucopenia e Teste do Pezinho). |
| 52 | 4.8.2 Hematologia Pediátrica (seção) | Hematologia Pediátrica — Critérios de exclusão + "Necessita informar" | Ambulatório 1ª vez - Hematologia (Infantil) | **AUSENTE** | Procedimento sem nenhuma regra (2 pedidos "Sem regras"). |
| 53 | 4.8.2 Hematologia Pediátrica | ANEMIA | Ambulatório 1ª vez - Hematologia (Infantil) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (o conteúdo desta tabela foi parar em Hematologia (Adulto)). Os exames obrigatórios mudam por ramo dentro da mesma fila: sem regra condicional, ou se cobra a mais (como hoje no adulto) ou se deixa opcional. |
| 54 | 4.8.2 Hematologia Pediátrica | DISTÚRBIOS DOS LEUCÓCITOS (LEUCOPENIA) | Ambulatório 1ª vez - Hematologia (Infantil) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (o conteúdo desta tabela foi parar em Hematologia (Adulto)). Os exames obrigatórios mudam por ramo dentro da mesma fila: sem regra condicional, ou se cobra a mais (como hoje no adulto) ou se deixa opcional. |
| 55 | 4.8.2 Hematologia Pediátrica | DISTÚRBIOS HEMORRÁGICOS | Ambulatório 1ª vez - Hematologia (Infantil) | **NAO_REPRESENTAVEL_HOJE** | Sem regra (o conteúdo desta tabela foi parar em Hematologia (Adulto)). Os exames obrigatórios mudam por ramo dentro da mesma fila: sem regra condicional, ou se cobra a mais (como hoje no adulto) ou se deixa opcional. |

## Blocos por recurso

### 1. 4.4 Ortopedia (regras gerais da seção) — Ortopedia — abertura da seção + "ATENÇÃO" + "Orientações gerais"

1. **Seção / página / recurso:** 4.4 Ortopedia (regras gerais da seção) · p.25, 29 · Ortopedia — abertura da seção + "ATENÇÃO" + "Orientações gerais"
2. **Estrutura:** INCLUSAO_EXCLUSAO (com CONDICIONAL por idade: fila adulta × infantil; e por indicação: fratura)
3. **Requisitos (literal):**
   - [exclusão/redirecionamento] Diagnóstico de TUMOR ÓSSEO → fila "Ambulatório de 1ª vez – Tumores do Tecido Ósseo-Conectivo".
   - [exclusão/redirecionamento] Suspeita de metástase óssea → inserir de acordo com o sítio primário.
   - [exclusão] Lesões intrasubstanciais/meniscais degenerativas, tendinites/sinovites e condropatias patelares → tratar nos ambulatórios locais.
   - [exclusão] "Critérios de Exclusão: Vigência de infecção." (p.29: infecção aguda/ativa é pendenciada/cancelada; perguntar o hospital da 1ª cirurgia, se houve contato para reabordagem e se a infecção está em remissão).
   - [solicitante] "Todos os encaminhamentos médicos deverão ser encaminhados por Ortopedista."
   - [idade] "Filas adultas: maiores de 16 anos." / "Menores de 16 anos deverão ser inseridos nas filas infantis."
   - [documento — CONDICIONAL a fratura] "Em casos de fraturas, todas as solicitações deverão ter o exame de Radiografia da fratura com a data do evento."
   - [CID] CIDs específicos à patologia descrita; CID inespecífico ou incoerente é pendenciado/cancelado.
   - [orientação] A unidade acompanha o pedido (pendências e follow-up) e comunica o agendamento; a Central regula só o acesso; não interfere na fila interna.
4. **Recurso SER:** Todas as 11 filas NAO_AE de ortopedia: Ambulatório 1ª vez em Ortopedia - Joelho (Adulto); Ambulatório 1ª Vez em Ortopedia - Joelho (Infantil); Ambulatório 1ª vez em Ortopedia - Mão (Adulto); Ambulatório 1ª vez em Ortopedia - Mão (Infantil); Ambulatório 1ª vez em Ortopedia - Quadril (Adulto); Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto); Ambulatório 1ª Vez em Ortopedia - Ortopedia Pediátrica (exceto coluna); Ambulatório 1ª vez em Ortopedia - Pé & Tornozelo (Adulto); Ambulatório 1ª vez em Ortopedia - Trauma Ortopédico de Média Complexidade; Ambulatório 1ª vez em Ortopedia - Sequelas Pós Traumáticas (Adulto); Ambulatório 1ª Vez em Ortopedia - Reconstrução e Alongamento Ósseo (Fixador Externo). Destino do redirecionamento: Ambulatório 1ª Vez - Tumores do Tecido Ósseo e Conectivo (Adulto) / Ambulatório 1ª Vez - Tumores do Tecido Ósseo e Conectivo (Infantil).
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)`; `Ambulatório 1ª Vez em Ortopedia - Joelho (Infantil)`; `Ambulatório 1ª vez em Ortopedia - Mão (Adulto)`; `Ambulatório 1ª vez em Ortopedia - Mão (Infantil)`; `Ambulatório 1ª vez em Ortopedia - Quadril (Adulto)`; `Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)`; `Ambulatório 1ª Vez em Ortopedia - Ortopedia Pediátrica (exceto coluna)`; `Ambulatório 1ª vez em Ortopedia - Pé & Tornozelo (Adulto)`; `Ambulatório 1ª vez em Ortopedia - Trauma Ortopédico de Média Complexidade`; `Ambulatório 1ª vez em Ortopedia - Sequelas Pós Traumáticas (Adulto)`; `Ambulatório 1ª Vez em Ortopedia - Reconstrução e Alongamento Ósseo (Fixador Externo)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhum dos 11 procedimentos de ortopedia (nem os 2 de coluna) tem regra — ativa ou inativa. A análise do espelho dá "Sem regras": Joelho Adulto 104 pedidos, Quadril 12, Ombro/Cotovelo 10, Reconstrução 4, Pé 2, Mão Adulto 1, Mão Infantil 1, Ortopedia Pediátrica 1 (Coluna Adulto 401, ver 4.4.3).
   - A extração antiga (spike-e) rotulou estas linhas como "ONCOLOGIA - JOELHO…", "ONCOLOGIA - ESCOLIOSE", "ONCOLOGIA - HÉRNIAS DE DISCO" com pareamento SEM_PAR — por isso nada foi importado (e nada caiu em procedimento de Oncologia).
   - Contradição interna do manual: a abertura manda tendinites/sinovites para o ambulatório local, mas a tabela da Mão (4.4.2) tem a linha TENDINOPATIAS. A exclusão não pode virar regra global de ortopedia.
   - "maiores de 16" × "menores de 16" deixa 16 anos exatos sem fila; leitura coerente com "Joelho infantil até 15" e "Pediatria até 15": adulto ≥ 16, infantil ≤ 15.
   - O requisito "ortopedista" não é dedutível: o espelho não traz a especialidade/CBO do solicitante.
8. **Proposta (modelo atual):**
   - Em cada uma das 11 filas: Documento obrigatório (Bloqueia): encaminhamento médico "de ORTOPEDISTA, descrevendo de forma clara e detalhada o caso e indicando o procedimento".
   - Pergunta Sim/Não (Não bloqueia): "O encaminhamento foi emitido por ortopedista?"
   - Pergunta Sim/Não (Sim bloqueia): "Há infecção ativa/aguda (vigência de infecção)?" — com o texto da p.29 na descrição.
   - Dedutível idade (Bloqueia): filas "(Adulto)" idade_min 16 (Joelho, Mão, Quadril, Ombro/Cotovelo, Pé & Tornozelo, Sequelas); filas "(Infantil)"/Pediátrica idade_max 15 (Joelho, Mão, Ortopedia Pediátrica). Trauma Médio e Reconstrução/Fixador não têm faixa no nome — sem regra de idade.
   - Dedutível CID excluídos [C40, C41, D16, C79.5] com severidade Ressalva (sugestão nossa — o manual não lista CID): "tumor ósseo → Tumores do Tecido Ósseo e Conectivo; metástase → fila do sítio primário". NÃO aplicar à Coluna (a tabela da coluna tem a linha TUMOR).
   - Só no Joelho (Adulto): Pergunta Sim/Não (Sim bloqueia) "É lesão intrasubstancial/meniscal degenerativa, sinovite ou condropatia patelar?" — não estender à Mão.
   - Informativa: CID específico obrigatório; acompanhamento do pedido pela unidade.
   - **Só com regra condicional:** "Radiografia da fratura com a data do evento" só vale quando a indicação é fratura (Trauma Médio, Ombro-fratura, Coluna-fraturas, Mão-fraturas) → hoje embutir no texto da opção/documento dessas indicações; no futuro, Documento obrigatório SE indicação = fratura.

### 2. 4.4.1 Ortopedia – Joelho — LESÕES LIGAMENTARES/ MENISCO EM PACIENTES COM MENOS DE 50 ANOS

1. **Seção / página / recurso:** 4.4.1 Ortopedia – Joelho · p.25 · LESÕES LIGAMENTARES/ MENISCO EM PACIENTES COM MENOS DE 50 ANOS
2. **Estrutura:** CONDICIONAL (eixo: indicação + idade < 50, dentro da fila Joelho (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] descrever de forma clara e detalhada, indicando o procedimento.
   - [documento] laudo do exame de ressonância magnética.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Procedimento sem nenhuma regra (104 pedidos "Sem regras").
   - O laudo de RNM é obrigatório só nesta indicação (a artroplastia pede RX com carga, não RNM): uma caixa "Laudo de RNM" obrigatória para a fila inteira cobraria a mais.
8. **Proposta (modelo atual):**
   - Proposta comum ao Joelho (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico (indicando o procedimento) + Documento obrigatório "Exame de imagem do joelho exigido para a indicação (ver opção marcada)" + Pergunta lista basta-uma (Não → Ressalva) "Qual indicação, com o exame exigido?": [1] Lesão ligamentar/meniscal, < 50 anos — laudo de RNM; [2] Lesão ligamentar/meniscal, ≥ 50 anos, com indicação cirúrgica confirmada pelo ortopedista após discussão do tratamento conservador — laudo de RNM + imagem de RX com carga dos joelhos; [3] Artroplastia total de joelho — imagem de RX com carga dos joelhos; [4] Osteotomia/deformidade angular — laudo de RNM + RX com carga (RX panorâmico de MMII se possível); [5] Instabilidade femoropatelar — laudo de TC com TA-GT e/ou laudo de RNM. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** Documento "Laudo de RNM do joelho" obrigatório SE indicação = lesão ligamentar/menisco.

### 3. 4.4.1 Ortopedia – Joelho — LESÕES LIGAMENTARES/ MENISCO EM PACIENTES A PARTIR DE 50 ANOS

1. **Seção / página / recurso:** 4.4.1 Ortopedia – Joelho · p.25 · LESÕES LIGAMENTARES/ MENISCO EM PACIENTES A PARTIR DE 50 ANOS
2. **Estrutura:** CONDICIONAL (eixo: idade ≥ 50 × indicação cirúrgica confirmada pelo ortopedista)
3. **Requisitos (literal):**
   - [critério clínico/orientação] "Geralmente, para pacientes a partir de 50 anos não se aplica solicitação do procedimento" — discutir com o ortopedista o tratamento conservador.
   - [critério clínico] Caso o procedimento cirúrgico seja indicado após avaliação pelo ortopedista:
   - [documento] laudo do exame de RNM, e
   - [documento, "obrigatório"] imagem do exame de RX com carga dos joelhos.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra.
   - ARMADILHA: a extração antiga transformou a linha em Dedutível idade_min=50 ("Geralmente, para pacientes a partir de 50 anos…", spike-e, REUNI p.25). Se importada, barra todo paciente com menos de 50 anos da fila do Joelho e deixa passar quem tem 50+ — inverte o sentido. A idade aqui não é barreira, é gatilho de exigência extra.
8. **Proposta (modelo atual):**
   - Opção [2] da lista do Joelho (Adulto) (ver linha anterior). Pergunta Sim/Não (Ressalva) "Paciente com 50 anos ou mais, com lesão ligamentar/meniscal: a indicação cirúrgica foi confirmada pelo ortopedista após discutir o tratamento conservador?" — a Ressalva respeita o "geralmente".
   - **Só com regra condicional:** SE idade ≥ 50 (deduzível do nascimento) E indicação = lesão ligamentar/menisco → Pergunta "indicação cirúrgica confirmada?" (Não = Ressalva) + Documento "Imagem de RX com carga dos joelhos" obrigatório.

### 4. 4.4.1 Ortopedia – Joelho — ARTROPLASTIA TOTAL DE JOELHO

1. **Seção / página / recurso:** 4.4.1 Ortopedia – Joelho · p.25 · ARTROPLASTIA TOTAL DE JOELHO
2. **Estrutura:** CONDICIONAL (eixo: indicação, dentro da fila Joelho (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento] imagem do exame de RX com carga dos joelhos (em negrito no manual).
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra.
   - O RX com carga é exigido aqui e no ≥ 50 anos/osteotomia, mas não na lesão < 50 nem na instabilidade femoropatelar.
8. **Proposta (modelo atual):**
   - Opção [3] da lista do Joelho (Adulto).
   - **Só com regra condicional:** Documento "Imagem de RX com carga dos joelhos" obrigatório SE indicação ∈ {artroplastia, osteotomia, lesão ≥ 50 anos}.

### 5. 4.4.1 Ortopedia – Joelho — OSTEOTOMIA/ DEFORMIDADES ANGULARES

1. **Seção / página / recurso:** 4.4.1 Ortopedia – Joelho · p.25 · OSTEOTOMIA/ DEFORMIDADES ANGULARES
2. **Estrutura:** CONDICIONAL (eixo: indicação), com item opcional
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento] laudo de RNM;
   - [documento] RX com carga dos joelhos;
   - [documento opcional] "se possível", RX panorâmico dos membros inferiores.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra.
8. **Proposta (modelo atual):**
   - Opção [4] da lista do Joelho (Adulto) + Documento opcional (obrigatorio=false) "RX panorâmico dos membros inferiores (se possível)".
   - **Só com regra condicional:** Documentos "Laudo de RNM" e "RX com carga" obrigatórios SE indicação = osteotomia/deformidade angular.

### 6. 4.4.1 Ortopedia – Joelho — INSTABILIDADE FEMOROPATELAR

1. **Seção / página / recurso:** 4.4.1 Ortopedia – Joelho · p.25 · INSTABILIDADE FEMOROPATELAR
2. **Estrutura:** CONDICIONAL (eixo: indicação) com LISTA_BASTA_UM interna (TC TA-GT e/ou RNM)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento — basta um] laudo de TC com estudo TA-GT (preferencialmente) e/ou laudo de RNM.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Joelho (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra.
8. **Proposta (modelo atual):**
   - Opção [5] da lista do Joelho (Adulto).
   - **Só com regra condicional:** Documento "Laudo de TC com TA-GT ou de RNM" obrigatório SE indicação = instabilidade femoropatelar.

### 7. 4.4.1 Ortopedia – Joelho — JOELHO (INFANTIL – ATÉ 15 ANOS)

1. **Seção / página / recurso:** 4.4.1 Ortopedia – Joelho · p.25 · JOELHO (INFANTIL – ATÉ 15 ANOS)
2. **Estrutura:** SIMPLES na base + CONDICIONAL nos extras (eixo: indicação — deformidade angular / lesão menisco-ligamentar)
3. **Requisitos (literal):**
   - [idade] até 15 anos.
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento] imagem de RX do joelho em AP e perfil — "para todos os casos".
   - [documento — condicional] RX panorâmico de membros inferiores nos casos de deformidades angulares.
   - [documento — condicional] laudo da RNM nas lesões menisco-ligamentares (além do RX AP e perfil).
4. **Recurso SER:** Ambulatório 1ª Vez em Ortopedia - Joelho (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez em Ortopedia - Joelho (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra.
   - A extração antiga rotulou como "ONCOLOGIA - JOELHO (INFANTIL – ATÉ 15 ANOS)" (SEM_PAR) e só pegou os dois extras condicionais, não a base.
8. **Proposta (modelo atual):**
   - Dedutível idade_max 15 (Bloqueia).
   - Documento obrigatório (Bloqueia): encaminhamento médico indicando o procedimento.
   - Documento obrigatório (Bloqueia): "Imagem de RX do joelho em AP e perfil".
   - Documento opcional (obrigatorio=false): "RX panorâmico de MMII (deformidade angular) / laudo de RNM (lesão menisco-ligamentar)".
   - + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** Tornar obrigatório o RX panorâmico SE deformidade angular e o laudo de RNM SE lesão menisco-ligamentar.

### 8. 4.4.2 Ortopedia – Mão — SÍNDROME DO TÚNEL DO CARPO

1. **Seção / página / recurso:** 4.4.2 Ortopedia – Mão · p.26 · SÍNDROME DO TÚNEL DO CARPO
2. **Estrutura:** LISTA_BASTA_UM ("USG ou eletroneuromiografia, com laudo, se possível ou um exame clínico detalhado")
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento — opcional/alternativo] USG ou eletroneuromiografia, com laudo, "se possível", OU exame clínico detalhado (conteúdo do encaminhamento).
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Mão (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Mão (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra.
   - Nada além do encaminhamento é obrigatório: o exame pode ser substituído por exame clínico detalhado — cadastrar USG/ENMG como obrigatório seria erro.
8. **Proposta (modelo atual):**
   - Proposta comum à Mão (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico (indicando o procedimento; o "exame clínico detalhado" da linha do túnel do carpo cabe aqui) + Documento opcional (obrigatorio=false) "Exame de imagem/ENMG com laudo, conforme a lesão" + Pergunta lista basta-uma (Não → Ressalva) "Qual lesão, com o exame exigido?": [1] Síndrome do túnel do carpo (USG ou ENMG com laudo se possível, ou exame clínico detalhado); [2] Cisto sinovial / dedo em gatilho (USG ou RNM com laudo, se possível); [3] Tendinopatia (RNM com laudo, se possível); [4] Fratura de escafoide não consolidada — COM laudo de RNM ou TC; [5] Necrose de ossos do punho / instabilidade cárpica — COM laudo de RNM; [6] Fratura não consolidada/viciosa, pseudoartrose ou artrose do punho — COM RX de punho AP e perfil ou TC, com laudo. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** —

### 9. 4.4.2 Ortopedia – Mão — CISTO SINOVIAL E DEDO EM GATILHO

1. **Seção / página / recurso:** 4.4.2 Ortopedia – Mão · p.26 · CISTO SINOVIAL E DEDO EM GATILHO
2. **Estrutura:** SIMPLES (exame "se possível")
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento opcional] exame, com laudo, de USG ou RNM, "se possível".
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Mão (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Mão (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra.
8. **Proposta (modelo atual):**
   - Coberto pela proposta comum da Mão (Adulto), opção [2].
   - **Só com regra condicional:** —

### 10. 4.4.2 Ortopedia – Mão — TENDINOPATIAS

1. **Seção / página / recurso:** 4.4.2 Ortopedia – Mão · p.26 · TENDINOPATIAS
2. **Estrutura:** SIMPLES (exame "se possível")
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento opcional] RNM com laudo, "se possível".
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Mão (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Mão (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra.
   - Conflita com a abertura da seção 4.4 ("tendinites… deverão ser tratadas nos ambulatórios locais"). Na Mão, a tabela prevalece.
8. **Proposta (modelo atual):**
   - Coberto pela proposta comum da Mão (Adulto), opção [3].
   - **Só com regra condicional:** —

### 11. 4.4.2 Ortopedia – Mão — FRATURAS DE ESCAFOIDE NÃO CONSOLIDADA

1. **Seção / página / recurso:** 4.4.2 Ortopedia – Mão · p.26 · FRATURAS DE ESCAFOIDE NÃO CONSOLIDADA
2. **Estrutura:** CONDICIONAL (eixo: lesão) com LISTA_BASTA_UM interna (RNM ou TC)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento — basta um] laudo de RNM ou TC.
   - [documento — orientação geral p.29] radiografia da fratura com a data do evento.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Mão (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Mão (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra.
   - Exame obrigatório só para esta lesão, numa fila em que três lesões têm exame apenas "se possível".
8. **Proposta (modelo atual):**
   - Opção [4] da proposta comum da Mão (Adulto).
   - **Só com regra condicional:** Documento "Laudo de RNM ou TC" obrigatório SE lesão = fratura de escafoide não consolidada.

### 12. 4.4.2 Ortopedia – Mão — NECROSE DE OSSOS DO PUNHO, INSTABILIDADES CÁRPICAS

1. **Seção / página / recurso:** 4.4.2 Ortopedia – Mão · p.26 · NECROSE DE OSSOS DO PUNHO, INSTABILIDADES CÁRPICAS
2. **Estrutura:** CONDICIONAL (eixo: lesão)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento] "exame laudo de RNM com laudo" (laudo de RNM).
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Mão (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Mão (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra.
8. **Proposta (modelo atual):**
   - Opção [5] da proposta comum da Mão (Adulto).
   - **Só com regra condicional:** Documento "Laudo de RNM do punho" obrigatório SE lesão = necrose/instabilidade cárpica.

### 13. 4.4.2 Ortopedia – Mão — FRATURAS NÃO CONSOLIDADAS, VICIOSAS, PSEUDOARTROSE, ARTROSES DO PUNHO

1. **Seção / página / recurso:** 4.4.2 Ortopedia – Mão · p.26 · FRATURAS NÃO CONSOLIDADAS, VICIOSAS, PSEUDOARTROSE, ARTROSES DO PUNHO
2. **Estrutura:** CONDICIONAL (eixo: lesão) com LISTA_BASTA_UM interna (RX punho AP e perfil ou TC)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento — basta um] exame, com laudo, de RX de punho AP e perfil ou TC.
   - [documento — orientação geral p.29, se fratura] radiografia da fratura com a data do evento.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Mão (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Mão (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra.
8. **Proposta (modelo atual):**
   - Opção [6] da proposta comum da Mão (Adulto).
   - **Só com regra condicional:** Documento "RX de punho AP e perfil ou TC, com laudo" obrigatório SE lesão ∈ {fratura não consolidada/viciosa, pseudoartrose, artrose do punho}.

### 14. 4.4.2 Ortopedia – Mão — MÃO (INFANTIL)

1. **Seção / página / recurso:** 4.4.2 Ortopedia – Mão · p.26 · MÃO (INFANTIL)
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando o procedimento.
   - [documento] exame de RX de mão AP e oblíqua.
   - [idade — orientação geral p.29] menores de 16 anos nas filas infantis.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Mão (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Mão (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra (1 pedido "Sem regras").
8. **Proposta (modelo atual):**
   - Dedutível idade_max 15 (Bloqueia).
   - Documento obrigatório (Bloqueia): encaminhamento médico indicando o procedimento.
   - Documento obrigatório (Bloqueia): "RX de mão AP e oblíqua".
   - + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** —

### 15. 4.4.3 Ortopedia – Coluna (regra da seção) — Coluna — "Todos os encaminhamentos médicos deverão ser encaminhados por Neurocirurgião ou Ortopedista."

1. **Seção / página / recurso:** 4.4.3 Ortopedia – Coluna (regra da seção) · p.26 · Coluna — "Todos os encaminhamentos médicos deverão ser encaminhados por Neurocirurgião ou Ortopedista."
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - [solicitante] encaminhamento de neurocirurgião ou ortopedista.
   - [herdado de 4.4] exclusão por infecção; filas adultas ≥ 16 / infantis ≤ 15; fratura com RX e data do evento; CID específico.
4. **Recurso SER:** Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto)`; `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra nas duas filas — Coluna Adulto é o MAIOR volume do trecho: 401 pedidos no espelho, todos "Sem regras".
   - O SER tem só duas filas de coluna (Adulto/Infantil); as oito linhas da tabela são indicações dentro delas.
8. **Proposta (modelo atual):**
   - Proposta comum às duas filas de Coluna: Documento obrigatório (Bloqueia): encaminhamento médico "descrevendo de forma clara e detalhada (hipótese diagnóstica no tumor; se há patologia primária na escoliose)" + Documento obrigatório (Bloqueia) "Exame de imagem da coluna com laudo, conforme a indicação" + Pergunta lista basta-uma (Não → Bloqueia) "O caso se enquadra em uma destas situações, com o exame exigido?": [1] Hérnia de disco com compressão medular; [2] com compressão radicular; [3] hérnia extrusa — (1–3 com laudo de RNM, preferência, ou TC); [4] Abaulamento discal com ENMG demonstrando comprometimento radicular; [5] Escoliose com ângulo de Cobb > 30º em RX panorâmico de coluna AP e perfil (laudo e imagem); [6] Tumor, com hipótese diagnóstica (laudo de RNM, preferência, ou TC); [7] Espondiloartrose (RX e/ou TC e/ou RNM); [8] Espondilólise (laudo de TC e/ou RNM); [9] Espondilolistese (RX da coluna e/ou TC e/ou RNM); [10] Fratura cervical; [11] Fratura com déficit neurológico; [12] Fratura tóraco-lombar com compressão > 50% do corpo vertebral; [13] Fratura com retropulsão do muro posterior com compressão do canal medular — (10–13 com RX e/ou TC e/ou RNM, e RX da fratura com data do evento); [14] Outra patologia cirúrgica da coluna que NÃO seja hérnia, escoliose ou fratura — imagem e laudo de TC e/ou RNM. + Pergunta Sim/Não (Não bloqueia) "Encaminhamento de neurocirurgião ou ortopedista?" + Pergunta infecção ativa (Sim bloqueia) + Dedutível idade (Adulto idade_min 16; Infantil idade_max 15). NÃO aplicar a exclusão de CID de tumor ósseo da seção 4.4 às filas de coluna.
   - **Só com regra condicional:** Ver as linhas da tabela: a modalidade de exame muda por indicação.

### 16. 4.4.3 Ortopedia – Coluna — HÉRNIAS DE DISCO

1. **Seção / página / recurso:** 4.4.3 Ortopedia – Coluna · p.26 · HÉRNIAS DE DISCO
2. **Estrutura:** CONDICIONAL (eixo: indicação) + LISTA_BASTA_UM (itens a–d)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento — basta um] laudo de RNM (preferência) ou TC da lesão.
   - [critério clínico — "Inclui", basta um] a. compressão medular; b. compressão radicular; c. hérnia extrusa; d. abaulamentos discais com eletroneuromiografia demonstrando comprometimento radicular (ENMG = documento implícito no item d).
4. **Recurso SER:** Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto)`; `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (Coluna Adulto: 401 pedidos "Sem regras").
   - A extração antiga gravou o "Inclui:" inteiro como uma pergunta única (rótulo "ONCOLOGIA - HÉRNIAS DE DISCO", SEM_PAR) — a lista a–d é "basta um", não um bloco Sim/Não.
   - RX não serve para hérnia, mas serve para espondiloartrose/listese/fratura: a caixa genérica de imagem aceita RX.
8. **Proposta (modelo atual):**
   - Opções [1]–[4] da proposta comum da Coluna (ver regra da seção 4.4.3).
   - **Só com regra condicional:** Documento "Laudo de RNM ou TC" obrigatório SE indicação = hérnia; "ENMG" obrigatória SE item d.

### 17. 4.4.3 Ortopedia – Coluna — ESCOLIOSE

1. **Seção / página / recurso:** 4.4.3 Ortopedia – Coluna · p.26 · ESCOLIOSE
2. **Estrutura:** CONDICIONAL (eixo: indicação) com critério de valor
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, indicando se há patologia primária.
   - [documento] laudo e imagem de RX panorâmico de coluna em AP e perfil.
   - [valor] ângulo de Cobb > 30º.
4. **Recurso SER:** Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto)`; `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (Coluna Adulto: 401 pedidos "Sem regras").
   - A extração antiga ("ONCOLOGIA - ESCOLIOSE", SEM_PAR) pegou só o documento, sem o "indicar se há patologia primária".
8. **Proposta (modelo atual):**
   - Opção [5] da proposta comum da Coluna (ver regra da seção 4.4.3).
   - **Só com regra condicional:** Documento "RX panorâmico AP e perfil (laudo e imagem)" obrigatório SE indicação = escoliose; o Cobb > 30º não é dedutível (não há campo).

### 18. 4.4.3 Ortopedia – Coluna — TUMOR

1. **Seção / página / recurso:** 4.4.3 Ortopedia – Coluna · p.27 · TUMOR
2. **Estrutura:** CONDICIONAL (eixo: indicação)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada, com hipótese diagnóstica.
   - [documento — basta um] laudo de RNM (preferência) ou TC da lesão.
4. **Recurso SER:** Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto)`; `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (Coluna Adulto: 401 pedidos "Sem regras").
   - Choca com a abertura da 4.4 (tumor ósseo → Tumores do Tecido Ósseo-Conectivo): a exclusão por CID C40/C41/D16 não pode valer na Coluna.
8. **Proposta (modelo atual):**
   - Opção [6] da proposta comum da Coluna (ver regra da seção 4.4.3).
   - **Só com regra condicional:** Documento "Laudo de RNM ou TC" obrigatório SE indicação = tumor.

### 19. 4.4.3 Ortopedia – Coluna — ESPONDILOARTROSE

1. **Seção / página / recurso:** 4.4.3 Ortopedia – Coluna · p.27 · ESPONDILOARTROSE
2. **Estrutura:** LISTA_BASTA_UM (RX/TC/RNM)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento — basta um] imagem de RX e/ou laudo de TC e/ou laudo da RNM.
4. **Recurso SER:** Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto)`; `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra (Coluna Adulto: 401 pedidos "Sem regras").
8. **Proposta (modelo atual):**
   - Opção [7] da proposta comum da Coluna (ver regra da seção 4.4.3).
   - **Só com regra condicional:** — (a caixa genérica "exame de imagem da coluna" já é fiel).

### 20. 4.4.3 Ortopedia – Coluna — ESPONDILÓLISE

1. **Seção / página / recurso:** 4.4.3 Ortopedia – Coluna · p.27 · ESPONDILÓLISE
2. **Estrutura:** CONDICIONAL (eixo: indicação) com LISTA_BASTA_UM
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento — basta um] laudo de TC e/ou laudo da RNM (RX não basta).
4. **Recurso SER:** Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto)`; `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (Coluna Adulto: 401 pedidos "Sem regras").
8. **Proposta (modelo atual):**
   - Opção [8] da proposta comum da Coluna (ver regra da seção 4.4.3).
   - **Só com regra condicional:** Documento "Laudo de TC ou RNM" obrigatório SE indicação = espondilólise.

### 21. 4.4.3 Ortopedia – Coluna — ESPONDILOLISTESE

1. **Seção / página / recurso:** 4.4.3 Ortopedia – Coluna · p.27 · ESPONDILOLISTESE
2. **Estrutura:** LISTA_BASTA_UM (RX/TC/RNM)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento — basta um] imagem de RX da coluna e/ou laudo de TC e/ou laudo de RNM.
4. **Recurso SER:** Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto)`; `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra (Coluna Adulto: 401 pedidos "Sem regras").
8. **Proposta (modelo atual):**
   - Opção [9] da proposta comum da Coluna (ver regra da seção 4.4.3).
   - **Só com regra condicional:** — (caixa genérica é fiel).

### 22. 4.4.3 Ortopedia – Coluna — FRATURAS

1. **Seção / página / recurso:** 4.4.3 Ortopedia – Coluna · p.27 · FRATURAS
2. **Estrutura:** LISTA_BASTA_UM (itens 1–4) + documento de imagem
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento — basta um] imagem de RX e/ou imagem e laudo de TC e/ou imagem e laudo da RNM da fratura.
   - [critério clínico — "Inclui", basta um] 1. fraturas cervicais (todas); 2. com déficit neurológico; 3. tóraco-lombares ("toroco-lombares", sic) com compressão > 50% do corpo vertebral; 4. com retropulsão do muro posterior com compressão do canal medular.
   - [documento — orientação geral p.29] radiografia da fratura com a data do evento.
4. **Recurso SER:** Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto)`; `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra (Coluna Adulto: 401 pedidos "Sem regras").
8. **Proposta (modelo atual):**
   - Opções [10]–[13] da proposta comum da Coluna (ver regra da seção 4.4.3).
   - **Só com regra condicional:** "Data do evento na radiografia" só para fratura → embutido no texto da opção.

### 23. 4.4.3 Ortopedia – Coluna — PATOLOGIA CIRÚRGICA DA COLUNA VERTEBRAL (ADULTO E INFANTIL)

1. **Seção / página / recurso:** 4.4.3 Ortopedia – Coluna · p.27 · PATOLOGIA CIRÚRGICA DA COLUNA VERTEBRAL (ADULTO E INFANTIL)
2. **Estrutura:** CONDICIONAL (eixo: indicação — linha "guarda-chuva" com o nome da própria fila)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento — basta um] imagem e laudo de TC e/ou imagem e laudo de RNM "da fratura" (sic — o texto parece copiado da linha de fraturas).
4. **Recurso SER:** Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto) ; Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Adulto)`; `Ambulatório 1ª Vez - Patologia Cirúrgica da Coluna Vertebral (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (Coluna Adulto: 401 pedidos "Sem regras").
   - A linha tem o mesmo nome das duas filas do SER; ambígua: pode ser a regra das "demais" patologias ou um resumo. O "da fratura" no texto é provável erro do manual.
8. **Proposta (modelo atual):**
   - Opção [14] da proposta comum da Coluna (ver regra da seção 4.4.3).
   - **Só com regra condicional:** Documento "Imagem e laudo de TC ou RNM" obrigatório SE indicação não for hérnia/escoliose/fratura.

### 24. 4.4.4 Ortopedia – Quadril Adulto — COXARTROSE

1. **Seção / página / recurso:** 4.4.4 Ortopedia – Quadril Adulto · p.27 · COXARTROSE
2. **Estrutura:** CONDICIONAL (eixo: indicação, dentro da fila Quadril (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] exame de RX da bacia + perfil do lado acometido.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Quadril (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Quadril (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (12 pedidos "Sem regras").
   - O exame exigido muda por indicação (RX × RNM × ambos); só "algum exame de imagem do quadril" é comum a todas.
8. **Proposta (modelo atual):**
   - Proposta comum ao Quadril (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico + Documento obrigatório "Exame de imagem do quadril do lado acometido, conforme a indicação" + Pergunta lista basta-uma (Não → Ressalva) "Qual indicação, com o exame exigido?": [1] Coxartrose — RX da bacia + perfil do lado acometido; [2] Osteonecrose — RX da bacia + perfil do lado acometido + RNM (laudo e imagem); [3] Impacto femoroacetabular — laudo de RNM do lado acometido; [4] Revisão de artroplastia total do quadril — RX da bacia + perfil do lado acometido. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** RX bacia + perfil obrigatório SE coxartrose.

### 25. 4.4.4 Ortopedia – Quadril Adulto — OSTEONECROSE

1. **Seção / página / recurso:** 4.4.4 Ortopedia – Quadril Adulto · p.27 · OSTEONECROSE
2. **Estrutura:** CONDICIONAL (eixo: indicação, dentro da fila Quadril (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] RX da bacia + perfil do lado acometido;
   - [documento] RNM (laudo e imagem).
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Quadril (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Quadril (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (12 pedidos "Sem regras").
   - O exame exigido muda por indicação (RX × RNM × ambos); só "algum exame de imagem do quadril" é comum a todas.
8. **Proposta (modelo atual):**
   - Proposta comum ao Quadril (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico + Documento obrigatório "Exame de imagem do quadril do lado acometido, conforme a indicação" + Pergunta lista basta-uma (Não → Ressalva) "Qual indicação, com o exame exigido?": [1] Coxartrose — RX da bacia + perfil do lado acometido; [2] Osteonecrose — RX da bacia + perfil do lado acometido + RNM (laudo e imagem); [3] Impacto femoroacetabular — laudo de RNM do lado acometido; [4] Revisão de artroplastia total do quadril — RX da bacia + perfil do lado acometido. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** RX + RNM obrigatórios SE osteonecrose.

### 26. 4.4.4 Ortopedia – Quadril Adulto — IMPACTO FEMORO-ACETABULAR

1. **Seção / página / recurso:** 4.4.4 Ortopedia – Quadril Adulto · p.27 · IMPACTO FEMORO-ACETABULAR
2. **Estrutura:** CONDICIONAL (eixo: indicação, dentro da fila Quadril (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] laudo do exame de RNM do lado acometido.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Quadril (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Quadril (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (12 pedidos "Sem regras").
   - O exame exigido muda por indicação (RX × RNM × ambos); só "algum exame de imagem do quadril" é comum a todas.
8. **Proposta (modelo atual):**
   - Proposta comum ao Quadril (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico + Documento obrigatório "Exame de imagem do quadril do lado acometido, conforme a indicação" + Pergunta lista basta-uma (Não → Ressalva) "Qual indicação, com o exame exigido?": [1] Coxartrose — RX da bacia + perfil do lado acometido; [2] Osteonecrose — RX da bacia + perfil do lado acometido + RNM (laudo e imagem); [3] Impacto femoroacetabular — laudo de RNM do lado acometido; [4] Revisão de artroplastia total do quadril — RX da bacia + perfil do lado acometido. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** Laudo de RNM obrigatório SE impacto femoroacetabular.

### 27. 4.4.4 Ortopedia – Quadril Adulto — REVISÃO DE ARTROPLASTIA TOTAL DO QUADRIL

1. **Seção / página / recurso:** 4.4.4 Ortopedia – Quadril Adulto · p.27 · REVISÃO DE ARTROPLASTIA TOTAL DO QUADRIL
2. **Estrutura:** CONDICIONAL (eixo: indicação, dentro da fila Quadril (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] exame de RX da bacia + perfil do lado acometido.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Quadril (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Quadril (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (12 pedidos "Sem regras").
   - O exame exigido muda por indicação (RX × RNM × ambos); só "algum exame de imagem do quadril" é comum a todas.
8. **Proposta (modelo atual):**
   - Proposta comum ao Quadril (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico + Documento obrigatório "Exame de imagem do quadril do lado acometido, conforme a indicação" + Pergunta lista basta-uma (Não → Ressalva) "Qual indicação, com o exame exigido?": [1] Coxartrose — RX da bacia + perfil do lado acometido; [2] Osteonecrose — RX da bacia + perfil do lado acometido + RNM (laudo e imagem); [3] Impacto femoroacetabular — laudo de RNM do lado acometido; [4] Revisão de artroplastia total do quadril — RX da bacia + perfil do lado acometido. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** RX bacia + perfil obrigatório SE revisão de ATQ.

### 28. 4.4.5 Ortopedia – Ombro/Cotovelo — LESÃO LIGAMENTARES OU DO MANGUITO ROTADOR

1. **Seção / página / recurso:** 4.4.5 Ortopedia – Ombro/Cotovelo · p.28 · LESÃO LIGAMENTARES OU DO MANGUITO ROTADOR
2. **Estrutura:** CONDICIONAL (eixo: indicação, dentro da fila Ombro / Cotovelo (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] imagem e laudo de RNM do ombro.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (10 pedidos "Sem regras").
   - A fila do SER é "Ombro / Cotovelo", mas o manual não tem nenhuma linha de cotovelo.
8. **Proposta (modelo atual):**
   - Proposta comum ao Ombro/Cotovelo (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico (com o exame físico detalhado) + Documento obrigatório "Exame de imagem do ombro/cotovelo conforme a indicação" + Pergunta lista basta-uma (Não → Ressalva, porque a tabela não tem nenhuma linha de cotovelo) "Qual indicação, com o exame exigido?": [1] Lesão ligamentar ou do manguito rotador — imagem e laudo de RNM do ombro; [2] Instabilidade do ombro — exame físico detalhado + imagem e laudo de RNM do ombro; [3] Luxação acrômio-clavicular — exame físico detalhado + imagem e laudo de RX do ombro; [4] Fratura — imagem de RX evidenciando a(s) fratura(s), com data do evento; [5] Artrose — imagem do RX do ombro. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** RNM (imagem+laudo) obrigatória SE lesão ligamentar/manguito.

### 29. 4.4.5 Ortopedia – Ombro/Cotovelo — INSTABILIDADE DO OMBRO

1. **Seção / página / recurso:** 4.4.5 Ortopedia – Ombro/Cotovelo · p.28 · INSTABILIDADE DO OMBRO
2. **Estrutura:** CONDICIONAL (eixo: indicação, dentro da fila Ombro / Cotovelo (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [conteúdo do encaminhamento] descrever de forma clara e detalhada o exame físico.
   - [documento] imagem e laudo de RNM do ombro.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (10 pedidos "Sem regras").
   - A fila do SER é "Ombro / Cotovelo", mas o manual não tem nenhuma linha de cotovelo.
8. **Proposta (modelo atual):**
   - Proposta comum ao Ombro/Cotovelo (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico (com o exame físico detalhado) + Documento obrigatório "Exame de imagem do ombro/cotovelo conforme a indicação" + Pergunta lista basta-uma (Não → Ressalva, porque a tabela não tem nenhuma linha de cotovelo) "Qual indicação, com o exame exigido?": [1] Lesão ligamentar ou do manguito rotador — imagem e laudo de RNM do ombro; [2] Instabilidade do ombro — exame físico detalhado + imagem e laudo de RNM do ombro; [3] Luxação acrômio-clavicular — exame físico detalhado + imagem e laudo de RX do ombro; [4] Fratura — imagem de RX evidenciando a(s) fratura(s), com data do evento; [5] Artrose — imagem do RX do ombro. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** RNM obrigatória SE instabilidade.

### 30. 4.4.5 Ortopedia – Ombro/Cotovelo — LUXAÇÃO ACRÔMIO-CLAVICULAR

1. **Seção / página / recurso:** 4.4.5 Ortopedia – Ombro/Cotovelo · p.28 · LUXAÇÃO ACRÔMIO-CLAVICULAR
2. **Estrutura:** CONDICIONAL (eixo: indicação, dentro da fila Ombro / Cotovelo (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [conteúdo do encaminhamento] descrever de forma clara e detalhada o exame físico.
   - [documento] imagem e laudo de RX do ombro.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (10 pedidos "Sem regras").
   - A fila do SER é "Ombro / Cotovelo", mas o manual não tem nenhuma linha de cotovelo.
8. **Proposta (modelo atual):**
   - Proposta comum ao Ombro/Cotovelo (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico (com o exame físico detalhado) + Documento obrigatório "Exame de imagem do ombro/cotovelo conforme a indicação" + Pergunta lista basta-uma (Não → Ressalva, porque a tabela não tem nenhuma linha de cotovelo) "Qual indicação, com o exame exigido?": [1] Lesão ligamentar ou do manguito rotador — imagem e laudo de RNM do ombro; [2] Instabilidade do ombro — exame físico detalhado + imagem e laudo de RNM do ombro; [3] Luxação acrômio-clavicular — exame físico detalhado + imagem e laudo de RX do ombro; [4] Fratura — imagem de RX evidenciando a(s) fratura(s), com data do evento; [5] Artrose — imagem do RX do ombro. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** RX (imagem+laudo) obrigatório SE luxação AC.

### 31. 4.4.5 Ortopedia – Ombro/Cotovelo — FRATURA

1. **Seção / página / recurso:** 4.4.5 Ortopedia – Ombro/Cotovelo · p.28 · FRATURA
2. **Estrutura:** CONDICIONAL (eixo: indicação, dentro da fila Ombro / Cotovelo (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] imagem de RX evidenciando fratura(s).
   - [documento — p.29] radiografia da fratura com a data do evento.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (10 pedidos "Sem regras").
   - A fila do SER é "Ombro / Cotovelo", mas o manual não tem nenhuma linha de cotovelo.
8. **Proposta (modelo atual):**
   - Proposta comum ao Ombro/Cotovelo (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico (com o exame físico detalhado) + Documento obrigatório "Exame de imagem do ombro/cotovelo conforme a indicação" + Pergunta lista basta-uma (Não → Ressalva, porque a tabela não tem nenhuma linha de cotovelo) "Qual indicação, com o exame exigido?": [1] Lesão ligamentar ou do manguito rotador — imagem e laudo de RNM do ombro; [2] Instabilidade do ombro — exame físico detalhado + imagem e laudo de RNM do ombro; [3] Luxação acrômio-clavicular — exame físico detalhado + imagem e laudo de RX do ombro; [4] Fratura — imagem de RX evidenciando a(s) fratura(s), com data do evento; [5] Artrose — imagem do RX do ombro. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** RX com data do evento obrigatório SE fratura.

### 32. 4.4.5 Ortopedia – Ombro/Cotovelo — ARTROSE

1. **Seção / página / recurso:** 4.4.5 Ortopedia – Ombro/Cotovelo · p.28 · ARTROSE
2. **Estrutura:** CONDICIONAL (eixo: indicação, dentro da fila Ombro / Cotovelo (Adulto))
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] imagem do RX do ombro.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Ombro / Cotovelo (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (10 pedidos "Sem regras").
   - A fila do SER é "Ombro / Cotovelo", mas o manual não tem nenhuma linha de cotovelo.
8. **Proposta (modelo atual):**
   - Proposta comum ao Ombro/Cotovelo (Adulto): Documento obrigatório (Bloqueia): encaminhamento médico (com o exame físico detalhado) + Documento obrigatório "Exame de imagem do ombro/cotovelo conforme a indicação" + Pergunta lista basta-uma (Não → Ressalva, porque a tabela não tem nenhuma linha de cotovelo) "Qual indicação, com o exame exigido?": [1] Lesão ligamentar ou do manguito rotador — imagem e laudo de RNM do ombro; [2] Instabilidade do ombro — exame físico detalhado + imagem e laudo de RNM do ombro; [3] Luxação acrômio-clavicular — exame físico detalhado + imagem e laudo de RX do ombro; [4] Fratura — imagem de RX evidenciando a(s) fratura(s), com data do evento; [5] Artrose — imagem do RX do ombro. + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** RX do ombro obrigatório SE artrose.

### 33. 4.4.6 Ortopedia – Pediatria (até 15 anos) — ACOMETIMENTO ORTOPÉDICO COM INDICAÇÃO CIRÚRGICA

1. **Seção / página / recurso:** 4.4.6 Ortopedia – Pediatria (até 15 anos) · p.28 · ACOMETIMENTO ORTOPÉDICO COM INDICAÇÃO CIRÚRGICA
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - [idade] até 15 anos (título da subseção).
   - [critério clínico] acometimento ortopédico com indicação cirúrgica.
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] imagem do RX da lesão.
4. **Recurso SER:** Ambulatório 1ª Vez em Ortopedia - Ortopedia Pediátrica (exceto coluna)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez em Ortopedia - Ortopedia Pediátrica (exceto coluna)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra (1 pedido SER "Sem regras").
   - A fila do SER é "(exceto coluna)": coluna infantil vai para Patologia Cirúrgica da Coluna (Infantil).
   - A "Ortopedia Pediátrica" do SERNIT (146 pedidos, sistema 3) é outro sistema — este manual não a cobre.
8. **Proposta (modelo atual):**
   - Dedutível idade_max 15 (Bloqueia).
   - Pergunta Sim/Não (Não bloqueia): "Há indicação cirúrgica?"
   - Documento obrigatório (Bloqueia): encaminhamento médico.
   - Documento obrigatório (Bloqueia): "Imagem do RX da lesão".
   - + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** —

### 34. 4.4.7 Outros Ambulatórios Ortopédicos — PÉ / TORNOZELO

1. **Seção / página / recurso:** 4.4.7 Outros Ambulatórios Ortopédicos · p.28 · PÉ / TORNOZELO
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] imagem do Rx da lesão.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Pé & Tornozelo (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Pé & Tornozelo (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra (2 pedidos "Sem regras").
8. **Proposta (modelo atual):**
   - Dedutível idade_min 16 (Bloqueia).
   - Documento obrigatório (Bloqueia): encaminhamento médico.
   - Documento obrigatório (Bloqueia): "Imagem do RX da lesão".
   - + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** —

### 35. 4.4.7 Outros Ambulatórios Ortopédicos — TRAUMAS DE MÉDIA COMPLEXIDADE (FRATURAS OCORRIDAS EM ATÉ 21 DIAS)

1. **Seção / página / recurso:** 4.4.7 Outros Ambulatórios Ortopédicos · p.28 · TRAUMAS DE MÉDIA COMPLEXIDADE (FRATURAS OCORRIDAS EM ATÉ 21 DIAS)
2. **Estrutura:** SIMPLES + critério temporal
3. **Requisitos (literal):**
   - [critério clínico/tempo] fratura ocorrida há até 21 dias.
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] imagem do Rx PA e Perfil.
   - [documento — p.29] radiografia da fratura com a data do evento.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Trauma Ortopédico de Média Complexidade
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Trauma Ortopédico de Média Complexidade`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra.
   - Os 21 dias não são dedutíveis: o espelho não tem a data do evento.
8. **Proposta (modelo atual):**
   - Pergunta Sim/Não (Não bloqueia): "A fratura ocorreu há no máximo 21 dias?"
   - Documento obrigatório (Bloqueia): encaminhamento médico.
   - Documento obrigatório (Bloqueia): "Imagem do RX PA e perfil da fratura, com a data do evento".
   - + regras gerais da 4.4, SEM regra de idade (o nome da fila não diz adulto/infantil).
   - **Só com regra condicional:** —

### 36. 4.4.7 Outros Ambulatórios Ortopédicos — SEQUELAS PÓS-TRAUMÁTICAS (FRATURAS CONSOLIDADAS DE FORMA VICIOSA)

1. **Seção / página / recurso:** 4.4.7 Outros Ambulatórios Ortopédicos · p.29 · SEQUELAS PÓS-TRAUMÁTICAS (FRATURAS CONSOLIDADAS DE FORMA VICIOSA)
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - [critério clínico] fratura consolidada de forma viciosa.
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] imagem do Rx PA e Perfil.
4. **Recurso SER:** Ambulatório 1ª vez em Ortopedia - Sequelas Pós Traumáticas (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Ortopedia - Sequelas Pós Traumáticas (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra.
8. **Proposta (modelo atual):**
   - Dedutível idade_min 16 (Bloqueia).
   - Documento obrigatório (Bloqueia): encaminhamento médico.
   - Documento obrigatório (Bloqueia): "Imagem do RX PA e perfil".
   - + as regras gerais da seção 4.4 (encaminhamento de ortopedista, exclusão por infecção ativa, idade da fila, CID de tumor ósseo/metástase como Ressalva).
   - **Só com regra condicional:** —

### 37. 4.4.7 Outros Ambulatórios Ortopédicos — RECONSTRUÇÃO E ALONGAMENTO ÓSSEO (FIXADOR EXTERNO)

1. **Seção / página / recurso:** 4.4.7 Outros Ambulatórios Ortopédicos · p.29 · RECONSTRUÇÃO E ALONGAMENTO ÓSSEO (FIXADOR EXTERNO)
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] clara e detalhada.
   - [documento] imagem do Rx PA e Perfil.
4. **Recurso SER:** Ambulatório 1ª Vez em Ortopedia - Reconstrução e Alongamento Ósseo (Fixador Externo)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª Vez em Ortopedia - Reconstrução e Alongamento Ósseo (Fixador Externo)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra (4 pedidos "Sem regras").
   - O formulário do SER desta fila pede ainda "Descreva o Tratamento Conservador realizado, se houver" e "Quanto tempo durou o tratamento?" — o manual não cita.
8. **Proposta (modelo atual):**
   - Documento obrigatório (Bloqueia): encaminhamento médico.
   - Documento obrigatório (Bloqueia): "Imagem do RX PA e perfil".
   - + regras gerais da 4.4, sem regra de idade (nome da fila sem faixa).
   - **Só com regra condicional:** —

### 38. 4.5 Neurocirurgia — NEUROCIRURGIA - ADULTO E INFANTIL (EXCETO COLUNA)

1. **Seção / página / recurso:** 4.5 Neurocirurgia · p.30 · NEUROCIRURGIA - ADULTO E INFANTIL (EXCETO COLUNA)
2. **Estrutura:** SIMPLES (exame: TC ou RNM, basta um)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] história clínica, uso de medicamentos regulares (quais?) e tempo da lesão.
   - [documento — basta um] exames de imagem (TC ou RNM).
4. **Recurso SER:** Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia Adulto (Exceto Coluna) ; Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia Infantil (Exceto Coluna)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia Adulto (Exceto Coluna)`; `Ambulatório 1ª vez em Neurocirurgia - Neurocirurgia Infantil (Exceto Coluna)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma das duas filas tem regra (Infantil: 2 pedidos "Sem regras").
   - A extração antiga leu a linha (SEM_PAR) e não a ligou a nenhuma fila; o mesmo texto "Inserir Exames de Imagem (TC ou RNM)" foi para o Neurovascular, mas não para cá.
8. **Proposta (modelo atual):**
   - Nas duas filas: Documento obrigatório (Bloqueia): encaminhamento médico "com história clínica, medicamentos de uso regular (quais) e tempo da lesão".
   - Documento obrigatório (Bloqueia): "Exame de imagem (TC ou RNM)".
   - Sem regra de idade (a seção 4.5 não define faixa).
   - **Só com regra condicional:** —

### 39. 4.5 Neurocirurgia — EPILEPSIA REFRATÁRIA OU FÁRMACO RESISTENTE (ADULTO E INFANTIL) — fila Adulto

1. **Seção / página / recurso:** 4.5 Neurocirurgia · p.30 · EPILEPSIA REFRATÁRIA OU FÁRMACO RESISTENTE (ADULTO E INFANTIL) — fila Adulto
2. **Estrutura:** SIMPLES (exame "se possível")
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] tipo e frequência das crises; medicações utilizadas; o que comprova a refratariedade; outros tratamentos já tentados (inclusive cirurgias ou alternativos); doença psiquiátrica associada; desenvolvimento cognitivo.
   - [documento] "Inserir EEG de rotina e RNM de encéfalo (se possível)".
4. **Recurso SER:** Ambulatório 1ª vez em Neurocirurgia - Epilepsia Refrataria ou Fármaco-Resistente (Adulto) (rótulo do SER sem acento em "Refrataria")
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Neurocirurgia - Epilepsia Refrataria ou Fármaco-Resistente (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra: o pareamento "composto" da extração antiga ligou a linha "(ADULTO E INFANTIL)" só à fila Infantil.
8. **Proposta (modelo atual):**
   - Igual à fila Infantil corrigida (próxima linha): Documento obrigatório (Bloqueia): encaminhamento médico com o conteúdo listado; Documento "EEG de rotina" (obrigatório, severidade Ressalva); Documento opcional (obrigatorio=false) "RNM de encéfalo (se possível)".
   - **Só com regra condicional:** —

### 40. 4.5 Neurocirurgia — EPILEPSIA REFRATÁRIA OU FÁRMACO RESISTENTE (ADULTO E INFANTIL) — fila Infantil

1. **Seção / página / recurso:** 4.5 Neurocirurgia · p.30 · EPILEPSIA REFRATÁRIA OU FÁRMACO RESISTENTE (ADULTO E INFANTIL) — fila Infantil
2. **Estrutura:** SIMPLES (exame "se possível")
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] tipo e frequência das crises; medicações utilizadas; o que comprova a refratariedade; outros tratamentos já tentados (inclusive cirurgias ou alternativos); doença psiquiátrica associada; desenvolvimento cognitivo.
   - [documento] "Inserir EEG de rotina e RNM de encéfalo (se possível)".
4. **Recurso SER:** Ambulatório 1ª vez em Neurocirurgia - Epilepsia Refratária ou Fármaco-Resistente (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Neurocirurgia - Epilepsia Refratária ou Fármaco-Resistente (Infantil)`
   - Regras ATIVAS (2):
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (fonte: CRECE/REUNI — requisito global)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Inserir EEG de rotina e RNM de encéfalo (se possível) (fonte: REUNI p.30)
   - Regras inativas: nenhuma.
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - O "(se possível)" virou Documento OBRIGATÓRIO que Bloqueia: todo pedido sem EEG+RNM fica "A conferir documento" (e trava o envio no assistente).
   - Ambiguidade do manual: "(se possível)" pode valer só para a RNM ou para os dois exames. A regra junta EEG e RNM numa caixa só, sem distinguir.
   - O encaminhamento é a regra global genérica; não pede o conteúdo específico (refratariedade, crises, tratamentos, psiquiatria, cognição).
8. **Proposta (modelo atual):**
   - Desativar a regra "Inserir EEG de rotina e RNM de encéfalo (se possível)" e criar duas: Documento "EEG de rotina" (obrigatório, Ressalva) e Documento "RNM de encéfalo (se possível)" (obrigatorio=false). Se a regulação ler o "se possível" como valendo para os dois, os dois ficam opcionais.
   - Trocar o rótulo do encaminhamento pelo conteúdo da linha.
   - **Só com regra condicional:** —

### 41. 4.5 Neurocirurgia — PLEXO BRAQUIAL/NERVOS PERIFÉRICOS

1. **Seção / página / recurso:** 4.5 Neurocirurgia · p.30 · PLEXO BRAQUIAL/NERVOS PERIFÉRICOS
2. **Estrutura:** CONDICIONAL (eixo: trauma) + documento "se houver"
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] solicitação médica informando tratamento realizado (clínico e cirúrgico), uso de medicações regulares (quais) e tempo de lesão.
   - [documento — condicional] exames de imagem "em caso de trauma".
   - [documento opcional] eletroneuromiografia "(se houver)".
4. **Recurso SER:** Ambulatório 1ª vez em Neurocirurgia - Plexo Braquial ; Ambulatório 1ª vez em Neurocirurgia - Nervos Periféricos
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Neurocirurgia - Plexo Braquial`; `Ambulatório 1ª vez em Neurocirurgia - Nervos Periféricos`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma das duas filas tem regra; a extração antiga deixou a linha SEM_PAR.
   - Não confundir com as filas de MICROCIRURGIA "Lesão de Plexo Braquial (Adulto/Infantil)" — especialidade diferente, sem linha neste manual.
8. **Proposta (modelo atual):**
   - Nas duas filas: Documento obrigatório (Bloqueia): encaminhamento médico com tratamento realizado (clínico e cirúrgico), medicações e tempo de lesão.
   - Pergunta Sim/Não (Não → Ressalva): "Se a lesão é traumática, há exame de imagem anexado? (responda Sim se não houve trauma)".
   - Documento opcional (obrigatorio=false): "Eletroneuromiografia (se houver)".
   - **Só com regra condicional:** Documento "Exame de imagem" obrigatório SE etiologia = trauma.

### 42. 4.5 Neurocirurgia — NEUROVASCULAR

1. **Seção / página / recurso:** 4.5 Neurocirurgia · p.30 · NEUROVASCULAR
2. **Estrutura:** SIMPLES (TC ou RNM, basta um)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] tratamento a ser realizado (clínico e cirúrgico), uso de medicações regulares (quais) e tempo de lesão.
   - [documento — basta um] exames de imagem (TC ou RNM).
4. **Recurso SER:** Ambulatório 1ª vez em Neurocirurgia - Neurovascular
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Neurocirurgia - Neurovascular`
   - Regras ATIVAS (2):
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (fonte: CRECE/REUNI — requisito global)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Inserir Exames de Imagem (TC ou RNM) (fonte: REUNI p.30)
   - Regras inativas: nenhuma.
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Só cosmético: o rótulo do encaminhamento é o global ("descrição clara e detalhada do caso") e não cita tratamento/medicações/tempo de lesão.
8. **Proposta (modelo atual):**
   - Manter. Opcional: trocar o rótulo do encaminhamento pelo conteúdo da linha.
   - **Só com regra condicional:** —

### 43. 4.5 Neurocirurgia — PARKINSON/ MOVIMENTOS INVOLUNTÁRIOS

1. **Seção / página / recurso:** 4.5 Neurocirurgia · p.30 · PARKINSON/ MOVIMENTOS INVOLUNTÁRIOS
2. **Estrutura:** SIMPLES (só encaminhamento)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] necessidade do tratamento cirúrgico a ser realizado; uso de medicações regulares que comprovem a refratariedade (quais); tempo de evolução.
   - (nenhum exame exigido)
4. **Recurso SER:** Ambulatório 1ª vez em Neurocirurgia - Parkinson / Movimentos Involuntários
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez em Neurocirurgia - Parkinson / Movimentos Involuntários`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra (nem o encaminhamento global).
8. **Proposta (modelo atual):**
   - Documento obrigatório (Bloqueia): encaminhamento médico "com a necessidade do tratamento cirúrgico, medicações de uso regular que comprovem a refratariedade (quais) e tempo de evolução".
   - Opcional: Pergunta Sim/Não (Não → Ressalva) "A refratariedade ao tratamento medicamentoso está documentada?"
   - **Só com regra condicional:** —

### 44. 4.6 Obstetrícia – Pré-natal de alto risco estratégico — PRÉ-NATAL DE ALTO RISCO ESTRATÉGICO

1. **Seção / página / recurso:** 4.6 Obstetrícia – Pré-natal de alto risco estratégico · p.31 · PRÉ-NATAL DE ALTO RISCO ESTRATÉGICO
2. **Estrutura:** INCLUSAO_EXCLUSAO (inclusão = LISTA_BASTA_UM de 18 condições; um documento CONDICIONAL à gemelaridade)
3. **Requisitos (literal):**
   - [inclusão — basta uma] Gestantes com: cardiopatias; colagenoses; doenças hematológicas; hemopatias; nefropatias; neuropatias; patologias malignas; tireoideopatias; pneumopatias; gestantes transplantadas; patologias uterinas; incompetência istmo cervical; gemelar (sem comorbidades até 28 semanas): somente mono/monoamniótico ou corionicidade desconhecida até 16 semanas; aloimunização materna sem comorbidades em qualquer IG (Coombs indireto +); peso > 140 kg (incluir peso e altura); H.P.P. de natimorto, neomorto, prematuridade, malformação fetal; H.P.P. de TVP; H.P. familiar de cromossomopatias.
   - [conteúdo do encaminhamento] história clínica, quadro atual, medicações em uso, exames ou relatórios comprovando as patologias.
   - [documento] laudo de USG e data provável de parto.
   - [documento] laudo dos exames REALIZADOS com data recente.
   - [documento — CONDICIONAL] "Para casos de gemelaridade": laudo de USG com gestação gemelar monocoriônica, mono amniótica ou di-amniótica.
   - [exclusão] gestação acima de 36 semanas; emergências obstétricas; bolsa rota.
4. **Recurso SER:** Ambulatório 1ª vez - Pré Natal de Alto Risco Estratégico
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Pré Natal de Alto Risco Estratégico`
   - Regras ATIVAS (10):
     - [ATIVA v1 Dedutível/Bloqueia, sexo F] - Gestantes que apresentarem (fonte: REUNI p.31)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (fonte: CRECE/REUNI — requisito global)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Encaminhamento médico com a história clínica, relatando o quadro atual, medicações em uso, exames ou relatórios comprovando as patologias acima (fonte: REUNI p.31)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Laudo de USG e data provável de parto (fonte: REUNI p.31)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Laudo dos exames REALIZADOS com data recente (fonte: REUNI p.31)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Para casos de gemelaridade: Laudo de USG c/ gestação gemelar monocoriônica, mono amniótica ou Di-amniótica (fonte: REUNI p.31)
     - [ATIVA v1 Pergunta/Bloqueia, bloqueia se Sim/marcou opção] Emergências Obstétricas (fonte: REUNI p.31)
     - [ATIVA v1 Pergunta/Bloqueia, bloqueia se Sim/marcou opção] Gestação acima de 36 semanas (fonte: REUNI p.31)
     - [ATIVA v1 Pergunta/Bloqueia, lista basta-uma, 18 opções: Cardiopatias | Colagenoses | Doenças Hematológicas | Hemopatias | Nefropatias | Neuropatias | Patologias malignas | Tireoideopatias | Pneumopatias | Gestantes Transplantadas | Patologias uterinas | Incompetência Istmo cervical | Gemelar (sem comorbidades até 28 semanas): somente gestação gemelar mono/monoamniótico ou com corionicidade desconhecida até 16 semanas | Alo imunização materna, sem comorbidades em qualquer idade gestacional (resultado de COOMBS indireto +) | Peso maior que 140Kg (incluir peso e altura na solicitação) | H.P.P. de natimorto, neomorto, prematuridade, malformação fetal | H.P.P. de trombose venosa profunda | H. P. Familiar de cromossomopatias; bloqueia se Não/nenhuma opção] Gestantes que apresentarem (basta uma condição) — "O paciente tem ao menos uma das condições abaixo?" (fonte: REUNI p.31)
     - [ATIVA v2 Pergunta/Bloqueia, bloqueia se Sim/marcou opção] Bolsa rota (fonte: REUNI p.31)
   - Regras inativas (31 distintas):
     - [inativa v1 Dedutível/Bloqueia, sexo F] - Gestantes que apresentarem aos exames de USG as seguintes patologias (fonte: REUNI p.31)
     - [inativa v1 Dedutível/Bloqueia, sexo F] Gestantes Transplantadas (fonte: REUNI p.31)
     - [inativa v1 Documento/Bloqueia, obrigatório] H. P. Familiar de cromossomopatias Inserir no SER de forma clara e detalhada (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Alo imunização materna, sem comorbidades em qualquer idade gestacional (resultado de COOMBS indireto +) (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Cardiopatias (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Colagenoses (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Dilatação pielocalicial (> 6,5mm) (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Doenças Hematológicas (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Encefalocele (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Fetos com múltiplas malformações (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Gastrosquise (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Gemelar (sem comorbidades até 28 semanas): somente gestação gemelar mono/monoamniótico ou com corionicidade desconhecida até 16 semanas (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] H.P.P. de natimorto, neomorto, prematuridade, malformação fetal (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] H.P.P. de trombose venosa profunda (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Hemopatias (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Hidrocefalia (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Hérnia diafragmática (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Incompetência Istmo cervical (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Mielomeningocele (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Nefropatias (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Neuropatias (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Onfalocele (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Patologias malignas (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Patologias uterinas (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Peso maior que 140Kg (incluir peso e altura na solicitação) (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Pneumopatias (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Rim policístico bilateral (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Tireoideopatias (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Sim/marcou opção] Bolsa rota ACONSELHAMENTO EM MALFORMAÇÃO FETAL (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Sim/marcou opção] Gestação acima de 31 semanas e 6 dias (fonte: REUNI p.31)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Sim/marcou opção] Malformações cardíacas (fonte: REUNI p.31)
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - A regra "Para casos de gemelaridade: Laudo de USG…" é Documento OBRIGATÓRIO que Bloqueia para TODA gestante — o manual só exige em gemelar. Toda gestação única fica "A conferir documento" e o assistente pede um laudo que não existe.
   - A Dedutível sexo F tem a descrição "- Gestantes que apresentarem" (fragmento da extração). O efeito (sexo F) está certo e não conflita: o SER tem fila própria "Pré Natal - Gestação de Homen Trans".
   - Encaminhamento em dobro: a regra global e a específica da p.31 estão as duas ativas.
   - O resto confere com o texto: lista de 18 opções (basta uma, Não bloqueia) e as 3 exclusões (Sim bloqueia).
   - 13 regras INATIVAS deste procedimento são da p.32 (Aconselhamento em malformação fetal) com fonte "REUNI p.31" — procedimento e página errados (ver seção "Regras fora do lugar").
8. **Proposta (modelo atual):**
   - Na regra de gemelaridade, mudar para obrigatorio=false (ou Ressalva) e começar o rótulo com "Se gemelar:".
   - Renomear a Dedutível sexo F para "Gestante (sexo feminino)".
   - Desativar um dos dois encaminhamentos, de preferência o global.
   - **Só com regra condicional:** Documento "Laudo de USG gemelar (mono/di-amniótica)" obrigatório SE a opção "Gemelar…" foi marcada na lista de inclusão.

### 45. 4.6 Obstetrícia – Pré-natal de alto risco estratégico — ACONSELHAMENTO EM MALFORMAÇÃO FETAL

1. **Seção / página / recurso:** 4.6 Obstetrícia – Pré-natal de alto risco estratégico · p.32 · ACONSELHAMENTO EM MALFORMAÇÃO FETAL
2. **Estrutura:** INCLUSAO_EXCLUSAO (inclusão = LISTA_BASTA_UM de 9 achados de USG)
3. **Requisitos (literal):**
   - [inclusão — basta uma, "aos exames de USG"] mielomeningocele; onfalocele; encefalocele; gastrosquise; rim policístico bilateral; dilatação pielocalicial (> 6,5 mm); hérnia diafragmática; hidrocefalia; fetos com múltiplas malformações.
   - [conteúdo do encaminhamento] descrevendo de forma clara e detalhada o caso.
   - [exclusão] gestação acima de 31 semanas e 6 dias; malformações cardíacas.
4. **Recurso SER:** Ambulatório 1ª vez - Aconselhamento em malformação fetal
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Aconselhamento em malformação fetal`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra ativa nem inativa no procedimento certo (2 pedidos "Sem regras").
   - O conteúdo FOI extraído, mas caiu no Pré-Natal de Alto Risco com fonte "REUNI p.31" (é da p.32) e depois foi desativado lá — nunca foi recriado aqui.
   - O SER tem ainda a fila "Avaliação de mielomeningocele intraútero", fora deste trecho do manual.
8. **Proposta (modelo atual):**
   - Dedutível sexo F (Bloqueia), descrição "Gestante (sexo feminino)".
   - Pergunta lista basta-uma (Não bloqueia) "A USG mostra ao menos um destes achados?" com as 9 opções.
   - Pergunta Sim/Não (Sim bloqueia): "Gestação acima de 31 semanas e 6 dias?"
   - Pergunta Sim/Não (Sim bloqueia): "Malformação cardíaca?"
   - Documento obrigatório (Bloqueia): encaminhamento médico.
   - Documento opcional (obrigatorio=false): "Laudo de USG com o achado" — inferido de "aos exames de USG"; o manual não o lista como anexo.
   - **Só com regra condicional:** —

### 46. 4.7 Microcirurgias — MICROCIRURGIA RECONSTRUTORA (INFANTIL)

1. **Seção / página / recurso:** 4.7 Microcirurgias · p.32 · MICROCIRURGIA RECONSTRUTORA (INFANTIL)
2. **Estrutura:** LISTA_BASTA_UM (critérios de inclusão)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] descrevendo de forma clara e detalhada o caso.
   - [documento] exames realizados pertinentes ao diagnóstico.
   - [inclusão — basta um] lesão de plexo braquial; reconstrução com retalhos musculares ou microcirúrgicos.
4. **Recurso SER:** Ambulatório 1ª vez - Microcirurgia Reconstrutora (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Microcirurgia Reconstrutora (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra.
   - O SER tem filas próprias "Microcirurgia - Lesão de Plexo Braquial (Adulto/Infantil)", sem linha no manual; o critério "lesão de plexo braquial" desta linha pode estar desatualizado (o manual é de 2022).
   - Existe uma origem INATIVA errada: o recurso "Ambulatório 1ª vez em Cardiologia - Doenças Neuromusculares" aparece ligado a este procedimento (origem_ativa=false). Sem efeito na análise (ela só usa origens ativas), mas o resumo do catálogo mostra o par.
8. **Proposta (modelo atual):**
   - Documento obrigatório (Bloqueia): encaminhamento médico.
   - Documento obrigatório (severidade Ressalva — o texto é vago): "Exames realizados pertinentes ao diagnóstico".
   - Pergunta lista basta-uma (Não bloqueia): [1] Lesão de plexo braquial; [2] Reconstrução com retalhos musculares ou microcirúrgicos. Confirmar com a regulação se a opção [1] continua valendo, dado que existe fila própria de plexo.
   - **Só com regra condicional:** —

### 47. 4.7 Microcirurgias — MICROCIRURGIA RECONSTRUTORA (ADULTO)

1. **Seção / página / recurso:** 4.7 Microcirurgias · p.32 · MICROCIRURGIA RECONSTRUTORA (ADULTO)
2. **Estrutura:** LISTA_BASTA_UM (critérios de inclusão)
3. **Requisitos (literal):**
   - [conteúdo do encaminhamento] descrevendo de forma clara e detalhada o caso.
   - [documento] exames realizados pertinentes ao diagnóstico.
   - [inclusão — basta um] lesão de plexo braquial; reconstrução com retalhos musculares ou microcirúrgicos.
4. **Recurso SER:** Ambulatório 1ª vez - Microcirurgia Reconstrutora (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Microcirurgia Reconstrutora (Adulto)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Sem regra.
   - O SER tem filas próprias "Microcirurgia - Lesão de Plexo Braquial (Adulto/Infantil)", sem linha no manual; o critério "lesão de plexo braquial" desta linha pode estar desatualizado (o manual é de 2022).
   - O formulário do SER desta fila pede "Descreva o Tratamento Conservador realizado, se houver" e "Quanto tempo durou o tratamento?".
8. **Proposta (modelo atual):**
   - Documento obrigatório (Bloqueia): encaminhamento médico.
   - Documento obrigatório (severidade Ressalva — o texto é vago): "Exames realizados pertinentes ao diagnóstico".
   - Pergunta lista basta-uma (Não bloqueia): [1] Lesão de plexo braquial; [2] Reconstrução com retalhos musculares ou microcirúrgicos. Confirmar com a regulação se a opção [1] continua valendo, dado que existe fila própria de plexo.
   - **Só com regra condicional:** —

### 48. 4.8.1 Hematologia Adulto (seção) — Hematologia Adulto — Critérios de exclusão + "Necessita informar"

1. **Seção / página / recurso:** 4.8.1 Hematologia Adulto (seção) · p.33 · Hematologia Adulto — Critérios de exclusão + "Necessita informar"
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - [exclusão] pacientes em tratamento em outros serviços de Hematologia.
   - [exclusão] anemia hipocrômica SEM investigação clínica de perda de sangue (qualquer sítio), ingesta inadequada e SEM dosagem de ferro sérico, transferrina e ferritina sérica.
   - [exclusão] eritrocitose secundária a DPOC.
   - [exclusão] trombofilia.
   - [conteúdo do encaminhamento — "Necessita informar"] relato sucinto do quadro clínico, tempo de evolução e tratamentos realizados; medicamentos de uso regular (especificar); investigação completa (história familiar de hematopatia; se realizou tratamento); descartar viroses e intoxicações medicamentosas; descartar uso de medicamentos que interfiram na coagulação.
4. **Recurso SER:** Ambulatório 1ª vez - Hematologia (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Hematologia (Adulto)`
   - Regras ATIVAS (10):
     - [ATIVA v1 Documento/Bloqueia, obrigatório] 02 hemogramas (intervalo > ou = 15 dias) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] 02 hemogramas recentes (intervalo mínimo de 1 mês) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Coagulograma e Plaquetas (2 exames diferentes) (fonte: REUNI p.35)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Eletroforese de Hb (em suspeita de hemoglobinopatias) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (fonte: CRECE/REUNI — requisito global)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Provas de função hepática e Renal/ FAN (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Sorologias para hepatite viral (HBV/ HCV), HIV (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD (fonte: REUNI p.33)
     - [ATIVA v1 Pergunta/Bloqueia, lista basta-uma, 9 opções: Alterações no coagulograma em exames subsequentes | Anemia e icterícia com aumento de bilirrubina indireta | Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como Insuficiência Renal, diabetes mellitus, colagenoses e hepatopatias | Atipias linfocitárias na ausência de quadro viral agudo | Esplenomegalia (no adulto, depois de afastada doença hepática de qualquer etiologia) | Hemoglobina sérica < 9 g/dl, de padrão macrocítico | Leucometria global < ou = 3000/mm³ | Neutrófilos < ou = 1200/mm³ | Síndromes hemorrágicas: hematomas, equimoses, epistaxes, metrorragia (quando isolada deve ser avaliada ginecológica) com plaquetas < 75.000 e/ou TAP ou PTT alterados; bloqueia se Não/nenhuma opção] Critérios de inclusão (basta um) — "O paciente se enquadra em ao menos um dos critérios abaixo?" (fonte: REUNI p.33 · REUNI p.35)
   - Regras inativas (21 distintas):
     - [inativa v1 Documento/Bloqueia, obrigatório] 02 hemogramas (intervalo > ou = 15 dias) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] 02 hemogramas recentes (intervalo mínimo de 1 mês) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Eletroforese de Hb (em suspeita de hemoglobinopatias) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitaminaB12, ácido fólico (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Encaminhamento médico descrevendo de forma clara e detalhada o caso (fonte: REUNI p.33) ×4
     - [inativa v1 Documento/Bloqueia, obrigatório] Encaminhamento médico descrevendo de forma clara e detalhada o caso (fonte: REUNI p.35)
     - [inativa v1 Documento/Bloqueia, obrigatório] Esplenomegalia no adulto, DEPOIS DE AFASTADA doença hepática de qualquer etiologia. Inserir no SER (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Inserir no SER (fonte: REUNI p.33) ×3
     - [inativa v1 Documento/Bloqueia, obrigatório] Inserir no SER (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] / Agendamento (fonte: REUNI p.33) ×4
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] / Agendamento (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Alterações no coagulograma em exames subsequentes (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia e icterícia com aumento de bilirrubina indireta (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como Insuficiência Renal, diabetes mellitus, colagenoses e hepatopatias (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como InsuficiênciaRenal, diabetes mellitus, colagenoses e hepatopatias (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Atipias linfocitárias na ausência de quadro viral agudo (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Esplenomegalia (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Hemoglobina sérica < 9 g/dl, de padrão macrocítico (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Leucometria global < ou = 3000/mm³ (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Neutrófilos < ou = 1200/mm³ (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Síndromes hemorrágicas: hematomas, equimoses, epistaxes, metrorragia (quando isolada deve ser avaliada ginecológica) com plaquetas < 75.000 e/ou TAP ou PTT alterados (fonte: REUNI p.35)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma das 4 exclusões está cadastrada (nem ativa nem inativa).
   - O "Necessita informar" não aparece; o encaminhamento é o rótulo global.
   - Existe uma origem INATIVA errada: "Ambulatório de 1ª vez em Cardiologia - Amiloidose Cardíaca" ligada a Hematologia (Adulto). A análise ignora origem inativa, mas o resumo do catálogo mostra "regras ativas: 10" para a Amiloidose.
8. **Proposta (modelo atual):**
   - Pergunta lista (Sim bloqueia = "marcou uma das opções", como o motor já faz) "Algum destes critérios de exclusão se aplica?": em tratamento em outro serviço de Hematologia | anemia hipocrômica sem investigação de perda de sangue/ingesta e sem ferro sérico, transferrina e ferritina | eritrocitose secundária a DPOC | trombofilia.
   - Opcional (sugestão nossa, Ressalva): Dedutível CID excluídos [D68.5, D68.6] (trombofilias).
   - Trocar o rótulo do encaminhamento pelo texto do "Necessita informar".
   - **Só com regra condicional:** —

### 49. 4.8.1 Hematologia Adulto — ANEMIA

1. **Seção / página / recurso:** 4.8.1 Hematologia Adulto · p.33 · ANEMIA
2. **Estrutura:** CONDICIONAL (eixo: tipo de alteração hematológica — anemia / leucopenia / hemorrágico, na mesma fila)
3. **Requisitos (literal):**
   - [documento] encaminhamento médico descrevendo de forma clara e detalhada o caso.
   - [documento] 02 hemogramas (intervalo ≥ 15 dias).
   - [documento] eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico.
   - [documento] sorologias para hepatite viral (HBV/HCV), HIV.
   - [documento] provas de função hepática e renal/FAN.
   - [documento — condicional] eletroforese de Hb (em suspeita de hemoglobinopatias).
   - [inclusão — basta um] Hb sérica < 9 g/dl, padrão macrocítico; anemia e icterícia com aumento de bilirrubina indireta; anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas (insuficiência renal, diabetes mellitus, colagenoses, hepatopatias).
4. **Recurso SER:** Ambulatório 1ª vez - Hematologia (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Hematologia (Adulto)`
   - Regras ATIVAS (10):
     - [ATIVA v1 Documento/Bloqueia, obrigatório] 02 hemogramas (intervalo > ou = 15 dias) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] 02 hemogramas recentes (intervalo mínimo de 1 mês) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Coagulograma e Plaquetas (2 exames diferentes) (fonte: REUNI p.35)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Eletroforese de Hb (em suspeita de hemoglobinopatias) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (fonte: CRECE/REUNI — requisito global)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Provas de função hepática e Renal/ FAN (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Sorologias para hepatite viral (HBV/ HCV), HIV (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD (fonte: REUNI p.33)
     - [ATIVA v1 Pergunta/Bloqueia, lista basta-uma, 9 opções: Alterações no coagulograma em exames subsequentes | Anemia e icterícia com aumento de bilirrubina indireta | Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como Insuficiência Renal, diabetes mellitus, colagenoses e hepatopatias | Atipias linfocitárias na ausência de quadro viral agudo | Esplenomegalia (no adulto, depois de afastada doença hepática de qualquer etiologia) | Hemoglobina sérica < 9 g/dl, de padrão macrocítico | Leucometria global < ou = 3000/mm³ | Neutrófilos < ou = 1200/mm³ | Síndromes hemorrágicas: hematomas, equimoses, epistaxes, metrorragia (quando isolada deve ser avaliada ginecológica) com plaquetas < 75.000 e/ou TAP ou PTT alterados; bloqueia se Não/nenhuma opção] Critérios de inclusão (basta um) — "O paciente se enquadra em ao menos um dos critérios abaixo?" (fonte: REUNI p.33 · REUNI p.35)
   - Regras inativas (21 distintas):
     - [inativa v1 Documento/Bloqueia, obrigatório] 02 hemogramas (intervalo > ou = 15 dias) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] 02 hemogramas recentes (intervalo mínimo de 1 mês) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Eletroforese de Hb (em suspeita de hemoglobinopatias) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitaminaB12, ácido fólico (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Encaminhamento médico descrevendo de forma clara e detalhada o caso (fonte: REUNI p.33) ×4
     - [inativa v1 Documento/Bloqueia, obrigatório] Encaminhamento médico descrevendo de forma clara e detalhada o caso (fonte: REUNI p.35)
     - [inativa v1 Documento/Bloqueia, obrigatório] Esplenomegalia no adulto, DEPOIS DE AFASTADA doença hepática de qualquer etiologia. Inserir no SER (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Inserir no SER (fonte: REUNI p.33) ×3
     - [inativa v1 Documento/Bloqueia, obrigatório] Inserir no SER (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] / Agendamento (fonte: REUNI p.33) ×4
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] / Agendamento (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Alterações no coagulograma em exames subsequentes (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia e icterícia com aumento de bilirrubina indireta (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como Insuficiência Renal, diabetes mellitus, colagenoses e hepatopatias (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como InsuficiênciaRenal, diabetes mellitus, colagenoses e hepatopatias (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Atipias linfocitárias na ausência de quadro viral agudo (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Esplenomegalia (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Hemoglobina sérica < 9 g/dl, de padrão macrocítico (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Leucometria global < ou = 3000/mm³ (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Neutrófilos < ou = 1200/mm³ (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Síndromes hemorrágicas: hematomas, equimoses, epistaxes, metrorragia (quando isolada deve ser avaliada ginecológica) com plaquetas < 75.000 e/ou TAP ou PTT alterados (fonte: REUNI p.35)
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - As 10 regras ativas somam com E os exames dos TRÊS ramos e mais um do pediátrico, todos como Documento obrigatório que Bloqueia.
   - ATIVA e de outro procedimento: "Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD" (fonte "REUNI p.33") — é da tabela de Hematologia PEDIÁTRICA (p.35). Cobra triagem neonatal de adulto.
   - Os exames da anemia estão todos lá, mas o caso de anemia também é cobrado por "02 hemogramas recentes (intervalo mínimo de 1 mês)" (leucopenia), "Coagulograma e Plaquetas" (hemorrágico) e "Teste do Pezinho" (pediátrico).
   - "Eletroforese de Hb (em suspeita de hemoglobinopatias)" — condicional no próprio texto — virou obrigatória para todos.
8. **Proposta (modelo atual):**
   - Correção hoje (Hematologia Adulto): (a) DESATIVAR "Teste do Pezinho com alterações…" (é da Hematologia Pediátrica, p.35); (b) nas demais caixas de exame, mudar para obrigatorio=false e prefixar o ramo no rótulo — "[Anemia] 02 hemogramas (intervalo ≥ 15 dias)", "[Anemia] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico", "[Anemia] Sorologias HBV/HCV/HIV", "[Anemia] Provas de função hepática e renal/FAN", "[Anemia, se suspeita de hemoglobinopatia] Eletroforese de Hb", "[Leucopenia] 02 hemogramas recentes (intervalo mínimo de 1 mês)", "[Distúrbio hemorrágico] Coagulograma e plaquetas (2 exames diferentes)"; (c) manter a lista de inclusão de 9 opções (basta uma) — ela já é a união fiel dos três ramos; (d) manter um Documento obrigatório genérico "Exames laboratoriais exigidos para a alteração marcada" para não perder a cobrança; (e) criar a Pergunta de exclusão e trocar o rótulo do encaminhamento pelo "Necessita informar" (ver linha da seção).
   - **Só com regra condicional:** Cada caixa de exame obrigatória SE a opção marcada na lista de inclusão pertence ao ramo dela (anemia / leucopenia / hemorrágico); eletroforese de Hb SE suspeita de hemoglobinopatia.

### 50. 4.8.1 Hematologia Adulto — DISTÚRBIOS DOS LEUCÓCITOS (LEUCOPENIA)

1. **Seção / página / recurso:** 4.8.1 Hematologia Adulto · p.33 · DISTÚRBIOS DOS LEUCÓCITOS (LEUCOPENIA)
2. **Estrutura:** CONDICIONAL (eixo: tipo de alteração hematológica — anemia / leucopenia / hemorrágico, na mesma fila)
3. **Requisitos (literal):**
   - [documento] encaminhamento médico descrevendo de forma clara e detalhada o caso.
   - [documento] 02 hemogramas recentes (intervalo mínimo de 1 mês).
   - [inclusão — basta um] leucometria global ≤ 3000/mm³; neutrófilos ≤ 1200/mm³; atipias linfocitárias na ausência de quadro viral agudo; esplenomegalia no adulto, DEPOIS DE AFASTADA doença hepática de qualquer etiologia.
4. **Recurso SER:** Ambulatório 1ª vez - Hematologia (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Hematologia (Adulto)`
   - Regras ATIVAS (10):
     - [ATIVA v1 Documento/Bloqueia, obrigatório] 02 hemogramas (intervalo > ou = 15 dias) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] 02 hemogramas recentes (intervalo mínimo de 1 mês) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Coagulograma e Plaquetas (2 exames diferentes) (fonte: REUNI p.35)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Eletroforese de Hb (em suspeita de hemoglobinopatias) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (fonte: CRECE/REUNI — requisito global)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Provas de função hepática e Renal/ FAN (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Sorologias para hepatite viral (HBV/ HCV), HIV (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD (fonte: REUNI p.33)
     - [ATIVA v1 Pergunta/Bloqueia, lista basta-uma, 9 opções: Alterações no coagulograma em exames subsequentes | Anemia e icterícia com aumento de bilirrubina indireta | Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como Insuficiência Renal, diabetes mellitus, colagenoses e hepatopatias | Atipias linfocitárias na ausência de quadro viral agudo | Esplenomegalia (no adulto, depois de afastada doença hepática de qualquer etiologia) | Hemoglobina sérica < 9 g/dl, de padrão macrocítico | Leucometria global < ou = 3000/mm³ | Neutrófilos < ou = 1200/mm³ | Síndromes hemorrágicas: hematomas, equimoses, epistaxes, metrorragia (quando isolada deve ser avaliada ginecológica) com plaquetas < 75.000 e/ou TAP ou PTT alterados; bloqueia se Não/nenhuma opção] Critérios de inclusão (basta um) — "O paciente se enquadra em ao menos um dos critérios abaixo?" (fonte: REUNI p.33 · REUNI p.35)
   - Regras inativas (21 distintas):
     - [inativa v1 Documento/Bloqueia, obrigatório] 02 hemogramas (intervalo > ou = 15 dias) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] 02 hemogramas recentes (intervalo mínimo de 1 mês) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Eletroforese de Hb (em suspeita de hemoglobinopatias) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitaminaB12, ácido fólico (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Encaminhamento médico descrevendo de forma clara e detalhada o caso (fonte: REUNI p.33) ×4
     - [inativa v1 Documento/Bloqueia, obrigatório] Encaminhamento médico descrevendo de forma clara e detalhada o caso (fonte: REUNI p.35)
     - [inativa v1 Documento/Bloqueia, obrigatório] Esplenomegalia no adulto, DEPOIS DE AFASTADA doença hepática de qualquer etiologia. Inserir no SER (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Inserir no SER (fonte: REUNI p.33) ×3
     - [inativa v1 Documento/Bloqueia, obrigatório] Inserir no SER (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] / Agendamento (fonte: REUNI p.33) ×4
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] / Agendamento (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Alterações no coagulograma em exames subsequentes (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia e icterícia com aumento de bilirrubina indireta (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como Insuficiência Renal, diabetes mellitus, colagenoses e hepatopatias (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como InsuficiênciaRenal, diabetes mellitus, colagenoses e hepatopatias (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Atipias linfocitárias na ausência de quadro viral agudo (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Esplenomegalia (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Hemoglobina sérica < 9 g/dl, de padrão macrocítico (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Leucometria global < ou = 3000/mm³ (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Neutrófilos < ou = 1200/mm³ (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Síndromes hemorrágicas: hematomas, equimoses, epistaxes, metrorragia (quando isolada deve ser avaliada ginecológica) com plaquetas < 75.000 e/ou TAP ou PTT alterados (fonte: REUNI p.35)
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - As 10 regras ativas somam com E os exames dos TRÊS ramos e mais um do pediátrico, todos como Documento obrigatório que Bloqueia.
   - ATIVA e de outro procedimento: "Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD" (fonte "REUNI p.33") — é da tabela de Hematologia PEDIÁTRICA (p.35). Cobra triagem neonatal de adulto.
   - A leucopenia exige 2 caixas no manual (encaminhamento + 2 hemogramas ≥ 1 mês). Em produção ela recebe mais 7 caixas obrigatórias: sorologias, FAN/função hepática e renal, eletroforese de proteínas/ferro/B12, eletroforese de Hb, 2 hemogramas ≥ 15 dias, coagulograma e Teste do Pezinho.
8. **Proposta (modelo atual):**
   - Correção hoje (Hematologia Adulto): (a) DESATIVAR "Teste do Pezinho com alterações…" (é da Hematologia Pediátrica, p.35); (b) nas demais caixas de exame, mudar para obrigatorio=false e prefixar o ramo no rótulo — "[Anemia] 02 hemogramas (intervalo ≥ 15 dias)", "[Anemia] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico", "[Anemia] Sorologias HBV/HCV/HIV", "[Anemia] Provas de função hepática e renal/FAN", "[Anemia, se suspeita de hemoglobinopatia] Eletroforese de Hb", "[Leucopenia] 02 hemogramas recentes (intervalo mínimo de 1 mês)", "[Distúrbio hemorrágico] Coagulograma e plaquetas (2 exames diferentes)"; (c) manter a lista de inclusão de 9 opções (basta uma) — ela já é a união fiel dos três ramos; (d) manter um Documento obrigatório genérico "Exames laboratoriais exigidos para a alteração marcada" para não perder a cobrança; (e) criar a Pergunta de exclusão e trocar o rótulo do encaminhamento pelo "Necessita informar" (ver linha da seção).
   - **Só com regra condicional:** Cada caixa de exame obrigatória SE a opção marcada na lista de inclusão pertence ao ramo dela (anemia / leucopenia / hemorrágico); eletroforese de Hb SE suspeita de hemoglobinopatia.

### 51. 4.8.1 Hematologia Adulto — DISTÚRBIOS HEMORRÁGICOS

1. **Seção / página / recurso:** 4.8.1 Hematologia Adulto · p.34 · DISTÚRBIOS HEMORRÁGICOS
2. **Estrutura:** CONDICIONAL (eixo: tipo de alteração hematológica — anemia / leucopenia / hemorrágico, na mesma fila)
3. **Requisitos (literal):**
   - [documento] encaminhamento médico descrevendo de forma clara e detalhada o caso.
   - [documento] coagulograma e plaquetas (2 exames diferentes).
   - [inclusão — basta um] síndromes hemorrágicas (hematomas, equimoses, epistaxes, metrorragia — isolada deve ser avaliada pela ginecologia) com plaquetas < 75.000 e/ou TAP ou PTT alterados; alterações no coagulograma em exames subsequentes.
4. **Recurso SER:** Ambulatório 1ª vez - Hematologia (Adulto)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Hematologia (Adulto)`
   - Regras ATIVAS (10):
     - [ATIVA v1 Documento/Bloqueia, obrigatório] 02 hemogramas (intervalo > ou = 15 dias) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] 02 hemogramas recentes (intervalo mínimo de 1 mês) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Coagulograma e Plaquetas (2 exames diferentes) (fonte: REUNI p.35)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Eletroforese de Hb (em suspeita de hemoglobinopatias) (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER. (fonte: CRECE/REUNI — requisito global)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Provas de função hepática e Renal/ FAN (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Sorologias para hepatite viral (HBV/ HCV), HIV (fonte: REUNI p.33)
     - [ATIVA v1 Documento/Bloqueia, obrigatório] Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD (fonte: REUNI p.33)
     - [ATIVA v1 Pergunta/Bloqueia, lista basta-uma, 9 opções: Alterações no coagulograma em exames subsequentes | Anemia e icterícia com aumento de bilirrubina indireta | Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como Insuficiência Renal, diabetes mellitus, colagenoses e hepatopatias | Atipias linfocitárias na ausência de quadro viral agudo | Esplenomegalia (no adulto, depois de afastada doença hepática de qualquer etiologia) | Hemoglobina sérica < 9 g/dl, de padrão macrocítico | Leucometria global < ou = 3000/mm³ | Neutrófilos < ou = 1200/mm³ | Síndromes hemorrágicas: hematomas, equimoses, epistaxes, metrorragia (quando isolada deve ser avaliada ginecológica) com plaquetas < 75.000 e/ou TAP ou PTT alterados; bloqueia se Não/nenhuma opção] Critérios de inclusão (basta um) — "O paciente se enquadra em ao menos um dos critérios abaixo?" (fonte: REUNI p.33 · REUNI p.35)
   - Regras inativas (21 distintas):
     - [inativa v1 Documento/Bloqueia, obrigatório] 02 hemogramas (intervalo > ou = 15 dias) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] 02 hemogramas recentes (intervalo mínimo de 1 mês) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Eletroforese de Hb (em suspeita de hemoglobinopatias) (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitaminaB12, ácido fólico (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Encaminhamento médico descrevendo de forma clara e detalhada o caso (fonte: REUNI p.33) ×4
     - [inativa v1 Documento/Bloqueia, obrigatório] Encaminhamento médico descrevendo de forma clara e detalhada o caso (fonte: REUNI p.35)
     - [inativa v1 Documento/Bloqueia, obrigatório] Esplenomegalia no adulto, DEPOIS DE AFASTADA doença hepática de qualquer etiologia. Inserir no SER (fonte: REUNI p.33)
     - [inativa v1 Documento/Bloqueia, obrigatório] Inserir no SER (fonte: REUNI p.33) ×3
     - [inativa v1 Documento/Bloqueia, obrigatório] Inserir no SER (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] / Agendamento (fonte: REUNI p.33) ×4
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] / Agendamento (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Alterações no coagulograma em exames subsequentes (fonte: REUNI p.35)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia e icterícia com aumento de bilirrubina indireta (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como Insuficiência Renal, diabetes mellitus, colagenoses e hepatopatias (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como InsuficiênciaRenal, diabetes mellitus, colagenoses e hepatopatias (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Atipias linfocitárias na ausência de quadro viral agudo (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Esplenomegalia (fonte: REUNI p.33)
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Hemoglobina sérica < 9 g/dl, de padrão macrocítico (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Leucometria global < ou = 3000/mm³ (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Neutrófilos < ou = 1200/mm³ (fonte: REUNI p.33) ×2
     - [inativa v1 Pergunta/Bloqueia, bloqueia se Não/nenhuma opção] Síndromes hemorrágicas: hematomas, equimoses, epistaxes, metrorragia (quando isolada deve ser avaliada ginecológica) com plaquetas < 75.000 e/ou TAP ou PTT alterados (fonte: REUNI p.35)
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - As 10 regras ativas somam com E os exames dos TRÊS ramos e mais um do pediátrico, todos como Documento obrigatório que Bloqueia.
   - ATIVA e de outro procedimento: "Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD" (fonte "REUNI p.33") — é da tabela de Hematologia PEDIÁTRICA (p.35). Cobra triagem neonatal de adulto.
   - O distúrbio hemorrágico recebe 7 caixas obrigatórias que não são dele (anemia, leucopenia e Teste do Pezinho).
   - A caixa "Coagulograma e Plaquetas" e a opção de síndromes hemorrágicas citam "REUNI p.35" (tabela PEDIÁTRICA). O texto é idêntico ao da p.34 (adulto), então o conteúdo está certo, mas a fonte está errada — a tabela adulta da p.34 nem aparece na extração antiga.
8. **Proposta (modelo atual):**
   - Correção hoje (Hematologia Adulto): (a) DESATIVAR "Teste do Pezinho com alterações…" (é da Hematologia Pediátrica, p.35); (b) nas demais caixas de exame, mudar para obrigatorio=false e prefixar o ramo no rótulo — "[Anemia] 02 hemogramas (intervalo ≥ 15 dias)", "[Anemia] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico", "[Anemia] Sorologias HBV/HCV/HIV", "[Anemia] Provas de função hepática e renal/FAN", "[Anemia, se suspeita de hemoglobinopatia] Eletroforese de Hb", "[Leucopenia] 02 hemogramas recentes (intervalo mínimo de 1 mês)", "[Distúrbio hemorrágico] Coagulograma e plaquetas (2 exames diferentes)"; (c) manter a lista de inclusão de 9 opções (basta uma) — ela já é a união fiel dos três ramos; (d) manter um Documento obrigatório genérico "Exames laboratoriais exigidos para a alteração marcada" para não perder a cobrança; (e) criar a Pergunta de exclusão e trocar o rótulo do encaminhamento pelo "Necessita informar" (ver linha da seção).
   - **Só com regra condicional:** Cada caixa de exame obrigatória SE a opção marcada na lista de inclusão pertence ao ramo dela (anemia / leucopenia / hemorrágico); eletroforese de Hb SE suspeita de hemoglobinopatia.

### 52. 4.8.2 Hematologia Pediátrica (seção) — Hematologia Pediátrica — Critérios de exclusão + "Necessita informar"

1. **Seção / página / recurso:** 4.8.2 Hematologia Pediátrica (seção) · p.34 · Hematologia Pediátrica — Critérios de exclusão + "Necessita informar"
2. **Estrutura:** INCLUSAO_EXCLUSAO com exclusão CONDICIONAL (eixo: idade < 1 ano × suspeita)
3. **Requisitos (literal):**
   - [exclusão — idade com exceção] menores de 1 ano de idade, SALVO com suspeita de doenças hemorrágicas hereditárias ou hemoglobinopatias.
   - [exclusão] anemia hipocrômica SEM investigação clínica de perda de sangue (qualquer sítio), ingesta inadequada e SEM dosagem de ferro sérico, transferrina e ferritina sérica.
   - [conteúdo do encaminhamento — "Necessita informar"] mesmos 5 itens do adulto.
4. **Recurso SER:** Ambulatório 1ª vez - Hematologia (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Hematologia (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Procedimento sem nenhuma regra (2 pedidos "Sem regras").
   - O conteúdo pediátrico foi extraído (p.35), mas TUDO foi parear com Hematologia (Adulto): a extração antiga juntou as páginas 33 e 35 (rotulou as linhas pediátricas de anemia/leucopenia como "REUNI p.33").
   - O "menor de 1 ano" NÃO pode ser Dedutível idade_min 1 que Bloqueia: a exceção (suspeita de doença hemorrágica hereditária ou hemoglobinopatia) barraria bebês elegíveis.
   - Existe uma origem INATIVA errada: "Ambulatório 1ª vez em Cardiologia - Arritimias (Infantil)" ligada a Hematologia (Infantil). Sem efeito na análise.
8. **Proposta (modelo atual):**
   - Pergunta Sim/Não (Sim bloqueia): "Menor de 1 ano SEM suspeita de doença hemorrágica hereditária ou de hemoglobinopatia?"
   - Alternativa: Dedutível idade_min 1 com severidade Ressalva (avisa e deixa o agente decidir) — nunca Bloqueia.
   - Pergunta Sim/Não (Sim bloqueia): "Anemia hipocrômica sem investigação de perda de sangue/ingesta e sem ferro sérico, transferrina e ferritina?"
   - Documento obrigatório (Bloqueia): encaminhamento médico com o texto do "Necessita informar".
   - **Só com regra condicional:** Dedutível "idade < 1 ano → Bloqueia" SALVO se a resposta à pergunta de suspeita (hemorrágica hereditária/hemoglobinopatia) for Sim.

### 53. 4.8.2 Hematologia Pediátrica — ANEMIA

1. **Seção / página / recurso:** 4.8.2 Hematologia Pediátrica · p.35 (a tabela da 4.8.2 começa na p.34 e fica inteira na p.35) · ANEMIA
2. **Estrutura:** CONDICIONAL (eixo: tipo de alteração hematológica, na mesma fila)
3. **Requisitos (literal):**
   - [documento] encaminhamento descrevendo de forma clara e detalhada o caso.
   - [documento] 02 hemogramas (intervalo ≥ 15 dias).
   - [documento] eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico.
   - [documento — condicional] eletroforese de Hb (em suspeita de hemoglobinopatias).
   - [documento — condicional pelo texto "com alterações"] Teste do Pezinho com alterações para hemoglobinopatias, deficiência de G6PD.
   - [inclusão — basta um] Hb < 9 g/dl macrocítica; anemia e icterícia com aumento de bilirrubina indireta; anemia normo/normo EXCLUÍDAS doenças crônicas (IR, DM, colagenoses, hepatopatias).
   - (Sem sorologias e sem função hepática/renal/FAN — diferente do adulto.)
4. **Recurso SER:** Ambulatório 1ª vez - Hematologia (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Hematologia (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (o conteúdo desta tabela foi parar em Hematologia (Adulto)).
   - Os exames obrigatórios mudam por ramo dentro da mesma fila: sem regra condicional, ou se cobra a mais (como hoje no adulto) ou se deixa opcional.
8. **Proposta (modelo atual):**
   - Proposta hoje (Hematologia Infantil, do zero): Pergunta lista basta-uma (Não bloqueia) com as 9 opções do pediátrico (iguais às do adulto, com a esplenomegalia SEM o "no adulto, depois de afastada doença hepática"); Documento obrigatório genérico "Exames laboratoriais exigidos para a alteração marcada"; caixas por ramo como obrigatorio=false: "[Anemia] 02 hemogramas (intervalo ≥ 15 dias)", "[Anemia] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico", "[Anemia, se suspeita] Eletroforese de Hb", "[Anemia] Teste do Pezinho com alterações para hemoglobinopatias / deficiência de G6PD", "[Leucopenia] 02 hemogramas recentes (intervalo mínimo de 1 mês)", "[Hemorrágico] Coagulograma e plaquetas (2 exames diferentes)". SEM sorologias HBV/HCV/HIV e SEM função hepática/renal/FAN — o pediátrico não pede.
   - **Só com regra condicional:** Caixa de exame obrigatória SE a opção marcada pertence ao ramo; eletroforese de Hb SE suspeita; Teste do Pezinho SE houve alteração na triagem neonatal.

### 54. 4.8.2 Hematologia Pediátrica — DISTÚRBIOS DOS LEUCÓCITOS (LEUCOPENIA)

1. **Seção / página / recurso:** 4.8.2 Hematologia Pediátrica · p.35 (a tabela da 4.8.2 começa na p.34 e fica inteira na p.35) · DISTÚRBIOS DOS LEUCÓCITOS (LEUCOPENIA)
2. **Estrutura:** CONDICIONAL (eixo: tipo de alteração hematológica, na mesma fila)
3. **Requisitos (literal):**
   - [documento] encaminhamento descrevendo de forma clara e detalhada o caso.
   - [documento] 02 hemogramas recentes (intervalo mínimo de 1 mês).
   - [inclusão — basta um] leucometria ≤ 3000/mm³; neutrófilos ≤ 1200/mm³; atipias linfocitárias na ausência de quadro viral agudo; esplenomegalia (sem a ressalva do adulto).
4. **Recurso SER:** Ambulatório 1ª vez - Hematologia (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Hematologia (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (o conteúdo desta tabela foi parar em Hematologia (Adulto)).
   - Os exames obrigatórios mudam por ramo dentro da mesma fila: sem regra condicional, ou se cobra a mais (como hoje no adulto) ou se deixa opcional.
8. **Proposta (modelo atual):**
   - Proposta hoje (Hematologia Infantil, do zero): Pergunta lista basta-uma (Não bloqueia) com as 9 opções do pediátrico (iguais às do adulto, com a esplenomegalia SEM o "no adulto, depois de afastada doença hepática"); Documento obrigatório genérico "Exames laboratoriais exigidos para a alteração marcada"; caixas por ramo como obrigatorio=false: "[Anemia] 02 hemogramas (intervalo ≥ 15 dias)", "[Anemia] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico", "[Anemia, se suspeita] Eletroforese de Hb", "[Anemia] Teste do Pezinho com alterações para hemoglobinopatias / deficiência de G6PD", "[Leucopenia] 02 hemogramas recentes (intervalo mínimo de 1 mês)", "[Hemorrágico] Coagulograma e plaquetas (2 exames diferentes)". SEM sorologias HBV/HCV/HIV e SEM função hepática/renal/FAN — o pediátrico não pede.
   - **Só com regra condicional:** Caixa de exame obrigatória SE a opção marcada pertence ao ramo; eletroforese de Hb SE suspeita; Teste do Pezinho SE houve alteração na triagem neonatal.

### 55. 4.8.2 Hematologia Pediátrica — DISTÚRBIOS HEMORRÁGICOS

1. **Seção / página / recurso:** 4.8.2 Hematologia Pediátrica · p.35 (a tabela da 4.8.2 começa na p.34 e fica inteira na p.35) · DISTÚRBIOS HEMORRÁGICOS
2. **Estrutura:** CONDICIONAL (eixo: tipo de alteração hematológica, na mesma fila)
3. **Requisitos (literal):**
   - [documento] encaminhamento descrevendo de forma clara e detalhada o caso.
   - [documento] coagulograma e plaquetas (2 exames diferentes).
   - [inclusão — basta um] síndromes hemorrágicas com plaquetas < 75.000 e/ou TAP ou PTT alterados (metrorragia isolada → ginecologia); alterações no coagulograma em exames subsequentes.
4. **Recurso SER:** Ambulatório 1ª vez - Hematologia (Infantil)
5. **Nosso:** procedimento(s) canônico(s) `Ambulatório 1ª vez - Hematologia (Infantil)`
   - Regras ATIVAS: nenhuma.
   - Regras inativas: nenhuma.
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Sem regra (o conteúdo desta tabela foi parar em Hematologia (Adulto)).
   - Os exames obrigatórios mudam por ramo dentro da mesma fila: sem regra condicional, ou se cobra a mais (como hoje no adulto) ou se deixa opcional.
8. **Proposta (modelo atual):**
   - Proposta hoje (Hematologia Infantil, do zero): Pergunta lista basta-uma (Não bloqueia) com as 9 opções do pediátrico (iguais às do adulto, com a esplenomegalia SEM o "no adulto, depois de afastada doença hepática"); Documento obrigatório genérico "Exames laboratoriais exigidos para a alteração marcada"; caixas por ramo como obrigatorio=false: "[Anemia] 02 hemogramas (intervalo ≥ 15 dias)", "[Anemia] Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico", "[Anemia, se suspeita] Eletroforese de Hb", "[Anemia] Teste do Pezinho com alterações para hemoglobinopatias / deficiência de G6PD", "[Leucopenia] 02 hemogramas recentes (intervalo mínimo de 1 mês)", "[Hemorrágico] Coagulograma e plaquetas (2 exames diferentes)". SEM sorologias HBV/HCV/HIV e SEM função hepática/renal/FAN — o pediátrico não pede.
   - **Só com regra condicional:** Caixa de exame obrigatória SE a opção marcada pertence ao ramo; eletroforese de Hb SE suspeita; Teste do Pezinho SE houve alteração na triagem neonatal.

## Regras de produção com fonte "REUNI p.25–p.34" (e p.35) fora do lugar

Todas as regras com essas fontes estão em 4 procedimentos: Hematologia (Adulto) — 9 ativas e 34 inativas (fora a regra global de encaminhamento); Pré-Natal de Alto Risco — 9 ativas e 31 inativas (idem); Epilepsia Infantil — 1 ativa; Neurovascular — 1 ativa. Nenhuma regra de ortopedia.

| Regra | Estado | Onde está | Onde deveria estar | Efeito |
|---|---|---|---|---|
| "Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD" (fonte "REUNI p.33") | **ATIVA**, Documento obrigatório/Bloqueia | Hematologia (Adulto) | Hematologia (Infantil), ramo anemia, p.35 | Cobra triagem neonatal de todo adulto; pedido fica "A conferir documento" |
| "Coagulograma e Plaquetas (2 exames diferentes)" (fonte "REUNI p.35") | ATIVA | Hematologia (Adulto) | Conteúdo idêntico na p.34 (adulto) — só a fonte está errada | Nenhum no conteúdo; o problema é ser obrigatório para os 3 ramos |
| Lista "Critérios de inclusão (basta um)" (fonte "REUNI p.33 · REUNI p.35") | ATIVA | Hematologia (Adulto) | p.33–34 (o texto das opções é o do adulto) | Nenhum — fonte mista |
| 5 inativas com fonte "REUNI p.35" + duplicatas pediátricas rotuladas "p.33" ("Esplenomegalia" sem ressalva, segunda cópia de anemia/leucopenia) | inativas | Hematologia (Adulto) | Hematologia (Infantil) | Nenhum hoje; mostram que a extração juntou as páginas 33 e 35 |
| "- Gestantes que apresentarem aos exames de USG as seguintes patologias" (Dedutível sexo F), Mielomeningocele, Onfalocele, Encefalocele, Gastrosquise, Rim policístico bilateral, Dilatação pielocalicial (> 6,5mm), Hérnia diafragmática, Hidrocefalia, Fetos com múltiplas malformações, "Gestação acima de 31 semanas e 6 dias", "Malformações cardíacas", "Bolsa rota ACONSELHAMENTO EM MALFORMAÇÃO FETAL" — todas com fonte "REUNI p.31" | inativas | Pré-Natal de Alto Risco | Aconselhamento em malformação fetal, p.32 | Nenhum hoje; o procedimento certo segue sem regra |
| "Gestantes Transplantadas" como Dedutível sexo F; "H. P. Familiar de cromossomopatias Inserir no SER de forma clara e detalhada" como Documento | inativas | Pré-Natal de Alto Risco | São opções da lista (já estão nela) | Nenhum — fragmentos da extração |
| "Inserir EEG de rotina e RNM de encéfalo (se possível)" (fonte "REUNI p.30") | ATIVA, Documento obrigatório/Bloqueia | Epilepsia (Infantil) | Também Epilepsia (Adulto); obrigatoriedade errada | "se possível" virou obrigatório |
| "Inserir Exames de Imagem (TC ou RNM)" (fonte "REUNI p.30") | ATIVA | Neurovascular | Correto. O mesmo texto também vale para Neurocirurgia Adulto/Infantil (exceto coluna), que ficou sem regra | — |

Pares de catálogo errados, todos com origem INATIVA (a análise usa só origem ativa, então não há efeito; mas o `ser_catalogo_resumo.txt` mostra "regras ativas" para eles): Cardiologia Amiloidose Cardíaca → Hematologia (Adulto) (aparece com "regras ativas: 10"); Cardiologia Arritimias (Infantil) → Hematologia (Infantil); Cardiologia Doenças Neuromusculares → Microcirurgia Reconstrutora (Infantil).

## Ordem sugerida de cadastro (por impacto)

1. **Hematologia (Adulto)**: desativar o Teste do Pezinho e passar as caixas de ramo para opcionais com prefixo (é regra ativa errada).
2. **Coluna Adulto + Infantil** (401 pedidos): proposta comum da 4.4.3 — encaminhamento, imagem obrigatória, lista de 14 situações com o exame no texto, idade e infecção.
3. **Joelho (Adulto)** (104 pedidos): lista de 5 indicações com o exame no texto, imagem obrigatória genérica, idade ≥ 16, exclusão de lesão degenerativa. NUNCA idade_min 50.
4. **Pré-natal**: laudo gemelar vira opcional. **Malformação fetal**: cadastrar do zero.
5. **Epilepsia**: separar EEG (obrigatório/Ressalva) da RNM (opcional) e copiar para o Adulto.
6. **Hematologia (Infantil)**, Quadril, Ombro/Cotovelo, filas simples de ortopedia (Pé, Sequelas, Reconstrução, Trauma, Pediátrica, Mão e Joelho infantis), Neurocirurgia sem regra e Microcirurgia.
