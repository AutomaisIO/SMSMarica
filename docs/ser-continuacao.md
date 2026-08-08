# SER — ponto de retomada (sessões de 05–06/08/2026)

Documento de handoff. Quem retomar deve ler **este arquivo primeiro**, depois
[`docs/ser.md`](./ser.md) (protocolo) e [ADR-0042](./adr/0042-ser-segunda-fonte-de-regulacao.md)
(decisão de arquitetura).

> **Frase para retomar:** *"Continuar o SER a partir de `docs/ser-continuacao.md`."*

---

## 1. Onde estamos

**Em produção** (branch `main`, deployado):

- Tabelas `ser_solicitacao`, `ser_evento`, `ser_gatilho`, `ser_varredura_execucao`,
  `ser_varredura_falha`.
- Motor `SMSMarica.Core/Integracoes/SerWeb/` (sessão JSF/Seam, parsers, varredor, runner).
- Telas `Regulação → SER` e `Regulação → Configuração` (aba SER + aba **SER — consulta direta**).
- Permissões `RegulacaoSer = 54` e `RegulacaoConfiguracao = 51`.

**Branch `feat/ser-varredura-por-export`** (NÃO deployada) — o que a sessão de 06/08 entregou:

| | |
|---|---|
| Varredura da grade **por export de 500** | `Varredura/Export/` — leitor, parser de BIFF8 e varredor por janela adaptativa |
| Ponteiro de retomada **funcionando** | cursor gravado **junto com cada lote**, migration `20260806200219_SerPonteiroRetomadaEExport` |
| `unidade_executora` no espelho | coluna nova; só o export traz esse dado |
| Scheduler diário | `VarreduraSerScheduler`, **desligado por padrão** (`Ser:Varredura:Ativo`) |
| Tela | coluna *Progresso* (fase, cursor, pendentes) + status `Interrompida` em roxo |
| Testes | 25 unitários verdes, incluindo prova de cobertura sob corte de 500 |

**Sessão de 07/08 (tarde):** a amarração do filtro de Solicitante foi decifrada, provada por
sonda e portada para o motor — `SerExportLeitor.AmarrarSolicitanteAsync` + parsers novos em
`SerHtmlParser`, com testes (`SerExportLeitorTests`). Ver o Passo 3 abaixo e `docs/ser.md §4.3`.

**Estado dos dados em produção — continua sem valer para decisão operacional:**

| | |
|---|---|
| Solicitações espelhadas | 14.163 (**~35% incompletas**) |
| Eventos de histórico | **1** de 14.163 |
| Gatilhos na fila | 14.163 (sem consumidor) |

Conferência contra a tela do SER (Bernardo, 06/08): Chegada confirmada 7.723 no SER × 4.895
na base; Chegada não confirmada 3.471 × 2.252; Alta > 0 × **0**.

---

## 2. O que fazer, em ordem

### Passo 1 — ~~Validar a contagem pela consulta direta~~ **INEXECUTÁVEL como estava escrito**

A instrução anterior mandava comparar a contagem da consulta direta com **7.723**. Aquela tela
usava a tela de Solicitação, que **trava em 100 por construção** — 5 páginas × 20. "Bateu no
teto" ali não distingue 101 de 8.000, então nunca daria para conferir cobertura por ela.

Corrigido em 06/08: a consulta direta ganhou um seletor de fonte e usa o **export** por padrão,
onde a contagem é real até 500 e o SER **avisa por escrito** quando corta.

### Passo 2 — ~~Deployar a branch~~ **FEITO em 06/08**

`main` em `1767848`+; migration `20260806200219_SerPonteiroRetomadaEExport` aplicada e conferida
em produção (8 colunas, `ser_solicitacao` intacta em 14.163). Lembrete que continua valendo: o
AutoMigrate do startup **não** aplica — conferir `smsmarica.__migrations` a cada deploy.

### Passo 3 — Exercitar o export uma vez, com janela curta

> **06/08 noite:** o export foi exercitado CONTRA O SER REAL por sonda local somente-leitura, e
> dois defeitos foram achados e corrigidos: (1) a busca responde com **redirect A4J** e o motor
> parseava os 267 bytes do redirect como se fossem o resultado — zero linhas viravam "cobertura
> completa"; (2) a **data vem como serial do Excel** e entraria nula no espelho. O layout do
> `.xls` foi confirmado (12 colunas, `Sheet1`) e o teto de 500 + aviso foram medidos: 310 linhas
> em julho/2026 sem aviso, 500 com aviso na janela toda. Falta rodar pelo painel.

