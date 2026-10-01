# Conversão das regras de elegibilidade em perguntas de lista — revisão

Gerado em 01/10/2026 a partir da exportação das regras de produção (574 regras, todas ativas, 72
procedimentos), conferido contra os PDFs dos manuais (CRECE e REUNI).

**O defeito:** o manual escreve critério alternativo como lista (“basta uma destas condições”). A
importação gravou cada item como pergunta própria, e o avaliador soma as regras com E — para passar,
o paciente precisaria ter TODAS. Na prática a unidade responde “Não sei” a tudo menos uma (foi o que
aconteceu na solicitação nº 13, Genética Pediátrica) ou o pedido trava.

**O que o plano faz** (141 itens): 36 perguntas de lista novas, 9 correções por nova
versão, e cabeçalhos/cacos/duplicatas desligados — 45 regras novas e 387 regras desligadas
no total. **Nada é apagado:** o que sai fica inativo, com as respostas antigas penduradas, e o log
permite desfazer.

**O que o plano NÃO faz:** decidir o que é clínico e ambíguo. Esses pontos estão marcados com
“Para a regulação confirmar”.

Exclusões (“barra quando a resposta é Sim”) **não** mudam: somar exclusões com E é o certo — qualquer
uma que se aplique barra.

## Ambulatório 1ª vez - Cirurgia Bariátrica (Adulto)

- **desliga** “Conforme preconiza a Portaria SAS n° 492 de 31/08/2007, abaixo” — cabeçalho ("Conforme preconiza a Portaria…, abaixo") sem critério
- **nova versão** de “a. Obesidade decorrente de doença endócrina (p. ex., Síndrome de Cushing devido à hiperplasia suprarrenal). b.”: `{"tipo": 2, "pergunta": "Algum destes critérios de exclusão se aplica ao paciente?", "resposta_bloqueia": 1, "documento_rotulo": null}` — são critérios de EXCLUSÃO gravados como documento obrigatório — viraram pergunta que barra no "Sim"
- **nova versão** de “acima. Se entre 16 e 18 anos, deverão apresentar comprovação de consolidação das epífises dos ossos longos”: `{"obrigatorio": false}` — comprovação de epífises só vale para 16–18 anos; obrigatória para todos travava adulto

## Ambulatório 1ª vez - Hematologia (Adulto)

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: REUNI p.33, REUNI p.35

- [ ] Alterações no coagulograma em exames subsequentes
- [ ] Anemia e icterícia com aumento de bilirrubina indireta
- [ ] Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como Insuficiência Renal, diabetes mellitus, colagenoses e hepatopatias
- [ ] Atipias linfocitárias na ausência de quadro viral agudo
- [ ] Esplenomegalia (no adulto, depois de afastada doença hepática de qualquer etiologia)
- [ ] Hemoglobina sérica < 9 g/dl, de padrão macrocítico
- [ ] Leucometria global < ou = 3000/mm³
- [ ] Neutrófilos < ou = 1200/mm³
- [ ] Síndromes hemorrágicas: hematomas, equimoses, epistaxes, metrorragia (quando isolada deve ser avaliada ginecológica) com plaquetas < 75.000 e/ou TAP ou PTT alterados

Substitui 16 pergunta(s)/regra(s) soltas, que ficam inativas.
- também desligada: `3c0d473f` — duplicata
- também desligada: `ca049194` — duplicata
- também desligada: `e37923fc` — duplicata
- também desligada: `83c3f7ec` — duplicata
- também desligada: `42a7d9e5` — duplicata
- também desligada: `8767fb4f` — duplicata
- também desligada: `1aeefdac` — condição gravada como documento — entrou no texto da opção Esplenomegalia
- **desliga** “/ Agendamento” — caco "/ Agendamento"
- **desliga** “/ Agendamento” — caco "/ Agendamento"
- **desliga** “/ Agendamento” — caco "/ Agendamento"
- **desliga** “/ Agendamento” — caco "/ Agendamento"
- **desliga** “/ Agendamento” — caco "/ Agendamento"
- **desliga** “Inserir no SER” — caco "Inserir no SER" sem documento
- **desliga** “Inserir no SER” — caco "Inserir no SER" sem documento
- **desliga** “Inserir no SER” — caco "Inserir no SER" sem documento
- **desliga** “Inserir no SER” — caco "Inserir no SER" sem documento
- **desliga** “02 hemogramas (intervalo > ou = 15 dias)” — documento duplicado
- **desliga** “02 hemogramas recentes (intervalo mínimo de 1 mês)” — documento duplicado
- **desliga** “Eletroforese de Hb (em suspeita de hemoglobinopatias)” — documento duplicado
- **desliga** “Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitaminaB12, ácido fólico” — documento duplicado
- **desliga** “Encaminhamento médico descrevendo de forma clara e detalhada o caso” — duplicata do encaminhamento médico (já existe a regra global)
- **desliga** “Encaminhamento médico descrevendo de forma clara e detalhada o caso” — duplicata do encaminhamento médico (já existe a regra global)
- **desliga** “Encaminhamento médico descrevendo de forma clara e detalhada o caso” — duplicata do encaminhamento médico (já existe a regra global)
- **desliga** “Encaminhamento médico descrevendo de forma clara e detalhada o caso” — duplicata do encaminhamento médico (já existe a regra global)
- **desliga** “Encaminhamento médico descrevendo de forma clara e detalhada o caso” — duplicata do encaminhamento médico (já existe a regra global)

> **Para a regulação confirmar:** Os documentos restantes (hemogramas, eletroforeses, coagulograma, sorologias, teste do pezinho) são, no manual, de indicações específicas — hoje são OBRIGATÓRIOS para todo pedido. Proposta: torná-los opcionais. Não está no plano: é decisão da regulação.

## Ambulatório 1ª vez - Hematologia (Oncologia)

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão/agendamento (basta um)* · fonte: REUNI p.20

