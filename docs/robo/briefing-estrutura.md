# Briefing do robô — a estrutura que o treinamento manipula

> Este documento é **injetado no prompt** do agente treinador (`RoboBriefingService`). Ele
> descreve a *estrutura* — o que é assunto, regra global, regra local, condição, comando e canal
> de saída. O **estado atual** (quais assuntos existem, com quais treinos) NÃO mora aqui: é lido
> do banco em tempo de execução e anexado ao briefing. Documento estático descreve a máquina;
> o banco descreve a configuração da máquina. Se este texto e o banco divergirem, o banco vence
> e este texto está com defeito.

## 1. As cinco camadas do prompt do robô

Toda resposta do robô ao cidadão nasce de um prompt montado por
`RoboPrompt.MontarInstrucao(...)` + `RoboGuardrail.Texto`. A ordem abaixo é a ordem real de
montagem, e importa: o que vem depois tende a vencer o que veio antes.

| # | Camada | Onde mora | Quem edita | Alcance |
|---|--------|-----------|------------|---------|
| 1 | **Persona global** | `robo_configuracao.persona_global` (banco, linha única) | humano, pela tela | todos os assuntos |
| 2 | **Data de hoje** | gerado (`RoboPrompt`) | ninguém | todos |
| 3 | **Persona do assunto** | `robo_assunto.instrucoes_persona` | humano, pela tela | um assunto |
| 4 | **Regras do assunto (treinos)** | `robo_assunto_treino` (lista ordenada) | humano **e o agente treinador** | um assunto |
| 5 | **Blocos de situação** | gerado: dentro/fora de horário, sem comandos, perto do limite, despedida com URL do app | ninguém | todos |
| 6 | **Guardrail** | `RoboGuardrail.Texto` — **código C#** | só por PR/deploy | todos, e vence tudo |

Consequência prática para o treinamento: **uma crítica só é resolvível sem código quando cabe na
camada 4** (ou na 3, ou nas condições de roteamento). Se a correção exigir mexer no guardrail
(camada 6), ela é uma *pendência de alteração de código* — não há como aplicá-la pela tela.

## 2. Assunto

Um `RoboAssunto` é a unidade de competência do robô. Não existe "bot pra tudo": cada assunto
carrega sua persona, seus treinos, suas condições de ativação e o conjunto de comandos liberados.

Campos que mudam comportamento:

- `Nome`, `Descricao` — identificação.
- `InstrucoesPersona` — camada 3 acima.
- `Modelo` — modelo de IA do assunto; nulo usa `robo_configuracao.modelo_padrao`.
- `Ativo` — desligado, o assunto não é considerado na classificação.
- `HorarioInicio` / `HorarioFim` / `DiasSemana` (bitmask, bit 0 = domingo) — janela do assunto.
  Fora dela o robô é instruído a **não prometer atendente**.
- `MaxInteracoesSemResolver` — teto de turnos antes do hand-off; no último turno o prompt ganha
  o bloco "esta é a última mensagem".
- `LimiarConfianca` (0..1) — abaixo disso, hand-off.
- `EscalonamentoUnidadeId` — para qual fila cai o hand-off.
- `Ordem` — prioridade na classificação (menor primeiro).
- `Padrao` — **no máximo um** assunto é o padrão: é ele que atende quando nenhuma condição casa.
  Sem assunto padrão, "sem assunto" não é neutro — é o robô sem orientação, sem treinos e sem
  limiar. Cerca de 22% das mensagens caem nesse caminho.

## 3. Regra local = treino (`RoboAssuntoTreino`)

É o material que o operador (e agora o agente treinador) vai acumulando por assunto. Todo treino
**ativo** entra no prompt como um item de uma lista `Regras (siga cada uma):`, na ordem de
`Ordem`. Campos: `Tipo`, `Titulo`, `Conteudo`, `Ordem`, `Ativo`.

