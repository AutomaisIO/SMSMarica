# ADR-0060 — Módulo de Ouvidoria da saúde: manifestação com protocolo, ponto de resposta por unidade, trilho de denúncia isolado

**Status:** aceito · **Data:** 2026-09-20
**Implantação:** em andamento — planejamento em `SMSMais.ouvidoria/`; fase 1 (backend + painel) iniciada em 20/09/2026.

## Contexto

O SMSMais é entregue como uma instância por município (ADR-0043) e o produto passa a ser oferecido a **secretarias de saúde** em geral. Toda secretaria é obrigada pela **Lei 13.460/2017** a receber, protocolar e responder manifestações do cidadão em 30 dias (prorrogáveis uma vez), publicar relatório anual e fazer pesquisa de satisfação. Na prática, a maioria opera por WhatsApp, e-mail e planilha, ou depende da ouvidoria-geral da prefeitura, sem sistema da saúde.

O levantamento em [`SMSMais.ouvidoria/`](../../SMSMais.ouvidoria/README.md) (marco legal, OuvidorSUS × Fala.BR, SMS-SP/Rio/BH/Maricá, indicadores) mostrou três fatos que orientam o desenho:

1. **Mais da metade das manifestações municipais de saúde são solicitações de acesso** (vaga, exame, medicamento) e a **Central de Regulação** já é o 2º alvo de reclamações em Maricá. Ouvidoria de saúde é a porta de queixa da regulação e da farmácia — precisa enxergar a solicitação de regulação, não duplicá-la.
2. **A ouvidoria não resolve nem investiga.** Ela encaminha a um **ponto de resposta** (a unidade/área), cobra, valida a resposta e devolve ao cidadão. Denúncia vai a uma unidade apuratória, pseudonimizada, com **log nominal de todo acesso** (Decreto 10.153/2019 art. 6º §3º).
3. **Quem sustenta uma rede de ouvidoria é o ponto de resposta por unidade com posse explícita** (SMS-SP tem ~2.000 com login individual). O SMSMais já tem esse padrão em Conversas (ADR-0047) e Confirmações (ADR-0059).

## Decisão

### 1. Ouvidoria é módulo do SMSMais, na instância do município
Sem sistema à parte e sem `TenantId`. Entidades novas em `smsmarica.*` (regra de negócio, pt-BR); pessoas continuam em `fhir.*` (`Patient` para o referido, `Practitioner` para o envolvido), FK só na direção `smsmarica → fhir` (ADR-0001/0007). Nada institucional em código: canais, domínio público e número de WhatsApp vêm de `smsmarica.instituicao` (ADR-0043).

### 2. Manifestação com protocolo público e seis tipos fechados
`OuvidoriaManifestacao` com **protocolo legível** (`AAAA-NNNNNN`, sequência por ano) + **código de acesso** para acompanhamento sem login. Tipos (enum, string no JSON): `Solicitacao`, `Reclamacao`, `Denuncia`, `Sugestao`, `Elogio`, `Informacao`. Regras impostas na criação: **solicitação e informação nunca anônimas; anônimo só em denúncia** (vira "comunicação de irregularidade", sem código de acesso). Identificação em três níveis: `Identificada`, `Sigilosa`, `Anonima`. **Dados do manifestante nunca vão ao ponto de resposta**: o DTO de tramitação não os carrega.

Campos de contexto que um genérico não tem: `UnidadeId` (local do fato, CNES), `PatientId?` (referido), `PractitionerId?` (envolvido, só no trilho de denúncia/apuração), `SolicitacaoRegulacaoId?` (vínculo com o pedido real, ADR-0052), `ProtocoloExterno?` (OuvidorSUS / Fala.BR / ouvidoria-geral / 156), `Canal` (WhatsApp, PWA, site público, presencial, telefone, e-mail, carta, urna, busca ativa, 136, Fala.BR, outro) e `Origem` (cidadão, ouvidoria ativa, de ofício, coletiva).

