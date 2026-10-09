# Confronto CRECE (Manual do Solicitante, v1 30/11/2022) pp. 7–23 × regras de elegibilidade em produção — parte A

Gerado em 09/10/2026. Trabalho só de leitura. Fontes: PDF do CRECE, conferido visualmente página a página (11–23) além do texto extraído; exportação de produção de 09/10 (`regras.json`, `ser_recursos.json`, `analise_espelho_por_procedimento.json`); `spike-e-manual-regras.csv`; `conversao-listas/REVISAO.md` (01/10). Para descobrir que canônico um pedido do SER alcança, reproduzi o `AnaliseRegrasEspelhoService.ContextoAsync`: origem ativa do sistema SER, casada pelo rótulo normalizado, com a origem confirmada ganhando.

**Trecho:** regras gerais 1–4 (pp. 7–10) e 4.1.1 Enfermagem a 4.1.10 Fisioterapia (pp. 11–23). São **39 linhas de recurso** (a Fisioterapia foi partida em dois ramos) e **4 linhas gerais**. A p.24 não foi necessária: a tabela da Fisioterapia termina na p.23.

## Resumo

Vereditos das 39 linhas de recurso: **COBERTO** 10, **PARCIAL** 5, **AUSENTE** 11, **ERRADO** 5, **NAO_REPRESENTAVEL_HOJE** 0, **SEM_RECURSO_NO_SER** 8.
Linhas gerais: G1 NAO_REPRESENTAVEL_HOJE, G2 PARCIAL, G3 COBERTO, G4 PARCIAL.

Na prática, um pedido do SER que chega para um recurso deste trecho cai em uma de três situações:

- **Regras certas e alcançáveis** (10 COBERTO + 5 PARCIAL): Coloproctologia, Pequenos Procedimentos, Ovário, Hipófise/Adrenal, DM1, Pediatria Diabetes, Síndrome Metabólica, Osteometabólicas, Tireoide, Pós-bariátrica; com lacunas: Insuficiência Cardíaca, Psoríase, Adolescente Diabetes, Gestante, Fisiatria.
- **«Sem regras»** (11 AUSENTE + 3 ERRADO por canônico errado): Alergologia, Cardiologia, Biópsia de Pele, Dermato Pediatria, as 4 de Endócrino (Diabetes, Gestacional, Dislipidemia, Obesidade), Endócrino Pediatria, Fisioterapia Uroginecológica, Atendimento Multiprofissional; mais Angiologia, Cardio Pediatria e Clínica da Dor, cujas regras existem mas num gêmeo que o SER não alcança.
- **Regra ativa que barra quem o manual inclui** (2 ERRADO): Alergologia Pediatria (exclusões × inclusões, contradição do próprio manual) e Dermatologia geral (exclusões da Hanseníase caídas no recurso geral).

### Os 5 problemas mais graves

1. **Dermatologia geral — exclusão da Hanseníase aplicada a toda a Dermatologia (regra ativa errada).** «Pacientes com diagnóstico confirmado e acompanhamentos regulares de casos simples» (Sim barra) é exclusão da linha HANSENÍASE COMPLICADA (p.17) e está ativa no canônico `CONSULTA EM DERMATOLOGIA` [8143-7932]. Barra psoríase, vitiligo ou acne crônica estável. As outras duas exclusões da hanseníase também estão lá, e a lista ganhou 6 opções de Hanseníase e Hemangiomas. A REVISAO de 01/10 manteve essas exclusões sem notar a origem.
2. **Alergologia Pediatria — 5 exclusões ativas barram opções da própria lista de inclusão** (lactente sibilante, dermatite de contato extensa, alergia alimentar sistêmica grave, imunodeficiência primária, reação a medicamentos). A contradição está no manual; a REVISAO de 01/10 marcou «precisa de decisão da regulação» e nada mudou. É o recurso do trecho com mais tráfego no espelho: 34 pedidos «A conferir».
3. **Regras CRECE em gêmeos que o SER não alcança.** Angiologia [8136-77ed], Cardiologia Pediatria [8130-7f43] e Clínica da Dor [8146-7a42] têm as regras certas, mas no canônico cuja única origem ativa é SISREG, e as regras têm `sistema=SER`. O pedido do SER resolve para outro canônico, com origem confirmada por pessoa e 0 regras. São 12 regras ativas mortas neste trecho. Na base inteira, 27 de 232 regras ativas estão em canônico sem origem SER ativa (também Cirurgia Pediátrica, Gastroenterologia, Mastologia, Nefrologia-Geral, Odonto Endodontia e Estomatologia, Otorrino).
4. **Linhas inteiras nunca importadas**, com recurso ativo e pedidos chegando: Alergologia (13 pedidos «Sem regras»), Dermatologia Pediatria (19), Cardiologia adulto, Endócrino Pediatria (2), Obesidade, Diabetes/DM2, Diabete Gestacional, Dislipidemia, Fisioterapia Uroginecológica (1), Biópsia de Pele (5). Causas: a extração do spike-e perdeu linhas (Alergologia adulto, Cardiologia adulto, Dermato e Endócrino Pediatria, Obesidade adulto) e o pareamento por nome falhou («DIABETE» × «DIABETES», «DISLEPIDEMIA», «FISIOTERAPIA» × «…UROGINECOLÓGICA»). Nenhum desses recursos tem nem o encaminhamento global.
5. **Fisiatria — idade «acima de 60» virou opção da lista**, quando pela estrutura do manual é cumulativa. Na Síndrome Metabólica a mesma revisão leu a mesma estrutura ao contrário. A pendência «Para a regulação confirmar» de 01/10 continua aberta. Há uma saída que cabe no modelo de hoje: Dedutível idade mínima 61 como **Ressalva**.

### Recursos CONDICIONAIS / não representáveis hoje

Nenhum recurso do trecho fica **inteiro** sem representação. As partes condicionais são:

- **Alergologia Pediatria**: faixa etária por indicação (rinite e asma 2–12; lactente sibilante ≤ 2). Só uma regra condicional confere isso; hoje fica no texto da opção.
- **Cardiologia (adulto)**: «Risco cirúrgico — apenas maiores de 60 anos» é incluído e excluído ao mesmo tempo. Contorno: pergunta composta «risco cirúrgico com ≤ 60 anos?» (Sim barra). A dedução automática exigiria condicional.
- **Cardiologia Pediatria**: a OBS do esporte (só com suspeita de cardiopatia congênita, arritmia ou morte súbita familiar). Contorno na redação da pergunta.
- **Lesões de Pele (Enfermagem)**: úlcera venosa só sem cura em 12 semanas ou sem redução de 70% em 18. Contorno no texto da opção. Esta linha não tem recurso no SER.
- **Dislipidemia**: limiares de criança × adulto. Contorno no texto da opção.
- **Hanseníase dentro da Dermatologia geral**: as exclusões só valem se a indicação for hanseníase. Contorno na redação.
- **Fisioterapia**: sexo masculino adulto só no ramo uroginecológico. Como o SER tem recurso próprio desse ramo, deixa de ser condicional.
- **G1 (dados cadastrais)**: só o CPF é dedutível; CNS, telefone, endereço e nome da mãe não são. E não existe regra global.

### Achados transversais

- **Severidade**: todas as 232 regras ativas da base são *Bloqueia*. Ressalva e Aviso nunca foram usadas, mas resolvem bem os casos ambíguos deste trecho (Fisiatria, Fisioterapia ♂, exclusões contraditórias da Alergo Pediatria).
- **Encaminhamento «global»**: existe em 72 canônicos. Dos 214 recursos AE, 171 não têm nenhuma regra ativa.
- **Gêmeos**: o `reparar_catalogo_posicional.py` (01/10) não mexe em origem *confirmada*, e o `desativar_gemeos_catalogo.py` não desativa canônico *com regra*. Por isso o par «canônico com regras + origem SISREG» × «canônico confirmado para o SER, sem regras» sobreviveu. Vale uma consulta periódica: «canônico com regra `sistema=2` e sem origem SER ativa».
- **`fonte` imprecisa**: as exclusões e as opções o14–o23 da Alergologia Pediatria citam «CRECE p.12», mas estão na p.13.
- **Textos com caco do PDF**: em Hipófise/Adrenal, o marcador «o» vazou para o texto («história de o prolactina elevada»). Na Pediatria Diabetes, «≥ 99» virou «> 99».

### Regras com fonte CRECE p.7–p.23 fora do lugar

| Canônico | Regras | Problema |
|---|---|---|
| CONSULTA EM DERMATOLOGIA [01a07a08-8143-7932-8484-1ad2ae2f587b] | 3 perguntas de exclusão ativas + 6 opções da lista (fonte p.17) | São da HANSENÍASE COMPLICADA e da PEDIATRIA HEMANGIOMAS, linhas sem recurso no SER. As exclusões barram a Dermatologia geral. |
| CONSULTA EM DERMATOLOGIA [8143-7932] | 4 inativas (Eletrocoagulação, Exérese, Shaving, Fulguração; p.16) | São da BIÓPSIA DE PELE. Foram desligadas em 01/10 e nunca recriadas no canônico da Biópsia [813a-7d63]. |
| CONSULTA EM ANGIOLOGIA [01a07a08-8136-77ed-9714-a25ef0efa68b] | 3 ativas + 10 inativas (p.13) | Gêmeo com origem só SISREG; o SER resolve para [01a07a08-8120-743a-beea-5c43413e57fd]. Inalcançáveis. |
| CONSULTA EM CARDIOLOGIA - PEDIATRIA [01a07a08-8130-7f43-bfe1-b45344ff32f9] | 6 ativas + 7 inativas (p.14) | Gêmeo (origem SER AE inativa, SISREG ativa); o SER resolve para [01a07a08-811d-7a75-aa91-233afb5ce602]. Inalcançáveis. |
| CONSULTA EM CLINICA MEDICA - CLINICA DE DOR [01a07a08-8146-7a42-8224-274ed0c1f136] | 3 ativas (p.15) | Gêmeo com nomes cruzados («DE DOR»/«DA DOR»); o SER resolve para [01a07a08-8125-7bb0-8d8b-7165e44c387c]. Inalcançáveis. |

Nenhuma regra com fonte p.7–p.10: as regras gerais só aparecem como o encaminhamento «CRECE/REUNI — requisito global». Não achei regra do trecho em canônico de outra especialidade, nem com origem NAO_AE (não há vazamento CRECE → REUNI neste trecho).

### O que ficou pendente da REVISAO de 01/10 (deste trecho)

- Alergologia Pediatria: as exclusões que contradizem a inclusão. Aberto, sem decisão.
- Fisiatria: idade «acima de 60» como opção ou como regra cumulativa. Aberto.
- Dermatologia: «as exclusões (alta terapêutica, hanseníase com boa evolução, casos simples) continuam». A revisão não percebeu que são da linha Hanseníase; virou o problema 1.
- Dermatologia → Biópsia de Pele: os 4 itens «saem daqui» e nunca foram cadastrados no destino. Aberto.
- Tireoide: «Investigação de disfunção tireoidiana» tratada como cabeçalho. É defensável, mas fica para a regulação confirmar.
- Síndrome Metabólica: «idade continua regra separada». Feito, mas inconsistente com a Fisiatria.

