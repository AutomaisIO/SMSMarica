# Confronto REUNI (rede geral do SER, ramo NAO_AE) × regras em produção — trecho D (p.35–44)

> Manual: `REUNI_MANUAL DO SOLICITANTE_V3 29.12.20222 - Copia.pdf`, páginas 35–44 (4.9 a 4.18; a tabela de Hematologia Pediátrica do 4.8.2, que também está na p.35, entrou como fronteira).
> Produção: exportação de 09/10/2026 (`regras.json`, `ser_recursos.json`, `analise_espelho_por_procedimento.json`). Trabalho só de leitura.
> **Página = índice do PDF**, que é o que a coluna `fonte` usa. O rodapé impresso diverge a partir da p.37: as páginas 37 e 40 do PDF não têm número, a p.38 do PDF imprime "37", a 39 imprime "38", a 41 imprime "39" e a 44 imprime "42".
> Tabelas conferidas visualmente (páginas renderizadas com PyMuPDF), não só pelo texto `-layout`.

## Resumo

- **41 recursos/linhas** analisados. Por veredito: **AUSENTE** 24, **SEM_RECURSO_NO_SER** 8, **SO_ORIENTACAO** 5, **COBERTO** 3, **ERRADO** 1.
- **Produção quase vazia neste trecho.** Só há regras de fonte p.35 (Hematologia, no procedimento errado), p.36 (Genética) e p.37 (Hormonização). **Nada da p.38 à p.44 existe em produção** — nenhuma cintilografia, PET-CT, angiotomografia, RM, EEG, elastografia, teste do suor ou transplante tem regra. Esses recursos somam **~155 pedidos no espelho com veredito "Sem regras"** (EEG adulto 43, cintilografia do miocárdio 39, EEG infantil 21, elastografia 20, cintilografia de ossos 7, renal 6, tireoide 5, Pré-Angioplastia 5, PET-CT 4...).
- **Os fragmentos corrompidos da extração antiga (p.37–38) NÃO sobreviveram.** Conferi `regras.json` por "SOS O TEIRO", "MÃO OU RIAL", "OIDE ÃO", "CIA R", "TIL) Anexar", "RPO DE", "CORPO INT": zero ocorrências. Motivo: no CSV antigo, todas essas linhas ficaram `SEM_PAR` e nunca foram importadas. O risco é **se alguém reimportar o CSV**: ele traz idade mín. 18 no bloco de preparo da p.39 (texto do Teste do Suor, que é exame pediátrico), idade mín. 5 da RM sem sedação, tudo pendurado na "Cintilografia de Corpo Inteiro", as indicações do PET-CT como anexos, e perdeu inteira a linha do Transplante RENAL adulto.
- **Erro grave em produção (fonte p.35/p.33):** a tabela de **Hematologia PEDIÁTRICA** foi lida como se fosse a adulta. Resultado: a regra ATIVA `cc8f8ccf` cobra **Teste do Pezinho** (Documento/Bloqueia) de todo pedido de **Hematologia Adulto**, e a Hematologia Infantil ficou **sem nenhuma regra**.
- **Coberto de verdade:** Genética Pediátrica, Genética Adulto e Hormonização (idade ≥16; o texto `-layout` encosta o "16 anos" na linha da Zika, mas produção acertou).
- **Barreiras escondidas sob "preparo":** a Elastografia (4.16) traz idade ≥18, indicação (doença hepática crônica), exclusão de doença aguda e contraindicações (gravidez, enzimas > 400). O Teste do Suor exige "somente após o segundo mês de vida" — e o modelo só tem idade em anos.
- **Detalhe do motor que muda propostas:** regra Informativa é descartada da análise (`AnaliseRegrasEspelhoService`: `avaliaveis = regras.Where(r => r.Tipo != Informativa)`). Cadastrar só o preparo como Informativa **não tira** o pedido de "Sem regras".

## Tabela-resumo

| id | seção | pág. | recurso no manual | recurso no SER (NAO_AE) | estrutura | veredito | regras ativas | pedidos espelho |
|---|---|---|---|---|---|---|---|---|
| D01 | 4.8.2 Hematologia Pediátrica | 35 | HEMATOLOGIA PEDIÁTRICA — linhas ANEMIA / DISTÚRBIOS DOS LEUCÓCITOS (LEUCOPENIA) / DISTÚRBIOS HEMORRÁGICOS | Ambulatório 1ª vez - Hematologia (Infantil) | CONDICIONAL | **ERRADO** | 0 no Infantil (3 da p.35 no Adulto) | Hematologia (Infantil): 2 (Sem regras); Hematologia (Adulto): 1 (A conferir) |
| D02 | 4.9 Cirurgia Crânio-Maxilo-Facial | 35 | CRÂNIO-MAXILO-FACIAL (ADULTO) | Ambulatório 1ª vez - Cranio Maxilo Facial (Adulto) | SIMPLES | **AUSENTE** | 0 | 0 |
| D03 | 4.9 Cirurgia Crânio-Maxilo-Facial | 35 | CRÂNIO-MAXILO-FACIAL (INFANTIL) | Ambulatório 1ª vez - Cranio Maxilo Facial (Infantil) | SIMPLES | **AUSENTE** | 0 | 0 |
| D04 | 4.10 Cirurgia Plástica | 36 | CIRURGIA PLÁSTICA REPARADORA DE MAMA (PÓS-MASTECTOMIA ONCOLÓGICA) | Ambulatório 1ª vez em Cirurgia Plástica Reparadora - Mama (Oncologia) | SIMPLES | **AUSENTE** | 0 | 0 |
| D05 | 4.11 Genética Médica | 36 | GENÉTICA MÉDICA PEDIÁTRICA | Ambulatório 1ª vez em Genética Médica - Pediatria | LISTA_BASTA_UM | **COBERTO** | 3 | 7 (A conferir) |
| D06 | 4.11 Genética Médica | 36–37 | GENÉTICA MÉDICA ADULTO | Ambulatório 1ª vez em Genética Médica - Adulto | INCLUSAO_EXCLUSAO | **COBERTO** | 9 | 10 (A conferir) |
| D07 | 4.12 Outras Especialidades | 37 | AVALIAÇÃO DIAGNÓSTICA INFECÇÃO CONGÊNITA ZIKA/ STORCH/CHIKUNGUNHA | Avaliação diagnóstica infecção congênita Zika/Storch/Oropuche | SIMPLES | **AUSENTE** | 0 | 0 |
| D08 | 4.12 Outras Especialidades | 37 | HORMONIZAÇÃO SAÚDE - TRANS | Ambulatório de 1ª vez - Hormonização - Saúde - Trans | SIMPLES | **COBERTO** | 2 | 8 (A conferir — pelo Documento de encaminhamento) |
| D09 | 4.12 Outras Especialidades | 37 | CENTRO DE REFERÊNCIA MULTIDISCIPLINAR PÓS-COVID 19 | não achado | SIMPLES | **SEM_RECURSO_NO_SER** | 0 | — |
| D10 | 4.13 Transplante | 37 | CARDÍACO | Ambulatório de 1ª Vez - Pré-Transplante Cardíaco | LISTA_BASTA_UM | **AUSENTE** | 0 | 1 (Sem regras) |
| D11 | 4.13 Transplante | 37 | RENAL (INFANTIL) | Ambulatório de 1ª Vez - Transplante Renal (Infantil) | SIMPLES | **AUSENTE** | 0 | 0 |
| D12 | 4.13 Transplante | 37 | FÍGADO (INFANTIL) | Ambulatório de 1ª Vez - Transplante de Fígado (Infantil) | SIMPLES | **AUSENTE** | 0 | 0 |
| D13 | 4.13 Transplante | 38 | RENAL (adulto implícito; a linha diz só 'RENAL') | Ambulatório de 1ª Vez - Transplante Renal (Adulto) | SIMPLES | **AUSENTE** | 0 | 0 |
| D14 | 4.13 Transplante | 38 | PULMONAR | Ambulatório de 1ª Vez - Pré-Transplante de Pulmão | INCLUSAO_EXCLUSAO | **AUSENTE** | 0 | 0 |
| D15 | 4.14 Cintilografias | 38 | CINTILOGRAFIA DO MIOCÁRDIO EM REPOUSO E ESTRESSE AMBULATORIAL | Cintilografia do Miocárdio em REPOUSO e/ou STRESS (Ambulatorial) | SIMPLES | **AUSENTE** | 0 | 39 (Sem regras) |
| D16 | 4.14 Cintilografias | 38 | CINTILOGRAFIA DO MIOCÁRDIO (PACIENTES INTERNADOS) | Cintilografia do Miocardio (Internados) | SIMPLES | **AUSENTE** | 0 | 0 |
| D17 | 4.14 Cintilografias | 38 | CINTILOGRAFIA RENAL ESTÁTICA E/OU DINÂMICA - DMSA (AMBULATORIAL) | Cintilografia Renal Estática e/ou Dinâmica (Ambulatorial) | SIMPLES | **AUSENTE** | 0 | 6 (Sem regras) |
| D18 | 4.14 Cintilografias | 38 | CINTILOGRAFIA DE OSSOS COM OU SEM FLUXO SANGUÍNEO - CORPO INTEIRO (AMBULATORIAL) | Cintilografia de Ossos c/ ou s/ Fluxo Sanguíneo - Corpo Inteiro (Ambulatorial) | SIMPLES | **AUSENTE** | 0 | 7 (Sem regras) |
| D19 | 4.14 Cintilografias | 38 | CINTILOGRAFIA DE PULMÃO POR VENTILAÇÃO E/OU PERFUSÃO (AMBULATORIAL) | Cintilografia de Pulmão por VENTILAÇÃO e/ou PERFUSÃO (Ambulatorial) | SIMPLES | **AUSENTE** | 0 | 0 |
| D20 | 4.14 Cintilografias | 38 | CINTILOGRAFIA DE TIREOIDE COM OU SEM CAPTAÇÃO (AMBULATORIAL) | Cintilografia de Tireoide c/ ou s/ Captação (Ambulatorial) | SIMPLES | **AUSENTE** | 0 | 5 (Sem regras) |
| D21 | 4.14 Cintilografias | 38 | CINTILOGRAFIA DE CORPO INTEIRO PESQUISA DE NEOPLASIAS (AMBULATORIAL) | não achado | SIMPLES | **SEM_RECURSO_NO_SER** | 0 | — |
| D22 | 4.14 Cintilografias | 39 | LINFOCINTILOGRAFIA (AMBULATORIAL) | Linfocintilografia (Ambulatorial) | SIMPLES | **AUSENTE** | 0 | 0 |
| D23 | 4.14 Cintilografias | 39 | CINTILOGRAFIA DE FÍGADO E VIAS BILIARES (AMBULATORIAL) | não achado | SIMPLES | **SEM_RECURSO_NO_SER** | 0 | — |
| D24 | 4.14 Cintilografias | 39 | CINTILOGRAFIA DE ARTICULAÇÕES / EXTREMIDADES / ÓSSEA (AMBULATORIAL) | não achado | SIMPLES | **SEM_RECURSO_NO_SER** | 0 | — |
| D25 | 4.14 Cintilografias | 39 | CINTILOGRAFIA DE CAMPOS PULMONARES E ESVAZIAMENTO GÁSTRICO (AMBULATORIAL) | não achado | SIMPLES | **SEM_RECURSO_NO_SER** | 0 | — |
| D26 | 4.14 Cintilografias | 39 | CINTILOGRAFIA DE CÂMARAS CARDÍACAS | não achado | SIMPLES | **SEM_RECURSO_NO_SER** | 0 | — |
| D27 | 4.15 Teste do Suor – Dosagem de Cloreto no Suor | 39 | TESTE DO SUOR – DOSAGEM DE CLORETO NO SUOR | Dosagem de cloreto no suor | SO_ORIENTACAO | **SO_ORIENTACAO** | 0 | 0 |
| D28 | 4.16 Elastografia | 40 | ELASTOGRAFIA | Elastografia Hepática Transitória | INCLUSAO_EXCLUSAO | **AUSENTE** | 0 | 20 (Sem regras) |
| D29 | 4.17 Outros Exames | 40 | TOMOGRAFIA POR EMISSÃO DE PÓSITRONS (PET-CT) | Tomografia por Emissão de Pósitrons (PET-CT) | LISTA_BASTA_UM | **AUSENTE** | 0 | 4 (Sem regras) |
| D30 | 4.17 Outros Exames | 40 | ANGIOTOMOGRAFIA | 2 recursos (exceto Coronária / Coronariana) | SIMPLES | **AUSENTE** | 0 | 0 |
| D31 | 4.17 Outros Exames | 40 | RESSONÂNCIA MAGNÉTICA SEM SEDAÇÃO (INTERNADOS) | não achado | INCLUSAO_EXCLUSAO | **SEM_RECURSO_NO_SER** | 0 | — |
| D32 | 4.17 Outros Exames | 41 | RESSONÂNCIA MAGNÉTICA COM SEDAÇÃO (INTERNADOS) | Ressonância Magnética - COM SEDAÇÃO (Internados) | INCLUSAO_EXCLUSAO | **AUSENTE** | 0 | 0 |
| D33 | 4.17 Outros Exames | 41 | ELETROENCEFALOGRAMA (ADULTO) | EEG Simples Adulto | INCLUSAO_EXCLUSAO | **AUSENTE** | 0 | 43 (Sem regras) |
| D34 | 4.17 Outros Exames | 41 | ELETROENCEFALOGRAMA (INFANTIL) | EEG Simples Infantil | INCLUSAO_EXCLUSAO | **AUSENTE** | 0 | 21 (Sem regras) |
| D35 | 4.17 Outros Exames | 41 | VÍDEO EEG (ADULTO) | Vídeo EEG Adulto - (Ambulatorial) | INCLUSAO_EXCLUSAO | **AUSENTE** | 0 | 2 (Sem regras) |
| D36 | 4.17 Outros Exames | 41 | VÍDEO EEG (INFANTIL) | Vídeo EEG Pedíatrico - (Ambulatorial) | INCLUSAO_EXCLUSAO | **AUSENTE** | 0 | 0 |
| D37 | 4.18.1 Preparo prévio para Angioplastia | 41–42 | ANGIOPLASTIA (preparo) | não achado | SO_ORIENTACAO | **SO_ORIENTACAO** | 0 | Pré Angioplastia: 5 (Sem regras) |
| D38 | 4.18.2 Preparo prévio para Arteriografia de membros | 42 | ARTERIOGRAFIA DE MEMBROS (preparo) | Arteriografia Periférica (Ambulatorial) | SO_ORIENTACAO | **SO_ORIENTACAO** | 5 + 5 (p.13) | Ambulatorial: 2 (A conferir) |
| D39 | 4.18.3 Preparo prévio para Cateterismo Cardíaco | 43 | CATETERISMO CARDÍACO (preparo) | Cateterismo Cardíaco (Ambulatorial) | SO_ORIENTACAO | **SO_ORIENTACAO** | 5 + 8 (p.13) | Ambulatorial: 6 (A conferir) |
| D40 | 4.18.4 Preparo prévio para Teste Cardiopulmonar de Exercício | 43 | TESTE CARDIOPULMONAR DE EXERCÍCIO (ERGOESPIROMETRIA) (preparo) | não achado | SO_ORIENTACAO | **SEM_RECURSO_NO_SER** | 0 | — |
| D41 | 4.18.5 Preparo prévio para Eletroencefalograma | 44 | ELETROENCEFALOGRAMA (preparo) | EEG Simples Adulto | SO_ORIENTACAO | **SO_ORIENTACAO** | 0 | ver D33/D34 |

