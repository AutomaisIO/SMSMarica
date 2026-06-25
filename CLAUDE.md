# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository state

**Multi-project monorepo.** Backend `SMSMarica.server` está em **3 projetos** (Data + Core + Api) + 1 de testes — ver [ADR-0004](./docs/adr/0004-arquitetura-tres-projetos.md). CRUD coberto bem além das 9 entidades originais (Pacientes, Unidades, Motoristas, Avaliacoes, Usuarios, Veiculos, Tratamentos, Rotas, Rastreamento) — também Laudos, SolicitacoesExame, Procedimentos SIGTAP, Médicos, Perfis, Translados, Tipos de Exame, etc.

**Demais subprojetos no monorepo** (nem todos no README/stack antigos):
- `SMSMarica.front` — **painel web já implementado** (não é mais README-only): React + Vite + TS com ~20 features em `src/features/` (auth, pacientes, laudos, solicitacoes-exame, ia, pacs, procedimentos-sigtap, medicos, perfis, rastreamento, translados, tratamentos, unidades, usuarios, veiculos…).
- `Automais.Fhir` — **serviço FHIR R4 autônomo** ([ADR-0010](./docs/adr/0010-servico-fhir-autonomo.md)), solução própria (`Automais.Fhir.slnx`), 3 camadas (Data/Core/Api) espelhando o server. Persistência JSONB + Firely SDK, schema `fhir` próprio, DbContext próprio (`ConnectionStrings:FhirDb`). Já em produção (porta 5081). É o hub canônico clínico; demais sistemas falam com ele via API FHIR. Tem `Automais.Fhir/README.md` próprio.
- `SMSMarica.EquipamentoSim` — simulador de equipamento DICOM (Python 3.11+, `pynetdicom`/`pydicom`, CLI `equipamento`) para testar o ciclo Solicitação de Exame → Worklist → Execução.
- `Salux` — engenharia reversa do Salux HIS (Oracle 12c do HCML). **Tem CLAUDE.md próprio com regras não-negociáveis** (PROD Oracle é read-only absoluto via `scripts/_guard.py`; `capturas/` e `.env` são gitignored por conterem PII). Ler `Salux/CLAUDE.md` antes de tocar nessa pasta.
- `SMSMarica.arquivos.pwa` — **PWA "Arquivos Saúde Maricá"** (React + Vite + TS) para digitalizar exames em papel pelo celular e anexá-los à anamnese, via **ponte por QR** (sem login; token de upload escopado). Domínio `arquivos.smsmarica.online`; deploy `deploy-arquivos.yml` → `/var/www/smsmarica-arquivos`. Ver [ADR-0019](./docs/adr/0019-anexos-exame-pwa-qr-armazenamento.md).
- `SMSMarica.cidadao.app` está scaffoldado (Flutter, login mock + perfil consumindo `GET /pacientes/{id}`). `SMSMarica.agente.app` ainda é README-only.

Documentation is in **Portuguese (pt-BR)**. Match that language for docs, commit messages, and code comments. Identifiers follow [`docs/conventions.md §1`](./docs/conventions.md): pt-BR for domain (`Paciente`, `Veiculo`), en-US for technical infrastructure (`DbContext`, `Service`, `Controller`).

## Start here

Always read the canonical documentation in [`docs/`](./docs/) before making architectural decisions:

| Arquivo | Quando consultar |
|---------|------------------|
| [`docs/visao.md`](./docs/visao.md) | **Norte estratégico** — hub FHIR R4 da SMS Maricá. Consultar antes de decisões que afetam modelagem do cidadão, identificadores ou integração com sistemas externos. |
| [`docs/architecture.md`](./docs/architecture.md) | Layout 3-projetos, organização por entidade, fluxo de erro |
| [`docs/domain.md`](./docs/domain.md) | Glossário + invariantes do domínio antes de modelar qualquer entidade |
| [`docs/database.md`](./docs/database.md) | Schema `smsmarica`, naming, migrations |
| [`docs/conventions.md`](./docs/conventions.md) | Git, commits, estilo por stack |
| [`docs/roadmap.md`](./docs/roadmap.md) | Marcos M1..M7 e dependências |
| [`docs/pacs.md`](./docs/pacs.md) | Servidor de imagens (dcm4chee-arc), DICOMweb, integração com `features/pacs` |
| [`docs/adr/`](./docs/adr/) | Decisões arquiteturais registradas (0001 schema, 0003 Android-only, 0004 três projetos, 0005 usuário unificado, 0006 papel derivado, **0007 schema FHIR separado**, **0008 mapeamento clínico FHIR**, **0009 identidade e proveniência multi-PEP**, **0010 serviço FHIR autônomo (Automais.Fhir)**, **0011 módulo IA (consulta em linguagem natural multi-alvo)**, **0012 agendamento local + integração SISREG só-leitura**, **0013 agenda multi-recurso (especialidade/médico/equipamento)**, **0014 importação de PEPs no backend (canal Oracle persistente, multi-base)**) |

