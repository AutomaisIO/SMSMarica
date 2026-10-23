# ADR-0083 — Aviso automático da remarcação feita no SISREG: quem conhecia a data antiga recebe "Mudança de data" (dois modelos, com foto), e a resposta antiga deixa de valer

- **Status:** Aceito
- **Data:** 2026-10-23
- **Contexto módulo:** Confirmações / Mensageria ([ADR-0059](./0059-atendimento-humano-de-confirmacao.md)) e a fila
  de Alterações da agenda (sincronização do SISREG)
- **Relacionado:** [ADR-0072](./0072-aviso-agendamentos-ser-sernit-esus.md) (o SER/SERNIT/ESUS já avisa a
  troca de dia sozinho), [ADR-0059](./0059-atendimento-humano-de-confirmacao.md) (humano entrou, o automático
  para)

## Contexto

A sincronização diária relê a agenda de cada unidade no SISREG e, desde 04/09/2026, compara o que
lê com o que já está aqui: a data nova é aplicada na solicitação e a mudança vira uma linha em
`sisreg_alteracao_agenda`. Parava aí, de propósito — o código dizia "não avisa ninguém aqui; disparar
WhatsApp a cada remarcação seria decidir pelo regulador, sem saber com que frequência acontece". Em
20/09 a decisão foi medir antes (`docs/operacao/lote-confirmacao-cdt-2026-09.md` § Em observação).

Consequências que se acumularam:

- o paciente que recebeu a data antiga ficava com ela; quem tinha **confirmado** seguia "Confirmado"
  para um horário que não existia mais;
- o botão **Avisar paciente** da fila, o único caminho, **nunca funcionou**: chamava o envio manual de
  exame/laudo, que recusa a confirmação desde 16/07 (`comunicacao.finalidade_invalida`);
- a remarcação não aparecia na linha do tempo da solicitação — quem abria via só a data nova.

O SER/SERNIT/ESUS, ao contrário, já avisava a troca de dia sozinho (ADR-0072). Pedido do Bernardo
(23/10/2026): avisar automaticamente ao detectar a alteração e marcar a mudança de data no histórico
da solicitação.

## Decisão

1. **A remarcação do SISREG avisa o paciente sozinha**, com uma chave própria em Mensageria → Regras
   (`confirmacao_configuracao.aviso_remarcacao_habilitado`), que **nasce ligada** e vale **daqui para
   frente** (`aviso_remarcacao_ligado_em`; a migration carimba a virada para não soltar o estoque de
   remarcações antigas da fila). Respeita as mesmas chaves por unidade e por procedimento da confirmação.
2. **Quem já conhecia a data antiga recebe um aviso próprio de "Mudança de data"**, com foto própria
   (a Maria com leve constrangimento, "MUDANÇA DE DATA! CONFIRA A NOVA DATA."), em **dois modelos** — pedido do Bernardo no mesmo
   dia: a confirmação comum leria como se nada tivesse mudado. "Conhecia" = recebeu a mensagem com a data,
   ou tinha confirmado por qualquer canal; a data anterior é a que ele tinha na mão (com duas remarcações
   antes de um aviso, a primeira). Fica em `comunicacao_paciente.remarcada_de`, e é ela que escolhe o
   modelo na hora do envio:
   - **número provado** (verificado, ou que acabou de se identificar) → `agendamento_data_alterada`: a
     data anterior e a nova, com **"Confirmar nova data"** (o link de sempre; abrir = confirmar) e **"Não
     poderei ir"**. Sem "Falar com atendente" (decisão do Bernardo);
   - **número não provado** → `agendamento_data_alterada_anonimo`: só que a data mudou, sem dado nenhum
     (ADR-0057), com **"Quero mais informações"** e **"Não sou essa pessoa"** — a mesma conversa de
     identificação da primeira mensagem. Identificado, recebe as duas datas na conversa, com "Sim,
     confirmo" e "Não poderei ir".
   Conforme o que a mensagem anterior fez: já levou a data antiga → é rearmada e sai de novo, como
   mudança; ainda não tinha levado → sai com a data nova (como mudança, se ele sabia a data por ter
   confirmado); nunca houve mensagem → a primeira já sai com a data nova.
   **Sem os dois modelos aprovados na Meta, a mensagem espera na fila** (de hora em hora olha de novo; não
   gasta tentativa) e a remarcação fica visível para a equipe com esse motivo. A confirmação comum nunca
   sai no lugar — nem o aviso de cancelamento, que chegou a ser cogitado por ter frase livre e foi
   descartado: a foto dele diz "NOTIFICAÇÃO DE CANCELAMENTO".
