# Robô de atendimento — análise por simulação sobre cenário real (01/09/2026)

> Primeira edição da análise periódica do robô. O método está registrado na skill
> [`analisar-robo-cenarios-reais`](../../.claude/skills/analisar-robo-cenarios-reais/SKILL.md) para
> ser repetido — **sempre sobre tráfego real**, nunca sobre casos inventados.

## Contexto

Na noite de 29/08 o robô (motor antigo) produziu uma sequência de falhas graves em produção —
desmentiu um agendamento que a própria atendente havia anunciado (caso VALDERI), pediu CPF dígito a
dígito, "confirmou" identidade sem os dados completos, vazou o nome de um cidadão sem identidade
conferida, opinou sobre cirurgia de hérnia e inventou lista de documentos — e foi desligado.

Depois das correções (motor Messages API, ferramentas de consulta, conjunto Base, assunto padrão,
guardrail), a pergunta era: **o que ainda quebraria se religássemos?** Em vez de adivinhar, a
resposta veio de três medições sobre o tráfego real.

## Método (resumo)

1. **Leitura determinística do código** que monta o prompt e o payload.
2. **Réplica exata do classificador** sobre 2.500 mensagens reais de cidadãos (30 dias).
3. **12 cenas reais remontadas** — as conversas de 29/08 e padrões do corpus — cada uma com o
   prompt de sistema, as ferramentas e o histórico **exatamente** como produção geraria, simuladas
   no **próprio Haiku 4.5** (o modelo de produção) e julgadas por revisor adversarial com checklist
   de 11 itens. Cada problema classificado por origem: `modelo`, `prompt`, `codigo` ou
   `classificador`.

## O que o material atual já previne (confirmado nas simulações)

Nenhum dos desastres de 29/08 se repetiu com o material de 01/09:

- VALDERI seria confirmado com gate de identidade correto — **sem** "não há agendamento";
- o relato de hérnia recebe a frase padrão + oferta de CEP para urgência, **zero** pergunta de
  sintoma, zero opinião;
- ADOLPHO não recebe lista de documentos inventada nem "falha de identidade" alucinada;
- pedido de marcação é recusado sem inventar central/telefone;
- endereço não é inventado; troca de telefone não vira promessa de "já atualizei";
- dígitos órfãos não viram "verificação" de mentira.

## Erros antecipados → corrigidos (deploy `b31e1a7` + conteúdo no banco, robô desligado)

