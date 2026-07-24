# SMSMarica.secretario.pwa — Painel do Secretário

Painel executivo **público** (sem autenticação na v1) para o Secretário de Saúde
de Maricá, em **secretario.smsmarica.online**. Mostra números **ao vivo** do
Hospital Municipal Conde Modesto Leal (HMCML) vindos do Oracle do Salux HIS:
fila da emergência por cor de risco, internados agora, atendimentos e
internações por período, e tempos de espera por classificação.

**Só dados AGREGADOS — zero PII.** Nenhum nome, CPF, prontuário ou dado
individual sai do back; o contrato (`docs/contrato-painel.json`) só carrega
contagens, médias e percentis. É isso que permite o painel ser público.

## Arquitetura

```
 Oracle Salux (10.50.0.18:1521/ORASX01, schema INFOSAUDE, READ-ONLY)
        |
        |  túnel WireGuard existente (droplet DO → rede do hospital)
        v
 SMSMarica.Secretario.Api (.NET 10, porta 5090, só loopback)
   - tick rápido 60 s (Q1 "agora") + tick lento 10 min (Q2–Q6), sequencial
   - snapshot em /var/lib/smsmarica-secretario/snapshot-painel.json
   - GET /api/painel serve o snapshot (nunca consulta Oracle no request)
        |
        |  nginx: proxy /api/ → 127.0.0.1:5090  +  estáticos
        v
 PWA (React + Vite + TS) em /var/www/smsmarica-secretario
   - fetch same-origin /api/painel — sem VITE_API_BASE_URL
   - https://secretario.smsmarica.online
```

## Estrutura de pastas

```
SMSMarica.secretario.pwa/
├── back/                 # API .NET (SMSMarica.Secretario.Api — projeto único, layout flat)
├── front/                # PWA React + Vite + TS
├── docs/
│   ├── contrato-painel.json    # contrato EXATO do GET /api/painel (com dados reais de exemplo)
│   ├── consultas-oracle.md     # consultas SQL validadas + fatos do schema + cadência
│   └── runbook-producao.md     # passo a passo de produção (DNS, nginx, cert, env, deploy)
└── README.md
```

## Dev local

**Back** — a credencial Oracle vem por variáveis de ambiente (nunca em arquivo
versionado):

```powershell
$env:Salux__Usuario = "<usuario_leitura>"
$env:Salux__Senha   = "<senha>"
$env:Salux__Host    = "10.50.0.18"     # requer rota até o Oracle (VPN/túnel)
$env:Salux__Porta   = "1521"
$env:Salux__Servico = "ORASX01"
dotnet run --project back   # http://localhost:5090
```

Sem alcance ao Oracle, o back sobe e serve o último snapshot (ou
`oracle.ok=false` se nunca houve um).

**Front** — usa `docs/contrato-painel.json` como mock em dev (os valores são
reais, capturados em 24/07/2026):

```bash
cd front
npm install
npm run dev        # Vite; /api mockado com o contrato
npm run build
```

## Cadência de atualização

Definida em `docs/consultas-oracle.md`:

- **Tick rápido (60 s)** — bloco `agora` (3 consultas baratas, <1 s cada).
- **Tick lento (10 min)** — períodos, séries e espera por cor (a Q6 faz join no
  `EDOC_MOVIMENTO` de 5M de linhas e leva dezenas de segundos).
- Consultas **sequenciais, nunca em paralelo** — o Oracle é produção viva de
  hospital. Execução sempre read-only (`SET TRANSACTION READ ONLY` + guard de
  SQL só-SELECT, mesmo padrão do `SaluxOracleFonte` do SMSMarica.server).

O front consome o snapshot; o campo `oracle` do contrato informa saúde da fonte
(`ok`, `ultimoErro`, `ultimaAtualizacaoOk`) para o painel sinalizar dado
possivelmente defasado sem sair do ar.

## Decisões

- **Serviço standalone, fora do SMSMarica.server** — isolamento de carga e de
  deploy: um painel público não pode competir por pool/threads com o sistema
  interno, nem um deploy do painel derrubar o server (e vice-versa). Mesmo
  precedente do Automais.Fhir ([ADR-0010](../docs/adr/0010-servico-fhir-autonomo.md)).
  *Nota: formalizar em ADR futuro.*
- **Snapshot em disco, request nunca toca o Oracle** — o painel público tolera
  dado com até 10 min de idade; o Oracle do hospital não tolera carga de
  visitantes. `Painel__SnapshotPath` sobrevive a restart do serviço.
- **Hospital fixo `cd_hospital = 1` (HMCML)** — UPA Inoã e PA Santa Rita
  existem na base mas estão sem movimento (ver `docs/consultas-oracle.md`).
- **Sem auth na v1** — dados agregados públicos por decisão de produto;
  reavaliar se entrar qualquer recorte sensível.

## Deploy e produção

- Workflow: [`.github/workflows/deploy-secretario.yml`](../.github/workflows/deploy-secretario.yml)
  — 2 jobs independentes (back e front), trigger em push a `main` com paths
  deste projeto + `workflow_dispatch`.
- Passo a passo de produção (DNS, nginx, certbot, env com a credencial Oracle,
  validação, rollback): [`docs/runbook-producao.md`](./docs/runbook-producao.md).
  **Nenhum passo roda automaticamente — produção só com OK explícito do operador.**
- A credencial Oracle vive **só** em `/etc/smsmarica-secretario/env` (chmod 600),
  colocada manualmente 1x; o deploy nunca a sobrescreve e nada de segredo entra
  no git.

## Documentos

- [`docs/contrato-painel.json`](./docs/contrato-painel.json) — contrato do `GET /api/painel` (a verdade do shape).
- [`docs/consultas-oracle.md`](./docs/consultas-oracle.md) — SQL validado, fatos do schema, cadência.
- [`docs/runbook-producao.md`](./docs/runbook-producao.md) — runbook de produção.
