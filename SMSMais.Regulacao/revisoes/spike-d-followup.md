# Spike d — Classificação de follow-up do SER e do SERNIT

- Data: 05/09/2026 · Executor: Claude Opus 5 (sessão de implementação) · OK de produção: **não se aplica** (só leitura)
- Credencial: nenhuma externa. Leitura do Postgres de produção (`smsmarica.ser_evento`, `sernit_evento`).
- Requisições a sistema externo: **0**. Nenhuma escrita.
- Corpus: **18.904** follow-ups do SER e **312** do SERNIT (todos, não amostra).
- Saídas: [`spike-d-regras-followup.json`](spike-d-regras-followup.json) (o `regras_followup_json` inicial do plano 09) e `SMSMais.server/tests/SMSMais.Tests/Regulacao/Pendencias/Fixtures/followups-rotulados.csv` (60 casos rotulados, sem PII).

## Resumo em uma frase

**Sim, dá para classificar sem IA** — regex simples deixam só **13% em "Outro"** —, mas a taxonomia do plano 13 está errada: as duas maiores classes reais (**"sem vaga"**, 22%, e **"risco reclassificado pelo regulador"**, 9% no SER e **81% no SERNIT**) não estavam nela, e o template que parecia "documento criticado" é na verdade **orientação ao paciente**, que não pede ação nenhuma da unidade.

## 1. A taxonomia do plano não sobrevive ao corpus

O plano 13 pedia quatro classes: `FalhaContato | DocumentoCriticado | Agendamento | Outro`. O que os dados mostram:

| Classe do plano | O que aconteceu |
|---|---|
| `FalhaContato` | **Existe e é a maior**: 40% do SER. Confirmada. |
| `DocumentoCriticado` | Existe, mas é **0,3%** — e quase todo texto que parece documental é outra coisa (§2). |
| `Agendamento` | **0,7%**. Quase não aparece como follow-up porque `Agendar` é um **evento próprio** do SER; o follow-up comenta o agendamento, não o registra. |
| `Outro` | Seria 60% do corpus com a taxonomia do plano. |

Faltavam duas classes que juntas são **31% do SER e 81% do SERNIT**, e mais três pequenas mas acionáveis.

## 2. Taxonomia proposta e distribuição medida

| Categoria | SER | % | SERNIT | % | Vira pendência? |
|---|---:|---:|---:|---:|---|
| **FalhaContato** | 7.533 | 39,8% | 0 | — | **sim — pendência de contato** |
| **SemVaga** | 4.124 | 21,8% | 6 | 1,9% | não (é estado, não pedido) |
| Outro | 2.470 | 13,1% | 44 | 14,1% | não |
| **ContatoRealizado** | 2.397 | 12,7% | 3 | 1,0% | não (mas ver §3) |
| **ReclassificacaoRisco** | 1.732 | 9,2% | **252** | **80,8%** | não |
| **OrientacaoAoPaciente** | 364 | 1,9% | 0 | — | não — **e é isto que muda o plano 06** |
| Agendamento | 127 | 0,7% | 4 | 1,3% | não |
| CancelamentoOuReagendamento | 91 | 0,5% | 2 | 0,6% | não (é pedido da ponta *para* a central) |
| **SolicitacaoAoSolicitante** | 66 | 0,3% | 1 | 0,3% | **sim — pendência de documento/informação** |

**A distinção que mais importa** é entre duas famílias que o plano tratava como uma:

- **`OrientacaoAoPaciente`** (364) — *"Favor informar ao paciente sobre esta consulta … bem como os documentos de identificação pessoal"*. A central está mandando a unidade **avisar o paciente o que levar no dia**. Não há nada a corrigir na solicitação.
- **`SolicitacaoAoSolicitante`** (66) — *"Favor anexar laudo de biópsia"*, *"PARA PENDENCIAR: Anexo com laudo incompleto"*, *"Informar peso e se já apresentou reação a contraste iodado"*. Aqui sim a central quer algo **da unidade**, e é isto que vira pendência.

Misturá-las faria a fila de pendências do plano 06 nascer com **5,5× mais itens do que o real**, quase todos falsos — e a fila que cria trabalho falso é abandonada na primeira semana.

O **SERNIT é outro animal**: 81% dos seus follow-ups são `Risco reclassificado pelo Regulador`, e ele **não tem um único** registro de falha de contato. Quem for implementar o plano 06 não deve esperar simetria entre os dois sistemas.

## 3. `ContatoRealizado` tem sub-desfecho, e ele muda o que o sistema faz

Não basta saber que o contato aconteceu; o corpus registra o que o paciente respondeu. Medido sobre os 2.397:

| Sub-desfecho | N | O que deveria acontecer |
|---|---:|---|
| `Aguarda` | 1.081 | nada — só registra |
| `Indefinido` | ~950 | nada |
| `NaoAguarda` (não quer mais, já fez, particular, faleceu) | 347 | **avisar a unidade**: a solicitação provavelmente deve ser cancelada |
| `NaoComparecera` | 9 | avisar a unidade |

Os 347 `NaoAguarda` são vaga que volta para a fila. O plano 06 não previu esse caminho — previa só falha de contato e documento criticado.

## 4. Precisão medida

Método: em vez de rotular 100 linhas aleatórias (o que gastaria quase toda a amostra na classe dominante), rotulei **~130 itens estratificados por classe predita** — que é o que mede *precisão*, o critério do plano. Cada rodada de erro virou correção de regex e nova amostra.