Tipos (`TipoTreinoRobo`, valores estáveis):

| Valor | Tipo | Uso |
|---|---|---|
| 1 | `Instrucao` | diretriz de conduta neste assunto |
| 2 | `Exemplo` | pergunta do cidadão + resposta desejada (few-shot) |
| 3 | `Glossario` | termo local e o que significa |
| 4 | `Do` | o que o robô **deve** fazer |
| 5 | `Dont` | o que o robô **não** deve fazer |

Como o treino vira texto no prompt: `- {Titulo}: {Conteudo}` (o título é opcional e some quando
vazio). Portanto o `Conteudo` precisa se sustentar sozinho como uma frase imperativa — ele será
lido fora de qualquer contexto de tela.

**Exclusão de treino é lógica**: desativa-se (`Ativo = false`), não se apaga. É o que permite
desfazer uma correção do agente.

## 4. Condição de ativação (`RoboAssuntoCondicao`) — o roteamento

Antes de chamar a IA, `RoboClassificador` faz um pré-match barato da mensagem contra as condições
de cada assunto ativo, na ordem de `RoboAssunto.Ordem`. Tipos (`TipoCondicaoRobo`):

| Valor | Tipo | Semântica |
|---|---|---|
| 1 | `PalavraChave` | o texto **contém a palavra**, insensível a acento e caixa |
| 2 | `Regex` | o texto casa com a expressão regular |
| 3 | `Frase` | o texto **contém a frase inteira**, insensível a acento e caixa |

Crítica típica que se resolve **aqui e não em treino**: "isso caiu no assunto errado", "ele
respondeu como se fosse confirmação de agendamento e a pessoa falava de laudo". Mexer em treino
nesse caso é remendo: a regra vai para o assunto que nem deveria ter sido escolhido.

Cuidado ao criar condição: uma palavra-chave genérica ("exame", "consulta") num assunto de
`Ordem` baixa **rouba** mensagens de todos os assuntos abaixo dele. Condição nova exige checar
colisão com os assuntos de ordem menor.

## 5. Comandos = as ferramentas (canal de ação e de saída)

O robô só faz o que tem ferramenta para fazer. O conjunto da sessão é
**`ComandoRoboCatalogo.Base` ∪ comandos habilitados do assunto**.

- **Base (sempre disponíveis, mesmo sem assunto)**: `ConsultarCadastro`,
  `ConsultarStatusAgendamento`, `ConsultarUnidades`, `EncaminharParaHumano`. São só leitura.
- **Por assunto** (`robo_assunto_comando.habilitado`): o resto do catálogo, incluindo os de
  escrita (`ConfirmarPresenca`, `IniciarCancelamento`, `RegistrarNumeroErrado`,
  `VerificarCadastro`).
- **`ResponderCidadao` não é um comando opcional**: é a ferramenta **terminal obrigatória**,
  sempre presente. É o **único canal de saída** — texto solto do modelo é descartado.

O catálogo é **código** (`ComandoRoboCatalogo.Itens`), não dado. Comando novo = alteração de
código. A tela só liga e desliga o que já existe.

Regra estrutural que cai direto no treinamento: **sem ferramenta, o robô é proibido de pedir dado
pessoal**. Se a crítica for "ele deveria ter consultado X" e não existe comando para X, a
resposta correta é uma *pendência de alteração de código*, nunca um treino mandando ele afirmar
o que não pode verificar.

## 6. Saída — o que o cidadão lê

`responder_cidadao(texto, handoff, motivo, confianca)`. O campo `texto` é **exclusivamente** a
mensagem que o cidadão lê: nada de raciocínio, nome de ferramenta ou descrição de passo interno.
Se o modelo encerra o turno sem chamar essa ferramenta, a prosa dele é **descartada** e a conversa
cai em hand-off com motivo `sem-uso-da-ferramenta-de-saida`.

Formatação é de WhatsApp: negrito com **um** asterisco, itálico com `_`, sem markdown.

