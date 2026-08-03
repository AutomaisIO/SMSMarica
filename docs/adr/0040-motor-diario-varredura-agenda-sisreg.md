# ADR-0040 — Motor diário de varredura da agenda do SISREG, por unidade

- **Status:** aceito
- **Data:** 2026-08-03
- **Contexto relacionado:** [ADR-0012](./0012-agendamento-local-e-sisreg.md) (SISREG só-leitura),
  [ADR-0021](./0021-ecossistema-solicitacao-e-fulfillment.md) (pendência de mapeamento),
  [ADR-0035](./0035-pendencia-importacao-primeira-classe.md) (pendência de importação de 1ª classe)

## Contexto

As solicitações do SISREG só entravam no sistema por **upload manual de arquivo** (o TXT/CSV do
`expo_solicitacoes`), unidade por unidade. Não escala para a rede da SMS.

As peças para automatizar já existiam pela metade: a tela de Mapeamento SISREG cadastrava
credencial por unidade e listava profissionais com seus procedimentos, com checkbox nos dois
níveis. Mas **nada consumia esses checkboxes** além do "Sincronizar profissionais (FHIR)". A tela
até exibia "Requisições por varredura" — de uma varredura que nunca foi escrita.

## Decisão

Um motor diário, **por unidade**, que varre a agenda do SISREG pela tela `cons_agendas` e entrega
as marcações ao mesmo fluxo de importação do TXT.

### 1. A fonte é a exportação de arquivo (`expo_solicitacoes`)

> **Revisto em 03/08/2026.** A primeira versão raspava a tela de agenda (`cons_agendas`), porque o
> `expo_solicitacoes` nunca tinha sido aberto com sucesso — a única captura era o bloqueio
> *"Aplicativo bloqueado para uso de 8 as 15 horas"*. Aberto fora do horário, ele se mostrou
> **estritamente melhor**, e a fonte foi trocada.

Medido no CDT, mesmo par profissional × procedimento, 419 registros em julho:

| | Raspagem (`cons_agendas`) | Exportação (`expo_solicitacoes`) |
|---|---|---|
| Requisições | 9 (paginado de 50) | **1** |
| Código SIGTAP | não informa | **coluna 2** |
| Datas de solicitação e regulação | não informa | sim |
| Endereço do paciente | não informa | sim |

A exportação é **9× mais barata no recurso escasso** (o orçamento anti-robô) e mais rica. E reusa o
`AgendaTxtParser`, que já roda em produção no upload manual — validado contra o arquivo real:
91/91 marcações, 0 rejeitadas, SIGTAP em todas.

Ela exige profissional e procedimento, como a tela de agenda. Logo, varrer = percorrer o produto
cartesiano dos pares habilitados — e é isso que dá sentido aos checkboxes: **eles são a régua de
custo**.

Duas restrições medidas, ambas tratadas em código:

- **Teto de 700 registros por exportação.** Intervalos de 61 e de 212 dias devolveram exatamente
  700. É truncamento **silencioso** (o cabeçalho diz 700, as linhas são 700), então ao bater no
  teto a janela é partida ao meio e reconsultada.
- **Código de grupo não devolve nada.** `GRUPO - MAMOGRAFIA` retorna 0 mesmo no período em que o
  item individual retorna 419 — diferente do `cons_agendas`, aqui o grupo não agrega. A varredura
  pula códigos terminados em `000` e loga, em vez de gastar requisição à toa.

### 2. O SIGTAP vem no arquivo; o código do SISREG é só filtro

O arquivo traz, lado a lado, o código do SISREG (coluna 1) e o **SIGTAP** (coluna 2) — dito pelo
próprio SISREG, não inferido. Isso dissolve o problema que dominou o desenho anterior.

Duas versões descartadas, registradas porque o raciocínio se repete:

1. **Portão antes de varrer** (procedimento sem de-para confirmado não era varrido). Obrigava a
   mapear 116 procedimentos que talvez nunca tivessem agendamento.
2. **Resolução por nome exato contra o catálogo SIGTAP.** Necessária enquanto a fonte era a
   raspagem, que não informa SIGTAP. Com a exportação, é redundante para o caminho feliz.