Antes de confiar na rodada inteira: disparar `Só a grade` numa janela de poucos meses e
conferir nos logs `SER/export:` que os lotes saem, que o aviso de corte é reconhecido e que
o parser da planilha achou o cabeçalho.

> ~~**PARE AQUI ANTES DE RECARREGAR (07/08/2026).**~~ **RESOLVIDO na tarde de 07/08/2026.**
> O protocolo da amarração foi lido direto do `ui.pack.js`/`framework.pack.js` que o SER serve
> (sem DevTools): são **duas** requisições A4J — fetch com `inputvalue` + onselect com o
> **índice da linha no hidden `_selection`**, ambas com `AJAXREQUEST=_viewRoot`. O `_selection`
> parecia "sempre vazio" porque o RichFaces o preenche só durante o submit e o limpa em seguida.
> Provado por sonda em 3 rodadas (recorte de Maricá determinístico, 1º = 2727024; controle
> só-texto instável e de outro recorte) e portado para o motor
> (`SerExportLeitor.AmarrarSolicitanteAsync`, com falha explícita quando a sugestão não vem).
> Protocolo completo em `docs/ser.md §4.3`.

**O que continua sem prova:**

- ~~Se o `suggUnidadeSol` de fato recorta para Maricá~~ — **provado em 07/08**: com a amarração,
  recorta (1º = 2727024, determinístico); sem ela, é o Estado inteiro. A credencial GESTOR SMS
  MARICA **não** escopa sozinha esta tela.
- **Se o export refaz a consulta ou devolve o resultado guardado na conversa Seam.** Mandamos os
  filtros nas duas requisições justamente para não depender da resposta. (O export da sonda
  filtrada veio do recorte certo — 500 linhas abrindo no mesmo 1º registro da grade — então o
  caminho está coerente nas duas hipóteses.)
- **A sonda local** (`Automais.SER`, somente leitura) segue sendo o caminho mais barato para
  qualquer pergunta nova: bate no SER real e salva o HTML, sem deploy.

### Passo 4 — Recarregar a base

**Antes de recarregar, limpar** `ser_solicitacao`, `ser_evento` e `ser_gatilho`: os 14.163
gatilhos atuais nasceram de uma varredura errada e virariam lixo permanente. Depois conferir
contra os números do §1.

> **A limpeza é SQL manual** (não há caminho no código; mitigante: as FKs têm `ON DELETE
> CASCADE`, então o DELETE em `ser_solicitacao` arrasta eventos e gatilhos). E antes de rodar a
> recarga inteira, resolver os achados marcados **[recarga]** no §2b — em especial o gatilho
> reincidente, que envenena a varredura em loop.

## 2b. Repasse de 07/08/2026 — revisão adversarial do stack

33 achados brutos, 28 confirmados por verificação adversarial (5 refutados). **Corrigidos no
mesmo dia** (todos com teste): busca sem redirect A4J era warning e virava "lote vazio =
cobertura completa" com cursor avançando (agora falha dura); onselect sem validação podia
degradar para busca sem filtro com cara de filtrada (agora exige o envelope A4J); `PrepararAsync`
retinha a tela velha em silêncio (agora falha); consulta direta lia o Estado inteiro POR PADRÃO
com justificativa já desmentida (agora filtra por padrão, e leitura sem filtro deixa rastro em
log); cabeçalho da planilha aceitava 4 colunas quaisquer sem as estruturais (agora exige ID +
Data da Solicitação, com off-by-one da janela de busca corrigido); login/módulo tinham fallback
para caminho constante (agora `ser.form_sem_action`); canários novos para a trava × parâmetros
da amarração e para a assinatura OLE2.

### Sessão de 08/08/2026 — o portão de sincronismo e a ALTA

**Primeira varredura completa da grade com o filtro certo:** Concluída em 26 min, 244 buscas,
24.298 solicitações, **0 fatias truncadas**.

**O portão foi medido** (contagem do Bernardo situação a situação na tela do SER × espelho):

