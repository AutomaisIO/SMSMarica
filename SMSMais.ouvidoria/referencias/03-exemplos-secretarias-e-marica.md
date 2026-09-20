# Ouvidorias de saúde no Brasil — casos concretos, Maricá e o que ensinam sobre requisitos

> Pesquisa web de 20/09/2026 (agente de pesquisa, sessão Claude Fable 5.1), com extração de ~20 PDFs. Onde a fonte não foi lida diretamente está marcado **[não confirmado]**. Extratos em [`fontes/`](./fontes/) (`marica_2T2025.*`, `marica_3T2025.*`, `marica_4T2025.*`, `rio_decreto_44746_2018.*`, `smssp_*`, `sesrj_manual_ouvidorias_descentralizadas.txt`, `sesa_es_relatorio_gerencial_ouvidoria_2024.txt`, `ouvsus_relatorio_gestao_2024.txt`, `inaja_pe_decreto_006_2025_regulamento_13460.txt`, `pr_manual_implantacao_ouvidorias_sus.txt`, `abo_ouvidoria_sus_sao_paulo_2023.txt`).

---

## 0. Síntese — 14 lições para o requisito de sistema

1. **Duas instâncias, não uma.** Rio (Decreto 44.746/2018), Maricá (Central 156 = "primeira instância"), Curitiba e BH ("a Ouvidoria não substitui os canais de solicitação de serviço") separam *solicitação de serviço* (atendimento/protocolo) de *manifestação de ouvidoria*. No Rio, "reclamação" só existe sobre um protocolo 1746 anterior; sem protocolo é "crítica". O sistema precisa de **vínculo manifestação → protocolo/atendimento de origem**.
2. **Tipologia legal fixa** (Lei 13.460): reclamação, denúncia, sugestão, elogio, solicitação (+ "informação"/LAI; alguns acrescentam "comunicação" anônima — Inajá; "crítica" — Rio; "orientação" — BH). Tipo + **assunto/subassunto** obrigatórios na conclusão. BH e SMS-SP publicam ranking por *serviço* ("Consulta Médica ESF", "Marcação de Exame") — o catálogo tem de ser da saúde.
3. **Prazo 30 dias corridos, prorrogável uma vez por 30, com justificativa registrada.** **Prazo interno da área menor**: SP 20 dias e 2 dias úteis para trânsito; Inajá 20; SES-RJ pede resposta em 10 dias e usa **prioridade** (urgente 2 dias úteis; comum 15; com processo 30). Complementação suspende o prazo.
4. **Três modos de identificação**: identificada, sigilosa (identidade não segue para a área) e anônima (BH, Curitiba, Recife, SES-RJ; Fala.BR só em denúncia). Anônima **não recebe resposta** (Curitiba); denúncia anônima exige "indícios mínimos de relevância, autoria e materialidade".
5. **Protocolo + senha/acompanhamento sem cadastro** (BH, Fortaleza, Fala.BR) vs. exigência de gov.br (SMS-SP) e CPF/CNS (Recife). Trade-off de acessibilidade.
6. **"Ponto de resposta"/interlocutor na unidade é entidade de primeira classe.** SP tem ~1.400 (2022) a ~2.000 (2025) pontos de resposta com login individual; Inajá exige ouvidor setorial titular + suplente por portaria. A ouvidoria **não apura** — encaminha, cobra e devolve; a área responde; a ouvidoria "audita a qualidade da inserção e da resposta" (SMS-SP) e devolve para reanálise se insatisfatória (Curitiba).
7. **Resolutividade é campo obrigatório na conclusão** (CGU 116/2024 art. 29), mas o Manual MS 2014 adverte: é indicador **da rede**, não da ouvidoria; meta sugerida 70%. BH publica: demanda atendida 60,9%, gerou orientação 36,4%.
8. **Indicadores mínimos publicados**: volume por tipo/assunto/canal/regional-unidade; % respondidas; % no prazo (faixas 1–30 / 31–60 / >60 — BH e SESA-ES); **TMR** (Recife 17 dias; PR 10); **estoque** (Rio: VME); **satisfação** (Recife 10% respondem, 60% satisfeitos). Rio fixa metas: PMR >90%, TMA <10 dias, estoque ≤10% do volume de 30 dias.
9. **Relatórios**: mensal ao gestor (Rio; SMS-SP), trimestral por unidade (SMS-SP com "considerações do ouvidor" **e** "do gestor"; Maricá geral), anual até 28/02 (Rio), ao **Conselho Municipal de Saúde e Conselhos Gestores** (SMS-SP Portaria 152/2026).
10. **Canais**: telefone dominante (OuvSUS 2024: 30% telefone local + 23% 136; BH 53%; SES-RJ 57%); presencial relevante em hospital (Tide Setubal 46%); **urna** ainda existe (SES-RJ tem POP; OuvSUS 0,95%); WhatsApp marginal onde não é canal principal (BH 0,15%), mas é *o* canal em Maricá.
11. **Um sistema único de registro** é mandado por norma (SP; Rio: papel digitalizado "imediatamente"; CGU: tudo entra no Fala.BR).
12. **Vínculo com contratos de gestão/OSS**: SMS-SP usa prazo de resposta da ouvidoria como indicador qualitativo das OSS (Portaria 333/2022) — exportar por unidade/prestador.
13. **IA**: Fala.BR classifica o tipo por IA desde 06/04/2026; OGE-MG regulamentou IA para "verificação de plausibilidade" (Resolução 10/2025) [não confirmado]; chatbot "Zé Gotinha" do MS é para **disseminação**, não registro.
14. **Problema recorrente = atraso e estoque**, não ausência de canal: Maricá 10,2% "atrasados" (2T/2025); Recife 75% no prazo; SESA-ES 623 manifestações >60 dias.

