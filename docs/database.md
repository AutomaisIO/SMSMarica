# Banco de dados — SMS Maricá

## 1. Regra crítica: isolamento por schema

O SMSMarica **compartilha** a instância e o banco lógico PostgreSQL com outros produtos (notadamente Automais.IO). O banco convencional é `defaultdb`.

**Todo** o modelo do SMSMarica vive **exclusivamente** no schema:

```
smsmarica
```

Decisão registrada em [ADR-0001](./adr/0001-schema-isolation.md).

### O que isso implica

- Nenhuma tabela do SMSMarica fora do schema `smsmarica`.
- Nenhuma FK, view, trigger ou migration referenciando objetos de outros schemas.
- `DbContext` base aplica `HasDefaultSchema("smsmarica")` — não depender de cada entidade declarar.
- Connection string pode apontar para `defaultdb` compartilhado; isolamento é **por schema, não por banco**.

### O que NÃO fazer

- `JOIN` com tabelas de outro produto ("só um selectzinho rápido"). Se precisar de dado externo, vem por API.
- Usar `search_path` do role para "achar" tabelas fora do schema — isso mascara a dependência e volta a morder depois.
- Criar migrations que dropam/alteram objetos não pertencentes ao SMSMarica, mesmo que existam no mesmo banco.

## 2. Organização por módulo

Cada módulo do backend tem seu próprio `DbContext` apontando para `smsmarica`. Não há um "DbContext gigante".

**Prefixo de tabela por módulo** evita colisões e deixa claro quem é dono:

| Módulo | Prefixo | Exemplos |
|--------|---------|----------|
| Pacientes | `paciente_` | `paciente`, `paciente_documento`, `paciente_acompanhante` |
| Tratamentos | `tratamento_` | `tratamento`, `tratamento_periodicidade`, `tratamento_sessao` |
| Unidades | `unidade_` | `unidade` |
| Veiculos | `veiculo_` | `veiculo`, `veiculo_fileira`, `veiculo_assento` |
| Motoristas | `motorista_` | `motorista` |
| Translado | `translado_` | `translado_rota`, `translado_alocacao` |
| Rastreamento | `rastreamento_` | `rastreamento_ponto_gps`, `rastreamento_geofence`, `rastreamento_evento_chegada` |
| Avaliacoes | `avaliacao_` | `avaliacao` |
| Identidade | `identidade_` | `identidade_usuario`, `identidade_perfil` |

## 3. Convenções de naming

- **snake_case** para tabelas e colunas (convenção Postgres).
- Chaves primárias: `id` (UUID v7 quando suportado, senão UUID v4).
- Chaves estrangeiras: `<entidade>_id` (ex.: `paciente_id`).
- Timestamps: `criado_em`, `atualizado_em` (sempre `timestamptz`, UTC).
- Soft delete: `removido_em` nulável — quando aplicável; a decisão é por agregado, não global.
- Booleanos: prefixo `eh_` ou `tem_` (`eh_ativo`, `tem_acompanhante`).
- Enums: colunas `varchar` com check constraint ou enum nativo Postgres — preferir **smallint + tabela lookup** para enums que podem evoluir.

## 4. Migrations

- Uma pasta de migrations **por módulo**, em `Module.Infrastructure/Migrations/`.
- Cada módulo tem seu `DbContext`, então cada `dotnet ef migrations add` especifica o projeto-alvo.
- **Nunca** amend ou rewrite de uma migration que já foi aplicada em ambiente compartilhado. Nova migration corrigindo.
- O Host aplica migrations na inicialização em ordem determinística (módulos com dependência vêm depois dos dependidos — mas como módulos **não** compartilham tabelas, a ordem importa pouco).
- Em ambiente de teste, migrations rodam contra Postgres real via TestContainers.

### Comando padrão (a partir de `SMSMarica.server/`)

```bash
dotnet ef migrations add <Nome> \
  --project src/Modules/<Modulo>/SMSMarica.Modules.<Modulo>.Infrastructure \
  --startup-project src/Host/SMSMarica.Api \
  --context <Modulo>DbContext
```

## 5. GPS / Time-series

Dados de GPS em alta frequência (posição dos motoristas) vão inicialmente para `rastreamento_ponto_gps` em Postgres. Quando o volume pressionar (hoje: não é problema):

- Opção 1: **TimescaleDB** (continua em Postgres, mesmo cluster, hypertable no schema `smsmarica`).
- Opção 2: **InfluxDB** externa, com `rastreamento_ponto_gps` virando buffer/Outbox para ingestão.

A decisão será registrada em ADR próprio quando chegar o momento. Até lá: Postgres puro, índice em `(motorista_id, capturado_em)`, retenção a definir.

## 6. LGPD

Dados sensíveis de pacientes (documentos, saúde) implicam cuidado com:

- Retenção: políticas a serem definidas com a Secretaria.
- Criptografia em repouso: a cargo da infra do cluster Postgres.
- Anonimização para dados analíticos/dashboards: views em schema separado futuro (ainda dentro de `smsmarica`) com dados agregados.
- Logs **não devem** conter CPF, CNS, documentos, ou GPS individual identificável.
