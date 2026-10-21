# ADR-0072 — Aviso dos agendamentos do SER, do SERNIT e do ESUS pelo WhatsApp (âncora própria, nome do local)

**Status:** aceito · **Data:** 2026-10-10 · em produção desde 10/10/2026 · revisto em 11/10/2026 (sem endereço)
**Relacionado:** [ADR-0042](./0042-ser-segunda-fonte-de-regulacao.md) (o espelho do SER não é `solicitacao`) ·
[ADR-0063](./0063-esus-sao-goncalo-e-analise-de-regras-dos-espelhos.md) (ESUS de São Gonçalo) ·
[ADR-0057](./0057-destinatario-correto-e-contato-negado.md) (destinatário correto) ·
[ADR-0059](./0059-atendimento-humano-de-confirmacao.md) (Confirmações) ·
[ADR-0065](./0065-medicos-do-ser-espelho-separado.md) (ligação só por confirmação humana)

## Contexto

Em 10/10/2026 uma unidade perguntou se a paciente de um pedido do SER, agendado para 19/10 num hospital do
Rio de Janeiro, tinha sido avisada. Não tinha: **só o agendamento do SISREG avisava o paciente.** A
`comunicacao_paciente` era ancorada na espinha `solicitacao`, e o único gatilho de aviso de agendamento
era a importação do SISREG. Os espelhos do SER, do SERNIT e do ESUS SG (ADR-0042/0063) nunca viram
`solicitacao`, e a conciliação da regulação não avisava ninguém. O paciente só sabia da data se alguém da
equipe mandasse a mensagem à mão.

Medido em produção no mesmo dia: 128 agendamentos futuros no SER, 17 no SERNIT e 20 no ESUS. Desses 165,
só **1** estava ligado a uma solicitação da regulação; os demais existem só no espelho.

Pedido do dono: avisar direto quando agendar, com a **mesma conversa** do SISREG (primeira mensagem
anônima, "Quero mais informações", CPF e nascimento quando o número não é confirmado), mas a confirmação
completa **com o local e o procedimento** e **sem mandar buscar a guia no posto**, porque o atendimento
é fora do município.

## Decisão

1. **Âncora própria: `agendamento_externo`**, uma linha por pedido do espelho × data. Guarda o sistema, o
   pedido no espelho, o paciente, o procedimento, o dia, a hora, o local (como o sistema escreve) e a
   **resposta do paciente** (mesma semântica de `Solicitacao.StatusConfirmacao`). A `comunicacao_paciente`
   ganha `agendamento_externo_id`, exclusivo com `solicitacao_id`, e o mesmo índice único por finalidade.
   - **Não** se promove o pedido a `solicitacao`, que o ADR-0042 deixava em aberto. A espinha exige
     unidade executante do município e alimenta worklist, recepção, gate do PACS e indicadores; um
     atendimento no Rio de Janeiro não pertence a nenhum deles. O aviso só precisa do que o paciente
     precisa saber.
2. **Varredura dos espelhos a cada 10 minutos** (`AgendamentoExternoWorker`). Os gatilhos dos espelhos
   (`ser_gatilho` & cia.) não servem: o `ProcessadoEm` deles significa "visto por uma pessoa" na tela de
   Notificações. O espelho já é a verdade do que está agendado, e lê-lo inteiro custa milhares de linhas,
   não milhões. A varredura é idempotente e se corrige sozinha.
   - Só avisa **de amanhã em diante**.
   - Data nova no espelho: o agendamento vira `Reagendado`, o aviso que ainda não saiu vira
     `Dispensada`, e nasce um agendamento novo com aviso novo.
   - Saiu de "Agendada": `Desmarcado`, e o que não saiu não sai mais.
   - Voltou a "Agendada" com o mesmo dia: a mesma linha volta a valer, sem aviso duplicado.