### 3. Taxonomia compatível com o OuvidorSUS + marcadores locais
`OuvidoriaAssunto` (árvore assunto → subassunto, com `CodigoOuvidorSus?` para exportação) + `OuvidoriaMarcador` (tags livres da instância). A árvore é **catálogo editável pelo ouvidor**, seedada com os 22 assuntos do Manual do MS; o Manual de Tipificação Ouv3 completo entra quando for baixado (`SMSMais.ouvidoria/referencias`). Classificação inicial pode ser sugerida (IA, fase 3) mas **a estatística usa sempre a classificação da ouvidoria**.

### 4. Máquina de estados com trilha append-only e dois relógios
Estados: `Registrada → EmTriagem → Encaminhada → AguardandoComplementacao → RespondidaPelaArea → EmValidacao → Respondida → (Recurso → Encaminhada) → Concluida | Arquivada | EncaminhadaOutroOrgao`. Cada transição é uma linha em `OuvidoriaEvento` (quem, quando, de → para, texto, anexos, visível ao cidadão?). Sem `UPDATE` de histórico.

Relógios: **cidadão 30 dias corridos + 1 prorrogação de 30 com justificativa registrada e aviso** (`PrazoRespostaEm`, `ProrrogadoEm`); **ponto de resposta 20 dias, configurável por instância para menos** (`PrazoAreaEm`); **prioridade** (`Urgente` 2 dias úteis / `Alta` 10 / `Normal` 20) encurta o prazo da área; **complementação suspende o relógio uma única vez** e auto-arquiva em 20 dias sem resposta; **encaminhamento a outro órgão não admite prorrogação**. Colunas derivadas `DiasAteResposta` e `DiasAtraso` gravadas na conclusão.

Conclusão exige **conteúdo mínimo por tipo** (PN CGU 116 art. 29), **resolutividade** (`Resolvida` / `NaoResolvida`, alterável) e **situação final estruturada** (`Atendida`, `NaoAtendida` + motivo, `NaoLocalizado`, `Faleceu`; denúncia: `Procede` / `NaoProcede` / `Inconclusiva`). Arquivamento só com motivo tipificado (art. 31).

### 5. Ponto de resposta é entidade de primeira classe, com posse
`OuvidoriaPontoResposta` = unidade (CNES) **ou** área central (regulação, farmácia, RH, vigilância, apuração), com **titular e suplentes** (usuários) e prazo próprio. Encaminhar atribui posse ao ponto; a resposta da área volta à ouvidoria, que **valida antes de devolver** ao cidadão (pode devolver para reanálise). Atraso gera **cobrança** (evento) e **escalonamento** ao gestor do ponto e ao ouvidor. Usuário com escopo de ponto de resposta enxerga só as manifestações encaminhadas ao seu ponto, **sem dados do manifestante**.

### 6. Trilho de denúncia isolado
Denúncia tem `HabilitadaEm` (juízo de admissibilidade: autoria, materialidade, competência), `UnidadeApuratoriaId` (um ponto de resposta do tipo `Apuracao`), texto **pseudonimizado** editado pela ouvidoria antes do encaminhamento (`TeorPseudonimizado`), e **`OuvidoriaAcessoIdentidade`**: toda leitura dos dados do denunciante grava usuário, data e justificativa. Compartilhar identidade com outro órgão exige consentimento registrado (20 dias). Permissão separada: técnico **com** e **sem** acesso a sigilosas.

### 7. Canais: captação em vários lugares, registro num só
- **Painel** (`SMSMais.front`, feature `ouvidoria`): registro presencial/telefônico pelo técnico, triagem, tramitação, painéis, relatórios.
- **Site público por instância** `ouvidoria.<domínio>` (novo PWA `SMSMais.ouvidoria.pwa`, molde do Arquivos): registrar sem login, acompanhar por protocolo + código, complementar, recorrer, responder pesquisa.
- **App do cidadão** (PWA logado): as próprias manifestações.
- **WhatsApp** pelo Automais.Zap (ADR-0044) e Mensageria (ADR-0059): notificações de cada etapa e, na fase 2, captação inicial. Regra do destinatário correto (ADR-0057) vale; manifestação sigilosa **não** gera mensagem com teor.
- Nunca WhatsApp, e-mail ou papel como repositório: **tudo vira manifestação com protocolo**.

