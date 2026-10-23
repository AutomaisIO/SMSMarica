# ADR-0081 — Unidade padrão obrigatória: a lotação da pessoa, separada da unidade em que ela entra

- **Status:** Aceito
- **Data:** 2026-10-23
- **Contexto módulo:** Usuários / vínculo usuário↔unidade (`usuario_unidade`)
- **Relacionado:** [ADR-0079](./0079-comunicacao-interna-entre-unidades.md) (Comunicação interna — §2
  muda: participação passa a ser pela unidade padrão), ADR-0037 (escopo por unidade fail-closed, em
  `Core/Common/Unidades/EscopoUnidade.cs` — não muda), [ADR-0047](./0047-posse-de-conversa-filas.md) (Central de
  Atendimento — a conversa herda a unidade padrão de quem a assume, como já herdava a "principal")

## Contexto

O vínculo usuário↔unidade (`usuario_unidade`) serve a duas coisas que, até aqui, andavam juntas:

1. **Escopo de dados** — de quais unidades a pessoa enxerga os pedidos (ADR-0037). O técnico de
   regulação é vinculado a quase todas as 56 unidades porque trabalha os pedidos de todas.
2. **Identidade na equipe** — de qual unidade a pessoa "é". A Comunicação interna (ADR-0079) derivava
   isso de **todos** os vínculos: em 23/10/2026 os 28 técnicos de regulação apareciam na equipe de
   cada uma das 56 unidades e receberiam a conversa de todas elas.

A marca `principal` existia, mas era opcional (90 dos 123 usuários ativos estavam sem ela) e fazia
outro papel: decidir em qual unidade a pessoa **entra** no painel. Os 18 técnicos que a tinham
apontada para a Central de Regulação entravam filtrando a fila por ela.

O pedido do Bernardo (23/10/2026): todo usuário tem uma unidade padrão, obrigatória; no chat interno a
pessoa aparece só nela; e a regulação ganha uma unidade de lotação que **não existe no SISREG** — o
Complexo Regulador — onde ficam os técnicos de regulação, o perfil "Regulacao" e os de acesso global.

## Decisão

1. **A unidade padrão é a lotação da pessoa, e é obrigatória.** No banco continua sendo
   `usuario_unidade.principal` (sem coluna nova); na tela passa a se chamar "Unidade padrão".
   - Gravar os vínculos (`PUT /usuarios/{id}/unidades`) exige ao menos uma unidade e **exatamente uma**
     padrão.
   - Incluir alguém pela tela da unidade faz dela a padrão **se a pessoa ainda não tem**.
   - Remover pela tela da unidade **recusa** a padrão — troca-se no cadastro do usuário.
   - Não há constraint no banco ("toda linha de `usuario` tem uma padrão" não cabe num CHECK);
     a regra é dos caminhos de escrita. Quem ficou sem padrão aparece no filtro
     **"Sem unidade padrão"** da tela de Usuários, e o próximo salvar do cadastro exige escolher.
2. **"A única" deixa de valer como padrão.** `ObterPrincipalIdAsync` devolve só a marcada. A migration
   `UnidadePadraoObrigatoria` marca, uma vez, quem tinha uma unidade só e nenhuma marcada (o que o
   sistema já presumia). Quem tem várias e nenhuma marcada fica para o administrador escolher.
3. **Comunicação interna: cada pessoa é de uma unidade só — a padrão** (muda o ADR-0079 §2). A lista de
   pessoas, a participação em "Todos da unidade", o grupo do hub, o contador de não lidas e a unidade de
   onde a pessoa fala vêm da padrão. Os demais vínculos não contam no chat. O "falando como" deixa de
   existir. Em 23/10/2026 não havia nenhuma conversa interna gravada — nada de histórico mudou de dono.
4. **A unidade em que a pessoa entra no painel não é mais decidida pela padrão.** Com uma unidade só,
   entra nela; com várias, entra em **"Todas as unidades"**, sem tela de escolha (troca-se no
   cabeçalho, como antes). Sem isso, o técnico lotado no Complexo Regulador — que não tem pedido
   nenhum — entraria com a fila vazia. A tela "Escolha a unidade" do login sai.
5. **O Complexo Regulador é dado da instância, não código.** É cadastrado como unidade comum, sem CNES
   (não está no SISREG; a unidade do TFD já vive assim desde 29/09 sem efeito nos sincronismos), e a
   atribuição da padrão às pessoas é uma operação de dados em produção — nada disso entra em migration
   (CLAUDE.md, regra 9: seed institucional nunca vai para migration).

## Consequências

- **Escopo não muda.** O técnico continua enxergando os pedidos de todas as unidades a que é vinculado;
  ganhar o Complexo como padrão só acrescenta um vínculo.
- **Central de Atendimento:** a conversa que um técnico inicia ou assume sem unidade passa a ficar no
  Complexo Regulador (antes, na Central de Regulação). Os demais técnicos lotados lá a enxergam.
- **Quem ainda está sem padrão** (contas sem perfil, médicos importados, multi-unidade sem marca) não
  aparece em unidade nenhuma do chat — só na conversa direta — até o administrador escolher.
- **Motoristas** (usuário com `MotoristaId`) ficam de fora: não usam o painel nem a tela de Usuários.
- Trocar a padrão vale para o tempo real do chat depois que a aba reconecta (o grupo do hub é lido na
  conexão); as listas já refletem na hora.
