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

**O que continua sem prova:**

- **Se o `suggUnidadeSol` de fato recorta para Maricá.** Medido: mandá-lo **não zera** a
  consulta. Se ele filtra alguma coisa, não se sabe — e a credencial já é de um operador GESTOR
  SMS MARICA, então pode ser redundante. Enquanto não houver prova, não confiar nele para
  afirmar escopo.
- **Se o export refaz a consulta ou devolve o resultado guardado na conversa Seam.** Mandamos os
  filtros nas duas requisições justamente para não depender da resposta.
- **A sonda local** (`Automais.SER`, somente leitura) é o caminho mais barato para responder
  qualquer uma dessas: bate no SER real e salva o HTML, sem deploy.

### Passo 4 — Recarregar a base

**Antes de recarregar, limpar** `ser_solicitacao`, `ser_evento` e `ser_gatilho`: os 14.163
gatilhos atuais nasceram de uma varredura errada e virariam lixo permanente. Depois conferir
contra os números do §1.

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
