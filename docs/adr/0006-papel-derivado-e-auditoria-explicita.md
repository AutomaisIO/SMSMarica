# ADR-0006 — Papel derivado por relação e auditoria explícita por `excluido_em`

- **Status**: Aceito
- **Data**: 2026-05-23
- **Decisores**: Bruno (product/eng)
- **Supersede**: parcialmente o [ADR-0005](./0005-usuario-unificado-com-papeis.md) (princípios 3, 7, 8)

## Contexto

O ADR-0005 estabeleceu duas decisões que mostraram fragilidades práticas durante a primeira rodada de uso real:

1. **Discriminador `usuario.tipo_papel`** — coluna enum nullable em `usuario` que indica o papel ativo. Como ela é independente da existência da linha em `medico`/`motorista`/`paciente`, é possível ficar **inconsistente**: alguém edita `tipo_papel` à mão e perdemos a coerência ("usuário é médico mas não existe linha em `medico`", ou pior, o inverso). Não há mecanismo no banco que impeça a divergência.

2. **`<papel>.ativo` como marcador de exclusão lógica** — usar a mesma flag `ativo` (booleano) tanto para "acesso bloqueado temporariamente" quanto para "excluído permanentemente" misturou dois conceitos. Quando o operador removia uma pessoa, a linha permanecia "ativo=false", indistinguível de uma desativação temporária. Sem timestamp/quem fez, não há rastro auditável.

Casos reais que motivaram a revisão:

- "Excluí 'MARIANA' em Usuários, sumiu da lista, mas continua aparecendo em Médicos" — listagem de Médicos não filtrava `m.ativo`, e a exclusão pela tela de Usuários só marcou `usuario.ativo=false` sem cascatear.
- A lista de Usuários mostrava pessoas que já tinham papel (Médico, Motorista), porque o backend não filtrava por `tipo_papel`.
- Sem `excluido_por`/`excluido_em` não havia auditoria — não dava pra responder "quem removeu esse cadastro?".

## Decisão

### 1. Papel é derivado da existência de linha no schema, sem discriminador

`usuario.tipo_papel` é removido. O papel de um usuário passa a ser determinado por consulta:

- `Medico WHERE usuario_id = ? AND excluido_em IS NULL` → é médico
- `Motorista WHERE usuario_id = ? AND excluido_em IS NULL` → é motorista
- `Paciente WHERE usuario_id = ? AND excluido_em IS NULL` → é paciente
- Nenhuma linha → não tem papel (operador genérico)

Na camada EF, isso vira navegações 1:1 opcionais em `Usuario`:

```csharp
public Medico? Medico { get; set; }
public Motorista? Motorista { get; set; }
public Paciente? Paciente { get; set; }
```

O `UsuarioDto` expõe um campo derivado `PapelAtual: string?` ("Medico"/"Motorista"/"Paciente"/null) calculado no mapper a partir das navegações — o front nunca lê uma coluna discriminadora.

**Garantia 1:1 mantida** pelo UNIQUE index em `<papel>.usuario_id`, igual ao ADR-0005. A restrição "no máximo 1 papel" é checada na camada de serviço (em `PromoverAsync`) verificando se já existe linha em outra tabela.

### 2. `ativo` e `excluido_em` são conceitos distintos

| Campo | Tabela | Significado |
|-------|--------|-------------|
| `usuario.ativo` (bool) | `usuario` apenas | **Acesso liberado/bloqueado**. Login permitido se `true`. Pode ser alternado livremente para suspensão temporária. |
| `<entidade>.excluido_em` (timestamp nullable) | `usuario`, `medico`, `motorista`, `paciente` | **Exclusão lógica**. Não-null = excluído. Não volta a "vigente" sem ação explícita (reativação). |

Consequências:

- `medico.ativo`, `motorista.ativo`, `paciente.ativo` são **removidos**. As tabelas de papel não têm flag de acesso — quem controla acesso é o `Usuario`.
- Listagens passam a filtrar `WHERE excluido_em IS NULL` (não mais `WHERE ativo`).
- "Excluir Médico" cascateia: marca `medico.excluido_em` E `usuario.excluido_em`. Pessoa some do sistema. (Linhas permanecem para preservar referências históricas — laudos assinados, rotas concluídas.)
- "Bloquear acesso" toca apenas `usuario.ativo = false`, sem afetar a presença do papel nas listagens.

### 3. Auditoria nas 4 entidades-pessoa

`usuario`, `medico`, `motorista`, `paciente` ganham 6 colunas:

| Campo | Tipo | Quando preenchido |
|-------|------|-------------------|
| `criado_em` | `timestamptz` (NOT NULL) | Já existia. |
| `criado_por` | `uuid` (nullable) | Setado em insert pelo service via `IUsuarioAtualAccessor.UsuarioId`. |
| `atualizado_em` | `timestamptz` (nullable) | Setado em update. |
| `atualizado_por` | `uuid` (nullable) | Setado em update via accessor. |
| `excluido_em` | `timestamptz` (nullable) | Setado em "exclusão" (não-null = excluído). |
| `excluido_por` | `uuid` (nullable) | Quem executou a exclusão. |

`IUsuarioAtualAccessor` (`Core/Identidade`) abstrai a leitura do `sub` do JWT da requisição corrente; implementação `UsuarioAtualAccessor` (`Api/Auth`) lê de `IHttpContextAccessor`. Em contextos sem requisição (seed, jobs), `UsuarioId` retorna `null`.

### 4. Listagem de "Usuários" mostra apenas quem não tem papel

`IdentidadeService.ListarAsync` passa a filtrar:

```csharp
.Where(u => u.ExcluidoEm == null
            && u.Medico == null
            && u.Motorista == null
            && u.Paciente == null)
```

Coerente com o §16 do ADR-0005 ("tela 'Usuários' lista quem ainda não tem papel"). Médicos vivem em "Médicos", motoristas em "Motoristas", etc.

### 5. Bloqueio de DELETE de Usuário com papel

`IdentidadeService.DesativarAsync` lança `409 Conflict` se o usuário tem papel ativo, orientando o operador a usar a tela do papel correspondente. Defesa em profundidade contra chamadas diretas à API que ignoram o filtro da listagem.

## Alternativas consideradas

### A. Manter `tipo_papel` e adicionar um trigger SQL de consistência

**Prós:** menos refator. **Contras:** trigger acopla schema ao SGBD; já temos a navegação EF como fonte de verdade; o discriminador continua sendo um campo derivável. **Rejeitada.**

### B. Mover `ativo` para `usuario` mas manter `<papel>.ativo` como "papel suspenso"

**Prós:** permite suspender um médico (impedir de aparecer em escala) sem bloquear o login. **Contras:** complica o modelo (3 estados: ativo+vigente, ativo+suspenso, excluído). Suspensão de papel não é caso real hoje — quando aparecer, criamos novo campo (`suspenso_em`/`suspenso_por`). **Rejeitada por ora.**

### C. Auditoria em todas as entidades (Veiculo, Avaliacao, Tratamento, etc.)

**Prós:** rastreabilidade universal. **Contras:** migration grande, custo de implementação grande, sem caso de uso imediato. **Adiada** — escopo desta rodada é "pessoas" (Usuario + 3 papéis). Outras entidades ganham auditoria conforme demanda, em ADRs próprios.

## Consequências

### Positivas

- Não dá pra ficar inconsistente entre `usuario.tipo_papel` e linhas das tabelas filhas — o schema é a única fonte.
- Exclusão é explícita e auditável (`excluido_em IS NOT NULL`, `excluido_por = <quem>`).
- "Acesso bloqueado temporariamente" vs "removido do sistema" viraram conceitos distintos no nome e no efeito.
- Listagens de Usuário/Médico/Motorista/Paciente passam a ser mutuamente exclusivas (sem duplicação de uma mesma pessoa em telas diferentes).
- Toda criação/edição/exclusão grava quem executou — diagnóstico de incidentes melhora.

### Negativas

- Migration drop-de-colunas + drop-do-check (`ck_usuario_papel_exige_cpf`) é uma mudança breaking. Backfill: registros com `ativo=false` recebem `excluido_em = COALESCE(atualizado_em, criado_em)`.
- DTOs `MedicoDto`/`MotoristaDto` perderam o campo `Ativo` e ganharam `UsuarioAtivo` (espelhando `Usuario.Ativo`) — front teve que renomear.
- Após criar Médico/Motorista, front precisa fazer um `GET` extra pra resolver `usuarioId` (pra aplicar perfis/overrides). Possível melhoria futura: o `POST` retornar `{ id, usuarioId }` em vez de só `id`.

### Condições para revisitar

- Se aparecer requisito real de "suspender papel sem excluir" (médico de férias longas, motorista perdeu CNH temporariamente), avaliar adicionar `<papel>.suspenso_em`/`<papel>.suspenso_por` em vez de reintroduzir `<papel>.ativo`.
- Se a auditoria precisar contar **todas** as mudanças (não só create/update/delete) — quando isso vier, considerar tabela de eventos (`auditoria_evento`) em vez de mais colunas inline.

## Enforcement

- Code review: nova entidade-pessoa deve incluir as 6 colunas de auditoria desde a primeira migration.
- `CLAUDE.md`: referência este ADR na lista de regras não-negociáveis (substituindo as menções a `tipo_papel`/`<papel>.ativo` quando elas aparecerem).
- Services nunca devem ler `tipo_papel` (coluna removida); detecção de papel é via navegações 1:1.