- [ ] Dor óssea, com eletroforese de proteínas séricas, evidenciando Gamopatia Monoclonal (casos de Mieloma múltiplo)
- [ ] Bicitopenia após afastar todas as causas clínicas (hipotireoidismo, doenças hepáticas, uso de medicamentos, esplenomegalia com doença hepática)
- [ ] Citopenias + manifestações clínicas suspeitas de leucemia aguda (fadiga generalizada, fraqueza, palidez, equimose, petéquias, sangramentos, infecções recorrentes)
- [ ] Citopenias + linfonodomegalia e esplenomegalia não explicada por quadro infeccioso agudo
- [ ] Presença de blastos/promielócitos no sangue periférico
- [ ] Paciente com febre + neutropenia (< 1500 /μL)
- [ ] Bicitopenia/pancitopenia com alterações hematológicas graves: Hemoglobina < 7 g/dl e/ou Neutrófilos < 500 cels/μL e/ou Plaquetas < 50 mil cels/mm³
- [ ] Trombocitose: isolada > 600.000/mm³ em 3 exames c/ intervalo < 1 mês; ou acompanhada de leucocitose e aumento do hematócrito; ou associada a sintomas vasomotores, sangramento ou trombose (após avaliação em serviço de urgência/emergência); ou associada a leucocitose ou policitemia — excluir trombocitose persistente por causas secundárias (quadro infeccioso atual, anemia ferropriva, esplenectomia/asplenia, trauma/cirurgia recente)
- [ ] Leucocitose com desvio à esquerda em ausência de infecção: com manifestações clínicas suspeitas de leucemia aguda; ou presença de blastos e promielócitos no sangue periférico; ou leucostase (sintomas respiratórios, neurológicos, priapismo com hiperleucocitose) ou leucócitos > 100 mil cels/mm³
- [ ] Policitemia: suspeita de Policitemia vera (Hb > 16,0 g/dl em mulheres e > 16,5 g/dl em homens) com sintomas sugestivos (prurido após o banho, gota, trombose venosa ou arterial prévia, sangramento, esplenomegalia); ou policitemia persistente após repetir o hemograma em 1 mês e excluir causas secundárias (DPOC, tabagismo, hepatocarcinoma, carcinoma renal)

Substitui 4 pergunta(s)/regra(s) soltas, que ficam inativas.
- também desligada: `eb59818f` — sub-critério da bicitopenia grave — entrou no texto da opção
- também desligada: `36ab06c7` — sub-critério da bicitopenia grave — entrou no texto da opção
- também desligada: `7d00ce8b` — plaquetas + trombocitose + leucocitose + policitemia colados numa regra de "sexo F" (barrava todo homem) — viraram opções

> **Para a regulação confirmar:** O manual (CRECE "Oncologia – Hematologia adulto") lista as indicações por letra (a, b, c.1–c.4, d/e, f, g); a extração colou várias numa regra só e marcou a de plaquetas como "sexo F". As opções são o texto do manual partido por letra (d e e são o mesmo texto repetido no manual). A OBSERVAÇÃO do manual — suspeita de leucemia aguda não é regulada ambulatorialmente, precisa estar internado — não cabe em pergunta: fica para a regulação decidir se vira regra informativa.

## Ambulatório 1ª vez - Hematologia Pediátrica (Oncologia)

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão/agendamento (basta um)* · fonte: REUNI p.22

- [ ] Bicitopenia após afastar todas as causas clínicas (hipotireoidismo, doenças hepáticas, uso de medicamentos, esplenomegalia com doença hepática)
- [ ] Citopenias com manifestações clínicas suspeitas de leucemia aguda (fadiga generalizada, fraqueza, palidez, equimose, petéquias, sangramentos, infecções recorrentes)
- [ ] Citopenias com linfonodomegalia e esplenomegalia não explicada por quadro infeccioso agudo
- [ ] Presença de blastos/promielócitos no sangue periférico
- [ ] Paciente com febre + neutropenia (< 1500 /μL)
- [ ] Bicitopenia/pancitopenia com alterações hematológicas graves: Hemoglobina < 7 g/dl e/ou Neutrófilos < 500 cels/μL e/ou Plaquetas < 50 mil cels/mm³
- [ ] Trombocitose: isolada > 600.000/mm³ em 3 exames c/ intervalo < 1 mês; ou acompanhada de leucocitose e aumento do hematócrito; ou associada a sintomas vasomotores, sangramento ou trombose (após avaliação em serviço de urgência/emergência); ou associada a leucocitose ou policitemia — excluir trombocitose persistente por causas secundárias (quadro infeccioso atual, anemia ferropriva, esplenectomia/asplenia, trauma/cirurgia recente)
- [ ] Leucocitose com desvio à esquerda em ausência de infecção: com manifestações clínicas suspeitas de leucemia aguda; ou presença de blastos e promielócitos no sangue periférico

Substitui 4 pergunta(s)/regra(s) soltas, que ficam inativas.
- também desligada: `33de5332` — sub-critério da bicitopenia grave — entrou no texto da opção
- também desligada: `ab219522` — sub-critério da bicitopenia grave — entrou no texto da opção
- também desligada: `ceeb19b0` — plaquetas + trombocitose + leucocitose coladas numa regra só — viraram opções

> **Para a regulação confirmar:** Mesmo caso da Hematologia (Oncologia) adulto: opções = texto do manual (REUNI p.22) partido por letra.

## Ambulatório 1ª vez - Pré Natal de Alto Risco Estratégico

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Gestantes que apresentarem (basta uma condição)* · fonte: REUNI p.31

- [ ] Cardiopatias
- [ ] Colagenoses
- [ ] Doenças Hematológicas
- [ ] Hemopatias
- [ ] Nefropatias
- [ ] Neuropatias
- [ ] Patologias malignas
- [ ] Tireoideopatias
- [ ] Pneumopatias
- [ ] Gestantes Transplantadas
- [ ] Patologias uterinas
- [ ] Incompetência Istmo cervical
- [ ] Gemelar (sem comorbidades até 28 semanas): somente gestação gemelar mono/monoamniótico ou com corionicidade desconhecida até 16 semanas
- [ ] Alo imunização materna, sem comorbidades em qualquer idade gestacional (resultado de COOMBS indireto +)
- [ ] Peso maior que 140Kg (incluir peso e altura na solicitação)
- [ ] H.P.P. de natimorto, neomorto, prematuridade, malformação fetal
- [ ] H.P.P. de trombose venosa profunda
- [ ] H. P. Familiar de cromossomopatias

