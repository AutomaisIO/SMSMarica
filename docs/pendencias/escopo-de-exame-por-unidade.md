# Proposta — o tipo de exame passa a ter escopo por unidade (e a flag de worklist vai junto)

**Status:** proposta para decidir. Levantada em 2026-09-08, a partir de um caso real.
**Relacionado:** [ADR-0013](../adr/0013-agenda-multi-recurso.md) (equipamento é entidade `smsmarica`) ·
[ADR-0055](../adr/0055-catalogo-canonico-e-embeddings.md) (catálogo canônico; oferta interna derivada) ·
[`provisionamento-equipamento-pacs-pela-tela.md`](./provisionamento-equipamento-pacs-pela-tela.md)

## O caso que expôs o problema

Exame `260908001`, RADIOGRAFIA DE TORAX (PA), executante CDT, criado à mão em 08/09:

| Hora | O quê |
|---|---|
| 13:23:56 | A recepção autoriza. O sistema detecta que não dá para enviar, carimba o motivo no exame, deixa-o fora da fila e abre o **ERRO-KF775R** |
| 13:38:01 | Alguém clica em **Reenviar worklist**. O exame entra na fila **e a mensagem de erro é apagada** |
| desde então | `tentativas_envio = 0`, `ultima_tentativa_em` vazio — o worker **nunca tocou** no exame |

Duas falhas distintas, e só a segunda é bug de código:

1. **O motivo real:** o tipo "RADIOGRAFIA DE TORAX (PA)" está com o envio à worklist desligado.
   Mas a flag é **global do município** — desligá-la para o Hospital Santo Antônio (que não tem
   aparelho) desliga também para o CDT (que tem). Não existe estado correto: o mesmo bit precisa
   valer `false` numa unidade e `true` na outra.
2. **O que escondeu o problema:** `ReenviarWorklistAsync` não chama `ImpedimentoEnvioPacs`. Ele
   limpa `ErroIntegracaoPacs` e enfileira mesmo quando é impossível enviar. O operador clica, o
   aviso some, e nada acontece. `AutorizarAsync` faz a checagem certa; o reenvio não.

## Os números que sustentam o desenho

Medidos em produção em 08/09/2026:

| | |
|---|---|
| Tipos de exame ativos | **204** (59 com worklist ligada) |
| Unidades cadastradas | 177 — mas só **34** já executaram alguma coisa |
| **Pares (unidade × tipo) que existem de fato** | **298** |
| Pares em unidades que **têm equipamento** | **62** (47 no CDT, 15 no CMI) |
| Equipamentos ativos | 6, em **2** unidades |
| Unidades com ambiguidade de destino | 1 — o CDT tem **dois** ultrassons |
| `tipo_exame.unidade_padrao_id` em uso | **0** (coluna morta) |

Duas leituras importantes:

- **A matriz é esparsa: 298 de 36.108 combinações possíveis (0,8%).** A associação não é uma
  explosão combinatória; é uma tabela pequena, e o backfill sai pronto do histórico.
- **O trabalho de configuração é finito e pequeno: 62 pares.** Só faz sentido configurar destino
  DICOM onde existe aparelho.

### A oferta de imagem NÃO dá para derivar da escala

O [ADR-0055 §5](../adr/0055-catalogo-canonico-e-embeddings.md) decidiu que a oferta interna é
**derivada de `sisreg_escala`, não armazenada**. Testei se isso cobriria imagem: dos **298** pares
reais, só **23 (7,7%)** aparecem na escala da mesma unidade — no CDT, 11 de 47; no CMI, 2 de 15.

Faz sentido: escala do SISREG é de **profissional/consulta**; exame de imagem chega por outros
caminhos (demanda espontânea, solicitação manual, módulo 53). **Logo, para imagem a oferta precisa
ser cadastrada.** Isso não contradiz o ADR-0055 — delimita: aquilo trata do que é *regulado*; isto
trata de *para qual máquina o exame vai quando for executado aqui*.

## A proposta

### 1. Tabela nova `smsmarica.tipo_exame_unidade`

| Coluna | Papel |
|---|---|
| `tipo_exame_id`, `unidade_id` | o par; único, filtrado por `excluido_em IS NULL` |
| `enviar_para_worklist` | **a flag, agora por unidade**. Default `false` |
| `equipamento_id` (nullable) | destino explícito, quando a unidade quer amarrar |
| `ativo` | esta unidade executa este exame |
| auditoria + soft delete | padrão ADR-0006 |

