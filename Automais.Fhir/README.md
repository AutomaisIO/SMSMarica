# Automais.Fhir

Serviço FHIR R4 autônomo da SMS Maricá — o **hub canônico** de dados clínicos. Os demais sistemas (transporte, PEPs, indicadores) falam com ele **via API FHIR**. Decisão arquitetural em [ADR-0010](../docs/adr/0010-servico-fhir-autonomo.md).

## Arquitetura

Solução própria (`Automais.Fhir.slnx`), padrão 3-camadas espelhando o `SMSMarica.server`:

| Projeto | Papel |
|---|---|
| `Automais.Fhir.Data` | EF Core + Npgsql; dono do schema `fhir`. Persistência **JSONB** (recurso inteiro em coluna `jsonb` + colunas de search params). |
| `Automais.Fhir.Core` | Serviços de domínio, serialização/validação via **Firely SDK** (`Hl7.Fhir.R4`), regras de consistência. |
| `Automais.Fhir.Api` | REST FHIR (`/fhir/Patient`...), `OperationOutcome`, OpenAPI/Scalar. |
| `Automais.Fhir.Tests` | xUnit. |

Dependências: `Data ← nada` · `Core ← Data` · `Api ← Core + Data` · `Tests ← os três`.

## Persistência (document-store)

Cada tipo de recurso = uma tabela no schema `fhir`. O recurso FHIR vive **inteiro** na coluna `content jsonb`; ao lado há metadados comuns (`id`, `version_id`, `last_updated`, `meta_source`, `is_deleted`) e poucas colunas de busca extraídas e indexadas. GET devolve o documento; PUT/POST gravam o documento — sem montar/desmontar.

## Banco

Mesmo cluster Postgres do SMSMarica (DigitalOcean `defaultdb`), **schema `fhir` próprio**, DbContext próprio. Connection string isolada (`ConnectionStrings:FhirDb`) — mover o banco depois é só mudar a string.

## Comandos

```bash
cd FHIR
dotnet build                                        # 0 erros, 0 warnings esperado
dotnet test                                         # round-trip Firely (testes de integração exigem Docker)
dotnet run --project src/Automais.Fhir.Api         # /docs (Scalar), /openapi/v1.json, /health

# Nova migration
dotnet ef migrations add <Nome> \
  --project src/Automais.Fhir.Data \
  --startup-project src/Automais.Fhir.Api
```

A connection string vem de `ConnectionStrings:FhirDb` (env `ConnectionStrings__FhirDb` em produção). Para `dotnet ef` em design-time sem appsettings, use o env `FHIR_DB`.

## Endpoints (Patient — primeira fatia)

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/fhir/Patient` | Cria (gera id + Meta). |
| `GET` | `/fhir/Patient/{id}` | Lê pelo id lógico. |
| `PUT` | `/fhir/Patient/{id}` | Substitui (incrementa versão). |
| `DELETE` | `/fhir/Patient/{id}` | Exclusão lógica. |
| `GET` | `/fhir/Patient?identifier=system\|valor&name=...` | Busca (Bundle searchset). |

Conteúdo trafega como `application/fhir+json`. Erros viram `OperationOutcome`.

## Estado atual

Fatia vertical do **Patient** no ar (deployada em produção, porta 5081; migration `InicialFhirSchema` aplicada). Próximos: auth (JWT próprio) e mais recursos (Practitioner, Organization, depois clínicos dos ADRs 0008/0009). **Sem front próprio** — o hub é API-only; o front/consumidor é o `smsmarica.online` (ADR-0010 §6).
