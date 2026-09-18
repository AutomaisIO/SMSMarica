# ADR-0058 — Estratégias de fila: simulador determinístico + agente que só escolhe parâmetros livres

**Status:** aceito · **Data:** 2026-09-17
**Relacionado:** [ADR-0050](./0050-motor-robo-messages-api.md) (loop de tool-use no .NET; resposta só
pela ferramenta terminal) · [ADR-0055](./0055-catalogo-canonico-e-embeddings.md) (catálogo canônico;
oferta interna derivada da escala) · [ADR-0012](./0012-agendamento-local-e-integracao-sisreg-leitura.md)
(SISREG só-leitura) · [ADR-0043](./0043-instancia-por-municipio.md) (uma instância por município)

## Contexto

A Agenda lê a oferta regulada (escalas do SISREG × marcações) e a demanda (fila de espera do
SISREG, ~74 mil abertas desde a carga de 12/09/2026). Ela responde "onde sobra, onde falta, quem
espera quanto". Não responde a pergunta do gestor: **"o que eu preciso mudar na oferta para zerar
(ou estabilizar) a fila deste procedimento, e em quanto tempo?"**

Havia três formas de responder, e duas são armadilhas:

1. **Pedir ao modelo de linguagem que calcule.** Modelo erra aritmética e não é reproduzível: um
   "zera em 14 semanas" que muda quando se pergunta de novo não pode ir ao secretário.
2. **Deixar o modelo mexer em tudo.** O gestor que já sabe que "não vai contratar" precisa que isso
   seja fato, não pedido; instrução no prompt não é garantia.
3. **Simulação determinística com o modelo escolhendo só o que está livre** — a decisão.

## Decisão

### 1. Os números são nossos, nunca do modelo

A projeção da fila é uma função pura em .NET (`SimuladorFila.Projetar`), sem banco e sem IA.
Passo semanal, horizonte até 156 semanas:

```
capacidade/semana = profissionais × diasPorSemana × horasPorDia × atendimentosPorHora × aproveitamento
                    + mutirões da semana
fila(t+1) = fila(t) + entrada − min(capacidade, fila(t) + entrada)
```

Saída: semana em que zera (ou "não zera" e quanto cresce), capacidade de equilíbrio (= entrada),
capacidade necessária para zerar num prazo, pico, atendidos até zerar, série. Coberta por teste
sem banco — a fórmula não muda sem um teste vermelho dizendo o quê.

### 2. O agente tem duas ferramentas, uma delas terminal

O agente (`EstrategiaAgenteIa`) roda o loop de tool-use no .NET sobre a Messages API, no molde do
[ADR-0050](./0050-motor-robo-messages-api.md), com o modelo da Configuração da IA (troca pela tela,
sem deploy). Ferramentas:

- `simular(parametros)` — roda o `SimuladorFila` com os números que o modelo mandou e devolve os
  marcos. O modelo experimenta quantas vezes quiser (teto de 10 iterações).
- `propor_estrategia(parametros, resumo, acoes[], riscos[], confianca)` — **terminal**. Prosa sem
  ela é descartada.

O `system` vai com `cache_control: ephemeral` e carrega o **cenário em markdown** (fila por risco e
faixa, séries de entrada e vazão, oferta por unidade e profissional, aproveitamento, parâmetros
com estado). Sem PII: nomes de profissional entram porque já são dado público do SISREG e
aparecem na tela de Agenda; paciente nunca.

### 3. Trava é contrato, não instrução

Cada parâmetro numérico carrega `{ valor, travado, min?, max? }`. `ParametrosEstrategiaAplicador`
aplica o que o modelo mandou **rejeitando** parâmetro travado com valor diferente e parâmetro
livre fora de mín/máx. A violação volta ao modelo como erro de ferramenta; ele corrige ou a rodada
termina em `Falha` — registrada com o custo, sem 500, sem virar a rodada atual.

### 4. Rodada é append-only

`estrategia_fila` é a cabeça (nome, procedimento, estado, parâmetros vigentes, rodada atual).
`estrategia_fila_rodada` guarda cada execução: parâmetros de entrada (com travas), **snapshot do
cenário**, parâmetros resultantes, projeção, proposta, modelo/tokens/custo/duração, falha. Nunca se
edita nem apaga uma rodada: reabrir e rodar de novo cria a seguinte. É o que permite comparar
rodadas e, depois de aplicada, medir a fila real contra a projeção.

### 5. "Aplicar" é anotação humana — nada escreve no SISREG