## Tabela-resumo

| # | Seção | Pág. | Recurso no manual | Recurso no SER (AE) | Estrutura | Ativas alcançáveis | Veredito |
|---|---|---|---|---|---|---|---|
| G1 | 1. Competências do solicitante (1.1–1.6) | 7 | Regras gerais — dados cadastrais e acompanhamento | (todos os recursos AE) | SIMPLES + SO_ORIENTACAO | — | **NAO_REPRESENTAVEL_HOJE** |
| G2 | 2. Orientações para solicitação (2.1–2.6) | 8 | Regras gerais — informações e anexos | (todos os recursos AE) | SO_ORIENTACAO (+ documento genérico) | — | **PARCIAL** |
| G3 | 3. Situação no sistema (3.1–3.8) | 9 | Situações do pedido no módulo ambulatorial | — | SO_ORIENTACAO | — | **COBERTO** |
| G4 | 4. Recursos CRECE — instruções | 10 | Dados da solicitação na Prancheta do SER | (bloco fixo do formulário do SER) | SIMPLES (campos do formulário) | — | **PARCIAL** |
| R1 | 4.1.1 Consulta de Enfermagem | 11 | ATENDIMENTO MULTIPROFISSIONAL – OBESIDADE GRAVE | ATENDIMENTO MULTIPROFISSIONAL OBESIDADE GRAVE | SO_ORIENTACAO (+ encaminhamento) | 0 | **AUSENTE** |
| R2 | 4.1.1 Consulta de Enfermagem | 11 | CONSULTA DE ENFERMAGEM NA ATENÇÃO ESPECIALIZADA – PÉ DIABÉTICO | não achado | INCLUSAO_EXCLUSAO (inclusão = LISTA_BASTA_UM) | — | **SEM_RECURSO_NO_SER** |
| R3 | 4.1.1 Consulta de Enfermagem | 11 | CONSULTA EM ENFERMAGEM NA ATENÇÃO ESPECIALIZADA PARA AVALIAÇÃO DE LESÕES DE PELE | não achado | CONDICIONAL | — | **SEM_RECURSO_NO_SER** |
| R4 | 4.1.2 Alergologia | 12 | ALERGOLOGIA | CONSULTA EM ALERGOLOGIA | LISTA_BASTA_UM + exclusão | 0 | **AUSENTE** |
| R5 | 4.1.2 Alergologia | 12–13 | ALERGOLOGIA PEDIATRIA | CONSULTA EM ALERGOLOGIA - PEDIATRIA | LISTA_BASTA_UM + INCLUSAO_EXCLUSAO contraditória + CONDICIONAL | 8 | **ERRADO** |
| R6 | 4.1.3 Angiologia | 13 | ANGIOLOGIA | CONSULTA EM ANGIOLOGIA | LISTA_BASTA_UM + exclusão | 0 (3 no gêmeo) | **ERRADO** |
| R7 | 4.1.4 Cardiologia | 14 | CARDIOLOGIA (A partir de 15 anos) | CONSULTA EM CARDIOLOGIA | INCLUSAO_EXCLUSAO contraditória + LISTA_BASTA_UM + CONDICIONAL | 0 | **AUSENTE** |
| R8 | 4.1.4 Cardiologia | 14–15 | CARDIOLOGIA PEDIATRIA | CONSULTA EM CARDIOLOGIA - PEDIATRIA | LISTA_BASTA_UM + exclusões + documentos | 0 (6 no gêmeo) | **ERRADO** |
| R9 | 4.1.4 Cardiologia | 15 | INSUFICIÊNCIA CARDÍACA | CONSULTA EM CARDIOLOGIA - INSUFICIENCIA CARDIACA | SIMPLES + exclusões | 4 | **PARCIAL** |
| R10 | 4.1.5 Clínica Médica | 15 | CLÍNICA DA DOR | CONSULTA EM CLINICA MEDICA - CLINICA DE DOR | SIMPLES + exclusão | 0 (3 no gêmeo) | **ERRADO** |
| R11 | 4.1.5 Clínica Médica | 15 | DOENÇAS RARAS | não achado | SIMPLES | 0 (NAO_AE) | **SEM_RECURSO_NO_SER** |
| R12 | 4.1.6 Coloproctologia | 16 | COLOPROCTOLOGIA | CONSULTA EM COLOPROCTOLOGIA | SIMPLES (enumeração = basta uma) + exclusões | 5 | **COBERTO** |
| R13 | 4.1.7 Dermatologia | 16 | GERAL (Dermatologia) | CONSULTA EM DERMATOLOGIA | LISTA_BASTA_UM | 5 | **ERRADO** |
| R14 | 4.1.7 Dermatologia | 16 | BIÓPSIA DE PELE | CONSULTA EM DERMATOLOGIA - BIOPSIA DE PELE | LISTA_BASTA_UM | 0 | **AUSENTE** |
| R15 | 4.1.7 Dermatologia | 17 | HANSENÍASE COMPLICADA | não achado | INCLUSAO_EXCLUSAO (inclusão = LISTA_BASTA_UM) | — (no geral) | **SEM_RECURSO_NO_SER** |
| R16 | 4.1.7 Dermatologia | 17 | PEDIATRIA HEMANGIOMAS | não achado | LISTA_BASTA_UM | — (no geral) | **SEM_RECURSO_NO_SER** |
| R17 | 4.1.7 Dermatologia | 17 | PEDIATRIA (Dermatologia) | CONSULTA EM DERMATOLOGIA - PEDIATRIA | LISTA_BASTA_UM | 0 | **AUSENTE** |
| R18 | 4.1.7 Dermatologia | 17 | PEQUENOS PROCEDIMENTOS | CONSULTA EM DERMATOLOGIA - PEQUENOS PROCEDIMENTOS | LISTA_BASTA_UM | 2 | **COBERTO** |
| R19 | 4.1.7 Dermatologia | 18 | PSORÍASE | CONSULTA EM DERMATOLOGIA - PSORIASE | SIMPLES + documento + exclusão | 3 | **PARCIAL** |
| R20 | 4.1.8 Endocrinologia | 18 | ADOLESCENTE DIABETES | CONSULTA EM ENDOCRINOLOGIA - ADOLESCENTE - DIABETES | SIMPLES (idade + diagnóstico) | 2 | **PARCIAL** |
| R21 | 4.1.8 Endocrinologia | 18 | ADOLESCENTE (endocrinopatias) | não achado | SIMPLES (idade + diagnóstico; lista de exemplos aberta) | — | **SEM_RECURSO_NO_SER** |
| R22 | 4.1.8 Endocrinologia | 18 | DIABETES GESTACIONAL | CONSULTA EM ENDOCRINOLOGIA - DIABETE GESTACIONAL | SO_ORIENTACAO (+ encaminhamento) | 0 | **AUSENTE** |
| R23 | 4.1.8 Endocrinologia | 18 | DIABETES TIPO 2 | provável CONSULTA EM ENDOCRINOLOGIA - DIABETES | SIMPLES (critério composto cumulativo) | 0 | **AUSENTE** |
| R24 | 4.1.8 Endocrinologia | 19 | DISLEPIDEMIA | CONSULTA EM ENDOCRINOLOGIA - DISLIPIDEMIA | LISTA_BASTA_UM + CONDICIONAL leve | 0 | **AUSENTE** |
| R25 | 4.1.8 Endocrinologia | 19 | DOENÇAS DO OVÁRIO | CONSULTA EM ENDOCRINOLOGIA - DOENCAS DO OVARIO | LISTA_BASTA_UM | 2 | **COBERTO** |
| R26 | 4.1.8 Endocrinologia | 19 | GESTANTE (EXCETO DIABETES) | CONSULTA EM ENDOCRINOLOGIA - GESTANTE (EXCETO DIABETES) | SIMPLES | 2 | **PARCIAL** |
| R27 | 4.1.8 Endocrinologia | 19–20 | HIPÓFISE / ADRENAL | CONSULTA EM ENDOCRINOLOGIA - HIPOFISE/ADRENAL | LISTA_BASTA_UM (duas sublistas fundidas) | 2 | **COBERTO** |
| R28 | 4.1.8 Endocrinologia | 20 | DIABETES MELLITUS TIPO I | CONSULTA EM ENDOCRINOLOGIA - DIABETES MELLITUS TIPO I | SIMPLES + exclusão | 3 | **COBERTO** |
| R29 | 4.1.8 Endocrinologia | 20 | OBESIDADE ADOLESCENTE | não achado | SIMPLES (critério composto: IMC E/OU comorbidades) | — | **SEM_RECURSO_NO_SER** |
| R30 | 4.1.8 Endocrinologia | 20 | OBESIDADE | CONSULTA EM ENDOCRINOLOGIA - OBESIDADE | LISTA_BASTA_UM | 0 | **AUSENTE** |
| R31 | 4.1.8 Endocrinologia | 21 | PEDIATRIA DIABETES | CONSULTA EM ENDOCRINOLOGIA - PEDIATRICA DIABETES | LISTA_BASTA_UM | 2 | **COBERTO** |
| R32 | 4.1.8 Endocrinologia | 21 | SÍNDROME METABÓLICA | CONSULTA EM ENDOCRINOLOGIA - SINDROME METABOLICA | LISTA_BASTA_UM + idade (leitura E/OU ambígua) | 3 | **COBERTO** |
| R33 | 4.1.8 Endocrinologia | 21 | DOENÇAS OSTEOMETABÓLICAS | CONSULTA EM ENDOCRINOLOGIA - DOENCAS OSTEOMETABOLICAS | LISTA_BASTA_UM | 2 | **COBERTO** |
| R34 | 4.1.8 Endocrinologia | 21–22 | PEDIATRIA (Endocrinologia) | CONSULTA EM ENDOCRINOLOGIA - PEDIATRICA | LISTA_BASTA_UM (aberta: «Outros») | 0 | **AUSENTE** |
| R35 | 4.1.8 Endocrinologia | 22 | TIREOIDE | CONSULTA EM ENDOCRINOLOGIA - TIREOIDE | LISTA_BASTA_UM + exclusões | 6 | **COBERTO** |
| R36 | 4.1.8 Endocrinologia | 22 | PÓS-BARIÁTRICA | CONSULTA ENDOCRINOLOGIA PÓS BARIÁTRICA | SIMPLES | 2 | **COBERTO** |
| R37 | 4.1.9 Fisiatria | 23 | FISIATRIA | CONSULTA EM FISIATRIA | LISTA_BASTA_UM + idade (E/OU ambíguo) + exclusões | 4 | **PARCIAL** |
| R38 | 4.1.10 Fisioterapia | 23 | FISIOTERAPIA — ramo uroginecologia | CONSULTA EM FISIOTERAPIA UROGINECOLÓGICA | CONDICIONAL no manual | 0 | **AUSENTE** |
| R39 | 4.1.10 Fisioterapia | 23 | FISIOTERAPIA — reabilitação cardíaca/pulmonar/neurológica e respiratória | não achado | LISTA_BASTA_UM + exclusão | 0 (NAO_AE) | **SEM_RECURSO_NO_SER** |

