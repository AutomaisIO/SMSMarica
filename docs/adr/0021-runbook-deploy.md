# Runbook de deploy — ADR-0021 (ecossistema Solicitação / split SolicitacaoExame)

> **Operação de produção destrutiva.** Executar na **janela pós-17h**, com **backup feito** e
> **OK explícito a cada passo**. Prod tem laudos reais ([[feedback_laudos_reais_nunca_apagar]]).
> Branch: `feat/adr21-cutover`. Migrations (nesta ordem): `EcossistemaSolicitacaoExpand` →
> `MigrarDadosSolicitacaoExame` → `ContractSolicitacaoExame`.

## Ordem e por quê (ler antes)

- O **código novo** (repointed) só funciona com o schema pós-`contract` (colunas dependentes
  renomeadas). O **contract** dropa `solicitacao_exame` → quebra o código antigo. Logo o código
  deve subir **logo após** o contract. Há uma **janela curta de downtime** entre o contract e o
  deploy do código (tempo do CI/CD) — aceitável pós-expediente.
- `dotnet ef database update <migration>` aplica as pendentes **em ordem** e registra o histórico
  (`__EFMigrationsHistory`). AutoMigrate do startup NÃO aplica ([[reference_automigrate_startup_nao_aplica]]).
- Rodar tudo a partir de um checkout da branch `feat/adr21-cutover` (tem todas as migrations).

## Pré-requisitos

- [ ] Estar na branch `feat/adr21-cutover`, `git pull`, `dotnet build` 0/0.
- [ ] Conn string de prod nos user-secrets (`ConnectionStrings:DefaultDb`, id `smsmarica-api-dev`).
- [ ] (Recomendado) Rodar a suíte com Docker: `cd SMSMarica.server && dotnet test`. Precisa Docker
      (Testcontainers). É a validação COMPORTAMENTAL que não deu pra fazer no ambiente de dev.

---

## Passo 0 — BACKUP do schema `smsmarica` (INEGOCIÁVEL)

No **server** (ou console DO) — tem `pg_dump`; o ambiente de dev não tem. Exemplo:

```bash
# no server smsmarica.online (ajuste host/porta/creds — vêm do env de prod)
pg_dump "$PROD_CONN" --schema=smsmarica --format=custom \
  --file ~/SMSMarica-secrets/backup-pre-adr0021-$(date +%Y%m%d-%H%M).dump
```

Guardar em `~/SMSMarica-secrets` ([[reference_dataprotection_backup]]). **Este é o rollback de
última instância.** Não prosseguir sem ele.

---

## Passo 1 — Aplicar expand + migrate (NÃO destrutivo; `solicitacao_exame` intacta)

A partir do checkout da branch, apontando `dotnet ef` para PROD via override de env:

```bash
cd SMSMarica.server
export ConnectionStrings__DefaultDb="<conn string de PROD dos user-secrets>"
dotnet ef database update MigrarDadosSolicitacaoExame \
  --project src/SMSMarica.Data --startup-project src/SMSMarica.Api
```

Isso aplica `EcossistemaSolicitacaoExpand` (cria `solicitacao` + `exame_imagem` vazias) e
`MigrarDadosSolicitacaoExame` (copia dados; id do exame preservado no satélite). `solicitacao_exame`
**continua existindo** — a prod segue funcionando com o código atual.

## Passo 2 — VERIFY GATE 1 (antes do contract)

```sql
-- counts batem (categoria=2 é Imagem)
SELECT
  (SELECT count(*) FROM smsmarica.solicitacao_exame)                     AS legado,
  (SELECT count(*) FROM smsmarica.exame_imagem)                          AS satelite,
  (SELECT count(*) FROM smsmarica.solicitacao WHERE categoria=2)         AS espinha_imagem;
-- id preservado: todo exame antigo existe no satélite com o MESMO id
SELECT count(*) FROM smsmarica.solicitacao_exame se
 WHERE NOT EXISTS (SELECT 1 FROM smsmarica.exame_imagem ei WHERE ei.id = se.id); -- espera 0
-- StudyUID preservado (laudos continuam achando)
SELECT count(*) FROM smsmarica.solicitacao_exame se
 WHERE se.study_instance_uid NOT IN (SELECT study_instance_uid FROM smsmarica.exame_imagem); -- espera 0
```

- [ ] `legado == satelite == espinha_imagem`
- [ ] id-órfão = 0, studyUID-órfão = 0
- [ ] spot-check: 2-3 exames com laudo assinado (por accession) presentes no satélite.

**Se algo falhar → PARAR.** Nada foi destruído (contract não rodou). Investigar / restaurar backup.

## Passo 3 — Aplicar CONTRACT (DESTRUTIVO: dropa `solicitacao_exame`)

```bash
dotnet ef database update ContractSolicitacaoExame \
  --project src/SMSMarica.Data --startup-project src/SMSMarica.Api
```

Renomeia colunas dependentes de execução (preserva dados), **traduz** as FKs de regulação
(comunicação/contato/login_link: id-exame → id-espinha via `exame_imagem.solicitacao_id`), recria
FKs e dropa `solicitacao_exame`. **A partir daqui o código ANTIGO (em prod) está quebrado** — seguir
imediatamente para o Passo 4.

## Passo 4 — Deploy do código (merge → main → push)

```bash
git checkout main && git pull
git merge --no-ff feat/adr21-cutover
git push origin main       # dispara o auto-deploy em prod
```

Acompanhar o deploy até o app subir (systemd `smsmarica-server`). Downtime = tempo do CI/CD.

## Passo 5 — VERIFY GATE 2 + smoke-test

```sql
-- solicitacao_exame não existe mais
SELECT to_regclass('smsmarica.solicitacao_exame');           -- espera NULL
-- nenhuma FK órfã de regulação
SELECT count(*) FROM smsmarica.comunicacao_paciente c
 WHERE c.solicitacao_id IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM smsmarica.solicitacao s WHERE s.id=c.solicitacao_id); -- espera 0
-- colunas antigas sumiram
SELECT table_name FROM information_schema.columns
 WHERE table_schema='smsmarica' AND column_name='solicitacao_exame_id'; -- espera vazio
```

Smoke no app (com você): listar exames (aparecem, com laudo), abrir um exame, listar consultas,
importar 1 marcação SISREG (exame + consulta), ver "Mapeamento pendente" e vincular um SIGTAP,
confirmar 1 agendamento por WhatsApp (sandbox), abrir 1 laudo.

## Rollback

- **Antes do contract (Passos 1-2):** `dotnet ef database update AddNumeroTicket` (ou a migration
  imediatamente anterior a `EcossistemaSolicitacaoExpand`) reverte via `Down()` (dropa as tabelas
  novas). Código antigo intacto.
- **Depois do contract (Passo 3+):** o `Down()` não restaura dados com fidelidade → **restaurar do
  backup** (`pg_restore` do dump do Passo 0) e reverter o código (`git revert` do merge / redeploy
  do commit anterior).

## Pós-deploy

- [ ] Dropar o banco scratch de teste: `DROP DATABASE smsmarica_scratch_adr21;` (cluster DO).
- [ ] Conferir `smsmarica.__EFMigrationsHistory` tem as 3 migrations.
- [ ] Rodar a skill `sincronizar-permissoes` p/ conferir os módulos `Consultas`/`MapeamentoSigtap`.
