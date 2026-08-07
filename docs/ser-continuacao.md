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
7. Varredura de **ALTA** não aplica recorte de solicitante nenhum (a tela de Solicitação nem tem
   o campo) — o escopo Maricá ali depende de a tela ser escopada pelo operador, **o que nunca
   foi provado**. Medir com a sonda antes de confiar (os indícios são bons: a busca manual do
   Bernardo em 07/08 na tela de Solicitação devolveu o recorte de Maricá).
8. `PlanilhaSerParser.Ler()` inteiro sem teste de integração (só `ConverterData` é coberto) —
   falta um BIFF8 sintético pequeno no repositório, sem PII, exercitando cabeçalho, sinônimos,
   linha-lixo e encoding cp1252.
9. Dívida de releitura de histórico é edge-triggered e se perde quando a rodada morre com Erro
   ou roda `SomenteGrade` (`SerSincronizacaoService:486`).
10. `Interrompida` órfã: a retomada da subida reenfileira só a mais antiga e nada volta a ler as
    demais; o scheduler adia enquanto existir qualquer pendente (`VarreduraSerRunner:106`).
11. Encolhimento de janela clampada repete exports idênticos truncados contra o SER até o passo
    caber (`VarredorSerPorExport:72`) — desperdício, sem perda.
12. Trava de leitura: dois pontos cegos na camada de rótulo (componente que só existe em script
    nunca é conferido; `alt`/`onclick` não entram no rótulo). Sem furo ativo hoje.
13. Bordas de `ConverterData` sem teste (limites 32874/73051 inclusive, serial inteiro à
    meia-noite, número abaixo do mínimo).
14. Registrado por projeto (não é defeito): a tela de **Solicitação** reusa a página de
    resultado como base do submit seguinte (ciclo otimizado do histórico, `docs/ser.md §6`) — a
    regra do "GET novo por busca" foi medida e vale para a tela de **Histórico**; a instabilidade
    equivalente na tela de Solicitação nunca foi medida. Se aparecer não-determinismo lá, esse é
    o primeiro suspeito.

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
