# ADR-0050 — Motor do robô de atendimento na Messages API (loop de tool-use no .NET)

**Status:** aceito · **Data:** 2026-08-29
**Relacionado:** [ADR-0011](./0011-modulo-ia-consulta-natural.md) §3 (provedor de IA configurável,
primeiro alvo Messages API), módulo Robô de Atendimento (`SMSMais.Core/RoboAtendimento`), serviço
`SMSMais.aiengine` (kinds `agente`/`dados` seguem intocados).
**Motivado por:** o incidente do go-live do robô em 26/08/2026.

## Contexto

O robô de atendimento do WhatsApp subiu sobre o **Claude Code por assinatura** — o serviço Python
`SMSMais.aiengine`, kind `atendimento`, usando `claude-agent-sdk`. Foi um atalho assumido: o
ADR-0011 §3 já prescrevia a **Anthropic Messages API com tool use e prompt caching**, e o plano do
robô registrou "assinatura agora, medir e migrar depois".

A noite de 26/08 foi a medição. Com o robô ligado por algumas horas, sobre 62 conversas reais:

| Sintoma medido | Causa estrutural |
|---|---|
| Latência **p50 159 s, p90 1362 s (22,7 min)** | um subprocesso do agente + servidor MCP por turno, fila sequencial e polling de 15 s |
| **34 tarefas** em `Connection refused (127.0.0.1:5085)` | o motor é outro processo, e ele cai |
| O robô mandou ao cidadão a **própria prosa de raciocínio** ("preciso carregar o schema da ferramenta `verificar_cadastro`") | sem fechar com a ferramenta terminal dentro do `max_turns`, o fallback enviava o texto solto |
| Custo **não atribuível** por atendimento | assinatura não dá preço por chamada |
| 5,7 M tokens de entrada para 145 k de saída (~42,8 k por tarefa) | contexto reenviado inteiro a cada turno, **sem prompt caching** |

O terceiro item é o que decide o ADR. Ele não foi um erro de instrução — foi um erro de
**arquitetura**: quando o canal de saída é "o que sobrar do stream", nenhum guardrail garante que
só a mensagem certa chegue ao cidadão. Enquanto o loop for de outro processo, o robô continua
podendo falar o que não devia.

## Decisão

**O loop de tool-use passa a rodar no `SMSMais.server`, chamando a Messages API direto.**

1. **Nova implementação de `IRoboAtendimentoMotor`** (`RoboAtendimentoMotorApi`), que monta a
   requisição, lê os blocos `tool_use`, executa os comandos e devolve a resposta. A costura já
   existia — o `RoboAtendimentoProcessador` não muda.
2. **Comandos executam in-process** pelo `IRoboComandoDispatcher`, que já fazia habilitação por
   assunto, idempotência e trilha `RoboAcao`. Some o guichê de loopback `/robo-comando`.
3. **A resposta ao cidadão só sai da ferramenta terminal `responder_cidadao`.** Terminando sem
   ela, a prosa é **descartada** (fica no log) e o cidadão recebe uma mensagem segura com
   hand-off. Isto é estrutura, não instrução.
4. **Prompt caching** (`cache_control: ephemeral`) no bloco `system`, que carrega persona, treinos
   e guardrail — a parte que repete a cada turno.
5. **Chave**: a que já existe cifrada em `IaConfiguracao.TokenCifrado` (Data Protection, purpose
   `SMSMarica.Ia.Segredos`), a mesma que o `DistribuidorIa` do TFD usa. Sem campo novo, sem Secret
   no GitHub, sem mudança no deploy.
6. **`RoboConfiguracao.Motor`** (`Assinatura` | `Api`, default `Assinatura`) escolhe o motor a cada
   turno. O deploy não muda comportamento; virar e voltar é uma troca na tela.
7. **Custo estimado por tabela de preços** (`PrecoModeloIa`): a Messages API devolve `usage` mas
   não `total_cost_usd`, que vinha pronto do SDK.

O `aiengine` **continua servindo Agente IA e Consulta** pela assinatura. A migração é só do kind
`atendimento`.

## Por que não trocar o SDK pela API dentro do Python

