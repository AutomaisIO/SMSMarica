# ADR-0051 — Treinamento do robô: a crítica do atendente vira correção, com adversários e simulação

- **Status:** aceito
- **Data:** 04/09/2026
- **Contexto:** [ADR-0050](./0050-motor-robo-messages-api.md) (motor na Messages API),
  [ADR-0011](./0011-modulo-ia-consulta-natural.md) (módulo IA)
- **Documento de apoio:** [`docs/robo/briefing-estrutura.md`](../robo/briefing-estrutura.md)

## Contexto

O robô de atendimento erra, e quem vê o erro primeiro é o atendente humano que está lendo a
conversa. Até aqui, o que ele podia fazer era clicar em "marcar erro" e escrever uma nota: isso
virava uma linha em `robo_erro_resposta` para alguém revisar depois, à mão. Na prática, o caminho
entre "o robô prometeu atendente num sábado" e "o robô parou de prometer atendente no sábado"
passava por um humano abrir a tela de assuntos, entender em qual das seis camadas do prompt aquilo
cabia, escrever a regra e torcer para que ela não brigasse com outra.

Três coisas tornavam isso caro:

1. **A estrutura não é óbvia.** Persona global, persona do assunto, treinos, condições de
   roteamento, comandos habilitados e guardrail se parecem, mas moram em lugares diferentes e têm
   alcances diferentes. Corrigir na camada errada é o erro mais comum — e o mais silencioso.
2. **Regra nova quase nunca é errada sozinha.** Ela erra ao *anular* ou *tornar dúbia* uma regra
   que já estava lá. Quem acabou de escrever a regra é justamente quem não enxerga isso.
3. **"Treinado" não significa "corrigido".** Escrever a regra não prova que o robô mudou de
   comportamento.

## Decisão

A crítica do atendente vira um **item de treinamento** (`robo_treinamento_item`), e o item passa
por um ciclo com dono definido em cada etapa.

### 1. A captura continua na bolha

O botão discreto abaixo de cada resposta do robô, na Central de Atendimento, agora diz **Treinar**
e abre um campo para a crítica. Ele registra a marcação de erro antiga (o relatório por assunto
continua valendo) **e** abre o item, congelando junto: o texto que o robô disse, o assunto
engajado e os 12 turnos anteriores da conversa.

O contexto é **congelado**, não lido ao vivo depois: a conversa segue andando, e analisar a
crítica com mensagens que só chegaram depois levaria o agente a corrigir um erro que o robô não
tinha como não cometer.

### 2. Antes de treinar, pergunta-se o que mais o humano sabe

Clicar em "Treinar" abre um modal pedindo observações. É onde entra o contexto que só o operador
tem — o porquê da regra, o que já foi decidido antes — e é o que evita uma rodada inteira de
pergunta e resposta.

### 3. O agente roda em três fases, no `claude-fable-5-1`

O briefing que ele lê é montado em tempo de execução: o documento de estrutura
(`docs/robo/briefing-estrutura.md`, embarcado no assembly) **mais o estado real do banco** —
configuração global, persona, guardrail, catálogo de comandos e todos os assuntos com seus treinos,
condições e comandos, com os ids reais.

**A alternativa rejeitada foi um "prompt que vai crescendo" mantido à mão.** Um arquivo assim
desincroniza do que o robô executa, e a partir daí o treinamento passa a raciocinar sobre um robô
que não existe. O que cresce são os treinos no banco — que já *são* o modelo treinado.

1. **Proposta** — diagnostica a causa, escolhe a camada e propõe a correção mínima.
2. **Adversários** — três leituras hostis e independentes, em paralelo: *conflito de regras*
   (anula, contradiz, cria ambiguidade ou redundância?), *roteamento e escopo* (a regra está no
   assunto certo? a condição rouba mensagens de outro assunto?) e *dano ao cidadão* (afirmar sem
   ferramenta, vazar dado, opinar sobre saúde, prometer o que não existe). Um adversário que falha
   não derruba o ciclo, mas o juiz é avisado de que a proposta foi menos atacada do que deveria.
3. **Juiz** — com a proposta e os ataques na mão: aplica, abre pendência ou descarta.

### 4. O agente aplica sozinho — e só duas coisas

Ele altera **treino** e **condição de ativação** do assunto, direto, sem esperar aprovação. Toda
alteração guarda o valor anterior e tem **desfazer**; nada é apagado (desativar é `Ativo = false`).

Tudo o mais — persona do assunto, persona global, horário, limiar, ligar/desligar comando — vira
**pendência de regra de negócio**. Guardrail, catálogo de comandos e motor viram **pendência de
alteração de código**, que exige autorização explícita antes de virar trabalho de desenvolvimento.

Pendência **trava** o item: ele para em `AguardandoHumano` e só anda quando alguém responde.
Respondida a última, o item volta para a fila sozinho, e as respostas entram na análise seguinte
como decisões vinculantes.

### 5. Aplicou, tem que simular

Toda alteração aplicada deixa o item em `SimulacaoPendente`, e a simulação de verificação dispara
automática: o caso é reensaiado contra o robô como ele está agora — mesmo prompt, mesmas
ferramentas, sem falar com o cidadão e sem executar comando de escrita — e um juiz diz se a crítica
foi atendida (`Passou` / `Falhou` / `Duvidoso`).

A simulação **não força o assunto**: deixa o classificador escolher de novo. É assim que uma
correção de roteamento aparece.

O botão "Simular situação" fica disponível para sempre, inclusive em item concluído — e desfazer
uma alteração devolve o item para `SimulacaoPendente`, porque o veredito anterior passou a valer
para um robô que não existe mais.

### 6. A análise roda fora da requisição

O clique só marca `Analisando`; o `RoboTreinamentoWorker` processa **um item por vez** (dois itens
mexendo no mesmo assunto ao mesmo tempo produziriam regras que nenhum adversário chegou a ver), com
teto de 3 tentativas antes de parar em `Falhou`.

## Consequências

- O caminho entre ver o erro e corrigir o robô encurta para dois cliques e um texto, e passa a ser
  auditável: o rastro completo (proposta, cada ataque, veredito do juiz) fica em `analise_json`.
- Fica registrado **por que** cada regra entrou, com justificativa legível seis meses depois.
- Custa mais por item (Fable, cinco chamadas ao modelo) e custa tempo: um ciclo leva minutos.
  O volume é baixo — é análise de regra, não atendimento.
- Um agente que aplica sozinho pode aplicar errado. As contenções são: escopo de duas coisas,
  adversários antes, desfazer sempre, e simulação obrigatória depois.
- O treinamento **não** é um caminho paralelo para reescrever o robô. Persona global e guardrail
  continuam sendo decisão humana e código.
- `docs/robo/briefing-estrutura.md` passa a ser código de produção: ele é embarcado no assembly do
  `SMSMais.Core` e vai no prompt. Documento errado = agente treinando errado.

## Alternativas consideradas

- **Aplicar só com aprovação humana em tudo.** Rejeitada: recria o gargalo que o recurso existe
  para desfazer. O desfazer + a simulação cobrem o risco de aplicar treino e condição.
- **Uma passagem só (sem adversários).** Rejeitada: o modo de falha dominante não é a regra errada,
  é a regra que conflita com outra — e é exatamente isso que uma passagem única não vê.
- **Deixar o agente editar qualquer coisa do robô.** Rejeitada: persona global e guardrail valem
  para todos os assuntos, e o guardrail nasceu de incidentes reais de produção. Mudança ali é
  decisão de gente.