| Situação | nosso CONSULTA / SER | nosso EXAME / SER | |
|---|---|---|---|
| Em fila | 1.670 / 1.670 | 753 / 753 | **exato** |
| Agendada | 729 / 729 | 334 / 334 | **exato** |
| Chegada não confirmada | 3.471 / 3.471 | 1.163 / 1.163 | **exato** |
| Cancelada | 3.970 / 3.970 | 1.499 / 1.499 | **exato** |
| Pendente | 51 / <100 | 33 / <50 | compatível |
| Chegada confirmada | 7.700 / 7.722 | 759 / 759 | −22 |
| Alta | 737 / 1.067 | 1.717 / 2.240 | **−853** |

Quatro situações batendo exatas nos dois tipos (12.796 registros) é a **validação definitiva da
amarração do solicitante em produção** — lendo o Estado inteiro esses números viriam muito
maiores, não idênticos. O −22 em Chegada confirmada é compatível com movimento da fila entre a
varredura (21:33–21:53) e a contagem; confirma-se sozinho na próxima rodada.

**O −853 da ALTA foi diagnosticado e corrigido:** a detecção do teto funcionava; quem perdia era
a leitura paginada. ALTA passou a ser lida pelo **export da tela de Solicitação** (`docs/ser.md
§4.4`) — uma requisição no lugar de cinco, sem os caminhos de perda calada. Junto saíram:

- **Achado 7 RESPONDIDO** — a tela de Solicitação **é** escopada pela credencial: 100 de 100
  linhas de ALTA vieram com `GESTOR SMS MARICA`/`MARICA`. Nunca foi vazamento de escopo, era
  buraco de cobertura.
- **Achado 11 corrigido** — o encolhimento da janela agora clampa no intervalo real antes de
  partir ao meio; antes repetia o mesmo lote cortado contra o SER sem trazer nada novo.
- **Falso positivo de remarcação corrigido** — o diff de agendamento passa a comparar pela
  **data**, não pelo texto cru. As duas telas escrevem o mesmo agendamento de formas diferentes
  (`28/01/2020 13:15 - HOSPITAL X` × `28/01/2020`), e comparar texto gerou **7.388 gatilhos
  falsos numa rodada só**. Era pré-requisito do marco zero.
- A leitura paginada (`VarredorSer`) foi **removida** — código morto que fala com produção é
  passivo.

**O segundo eixo do sincronismo continua em zero:** histórico lido em **1 de 24.586**. Sem
histórico não há FollowUP, e é por isso que o gatilho `NovoFollowUp` está zerado — não por não
haver, mas por nunca termos lido.

### Incidente de 08/08/2026 — a remontagem de "Agendado para" derrubou a varredura

A primeira versão do leitor de ALTA tentou remontar a coluna *Agendado para* juntando os
fragmentos ao registro anterior. **O layout não é esse** (`docs/ser.md §4.4`): os registros vêm
todos primeiro e os fragmentos num bloco depois, e o número de linhas por registro é variável —
não há como remontar. O resultado em produção foi concatenar agendamentos alheios num registro
só, estourar o `varchar(300)` e **matar a rodada** no `SaveChanges`.

**Dois bloqueadores previstos no §2b saíram do papel na mesma queda:**

- o `finally` tentou salvar com o change tracker envenenado, o `Status=Erro` **não persistiu** e a
  execução ficou presa em `EmExecucao` (item 2);
- presa assim, ela **trava o disparo diário**, porque o scheduler adia enquanto houver pendente
  (item 10).

**Corrigido:** a coluna passou a ser descartada nesse export; o merge preserva com `??` o
agendamento que a tela de Histórico trouxe; e o texto é truncado antes de gravar, para que campo
grande nunca mais derrube uma rodada inteira.

**O que ficou provado de bom na mesma rodada:** as seis situações não-Alta foram varridas gerando
**5 gatilhos**, contra 18.564 da rodada anterior — a normalização do agendamento eliminou os
7.388 falsos "remarcou", como previsto.

**Abertos, em ordem de urgência** (os `[recarga]` precisam sair antes do Passo 4):

