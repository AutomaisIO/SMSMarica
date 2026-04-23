# ADR-0004 — Arquitetura simplificada de 3 projetos (Data + Core + Api)

- **Status**: Aceito
- **Data**: 2026-04-22
- **Decisores**: Bruno (product/eng)
- **Supersedes**: [ADR-0002](./0002-modular-monolith.md) (Modular Monolith + Clean Architecture por módulo)

## Contexto

O scaffold inicial do `SMSMarica.server` seguiu [ADR-0002](./0002-modular-monolith.md) — Modular Monolith com 4 camadas por módulo. Resultado prático: **44 projetos** (4 BuildingBlocks + 9 módulos × 4 camadas + Host + 3 testes), build verde, mas avaliação imediata do produto: **complexo demais para o estágio**.

Pontos concretos de atrito:

- Para CRUDs de entidades simples (Paciente, Unidade, Veículo etc.), a divisão Domain → Application → Infrastructure → Api adicionava 4 arquivos e 4 namespaces para o que cabe num service de 80 linhas.
- O aparato (BuildingBlocks com `Entity<TId>`, `AggregateRoot`, `Result<T>`, `IApiModule` por reflexão, MediatR + pipeline de validação/logging/transação) **antecipava** decisões que ainda não são problemas reais.
- Setup inicial (4 camadas + DI por módulo + descobrimento por reflection) já consumiu mais ciclos que o domínio merece.
- Quando o produto crescer, **modularizar é fácil**; o oposto (extrair de modular monolith para algo mais simples) é raro mas possível. O custo da reversão agora é baixo e o ganho de velocidade no MVP é alto.

## Decisão

Substituir Modular Monolith por **3 projetos** + 1 de testes:

```
SMSMarica.server/
├── src/
│   ├── SMSMarica.Data/    (POCOs + DbContext + Configurations + Migrations)
│   ├── SMSMarica.Core/    (services + DTOs + validators + mappers)
│   └── SMSMarica.Api/     (controllers MVC + middleware + Program.cs)
└── tests/
    └── SMSMarica.Tests/   (xUnit + Testcontainers)
```

**Referências:** `Data ← nada` · `Core ← Data` · `Api ← Core + Data`.

**Mantido (das decisões anteriores):**

- .NET 10 LTS
- Schema PostgreSQL `smsmarica` exclusivo (ADR-0001)
- Identificadores em pt-BR para domínio
- 9 entidades mapeadas (Pacientes, Tratamentos, Unidades, Veiculos, Motoristas, Translado, Rastreamento, Avaliacoes, Identidade) — agora como POCOs simples em `Data/Entities/`, organizadas por **pasta** dentro de cada projeto (não por csproj)
- FluentValidation
- Mapperly
- Central Package Management

**Removido:**

- BuildingBlocks (4 projetos)
- Modular Monolith de 9 × 4 csproj
- MediatR e CQRS
- `Result<T>` e Result-pattern (substituído por **exceções tipadas** + middleware)
- ASP.NET Core Identity (adiado até auth virar prioridade)
- OpenTelemetry (adiado)
- Swashbuckle (substituído por **OpenAPI nativo do .NET 10 + Scalar**)

**Novo:**

- **Controllers MVC** (não Minimal APIs) — `[ApiController]`, 1 controller por entidade, CRUD via `HttpGet/Post/Put/Delete`
- **Services simples** com `SmsMaricaDbContext` injetado direto — sem repositório por cima
- **Exceções tipadas** (`NaoEncontradoException`, `ConflitoException`, `ValidacaoException`) → middleware mapeia para `ProblemDetails`
- **Swagger sempre ligado** (dev e prod) — `/openapi/v1.json` (spec) + `/docs` (Scalar UI)

## Alternativas consideradas

### A. Manter Modular Monolith (ADR-0002)

**Prós:** fronteiras fortes, escalável, paralelismo entre instâncias.
**Contras:** custo de manutenção desproporcional ao tamanho do produto agora; complica o ramp-up de novos contribuidores; cada nova feature precisa atravessar 4 camadas + 1 csproj.
**Rejeitada** após uso real.

### B. Vertical Slice Architecture (1 pasta por feature, sem camadas)

**Prós:** velocidade máxima por feature.
**Contras:** mistura persistência com regra; difícil reaproveitar Data entre features; testes ficam acoplados.
**Rejeitada.**

### C. Single project (tudo em um csproj)

**Prós:** zero cerimônia.
**Contras:** perde fronteiras úteis (Data isolado permite reuso por jobs/migrations sem subir Api).
**Rejeitada.**

## Consequências

### Positivas

- Onboarding ~3× mais rápido — quem entra entende o codebase em uma sessão.
- Cada CRUD é literalmente: 1 entidade + 1 config + 3 DTOs + 1 service + 1 controller.
- Migrations centralizadas em `SMSMarica.Data/Migrations/` — uma `Initial` cobre todas as 16 tabelas.
- Build mais rápido (4 csproj vs 44).
- Possível abrir um IDE e navegar tudo sem se perder.

### Negativas

- Não há mais barreira de compilador entre subdomínios — convenção (pastas + namespaces) substitui a barreira física.
- Se um subdomínio crescer ao ponto de virar candidato a serviço próprio, a extração será mais trabalhosa do que seria com modular monolith. Aceitamos esse trade-off.
- Sem Result<T>, exceções fazem o controle de fluxo de erro — o middleware de exceções precisa estar bem testado.

### Condições para revisitar

- Quando 2+ subdomínios precisarem ciclos de release independentes.
- Quando aparecer um time dedicado a um subdomínio específico.
- Quando o tamanho de qualquer pasta `Core/<X>/` ultrapassar ~30 arquivos consistentemente.

Quando revisitar: novo ADR (ex.: `0010-modularizar-rastreamento.md`) avaliando extração específica.