## 7. Guardrail — o que nenhum treino pode contradizer

`RoboGuardrail.Texto` é anexado ao system de **todo** assunto e nasceu de erros reais de produção.
Um treino que contrarie qualquer item abaixo é **inválido** e deve virar pendência, não regra:

0. **Saúde não se orienta por aqui.** Sintoma, dor, urgência, remédio, gravidade: não opina, não
   orienta, não pergunta sobre sintoma. Encaminha ao posto / rede de urgência. Vence todas as
   outras regras.
1. Formatação de WhatsApp.
2. **Privacidade**: nada de procedimento, data, hora ou local antes de identidade confirmada por
   comando — nem repetindo do histórico.
3. **Identidade**: quatro primeiros dígitos do CPF de uma vez, depois mês/ano; nunca CPF completo,
   nunca dígito a dígito, nunca exemplo de formato.
4. **Nunca afirmar sem ferramenta** — inclusive ausência ("você não tem nada agendado").
5. **Hand-off só a pedido** ou por impossibilidade real; nunca como cortesia, nunca na primeira
   mensagem, nunca fora do horário humano.
6. Não contradizer atendente humano; cortesia a assunto encerrado não reabre atendimento.
7. Sempre 2ª pessoa com quem escreve.
8. **Não agenda, não remarca, não desmarca.** Orienta o posto.
9. **Não inventa canais**: só o posto presencial e o app do cidadão. Não existe 0800 nem central.
10. Saída só por `responder_cidadao`.

## 8. Trava de humano e horário

Duas janelas diferentes, que se confundem com facilidade:

- **Janela do assunto** (`RoboAssunto.HorarioInicio/Fim/DiasSemana`): quando *aquele assunto*
  atende.
- **Janela do atendente humano** (`robo_configuracao.HoraAtendimentoHumanoInicio/Fim` +
  `DiasSemanaAtendimentoHumano`, padrão 62 = seg-sex): quando *existe gente*. Fora dela o robô
  assume mesmo com humano na sessão, e é proibido de prometer atendente.

Crítica do tipo "ele prometeu atendente num sábado" quase sempre é configuração de janela, não
treino.

## 9. O que o treinamento pode e não pode mudar

| Alvo | Quem pode mudar | Via treinamento? |
|------|-----------------|------------------|
| Treino do assunto (camada 4) | humano e **agente** | **sim**, aplicado direto (com desfazer) |
| Condição de ativação (roteamento) | humano e **agente** | **sim**, aplicado direto (com desfazer) |
| Persona do assunto (camada 3) | humano | pendência de regra de negócio |
| Persona global (camada 1) | humano | pendência de regra de negócio — afeta todos os assuntos |
| Janelas de horário, limiar, teto, escalonamento | humano | pendência de regra de negócio |
| Ligar/desligar comando do assunto | humano | pendência de regra de negócio |
| Guardrail, catálogo de comandos, novo comando, motor | **código** | pendência de alteração de código |

## 10. Vocabulário do processo de treinamento

- **Crítica** — o que o atendente escreveu ao marcar uma resposta do robô pelo ícone na bolha.
- **Item de treinamento** — a unidade de trabalho: crítica + observação + análise + alterações +
  pendências + simulações.
- **Adversários** — passagem obrigatória de reinterpretação: antes de aplicar, a regra proposta é
  atacada por leitores que tentam mostrar que ela **anula**, **contradiz** ou **torna dúbia** uma
  regra existente (global, do guardrail ou de outro assunto).
- **Pendência** — bloqueio que exige humano: *regra de negócio* (decisão que o agente não pode
  tomar) ou *alteração de código* (precisa de PR/deploy, e de autorização explícita).
- **Simulação** — ensaio de um turno contra o modelo treinado atual, sem falar com o cidadão e
  sem executar comando de escrita. Após qualquer alteração aplicada, fica **pendente** até rodar.