1. **[recarga] Gatilho reincidente viola o índice único e trava a varredura em loop**
   (`SerSincronizacaoService.RegistrarGatilho:621`): `Add` cego contra
   `ux_ser_gatilho_solicitacao_tipo_chave`; a mesma transição repetida (remarcação, ciclo
   Cancelada→EmFila→Cancelada) estoura o unique e derruba o `SaveChanges` do lote.
2. **[recarga] `SaveChanges` do `finally` com change tracker envenenado**
   (`SerSincronizacaoService:155`): após `DbUpdateException`, o `Status=Erro` nunca persiste, a
   execução fica `EmExecucao` no banco e o scheduler adia o disparo diário para sempre.
3. **[recarga] `FatiaTruncada` só vive em memória** (`VarredorSerPorExport:128`): o cursor do
   dia truncado avança e persiste na hora, mas a fatia truncada só é gravada quando a situação
   INTEIRA termina — queda no meio vira Concluída com buraco não declarado.
4. **[recarga] Retomada no meio da grade monta fila de histórico parcial**
   (`SerSincronizacaoService.MontarFilaDeHistoricoAsync:486`): a remontagem-superconjunto do
   banco só roda com `pedidos` vazio, e a retomada chega com a lista parcialmente populada — o
   histórico dos trechos pré-queda é pulado.
5. Desligamento gracioso grava `Cancelada` e `RetomarInterrompidasAsync` não retoma `Cancelada`
   (`SerSincronizacaoService:142` + `VarreduraSerRunner:87`) — todo deploy no meio de rodada
   vira rodada perdida que exige disparo manual.
6. Retomada descarta o escopo de situações original (`VarreduraSerRunner:108` passa
   `Situacoes=null` ignorando `execucao.SituacoesVarridas`) — rodada parcial por escopo vira
   varredura de tudo.
7. ~~Varredura de **ALTA** não aplica recorte de solicitante~~ — **RESPONDIDO em 08/08**: a tela
   é escopada pela credencial do operador (100 de 100 linhas com `MARICA`). Não era vazamento.
8. `PlanilhaSerParser.Ler()` inteiro sem teste de integração (só `ConverterData` e a continuação
   de linha são cobertos) — falta um BIFF8 sintético pequeno no repositório, sem PII,
   exercitando cabeçalho, sinônimos, linha-lixo e encoding cp1252.
9. Dívida de releitura de histórico é edge-triggered e se perde quando a rodada morre com Erro
   ou roda `SomenteGrade` (`SerSincronizacaoService:486`).
10. `Interrompida` órfã: a retomada da subida reenfileira só a mais antiga e nada volta a ler as
    demais; o scheduler adia enquanto existir qualquer pendente (`VarreduraSerRunner:106`).
11. ~~Encolhimento de janela clampada repete exports idênticos~~ — **corrigido em 08/08**, com
    teste que proíbe pedir o mesmo recorte duas vezes ao SER.
12. Trava de leitura: dois pontos cegos na camada de rótulo (componente que só existe em script
    nunca é conferido; `alt`/`onclick` não entram no rótulo). Sem furo ativo hoje.
13. Bordas de `ConverterData` sem teste (limites 32874/73051 inclusive, serial inteiro à
    meia-noite, número abaixo do mínimo).
14. Registrado por projeto (não é defeito): a tela de **Solicitação** reusa a página de
    resultado como base do submit seguinte (ciclo otimizado do histórico, `docs/ser.md §6`) — a
    regra do "GET novo por busca" foi medida e vale para a tela de **Histórico**; a instabilidade
    equivalente na tela de Solicitação nunca foi medida. Se aparecer não-determinismo lá, esse é
    o primeiro suspeito.

### Passo 4b — Marco zero dos gatilhos (decisão do Bernardo, 08/08/2026)

**Só depois de sincronizado.** A regra é dele e está certa: enquanto o espelho está incompleto,
toda "solicitação nova" que a varredura acha não é novidade — é coisa que a gente ainda não tinha
lido. Gatilho nessa fase é ruído, não sinal. Zerar antes de sincronizar também não adianta: a
própria carga geraria a pilha de volta.

Ordem: **sincronizar → conferir o portão → zerar → a partir dali, diferença é diferença.**

**Zerar = carimbar `processado_em` + `processado_por = 'marco-zero'`**, não apagar: preserva a
trilha e é reversível.

