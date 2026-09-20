# Ouvidoria da Saúde — levantamento de requisitos do módulo

> **Estado:** levantamento (20/09/2026). **Nada implementado.** Esta pasta guarda os aprendizados, as referências e os requisitos do módulo de Ouvidoria do SMSMais.
> **Quem lê:** quem for planejar/implementar o módulo. Leia este README primeiro; os detalhes e as fontes estão em [`referencias/`](./referencias/).
> Pesquisa feita em 20/09/2026 (Claude Fable 5.1, quatro agentes de pesquisa web em paralelo). Cada afirmação nos relatórios está marcada como confirmada na fonte ou como inferência.

## Índice da pasta

| Arquivo | Conteúdo |
|---|---|
| [`referencias/01-marco-legal.md`](./referencias/01-marco-legal.md) | Lei 13.460/2017, Decretos 9.492 e 10.153, Lei 13.608, LAI, LGPD, Leis 8.080/8.142, Portaria de Consolidação 1/2017 (arts. 109–119), Portaria SGEP 8/2007, PN CGU 116/2024, ISO 10002. **Termina com os 18 requisitos que a norma impõe, cada um com o artigo-fonte.** |
| [`referencias/02-ouvidorsus-e-falabr.md`](./referencias/02-ouvidorsus-e-falabr.md) | Os dois sistemas de referência: tipologia, taxonomia (23/246/897/1.897), campos, fluxo, prazos, rede, pesquisa de satisfação, painel Resolveu?, API do Fala.BR. Comparação, o que copiar, armadilhas. |
| [`referencias/03-exemplos-secretarias-e-marica.md`](./referencias/03-exemplos-secretarias-e-marica.md) | **Maricá** (Ouvidoria-Geral, 156, Alô Saúde, relatórios trimestrais 2025), SMS-SP (caso mais maduro), Rio, BH, Curitiba, Fortaleza, Recife, SES-RJ/SP/MG, OuvSUS 2024, SESA-ES. 14 lições + requisitos extraídos de normas e TRs. |
| [`referencias/04-indicadores-processo-boas-praticas.md`](./referencias/04-indicadores-processo-boas-praticas.md) | Indicadores com fórmula (Resolveu?, ABO, ANS, contratos de OS), processo interno em 9 etapas, papéis, escalonamento, reabertura, fluxo de denúncia, relação com as unidades, canais, ouvidoria ativa, IA. Implicações para o modelo de dados. |
| [`referencias/fontes/`](./referencias/fontes/) | Extratos de texto (e alguns PDFs) dos documentos públicos lidos: leis, portarias, manuais do MS/CGU/SES-RJ, relatórios de Maricá, SP, ES, OuvSUS. **Nenhum dado de paciente.** |

---

## 0. Premissas de produto (Bernardo, 20/09/2026)

1. **Ouvidoria é módulo do SMSMais**, não sistema à parte. Roda **na instância de cada município** (ADR-0043: um droplet, um banco, um domínio por prefeitura; sem `TenantId`). Em Maricá é o `smsmarica.online`; no próximo cliente, o domínio dele. Nada institucional em código: nomes, canais, DPO e número de WhatsApp vêm de `smsmarica.instituicao`.
2. **O alvo é secretaria de saúde**, não ouvidoria-geral de prefeitura. Primeiro cliente fora de Maricá: **Niterói** (Fundação Municipal de Saúde, aparentemente sem sistema; processo manual). Depois, outras SMS. O diferencial é o que um genérico não tem: taxonomia do OuvidorSUS, ponto de resposta por unidade (CNES), vínculo com regulação/fila, paciente e profissional do hub FHIR, indicador contratual de OS, relatório ao Conselho de Saúde.
3. **WhatsApp pelo Automais.Zap** (ADR-0044: um App único na Meta + roteador). O número de ouvidoria de cada município é mais um número roteado; disparos e recebimento passam pelo mesmo relay das Confirmações/Mensageria. Nunca WhatsApp como repositório: toda conversa vira manifestação com protocolo no sistema.
4. **Tela do cidadão em dois lugares**: dentro do **app do cidadão** (PWA, logado, vê as próprias manifestações) e num **site público por instância, `ouvidoria.<domínio>`** (em Maricá `ouvidoria.smsmarica.online`, ainda não publicado): registrar sem login, acompanhar por protocolo + código, responder pesquisa. Mesmo molde do PWA Arquivos (`SMSMais.arquivos.pwa` → `arquivos.smsmarica.online`, deploy próprio em `/var/www`). O domínio vem de `instituicao`, nunca de código.
5. **Reaproveitar tudo que puder** (§5): Unidades, `fhir.patient`/`practitioner`, Conversas + Mensageria (ADR-0059) com a regra de destinatário correto (ADR-0057), `PesquisasSatisfacao`, Regulação (ADR-0052), catálogo canônico (ADR-0055), Perfis/`ModuloPermissao`, Auditoria, Institucional, PWA do cidadão, módulo IA. O que é novo: manifestação, tramitação, ponto de resposta, trilho de denúncia, taxonomia, painéis e relatórios da ouvidoria.

