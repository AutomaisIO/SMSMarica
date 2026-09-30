# ADR-0063 — ESUS de São Gonçalo como fonte de regulação e análise automática das regras sobre os pedidos que chegam

**Status:** aceito · **Data:** 2026-09-30
**Relacionado:** [ADR-0042](./0042-ser-segunda-fonte-de-regulacao.md) (SER como segunda fonte — este é o
terceiro irmão) · [ADR-0052](./0052-fila-pre-regulacao-e-agente-regulador.md) (solicitação da regulação ligada a
espelhos por FK) · [ADR-0055](./0055-catalogo-canonico-e-embeddings.md) (catálogo canônico) ·
[ADR-0041](./0041-identidade-incompleta-no-hub.md) (sem CPF entra marcado) ·
[ADR-0009](./0009-identidade-e-proveniencia-multi-pep.md) (um registro por fonte) ·
[ADR-0012](./0012-agendamento-local-e-integracao-sisreg-leitura.md) (só leitura)

## Contexto

Maricá manda pacientes para exames em São Gonçalo pela PPI (retina, glaucoma, catarata, estrabismo,
pterígio, YAG, auditiva e outros). Quem inclui esses pedidos é a própria equipe de Maricá, direto no
sistema de regulação de São Gonçalo: o **ESUS** (`saogoncalo.esusmais.com.br`). O nome confunde:
**não é o e-SUS do governo** (o e-SUS APS do Ministério). É o produto ESUS da empresa esusmais, que São
Gonçalo usa como sistema municipal de regulação. Maricá entra nele como uma "unidade solicitante".

Medido no laboratório (`Automais.esus_saocongalo/`, 30/09/2026):

- A conta de Maricá vê a **fila de regulação** (650 pedidos de exame; consulta vazia) e os **pacientes
  agendados pela fila** (4.924 agendamentos de exame de 2019 a 2026). A tela de **excluídos de exame é
  negada** ("Usuário não possui permissão").
- Não há HTML para raspar: o front é um SPA que fala JSON com dois backends (Node/GraphQL em `:8001`,
  PHP legado em `:9001`). Login duplo, um token por backend.
- Um pedido (`fil_id`) pode ter **várias sessões agendadas** (`eap_id` diferentes). Fila e agendados
  são conjuntos disjuntos com a mesma chave: agendado, o pedido sai da fila.
- O "total" que o ESUS declara conta registros únicos, mas o offset/limite conta linhas brutas, e a
  lista de agendados repete linhas. Paginar até "offset ≥ total" **perdeu agendamentos**.
- A mediana de espera na fila de exame passava de **900 dias**. O próprio ESUS avisou por
  WhatsApp/SMS só 4 dos 64 agendados do período medido.

Ao mesmo tempo, o pedido do Bernardo foi "analisar as novas solicitações com as regras". O
levantamento mostrou que as regras de elegibilidade (plano 03, `regulacao_regra`) **só rodavam no
assistente de Nova Solicitação**, para pedidos criados dentro do SMSMais. O que alguém incluía direto
no SER, no SERNIT ou no ESUS nunca era conferido.

## Decisão

### 1. ESUS SG é o terceiro irmão do SER, no mesmo desenho

- Espelho próprio em `smsmarica.esussg_*` (`solicitacao`, `evento`, `gatilho`, `varredura_execucao`,
  `varredura_falha`, `catalogo_recurso`), **uma linha por pedido para todas as situações**.
  Chave natural (tipo, `fil_id`).
- `SistemaRegulacao.EsusSg = 5`. O valor `Esus = 4` continua **reservado ao e-SUS do governo**: são
  produtos diferentes e não compartilham identificador.
- Situações derivadas do que a conta vê: `EmFila`, `Pendente` (pendência ativa), `Agendada` e
  **`SaiuDaFila`**. A última marca quem não está mais em nenhuma das duas listas. O motivo
  (exclusão, cancelamento, transferência) não é visível, então **não** vira "cancelada" nem na
  regulação (`MapaSituacaoExterna.DeEsusSg` devolve nulo) nem na ficha do paciente (situação própria
  "Saiu da fila").
- A saída só é declarada quando a leitura **fechou a conta** (únicos = declarado) na fila daquele tipo
  e em todos os meses de agendados. Falha de leitura nunca vira "saiu da fila". A rodada com falha
  termina como `Parcial`, nunca como `Concluída`.
- A trilha é **montada**. O ESUS não mostra histórico por pedido à conta de Maricá. Os eventos saem
  dos marcos com data e autor das listas (inclusão na fila pelo servidor de Maricá, cada sessão
  agendada pelo servidor de São Gonçalo) e das diferenças entre rodadas (prioridade, pendência,
  reagendamento, resposta do paciente, saída). Evento de diferença carrega a data em que a varredura
  percebeu, e diz isso. A tabela de eventos usa os **mesmos nomes de coluna** do `sernit_evento`, e as
  estatísticas de operadores funcionam só com o prefixo novo.
