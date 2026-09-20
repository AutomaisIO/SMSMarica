# Indicadores, processo interno e boas práticas de ouvidoria em saúde pública

> Pesquisa web de 20/09/2026 (agente de pesquisa, sessão Claude Fable 5.1). Legenda: **[C]** = confirmado na fonte (texto lido); **[S]** = só via snippet de busca; **[I]** = inferência.
> Extratos de texto dos PDFs lidos estão em [`fontes/`](./fontes/) (documentos públicos, sem dado de paciente).

---

## 1. Indicadores

### 1.1 Base federal (CGU / Fala.BR) — o que o Painel "Resolveu?" mede [C]
Fonte: https://wiki.cgu.gov.br/index.php/Ouvidoria_-_Pain%C3%A9is_e_Dados_Abertos · Painel: http://paineis.cgu.gov.br/resolveu/index.htm

- **Total de manifestações**, **Em tratamento** ("ainda não respondidas conclusivamente, nem arquivadas"), **Respondidas** (com % **dentro/fora do prazo**), **Arquivadas**, **Encaminhamentos** (a outro órgão por incompetência).
- **Tempo médio de resposta**: apropriado a cada manifestação, somado e dividido pela quantidade.
- **Taxa de resolutividade**: última resposta conclusiva marcada como resolvida / total. Quem marca é a **ouvidoria**, no envio da resposta conclusiva (Portaria Normativa CGU 116/2024, art. 29 §único: "não resolvida enquanto persistirem providências a serem adotadas pela unidade interna responsável"; art. 30: pode ser alterada a qualquer momento).
- **Satisfação do usuário**: pesquisa facultativa após a resposta conclusiva; escala 5 pontos convertida em % (muito insatisfeito 0, insatisfeito 25, regular 50, satisfeito 75, muito satisfeito 100). O painel também traz "percepção quanto à resolutividade" na visão do cidadão ("sua demanda foi resolvida?") [S].
- **Dados abertos** mensais, pseudonimizados: data de registro, prazo, faixa etária, raça/cor, gênero, município, tipo, órgão, assunto, **dias para resolução**, **dias de atraso**. → Modelagem: guardar `dias_ate_resposta` e `dias_atraso` como colunas derivadas.

### 1.2 Indicadores clássicos com fórmula [C]
Artigo ABO (Associação Brasileira de Ouvidores, caso ARSAE-MG): https://revista.abonacional.org.br/files/edicoes/artigos/1_10.pdf
- **Índice de resolubilidade** = resolvidas × 100 / recebidas.
- **Índice de atendimento aos prazos** = encerradas em até N dias × 100 / total (o indicador "sensível": caiu de 93% para 78% em 3 anos).
- **Prazo médio de resposta** = média(data resposta − data registro).
- Os três "são de fácil obtenção e monitoramento e refletem de forma clara o trabalho da equipe"; recomenda metas explícitas.

### 1.3 Referência "por 1.000" (ANS) [C]
https://www.ans.gov.br/aans/noticias-ans/consumidor/4693-ans-divulga-relatorio-de-ouvidorias-2018
- **TDO — Taxa de Demandas de Ouvidoria** = demandas por 1.000 beneficiários (9,2 no setor). RN 323/2013: resposta conclusiva ≤ 7 dias úteis, pactuável até 30 dias úteis.
- Mix de tipos no setor: consulta 52,9%, reclamação 42,4%, elogio 3,3%, sugestão 1,1%, denúncia 0,3%.
- [I] No SUS o denominador natural é **atendimentos da unidade no período** (contrato de Salto/SP usa isso) ou população adscrita da UBS.

