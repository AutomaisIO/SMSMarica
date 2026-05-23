# ADR-0005 — Usuário unificado com papéis profissionais 1:1

- **Status**: Aceito
- **Data**: 2026-05-23
- **Decisores**: Bruno (product/eng)

## Contexto

O sistema tem **três tabelas de "pessoa"** independentes — `paciente`, `usuario`, `motorista` — que compartilham ~12 campos base (`nome_completo`, `cpf`, `data_nascimento`, `telefone`, `endereco`, `foto_base64`, etc.) e zero integridade referencial entre si. Mesmo CPF pode existir nas três sem que o sistema saiba.

O roadmap próximo inclui **Médico, Enfermeiro, Recepcionista, Admin** — cada novo "tipo de pessoa" repetiria o mesmo padrão de duplicação, multiplicaria validações e dificultaria saber "essa pessoa já está cadastrada em outro papel?".

Restrições levantadas durante a discussão:

- Cada usuário tem **no máximo 1 papel profissional ativo** (médico não é motorista; é impositivo).
- Quem usa cada tela é diferente: tela "Médicos" lista médicos, tela "Motoristas" lista motoristas; tela "Usuários" lista quem ainda não tem papel (administrativos genéricos).
- Ao cadastrar Médico, se o CPF já existir como Usuario, o fluxo deve **promover** aquele usuário em vez de criar duplicata.
- Ao remover Médico que tem histórico (laudo assinado, etc.), soft-delete; sem histórico, hard-delete e o usuário volta a aparecer na lista "Usuários".

## Decisão

`Usuario` passa a ser o **núcleo de identidade** de toda pessoa que se autentica. Cada **papel profissional** é uma entidade dedicada com relação **1:1 com Usuario** carregando apenas os campos específicos do papel (CRM, CNH, COREN…).

### Princípios não-negociáveis

1. **Usuario é o núcleo.** Toda pessoa autenticável tem 1 linha em `usuario`. Credenciais (`senha_hash`, `deve_trocar_senha`, `ultimo_acesso_em`) e dados pessoais base (nome, CPF, RG, data de nascimento, sexo, endereço, foto, telefone, e-mail) vivem **só** lá.

2. **Cada papel = 1 tabela 1:1 com `usuario`.** Tabelas `medico`, `motorista`, `enfermeiro`, `recepcionista`, `paciente` carregam **apenas** campos específicos do papel. FK `usuario_id` UNIQUE garante 1:1 estrito. Zero campo "base de pessoa" duplicado nessas tabelas.

3. **Um Usuario tem no máximo 1 papel profissional.** Discriminador `usuario.tipo_papel` (enum nullable) marca qual. Validação primária no service `UsuarioService.PromoverAsync`; reforço por DB via `CHECK (tipo_papel IS NULL OR cpf IS NOT NULL)` e UNIQUE filtered em `cpf`.

4. **Sem herança EF (TPT/TPH).** Composição via FK 1:1 entre POCOs independentes. Razão: simplicidade nas queries cross-papel, manutenção do padrão atual (POCOs simples, [ADR-0004](./0004-arquitetura-tres-projetos.md)), e flexibilidade para campos divergentes.

5. **CPF é único globalmente em `usuario`.** `UNIQUE INDEX cpf WHERE cpf IS NOT NULL`. Documentos profissionais (CRM, CNH, COREN) são únicos dentro da própria tabela de papel.

6. **Promoção (Usuario → papel):** se CPF já existe em `usuario`, o endpoint de criação do papel retorna `409 Conflict` com o usuário existente. Front confirma com o operador, e o endpoint `POST /<papel>/promover { usuarioId, ...campos }` cria apenas a linha no papel + atualiza `usuario.tipo_papel`. **Nunca duplica.**

7. **Demoção/exclusão de papel é condicional:**
   - **Sem dependências históricas:** `DELETE` hard da linha em `medico` (ou outro), `usuario.tipo_papel = NULL`. Usuário volta a aparecer em "Usuários".
   - **Com dependências históricas** (médico assinou laudo, motorista conduziu rota, etc.): soft-delete (`<papel>.ativo = false`). Linha permanece para preservar referência.
   - Operador escolhe **explicitamente** "eliminar perfil profissional" vs "desativar usuário inteiro" — ações distintas, fluxos distintos.

8. **Exclusão de Usuario inteiro segue a mesma regra.** Hard-delete só sem vínculos transversais (avaliações emitidas, auditoria); caso contrário soft via `usuario.ativo = false`.

### Convenções de nomenclatura