## 1. O que é uma ouvidoria de saúde (e o que não é)

A ouvidoria **recebe, registra, classifica, encaminha, cobra e devolve** manifestações do cidadão sobre os serviços de saúde; ela **não resolve** o problema e **não investiga** — quem resolve é a unidade/área (ponto de resposta) e quem investiga é corregedoria/comissão de ética/CRM. O Manual do MS é explícito: ouvidoria "não é estrutura de marcação de consulta nem auditoria/corregedoria". A PN CGU 116/2024 art. 34 **veda** à ouvidoria fazer diligência, depoimento ou investigação.

Seis tipos oficiais (DOGES/MS, Quadro 5 do Manual 2014), com a **natureza da providência** derivada:

| Tipo | Definição curta | Providência |
|---|---|---|
| **Solicitação** | contém **requerimento** de atendimento/acesso (vaga, exame, medicamento) | Atender |
| **Reclamação** | insatisfação **sem** requerimento | Apurar |
| **Denúncia** | irregularidade ou indício; vai a unidade apuratória | Apurar |
| **Sugestão** | proposta de melhoria | Conhecer |
| **Elogio** | satisfação; vai ao agente e à chefia | Conhecer |
| **Informação** | pergunta; se respondida no ato é "disseminação" | Atender |

Mais "**Comunicação de irregularidade**" = denúncia obrigatoriamente anônima, sem protocolo de acompanhamento (OuvidorSUS 3). Pedido de **acesso à informação** é LAI (20+10 dias), outro fluxo.

**Fato dominante para Maricá:** no OuvidorSUS 2024, 53% das manifestações sobre serviços municipais são **solicitações** (vaga, exame, medicamento) e 30% reclamações. Em Maricá, o 4º tri/2025 já mostra a **Central de Regulação** como 2º alvo de reclamações de saúde. Ouvidoria de saúde municipal é, em grande parte, **a porta de queixa da regulação e da farmácia**.

## 2. O que a lei obriga (resumo — detalhes em `01-marco-legal.md`)

A **Lei 13.460/2017** vale para o município desde 2019 e é a fonte das obrigações duras:

| Obrigação | Norma |
|---|---|
| Nunca recusar manifestação; múltiplos canais; verbal reduzida a termo | L13460 arts. 10 §4º, 11 |
| CPF basta para identificar; nenhum outro número exigível; sem campo "motivo" | L13460 arts. 10 §2º, 10-A |
| Comprovante de recebimento (protocolo) + ciência formal da decisão | L13460 art. 12 |
| **30 dias**, prorrogável **uma vez** por 30 com justificativa; áreas internas **20+20** | L13460 art. 16 |
| Identificação do manifestante = informação pessoal com **restrição de acesso** | L13460 art. 10 §7º; LAI art. 31 |
| **Relatório de gestão anual** publicado na íntegra (nº, motivos, recorrentes, providências) | L13460 arts. 14–15 |
| **Pesquisa de satisfação ao menos anual**, publicada com **ranking de reclamações** | L13460 art. 23 |
| Carta de Serviços com prazo por serviço e "como reclamar" | L13460 art. 7º |
| Conselho de Usuários (consultivo); articulação com o Conselho Municipal de Saúde | L13460 arts. 18–22; L8142 |
| Município **deve manter ouvidoria ou correição** para denúncias; proteção do denunciante | L13.608 arts. 4º-A/B/C |
| Sigilo da fonte a pedido; gestores **devem usar os dados** da ouvidoria | PRC 1/2017 arts. 115 VI, 118 |
| Dado de saúde é sensível; base legal = obrigação legal/política pública (**não** consentimento); encarregado; retenção documentada | LGPD arts. 11, 23, 46; Guia Renouv 2022 |