- Motor igual ao do SERNIT: fila de capacidade 1, runner que retoma na subida pelo cursor (mês de
  agendados), agendador diário configurável na tela. A **carga inicial** lê todo o histórico mês a
  mês desde 2015. A **diária** lê a fila inteira e os agendados de 45 dias atrás a 400 dias à frente.
  A janela à frente é larga para que um pedido agendado para daqui a meses não seja dado como sumido.
- Catálogo: o combo "procedimentos reguláveis por solicitante" do próprio ESUS (14 exames medidos)
  vira origem do catálogo canônico com `Sistema = EsusSg`. É o que dá procedimento canônico aos pedidos
  do SG e deixa as regras alcançá-los. O ESUS entra como destino no assistente, sem formulário
  dinâmico: a inclusão no ESUS não foi mapeada.
- Paciente: conciliação com o hub FHIR pelo upsert canônico, âncora no CPF válido e, sem ele, no CNS
  com a marca de identidade incompleta. Um registro por fonte (`meta.source` `esussg-saogoncalo`).
- **Só leitura.** A trava recusa toda ação do legado que não comece por verbo de leitura, e também a
  que embute escrita. Conferido contra as 736 ações que o front conhece. `obter-comprovante-*` também
  é recusada: gerar o comprovante pode carimbar "comprovante impresso" no SG. Não há porta de
  escrita. O usuário de Maricá **tem** permissão de agendar e excluir no SG; usar isso exige novo OK
  e novo desenho.
- Módulos de permissão `RegulacaoEsusSg = 77` (só `Consulta` nesta entrega) e `EstatisticaEsusSg = 78`.
  A configuração do motor usa `RegulacaoConfiguracao`, como SER/SERNIT.
- O paciente vê: a ficha lista os agendamentos do ESUS SG, e o robô do WhatsApp informa o agendamento
  futuro com data, hora e unidade de São Gonçalo. Na "posição na regulação" o robô segue a regra
  minimizada de sempre, e "saiu da fila" tem o mesmo tratamento do cancelado (um atendente entra em
  contato).

### 2. Análise automática das regras sobre os pedidos que CHEGAM (SER, SERNIT e ESUS SG)

- Um worker, a cada 10 minutos, passa os pedidos **em aberto** (em fila ou pendentes) dos três espelhos
  pelo **mesmo avaliador puro** do assistente (`AvaliadorElegibilidade`), com o **mesmo critério de
  regras**: regras ativas do procedimento canônico; regra com sistema só vale para aquele sistema.
  Entra o que o espelho sabe: nascimento, sexo, CPF e CID (o ESUS não traz CID).
- O pedido chega ao procedimento canônico pelo **rótulo** da origem do mesmo sistema. O espelho guarda
  só o texto do recurso, e a chave é normalizada como no resto da regulação (`ChaveRotulo`).
- Veredito por pedido, em `regulacao_analise_espelho`: `Bloqueado`, `AConferir`, `ComRessalva`, `Apto`,
  `SemRegras` e `SemProcedimento`. `AConferir` significa: há uma regra que bloqueia e depende de
  pergunta ou documento, e só uma pessoa responde isso. Guarda as regras avaliadas com resultado e
  motivo.
- Reanálise **por hash** da entrada (dado do pedido + versão das regras + idade em anos): regra editada
  na tela é pega na passada seguinte, sem gatilho. A idade entra em anos, e o pedido volta à análise no
  aniversário, não todo dia.
- É um **parecer ao lado**. Não muda o pedido, não muda a situação e não escreve nada no sistema
  externo.

## Consequências

- Maricá passa a enxergar, no próprio SMSMais, a fila e os agendamentos que tem em São Gonçalo:
  posição, prioridade, espera, próxima sessão, unidade. A informação chega à ficha e ao robô.
- A análise só é útil para os procedimentos que **têm regra**. As 574 regras em produção (CRECE/REUNI)
  estão todas presas a destinos do SER. Os procedimentos do SG (retina, glaucoma, auditiva…) começam
  como `SemRegras` até alguém cadastrar regras para eles na tela de Regras de elegibilidade.
- O servidor não impõe `BloqueiaEnvio` no envio à fila (achado do levantamento, já existente). Continua
  só no front, e fica anotado aqui como dívida.
- No robô, o SER e o SERNIT usam os agendados só para não dizer "não encontrei". Eles não entram na
  resposta. O ESUS entra. Igualar os três é decisão separada.
- Escrita no ESUS (incluir na fila, observação) fica para quando for mapeada, autorizada por ação e
  com o mesmo `IEscritaExternaGate` do SER/SERNIT.
