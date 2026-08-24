# Convenções — SMS Maricá

Este documento fixa como escrevemos e versionamos código no ecossistema SMSMarica. Mudanças aqui são feitas por PR com discussão registrada.

## 1. Idioma

- **Documentação**: pt-BR.
- **Identificadores de domínio** (entidades, value objects, comandos, eventos, endpoints): **pt-BR sem acento** — `Paciente`, `Veiculo`, `Alocacao`, `SessaoDeTranslado`. Motivo: alinhamento com glossário do município + evita problemas com ferramentas que não lidam bem com diacríticos em identificadores.
- **Identificadores técnicos/infraestrutura**: en-US — `Repository`, `Handler`, `Command`, `Query`, `Middleware`, `DbContext`, `Endpoint`, `Service`.
- **Mensagens de commit, PR, issue, changelog**: pt-BR.
- **Mensagens de erro para usuário final**: pt-BR.
- **Logs técnicos** (para devs/ops): pode ser en-US; não expor dados pessoais.

## 2. Git

### 2.1 Branching

- `main` — sempre deployável. Nada direto: entra via PR.
- Branches de trabalho nomeadas pela **entrega do plano**:
  - `s/<entrega>-<slug>` — backend (ex.: `s/s2.1-unidades`)
  - `f/<entrega>-<slug>` — frontend web
  - `c/<entrega>-<slug>` — app cidadão
  - `a/<entrega>-<slug>` — app agente
  - `d/<slug>` — documentação
- Branches longas são anti-padrão. PR em até 3 dias de vida, idealmente 1.

### 2.2 Commits — Conventional Commits

Formato:

```
<tipo>(<escopo opcional>): <descrição em pt-BR, imperativo>

<corpo opcional>

<rodapé opcional>
```

Tipos aceitos: `feat`, `fix`, `refactor`, `perf`, `docs`, `test`, `chore`, `build`, `ci`.

Escopo sugerido: nome do módulo/feature (`pacientes`, `veiculos`, `infra`, `auth`).

Exemplos:

```
feat(pacientes): adiciona endpoint de cadastro com validacao de CNS
fix(veiculos): corrige numeracao de assentos em fileira parcial
docs(adr): registra decisao de modular monolith
```

Commits de mesclagem (`Merge …`): evitar. Usar `rebase` + `squash` no PR.

### 2.3 Pull Requests

- Título: segue Conventional Commits (vira título do squash).
- Descrição obrigatória (template implícito):
  - **O que**: resumo técnico.
  - **Por que**: motivação de produto/arquitetura.
  - **Como testar**: passos objetivos.
  - **Checklist**: build verde, testes verdes, migrations aplicam em Postgres limpo, docs atualizados.
- Mínimo 1 aprovação para merge. Auto-merge habilitado após aprovação.

## 3. C# / .NET (`SMSMarica.server`)

### 3.1 Estilo

- `Nullable` enabled em toda a solution (via `Directory.Build.props`).
- `TreatWarningsAsErrors=true` — nada de warning ignorado.
- `file_scoped namespace` obrigatório (enforced via `.editorconfig`).
- `var` quando o tipo é óbvio; tipo explícito quando ajuda leitura.
- `async`/`await` em toda cadeia que toca IO. **Nunca** `.Result` ou `.Wait()` em código de produção.
- Prefira `record` para DTOs e value objects imutáveis; `class` para agregados.
- Expressões `switch` e pattern matching sempre que aplicável.

### 3.2 Organização

- **Namespace = caminho de pasta**.
- Uma classe por arquivo. Nome do arquivo = nome da classe.
- Dentro de um módulo, subdivida por feature se a camada crescer:
  ```
  Application/
    Pacientes/
      Commands/CriarPaciente/
        CriarPacienteCommand.cs
        CriarPacienteCommandHandler.cs
        CriarPacienteCommandValidator.cs
      Queries/ObterPacientePorId/
        ObterPacientePorIdQuery.cs
        ObterPacientePorIdQueryHandler.cs
  ```

### 3.3 Dependências

