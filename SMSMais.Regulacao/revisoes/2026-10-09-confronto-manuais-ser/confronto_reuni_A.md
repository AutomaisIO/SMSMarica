# Confronto REUNI (rede geral do SER, ramo NAO_AE) × regras em produção — trecho A (p.7–14)

Fonte: *REUNI_MANUAL DO SOLICITANTE_V3 29.12.2022*, p.7–14 (regras gerais 1–3, 4.1 Aparelho Cardiovascular, 4.1.6 Exames em Cardiologia, 4.2 Cirurgia Bariátrica). As tabelas foram conferidas na imagem renderizada do PDF; o texto `-layout` mistura as colunas das p.11 e p.13. Produção: export de 09/10/2026 (`regras.json`, `ser_recursos.json`, `analise_espelho_por_procedimento.json`). Trabalho só de leitura.

## Resumo

O trecho tem 3 blocos de regras gerais e 23 linhas de recurso, sendo uma delas a bariátrica, que no SER se divide em Adulto e Superobesidade. Das 23 linhas, só **3 estão cobertas** (Cirurgia Cardíaca Pediátrica, Aorta Torácica e Orovalvar) e **5 estão parciais**: Cardiopatia Congênita Adulto, os dois Cateterismos e as duas Arteriografias Periféricas. Nessas cinco, a falha que se repete é o encaminhamento global genérico, que não exige o conteúdo que o manual pede (história clínica, alergia a iodo, modo ventilatório). **11 estão sem regra nenhuma** — nem o encaminhamento global, que só foi replicado em procedimentos que já tinham regra importada. Entre elas estão CDI, Revascularização, Marcapasso, Pré-Angioplastia, toda a Cirurgia Vascular, Eco de Estresse, Arteriografia Cerebral e Bariátrica Superobesidade; juntas, somam 59 pedidos no espelho do SER caindo em "Sem regras". **3 estão erradas**: (1) Estudo Eletrofisiológico/Ablação recebeu as regras das linhas de Ecocardiograma da p.12, inclusive o caco "…ECOCARDIOGRAMA DE ESTRESSE" e "Informar modo ventilatório", e julga 12 pedidos com regra do recurso errado; (2) a Bariátrica (Adulto), com 107 pedidos "A conferir", tem a inclusão como um Sim/Não truncado, uma caixinha de epífises com o caco "acima." que aparece para todos (Documento ignora idade), nenhuma faixa de idade deduzida e o encaminhamento em duplicidade; (3) a Avaliação de Cardiopatia Congênita Pediátrica tem um erro de um ano (máx. 18 em vez de 17). O Eco Transesofágico do manual é "(INTERNADOS)" e o catálogo só tem "(ambulatorial)" (SEM_RECURSO_NO_SER). No trecho, a parte condicional se resume a 4 pontos: o nefrologista no Cateterismo Internados, o laudo do endocrinologista e as epífises na Bariátrica, e o prazo só do Doppler na Carotídea. Os três primeiros dá para cobrir hoje com uma "pergunta negada" (ex.: "tem U>80 ou Cr>2,0 SEM avaliação do nefrologista?" — Sim bloqueia); o que não dá é tornar uma caixinha de documento condicional. Há 42 regras com fonte REUNI p.7–14 (36 ativas); nenhuma vem das p.7–9.

Contagem por veredito: COBERTO 3, PARCIAL 5, AUSENTE 11, ERRADO 3, NAO_REPRESENTAVEL_HOJE 1, SEM_RECURSO_NO_SER 1, NAO_SE_APLICA 2.

## Tabela-resumo

