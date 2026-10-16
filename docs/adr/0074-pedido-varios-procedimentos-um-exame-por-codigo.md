# ADR-0074 — Pedido do SISREG com mais de um procedimento: um exame por código interno, número de acesso com sufixo, código interno no (0008,0100)

**Status:** aceito · **Data:** 2026-10-16 · **Implementação:** branch `feat/rx-varios-procedimentos`
**Relacionado:** [ADR-0021](./0021-ecossistema-solicitacao-e-fulfillment.md) (solicitação como espinha, satélite de imagem) ·
[ADR-0056](./0056-escopo-de-exame-por-unidade.md) (tipo × unidade decide worklist e aparelho) ·
[ADR-0012](./0012-agendamento-local-e-integracao-sisreg-leitura.md) (SISREG só leitura) ·
ticket #149 (pedido com vários procedimentos) · [`docs/pacs.md`](../pacs.md)

## Contexto

O fornecedor do raio-X vai cadastrar os procedimentos no aparelho e escolher o protocolo pelo código que
chega na worklist, no campo (0008,0100). Para decidir **qual código** mandar e **o que fazer quando um
pedido traz mais de um procedimento**, foi medido em 16/10/2026, nos 1.462 arquivos exportados do
SISREG que temos guardados (1.016.979 linhas, de 2019 a 2026) e no banco de produção:

**O código interno do SISREG** (coluna 1 da linha exportada; a coluna 2 é o SIGTAP):

- vem em **100%** das linhas exportadas;
- cada um dos 1.127 códigos tem **uma descrição só e um SIGTAP só**, sem exceção;
- o SIGTAP é mais genérico: 120 dos 300 SIGTAPs têm mais de um código interno (direito e esquerdo,
  variações do mesmo exame, tabela antiga). O SIGTAP não diferencia lado, o código interno diferencia;
- na nossa base, 612 de 1,05 milhão de pedidos estão sem código (lançados à mão, varredura da agenda,
  linhas antigas). **Dos pedidos agendados daqui para a frente, nenhum.** A nota "~1/3 vem vazio" nos
  comentários do código é de uma amostra de 10/08/2026 e não vale mais.

**Pedidos com mais de um procedimento** (o mesmo número do SISREG em várias linhas, uma por código):

- são 2.986; em **todos**, as linhas têm a mesma data, a mesma hora, a mesma unidade executante e a
  mesma situação de chegada;
- o SISREG trata o pedido como **um só**: cancelar, marcar presença ou falta vale para todas as linhas,
  nunca para uma;
- no raio-X os procedimentos do mesmo pedido são sempre do próprio raio-X.

**Raio-X, lado direito e esquerdo:**

- quase sempre chegam como **pedidos separados**, um número e um código por lado: 4.275 casos do
  mesmo paciente no mesmo dia. No mesmo pedido, só 4 em todo o histórico;
- coluna nunca veio com dois segmentos no mesmo pedido (7.635 pedidos);
- 91 pedidos usam **um código só que já é dos dois lados** (quadril D e E `1304129`, ATM bilateral
  `1304005`, fêmur D e E `1304127`, órbitas `1304003`).

**Como está hoje:**

- uma solicitação tem no máximo **um** exame de imagem (índice único em `exame_imagem.solicitacao_id`),
  e uns 30 arquivos do backend contam com isso;
- quando a segunda linha de um pedido chega, ela **substitui** a primeira (é o ticket #149, cuja correção
  — `solicitacao_procedimento` — existe só localmente);
- a worklist **não envia** o (0008,0100): o campo `tipo_exame.codigos_protocolo` existe e nada o lê;
- o número de acesso é `AAMMDD` + sequência do dia (`GeradorIdentificadores.ProximoAccessionAsync`);
- o laudo é amarrado ao estudo (`laudo.study_instance_uid`);
- a sincronização não manda nada ao PACS: o exame só vai à worklist quando a recepção **autoriza** o
  pedido, com o paciente no balcão (`POST /solicitacoes/{id}/autorizar`).

## Decisão

