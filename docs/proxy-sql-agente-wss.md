# Proxy SQL por agente (WSS reverso) — contrato do agente

> **Para o Claude aberto no servidor de destino (onde mora o SQL Server).**
> Você vai construir o **agente proxy** em Python. Ele roda no servidor de destino, disca **para
> fora** até o smsmarica por WebSocket seguro (WSS), recebe consultas SQL, executa **somente
> leitura** contra o banco local e devolve as linhas. O lado do smsmarica **já está implementado**
> (ADR-0023) — este documento é o contrato que os dois lados cumprem.

---

## 1. Como funciona

```
[SQL Server]  ──local, read-only──  [agente.py]  ──disca pra fora (443)──▶  smsmarica (servidor WSS)
   credenciais                       driver + guard + .env                    valida token, manda {sql}
   no .env do destino                                                          recebe {colunas, linhas}
```

- **O agente inicia a conexão** (saída na 443). Nenhuma porta de entrada aberta no destino.
- **As credenciais do banco ficam no `.env` do destino.** O smsmarica nunca as vê — ele só conhece
  o agente, por um token.
- **Read-only nos dois lados.** O smsmarica valida o SQL antes de mandar; o agente valida de novo
  antes de tocar no banco.

---

## 2. Bootstrap — o agente se auto-configura (sem cadastrar nada na mão)

O agente se provisiona sozinho no **primeiro run**, pedindo o **login do admin** uma única vez
(estilo `gh auth login`). As credenciais do admin são usadas na hora e **descartadas** — nunca
ficam salvas. O que fica salvo é só o **token do agente** (bearer scoped a essa base).

Fluxo do primeiro run (`python agente.py setup`):

1. Pergunta interativamente **e-mail + senha do admin** (use `getpass` — a senha não ecoa).
2. `POST https://api.smsmarica.online/identidade/login` com `{ "email": ..., "senha": ... }`
   → resposta `{ "token": "<JWT>", ... }`.
3. `POST https://api.smsmarica.online/ia/configuracao/agentes/provisionar`
   com header `Authorization: Bearer <JWT>` e corpo `{ "slug": "hospital-x-sqlserver", "nome": "Hospital X (SQL Server)" }`
   → resposta `{ "slug": ..., "token": "<TOKEN DO AGENTE>", "wssUrl": "wss://api.smsmarica.online/agentes/sql" }`.
   Idempotente: se a base já existir, só rotaciona o token.
4. **Grava o token** localmente (arquivo `~/.smsmarica-agente/<slug>.token`, permissão `0600`, ou no `.env`).
5. **Descarta e-mail/senha do admin.** Nunca escrever em disco.
6. Segue para conectar (seção 4).

Runs seguintes (`python agente.py`): já existe token salvo → conecta direto, sem pedir login.
Rotação manual: `python agente.py rotate` refaz o passo de login e regenera o token.

> O admin precisa da permissão **Configuração da IA** (InteligenciaConfiguracao / Edição). É o
> mesmo perfil que já cadastra bases hoje.

---

## 3. O `.env` do agente (no servidor de destino)

Só o que é do **banco local** precisa ser preenchido à mão. A parte do smsmarica o bootstrap resolve.

```dotenv
# smsmarica — o bootstrap preenche o token; o resto tem default
SMSMARICA_BASE=https://api.smsmarica.online     # para login + provisionamento
AGENTE_SLUG=hospital-x-sqlserver                # id do agente (você escolhe)
# AGENTE_TOKEN é gravado pelo bootstrap (ou em ~/.smsmarica-agente/<slug>.token)

# Banco de destino (NUNCA sai desta máquina)
SQL_HOST=10.0.0.5
SQL_PORT=1433
SQL_DATABASE=Prontuario
SQL_USER=leitura_indicadores      # conta READ-ONLY dedicada
SQL_PASSWORD=...
SQL_DRIVER=ODBC Driver 18 for SQL Server   # se usar pyodbc
```

> `.env` e o arquivo de token no `.gitignore`. Senha do banco e token **só existem nesta máquina**.

---

## 4. O handshake da WSS

Com o token em mãos, o agente conecta em:
```
wss://api.smsmarica.online/agentes/sql?agente=<AGENTE_SLUG>&token=<AGENTE_TOKEN>
```
- O smsmarica valida o token (hash guardado na base). Se bater, a conexão fica aberta.
- Se o token estiver errado ou a base não existir/estiver inativa → **HTTP 401**, sem WebSocket.
  (Nesse caso, rode `setup`/`rotate` para reprovisionar.)
- **Última conexão vence:** se o mesmo slug reconectar, a sessão anterior é derrubada.

---

## 5. O protocolo (mensagens de texto JSON sobre o WebSocket)