Plano de implementação: `C:\Users\berna\.claude\plans\deep-gathering-kahn.md`.

## Regras não-negociáveis (resumo)

As regras abaixo não podem ser violadas sem novo ADR.

1. **Dois schemas: `smsmarica` (negócio, pt-BR) + `fhir` (canônico FHIR R4, en)** — [ADR-0001](./docs/adr/0001-schema-isolation.md) + [ADR-0007](./docs/adr/0007-schema-fhir-separado.md). Identidade do cidadão/profissional (Patient, Practitioner, identifiers, names, addresses, telecoms, contacts, photos, qualifications, consents, lookups) vive em `fhir.*` em inglês. Regras de negócio (Usuario/RBAC, Motorista, Tratamento, RotaDiaria, Laudo, SolicitacaoExame, etc.) vivem em `smsmarica.*` em pt-BR. `HasDefaultSchema("smsmarica")` continua + cada configuration FHIR chama `.ToTable(..., schema: "fhir")`. **FKs cross-schema só na direção `smsmarica → fhir`** (proibida a inversa). Um único `SmsMaricaDbContext`.

2. **Arquitetura 3-projetos** — [ADR-0004](./docs/adr/0004-arquitetura-tres-projetos.md). Backend é `SMSMarica.Data` + `SMSMarica.Core` + `SMSMarica.Api`. Não criar projetos novos para "modular" subdomínios — usar pastas dentro de cada projeto. Quem quiser modular monolith de novo precisa de novo ADR. (ADR-0002 está **superseded**.)

3. **Dependências entre projetos:**
   - `Data` ← nada
   - `Core` ← `Data`
   - `Api` ← `Core` + `Data`
   - `Tests` ← `Core` + `Data` + `Api`

4. **Migrations imutáveis** — uma migração já aplicada nunca é editada. Correção vira nova migration. Pasta única `SMSMarica.Data/Migrations/`.

5. **`agente.app` é Android-only** — [ADR-0003](./docs/adr/0003-flutter-android-only-agente.md). Não gerar pasta `ios/` nem condicionais `Platform.isIOS` nesse projeto.

6. **Erros via exceções tipadas** — services lançam `NaoEncontradoException`/`ConflitoException`/`ValidacaoException` (em `SMSMarica.Core/Common/Excecoes/`). `ExceptionHandlingMiddleware` na Api mapeia para `ProblemDetails`. Não retornar `null` em vez de lançar.

7. **OpenAPI sempre exposto** — `MapOpenApi()` + `MapScalarApiReference("/docs")` ficam **fora** de `if (env.IsDevelopment())`. Decisão de produto: spec acessível em dev e prod.

8. **Usuario stripped + papel por FK nullable** — [ADR-0007](./docs/adr/0007-schema-fhir-separado.md) (supersede parcial de [ADR-0005](./docs/adr/0005-usuario-unificado-com-papeis.md) + [ADR-0006](./docs/adr/0006-papel-derivado-e-auditoria-explicita.md)). `usuario` carrega apenas Email/SenhaHash/RBAC/Ativo/DeveTrocarSenha/NomeExibicao (denormalizado). Identidade clínica vive em `fhir.patient` (cidadão) ou `fhir.practitioner` (médico/enfermeiro) ou inline em `smsmarica.motorista` (não é entidade clínica FHIR). Papel determinado pela FK nullable setada: `Usuario.PatientId?` / `PractitionerId?` / `MotoristaId?`. CHECK constraint `ck_usuario_papel_unico` garante no máximo 1 setada. Unique indexes filtrados em cada FK. **`Papel` ≠ `Perfil`**: Papel é entidade impositiva (1:1); Perfil é bag de permissões RBAC (N:N).