**O que fica global no `TipoExame`:** nome, código SISREG, SIGTAP, modalidade, descrições DICOM,
códigos de protocolo. Identidade e perfil técnico do procedimento não mudam de unidade para
unidade — o que muda é quem faz e em qual máquina.

**O que sai dele:** `enviar_para_worklist` (vira por unidade) e `unidade_padrao_id` (morta, 0 usos).

### 2. O `equipamento_id` na associação resolve dois problemas de uma vez

Não é só cadastro — muda a régua de resolução da estação:

1. **Mata a ambiguidade recorrente.** O CDT tem dois ultrassons; hoje a recepção escolhe a sala em
   **toda** autorização (`autorizacao.equipamento_obrigatorio`). Amarrando o tipo ao aparelho, a
   pergunta some para os casos que têm resposta fixa.
2. **Tira a modalidade do caminho crítico.** Hoje o resolvedor exige
   `equipamento.modalidade == tipo.modalidade`, casamento por igualdade exata. Foi exatamente esse
   fio que fez, em 04/09, cinco radiografias marcadas como `MG` apontarem para o **mamógrafo** —
   165 exames a um clique de ir para a sala errada. Com destino explícito, a modalidade vira
   conferência, não a única amarra.

Ordem de resolução proposta:

```
1. exame.equipamento_id            escolha da recepção na autorização   (já existe)
2. tipo_exame_unidade.equipamento_id   configuração da unidade          (novo)
3. dedução por unidade + modalidade    fallback de hoje
4. falha explícita                     worklist.sem_equipamento
```

### 3. A regra de envio substitui a flag global

| Situação | O que acontece |
|---|---|
| Sem associação (tipo, unidade) | não envia — "a unidade não tem este exame no escopo" |
| Associação com flag desligada | não envia — motivo atual |
| Associação com flag ligada | envia |

### 4. Migração sem mudar o comportamento no cutover

- Migration **só cria a tabela** (vazia).
- **Backfill idempotente por script/endpoint administrativo**, não em migration — migration é
  imutável e roda igual em instância nova, onde não há histórico para derivar. Popula as 298 linhas
  a partir dos pares reais, com `enviar_para_worklist` = o valor atual da flag global do tipo.
  Assim o dia seguinte ao deploy é idêntico ao anterior.
- `TipoExame.EnviarParaWorklist` **fica** durante a transição (o código passa a ler a associação) e
  só é removida numa migration posterior, depois de confirmado que ninguém lê.

### 5. Importação cria a associação sozinha

No `ResolvedorTipoExameSisreg` (e nos equivalentes SER/SERNIT), quando chega procedimento novo para
a unidade U:

1. **Tipo global** — cria se não existir *(já faz hoje)*.
2. **Associação (tipo, U)** — cria se não existir, `ativo = true`,
   `enviar_para_worklist = false`, `equipamento_id = null` *(automático, novo)*.
3. **Ligar a flag e amarrar o equipamento** — **manual**, sempre.

Detalhe de implementação: hoje o tipo é resolvido em `ImportacaoSisregService.cs:321` e a unidade
executante em `:469` — a associação precisa ser criada depois que os dois existem, no mesmo fluxo.

Isso mantém a regra que já vale ("NASCE DESLIGADO") e ganha uma consequência boa: a associação
desligada **é** a fila de trabalho. Hoje ninguém sabe que falta configurar até um paciente estar no
balcão e o `ERRO-KF775R` aparecer.

### 6. Consertar o "Reenviar worklist"

Independente do resto: `ReenviarWorklistAsync` passa a chamar `ImpedimentoEnvioPacs` e **recusa**
com o motivo, em vez de apagar o erro e fingir que enfileirou. É o defeito nº 2 do caso acima e
não depende do modelo novo.

## UX — o que está confuso hoje

Três lugares desconexos, e nenhum responde a pergunta que a operação faz:

- `/app/equipamentos` — lista global de aparelhos com coluna Unidade;
- **aba Equipamentos** dentro da Unidade — a mesma lista, filtrada (colunas duplicadas no código);
- `/app/tipos-exame` + formulário com **10 campos** que misturam três públicos: administrativo
  (nome, código SISREG, SIGTAP), técnico DICOM (modalidade, *Requested Procedure Description*,
  *Scheduled Procedure Step Description*, códigos de protocolo) e operacional (tempo estimado,
  **Integração PACS**).