Vale a régua de sempre: sem SIGTAP, `CategoriaSigtap.Resolver("")` devolve `Outro`, a solicitação
nasce sem satélite de imagem e sem worklist — **e marcada como sucesso**. Por isso a marcação sem
SIGTAP vira pendência `SigtapNaoMapeado` em vez de entrar torta. Com a exportação isso passa a ser
exceção, não regra.

Como a pendência é por solicitação e a correção é por procedimento, a aba de Erros agrupa as de
causa `SigtapNaoMapeado` por procedimento — 1 linha "MAMOGRAFIA BILATERAL — 200 solicitações" — com
mapeamento e revalidação em lote. Sem isso, resolver 200 pendências idênticas seria clicar 200
vezes, e elas afogariam as pendências que exigem olhar caso a caso.

**Nunca há resolução automática de SIGTAP abaixo de igualdade exata de nome**, onde ela ainda
acontece. SIGTAP errado não estoura em lugar nenhum: vira worklist errada, exame errado no PACS e
laudo no lugar errado, semanas depois.

### 3. A ingestão é aditiva — o caminho do TXT não muda

`IImportacaoSisregService` ganhou **um** membro (`ImportarMarcacoesAsync`), `RegistrarFalhaExecucaoAsync`
ganhou um parâmetro opcional (`origem`), e `ReplicarFalhaAsync`/`ObterFalhaDetalheAsync` ganharam
um branch cada. O núcleo `ExecutarMarcacaoAsync` — idempotência por nº do SISREG, resolução
CNS→local→CADSUS→CPF, criação de solicitação, fila de WhatsApp — **não foi tocado**. Extraí-lo para
um serviço próprio era refatorar 190 linhas em produção sem ganho para o motor.

A proveniência vira `RegistroVarreduraRaw`, envelope JSON versionado gravado no mesmo `LinhaRaw`
que o TXT usa. **O envelope guarda o `pa`, nunca o SIGTAP**: o de-para é *configuração*, não
observação, e é resolvido de novo no reprocesso. É isso que faz o botão "Validar" funcionar depois
que o operador confirma o SIGTAP que faltava.

### 4. `ProximoRunEm == null` significa NÃO elegível

Inverso da agenda do PEP ([ADR-0024](./0024-sincronismo-continuo-salux.md)), e de propósito. Para
"a cada 30 minutos", tratar null como "elegível já" é inofensivo. Para "diário às 04:30", faria o
primeiro tick depois de um deploy varrer no meio da tarde — **derrubando a sessão do atendente**,
porque o SISREG aceita uma sessão por operador. Quem liga a agenda calcula o próximo horário ao
salvar.

### 5. Rastreio próprio, com status `Parcial`

`SisregVarreduraExecucao` não reusa `SisregImportacaoExecucao`: aquela é varrida por
`ImportacaoLoteService.LimparOrfasAsync`, que marca como erro toda linha pendente/em execução **sem
filtrar por lote** — um upload manual de TXT mataria a linha de uma varredura em curso.

O status `Parcial` existe porque marcar "Concluída" uma varredura interrompida é operacionalmente
perigoso: o operador conclui que o dia está importado e não está.

### 6. Orçamento por unidade, e a incerteza declarada

O SISREG dispara reCAPTCHA por volta de **700 requisições**; relogar não resolve, só um humano no
navegador com aquele operador. **Não sabemos se o limite é por operador ou por IP** — o laboratório
nunca isolou as duas variáveis. Adotamos "por operador" (cada unidade tem sua credencial, logo seu
orçamento), porque relogar com sessão nova no mesmo IP não limpa o bloqueio e o destravamento é
amarrado ao operador.

Proteções: teto de 500 requisições por execução, pausa de 350 ms entre requisições, **uma varredura
por vez em toda a instalação** (as unidades saem pelo mesmo IP via túnel WireGuard), e CAPTCHA →
`Parcial` com cursor de retomada, preservando tudo que já entrou (a importação acontece a cada
página, não no fim).

**Se o limite for por IP**, o sintoma será `SISREG_VARREDURA_CAPTCHA` em unidades diferentes na
mesma noite. A correção então é espaçar as horas e/ou acrescentar um teto global — o código já está
estruturado para isso.

### 7. Confirmação ao paciente vira decisão explícita, em dois níveis