Decisão explícita do Bernardo (17/09/2026): **nada do que for decidido na simulação aplica no
SISREG nem em lugar nenhum**. A estratégia fica no nosso banco para ser consultada;
`aplicada_em`/`aplicada_por`/`aplicacao_nota` registram que alguém a executou por fora. O SISREG
continua só-leitura ([ADR-0012](./0012-agendamento-local-e-integracao-sisreg-leitura.md)).

### 6. O eixo é a família do procedimento do SISREG

Procedimento = código `pa` + nome, com a família grupo↔item que já valia na tela de Ofertas
(grupo `XXXX000` cobre os itens `XXXX*`; a fila só tem nome, a escala tem código + nome). A regra
foi extraída para `FamiliaProcedimentoSisreg`, compartilhada pelas duas telas — **uma régua só**,
para os dois mostrarem o mesmo número. Procedimento **com fila e sem escala** entra na lista com
código nulo: é o caso mais grave, e é o que uma lista montada a partir da escala deixaria de fora.
Vínculo com o catálogo canônico ([ADR-0055](./0055-catalogo-canonico-e-embeddings.md)) só para
exibir o nome; não é FK obrigatória.

### 7. O cenário é determinístico e explica de onde cada número sai

`CenarioFilaService`, SQL cru em dia de Brasília, semanas de segunda a domingo, semana corrente
fora das médias:

| Bloco | Fonte | Por quê |
|---|---|---|
| Fila | `sisreg_fila_pendente` aberta, nomes da família | quem espera HOJE (a fila é estado, não histórico) |
| Entrada | união por nº de solicitação: fila (aberta ou saída) ∪ marcações, por `data_solicitacao` | só a fila subestima — quem foi rápido já saiu dela |
| Vazão | marcações por `data_agendada` | o que a rede atende de verdade, não o que a escala promete |
| Oferta | escalas ativas expandidas nas próximas 4 semanas ÷ 4 | mesmo método da Análise de vagas; vaga da regulação = 1ª vez + reserva; **agenda local fora** |
| Aproveitamento | marcações ÷ vagas ofertadas nas últimas 8 semanas | escala viva com vaga morta (ECG do CDT) aparece aqui, e a capacidade efetiva é vagas × isto |

Os parâmetros iniciais reproduzem a oferta de hoje por construção
(`atendimentosPorHora = vagas/semana ÷ (profissionais × dias × horas)`), então "rodar sem mudar
nada" é o cenário atual. Sem escala, entram valores de partida (5 dias, 4 h, 2/h, aproveitamento
0,85) para o agente ter de onde propor.

### 8. Permissão e escopo

`ModuloPermissao.EstrategiasFila = 66` (append-only). Consulta = lista, cenário e simular sem
gravar; Inclusão = salvar e rodar o agente (gasta chamada de IA); Edição = editar, rerodar, marcar
aplicada; Exclusão = arquivar/excluir. Escopo: **rede toda** — é planejamento, o mesmo critério da
Demanda regulada.

## Consequências

- **Positivas:** número reproduzível e conferível no papel; o agente é útil onde é bom (escolher
  combinações, escrever ações para gente) e afastado de onde é ruim (aritmética); travas viram
  garantia; histórico completo de cada tentativa; custo por rodada registrado.
- **A vigiar:** a rodada do agente roda síncrona no request (30–90 s; timeout do cliente 4 min) —
  se ficar lenta na prática, vira tarefa em fila; o modelo é linear (entrada constante, sem
  sazonalidade) — quem quiser "a demanda cresce 10%" trava `entradaSemanal` num valor maior; o
  aproveitamento mistura vaga de retorno e agenda local no denominador (é estimativa, dito na
  tela); a lista de procedimentos é cacheada por 10 min no backend.
- **Não há orçamento global de IA** (não existe no projeto); o teto é por rodada (10 iterações ×
  6k tokens de saída).

## Alternativas descartadas

- **Modelo calcula a projeção** (prompt com os dados, resposta com o prazo): não reproduzível, não
  auditável.
- **Otimizador matemático sem agente** (busca em grade sobre os parâmetros livres): dá o mínimo
  numérico, mas não escreve "abrir 2 blocos na 3ª e 5ª no CDT" nem pondera que aproveitamento
  baixo pede ação antes de vaga nova. Fica como possível pré-passo se o custo do agente pesar.
- **Escrever no SISREG** ao "aplicar": vetado pelo Bernardo e pelo ADR-0012.