1. **O código do procedimento para o aparelho é o código interno do SISREG.** Ele vai em (0008,0100),
   com o esquema em (0008,0102) e a descrição em (0008,0104).
   - **Liga-se por equipamento, e nasce desligado.** O cadastro do equipamento diz se ele recebe o
     código e em que sequência(s): a do procedimento solicitado (0032,1064) e/ou a do protocolo
     agendado (0040,0008). Desligado, o item sai **byte a byte** como antes — os outros DX, os US, os
     MG e o Fuji não mudam. Ao ligar, as duas sequências vêm marcadas: mandar nas duas evita
     depender de qual delas cada aparelho lê. Destino novo é um valor a mais no campo combinável
     (`DestinoCodigoProcedimento`), sem mexer nos já cadastrados.
   - O primeiro a ligar é o raio-X do CDT, depois do deploy, quando o fornecedor tiver a lista.
   - A descrição sai **sem acento**, pela mesma regra do resto da worklist (o Fuji não aceita acento e
     não declaramos o conjunto de caracteres), com no máximo 64 caracteres, abreviando em vez de
     cortar (`RADIOGRAFIA` → `RX`), para não perder o lado.
   - O código vem do **procedimento que o exame representa**, não do tipo de exame. Pedido sem código
     (lançado à mão) usa o código do tipo de exame; sem nenhum, a sequência não é enviada e o técnico
     escolhe o protocolo no aparelho.
   - Esquema configurável por equipamento; vazio = `99SMS` (esquema privado; os privados começam
     com "99").