## Regras de produção com fonte "REUNI p.35"…"p.44" — o que está fora do lugar

| fonte | qtd (ativas) | procedimento | situação |
|---|---|---|---|
| REUNI p.35 | 6 (1) + 1 lista mista "p.33 · p.35" (ativa) | Ambulatório 1ª vez - Hematologia (**Adulto**) | **Procedimento errado.** A p.35 é a tabela PEDIÁTRICA. `cf76f512` (Coagulograma e Plaquetas, ativa) tem texto idêntico à linha adulta da p.34, então o conteúdo vale para o adulto — só a fonte está errada (deveria ser p.34). A lista `3e5feedc` idem. Inativas: `8858e168`, `c6957ec1`, `017df45d` ("/ Agendamento", fragmento), `b11b0c7b`, `864c1192`. |
| REUNI p.33 (mas é p.35) | 1 (1) | Ambulatório 1ª vez - Hematologia (**Adulto**) | **ERRADO e ativo:** `cc8f8ccf` Documento/Bloqueia "Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD" — só existe na tabela pediátrica. As demais cópias "p.33" vindas da tabela pediátrica têm texto igual ao adulto (ex.: "vitamina B12" com espaço é a grafia da p.35; a p.33 escreve "vitaminaB12") e são inofensivas. |
| REUNI p.36 | 26 (10) | Genética Médica - Pediatria / - Adulto | Procedimentos certos; ativas fiéis ao manual. 16 inativas são a fragmentação antiga, já trocada por listas. |
| REUNI p.37 | 3 (1) | Hormonização - Saúde - Trans | Certo (idade ≥16). 2 inativas são fragmentos. |
| REUNI p.38–p.44 | 0 | — | Nada em produção. |

Resíduos de pareamento (origens SER **desligadas** apontando para procedimentos deste trecho; não afetam a análise, que só usa origem ativa, mas inflam a coluna "regras ativas" do `ser_catalogo_resumo.txt`): Amiloidose Cardíaca → Hematologia (Adulto) (o resumo mostra "10 regras ativas" para Amiloidose); Arritimias (Infantil) → Hematologia (Infantil); Hipertensão Arterial Resistente (Adulto) → Genética Pediatria; Avaliação de Cardiopatia Congênita Pediátrica (Internados) → Genética Adulto.

## Recursos do SER (NAO_AE) da mesma família SEM linha neste trecho do manual

Não confrontados (o manual não fala deles aqui); listados para não serem confundidos com os pareamentos acima:

- Cintilografias: Dacriocintilografia; Glândulas Salivares; Paratireóides (1 pedido 'Sem regras'); Perfusão Cerebral; Segmento Ósseo c/ Gálio 67; PCI para Neoplasia de Tireoide; Sistema Digestivo; 'Cintilografias (Internados)'.
- Transplantes: Transplante de Fígado (Adulto); Pré-Transplante de Córnea (17 pedidos 'Sem regras'); Pré-Transplante de Medula Óssea Alogênico (2) e Autólogo (1); pré Transplante Cardíaco Infantil (ver D10).
- Imagem: Ressonância Magnética - COM SEDAÇÃO (Ambulatorial) — 68 pedidos 'Sem regras'; Tomografia Computadorizada - COM SEDAÇÃO (2); Arteriografia Cerebral (3).
- Ecocardiograma Transesofágico e Ecocardiograma de Estresse existem no catálogo, mas **não aparecem nas p.35–44** (o REUNI só cita ecocardiograma como exame a anexar).

## Recursos CONDICIONAIS / não representáveis hoje

- **D01 Hematologia Pediátrica** — eixo "tipo de alteração" decide quais exames anexar; exclusão "menor de 1 ano, salvo suspeita de doença hemorrágica hereditária ou hemoglobinopatia". Só representável com pergunta combinada.
- **D27 Teste do Suor** — "somente após o segundo mês de vida": idade em meses não existe no modelo (só pergunta).
- **D31 RM sem sedação (internados)** — sem recurso; pendurar nas RMs por região exigiria regra condicionada ao caráter internado × ambulatorial.
- **D15 Cintilografia do miocárdio** — Teste ergométrico literal obrigatório; possivelmente condicional a "estresse em esteira".
- **D09 Pós-COVID / D13 Transplante Renal** — itens que pressupõem internação/diálise (resolvíveis com pergunta).
- Transplantes (D11–D14): "Anexar (ou descrever)" = anexo OU texto — Documento/Bloqueia seria mais rígido que o manual; usar Pergunta.

## Por recurso

### D01 — HEMATOLOGIA PEDIÁTRICA — linhas ANEMIA / DISTÚRBIOS DOS LEUCÓCITOS (LEUCOPENIA) / DISTÚRBIOS HEMORRÁGICOS

1. **Seção / página:** 4.8.2 Hematologia Pediátrica (fronteira com o trecho anterior) — p.35 (exclusões do 4.8.2 na p.34)
2. **Estrutura:** CONDICIONAL (eixo = tipo de alteração hematológica) + INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - *documento* — Encaminhamento médico descrevendo de forma clara e detalhada o caso (nas 3 linhas)
   - *exame a anexar (eixo ANEMIA)* — 02 hemogramas (intervalo > ou = 15 dias); Eletroforese de proteínas, ferro sérico, ferritina, saturação da transferrina, vitamina B12, ácido fólico
   - *exame a anexar condicional (eixo ANEMIA)* — Eletroforese de Hb (em suspeita de hemoglobinopatias)
   - *exame a anexar (eixo ANEMIA; sem 'se houver', mas só existe se o pezinho veio alterado)* — Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD
   - *critério clínico (ANEMIA, basta um)* — Hemoglobina sérica < 9 g/dl, de padrão macrocítico; Anemia e icterícia com aumento de bilirrubina indireta; Anemia normocítica e normocrômica EXCLUÍDAS doenças crônicas como Insuficiência Renal, diabetes mellitus, colagenoses e hepatopatias
   - *exame a anexar (eixo LEUCOPENIA)* — 02 hemogramas recentes (intervalo mínimo de 1 mês)
   - *critério clínico (LEUCOPENIA, basta um)* — Leucometria global < ou = 3000/mm³; Neutrófilos < ou = 1200/mm³; Atipias linfocitárias na ausência de quadro viral agudo; Esplenomegalia (SEM o qualificador do adulto 'depois de afastada doença hepática')
   - *exame a anexar (eixo HEMORRÁGICOS)* — Coagulograma e Plaquetas (2 exames diferentes)
   - *critério clínico (HEMORRÁGICOS, basta um)* — Síndromes hemorrágicas: hematomas, equimoses, epistaxes, metrorragia (...) com plaquetas < 75.000 e/ou TAP ou PTT alterados; Alterações no coagulograma em exames subsequentes
   - *exclusão (p.34, contexto)* — Menores de 1 ano de idade, salvo com suspeitas de doenças hemorrágicas hereditárias ou hemoglobinopatias; Anemia hipocrômica sem investigação clínica de perda de sangue (...) e sem dosagem de ferro sérico, transferrina e ferritina sérica