| seção | recurso (manual) | recurso SER (NAO_AE) | veredito | problema principal |
|---|---|---|---|---|
| 1.2 / 4 (introdução) | Dados cadastrais e do pedido obrigatórios (valem para todo recurso) | — (todos os recursos NAO_AE) | **NAO_REPRESENTAVEL_HOJE** | O modelo não tem regra global; o único item de cadastro que o Dedutível sabe checar é o CPF (exige_cpf), e nenhuma regra o usa. |
| 1.1, 1.3–1.6, 2.1–2.6 | Orientações ao solicitante (anexar conforme protocolo, pendências em 60 dias, chave de autorização, cancelamento com motivo) | — | **NAO_SE_APLICA** | Não é critério de elegibilidade. O ponto que importa para o sistema é operacional: pendência sem resposta em 60 dias é cancelada pelo SER — interessa à fila de pendências, não ao motor de regras. |
| 3.1–3.8 | Situação no sistema (Em fila, Pendência, Agendado, Chegada confirmada, Chegada não confirmada, Atendido, Alta, Cancelado) | — | **NAO_SE_APLICA** | Sem relação com regras; serve para conferir o mapeamento de status do espelho do SER. |
| 4.1.1 | CIRURGIA CARDÍACA PEDIÁTRICA | Ambulatório 1ª vez em Cardiologia - Cirurgia Cardíaca Pediátrica (tipo1 v1045) | **COBERTO** | Nada de conteúdo. O recurso v1045 tem um vínculo INATIVO com "Cirurgia Bariátrica - Superobesidade (IMC acima 55)" (pareamento errado antigo; a análise só usa origem ativa, então não afeta). |
| 4.1.1 | AVALIAÇÃO DE CARDIOPATIA CONGÊNITA PEDIÁTRICA (INTERNADOS) | Avaliação de Cardiopatia Congênita Pediátrica (Internados) (tipo1 v1047) | **ERRADO** | (leve) Dedutível 9d289a2b com idade_max_anos = 18: o avaliador só barra idade > máximo, então o paciente com 18 anos completos passa. "Menores de 18" = máximo 17. |
| 4.1.1 | CARDIOPATIA CONGÊNITA (ADULTOS) | Ambulatório 1ª vez em Cardiologia - Cardiopatia Congênita (Adulto) (tipo1 v1044) | **PARCIAL** | A idade (≥ 18) está dentro da Pergunta 876bb6c5, junto com o critério clínico, então não é deduzida do cadastro. |
| 4.1.2 | ANEURISMA/ DISSECÇÃO DE AORTA TORÁCICA | Ambulatório 1ª vez em Cirurgia Cardiovascular - Aneurisma / Dissecção de Aorta Torácica (tipo1 v1057) | **COBERTO** | Nenhum ("Angioressonâcia" é erro de digitação do próprio manual). É o destino da Obs. de 4.1.5 (dissecção acima da saída das renais). |
| 4.1.2 | IMPLANTE DE CARDIODESFIBRILADOR (CDI) | Ambulatório 1ª Vez em Cardiologia - Implante de Cardiodesfibrilador (CDI) (tipo1 v1058) | **AUSENTE** | Nenhuma regra, nem o encaminhamento global. Provável perda na importação: o nome quebra em "CARDIODESFIBRILADO / R(CDI)" e, no texto extraído, as colunas se misturam com as da linha da Aorta Torácica. |
| 4.1.2 | CIRURGIA OROVALVAR | Ambulatório 1ª vez em Cirurgia Cardiovascular - Cirurgia Orovalvar (tipo1 v1053) | **COBERTO** | A Pergunta 01cefd82 está escrita como afirmação (funciona, mas lê mal); o "<6m" está só no texto. |
| 4.1.2 | CIRURGIA DE REVASCULARIZAÇÃO DO MIOCÁRDIO | Ambulatório 1ª vez em Cardiologia - Cirurgia de Revascularização do Miocárdio (tipo1 v1054) | **AUSENTE** | Nenhuma regra, nem o encaminhamento global. |
| 4.1.2 | IMPLANTE DE MARCAPASSO | Ambulatório 1ª vez em Cardiologia - Implante de Marcapasso (tipo1 v1055) | **AUSENTE** | Nenhuma regra. O SER também tem "Implante de Ressincronizador Cardíaco" (v1056), sem linha no manual: não é este. |
| 4.1.3 | ESTUDO ELETROFISIOLÓGICO (ABLAÇÃO) | Ambulatório 1ª vez em Cardiologia Estudo Eletrofisiológico / Ablação (tipo1 v1059) | **ERRADO** | 3 das 4 regras ativas são de OUTRO recurso, a tabela 4.1.6.1 da p.12 (Ecocardiograma Transesofágico (Internados) / Ecocardiograma de Estresse): 9c34827a "Inserir exame de Ecocardiograma Transtorácico. ECOCARDIOGRAMA DE ESTRESSE" (é um caco: o requisito saiu grudado no nome do recurso seguinte), e8423576 "Inserir exame de Ecocardiograma Transtorácico" e a Informativa 4d5af645 "Informar modo ventilatório" (modo ventilatório é de paciente internado e não faz sentido numa consulta ambulatorial). |
| 4.1.4 | PRÉ-ANGIOPLASTIA | Ambulatório 1ª vez em Cardiologia - Pré Angioplastia Coronariana (tipo1 v1060) | **AUSENTE** | Nenhuma regra. |
| 4.1.5 | Vasculopatia Arterial Periférica | Ambulatório 1ª vez em Cirurgia Vascular - Vasculopatia Arterial Periférica (tipo1 v1067) | **AUSENTE** | Nenhuma regra. Tirando a bariátrica, é o maior volume do trecho sem regra nenhuma no espelho. |
| 4.1.5 | Vasculopatia Carotídea | Ambulatório 1ª vez em Cirurgia Vascular - Vasculopatia Carotídea (tipo1 v1069) | **AUSENTE** | Nenhuma regra. |
| 4.1.5 | ANEURISMA/ DISSECÇÃO DE AORTA ABDOMINAL | Ambulatório 1ª vez em Cirurgia Vascular - Aneurisma / Dissecção de Aorta Abdominal (tipo1 v1066) | **AUSENTE** | Nenhuma regra. |
| 4.1.5 | FÍSTULA ARTERIOVENOSA PARA HEMODIÁLISE | Fístula Arterio Venosa para Hemodiálise (tipo1 v1070) | **AUSENTE** | Nenhuma regra. O único requisito é o encaminhamento, e o encaminhamento global só foi replicado nos procedimentos que já tinham regra importada. |
| 4.1.6.1 | ECOCARDIOGRAMA TRANSESOFÁGICO (INTERNADOS) | não achado no catálogo — o SER só tem "Ecocardiograma Transesofágico (ambulatorial)" (tipo2 v1044) | **SEM_RECURSO_NO_SER** | O manual (v3, 2022) traz só a versão (INTERNADOS); o catálogo atual tem só a (ambulatorial), que está sem nenhuma regra. |
| 4.1.6.1 | ECOCARDIOGRAMA DE ESTRESSE | Ecocardiograma de Estresse (tipo2 v1045) | **AUSENTE** | Nenhuma regra: os requisitos desta linha caíram em "Estudo Eletrofisiológico / Ablação" (a 9c34827a até leva o nome "ECOCARDIOGRAMA DE ESTRESSE" grudado). |
| 4.1.6.2 | CATETERISMO CARDÍACO (AMBULATORIAL) | Cateterismo Cardíaco (Ambulatorial) (tipo2 v1049) | **PARCIAL** | O encaminhamento é o texto global e não traz o conteúdo exigido (História clínica, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo). |
| 4.1.6.2 | CATETERISMO CARDÍACO (INTERNADOS) | Cateterismo Cardíaco (Internados) (tipo2 v1046) | **PARCIAL** | O encaminhamento global não traz o conteúdo (História clínica, informar modo ventilatório, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo). |
| 4.1.6.2 | ARTERIOGRAFIA PERIFÉRICA (AMBULATORIAL) | Arteriografia Periférica (Ambulatorial) (tipo2 v1047) | **PARCIAL** | O encaminhamento global não traz o conteúdo (História clínica, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo). |
| 4.1.6.2 | ARTERIOGRAFIA CEREBRAL (AMBULATORIAL) | Arteriografia Cerebral (Ambulatório) (tipo2 v1118) | **AUSENTE** | Nenhuma regra. |
| 4.1.6.2 | ARTERIOGRAFIA PERIFÉRICA (INTERNADOS) | Arteriografia Periférica (Internados) (tipo2 v1048) | **PARCIAL** | O encaminhamento global não traz o conteúdo (História clínica, informar modo ventilatório, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo). |
| 4.2.1 | CIRURGIA BARIÁTRICA (Portaria SAS nº 492 de 31/08/2007) | Ambulatório 1ª vez - Cirurgia Bariátrica (Adulto) (tipo1 v1073). Também "…Cirurgia Bariátrica - Superobesidade (IMC acima 55)" (v1074), na linha seguinte. | **ERRADO** | A 1c6bf33e (Pergunta Sim/Não, Não bloqueia) despeja os critérios a–d num texto só, e cortado: a pergunta para nos 500 caracteres ("…Devem possuir laudo do endoc") e a descrição termina em "d. Idade entre 18 anos e 65 anos, com alguma das". O solicitante responde Sim/Não a um parágrafo truncado, quando o certo é uma lista "basta uma". |
| 4.2.1 | CIRURGIA BARIÁTRICA — 2º recurso do SER (mesma linha do manual) | Ambulatório 1ª vez - Cirurgia Bariátrica - Superobesidade (IMC acima 55) (tipo1 v1074) | **AUSENTE** | 0 regras. O manual tem uma linha só para a bariátrica; o SER separa Adulto × Superobesidade (IMC > 55), e só o Adulto recebeu as regras. |

## Achados transversais

- **Encaminhamento global só onde já havia regra.** Dos 23 canônicos do trecho, 11 têm a regra "CRECE/REUNI — requisito global"; os outros 12 estão com zero regras.
- **Prazo nunca vira dado.** `validade_dias` e `tipo_exame_id` estão nulos nas 619 regras: todo "(< 6m)", "(< 3 m)", "há menos de 1 ano" e "(< 4 meses)" existe só no texto.
- **Documento ignora idade.** No `AvaliadorElegibilidade`, regra Documental devolve Indefinido sem olhar idade/sexo. Documento com faixa etária preenchida (bariátrica, epífises) engana quem lê o cadastro da regra.
- **Perguntas escritas como afirmação** ("Destina-se a pacientes em domicílio", "Doença orovalvar moderada ou grave…"): funcionam (Não bloqueia), mas leem mal na tela.
- **Contagem do catálogo inflada.** O `ser_catalogo_resumo.txt` mostra "regras ativas: 12" para Cardiopatia Congênita (Adulto) e "4" para Cirurgia Cardíaca Pediátrica, porque soma vínculos inativos/duplicados (v1044 → Bariátrica, v1045 → Superobesidade, v1047 → Genética Médica Adulto). A análise não é afetada: `AnaliseRegrasEspelhoService.ContextoAsync` usa só origens `Ativo`.
- **Condicional contornável.** O que o manual põe como condição ("se X, exigir Y") quase sempre cabe numa pergunta negada Sim/Não ("tem X SEM Y?" — Sim bloqueia). O que o modelo não faz é mostrar uma caixinha de Documento só quando X.

## Regras de produção do trecho que não batem com o manual (lixo / caco / lugar errado)

| regra | estado | procedimento | texto | problema |
|---|---|---|---|---|
| 9c34827a | ATIVA | Estudo Eletrofisiológico / Ablação | "Inserir exame de Ecocardiograma Transtorácico. ECOCARDIOGRAMA DE ESTRESSE" | Caco (requisito + nome do recurso seguinte) e procedimento errado: é da tabela 4.1.6.1. |
| e8423576 | ATIVA | Estudo Eletrofisiológico / Ablação | "Inserir exame de Ecocardiograma Transtorácico" | Procedimento errado (Eco de Estresse / Eco Transesofágico). |
| 4d5af645 | ATIVA | Estudo Eletrofisiológico / Ablação | Informativa "Informar modo ventilatório" | Procedimento errado (Eco Transesofágico (Internados)). |
| 61fe6349 | inativa | Estudo Eletrofisiológico / Ablação | Pergunta "Informar modo ventilatório" | Idem (v1 da anterior). |
| 5c339564 | ATIVA | Cirurgia Bariátrica (Adulto) | "acima. Se entre 16 e 18 anos, …epífises…" (Documento, idade 16–18) | Caco no início; a idade não tem efeito em Documento, então a caixinha aparece para todos. |
| a938e852 | inativa | Cirurgia Bariátrica (Adulto) | idem v1 (obrigatória) | Idem. |
| 1c6bf33e | ATIVA | Cirurgia Bariátrica (Adulto) | Inclusão a–d num Sim/Não | Texto truncado (500 car. na pergunta; a descrição termina em "com alguma das"). |
| b7a48bb0 | ATIVA | Cirurgia Bariátrica (Adulto) | Exclusão a–d | Caco no fim: "Anexar (ou descrever de forma clara e detalhada)". |
| e7e83329 | inativa | Cirurgia Bariátrica (Adulto) | Pergunta "Conforme preconiza a Portaria SAS n° 492 de 31/08/2007, abaixo" | Cabeçalho que virou pergunta (já inativa). |
| 4171219c | inativa | Cirurgia Bariátrica (Adulto) | Exclusão a–d como Documento | Tipo errado (já trocada pela b7a48bb0). |
| 59ece322 | inativa | Cateterismo Cardíaco (Internados) | "HT<27 e Hb<9,0…" com Não bloqueando | Lógica invertida (corrigida na v2 81febf81). |
| 86480e7d | inativa | Cateterismo Cardíaco (Internados) | IR/nefrologista como pergunta-afirmação | Virou Informativa na v2 (0e5fe3fd). |