2. **Um pedido é uma solicitação; cada código interno é um exame.** O pedido continua sendo **uma
   solicitação só**, como no SISREG, com os seus procedimentos guardados como itens (a correção do
   #149). Cada procedimento gera **um exame de imagem**, com o seu número de acesso, o seu item de
   worklist, o seu estudo e o seu laudo.
   - **Um código é um exame.** Só há mais de um exame quando o SISREG manda mais de um código. Código
     que já é dos dois lados (quadril D e E etc.) continua sendo um exame; não se desdobra.

3. **Número de acesso: o primeiro sem sufixo, os seguintes com `-1`, `-2`, `-3`.** Exemplo:
   `261016019` (joelho D) e `261016019-1` (joelho E), na ordem em que os procedimentos chegaram.
   - **Um número já enviado nunca muda.** Procedimento que chega depois ganha o próximo sufixo.
   - Cabe no limite do campo (16 caracteres) e o gerador de números continua certo: o número base
     sempre existe, então a sequência do dia segue contada.

4. **O que é do pedido fica na solicitação, uma vez só.** Cancelamento, presença, falta, confirmação no
   WhatsApp, chave de confirmação, anamnese, contatos e remarcação são da solicitação e valem para
   todos os exames dela. O exame guarda só a execução: procedimento, número de acesso, estudo, item de
   worklist, aparelho, situação da execução e laudo.
   - Cancelar (pela tela, pela atendente ou pela conciliação com o SISREG), excluir e trocar a unidade
     executante agem em **todos** os exames do pedido; a trava de "já tem imagem" olha todos.
   - A anamnese continua gravada num exame (é a tabela que existe), mas é a do **principal**: o exame
     extra lê e grava a dele, e o laudo do `-1` a encontra (`ExamesDoPedido.ExameDaAnamneseAsync`).

5. **Na recepção, autorizar o pedido envia todos os exames dele à worklist, automaticamente, sem
   modal.** Cada exame vai ao aparelho definido para o seu tipo naquela unidade (ADR-0056). Se um
   procedimento chegar depois de o pedido já estar autorizado, o exame novo também vai à worklist.

6. **Um laudo por exame.** Cada exame tem o seu estudo e o seu laudo. "Criar laudo" e "abrir imagem"
   continuam diretos, sem modal: cada estudo chega ligado ao seu exame pelo número de acesso.

7. **Nas telas, o vínculo fica visível.** A lista pagina por PEDIDO e mostra uma linha por exame vivo,
   cada uma com o seu procedimento, o seu número de acesso e o selo **"vinculado a 261016019"**; os
   filtros de situação, tipo e número de acesso olham todos os exames do pedido. No detalhe, o cartão
   do exame lista **Procedimentos deste pedido** (número, código interno, situação, atalho para o
   irmão). O Manual muda no mesmo commit (artigos *Exames e consultas*, *Anamnese* e o novo
   *Equipamentos de imagem*).

8. **Escopo: raio-X (DX).** Serve para qualquer modalidade em que cada código é um estudo. A tomografia
   com várias regiões no mesmo pedido (330 pedidos, ex.: tórax + abdome) costuma ser **um estudo só**
   para tudo; se a tomografia entrar na worklist, este ponto é revisto.

## Alternativas descartadas

- **Duas solicitações "irmãs", uma por linha.** Tudo o que é do pedido ficaria gravado duas vezes e teria
  de ser copiado a cada ação (cancelar pela equipe, resposta no WhatsApp, recepção, CPF, contatos,
  remarcação). Uma cópia esquecida deixaria uma irmã cancelada e a outra não. Também obrigaria a revisar
  os 54 arquivos que procuram a solicitação pelo número do SISREG e as contagens, que dobrariam.
- **Laudo por estudo, com modal para escolher o estudo ou o procedimento.** Desnecessário: com um item
  de worklist por código, cada estudo já chega ligado ao seu exame pelo número de acesso.
- **Desdobrar os códigos que já são dos dois lados** (quadril D e E nos códigos de cada lado). Decisão:
  um código é um exame.
- **Sufixo começando em `-2`.** Funciona, mas o `-2` faz procurar um `-1` que é, na verdade, o original
  sem sufixo.
- **SIGTAP como código do aparelho.** Não diferencia lado e agrupa exames diferentes.

## Consequências

- **O #149 entra junto** (mesma migration, `ProcedimentosDaSolicitacaoEExamePorCodigo`), porque sem ele
  a segunda linha do pedido se perde.
- **A solicitação passa a ter vários exames sem mudar o sentido de `Solicitacao.ExameImagem`.** Ele
  continua sendo "o" exame do pedido — agora o principal, por uma coluna própria
  (`exame_imagem.solicitacao_principal_id`, única, preenchida só no de ordem 0) — e a lista completa
  fica em `Solicitacao.ExamesImagem`. Assim os ~95 acessos que leem o exame da solicitação (telas,
  confirmações, robô) continuam certos; só o que precisa agir em todos (enviar, cancelar, excluir,
  trocar unidade) passou a usar a lista. A trava `ck_exame_imagem_principal` garante que o principal
  é o de ordem 0 e do próprio pedido.
- **Cadastro de equipamento: "omitido = mantém".** O painel não enviava o limite de descrição, e cada
  edição gravava 64 por cima do 16 do Fuji. O painel passou a enviar, e o servidor trata campo da
  worklist ausente como "mantém".
- **A importação**, ao receber uma linha nova de um pedido existente, cria o procedimento e o exame com o
  próximo sufixo.
- **A conciliação com o PACS não muda:** continua pelo número de acesso exato.
- **Não mudam:** faturamento, confirmação no WhatsApp (uma por pedido), contagens, anamnese (uma por
  pedido).
- A lista para o fornecedor foi gerada em 16/10/2026: 128 códigos da tabela atual (`1304xxx`), sem a
  tabela antiga (`0309xxx`, último pedido em 02/08/2023) e sem a panorâmica (odontológica).

## Pendências (não bloqueiam o ADR)

- **Confirmar com o técnico** que ele inicia um item de cada vez. Selecionar dois itens juntos no
  aparelho pode juntá-los num estudo só.
- **Aparelho que parte um exame em dois estudos:** aconteceu 4 vezes (joelho D duas vezes, joelho E com
  carga, coluna cervical com flexão). Os dois estudos têm o mesmo número de acesso; para caberem num
  laudo só, o laudo precisa passar a ser do exame e não do estudo. Fica para uma segunda etapa.
- **Corrigir os comentários "~1/3 vem vazio"** (`AgendaTxtParser`, `Solicitacao`,
  `ResolvedorTipoExameSisreg`).
- **Histórico:** os 2.986 pedidos antigos que perderam linhas não vão à worklist (são passado). Recompô-los
  pelos arquivos exportados é decisão à parte (ver #149).

## Ordem de implementação

1. Levar o #149 (procedimentos da solicitação) para produção.
2. Vários exames por solicitação: um por procedimento, número de acesso com sufixo, importação criando o
   exame quando a linha nova chega.
3. Recepção: autorizar envia todos os exames do pedido.
4. Worklist: código interno no (0008,0100), nas duas sequências.
5. Telas (selo "vinculado a", procedimentos na solicitação) e Manual.
6. Segunda etapa: laudo por exame, para o caso do aparelho que parte o exame em dois estudos.