Os decretos federais (9.492, 10.153) e a **PN CGU 116/2024** não obrigam o município, mas são o padrão nacional e a melhor especificação funcional pública: complementação em 20 dias que **suspende o prazo uma vez** e arquiva sem resposta; encaminhamento externo **sem prorrogação**; **conteúdo mínimo da resposta por tipo** (art. 29); motivos tipificados de arquivamento (art. 31); **pseudonimização** de denúncia antes de encaminhar, **log nominal e datado de todo acesso**, consentimento em 20 dias para compartilhar identidade, restrição por 100 anos (arts. 39–44); retenção 5+5 anos (denúncia 5+15).

Falta em Maricá: **não localizamos ato normativo municipal** regulamentando a Lei 13.460 (só Decreto 78/2025 citado, não lido) nem ouvidoria setorial da saúde. O módulo vai precisar de uma **portaria da SMS** instituindo o sistema como registro único e definindo prazos e pontos de resposta (modelo: Portarias SMS-SP 152/2026 e 870/2025; Decreto Inajá-PE 006/2025).

## 3. Como as outras secretarias fazem (resumo — detalhes em `03-exemplos-…`)

- **Duas instâncias**: solicitação de serviço (156/1746) ≠ manifestação de ouvidoria. Rio só aceita "reclamação" sobre protocolo anterior. Em Maricá o 156 já é "primeira instância sob a Ouvidoria".
- **Ponto de resposta por unidade** é entidade de primeira classe: SP tem ~2.000 com login individual; regra de titular + suplente. A ouvidoria audita a qualidade da resposta e devolve se insatisfatória.
- **Prazos internos menores que os legais**: SP 20 dias à área e 2 dias úteis de trânsito; SES-RJ por prioridade (urgente 2 dias úteis / 15 / 30); GDF 5 dias úteis; MS 3 dias úteis para encaminhar.
- **Três modos de identificação**: identificada, sigilosa (não vai à área), anônima (só denúncia, sem resposta).
- **Relatórios**: mensal ao gestor, **trimestral por unidade com "considerações do gestor"**, anual, ao Conselho Municipal de Saúde. Painéis com % no prazo por faixa (≤30/31–60/>60), tempo médio, **estoque**, resolutividade, satisfação. Rio: metas >90% no prazo, <10 dias, estoque ≤10%.
- **Canais**: telefone ainda domina (53% em BH); presencial pesa em hospital (46%); urna existe; WhatsApp é marginal em geral **mas é o canal de Maricá**. SES/SP proíbe WhatsApp como *repositório* em contratos de OS — o registro tem que ir ao sistema.
- **SMS-SP saiu do OuvidorSUS para sistema próprio em 2025** (10 anos depois). Sistema municipal próprio é caminho já trilhado.
- **Problema real noticiado é atraso e estoque**, não falta de canal: Maricá 10% "atrasados", Recife 25% fora do prazo, ES 623 com >60 dias.
- **Indicadores em contratos de OS**: resolução de reclamações ≥85% com causa raiz (ES), primeira tratativa ≤7 dias úteis, queixas por atendimento (Salto/SP). O HMCML já tem o indicador contratual **"Resolubilidade de Ouvidorias ≥ 90%"** no módulo Indicadores (seed `SeedIndicadoresHmcml`, hospital 1, item 12, hoje "fora do banco").

## 4. Requisitos consolidados do módulo