## Por recurso

### 1. 1.2 / 4 (introdução) — Dados cadastrais e do pedido obrigatórios (valem para todo recurso)

1. **Seção / página:** 1.2 / 4 (introdução) / p.7, 10
2. **Estrutura:** SO_ORIENTACAO (requisito global)
3. **Requisitos (manual):**
   - 1.2 — cadastro: "Número do CNS"; "Número de contato telefônico"; "Endereço completo"; "Data de nascimento"; "Nome completo da mãe"; "CPF"; "Dentre outros".
   - 4 — "Identificar o médico solicitante, devidamente cadastrado no SER."
   - 4 — "Informar a hipótese diagnóstica (classificação de risco e CID 10), a natureza da solicitação (mandado judicial – sim ou não), a unidade de origem e anexos (caso necessário)."
4. **Recurso SER:** — (todos os recursos NAO_AE)
5. **Nosso:** — · espelho SER: —
   - Ativas:
     - nenhuma: 0 das 619 regras usa exige_cpf; não existe regra global no modelo
6. **Veredito:** NAO_REPRESENTAVEL_HOJE
7. **Problemas:**
   - O modelo não tem regra global; o único item de cadastro que o Dedutível sabe checar é o CPF (exige_cpf), e nenhuma regra o usa.
   - CNS, telefone, endereço, nascimento e nome da mãe não têm tipo de regra. Classificação de risco, CID, mandado judicial e médico solicitante são campos do formulário do SER, não critério de elegibilidade.
8. **Proposta:**
   - Não replicar em ~400 procedimentos: tratar como checagem de cadastro no "Enviar ao SER" (avisar/barrar o envio sem CNS, CPF, telefone, endereço, nascimento e nome da mãe), que é onde o SER cobra.
   - Se tiver que ir para o motor de regras: Dedutível exige_cpf com severidade Ressalva, replicado como foi feito com o encaminhamento global. O resto pede regra global ou um Dedutível de cadastro novo (mudança de modelo).

### 2. 1.1, 1.3–1.6, 2.1–2.6 — Orientações ao solicitante (anexar conforme protocolo, pendências em 60 dias, chave de autorização, cancelamento com motivo)

1. **Seção / página:** 1.1, 1.3–1.6, 2.1–2.6 / p.7–8
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual):**
   - 2.3 "Observar a necessidade de anexar exames e solicitações médicas, conforme protocolo." (é o que as linhas das tabelas detalham)
   - 1.5/2.4 "responder as pendências no campo 'responder pendências', em até 60 dias, pois, o sistema cancelará automaticamente após este período."
   - 1.6/2.5 imprimir a chave de autorização e informar o paciente; 2.6 informar o motivo ANTES de cancelar.
4. **Recurso SER:** —
5. **Nosso:** — · espelho SER: —
   - Ativas: nenhuma
6. **Veredito:** NAO_SE_APLICA
7. **Problemas:**
   - Não é critério de elegibilidade. O ponto que importa para o sistema é operacional: pendência sem resposta em 60 dias é cancelada pelo SER — interessa à fila de pendências, não ao motor de regras.
8. **Proposta:**
   - Nenhuma regra.

### 3. 3.1–3.8 — Situação no sistema (Em fila, Pendência, Agendado, Chegada confirmada, Chegada não confirmada, Atendido, Alta, Cancelado)

1. **Seção / página:** 3.1–3.8 / p.9
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (manual):**
   - Glossário dos estados do pedido no módulo ambulatorial do SER.
4. **Recurso SER:** —
5. **Nosso:** — · espelho SER: —
   - Ativas: nenhuma
6. **Veredito:** NAO_SE_APLICA
7. **Problemas:**
   - Sem relação com regras; serve para conferir o mapeamento de status do espelho do SER.
8. **Proposta:**
   - Nenhuma regra.

### 4. 4.1.1 — CIRURGIA CARDÍACA PEDIÁTRICA

1. **Seção / página:** 4.1.1 / p.10
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento/exame: "Inserir exame de Ecocardiograma recente." (sem prazo numérico)
4. **Recurso SER:** Ambulatório 1ª vez em Cardiologia - Cirurgia Cardíaca Pediátrica (tipo1 v1045)
5. **Nosso:** Ambulatório 1ª vez em Cardiologia - Cirurgia Cardíaca Pediátrica · espelho SER: nenhum pedido no espelho
   - Ativas:
     - 0dc59c96 v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
     - 5e60601b v1 Documento/Bloqueia: "Inserir exame de Ecocardiograma recente" | fonte: REUNI p.10
6. **Veredito:** COBERTO
7. **Problemas:**
   - Nada de conteúdo. O recurso v1045 tem um vínculo INATIVO com "Cirurgia Bariátrica - Superobesidade (IMC acima 55)" (pareamento errado antigo; a análise só usa origem ativa, então não afeta).
8. **Proposta:**
   - Manter. Opcional: apagar o vínculo inativo v1045 → Superobesidade.

### 5. 4.1.1 — AVALIAÇÃO DE CARDIOPATIA CONGÊNITA PEDIÁTRICA (INTERNADOS)

1. **Seção / página:** 4.1.1 / p.10
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento/exame: "Inserir exame de Ecocardiograma recente."
   - Idade (dedutível): "Destina-se a pacientes menores de 18 anos."
4. **Recurso SER:** Avaliação de Cardiopatia Congênita Pediátrica (Internados) (tipo1 v1047)
5. **Nosso:** Avaliação de Cardiopatia Congênita Pediátrica (Internados) · espelho SER: nenhum pedido no espelho
   - Ativas:
     - 2733444a v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
     - 9d289a2b v1 Dedutível/Bloqueia: "Destina-se a pacientes menores de 18 anos" | idade None–18 | fonte: REUNI p.10
     - e30e9f18 v1 Documento/Bloqueia: "Inserir exame de Ecocardiograma recente" | fonte: REUNI p.10
6. **Veredito:** ERRADO
7. **Problemas:**
   - (leve) Dedutível 9d289a2b com idade_max_anos = 18: o avaliador só barra idade > máximo, então o paciente com 18 anos completos passa. "Menores de 18" = máximo 17.
   - Vínculo inativo v1047 → "Ambulatório 1ª vez em Genética Médica - Adulto" (não afeta a análise).
8. **Proposta:**
   - Nova versão de 9d289a2b: Dedutível, Bloqueia, idade_max_anos = 17, texto "Destina-se a pacientes menores de 18 anos." O resto fica como está.

### 6. 4.1.1 — CARDIOPATIA CONGÊNITA (ADULTOS)

1. **Seção / página:** 4.1.1 / p.10
2. **Estrutura:** CONDICIONAL leve (eixo: cardiopatia já operada → o encaminhamento precisa descrever as cirurgias e os procedimentos com as datas)
3. **Requisitos (manual):**
   - Documento (conteúdo obrigatório): "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (cardiopatias abordadas previamente necessitam da descrição das cirurgias e procedimentos realizados com as respectivas datas)."
   - Documento/exame: "Inserir exame de Ecocardiograma recente (< 6m)."
   - Idade (dedutível) + critério clínico: "Destina-se a pacientes com 18 anos ou mais com sintomas ou sequela da cardiopatia."
4. **Recurso SER:** Ambulatório 1ª vez em Cardiologia - Cardiopatia Congênita (Adulto) (tipo1 v1044)
5. **Nosso:** Ambulatório 1ª vez em Cardiologia - Cardiopatia Congênita (Adulto) · espelho SER: nenhum pedido no espelho
   - Ativas:
     - d5b9ae7d v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
     - 876bb6c5 v1 Pergunta/Bloqueia: "Destina-se a pacientes com 18 anos ou mais com sintomas ou sequela da cardiopatia" | bloqueia se: Não | fonte: REUNI p.10
     - a83d51c2 v1 Documento/Bloqueia: "Inserir exame de Ecocardiograma recente (< 6m)" | fonte: REUNI p.10
