# ADR-0052 — A solicitação de regulação nasce no SMSMais como entidade própria, agnóstica de destino, e passa por um agente regulador antes de existir em qualquer sistema externo

- **Status:** aceito — implementado nos incrementos 2 e 3 (07/09/2026)
- **Data:** 04/09/2026
- **Contexto:** [ADR-0021](./0021-ecossistema-solicitacao-e-fulfillment.md) (espinha `solicitacao` das marcações importadas), [ADR-0042](./0042-ser-segunda-fonte-de-regulacao.md) (SER como espelho fiel, sem promoção), ADR-0037 (escopo por unidade fail-closed), ADR-0039 (unidade como eixo durável).
- **Documento de apoio:** `SMSMais.Regulacao/README.md`, planos 02, 04, 05.

## Contexto

O SMSMais lê três sistemas de regulação e guarda espelhos fiéis de cada um: `solicitacao` (marcações do SISREG importadas), `ser_solicitacao`, `sernit_solicitacao`. Nenhum deles é o **pedido antes de existir** — a solicitação que a UBS quer fazer, com regras conferidas e documentos anexados, e que ainda não tem número em lugar nenhum.

Existem hoje dois rascunhos locais, um por sistema (`ser_solicitacao_rascunho`, `sernit_solicitacao_rascunho`), sem unidade solicitante, sem autor visível, sem triagem e sem envio. Copiá-los para o SISREG faria a terceira cópia e perpetuaria a divisão por sistema que o módulo veio acabar.

A transcrição do Bernardo pede uma fila única ("pré-regulação"), um agente regulador que vê todas as unidades e ajusta com histórico, e que só depois do OK dele a solicitação exista no sistema de regulação — com a exceção do fluxo interno, em que o próprio solicitante inclui no SISREG com a credencial dele (D-8) e a fila guarda "já no SISREG, aguardando OK".

## Decisão

### 1. Uma entidade própria: `regulacao_solicitacao`

Agnóstica de destino: fluxo (Interno / Externo / NAR), unidade solicitante, autor, "em nome de" (NAR), paciente, procedimento canônico, formulário em JSON com a versão do catálogo, estado, agente responsável, número externo e a trilha de "com qual credencial, em nome de quem". Prefixo `regulacao_` em tudo para não colidir com `solicitacao` (ADR-0021).

### 2. FK unidirecional para os espelhos

`regulacao_solicitacao` aponta (FK nullable, no máximo uma setada) para `solicitacao`, `ser_solicitacao` ou `sernit_solicitacao` quando o número externo é conciliado. **Nunca o inverso**: os espelhos continuam fiéis ao sistema externo (ADR-0042), e o SER/SERNIT continuam registrando "gestor SMS Maricá" como solicitante — a unidade real só existe deste lado do vínculo.

### 3. Máquina de estados com histórico append-only

`Rascunho → PendenteRegulacao → EmAnalise → EnviandoAoSistema → EnviadaAoSistema → EmFilaExterna → Agendada → Concluida`, com `Devolvida`, `FalhaEnvio`, `Cancelada`, `Recusada`. Cada transição grava `regulacao_evento` (quem, papel, unidade, jti, diff). O agente pode ajustar; o ajuste fica no histórico com o diff.

**O papel faz parte da transição**, e não é um filtro aplicado depois: `PendenteRegulacao → EmAnalise` existe para o agente e só para ele — com o par de estados sozinho, um solicitante assumiria o próprio caso e furaria a triagem.

**Correção do rascunho (07/09/2026):** este ADR dizia que "o estado externo nunca regride além de `EmFilaExterna`". Está errado, e a implementação faz o contrário: `Agendada → EmFilaExterna` é justamente o que acontece quando o sistema de regulação **desmarca**. A conciliação lê o *espelho*, que guarda a situação atual da última varredura — não um histórico fora de ordem —, então travar a volta faria a ficha dizer "agendada" para um paciente que perdeu a vaga, sem ninguém descobrir pela tela. Quem impede o absurdo é a própria máquina: de `Concluida`, `Cancelada` e `Recusada` não sai transição nenhuma.

