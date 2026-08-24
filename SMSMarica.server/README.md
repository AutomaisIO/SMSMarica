# SMSMarica.server

API backend do ecossistema SMSMarica. **3 projetos** em .NET 10 + EF Core + PostgreSQL.

Decisão arquitetural em [`../docs/adr/0004-arquitetura-tres-projetos.md`](../docs/adr/0004-arquitetura-tres-projetos.md).

## Estrutura

```
SMSMarica.server/
├── SMSMarica.sln
├── global.json                       (fixa SDK .NET 10)
├── Directory.Build.props             (Nullable, TWAE, net10.0)
├── Directory.Packages.props          (Central Package Management)
├── .editorconfig
├── src/
│   ├── SMSMais.Data/               (POCOs + DbContext + Configurations + Migrations)
│   │   ├── Entities/                 (16 POCOs em pt-BR)
│   │   ├── Configurations/           (16 IEntityTypeConfiguration, snake_case)
│   │   ├── Migrations/Initial.cs
│   │   ├── SmsMaisDbContext.cs     (HasDefaultSchema("smsmarica"))
│   │   └── DependencyInjection.cs    (AddData)
│   ├── SMSMais.Core/               (services + DTOs + validators + mappers)
│   │   ├── Common/Excecoes/          (NaoEncontrado, Validacao, Conflito)
│   │   ├── Common/ValueObjects/Gps.cs
│   │   ├── Pacientes/, Unidades/, Motoristas/, Avaliacoes/, Identidade/,
│   │   │  Veiculos/, Tratamentos/, Translado/, Rastreamento/
│   │   │                             (cada pasta: IService, Service, Mapper, Dtos/, Validators/)
│   │   └── DependencyInjection.cs    (AddCore)
│   └── SMSMais.Api/                (Program.cs + middleware + 9 controllers MVC)
│       ├── Program.cs                (Serilog + AddOpenApi + Scalar + ExceptionMiddleware)
│       ├── Middleware/ExceptionHandlingMiddleware.cs
│       ├── Controllers/              (1 controller por entidade, CRUD)
│       └── appsettings*.json
└── tests/
    └── SMSMais.Tests/              (xUnit + FluentAssertions + Testcontainers Postgres)
```

**Referências:** `Data ← nada` · `Core ← Data` · `Api ← Core + Data` · `Tests ← Core + Data + Api`.

## Como rodar

```bash
dotnet build                                     # 0 erros, 0 warnings
dotnet test                                      # requer Docker (Testcontainers)
dotnet run --project src/SMSMais.Api           # http://localhost:5080
```

URLs disponíveis após `run`:

- `http://localhost:5080/docs` — UI Scalar (substitui Swagger UI)
- `http://localhost:5080/openapi/v1.json` — spec OpenAPI
- `http://localhost:5080/health` — health check agregado (inclui DB)
- `http://localhost:5080/health/live` — liveness (só o processo; usado por orquestrador)
- `http://localhost:5080/health/ready` — readiness (só recursos externos tageados `ready` — hoje: DB)

**OpenAPI sempre ligado** (dev e prod) por decisão de produto.

### Flags de ambiente

| Env / config | Default | Descrição |
|---|---|---|
| `ConnectionStrings__DefaultDb` | (vazio) | Connection string Postgres. Obrigatória. |
| `AutoMigrate__Enabled` | `true` | Aplica migrations pendentes no startup em qualquer ambiente. Setar `false` se quiser migrar manualmente em prod. |
| `DetailedErrors` | `false` (em `Development` sempre `true`) | Expõe `exception.type/message/stackTrace` em `ProblemDetails.Extensions` quando uma exceção não mapeada ocorre. Ligue temporariamente em prod para diagnosticar, desligue depois. |
| `ASPNETCORE_ENVIRONMENT` | `Production` no systemd | Controla verbosidade, endpoints de dev, etc. |

## Como configurar o banco

A connection string **não é commitada** no repositório. `appsettings.json` traz um placeholder vazio; a string real vem de:

- **Dev local** → user-secrets (arquivo em `%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json`, fora do repo)
- **Produção / deploy** → variável de ambiente `ConnectionStrings__DefaultDb`

### Setar em dev via user-secrets

```bash
dotnet user-secrets --project src/SMSMais.Api set "ConnectionStrings:DefaultDb" \
  "Host=<host>;Port=25060;Database=defaultdb;Username=<user>;Password=<senha>;SSL Mode=Require;Trust Server Certificate=true"
```

A senha fica armazenada localmente no perfil do usuário Windows — **nunca no Git**.

### Cluster compartilhado (DigitalOcean)

O Postgres em produção é o `defaultdb` do cluster da Prefeitura na DigitalOcean, compartilhado com outros produtos. **Tudo do SMSMarica vive no schema `smsmarica`** ([ADR-0001](../docs/adr/0001-schema-isolation.md)). A migration `Initial` cria o schema automaticamente (`EnsureSchema`).

> `Trust Server Certificate=true` é OK pra dev/MVP. Para produção endurecida, trocar por `SSL Mode=VerifyFull` + `Root Certificate=<caminho do CA cert>` (DO disponibiliza na página do banco).

### Aplicar migrations

