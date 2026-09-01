# Robô de atendimento — aprendizados (log cumulativo)

> Cada entrada nasce de um caso REAL, nomeia a causa (`código` / `prompt` / `treino` / `classificador`
> / `modelo`) e aponta a correção. Manter este log é parte do processo: análise periódica sobre
> cenário real (skill `analisar-robo-cenarios-reais`), correção estrutural, registro aqui.

## 02/09/2026 — Botão "Falar com um atendente" não é conversa (AURORA / VALDIRENE)

**O caso.** Às 22h53, VALDIRENE clicou o botão do template e o robô respondeu *"Claro! Estou
transferindo você para um atendente humano. Aguarde um momento."* — às onze da noite, com ninguém
no atendimento. No **mesmo minuto** (22h56), AURORA clicou o mesmo botão e recebeu a resposta
certa. Mesmo prompt, mesmo modelo, dois resultados opostos.

**Causa:** `código` (mandávamos um CLIQUE DE BOTÃO para um LLM decidir) + `modelo` (variância do
Haiku sob instruções em tensão) + `treino` (o assunto mandava "use encaminhar_para_humano se
insistir" sem ressalva de horário).

**Regra do produto (definida pelo usuário):**
- **Dentro do horário:** botão = pedido direto de humano → atendimento humano IMEDIATO. O robô fica
  mudo; a conversa está na fila.
- **Fora do horário:** o ROBÔ assume o atendimento — abertura determinística oferecendo ajuda, SEM
  falar de horário.
- **Só se a pessoa insistir em humano DURANTE o atendimento do robô** é que se declara o
  fora-do-horário (sem prometer encaminhamento, retorno nem "registro da solicitação").

**Correção:** clique do botão (payload `atendente:` ou o texto literal) tratado deterministicamente
no handler — nunca mais passa por modelo. Treinos do assunto reescritos com a regra de horário.

**Medição (10 execuções por modelo, mesmo turno crítico, prompt real):** Haiku prometeu o que não
podia em **2/10** ("vou encaminhar sua solicitação… você será contatado"); Sonnet: **0/10**, com
fraseado consistente. É o dado para a decisão de modelo.

## 02/09/2026 — Só se pede dado quando HÁ informação para entregar (SIMONE)

**O caso.** *"Já fiz há muito tempo!!! Estou esperando o ultrassom complementar"* — o robô coletou
os 4 dígitos do CPF + mês/ano, conferiu identidade… para no fim dizer que não tinha nada sobre o
ultrassom. Burocracia sem entrega.

**Causa:** `código` (a ferramenta só funcionava com identidade — não havia como checar antes).

**Regra:** antes de pedir QUALQUER dado, verificar se existe o que informar. Existindo → identidade
→ informa. Não existindo → não pede nada: *"assim que houver informação sobre esse exame, a
Secretaria entra em contato por aqui"* e ENCERRA — sem nunca afirmar que o exame não foi/será
marcado.

**Correção:** `consultar_agendamentos` em DUAS FASES (fase 1 sem parâmetros responde só SE existe);
a pesquisa passou a cobrir as TRÊS fontes: SISREG (data futura) + espelhos SER e SERNIT (situação
*Agendada*, data em texto, rotulada "regulação").

## 02/09/2026 — Cortesia ao atendente é fecho, não pedido (MAURA)

**O caso.** A atendente mandou o aviso ao meio-dia; às 22h28 a cidadã respondeu "👏" e o robô
reabriu com *"Oi! Como posso ajudar?"*.

**Causa:** `código` (toda mensagem virava tarefa do robô) + `prompt` (nada dizia que cortesia
encerra).

**Correção:** mensagem que é SÓ cortesia (emoji, "ok", "obrigada", "confirmado", "estarei lá" —
régua conservadora) logo após mensagem de um HUMANO nem vira tarefa: silêncio. Guardrail: cortesia
a assunto tratado pelo atendente = ENCERRADO; só seguir se houver pedido A MAIS. O mesmo vale para
qualquer resposta do cidadão à conclusão de um humano: verificar se é só o fecho antes de dar
continuidade.

## 01/09/2026 — 12 correções da análise por simulação (VALDERI, ISIS, hérnia, fim de semana…)

Ver [`2026-09-01-simulacao-cenarios-reais.md`](./2026-09-01-simulacao-cenarios-reais.md): anúncio
da Secretaria descartado do payload, sábado contando como expediente, laço bot-a-bot com
auto-resposta de comércio, "oi" casando com "foi" no classificador, modelo sem noção de data,
saudação engolindo pedido real, e mais.

## Princípios que se repetem (a moral das entradas)

1. **Instrução não vence histórico; ferramenta vence.** Proibir por prompt não basta — dar o
   caminho certo (ferramenta/fase) resolve.
2. **Mecânico não vai a modelo.** Botão, cortesia, verificação de identidade, auto-resposta de
   robô: tudo determinístico. LLM é para conversa de verdade.
3. **Só se pede dado com algo a entregar.** Checar antes, pedir depois.
4. **O que um humano concluiu está concluído.** O robô trabalha a partir do humano, nunca por cima.
5. **Fora do horário, o robô atende — sem prometer gente.** A promessa que não se cumpre é pior
   que a espera anunciada.
6. **Medir antes de culpar o modelo.** Variância se mede (N execuções); a maior parte das "bolas
   fora" era desenho nosso.