- Injeção via construtor. Sem service locator.
- Interfaces ficam no projeto que **consome**, implementações no projeto que **depende**. Ex.: `IRepository` em `Application`, `EfRepository` em `Infrastructure`.
- Evitar pacotes transitivos expostos na API pública do projeto.

### 3.4 Testes

- Nome do método de teste: `Metodo_Cenario_ResultadoEsperado` (pt-BR ou en-US conforme domínio).
- Um assert lógico por teste (pode usar `FluentAssertions` para compor).
- Testes de domínio: puros, sem mock, sem DB.
- Testes de aplicação: mocks de dependências externas, sem DB.
- Testes de integração: TestContainers com Postgres real.
- Cobertura **não** é meta; comportamento crítico coberto é a meta.

## 4. TypeScript / React (`SMSMais.front`)

### 4.1 Estilo

- `strict: true` no `tsconfig`.
- `eslint` + `prettier` configurados via preset do time (a definir no F1).
- Componentes em `PascalCase.tsx`; hooks em `camelCase.ts` com prefixo `use`.
- Preferir **componentes funcionais** + hooks. Sem classe.

### 4.2 Organização

```
src/
  app/              (router, providers, layout)
  features/
    pacientes/
      api/          (queries/mutations TanStack Query)
      components/
      pages/
      schemas/      (zod)
      types.ts      (gerados de OpenAPI)
  shared/
    ui/             (componentes base — design system)
    api/            (cliente http, interceptors)
    auth/
    lib/
```

### 4.3 Estado

- **Servidor**: TanStack Query (cache-first, invalidate on mutate).
- **UI local**: `useState`/`useReducer`.
- **Global leve**: Zustand se necessário; evitar Redux.

### 4.4 Tema Maricá

- Cores principais: vermelho (derivado da paleta da logo horizontal) + branco. Definir tokens em `src/shared/ui/theme.ts`.
- Logo horizontal: `prefeitura_marica_horizontal_vermelho_slogan.webp`. Respeitar área de respiro.
- Acessibilidade: contraste AA mínimo; foco visível em todos interativos.

## 5. Dart / Flutter (`SMSMarica.cidadao.app`, `SMSMarica.agente.app`)

### 5.1 Estilo

- `very_good_analysis` ou `flutter_lints` como base (a definir no scaffold).
- `effective_dart` como referência.
- `final` por padrão; `const` onde puder.

### 5.2 Arquitetura

- **Feature-first**:
  ```
  lib/
    app/              (main, router, theme)
    features/
      agenda/
        data/         (repositories, data sources)
        domain/       (entities, use cases)
        presentation/ (pages, widgets, providers)
    shared/
      api/
      auth/
      ui/
  ```
- **State**: Riverpod 2.x (preferido) ou Bloc (se o time preferir; decisão por app no C1/A1).
- **Router**: go_router.
- **HTTP**: dio + openapi-generator (gera cliente a partir do Swagger do server).

### 5.3 Plataformas

- `cidadao.app`: Android + iOS.
- `agente.app`: Android only — `flutter create --platforms=android`. Não incluir pasta `ios/` por engano.

## 6. Database / PostgreSQL

Ver [database.md](./database.md). Resumo:

- Schema `smsmarica` exclusivo. `snake_case` em tabelas/colunas.
- Migrations por módulo, nunca editar uma migration já aplicada — criar nova.

## 7. Segurança e dados pessoais

- **Nunca** logar CPF, CNS, documentos, endereço exato, GPS identificável.
- Secrets (connection strings, JWT signing keys) via variáveis de ambiente ou user-secrets em dev. Jamais em git.
- Endpoints que retornam dados sensíveis exigem autorização explícita (policy por perfil).
- Revisar LGPD ao adicionar nova coleta de dado.

## 8. Revisão de código — o que perguntar

- O nome está claro em pt-BR para domínio?
- A regra de negócio está no **Domain**, não espalhada na Application/Api?
- O módulo referenciou outro módulo? (proibido — ver [architecture.md §3.3](./architecture.md))
- Migration foi editada post-hoc? (proibido)
- Há teste para a regra que acabou de mudar?
- Há warning/nullable-warning silenciado? (proibido — ou justificar com comentário e `#pragma`)