---

## 1. Maricá-RJ (prioridade)

**Estrutura e base legal**
- **Ouvidoria Geral do Município**, Ouvidora-Geral Barbara Machado da Costa. https://www.marica.rj.gov.br/orgao/ouvidoria-geral-do-municipio/ e https://www.marica.rj.gov.br/ouvidoria/
- Base legal citada na página (texto integral não lido): **Decreto Municipal nº 78/2025**; LC 398/2024 (estrutura administrativa; alterada pelas LC 405/2025 e 408/2025); Lei Orgânica art. 127. **Não localizada** lei municipal específica de ouvidoria nem regulamento local da Lei 13.460 com prazos. [não confirmado]
- LAI municipal: **Lei nº 3.073/2021** (https://esic.marica.rj.gov.br/lei_acesso/arquivos/regulamentacao_da_LAI_municipal.pdf). **e-SIC próprio** em https://esic.marica.rj.gov.br . **Sem evidência de adesão ao Fala.BR** nem de uso do OuvidorSUS; a página de "Ouvidorias Municipais" da SES-RJ exibe "0 resultados" para Maricá.
- Processos internos rodam no **1Doc** ("Prefeitura Sem Papel", Codemar). Portal de serviços na plataforma **ConectaBR** (https://marica.plataformaconectabr.com.br/) e **Portal do SIM** (https://sim.marica.rj.gov.br/). Não confirmado em qual sistema a ouvidoria registra manifestações; os relatórios não nomeiam sistema.

**Canais**
- **Central 156** (lançada 10/07/2023; seg–sex 8h–20h; 18 teleatendentes + 4 supervisores; operada pelo **Instituto Tero**, Contrato 374/2022 prorrogado em out/2025 — JOM 1796). Os relatórios da Ouvidoria descrevem o 156 como **"primeira instância de resolução de demandas", "sob a responsabilidade da Ouvidoria"**; recebe por WhatsApp **(21) 2042-7222**, telefone e e-mail; segmentado por secretaria em "informações" e "solicitações".
- **Ouvidoria**: WhatsApp **(21) 99506-4638** (seg–sex 8–20h), telefone (21) 3731-1467, e-mail ouvidoria@marica.rj.gov.br, presencial nos polos do **SIM** (Centro – Rua Álvares de Castro 272; Itaipuaçu – Rua Van Lerbergue 6766; São José do Imbassaí – RJ-106 km 21), formulário web (nome, CPF, telefone, endereço, mensagem).
- **Alô Saúde: WhatsApp (21) 99140-0674** — canal da Ouvidoria *e* telefone institucional da Secretaria de Saúde. Os relatórios trimestrais contam o Alô Saúde como fonte de dados. Sem descrição pública de escopo, horário, script ou sistema. Sem urna, sem app próprio, sem 0800.
- Tipos aceitos: elogios, reclamações, denúncias (site); relatórios acrescentam sugestões. Protocolo: "número de atendimento com prazo para resposta/execução" — **prazo não publicado**. Anonimato/sigilo: **não informado**.

**Relatórios trimestrais publicados** (gráficos são imagens; só o texto foi extraível)
- 2º tri/2025 (`fontes/marica_2T2025.txt`; https://www.marica.rj.gov.br/wp-content/uploads/2025/10/RELATORIO2TREIMESTRE2_638950292655239906.pdf): **arquivados 64,9%, em andamento 24,8%, atrasados 10,2%**. Saúde: foco de dengue 38, falta de médico 6, falta de medicamento 6, CDT 4; "reclamação de processo atrasado" 60; "reclamação de posto" 45.
- 3º tri/2025 (`fontes/marica_3T2025.txt`): arquivados 82,05%, andamento 12,67%, atrasados 5,28%. Saúde (55): reclamação à UBS 29, Farmácia Judicial 11, dengue 8, Ambulatório Péricles Siqueira 7.
- 4º tri/2025 (`fontes/marica_4T2025.txt`; https://www.marica.rj.gov.br/wp-content/uploads/2026/01/4TRIMESTREDE20251_639040002624804284.pdf): arquivados 87,16%, andamento 6,05%, atrasados 6,79%. Saúde (81): UBS 33, **Central de Regulação 23**, Farmácia Judicial 8, Péricles Siqueira 7, Hospital Conde Modesto Leal 5, dengue 5. Considerações: "aperfeiçoar a regulação para garantir mais transparência e agilidade"; "qualificar o atendimento nas UBS"; "investir em sistemas que agilizem processos".
- Notícia 2º tri/2026: **78% solucionadas, 559 encerradas com resposta, 15% aguardando retorno dos setores** — https://www.marica.rj.gov.br/noticia/prefeitura-de-marica-soluciona-mais-de-78-das-demandas-da-populacao-com-servicos-da-ouvidoria
- **O que não é publicado**: tempo médio de resposta, % no prazo legal, canal por volume, satisfação, resolutividade por unidade, relatório ao Conselho Municipal de Saúde. Categorias criadas ad hoc ("Reclamação de Posto") — sem árvore padronizada.

**Contexto noticiado**
- Audiência pública 29/05/2026: vereador exibiu "mala com mais de mil denúncias" (falta de ~22 medicamentos, insulina, fitas de glicemia, demora em marcação); o secretário pediu "encaminhamento formal da documentação" — as queixas **não passaram pela Ouvidoria**, sinal de canal paralelo (gabinete parlamentar). https://www.noticiadorio.com/post/com-or%C3%A7amento-bilion%C3%A1rio-sa%C3%BAde-de-maric%C3%A1-vira-alvo-de-cobran%C3%A7as-e-vereador-exibe-mala-com-mais-de-m
- CMS Maricá: https://www.marica.rj.gov.br/conselhos_municipais/conselho-municipal-de-saude-de-marica/ — nenhuma ata citando a Ouvidoria localizada.

**Lição Maricá**: há canais (156, WhatsApp, Alô Saúde, presencial) e relatório trimestral, mas **não há ouvidoria setorial da saúde identificada, não há prazo publicado, não há sistema nomeado, não há métrica de tempo**, e "atrasados" não é definido contra prazo legal. O gestor da unidade (UBS, Regulação, HCML) aparece só como rótulo de categoria — não há evidência pública de fluxo "responsável pela resposta" por unidade. A **Central de Regulação** já é o 2º alvo de reclamações de saúde (4T/2025), o que liga o módulo diretamente ao ecossistema de regulação do SMSMais.

---

## 2. SMS São Paulo — Rede de Ouvidorias SUS (o caso mais maduro)
- **Divisão de Ouvidoria do SUS** + **55–60 unidades descentralizadas** + **~1.400 a ~2.000 "pontos de resposta"** com login individual. 20 anos de rede. https://prefeitura.sp.gov.br/web/saude/w/rede-de-ouvidorias-sus-quem-somos ; artigo ABO 2023 (91.835 manifestações em 2022): `fontes/abo_ouvidoria_sus_sao_paulo_2023.txt`
- Canais: **Central SP156**, formulário web (**exige gov.br**), presencial nas STS/hospitais. A página lista o que a ouvidoria **não** faz (agendar, SAC, fiscalizar).
- Sistema: OuvidorSUS desde Portaria 757/2015 → **SIGRC (SP156) em set/2025**, obrigatório pela **Portaria SMS.G 870/2025** ("Unidade de Ouvidoria" registra; "Ponto de Resposta" responde; login pessoal; inativação imediata na saída; 1.700+ chamados de suporte pós-implantação).
- **Portaria SMS.G 152/2026**: recepção → protocolo → análise/complementação → encaminhamento → decisão; **30 dias ao cidadão; 20 à área; trânsito ≤ 2 dias úteis; prorrogação com justificativa**; anonimato permitido exceto em solicitações; relatório no mínimo anual com **parecer do gestor**, ao **CMS e aos Conselhos Gestores**; **pesquisa de satisfação no mínimo anual**.
- Relatórios anuais, semestrais, **trimestrais por unidade** e boletim mensal: https://prefeitura.sp.gov.br/web/saude/w/ouvidoria/267334 . Modelo (HM Tide Setubal 1T/2024, `fontes/smssp_relatorio_tide_setubal_1t2024.txt`): panorama, série mensal, **meios** (46% pessoalmente, 45% telefone, 4% web), classificação (73% reclamação), assuntos por tipo, elogios nominais ("Gente que faz o SUS"), prazo, **PAQ – Programa de Avaliação da Qualidade**, plano de ação, **considerações do ouvidor e do gestor**.
- Histórico (`fontes/smssp_ouvidoria_central_slides.txt`): **prazo por categoria** — denúncia 90 dias, reclamação 30, solicitação/informação/elogio/sugestão 15; resolutividade 50,9% (2015) → 76,6% (2016); **indicador de ouvidoria em contrato de gestão de OSS**; relatório para o CMS desenhado com Instituto Pólis; POPs (inserção, tipificação, fechamento, resolubilidade).

## 3. SMS Rio de Janeiro — 1746 + Sistema Municipal de Ouvidoria
- **Decreto Rio 44.746/2018** (`fontes/rio_decreto_44746_2018.txt`): art. 4º — **solicitações de serviço** entram pela Central 1746; **reclamações, denúncias, críticas, sugestões, elogios e pedidos de informação** vão ao Sistema Municipal de Ouvidoria. *Reclamação* = "insatisfação relativa a uma solicitação já realizada" (exige protocolo); *crítica* = "opinião desfavorável" sem solicitação prévia. Art. 16: papel digitalizado imediatamente; **art. 17: "em nenhuma hipótese será recusado o recebimento"**. Art. 27: **30 dias, prorrogável com justificativa**. Relatórios **mensal**, **anual até 28/02**, denúncias **bimestralmente ao Corregedor**. SMS com **Ouvidores Regionais nas CAPs**.
- Ouvidoria SMS: "segunda instância"; presencial na sede + **10 CAPs + 6 hospitais**. https://saude.prefeitura.rio/ouvidoria/
- **Painéis da OGM** (https://ouvidoria.prefeitura.rio/paineis-de-ouvidoria/): % dentro/fora do prazo por órgão, **estoque mensal**, resolutividade; indicadores **PMR**, **TMA**, **VME** com metas **>90%, <10 dias, ≤10%**. Rede: 47 ouvidorias, 167 agentes.

## 4. SMS Belo Horizonte (SMSA) — melhor relatório aberto encontrado
- Ouvidoria municipal única com módulo SUS: https://prefeitura.pbh.gov.br/ouvidoria/sus . Canais: **156**, Portal, **PBH APP**, presencial; **30 + 30**; **anônima, nominal ou sigilosa**; **protocolo + senha**.
- **Relatório SMSA 2024** (https://prefeitura.pbh.gov.br/sites/default/files/estrutura-de-governo/controladoria/ouvidoria/2024.jan-dez.relatorio-de-servicos.smsa_.pdf): **13.336 manifestações**; **89,75% reclamações**. Top serviços: **Consulta Médica ESF 24,12%**, Comunicação de Irregularidade 5,99%, **Marcação de Exame 5,73%**, Servidor Público 5,37%, Exames de laboratório 4,78%, Cirurgia 4,33%, Acesso a medicamentos 3,41%. **Prazo: 88,64% em 1–30 dias, 7,97% em 31–60, 2,65% >60.** Canais: **telefone 53,10%, OuvidorSUS 17,31%, Portal 13,41%, mobile 12,84%**, WhatsApp ~0,15%. Resultado: **atendida 60,93%, gerou orientação 36,43%**. **56,32% encaminhadas às 9 Regionais**, com tabela regional × especialidade (ortopedia 12,74%, cardiologia 7,75%…).
- Lição: taxonomia por **serviço** e desdobramento por **regional/unidade** e **especialidade** — o que a regulação precisa consumir.

## 5. Curitiba — Central 156 + Ouvidoria do SUS
- **0800-644-0041**, presencial; fluxo recebimento → análise → encaminhamento → acompanhamento → verificação → resposta. https://saude.curitiba.pr.gov.br/conteudo/ouvidoria-do-sus-curitiba/1366
- E-Cidadão (CGM): **identificada**, **sigilosa**, **anônima** (sem resposta); 30 dias prorrogáveis; resposta insatisfatória → "a CGM retornará o protocolo para reanálise".

## 6. Fortaleza — Ouvidoria Digital (CGM) + SMS
- **57 ouvidorias setoriais**; web + app; **156**; WhatsApp (85) 98814-4478; **anônimo permitido em todos os tipos; protocolo + senha**. https://ouvidoria.cgm.fortaleza.ce.gov.br/
- Jan–jun/2023: **17.018 manifestações, 91,46% tratadas; internet 56,31%, e-mail 21,80%, presencial 8,10%**; **SMS = 51,70%** do total com a Agefis.
- SMS: **"Ouvidoria Digital" dentro do app Mais Saúde Fortaleza** (mesmo app de agendamento/fila da UPA). Lição: ouvidoria embutida no app do cidadão.

## 7. Recife — Ouvidoria Municipal da Saúde + OGMR
- **0800 281 1520**, presencial, **ConectaZap (81) 9117-1407**, e-mail, formulário; 6 tipos; **identificada / sigilosa / anônima**; exige CPF ou CNS; 30 dias. https://conecta.recife.pe.gov.br/servico/1295
- Relatório 2024 (CGM): **42.192 manifestações, 97% respondidas; TMR 17 dias (99 → 45 → 31 → 17, 2021–2024); 75% no prazo; 10% responderam pesquisa, 60% satisfeitos; Saúde = 35% dos registros.**

## 8. SES-RJ
- Lei estadual **7.989/2018**; **0800-0255525**; https://www.saude.rj.gov.br/ouvidoria/sobre-a-ouvidoria
- **Manual das Ouvidorias Descentralizadas** (`fontes/sesrj_manual_ouvidorias_descentralizadas.txt`; POPs por canal: presencial, telefônico, **busca ativa no leito**, e-mail, impresso, **urna**): **prioridade define prazo (Res. SES 207/2011 art. 10): urgente 2 dias úteis; sem urgência 15; com processo 30**; ofício-modelo pede resposta em **10 dias**; sigilo = não encaminhar contatos; anônima aceita por impresso/urna/e-mail "desde que haja possibilidade de apuração"; 3 tentativas de contato antes de encerrar; **arquivamento automático 60 dias após fechamento**.
- 1º tri/2023: 5.381 atendimentos; **telefone 57%, web 32%, e-mail 7%**; informação 50%, solicitação 25%, reclamação 16%, denúncia 5%.

## 9. SES-SP — Sistema Ouvidor SES/SP
- Sistema próprio com **subsistemas para ouvidorias estaduais e municipais**, painéis, pesquisa de satisfação, **dados abertos JSON** (https://nies.saude.sp.gov.br/ses/ouvidoria). SP respondeu **224.358 manifestações no OuvidorSUS em 2024 (36,6% do total nacional)**.

## 10. SES-MG / OGE-MG
- Ouvidoria de Saúde dentro da **OGE**: **162**, 136, WhatsApp, **chatbot**; "Ouvidoria Móvel". https://www.ouvidoriageral.mg.gov.br/ouvidorias-tematicas/ouvidoria-de-saude
- **Resolução OGE nº 10/2025** — primeira norma de ouvidoria pública sobre IA (verificação de plausibilidade) [não confirmado].

## 11. Benchmark complementar
- **OuvSUS Relatório 2024** (`fontes/ouvsus_relatorio_gestao_2024.txt`): **618.727 manifestações**; canais: telefone estados/municípios 30,36%, 136 22,66%, **pessoalmente 20,10%**, internet 14,36%, correios 3,14%, Fala.BR 2,76% ("problemas de integração"), **caixa de sugestão 0,95%**. Esfera municipal: **solicitação lidera (231.692)**; temas: "Ambulatório média complexidade" 38.486, "Rotinas/protocolos de unidade" 11.452, oftalmologia 9.298, "insatisfação: médico" 8.162, ortopedia 7.958, "demora no atendimento" 7.108.
- **SESA-ES 2024** (`fontes/sesa_es_relatorio_gerencial_ouvidoria_2024.txt`): 13.146 manifestações; **76,9% respondidas em ≤15 dias; 623 >60 dias**; 401 sigilosas, 1.563 anônimas, 10.681 identificadas; dois sistemas em paralelo — histograma de tempo por faixas de 5 dias.
- **Decreto Inajá-PE 006/2025** (`fontes/inaja_pe_decreto_006_2025_regulamento_13460.txt`): regulamento municipal-modelo da Lei 13.460 — ouvidor setorial titular + suplente por portaria; 20 dias à área; complementação suspende. Bom molde para o ato normativo de Maricá.
- **Manual do Ouvidor do Paraná** (`fontes/pr_manual_implantacao_ouvidorias_sus.txt`): implantação de ouvidorias do SUS municipais.

---

## 12. Requisitos funcionais extraídos de normas, manuais e TRs
**Sobre TRs de licitação**: não localizado TR municipal recente para *sistema de ouvidoria isolado*; o mais específico é o da CGU para o **e-Ouv** (https://lawinsider.com/pt/contracts/7GKcHeLweSm). Os requisitos abaixo vêm dele + Fala.BR + OuvidorSUS + PN CGU 116/2024 + Portarias SMS-SP 152/2026 e 870/2025 + Decreto Rio 44.746/2018 + Decreto Inajá 006/2025 + Manual MS 2014 + Manual SES-RJ.

**Registro**
- Tipos: reclamação, denúncia, sugestão, elogio, solicitação, informação (+ opcional "crítica"/"comunicação anônima"); **assunto e subassunto obrigatórios**, árvore da saúde, com **tags livres**.
- Canal de entrada como campo (telefone, presencial, web, app, WhatsApp, e-mail, carta, **urna**, busca ativa, itinerante); todo registro em papel/telefone entra no mesmo sistema.
- Identificação: identificada / **sigilosa** / **anônima**; **pseudonimização** de denúncias antes de encaminhar.
- Vínculo com **protocolo de atendimento/solicitação anterior** e com **unidade (CNES)**, paciente (CNS/CPF).
- Protocolo + senha/link, acompanhamento sem login; alternativa gov.br.
- **Prioridade** que altera prazo (SES-RJ). Desmembramento. "Nunca recusar recebimento".

**Tramitação**
- **Ponto de resposta / interlocutor por unidade**, titular e suplente, login pessoal, inativação na saída.
- Encaminhamento imediato após triagem; a órgão externo sem prorrogação; livre entre ouvidorias.
- Prazos configuráveis: cidadão 30+30; área 20 (+20); trânsito 2 dias úteis; **complementação suspende** e arquiva após prazo; alertas e **cobrança automática de atraso**.
- Resposta intermediária e **conclusiva** com classificação final e **resolutividade**; devolução para reanálise; **auditoria da qualidade da inserção e da resposta** (PAQ).
- Arquivamento automático N dias após fechamento; registro de tentativas de contato (3).
- Respostas padronizadas, busca por semelhança, sugestão de resposta, exportação para processo (SEI/1Doc).

**Cidadão**
- Notificação por e-mail/WhatsApp/SMS a cada etapa; **pesquisa de satisfação** na conclusão; base de conhecimento.

**Gestão e transparência**
- Painéis: volume por tipo/assunto/canal/**unidade/regional/prestador**, **% no prazo por faixas**, TMR, **estoque**, PMR, resolutividade, satisfação; metas configuráveis.
- Relatórios: mensal ao gestor, trimestral por unidade (considerações do ouvidor e do gestor), anual, **exportação para CMS e contratos de gestão/OSS**; dados abertos anonimizados.
- Perfis: gestor, ouvidor, triador, respondente/ponto focal, atendente, leitura para controle social; **logs de acesso** e sigilo de 100 anos para identidade.
- Integrações: Fala.BR (API), OuvidorSUS, e-SIC/LAI, 156/call center, app do cidadão.
- Não-funcionais: responsivo, eMAG, LGPD, linguagem simples.
- **IA** (2026): classificação automática do tipo (Fala.BR), sugestão de assunto/órgão, plausibilidade de denúncias (OGE-MG). Nenhum caso de IA **respondendo** ao cidadão.

## 13. Lacunas
- Texto do **Decreto 78/2025 de Maricá**; sistema que a Ouvidoria de Maricá usa; ouvidoria setorial da Saúde ou ouvidor do SUS cadastrado no MS; escopo formal do "Alô Saúde".
- Números por secretaria/tipo/canal de Maricá (gráficos sem OCR).
- Auditorias de **TCE/TCU** sobre ouvidorias de saúde municipais: nada específico encontrado.
- Relatório anual consolidado 2024 da Rede SMS-SP (não lido).

## Fontes principais (além das inline)
- Portaria GM/MS 2.416/2014: https://bvsms.saude.gov.br/bvs/saudelegis/gm/2014/prt2416_07_11_2014.html
- PN CGU 116/2024: https://www.in.gov.br/en/web/dou/-/portaria-normativa-cgu-n-116-de-18-de-marco-de-2024-549091878
- Fala.BR: https://wiki.cgu.gov.br/index.php/Fala.BR_-_M%C3%B3dulo_Ouvidoria ; adesão: https://www.gov.br/ouvidorias/pt-br/ouvidorias/rede-de-ouvidorias/adesao-e-cadastros/adesao-fala.br
- OuvidorSUS wiki: https://wiki.saude.gov.br/ouvidor/index.php/P%C3%A1gina_principal
- Manual das Ouvidorias do SUS (MS, 2014): https://bvsms.saude.gov.br/bvs/publicacoes/manual_ouvidoria_sus.pdf
- Manual de Ouvidoria Pública (CGU): https://repositorio.cgu.gov.br/bitstream/1/29959/14/manual_de_ouvidoria_publica.pdf
- Decreto Inajá-PE 006/2025: https://inaja.pe.transparenciamunicipal.online/uploads/5210/1/atos-oficiais/2025/decretos/1741953858_decreto--insititui-e-regulamenta-ouvidoria--inaja.pdf
