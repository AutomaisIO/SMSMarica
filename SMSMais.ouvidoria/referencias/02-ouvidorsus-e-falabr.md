# OuvidorSUS × Fala.BR — os dois sistemas de referência, vistos por quem vai construir um próprio

> Pesquisa web de 20/09/2026 (agente de pesquisa, sessão Claude Fable 5.1). Fontes lidas: Manual das Ouvidorias do SUS 2014, Guia de Implantação 2ª ed. 2014, Portaria Normativa CGU 116/2024, wiki OuvidorSUS 3 (DATASUS), wiki Fala.BR (CGU), dicionário de dados abertos do Fala.BR, fluxo "Tratamento de Manifestações da OuvSUS" 2025. Extratos em [`fontes/`](./fontes/) (`ms_manual_ouvidorias_sus_2014.txt`, `ms_guia_implantacao_2ed_2014.txt`, `wiki_ouvidorsus3_datasus.txt`, `ouvsus_tratamento_manifestacoes_2025.txt`, `falabr_dicionario_dados_abertos.pdf`, `cgu_portaria_normativa_116_2024_dou.txt`). O que **não** foi confirmado está marcado com ⚠.

---

## A) OuvidorSUS (Ministério da Saúde / Ouvidoria-Geral do SUS – OuvSUS, ex-DOGES)