O sintoma é o campo "Integração PACS" no cadastro global: uma decisão **de unidade** mora na tela
do **município**. Quem abre não tem como saber que aquele toggle afeta todas as unidades.

### Proposta de telas

**A unidade vira o centro de gravidade da configuração operacional** — é assim que a operação
pensa ("o que o CDT faz, em qual sala").

1. **Unidade → aba "Exames de imagem"** *(nova; é onde o trabalho acontece)*
   A associação, em tabela: `Tipo de exame | Equipamento (select dos aparelhos DESTA unidade) |
   Worklist (toggle) | Status`. Botão **"Adicionar exame ao escopo"** com busca no catálogo global.
   Linhas por configurar aparecem destacadas no topo.

2. **Unidade → aba "Equipamentos"** *(já existe)*
   Os aparelhos e, quando a outra pendência sair, o status de provisionamento no PACS.

3. **Tipos de Exame (global)** *(enxuga)*
   Só identidade + perfil DICOM. **Sai** o "Integração PACS". Ganha uma aba *"Unidades que
   executam"* — leitura, com atalho para a unidade. Vale separar o formulário em duas seções
   visíveis: *Identificação/faturamento* e *DICOM (avançado)*, que hoje estão embaralhadas.

4. **Painel "Exames a configurar"** *(novo, pequeno)*
   Associações ativas, em unidade **com** equipamento, que estão sem destino ou com worklist
   desligada. Hoje são **62** pares no total — a lista real de pendências é menor e cabe numa tela.

## Decisões tomadas (08/09/2026)

1. **Alcance: só destino DICOM.** A associação responde "quando um exame deste tipo for executado
   aqui, vai para qual máquina e se entra na worklist". **Não** vira gate de solicitação nem de
   agendamento — isso evita competir com a oferta derivada da escala (ADR-0055) e criar duas
   verdades sobre "quem oferece o quê".
2. **Sem associação ⇒ não envia**, com motivo explícito (fail-closed). Depois do backfill dos 298
   pares ninguém perde nada, e o silêncio de hoje vira mensagem.
3. **`equipamento_id`: um só (0..1)** — decisão de projeto, não contestada. Com dois aparelhos
   possíveis, deixa vazio e cai na dedução + escolha da recepção, como hoje.
4. **Unir com `regulacao_procedimento` (999 linhas, ADR-0055): depois.** Hoje são dois catálogos
   paralelos sem ponte (204 × 999) e juntá-los é projeto próprio. Vale prever o campo de ponte
   para não pagar duas migrations.

Quando isto sair do papel, vira ADR (próximo livre: **0056**) — é mudança de modelo com
consequência em regra de negócio.

## Já executado desta proposta

- **Item 6 (consertar o "Reenviar worklist") — FEITO em 08/09/2026.**
  `ReenviarWorklistAsync` passa a chamar `ImpedimentoEnvioPacs` e recusar com
  `solicitacaoExame.envio_impedido`, preservando o motivo na tela. `TipoExame` entrou no `Include`
  — sem isso a navegação viria nula e **todo** reenvio seria recusado como falso "desligado".
  Teste de regressão em `AutorizacaoSolicitacaoTests`.
- **O caso `260908001`**: a flag de "RADIOGRAFIA DE TORAX (PA)" foi ligada e o exame seguiu para a
  worklist do RX do CDT. Os outros 5 pendentes do mesmo tipo (3 do Hospital Santo Antônio, 2 do
  Ernesto Che Guevara) têm `proxima_tentativa_em` nulo e ficaram parados sem gerar erro — a mesma
  propriedade descrita acima, que é o que permite ligar um tipo sem respingar nas unidades sem
  aparelho.

## Custo estimado

Migration + entidade + service + backfill: pequeno. O grosso é front (a aba nova e o enxugamento do
formulário) e a troca dos pontos que hoje leem `TipoExame.EnviarParaWorklist`
(`EnviadorWorklistService.cs:77`, `SolicitacoesExameService.cs:655` e `:1212`,
`ImpedimentoEnvioPacs`). São poucos pontos, todos com teste possível.
