# SMSMarica.secretario.pwa — Painel do Secretário

Painel executivo **público** (sem autenticação na v1) para o Secretário de Saúde
de Maricá, em **secretario.smsmarica.online**. Mostra números **ao vivo** da rede
municipal de urgência — **Hospital Municipal Conde Modesto Leal** (Oracle do Salux
HIS), **UPA 24h Maricá** e **UPA 24h Santa Rita** (HIS em SQL Server, uma instância
cada) — com dois seletores empilhados: o **assunto** (`Emergência · Leitos e
internação`) e a **unidade** (`Geral · Conde · UPA · Sta. Rita`).

- **Emergência**: fila por cor de risco, internados agora, atendimentos e
  internações por período, tempos de espera por classificação e os CIDs mais
  registrados em cada cor.
- **Leitos e internação**: taxa de ocupação, ocupação por setor, perfil de quem
  está no leito (sexo e faixa etária) e tempo médio de permanência das altas,
  geral e por segmento.
- **Maternidade**: partos e via de parto, desfecho (natimortos, Apgar,
  malformação), idade gestacional, perfil da mãe (incluindo gravidez na
  adolescência) e medidas ao nascer.

Internação e maternidade **só existem no Conde**; nas UPAs essas seções não aparecem
(a tabela de internação do HIS delas parou em 25/01/2026), e na aba Geral vêm
etiquetadas com a unidade de origem.

**Só dados AGREGADOS — zero PII.** Nenhum nome, CPF, prontuário ou dado
individual sai do back; o contrato (`docs/contrato-painel.json`) só carrega
contagens, médias e percentis. É isso que permite o painel ser público.

## Arquitetura

```
 Oracle Salux (Conde)     SQL Server UPA-SRV      SQL Server STARITA-SRV
  schema INFOSAUDE, R/O    upa24h-marica-sqlserver  santarita-marica-sqlserver
        |                          |                        |
        | túnel WireGuard          | agente WSS reverso (ADR-0023)
        +--------------------------+------------------------+
                                   v
        Proxy SQL interno do smsmarica (127.0.0.1:5091 + token)
                                   |
                                   v
 SMSMarica.Secretario.Api (.NET 10, porta 5090, só loopback)
   - tick rápido 60 s ("agora") + tick lento 10 min (consolidados), sequencial
   - as três bases são ISOLADAS: uma fora do ar não impede as outras de atualizar
   - snapshot em /var/lib/smsmarica-secretario/snapshot-painel.json
   - GET /api/painel serve o snapshot (nunca consulta banco no request)
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
│   ├── consultas-oracle.md     # consultas SQL do Conde (Oracle/Salux) + fatos do schema
│   ├── consultas-sqlserver-upa.md  # consultas SQL das UPAs (SQL Server) + armadilhas do modelo
│   └── runbook-producao.md     # passo a passo de produção (DNS, nginx, cert, env, deploy)
└── README.md
```

## Dev local

**Back** — não há credencial de banco: só o token do proxy SQL, por variável de
ambiente (nunca em arquivo versionado). O proxy é loopback no droplet, então para
rodar local abra um túnel até ele:

```bash
ssh -N -L 15091:127.0.0.1:5091 root@smsmarica.online   # em outro terminal
```

```powershell
$env:ProxySql__Token   = "<token de /etc/smsmarica-secretario/env>"
$env:ProxySql__BaseUrl = "http://127.0.0.1:15091"
dotnet run --project back   # http://localhost:5090
```

Sem alcance ao proxy, o back sobe e serve o último snapshot (com a fonte marcada
em erro), ou responde 503 se nunca houve um.

**Front** — usa `src/mock/painel.json` como mock em dev, e `?mock=1` força o mock
mesmo com back no ar. Os valores são reais, capturados em 25/07/2026:

```bash
cd front
npm install
npm run dev        # Vite; /api mockado com o contrato
npm run build
```

## Cadência de atualização

Definida em `docs/consultas-oracle.md`:

- **Tick rápido (60 s)** — bloco `agora` de cada unidade (consultas baratas).
- **Tick lento (10 min)** — períodos, séries e espera por cor (a Q6 do Conde faz
  join no `EDOC_MOVIMENTO` de 5M de linhas e leva dezenas de segundos).
- Consultas **sequenciais, nunca em paralelo** — são produções vivas de unidade de
  saúde. Execução sempre read-only (guard de SQL só-SELECT aqui e de novo no proxy).
- **Falha de uma base não derruba as outras**: cada uma tem seu estado de erro, e o
  painel segue servindo o último número bom de cada lado.

O front consome o snapshot; `status` traz a saúde consolidada e `fontes[]` a de cada
base (`ok`, `ultimoErro`, `ultimaAtualizacaoOk`), para o painel dizer QUEM está
defasado sem sair do ar.

## Decisões