6. **Veredito:** PARCIAL
7. **Problemas:**
   - A idade (≥ 18) está dentro da Pergunta 876bb6c5, junto com o critério clínico, então não é deduzida do cadastro.
   - O encaminhamento global (d5b9ae7d) não traz a exigência das cirurgias e procedimentos anteriores com as datas.
   - O "< 6m" do Eco está só no texto (validade_dias nulo).
   - Vínculo inativo v1044 → "Cirurgia Bariátrica (Adulto)": por isso o resumo do catálogo mostra "12 regras ativas" neste recurso, quando são 3. A análise não é afetada.
8. **Proposta:**
   - Dedutível, Bloqueia, idade_min_anos = 18: "Destina-se a pacientes com 18 anos ou mais."
   - Nova versão de 876bb6c5: Pergunta Sim/Não, Não bloqueia: "O paciente tem sintomas ou sequela da cardiopatia congênita?"
   - Nova versão do encaminhamento d5b9ae7d com o texto literal do manual, incluindo o parêntese das cirurgias com as datas (o "se já foi operado" fica no texto, sem precisar de condição).
   - a83d51c2: validade_dias = 180.

### 7. 4.1.2 — ANEURISMA/ DISSECÇÃO DE AORTA TORÁCICA

1. **Seção / página:** 4.1.2 / p.11
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento/exame: "Inserir exame de Ecocardiograma recente."
   - Documento/exame (alternativas): "Inserir exames de imagem com laudos que comprovem a doença (Tomografia ou Angiotomografia ou Angioressonâcia)."
4. **Recurso SER:** Ambulatório 1ª vez em Cirurgia Cardiovascular - Aneurisma / Dissecção de Aorta Torácica (tipo1 v1057)
5. **Nosso:** Ambulatório 1ª vez em Cirurgia Cardiovascular - Aneurisma / Dissecção de Aorta Torácica · espelho SER: 17 A conferir
   - Ativas:
     - 59fb5698 v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
     - 2daef9c1 v1 Documento/Bloqueia: "Inserir exames de imagem com laudos que comprovem a doença (Tomografia ou Angiotomografia ou Angioressonâcia)" | fonte: REUNI p.11
     - 2074539d v1 Documento/Bloqueia: "Inserir exame de Ecocardiograma recente" | fonte: REUNI p.11
6. **Veredito:** COBERTO
7. **Problemas:**
   - Nenhum ("Angioressonâcia" é erro de digitação do próprio manual). É o destino da Obs. de 4.1.5 (dissecção acima da saída das renais).
8. **Proposta:**
   - Manter.

### 8. 4.1.2 — IMPLANTE DE CARDIODESFIBRILADOR (CDI)

1. **Seção / página:** 4.1.2 / p.11
2. **Estrutura:** LISTA_BASTA_UM (indicação) + documentos
3. **Requisitos (manual):**
   - Critério clínico, basta um: "Indicações: Diagnóstico de Taquicardia Ventricular sustentada ou morte súbita abortada ou disfunção ventricular grave com episódios de taquicardia ventricular não sustentada, comprovados ao Holter."
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento/exame: "Inserir ECG e Ecocardiograma (<6m)."
   - Documento/exame (alternativas): "Inserir Holter 24 horas ou teste ergométrico."
4. **Recurso SER:** Ambulatório 1ª Vez em Cardiologia - Implante de Cardiodesfibrilador (CDI) (tipo1 v1058)
5. **Nosso:** Ambulatório 1ª Vez em Cardiologia - Implante de Cardiodesfibrilador (CDI) · espelho SER: nenhum pedido no espelho
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra, nem o encaminhamento global. Provável perda na importação: o nome quebra em "CARDIODESFIBRILADO / R(CDI)" e, no texto extraído, as colunas se misturam com as da linha da Aorta Torácica.
8. **Proposta:**
   - Pergunta lista "basta uma" ("nenhuma destas" bloqueia), Bloqueia: "O paciente tem ao menos uma destas indicações?" — "Diagnóstico de Taquicardia Ventricular sustentada" | "Morte súbita abortada" | "Disfunção ventricular grave com episódios de taquicardia ventricular não sustentada, comprovados ao Holter".
   - Documento, Bloqueia: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento, Bloqueia: "Inserir ECG e Ecocardiograma (<6m).", validade_dias 180.
   - Documento, Bloqueia: "Inserir Holter 24 horas ou teste ergométrico."

### 9. 4.1.2 — CIRURGIA OROVALVAR

1. **Seção / página:** 4.1.2 / p.11
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Critério clínico: "Indicações: Doença orovalvar moderada ou grave comprovada por Ecocardiograma."
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento/exame: "Inserir ECG e Ecocardiograma (<6m)."
4. **Recurso SER:** Ambulatório 1ª vez em Cirurgia Cardiovascular - Cirurgia Orovalvar (tipo1 v1053)
5. **Nosso:** Ambulatório 1ª vez em Cirurgia Cardiovascular - Cirurgia Orovalvar · espelho SER: 6 A conferir
   - Ativas:
     - 01cefd82 v1 Pergunta/Bloqueia: "Doença orovalvar moderada ou grave comprovada por Ecocardiograma" | bloqueia se: Não | fonte: REUNI p.11
     - 938cae1b v1 Documento/Bloqueia: "Inserir ECG e Ecocardiograma (<6m)" | fonte: REUNI p.11
     - ba04ca84 v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
6. **Veredito:** COBERTO
7. **Problemas:**
   - A Pergunta 01cefd82 está escrita como afirmação (funciona, mas lê mal); o "<6m" está só no texto.
8. **Proposta:**
   - Opcional: pergunta "O paciente tem doença orovalvar moderada ou grave comprovada por Ecocardiograma?" (Não bloqueia) e validade_dias 180 em 938cae1b.

### 10. 4.1.2 — CIRURGIA DE REVASCULARIZAÇÃO DO MIOCÁRDIO

1. **Seção / página:** 4.1.2 / p.11
2. **Estrutura:** LISTA_BASTA_UM (tipo de lesão) + documentos
3. **Requisitos (manual):**
   - Critério clínico, basta um: "Indicações: Portadores de Doença Arterial Coronariana (DAC) comprovada por cateterismo cardíaco (lesão do tronco de coronária esquerda, lesão trivascular)."
   - Documento/exame: "Inserir Cateterismo Cardíaco realizado há menos de 1 ano."
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento/exame: "Inserir ECG e Ecocardiograma." (sem prazo)
4. **Recurso SER:** Ambulatório 1ª vez em Cardiologia - Cirurgia de Revascularização do Miocárdio (tipo1 v1054)
5. **Nosso:** Ambulatório 1ª vez em Cardiologia - Cirurgia de Revascularização do Miocárdio · espelho SER: 2 Sem regras
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra, nem o encaminhamento global.
8. **Proposta:**
   - Pergunta lista "basta uma" ("nenhuma destas" bloqueia): "DAC comprovada por cateterismo cardíaco com:" — "Lesão do tronco de coronária esquerda" | "Lesão trivascular".
   - Documento: "Inserir Cateterismo Cardíaco realizado há menos de 1 ano.", validade_dias 365.
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento: "Inserir ECG e Ecocardiograma."

### 11. 4.1.2 — IMPLANTE DE MARCAPASSO

1. **Seção / página:** 4.1.2 / p.11
2. **Estrutura:** LISTA_BASTA_UM (indicação) + documentos
3. **Requisitos (manual):**
   - Critério clínico, basta um: "Indicações: Bradiarritmias ou Bloqueios Atrioventriculares."
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento/exame: "Inserir ECG e Ecocardiograma (<6m)."
   - Documento/exame (alternativas): "Inserir Holter 24 horas ou teste ergométrico."
4. **Recurso SER:** Ambulatório 1ª vez em Cardiologia - Implante de Marcapasso (tipo1 v1055)
5. **Nosso:** Ambulatório 1ª vez em Cardiologia - Implante de Marcapasso · espelho SER: 2 Sem regras
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra. O SER também tem "Implante de Ressincronizador Cardíaco" (v1056), sem linha no manual: não é este.
8. **Proposta:**
   - Pergunta lista "basta uma" ("nenhuma destas" bloqueia): "Bradiarritmia" | "Bloqueio atrioventricular".
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento: "Inserir ECG e Ecocardiograma (<6m).", validade_dias 180.
   - Documento: "Inserir Holter 24 horas ou teste ergométrico."