### 4.1 Registro
- **R1** Tipologia fechada (6 tipos + comunicação de irregularidade) com regras: solicitação/informação nunca anônimas; anônimo só denúncia; providência derivada (Atender/Apurar/Conhecer).
- **R2** Taxonomia assunto → subassunto **compatível com OuvidorSUS 3** (importar o Manual de Tipificação Ouv3 do dataset de dados abertos) + **marcadores locais** + listas dependentes ligadas ao domínio já existente: unidade (CNES), procedimento (catálogo canônico, ADR-0055), medicamento.
- **R3** Canal de entrada (WhatsApp, 156, presencial, web/PWA, e-mail, carta, urna, busca ativa, 136/OuvidorSUS, Fala.BR) e **origem de atendimento** como campos separados; protocolo externo quando veio de outro sistema.
- **R4** Pessoas: manifestante × **referido** (paciente → `fhir.patient` se houver CPF/CNS) × **envolvidos** (profissional → `fhir.practitioner`, nunca exposto no trilho assistencial). Local do fato = unidade. Dia e hora da ocorrência.
- **R5** Identificação em três níveis (identificada / sigilosa / anônima); dados do manifestante **nunca** visíveis ao ponto de resposta; CPF como identificador; sem campo "motivo".
- **R6** Nunca recusar; formulário mínimo (tipo, identificação, teor); verbal reduzido a termo; papel digitalizado no ato; anexos.
- **R7** Protocolo automático + código de acesso; comprovante de recebimento pelo canal de entrada.
- **R8** Vínculo opcional com **solicitação de regulação / atendimento / ticket** de origem (Rio exige; aqui liga ao ADR-0052 e ao módulo de regulação).
- **R9** Desmembramento de uma manifestação em várias; detecção de **manifestação similar do mesmo cidadão** na triagem; duplicada arquiva citando a original.

### 4.2 Tramitação (máquina de estados com trilha append-only)
- **R10** Estados: Cadastrada → Em triagem (consistência, tipificação, prioridade, desmembramento) → Atribuída → Encaminhada (ponto de resposta / outra ouvidoria / órgão externo) → Aguardando complementação (20 d, suspende 1×, auto-arquiva) → Respondida pela área → Em validação pela ouvidoria → Respondida ao cidadão (intermediária/conclusiva) → Recurso/Reaberta (1×) → Concluída / Arquivada (motivo tipificado) / Encaminhada a outro órgão. Auto-arquivo 30 d após resposta sem recurso.
- **R11** **Ponto de resposta** por unidade e por área central (regulação, farmácia, RH…), com titular + suplente, login individual, inativação na saída; posse explícita (mesmo padrão dos ADR-0047/0059).
- **R12** Prazos: cidadão 30+30 (prorrogação única com justificativa registrada e aviso ao cidadão); área 20+20 (configurável para menos: 5–10 dias na prática); trânsito interno ≤ 2 dias úteis; encaminhamento externo sem prorrogação; **prioridade** (urgente/alta/normal) que encurta o prazo; feriados. Colunas derivadas `dias_ate_resposta`, `dias_atraso`.
- **R13** Cobrança e **escalonamento** por atraso: reiteração → chefia → autoridade máxima, como eventos; alertas ao ponto de resposta e ao ouvidor.
- **R14** Resposta conclusiva com **conteúdo mínimo por tipo** (art. 29 PN 116), linguagem simples, e marcação **resolvida/não resolvida** (alterável); **situação final estruturada** (atendida / não atendida + motivo: vagas insuficientes, não coberto pelo SUS, não compareceu… / não localizado / faleceu; denúncia: procede / não procede / inconclusiva).
- **R15** Validação da resposta da área pela ouvidoria antes da devolutiva (devolver para reanálise); auditoria de qualidade do registro e da resposta (PAQ).
- **R16** Mediação/conciliação registrada (não em denúncia).

### 4.3 Trilho de denúncia (isolado)
- **R17** Juízo de admissibilidade (autoria, materialidade, competência, compreensão); **habilitação datada** (gatilho antirretaliação).
- **R18** **Pseudonimização** por padrão ao encaminhar (dados cadastrais + trechos identificadores do teor e anexos; extrato/versão tarjada); acesso à identidade só com **justificativa logada (nome, data)**; consentimento do denunciante em 20 dias para compartilhar; restrição por 100 anos; retenção 5+15.
- **R19** Unidade apuratória (corregedoria, comissão de ética, vigilância, CRM) como destinatário; resposta ao cidadão = "encaminhada/arquivada"; retorno da conclusão da apuração. **A ouvidoria não investiga.**
- **R20** Denúncia de servidor/profissional: sigilo também do servidor citado durante a apuração; direito de defesa acontece na apuração, não na ouvidoria.
- **R21** Ouvidoria interna (servidores e colaboradores como manifestantes) com proteção contra retaliação.