«Ativas alcançáveis» = número de regras ativas, contando o encaminhamento global, no canônico que o pedido do SER alcança. «no gêmeo» = regras ativas que existem, mas num canônico que o pedido do SER não alcança.

Legenda dos vereditos: **COBERTO**, as regras alcançáveis representam o manual. **PARCIAL**, falta parte ou a leitura é discutível. **AUSENTE**, o recurso existe no SER e o canônico que o pedido alcança não tem regra do manual. **ERRADO**, há cadastro, mas ele barra quem o manual inclui ou está num canônico que o pedido do SER nunca alcança. **SEM_RECURSO_NO_SER**, a linha do manual não tem recurso no catálogo AE. **NAO_REPRESENTAVEL_HOJE**, o requisito não cabe no modelo atual, nem com contorno.

## Blocos por recurso

### G1 — Regras gerais — dados cadastrais e acompanhamento

1. **Seção / página / recurso no manual:** 1. Competências do solicitante (1.1–1.6) · p.7 · «Regras gerais — dados cadastrais e acompanhamento»
2. **Estrutura:** SIMPLES + SO_ORIENTACAO
3. **Requisitos:**
   - [valor/cadastro] 1.2 dados obrigatórios no SER: CNS, telefone de contato, endereço completo, data de nascimento, nome completo da mãe, CPF, «dentre outros»
   - [orientação] 1.3 conhecer os pré-requisitos dos recursos nos protocolos; 1.4 manter o cadastro atualizado
   - [orientação/prazo] 1.5 acompanhar a situação; pendências respondidas em até 60 dias
   - [orientação] 1.6 imprimir a chave de autorização e avisar o paciente/familiar
4. **Recurso no SER:** (todos os recursos AE)
5. **Nosso:** —
   - Ativas:
     - nenhuma: exige_cpf=true em 0 regras ativas na base inteira
6. **Veredito:** **NAO_REPRESENTAVEL_HOJE**
7. **Problemas:**
   - Do cadastro mínimo, só o CPF tem campo dedutível no modelo (exige_cpf); CNS, telefone, endereço e nome da mãe não são dedutíveis.
   - Não existe regra global: exigir CPF por regra obrigaria a replicar em ~214 canônicos com origem AE.
   - O prazo de 60 dias da pendência é operacional (módulo de pendências), não critério de elegibilidade.
8. **Proposta:** Validar o cadastro mínimo no envio (gate do formulário Externo), não em regra por procedimento. Se quiser via regras: Dedutível exige_cpf com severidade Ressalva (o ADR-0041 manda paciente sem CPF entrar marcado), replicada em todo canônico com origem SER AE. O prazo de 60 dias vira alerta no módulo de pendências (plano 06).

### G2 — Regras gerais — informações e anexos

1. **Seção / página / recurso no manual:** 2. Orientações para solicitação (2.1–2.6) · p.8 · «Regras gerais — informações e anexos»
2. **Estrutura:** SO_ORIENTACAO (+ documento genérico)
3. **Requisitos:**
   - [orientação] 2.1/2.2 informações pertinentes e o mais completas possível
   - [documento] 2.3 anexar exames e solicitações médicas, conforme protocolo
   - [orientação/prazo] 2.4 responder pendências em até 60 dias, senão o sistema cancela; 2.5 imprimir a chave; 2.6 informar o motivo antes de cancelar
4. **Recurso no SER:** (todos os recursos AE)
5. **Nosso:** 72 canônicos com a regra global do encaminhamento
   - Ativas:
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global) — em 72 canônicos; dos 214 recursos AE, só 42 resolvem para canônico que a tem; 171 recursos AE não têm nenhuma regra ativa
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - O «requisito global» só foi criado nos canônicos em que a importação de setembro achou alguma regra. No trecho, 13 recursos com origem SER AE não têm nem o encaminhamento (Atendimento Multiprofissional, Alergologia, Angiologia*, Cardiologia, Cardio Pediatria*, Clínica da Dor*, Biópsia de Pele, Dermato Pediatria, Diabete Gestacional, Diabetes, Dislipidemia, Obesidade, Endócrino Pediatria, Fisioterapia Uroginecológica — *têm no gêmeo inalcançável).
8. **Proposta:** Criar o Documento do encaminhamento em todo canônico que tem origem SER AE ativa (com a fonte do manual de cada linha). A regra global de verdade exigiria mudar o modelo.

### G3 — Situações do pedido no módulo ambulatorial

1. **Seção / página / recurso no manual:** 3. Situação no sistema (3.1–3.8) · p.9 · «Situações do pedido no módulo ambulatorial»
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos:**
   - [orientação] Em fila, Pendência, Agendado, Chegada confirmada, Chegada não confirmada (falta), Atendido, Alta, Cancelado
4. **Recurso no SER:** —
5. **Nosso:** —
   - Ativas: nenhuma
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Não se aplica: não é regra de elegibilidade. Só serve de referência para mapear os estados do espelho do SER («Chegada não confirmada» = falta).
8. **Proposta:** Nada a cadastrar como regra.

### G4 — Dados da solicitação na Prancheta do SER

1. **Seção / página / recurso no manual:** 4. Recursos CRECE — instruções · p.10 · «Dados da solicitação na Prancheta do SER»
2. **Estrutura:** SIMPLES (campos do formulário)
3. **Requisitos:**
   - [valor] dados sociais: nome, idade, CNS, CPF, sexo, nascimento, nome da mãe, endereço completo, telefone
   - [valor] médico solicitante cadastrado no SER
   - [valor] hipótese diagnóstica: classificação de risco e CID-10; natureza (mandado judicial sim/não); unidade de origem; anexos (caso necessário)
   - [orientação] a Central regula só o acesso; a fila interna da unidade executante é dela
4. **Recurso no SER:** (bloco fixo do formulário do SER)
5. **Nosso:** —
   - Ativas:
     - não é regra; o bloco fixo do formulário Externo (RegulacaoFormularioService) já tem Médico solicitante, Classificação de risco e Hipótese/CID
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - Não achei no bloco fixo do back o campo «mandado judicial (sim/não)». Não conferi o front.
8. **Proposta:** Manter no formulário, como campo obrigatório, e não em regra. Conferir se o mandado judicial é preenchido no envio ao SER.

### R1 — ATENDIMENTO MULTIPROFISSIONAL – OBESIDADE GRAVE

1. **Seção / página / recurso no manual:** 4.1.1 Consulta de Enfermagem · p.11 · «ATENDIMENTO MULTIPROFISSIONAL – OBESIDADE GRAVE»
2. **Estrutura:** SO_ORIENTACAO (+ encaminhamento)
3. **Requisitos:**
   - [documento] Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso
   - [orientação] Será realizada triagem para verificar se o paciente atende aos critérios mínimos para realização de exercícios e se apresentam problemas psiquiátricos que necessitem acompanhamento específico
   - [orientação] Todos são orientados a manterem seu acompanhamento médico habitual
   - [orientação] Observar critérios de inclusão e exclusão do prestador
4. **Recurso no SER:** ATENDIMENTO MULTIPROFISSIONAL OBESIDADE GRAVE (AE t1 v1079)
5. **Nosso:** ATENDIMENTO MULTIPROFISSIONAL OBESIDADE GRAVE [01a07a08-813b-7c24-bb44-251371f37487]
   - Ativas: nenhuma
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra, nem o encaminhamento.
   - O manual não traz critério de IMC. Não inventar.
8. **Proposta:** Documento/Bloqueia do encaminhamento (fonte CRECE p.11) + Informativa com o texto da triagem e do acompanhamento habitual. Nada condicional.

### R2 — CONSULTA DE ENFERMAGEM NA ATENÇÃO ESPECIALIZADA – PÉ DIABÉTICO

1. **Seção / página / recurso no manual:** 4.1.1 Consulta de Enfermagem · p.11 · «CONSULTA DE ENFERMAGEM NA ATENÇÃO ESPECIALIZADA – PÉ DIABÉTICO»
2. **Estrutura:** INCLUSAO_EXCLUSAO (inclusão = LISTA_BASTA_UM)
3. **Requisitos:**
   - [documento] encaminhamento médico com descrição clara e detalhada
   - [critério clínico — basta um] lesão do pé diabético, Classificação da Universidade do Texas, ESTÁGIO A (grau I, grau II e grau III) | pós-amputação com deiscência de sutura
   - [exclusão] lesão do pé diabético nos ESTÁGIOS B, C e D (Texas)
   - [orientação] critérios de exclusão e faixa etária do prestador
4. **Recurso no SER:** não achado no AE. Não há candidato equivalente; o NAO_AE «Ambulatório 1ª vez em Cirurgia Vascular - Pé diabético» (v1068) é cirurgia vascular (REUNI), não enfermagem
5. **Nosso:** —
   - Ativas: nenhuma
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Na importação ficou SEM_PAR. O catálogo AE atual (214 recursos) não tem consulta de enfermagem para pé diabético.
8. **Proposta:** Se o recurso aparecer: lista de 2 opções (Não barra), pergunta de exclusão «Lesão em estágio B, C ou D (Texas)?» (Sim barra) e encaminhamento.

### R3 — CONSULTA EM ENFERMAGEM NA ATENÇÃO ESPECIALIZADA PARA AVALIAÇÃO DE LESÕES DE PELE

1. **Seção / página / recurso no manual:** 4.1.1 Consulta de Enfermagem · p.11 · «CONSULTA EM ENFERMAGEM NA ATENÇÃO ESPECIALIZADA PARA AVALIAÇÃO DE LESÕES DE PELE»
2. **Estrutura:** CONDICIONAL (eixo: tipo de lesão — úlcera venosa) + INCLUSAO_EXCLUSAO
3. **Requisitos:**
   - [documento] encaminhamento médico com descrição clara e detalhada
   - [critério clínico — cumulativo] adesão adequada ao plano terapêutico E não respondem ao tratamento com as coberturas primárias da APS do RJ
   - [critério clínico — basta um] queimaduras grau I e II COM sedação para analgesia | lesão por pressão | feridas cirúrgicas | deiscência de suturas | feridas traumáticas e úlceras venosas
   - [condicional] úlcera venosa já acompanhada na APS: só quando não há 100% de cura em 12 semanas ou pelo menos redução de 70% em 18 semanas
   - [exclusão] queimaduras grau I e II SEM sedação; queimaduras grau III; lesão maligna; úlceras arteriais; lesões infectadas; dermatite atópica, impetigo, pênfigo, lesões psoriáticas e respostas alérgicas a tratamentos prévios