| # | Erro antecipado | Origem | Correção |
|---|---|---|---|
| 1 | O **anúncio da Secretaria era descartado do payload** (a API exige começar com turno `user`; o código removia os turnos iniciais do lado `assistant` — e em thread de notificação a Secretaria fala primeiro). Efeitos observados: *"eu não sei seu nome"* logo após o template chamar a pessoa pelo nome; *"não localizei sua consulta"* para quem tinha o anúncio na tela. A regra "nunca desminta a Secretaria" não funciona se o modelo não vê o que a Secretaria disse. | código | turno-abertura sintético `(início da conversa)` preserva o contexto |
| 2 | **Sábado contava como expediente**: a regra só olhava hora do dia. Na simulação (caso real de sábado 11h33) o robô prometeu *"vou conectar você agora"* — o cidadão esperaria em silêncio até segunda. | código | `robo_configuracao.dias_semana_atendimento_humano` (bitmask, default seg–sex) + seletor na tela |
| 3 | **Laço bot-a-bot**: 17 auto-respostas de outros WhatsApp Business em 14 dias ("X agradece seu contato. Como podemos ajudar?"). O assunto padrão *manda* responder com pergunta — que dispara nova auto-resposta. | código | detecção de auto-resposta **antes** do modelo; tarefa vira HandOff com nota, sem resposta |
| 4 | **Classificador por substring**: "oi" casava com "f**oi**" (41 capturas falsas/30d); "ciente" com "pa**ciente**" — inclusive um relato de **sangramento com câncer** que caiu em "Confirmação de presença". | código | `PalavraChave` casa com borda de palavra; `Frase` continua substring (válvula de escape) |
| 5 | O modelo **inventou** *"você escreveu no sábado"* — o prompt não informava nem o dia. | código | data de hoje no prompt (granularidade de dia, para não estourar o cache) |
| 6 | Descrição da ferramenta de exame ainda pedia **3 dígitos** do CPF. | prompt | 4, todos de uma vez |
| 7 | `confirmar_presenca` revelava procedimento/data **junto** com a pergunta "é você?" — se os dígitos fossem de outra pessoa, o dado já teria vazado. | prompt | confirmar o NOME sem revelar nada; agendamento só após `confirmado=true` |
| 8 | O retorno "não achou agendamento" mandava *"encaminhe para um atendente confirmar"* — promessa de atendente às 21h de sábado. | prompt | texto com consciência de horário + "agendamento já anunciado na conversa VALE" |
| 9 | **Saudações engolia mensagens com conteúdo** (*"Boa tarde Informo que estou mudando de contato…"*, *"Bom dia... pode marcar para outro dia"*): saudação como substring casava antes de qualquer assunto real. | banco | condição virou **uma regex de casamento integral** (saudação pura). Efeito medido: 272 mensagens com conteúdo real escapam para o assunto certo; 176 "Ok" puros entram |
| 10 | "hérnia", "câncer", "sangue", "tumor", "nódulo", "caroço" **não eram** condição do assunto Sintoma — o relato real de hérnia caiu no assunto padrão. | banco | +6 palavras-chave |
| 11 | Desabafo (4 anos de espera, "vcs não me atenderão") recebia de volta uma pergunta fria. | banco | treino de acolhimento no assunto padrão (acolher, não decidir pela pessoa, não inventar regra, orientar o posto antes de desistir, encaminhar) |
| 12 | Guardrail: handoff fora do horário era ambíguo (regra 0 mandava encaminhar; bloco fora-do-horário proibia prometer atendente — o modelo desligava o handoff). | prompt | handoff **vale** fora do horário (põe na fila da equipe); só não se **promete** atendente agora. + regra 9b: como responder "como sabe meu nome?" sem negar nem expor mecanismo |

Suíte de testes: 986/987 (5 novos de expediente com dia da semana). Migration
`DiasSemanaAtendimentoHumano` aplicada e conferida em produção.

## Números do classificador (30 dias, 2.500 mensagens)

| Assunto | Antes | Depois dos fixes |
|---|---|---|
| (padrão / sem assunto) | 36% | 36% |
| Falar c/ atendente | 24% | 24% (583 são o clique literal do botão) |
| Saudações | 20% | 20% (composição trocou: −272 com conteúdo real, +176 "Ok" puros) |
| Confirmação de presença | 13% | 13% |
| Auto-resposta (curto-circuito) | — | ~1% (29 mensagens que nem chegam ao modelo) |
| Sintoma/urgência | 23 msgs | 29 msgs (+hérnia/câncer/sangue) |

O assunto padrão sem condições continua sendo o **termômetro**: a contagem dele no relatório de
consumo é a taxa de erro do classificador.

## Decisões em aberto (não aplicadas)

1. **Assunto "Marcar/remarcar" dedicado** — agora que as saudações soltaram essas mensagens, um
   assunto com condições (`marcar`, `agendar`, `remarcar`) e instrução dura de não-promessa
   atenderia melhor que o padrão.
2. **"Falar com um atendente" = 583 cliques de botão/30d indo ao modelo** — dava para responder
   deterministicamente (sem IA, sem custo) e só encaminhar.
3. **Debounce de rajada** — o robô respondeu 3× a 3 mensagens enviadas no mesmo minuto.
4. **Mecanismo duplo de hand-off** (ferramenta `encaminhar_para_humano` × campo `handoff` do
   `responder_cidadao`) — funciona, mas é redundante; unificar um dia.

## Ressalvas do método

- A simulação roda o Haiku num harness de agente — muito próximo, não idêntico ao loop de produção
  (`tool_choice=any` real vs. role-play).
- Cada cena rodou **uma vez**; o modelo varia entre execuções. Falha não vista ≠ falha impossível.
- O acerto numa simulação pode ser sorte de amostragem — na cena da auto-resposta, o modelo acertou
  **desobedecendo** a instrução; a proteção verdadeira tinha de ser código, e virou.