3. **De onde vem cada dado.**
   - SER: o dia vem do texto da grade. A hora vem do evento "Agendar" do histórico ("Data do agendamento:
     19/10/2026 08:30"). Nos 128 agendamentos medidos, todos tinham o evento com a mesma data. O aviso
     espera o histórico por até 24 horas; depois disso, sai só com o dia, e a mensagem diz que o horário
     não veio.
   - SERNIT: dia, hora e local vêm juntos no texto.
   - ESUS: a data vem da coluna, a hora do texto, e o local é a unidade executora sem o CNES colado.
4. **A mesma conversa do SISREG.**
   - A primeira mensagem é o mesmo modelo aprovado, sem dado nenhum. Vai **até para número confirmado**:
     o "Quero mais informações" de quem já provou o número entrega os dados sem perguntar CPF.
   - Os dados saem **só como mensagem de sessão**, depois que a pessoa escreveu: não há modelo aprovado
     com o agendamento externo, e o da regulação manda retirar a guia.
   - Os botões têm prefixos próprios (`confext_*`, `cancext_*`), porque o id é de `agendamento_externo`.
5. **O local vai pelo nome, sem endereço** (revisto em 11/10/2026). A primeira versão ligava o nome do
   local, por uma pessoa, a uma Unidade de Atendimento (`unidade_atendimento_nome_externo`) para mandar o
   endereço. No dia seguinte o dono decidiu que o nome do hospital basta: a ligação e a tela saíram, e a
   mensagem leva o local como o sistema o escreve. A tabela ficou, vazia e sem uso. Tirá-la exigiria uma
   migration nova e mexer na publicação da replicação; se o endereço voltar a ser pedido, a ligação é por
   ela. A régua, nesse caso, continua a mesma do ADR-0065: a máquina sugere e quem liga é uma pessoa.
   "RIO IMAGEM LAGOS (CASIMIRO DE ABREU)" casa por nome com o Rio Imagem do Rio de Janeiro.
6. **A resposta não volta ao sistema de destino.** "Não poderei ir" fica no `agendamento_externo` e
   aparece no quadro *Contato com o paciente* (regulação e detalhe do pedido de cada sistema) e na
   Mensageria. Nada é escrito no SER, no SERNIT ou no ESUS.
7. **Chave na Mensageria → Regras** (`aviso_agendamento_externo_habilitado`). **Nasce ligada**, por pedido
   do dono. Desligada, a varredura não cria agendamentos novos e a fila segura o que já estava nela,
   salvo a resposta a quem acabou de se identificar.
8. Paciente com **óbito** na ficha não é avisado (`Dispensada`).

## Consequências

- O primeiro deploy põe na fila os agendamentos futuros que já existem (cerca de 165 em 10/10/2026). Eles
  saem no horário de envio, dentro da vazão configurada.
- O SER manda o próprio WhatsApp ao paciente ("Paciente confirmou presença no agendamento via WhatsApp"
  aparece na trilha). Com isso, o paciente pode receber as duas mensagens. A nossa fica registrada para a
  unidade, com a resposta do paciente.
- **Fica de fora nesta fase:**
  - o lembrete às vésperas;
  - a régua de reforço;
  - o aviso de cancelamento ou remarcação de um agendamento que já chegou ao paciente;
  - a aba de Confirmações (que é da espinha);
  - o robô consultar esses agendamentos.
- Banco: tabela nova `agendamento_externo` e `unidade_atendimento_nome_externo`; colunas novas em
  `comunicacao_paciente` e `confirmacao_configuracao`. Com a replicação lógica para a EVEO ligada, a DDL
  das colunas novas em tabelas publicadas precisa estar no destino **antes** do deploy.

## Fase 2 (21/10/2026): lembrete e aviso de cancelamento, por regulação

Pedido do dono: "enviar para o SER, SERNIT e ESUS mensagem antes, habilitar dentro de configurações da
própria regulação, com quantos dias antes" e "enviar informação de cancelamento igual ao SISREG, também
como opção na configuração".

1. **As chaves moram na tela de Configuração de cada regulação** (bloco *Avisos ao paciente*), numa tabela
   própria, `aviso_regulacao_configuracao` (uma linha por sistema: lembrete ligado, dias antes, aviso de
   cancelamento ligado e quando foi ligado). Não no `parametros_json` da credencial, porque salvar a
   credencial do SER e do SERNIT regrava aquele JSON inteiro e apagaria a chave em silêncio. Sem linha,
   tudo desligado; nada de seed em migration. O aviso de que foi **agendado** segue com a chave única da
   Mensageria (item 7). A tela de Configuração do **SISREG** ganhou o mesmo bloco, editando as chaves que
   ele já tinha em `confirmacao_configuracao`: um valor só, visto de dois lugares.
2. **Lembrete:** as regras do SISREG. Quem confirmou e tem número provado recebe `agendamento_proximo`, com
   dia e hora. Sem hora, ou com número não provado, vai a primeira mensagem de novo, e os dados saem na
   conversa depois da identificação. Quem avisou que não vai não recebe, e quem recebeu o aviso há menos de
   `LembreteIntervaloMinimoDias` também não. Não sai enquanto o aviso do agendamento ainda está na fila. As
   respostas "Sim! Está confirmado!" e "Não poderei ir." voltam como texto e caem no `agendamento_externo`
   (só a partir do lembrete que saiu com os dados, para não revelar o agendamento a número não provado).
3. **Aviso de cancelamento:** o modelo do SISREG (`agendamento_cancelado_anonimo`), anônimo para número
   não provado. Sai quando o agendamento é **desmarcado com a data pela frente e o sistema diz por quê**:
   `Cancelada` no SER e no SERNIT, ou **de volta à fila** nos três. Nesse segundo caso, a frase diz
   "foi desmarcado e o pedido voltou para a fila da regulação". O motivo fica em
   `agendamento_externo.motivo_desmarque`. **Pendência não avisa**: o SER às vezes pendencia e reagenda o
   mesmo dia. **Saída da fila do ESUS não avisa**: o ESUS não diz por quê, e "saiu da fila" não é
   cancelado (ADR-0063).
4. **Carência de 30 minutos** antes de o cancelamento poder sair. Se o pedido voltar a "Agendada" com o
   **mesmo dia** antes disso, o aviso é dispensado. Isso acontece, por exemplo, com uma leitura no meio da
   rodada do ESUS, que lê a fila antes dos agendados. Se o aviso já tinha saído e o pedido voltou com o
   mesmo dia, nasce um agendamento novo e o paciente recebe o aviso do agendamento outra vez, em vez de
   ficar com "foi cancelado" como última palavra.
5. **Ligar vale daqui para frente**, como no SISREG: o envio recusa o que entrou na fila antes da última
   vez em que a chave foi ligada.
6. **Remarcação** continua como no item 4 da fase 1: a data nova gera um aviso novo. Isso inclui o pedido
   que o paciente não conseguiu fazer e a regulação devolveu à fila. Conferido em produção em 21/10/2026:
   no SER 7023054, marcado para 14/10, devolvido e remarcado para 21/10, o aviso novo saiu em 16/10.

**Consequências:** as duas chaves nascem desligadas em cada regulação. Uma coluna nova em
`agendamento_externo` e uma tabela nova; desde a virada para a EVEO (14/10/2026) não há replicação a
preparar. Continua de fora a régua de reforço desses sistemas.