4. **Recurso no SER:** não achado no AE (nem candidato: «CONSULTA EM CIRURGIA PLASTICA - QUEIMADOS» é cirurgia)
5. **Nosso:** —
   - Ativas: nenhuma
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Na importação ficou SEM_PAR.
8. **Proposta:** Se o recurso aparecer: pergunta cumulativa (adesão + sem resposta às coberturas, Não barra). Lista basta-uma com 5 opções, com a condição da úlcera venosa escrita dentro da opção («Úlcera venosa sem 100% de cura em 12 semanas ou sem redução de 70% em 18 semanas»): resolve o condicional sem regra condicional. Exclusões numa lista «Algum destes se aplica?» com resposta_bloqueia=Sim. Encaminhamento.

### R4 — ALERGOLOGIA

1. **Seção / página / recurso no manual:** 4.1.2 Alergologia · p.12 · «ALERGOLOGIA»
2. **Estrutura:** LISTA_BASTA_UM + exclusão
3. **Requisitos:**
   - [documento] encaminhamento médico com descrição clara e detalhada
   - [critério clínico — basta um, 14 itens] Rinite alérgica de moderada a grave | Asma brônquica persistente não controlada ou parcialmente controlada ou asma controlada com altas doses de medicação inalatória ou com necessidade de esteroide sistêmico contínuo ou asma com histórico de crise com internação em CTI e/ou risco de morte | Conjuntivite alérgica com sintomas persistentes/moderada a grave | Urticária aguda recorrente sem identificação de agente causal ou urticária crônica (> 6 semanas) | Dermatite atópica moderada a grave ou não responsiva ao tratamento tópico habitual | Dermatite de contato para identificação do agente causal por testes de contato | Angioedema recorrente sem identificação de agente causal | Angioedema com deficiência quantitativa ou qualitativa do inibidor de c1 esterase | Anafilaxia alérgica ou pseudoalérgica para investigação diagnóstica | Reações a picadas de insetos (prurigo estrófulo) com evolução há mais de 6 meses | Reações locais ou sistêmicas à picada de formiga, marimbondo ou abelha | Reações alérgicas ou pseudoalérgicas a medicamentos (urticária - angioedema - vasculite cutânea - anafilaxia e ou outras reações cutâneas graves) | Alergia alimentar para investigação diagnóstica ou para acompanhamento após confirmada | Infecções respiratórias e cutâneas recorrentes e imunodeficiências primárias suspeitas ou confirmadas
   - [exclusão] Paciente portador de HIV/AIDS
4. **Recurso no SER:** CONSULTA EM ALERGOLOGIA (AE t1 v1169)
5. **Nosso:** CONSULTA EM ALERGOLOGIA [01a07a08-811e-709e-ba9e-de10c1ece35c] (origem confirmada)
   - Ativas: nenhuma
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - A importação de setembro não extraiu esta linha: o spike-e só tem «ALERGOLOGIA PEDIATRIA» na p.12.
   - 13 pedidos do espelho do SER estão como «Sem regras».
8. **Proposta:** Lista basta-uma com os 14 itens (Não barra). Pergunta «Paciente portador de HIV/AIDS?» (Sim barra); opcionalmente, Dedutível com CID excluídos B20–B24, que só confere o CID do pedido. Encaminhamento.

### R5 — ALERGOLOGIA PEDIATRIA

1. **Seção / página / recurso no manual:** 4.1.2 Alergologia · p.12–13 · «ALERGOLOGIA PEDIATRIA»
2. **Estrutura:** LISTA_BASTA_UM + INCLUSAO_EXCLUSAO contraditória + CONDICIONAL (eixo: indicação × faixa etária)
3. **Requisitos:**
   - [documento] encaminhamento médico com descrição clara e detalhada
   - [critério clínico — basta um, 23 itens] inclui 3 com faixa etária: «Rinite alérgica persistente de crianças entre 2 a 12 anos», «Asma brônquica persistente de crianças entre 2 a 12 anos», «Lactentes sibilante - até 2 anos de idade»
   - [exclusão] Lactente sibilante | Dermatite de contato extensa | Alergia alimentar com sintomas sistêmicos graves | Imunodeficiência primária (todas - incluindo angioedema hereditário) e secundárias | Investigação de Imunidade | Reação a medicamentos
4. **Recurso no SER:** CONSULTA EM ALERGOLOGIA - PEDIATRIA (AE t1 v1171)
5. **Nosso:** CONSULTA EM ALERGOLOGIA - PEDIATRIA [01a07a08-8135-78f3-a621-2b0009d3aa31]
   - Ativas:
     - Pergunta lista «Critérios de inclusão (basta um)» — 23 opções, Não barra (CRECE p.12)
     - 6 Perguntas Sim/Não, Sim barra: Lactente sibilante; Dermatite de contato extensa; Alergia alimentar com sintomas sistêmicos graves; Imunodeficiência primária (todas…) e secundárias; Investigação de Imunidade; Reação a medicamentos (fonte «CRECE p.12»)
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - 23 regras desligadas em 01/10: 20 perguntas soltas e 3 Dedutíveis de idade (2–12, 2–12, ≤2) — viraram a lista
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - 5 das 6 exclusões ativas barram opções da própria lista de inclusão: «Lactente sibilante» × «Lactentes sibilante - até 2 anos»; «Dermatite de contato extensa» × «…extensa e sem etiologia definida»; «Alergia alimentar com sintomas sistêmicos graves» × «…com reações sistêmicas graves»; «Imunodeficiência primária… e secundárias» e «Investigação de Imunidade» × «Imunodeficiência primária- suspeita…» e «História de imunodeficiência familiar»; «Reação a medicamentos» × «Reações adversas a drogas com sintomas sistêmicos graves». Quem marca essas inclusões é barrado. A contradição é do manual e foi apontada na REVISAO de 01/10 («Precisa de decisão da regulação»). Continua pendente.
   - As faixas etárias ficaram só no texto das opções. Nenhuma idade é conferida, nem um teto pediátrico.
   - A fonte das exclusões e das opções o14–o23 diz «CRECE p.12», mas o texto está na p.13.
   - 34 pedidos do espelho estão em «A conferir»: é o recurso do trecho com mais tráfego.
8. **Proposta:** Enquanto a regulação não decide, rebaixar as 5 exclusões conflitantes para Ressalva (o agente decide), via nova versão. Depois da decisão, desativar as exclusões ou as opções correspondentes. Idade por opção («se rinite/asma, então 2–12») exige regra condicional; hoje fica só no texto.

### R6 — ANGIOLOGIA

1. **Seção / página / recurso no manual:** 4.1.3 Angiologia · p.13 · «ANGIOLOGIA»
2. **Estrutura:** LISTA_BASTA_UM + exclusão
3. **Requisitos:**
   - [documento] encaminhamento médico com descrição clara e detalhada
   - [critério clínico — basta um, 10 itens] Doença arterial periférica | Claudicação intermitente | Ausência de pulsos arteriais | Varizes essenciais ou IVC (sem úlcera) | Linfedema | Elefantíase | Angiodisplasia | Síndrome do desfiladeiro cérvico-torácico | Artrite de Takayuasu | Doença de Behcet
   - [exclusão] Pacientes com feridas nas pernas
4. **Recurso no SER:** CONSULTA EM ANGIOLOGIA (AE t1 v1160)
5. **Nosso:** o pedido do SER resolve para CONSULTA EM ANGIOLOGIA [01a07a08-8120-743a-beea-5c43413e57fd] (origem SER AE confirmada), que tem 0 regras. As regras estão no gêmeo CONSULTA EM ANGIOLOGIA [01a07a08-8136-77ed-9714-a25ef0efa68b], cuja única origem ativa é do SISREG
   - Ativas:
     - (no gêmeo 8136-77ed) Pergunta lista 10 opções, Não barra (CRECE p.13)
     - (no gêmeo) Pergunta «Pacientes com feridas nas pernas», Sim barra
     - (no gêmeo) Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - (no gêmeo) 10 perguntas soltas desligadas em 01/10
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - O conteúdo está certo, mas no canônico errado. As regras têm sistema=SER, e o canônico delas só tem origem SISREG; o pedido do SER vai para o outro canônico (confirmado por pessoa). Resultado: as 3 regras nunca são avaliadas e o recurso fica «Sem regras».
   - O reparo posicional de 01/10 não mexe em origem confirmada, e o script de gêmeos só desativa canônico sem regra. Por isso o par ficou assim.
8. **Proposta:** Curadoria: recriar as 3 regras ativas (nova versão) no canônico 8120-743a e desativá-las no gêmeo, ou religar a origem SER AE ao 8136-77ed. O conteúdo não muda.

### R7 — CARDIOLOGIA (A partir de 15 anos)

1. **Seção / página / recurso no manual:** 4.1.4 Cardiologia · p.14 · «CARDIOLOGIA (A partir de 15 anos)»
2. **Estrutura:** INCLUSAO_EXCLUSAO contraditória + LISTA_BASTA_UM + CONDICIONAL (eixo: indicação «risco cirúrgico» × idade > 60)
3. **Requisitos:**
   - [idade] a partir de 15 anos (no nome do recurso)
   - [documento] encaminhamento médico; Ecocardiograma recente; exames de imagem com laudos que comprovem a doença (Tomografia ou Angiotomografia ou Angioressonância)
   - [critério clínico — basta um] Prótese valvar | Lesões oro-valvares congênita adulto | Hipertensão arterial resistente | Coronariopatias | Cardiomiopatias | Pós IAM | Arritmia | Insuficiência cardíaca | Risco cirúrgico (realizado pelo H.E. Eduardo Rabello, apenas para maiores de 60 anos)
   - [exclusão] Hipertensão com lesão de órgão-alvo (especificar) | Investigação de hipertensão secundária | Investigação de dor torácica | Doença orovalvar (diagnóstico e tratamento clínico) | Tratamento de insuficiência cardíaca | Risco cirúrgico
4. **Recurso no SER:** CONSULTA EM CARDIOLOGIA (AE t1 v1044)
5. **Nosso:** CONSULTA EM CARDIOLOGIA [01a07a08-8120-7cca-9857-6ffc3667c3ba] (confirmada); os gêmeos 8138-7302 e 8137-7a37 também estão sem regra
   - Ativas: nenhuma
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - A importação de setembro não extraiu esta linha: no spike-e, a p.14 só tem Cardiologia Pediatria.
   - O manual se contradiz: inclui «Insuficiência cardíaca» e exclui «Tratamento de insuficiência cardíaca»; inclui «Risco cirúrgico (> 60, H.E. Eduardo Rabello)» e exclui «Risco cirúrgico»; inclui «Prótese valvar/Lesões oro-valvares congênita» e exclui «Doença orovalvar (diagnóstico e tratamento clínico)».
   - Exigir tomografia ou angio de todo pedido é desproporcional (arritmia, HAS resistente).
