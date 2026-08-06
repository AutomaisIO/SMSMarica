# ADR-0042 — SER como segunda fonte de regulação: espelho antes de promoção

- **Status:** aceito
- **Data:** 2026-08-05
- **Contexto:** ADR-0021 (ecossistema de solicitação), ADR-0040 (motor diário de
  varredura), ADR-0001 / ADR-0007 (schemas)

## Contexto

O **SER — Sistema Estadual de Regulação** (SES-RJ, `ser.saude.rj.gov.br`) é a segunda
fonte de regulação a entrar no ecossistema, depois do SISREG. É por ele que Maricá
pede ao Estado consultas e exames que o município não executa: hoje há **1.484
solicitações "Em fila"** (medição de 05/08/2026), a mais antiga de 2016.

O SER não tem API. O acesso é por *web scraping* de uma aplicação **JSF 1.2 +
RichFaces 3.3.3 sobre JBoss Seam** — protocolo mapeado no laboratório
`Automais.SER/` e documentado em [`docs/ser.md`](../ser.md).

Três características do SER moldam tudo o que vem abaixo:

1. **Não existe filtro "alterado desde".** As três telas do módulo filtram por data
   de evento de negócio (solicitação, consulta/exame, agendamento, competência,
   alta) — nenhuma por data de auditoria. **O delta só existe por comparação de
   snapshots.**
2. **A tela corta em 100 registros** (5 páginas de 20). Não é limite do scraper.
3. **`Situação` é obrigatória na busca** — pesquisar sem ela devolve zero. Logo, uma
   varredura completa é sempre *N* passadas, uma por situação.

## Decisão

### 1. Schema `smsmarica`, tabelas `ser_*` — sem schema novo

O SER **não** ganha schema próprio. O ADR-0001/0007 fixa dois schemas, e o critério
que justifica o `fhir` é ser um **modelo canônico diferente**, não "é outro sistema".
O SISREG — mesma classe de coisa — já vive em `smsmarica` (`sisreg_configuracao`,
`sisreg_varredura_agenda`, …). Schema por sistema externo não escala: seriam
`sisreg`, `ser`, `salux`, `klinikos`.

### 2. Espelho fiel primeiro; promoção a `solicitacao` só quando for acionável

As solicitações do SER **não** entram direto em `smsmarica.solicitacao`. Motivos
concretos, não estéticos:

- `Solicitacao.UnidadeExecutanteId` é `Guid` **não-nulo**. Solicitação do SER não tem
  unidade executante em Maricá — quem executa é o Estado. Seria preciso inventar uma
  unidade fantasma.
- O SER não fornece código SIGTAP; fornece **"Recurso" em texto**
  ("CONSULTA EM OFTALMOLOGIA - PLASTICA OCULAR"). `ProcedimentoSigtapCodigo` ficaria
  sempre nulo e `Categoria` não teria de onde ser derivada.
- O vocabulário de situação é outro (Em fila / Pendente / Chegada Confirmada / Alta)
  e não mapeia 1-para-1 em `StatusSolicitacao`.
- **Contaminação:** ~5.000 linhas alheias entrariam em toda query e tela que lê
  `solicitacao` — worklist, recepção, gate do PACS, indicadores. Cada uma precisaria
  de um filtro novo.

Promover é sempre possível; despoluir depois, não. A promoção acontece quando a
solicitação vira acionável para o município (agendada → avisar o cidadão, TFD).

### 3. Chave externa qualificada pela fonte

Quando a promoção for implementada, a ligação **não** reutiliza
`Solicitacao.CodigoSolicitacao`. O índice único hoje é global:

```
codigo_solicitacao IS NOT NULL AND codigo_solicitacao <> '0000' AND excluido_em IS NULL
```

Os IDs do SER são numéricos de 7 dígitos (`3968616`, `1555533`); os códigos SISREG
são maiores ou iguais a 9999. **Colisão é possível.** A ligação correta é uma tabela
`solicitacao_origem_externa (solicitacao_id, fonte_id, codigo_externo, raw)`, única
por `(fonte_id, codigo_externo)` — que de quebra aposenta `Solicitacao.RawSisreg`,
nome herdado do primeiro conector. É a mesma lição já registrada em
`PepSincronizacaoEstado`, cujos nomes de fase foram neutralizados quando a segunda
base de PEP chegou.

*(Esta tabela não entra na primeira entrega — fica registrada como a forma correta
quando a promoção for implementada.)*

### 4. Uma tabela para todas as situações

`ser_solicitacao` guarda **todas** as situações; o que varia é a coluna `situacao`.
Não há tabela por status. A varredura roda uma passada por situação apenas porque o
SER exige o filtro — o destino é o mesmo.

### 5. Bisecção adaptativa por Data da Solicitação

`Data da Solicitação` é imutável (data de criação), logo serve de eixo estável de
fatiamento. O motor pesquisa uma faixa; se o datascroller mostrar 5 páginas (o teto),
parte a faixa ao meio e refaz. Faixa de 1 dia que ainda estoure é fatiada por `Tipo`
(CONSULTA/EXAME). O que ainda assim não couber é **registrado como truncado** —
nunca truncamos em silêncio.

### 6. Histórico: quando reler

| Gatilho | Ação |
|---|---|
| Solicitação nova | lê o histórico inteiro |
| Mudou de situação | relê o histórico e faz diff dos eventos |
| Está em `EM_FILA` | **relê o histórico todo dia**, mesmo sem mudar de situação |