Substitui 18 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “- Gestantes que apresentarem aos exames de USG as seguintes patologias” — cabeçalho da seção "Aconselhamento em Malformação Fetal" (outro recurso)
- **desliga** “Dilatação pielocalicial (> 6,5mm)” — condição da seção "Aconselhamento em Malformação Fetal" — outro recurso, que caiu neste procedimento
- **desliga** “Encefalocele” — condição da seção "Aconselhamento em Malformação Fetal" — outro recurso, que caiu neste procedimento
- **desliga** “Fetos com múltiplas malformações” — condição da seção "Aconselhamento em Malformação Fetal" — outro recurso, que caiu neste procedimento
- **desliga** “Gastrosquise” — condição da seção "Aconselhamento em Malformação Fetal" — outro recurso, que caiu neste procedimento
- **desliga** “Hérnia diafragmática” — condição da seção "Aconselhamento em Malformação Fetal" — outro recurso, que caiu neste procedimento
- **desliga** “Hidrocefalia” — condição da seção "Aconselhamento em Malformação Fetal" — outro recurso, que caiu neste procedimento
- **desliga** “Mielomeningocele” — condição da seção "Aconselhamento em Malformação Fetal" — outro recurso, que caiu neste procedimento
- **desliga** “Onfalocele” — condição da seção "Aconselhamento em Malformação Fetal" — outro recurso, que caiu neste procedimento
- **desliga** “Rim policístico bilateral” — condição da seção "Aconselhamento em Malformação Fetal" — outro recurso, que caiu neste procedimento
- **desliga** “Gestação acima de 31 semanas e 6 dias” — exclusão da seção "Aconselhamento em Malformação Fetal" — barrava gestante de alto risco entre 32 e 36 semanas
- **desliga** “Malformações cardíacas” — exclusão da seção "Aconselhamento em Malformação Fetal" (outro recurso)
- **nova versão** de “Bolsa rota ACONSELHAMENTO EM MALFORMAÇÃO FETAL”: `{"descricao": "Bolsa rota", "pergunta": "Bolsa rota"}` — texto corrompido na extração ("Bolsa rota ACONSELHAMENTO EM MALFORMAÇÃO FETAL" — RUNBOOK §2)

> **Para a regulação confirmar:** A lista confere com o PDF (REUNI p.31, 18 condições). A seção "Aconselhamento em Malformação Fetal" (REUNI p.32) é OUTRO recurso e tinha caído aqui — as 9 condições e as 2 exclusões dela saem deste procedimento (a exclusão "acima de 31 semanas e 6 dias" barrava gestante de alto risco de 32 a 36 semanas). Elas precisam ser cadastradas no recurso de Aconselhamento, se ele existir no catálogo.

## Ambulatório 1ª vez em Cardiologia Estudo Eletrofisiológico / Ablação

- **nova versão** de “Informar modo ventilatório”: `{"tipo": 4, "pergunta": null, "resposta_bloqueia": null}` — "Informar modo ventilatório" é instrução do encaminhamento, não pergunta de sim/não — vira informativa

## Ambulatório 1ª vez em Genética Médica - Adulto

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: REUNI p.36

- [ ] Pacientes com Aborto de Repetição: História de 03 abortos espontâneos ou mais de primeiro trimestre previamente investigados das causas infecciosas, materna, imunológicas, metabólicas e placentária
- [ ] Pacientes com necessidade de Aconselhamento Genético, com história pessoal ou familiar de condição de causa genética e/ou malformação congênita, excluindo câncer familiar

Substitui 2 pergunta(s)/regra(s) soltas, que ficam inativas.

## Ambulatório 1ª vez em Genética Médica - Pediatria

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes portadores das seguintes condições* · fonte: REUNI p.36

- [ ] Dismorfias, Anomalia Congênita e/ou Malformação Congênita
- [ ] Genitália ambígua
- [ ] Fraturas Patológicas
- [ ] Osteogênese imperfeita
- [ ] Síndromes com Malformações Congênitas que acometem Múltiplos Sistemas
- [ ] Doenças do desenvolvimento para uma investigação de uma síndrome genética
- [ ] Deficiência Intelectual e/ou Autismo SINDRÔMICO
- [ ] Síndromes com malformações congênitas associadas ao Nanismo
- [ ] Erros Inatos do Metabolismo (Aminoácido; Carboidratos; Glicosaminoglicano)
- [ ] Suspeita de bebês com síndrome de Down
- [ ] Anomalia Cromossômica Constitucional
- [ ] Doenças Raras

Substitui 12 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes portadores das seguintes condições” — cabeçalho solto da lista
- **nova versão** de “Pacientes na faixa etária de 0 a 19 anos incompletos”: `{"idade_max_anos": 18}` — "0 a 19 anos incompletos" = no máximo 18; a regra tinha 19 e deixava passar quem já fez 19

## Ambulatório de 1ª vez - Hormonização - Saúde - Trans

- **desliga** “Inserir no SER de forma clara e detalhada” — caco "Inserir no SER de forma clara e detalhada"
- **desliga** “Encaminhamento médico” — duplicata do encaminhamento médico

## CONSULTA EM ALERGOLOGIA - PEDIATRIA

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.12

- [ ] Asma brônquica persistente de crianças entre 2 a 12 anos
- [ ] Rinite alérgica persistente de crianças entre 2 a 12 anos
- [ ] Asma grave - crises frequentes (mais de cinco por ano)
- [ ] Internações - atendimento de emergência
- [ ] Prejuízo na Escola - dificuldades em exercícios e esportes
- [ ] Lactentes sibilante - até 2 anos de idade
- [ ] Sinusites frequentes
- [ ] Dermatite atópica moderada a grave
- [ ] Dermatite atópica grave com infecção secundária, grande extensão e prejuízo estético
- [ ] Urticária crônica e angioedema com mais de 6 semanas de duração
- [ ] Urticária e angioedema agudos e crônicos com fator etiológico desconhecido
- [ ] Alergia alimentar
- [ ] Anafilaxia
- [ ] Dermatite de contato extensa e sem etiologia definida
- [ ] Alergia alimentar com reações sistêmicas graves
- [ ] Reações adversas a drogas com sintomas sistêmicos graves
- [ ] Reação grave a picada de inseto com reação sistêmica ou reaccao local importante ou infecções secundárias e cicatrizes
- [ ] Imunodeficiência primária- suspeita se tiver: Pneumonia de repetição (mais de 2 no último ano); infecções de repetição (mais de 8 no último ano)
- [ ] Otite, sinusite e etc. com abscessos de repetição
- [ ] Reações adversas a BCG
- [ ] Monilíase oral por mais de 2 meses (sapinho)
- [ ] História de imunodeficiência familiar
- [ ] Infecções sistêmicas graves (sepse, meningite, etc.)

Substitui 23 pergunta(s)/regra(s) soltas, que ficam inativas.

> **Para a regulação confirmar:** As três faixas etárias (asma 2–12, rinite 2–12, lactente sibilante até 2) viraram opções: eram alternativas e, somadas, só passava quem tinha exatamente 2 anos. As EXCLUSÕES do manual repetem itens da inclusão (alergia alimentar sistêmica grave, dermatite de contato extensa, imunodeficiência primária, lactente sibilante, reação a medicamentos, investigação de imunidade) — o manual se contradiz; ficaram como estão, e quem marcar uma dessas inclusões vai bater na exclusão. Precisa de decisão da regulação.

