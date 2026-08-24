# SMS Maricá — Plataforma de saúde e mobilidade

Ecossistema de software para o programa de **transporte sanitário** da Secretaria Municipal de Saúde de Maricá (RJ): cadastro de pacientes, tratamentos com periodicidade, alocação em veículos, rastreamento de motoristas e canais para cidadão e operação.

## Subprojetos

| Pasta | O que é | Stack |
|-------|---------|-------|
| [`SMSMais.server/`](./SMSMais.server/) | API central e regras de negócio | .NET 10 + EF Core + PostgreSQL |
| [`SMSMais.front/`](./SMSMais.front/) | Painel web (operador + gestor) | React + Vite + TypeScript |
| [`SMSMarica.cidadao.app/`](./SMSMarica.cidadao.app/) | App do paciente | Flutter (iOS + Android) |
| [`SMSMarica.agente.app/`](./SMSMarica.agente.app/) | App do motorista | Flutter (Android only) |

Todos os clientes consomem exclusivamente a API do `SMSMais.server`.

## Documentação

Arquitetura, domínio e convenções estão em [`docs/`](./docs/):

- [`docs/architecture.md`](./docs/architecture.md) — visão do ecossistema, Modular Monolith, BuildingBlocks
- [`docs/domain.md`](./docs/domain.md) — glossário, modelo conceitual, invariantes
- [`docs/database.md`](./docs/database.md) — regra do schema `smsmarica`, migrations, naming
- [`docs/conventions.md`](./docs/conventions.md) — git, commits, estilo por stack
- [`docs/roadmap.md`](./docs/roadmap.md) — marcos e dependências
- [`docs/adr/`](./docs/adr/) — Architecture Decision Records

Instâncias de Claude Code devem começar por [`CLAUDE.md`](./CLAUDE.md).

## Regra crítica (leia antes de qualquer migration)

O backend compartilha a instância e o banco lógico PostgreSQL (`defaultdb`) com outros produtos. **Todo** o modelo do SMSMarica vive no schema `smsmarica` — zero dependência cross-schema. Detalhes e rationale em [`docs/adr/0001-schema-isolation.md`](./docs/adr/0001-schema-isolation.md).