8. **Proposta:** Dedutível idade mínima 15 (Bloqueia). Documento do eco (Bloqueia; «recente» sem prazo, a regulação define validade_dias). Documento de imagem com obrigatorio=false ou Ressalva. Lista de 9 opções (Não barra), com a opção «Risco cirúrgico — apenas maiores de 60 anos (H.E. Eduardo Rabello)». Exclusões não conflitantes numa lista com resposta_bloqueia=Sim (HAS com lesão de órgão-alvo, investigação de HAS secundária, investigação de dor torácica). Pergunta composta «Pedido de risco cirúrgico para paciente com 60 anos ou menos?» (Sim barra). «Tratamento de IC» e «Doença orovalvar» ficam para a regulação. Deduzir sozinho «risco cirúrgico ⇒ idade > 60» exigiria regra condicional.

### R8 — CARDIOLOGIA PEDIATRIA

1. **Seção / página / recurso no manual:** 4.1.4 Cardiologia · p.14–15 · «CARDIOLOGIA PEDIATRIA»
2. **Estrutura:** LISTA_BASTA_UM + exclusões + documentos («se houver») + CONDICIONAL na OBS (esporte só com suspeita)
3. **Requisitos:**
   - [critério clínico — basta um] Sopro cardíaco | Cianose | Arritmia (exame cardiovascular anormal e/ou ECG suspeito) | Insuficiência cardíaca | Síndromes genéticas com diagnóstico confirmado | Suspeita de doenças reumáticas | Síncope | Dor torácica na infância e na adolescência
   - [documento] encaminhamento; Ecocardiograma com laudo, SE HOUVER; todos os últimos exames (laboratorial, radiografia etc.)
   - [exclusão] Não marcar risco cirúrgico sem antecedentes de cardiopatias | Avaliação para academia/atividade física em criança saudável (OBS: só encaminhar com suspeita de cardiopatia congênita, arritmia ou história familiar de morte súbita)
4. **Recurso no SER:** CONSULTA EM CARDIOLOGIA - PEDIATRIA (AE t1 v1045)
5. **Nosso:** o pedido do SER resolve para [01a07a08-811d-7a75-aa91-233afb5ce602] (confirmada), com 0 regras. As regras estão no gêmeo [01a07a08-8130-7f43-bfe1-b45344ff32f9], que tem origem SER AE inativa e origem SISREG ativa
   - Ativas:
     - (no gêmeo 8130-7f43) Pergunta lista 7 opções, Não barra (CRECE p.14)
     - (no gêmeo) Documento/Bloqueia «Inserir Ecocardiograma com laudo, se houver», obrigatório
     - (no gêmeo) Documento/Bloqueia «Inserir todos os últimos exames realizados anteriormente…»
     - (no gêmeo) Pergunta «Não marcar Risco Cirúrgico sem antecedentes de cardiopatias», Sim barra
     - (no gêmeo) Pergunta «Avaliação cardiológica para academias… OBS…», Sim barra
     - (no gêmeo) Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - (no gêmeo) 7 perguntas soltas desligadas em 01/10
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - Inalcançável: as regras estão no canônico errado, como na Angiologia.
   - «Ecocardiograma com laudo, se houver» foi gravado como Documento obrigatório Bloqueia. Movido como está, travaria todo pedido sem eco.
   - As perguntas de exclusão estão escritas como ordem («Não marcar…»), e o «Sim» fica ambíguo.
   - A opção o2 junta «Cianose» e «arritmia», que são critérios distintos (no PDF a linha da arritmia está sem marcador). Não causa dano.
8. **Proposta:** Mover para o 811d-7a75. Eco com obrigatorio=false. Reescrever: «O pedido é de risco cirúrgico em paciente sem antecedente de cardiopatia?» e «O pedido é avaliação para academia/esporte de criança saudável, sem suspeita de cardiopatia congênita, arritmia ou história familiar de morte súbita?» (Sim barra). A OBS entra na própria pergunta, sem precisar de condicional.

### R9 — INSUFICIÊNCIA CARDÍACA

1. **Seção / página / recurso no manual:** 4.1.4 Cardiologia · p.15 · «INSUFICIÊNCIA CARDÍACA»
2. **Estrutura:** SIMPLES + exclusões
3. **Requisitos:**
   - [idade + critério clínico] Pacientes com mais de 18 anos com diagnóstico de IC e fração de ejeção no eco «< - 40%» (≤ 40%)
   - [documento] encaminhamento; Ecocardiograma realizado há menos de 6 meses
   - [exclusão] indicação de cirurgia de troca valvar ou revascularização miocárdica | sem exames que comprovem IC grave
4. **Recurso no SER:** CONSULTA EM CARDIOLOGIA - INSUFICIENCIA CARDIACA (AE t1 v1046)
5. **Nosso:** CONSULTA EM CARDIOLOGIA - INSUFICIENCIA CARDIACA [01a07a08-813a-76b2-beb8-bfe9c7664622]
   - Ativas:
     - Pergunta «Pacientes com mais de 18 anos com diagnóstico de IC, FE < - 40%», Não barra
     - Documento/Bloqueia «Inserir Ecocardiograma realizado há menos de 6 meses» (validade_dias nulo)
     - Pergunta «Pacientes com indicação de Cirurgia de Troca Valvar… e Pacientes sem exames que comprovem… IC Grave», Sim barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - Duas exclusões independentes viraram uma pergunta só, ligada por «e». Quem tem só uma delas pode responder «Não» e passar.
   - A idade está dentro da pergunta e não é deduzida.
   - O prazo de 6 meses do eco não foi cadastrado (validade_dias).
8. **Proposta:** Separar em 2 perguntas de exclusão, ou numa lista com resposta_bloqueia=Sim. Dedutível com idade mínima 19 («mais de 18»; 18 se a regulação ler «a partir de»). validade_dias=180 no Documento do eco.

### R10 — CLÍNICA DA DOR

1. **Seção / página / recurso no manual:** 4.1.5 Clínica Médica · p.15 · «CLÍNICA DA DOR»
2. **Estrutura:** SIMPLES + exclusão
3. **Requisitos:**
   - [critério clínico] dor crônica há mais de 06 meses
   - [documento] encaminhamento
   - [exclusão] pacientes oncológicos
4. **Recurso no SER:** CONSULTA EM CLINICA MEDICA - CLINICA DE DOR (AE t1 v1068)
5. **Nosso:** o pedido do SER resolve para «CONSULTA EM CLINICA MEDICA - CLINICA DA DOR» [01a07a08-8125-7bb0-8d8b-7165e44c387c] (confirmada), com 0 regras. As regras estão em «…CLINICA DE DOR» [01a07a08-8146-7a42-8224-274ed0c1f136]: origem SER AE inativa, origem SISREG «…CLINICA DA DOR» ativa (nomes cruzados)
   - Ativas:
     - (no gêmeo 8146-7a42) Pergunta «Pacientes com Dor Crônica há mais de 06 meses», Não barra
     - (no gêmeo) Pergunta «Pacientes oncológicos», Sim barra
     - (no gêmeo) Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - Inalcançável: canônico errado, com os nomes «DE DOR»/«DA DOR» cruzados entre SISREG e SER. O conteúdo está certo.
8. **Proposta:** Mover as 3 regras para o 8125-7bb0. Opcional: Dedutível com CID excluídos C00–C97 como Ressalva.

### R11 — DOENÇAS RARAS

1. **Seção / página / recurso no manual:** 4.1.5 Clínica Médica · p.15 · «DOENÇAS RARAS»
2. **Estrutura:** SIMPLES
3. **Requisitos:**
   - [critério clínico] acompanhamento clínico e investigação de Doenças Raras ou de difícil diagnóstico
   - [documento] encaminhamento
4. **Recurso no SER:** não achado no AE. Candidato NAO_AE (ramo REUNI): «Ambulatório 1ª vez - Clinica medica - Doenças Raras» (t1 v1071)
5. **Nosso:** Ambulatório 1ª vez - Clinica medica - Doenças Raras [01a07a08-8142-7301-ad8b-6930475f0ae1] — 0 regras
   - Ativas: nenhuma
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - O spike-e pareou com o NAO_AE («composto»), mas nada entrou em produção.
   - Regra cadastrada nesse canônico valeria também para o pedido REUNI.
8. **Proposta:** Se a regulação confirmar que o recurso é o NAO_AE: pergunta (Não barra) + encaminhamento no canônico, depois de conferir o REUNI.

### R12 — COLOPROCTOLOGIA

1. **Seção / página / recurso no manual:** 4.1.6 Coloproctologia · p.16 · «COLOPROCTOLOGIA»
2. **Estrutura:** SIMPLES (enumeração = basta uma) + exclusões
3. **Requisitos:**
   - [critério clínico] doenças anorretais benignas (hemorroidária, fissura anal, fístulas e abscessos, incontinência fecal)
   - [documento] encaminhamento
   - [exclusão] neoplasias anais e colorretais (benignas, malignas e de comportamento incerto) | colostomias e ileostomias (fechamento ou reconstrução de trânsito) | DII (Crohn e retocolite ulcerativa)
4. **Recurso no SER:** CONSULTA EM COLOPROCTOLOGIA (AE t1 v1097)
5. **Nosso:** CONSULTA EM COLOPROCTOLOGIA [01a07a08-813a-710c-8024-3ea67ea8d319]
   - Ativas:
     - Pergunta «Doenças anorretais benignas (…)», Não barra
     - 3 Perguntas de exclusão (neoplasias; colostomias/ileostomias; DII), Sim barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - A inclusão é uma pergunta só, que enumera as doenças. Como lista, mostraria ao regulador qual delas se aplica.
   - 13 pedidos do espelho em «A conferir».
8. **Proposta:** Opcional: virar lista de 4 opções. Opcional: CID excluídos C18–C21 e K50–K51 como Ressalva.

### R13 — GERAL (Dermatologia)

1. **Seção / página / recurso no manual:** 4.1.7 Dermatologia · p.16 · «GERAL (Dermatologia)»
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos:**
   - [critério clínico — basta um, 10 itens] dermatoses infecciosas | alérgicas | inflamatórias (psoríase, vitiligo, acne, hidradenite) | de causa imunológica e autoimunes | CBC, CEC e Melanoma | dermatoses pediátricas | DST | genodermatoses | tumores cutâneos benignos e malignos | queixas de imperfeições na pele
   - [documento] encaminhamento
   - [exclusão] só «observar o prestador»