### smsmarica → agente — pedido de consulta
```json
{ "tipo": "query", "id": "3f9c...", "sql": "SELECT ...", "maxLinhas": 1000, "timeoutSeg": 30 }
```

### agente → smsmarica — resultado (sucesso)
```json
{
  "tipo": "resultado",
  "id": "3f9c...",
  "ok": true,
  "colunas": ["numerador", "denominador"],
  "linhas": [ [1428, 12034] ]
}
```
- `id` **igual** ao do pedido (é assim que o smsmarica casa a resposta).
- `linhas` = lista de listas, na ordem de `colunas`.
- Valores: números, strings, `true`/`false`, `null`. **Datas → string ISO** (`"2026-06-01T08:12:00"`).
  Números decimais podem vir como número JSON; se houver risco de precisão, mande como string.
- Respeite `maxLinhas` (corte o excedente) e `timeoutSeg` (aborte a query no banco).

### agente → smsmarica — resultado (erro)
```json
{ "tipo": "resultado", "id": "3f9c...", "ok": false, "erro": "Invalid object name 'X'." }
```

### agente → smsmarica — keepalive (a cada ~30s)
```json
{ "tipo": "ping" }
```
Mantém a conexão viva através do nginx (que fecha ocioso). O smsmarica ignora o ping.

---

## 6. Read-only no agente (obrigatório — defesa em profundidade)

Antes de executar, o agente **rejeita** qualquer coisa que não seja consulta:
- Deve começar (ignorando comentários) com `SELECT` ou `WITH`.
- Bloquear se contiver, fora de string: `INSERT UPDATE DELETE MERGE DROP ALTER CREATE TRUNCATE
  GRANT REVOKE EXEC EXECUTE SP_ xp_ COMMIT ROLLBACK`.
- Um único statement (sem `;` interno).
- Conectar com **login read-only** e, no SQL Server, abrir com
  `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` (não trava a produção) — ou apontar para uma
  **réplica de leitura** se existir.

O smsmarica já aplica o mesmo guard antes de mandar, mas **não confie só nele**: o agente é a
última barreira antes do banco do hospital.

---

## 7. Robustez do agente

- **Reconexão automática** com backoff (ex.: 1s, 2s, 5s, 10s, máx 30s) se a WSS cair.
- **Uma conexão de banco por consulta** ou um pool pequeno; sempre com timeout de comando.
- **Cap de linhas** aplicado no cursor (`maxLinhas`), para não devolver um resultado gigante.
- Logar erros localmente (sem PII); nunca logar o token nem a senha.
- Rodar como **serviço** (systemd) que reinicia sozinho.

---

## 8. Stack sugerida

- Python 3.11+, `websockets` (cliente WSS), `python-dotenv`.
- Driver SQL Server: **`pyodbc`** (precisa do *ODBC Driver 18 for SQL Server* instalado) ou
  **`pymssql`** (mais simples de instalar, menos recursos). Confirme qual está disponível no destino.
- Esqueleto do laço: conectar WSS → autenticar por query string → `async for msg` → se `tipo==query`,
  validar guard, executar no banco, montar `resultado`, devolver → mandar `ping` periódico → em
  exceção de conexão, reconectar.

---

## 9. Teste de ponta a ponta

1. Suba o agente com o `.env` preenchido. Ele deve conectar (o smsmarica loga `Agente 'slug' conectado`).
2. No smsmarica, o cadastro da base mostra **agente conectado = true**.
3. Rode uma consulta trivial pelo consumidor (ex.: o módulo de indicadores ou o teste de conexão):
   `SELECT 1 AS numerador, 1 AS denominador` deve voltar `[[1,1]]`.
4. Rode uma consulta real de contagem e confira o número na mão.
5. Teste a segurança: mande um `UPDATE` — o agente deve **recusar** antes de tocar no banco.

---

## 10. Referência: o que o smsmarica já faz (não precisa refazer)

- **Login:** `POST /identidade/login {email, senha}` → `{token: <JWT>, ...}`.
- **Provisionamento:** `POST /ia/configuracao/agentes/provisionar {slug, nome}` (Bearer JWT admin)
  → cria a base via agente se não existir e devolve `{slug, token, wssUrl}`. Idempotente (rotaciona).
- **WSS** `/agentes/sql` valida `agente` + `token` (hash SHA-256, comparação em tempo constante) e
  mantém o registro em memória dos agentes conectados.
- `ProxyAgenteFonte : IFonteDados` — quando um indicador (ou o módulo IA) executa contra essa base,
  o smsmarica manda o `{sql}` pelo socket do agente e aguarda o `{resultado}` (com timeout e cap).
- Guard read-only aplicado antes de enviar.

**Nota de infraestrutura (nosso lado):** o nginx do smsmarica precisa encaminhar o *upgrade* de
WebSocket para `/agentes/sql` (igual já faz para `/hubs/`), com `proxy_read_timeout` folgado.