9. **Auditoria e exclusão lógica** — [ADR-0006](./docs/adr/0006-papel-derivado-e-auditoria-explicita.md) + [ADR-0007](./docs/adr/0007-schema-fhir-separado.md). `usuario`/`motorista` (smsmarica): `criado_em`/`criado_por`/`atualizado_em`/`atualizado_por`/`excluido_em`/`excluido_por`. `fhir.patient`/`fhir.practitioner` usam o equivalente em inglês: `created_at`/`created_by`/`updated_at`/`updated_by`/`deleted_at`/`deleted_by`. `usuario.ativo` = acesso (temporário); `excluido_em`/`deleted_at IS NOT NULL` = excluído permanente. Listagens filtram pelo respectivo soft-delete. Services obtêm "quem fez" via `IUsuarioAtualAccessor`.

## Stack

| | Stack | Observação |
|---|---|---|
| `SMSMarica.server` | .NET 10 LTS, ASP.NET Core, EF Core 10, PostgreSQL | CPM em `Directory.Packages.props`. Controllers MVC + FluentValidation auto + Mapperly + Serilog. xUnit + Testcontainers (precisa Docker pra rodar testes). |
| `Automais.Fhir` | .NET 10, EF Core + Npgsql, Firely SDK (`Hl7.Fhir.R4`), PostgreSQL (schema `fhir`, JSONB) | Solução própria (`Automais.Fhir.slnx`). Serviço FHIR autônomo ([ADR-0010](./docs/adr/0010-servico-fhir-autonomo.md)), em prod na porta 5081. |
| `SMSMarica.front` | React + Vite + TypeScript, Tailwind | **Implementado** (~20 features). Tema vermelho/branco (logo Maricá horizontal). npm (`package-lock.json`). |
| `SMSMarica.EquipamentoSim` | Python 3.11+, `pynetdicom`/`pydicom`, Typer CLI | Simulador DICOM para o ciclo Solicitação→Worklist→Execução. |
| `Salux` | Python 3.13, `paramiko`, `sqlplus`; alvo Oracle 12c | Engenharia reversa do Salux HIS. **Regras próprias em `Salux/CLAUDE.md`.** |
| `SMSMarica.cidadao.app` | Flutter (iOS + Android) | Riverpod + go_router + dio. **Ainda não está em produção** — login é mock; quebras de contrato com `/pacientes/{id}` são aceitáveis nesta fase. |
| `SMSMarica.agente.app` | Flutter Android only (planejado) | Foreground service + geofencing. |

## Comandos comuns

```bash
# Backend
cd SMSMarica.server
dotnet build                                       # 0 erros, 0 warnings esperado
dotnet test                                        # requer Docker para Testcontainers
dotnet run --project src/SMSMarica.Api             # http://localhost:5080
                                                   # /docs (Scalar UI)
                                                   # /openapi/v1.json (spec)
                                                   # /health

# Nova migration
dotnet ef migrations add <Nome> \
  --project src/SMSMarica.Data \
  --startup-project src/SMSMarica.Api

# Serviço FHIR autônomo (solução separada)
cd Automais.Fhir
dotnet build                                       # 0 erros, 0 warnings esperado
dotnet run --project src/Automais.Fhir.Api         # porta 5081 (/fhir/Patient...)

# Front (painel web)
cd SMSMarica.front
npm install
npm run dev                                        # Vite (http://localhost:5173)
npm run build                                      # tsc -b && vite build
npm run lint                                        # eslint .
```

## Adicionar uma nova entidade (CRUD completo)

Ver [`docs/architecture.md §3.6`](./docs/architecture.md). Resumo:

1. POCO em `Data/Entities/<X>.cs`
2. Configuração EF em `Data/Configurations/<X>Configuration.cs`
3. `DbSet<X>` em `SmsMaricaDbContext`
4. Migration (`dotnet ef migrations add ...`)
5. Pasta `Core/<X>/` com `IXService`/`XService`, `Dtos/`, `Validators/`, `Mapper.cs`
6. Registrar service em `Core/DependencyInjection.cs`
7. Controller em `Api/Controllers/<X>Controller.cs` com `[ApiController]` e CRUD
8. Testes em `tests/SMSMarica.Tests/<X>/`

Use o módulo Pacientes (já completo) como referência.