### 12. 4.1.3 — ESTUDO ELETROFISIOLÓGICO (ABLAÇÃO)

1. **Seção / página:** 4.1.3 / p.11
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Critério clínico: "Indicações: Arritmias complexas com indicação de avaliação de ablação por radiofrequência."
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento/exame: "Inserir ECG e Ecocardiograma (<6m)."
   - Documento/exame (alternativas): "Inserir Holter 24 horas ou teste ergométrico."
4. **Recurso SER:** Ambulatório 1ª vez em Cardiologia Estudo Eletrofisiológico / Ablação (tipo1 v1059)
5. **Nosso:** Ambulatório 1ª vez em Cardiologia Estudo Eletrofisiológico / Ablação · espelho SER: 12 A conferir
   - Ativas:
     - 9c34827a v1 Documento/Bloqueia: "Inserir exame de Ecocardiograma Transtorácico. ECOCARDIOGRAMA DE ESTRESSE" | fonte: REUNI p.12
     - 3431c438 v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
     - e8423576 v1 Documento/Bloqueia: "Inserir exame de Ecocardiograma Transtorácico" | fonte: REUNI p.12
     - 4d5af645 v2 Informativa/Bloqueia: "Informar modo ventilatório" | fonte: REUNI p.12
   - Inativas:
     - 61fe6349 v1 Pergunta/Bloqueia: "Informar modo ventilatório" | bloqueia se: Não | fonte: REUNI p.12
6. **Veredito:** ERRADO
7. **Problemas:**
   - 3 das 4 regras ativas são de OUTRO recurso, a tabela 4.1.6.1 da p.12 (Ecocardiograma Transesofágico (Internados) / Ecocardiograma de Estresse): 9c34827a "Inserir exame de Ecocardiograma Transtorácico. ECOCARDIOGRAMA DE ESTRESSE" (é um caco: o requisito saiu grudado no nome do recurso seguinte), e8423576 "Inserir exame de Ecocardiograma Transtorácico" e a Informativa 4d5af645 "Informar modo ventilatório" (modo ventilatório é de paciente internado e não faz sentido numa consulta ambulatorial).
   - Faltam todos os requisitos próprios do recurso: a indicação, ECG + Eco < 6m e Holter ou teste ergométrico.
   - O solicitante recebe duas caixinhas de Eco transtorácico (uma delas é o caco).
8. **Proposta:**
   - Inativar 9c34827a, e8423576 e 4d5af645 e levar o conteúdo para "Ecocardiograma de Estresse" e para o Eco Transesofágico (ver 4.1.6.1).
   - Pergunta Sim/Não, Não bloqueia: "O paciente tem arritmia complexa com indicação de avaliação de ablação por radiofrequência?"
   - Documento: "Inserir ECG e Ecocardiograma (<6m).", validade_dias 180.
   - Documento: "Inserir Holter 24 horas ou teste ergométrico."
   - Manter 3431c438 (encaminhamento).

### 13. 4.1.4 — PRÉ-ANGIOPLASTIA

1. **Seção / página:** 4.1.4 / p.12
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Critério clínico: "Indicações: Portadores de DAC com lesões moderadas a graves, comprovadas por cateterismo cardíaco."
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento/exame: "Inserir Cateterismo Cardíaco realizado há menos de 1 ano."
4. **Recurso SER:** Ambulatório 1ª vez em Cardiologia - Pré Angioplastia Coronariana (tipo1 v1060)
5. **Nosso:** Ambulatório 1ª vez em Cardiologia - Pré Angioplastia Coronariana · espelho SER: 5 Sem regras
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra.
8. **Proposta:**
   - Pergunta Sim/Não, Não bloqueia: "O paciente é portador de DAC com lesões moderadas a graves, comprovadas por cateterismo cardíaco?"
   - Documento: "Inserir Cateterismo Cardíaco realizado há menos de 1 ano.", validade_dias 365.
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."

### 14. 4.1.5 — Vasculopatia Arterial Periférica

1. **Seção / página:** 4.1.5 / p.12
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Documento (conteúdo obrigatório): "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso, com exame físico direcionado (amplitude dos pulsos arteriais, temperatura dos membros, presença de claudicação, etc.)."
   - Documento/exame (alternativas): "Inserir exame de imagem (Doppler ou Angiorressonância ou Angio-TC de membros ou Arteriografia)."
4. **Recurso SER:** Ambulatório 1ª vez em Cirurgia Vascular - Vasculopatia Arterial Periférica (tipo1 v1067)
5. **Nosso:** Ambulatório 1ª vez em Cirurgia Vascular - Vasculopatia Arterial Periférica · espelho SER: 14 Sem regras
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra. Tirando a bariátrica, é o maior volume do trecho sem regra nenhuma no espelho.
8. **Proposta:**
   - Documento: o encaminhamento com o texto literal (exame físico direcionado).
   - Documento: "Inserir exame de imagem (Doppler ou Angiorressonância ou Angio-TC de membros ou Arteriografia)."

### 15. 4.1.5 — Vasculopatia Carotídea

1. **Seção / página:** 4.1.5 / p.12
2. **Estrutura:** CONDICIONAL leve (eixo: qual exame foi anexado — o prazo vale só para o Doppler)
3. **Requisitos (manual):**
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento/exame (alternativas): "Inserir exame de Doppler de artérias carotídeas (< 4 meses) ou Angio-TC."
4. **Recurso SER:** Ambulatório 1ª vez em Cirurgia Vascular - Vasculopatia Carotídea (tipo1 v1069)
5. **Nosso:** Ambulatório 1ª vez em Cirurgia Vascular - Vasculopatia Carotídea · espelho SER: 2 Sem regras
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra.
   - O "< 4 meses" vale só para o Doppler (a Angio-TC não tem prazo): um validade_dias na caixinha única barraria uma Angio-TC antiga.
8. **Proposta:**
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
   - Documento: "Inserir exame de Doppler de artérias carotídeas (< 4 meses) ou Angio-TC." SEM validade_dias (o prazo fica no texto). Para ter prazo por alternativa seria preciso regra condicional ou "basta um documento", que o modelo não tem.

### 16. 4.1.5 — ANEURISMA/ DISSECÇÃO DE AORTA ABDOMINAL

1. **Seção / página:** 4.1.5 / p.12
2. **Estrutura:** INCLUSAO_EXCLUSAO (exclusão por redirecionamento: dissecção acima da saída das artérias renais → Aorta Torácica)
3. **Requisitos (manual):**
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada."
   - Documento/exame (alternativas): "Inserir exame de Angio-TC ou Tomografia."
   - Exclusão/redirecionamento: "Obs.: Dissecção acima da saída das Artérias Renais deverá ser encaminhada à Cirurgia Cardiovascular (Aneurisma/ Dissecção de Aorta Torácica)."
4. **Recurso SER:** Ambulatório 1ª vez em Cirurgia Vascular - Aneurisma / Dissecção de Aorta Abdominal (tipo1 v1066)
5. **Nosso:** Ambulatório 1ª vez em Cirurgia Vascular - Aneurisma / Dissecção de Aorta Abdominal · espelho SER: 4 Sem regras
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra.
8. **Proposta:**
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada."
   - Documento: "Inserir exame de Angio-TC ou Tomografia."
   - Pergunta Sim/Não, Sim bloqueia: "A dissecção é acima da saída das Artérias Renais?" Na descrição: "Deverá ser encaminhada à Cirurgia Cardiovascular (Aneurisma/ Dissecção de Aorta Torácica)." Aneurisma sem dissecção responde Não.

### 17. 4.1.5 — FÍSTULA ARTERIOVENOSA PARA HEMODIÁLISE

1. **Seção / página:** 4.1.5 / p.12
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso."
4. **Recurso SER:** Fístula Arterio Venosa para Hemodiálise (tipo1 v1070)
5. **Nosso:** Fístula Arterio Venosa para Hemodiálise · espelho SER: nenhum pedido no espelho
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra. O único requisito é o encaminhamento, e o encaminhamento global só foi replicado nos procedimentos que já tinham regra importada.
8. **Proposta:**
   - Documento, Bloqueia: o encaminhamento (mesmo texto do requisito global).

### 18. 4.1.6.1 — ECOCARDIOGRAMA TRANSESOFÁGICO (INTERNADOS)