4. **Recurso no SER:** CONSULTA EM DERMATOLOGIA (AE t1 v1071)
5. **Nosso:** CONSULTA EM DERMATOLOGIA [01a07a08-8143-7932-8484-1ad2ae2f587b]
   - Ativas:
     - Pergunta lista 16 opções, Não barra (fonte «CRECE p.16 · CRECE p.17»): as 10 da GERAL + 3 da HANSENÍASE COMPLICADA (o6, o7, o12) + 3 da PEDIATRIA HEMANGIOMAS (o8, o9, o10)
     - Pergunta «Pacientes com diagnóstico confirmado e acompanhamentos regulares de casos simples», Sim barra (é exclusão da HANSENÍASE)
     - Pergunta «Pacientes com boa evolução de hanseníase e/ou da reação hansênica», Sim barra
     - Pergunta «Pacientes apenas para simples avaliação de alta terapêutica», Sim barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - 20: 16 perguntas soltas (viraram a lista) + 4 itens da BIÓPSIA DE PELE desligados em 01/10
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - A importação leu a tabela da Dermatologia (Geral, Biópsia, Hanseníase, Hemangiomas) como se fosse de um recurso só. O spike-e chamou a linha de «DERMATOLOGIA - DOENÇAS RARAS».
   - Regra ativa errada: a exclusão da Hanseníase «diagnóstico confirmado e acompanhamentos regulares de casos simples» barra qualquer dermatose crônica estável mandada à Dermatologia geral (psoríase, vitiligo, acne em acompanhamento). As outras 2 exclusões não se aplicam fora da hanseníase e só confundem.
   - A REVISAO de 01/10 manteve essas exclusões («continuam») sem notar que são de outra linha.
   - As 6 opções a mais só deixam a lista mais permissiva, sem dano.
8. **Proposta:** Nova versão da lista só com as 10 opções da GERAL. Desativar as 3 exclusões de hanseníase. Se a regulação disser que Hanseníase/Hemangiomas entram por este recurso (não há recurso próprio no SER), manter as 6 opções e reescrever as exclusões com a condição no texto: «Paciente com hanseníase de caso simples em acompanhamento regular / com boa evolução / só para avaliação de alta?» (Sim barra). O condicional (só vale se a indicação for hanseníase) fica contornado no texto.

### R14 — BIÓPSIA DE PELE

1. **Seção / página / recurso no manual:** 4.1.7 Dermatologia · p.16 · «BIÓPSIA DE PELE»
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos:**
   - [critério/procedimento — basta um] Eletrocoagulação de lesão cutânea | Exérese de tumor de pele/cisto sebáceo/lipoma | Retirada de lesão por shaving | Fulguração/cauterização química de lesões cutâneas
   - [documento] encaminhamento
4. **Recurso no SER:** CONSULTA EM DERMATOLOGIA - BIOPSIA DE PELE (AE t1 v1070)
5. **Nosso:** CONSULTA EM DERMATOLOGIA - BIOPSIA DE PELE [01a07a08-813a-7d63-ad23-044b06ec73dd] (confirmada) — 0 regras
   - Ativas: nenhuma
   - Inativas:
     - os 4 itens estão INATIVOS no canônico da Dermatologia geral (desligados em 01/10: «saem daqui») e nunca foram recriados aqui
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Pendência aberta da REVISAO de 01/10: tirou os itens do geral sem cadastrar no destino.
   - 5 pedidos do espelho «Sem regras».
8. **Proposta:** Lista de 4 opções (Não barra) + encaminhamento, fonte CRECE p.16.

### R15 — HANSENÍASE COMPLICADA

1. **Seção / página / recurso no manual:** 4.1.7 Dermatologia · p.17 · «HANSENÍASE COMPLICADA»
2. **Estrutura:** INCLUSAO_EXCLUSAO (inclusão = LISTA_BASTA_UM)
3. **Requisitos:**
   - [critério clínico — basta um] casos suspeitos de difícil confirmação | em tratamento com quadros reacionais de difícil controle (reações adversas às drogas da PQT) | em alta terapêutica (PQT) com quadros reacionais pós-tratamento de difícil controle e suspeita de recidiva
   - [documento] encaminhamento
   - [exclusão] diagnóstico confirmado e acompanhamentos regulares de casos simples | boa evolução de hanseníase e/ou da reação hansênica | apenas para simples avaliação de alta terapêutica
4. **Recurso no SER:** não achado (nem AE nem NAO_AE). Candidato: CONSULTA EM DERMATOLOGIA (geral)
5. **Nosso:** itens espalhados no canônico da Dermatologia geral (ver GERAL)
   - Ativas:
     - (no canônico da Dermatologia geral) 3 opções da lista + 3 exclusões ativas
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Não há recurso próprio. Os itens foram parar na Dermatologia geral, onde as exclusões barram quem não devem.
8. **Proposta:** Ver a GERAL. Se for ficar no geral, as exclusões precisam de condicional («se hanseníase…»), contornável escrevendo a condição na pergunta.

### R16 — PEDIATRIA HEMANGIOMAS

1. **Seção / página / recurso no manual:** 4.1.7 Dermatologia · p.17 · «PEDIATRIA HEMANGIOMAS»
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos:**
   - [critério clínico — basta um] Hemangioma plano cavernoso e tuberoso | Malformações vasculares, arteriovenosas, capilares | Síndromes: Sturge-Weber, Kasabatt-Merritt, Nevus Azul, Osler-Rendu-Weber, Klippel-Trenanay e Mafucci
   - [documento] encaminhamento
4. **Recurso no SER:** não achado. Candidatos: CONSULTA EM DERMATOLOGIA - PEDIATRIA; CONSULTA EM DERMATOLOGIA
5. **Nosso:** as 3 opções estão na lista da Dermatologia geral
   - Ativas:
     - (no canônico da Dermatologia geral) 3 opções da lista
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem recurso próprio. As opções foram parar no geral, não no pediátrico.
8. **Proposta:** Se a regulação disser que entra pela Dermatologia - Pediatria, acrescentar as 3 opções à lista de lá.

### R17 — PEDIATRIA (Dermatologia)

1. **Seção / página / recurso no manual:** 4.1.7 Dermatologia · p.17 · «PEDIATRIA (Dermatologia)»
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos:**
   - [critério clínico — basta um] dermatoses infecciosas | alérgicas | inflamatórias | de causa imunológica ou autoimune | genodermatoses
   - [documento] encaminhamento
   - [exclusão] faixa etária e exclusões do prestador (o manual não dá idade)
4. **Recurso no SER:** CONSULTA EM DERMATOLOGIA - PEDIATRIA (AE t1 v1072)
5. **Nosso:** resolve para «CONSULTA EM DERMATOLOGIA - INFANTIL» [01a07a08-812d-7d4e-a3d2-8118e113df4a] (confirmada) — 0 regras; o gêmeo «…- PEDIATRIA» [01a07a08-8134-7cc9-b28d-8b7fcd93f0c9] também sem regra
   - Ativas: nenhuma
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Linha não extraída na importação.
   - 19 pedidos do espelho «Sem regras»: o recurso do trecho com mais pedidos descobertos.
8. **Proposta:** Lista de 5 opções (Não barra) + encaminhamento. Não inventar idade.

### R18 — PEQUENOS PROCEDIMENTOS

1. **Seção / página / recurso no manual:** 4.1.7 Dermatologia · p.17 · «PEQUENOS PROCEDIMENTOS»
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos:**
   - [critério — basta um] doenças dermatológicas para esclarecimento diagnóstico (inclusive com necessidade de biópsia) | procedimentos cirúrgicos como eletrocoagulação de pequenas lesões e cauterização química
   - [documento] encaminhamento
4. **Recurso no SER:** CONSULTA EM DERMATOLOGIA - PEQUENOS PROCEDIMENTOS (AE t1 v1074)
5. **Nosso:** CONSULTA EM DERMATOLOGIA - PEQUENOS PROCEDIMENTOS [01a07a08-813e-7c89-86ae-b6ca31bfad28]
   - Ativas:
     - Pergunta lista 2 opções, Não barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - 2 perguntas soltas (viraram a lista)
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - 6 pedidos do espelho em «A conferir».
8. **Proposta:** Nada.

### R19 — PSORÍASE

1. **Seção / página / recurso no manual:** 4.1.7 Dermatologia · p.18 · «PSORÍASE»
2. **Estrutura:** SIMPLES + documento + exclusão
3. **Requisitos:**
   - [critério clínico + documento] diagnóstico confirmado de psoríase E laudo histopatológico
   - [documento] encaminhamento
   - [exclusão] qualquer patologia que não seja psoríase
4. **Recurso no SER:** CONSULTA EM DERMATOLOGIA - PSORIASE (AE t1 v1073)
5. **Nosso:** CONSULTA EM DERMATOLOGIA - PSORIASE [01a07a08-8136-7ca1-adcb-d1049b5e9823]
   - Ativas:
     - Pergunta «…diagnóstico confirmado de Psoríase e com laudo histopatológico», Não barra
     - Pergunta «Qualquer patologia que não seja Psoríase», Sim barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - O laudo histopatológico é só perguntado; não há caixinha de anexo.
   - A exclusão é a negação da inclusão: redundante e com dupla negação confusa.
8. **Proposta:** Documento/Bloqueia «Laudo histopatológico confirmando psoríase». Desativar a exclusão redundante. Opcional: Dedutível com CID permitido L40, como Ressalva.

### R20 — ADOLESCENTE DIABETES

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.18 · «ADOLESCENTE DIABETES»
2. **Estrutura:** SIMPLES (idade + diagnóstico)
3. **Requisitos:**
   - [critério clínico] portador de Diabetes Mellitus
   - [idade] a partir de 12 anos até 17 anos, 11 meses e 29 dias
   - [documento] encaminhamento
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - ADOLESCENTE - DIABETES (AE t1 v1092)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - ADOLESCENTE - DIABETES [01a07a08-8130-7e73-91f6-e222959f7ea7]
   - Ativas:
     - Dedutível/Bloqueia idade 12–17 (descrição = frase inteira, incluindo «portadores de DM»)
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - A faixa 12–17 está correta (o avaliador usa anos completos, com limites inclusivos). Mas a Dedutível só confere a idade, e o parecer mostra como atendida a frase inteira, que inclui «portador de DM».
8. **Proposta:** Acrescentar Pergunta «Paciente portador de Diabetes Mellitus?» (Não barra) ou Dedutível com CID permitidos E10–E14 como Ressalva.

### R21 — ADOLESCENTE (endocrinopatias)

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.18 · «ADOLESCENTE (endocrinopatias)»
2. **Estrutura:** SIMPLES (idade + diagnóstico; lista de exemplos aberta)
3. **Requisitos:**
   - [documento] encaminhamento «e justificativa para a realização deste exame» (resto de copiar-e-colar: é consulta)
   - [critério clínico + idade] endocrinopatias já diagnosticadas ou em investigação, de 12 anos a 17a11m29d
   - [orientação] endocrinopatias atendidas por prestador: baixa estatura, atraso puberal, hirsutismo, síndromes genéticas com disfunções endócrinas, hipertireoidismo, entre outras