### 4.4 Cidadão
- **R22** Acompanhamento por protocolo + código **sem login** (e também dentro do PWA do cidadão, logado), com **histórico filtrado** (esconde tipificação, atribuição, prorrogação, anotações internas); "adicionar informação"; recurso após resposta.
- **R23** Notificações a cada etapa por WhatsApp/e-mail via **Mensageria** (ADR-0059), respeitando **destinatário correto** (ADR-0057) — manifestação sigilosa não gera mensagem com teor.
- **R24** **Pesquisa de satisfação** pós-resposta, uma por protocolo, com as 3 perguntas do Fala.BR ("demanda atendida? / resposta fácil de compreender? / satisfeito com o atendimento?" + comentário); separar resolutividade percebida de satisfação com a ouvidoria. Reaproveitar `PesquisasSatisfacao`.
- **R25** Base de conhecimento para resposta no ato ("disseminação") — casa com o módulo IA (ADR-0011) e o robô de atendimento (ADR-0050).

### 4.5 Gestão, transparência e integração
- **R26** Painel interno: volume por tipo/assunto/canal/unidade/ponto de resposta; % no prazo (faixas ≤30 / 31–60 / >60); tempo médio de resposta e tempo médio da área; **estoque**; resolutividade (ouvidoria) vs "atendida" (cidadão); satisfação; reincidência (mesmo CPF × assunto × unidade em N dias); recorrência por unidade; **manifestações por 1.000 atendimentos da unidade**; ranking de unidades e assuntos. Metas configuráveis (referência Rio: >90%, <10 d, ≤10%).
- **R27** Relatórios: mensal ao gestor; **trimestral por unidade** com campo "considerações do gestor" e plano de ação; **anual de gestão** (itens do art. 15 L13460 + art. 60 PN 116) publicável; exportação para o **Conselho Municipal de Saúde**; alimentação do indicador contratual do HMCML ("Resolubilidade de Ouvidorias").
- **R28** Painel público estilo "Resolveu?" e exportação de dados abertos **pseudonimizados** no esquema do dicionário do Fala.BR (data registro, prazo, resposta, tipo, assunto, unidade, dias para resolução, dias de atraso, demanda atendida, satisfação).
- **R29** Perfis/permissões: ouvidor (gestor), técnico de ouvidoria, técnico **com** e **sem** acesso a sigilosas, ponto de resposta (escopo = sua unidade/área), atendente 156/Alô Saúde (só registra), leitura para controle social. Módulo(s) novos no `ModuloPermissao` (próximo livre: 71+). Auditoria de acesso a dados pessoais.
- **R30** Interoperabilidade: campo protocolo externo (OuvidorSUS / Fala.BR / Ouvidoria-Geral / 156); tipos com os códigos do Fala.BR e taxonomia do OuvidorSUS para permitir, se o município aderir, virar cliente **WebService Respondente** do Fala.BR (OAuth2, polling, sem webhook). Receber o que o 136 encaminha ao município.
- **R31** Ouvidoria ativa: pesquisa proativa por amostragem pós-atendimento/alta (o gatilho de pesquisa já existe no SMSMais e está desligado), QR code na recepção/leito, urna com registro no sistema, itinerante via PWA; registro em ≤ 24 h.
- **R32** Página pública "Ouvidoria" (canais, horários, fluxo, relatórios, painel, ouvidor) e Carta de Serviços; institucional vem de `smsmarica.instituicao` (ADR-0043).
- **R33** IA só como **sugestão com override humano**: classificação de tipo/assunto/unidade com confiança; minuta de resposta sempre editada e assinada por técnico; **nunca** IA sobre denúncia antes da pseudonimização; guardar sugerido × enviado e rotular (Guia de Transparência em IA do gov.br; LGPD art. 20).

### 4.6 Não funcionais
- LGPD: base legal obrigação legal/política pública; publicidade da dispensa de consentimento e do encarregado; necessidade de conhecer; retenção; segurança desde a concepção.
- Acessibilidade (eMAG), linguagem simples, responsivo. Gravação/registro de atendimento telefônico se houver central própria.

## 5. Encaixe no SMSMais (o que já existe e será reaproveitado)