### A.1 O que é
- Sistema web do DATASUS com a Ouvidoria-Geral do SUS, em uso desde 2006/2007, regulamentado pela **Portaria SGEP/MS nº 8, de 25/05/2007** (⚠ página do bvsms retornou vazia; conteúdo via citações do Manual 2014: classificar/tipificar conforme os manuais; encaminhamento aos setores em **até 3 dias úteis**; o gestor responde pelos atos dos usuários que cadastrar).
- Ferramenta da **Rede Nacional de Ouvidorias do SUS (RNO/SUS)**, ex-**Sistema Nacional de Ouvidorias do SUS (SNO)**: rede descentralizada União–estados–municípios, autonomia dos entes (Manual 2014, §1.2; https://bvsms.saude.gov.br/bvs/publicacoes/manual_ouvidoria_sus.pdf).
- Versões: **OuvidorSUS 2** até 05/11/2023; **OuvidorSUS 3** desde então. Volume: **5,3 milhões de manifestações** acumuladas (abr/2024). Rede com **1.011 ouvidorias** no estudo 2014–2018 (SciELO: http://www.scielo.br/j/csc/a/v8dbxssvPnGCWzgW6q9S8yB/?lang=pt).
- Produção: `https://ouvidor.saude.gov.br/` (sem homologação pública). Acesso via **SCPA**.
- Novidades do Ouv3 (wiki DATASUS https://wiki.datasus.gov.br/ouvidor/index.php/P%C3%A1gina_principal): tipificação nova + **TAGs/marcadores por ouvidoria**; disseminação de artigos; controle de prazo com prorrogação (Lei 13.460); responsivo; eMAG; **integração com Fala.BR, CNES, Receita Federal e CADSUS**; **montagem de redes** estaduais/municipais com permissões pactuadas.

### A.2 Quem usa / adesão municipal
- **Gratuito e voluntário** para qualquer ouvidoria vinculada ao SUS. Passos: Formulário de Cadastro → **Termo de Adesão assinado pela autoridade máxima** → protocolo digital; dúvidas `sno@saude.gov.br` (https://www.gov.br/saude/pt-br/canais-de-atendimento/ouvsus/sistema-nacional-de-ouvidorias-do-sus/como-implantar-uma-ouvidoria-do-sus).
- Não há obrigação legal de usar o OuvidorSUS. Capacitação: EAD Fiocruz (ENSP).
- Argumento do MS contra sistema próprio: "diminuir os custos operacionais". **Contraponto real: SMS-SP** usou OuvidorSUS como registro único desde 2015 (Portaria 757/SMS.G/2015) e **migrou em set/2025 para sistema municipal próprio (SIGRC – Ouvidoria SUS, integrado ao SP156)** por Portaria SMS 870/2025 — rede de 58 unidades, ~1.900 pontos de resposta, 111 mil manifestações em 11 meses de 2024 (https://prefeitura.sp.gov.br/w/ouvidoria-sus-paulistana-integra-nova-plataforma-para-agilizar-atendimento ; https://www.cosemssp.org.br/noticias/acervo-digital/adesao-da-rede-de-ouvidorias-sus-ao-sigrc-modernizacao-unificacao-e-facilitacao-de-processos/ ; https://legislacao.prefeitura.sp.gov.br/portaria-secretaria-municipal-da-saude-sms-870-de-26-de-dezembro-de-2025 ; https://legislacao.prefeitura.sp.gov.br/portaria-secretaria-municipal-da-saude-sms-152-de-3-de-abril-de-2026). Rio usa o **1746** como 1ª instância; Porto Alegre usa **156 opção 6 + 136**. **Sistema municipal próprio é caminho já trilhado por capitais.**

### A.3 Canais (Ouvidoria-Geral)
- **136**: URA 24h; atendente seg–sex 8h–20h, sáb 8h–18h; 160 profissionais.
- **Formulário web**: `https://ouvidor.saude.gov.br/public/form-web/registrar`; consulta por protocolo + chave: `/public/form-web/consultar`. Ouvidorias aderentes podem colocar o link no site da secretaria (cai direto na ouvidoria).
- **WhatsApp 0800 275 0620** + chatbot (ago/2024), carta/presencial, e-mail; **Fala.BR** também entra como canal para o MS.
- Distribuição 2014–2018 (216.832 manifestações da OuvSUS): 136 = 53,26%; web = 34,24%; e-mail = 4,50%; carta = 3,18%; app DigiSUS = 2,81%; presencial = 0,23%.

### A.4 Classificação (tipo) — Quadro 5 do Manual 2014
| Tipo | Definição |
|---|---|
| **Denúncia** | comunicação que indica irregularidade ou indício de irregularidade na administração e/ou por entidade pública ou privada |
| **Reclamação** | insatisfação em relação às ações e serviços de saúde, **sem conteúdo de requerimento** |
| **Solicitação** | pode indicar insatisfação, mas **necessariamente contém requerimento** de atendimento/acesso |
| **Sugestão** | propõe ação útil à melhoria do sistema |
| **Elogio** | satisfação/agradecimento |
| **Informação** | questionamento sobre o sistema de saúde ou a assistência |

- Ouv3 acrescenta **"Comunicação de irregularidade"** = denúncia **obrigatoriamente anônima** (o sistema só habilita "Anônimo"; o manifestante não acompanha) (https://wiki.datasus.gov.br/ouvidor/index.php/COMUNICA%C3%87%C3%83O_DE_IRREGULARIDADE_versus_DEN%C3%9ANCIA). Distingue **Disseminação** (informação respondida no ato pela base de conhecimento BITS) de **Informação** (manifestação registrada).
- Regras de identificação (Manual 2014): **solicitação e informação NUNCA anônimas; solicitação NUNCA sob sigilo**; em demanda sigilosa o texto não pode permitir identificar o cidadão.
- Distribuição 2014–18: solicitações 38,52%, reclamações 27,39%, denúncias 23,46%, informações 6,31%, sugestões 2,18%, elogios 2,14%.
- **Natureza da providência** derivada do tipo (Quadro 7): Solicitação/Informação → **Atender**; Reclamação/Denúncia → **Apurar**; Elogio/Sugestão → **Conhecer**.

### A.5 Tipificação (assunto → subassuntos)
- Ouv2: **22 assuntos principais** (Manual 2014): Alimento; Assistência à Saúde; Assistência Farmacêutica; Assistência Odontológica; Assuntos Não Pertinentes; Cartão SUS; Comunicação; Conselho de Saúde; ESF/PACS; Financeiro; Gestão; Orientações em Saúde; Ouvidoria do SUS; Produtos para Saúde/Correlatos; Programa Farmácia Popular; Farmácia Popular – Copagamento; PNCT (tabagismo); DST/AIDS; SAMU; Transporte; Vigilância em Saúde; Vigilância Sanitária. Fiocruz cita **23 assuntos, 246 subassuntos 1, 897 subassuntos 2 e 1.897 subassuntos 3**.
- Exemplos de **subassunto 1**: Dificuldade de acesso; Insatisfação; Internação; Dificuldade em adquirir medicamento; Prótese; Fralda descartável; Cobrança indevida; Demora em ser atendido; Preço abusivo; Falha no sistema operacional; Animais sinantrópicos; Água e ambientes; Prevenção; Doenças. A Tabela 1 do Manual mostra **inconsistência classificação × tipificação** (ex.: "dificuldade de acesso" registrada como solicitação quando deveria ser reclamação) — o sistema deve detectar e corrigir.
- Top assuntos 2014–18: Gestão 27,56%; Assistência à Saúde 19,96%; Assistência Farmacêutica 14,11%; Vigilância Sanitária 11,13%; Farmácia Popular 5,58%; Vigilância em Saúde 4,02%.
- **Ouv3** (wiki "Como tipificar" https://wiki.datasus.gov.br/ouvidor/index.php/COMO_TIPIFICAR): tema "Sistema Único de Saúde" → **Tipificação** com busca textual → **listas dependentes** obrigatórias por tipificação (ex.: Doença, Medicamento, "Status/Problema Atenção à Saúde – Assistência Farmacêutica"), algumas multivaloradas → **Marcadores** (tags locais: "palavra-chave que complementa a tipificação, desde que não conste na tipificação ou nas listas") (https://wiki.datasus.gov.br/ouvidor/index.php/MARCADORES).
- ⚠ **"Manual de Tipificação – OuvidorSUS 3 (2023–2025)"** e **"Dicionário de dados – Ouv3"** existem como recursos do dataset https://dadosabertos.saude.gov.br/dataset/ouvidorsus (24 recursos, CC BY-ND 3.0, contato `cgios@saude.gov.br`), mas o download não funcionou por fetch. **Tentar pelo navegador** (ou `apidadosabertos.saude.gov.br`) — é a árvore completa a importar.

### A.6 Campos do registro
- Ouv3 ("Como registrar"): Atendimento → **Canal de atendimento**, **Origem de atendimento** (onde o cidadão se manifestou inicialmente, ex.: "Gabinete do Secretário"), **Manifestante** → "Registrar manifestação": obrigatórios só **Classificação, Identificação do manifestante e Teor**. Pessoas: **Manifestante** (quem contata) × **Referido** (em favor de quem, ex.: mãe pelo filho) × **Envolvidos** (ex.: profissional denunciado) (https://wiki.datasus.gov.br/ouvidor/index.php/MANIFESTANTE_versus_REFERIDO_versus_ENVOLVIDOS). Há tratamento para manifestante **sem CPF/CNPJ**.
- Dados mínimos por natureza (Manual 2014): **Solicitação** — dados do cidadão e do paciente, Cartão SUS, procedimento/medicamento, unidade que indicou, locais já procurados; **Reclamação/Denúncia** — dados do cidadão (se não anônimo), nome dos envolvidos, descrição, **dia e hora**, **local da ocorrência**; **Sugestão/Elogio** — a quem, unidade/setor, cargo, contato; **Informação** — pergunta e resposta dada.
- Pesquisa de perfil do cidadão (opcional): gênero, idade, escolaridade, estado civil, UF/município, cor/raça, orientação sexual, filhos, internet, ocupação, classe, **usa exclusivamente o SUS? tem plano?**

### A.7 Fluxo e ações
- Macroprocessos (Manual 2014): I Relacionamento com o cidadão (presencial, telefone, e-mail/carta/web, **resposta/retorno**, **avaliação de satisfação**); II Tratamento (2.1 análise e triagem → 2.2 encaminhamento → 2.3 acompanhamento e cobrança → 2.4 análise da resposta e fechamento); III Gestão da informação (estatísticas, **relatórios gerenciais**); IV Articulação com a rede; V Apoio ao gestor; VI Gestão interna.
- Triagem 2.1: consistência do registro (coerência, **desmembramento** em registros distintos por assunto/órgão, sem identificar o cidadão em sigilo/anônimo) → tipificação → **prioridade** → natureza da providência.
- Ações do Ouv3: **Tipificar**; **Encaminhar** para *outra Ouvidoria* / *Ponto de Resposta* / *Órgão Externo* (comentário e anexos); **Atribuir** a técnico; **Responder** (inclusive *Pedido de complementação*); **Prorrogar** (justificativa + anexos; recalcula prazos); *Anotação* (interna) × *Observação*; **Espelho da manifestação** (PDF); **Exportar lista**. **Ponto de resposta** = unidade prestadora que responde à ouvidoria; **Órgão externo** = instituição que não acessa o sistema.
- O cidadão, no acompanhamento, vê dados + histórico **exceto**: TIPIFICADA, ENCAMINHADA PARA PONTO DE RESPOSTA, CONCLUÍDA, ATRIBUÍDA, PRORROGADO PRAZO, ANOTAÇÃO DA OUVIDORIA, ARQUIVADA. Pode "adicionar informação" e, após resposta, **RECORRER** (https://wiki.datasus.gov.br/ouvidor/index.php/QUAIS_A%C3%87%C3%95ES_S%C3%83O_VISUALIZADAS_PELO_MANIFESTANTE_NO_ACOMPANHAMENTO_DE_DEMANDA).
- Fluxo OuvSUS 2025 (https://www.gov.br/saude/pt-br/canais-de-atendimento/ouvsus/publicacoes/tratamento-de-manifestacoes-da-ouvidoria-geral-do-sus.pdf): 1) recebimento e classificação (**complementação 20 dias → arquiva se não responder**); 2) análise e encaminhamento (busca de **manifestações parecidas do mesmo cidadão**; destino = área técnica / **ouvidoria estadual ou municipal** / outro órgão); 3) controle de prazo com **prorrogação única**, cobrança e **escalonamento ao superior imediato**; 4) avaliação da resposta, redação em **linguagem simples**, **recurso** pelo cidadão; **arquivamento automático 30 dias após a resposta sem recurso**. Em denúncia, a OuvSUS **pede autorização ao denunciante** antes de encaminhar às ouvidorias estaduais/municipais.
- **Situação final** (Manual 2014 §2.4): solicitação **atendida / não atendida / cidadão não localizado / faleceu**; se não atendida, motivo: falta de recursos; não coberto pelo SUS; cidadão fez particular; vagas insuficientes; não compareceu; outros. Denúncia/reclamação: **apurada?** → procede / não procede / inconclusiva. Resposta pelo **mesmo canal** de entrada quando veio por documento oficial.

### A.8 Prazos
- Histórico (Portaria 8/2007, Quadro 6): prioridade **Urgente 15 d / Alta 30 d / Média 60 d / Baixa 90 d**; encaminhamento em 3 dias úteis.
- Hoje: Ouv3 segue a **Lei 13.460/2017 art. 16: 30 dias, prorrogável uma vez por 30**; complementação 20 dias.

### A.9 Rede / hierarquia
- Papéis: **Ouvidoria de cadastro (origem)** → **intermediária** → **de destino**. Manifestante avisado do encaminhamento se tiver e-mail.
- Encaminha para **qualquer ouvidoria aderente do país**; **ver o teor de outra ouvidoria só com pactuação de "Rede"** (CIB/CIR) (https://wiki.datasus.gov.br/ouvidor/index.php/REDE).
- "O município tem a responsabilidade direta de viabilizar o acesso dos seus munícipes" — a maior parte do que o 136 recebe acaba encaminhada à ouvidoria municipal.

### A.10 Ouvidoria Ativa e satisfação
- Manual 2014 §5: **pesquisas de satisfação** (face a face, telefone, correio, internet), **Ouvidoria Itinerante**, **Carta SUS** (carta pós-internação com valor pago e cartão-resposta; >23 milhões enviadas), SMS Saúde.
- A pesquisa do OuvidorSUS (2014) só perguntava **como o cidadão avalia o serviço da ouvidoria**; o manual recomenda separar (a) **"considera-se atendido?"** de (b) desempenho da ouvidoria (rapidez, atenção, clareza, canal de retorno, resposta suficiente).

### A.11 Relatórios / dados
- Exportação (CSV), espelho, relatórios gerenciais; dados abertos anuais (Ouv2 e Ouv3, 2023–2025).
- ⚠ **API pública do Ouv3**: o dataset lista "API OUVIDOR OUV3", sem documentação encontrada; não confirmada API transacional. Integração municipal → OuvidorSUS parece **manual** (registro duplo ou link do formulário).

---

## B) Fala.BR (CGU / Ouvidoria-Geral da União)

### B.1 O que é
- Plataforma integrada **ouvidoria + LAI** (fusão e-Ouv + e-SIC); `https://falabr.cgu.gov.br`; >300 ouvidorias federais e **>3.000 órgãos não federais**. Login **Gov.BR desde 04/11/2024**.
- **7 tipos**: Acesso à Informação (LAI), Denúncia, Elogio, Reclamação, **Simplifique**, Solicitação, Sugestão. Não existe "Informação" como tipo de ouvidoria. Área **"Ouvidoria Interna"** para servidores (https://wiki.cgu.gov.br/index.php/Fala.BR_-_M%C3%B3dulo_Ouvidoria).
- **Portaria Normativa CGU nº 116/2024**: Fala.BR **obrigatório no Executivo federal "sem prejuízo de sua integração com sistemas informatizados de ouvidoria"** (art. 8); **toda manifestação recebida por qualquer canal deve ser registrada na base** (arts. 9–10) (https://www.gov.br/ouvidorias/pt-br/central-de-conteudos/legislacao/arquivos/portarias/portaria-normativa-cgu-no-116-consolidada.pdf/view). É a melhor **especificação funcional** pública de ouvidoria.

### B.2 Identificação, anonimato, sigilo, pseudonimização
- **Lei 13.460 art. 10 — a manifestação "conterá a identificação do requerente"**; §7 obriga a proteger a identidade. Decreto 9.492/2018 art. 23: registros sem identificação **não configuram manifestação e não obrigam resposta**, mas **denúncia anônima é admitida**.
- Evolução: o e-Ouv oferecia "Identificado sem restrição / com restrição / Não identificado"; **extintas** — hoje toda manifestação identificada tem dados **sempre restritos à ouvidoria** (não vai ao ponto de resposta), e **anonimato só para Denúncia** (não gera protocolo; não acompanha). Reclamações anônimas **desabilitadas** em 2022. O nome do analista **não aparece** para o cidadão.
- **Denúncia** (Decreto 10.153/2019): formulário próprio; formulários especializados para **assédio moral, assédio sexual e discriminação**; **pseudonimização** ao encaminhar; para ver os dados do denunciante o servidor **insere justificativa registrada no histórico**; compartilhar com outro órgão exige **"Pedido de consentimento" → "Autorizar Acesso a Dados"**; sem autorização vai **pseudonimizada**. Via API, dados do denunciante ficam pseudonimizados.
- Consentimento presumido: procurar a ouvidoria "implica automaticamente o consentimento" para registro (PN 116 art. 12). Temporalidade (art. 19): reclamação/solicitação/elogio/sugestão **5 anos corrente + 5 intermediário**; denúncia **5 + 15**.

### B.3 Formulário
- Passos: tipo → login Gov.BR (ou anônimo em denúncia) → **esfera** → **órgão destinatário** → **assunto** → **resumo** → **"Fale aqui"** (teor) → **local do fato** → **envolvidos** → **anexos (até 10, 30 MB total)** → revisão → **NUP/protocolo + código de acesso** por e-mail. Na tela da ouvidoria: **Serviço** (Portal gov.br), **Órgão de interesse**, **Subassunto**, **Tag**, **Modo de resposta**, **Canal de entrada**, **Responsável**. Subassunto e tags **não vão no cadastro via API — só depois**.
- Notificações por e-mail em: registro, prorrogação, encaminhamento, resposta, mudança de tipo.

### B.4 Prazos e fluxo (PN CGU 116/2024)
- **30 + 30 com justificativa expressa**; **sem prorrogação ao encaminhar a outro órgão** (art. 22 §1). **Complementação: 20 dias**; só o **primeiro pedido suspende**; sem resposta → **arquivamento sem resposta conclusiva** (art. 25). **Encaminhamento a outro órgão imediatamente após a triagem, no máximo 30 dias** (art. 26). **Área técnica: 20 dias, prorrogável 1× por 20** (art. 27). LAI: 20 + 10; recurso 10 dias.
- Etapas obrigatórias (art. 22): recebimento → registro → **triagem** (prioridade, individualizar/agrupar, distribuir) → complementação → encaminhamento externo → trâmite interno → **resposta conclusiva** (conteúdo mínimo por tipo, art. 29; linguagem simples, art. 28). Tipos de resposta: **complementação / intermediária / conclusiva**. Reabertura via API. Solução pacífica de conflitos (não em denúncia). **Fluxos internos devem ser publicados no site** (art. 21). Relatório de gestão anual.
- Perfis: Administrador, Gestor, Respondente, Triador, Cidadão, WebService. Ferramenta de **tarja de PDF**. **FalaBR.IA (Laion)**: prevê satisfação e resolutividade comparando a resposta em elaboração com respostas anteriores; sugere manifestações similares.

### B.5 Pesquisa de satisfação (texto exato do Fala.BR)
Após resposta conclusiva, facultativa, **uma vez por protocolo**:
1. **"A sua demanda foi atendida?"** — Sim / Não / Parcialmente atendida
2. **"A resposta fornecida foi fácil de compreender?"** — Muito fácil / Fácil / Regular / Difícil / Muito difícil
3. **"Você está satisfeito(a) com o atendimento prestado?"** — Muito satisfeito / Satisfeito / Regular / Insatisfeito / Muito insatisfeito
4. Comentário livre.
Além disso, **NPS no ato do registro** (0–10, "recomendaria o Fala.BR?").

### B.6 Painel "Resolveu?" e dados abertos
- Painel: `https://centralpaineis.cgu.gov.br/visualizar/resolveu` — volume, situação, **tempo médio**, **% dentro/fora do prazo**, **satisfação**; filtros por órgão, **esfera**, UF, período; desde jun/2026 aceita **parâmetro de órgão na URL**.
- Dados abertos mensais pseudonimizados: https://dadosabertos-download.cgu.gov.br/e-Ouv/manifestacoes-ouvidoria.zip + dicionário (17/03/2025). Colunas: Data Registro; Data Prazo Resposta; Data Resposta; Faixa Etária; Raça/Cor; Gênero; Município/UF do manifestante; Município/UF **do fato**; Tipo; Código SIORG; Órgão; **Assunto**; **Dias para Resolução**; **Dias de Atraso**; Formulário; Situação; Esfera; Serviço; **Demanda Atendida**; **Satisfação**. **Esquema mínimo de exportação para copiar.**

### B.7 API (Me-OUV)
- REST + **OAuth 2.0** (`POST https://falabr.cgu.gov.br/oauth/token`, token 1 dia); docs `https://falabr.cgu.gov.br/help`; treinamento `treinafalabr.cgu.gov.br` (https://wiki.cgu.gov.br/index.php?title=Fala.BR_-_API_Faq ; https://wiki.cgu.gov.br/images/1/18/Como_utilizar_a_API_do_Fala.BR.pdf).
- Perfis: **WebService Respondente** (cadastrar, responder, encaminhar, atualizar assunto/subassunto/tags/serviço/responsável, arquivar, reabrir) e **WebService Observador** (só leitura). Endpoints: `GET/POST /api/manifestacoes`, `GET /api/manifestacoes/{id}`, `POST …/{id}/respostas`, `…/atualizar`, `…/arquivar`, `…/reabrir`, `GET /api/assuntos`, `GET /api/tipos-resposta`, domínios. Payload mínimo: `IdTipoFormulario`, `IdTipoManifestacao`, `IdOuvidoriaDestino`, `TextoManifestacao`, `Manifestante{Nome,Email}`.
- Limites: **500 registros por consulta**; **sem webhooks**; manifestações via API **não se vinculam a conta de cidadão**. Acesso: o **Ouvidor** do órgão solicita (não federal → CGART).

### B.8 Adesão de municípios
- **Gratuita e voluntária**; hoje pela **IN Conjunta OGU-SNAI/CGU nº 1/2025 (vigente desde 01/05/2025)**: adesão por **órgão/entidade**, termo assinado pelo **dirigente máximo**, exige **normativo próprio** de ouvidoria e de LAI (https://www.gov.br/ouvidorias/pt-br/ouvidorias/rede-de-ouvidorias/adesao-e-cadastros/adesao-a-plataforma-fala.br). **RENOUV**: 2.801 ouvidorias (abr/2025). ⚠ Não confirmado se uma **secretaria municipal isolada** pode aderir sem a prefeitura.
- **Maricá**: a Ouvidoria-Geral da Prefeitura opera **formulário próprio** (nome, e-mail, CPF, telefone, endereço, mensagem de 180 caracteres), WhatsApp (21) 99506-4638, e-mail, presencial na Rua Álvares de Castro 346, e o canal **"Alô Saúde" WhatsApp (21) 99140-0674** (https://www.marica.rj.gov.br/ouvidoria/). ⚠ Sem evidência de adesão de Maricá ao Fala.BR nem ao OuvidorSUS. Ver [`03-exemplos-secretarias-e-marica.md`](./03-exemplos-secretarias-e-marica.md).

---

## C) Comparação, o que copiar, armadilhas

### C.1 Comparação rápida
| Dimensão | OuvidorSUS 3 | Fala.BR |
|---|---|---|
| Dono / escopo | MS-OuvSUS; só saúde/SUS | CGU-OGU; toda administração + LAI |
| Tipos | Reclamação, Denúncia, Solicitação, Sugestão, Elogio, Informação + Comunicação de irregularidade (anônima) | Reclamação, Denúncia, Solicitação, Sugestão, Elogio, Simplifique, LAI |
| Taxonomia | Assunto → subassunto 1/2/3 (23/246/897/1.897) + listas dependentes + marcadores locais | Assunto (OGU) → subassunto + tags + serviço (gov.br) |
| Anonimato | Comunicação de irregularidade obrigatoriamente anônima; sigilo como opção | Só denúncia; dados sempre restritos; pseudonimização com consentimento e justificativa de acesso |
| Rede | origem/intermediária/destino; ponto de resposta; órgão externo; "Rede" pactuada em CIB/CIR | Encaminhamento entre órgãos; sem prorrogação após encaminhar |
| Prazos | 30+30; complementação 20 d; (histórico 15/30/60/90 por prioridade) | 30+30; complementação 20 d (só a 1ª suspende); área técnica 20+20 |
| Satisfação | Avaliação do serviço da ouvidoria (manual recomenda "foi atendido?") | 3 perguntas + comentário + NPS |
| Dados/API | Dados abertos anuais; export CSV; API transacional não confirmada | Dados abertos mensais; API REST OAuth2 |
| Painel público | "Ouvidorias em números" | Painel Resolveu? com filtro por município |
| Adesão municipal | Termo + ficha, SCPA, EAD Fiocruz | Termo (IN 1/2025), normativo próprio, Gov.BR |

### C.2 Funcionalidades que um sistema municipal próprio deve copiar
1. **Tipo em 6 classes** com as definições do Quadro 5 e as **regras de identificação por tipo** (solicitação/informação nunca anônimas; anônimo só denúncia) + "comunicação de irregularidade"; **natureza da providência** derivada (Atender/Apurar/Conhecer).
2. **Taxonomia em 3 níveis compatível com o OuvidorSUS** (importar o Manual de Tipificação Ouv3) + **marcadores locais** + **listas dependentes** ligadas ao domínio já existente (unidade por **CNES** = `Unidade`, procedimento do catálogo canônico ADR-0055, medicamento). Exportação para MS/CGU fica trivial.
3. **Três níveis de identificação**: identificado (dados visíveis só à ouvidoria, nunca ao ponto de resposta), **sigilo** (texto revisado), **anônimo** (sem acompanhamento). Denúncia: **pseudonimização por padrão ao encaminhar**, **consentimento explícito** para compartilhar, **acesso aos dados pessoais só com justificativa logada** (trilha append-only — padrão do ADR-0052).
4. **Pessoas**: manifestante × **referido** (paciente → `fhir.patient` quando houver CPF/CNS) × **envolvidos** (profissional → `fhir.practitioner`); **local do fato = unidade**; **canal** e **origem de atendimento** separados.
5. **Protocolo + código de acesso**, acompanhamento público com **histórico filtrado**, "adicionar informação", **recurso/reabertura**; notificações por e-mail/WhatsApp (módulo Mensageria, ADR-0059) — respeitando a regra LGPD de destinatário correto (ADR-0057).
6. **Máquina de estados**: Cadastrada → Triagem (consistência, **desmembramento**, prioridade, tipificação) → Atribuída → Encaminhada (ponto de resposta / outra ouvidoria / órgão externo) → Aguardando complementação (20 d, suspende 1×; auto-arquiva) → Respondida (intermediária/conclusiva com **conteúdo mínimo por tipo**) → Recurso → Concluída/Arquivada (auto-arquivo 30 d após resposta). **Situação final estruturada**; **detecção de manifestações similares do mesmo cidadão** na triagem.
7. **Prazos**: 30+30 automáticos, **prorrogação única com justificativa**, ponto de resposta 20+20, **sem prorrogação ao encaminhar para fora**, alertas, **cobrança e escalonamento ao superior**, % no prazo e dias de atraso.
8. **Pesquisa de satisfação** com as 3 perguntas do Fala.BR + comentário, uma por protocolo, via WhatsApp/PWA; separar **resolutividade** de **satisfação com a ouvidoria**.
9. **Relatórios**: por unidade/tipo/assunto/prazo/canal, ranking de unidades, relatório ao **Conselho Municipal de Saúde**, **relatório de gestão anual**, **espelho PDF**, export no esquema do dicionário CGU e painel público estilo "Resolveu?".
10. **Base de conhecimento** para resposta no ato ("disseminação") — casa com o módulo IA (ADR-0011).
11. **Ouvidoria Ativa**: pesquisa proativa pós-atendimento/alta (o gatilho de pesquisa de satisfação já existe no SMSMais e está desligado), "Carta SUS" digital, ouvidoria itinerante (PWA).
12. Governança: **login individual**, **temporalidade** (5+5 / denúncia 5+15), publicação do **fluxo interno** e da **Carta de Serviços**, portaria da SMS instituindo o sistema como **registro único** (modelo: Portarias SMS-SP 757/2015 e 870/2025).

### C.3 Armadilhas
- **Duplicidade de canais**: Maricá já tem Ouvidoria-Geral com formulário/WhatsApp + "Alô Saúde"; o cidadão também pode registrar no **136/OuvidorSUS** e no **Fala.BR**; o 136 **encaminha ao município** via OuvidorSUS (se a SMS não for aderente, chega por ofício/e-mail ou não chega). Decidir: (a) SMS adere ao OuvidorSUS como **ouvidoria de destino** e o sistema próprio registra em duplicidade controlada (SP fez assim por 10 anos e trocou), ou (b) sistema próprio como registro único (portaria) com **campo "protocolo externo"** e conciliação manual.
- **Base legal**: OuvidorSUS e Fala.BR são voluntários; Decreto 9.492 e PN 116 são federais. Mas a **Lei 13.460/2017 vale para todas as esferas** e a **LGPD** também; adesão ao Fala.BR exige **normativo municipal próprio** — o mesmo que o sistema próprio precisará. ⚠ Exigências da SES-RJ/CIB para dados estaduais não pesquisadas.
- **Identificação**: por Lei 13.460 art. 10 a manifestação é identificada; **não permitir solicitação/reclamação anônima**; anônimo só denúncia, **sem acompanhamento**. Denúncia identificada: **não expor dados ao ponto de resposta** e logar acessos.
- **Classificação × tipificação inconsistentes**: prever auditoria de registro (SMS-SP audita a qualidade da inserção e da resposta).
- **Interoperabilidade**: adotar **códigos de tipo do Fala.BR** e **taxonomia do OuvidorSUS** desde o início; exportar no esquema CGU; se o município aderir ao Fala.BR, o sistema vira cliente **WebService Respondente** (sem webhook → polling; token diário).
- **Ouvidoria não é fila de solicitação**: 38% das manifestações são **solicitações** (vaga, exame, medicamento) — ligar ao ecossistema de solicitação/regulação (ADR-0021/0052) em vez de duplicar, e registrar a **situação final** com motivo.
- **Escala**: SMS-SP = 333 manifestações/dia, 58 unidades, ~1.900 pontos de resposta; o modelo de **pontos de resposta por unidade/setor** com posse e prazo (como ADR-0047/0059) é o que sustenta a rede.
- ⚠ **Não confirmados**: texto integral da Portaria 8/2007; Manual de Tipificação Ouv3; API transacional do Ouv3; lista exaustiva de situações do Fala.BR; adesão de Maricá; nº de municípios no Fala.BR. Páginas gov.br de notícias e Fiocruz fora do ar por defeso eleitoral (jul–out/2026).

### Fontes principais
- Manual das Ouvidorias do SUS (MS, 2014): https://bvsms.saude.gov.br/bvs/publicacoes/manual_ouvidoria_sus.pdf
- Guia de Implantação de Ouvidorias do SUS, 2ª ed. 2014: https://www.gov.br/saude/pt-br/canais-de-atendimento/ouvsus/arquivos/guia-para-implantacao-de-ouvidorias-do-sus-2014.pdf
- Wiki OuvidorSUS 3: https://wiki.datasus.gov.br/ouvidor/index.php/P%C3%A1gina_principal
- Tratamento de Manifestações da OuvSUS (2025): https://www.gov.br/saude/pt-br/canais-de-atendimento/ouvsus/publicacoes/tratamento-de-manifestacoes-da-ouvidoria-geral-do-sus.pdf
- Dataset OuvidorSUS: https://dadosabertos.saude.gov.br/dataset/ouvidorsus
- Fala.BR wiki: https://wiki.cgu.gov.br/index.php/Fala.BR_-_M%C3%B3dulo_Ouvidoria ; API: https://wiki.cgu.gov.br/index.php?title=Fala.BR_-_API_Faq ; https://falabr.cgu.gov.br/help
- PN CGU 116/2024: https://www.gov.br/ouvidorias/pt-br/central-de-conteudos/legislacao/arquivos/portarias/portaria-normativa-cgu-no-116-consolidada.pdf/view
- Lei 13.460/2017: https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2017/lei/l13460.htm ; Decreto 9.492/2018: https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/decreto/d9492.htm ; Decreto 10.153/2019: https://www.planalto.gov.br/ccivil_03/_ato2019-2022/2019/decreto/d10153.htm
- Painel Resolveu?: https://centralpaineis.cgu.gov.br/visualizar/resolveu ; dados abertos: https://www.gov.br/cgu/pt-br/acesso-a-informacao/dados-abertos/arquivos/ouvidoria
- SMS-SP (precedente de sistema próprio): https://prefeitura.sp.gov.br/web/saude/w/sobre-a-rede-de-ouvidoria