4. **Recurso no SER:** não achado no AE. Candidatos: CONSULTA EM ENDOCRINOLOGIA - ADOLESCENTE - DIABETES (só DM); CONSULTA EM ENDOCRINOLOGIA - PEDIATRICA; CONSULTA EM GINECOLOGIA ENDOCRINO / INFANTO PUBERAL
5. **Nosso:** —
   - Ativas: nenhuma
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Não foi extraída nem tem recurso no SER.
8. **Proposta:** Se o recurso aparecer: Dedutível idade 12–17 + Pergunta Sim/Não «endocrinopatia diagnosticada ou em investigação?». Os exemplos são lista aberta («entre outras»), então não viram lista fechada.

### R22 — DIABETES GESTACIONAL

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.18 · «DIABETES GESTACIONAL»
2. **Estrutura:** SO_ORIENTACAO (+ encaminhamento)
3. **Requisitos:**
   - [documento] encaminhamento
   - [orientação] critérios do prestador
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - DIABETE GESTACIONAL (AE t1 v1089)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - DIABETE GESTACIONAL [01a07a08-8146-736a-b9e0-5d8ad18607ee] — 0 regras
   - Ativas: nenhuma
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - No spike-e ficou SEM_PAR («DIABETES» × «DIABETE»). Não tem nem o encaminhamento.
8. **Proposta:** Encaminhamento. Opcional: Dedutível sexo F, que o recurso implica mas o manual não escreve; usar Ressalva.

### R23 — DIABETES TIPO 2

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.18 · «DIABETES TIPO 2»
2. **Estrutura:** SIMPLES (critério composto cumulativo)
3. **Requisitos:**
   - [documento] encaminhamento
   - [critério clínico — cumulativo] DM tipo 2 não controlada com 2 drogas orais + 1 dose de insulina basal ao dia
   - [exclusão implícita] sem acompanhamento de outro serviço especializado
4. **Recurso no SER:** provável «CONSULTA EM ENDOCRINOLOGIA - DIABETES» (AE t1 v1088). O rótulo não diz «tipo 2»; inferido por eliminação (DM1, gestacional, adolescente e pediátrica têm recurso próprio)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - DIABETES [01a07a08-813d-76d9-9f4c-741d72278c9f] — 0 regras
   - Ativas: nenhuma
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - No spike-e ficou SEM_PAR. O pareamento do recurso precisa de confirmação.
8. **Proposta:** Pergunta «DM2 não controlada com 2 antidiabéticos orais + 1 dose de insulina basal/dia?» (Não barra). Pergunta «Já acompanhado em outro serviço especializado?» (Sim barra). Encaminhamento.

### R24 — DISLEPIDEMIA

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.19 · «DISLEPIDEMIA»
2. **Estrutura:** LISTA_BASTA_UM + CONDICIONAL leve (eixo: idade — limiares de criança × adulto)
3. **Requisitos:**
   - [documento] encaminhamento
   - [valor — basta um] Hipercolesterolemia familiar com LDL-C > 200 mg/dl, TG > 500 mg/dl ou HDL-C < 20 mg/dl | Hipercolesterolemia familiar em crianças com LDL-C > 160 mg/dl ou TG > 200 mg/dl | risco alto ou muito alto sem atingir a meta de LDL-C (70 ou 50 mg/dl) com hipolipemiante instituído
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - DISLIPIDEMIA (AE t1 v1090)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - DISLIPIDEMIA [01a07a08-813c-71e3-8814-61968bf7a141] — 0 regras
   - Ativas: nenhuma
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - No spike-e ficou SEM_PAR («DISLEPIDEMIA», erro de grafia do manual).
8. **Proposta:** Lista de 3 opções (Não barra), com a condição de criança escrita na opção. Deduzir sozinho o limiar pela idade exigiria condicional. O lipidograma não é literal; no máximo, Documento opcional.

### R25 — DOENÇAS DO OVÁRIO

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.19 · «DOENÇAS DO OVÁRIO»
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos:**
   - [critério clínico — basta um] reposição hormonal pós-menopausa | infertilidade | SOP | hirsutismo | amenorreia primária ou secundária (já avaliada pela Ginecologia) | hipogonadismo
   - [documento] encaminhamento
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - DOENCAS DO OVARIO (AE t1 v1087)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - DOENCAS DO OVARIO [01a07a08-813a-76e9-904f-940de613eca0]
   - Ativas:
     - Pergunta lista 6 opções, Não barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - 5 perguntas soltas + Dedutível sexo=F (rotulada «Reposição hormonal feminina pós-menopausa») desligadas em 01/10
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Sexo F não é conferido. O manual não escreve isso, embora o recurso seja «ovário».
8. **Proposta:** Opcional: Dedutível sexo F como Ressalva.

### R26 — GESTANTE (EXCETO DIABETES)

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.19 · «GESTANTE (EXCETO DIABETES)»
2. **Estrutura:** SIMPLES
3. **Requisitos:**
   - [critério clínico] grávida portadora de doença endócrina, exceto diabetes
   - [documento] encaminhamento
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - GESTANTE (EXCETO DIABETES) (AE t1 v1093)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - GESTANTE (EXCETO DIABETES) [01a07a08-8146-7d20-87f8-512800ba7554]
   - Ativas:
     - Dedutível/Bloqueia sexo=F, com a frase inteira como descrição
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - A Dedutível só confere o sexo. Gravidez e «doença endócrina, exceto diabetes» não são perguntadas, e o parecer dá a frase inteira como atendida.
8. **Proposta:** Manter sexo F. Acrescentar Pergunta «Paciente gestante com doença endócrina (exceto diabetes)?» (Não barra). Opcional: CID excluídos O24 e E10–E14 como Ressalva.

