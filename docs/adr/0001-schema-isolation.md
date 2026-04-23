# ADR-0001 — Isolamento por schema PostgreSQL `smsmarica`

- **Status**: Aceito
- **Data**: 2026-04-22
- **Decisores**: Bruno (product/eng)

## Contexto

O SMSMarica vai rodar sobre a mesma instância PostgreSQL e o mesmo banco lógico (`defaultdb`) já usados por outro produto da casa (Automais.IO). Isso é um dado de infraestrutura — não temos liberdade (ainda) de subir um cluster próprio.

Coabitar dados de dois produtos no mesmo banco tem riscos conhecidos:

- Cross-joins "só um selectzinho" que viram acoplamento permanente.
- Migrations de um produto alterando/dropando tabelas do outro por engano.
- Permissões únicas ao role da conexão, difíceis de granular.
- Backup/restore coligados (restaurar um produto pode restaurar o outro).

## Decisão

Todo o modelo do SMSMarica vive **exclusivamente** no schema PostgreSQL `smsmarica`. Regras não-negociáveis:

1. `DbContext` base aplica `HasDefaultSchema("smsmarica")` — nenhuma entidade "escapa".
2. **Zero** FKs, views, joins ou migrations que referenciem objetos fora de `smsmarica`.
3. Quando o SMSMarica precisar de dado de outro produto, a integração é via **API**, não via SQL cross-schema.
4. Nome do schema: minúsculo, sem underscore — `smsmarica`.
5. Tabelas prefixadas por módulo (`paciente_*`, `veiculo_*`) — convenção, não regra técnica; facilita descoberta e auditoria.

## Alternativas consideradas

### A. Banco separado (`smsmarica_db`)

**Prós:** isolamento mais forte; backup/restore independente; roles simples.
**Contras:** requer provisionar um novo banco no cluster compartilhado — não disponível a curto prazo. Poderia ser adotado depois sem mudar o código, **desde que** a regra de schema seja respeitada desde o início (migrar vira um "dump/restore do schema smsmarica").

### B. Mesmo banco, sem schema dedicado (tabelas no `public`)

**Prós:** menos cerimônia em migrations e queries.
**Contras:** colisão de nomes com o outro produto garantida no médio prazo; impossível dar permissões diferentes; auditoria vira arqueologia. **Rejeitada.**

### C. Banco separado com Foreign Data Wrapper para queries cross-produto

**Prós:** mantém isolamento com "ponte" controlada.
**Contras:** complexidade operacional alta para um ganho que ainda não é necessidade. Guardado para o futuro se aparecer caso real.

## Consequências

- EF Core precisa do `HasDefaultSchema` consistente em todos os `DbContext` (cada módulo tem o seu, mas apontando para o mesmo schema). Centralizar em `BuildingBlocks.Infrastructure.ApplicationDbContextBase`.
- Comandos `dotnet ef migrations add` precisam do `--context` correto.
- Connection string pode apontar para `defaultdb` compartilhado; o isolamento é **no modelo**, não na string.
- Se um dia migrarmos para banco dedicado, a mudança é trocar a connection string — o modelo já está encapsulado.
- Restrição ergonomicamente tolerável: aceitamos "quase dois cliques de distância" para chegar nas tabelas via `smsmarica.paciente` em vez de `paciente`.

## Enforcement

- Code review: qualquer migration ou query que mencione outro schema é rejeitada.
- `Directory.Build.props` pode incluir analyzer customizado (futuro) que proibe referências a `public.` ou outros schemas em `.cs`.
- Testes de integração (TestContainers) rodam contra Postgres com **apenas** o schema `smsmarica` criado — qualquer dependência externa quebra.