## CONSULTA EM ANGIOLOGIA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.13

- [ ] Doença arterial periférica
- [ ] Claudicação intermitente
- [ ] Ausência de pulsos arteriais
- [ ] Varizes essenciais ou IVC (sem úlcera)
- [ ] Linfedema (edema linfático)
- [ ] Elefantíase
- [ ] Angiodisplasia (má formação vascular)
- [ ] Síndrome do desfiladeiro cérvico-torácico
- [ ] Artrite de Takayuasu
- [ ] Doença de Behcet

Substitui 10 pergunta(s)/regra(s) soltas, que ficam inativas.

## CONSULTA EM CARDIOLOGIA - PEDIATRIA

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.14

- [ ] Investigação diagnóstica de “Sopro Cardíaco”
- [ ] Investigação de Cianose. Investigação de arritmia cardíaca detectada através do exame cardiovascular anormal e/ou eletrocardiograma suspeito
- [ ] Investigação diagnóstica de Síncope
- [ ] Investigação diagnóstica de Insuficiência Cardíaca
- [ ] Investigação de dor torácica na infância e na adolescência
- [ ] Avaliação cardiológica nas síndromes genéticas, com diagnóstico confirmado
- [ ] Casos suspeitos de Doenças Reumáticas

Substitui 7 pergunta(s)/regra(s) soltas, que ficam inativas.

## CONSULTA EM CIRURGIA GERAL - ESOFAGO

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes que apresentem determinadas patologias, como* · fonte: CRECE p.50

- [ ] Doença do refluxo gastroesofágico
- [ ] Úlceras, Acalasia e Divertículos
- [ ] Não respondem satisfatoriamente ao tratamento clínico, inclusive, aqueles com manifestações atípicas cujo refluxo foi devidamente comprovado
- [ ] Esôfago de barret, Esteatose, Úlcera e sangramento esofágico
- [ ] Outra condição do mesmo grupo (descrever no encaminhamento)

Substitui 5 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes que apresentem determinadas patologias, como” — cabeçalho solto da lista
- **desliga** “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador” — instrução ("observar se há necessidade de exames do prestador"), não requisito

## CONSULTA EM CIRURGIA GERAL - PANCREAS

- **desliga** “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador” — instrução ("observar se há necessidade de exames do prestador"), não requisito

## CONSULTA EM CIRURGIA GERAL - PARTES MOLES

- **desliga** “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador” — instrução ("observar se há necessidade de exames do prestador"), não requisito

## CONSULTA EM CIRURGIA GERAL - VESICULA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes que apresentem determinadas patologias, como* · fonte: CRECE p.52

- [ ] Colelitíase na vigência de sintomas ou assintomáticos com história prévia de complicações (colecistite, colangite e/ou pancreatite)
- [ ] Coledocolitíase
- [ ] Cisto de Colédoco
- [ ] Pólipo de vesícula
- [ ] Outra patologia cirúrgica de vesícula (descrever no encaminhamento)

Substitui 4 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes que apresentem determinadas patologias, como” — cabeçalho solto da lista
- **desliga** “Observar se há necessidade de apresentação de exames complementares definidos pelo prestador” — instrução ("observar se há necessidade de exames do prestador"), não requisito

> **Para a regulação confirmar:** "Como" = lista de exemplos; a opção "Outra patologia cirúrgica de vesícula" foi acrescentada para não barrar o que o manual aceita. A regra "vagas exclusivamente para patologias cirúrgicas de vesícula" continua separada.

## CONSULTA EM CIRURGIA PEDIATRICA


> **Para a regulação confirmar:** "Patologias cirúrgicas" (texto cortado) e "indicação para cirurgia otorrinolaringológica" estão somadas. Não achei a seção no PDF para confirmar se são alternativas — não mexi.

## CONSULTA EM DERMATOLOGIA

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.16, CRECE p.17

- [ ] Pacientes portadores de dermatoses inflamatórias, como psoríase, vitiligo, acne e hidradenite
- [ ] Pacientes portadores de dermatoses infecciosas (bacterianas - virais - fúngicas e outras)
- [ ] Pacientes portadores de dermatoses de causa imunológica e autoimunes
- [ ] Pacientes portadores de dermatoses alérgicas (Ex.: dermatite atópica)
- [ ] Pacientes com Carcinoma Basocelular, Carcinoma Espinocelular e Melanoma
- [ ] Pacientes em Tratamento para Hanseníase com quadros reacionais hansênicos de difícil controle: Reações adversas às drogas utilizadas na poliquimioterapia
- [ ] Pacientes em alta terapêutica (PQT): Quadros reacionais pós-tratamento de difícil controle e suspeita de recidiva
- [ ] Hemangioma Plano Cavernoso e Tuberoso
- [ ] Malformações vasculares, arteriovenosas, capilares
- [ ] Síndromes: Sturge-Weber, Kasabatt-Merritt, Nevus Azul, Osler-Rendu-Weber, Klippel-Trenanay e Mafucci
- [ ] Dermatoses pediátricas
- [ ] Casos suspeitos de difícil confirmação
- [ ] Doenças sexualmente transmissíveis
- [ ] Genodermatoses
- [ ] Tumores cutâneos benignos e malignos
- [ ] Pacientes com queixas de imperfeições na pele

Substitui 16 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Eletrocoagulação de lesão cutânea” — item da seção "Biópsia de pele" — outro recurso do SER (CONSULTA EM DERMATOLOGIA - BIOPSIA DE PELE)
- **desliga** “Exérese de tumor de pele/cisto sebáceo/lipoma” — item da seção "Biópsia de pele" — outro recurso do SER (CONSULTA EM DERMATOLOGIA - BIOPSIA DE PELE)
- **desliga** “Retirada de lesão por shaving” — item da seção "Biópsia de pele" — outro recurso do SER (CONSULTA EM DERMATOLOGIA - BIOPSIA DE PELE)
- **desliga** “Fulguração/cauterização química de lesões cutâneas” — item da seção "Biópsia de pele" — outro recurso do SER (CONSULTA EM DERMATOLOGIA - BIOPSIA DE PELE)

> **Para a regulação confirmar:** Os quatro procedimentos de biópsia (eletrocoagulação, exérese, shaving, fulguração) são da seção "Biópsia de pele", que tem recurso próprio no SER — saem daqui. As exclusões (alta terapêutica, hanseníase com boa evolução, casos simples) continuam.

