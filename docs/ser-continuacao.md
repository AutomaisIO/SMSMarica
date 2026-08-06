# SER — ponto de retomada (sessão de 05–06/08/2026)

Documento de handoff. Quem retomar deve ler **este arquivo primeiro**, depois
[`docs/ser.md`](./ser.md) (protocolo) e [ADR-0042](./adr/0042-ser-segunda-fonte-de-regulacao.md)
(decisão de arquitetura).

> **Frase para retomar:** *"Continuar o SER a partir de `docs/ser-continuacao.md`:
> migrar a fase de grade para export de 500 e fazer o ponteiro funcionar."*

---

## 1. Onde estamos

**Em produção** (branch `main`, deployado):

- Tabelas `ser_solicitacao`, `ser_evento`, `ser_gatilho`, `ser_varredura_execucao`,
  `ser_varredura_falha` — migration aplicada.
- Motor `SMSMarica.Core/Integracoes/SerWeb/` (sessão JSF/Seam, parsers, varredor, runner).
- Telas: `Regulação → SER` (fila + detalhe com histórico) e `Regulação → Configuração`
  (aba SER com credencial/motor + aba **SER — consulta direta**).
- Permissões `RegulacaoSer = 54` e `RegulacaoConfiguracao = 51`.
- 14 testes unitários de parser/trava.

**Branch `feat/ser-varredura-por-export`** (NÃO deployada): ponteiro de retomada, começado
nesta sessão — ver §4.

**Estado dos dados em produção — não confiar:**

| | |
|---|---|
| Solicitações espelhadas | 14.163 (**~35% incompletas**) |
| Eventos de histórico | **1** de 14.163 |
| Gatilhos na fila | 14.163 (sem consumidor) |
| Execuções | 2, ambas em erro |

Conferência contra a tela do SER (feita pelo Bernardo, 06/08):

| Recorte (só CONSULTA) | No SER | Na base | Falta |
|---|---|---|---|
| Chegada confirmada | 7.723 | 4.895 | 37% |
| Chegada não confirmada | 3.471 | 2.252 | 35% |
| Alta | > 0 | **0** | tudo |

A perda constante de ~35% bate com a paginação morrendo por volta da 3ª de 5 páginas.

---

## 2. O que fazer, em ordem

### Passo 1 — Validar a correção do `action` (30 min, é o portão)

O bug de postar em caminho fixo em vez do `action` do form foi corrigido (`a483e8f`) mas
**nunca exercitado**. Antes de reescrever qualquer coisa:

1. `Regulação → Configuração → SER — consulta direta`
2. Filtrar `Chegada confirmada` + `CONSULTA`
3. Comparar a contagem com **7.723**

Se bater, o motor está curado e a base pode ser refeita. Se não bater, **parar e
investigar** — não adianta migrar para o export com o transporte quebrado.

### Passo 2 — Migrar a fase de grade para EXPORT (o grosso do trabalho)

Trocar `busca → paginar 20 em 20` por `busca → exportar → parsear`.

**Provado nesta sessão:**

- O botão `form0:btnExport` da tela de Histórico devolve `historico-pesquisar.xls`,
  **192 KB, BIFF8 real** (OLE2, assinatura `D0 CF 11 E0`) com **501 registros ROW**
  = 1 cabeçalho + **500 linhas**. Traz o resultado inteiro, não a página.
- É submit JSF comum: `jsfcljs(form0, {'form0:btnExport':'form0:btnExport'}, '')`.
- Filtro de escopo: **`form0:suggUnidadeSol` = `GESTOR SMS MARICA` em TEXTO PURO**. O
  hidden `form0:j_id37_selection` fica vazio mesmo clicando na sugestão — não é preciso.
- **Não** marcar `form0:municipio` (é *Município do Paciente*): paciente de outro município
  pode ter solicitação aberta por Maricá.
- A tela avisa explicitamente quando corta: *"Consulta muito ampla, retorno limitado em
  500 resultados"* — usar essa string como sinal de truncamento, não inferir por páginas.

**Limitações da tela de Histórico** (`historico-pesquisar.seam`):

- **Não tem ALTA** no combo de situação → ALTA continua pela tela de Solicitação.
- **Não oferece "Histórico da Solicitação"** no menu da linha → a fase de histórico
  continua na tela de Solicitação.
