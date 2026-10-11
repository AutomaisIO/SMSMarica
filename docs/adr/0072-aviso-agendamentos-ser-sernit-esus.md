# ADR-0072 — Aviso dos agendamentos do SER, do SERNIT e do ESUS pelo WhatsApp (âncora própria, endereço ligado por pessoa)

**Status:** aceito · **Data:** 2026-10-10 · implementado em 10/10/2026
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
completa **com o endereço e o procedimento** e **sem mandar buscar a guia no posto**, porque o atendimento
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
5. **Endereço ligado por pessoa** (`unidade_atendimento_nome_externo`). Os sistemas mandam só o nome do
   local. O endereço vem da Unidade de Atendimento (o cadastro do transporte, que já tem os hospitais de
   referência com endereço) ligada àquele nome, em Unidades de Atendimento → Locais dos agendamentos.
   - A máquina **sugere**, nunca liga: "RIO IMAGEM LAGOS (CASIMIRO DE ABREU)" casa por nome com o Rio
     Imagem do Rio de Janeiro. É a mesma régua do ADR-0065.
   - Local sem ligação: o aviso sai só com o nome.
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
  aparece na trilha). Com isso, o paciente pode receber as duas mensagens. A nossa traz o endereço e fica
  registrada para a unidade.
- **Fica de fora nesta fase:**
  - o lembrete às vésperas;
  - a régua de reforço;
  - o aviso de cancelamento ou remarcação de um agendamento que já chegou ao paciente;
  - a aba de Confirmações (que é da espinha);
  - o robô consultar esses agendamentos.
- Cada local novo do SER sem ligação faz avisos saírem sem endereço até alguém ligar. A lista mostra
  primeiro os que estão saindo sem endereço.
- Banco: tabela nova `agendamento_externo` e `unidade_atendimento_nome_externo`; colunas novas em
  `comunicacao_paciente` e `confirmacao_configuracao`. Com a replicação lógica para a EVEO ligada, a DDL
  das colunas novas em tabelas publicadas precisa estar no destino **antes** do deploy.