### R27 — HIPÓFISE / ADRENAL

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.19–20 · «HIPÓFISE / ADRENAL»
2. **Estrutura:** LISTA_BASTA_UM (duas sublistas fundidas)
3. **Requisitos:**
   - [documento] encaminhamento
   - [critério clínico — basta um] Hipófise: 8 itens; Supra-renal: 7 itens (ver manual)
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - HIPOFISE/ADRENAL (AE t1 v1081)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - HIPOFISE/ADRENAL [01a07a08-813e-7c04-b3f9-897e32c55abe]
   - Ativas:
     - Pergunta lista 15 opções (Hipófise — … / Supra-renal — …), Não barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - 15 perguntas soltas + cabeçalho «SUPRA RENAL» + caco «SUPRA RENAL: Observar os»
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Cosmético: o marcador «o» solto do PDF vazou para o texto das opções («com história de o prolactina elevada», «(estrias violáceas, o hipertensão arterial…»).
8. **Proposta:** Nova versão só para limpar o texto. Opcional.

### R28 — DIABETES MELLITUS TIPO I

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.20 · «DIABETES MELLITUS TIPO I»
2. **Estrutura:** SIMPLES + exclusão
3. **Requisitos:**
   - [critério clínico] DM1 de difícil controle, SEM acompanhamento em outro serviço especializado
   - [documento] encaminhamento
   - [exclusão] acompanhamento em outro serviço especializado
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - DIABETES MELLITUS TIPO I (AE t1 v1085)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - DIABETES MELLITUS TIPO I [01a07a08-813b-74cf-802e-9430ca3e0d92]
   - Ativas:
     - Pergunta inclusão, Não barra
     - Pergunta «Paciente em acompanhamento em outro serviço especializado», Sim barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - A exclusão repete a inclusão. É redundante, mas coerente.
8. **Proposta:** Nada.

### R29 — OBESIDADE ADOLESCENTE

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.20 · «OBESIDADE ADOLESCENTE»
2. **Estrutura:** SIMPLES (critério composto: IMC E/OU comorbidades)
3. **Requisitos:**
   - [valor + critério clínico] obesidade grave com IMC > 35, com intolerância à glicose e/ou dislipidemia e/ou resistência insulínica
   - [documento] encaminhamento
   - [orientação] faixa etária do prestador
4. **Recurso no SER:** não achado no AE. Candidatos: CONSULTA EM ENDOCRINOLOGIA - OBESIDADE; CONSULTA EM ENDOCRINOLOGIA - PEDIATRICA
5. **Nosso:** —
   - Ativas: nenhuma
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem recurso no SER. No spike-e ficou SEM_PAR.
8. **Proposta:** Se o recurso aparecer: Pergunta «IMC > 35 com intolerância à glicose, dislipidemia ou resistência insulínica?» (Não barra). Não inventar idade.

### R30 — OBESIDADE

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.20 · «OBESIDADE»
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos:**
   - [valor — basta um] obesidade grau II (IMC entre 35 e 39,9) | grau III (IMC acima de 40)
   - [documento] encaminhamento
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - OBESIDADE (AE t1 v1080)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - OBESIDADE [01a07a08-8136-7ee7-8571-adc0bd8715ed] — 0 regras
   - Ativas: nenhuma
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Linha não extraída: o spike-e só tem «OBESIDADE ADOLESCENTE».
8. **Proposta:** Lista de 2 opções ou Pergunta «IMC ≥ 35?» (Não barra) + encaminhamento.

### R31 — PEDIATRIA DIABETES

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.21 · «PEDIATRIA DIABETES»
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos:**
   - [critério clínico — basta um] DM1 | DM pós-transplante | DM2 | glicemia de jejum alterada (≥ 99 mg/dl) | intolerância à glicose (TOTG alterado)
   - [documento] encaminhamento
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - PEDIATRICA DIABETES (AE t1 v1094)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - PEDIATRICA DIABETES [01a07a08-8138-773c-af92-d9e75e528b63]
   - Ativas:
     - Pergunta lista 5 opções, Não barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - 5 perguntas soltas
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Cosmético: o PDF diz «glicemia ≥ 99» e o texto em produção diz «> 99».
8. **Proposta:** Nada (ou corrigir o sinal).

### R32 — SÍNDROME METABÓLICA

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.21 · «SÍNDROME METABÓLICA»
2. **Estrutura:** LISTA_BASTA_UM + idade (leitura E/OU ambígua)
3. **Requisitos:**
   - [critério clínico] pacientes com distúrbios endócrinos
   - [critério clínico] HAS, diabetes, obesidade (IMC < 35) e colesterol elevado
   - [idade] a partir de 18 anos
   - [documento] encaminhamento
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - SINDROME METABOLICA (AE t1 v1086)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - SINDROME METABOLICA [01a07a08-813b-7194-8859-a8787007f812]
   - Ativas:
     - Dedutível/Bloqueia idade ≥ 18
     - Pergunta lista 2 opções (os dois primeiros tópicos), Não barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - 2 perguntas soltas
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Interpretação: os dois primeiros tópicos viraram alternativas, e a idade ficou separada (cumulativa). Na Fisiatria, a mesma estrutura de tópicos foi lida ao contrário (idade virou alternativa). As duas leituras são inconsistentes entre si. Aqui o erro possível é ser permissivo demais, o que é seguro.
8. **Proposta:** Manter. Registrar a regra de leitura (tópico «•» cumulativo, subitem «o» alternativo) e aplicá-la igual nos dois recursos.

### R33 — DOENÇAS OSTEOMETABÓLICAS

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.21 · «DOENÇAS OSTEOMETABÓLICAS»
2. **Estrutura:** LISTA_BASTA_UM
3. **Requisitos:**
   - [documento] encaminhamento
   - [critério clínico — basta um, 8 itens] hiperparatireoidismo | hipo/pseudo-hipoparatireoidismo | hipercalcemias (exceto doença renal ou malignidade) | nefrolitíase de repetição (bilateral) | osteoporose (densitometria com T-score «maior que -2,5», com ou sem fratura) | Paget | osteogênese imperfeita | raquitismo e osteomalácia
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - DOENCAS OSTEOMETABOLICAS (AE t1 v1091)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - DOENCAS OSTEOMETABOLICAS [01a07a08-8131-734f-8d5b-6d517babe7e2]
   - Ativas:
     - Pergunta lista 8 opções, Não barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - 8 perguntas soltas + cabeçalho «Pacientes apresentando»
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - A descrição da regra ficou «Pacientes apresentando» (o cabeçalho). O texto do T-score é literal do manual, embora clinicamente o certo seja ≤ -2,5.
8. **Proposta:** Opcional: Documento «Densitometria óssea» com obrigatorio=false.

### R34 — PEDIATRIA (Endocrinologia)

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.21–22 · «PEDIATRIA (Endocrinologia)»
2. **Estrutura:** LISTA_BASTA_UM (aberta: «Outros»)
3. **Requisitos:**
   - [documento] encaminhamento
   - [critério clínico — basta um, 18 itens] baixa estatura | alta estatura | distúrbio da diferenciação sexual (genitália ambígua) | distúrbios da puberdade | hipotireoidismo congênito | distúrbios tireoidianos (hipo/hiper, bócios) | diabetes insipidus | ginecomastia | hipo/hiperparatireoidismo | oncológicos com deficiências hormonais | hiperprolactinemia | síndromes genéticas com baixa estatura e/ou deficiências hormonais | obesidade com comorbidades | dislipidemia | hipoglicemia | hiperplasia congênita de suprarrenal | síndrome de Cushing | outros (observar o prestador)
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - PEDIATRICA (AE t1 v1083)
5. **Nosso:** resolve para «CONSULTA EM ENDOCRINOLOGIA - PEDIATRIA» [01a07a08-811c-73c2-9e26-0a0148a1193d] (confirmada) — 0 regras
   - Ativas: nenhuma
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Linha não extraída na importação.
   - 2 pedidos do espelho «Sem regras» + 1 solicitação nossa.
8. **Proposta:** Lista de 17 opções + «Outros (descrever no encaminhamento)» (Não barra) + encaminhamento. Com «Outros», a lista não barra ninguém: vale pelo registro da indicação.

### R35 — TIREOIDE

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.22 · «TIREOIDE»
2. **Estrutura:** LISTA_BASTA_UM + exclusões
3. **Requisitos:**
   - [documento] encaminhamento
   - [critério clínico — basta um] tireoidite aguda/subaguda confirmada | hipertireoidismo sem tratamento; bócio uni/multinodular atóxico com nódulos (hipoecoicos > 1 cm; iso/mistos/hiperecoicos ≥ 1,5 cm; císticos > 3 cm) | nódulos > 1 cm (levar exames)
   - [exclusão] investigação de disfunção tireoidiana (hiper e hipo): | hipotireoidismo | tireoidite de Hashimoto | nódulos < 1 cm | nódulos císticos ≤ 3 cm
4. **Recurso no SER:** CONSULTA EM ENDOCRINOLOGIA - TIREOIDE (AE t1 v1082)
5. **Nosso:** CONSULTA EM ENDOCRINOLOGIA - TIREOIDE [01a07a08-8136-77b1-95ae-9015d02110b7]
   - Ativas:
     - Pergunta lista 3 opções, Não barra
     - 4 Perguntas de exclusão (Hipotireoidismo; Hashimoto; nódulos < 1 cm; císticos < - 3 cm), Sim barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - 3 perguntas soltas + cabeçalho + exclusão «Investigação de disfunção tireoidiana (Hiper e Hipotireoidismo)» (tratada como cabeçalho em 01/10)
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Ler «Investigação de disfunção tireoidiana» como cabeçalho é defensável (termina em «:»), mas no PDF ela é um tópico como os outros. Como exclusão, contradiria «hipertireoidismo sem tratamento». Fica para a regulação.
   - Quem tem vários nódulos (um > 1 cm e outro < 1 cm) pode responder «Sim» à exclusão. É caso de borda.
8. **Proposta:** Opcional: separar a opção 2 (hipertireoidismo × bócio atóxico).

### R36 — PÓS-BARIÁTRICA

1. **Seção / página / recurso no manual:** 4.1.8 Endocrinologia · p.22 · «PÓS-BARIÁTRICA»
2. **Estrutura:** SIMPLES
3. **Requisitos:**
   - [critério clínico] já submetidos à cirurgia bariátrica (qualquer técnica e tempo), para acompanhamento clínico
4. **Recurso no SER:** CONSULTA ENDOCRINOLOGIA PÓS BARIÁTRICA (AE t1 v1047)
5. **Nosso:** CONSULTA ENDOCRINOLOGIA PÓS BARIÁTRICA [01a07a08-813b-7a1f-a436-a050b671042d]
   - Ativas:
     - Pergunta inclusão, Não barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global) — o manual não pede encaminhamento nesta linha (só a regra geral 2.3)
6. **Veredito:** **COBERTO**
7. **Problemas:** nenhum
8. **Proposta:** Nada.

### R37 — FISIATRIA

1. **Seção / página / recurso no manual:** 4.1.9 Fisiatria · p.23 · «FISIATRIA»
2. **Estrutura:** LISTA_BASTA_UM + idade (E/OU ambíguo) + exclusões
3. **Requisitos:**
   - [idade] pacientes acima de 60 anos
   - [critério clínico — basta um, 9 itens] artrose | cervicalgias | tendinopatias de Quervain | dedo em gatilho | ombro doloroso | esporão de calcâneo | gonalgia | paralisia facial periférica | lombalgia de origem vertebral
   - [documento] encaminhamento
   - [exclusão] neoplasias (benignas, malignas e de comportamento incerto) | infecções urinárias
4. **Recurso no SER:** CONSULTA EM FISIATRIA (AE t1 v1162)
5. **Nosso:** CONSULTA EM FISIATRIA [01a07a08-8146-7fac-a33c-3de841544f42]
   - Ativas:
     - Pergunta lista 10 opções, com «Pacientes acima de 60 anos» como OPÇÃO, Não barra
     - Pergunta «Neoplasias (…)», Sim barra
     - Pergunta «Infecções urinárias», Sim barra
     - Documento/Bloqueia «Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER.» (fonte CRECE/REUNI — requisito global)
   - Inativas:
     - 7 perguntas soltas + cabeçalho + Dedutível idade ≥ 60 (desligada em 01/10)
6. **Veredito:** **PARCIAL**
7. **Problemas:**
   - A idade virou alternativa: qualquer pessoa acima de 60 passa sem condição, e qualquer idade passa com uma das condições. Pela estrutura (tópico «•» cumulativo, subitem «o» alternativo), a leitura mais provável é «acima de 60 E uma das alterações». É a mesma leitura que a revisão aplicou na Síndrome Metabólica.
   - Ficou pendente na REVISAO de 01/10 («Para a regulação confirmar»).
   - «Acima de 60» é 61 ou mais em anos completos. A Dedutível antiga (mínimo 60, inclusivo) aceitava quem tem 60.
   - Indício (não conclusivo): o próprio CRECE na p.14 diz que o H.E. Eduardo Rabello faz risco cirúrgico «apenas para maiores de 60».
8. **Proposta:** Representável hoje sem esperar a regulação: nova versão da lista sem a opção de idade + Dedutível idade mínima 61 com severidade Ressalva (o agente decide). Se a regulação confirmar o «E», passa a Bloqueia.

### R38 — FISIOTERAPIA — ramo uroginecologia

1. **Seção / página / recurso no manual:** 4.1.10 Fisioterapia · p.23 · «FISIOTERAPIA — ramo uroginecologia»
2. **Estrutura:** CONDICIONAL no manual (eixo: indicação — só o ramo uroginecológico restringe sexo e idade); no recurso específico do SER vira SIMPLES
3. **Requisitos:**
   - [sexo + idade] «somente usuários adultos do sexo masculino»
   - [critério clínico — basta um] incontinência urinária de esforço | urgência miccional | incontinência mista | pós-operatório de próstata
   - [documento] encaminhamento
   - [exclusão] pacientes queimados ou amputados
4. **Recurso no SER:** CONSULTA EM FISIOTERAPIA UROGINECOLÓGICA (AE t1 v1156)
5. **Nosso:** CONSULTA EM FISIOTERAPIA UROGINECOLÓGICA [01a07a08-8135-70d6-b677-75ab53029a9d] — 0 regras
   - Ativas: nenhuma
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - No spike-e ficou SEM_PAR (o manual diz só «FISIOTERAPIA»).
   - 1 pedido do espelho «Sem regras».
   - «Uroginecológica» só para homens é contraintuitivo, mas está literal. Confirmar antes de usar Bloqueia.
8. **Proposta:** Dedutível sexo M + idade mínima 18 (Ressalva até a regulação confirmar, depois Bloqueia). Lista de 4 opções (Não barra). Pergunta «Paciente queimado ou amputado?» (Sim barra). Encaminhamento. Como o recurso do SER já é o ramo, não precisa de condicional.

### R39 — FISIOTERAPIA — reabilitação cardíaca/pulmonar/neurológica e respiratória

1. **Seção / página / recurso no manual:** 4.1.10 Fisioterapia · p.23 · «FISIOTERAPIA — reabilitação cardíaca/pulmonar/neurológica e respiratória»
2. **Estrutura:** LISTA_BASTA_UM + exclusão
3. **Requisitos:**
   - [critério clínico — basta um] reabilitação cardíaca, pulmonar e neurológica | fisioterapia respiratória (adulto e infantil)
   - [documento] encaminhamento
   - [exclusão] queimados ou amputados
4. **Recurso no SER:** não achado no AE. Candidato NAO_AE «Reabilitação Cardíaca» (t1 v1048; REUNI; só a parte cardíaca)
5. **Nosso:** Reabilitação Cardíaca [01a07a08-8136-7e44-875f-44835087d3f0] (NAO_AE) — 0 regras
   - Ativas: nenhuma
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - 5 pedidos do espelho do NAO_AE «Reabilitação Cardíaca» estão «Sem regras», mas esse recurso é do REUNI.
8. **Proposta:** Nada no AE. Não aplicar o CRECE ao NAO_AE sem conferir o REUNI.
