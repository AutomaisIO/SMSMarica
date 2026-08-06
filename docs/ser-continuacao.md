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

### Passo 1 — Validar a correção do `action` — **PENDENTE, é o portão**

O bug de postar em caminho fixo foi corrigido (`a483e8f`) mas **nunca exercitado**.

1. `Regulação → Configuração → SER — consulta direta`
2. Filtrar `Chegada confirmada` + `CONSULTA`
3. Comparar a contagem com **7.723**

Se bater, o transporte está curado. Se não bater, **parar e investigar** — não adianta
recarregar a base com o transporte quebrado.

### Passo 2 — Deployar a branch

Precisa de `dotnet ef database update` **explícito**: o AutoMigrate do startup não aplica
migration (já mordeu três vezes). Conferir `smsmarica.__migrations` depois.

### Passo 3 — Exercitar o export uma vez, com janela curta

Antes de confiar na rodada inteira: disparar `Só a grade` numa janela de poucos meses e
conferir nos logs `SER/export:` que os lotes saem, que o aviso de corte é reconhecido e que
o parser da planilha achou o cabeçalho.

**O que pode dar errado aqui, e nunca foi testado contra o SER real:**

- **O layout de colunas do `.xls`.** Nunca guardamos uma amostra do arquivo. O parser mapeia
  **por nome de coluna** e procura a linha de cabeçalho nas 8 primeiras — se não reconhecer
  ao menos 4 colunas, ele **explode em vez de importar lixo**. Se explodir, é só acrescentar
  o sinônimo em `PlanilhaSerParser.Sinonimos`.
- **Se o export respeita o filtro re-enviado** ou se exporta o resultado guardado na conversa
  Seam. Mandamos os filtros nas duas requisições justamente para não depender da resposta.

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

O que destravou foi o Bernardo dizer *"quando eu opero na mão, na primeira consulta vem
certo"*. **Desconfiar do próprio scraper antes de acusar o sistema alvo.**