| Categoria | Lidos à mão | Erros | Precisão |
|---|---:|---:|---:|
| FalhaContato | 36 | 1 (corrigido) | **100%** pós-correção (10/10 na rodada final) |
| ContatoRealizado | 24 | 0 | **100%** |
| SemVaga | 16 | 0 | **100%** |
| ReclassificacaoRisco | 18 | 0 | **100%** |
| SolicitacaoAoSolicitante | 26 | 4 (corrigidos) | **100%** pós-correção (12/12) |
| OrientacaoAoPaciente | 20 | 2 (corrigidos) | **100%** pós-correção (12/12) |
| CancelamentoOuReagendamento | 10 | 0 | **100%** |
| Agendamento | 10 | 1 | **90%** |

Todas acima do piso de 80% do plano. O fixture de 60 casos bate 60/60 com o classificador.

### Seis defeitos reais encontrados medindo (todos viraram caso de regressão no fixture)

1. **`AO?` não casa "o".** O lookahead que devia excluir "informar **ao** paciente" foi escrito `(?!\s+AO?\s+PACIENTE)` — e `AO?` exige o "A" literal. Resultado: *"Sr Gestor, favor informar **o** paciente"* caía em `SolicitacaoAoSolicitante` e viraria pendência falsa. Corrigido para `(?!\s+(?:AO|A|O)\s+PACIENTE)`.
2. **"sem êxito" ≠ "sem sucesso".** O corpus usa as duas com frequência parecida; só uma estava na regra.
3. **"Feito contato telefônico" é contato *realizado*, não falha.** A regra de falha aceitava `FEITO … CONTATO` sem exigir pluralidade. Agora exige `diversas|várias|muitas|N` antes de "tentativas/contatos".
4. **`CONTATO TELEFONIC` é amplo demais** — aparece também em *"atualizar o **contato telefônico**"*, que é pedido à unidade. Foi retirado; `INFORMARAM QUE` cobre o caso legítimo.
5. **"sem disponibilidade de vaga" não casava com "sem vaga"**, nem `AG. VAGA` com "aguardando vaga".
6. **"o telefone é de outra pessoa / informam ser engano"** é falha de contato e não era reconhecido — e é justamente o caso em que a ponta **tem** de trazer telefone novo.

## 5. PII — por que o fixture não é um dump

O corpus tem, em texto livre, **nome completo de paciente, nome de familiar e telefone**. Escrevi um anonimizador automático e **ele não é confiável nos dois sentidos**:

- **deixa passar** nome entre parênteses e nome depois do telefone (`Em contato com (21)…, SUELI …`);
- **destrói texto legítimo** em CAIXA ALTA, onde toda palavra parece nome próprio — chegou a transformar `PACIENTE AGENDADO PARA O HOSPITAL…` em `PACIENTE <NOME> O HOSPITAL…`.

Por isso o fixture **não** é uma amostra anonimizada. É montado só a partir de textos que **se repetem ≥ 3 vezes** no corpus — repetição é prova de template, e template não carrega nome — com um filtro que rejeita qualquer linha com telefone ou sequência de 5+ dígitos, e conferência linha a linha. Os 8 casos-armadilha do fim foram **escritos à mão**. Nenhum nome de pessoa vai para o repositório; os nomes que aparecem são de hospitais.

## 6. O que muda nos planos

| Plano | § | Mudança |
|---|---|---|
| 13 | spike d | **Taxonomia trocada**: de 4 para 9 categorias. As duas maiores (`SemVaga`, `ReclassificacaoRisco`) não estavam previstas; `Agendamento` é residual porque `Agendar` é evento próprio. |
| 13 | spike d — método | Rotular 100 linhas aleatórias mede mal: 40% cairiam em `FalhaContato`. Medir por **amostra estratificada da classe predita** (~130 itens) é o que dá precisão por classe. |
| 06 | 6.2 (classificador) | Só **duas** categorias viram pendência: `FalhaContato` → pendência de **contato**; `SolicitacaoAoSolicitante` → pendência de **documento/informação**. As outras sete são registro. |
| 06 | 6.2 | **Separar `OrientacaoAoPaciente` de `SolicitacaoAoSolicitante`** é a decisão de projeto do plano 06: juntas, a fila nasceria com 5,5× itens falsos. |
| 06 | **novo** | `ContatoRealizado` com sub-desfecho **`NaoAguarda` (347 casos)** precisa de caminho próprio: avisar a unidade de que o paciente não quer mais. O plano não previa. |
| 06 | 6.3 | O **SERNIT não gera pendência de contato nenhuma** (0 em 312). Não esperar simetria entre os sistemas. |
| 06 | 6.2 (teste) | O fixture está em `tests/SMSMais.Tests/Regulacao/Pendencias/Fixtures/`, mas **`SMSMais.Tests.csproj` não tem regra de cópia para a saída** e nenhum teste hoje lê arquivo. Quem escrever o teste tem de acrescentar `<None Update="**\Fixtures\**" CopyToOutputDirectory="PreserveNewest" />` — senão o teste falha por arquivo não encontrado. Não mexi no `.csproj` porque spike não altera código (plano 13, "Fora de escopo"). |
| 09 | configuração | `regras_followup_json` inicial pronto em [`spike-d-regras-followup.json`](spike-d-regras-followup.json): 8 regras ordenadas, **a ordem importa** (a primeira que casa vence), normalização declarada, e `vira_pendencia` por categoria. |

## 7. Ressalva

O classificador é bom para **rotear**, não para **decidir sozinho**. `Outro` fica em 13% e ainda guarda casos recuperáveis ("no aguardo dos exames", "prestador não liberou agenda", "paciente já realizou"). A tela do plano 06 deve permitir que o agente **reclassifique** uma pendência à mão, e essa correção é o insumo para a próxima versão das regras — que por isso vivem em configuração versionada (plano 09), não em código.