- Sem Situação preenchida, exige nome/código/CNS/CPF/ID do paciente.

**Cursor por data** (ideia do Bernardo, substitui a bisecção): exporta um lote, lê a maior
`Data da Solicitação` do arquivo, refaz a busca a partir dali, repete até o lote voltar
abaixo de 500 sem aviso de corte.

**Decisão pendente:** biblioteca para ler `.xls` BIFF8 em .NET. Nenhuma está no
`Directory.Packages.props`. Recomendação: **ExcelDataReader** (menor, só leitura) sobre NPOI.

### Passo 3 — Fazer o ponteiro funcionar

Os campos existem (§4) mas **ninguém grava neles**. Falta:

- `SerSincronizacaoService` gravar `Fase`, `CursorSituacao`, `CursorData`, `CursorIdSer` e
  `HistoricosPendentes` **a cada lote**, não só no fim.
- Na retomada, pular o que já foi feito usando esses cursores.
- Migration para as colunas novas.

### Passo 4 — Mostrar na tela de Configuração da Regulação

Fase atual, cursor, quantos faltam, nº de retomadas, e o status `Interrompida`.

### Passo 5 — Recarregar a base

**Antes de recarregar, limpar** `ser_solicitacao`, `ser_evento` e `ser_gatilho`: os 14.163
gatilhos atuais nasceram de uma varredura errada e virariam lixo permanente.

Depois conferir contra os números do §1.

### Passo 6 — Scheduler diário

Ainda não existe. Hoje só há disparo manual.

---

## 3. Regras que não podem ser esquecidas

1. **Postar sempre no `action` lido do `<form>`**, nunca em caminho constante. Com os mesmos
   campos, headers e ViewState, o caminho fixo devolve listagem **diferente e incompleta**,
   sem erro nenhum. Foi o que corrompeu a carga inicial. (`docs/ser.md §3.3`)
2. **`AJAXREQUEST` é obrigatório** nos submits A4J; sem ele a ação não roda e devolve 200.
3. **ViewState vem de dentro do form submetido**, não o primeiro do documento.
4. **A tela de histórico também tem `form0`** mas não tem o botão Pesquisar — o
   discriminador de "página de formulário" é o BOTÃO.
5. **Sessão do SER é única por operador**: varrer derruba quem estiver logado. Uma rodada
   por vez, sempre.
6. **FollowUP é `Em fila → Em fila`** — não aparece em diff de grade. Por isso todo `EM_FILA`
   tem o histórico relido diariamente, um a um (2 requisições, ~0,6 s cada).
7. **`Alta` é estado terminal e não tem histórico** no SER. O que não foi capturado antes da
   alta, perdeu-se.
8. **Somente leitura.** A trava tem duas camadas (nome do parâmetro **e** rótulo visível),
   porque `Registrar FollowUP` tem id opaco `j_id169`.

## 4. O que está na branch `feat/ser-varredura-por-export`

Compila, **sem migration**, não deployado:

- `StatusVarreduraSer.Interrompida` — parada retomável (não é erro, não é concluída).
- `FaseVarreduraSer` (Grade / Historico / Finalizada).
- Colunas de ponteiro em `SerVarreduraExecucao`: `Fase`, `CursorSituacao`, `CursorData`,
  `CursorIdSer`, `HistoricosPendentes`, `Retomadas`, `RetomadaEm`.
- `VarreduraSerRunner` **retoma** interrompidas na subida em vez de marcá-las como erro
  (o comportamento anterior contrariava o requisito de sobreviver a restart).
- `PedidoVarreduraSer.ExecucaoParaRetomar` e a assinatura de `ExecutarAsync`.

## 5. Erros de diagnóstico desta sessão (para não repetir)

Três conclusões minhas que **estavam erradas**, todas por inferir de amostra pequena:

1. "A tela de Histórico traz PII de outros municípios" — **não traz**. IDs baixos (2.7M) são
   de Maricá; a faixa da base é 873.917–8.147.763.
2. "A listagem descarta silenciosamente os mais antigos ao bater no teto" — **não descarta**.
3. "A base inteira é não-confiável por causa disso" — a causa era o `action`, não o teto.

O que destravou foi o Bernardo dizer *"quando eu opero na mão, na primeira consulta vem
certo"*. **Desconfiar do próprio scraper antes de acusar o sistema alvo.**
