# SMSMarica.Secretario.Api

Backend do painel executivo público do Secretário de Saúde (secretario.smsmarica.online).
Micro-serviço .NET 10 minimal API, **somente leitura**, que consulta o Oracle do Salux HIS
(HMCML) em cadência fixa e serve um snapshot agregado em `GET /api/painel` — shape exato de
[`../docs/contrato-painel.json`](../docs/contrato-painel.json), consultas em
[`../docs/consultas-oracle.md`](../docs/consultas-oracle.md).

## Rodar local

```bash
cd back
dotnet run          # http://localhost:5090  →  /health  e  /api/painel
```

Sem credencial o serviço sobe, loga aviso e serve o último `snapshot-painel.json`
persistido (ou 503 `{ "mensagem": "aguardando primeira carga do Salux" }`), **sem tentar
conectar no Oracle**.

### Credencial (nunca versionada)

Por variável de ambiente:

```bash
set Salux__Usuario=usuario_leitura
set Salux__Senha=***
```

Ou por `appsettings.Local.json` (gitignored) ao lado do `appsettings.json`:

```json
{ "Salux": { "Usuario": "usuario_leitura", "Senha": "***" } }
```

Precedência (padrão ASP.NET): `appsettings.json` < `appsettings.Local.json` < variáveis de
ambiente — o `Local.json` é registrado antes dos providers de env, então env vars vencem.

## Cadência

- **Tick rápido (60 s)** — Q1a/Q1b/Q1c (seção `agora`).
- **Tick lento (600 s)** — Q2..Q6 (atendimentos, internações, espera por cor).
- Sempre **sequencial** (uma consulta por vez — o Oracle é produção viva de hospital);
  primeiro ciclo roda já no startup; falha marca `oracle.ok=false` + `ultimoErro` e mantém
  o último snapshot bom.
- Após cada ciclo lento OK o snapshot é persistido em `Painel:SnapshotPath`
  (default `snapshot-painel.json`) e recarregado no startup — restart não serve tela vazia
  nem martela o Oracle.

## Decisões

- Projeto único (sem split 3 camadas): é um micro-serviço de leitura com um único endpoint.
- Execução read-only "de cinto e suspensório": `SET TRANSACTION READ ONLY` + `SqlReadOnlyGuard`
  (copiados do SMSMais.server) em toda consulta, mesmo sendo SQL constante do código.
- Períodos (`:ini`/`:fim`) são expressões `TRUNC(SYSDATE...)` constantes montadas no código —
  não existe input de usuário neste serviço.
- Cores normalizadas para o contrato (VERMELHO/AMARELO/VERDE/AZUL/SEM_CLASSIFICACAO);
  "SALUX" soma em SEM_CLASSIFICACAO. Sempre saem as 5 entradas, mesmo com qtd 0.
- Média diária do mês atual usa só dias completos ÷ (dias corridos − 1); no dia 1º a média
  é `null` (não há dia completo — nunca dividir por zero).
- Timestamps `DateTimeOffset` no fuso America/Sao_Paulo (regra fixa de Brasília do repo).
- CORS: GET liberado para qualquer origem — dados públicos agregados, sem PII.