**A varredura pode pular estados.** Ela lê o estado atual, não a sequência: entre o nosso envio e a primeira leitura, o caso pode já estar agendado, concluído ou cancelado. Por isso `EnviadaAoSistema` transita direto para qualquer um dos três.

### 4. Agente regulador é módulo, não perfil

Reaproveitamos o bloco reservado e nunca implementado do enum: `Regulacao` (47) para o solicitante, escopado por unidade via `EscopoUnidade`; `RegulacaoTriagem` (48) para o agente, que amplia o escopo para todas as unidades e libera as ações de triagem e envio; `RegulacaoConfiguracao` (51) para regras, catálogo e configuração. Mesmo idioma de `Conversas` + `ConversasSupervisao`. Os valores 49 e 50 seguem reservados.

### 5. Deprecação dos rascunhos por sistema

Os rascunhos existentes são migrados por job idempotente para `regulacao_solicitacao` (fluxo Externo), as telas antigas viram somente-leitura e as tabelas são removidas uma release depois. Não ficam como satélite: duas verdades para "pedido que ainda não existe" recriariam o problema.

### 6. CPF obrigatório na transição para a fila, não no cadastro

O ADR-0041 continua valendo (paciente sem CPF entra marcado). O módulo só não **regula** sem CPF, porque o SERNIT não grava sem ele e os demais precisam dele para casar identidade.

## Consequências

- Um só wizard e uma só fila para os três sistemas; o front extrai o núcleo comum (`shared/regulacao/`) em vez de clonar `features/ser` pela terceira vez.
- As notificações do SER/SERNIT passam a ter unidade solicitante real — via o vínculo — e podem ser filtradas por "minha unidade".
- O incremento 3 entrega valor sem escrita externa: o agente registra o número que incluiu à mão; os incrementos 5 e 7 trocam o botão por envio automático sem mudar o modelo.
- O enum de permissões não cresce; os comentários do bloco 47–51 foram reescritos (citavam um "ADR-0024" que é de outro assunto, o sync contínuo Salux→FHIR).
- **A notificação não é uma tabela de avisos**: é uma leitura da própria trilha, filtrada pelos tipos que pedem atenção (`NumeroExterno`, `SituacaoExterna`, `Devolucao`, `Recusa`, `FalhaEnvio`, `PendenciaAberta`). Duplicar o fato criaria duas versões da mesma história, que divergem no dia em que uma escrita falha. O "visto" é por usuário (`regulacao_evento_visto`) — a mesma movimentação interessa a quem abriu o pedido e ao agente, e um não pode apagar o aviso do outro.
- **O claim do agente é uma gravação condicional** (`UPDATE … WHERE status = PendenteRegulacao`), não um "leia e escreva": dois agentes clicando junto passariam os dois pela leitura, e o segundo sobrescreveria o primeiro sem ninguém notar.

## Alternativas consideradas

- **Estender `ser_solicitacao_rascunho` com colunas de unidade/estado e clonar para o SISREG.** Rejeitado: três tabelas, três telas, três máquinas de estado; e o fluxo NAR/Interno não cabe num rascunho "do SER".
- **Promover a pré-regulação para `solicitacao` (ADR-0021).** Rejeitado: `solicitacao` exige unidade executante não nula e nasce de marcação já feita; misturar "pedido" com "marcação" contamina relatórios e o PACS.
- **Perfil de acesso "Agente regulador".** Rejeitado: perfil é bag de módulos; o que se quer é um módulo a mais em qualquer perfil, como já se faz com supervisão de conversas.

## Verificação

Solicitação aberta por uma UBS não aparece para outra; aparece para o agente; cada ajuste do agente tem evento com diff; número registrado casa com o espelho na varredura seguinte e a notificação chega só à UBS de origem.

**Verificado em 07/09/2026** por 56 testes automatizados no módulo (escopo fail-closed, agente vendo tudo, claim concorrente com dois `DbContext`, diff do ajuste, transições inválidas recusadas, número externo duplicado barrado pelo índice único) e pela execução em produção: catálogo canônico com 999 procedimentos, migração dos rascunhos legados concluída e telas antigas fechadas (410) às 17:04:08Z.