4. **Recurso no SER:** Ambulatório 1ª vez - Hematologia (Infantil) [NAO_AE tipo1 v1078]
5. **Nosso:** Ambulatório 1ª vez - Hematologia (Infantil) — 0 regras. O conteúdo da p.35 foi parar em 'Ambulatório 1ª vez - Hematologia (Adulto)'.
   - Ativas:
     - Hematologia (Infantil): nenhuma
     - Hematologia (Adulto) cc8f8ccf [ATIVA v1 Documento/Bloqueia] 'Teste do Pezinho com alterações para hemoglobinopatias, Deficiência de G6PD' — fonte 'REUNI p.33', mas o texto só existe na tabela PEDIÁTRICA da p.35
     - Hematologia (Adulto) cf76f512 [ATIVA v1 Documento/Bloqueia] 'Coagulograma e Plaquetas (2 exames diferentes)' — fonte 'REUNI p.35' (texto idêntico à linha adulta da p.34)
     - Hematologia (Adulto) 3e5feedc [ATIVA v1 Pergunta lista basta-uma/Bloqueia, 9 opções] — fonte 'REUNI p.33 · REUNI p.35'
   - Inativas:
     - Hematologia (Adulto), fonte p.35: 8858e168 'Inserir no SER' (Documento); c6957ec1 'Encaminhamento médico...' (Documento); 017df45d '/ Agendamento' (Pergunta — fragmento de 'Critérios de Inclusão/ Agendamento'); b11b0c7b 'Síndromes hemorrágicas...' (Pergunta); 864c1192 'Alterações no coagulograma...' (Pergunta)
   - Pedidos no espelho: Hematologia (Infantil): 2 (Sem regras); Hematologia (Adulto): 1 (A conferir)
6. **Veredito:** **ERRADO**
7. **Problemas:**
   - Regra ATIVA errada: cc8f8ccf (Documento/Bloqueia 'Teste do Pezinho...') está no procedimento ADULTO. A tabela adulta (p.33) não pede Teste do Pezinho; ele só aparece na tabela pediátrica (p.35). Todo pedido adulto de Hematologia fica 'A conferir' cobrando um exame neonatal — quem confere pode devolver por um requisito que não existe.
   - Hematologia (Infantil) não tem NENHUMA regra (2 pedidos no espelho com veredito 'Sem regras'), embora o manual tenha tabela própria e exclusões próprias (4.8.2).
   - A extração antiga leu a tabela pediátrica como continuação da adulta: ANEMIA/LEUCÓCITOS pediátricos viraram cópias 'p.33' e DISTÚRBIOS HEMORRÁGICOS virou 'p.35', tudo pareado com Hematologia (Adulto). A linha adulta de Distúrbios Hemorrágicos (p.34) nunca foi lida; o texto da p.35 é idêntico, então cf76f512 vale para o adulto, mas com fonte errada.
   - Achatamento do eixo: a tabela é condicional pelo tipo de alteração, mas os documentos das 3 linhas viraram Documento/Bloqueia somados com E (2 hemogramas ≥15 dias E 2 hemogramas ≥1 mês E coagulograma+plaquetas E 'Eletroforese de Hb (em suspeita...)'). Um pedido de anemia fica cobrado por coagulograma e vice-versa. (O Adulto é do trecho p.33–34; o mesmo erro se repetiria se o Infantil fosse cadastrado do mesmo jeito.)
8. **Proposta (modelo atual):**
   - Hematologia (Adulto): desativar cc8f8ccf (Teste do Pezinho); nova versão de cf76f512 com fonte 'REUNI p.34'; nova versão da lista 3e5feedc com fonte 'REUNI p.33 · REUNI p.34'.
   - Hematologia (Infantil): Documento/Bloqueia encaminhamento (requisito global).
   - Hematologia (Infantil): Pergunta lista 'basta uma'/Bloqueia com as 9 condições da tabela pediátrica ('Esplenomegalia' sem o qualificador do adulto).
   - Hematologia (Infantil): Pergunta Sim/Não/Ressalva 'Anexou os exames da alteração marcada? (anemia: 2 hemogramas com intervalo ≥15 dias + eletroforese de proteínas, ferro, ferritina, sat. transferrina, B12, ácido fólico; leucopenia: 2 hemogramas com intervalo ≥1 mês; hemorrágico: coagulograma e plaquetas)' — Não = ressalva, em vez de 3 Documentos/Bloqueia somados.
   - Hematologia (Infantil): Informativa 'Eletroforese de Hb em suspeita de hemoglobinopatia; Teste do Pezinho quando alterado para hemoglobinopatias/G6PD'.
   - Exclusões 4.8.2 (p.34): Pergunta Sim-bloqueia 'Menor de 1 ano SEM suspeita de doença hemorrágica hereditária ou hemoglobinopatia?' (a exceção vai dentro do texto); Pergunta Sim-bloqueia 'Anemia hipocrômica sem investigação de perda de sangue e sem dosagem de ferro sérico, transferrina e ferritina?'.
   - *Exigiria regra condicional:* Documentos por eixo (alteração marcada → exames exigidos) e 'idade < 1 ano E sem suspeita X' como Dedutível+Pergunta. Hoje só dá com pergunta combinada.

### D02 — CRÂNIO-MAXILO-FACIAL (ADULTO)

1. **Seção / página:** 4.9 Cirurgia Crânio-Maxilo-Facial — p.35
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER o encaminhamento médico descrevendo de forma clara e detalhada
   - *exame a anexar (obrigatório)* — apresentando imagem do Rx PA e Perfil
   - *exame opcional* — TC (se possível)
4. **Recurso no SER:** Ambulatório 1ª vez - Cranio Maxilo Facial (Adulto) [NAO_AE tipo1 v1062]
5. **Nosso:** Ambulatório 1ª vez - Cranio Maxilo Facial (Adulto) — 0 regras (há 2 procedimentos homônimos; só um com origem SER ativa)
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra, nem o requisito global de encaminhamento.
   - A extração antiga não leu esta tabela (da p.35 só saiu hematologia).
8. **Proposta (modelo atual):**
   - Documento/Bloqueia encaminhamento (requisito global).
   - Documento/Bloqueia 'Imagem do Rx PA e Perfil'.
   - Informativa 'TC, se possível' (Informativa não entra na análise; serve de orientação).
   - *Exigiria regra condicional:* nada

### D03 — CRÂNIO-MAXILO-FACIAL (INFANTIL)

1. **Seção / página:** 4.9 Cirurgia Crânio-Maxilo-Facial — p.35
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER o encaminhamento médico descrevendo de forma clara e detalhada
   - *exame a anexar (obrigatório)* — apresentando imagem do Rx PA e Perfil
   - *exame opcional* — TC (se possível)
4. **Recurso no SER:** Ambulatório 1ª vez - Cranio Maxilo Facial (Infantil) [NAO_AE tipo1 v1061]
5. **Nosso:** Ambulatório 1ª vez - Cranio Maxilo Facial (Infantil) — 0 regras (há 2 procedimentos homônimos; só um com origem SER ativa)
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra, nem o requisito global de encaminhamento.
   - A extração antiga não leu esta tabela (da p.35 só saiu hematologia).
8. **Proposta (modelo atual):**
   - Documento/Bloqueia encaminhamento (requisito global).
   - Documento/Bloqueia 'Imagem do Rx PA e Perfil'.
   - Informativa 'TC, se possível' (Informativa não entra na análise; serve de orientação).
   - *Exigiria regra condicional:* nada

### D04 — CIRURGIA PLÁSTICA REPARADORA DE MAMA (PÓS-MASTECTOMIA ONCOLÓGICA)

1. **Seção / página:** 4.10 Cirurgia Plástica — p.36
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *conteúdo do encaminhamento* — encaminhamento médico com indicação do local e data da realização da mastectomia por doença oncológica
   - *documento* — liberação para a realização do procedimento de cirurgia reparadora (o manual não diz quem libera)
   - *critério clínico (implícito)* — mastectomia por doença oncológica já realizada
4. **Recurso no SER:** Ambulatório 1ª vez em Cirurgia Plástica Reparadora - Mama (Oncologia) [NAO_AE tipo1 v1063] — formulário do SER já tem 'Paciente já realizou cirurgia oncológica?' e datas de biópsia; lista de CID restrita (assinatura 0|21|0)
5. **Nosso:** Ambulatório 1ª vez em Cirurgia Plástica Reparadora - Mama (Oncologia) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra. A extração antiga leu a linha (Documental) mas ficou SEM_PAR e não foi importada.
8. **Proposta (modelo atual):**
   - Pergunta Sim/Não/Bloqueia (Não bloqueia) 'Mastectomia por doença oncológica já realizada, com local e data informados no encaminhamento?'.
   - Documento/Bloqueia 'Liberação para a realização da cirurgia reparadora'.
   - Documento/Bloqueia encaminhamento (global).
   - Não criar Dedutível de sexo nem de CID: o manual não restringe sexo e não lista CID; o SER já restringe a lista de CID.
   - *Exigiria regra condicional:* nada

### D05 — GENÉTICA MÉDICA PEDIÁTRICA

1. **Seção / página:** 4.11 Genética Médica — p.36
2. **Estrutura:** LISTA_BASTA_UM (+ idade)
3. **Requisitos (literal):**
   - *documento* — Inserir no SER o encaminhamento médico com a descrição clínica do caso
   - *idade* — Pacientes na faixa etária de 0 a 19 anos incompletos
   - *critério clínico (basta um, 12 itens)* — Dismorfias, Anomalia Congênita e/ou Malformação Congênita; Genitália ambígua; Fraturas Patológicas; Osteogênese imperfeita; Síndromes com Malformações Congênitas que acometem Múltiplos Sistemas; Doenças do desenvolvimento para uma investigação de uma síndrome genética; Deficiência Intelectual e/ou Autismo SINDRÔMICO; Síndromes com malformações congênitas associadas ao Nanismo; Erros Inatos do Metabolismo (Aminoácido; Carboidratos; Glicosaminoglicano); Suspeita de bebês com síndrome de Down; Anomalia Cromossômica Constitucional; Doenças Raras
   - *orientação* — Observar critérios de inclusão e exclusão do prestador