| Já existe | Uso na ouvidoria |
|---|---|
| `Unidades` (CNES), `X-Unidade-Id` | local do fato; escopo do ponto de resposta |
| `fhir.patient` / `fhir.practitioner` | referido e envolvidos, sem duplicar cadastro |
| `Conversas` + `Notificacoes/WhatsApp` + Mensageria (ADR-0059) | canal de captação (Alô Saúde / WhatsApp da ouvidoria) e notificações; regra ADR-0057 |
| `PesquisasSatisfacao` + gatilho de alta (desligado) | pesquisa pós-resposta e ouvidoria ativa |
| `Tickets` (Suporte) | modelo de máquina de estados com posse; **não** reutilizar a entidade (domínio diferente: cidadão vs operador) |
| `Regulacao` / solicitação de regulação (ADR-0052) | vínculo da manifestação-solicitação ao pedido real; situação final "vagas insuficientes" |
| Catálogo canônico de procedimentos (ADR-0055) | lista dependente da tipificação |
| `Auditoria` + `IUsuarioAtualAccessor` | log nominal e datado de acesso a denúncia |
| `Indicadores` (HMCML item "Resolubilidade de Ouvidorias") | fonte do indicador contratual |
| PWA do cidadão / PWA Arquivos (ponte por QR) | acompanhamento logado; QR de recepção/leito |
| `Institucional` (ADR-0043) | nada institucional em código: nomes, canais, DPO |
| Módulo IA (ADR-0011/0050) | classificação sugerida; disseminação |

Régua smsmarica × FHIR: manifestação, tramitação, pontos de resposta, pesquisa → **`smsmarica.*`** (regra de negócio). Só pessoa (paciente/profissional) fica em `fhir.*`.

## 6. Decisões em aberto (para o Bernardo)

- **D-1 Posição institucional (parcialmente decidida).** Decidido: módulo da **SMS**, na instância do município (§0). Em aberto por município: como a ouvidoria da saúde se relaciona com a **Ouvidoria-Geral** local (em Maricá a OGM já recebe saúde por 156/WhatsApp/Alô Saúde). Mínimo viável: SMS como **ponto de resposta** da OGM com sistema próprio e conciliação por protocolo externo; ideal: portaria fazendo do SMSMais o registro único da saúde.
- **D-2 OuvidorSUS/Fala.BR.** Aderir ao OuvidorSUS 3 como ouvidoria de destino (recebe o que chega pelo 136) e/ou ao Fala.BR (API)? Ambos voluntários e gratuitos; exigem termo do dirigente máximo e normativo local. Sem adesão, o 136 chega por ofício ou não chega.
- **D-3 Canal principal (parcialmente decidida).** Decidido: WhatsApp via **Automais.Zap** (número da ouvidoria do município roteado) + PWA + presencial nas unidades (QR/urna). Em aberto: integração com a central telefônica local (156 em Maricá) e se o robô de atendimento faz a captação inicial (só registro e protocolo; nunca classificação final; nunca denúncia sem pseudonimização).
- **D-4 Prazos internos.** Legal 30+30 / 20+20. Adotar prazo à unidade de 10 dias (SES-RJ) ou 5 dias úteis (GDF) com prioridade urgente de 2 dias úteis?
- **D-5 Anonimato.** Só denúncia (padrão Fala.BR) ou também reclamação (BH, Fortaleza)? Recomendação: só denúncia, como comunicação de irregularidade.
- **D-6 Alcance da apuração.** Quem é a unidade apuratória em Maricá (corregedoria da prefeitura, comissão de ética da SMS, RH)? Define o destinatário do trilho de denúncia.
- **D-7 Solicitação de acesso.** Manifestação-solicitação abre/vincula pedido de regulação automaticamente ou só registra e encaminha à Regulação como ponto de resposta?

## 7. Próximos passos sugeridos
1. Bernardo decide D-1..D-7 (ou grava áudios, como na Regulação → `descricao inicial.txt`).
2. Baixar pelo navegador o **Manual de Tipificação OuvidorSUS 3** e o **dicionário Ouv3** em https://dadosabertos.saude.gov.br/dataset/ouvidorsus (o fetch automático falhou) → guardar em `referencias/fontes/`.
3. Obter o **Decreto 78/2025** de Maricá e confirmar se há ouvidor do SUS cadastrado no MS.
4. Escrever o ADR do módulo (entidades, máquina de estados, trilho de denúncia, permissões) e o plano numerado no padrão de `SMSMais.Regulacao/`.
5. Minuta de **portaria da SMS** instituindo o sistema como registro único (molde: SMS-SP 152/2026 + Inajá 006/2025).