3. **A resposta anterior deixa de valer.** "Confirmado" era sobre a data antiga: volta para "sem
   resposta" — é isso que libera a confirmação a sair (o envio barra quem "já respondeu") e que devolve a
   ficha para **Não confirmados** em Confirmações. O **"não vou"** não é apagado pelo automático (pode ser
   justamente a causa da remarcação); só uma pessoa o reabre, pelo botão.
4. **O que o automático não avisa fica para a equipe, com o motivo**, na fila de Alterações da agenda:
   o paciente tinha dito que não ia; remarcado para hoje; unidade ou procedimento sem aviso por
   WhatsApp; em atendimento humano (ADR-0059); a mensagem antiga não chegou; a data nova não é a da
   solicitação (outro procedimento do mesmo pedido). Nesses casos, quem tinha confirmado também volta a
   "sem resposta", menos no último.
5. **A decisão não é da importação, é de um passo à parte** (`AvisoRemarcacaoService` + worker a cada
   10 min). A importação vê uma linha por vez; o passo à parte espera a **leitura da unidade terminar**
   e olha o conjunto antes de avisar qualquer um:
   - **freio de volume:** 20 ou mais remarcações da unidade em 24 h **e** mais de 10% da agenda futura
     dela → nenhuma sai, todas ficam como "Retida" e chega aviso no celular
     (`sisreg.remarcacao_retida`). Nada na ficha do paciente é mexido;
   - **vai-e-volta:** data que volta para uma que já tinha sido trocada no último mês fica retida.
   Avisar um paciente de uma data errada é a única mensagem sem conserto, e leitura errada do SISREG já
   produziu 1.378 linhas falsas num dia (06/09/2026).
6. **O lembrete das vésperas acompanha a data nova**: o da data antiga é dispensado com carimbo e o
   lembrete o rearma quando chegar a hora da nova.
7. **"Avisar paciente" passa a funcionar** e usa o mesmo caminho, sem as travas da máquina (é uma
   pessoa decidindo): reabre inclusive o "não vou", vale para unidade sem aviso automático, e sai na hora
   dentro do horário de envio — como mudança de data quando a linha é uma remarcação e o paciente conhecia
   a data antiga. Não revoga mais os links antigos — eles mostram a data de agora, e revogar
   derrubava a sessão do paciente certo.
8. **Toda mudança do SISREG entra na linha do tempo da solicitação** (remarcação, troca de profissional
   ou de procedimento, sumiço), lida direto de `sisreg_alteracao_agenda`, com o que foi feito com o
   paciente (`nota_aviso`). Vale também para as alterações registradas desde 04/09.

## Os modelos na Meta

**Criados pela plataforma**, no painel do Automais.Zap (Templates → criar), que ganhou junto foto no
cabeçalho (uma das Artes, enviada à Meta como a imagem de exemplo que a aprovação exige, pela mesma Upload
API da foto de perfil) e botões (resposta rápida e link, inclusive o link que muda a cada envio). Criado
com foto, o modelo já fica com a arte escolhida para os envios. Arte: `mudanca-de-data.jpg` (1125×628, a
Maria sobre o vermelho, logo nova), feita a partir da `agendamento-proximo` trocando o texto e a expressão:
**leve constrangimento, não sorriso** — é uma notícia que incomoda (pedido do Bernardo).

Os dois: categoria **Utilidade**, idioma **pt_BR**, foto `mudanca-de-data.jpg`. Nomes configuráveis em
`ComunicacaoPaciente:TemplateMudancaDeData` e `TemplateMudancaDeDataAnonimo`.

**`agendamento_data_alterada`** (número provado)

```
Olá, *{{1}}*. Esse é o canal oficial do *Alô Maricá* da Secretaria Municipal de Saúde.

A data {{2}} *mudou*.

Data anterior: {{3}}
*Nova data: {{4}}*

Por favor, confirme se poderá comparecer na nova data.
```

Exemplos: `Sra. Maria | do seu exame de MAMOGRAFIA BILATERAL | 10/11/2026 às 08:00h | 20/11/2026 às
09:30h`. Botões, nesta ordem (a ordem é o índice no envio):

