# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## O que é

Esta pasta hospeda o trabalho de **engenharia reversa do Salux HIS** (sistema hospitalar legado em Oracle 12c) usado pela SMS Maricá / Hospital Conde Modesto Leal (HCML). O objetivo final é **replicar telas do Salux desktop (portal.exe e companheiros) em uma aplicação React+Vite** consumindo um backend próprio.

Fase atual (2026-05-27): **discovery**. Mapeando as queries que cada tela do Salux desktop dispara contra o banco PROD, documentando em `docs/queries/<tela>.md`. Nada de React ainda.

## Regras não-negociáveis

1. **PRODUCAO é read-only absoluto.** O banco em `10.50.0.18:1521/ORASX01` é o hospital vivo. Toda conexão passa por `scripts/_guard.py` (rejeita não-SELECT) e abre com `SET TRANSACTION READ ONLY`. Nunca contornar.
2. **Não conectar com a conta `SUPERVISOR` pra leitura de V$.** Ela não tem grant em V$SQL e tem privilégio de escrita amplo no schema Salux — risco alto sem benefício. Usar `salux_obs` (criada por nós, `SELECT_CATALOG_ROLE` only).
3. **`.env` nunca vai pro git.** Credenciais Oracle, SSH root, senhas geradas — tudo fica local. `.gitignore` já cobre `.env`, `.env.*` (exceto `.env.example`), `capturas/`.
4. **`capturas/` é gitignored.** Pode conter dados clínicos (PII) — nomes de paciente, CPF, prontuário em binds capturados de V$SQL_BIND_CAPTURE. Antes de commitar qualquer `docs/queries/<tela>.md`, redigir binds com PII pra placeholders (`<CPF>`, `<NOME>`).
5. **Não criar usuários, dar grants, ou tocar em SYS sem autorização explícita.** A conta `salux_obs` já existe; pra qualquer mudança nova no banco, perguntar.

## Stack

| Componente | Stack |
|---|---|
| Captura | Python 3.13 (sistema), `paramiko`, `python-dotenv`. `sqlplus.exe` 12.1 (`C:\Oracle1\...`) via subprocess. |
| Banco-alvo | Oracle 12.2 em Linux (`oracle.mgandhi.rio.br`), SID `ORASX01`. |
| Cliente observado | Salux desktop (`portal.exe`, `suprim.exe`, `centro.exe`, `fatsusii.exe`, `Salux.Services.exe`). |
| Futuro front | React + Vite + TypeScript (ainda não iniciado). |

**Por que sqlplus subprocess** e não `python-oracledb` direto: o banco usa password verifier `0x939` que o thin mode não cobre, e o único Oracle Client local (12.1 em `C:\Oracle1`) é 32-bit — incompatível com Python 64-bit. Subprocess de sqlplus.exe contorna ambos, é throughput-aceitável (queries de V$, baixo volume).

## Setup inicial (caso o `.env` se perca ou alguém clone)

1. Garantir Python 3.13+ e que `sqlplus` está em `PATH` (já presente via `C:\Oracle1\product\12.1.0\client_1\bin\sqlplus.exe`).
2. Instalar deps:
   ```powershell
   python -m pip install --user oracledb python-dotenv paramiko
   ```
3. Copiar `.env.example` → `.env` e preencher:
   ```
   SALUX_ORACLE_USER=salux_obs                # conta read-only criada por nós
   SALUX_ORACLE_PASSWORD=<senha do salux_obs>
   SALUX_ORACLE_DSN=10.50.0.18:1521/ORASX01
   SALUX_APP_USUARIO=SYBSA                    # conta usada pelo Salux desktop
   SALUX_APP_MACHINE_LIKE=%DESKTOP-T7PIF3P%   # máquina do usuário alvo
   SALUX_SESSAO_SID=<descobrir via identificar_sessao.py>
   SALUX_SESSAO_SERIAL=<idem>
   SALUX_SSH_HOST=10.50.0.18                  # opcional, usado por setup_observador.py
   SALUX_SSH_USER=root
   SALUX_SSH_PASSWORD=<senha root do servidor Oracle>
   ```
4. Se `salux_obs` não existir mais (perdemos o ambiente):
   ```powershell
   python "scripts/setup_observador.py"   # SSH root → su oracle → sqlplus / as sysdba
   ```
   O script é idempotente: se a conta existir, reseta a senha; senão cria. Pede `SALUX_SSH_*` no `.env`.
5. Smoke test:
   ```powershell
   $env:PYTHONIOENCODING="utf-8"; python "scripts/teste_conexao.py"
   ```
   Deve mostrar `DB_NAME=ORASX01` + sessões SUPERVISOR.

## Estrutura