1. **Seção / página:** 4.1.6.1 / p.12
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Documento (conteúdo): "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso e justificativa para a realização deste exame."
   - Dado clínico: "Informar modo ventilatório."
   - Documento/exame: "Inserir exame de Ecocardiograma Transtorácico."
4. **Recurso SER:** não achado no catálogo — o SER só tem "Ecocardiograma Transesofágico (ambulatorial)" (tipo2 v1044)
5. **Nosso:** Ecocardiograma Transesofágico (ambulatorial) · espelho SER: 10 Sem regras
   - Ativas: nenhuma
6. **Veredito:** SEM_RECURSO_NO_SER
7. **Problemas:**
   - O manual (v3, 2022) traz só a versão (INTERNADOS); o catálogo atual tem só a (ambulatorial), que está sem nenhuma regra.
   - As regras desta linha foram parar em "Estudo Eletrofisiológico / Ablação" (ver 4.1.3).
8. **Proposta:**
   - Não cadastrar "modo ventilatório" no ambulatorial (é coisa de internado).
   - Com OK da regulação, aplicar ao ambulatorial o que as duas linhas de eco têm em comum: Documento "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso e justificativa para a realização deste exame." e Documento "Inserir exame de Ecocardiograma Transtorácico.", com severidade Ressalva enquanto não houver protocolo do ambulatorial.

### 19. 4.1.6.1 — ECOCARDIOGRAMA DE ESTRESSE

1. **Seção / página:** 4.1.6.1 / p.12
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Documento (conteúdo): "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso e justificativa para a realização deste exame."
   - Documento/exame: "Inserir exame de Ecocardiograma Transtorácico."
4. **Recurso SER:** Ecocardiograma de Estresse (tipo2 v1045)
5. **Nosso:** Ecocardiograma de Estresse · espelho SER: nenhum pedido no espelho
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra: os requisitos desta linha caíram em "Estudo Eletrofisiológico / Ablação" (a 9c34827a até leva o nome "ECOCARDIOGRAMA DE ESTRESSE" grudado).
8. **Proposta:**
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso e justificativa para a realização deste exame."
   - Documento: "Inserir exame de Ecocardiograma Transtorácico."

### 20. 4.1.6.2 — CATETERISMO CARDÍACO (AMBULATORIAL)

1. **Seção / página:** 4.1.6.2 / p.13
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Critério: "Destina-se a pacientes em domicílio."
   - Documento: "Inserir no SER a solicitação médica em formulário de Alto Custo."
   - Documento (conteúdo obrigatório): "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (História clínica, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo)."
   - Documento/exame (alternativas): "Inserir exame de ECG e/ou Ecocardiograma ou Teste ergométrico ou Cintilografia Miocárdica."
   - Valor laboratorial: "Inserir resultado de Hematócrito, hemoglobina, ureia, creatinina e glicose (< 3 m)."
4. **Recurso SER:** Cateterismo Cardíaco (Ambulatorial) (tipo2 v1049)
5. **Nosso:** Cateterismo Cardíaco (Ambulatorial) · espelho SER: 6 A conferir
   - Ativas:
     - 407e5dfc v1 Documento/Bloqueia: "Inserir resultado de Hematócrito, hemoglobina, ureia, creatinina e glicose (< 3 m)" | fonte: REUNI p.13
     - 27acf724 v1 Documento/Bloqueia: "Inserir no SER a solicitação médica em formulário de Alto Custo" | fonte: REUNI p.13
     - 92b2ba67 v1 Documento/Bloqueia: "Inserir exame de ECG e/ou Ecocardiograma ou Teste ergométrico ou Cintilografia Miocárdica" | fonte: REUNI p.13
     - 82dc78ba v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
     - a741ae4a v1 Pergunta/Bloqueia: "Destina-se a pacientes em domicílio" | bloqueia se: Não | fonte: REUNI p.13
6. **Veredito:** PARCIAL
7. **Problemas:**
   - O encaminhamento é o texto global e não traz o conteúdo exigido (História clínica, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo).
   - O "(< 3 m)" dos exames de laboratório está só no texto (validade_dias nulo).
   - A a741ae4a é uma afirmação ("Destina-se a pacientes em domicílio") usada como pergunta.
8. **Proposta:**
   - Nova versão de 82dc78ba com o texto literal: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (História clínica, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo)."
   - 407e5dfc: validade_dias 90.
   - a741ae4a: "O paciente está em domicílio (não internado)?" — Não bloqueia.

### 21. 4.1.6.2 — CATETERISMO CARDÍACO (INTERNADOS)

1. **Seção / página:** 4.1.6.2 / p.13
2. **Estrutura:** CONDICIONAL (eixo: função renal — U>80 ou Cr>2,0 exige avaliação do nefrologista) + exclusão (HT<27 e Hb<9,0)
3. **Requisitos (manual):**
   - Critério + dado: "Destina-se a pacientes em ambiente hospitalar. Informar o tipo de leito atual (CTI ou enfermaria)."
   - Documento: "Inserir no SER a solicitação médica em formulário de Alto Custo."
   - Documento (conteúdo obrigatório): "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (História clínica, informar modo ventilatório, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo)."
   - Valor laboratorial: "Inserir resultado de Hematócrito, hemoglobina, ureia, creatinina e glicose."
   - Dado clínico: "Inserir se o paciente foi trombolisado ou não, tempo de início de sintomas do infarto, curva enzimática de CK, CKMB e TROPONINA."
   - Documento/exame: "Inserir exame de ECG e/ou Ecocardiograma."
   - Condicional: "Caso o paciente apresente alterações sugestivas de Insuficiência Renal (U>80 ou Cr>2,0 é necessária avaliação do nefrologista)."
   - Exclusão: "HT<27 e Hb<9,0 não poderão realizar o exame."
4. **Recurso SER:** Cateterismo Cardíaco (Internados) (tipo2 v1046)
5. **Nosso:** Cateterismo Cardíaco (Internados) · espelho SER: nenhum pedido no espelho
   - Ativas:
     - 77b3554e v1 Documento/Bloqueia: "Inserir resultado de Hematócrito, hemoglobina, ureia, creatinina e glicose" | fonte: REUNI p.13
     - 1ed71fe3 v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
     - b254aa9b v1 Documento/Bloqueia: "Inserir no SER a solicitação médica em formulário de Alto Custo" | fonte: REUNI p.13
     - 8de3d0b8 v1 Pergunta/Bloqueia: "Destina-se a pacientes em ambiente hospitalar. Informar o tipo de leito atual (CTI ou enfermaria)" | bloqueia se: Não | fonte: REUNI p.13
     - 979e0de3 v1 Documento/Bloqueia: "Inserir se o paciente foi trombolisado ou não, tempo de início de sintomas do infarto, curva enzimática de CK, CKMB e TROPONINA" | fonte: REUNI p.13
     - e48ade81 v1 Documento/Bloqueia: "Inserir exame de ECG e/ou Ecocardiograma" | fonte: REUNI p.13
     - 81febf81 v2 Pergunta/Bloqueia: "HT<27 e Hb<9,0 não poderão realizar o exame" | pergunta: "O paciente tem HT < 27 e Hb < 9,0?" | bloqueia se: Sim | fonte: REUNI p.13
     - 0e5fe3fd v2 Informativa/Bloqueia: "Caso o paciente apresente alterações sugestivas de Insuficiência Renal (U>80 ou Cr>2,0 é necessária avaliação do nefrologista)" | fonte: REUNI p.13
   - Inativas:
     - 59ece322 v1 Pergunta/Bloqueia: "HT<27 e Hb<9,0 não poderão realizar o exame" | bloqueia se: Não | fonte: REUNI p.13
     - 86480e7d v1 Pergunta/Bloqueia: "Caso o paciente apresente alterações sugestivas de Insuficiência Renal (U>80 ou Cr>2,0 é necessária avaliação do nefrologista)" | bloqueia se: Não | fonte: REUNI p.13
6. **Veredito:** PARCIAL
7. **Problemas:**
   - O encaminhamento global não traz o conteúdo (História clínica, informar modo ventilatório, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo).
   - A 8de3d0b8 junta o critério (ambiente hospitalar) com um dado ("Informar o tipo de leito atual (CTI ou enfermaria)") numa pergunta Sim/Não, e o tipo de leito não é registrado.
   - A exigência condicional do nefrologista ficou só como Informativa (0e5fe3fd), que ninguém confere.
   - A 81febf81 segue o literal "HT<27 e Hb<9,0" (os dois juntos). Se a regulação lê como "ou", a regra deixa passar quem tem só um deles baixo — confirmar com a regulação.