4. **Recurso no SER:** Ambulatório 1ª vez em Genética Médica - Pediatria [NAO_AE tipo1 v1075]
5. **Nosso:** Ambulatório 1ª vez em Genética Médica - Pediatria
   - Ativas:
     - 07c7c2a2 [ATIVA v2 Dedutível/Bloqueia] idade 0–18 'Pacientes na faixa etária de 0 a 19 anos incompletos' (o avaliador compara anos completos e bloqueia se idade > 18 — correto)
     - 25f6532b [ATIVA v1 Pergunta lista basta-uma/Bloqueia] 'O paciente tem ao menos uma das condições abaixo?' — 12 opções idênticas ao manual
     - 18f8413d [ATIVA v1 Documento/Bloqueia] encaminhamento (requisito global)
   - Inativas:
     - 07533079 Dedutível idade 0–19 (v1, substituída pela v2 0–18)
     - 13 Perguntas avulsas 'Pacientes portadores das seguintes condições: <item>' + 1 cabeçalho e9960874 (fragmentação da extração antiga, já trocadas pela lista)
   - Pedidos no espelho: 7 (A conferir)
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nenhum de conteúdo. Resíduo: origem SER DESLIGADA 'Ambulatório 1ª vez em Cardiologia - Hipertensão Arterial Resistente (Adulto)' apontando para este procedimento (pareamento errado antigo). Não afeta a análise (ela só usa origem ativa), mas infla a coluna 'regras ativas' do resumo do catálogo para o recurso de Hipertensão.
   - 'Observar critérios do prestador' não é representável (orientação).
8. **Proposta (modelo atual):**
   - Nada obrigatório. Opcional: Informativa 'Observar critérios de inclusão e exclusão do prestador'.
   - *Exigiria regra condicional:* nada

### D06 — GENÉTICA MÉDICA ADULTO

1. **Seção / página:** 4.11 Genética Médica — p.36–37
2. **Estrutura:** INCLUSAO_EXCLUSAO (inclusão basta-um)
3. **Requisitos (literal):**
   - *documento* — Inserir no SER o encaminhamento médico com a descrição clínica do caso
   - *critério clínico (inclusão, basta um)* — Pacientes com necessidade de Aconselhamento Genético, com história pessoal ou familiar de condição de causa genética e/ou malformação congênita, excluindo câncer familiar; Pacientes com Aborto de Repetição: História de 03 abortos espontâneos ou mais de primeiro trimestre previamente investigados das causas infecciosas, materna, imunológicas, metabólicas e placentária
   - *exclusão (7)* — Síndrome de Down já diagnosticado; Síndrome Genética já diagnosticada; Suspeita da síndrome do X-frágil; Autismo isolado não-sindrômico; Doença de Huntington; Síndromes de Ataxia Hereditárias; Ataxias Hereditárias familiar de instalação no Adulto
   - *orientação* — Observar critérios de inclusão e exclusão do prestador
4. **Recurso no SER:** Ambulatório 1ª vez em Genética Médica - Adulto [NAO_AE tipo1 v1076]
5. **Nosso:** Ambulatório 1ª vez em Genética Médica - Adulto
   - Ativas:
     - a2177a52 [ATIVA v1 Pergunta lista basta-uma/Bloqueia] 2 opções (aconselhamento genético; aborto de repetição)
     - 7 Perguntas Sim-bloqueia (2f2de73f, d99ec34d, 486be2fc, 18b4c47e, 599bc9e5, f81ecc75, 609f8f62) — uma por exclusão, textos literais
     - 8b88ed30 [ATIVA v1 Documento/Bloqueia] encaminhamento (requisito global)
   - Inativas:
     - c5e1756b e 94f72a34: as duas inclusões como Perguntas avulsas (substituídas pela lista)
   - Pedidos no espelho: 10 (A conferir)
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nenhum de conteúdo. Resíduo: origem SER DESLIGADA 'Avaliação de Cardiopatia Congênita Pediátrica (Internados)' apontando para este procedimento (sem efeito na análise).
   - Correto não haver Dedutível de idade: o manual não dá idade para o adulto.
8. **Proposta (modelo atual):**
   - Nada obrigatório. Opcional (só usabilidade): juntar as 7 exclusões numa Pergunta única 'Algum destes critérios de exclusão se aplica?' (Sim bloqueia), como já foi feito na Bariátrica.
   - *Exigiria regra condicional:* nada

### D07 — AVALIAÇÃO DIAGNÓSTICA INFECÇÃO CONGÊNITA ZIKA/ STORCH/CHIKUNGUNHA

1. **Seção / página:** 4.12 Outras Especialidades — p.37
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento / conteúdo* — Inserir no SER de forma clara e detalhada o encaminhamento médico de casos suspeitos de infecção congênita em crianças
   - *critério clínico* — caso suspeito de infecção congênita; população 'crianças' (sem idade numérica)
4. **Recurso no SER:** Avaliação diagnóstica infecção congênita Zika/Storch/Oropuche [NAO_AE tipo1 v1149] (o SER trocou Chikungunha por Oropuche)
5. **Nosso:** Avaliação diagnóstica infecção congênita Zika/Storch/Oropuche — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - Armadilha de extração: no texto -layout, 'Idade a partir de 16 anos' aparece encostado nesta linha; a conferência visual mostra que é da HORMONIZAÇÃO. Produção acertou; quem reextrair do .txt pode pendurar 16 anos aqui e barrar todas as crianças.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia encaminhamento (global).
   - Pergunta Sim/Não/Bloqueia (Não bloqueia) 'Caso suspeito de infecção congênita (Zika/STORCH/Chikungunha/Oropouche)?'.
   - Não criar Dedutível de idade: 'crianças' não tem número no manual (no máximo Aviso).
   - *Exigiria regra condicional:* nada

### D08 — HORMONIZAÇÃO SAÚDE - TRANS

1. **Seção / página:** 4.12 Outras Especialidades — p.37
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER de forma clara e detalhada: Encaminhamento médico
   - *idade* — Idade a partir de 16 anos
4. **Recurso no SER:** Ambulatório de 1ª vez - Hormonização - Saúde - Trans [NAO_AE tipo1 v1156]
5. **Nosso:** Ambulatório de 1ª vez - Hormonização - Saúde - Trans
   - Ativas:
     - 736b9f37 [ATIVA v1 Dedutível/Bloqueia] idade mín. 16 'Idade a partir de 16 anos'
     - a7cf7470 [ATIVA v1 Documento/Bloqueia] encaminhamento (requisito global)
   - Inativas:
     - 851fcb7d 'Inserir no SER de forma clara e detalhada' (Documento, fragmento)
     - 40b4f956 'Encaminhamento médico' (Documento, duplicado do global)
   - Pedidos no espelho: 8 (A conferir — pelo Documento de encaminhamento)
6. **Veredito:** **COBERTO**
7. **Problemas:**
   - Nenhum.
8. **Proposta (modelo atual):**
   - Nada.
   - *Exigiria regra condicional:* nada

### D09 — CENTRO DE REFERÊNCIA MULTIDISCIPLINAR PÓS-COVID 19

1. **Seção / página:** 4.12 Outras Especialidades — p.37
2. **Estrutura:** SIMPLES (com condicional implícito)
3. **Requisitos (literal):**
   - *documento* — Encaminhamento médico
   - *documento* — Comprovação de infecção prévia por COVID-19
   - *conteúdo do encaminhamento (sem 'se houver', mas só cabe a quem internou)* — Descrição de internação e tipo de leito
   - *critério clínico* — Critérios de inclusão: Pacientes com agravamento de comorbidades ou sequelas após a infecção por COVID-19
4. **Recurso no SER:** não achado (nenhum rótulo com 'COVID' no catálogo NAO_AE nem AE)
5. **Nosso:** —
   - Ativas: nenhuma
   - Pedidos no espelho: —
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - O recurso não existe mais no catálogo do SER (manual de 2022).
8. **Proposta (modelo atual):**
   - Nada enquanto não houver recurso. Se voltar: Documento/Bloqueia comprovação de COVID; Pergunta/Bloqueia inclusão; Pergunta/Ressalva 'Houve internação? Descreveu internação e tipo de leito?'.
   - *Exigiria regra condicional:* 'Descrição de internação' só se houve internação (resolvível com a pergunta acima).

### D10 — CARDÍACO

1. **Seção / página:** 4.13 Transplante — p.37
2. **Estrutura:** LISTA_BASTA_UM (+ idade + documentos)
3. **Requisitos (literal):**
   - *idade* — Critério de inclusão: Idade inferior aos 65 anos
   - *critério clínico (indicações, basta uma)* — Insuficiência Cardíaca Classe III ou IV (NYHA); Insuficiência Cardíaca Classe II, III ou IV + Terapia de Ressincronização Cardíaca; Cardiomiopatia Hipertrófica
   - *documento (exige especialidade do solicitante)* — Encaminhamento por cardiologista com descrição do caso
   - *exame a anexar com validade* — Ecocardiograma (< 6m)
   - *exame opcional* — Holter 24 horas (se necessário)
4. **Recurso no SER:** Ambulatório de 1ª Vez - Pré-Transplante Cardíaco [NAO_AE tipo1 v1161] (existe também 'Ambulatório de 1ª vez - pré Transplante Cardíaco Infantil' v1165; o manual não separa infantil)
5. **Nosso:** Ambulatório de 1ª Vez - Pré-Transplante Cardíaco — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 1 (Sem regras)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (1 pedido no espelho, 'Sem regras').
   - Extração antiga (SEM_PAR, não importada) colou 'Cardiomiopatia Hipertrófica' com 'Anexar' e a tipou como Documental — teria virado documento a anexar em vez de indicação.
   - Aplicar ao Pré-Transplante Cardíaco Infantil só com OK clínico: a lista NYHA/ressincronização é de adulto.
8. **Proposta (modelo atual):**
   - Dedutível/Bloqueia idade_max_anos = 64 ('inferior aos 65').
   - Pergunta lista basta-uma/Bloqueia com as 3 indicações.
   - Documento/Bloqueia 'Encaminhamento por cardiologista com descrição do caso'.
   - Documento/Bloqueia 'Ecocardiograma (< 6 meses)' com validade_dias = 180.
   - Informativa 'Holter 24 horas, se necessário'.
   - *Exigiria regra condicional:* nada

### D11 — RENAL (INFANTIL)