### 1.4 Números reais de redes SUS (para calibrar metas)
- **SMS-SP, Hospital Tide Setubal** (relatório trimestral): 114 manifestações/trim (38/mês); canais presencial 46%, telefone 45%; reclamação 73%, solicitação 15%, elogio 11%, denúncia 1%; reclamações 84% no assunto "Gestão" (54% estabelecimento/rotinas, 36% RH/atendimento, 10% materiais) [C — `fontes/smssp_relatorio_tide_setubal_1t2024.txt`].
- **Rio** (Sistema Municipal de Ouvidoria + 1746): ~258 mil manifestações em 2025, **92% no prazo, tempo médio 12 dias**, 150 profissionais [C]: https://prefeitura.rio/casa-civil/sistema-municipal-de-ouvidoria-completa-25-anos-com-92-das-demandas-respondidas-e-anuncia-melhor-atendimento-em-2025/
- **Manaus** (Ouvidoria SUS/Semsa) 2022-25: 20.198 manifestações; 11.938 exigiram resposta técnica; **resolutividade 97,46%**; separa "módulo de registro" (precisa de área técnica) de "gestão de conteúdo" (respondida na hora) e SIC [C]: https://www.manaus.am.gov.br/semcom/prefeitura-semsa-ouvidoriasus/
- **Hospital Centenário (RS)** 1º sem/2026: 580 manifestações, **80% resolvidas**, tempo médio 20–30 dias; canais presencial 50%, "Zap Saúde" 30%, telefone 10%, OuvidorSUS 10% [C]: https://www.startcomunicacaosl.com.br/post/ouvidoria-do-hospital-centen%C3%A1rio-registra-580-manifesta%C3%A7%C3%B5es-no-primeiro-semestre-de-2026-e-amplia-ac
- SES-SC "taxa de resposta supera 99%" [S]; SES-PR ">95% respondidas" [S].
- **SMS-SP Ouvidoria Central** (slides, `fontes/smssp_ouvidoria_central_slides.txt`): ofícios da Ouvidoria Geral do Município **90% respondidos no prazo**; faz "ranqueamento das unidades mais denunciadas" com a Auditoria; **PAQ** (Programa de Avaliação da Qualidade) audita a qualidade do registro e da resposta [C].