```bash
dotnet ef database update \
  --project src/SMSMais.Data \
  --startup-project src/SMSMais.Api
```

**Por padrão o `Program.cs` aplica migrations automaticamente no startup em qualquer ambiente** (`AutoMigrate:Enabled=true`). Se falhar (ex.: Postgres indisponível no boot), o processo continua vivo e `/health` reporta `Unhealthy` no check `db` até o banco voltar. Para desligar a migração automática em prod e gerenciar manualmente, seta `AutoMigrate__Enabled=false` no env file.

### Criar uma nova migration

```bash
dotnet ef migrations add <Nome> \
  --project src/SMSMais.Data \
  --startup-project src/SMSMais.Api
```

## Como adicionar uma nova entidade

Ver [`../docs/architecture.md §3.6`](../docs/architecture.md) — checklist de 8 passos. Use o módulo **Pacientes** como referência (template completo end-to-end).

## Deploy automático

Workflow do GitHub Actions em [`.github/workflows/deploy-server.yml`](../.github/workflows/deploy-server.yml). Dispara em:

- `push` em `main` que toque qualquer arquivo em `SMSMarica.server/**`
- manualmente via **Actions → Deploy SMSMarica.server → Run workflow**

### Secrets que o workflow usa

| Secret | Obrigatório | Para quê |
|---|---|---|
| `HOST_SMSMARICA` | sim | Hostname/IP do servidor |
| `USER_SMSMARICA` | sim | Usuário SSH (preferencialmente `root` ou user com `sudo -n`) |
| `PASS_SMSMARICA` | sim | Senha SSH |
| `DB_CONNECTION_SMSMARICA` | opcional | Connection string Postgres. Se setado, o workflow grava em `/etc/smsmarica-server/env` a cada deploy. Se não, o arquivo precisa ser configurado manualmente uma vez. |

> Migração para chave SSH no lugar de senha é um TODO de hardening — deixa no backlog.

### O que o workflow faz no servidor (idempotente)

1. Instala `curl`/`tar` se faltarem (apt).
2. **Instala .NET 10 ASP.NET Core runtime** em `/opt/dotnet` e linka `/usr/local/bin/dotnet` — só se ainda não estiver presente.
3. Cria o usuário de sistema `smsmarica` (sem shell, sem home dir utilizável).
4. Garante `/opt/smsmarica/server` e `/etc/smsmarica-server/`.
5. Escreve `/etc/smsmarica-server/env` (chmod 600, owner `smsmarica`) se o secret `DB_CONNECTION_SMSMARICA` estiver presente.
6. Cria o unit systemd `/etc/systemd/system/smsmarica-server.service` (se ainda não existe), `daemon-reload` e `enable`.
7. **Para** o serviço se estiver ativo.
8. Substitui tudo em `/opt/smsmarica/server` pelo novo artefato.
9. **Inicia** o serviço.
10. Valida `is-active`; em falha, imprime `journalctl -n 80` e aborta.

### Layout final no servidor

```
/opt/dotnet/                                (runtime .NET 10 ASP.NET Core)
/opt/smsmarica/server/                      (binário + dependências da publish)
/etc/smsmarica-server/env                   (chmod 600 — connection string)
/etc/systemd/system/smsmarica-server.service
```

Serviço escuta em `http://0.0.0.0:5080`. Coloque um nginx na frente para TLS/443 se for exposto publicamente.

### Logs e operação no servidor

```bash
systemctl status smsmarica-server
journalctl -u smsmarica-server -f        # logs ao vivo
journalctl -u smsmarica-server -n 200    # últimas 200 linhas
systemctl restart smsmarica-server       # restart manual

# Estado detalhado do health (inclui DB):
curl http://127.0.0.1:5080/health | jq
curl http://127.0.0.1:5080/health/ready | jq
```

### Diagnóstico quando CRUDs voltam 500

1. `curl https://<host>/health/ready` — se `db` estiver `Unhealthy`, o problema é banco (conexão/credenciais/migração).
2. `journalctl -u smsmarica-server -n 200 | grep -E "Erro|Falha|Exception"` — o log do Serilog tem a exceção original completa (tipo + mensagem + stack).
3. Para expor a causa raiz no próprio response HTTP temporariamente, adicione `DetailedErrors=true` em `/etc/smsmarica-server/env` e reinicie o serviço. **Desligue depois**, senão stack traces ficam expostos publicamente.

## Regras

- **Schema `smsmarica` exclusivo** — zero referências cross-schema. Ver [`../docs/database.md`](../docs/database.md) e [`../docs/adr/0001-schema-isolation.md`](../docs/adr/0001-schema-isolation.md).
- **Errors via exceções tipadas** — services lançam `NaoEncontradoException`/`ConflitoException`/`ValidacaoException` (em `SMSMais.Core/Common/Excecoes/`); middleware mapeia para `ProblemDetails`.
- **Identificadores em pt-BR para domínio** (`Paciente`, `Veiculo`); en-US para infra (`DbContext`, `Service`).
- **Controllers MVC** (`[ApiController]`), 1 por entidade, CRUD em 5 actions.

Ver também [`../CLAUDE.md`](../CLAUDE.md) para guidance de instâncias de Claude Code.