1. **Seção / página:** 4.13 Transplante — p.37
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento OU texto no pedido* — Anexar (ou descrever de forma clara e detalhada): Encaminhamento médico com descrição do caso
4. **Recurso no SER:** Ambulatório de 1ª Vez - Transplante Renal (Infantil) [NAO_AE tipo1 v1158]
5. **Nosso:** Ambulatório de 1ª Vez - Transplante Renal (Infantil) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - 'Anexar (ou descrever...)' aceita texto no lugar do anexo: um Documento/Bloqueia (que cobra anexo) seria mais rígido que o manual.
8. **Proposta (modelo atual):**
   - Pergunta Sim/Não/Bloqueia (Não bloqueia) 'Encaminhamento com descrição do caso anexado OU descrito no pedido?' — em vez do Documento global.
   - *Exigiria regra condicional:* nada

### D12 — FÍGADO (INFANTIL)

1. **Seção / página:** 4.13 Transplante — p.37
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento OU texto no pedido* — Anexar (ou descrever de forma clara e detalhada): Encaminhamento médico com descrição do caso
4. **Recurso no SER:** Ambulatório de 1ª Vez - Transplante de Fígado (Infantil) [NAO_AE tipo1 v1159]
5. **Nosso:** Ambulatório de 1ª Vez - Transplante de Fígado (Infantil) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - 'Anexar (ou descrever...)' aceita texto no lugar do anexo: um Documento/Bloqueia (que cobra anexo) seria mais rígido que o manual.
8. **Proposta (modelo atual):**
   - Pergunta Sim/Não/Bloqueia (Não bloqueia) 'Encaminhamento com descrição do caso anexado OU descrito no pedido?' — em vez do Documento global.
   - *Exigiria regra condicional:* nada

### D13 — RENAL (adulto implícito; a linha diz só 'RENAL')

1. **Seção / página:** 4.13 Transplante — p.38
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento OU texto* — Anexar (ou descrever de forma clara e detalhada): Encaminhamento médico com descrição do caso
   - *conteúdo* — Dias em que realiza o tratamento dialítico
   - *conteúdo* — Informar o tipo de acesso vascular
   - *exame a anexar (ou descrever)* — Exames laboratoriais (sorologias, Hematócrito e Hemoglobina e ureia e Creatinina)
   - *opcional* — Informar, caso seja vontade do paciente, o Centro Transplantador de preferência
4. **Recurso no SER:** Ambulatório de 1ª Vez - Transplante Renal (Adulto) [NAO_AE tipo1 v1160]
5. **Nosso:** Ambulatório de 1ª Vez - Transplante Renal (Adulto) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - A extração antiga PERDEU esta linha inteira (quebra de página p.37→p.38; não há nenhuma linha 'RENAL' adulto no CSV).
8. **Proposta (modelo atual):**
   - Pergunta Sim/Não/Bloqueia (Não bloqueia) 'Encaminhamento anexado ou descrito?'.
   - Pergunta Sim/Não/Bloqueia (Não bloqueia) 'Exames laboratoriais (sorologias, hematócrito, hemoglobina, ureia e creatinina) anexados ou transcritos?'.
   - Pergunta Sim/Não/Ressalva 'Informou os dias de diálise e o tipo de acesso vascular?'.
   - Informativa 'Centro Transplantador de preferência, se for vontade do paciente'.
   - *Exigiria regra condicional:* 'Dias de diálise' pressupõe paciente em diálise; o manual não trata transplante preemptivo — não transformar em bloqueio.

### D14 — PULMONAR

1. **Seção / página:** 4.13 Transplante — p.38
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - *documento OU texto* — Anexar (ou descrever de forma clara e detalhada): Encaminhamento médico com descrição do caso
   - *exclusão* — Critérios de Exclusão: Pacientes agudamente doentes ou clinicamente instáveis
4. **Recurso no SER:** Ambulatório de 1ª Vez - Pré-Transplante de Pulmão [NAO_AE tipo1 v1164]
5. **Nosso:** Ambulatório de 1ª Vez - Pré-Transplante de Pulmão — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
8. **Proposta (modelo atual):**
   - Pergunta Sim/Não/Bloqueia (Não bloqueia) 'Encaminhamento anexado ou descrito?'.
   - Pergunta Sim/Não/Bloqueia (Sim bloqueia) 'Paciente agudamente doente ou clinicamente instável?'.
   - *Exigiria regra condicional:* nada

### D15 — CINTILOGRAFIA DO MIOCÁRDIO EM REPOUSO E ESTRESSE AMBULATORIAL

1. **Seção / página:** 4.14 Cintilografias — p.38
2. **Estrutura:** SIMPLES (com condicional implícito)
3. **Requisitos (literal):**
   - *documento* — Solicitação médica em formulário de Alto Custo, com indicação da patologia cardiológica suspeita ou comprovada
   - *exames a anexar (lista sem 'e/ou' — literal: os três)* — ECG, Ecocardiograma, Teste ergométrico
   - *conteúdo do pedido* — Obs: Especificar se o exame de estresse será realizado em esteira ou farmacológico
4. **Recurso no SER:** Cintilografia do Miocárdio em REPOUSO e/ou STRESS (Ambulatorial) [NAO_AE tipo2 v1060]
5. **Nosso:** Cintilografia do Miocárdio em REPOUSO e/ou STRESS (Ambulatorial) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 39 (Sem regras)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra — é o recurso de MAIOR volume do trecho entre os exames de cintilografia (39 pedidos no espelho, todos 'Sem regras').
   - Ambiguidade: o manual lista Teste ergométrico como obrigatório, mas o estresse farmacológico costuma ser indicado justamente a quem não faz esteira. Teste ergométrico como Documento/Bloqueia pode travar pedido legítimo.
   - Extração antiga truncou o nome ('EM REPOU ESTRESSE AMBULATOR' / 'SO E RIAL Inserir no SER') — nada disso está em produção.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia 'Solicitação médica em formulário de Alto Custo, com indicação da patologia cardiológica suspeita ou comprovada'.
   - Documento/Bloqueia 'ECG'; Documento/Bloqueia 'Ecocardiograma'; Documento/RESSALVA 'Teste ergométrico' (por causa da ambiguidade).
   - Pergunta lista basta-uma/Bloqueia 'Modalidade do estresse' com opções 'esteira' | 'farmacológico' (garante que foi especificado).
   - *Exigiria regra condicional:* Teste ergométrico obrigatório só quando o estresse for em esteira (se o regulador confirmar essa leitura).

### D16 — CINTILOGRAFIA DO MIOCÁRDIO (PACIENTES INTERNADOS)

1. **Seção / página:** 4.14 Cintilografias — p.38
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Solicitação médica em formulário de Alto Custo, com indicação da patologia cardiológica suspeita ou comprovada
   - *conteúdo* — Modo ventilatório (no PDF aparece colado ao fim do item do formulário)
   - *exames a anexar* — ECG, Ecocardiograma, Teste ergométrico
   - *critério (caráter)* — Obs.: Destina-se a pacientes internados
4. **Recurso no SER:** Cintilografia do Miocardio (Internados) [NAO_AE tipo2 v1065]
5. **Nosso:** Cintilografia do Miocardio (Internados) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - Teste ergométrico para paciente internado é estranho, mas é literal.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia formulário de Alto Custo (texto cardiológico).
   - Pergunta Sim/Não/Bloqueia (Não bloqueia) 'Paciente está internado?' (mesmo padrão já usado em Cateterismo/Arteriografia Internados, p.13).
   - Pergunta Sim/Não/Ressalva 'Informou o modo ventilatório?'.
   - Documento/Bloqueia 'ECG'; Documento/Bloqueia 'Ecocardiograma'; Documento/Ressalva 'Teste ergométrico'.
   - *Exigiria regra condicional:* nada

### D17 — CINTILOGRAFIA RENAL ESTÁTICA E/OU DINÂMICA - DMSA (AMBULATORIAL)

1. **Seção / página:** 4.14 Cintilografias — p.38
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER: Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita.
4. **Recurso no SER:** Cintilografia Renal Estática e/ou Dinâmica (Ambulatorial) [NAO_AE tipo2 v1062]
5. **Nosso:** Cintilografia Renal Estática e/ou Dinâmica (Ambulatorial) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 6 (Sem regras)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - Extração antiga: nome truncado ('RENA ... DINÂM DMSA (AMBULATORIA') e fragmento 'AL ICA - AL) Inserir no SER' como critério — não está em produção.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia "Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita" (mesmo texto em todas as cintilografias ambulatoriais; é o mesmo requisito que já existe em Arteriografia/Cateterismo p.13).
   - Documento/Bloqueia encaminhamento (global) é redundante aqui: o formulário de Alto Custo já é a solicitação médica.
   - *Exigiria regra condicional:* nada

### D18 — CINTILOGRAFIA DE OSSOS COM OU SEM FLUXO SANGUÍNEO - CORPO INTEIRO (AMBULATORIAL)

1. **Seção / página:** 4.14 Cintilografias — p.38
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER: Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita.
4. **Recurso no SER:** Cintilografia de Ossos c/ ou s/ Fluxo Sanguíneo - Corpo Inteiro (Ambulatorial) [NAO_AE tipo2 v1061]
5. **Nosso:** Cintilografia de Ossos c/ ou s/ Fluxo Sanguíneo - Corpo Inteiro (Ambulatorial) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 7 (Sem regras)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - Extração antiga: 'CINTILOGRAFIA DE OS ... CORPO INT' e o fragmento 'SOS O TEIRO Inserir no SER' gravado como critério — CONFERIDO: nada disso existe em produção (0 regras com fonte p.38).
8. **Proposta (modelo atual):**
   - Documento/Bloqueia "Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita" (mesmo texto em todas as cintilografias ambulatoriais; é o mesmo requisito que já existe em Arteriografia/Cateterismo p.13).
   - Documento/Bloqueia encaminhamento (global) é redundante aqui: o formulário de Alto Custo já é a solicitação médica.
   - *Exigiria regra condicional:* nada

### D19 — CINTILOGRAFIA DE PULMÃO POR VENTILAÇÃO E/OU PERFUSÃO (AMBULATORIAL)

1. **Seção / página:** 4.14 Cintilografias — p.38
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER: Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita.
4. **Recurso no SER:** Cintilografia de Pulmão por VENTILAÇÃO e/ou PERFUSÃO (Ambulatorial) [NAO_AE tipo2 v1071]
5. **Nosso:** Cintilografia de Pulmão por VENTILAÇÃO e/ou PERFUSÃO (Ambulatorial) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - Extração antiga: 'PULM ... E/O PERFUSÃO (AMBULATOR' e fragmento 'MÃO OU RIAL) Inserir no SER' — não está em produção.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia "Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita" (mesmo texto em todas as cintilografias ambulatoriais; é o mesmo requisito que já existe em Arteriografia/Cateterismo p.13).
   - Documento/Bloqueia encaminhamento (global) é redundante aqui: o formulário de Alto Custo já é a solicitação médica.
   - *Exigiria regra condicional:* nada