### 8. Pesquisa e indicadores
Pesquisa pós-resposta, uma por protocolo, com as três perguntas do Fala.BR ("demanda atendida?", "resposta fácil de compreender?", "satisfeito com o atendimento?") + comentário, disparada pelo canal de entrada. Indicadores por período × unidade × tipo × assunto: volume, % no prazo por faixa (≤30 / 31–60 / >60), tempo médio (total e da área), estoque, resolutividade (ouvidoria) × "atendida" (cidadão), satisfação, reincidência (mesmo CPF × assunto × unidade em 90 dias), manifestações por 1.000 atendimentos. Relatório trimestral por unidade com "considerações do gestor" e relatório anual (Lei 13.460 art. 15). Alimenta o indicador contratual "Resolubilidade de Ouvidorias" do HMCML.

### 9. Permissões (`ModuloPermissao`)
`Ouvidoria` (71: técnico — registra, tria, encaminha, responde ao cidadão), `OuvidoriaGestao` (72: ouvidor — configurações, pontos de resposta, taxonomia, relatórios, escalonamento), `OuvidoriaSigilo` (73: acesso a denúncias e à identidade em sigilosas; todo acesso é logado), `OuvidoriaPontoResposta` (74: responde pelo seu ponto; nunca vê manifestante). Endpoints públicos do cidadão (`/publico/ouvidoria/*`) sem JWT, com protocolo + código e limitação de taxa.

## Consequências

- Nasce um segundo "domínio de atendimento" ao lado de Conversas/Confirmações; eles compartilham Mensageria e Automais.Zap, mas **manifestação não é conversa nem ticket** — não reutilizar `Ticket` (operador interno) nem `Conversa` (thread de WhatsApp) como entidade.
- O município precisa de **ato normativo** instituindo o SMSMais como registro único da ouvidoria da saúde e nomeando pontos de resposta (molde: Portarias SMS-SP 152/2026 e 870/2025; Decreto Inajá-PE 006/2025). Sem isso o módulo funciona, mas convive com o canal paralelo.
- Adesão ao OuvidorSUS/Fala.BR fica **opcional e por município**; o modelo já carrega `ProtocoloExterno` e códigos compatíveis para integrar depois.
- Retenção: manifestações comuns 5+5 anos, denúncias 5+15; identidade do denunciante restrita por 100 anos — **nunca expurgar identidade**, só restringir.
- IA (fase 3) só sugere tipo/assunto/unidade e minuta; humano decide e assina; **nunca** sobre denúncia antes da pseudonimização.

## Fases

| Fase | Entrega |
|---|---|
| **1** | Backend (entidades, migration, services, controllers, permissões, seed de taxonomia) + painel do técnico/ouvidor (registro, fila, triagem, encaminhar, responder, concluir, pontos de resposta, taxonomia) + notificações por WhatsApp/Mensageria nas transições. |
| **2** | Site público `ouvidoria.<domínio>` + app do cidadão + pesquisa pós-resposta + captação por WhatsApp + painéis e relatório trimestral/anual + exportação pseudonimizada (esquema Fala.BR). |
| **3** | Ouvidoria ativa (QR na recepção/leito, urna digital, itinerante), IA de sugestão, integração OuvidorSUS/Fala.BR (WebService Respondente), mediação. |

## Referências
- Levantamento e fontes: [`SMSMais.ouvidoria/`](../../SMSMais.ouvidoria/README.md)
- Lei 13.460/2017; Decretos 9.492/2018 e 10.153/2019; Lei 13.608/2018; PN CGU 116/2024; Portaria de Consolidação GM/MS 1/2017 arts. 109–119; Manual das Ouvidorias do SUS (MS, 2014).
- ADR-0043 (instância por município), ADR-0044 (Automais.Zap), ADR-0047/0059 (posse), ADR-0052 (solicitação de regulação), ADR-0055 (catálogo), ADR-0057 (destinatário correto).