### 1.5 Indicadores contratuais de OS (contratos de gestão) [C]
- **ES – SESA/HEUE, Manual de Indicadores Qualitativos** (`fontes/es_heue_manual_indicadores_qualitativos.txt`; https://saude.es.gov.br/media/OSS/HEUE/MANUAL%20DE%20INDICADORES%20QUALITATIVOS%20-HEUE.pdf): "Atenção ao Usuário" = 25% da parte variável:
  - **Resolução de Reclamações** = resolvidas / recebidas × 100; **meta 85%/trimestre**; conta reclamações de SAU/Ouvidoria/**urna** e as detectadas na pesquisa de satisfação; só as "dentro da governabilidade da OS"; resposta válida exige **causa raiz** e evidências (plano de ação, ata); **primeira tratativa ≤ 7 dias úteis**; pendências ≤ 5 dias úteis; retorno ao cidadão "pelo canal de escolha do mesmo" para registrar satisfação final.
  - **Satisfação do Usuário** = (muito satisfeito + satisfeito) / questionários efetivos × 100; **meta 90%**; amostra mensal por setor, com identificação (nome, prontuário, leito, forma da pesquisa: presencial/telefone/urna), checagem por telefone.
- **PE – SES/PE** (HJMO, HRSM): "**Taxa de resolução das queixas recebidas**" + "Satisfação do usuário" [C]: https://portal.saude.pe.gov.br/wp-content/uploads/2025/12/HJMO-2%C2%B0-TRI.pdf
- **SP – SES/SP, contrato CGCSS 2025** (`fontes/sessp_contrato_cgcss_2025.txt`; https://ses.sp.bvs.br/wp-content/uploads/2025/09/E_CG-CGCSS-HI_100925.pdf): OS mantém Serviço de Ouvidoria **na unidade** (dias úteis 8–17h, ouvidor com superior completo, subordinado à autoridade máxima, sem acúmulo); **fluxo de 9 etapas** (recebimento → análise → encaminhamento → acompanhamento → resposta da área → análise e avaliação da resposta → devolutiva → conclusão → finalização); tudo no sistema Ouvidor SES/SP; "**vedada a utilização de WhatsApp para recebimento de manifestações**" e "vedado o processamento das denúncias fora do Sistema"; colaboradores também são usuários e a OS deve afastar retaliação. Indicador "Humanização e Ouvidoria" pesa 10–20%/trimestre.
- **Salto/SP – contrato IGATS** (`fontes/sp_salto_contrato_igats_2022.txt`): "[nº de manifestações **queixosas** / **total de atendimentos realizados mensalmente**] × 100" = taxa de reclamações por atendimento; resposta a queixas em **≤ 5 dias úteis**; pesquisa de satisfação mensal.
- **BA – Hospital Metropolitano** (`fontes/ba_hospital_metropolitano_contrato_gestao.txt`): pesquisa pós-hospitalização normatizada; medidas sobre queixas em ≤ 30 dias úteis; SAC com relatório mensal.
- Manual Rio (OS área saúde) veio como PDF-imagem, não legível.

### 1.6 PNASS [C]
Caderno PNASS 2015: https://www.gov.br/saude/pt-br/acesso-a-informacao/gestao-do-sus/programacao-regulacao-controle-e-financiamento-da-mac/publicacoes/caderno-pnass-2015.pdf
- Item **R30**: "Conta com ouvidoria ou outros tipos de serviços de escuta voltados para usuários" — exige funcionamento regular, **retorno das demandas ao usuário**, e "espaço institucional de discussão da informação trazida pelo processo de escuta e não apenas o envio delas às diferentes áreas". **Não conta a Ouvidoria Municipal** — tem que ser escuta da própria unidade.
- Questionário de usuários, pergunta 11: "**O(a) senhor(a) sabe onde reclamar quando não é bem atendido?**" — indicador de conhecimento do canal.

### 1.7 Reincidência / recorrência
- CGE-PR: **alerta automático de denúncias reincidentes** ("red flag") [S]: https://www.cge.pr.gov.br/Noticia/Denuncias-reincidentes-terao-alerta-automatico-na-Ouvidoria
- Portaria 116/2024, art. 4º IV: "busca pela produção de **soluções coletivas** a partir do conjunto de problemas individuais recorrentes"; art. 31 I: manifestação **duplicada do mesmo manifestante** é arquivada citando o protocolo original [C].
- [I] Sem fórmula padronizada. Sugestão: (a) reincidência do **cidadão** = nova manifestação do mesmo CPF sobre mesmo assunto×unidade em N dias após resposta conclusiva; (b) recorrência da **unidade** = nº mesmo assunto×unidade na janela; (c) reabertura contestada.

### 1.8 Lista consolidada para o modelo [I]
Volume por tipo/assunto/subassunto/canal/unidade; **manifestações por 1.000 atendimentos da unidade**; % respondidas no prazo (legal 30d e interno 20d); tempo médio de resposta e tempo médio da **área** (encaminhamento → resposta da área); % resolvidas (marcação da ouvidoria) vs % "resolvida" na percepção do cidadão; satisfação 5 pontos; taxa de resposta à pesquisa; reaberturas/contestações; reincidência; % denúncias encaminhadas a apuração e % arquivadas por falta de elementos; encaminhamentos a outro órgão; elogios por profissional (SMS-SP: programa "Gente que faz o SUS" com certificado trimestral aos elogiados [C]); ranking de assuntos e de unidades; qualidade do registro (PAQ SMS-SP: "encaminhamento incorreto", "falta de dados do paciente").

---

## 2. Processo interno padrão

### 2.1 Prazos legais e regulamentares
- **Lei 13.460/2017** art. 16: decisão final ao usuário em **30 dias, prorrogável por igual período** com justificativa; art. 14: mecanismos **proativos e reativos**; art. 15: **relatório de gestão anual** à autoridade máxima, publicado na íntegra; art. 23: pesquisa de satisfação **pelo menos anual**; art. 7º: Carta de Serviços; arts. 18–21: Conselho de Usuários. Texto: https://ouvidoria.prefeitura.rio/wp-content/uploads/sites/28/2022/03/LEI-FEDERAL-No-13460.pdf
- **Decreto 9.492/2018** (federal): áreas internas respondem à ouvidoria em **20 dias, prorrogáveis uma vez** [S]: http://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/decreto/d9492.htm
- **Portaria Normativa CGU 116/2024** (revogou a 581/2021) [C — `fontes/cgu_portaria_normativa_116_2024_dou.txt`]:
  - Art. 25: pedido de **complementação** ao cidadão — 20 dias; **suspende o prazo uma única vez**; sem resposta → arquiva sem resposta conclusiva.
  - Art. 26: encaminhamento a outro órgão "imediatamente após a triagem", máximo 30 dias, **sem prorrogação**.
  - Art. 27: áreas técnicas respondem em 20 dias (+20).
  - Art. 28: linguagem "precisa, objetiva, simples e acessível", resposta ao fato **primeiro**, institucional por último.
  - Art. 29: **conteúdo mínimo da resposta conclusiva por tipo** — elogio: informar encaminhamento ao agente e chefia; reclamação: análise do fato + providências; solicitação: providência ou possibilidade/forma/meio; sugestão: manifestação do gestor sobre adoção e prazo. No mesmo ato registra **resolvida/não resolvida**.
  - Art. 31: causas de **arquivamento** (duplicidade, texto confuso, falta de urbanidade, ataque à honra sem elementos, cópia só para conhecimento…).
  - Arts. 52–55: **resolução pacífica de conflitos** (mediação) — a qualquer tempo; não se aplica a denúncias.
- **Prazos internos mais curtos em redes de saúde** [C]:
  - **SMS-SP Portaria 152/2026**: 30 dias corridos ao cidadão; **áreas demandadas 20 dias corridos**; **unidades intermediárias no máximo 2 dias úteis** para repassar (art. 34): https://legislacao.prefeitura.sp.gov.br/portaria-secretaria-municipal-da-saude-sms-152-de-3-de-abril-de-2026
  - **SES-RJ** (Resolução SES 207/2011, art. 10): **prioridade 1 urgente = 2 dias úteis; 2 não urgente = 15 dias; 3 com processo administrativo = 30 dias**; ofício-padrão à área pede resposta em **10 dias** (`fontes/sesrj_manual_ouvidorias_descentralizadas.txt`).
  - **MS, Regulamento do OuvidorSUS** (Guia de Implantação, `fontes/ms_guia_implantacao_2ed_2014.txt`): encaminhar aos órgãos em **≤ 3 dias úteis**; conclusão por prioridade **Urgente 15 / Alta 30 / Média 60 / Baixa 90 dias**, contados do encaminhamento: https://bvsms.saude.gov.br/bvs/publicacoes/guia_orientacoes_implantacao_ouvidorias_sus.pdf
  - **GDF Portaria 157/2019 (Saúde)**: gestores respondem em **5 dias úteis**; unidades sem ouvidor **designam interlocutor** publicado em DO e capacitado; resposta a denúncia de má conduta contém **posicionamento formal do chefe imediato**; infrações vão à Unidade de Correição: https://www.sinj.df.gov.br/sinj/Norma/aca0b642947047369d94673a3d5c3e1d/Portaria_157_10_07_2019.html

### 2.2 Etapas (consenso das fontes) [C]
Guia MS: recebimento → registro → análise → **classificação** (tipo) → **tipificação** (assunto/subassunto) → encaminhamento → acompanhamento → resposta → conclusão. SES/SP: 9 etapas com "análise e avaliação da resposta da área" **antes** da devolutiva. SES-RJ POP: ao receber a resposta da área "avaliar se o conteúdo é claro e objetivo, se está relacionado ao relato e alinhado aos princípios do SUS"; "uma manifestação só deve ser fechada mediante uma resposta satisfatória, o que não significa que tenha que ser atendida"; a ouvidoria redige a resposta ao cidadão "em linguagem clara e objetiva". Prioridade e **data-limite** fixadas **antes** do encaminhamento.

### 2.3 Classificação (tipos) — definições DOGES/MS [C]
Denúncia (irregularidade), Reclamação (insatisfação **sem** requerimento), Solicitação (contém **requerimento** de atendimento/acesso), Sugestão, Elogio, Informação. SMS-SP 2026 separa **denúncia administrativa** (erário/cargo) de **denúncia de vigilância em saúde** (sanitária/condições de trabalho) e registra que solicitação **não pode ser anônima**. Mais de um tipo: escolher o predominante. OGE-SP (`fontes/ogesp_guia_tratamento_manifestacoes_2020.txt`): **reclassificação** pelo servidor é permitida e a estatística usa a classificação da ouvidoria, não a do cidadão.

### 2.4 Tipificação (assunto/subassunto)
Manual de Tipificação do OuvidorSUS é a referência nacional; OuvidorSUS 3 (2023) trouxe "**Tipificação Aprimorada**" com **TAGS próprias** por ouvidoria [S]: https://agenciagov.ebc.com.br/noticias/202311/saiba-mais-sobre-o-novo-ouvidorsus-plataforma-para-reclamacoes-e-elogios-relacionados-ao-sus · Dicionários de dados Ouv2/Ouv3: https://dadosabertos.saude.gov.br/dataset/ouvidorsus · Árvore em uso na SMS-SP: "Gestão" → Estabelecimento de Saúde / Recursos Humanos / Recursos Materiais; "Assistência à Saúde" → Cirurgia / Consulta-Atendimento-Tratamento / Diagnóstico / Transferência; "Transporte". → Modelagem: tipo (enum fechado) + assunto/subassunto (catálogo versionado) + tags livres da instância.

### 2.5 Papéis [C]
- **Ouvidor** (SES-RJ "Coordenador"): monitora indicadores, garante canais, orienta encaminhamento, envia relatórios à direção. **Ouvidor assistente / técnico**: recebe, identifica, registra, tipifica, acompanha, cobra. **Apoio administrativo**: acolhe presencial, verifica diariamente e-mail/**urnas**/cartas, agenda atendimento no leito.
- **Ponto de resposta** (SMS-SP): autoridade máxima de cada área indica **responsável e suplente**. **Ponto Focal** (OuvidorSUS): "recebe as demandas que lhe são encaminhadas pela sua ouvidoria e atua como ponto de resposta": https://wiki.saude.gov.br/ouvidor/index.php/PONTO_FOCAL
- OGE-SP: perfis de sistema — servidor **com** e **sem** acesso a manifestações sigilosas.
- Portaria 116 art. 24: ouvidoria tem **livre acesso a todos os setores** e participa de reuniões.

### 2.6 Escalonamento quando a área não responde
- SES-RJ: fora do prazo, "**cobrança** de resposta **reiterando o encaminhamento**", e o cidadão é informado que ainda não há pronunciamento. GDF: descumprimento vai à Unidade de Correição. SMS-SP: relatórios têm "parecer do gestor".
- [I] Nenhum escalonamento automático normatizado; padrão: reiteração → chefia imediata → autoridade máxima → relatório gerencial. Modelar como eventos append-only.

### 2.7 Reabertura, contestação, recurso [C]
- SES-RJ: contestação da resposta → "anotar o relato do usuário como **NOVO DETALHE** e **REENCAMINHAR**".
- OGE-SP: reabertura **uma única vez**, apenas para (a) nova resposta ao cidadão (fato novo/retificação) ou (b) encaminhar a outro órgão após encerramento; não reabrir "para acrescentar informação".
- Fala.BR: "reabertura do tratamento" pelo setor **não reabre para o manifestante** [S].
- SMS-SP: Ouvidoria Geral do Município + Corregedoria como **instância recursal**.
- Portaria 116 art. 30: marcação de resolutividade pode ser alterada depois.

### 2.8 Manifestações coletivas e "de ofício"
- SMS-SP: trata "manifestações **pessoais e coletivas**". Portaria 116 art. 4º IV: soluções coletivas a partir do recorrente. Ouvidoria ativa gera registros "durante a ação ou em até 24 horas" (Port. 581 art. 82): https://www.gov.br/ouvidorias/pt-br/ouvidorias/sisouv/acervo/portarias-cgu/boas-practicas-em-ouvidoria-ativa
- [I] Modelar `origem` = {cidadão, ouvidoria ativa/pesquisa, de ofício (agregação de recorrentes), coletiva (N manifestantes vinculados)}.

### 2.9 Fluxo de denúncia (separado da reclamação) [C]
- Portaria 116, arts. 33–36: análise prévia exige **autoria, materialidade, compreensão** (ou indícios); se faltar, pedir complementação (**salvo anônimas**); **ouvidoria não faz diligência, depoimento, acareação ou investigação** (art. 34); resposta conclusiva informa **encaminhamento às unidades apuratórias** ou justifica arquivamento (art. 35).
- Decreto 10.153/2019 + Portaria 116 arts. 39–44: **pseudonimização** (suprimir campos cadastrais e trechos em 1ª pessoa; voz/imagem), acesso restrito por **100 anos**, **consentimento do denunciante** (20 dias; silêncio = negativa) para compartilhar identidade com outro órgão; retaliação apurada pela CGU. Decreto: http://www.planalto.gov.br/ccivil_03/_ato2019-2022/2019/decreto/d10153.htm
- CGU (curso Tratamento de Denúncias): ouvidoria cadastra, analisa e distribui à área de apuração; corregedoria faz **juízo de admissibilidade** depois [S]: https://repositorio.cgu.gov.br/bitstream/1/56317/1/Material_do_aluno_Curso_denuncias_Out18.pdf · Portaria CGU 1.089/2018 (fluxo): https://www.gov.br/cgu/pt-br/assuntos/integridade-publica/programa-de-integridade/arquivos/ogu-portaria-cgu-no-1-089-2018-fluxo-para-tratamento-de-denuncias.pdf
- SES-RJ: anônima só aceita "quando apresentar indícios confiáveis e consistentes"; ouvidoria **recomenda averiguação**; formulário marca SIGILO e **não encaminha contatos** à área.
- OGE-SP: denúncia à Comissão de Ética pode ser **encerrada** com "recebida e destinada a X"; quando a comissão decide, reabre-se para comunicar.
- Guia OuvSUS/MS: anônima é "**comunicação de irregularidade**" sem acompanhamento pelo cidadão [S].
→ **Dois trilhos** com ACL distintas: `reclamacao_assistencial` (vai ao gestor da unidade) e `denuncia` (vai a unidade apuratória; identidade pseudonimizada; log de acesso; consentimento para compartilhar).

---

## 3. Relação com as unidades de saúde

- **Rede descentralizada** é o padrão: SMS-SP tem **60 unidades descentralizadas** (CRS/STS, hospitais, HSPM, SAMU) com ouvidor e técnico locais; cada unidade produz **relatório trimestral** público com "considerações do ouvidor" e "**considerações do gestor**" e plano de ação 5W2H [C]. SES-RJ: "Ouvidorias Descentralizadas" nas unidades próprias. Rio/SMS: ouvidorias setoriais nas CAPs e hospitais; entrada pelo 1746 [S]: https://saude.prefeitura.rio/ouvidoria/
- Unidades sem ouvidor → **interlocutor designado**, publicado, capacitado (GDF); ponto de resposta + suplente por área (SMS-SP).
- PNASS R30 exige escuta **local** com retorno e fórum de discussão. SES/SP exige ouvidoria **sediada na unidade** gerida por OS.
- **Reclamação contra profissional específico**:
  - GDF art. 6º: resposta exige **posicionamento formal do chefe imediato**. Portaria 116 art. 34: ouvidoria não investiga nem ouve o envolvido. → [I] o "direito de defesa" acontece na **unidade apuratória** (sindicância/PAD, comissão de ética), não na ouvidoria.
  - Conselhos profissionais: CRM-PR — denúncia por escrito, **não anônima**, identificação completa; abre-se **sindicância**; hospitais com >30 médicos precisam de **Comissão de Ética Médica** [C]: https://www.crmpr.org.br/Sobre-denuncia-1-49025.shtml · CFM CPEP: https://portal.cfm.org.br/etica-medica/codigo-de-processo-etico-profissional-atual/capitulo-ii-da-sindicancia/
  - Portaria MS 1.820/2009 (Carta dos Direitos): usuário tem **dever** de comunicar irregularidades; direito a **nome social** em todo registro: https://bvsms.saude.gov.br/bvs/saudelegis/gm/2009/prt1820_13_08_2009.html
  - SES-SP contrato: colaboradores também são usuários; OS "deve afastar atos de retaliação".
- **Fechamento do ciclo**: PNASS R30 pede "espaço institucional de discussão"; SMS-SP Tide Setubal discute no **huddle diário** e no Conselho Gestor; ES exige **causa raiz** com evidências para contar como resolvida.

---

## 4. Canais e acessibilidade

- **WhatsApp**: Aracaju (jan–jun/2026: 83 manifestações formalizadas e 427 atendimentos) [S]; Fortaleza, São Luís, Contagem, Manaus (WhatsApp Ouvidoria e WhatsApp SIC separados); Hospital Centenário: 30% por "Zap Saúde". **Contraponto**: SES/SP **proíbe** WhatsApp como canal de recebimento em contrato de OS — o registro tem que ir ao sistema oficial. → [I] WhatsApp é **canal de captação** que gera protocolo no sistema; nunca repositório.
- **Presencial/urna/QR**: urnas com verificação diária (SES-RJ); ES conta reclamações de **urna** no indicador e exige divulgar a localização; Mossoró: QR fixado nas UPAs + **urnas rotativas 15 dias por local** para quem não usa celular [C]: https://prefeiturademossoro.com.br/noticias/ouvidoria-disponibiliza-qr-code-para-populacao-mossoroense-avaliar-servicos-da-prefeitura/9600 ; Hospital Centenário: QR **ao lado da identificação do leito**.
- **136 / OuvidorSUS**: Disque Saúde 136 gratuito, seg–sex 8–20h e sáb 8–18h [S]: https://www.gov.br/saude/pt-br/canais-de-atendimento/ouvsus · consulta pública de protocolo: https://ouvidor.saude.gov.br/public/form-web/consultar
- **Libras**: GDF via videochamada/QR; Centrais de Intermediação em Libras (SP, DF); Maceió (videochamada nas unidades) [S]; VLibras.
- **Carta de Serviços**: Lei 13.460 art. 7º; exemplo hospitalar: https://cht.saude.pr.gov.br/Pagina/Carta-de-Servicos-Ouvidoria
- Portaria 116 arts. 12–17: guarda de registros de atendimento telefônico/presencial 5 anos corrente + 5 (ou +15 para denúncias) [C].

---

## 5. Ouvidoria ativa

- Conceito incorporado ao SUS a partir do Decreto 7.508/2011; ações: eventos, locais de convívio, **locais de prestação do serviço**, correspondência, **enquetes online**; priorizar "populações vulneráveis ou digitalmente excluídas"; registrar em ≤ 24h [C]: https://www.gov.br/ouvidorias/pt-br/ouvidorias/sisouv/acervo/portarias-cgu/boas-praticas-em-ouvidoria-ativa · MS "Ouvidoria Ativa do SUS" (não abriu): https://bvsms.saude.gov.br/bvs/publicacoes/ouvidoria_ativa_sus_ampliando_escuta.pdf
- SES-RJ POP "Acolhimento por **Busca Ativa**" nas unidades.
- **Mogi das Cruzes – Ouvidoria Participativa** (2026): questionários por amostragem em triagens e recepções de UBS/USF/PA + vistoria visual; rodízio por **calendário sigiloso**; resultado vira "bússola orçamentária" [C]: https://www.ar10.com.br/noticia/4648/sao-paulo/mogi-das-cruzes/municipio-institui-ouvidoria-participativa-com-auditoria-e-pesquisas-em-unidades-de-saude.html
- **Pesquisa proativa por amostragem**: modelo ES (mensal por setor, checagem telefônica, identificação do entrevistado); PNASS (telefone, amostra por estabelecimento).
- **Relatórios**: Lei 13.460 anual; SMS-SP **trimestral por unidade** + semestral/anual da rede + boletim mensal "Ouvidoria em Dados", **compartilhados com Conselhos Gestores** e publicados (Portaria 152 arts. 27–28: nº por classificação, motivos, recorrentes, parecer do gestor); formato de relatório para o **Conselho Municipal de Saúde** desenhado com o Instituto Pólis.

---

## 6. IA/LLM em ouvidoria pública no Brasil

- **Fala.BR com IA (CGU, desde 6/abr/2026)** [C]: classifica automaticamente o **tipo** a partir do texto; próximos passos: sugerir **assunto, órgão destinatário e serviço**. Notícias não mencionam revisão humana, salvaguardas ou explicabilidade — **lacuna**. https://agenciabrasil.ebc.com.br/geral/noticia/2026-04/ouvidoria-falabr-passa-usar-ia-para-agilizar-atendimento · https://www.gov.br/cgu/pt-br/assuntos/noticias/2026/04/fala-br-ganha-novo-formato-e-passa-a-usar-inteligencia-artificial-para-simplificar-atendimento-ao-cidadao — [I] como a Portaria 116 prevê reclassificação pelo ouvidor, a IA é na prática **sugestão com override humano**.
- **Estudo CGU/OGU com PLN em >4.000 manifestações**: recomendou classificação **padronizada e automatizada de assuntos**, dados demográficos no registro, **modelos de resposta**, FAQ para recorrentes; "o grande volume nem sempre se traduz em aprendizado institucional" [C]: https://convergenciadigital.com.br/governo/cgu-recomenda-as-ouvidorias-que-estruturem-dados-para-inteligencia-artificial/
- **Chatbots municipais com IA no WhatsApp** que incluem ouvidoria: Salgueiro/PE "SIA" (24×7, texto e **áudio**, integrado ao 1Doc) [C]; Florianópolis "Tia" (Softplan) [S]. Nenhum descreve tratamento de denúncia/sigilo pelo bot — [I] risco: denúncia sensível em canal sem pseudonimização.
- **Limites aplicáveis** [C]: Guia de Design de Transparência em IA (gov.br/SGD) — quando IA participa de decisão que afeta o cidadão ("classificação de urgência em um serviço de saúde"), informar **que há IA, quais dados, se houve revisão humana, responsável e canal de contestação**; **direito à revisão humana (LGPD art. 20)**: https://www.gov.br/governodigital/pt-br/infraestrutura-nacional-de-dados/inteligencia-artificial-1/publicacoes/guia_design_transparencia_ia.pdf/@@download/file · CGU "Ouvidorias Públicas e a LGPD" (`fontes/renouv_guia_lgpd_ouvidorias_2022.txt`).
- **Resposta sugerida / resumo por LLM em ouvidoria**: nenhum caso público brasileiro documentado além da classificação do Fala.BR [C — busca negativa]. [I] Se implementar: (1) sugestão de tipo/assunto/unidade com confiança e override obrigatório; (2) minuta de resposta **sempre** editada/assinada por técnico (art. 28–29 da Portaria 116 = bom prompt de validação); (3) **nunca** IA em denúncia antes da pseudonimização; (4) guardar versão sugerida × enviada para medir taxa de correção humana.

---

## 7. Implicações para o modelo de dados [I]
1. `manifestacao`: tipo (6 valores DOGES + subtipos de denúncia), assunto/subassunto (catálogo), tags, canal, origem (cidadão/ativa/ofício/coletiva), unidade-alvo, profissional citado (sem expor no trilho assistencial), prioridade + data-limite, anonimato/sigilo, protocolo público.
2. Máquina de estados com trilha append-only: recebida → em análise → complementação solicitada (suspende prazo 1×) → encaminhada (área, prazo interno) → reiterada/escalonada → respondida pela área → em validação → respondida ao cidadão (`resolvida` + conteúdo mínimo por tipo) → pesquisa enviada → contestada/reaberta (1×) → concluída/arquivada (motivo art. 31) / encaminhada a outro órgão.
3. Trilho `denuncia` isolado: pseudonimização, log de acesso à identidade, consentimento, unidade apuratória.
4. Pontos de resposta por unidade/área (titular + suplente), com SLA próprio e métricas por área.
5. Pesquisa pós-resposta (5 pontos + "foi resolvida?") e pesquisa proativa por amostragem — tabelas distintas.
6. Indicadores por período × unidade × tipo × assunto, com denominador de atendimentos para "por 1.000".
7. Relatório trimestral por unidade com "considerações do gestor" e plano de ação 5W2H; exportável ao Conselho Municipal de Saúde.
8. Campos de IA: `classificacao_sugerida`, `confianca`, `classificacao_final`, `resposta_sugerida`, `resposta_enviada`, `revisado_por`.

## Lacunas
- Manual das Ouvidorias do SUS 2014 e "Ouvidoria Ativa do SUS" (bvsms) inacessíveis nesta sessão.
- Manual de contratos OS do Rio é PDF-imagem.
- Nenhuma fonte deu fórmula oficial de "reincidência".
- Salvaguardas humanas da IA do Fala.BR não estão descritas publicamente.