Até aqui, **toda** marcação importada enfileirava WhatsApp — sem controle nenhum
(`ImportacaoSisregService`, chamada incondicional a `EnfileirarAsync`). Agora há dois gates,
combinados por "E":

- **Gatilho mestre da unidade** (`sisreg_varredura_agenda.enviar_confirmacao`) — vale para toda
  importação da unidade. Por isso a linha pode existir numa unidade que nunca varre, e a entidade
  deixou de ser "só agenda" para ser a configuração SISREG da unidade.
- **Por procedimento, dentro da unidade** (`sisreg_procedimento_profissional.enviar_confirmacao`) —
  ao lado do `habilitado`, na mesma linha. **Não é decisão nacional**: quem decide se um exame
  merece aviso é a unidade que o executa, e duas unidades podem decidir diferente para o mesmo
  procedimento. (O de-para SIGTAP continua global — aquele *é* nacional.)

Como o mesmo procedimento aparece sob vários profissionais da unidade, o serviço aplica a mudança
a **todas** as linhas daquele código na unidade e devolve quantas foram afetadas. Sem isso, o
operador desligaria o aviso num profissional e continuaria enviando pelos outros, sem nada indicar.

**Os dois nascem ligados, e ausência de configuração significa ENVIAR.** Unidade sem linha e
procedimento fora do mapeamento continuam enviando. Um gate novo que silenciasse por omissão
quebraria o combinado com o paciente — validado em produção desde 06/07 — sem que nenhuma tela
mostrasse.

A importação por ARQUIVO não conhece o `pa`; para ela os códigos equivalentes são resolvidos pelo
de-para a partir do SIGTAP, para o mesmo exame não se comportar diferente conforme o caminho pelo
qual entrou. Esse caminho está em extinção, mas enquanto existir respeita o mesmo gatilho.

### 8. Janela 22:00–06:00 é recusa, não aviso

Sessão única por operador: o motor rodando às 10h com a credencial da unidade **derruba o
atendente**. `SalvarAgendaAsync` recusa hora fora da janela.

## Consequências

**Ganhamos** um dado que o TXT não dava: **data de nascimento** do paciente (a mensagem "o SISREG
não informa a data de nascimento" deixa de valer para pendências de varredura), além de situação do
agendamento, vaga e profissional executante.

**Perdemos**, e isso é degradação declarada, não silenciosa: `DataSolicitacao`/`DataRegulacao`
(existem só no TXT), CPF/nome do médico solicitante, e endereço/IBGE. Buscar a ficha detalhe para
preencher custaria **+1 requisição por agendamento** — nas 753 marcações do CDT, +753 requisições,
estourando o orçamento sozinho.

A situação do SISREG (`Pendente Confirmação`/`Confirmado`/`Falta`) **não** é mapeada para o nosso
`StatusConfirmacao`: aquele é a confirmação do **paciente** por WhatsApp/app
([ADR-0034](./0034-cancelamento-pelo-paciente-como-sinal.md)), este é a recepção registrando
comparecimento. Coisas diferentes.

`MarcadosRegScraper`/`MarcadosRegParser` foram **apagados**: zero chamadores, e desenho errado para
o problema (mira a visão do solicitante e faz uma requisição de ficha por agendamento).

A tela `/app/sisreg/mapeamento` foi **aposentada** — vira redirect. Credencial, mapeamento, de-para
SIGTAP e sincronismo agora vivem na aba **SISREG** do detalhe da unidade.

## Alternativas descartadas

- **Baixar o TXT automaticamente** — bloqueado das 8h às 15h, parâmetros do formulário desconhecidos
  (nunca foi aberto), e granularidade de unidade inteira, incompatível com a seleção por
  profissional/procedimento.
- **Coluna `codigo_sigtap` por par profissional×procedimento** — obrigaria 256 decisões no CDT para
  ~40 `pa` distintos, refeitas em cada unidade, e permitiria o mesmo `pa` mapeado para dois SIGTAPs.
  Se a premissa de que o `pa` é nacional se mostrar errada, o sinal é o log `SISREG_PA_DIVERGENTE`.
- **Extrair `ExecutarMarcacaoAsync` para serviço próprio** — refatoração de código em produção, sem
  cobertura unitária do núcleo, para um ganho que o motor não precisa.