`Perfil` (existente, RBAC) **≠** `Papel` (novo, profissão). Distinção obrigatória:

| Conceito | Termo | Tabela | Classe |
|----------|-------|--------|--------|
| Identidade + credenciais + dados base de pessoa | Usuario | `usuario` | `Usuario` |
| Bag de permissões RBAC (já existe) | Perfil | `perfil`, `usuario_perfil`, `permissao_perfil` | `Perfil` |
| Papel profissional (NOVO) | Papel | `medico`, `motorista`, `enfermeiro`, `recepcionista`, `paciente` | `Medico`, `Motorista`, `Enfermeiro`, `Recepcionista`, `Paciente` |
| Discriminador | `TipoPapel` (enum) | `usuario.tipo_papel` | `TipoPapel` |

Enum `TipoPapel`: `Medico` · `Enfermeiro` · `Motorista` · `Recepcionista` · `Paciente` · `Admin` (sem tabela; só flag) · `null` (operador genérico sem papel).

## Alternativas consideradas

### A. Manter status quo (3 tabelas duplicadas, mais 4 tabelas novas para Médico/Enfermeiro/Recepcionista/Admin)

**Prós:** zero refatoração imediata.
**Contras:** duplicação cresce; CPF nunca é fonte única; cada nova profissão é uma reescrita de "campos base de pessoa". **Rejeitada.**

### B. Herança EF (TPT — Table-per-Type)

**Prós:** mais "OO"; EF emite JOIN automático em queries polimórficas.
**Contras:** dificulta query "todos os usuarios sem papel" sem hacks; muda o padrão atual do projeto (POCOs simples); 1 usuário ↔ múltiplos papéis fica esquisito (multi-table inheritance no EF tem limitações). **Rejeitada.**

### C. Herança EF (TPH — Table-per-Hierarchy, tabela única `usuario` com discriminator)

**Prós:** uma tabela só, queries diretas.
**Contras:** acumula colunas nulas (todos os campos de todos os papéis na mesma tabela); índices únicos por papel (CRM, CNH) ficam filtered + complexos; alterar um papel toca a tabela de TODOS. **Rejeitada.**

### D. Campos JSONB de extensão por papel em `usuario`

**Prós:** zero migrations para novos papéis.
**Contras:** perde tipagem, indexação seletiva, validação por banco; choca com o padrão atual (EF Core + migrations estritas). **Rejeitada.**

## Consequências

### Positivas

- CPF é fonte única — a pergunta "essa pessoa já está cadastrada?" tem resposta exata.
- Telas claras: cada profissão tem sua tela com seus campos; "Usuários" só lista quem não tem papel.
- Adicionar novo papel = 1 tabela + 1 valor no enum + 1 controller (não toca os outros papéis).
- Histórico preservado por soft-delete específico do papel sem "sujar" `usuario`.
- Endpoint `/auth/login` único atende qualquer papel — autenticação não muda por profissão.

### Negativas

- Migração do `Motorista` existente é complexa (FKs em `rota_diaria`, `ponto_gps`, `avaliacao` apontam para o PK atual). Estratégia: preservar PK migrando schema com SQL puro no `MigrationBuilder` (ver Fase 3 do plano de implementação).
- Migração do `Paciente` é mais delicada por causa do `cidadao.app` em produção consumindo `GET /pacientes/{id}`. Resolvido mantendo o endpoint como **facade** sobre o JOIN `usuario × paciente`. Decisão dedicada virá em ADR-0006 quando essa fase chegar.
- Listagens passam a usar JOIN — performance precisa ser monitorada; índices em `<papel>.usuario_id` e `<papel>.ativo` desde o início.
- Ambiguidade conceitual `Perfil` vs `Papel` exige reforço em onboarding (atenuado por glossário em [domain.md](../domain.md) §1 e este ADR).

### Condições para revisitar

- Se aparecer caso real de usuário com 2 papéis simultâneos (médico que dirige na zona rural). Revisão: relaxar princípio 3 e remover `usuario.tipo_papel` em favor de listar papéis ativos por presença de linha nas tabelas filhas.
- Se a tabela `usuario` ultrapassar ~25 colunas, considerar extrair "dados pessoais" para tabela própria (`pessoa`) com `usuario.pessoa_id`.

## Enforcement

- Code review: qualquer nova entidade tipo-pessoa precisa optar entre "papel de Usuario (1:1)" e "entidade de domínio independente" e justificar.
- `docs/architecture.md §3.6` (checklist de nova entidade) distingue os dois caminhos.
- `CLAUDE.md` referencia este ADR como regra não-negociável.