- **Serviço standalone, fora do SMSMarica.server** — isolamento de carga e de
  deploy: um painel público não pode competir por pool/threads com o sistema
  interno, nem um deploy do painel derrubar o server (e vice-versa). Mesmo
  precedente do Automais.Fhir ([ADR-0010](../docs/adr/0010-servico-fhir-autonomo.md)).
  *Nota: formalizar em ADR futuro.*
- **Snapshot em disco, request nunca toca o Oracle** — o painel público tolera
  dado com até 10 min de idade; o Oracle do hospital não tolera carga de
  visitantes. `Painel__SnapshotPath` sobrevive a restart do serviço.
- **Hospital fixo `cd_hospital = 1` (HMCML)** — dentro do Salux existem também
  `cd_hospital` 2 ("UPA Inoã") e 3 ("PA Santa Rita"), **sem movimento** (ver
  `docs/consultas-oracle.md`). ⚠️ **Não confundir**: o "PA Santa Rita" morto no
  Salux *não* é a UPA 24h Santa Rita do painel. As UPAs do painel vêm de outro
  sistema, o HIS em SQL Server — UPA Maricá (`unid_codigo 0006`, servidor UPA-SRV)
  e Santa Rita (`0007`, STARITA-SRV). **As duas bases se chamam `UPA24H`**, então a
  unidade só se distingue pelo slug do proxy, nunca pelo nome do banco.
- **Sem credencial de banco no painel** — desde 25/07/2026 tudo passa pelo proxy
  SQL interno do smsmarica; o painel manda slug da base + SQL e não sabe dialeto
  nem transporte. Em `/etc/smsmarica-secretario/env` só existe `ProxySql__Token`.
- **O que não existe vem nulo, nunca zero** — as UPAs não internam; mandar 0 diria
  que elas não internaram ninguém, quando elas não internam. O front some com a seção.
- **Ocupação sai dos PACIENTES, não do flag do leito** — `ID_SIT_LEITO='O'` e a
  contagem de internados discordam (162 × 155 em 25/07). Usar o flag faria a aba de
  leitos contradizer o "internados agora" da aba de emergência. Leitos bloqueados
  saem do denominador e são reportados à parte.
- **Cadastro ruim é dito, não maquiado** — Santa Rita tem 2 leitos cadastrados para
  262 atendimentos/dia e a tabela não é atualizada. Abaixo de 5 leitos o painel manda
  `indisponivel` com o motivo em vez de publicar "0 de 2". É limiar, não lista fixa:
  volta sozinho quando a unidade cadastrar.
- **Meta de triagem é da unidade, não da rede** — cada unidade usa alvos próprios
  para a mesma cor (Amarelo: 30 min no Conde, 60 na UPA Maricá, 30 em Santa Rita).
  Na aba Geral a meta e o "% na meta" simplesmente não aparecem; o consolidado
  mostra volume e tempo, e tempo-contra-meta se vê abrindo a unidade.
- **Quando duas fontes discordam, vale a mais objetiva** — prematuridade sai da
  idade gestacional, não do flag `IN_PREMATURO` (marcado à mão, discordava do
  próprio registro em 11 de 23 casos). Idem ocupação, que sai dos pacientes e não
  do flag do leito.
- **Sem auth na v1** — dados agregados públicos por decisão de produto;
  reavaliar se entrar qualquer recorte sensível.

## Deploy e produção

- Workflow: [`.github/workflows/deploy-secretario.yml`](../.github/workflows/deploy-secretario.yml)
  — 2 jobs independentes (back e front), trigger em push a `main` com paths
  deste projeto + `workflow_dispatch`.
- Passo a passo de produção (DNS, nginx, certbot, env com a credencial Oracle,
  validação, rollback): [`docs/runbook-producao.md`](./docs/runbook-producao.md).
  **Nenhum passo roda automaticamente — produção só com OK explícito do operador.**
- O token do proxy SQL vive **só** em `/etc/smsmarica-secretario/env` (chmod 600),
  colocado manualmente 1x; o deploy nunca o sobrescreve e nada de segredo entra
  no git.
- **O snapshot persistido de uma versão anterior do contrato é ignorado** (log de
  aviso) em vez de servir tela quebrada — o painel volta em até 1 min.

## Documentos

- [`docs/contrato-painel.json`](./docs/contrato-painel.json) — contrato do `GET /api/painel` (a verdade do shape).
- [`docs/consultas-oracle.md`](./docs/consultas-oracle.md) — SQL do Conde (Oracle/Salux): validado, fatos do schema, cadência.
- [`docs/consultas-sqlserver-upa.md`](./docs/consultas-sqlserver-upa.md) — SQL das UPAs (SQL Server) e as três armadilhas do modelo delas.
- [`docs/runbook-producao.md`](./docs/runbook-producao.md) — runbook de produção.