Foi a alternativa mais óbvia — e é uma armadilha. O `aiengine` apaga `ANTHROPIC_API_KEY` em três
camadas de propósito: `main.py` (remoção em runtime), o gate de startup, e
`UnsetEnvironment=ANTHROPIC_API_KEY` no systemd. A razão é que **a precedência da API key vence o
token da assinatura**: uma chave dentro daquele processo desviaria a cobrança de *Agente IA* e
*Consulta* para a API metrada. Migrar no .NET não encosta nessas travas.

Some-se que o Python manteria dois serviços, o guichê de loopback e a latência de rede — sem
resolver o problema central, que é o canal de saída não ser controlado por nós.

## Consequências

**Ganhos.** Resposta em segundos em vez de minutos (some o subprocesso, some o polling entre
processos). Vazamento de raciocínio deixa de ser possível por construção. Custo e tokens medidos
por turno, alimentando o relatório de consumo por assunto que já existe. O robô deixa de depender
de um segundo serviço estar de pé. E o motor virou testável: 9 testes com a API dublada, contra
zero cobertura .NET do runtime do robô antes disso.

**Custos aceitos.** Passamos a pagar por token no atendimento (antes diluído na assinatura) — com
o volume atual, ordem de centavos por dia, e agora visível. Robô e TFD dividem a mesma chave na
fatura da Anthropic; a separação por assunto fica no relatório interno. E o guardrail, que era
texto no Python, virou constante C# (`RoboGuardrail`): mudar o comportamento do robô agora exige
deploy — o que é mais disciplina, não menos, mas é uma perda de agilidade que vale registrar.

**Reversível.** Enquanto o piloto não fechar, `Motor = Assinatura` devolve o caminho antigo sem
deploy. A limpeza (remover `atendimento.py`, `atendimento_tool.py`, `RoboComandoEndpoint`,
`RoboAtendimentoMotorHttp`) só acontece depois — até lá, são o caminho de volta.

## Defeitos corrigidos de carona

A leitura do código para a migração revelou três coisas que ninguém tinha visto:

- **`RoboAssunto.LimiarConfianca` era gravado e nunca comparado.** O hand-off por baixa confiança —
  decisão registrada desde o plano original do robô — **nunca funcionou**. Agora
  `Confianca < LimiarConfianca` gera hand-off.
- **`ConsultarStatusAgendamento` era uma ferramenta fantasma**: estava no catálogo da tela, podia
  ser habilitada por assunto, e não tinha handler nenhum — só respondia "Comando indisponível".
- **`VerificarCadastro` tinha handler e ferramenta, mas estava fora de `Habilitaveis`**: a tela
  recusava habilitá-lo, e ele só funcionava semeado direto no banco.

## Alternativas consideradas

**Manter a assinatura e só endurecer o guardrail.** Foi o que se fez na própria noite do incidente,
e resolveu os sintomas de texto (negrito, exemplos de CPF, oferta de atendente). Não resolve
latência, custo cego, nem o canal de saída — a prosa continuava podendo vazar pelo fallback.
Rejeitada como solução final; serviu de contenção.

**Trocar o SDK pela API dentro do Python.** Ver a seção própria: reativaria a precedência da API
key sobre a assinatura dos outros kinds.

**Desligar o robô e ficar só no determinístico.** A máquina de verificação cadastral
(`VerificacaoCadastralWhatsAppHandler`) já cobre identidade sem LLM nenhum, e é o que está de pé
hoje. Mas ela responde só ao desafio cadastral; dúvida aberta ("cadê meu laudo?") continua caindo
na fila humana. O robô continua fazendo sentido — desde que fora do caminho crítico de identidade.

## Verificação

Suíte do server verde (960/961, 1 skip conhecido), incluindo os 9 testes novos do motor: resposta
só pela ferramenta terminal, prosa descartada quando ela não vem, laço de tool-use com execução de
comando, teto de iterações, tokens e custo, `529` reesperado no próprio turno, erro permanente
propagado ao worker, ausência de chave falhando explícito, e histórico virando turnos
`user`/`assistant`.

Em produção, o roteiro é: subir com `Motor = Assinatura` (nada muda) → virar para `Api` com o robô
**global desligado** e exercitar pela tela **Simular atendimento** → ligar **um** assunto em
horário comercial → abrir os demais por etapas. A meta de latência é p90 abaixo de 30 s, contra os
1362 s medidos hoje.