A terceira linha é a que custa caro e é a que não dá para evitar: **FollowUP não
muda a situação** (`Em fila -> Em fila`). Sem reler, tentativa de contato com paciente
é invisível.

**Por que reler só `EM_FILA` é suficiente.** O menu *Opções* oferece "Registrar
FollowUP" também em solicitações canceladas, o que sugeriria um furo. Não é, por duas
razões operacionais:

1. **O FollowUP precede o cancelamento.** As tentativas de contato acontecem enquanto
   a solicitação está em fila; o cancelamento é a consequência delas. Logo o evento é
   capturado na releitura diária do `EM_FILA`, antes de a situação mudar.
2. **Cancelada volta para `EM_FILA`.** O descancelamento é comum (visto em produção:
   `15/05/2024 Cancelar` → `13/06/2024 Solicitar`, descancelamento por deliberação da
   CIB). Voltar é mudança de situação, o diff pega, e a regra da segunda linha manda
   **reler o histórico inteiro** — como `ser_evento` é único por
   `(solicitação, data, evento)`, tudo que tenha sido registrado durante a janela
   cancelada entra nesse momento.

Ou seja: a releitura por mudança de situação é a **rede de segurança** da releitura
diária. Nada se perde; no pior caso, um FollowUP registrado durante o período
cancelado só é conhecido quando a solicitação retorna à fila.

### 6.1 Máquina de estados: `EmFila` é o centro; `Alta` é terminal

**Qualquer situação pode voltar para `EmFila` — exceto `Alta`.** Cancelada volta por
descancelamento, pendente volta por resolução de pendência, agendada volta se a
marcação cair. `Alta` é o único estado absorvente.

Duas consequências de projeto:

1. **Toda solicitação vista em `EmFila` precisa ser comparada com a situação anterior**
   antes de decidir o que fazer: se mudou, é transição (gatilho + releitura de
   histórico com diff); se já era, é releitura diária (caça ao FollowUP). É o mesmo
   ramo condicional, e é por isso que `ser_solicitacao` guarda
   `situacao_anterior` + `situacao_mudou_em`.

2. **`Alta` fecha a porta do histórico para sempre.** Solicitação em Alta não oferece
   "Histórico da Solicitação" no menu *Opções*, e como o estado é absorvente ela nunca
   mais voltará a um estado que ofereça. **A nossa cópia em `ser_evento` passa a ser o
   único registro daquela trilha.** Isso transforma a releitura diária do `EmFila` de
   otimização em *captura enquanto é possível*.

   *Pendência de investigação:* a tela **Consulta → Histórico de Consulta/Exame**
   (`historico-pesquisar.seam`) tem coluna "Ação/Histórico" própria e pode oferecer a
   trilha de solicitações em Alta. Não foi testada. Se oferecer, o ponto 2 deixa de ser
   irreversível.

Custo medido (05/08/2026): **0,617 s por solicitação** (2 requisições — pesquisar por
`ID Solicitação` reusando o form + abrir o histórico). 1.484 em fila é cerca de
15 min; 5.000 é cerca de 50 min.

**Solicitações em `Alta` não têm histórico** — o menu *Opções* não oferece o item. O
motor registra `HistoricoIndisponivel` em vez de fingir que leu.

### 7. Gatilhos: estrutura agora, consumo depois

`ser_gatilho` nasce nesta entrega **sem consumidor**. Registra `MudancaSituacao` e
`NovoFollowUp` com o payload do que mudou. A decisão de existir desde já é
deliberada: o dado que dispara o gatilho é justamente o que se perde se não for
capturado no momento da varredura.

### 8. Somente leitura, com trava estrutural

O motor **não escreve no SER**. A trava tem duas camadas, porque uma só não cobre
JSF: bloqueio por **nome de parâmetro** e por **rótulo visível do componente**. O
menu *Opções* de cada linha tem `Editar`, `Cancelar` e **`Registrar FollowUP`** —
este último tem id opaco (`j_id169`) e o verbo só existe no texto do elemento.

## Consequências

**Positivas**

- Re-varrer é idempotente: o espelho é fiel e chaveado por `id_ser`.
- Zero risco aos fluxos existentes — nenhuma linha do SER aparece em `solicitacao`.
- A trilha de eventos (`ser_evento`) responde "quando foi cancelado/agendado/
  pendência/alta/contato" com usuário, IP e observação — dado que o SISREG não dá.
- Os dados cadastrais que vêm no histórico (nome da mãe, nascimento, endereço, três
  telefones incluindo WhatsApp) alimentam o hub FHIR e a dedup por CPF/CNS.

**Negativas / custos aceitos**

- Duplicação conceitual: uma solicitação regulada pelo Estado existe em `ser_*` e
  (quando promovida) em `solicitacao`. Aceito em troca de não contaminar a execução.
- A varredura completa é O(N/20) requisições — não há incremental possível.
- Depende de HTML: recompilação da página pela SES-RJ quebra os parsers. Mitigado
  procurando componentes por **rótulo** (`title="Pesquisar"`, texto
  `Historico da Solicitação`) em vez de `j_id` fixo.

## Alternativas descartadas

- **Schema `ser` próprio** — contraria ADR-0001/0007 e não escala por sistema externo.
- **Gravar direto em `smsmarica.solicitacao`** — quebra em `UnidadeExecutanteId`
  não-nulo, força unidade fantasma e contamina worklist/recepção/PACS.
- **Incremental por data** — não existe filtro de atualização em nenhuma tela do SER.
- **Reusar `CodigoSolicitacao`** — índice único global; IDs do SER colidem com SISREG.