> **Pré-requisito técnico:** o **gatilho reincidente** (item 1 dos abertos) precisa estar
> corrigido antes. O índice único é `(solicitação, tipo, chave)` **sem filtro por processado** —
> carimbar como processado **não libera a chave**, então a próxima repetição da mesma transição
> estoura o índice e derruba a varredura. Marcar sem corrigir troca um problema por outro.

### Passo 5 — Ligar o scheduler

`Ser:Varredura:Ativo=true` (hora padrão 02:30 de Brasília). Só depois que a carga bater.

### Passo 6 — Consumir os gatilhos

`ser_gatilho` continua sem consumidor. É o que falta para o espelho virar operação.

---

## 3. Regras que não podem ser esquecidas

1. **Postar sempre no `action` lido do `<form>`**, nunca em caminho constante. Com os mesmos
   campos, headers e ViewState, o caminho fixo devolve listagem **diferente e incompleta**,
   sem erro nenhum. Foi o que corrompeu a carga inicial. (`docs/ser.md §3.3`)
2. **`AJAXREQUEST` é obrigatório** nos submits A4J — mas **o Exportar não é A4J** (`jsfcljs`,
   Mojarra): mandar `AJAXREQUEST` nele é ruído.
3. **ViewState vem de dentro do form submetido**, não o primeiro do documento.
4. **O discriminador de "página de formulário" é o BOTÃO Pesquisar**, não o `form0` — a tela
   de histórico também tem `form0`.
5. **Sessão do SER é única por operador**: varrer derruba quem estiver logado. Uma rodada por
   vez, sempre. Por isso o scheduler roda de madrugada.
6. **FollowUP é `Em fila → Em fila`** — não aparece em diff de grade. Por isso todo `EM_FILA`
   tem o histórico relido diariamente.
7. **`Alta` é estado terminal, não tem histórico e não existe no combo da tela de export.**
   ALTA continua sendo varrida pela tela de Solicitação.
8. **Somente leitura.** Trava em duas camadas (nome do parâmetro **e** rótulo visível).
9. **Nunca inferir truncamento por contagem de linhas.** 500 exatos podem ser o total real —
   quem decide é o aviso `form0:messages`.
10. **Cursor só avança depois do lote gravado.** Cursor à frente do que foi persistido vira
    buraco invisível na retomada.

## 4. Erros de diagnóstico já cometidos (para não repetir)

Da sessão de 06/08, todos por inferir de amostra pequena:

1. "A tela de Histórico traz PII de outros municípios" — **não traz**. IDs baixos (2.7M) são
   de Maricá; a faixa da base é 873.917–8.147.763.
2. "A listagem descarta silenciosamente os mais antigos ao bater no teto" — **não descarta**.
3. "A base inteira é não-confiável por causa disso" — a causa era o `action`, não o teto.

Da noite de 06/08, investigando a grade vazia do export — **três hipóteses, todas erradas**:

4. "O filtro de data está matando a consulta" — **não está**; a data funciona e é o que faz o
   aviso de corte sumir quando a janela é estreita.
5. "O `GESTOR SMS MARICA` em texto solto zera o resultado" — **não zera**.
6. "O tipo CONSULTA atrapalha" — **não atrapalha**.

A causa era o **redirect A4J não seguido**. A lição se repete: quando o alvo responde 200 e vazio,
o suspeito é o próprio transporte, não os filtros — e uma sonda que salva a resposta crua responde
em minutos o que três ciclos de deploy não responderam.

O que destravou foi o Bernardo dizer *"quando eu opero na mão, na primeira consulta vem
certo"*. **Desconfiar do próprio scraper antes de acusar o sistema alvo.**

Da tarde de 07/08, fechando o caso do Solicitante:

7. "O hidden `_selection` fica vazio, então não é por ele que o servidor sabe" — **era por ele**.
   O RichFaces escreve o índice, submete e **limpa o campo em seguida**: olhar o DOM depois do
   clique não diz o que viajou no request. E o que resolveu de vez não foi capturar tráfego, foi
   **ler o JS do componente** (`ui.pack.js` servido pelo próprio SER): o protocolo inteiro estava
   declarado lá — inclusive o `inputvalue` (param default que o init não sobrescreve) e o
   `AJAXREQUEST=_viewRoot` (o init não passa `containerId`). Fonte primária antes de dedução.