### D20 — CINTILOGRAFIA DE TIREOIDE COM OU SEM CAPTAÇÃO (AMBULATORIAL)

1. **Seção / página:** 4.14 Cintilografias — p.38
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER: Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita.
4. **Recurso no SER:** Cintilografia de Tireoide c/ ou s/ Captação (Ambulatorial) [NAO_AE tipo2 v1063]
5. **Nosso:** Cintilografia de Tireoide c/ ou s/ Captação (Ambulatorial) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 5 (Sem regras)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - Extração antiga: 'TIRE COM OU SEM CAPTAÇ' e fragmento 'OIDE ÃO Inserir no SER' — não está em produção.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia "Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita" (mesmo texto em todas as cintilografias ambulatoriais; é o mesmo requisito que já existe em Arteriografia/Cateterismo p.13).
   - Documento/Bloqueia encaminhamento (global) é redundante aqui: o formulário de Alto Custo já é a solicitação médica.
   - *Exigiria regra condicional:* nada

### D21 — CINTILOGRAFIA DE CORPO INTEIRO PESQUISA DE NEOPLASIAS (AMBULATORIAL)

1. **Seção / página:** 4.14 Cintilografias — p.38
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER: Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita.
4. **Recurso no SER:** não achado exato — candidato parcial: 'Cintilografia de PCI para Neoplasia de Tireoide' [NAO_AE tipo2 v1073] (PCI = pesquisa de corpo inteiro, mas restrita à tireoide)
5. **Nosso:** —
   - Ativas: nenhuma
   - Pedidos no espelho: —
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem recurso correspondente exato no catálogo NAO_AE; pareamento com o candidato exige confirmação humana.
   - Extração antiga: esta linha virou a 'lixeira' da página — absorveu Linfocintilografia, Fígado e Vias Biliares, Articulações, Campos Pulmonares, Câmaras Cardíacas, o Teste do Suor (p.39), o bloco inteiro de 'Outros exames' (p.40) e o preparo da Angioplastia (p.41). Tudo SEM_PAR; nada em produção.
8. **Proposta (modelo atual):**
   - Nenhuma regra até alguém confirmar o pareamento. Como o requisito é idêntico em todas as cintilografias ambulatoriais, se o pareamento for confirmado a mesma regra serve sem conflito: Documento/Bloqueia "Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita" (mesmo texto em todas as cintilografias ambulatoriais; é o mesmo requisito que já existe em Arteriografia/Cateterismo p.13).
   - *Exigiria regra condicional:* nada

### D22 — LINFOCINTILOGRAFIA (AMBULATORIAL)

1. **Seção / página:** 4.14 Cintilografias — p.39
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER: Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita.
4. **Recurso no SER:** Linfocintilografia (Ambulatorial) [NAO_AE tipo2 v1070]
5. **Nosso:** Linfocintilografia (Ambulatorial) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - Extração antiga engoliu esta linha dentro de 'CORPO INTEIRO PESQUISA DE NEOPLASIAS' — não está em produção.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia "Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita" (mesmo texto em todas as cintilografias ambulatoriais; é o mesmo requisito que já existe em Arteriografia/Cateterismo p.13).
   - Documento/Bloqueia encaminhamento (global) é redundante aqui: o formulário de Alto Custo já é a solicitação médica.
   - *Exigiria regra condicional:* nada

### D23 — CINTILOGRAFIA DE FÍGADO E VIAS BILIARES (AMBULATORIAL)

1. **Seção / página:** 4.14 Cintilografias — p.39
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER: Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita.
4. **Recurso no SER:** não achado — candidato: 'Cintilografia do Sistema Digestivo (Ambulatorial)' [NAO_AE tipo2 v1064]
5. **Nosso:** —
   - Ativas: nenhuma
   - Pedidos no espelho: —
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem recurso correspondente exato no catálogo NAO_AE; pareamento com o candidato exige confirmação humana.
8. **Proposta (modelo atual):**
   - Nenhuma regra até alguém confirmar o pareamento. Como o requisito é idêntico em todas as cintilografias ambulatoriais, se o pareamento for confirmado a mesma regra serve sem conflito: Documento/Bloqueia "Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita" (mesmo texto em todas as cintilografias ambulatoriais; é o mesmo requisito que já existe em Arteriografia/Cateterismo p.13).
   - *Exigiria regra condicional:* nada

### D24 — CINTILOGRAFIA DE ARTICULAÇÕES / EXTREMIDADES / ÓSSEA (AMBULATORIAL)

1. **Seção / página:** 4.14 Cintilografias — p.39
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER: Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita.
4. **Recurso no SER:** não achado — candidatos: 'Cintilografia de Segmento Ósseo c/ Gálio 67' [v1072]; 'Cintilografia de Ossos c/ ou s/ Fluxo Sanguíneo - Corpo Inteiro (Ambulatorial)' [v1061]
5. **Nosso:** —
   - Ativas: nenhuma
   - Pedidos no espelho: —
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem recurso correspondente exato no catálogo NAO_AE; pareamento com o candidato exige confirmação humana.
8. **Proposta (modelo atual):**
   - Nenhuma regra até alguém confirmar o pareamento. Como o requisito é idêntico em todas as cintilografias ambulatoriais, se o pareamento for confirmado a mesma regra serve sem conflito: Documento/Bloqueia "Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita" (mesmo texto em todas as cintilografias ambulatoriais; é o mesmo requisito que já existe em Arteriografia/Cateterismo p.13).
   - *Exigiria regra condicional:* nada

### D25 — CINTILOGRAFIA DE CAMPOS PULMONARES E ESVAZIAMENTO GÁSTRICO (AMBULATORIAL)

1. **Seção / página:** 4.14 Cintilografias — p.39
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER: Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita.
4. **Recurso no SER:** não achado — candidato: 'Cintilografia do Sistema Digestivo (Ambulatorial)' [NAO_AE tipo2 v1064]
5. **Nosso:** —
   - Ativas: nenhuma
   - Pedidos no espelho: —
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem recurso correspondente exato no catálogo NAO_AE; pareamento com o candidato exige confirmação humana.
8. **Proposta (modelo atual):**
   - Nenhuma regra até alguém confirmar o pareamento. Como o requisito é idêntico em todas as cintilografias ambulatoriais, se o pareamento for confirmado a mesma regra serve sem conflito: Documento/Bloqueia "Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita" (mesmo texto em todas as cintilografias ambulatoriais; é o mesmo requisito que já existe em Arteriografia/Cateterismo p.13).
   - *Exigiria regra condicional:* nada

### D26 — CINTILOGRAFIA DE CÂMARAS CARDÍACAS

1. **Seção / página:** 4.14 Cintilografias — p.39
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Inserir no SER: Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita.
4. **Recurso no SER:** não achado (nenhum rótulo 'câmaras'/'ventriculografia'); o genérico 'Cintilografias (Internados)' [v1066] não é a mesma coisa
5. **Nosso:** —
   - Ativas: nenhuma
   - Pedidos no espelho: —
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem recurso correspondente exato no catálogo NAO_AE; pareamento com o candidato exige confirmação humana.
8. **Proposta (modelo atual):**
   - Nenhuma regra até alguém confirmar o pareamento. Como o requisito é idêntico em todas as cintilografias ambulatoriais, se o pareamento for confirmado a mesma regra serve sem conflito: Documento/Bloqueia "Solicitação médica em formulário de Alto Custo, com indicação da patologia suspeita" (mesmo texto em todas as cintilografias ambulatoriais; é o mesmo requisito que já existe em Arteriografia/Cateterismo p.13).
   - *Exigiria regra condicional:* nada

### D27 — TESTE DO SUOR – DOSAGEM DE CLORETO NO SUOR