```
Link: Confirmar nova data | https://app.smsmarica.online/entrar/{{1}} | https://app.smsmarica.online/entrar/0f8b1c2e-7a41-4c55-9d3e-2b6f0a9c1d42
Resposta: Não poderei ir
```

O envio põe o token no link e o payload `confirma:` no "Não poderei ir" (o mesmo tratador da confirmação).
`{{2}}` já chega flexionado ("do seu exame de …" / "da sua consulta de …"): sem palavra de gênero fixo no
texto, a lição do "foi agendada" do `confirmacao_regulacao`.

**`agendamento_data_alterada_anonimo`** (número não provado)

```
Olá, *{{1}}*. Esse é o canal oficial do *Alô Maricá* da Secretaria Municipal de Saúde.

Comunicamos que a data {{2}} *mudou*. Para ver a nova data, toque em *Quero mais informações*.
```

Exemplos: `Sra. Maria | do seu exame`. Botões:

```
Resposta: Quero mais informações
Resposta: Não sou essa pessoa
```

Os textos dos botões são os que a máquina de identificação reconhece (a Meta devolve o texto do botão).
A tela Mensageria → Testar modelo já sabe as variáveis dos dois.

## Consequências

- O paciente remarcado recebe a data certa sem depender de alguém olhar a fila; a fila guarda só o que
  precisa de gente, e diz por quê.
- Até os modelos serem aprovados, quem conhecia a data antiga não recebe nada sozinho — fica na fila de
  Alterações da agenda com o motivo, e a mensagem sai assim que a Meta aprovar. Se a aprovação demorar
  além do dia do atendimento, a mensagem cai como "sem data futura" no envio, como qualquer outra.
- Quem recebe o anônimo e não toca em "Quero mais informações" sabe que a data mudou, mas não para
  quando — como na primeira mensagem. A régua de reforço, quando ligada, insiste como nela.
- O freio pode segurar remarcação legítima em unidade pequena (agenda inteira mudada). É o lado seguro:
  a equipe confere e avisa pelo botão.
- A remarcação de **outro procedimento** de um pedido com vários não é avisada sozinha: a confirmação
  fala do principal.
- Mudança só de hora no mesmo dia **é** avisada (o comparador já não conta "ganhou a hora que não
  tinha" como remarcação). No SER/SERNIT/ESUS, mudança só de hora continua silenciosa (ADR-0072).

## Implementação

`AvisoRemarcacaoService` e `AvisoRemarcacaoWorker` (`SMSMais.Core/Notificacoes/Confirmacoes/`), decisão
pura em `AvisoRemarcacaoService.Decidir`, data que o paciente conhecia em `DataQueOPacienteConheciaAsync`;
no envio, `ComunicacaoPacienteService.MudancaDeData` (o modelo provado), `EnviarPrimeiraMensagemAsync`
com `mudancaDeData` (o anônimo), a espera pelo modelo aprovado em `EnviarAsync` e a linha "Mudança de
data" nos detalhes da conversa; `LembreteAgendamentoService` rearma o lembrete carimbado;
`AlteracoesAgendaService.ComunicarAsync` usa `PrepararAvisoManualAsync`; `SolicitacaoHistoricoService`
junta `sisreg_alteracao_agenda` aos eventos (`HistoricoEventoDto.Detalhe`). Migration
`AvisoRemarcacaoSisreg`: `confirmacao_configuracao.aviso_remarcacao_habilitado` /
`aviso_remarcacao_ligado_em`, `sisreg_alteracao_agenda.aviso_automatico_em` / `nota_aviso` e
`comunicacao_paciente.remarcada_de`. Front:
Mensageria → Regras (bloco "Remarcações feitas no SISREG"), coluna "Aviso ao paciente" em Alterações de
Agenda, rótulos na linha do tempo do detalhe da solicitação; artigo novo do manual `alteracoes-agenda`.
Testes: `AvisoRemarcacaoDecisaoTests` (decisão), `AvisoRemarcacaoTests` (banco) e
`AvisoRemarcacaoEnvioTests` (os dois modelos e a espera no envio). No Automais.Zap: `NovoTemplate` com
`ImagemCabecalho` e `Botoes`, `GraphMetaClient.SubirParaUploadApiAsync` (agora também da foto de perfil) e
o formulário de criar em `Pages/Admin/Templates` (`LerBotoes`), testados em `GraphMetaClientTests`.