```
Salux/
├── .env                      # gitignored
├── .env.example              # template, commitado
├── .gitignore
├── CLAUDE.md                 # este arquivo
├── scripts/
│   ├── _guard.py             # bloqueia SQL que não seja SELECT/WITH/EXPLAIN
│   ├── conexao.py            # interface read-only via sqlplus subprocess
│   ├── ssh_servidor.py       # paramiko wrapper pro 10.50.0.18 root
│   ├── teste_conexao.py      # smoke test
│   ├── probe_servidor.py     # mapeia ORACLE_HOME/SID via SSH
│   ├── setup_observador.py   # cria salux_obs via SYS
│   ├── diagnostico_privilegios.py  # confere acesso a V$/DBA_ views
│   ├── sessoes_amplas.py     # lista sessões ativas (todos usuários)
│   ├── identificar_sessao.py # filtra V$SESSION pela máquina + usuário alvo
│   ├── espiar_ultima.py      # mostra última e atual SQL da sessão alvo
│   ├── marcar.py             # grava timestamp do banco em capturas/marcador.txt
│   └── capturar.py           # captura queries da sessão alvo desde o marcador
├── capturas/                 # gitignored: JSON brutos + marcador.txt
└── docs/
    └── queries/              # 1 .md por tela documentada (commitável após redação de PII)
```

## Fluxo de trabalho (resumo)

A skill `salux-capturar-tela` cobre o ciclo completo. Resumo:

1. **Usuário abre o Salux desktop** em `DESKTOP-T7PIF3P` como SYBSA.
2. **Identificar a sessão**: `python scripts/identificar_sessao.py` — pega SID/SERIAL e atualiza `.env`.
3. **Para cada tela a documentar:**
   - Usuário diz: "vou abrir <tela>"
   - Rodar: `python scripts/marcar.py inicio`
   - Usuário executa no Salux
   - Usuário diz: "feito"
   - Rodar: `python scripts/capturar.py <rotulo-kebab>`
   - Revisar `docs/queries/<rotulo>.md` com o usuário

## Identidades Oracle do Salux

| Conta | Quem usa | Privilégio | Para que serve |
|---|---|---|---|
| `SYBSA` | Aplicação Salux (portal.exe etc) | Owner do schema | Schema-owner. Todas as queries da app são parsed como SYBSA. **Filtrar V$SQL.parsing_schema_name = 'SYBSA'**. |
| `SUPERVISOR` | Admin Salux (humano) | Custom roles (ROLE_CRIARUSUARIOS, ROLE_INFOSAUDE) | Pode ver V$SESSION mas **não V$SQL**. Senha conhecida (`super`). |
| `salux_obs` | Nós (discovery) | `CREATE SESSION` + `SELECT_CATALOG_ROLE` | **Conta de leitura para V$/DBA_/ALL_ views.** Sem acesso a tabelas do schema. Senha gerada, no `.env`. |
| `SYS` | DBA | SYSDBA | Acessado **só** via SSH root → `su - oracle` → `sqlplus / as sysdba`. Usado uma vez pra criar `salux_obs`. |

## Estratégia de captura

V$SQL agrega queries por SQL_ID (não por execução). Pra correlacionar com a sessão do usuário:
- **Marcador**: salva `SYSTIMESTAMP` ANTES da ação. Toda query com `last_active_time > marcador` é candidata.
- **Filtro de schema**: `parsing_schema_name = 'SYBSA'` exclui ruído de outras contas.
- **Filtro de SID** (opcional, via `SALUX_SESSAO_SID`): restringe via `V$OPEN_CURSOR` ⋃ `V$SESSION.sql_id/prev_sql_id`. Reduz ruído de outras sessões SYBSA (existem ~170 simultâneas).

**Limites:** se uma query executou em < 1 segundo entre dois pollings, `last_active_time` pode acabar fora da janela. Se a sessão tem muitos child cursors agrupados em um SQL_ID, vai aparecer só uma entrada com `executions` somando tudo.

## Pegadinhas

- `SET LINESIZE` > 4000 em sqlplus dispara warning "rows will be truncated" e quebra o parser. Manter em 4000.
- `:s` como bind name colide com `:serial` se o replacement não usar word boundary. `conexao.py` usa regex `:[A-Za-z_]\w*` exato.
- BOM (UTF-16 LE) no início de scripts SQL faz sqlplus 12.1 reclamar `SP2-0734: início de comando desconhecido`. Escrever sempre com `encoding="utf-8"` no Python (sem BOM).
- `PYTHONIOENCODING=utf-8` é necessário no PowerShell — sem ele, caracteres acentuados quebram (`'charmap' codec can't encode`).
- O Salux conecta como SYBSA mas o **schema lógico** das tabelas também é SYBSA (vejo `INSERT INTO funcionario_modulo_maquina` sem prefixo). Pra consultar tabelas via observador, precisaria de grant explícito (e queremos evitar — pedir SYBSA password ao usuário se necessário).

## Memória relacionada

Já registrado em `C:\Users\berna\.claude\projects\C--Projetos-GIT-SMSMarica\memory\`:
- `reference_salux_his.md` — overview do Salux desktop
- `reference_salux_observador.md` — setup da conta `salux_obs`
- `feedback_salux_oracle_producao.md` — PROD é intocável

Skill: `~/.claude/skills/salux-capturar-tela/SKILL.md`.

## Roadmap (não detalhado aqui)

Pós-discovery: backend de leitura (provavelmente .NET seguindo padrão SMSMais.server, ver `../CLAUDE.md`) + React+Vite consumindo. ADR ainda não escrito. **Nenhuma decisão arquitetural sem novo ADR no monorepo SMSMarica.**