1. **Seção / página:** 4.15 Teste do Suor – Dosagem de Cloreto no Suor (4.15.1 Preparo) — p.39
2. **Estrutura:** SO_ORIENTACAO (com 1 item que barra)
3. **Requisitos (literal):**
   - *idade (barra de verdade)* — a. SOMENTE após o segundo mês de vida
   - *preparo (dia do exame)* — b. NÃO apresentar com febre (se temperatura acima de 37,5ºC e deverá ser reagendado
   - *preparo* — c. É NECESSÁRIO SUSPENDER corticoide 30 dias antes do exame (consulte seu médico antes de suspender o medicamento)
   - *preparo* — d. Beber líquidos de hora em hora (...) 03 dias ANTES DO EXAME; e. NÃO USAR creme ou pomada nos braços e tronco 2 dias antes; f. Levar um agasalho; g. Observar demais orientações do prestador
4. **Recurso no SER:** Dosagem de cloreto no suor [NAO_AE tipo2 v1128]
5. **Nosso:** Dosagem de cloreto no suor — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **SO_ORIENTACAO**
7. **Problemas:**
   - O único item que barraria ('após o segundo mês de vida') não cabe como Dedutível: o modelo só tem idade em ANOS (idade_min_anos = 0 não barra nada) → NAO_REPRESENTAVEL_HOJE como dedução.
   - Armadilha da extração antiga: a linha 'PREPARO PARA O PROCEDIMENTO' da p.39 saiu com idade_min = 18 (misturou o 'Maiores de 18 anos' da Elastografia). Se reimportada aqui, barraria crianças num exame essencialmente pediátrico (diagnóstico de fibrose cística). Não está em produção.
   - Informativa não muda o veredito da análise (o serviço descarta Informativa dos avaliáveis): com só Informativa o pedido continua 'Sem regras'.
8. **Proposta (modelo atual):**
   - Pergunta Sim/Não/Bloqueia (Não bloqueia) 'Na data do exame o paciente terá mais de 2 meses de vida?' (ou Ressalva, se preferirem não travar).
   - Informativa com o preparo a–g (corticoide 30 dias antes com orientação médica; febre > 37,5 °C reagenda).
   - *Exigiria regra condicional:* Dedutível de idade em MESES (ex.: idade_min_meses) resolveria o item 'a' sem pergunta.

### D28 — ELASTOGRAFIA

1. **Seção / página:** 4.16 Elastografia (4.16.1 Preparo) — p.40
2. **Estrutura:** INCLUSAO_EXCLUSAO (escondida sob o título 'Preparo')
3. **Requisitos (literal):**
   - *idade* — a. Maiores de 18 anos
   - *preparo* — b. Jejum absoluto de 6 horas
   - *critério clínico (indicação, basta um)* — c. Pacientes portadores de hepatites crônicas por vírus (B,C), álcool, gordura no fígado, doença autoimune, uso crônico de medicamentos (artrite reumatoide, psoríase, CA de Mama)
   - *exclusão* — c. Nas doenças agudas não há indicação
   - *exclusão (contraindicação)* — d. Contraindicação: Gravidez, Enzimas do fígado muito altas (>400)
   - *preparo* — e. Suspender medicamentos para Glicose no dia do exame; f. Observar demais orientações do prestador
4. **Recurso no SER:** Elastografia Hepática Transitória [NAO_AE tipo2 v1058]
5. **Nosso:** Elastografia Hepática Transitória — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 20 (Sem regras)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (20 pedidos no espelho, 'Sem regras').
   - Ler a seção como 'preparo = só orientação' perderia três barreiras reais: idade (a), indicação crônica / exclusão de doença aguda (c) e contraindicações (d).
   - 'Maiores de 18 anos' é ambíguo (>18 ou ≥18); leitura usual = maioridade (≥18).
8. **Proposta (modelo atual):**
   - Dedutível/Bloqueia idade_min_anos = 18.
   - Pergunta lista basta-uma/Bloqueia: hepatite crônica viral (B ou C) | doença hepática por álcool | gordura no fígado | doença autoimune | uso crônico de medicamentos (artrite reumatoide, psoríase, CA de mama).
   - Pergunta Sim-bloqueia 'Doença hepática AGUDA?'.
   - Pergunta Sim-bloqueia 'Gestante?' e Pergunta Sim-bloqueia 'Enzimas hepáticas > 400?' (gravidez não dá para deduzir por sexo).
   - Informativa: jejum absoluto de 6 h; suspender medicamentos para glicose no dia do exame.
   - *Exigiria regra condicional:* nada

### D29 — TOMOGRAFIA POR EMISSÃO DE PÓSITRONS (PET-CT)

1. **Seção / página:** 4.17 Outros Exames — p.40
2. **Estrutura:** LISTA_BASTA_UM (+ documentos)
3. **Requisitos (literal):**
   - *documento* — Encaminhamento médico com descrição do caso
   - *documento* — Formulário de Alto Custo
   - *critério clínico (indicações, basta uma — estão como itens de 'Inserir no SER', mas são indicações, não anexos)* — Estadiamento clínico do câncer de pulmão de células não pequenas (carcinoma epidermoide, adenocarcinoma e carcinoma de células grandes), potencialmente ressecável; Detecção de metástases exclusivamente hepáticas e potencialmente ressecáveis de câncer colorretal; Estadiamento e avaliação da resposta ao tratamento de linfomas de Hodgkin e não-Hodgkin
4. **Recurso no SER:** Tomografia por Emissão de Pósitrons (PET-CT) [NAO_AE tipo2 v1115] — formulário do SER exige laudo histopatológico/exame de imagem e grau histopatológico; lista de CID restrita (assinatura 0|21|0)
5. **Nosso:** Tomografia por Emissão de Pósitrons (PET-CT) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 4 (Sem regras)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra (4 pedidos no espelho, 'Sem regras').
   - A extração antiga tipou as 3 indicações como 'Documental' — teriam virado anexos absurdos ('anexe: Estadiamento clínico do câncer de pulmão...'). Não está em produção.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia encaminhamento com descrição do caso.
   - Documento/Bloqueia 'Formulário de Alto Custo'.
   - Pergunta lista basta-uma/Bloqueia com as 3 indicações.
   - Não criar Dedutível de CID: o manual não lista CID e o SER já restringe a lista.
   - *Exigiria regra condicional:* nada

### D30 — ANGIOTOMOGRAFIA

1. **Seção / página:** 4.17 Outros Exames — p.40
2. **Estrutura:** SIMPLES
3. **Requisitos (literal):**
   - *documento* — Encaminhamento médico com descrição do caso
   - *documento* — Formulário de Alto Custo
4. **Recurso no SER:** DOIS recursos: 'Angiotomografia - exceto Coronária (Ambulatorial)' [NAO_AE tipo2 v1116] e 'Angiotomografia Coronariana (ambulatorial)' [NAO_AE tipo2 v1117]
5. **Nosso:** os dois procedimentos homônimos — 0 regras cada
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra em nenhum dos dois.
8. **Proposta (modelo atual):**
   - Nos DOIS procedimentos: Documento/Bloqueia encaminhamento; Documento/Bloqueia 'Formulário de Alto Custo'.
   - *Exigiria regra condicional:* nada

### D31 — RESSONÂNCIA MAGNÉTICA SEM SEDAÇÃO (INTERNADOS)

1. **Seção / página:** 4.17 Outros Exames — p.40
2. **Estrutura:** INCLUSAO_EXCLUSAO (+ escopo de exames)
3. **Requisitos (literal):**
   - *documento* — Encaminhamento médico com descrição do caso; Formulário de Alto Custo
   - *conteúdo* — Modo ventilatório
   - *operacional (depois de inserido)* — Atualização diária de evolução clínica, descrita no sistema SER
   - *critério (caráter + idade + exclusão)* — Destina-se apenas para pacientes internados, acima de 5 anos de idade que não necessitam de sedação
   - *escopo* — Tipos de exames: RNM de Crânio, Face, ATM, Mastoide, Órbitas, Coluna Cervical, Torácica e Lombar, Pescoço; Angiorressonância de Crânio e/ou Pescoço
4. **Recurso no SER:** não achado (o catálogo NAO_AE só tem 'Ressonância Magnética - COM SEDAÇÃO (Internados)' e RMs por região sem marca de internado)
5. **Nosso:** —
   - Ativas: nenhuma
   - Pedidos no espelho: —
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Não reaproveitar nas RMs por região (Crânio, Coluna, Pescoço/Laringe, Angiorressonância Cerebral...): a regra vale para o procedimento inteiro, então 'só internados' / 'acima de 5 anos' barraria os pedidos ambulatoriais desses mesmos procedimentos — NAO_REPRESENTAVEL_HOJE nesse mapeamento.
   - Extração antiga tinha idade_min = 5 para esta linha (SEM_PAR) — não está em produção.
8. **Proposta (modelo atual):**
   - Nenhuma regra enquanto não houver recurso próprio.
   - *Exigiria regra condicional:* Regra condicionada ao caráter (internado × ambulatorial) dentro do mesmo procedimento.

### D32 — RESSONÂNCIA MAGNÉTICA COM SEDAÇÃO (INTERNADOS)

1. **Seção / página:** 4.17 Outros Exames — p.41
2. **Estrutura:** INCLUSAO_EXCLUSAO (+ escopo de exames)
3. **Requisitos (literal):**
   - *documento* — Encaminhamento médico com descrição do caso; Formulário de Alto Custo
   - *conteúdo* — Modo ventilatório
   - *operacional* — Atualização diária de evolução clínica, descrita no sistema SER
   - *critério (caráter)* — Destina-se apenas para pacientes internados
   - *escopo* — Tipos de exames: RNM de Crânio, Face, ATM, Mastoide, Órbitas, Coluna Cervical, Torácica e Lombar, Pescoço; Angiorressonância de Crânio e/ou Pescoço
4. **Recurso no SER:** Ressonância Magnética - COM SEDAÇÃO (Internados) [NAO_AE tipo2 v1077]
5. **Nosso:** Ressonância Magnética - COM SEDAÇÃO (Internados) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - 'Ressonância Magnética - COM SEDAÇÃO (Ambulatorial)' (68 pedidos 'Sem regras') não tem linha neste trecho — não copiar 'só internados' para ela.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia encaminhamento; Documento/Bloqueia 'Formulário de Alto Custo'.
   - Pergunta Sim/Não/Bloqueia (Não bloqueia) 'Paciente está internado?'.
   - Pergunta lista basta-uma/Bloqueia 'O exame pedido é um destes?' (RNM de crânio, face, ATM, mastoide, órbitas, coluna cervical/torácica/lombar, pescoço; angiorressonância de crânio e/ou pescoço).
   - Pergunta Sim/Não/Ressalva 'Informou o modo ventilatório?'.
   - Informativa 'Atualizar diariamente a evolução clínica no SER'.
   - *Exigiria regra condicional:* nada

### D33 — ELETROENCEFALOGRAMA (ADULTO)

1. **Seção / página:** 4.17 Outros Exames — p.41
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - *documento* — Inserir no SER, descrevendo de forma clara e detalhada: Encaminhamento médico com descrição do caso
   - *exclusão* — Critérios de exclusão: Pacientes com necessidade de sedação
4. **Recurso no SER:** EEG Simples Adulto [NAO_AE tipo2 v1122]
5. **Nosso:** EEG Simples Adulto — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 43 (Sem regras)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
   - Maior volume do trecho (43 pedidos).
8. **Proposta (modelo atual):**
   - Documento/Bloqueia encaminhamento (global).
   - Pergunta Sim/Não/Bloqueia (Sim bloqueia) 'Paciente necessita de sedação para o exame?'.
   - Informativa com o preparo 4.18.5 (só EEG; o manual não fala de Vídeo EEG no preparo).
   - *Exigiria regra condicional:* nada

### D34 — ELETROENCEFALOGRAMA (INFANTIL)

1. **Seção / página:** 4.17 Outros Exames — p.41
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - *documento* — Inserir no SER, descrevendo de forma clara e detalhada: Encaminhamento médico com descrição do caso
   - *exclusão* — Critérios de exclusão: Pacientes com necessidade de sedação
4. **Recurso no SER:** EEG Simples Infantil [NAO_AE tipo2 v1121]
5. **Nosso:** EEG Simples Infantil — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 21 (Sem regras)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia encaminhamento (global).
   - Pergunta Sim/Não/Bloqueia (Sim bloqueia) 'Paciente necessita de sedação para o exame?'.
   - Informativa com o preparo 4.18.5 (só EEG; o manual não fala de Vídeo EEG no preparo).
   - *Exigiria regra condicional:* nada

### D35 — VÍDEO EEG (ADULTO)

1. **Seção / página:** 4.17 Outros Exames — p.41
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - *documento* — Inserir no SER, descrevendo de forma clara e detalhada: Encaminhamento médico com descrição do caso
   - *exclusão* — Critérios de exclusão: Pacientes com necessidade de sedação
4. **Recurso no SER:** Vídeo EEG Adulto - (Ambulatorial) [NAO_AE tipo2 v1119]
5. **Nosso:** Vídeo EEG Adulto - (Ambulatorial) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 2 (Sem regras)
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia encaminhamento (global).
   - Pergunta Sim/Não/Bloqueia (Sim bloqueia) 'Paciente necessita de sedação para o exame?'.
   - *Exigiria regra condicional:* nada

### D36 — VÍDEO EEG (INFANTIL)

1. **Seção / página:** 4.17 Outros Exames — p.41
2. **Estrutura:** INCLUSAO_EXCLUSAO
3. **Requisitos (literal):**
   - *documento* — Inserir no SER, descrevendo de forma clara e detalhada: Encaminhamento médico com descrição do caso
   - *exclusão* — Critérios de exclusão: Pacientes com necessidade de sedação
4. **Recurso no SER:** Vídeo EEG Pedíatrico - (Ambulatorial) [NAO_AE tipo2 v1120]
5. **Nosso:** Vídeo EEG Pedíatrico - (Ambulatorial) — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: 0
6. **Veredito:** **AUSENTE**
7. **Problemas:**
   - Nenhuma regra.
8. **Proposta (modelo atual):**
   - Documento/Bloqueia encaminhamento (global).
   - Pergunta Sim/Não/Bloqueia (Sim bloqueia) 'Paciente necessita de sedação para o exame?'.
   - *Exigiria regra condicional:* nada

### D37 — ANGIOPLASTIA (preparo)

1. **Seção / página:** 4.18.1 Preparo prévio para Angioplastia — p.41–42
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - *preparo* — a. Jejum absoluto de 4 horas; b. Pode tomar medicamentos com água
   - *trazer no dia (não 'inserir no SER')* — c. Trazer receita médica e exames realizados: Eletrocardiograma, RX, Teste Ergométrico, ECO, Cintilografia Miocárdica, Holter, Cateterismo (laudo e filme)
   - *trazer no dia* — d. identidade, CPF, comprovante de residência, cartão do SUS, pedido do exame, exame laboratorial recente (hemograma e bioquímica), prescrição médica atual; e. identidade do acompanhante
   - *medicação* — f. Iniciar 3 dias antes: AAS 100mg 2 cp após o almoço; Clopidogrel 75mg 1 cp/dia; g. diabéticos: insulina suspensa 1 dia antes, hipoglicemiantes orais 3 dias antes; h. Marevan: orientação médica; i. alérgico a iodo: Meticorten, Polaramine, Ranitidina por 3 dias; j. manter demais medicações
   - *consequência* — O não cumprimento de qualquer um dos itens pode acarretar cancelamento do exame
4. **Recurso no SER:** não achado como exame (não há 'Angioplastia' no catálogo NAO_AE); candidato: 'Ambulatório 1ª vez em Cardiologia - Pré Angioplastia Coronariana' [NAO_AE tipo1 v1060] — é CONSULTA, não o procedimento
5. **Nosso:** Ambulatório 1ª vez em Cardiologia - Pré Angioplastia Coronariana — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: Pré Angioplastia: 5 (Sem regras)
6. **Veredito:** **SO_ORIENTACAO**
7. **Problemas:**
   - Nada aqui deve barrar a regulação: o cateterismo prévio (laudo e filme) é pré-requisito clínico da angioplastia, mas o manual só manda TRAZER no dia, não inserir no SER. O formulário do SER da consulta Pré-Angioplastia já pede 'Laudo da Ergometria, Cintilografia, Ecocardiograma com Dobutamina ou ECG e Enzimas'.
   - A extração antiga pendurou este preparo na linha 'CINTILOGRAFIA DE CORPO INTEIRO' (p.41) — não está em produção.
8. **Proposta (modelo atual):**
   - Informativa com o preparo, só quando existir o procedimento de angioplastia; não pendurar na consulta Pré-Angioplastia.
   - *Exigiria regra condicional:* nada

### D38 — ARTERIOGRAFIA DE MEMBROS (preparo)

1. **Seção / página:** 4.18.2 Preparo prévio para Arteriografia de membros — p.42
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - *preparo* — a. Jejum absoluto de 7 horas; d. higienizados, sem adornos e próteses
   - *trazer no dia* — b. identidade, CPF, comprovante de residência, cartão do SUS, pedido, exame laboratorial recente (hemograma e bioquímica), prescrição; c. 'O exame de bioquímica deve conter, principalmente, ureia, creatinina e coagulograma'; e. 'Apresentar relatório médico, da unidade de origem, com caso detalhado'
   - *acompanhante* — f. Obrigatório a presença do acompanhante com documento de identificação
   - *medicação* — g. Metformina 48h antes; Sulfonilureias, Diuréticos e Insulina na manhã do exame; Clexane no dia anterior; h. alérgico a iodo: Meticorten, Polaramine, Ranitidina por 3 dias
4. **Recurso no SER:** Arteriografia Periférica (Ambulatorial) [NAO_AE tipo2 v1047] e Arteriografia Periférica (Internados) [v1048]
5. **Nosso:** Arteriografia Periférica (Ambulatorial) / (Internados) — regras ativas vêm da p.13, não do preparo
   - Ativas:
     - Ambulatorial: b2782555 Alto Custo; 88b9ea2c Doppler; 309b9954 'Hematócrito, hemoglobina, ureia e creatinina'; 027f51cd Pergunta 'Destina-se a pacientes em domicílio'; ce892796 global
     - Internados: b75f4dfd Alto Custo; 2e8f285c Doppler do membro afetado; 605c3d8c 'Hematócrito, hemoglobina, ureia e creatinina'; a2c6a87f Pergunta 'ambiente hospitalar'; 098b1b90 global
   - Pedidos no espelho: Ambulatorial: 2 (A conferir)
6. **Veredito:** **SO_ORIENTACAO**
7. **Problemas:**
   - O preparo exige COAGULOGRAMA no dia; a regra ativa de laboratoriais (p.13) não o inclui. Pedido 'em ordem' na regulação pode ser cancelado no dia por falta de coagulograma. Não é motivo para barrar, mas vale uma ressalva.
   - Preparo (jejum, suspensão de medicamentos, acompanhante obrigatório) não está cadastrado em lugar nenhum.
8. **Proposta (modelo atual):**
   - Documento/RESSALVA (não Bloqueia) 'Coagulograma recente — exigido no dia da arteriografia' nos dois procedimentos.
   - Informativa com o preparo a–h. Não pendurar em 'Arteriografia Cerebral (Ambulatório)' (o preparo é de membros).
   - *Exigiria regra condicional:* nada

### D39 — CATETERISMO CARDÍACO (preparo)

1. **Seção / página:** 4.18.3 Preparo prévio para Cateterismo Cardíaco — p.43
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - *preparo / trazer / medicação* — Texto idêntico ao da Angioplastia (a–j): jejum 4h; trazer ECG, RX, TE, ECO, Cintilografia, Holter, 'Cateterismo (laudo e filme)'; documentos; AAS 100mg + Clopidogrel 75mg 3 dias antes; diabéticos; Marevan; alergia a iodo
4. **Recurso no SER:** Cateterismo Cardíaco (Ambulatorial) [NAO_AE tipo2 v1049]; Cateterismo Cardíaco (Internados) [v1046]; Cateterismo Cardíaco Pediatrico (Ambulatorial) [v1050]
5. **Nosso:** Cateterismo Cardíaco (Ambulatorial) 5 ativas / (Internados) 8 ativas — todas da p.13; Pediátrico 0
   - Ativas:
     - Ambulatorial: 407e5dfc laboratoriais (<3 m); 27acf724 Alto Custo; 92b2ba67 'ECG e/ou Eco ou TE ou Cintilografia'; a741ae4a Pergunta domicílio; 82dc78ba global
     - Internados: 77b3554e laboratoriais; b254aa9b Alto Custo; 8de3d0b8 Pergunta hospitalar/leito; 979e0de3 trombólise/enzimas; e48ade81 ECG e/ou Eco; 81febf81 Pergunta 'HT<27 e Hb<9,0' Sim-bloqueia; 0e5fe3fd Informativa IR; 1ed71fe3 global
   - Inativas:
     - Internados: 59ece322 e 86480e7d (v1, substituídas)
   - Pedidos no espelho: Ambulatorial: 6 (A conferir)
6. **Veredito:** **SO_ORIENTACAO**
7. **Problemas:**
   - O preparo do cateterismo é cópia literal do da angioplastia: manda trazer 'Cateterismo (laudo e filme)' ao próprio cateterismo e iniciar AAS + Clopidogrel 3 dias antes de um exame diagnóstico. Inconsistência do manual — não promover nada disto a bloqueio.
   - Se virar Informativa, NÃO pendurar no Cateterismo Pediátrico (posologia de adulto).
8. **Proposta (modelo atual):**
   - Informativa só no Cateterismo Cardíaco (Ambulatorial); no Internados, apenas o que cabe a internado (jejum, medicação, alergia a iodo).
   - *Exigiria regra condicional:* nada

### D40 — TESTE CARDIOPULMONAR DE EXERCÍCIO (ERGOESPIROMETRIA) (preparo)

1. **Seção / página:** 4.18.4 Preparo prévio para Teste Cardiopulmonar de Exercício (Ergoespirometria) — p.43
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - *trazer no dia* — a. identidade, CPF, comprovante de residência, cartão do SUS, pedido; b. agendamento do SER (espelho) com o número da chave de autorização; c. identidade do acompanhante
   - *preparo* — d. chegar 30 minutos antes; e. Homem: aparelho de barbear descartável; f. Mulher: sutiã ou top; g. bermuda, toalha e tênis; h. manter medicações; i. NÃO estar em jejum nem comer demais; j. dirigir-se ao NIR
4. **Recurso no SER:** não achado (nenhum 'Ergoespirometria'/'Teste Cardiopulmonar' no NAO_AE; o AE tem 'TESTE DE ESFORCO OU TESTE ERGOMETRICO 2', que é outro exame)
5. **Nosso:** —
   - Ativas: nenhuma
   - Pedidos no espelho: —
6. **Veredito:** **SEM_RECURSO_NO_SER**
7. **Problemas:**
   - Sem recurso. 'Homem/Mulher' são orientações de vestimenta, não elegibilidade — não criar Dedutível de sexo.
8. **Proposta (modelo atual):**
   - Nada.
   - *Exigiria regra condicional:* nada

### D41 — ELETROENCEFALOGRAMA (preparo)

1. **Seção / página:** 4.18.5 Preparo prévio para Eletroencefalograma — p.44
2. **Estrutura:** SO_ORIENTACAO
3. **Requisitos (literal):**
   - *trazer no dia* — a. identidade, CPF, comprovante de residência, cartão do SUS, pedido; b. agendamento do SER (espelho) com o número da chave de autorização
   - *preparo* — c. lavar bem os cabelos com sabonete neutro, secos no início do procedimento; d. nenhum cosmético no cabelo; não suspender medicamentos de uso contínuo, mas informá-los ao médico
4. **Recurso no SER:** EEG Simples Adulto [v1122] e EEG Simples Infantil [v1121]
5. **Nosso:** EEG Simples Adulto / EEG Simples Infantil — 0 regras
   - Ativas: nenhuma
   - Pedidos no espelho: ver D33/D34
6. **Veredito:** **SO_ORIENTACAO**
7. **Problemas:**
   - Nada barra. Informativa sozinha não tira o pedido de 'Sem regras'.
8. **Proposta (modelo atual):**
   - Informativa nos dois EEG (junto com as regras propostas em D33/D34).
   - *Exigiria regra condicional:* nada
