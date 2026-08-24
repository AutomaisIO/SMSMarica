# ADR-0046 — O produto chama-se SMSMais: rename de código com carve-outs de runtime

**Status:** aceito · **Data:** 2026-08-24
**Supersede:** a regra derivada nº 1 do [ADR-0043](./0043-instancia-por-municipio.md) ("SMSMarica
não é renomeado"). As demais regras do 0043 — sem `TenantId`, nada institucional em código ou
migration — **continuam valendo**.
**Relacionado:** [ADR-0004](./0004-arquitetura-tres-projetos.md) (migrations imutáveis),
[ADR-0021](./0021-runbook-deploy.md) (AutoMigrate falha calado).

## Contexto

O ADR-0043 decidiu, em 17/08, que `SMSMarica` viraria nome de código interno do produto — renomear
custava caro e o cliente não vê namespace. Uma semana depois a premissa mudou: a **segunda
prefeitura foi contratada**, a VM e o banco dela já existem, foi criada a organização
`SMSMais-Sistema-de-Saude` no GitHub, e o nome comercial do produto está travado como **SMSMais**
(seguindo a família Automais / Falarmais / AeroMais). Um produto vendido a N municípios carregando
o nome de um deles em toda pasta, namespace e unit deixou de ser aceitável para o dono do produto.

A decisão de reverter a regra foi tomada com o custo medido de novo (20-24/08, repo em `main`):
42.474 ocorrências em 1.471 arquivos, das quais **89% dentro das migrations**; 2.069 arquivos com
o token no caminho; 4 grafias distintas (`SMSMarica`, `smsmarica`, `SmsMarica`, `SMSMARICA`) com
papéis completamente diferentes — e é essa distinção que torna o rename seguro ou catastrófico.

## Decisão

**O nome de código do produto passa a ser `SMSMais`** — pastas, projetos, namespaces, classes,
nomes npm e pacotes. O rename foi executado **em fatias, uma por projeto, cada uma commitada com a
solução compilando** (a independência entre namespace, nome de pasta e nome de assembly em .NET é
o que permite o repo ficar "misto" e verde entre fatias).

**O que NÃO foi renomeado (carve-outs).** Estes valores carregam o nome antigo e **devem
permanecer assim** até o bloco que os trata — ou para sempre, quando o dono é externo:

| # | Valor | Onde | Por quê |
|---|---|---|---|
| 1 | Schema Postgres `smsmarica` (+ `smsmarica.__migrations`, extensão `vector` dentro dele) | `SmsMaisDbContext.SchemaPadrao`, 269 migrations | Bloco 3 (um `ALTER SCHEMA ... RENAME` futuro, nas duas instâncias). Migrations continuam imutáveis: o EF compara só o `migration_id`, que não carrega namespace |
| 2 | Domínios `*.smsmarica.online` e `smsmarica.app.br` | fronts, CORS, workflows, magic-links já enviados | São domínios **de Maricá**, não do produto. A 2ª instância tem os dela; viram configuração |
| 3 | `https://smsmarica.saude.marica/source/` (`meta.source` FHIR) | `Automais.Fhir.Core/Fhir/MetaSources.cs` + mappers | Gravado em todo recurso FHIR já persistido. Vira env var por instância com este default |
| 4 | Headers `X-SMSMarica-*` | Api ↔ aiengine (o FastAPI **deriva o header do nome do parâmetro** `x_smsmarica_internal_key`) | Contrato entre dois deploys independentes — Bloco 2, em lockstep |
| 5 | Chaves de browser `smsmarica*` (localStorage, IndexedDB `smsmarica-pdf`, BroadcastChannels) | painel + PWAs | Renomear desloga operadores e pacientes e órfã caches. Bloco 2, com shim de migração |
| 6 | Purpose `"SMSMarica.Ia.Segredos"` | `Api/Auth/ProtetorSegredos.cs` | Entra na **derivação da chave** do Data Protection — um caractere trocado torna indecifráveis todas as credenciais de integração. Literal congelado, para sempre |
| 7 | JWT `Issuer="smsmarica"` / `Audience="smsmarica-front"` e `UserSecretsId=smsmarica-api-dev` | `appsettings.json`, csproj | Trocar invalida todos os tokens vivos (logout geral) / perde os user-secrets locais. Bloco 2, com janela |
| 8 | Units systemd `smsmarica-*`, caminhos `/opt|/etc|/var|/srv/*smsmarica*`, usuário POSIX, secrets `*_SMSMARICA`, tarballs, `DEPLOY_DIR` | workflows + servidor de Maricá | Lado servidor da instância Maricá — Bloco 2. **A instância nova já nasce com nomes `smsmais-*`** |
| 9 | `GESTOR SMS MARICA`, `"UPA MARICA"`, `cd_cidade=330270` | integrações SER/Klinikos/Salux | Valores cadastrados em **sistemas de terceiros** (Estado, PEPs). Nunca |
| 10 | `sip_smsmarica.conf` | Asterisk da FalarMais | `#include` no servidor de um terceiro, sem git/CI. Nunca, sem janela combinada |
| 11 | `docs/adr/**` e docs-inventário de época (`marco-1-e-linha-de-corte`, `reconciliacao-working-tree`, `ser-continuacao`) | — | Registro histórico não se reescreve |
| 12 | Repo `AutomaisIO/SMSMarica` (deploy key do Agente IA, `/srv/smsmarica-repo`) | `setup-aiengine-host.sh` | Muda junto com a **transferência do repo** para a org `SMSMais-Sistema-de-Saude` (passo próprio, com checklist: secrets, deploy key, clone do agente, token cross-org do espelho AGPL) |

**Exceção consciente à regra 4 (migrations imutáveis):** os 134 `.Designer.cs` e o snapshot foram
editados — apenas namespace, `[DbContext(typeof(...))]` e as strings tipo-qualificadas do modelo.
O `migration_id` (o que a `__migrations` compara) e o SQL de Up/Down não mudaram: os **11.996
literais `"smsmarica"` de schema foram conferidos byte a byte antes e depois**. O gate
`dotnet ef migrations has-pending-model-changes` respondeu **No changes** após cada fatia do
backend — é a prova de que modelo e snapshot não dessincronizaram (o modo de falha do incidente
TFD/mensageria).

**`SMSMarica.secretario.pwa` não foi renomeado por decisão de produto:** o Painel do Secretário é
específico de Maricá (números ao vivo do Oracle do HMCML), não faz parte do produto replicável.

## Consequências

- Fatias executadas em 24/08 (commits `55638b4..759b4f7`): testes, arquivos.pwa, cidadao.pwa,
  front, aiengine, EquipamentoSim, **Data (+`SmsMaisDbContext`), Core, Api, pasta server
  (`SMSMais.slnx`)**, apps Flutter (bundle ids novos `io.automais.smsmais.*` — apps nunca
  publicados, id livre até a primeira publicação). Auditoria final: zero tokens qualificados
  vivos fora do histórico.
- O deploy do server ganhou um **passo de reconciliação da unit**: a unit existente em produção
  aponta para `SMSMarica.Api.dll`; no primeiro deploy pós-rename o script corrige o `ExecStart`
  in-place e faz `daemon-reload` — sem isso o serviço não subiria com o Actions "success".
- Dois achados de padrão que valem para qualquer rename futuro: (a) **referências de namespace
  RELATIVAS** (`Data.Entities.X`, `Core.X`) só compilavam pela raiz comum e quebram quando as
  raízes divergem — foram qualificadas por extenso; (b) **`InternalsVisibleTo` acopla pelo nome
  do assembly** e não aparece em busca por namespace.
- Blocos pendentes: **Bloco 2** (infra de Maricá: caminhos, units, JWT, chaves de browser, com 1
  janela pequena) e **Bloco 3** (schema, `ALTER SCHEMA` nas duas bases, 1 janela). Nenhum dos dois
  bloqueia a segunda prefeitura.