8. **Proposta:**
   - Nova versão do encaminhamento 1ed71fe3 com o texto literal: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (História clínica, informar modo ventilatório, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo)."
   - Nova versão de 8de3d0b8 como lista "basta uma" ("nenhuma destas" bloqueia): "Onde o paciente está internado?" — "CTI" | "Enfermaria".
   - Trocar a Informativa 0e5fe3fd por uma Pergunta Sim/Não, Sim bloqueia: "O paciente tem alterações sugestivas de Insuficiência Renal (U>80 ou Cr>2,0) SEM avaliação do nefrologista?" A pergunta negada embute a condição; com regra condicional seria "se U>80 ou Cr>2,0 → Documento: avaliação do nefrologista".
   - Manter 81febf81 (v2) e as demais.

### 22. 4.1.6.2 — ARTERIOGRAFIA PERIFÉRICA (AMBULATORIAL)

1. **Seção / página:** 4.1.6.2 / p.13
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Critério: "Destina-se a pacientes em domicílio."
   - Documento: "Inserir no SER a solicitação médica em formulário de Alto Custo."
   - Documento (conteúdo obrigatório): "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (História clínica, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo)."
   - Valor laboratorial: "Inserir resultado de Hematócrito, hemoglobina, ureia e creatinina."
   - Documento/exame: "Inserir exame de Doppler (exceto para arteriografia cerebral)."
4. **Recurso SER:** Arteriografia Periférica (Ambulatorial) (tipo2 v1047)
5. **Nosso:** Arteriografia Periférica (Ambulatorial) · espelho SER: 2 A conferir
   - Ativas:
     - b2782555 v1 Documento/Bloqueia: "Inserir no SER a solicitação médica em formulário de Alto Custo" | fonte: REUNI p.13
     - ce892796 v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
     - 88b9ea2c v1 Documento/Bloqueia: "Inserir exame de Doppler (exceto para arteriografia cerebral)" | fonte: REUNI p.13
     - 309b9954 v1 Documento/Bloqueia: "Inserir resultado de Hematócrito, hemoglobina, ureia e creatinina" | fonte: REUNI p.13
     - 027f51cd v1 Pergunta/Bloqueia: "Destina-se a pacientes em domicílio" | bloqueia se: Não | fonte: REUNI p.13
6. **Veredito:** PARCIAL
7. **Problemas:**
   - O encaminhamento global não traz o conteúdo (História clínica, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo).
   - A 88b9ea2c mantém o "(exceto para arteriografia cerebral)", que não faz sentido dentro de um procedimento periférico (sobra do manual).
   - A 027f51cd é uma afirmação usada como pergunta.
8. **Proposta:**
   - Nova versão de ce892796 com o texto literal: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (História clínica, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo)."
   - Nova versão de 88b9ea2c: "Inserir exame de Doppler."
   - 027f51cd: "O paciente está em domicílio (não internado)?" — Não bloqueia.

### 23. 4.1.6.2 — ARTERIOGRAFIA CEREBRAL (AMBULATORIAL)

1. **Seção / página:** 4.1.6.2 / p.13
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Documento: "Inserir no SER a solicitação médica em formulário de Alto Custo."
   - Documento (conteúdo obrigatório): "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (História clínica, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo)."
   - Documento/exame (alternativas): "Inserir Exame de imagem (TC ou RNM de crânio)."
4. **Recurso SER:** Arteriografia Cerebral (Ambulatório) (tipo2 v1118)
5. **Nosso:** Arteriografia Cerebral (Ambulatório) · espelho SER: 3 Sem regras
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - Nenhuma regra.
   - Atenção: nesta linha o manual NÃO pede hematócrito/hemoglobina/ureia/creatinina, nem Doppler, nem "em domicílio". Não copiar as regras da arteriografia periférica.
8. **Proposta:**
   - Documento: "Inserir no SER a solicitação médica em formulário de Alto Custo."
   - Documento: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (História clínica, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo)."
   - Documento: "Inserir Exame de imagem (TC ou RNM de crânio)."

### 24. 4.1.6.2 — ARTERIOGRAFIA PERIFÉRICA (INTERNADOS)

1. **Seção / página:** 4.1.6.2 / p.13
2. **Estrutura:** SIMPLES
3. **Requisitos (manual):**
   - Critério: "Destina-se a pacientes em ambiente hospitalar."
   - Documento: "Inserir no SER a solicitação médica em formulário de Alto Custo."
   - Documento (conteúdo obrigatório): "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (História clínica, informar modo ventilatório, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo)."
   - Valor laboratorial: "Inserir resultado de Hematócrito, hemoglobina, ureia e creatinina."
   - Documento/exame: "Inserir Exame de Doppler do Membro afetado."
4. **Recurso SER:** Arteriografia Periférica (Internados) (tipo2 v1048)
5. **Nosso:** Arteriografia Periférica (Internados) · espelho SER: nenhum pedido no espelho
   - Ativas:
     - a2c6a87f v1 Pergunta/Bloqueia: "Destina-se a pacientes em ambiente hospitalar" | bloqueia se: Não | fonte: REUNI p.13
     - 098b1b90 v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
     - 2e8f285c v1 Documento/Bloqueia: "Inserir Exame de Doppler do Membro afetado" | fonte: REUNI p.13
     - 605c3d8c v1 Documento/Bloqueia: "Inserir resultado de Hematócrito, hemoglobina, ureia e creatinina" | fonte: REUNI p.13
     - b75f4dfd v1 Documento/Bloqueia: "Inserir no SER a solicitação médica em formulário de Alto Custo" | fonte: REUNI p.13
6. **Veredito:** PARCIAL
7. **Problemas:**
   - O encaminhamento global não traz o conteúdo (História clínica, informar modo ventilatório, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo).
   - A a2c6a87f é uma afirmação usada como pergunta.
8. **Proposta:**
   - Nova versão de 098b1b90 com o texto literal: "Inserir no SER o encaminhamento médico com a descrição clara e detalhada do caso (História clínica, informar modo ventilatório, tratamentos prévios efetuados, uso de medicações regulares, história de alergia a iodo)."
   - a2c6a87f: "O paciente está internado?" — Não bloqueia.

### 25. 4.2.1 — CIRURGIA BARIÁTRICA (Portaria SAS nº 492 de 31/08/2007)

1. **Seção / página:** 4.2.1 / p.14
2. **Estrutura:** INCLUSAO_EXCLUSAO + CONDICIONAL (eixos: faixa de IMC → laudo do endocrinologista; idade 16–18 → comprovação de consolidação das epífises)
3. **Requisitos (manual):**
   - Inclusão (basta um): "a. IMC > ou = 40 kg/m², sem comorbidades, que não respondem ao tratamento conservador (dieta, psicoterapia, atividade física, etc..), realizado durante pelo menos dois anos e sob orientação de equipe credenciada / habilitada como Unidade de Assistência de Alta Complexidade ao paciente portador de obesidade." | "b. IMC > ou = 40 kg/m² com comorbidades que ameaçam a vida." | "c. IMC entre 35 e 39,9 kg/m² portadores de doenças crônicas desencadeadas ou agravadas pela obesidade. Devem possuir laudo do endocrinologista informando que não há obesidade por doenças endócrinas ou que essa esteja em tratamento estabilizada."
   - Condicional (eixo IMC): o laudo do endocrinologista só é exigido no critério c.
   - Idade (dedutível) + condicional (eixo idade): "d. Idade entre 18 anos e 65 anos, com alguma das indicações acima. Se entre 16 e 18 anos, deverão apresentar comprovação de consolidação das epífises dos ossos longos."
   - Exclusão (qualquer uma barra): "a. Obesidade decorrente de doença endócrina (p. ex., Síndrome de Cushing devido à hiperplasia suprarrenal)." | "b. Incapacidade intelectual para compreender todos os aspectos do tratamento." | "c. Ausência de suporte familiar constante." | "d. Alcoolismo; dependência química e outras drogas, distúrbio psicótico grave (bulimia, inclusive) ou história recente de tentativa de suicídio."
   - "Anexar (ou descrever de forma clara e detalhada):" "Encaminhamento médico descrevendo o quadro clinico e indicando o procedimento." e "Hemograma com plaquetas; Coagulograma; Ureia e Creatinina; Glicemia de jejum; Hemoglobina glicosilada (HbAIC); Ácido úrico; T3, T4, TSH; Colesterol total, HDL e Triglicerídeos."