## CONSULTA EM DERMATOLOGIA - PEQUENOS PROCEDIMENTOS

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.17

- [ ] Pacientes portadores de doenças dermatológicas para esclarecimento diagnóstico (inclusive com necessidade de biópsia)
- [ ] Realização de procedimentos cirúrgicos como eletrocoagulação de pequenas lesões e cauterização química

Substitui 2 pergunta(s)/regra(s) soltas, que ficam inativas.

## CONSULTA EM ENDOCRINOLOGIA - DOENCAS DO OVARIO

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.19

- [ ] Amenorreia primária ou secundária – Pacientes sem menarca após os 13 anos ou sem menstruação por mais de 3 meses já avaliados pela Ginecologia
- [ ] Hipogonadismo – Pacientes com deficiência dos hormônios femininos
- [ ] Hirsutismo - Aumento dos pelos corporais (face, tórax, abdome, etc.)
- [ ] Infertilidade
- [ ] Reposição hormonal feminina pós-menopausa
- [ ] Síndrome dos ovários policísticos

Substitui 6 pergunta(s)/regra(s) soltas, que ficam inativas.

## CONSULTA EM ENDOCRINOLOGIA - DOENCAS OSTEOMETABOLICAS

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes apresentando* · fonte: CRECE p.21

- [ ] Hiperparatireoidismo
- [ ] Hipoparatireoidismo/Pseudohipoparatireoidismo – níveis de cálcio reduzidos
- [ ] Hipercalcemias (Exceto ligadas a doença renal ou malignidade)
- [ ] Nefrolitíase de repetição (bilateral)
- [ ] Osteoporose (Densitometria óssea com T score maior que -2,5 desvios padrão, com ou sem fratura prévia
- [ ] Doença de Paget
- [ ] Osteogênese Imperfeita
- [ ] Raquitismo e Osteomalacia

Substitui 8 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes apresentando” — cabeçalho solto da lista

## CONSULTA EM ENDOCRINOLOGIA - HIPOFISE/ADRENAL

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão — Hipófise ou Supra-renal (basta um)* · fonte: CRECE p.19

- [ ] Hipófise — Hiperprolactinemia: Galactorréia e/ou amenorréia com história de o prolactina elevada
- [ ] Hipófise — Síndrome de Cushing: Níveis de cortisol elevados ou investigação de hipercortisolismo em pacientes com clínica sugestiva (estrias violáceas, o hipertensão arterial, diabetes mellitus, osteoporose, etc.)
- [ ] Hipófise — Pan-hipopituitarismo: Investigação de deficiência de hormônios hipofisários (cortisol baixo, amenorreia, níveis de testosterona reduzidos, etc.)
- [ ] Hipófise — Acromegalia: Níveis elevados de IGF-1 e hormônio de crescimento com alterações em face e extremidades
- [ ] Hipófise — Incidentalomahipofisário ou sela vazia: massas ou cistos ou aspecto de sela vazia na região da sela turca em exames de imagem
- [ ] Hipófise — História de apoplexia hipofisária
- [ ] Hipófise — Diabetes insípidus: produção de urina e sede excessivas, afastadas outras causas comuns de poliúria ( Diabetes Mellitus)
- [ ] Hipófise — Pós-operatório de cirurgia hipofisária
- [ ] Supra-renal — Insuficiência Adrenal: fraqueza, níveis de sódio reduzidos, eosinófilos aumentados, hipotensão
- [ ] Supra-renal — Hiperplasia Adrenal Congênita
- [ ] Supra-renal — Investigação de massas adrenais
- [ ] Supra-renal — Hiperaldosteronismo
- [ ] Supra-renal — Hipertensão arterial em uso de 4 anti-hipertensivos ou 3 anti- hipertensivos sem controle, podendo estar associado a hipocalemia
- [ ] Supra-renal — Feocromocitoma
- [ ] Supra-renal — Hipertensão em paroxismos, cefaleia, palpitações, etc

Substitui 15 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “SUPRA RENAL” — cabeçalho solto ("SUPRA RENAL")
- **desliga** “SUPRA RENAL: Observar os” — caco ("SUPRA RENAL: Observar os")

> **Para a regulação confirmar:** Hipófise e Supra-renal eram DUAS listas no mesmo procedimento — quem tinha doença de hipófise "não" tinha da supra-renal e era barrado. Viraram uma lista só.

## CONSULTA EM ENDOCRINOLOGIA - PEDIATRICA DIABETES

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.21

- [ ] Diabete Mellitus Tipo 1
- [ ] Diabete Mellitus Tipo 2
- [ ] Diabete Mellitus Pós-Transplante
- [ ] Glicemia de Jejum alterada (glicemia > 99mg/dl)
- [ ] Intolerância a Glicose (com teste oral de intolerância a glicose alterado TOTG alterado)

Substitui 5 pergunta(s)/regra(s) soltas, que ficam inativas.

## CONSULTA EM ENDOCRINOLOGIA - SINDROME METABOLICA

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.21

- [ ] Paciente com Hipertensão Arterial, Diabetes, Obesidade (IMC < 35) e níveis de colesterol elevados
- [ ] Pacientes com Distúrbios Endócrinos

Substitui 2 pergunta(s)/regra(s) soltas, que ficam inativas.

> **Para a regulação confirmar:** A idade (a partir de 18 anos) continua regra separada — é requisito, não alternativa.

## CONSULTA EM ENDOCRINOLOGIA - TIREOIDE

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes apresentando as seguintes alterações* · fonte: CRECE p.22

- [ ] Tireoidite aguda (bacteriana/fúngica) e subaguda confirmadas
- [ ] Hipertireoidismo (doença de graves - bócio nodular tóxico) sem tratamento, Bócio uni ou multinodular atóxico com nódulos: hipoecóicos maiores de 1 cm; isoecóico, mistos e hiperecóico > - 1 - 5 cm; císticos acima de 3 cm
- [ ] Nódulos maiores que 01 cm. (Obs.: levar exames para consulta, caso tenha)

Substitui 3 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes apresentando as seguintes alterações” — cabeçalho solto da lista
- **desliga** “Investigação de disfunção tireoidiana (Hiper e Hipotireoidismo)” — cabeçalho da lista de exclusões ("Investigação de disfunção tireoidiana:") — os itens dela continuam como exclusão

## CONSULTA EM FISIATRIA

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.23

- [ ] Pacientes acima de 60 anos
- [ ] Cervicalgias
- [ ] Ombro doloroso
- [ ] Tendinopatias de Quervain
- [ ] Dedo em Gatilho
- [ ] Esporão de calcâneo
- [ ] Paralisia facial periférica
- [ ] Lombalgia de origem vertebral
- [ ] Artrose
- [ ] Gonalgia

Substitui 8 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes portadores das seguintes alterações” — cabeçalho solto da lista

> **Para a regulação confirmar:** "Pacientes acima de 60 anos" virou opção da lista (no manual é um tópico de inclusão ao lado das alterações). "Artrose" e "Gonalgia" estão no PDF (CRECE p.23) e a extração perdeu — acrescentadas. Se a regulação ler "acima de 60 E com uma das alterações", a idade volta a ser regra separada.

## CONSULTA EM GASTROENTEROLOGIA

**Pergunta de lista** — “O paciente se enquadra em ao menos um dos critérios abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão — Geral ou Doença Inflamatória Intestinal (basta um)* · fonte: CRECE p.24, CRECE p.25

- [ ] Epigastralgia refratária ao uso de inibidor de bomba de prótons
- [ ] Úlcera péptica que persiste após controle endoscópico (realizado 8 a 12 semanas após início do tratamento) e/ou complicada (passado de hemorragia e/ou estenose)
- [ ] Doença do Refluxo Gastroesofágico (DRGE) com manifestações típicas e/ou associada à hérnia hiatal e refratária ao tratamento otimizado por 3 meses
- [ ] DRGE com esofagite grau C ou D de Los Angeles e/ou complicada com úlcera
- [ ] Estenose péptica e/ou esôfago de Barrett
- [ ] Queixas otorrinolaringológicas (pigarro, rouquidão, globus cervical, faringite de repetição) com investigação inicial sugestiva de DRGE e de difícil tratamento
- [ ] Queixas respiratórias (tosse crônica, asma de início recente, pneumonias de Repetição, fibrose pulmonar idiopática) cuja investigação inicial sugira DRGE
- [ ] Esofagites infeciosas
- [ ] Esofagites secundárias a doenças dermatológicas
- [ ] Pancreatite crônica
- [ ] Lesões nodulares ou císticas do pâncreas
- [ ] Fibrose cística
- [ ] Diarréia crônica (> 4 semanas com exame parasitológico negativo)
- [ ] Constipação sem melhora após 12 semanas de tratamento na Atenção Primária em Saúde
- [ ] Doença celíaca
- [ ] Colite microscópica
- [ ] Pólipos de cólon, exceto: pólipos hiperplásicos menores do que 10 mm localizados no reto ou sigmoide - 1 a 2 adenomas tubulares menores do que 10 mm
- [ ] Tumores neuroendócrinos gastrointestinais
- [ ] Sangramento gastrointestinal/anemia ferropriva de origem obscura - Doenças do peritônio
- [ ] DII, maiores de 18 anos — Doença de Crohn (CID K50.0 e K50.1)
- [ ] DII, maiores de 18 anos — Retocolite Ulcerativa (CID K51.1)
- [ ] DII, maiores de 18 anos — Colite Indeterminada (CID K50.8)

Substitui 25 pergunta(s)/regra(s) soltas, que ficam inativas.
- também desligada: `296b48c1` — idade ≥ 18 é só da seção DII — está no texto das opções de DII
- também desligada: `d8a83269` — exclusão só da seção DII — barrava paciente geral em investigação
- também desligada: `450ed419` — exclusão só da seção DII — barrava paciente geral em investigação
- **desliga** “Pacientes com suspeita de alergia alimentar que apresentem sintomas digestivos (vômitos, diarreia, sangramento” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Baixa estatura desde que tenham sido previamente avaliados pelo setor de endocrinologia” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Constipação crônica não responsiva ao tratamento inicial proposto pelo Pediatra Geral” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Todos aqueles com encoprese” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Diarreia com duração superior a 01 mês e exame parasitológico negativo” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Dor abdominal com duração superior a 02 meses e/ou pelo menos 3 episódios recorrentes num período de 02 meses,” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Dor epigástrica ou gastrite não responsiva ao tratamento com antagonista H2 e com exame parasitológico negativ” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Fibrose cística com comprometimento do trato digestivo” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Todos os pacientes com hemorragia digestiva alta ou baixa” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Hepatites desde que tenham IgM negativo para Hepatite A ou IgM positivo para Hepatite A por período superior a” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Hepatoesplenomegalia” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Hipertensão porta” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Icterícia: Todos os pacientes com aumento de bilirrubina direta” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Icterícia: Criancas > - 2 anos com aumento de bilirrubina indireta” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Refluxo gastroesofágico: Todos os pacientes maiores de 2 anos” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Refluxo gastroesofágico: Pacientes menores de 2 anos que apresentam algum dos sintomas como anemia não respons” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Litíase biliar (Apresentar ultrassonografia abdominal” — critério da seção PEDIATRIA — a Gastro Pediátrica tem recurso próprio no SER
- **desliga** “Pacientes que apresentam as seguintes alterações” — cabeçalho solto
- **desliga** “Pacientes que apresentam as seguintes alterações” — cabeçalho solto
- **desliga** “Icterícia” — cabeçalho solto ("Icterícia")
- **desliga** “Refluxo gastroesofágico” — cabeçalho solto ("Refluxo gastroesofágico")
- **desliga** “É obrigatório encaminhamento médico preenchido, assinado e carimbado pelo médico solicitante” — duplicata do encaminhamento médico

> **Para a regulação confirmar:** O manual tem três seções (Geral, Pediatria, DII) e as três caíram neste procedimento; somadas, nada passava (≥18 E ≤2). A Pediatria tem recurso próprio no SER (CONSULTA EM GASTROENTEROLOGIA - PEDIATRIA) — os 17 critérios dela saem daqui e precisam ser cadastrados lá. Geral e DII viraram uma lista só; a idade da DII (maiores de 18) fica no texto da opção, e as duas exclusões da DII saem (barravam paciente da Geral em investigação). Se a DII também tiver recurso próprio, o mesmo vale para ela.

## CONSULTA EM GASTROENTEROLOGIA - GASTROSTOMIA - INTERNADOS

- **desliga** “É obrigatório encaminhamento médico preenchido, assinado e carimbado pelo médico solicitante” — duplicata do encaminhamento médico

## CONSULTA EM GASTROENTEROLOGIA - HEPATOLOGIA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes que apresentam as seguintes alterações* · fonte: CRECE p.26

- [ ] Hepatites agudas
- [ ] Hepatite B Crônica
- [ ] Hepatite autoimune
- [ ] Síndromes colestáticas crônicas
- [ ] Doença gordurosa não alcoólica-hepática
- [ ] Esquistossomose hepatoesplênica
- [ ] Hepatopatias a esclarecer

Substitui 7 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes que apresentam as seguintes alterações” — cabeçalho solto da lista

## CONSULTA EM GINECOLOGIA - CIRURGIA DE BAIXO E MEDIO RISCO

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes com indicação cirúrgica para resolução das seguintes patologias* · fonte: CRECE p.27

- [ ] Miomatose uterina
- [ ] Massas anexiais benignas
- [ ] Sangramento uterino anormal
- [ ] Adenomiose

Substitui 4 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes com indicação cirúrgica para resolução das seguintes patologias” — cabeçalho solto da lista

## CONSULTA EM GINECOLOGIA - ENDOCRINOLOGIA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes apresentando as seguintes patologias* · fonte: CRECE p.29

- [ ] Síndrome do Ovário Policístico
- [ ] Puberdade precoce e tardia
- [ ] Hiperprolactinemia/Galactorréia
- [ ] Síndrome do climatério
- [ ] Menopausa precoce e tardia
- [ ] Ginecologia da adolescência (a partir de 11 anos de idade)
- [ ] Irregularidade menstrual

Substitui 7 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes apresentando as seguintes patologias” — cabeçalho solto da lista

> **Para a regulação confirmar:** "Ginecologia da adolescência (a partir de 11 anos)" era regra de idade e barrava menores de 11 com qualquer outra patologia — virou opção.

## CONSULTA EM GINECOLOGIA - UROGINECOLOGIA

- **desliga** “Pacientes que apresentem condições de acordo com os” — caco de "Observar os critérios do prestador"

## CONSULTA EM GINECOLOGIA INFERTILIDADE

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes apresentando as seguintes condições* · fonte: CRECE p.30

- [ ] Casais com diagnóstico de infertilidade conjugal para investigação da etiologia ou para proposta de tratamentos com base em diagnósticos previamente realizados
- [ ] História de infertilidade (primária ou secundária) há mais de 1 ano se mulher com menos de 35 anos, ou 6 meses se mais de 35 anos

Substitui 2 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes apresentando as seguintes condições” — cabeçalho solto da lista

## CONSULTA EM GINECOLOGIA URODINAMICA

**Pergunta de lista** — “O paciente tem ao menos uma das indicações abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Indicações do Estudo Urodinâmico* · fonte: CRECE p.31

- [ ] Antes de qualquer tratamento cirúrgico para incontinência urinária de esforço (IUE)
- [ ] Insucesso em cirurgia prévia para IUE
- [ ] Insucesso no tratamento medicamentoso da incontinência Urinária

Substitui 3 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “do Estudo Urodinâmico” — cabeçalho solto ("do Estudo Urodinâmico")

> **Para a regulação confirmar:** "Sintomas urinários avaliados pelo ginecologista" e "exame de urina negativo" continuam separados — são requisitos que se somam.

## CONSULTA EM HEPATOLOGIA - HEPATITE CRONICA C

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.31

- [ ] Pacientes portadores de Hepatite C Crônica, sem cirrose hepática descompensada
- [ ] Hepatite C Aguda

Substitui 2 pergunta(s)/regra(s) soltas, que ficam inativas.

## CONSULTA EM NEFROLOGIA - GERAL

- **desliga** “Pacientes que apresentem condições de acordo com os” — caco de "Observar os critérios do prestador"
- **desliga** “Pacientes que apresentem condições de acordo com os” — caco de "Observar os critérios do prestador"

## CONSULTA EM NEUROLOGIA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Critérios de inclusão (basta um)* · fonte: CRECE p.32

- [ ] Doença de Parkinson (CID G20)
- [ ] Paralisia supranuclear progressiva
- [ ] Síndrome Corticobasal (SCB)
- [ ] Tremor essencial
- [ ] Distonias
- [ ] Mioclonia
- [ ] Doença de Huntington (DHQ)
- [ ] Epilepsia
- [ ] Esclerose múltipla
- [ ] Miastenia gravis.e outras miopatias
- [ ] Paralisia facial periférica
- [ ] Hidrocefalia
- [ ] Cisticercose
- [ ] Síndromes demenciais
- [ ] Coreias
- [ ] Ataxia
- [ ] Tique
- [ ] Cefaléia
- [ ] Desmaios

Substitui 15 pergunta(s)/regra(s) soltas, que ficam inativas.
- também desligada: `7b0d58bc` — duplicata ("Doença de Parkinson")
- **desliga** “Observar as condições apresentadas pelos pacientes de acordo com os” — caco de "Observar os critérios do prestador"
- **desliga** “Pacientes que apresentem condições de acordo com os” — caco de "Observar os critérios do prestador"
- **desliga** “Pacientes que apresentem problemas neurológicos caracterizados por movimentos involuntários que podem ocorrer ” — cabeçalho ("problemas neurológicos… tais como")
- **desliga** “Pacientes que apresentem síndromes clínicas com características próprias, tais como” — cabeçalho ("síndromes clínicas… tais como")
- **desliga** “Pacientes portadores de doenças benignas da pelve para tratamento cirúrgico” — regra da GINECOLOGIA (cirurgia de pelve) que caiu na Neurologia
- **desliga** “Pacientes portadores de doenças benignas do trato genital para tratamento cirúrgico” — regra da GINECOLOGIA (cirurgia do trato genital) que caiu na Neurologia

> **Para a regulação confirmar:** Duas seções do manual (Distúrbios do movimento e "Doença do neurônio motor", CRECE p.32) viraram uma lista. Coreias, Ataxia, Tique, Cefaléia e Desmaios estão no PDF e a extração perdeu — acrescentadas. As duas regras de cirurgia ginecológica que estavam aqui saem.

## CONSULTA EM NEUROLOGIA - ESCLEROSE MULTIPLA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes que apresentem suspeita de patologia desmielinizante, como* · fonte: CRECE p.34

- [ ] Esclerose múltipla
- [ ] Neuromielite Óptica (NMO)
- [ ] Encefalomielite aguda disseminada (ADEM)

Substitui 3 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes que apresentem suspeita de patologia desmielinizante, como” — cabeçalho solto da lista
- **desliga** “Pacientes que apresentem condições de acordo com os” — caco de "Observar os critérios do prestador"

## CONSULTA EM ODONTOLOGIA - CIRURGIA ORAL MENOR

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes que apresentam necessidade cirúrgica para tratamento das seguintes condições* · fonte: CRECE p.47

- [ ] Cirurgia de sisos
- [ ] Dentes inclusos, retidos ou impactados
- [ ] Dentes supranumerários
- [ ] Remoção de cistos e corpos estranhos
- [ ] Excisão de cálculo salivar
- [ ] Hiperplasias ou regularização de rebordo
- [ ] Frenectomias

Substitui 7 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes que apresentam necessidade cirúrgica para tratamento das seguintes condições” — cabeçalho solto da lista

## CONSULTA EM ODONTOLOGIA - ENDODONTIA

- **nova versão** de “Tratamentos endodônticos em 3ºs molares (sisos) serão realizados apenas nos casos em que o siso seja pilar par”: `{"tipo": 4, "pergunta": null, "resposta_bloqueia": null}` — regra condicional (só para siso) — como pergunta, o "Não" barrava todo tratamento de canal

## CONSULTA EM ODONTOLOGIA - ESTOMATOLOGIA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes que apresentam necessidade de diagnóstico e biópsia para as seguintes lesões na região maxilofacial* · fonte: CRECE p.48

- [ ] Lesões que não cicatrizam em 15 dias na cavidade oral
- [ ] Doenças da língua, lábio e da mucosa oral
- [ ] Leucoplasia e outras afecções do epitélio oral
- [ ] Líquen plano, Estomatites e lesões correlatas
- [ ] Hiperplasia irritativa da mucosa oral
- [ ] Cistos da região bucal
- [ ] Tumefação, massa ou tumoração não especificadas na cavidade oral
- [ ] Doença de glândulas salivares
- [ ] Xerostomia, queimação e ardência bucal
- [ ] Halitose

Substitui 10 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes que apresentam necessidade de diagnóstico e biópsia para as seguintes lesões na região maxilofacial” — cabeçalho solto da lista

## CONSULTA EM PNEUMOLOGIA - TUBERCULOSE COMPLICADA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes apresentando os seguintes casos* · fonte: CRECE p.41

- [ ] Tuberculose Extrapulmonar
- [ ] Tuberculose com intolerância ao esquema básico com indicação para tratamento alternativo
- [ ] Tuberculose com evolução desfavorável ao esquema básico
- [ ] Micobactéria Não-tuberculosa

Substitui 4 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes apresentando os seguintes casos” — cabeçalho solto da lista

## CONSULTA EM REUMATOLOGIA GERAL

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes que apresentem determinadas patologias, como* · fonte: CRECE p.42

- [ ] Doenças Autoimunes (Suspeita ou Confirmadas)
- [ ] Lupus Sistêmico
- [ ] Colagenoses
- [ ] Artrite Reumatoide
- [ ] Dermatomiosite
- [ ] Esclerodermia
- [ ] Síndrome de Sjogren
- [ ] Outra condição do mesmo grupo (descrever no encaminhamento)

Substitui 8 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes que apresentem determinadas patologias, como” — cabeçalho solto da lista

## CONSULTA EM UROLOGIA - LITIASE

- **nova versão** de “Pacientes que apresentem cálculo renal: observar os”: `{"descricao": "Pacientes que apresentem cálculo renal", "pergunta": "Pacientes que apresentem cálculo renal"}` — texto cortado no meio ("…: observar os") pela frase do prestador

## CONSULTA EM UROLOGIA DISFUNCAO MICCIONAL - PEDIATRIA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes que apresentem determinadas patologias, como* · fonte: CRECE p.45

- [ ] Bexiga neurogênica
- [ ] Quadro suspeito ou confirmado de enurese, incontinência urinária, infecção urinária de repetição, sem resposta aos tratamentos convencionais
- [ ] Outra condição do mesmo grupo (descrever no encaminhamento)

Substitui 3 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes que apresentem determinadas patologias, como” — cabeçalho solto da lista

## CONSULTA EM UROLOGIA GINECOLOGIA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes que apresentem determinadas patologias, como* · fonte: CRECE p.45

- [ ] Incontinência urinária
- [ ] Prolapso vaginal
- [ ] Prolapso genital
- [ ] Outra condição do mesmo grupo (descrever no encaminhamento)

Substitui 4 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes que apresentem determinadas patologias, como” — cabeçalho solto da lista

## CONSULTA EM UROLOGIA PEDIATRICA

**Pergunta de lista** — “O paciente tem ao menos uma das condições abaixo?” (barra só com “Nenhuma destas”)  
Texto do manual: *Pacientes que apresentem determinadas patologias, como* · fonte: CRECE p.44

- [ ] Anomalias congênitas urogenitais
- [ ] Patologias obstrutivas das vias urinárias
- [ ] Criptorquidia
- [ ] Enurese noturna
- [ ] Distúrbios de diferenciação sexual (genitália ambígua)
- [ ] Bexiga neurogênica
- [ ] Incontinência urinária
- [ ] Outra condição do mesmo grupo (descrever no encaminhamento)

Substitui 8 pergunta(s)/regra(s) soltas, que ficam inativas.
- **desliga** “Pacientes que apresentem determinadas patologias, como” — cabeçalho solto da lista

## Cateterismo Cardíaco (Internados)

- **nova versão** de “HT<27 e Hb<9,0 não poderão realizar o exame”: `{"pergunta": "O paciente tem HT < 27 e Hb < 9,0?", "resposta_bloqueia": 1}` — "HT<27 e Hb<9,0 não poderão realizar o exame" é exclusão: barrava quem respondia "Não"
- **nova versão** de “Caso o paciente apresente alterações sugestivas de Insuficiência Renal (U>80 ou Cr>2,0 é necessária avaliação ”: `{"tipo": 4, "pergunta": null, "resposta_bloqueia": null}` — orientação condicional (avaliação do nefrologista) — "Não" barrava quem não tem insuficiência renal

## Conferência automática contra o PDF

Todas as opções têm texto correspondente no PDF.

## Procedimentos fora do plano que ainda somam perguntas de inclusão

Nenhum.

## Achado à parte: a origem das regras aponta para o recurso errado

Em quase todas as regras, `procedimento_origem_id` aponta para uma origem de OUTRO procedimento (ex.:
as regras da Genética Pediátrica apontam para “Cardiologia - Hipertensão Arterial Resistente”). O
`procedimento_id` está certo, e é só ele que o avaliador usa — então o veredito não é afetado. Mas a
coluna está suja e não deve ser usada para nada até ser recalculada.