4. **Recurso SER:** Ambulatório 1ª vez - Cirurgia Bariátrica (Adulto) (tipo1 v1073). Também "…Cirurgia Bariátrica - Superobesidade (IMC acima 55)" (v1074), na linha seguinte.
5. **Nosso:** Ambulatório 1ª vez - Cirurgia Bariátrica (Adulto) · espelho SER: 107 A conferir
   - Ativas:
     - c56e5e49 v1 Documento/Bloqueia: "Hemograma com plaquetas; Coagulograma; Ureia e Creatinina; Glicemia de jejum; Hemoglobina glicosilada (HbAIC); Ácido úrico; T3, T4, TSH; Colesterol total, HDL e Triglicerídeos" | fonte: REUNI p.14
     - 1c6bf33e v1 Pergunta/Bloqueia: "a. IMC > ou = 40 kg/m², sem comorbidades, que não respondem ao tratamento conservador (dieta, psicoterapia, atividade física, etc..), realizado durante pelo menos dois anos e sob orientação de equipe credenciada / habilitada como Unidade de Assistência de Alta Complexidade ao paciente portador de obesidade. b. IMC > ou = 40 kg/m² com comorbidades que ameaçam a vida. c. IMC entre 35 e 39,9 kg/m² portadores de doenças crônicas desencadeadas ou agravadas pela obesidade. Devem possuir laudo do endocrinologista informando que não há obesidade por doenças endócrinas ou que essa esteja em tratamento estabilizada. d. Idade entre 18 anos e 65 anos, com alguma das" | pergunta: "a. IMC > ou = 40 kg/m², sem comorbidades, que não respondem ao tratamento conservador (dieta, psicoterapia, atividade física, etc..), realizado durante pelo menos dois anos e sob orientação de equipe credenciada / habilitada como Unidade de Assistência de Alta Complexidade ao paciente portador de obesidade. b. IMC > ou = 40 kg/m² com comorbidades que ameaçam a vida. c. IMC entre 35 e 39,9 kg/m² portadores de doenças crônicas desencadeadas ou agravadas pela obesidade. Devem possuir laudo do endoc" | bloqueia se: Não | fonte: REUNI p.14
     - e5e18a89 v1 Documento/Bloqueia: "Encaminhamento médico descrevendo o quadro clinico e indicando o procedimento" | fonte: REUNI p.14
     - 1b07c5ca v1 Documento/Bloqueia: "Encaminhamento médico com a descrição clara e detalhada do caso, inserido no SER." | fonte: CRECE/REUNI — requisito global
     - 5c339564 v2 Documento/Bloqueia: "acima. Se entre 16 e 18 anos, deverão apresentar comprovação de consolidação das epífises dos ossos longos" | idade 16–18 | NÃO obrigatório | fonte: REUNI p.14
     - b7a48bb0 v2 Pergunta/Bloqueia: "a. Obesidade decorrente de doença endócrina (p. ex., Síndrome de Cushing devido à hiperplasia suprarrenal). b. Incapacidade intelectual para compreender todos os aspectos do tratamento. c. Ausência de suporte familiar constante. d. Alcoolismo; dependência química e outras drogas, distúrbio psicótico grave (bulimia, inclusive) ou história recente de tentativa de suicídio. Anexar (ou descrever de forma clara e detalhada)" | pergunta: "Algum destes critérios de exclusão se aplica ao paciente?" | bloqueia se: Sim | fonte: REUNI p.14
   - Inativas:
     - e7e83329 v1 Pergunta/Bloqueia: "Conforme preconiza a Portaria SAS n° 492 de 31/08/2007, abaixo" | bloqueia se: Não | fonte: REUNI p.14
     - 4171219c v1 Documento/Bloqueia: "a. Obesidade decorrente de doença endócrina (p. ex., Síndrome de Cushing devido à hiperplasia suprarrenal). b. Incapacidade intelectual para compreender todos os aspectos do tratamento. c. Ausência de suporte familiar constante. d. Alcoolismo; dependência química e outras drogas, distúrbio psicótico grave (bulimia, inclusive) ou história recente de tentativa de suicídio. Anexar (ou descrever de forma clara e detalhada)" | fonte: REUNI p.14
     - a938e852 v1 Documento/Bloqueia: "acima. Se entre 16 e 18 anos, deverão apresentar comprovação de consolidação das epífises dos ossos longos" | idade 16–18 | fonte: REUNI p.14
6. **Veredito:** ERRADO
7. **Problemas:**
   - A 1c6bf33e (Pergunta Sim/Não, Não bloqueia) despeja os critérios a–d num texto só, e cortado: a pergunta para nos 500 caracteres ("…Devem possuir laudo do endoc") e a descrição termina em "d. Idade entre 18 anos e 65 anos, com alguma das". O solicitante responde Sim/Não a um parágrafo truncado, quando o certo é uma lista "basta uma".
   - A 5c339564 (Documento v2, não obrigatório) começa com o caco "acima." (resto de "com alguma das indicações acima.") e tem idade 16–18 preenchida. Só que o avaliador ignora idade em Documento (AvaliadorElegibilidade: Documental → Indefinido, sem olhar idade), então a caixinha das epífises aparece para TODO paciente.
   - A faixa de idade 18–65 não é deduzida (não existe Dedutível): um paciente de 70 anos passa.
   - Encaminhamento em duplicidade: 1b07c5ca (global) e e5e18a89 (texto do manual) viram duas caixinhas para a mesma coisa.
   - A b7a48bb0 (exclusão, Sim bloqueia) leva no fim o caco "Anexar (ou descrever de forma clara e detalhada)" e junta os 4 critérios num Sim/Não. Funciona, mas lê mal.
   - O laudo do endocrinologista (só no critério c) não está em lugar nenhum.
   - O manual aceita "Anexar (ou descrever de forma clara e detalhada)": as caixinhas obrigatórias de exames são mais duras que o manual.
8. **Proposta:**
   - Inativar a 1c6bf33e. Criar Pergunta lista "basta uma" ("nenhuma destas" bloqueia): "O paciente se enquadra em ao menos um dos critérios de inclusão (Portaria SAS nº 492/2007)?" com as opções a, b e c literais, e a c incluindo "…com laudo do endocrinologista informando que não há obesidade por doenças endócrinas ou que essa esteja em tratamento estabilizada" (assim o laudo entra sem condição; como caixinha própria só com regra condicional: se c → Documento).
   - Dedutível, Bloqueia, idade_min_anos 16 e idade_max_anos 65: "Idade entre 18 anos e 65 anos (entre 16 e 18 anos, com comprovação de consolidação das epífises dos ossos longos)."
   - Inativar a 5c339564. No lugar, Pergunta Sim/Não, Sim bloqueia: "O paciente tem menos de 18 anos e NÃO apresenta comprovação de consolidação das epífises dos ossos longos?" (com regra condicional seria: se idade < 18 → Documento).
   - Nova versão da b7a48bb0 como lista "basta uma" com Sim bloqueando (marcou alguma = barra): opções a–d literais, sem o caco.
   - Inativar o global 1b07c5ca e ficar com a e5e18a89 (texto do manual).
   - c56e5e49: ok; considerar severidade Ressalva, já que o manual aceita "descrever".

### 26. 4.2.1 — CIRURGIA BARIÁTRICA — 2º recurso do SER (mesma linha do manual)

1. **Seção / página:** 4.2.1 / p.14
2. **Estrutura:** INCLUSAO_EXCLUSAO + CONDICIONAL (igual à linha anterior)
3. **Requisitos (manual):**
   - Os mesmos da Cirurgia Bariátrica: IMC > 55 cai nos critérios a/b.
4. **Recurso SER:** Ambulatório 1ª vez - Cirurgia Bariátrica - Superobesidade (IMC acima 55) (tipo1 v1074)
5. **Nosso:** Ambulatório 1ª vez - Cirurgia Bariátrica - Superobesidade (IMC acima 55) · espelho SER: 27 Sem regras
   - Ativas: nenhuma
6. **Veredito:** AUSENTE
7. **Problemas:**
   - 0 regras. O manual tem uma linha só para a bariátrica; o SER separa Adulto × Superobesidade (IMC > 55), e só o Adulto recebeu as regras.
8. **Proposta:**
   - Copiar o conjunto CORRIGIDO da Bariátrica (Adulto), depois de confirmar com a regulação que a Portaria vale igual para a superobesidade.
