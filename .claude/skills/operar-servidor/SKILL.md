---
name: operar-servidor
description: Referência operacional do servidor de produção do SMSMais de Maricá (VM SMSMAIS-01 no datacenter EVEO desde 14/10/2026; antes, droplet DigitalOcean) para o Agente IA que roda nele — units systemd, portas, nginx, diretórios de deploy, migrations, variáveis de ambiente e limites de autorização. Use SEMPRE que a tarefa tocar infraestrutura do host — "reiniciar/status de serviço", "ver logs", "journalctl", "nginx", "deploy", "migration", "porta", "env do serviço", "certificado" — antes de rodar qualquer comando que não seja pura leitura de código.
---

# Operar o servidor de produção (SMSMAIS-01)

Você roda **dentro** do servidor de produção: a VM **SMSMAIS-01** (Ubuntu 24.04, 16 vCPU, 16 GB) no
cluster Proxmox do datacenter **EVEO** (SP), desde **14/10/2026**. Até essa data a produção rodava num
droplet da DigitalOcean (NY), hoje **desligado** — documento antigo, memória ou ticket que falar em
"droplet" está descrevendo o passado. Este documento é o mapa e as regras. Na dúvida entre dois
caminhos, escolha o reversível.

## Como o tráfego chega e sai (EVEO, 14/10/2026)

- **Entrada:** o DNS de `smsmarica.online`, `www.`, `api.`, `app.`, `arquivos.` e `secretario.` aponta
  para o IP público da EVEO. Quem atende é o **proxy01** (proxy reverso da EVEO, outra VM): ele
  termina o TLS (certificado Let's Encrypt **dele**, renovado lá) e repassa para o nginx desta VM. O
  nginx daqui confia no `X-Forwarded-For` só do proxy01 (`/etc/nginx/conf.d/real-ip-proxy01.conf`) —
  sem isso todo cliente teria o IP do proxy e os rate limits por IP da API virariam uma cota só.
- **Resolução interna (split DNS):** de dentro da EVEO o IP público da própria EVEO **não responde**
  (o roteador não faz hairpin). Por isso o `/etc/hosts` desta VM tem um bloco
  `# >>> EVEO: dominios publicados no proxy01 ...` que aponta os domínios do SMSMais e do
  Automais.Zap (`api.smsmais.automais.com`) para o **IP interno do proxy01**. Sem esse bloco a API
  não alcança o Zap (todo envio de WhatsApp falha com "Connection refused") — foi o que aconteceu de
  14/10 21h a 15/10 12h UTC. Domínio novo que a API precise chamar e que esteja publicado no
  proxy01 entra nesse bloco.
- **Saída:** direto pela internet da EVEO (IP brasileiro). O **SISREG** sai por aí, sem túnel — o
  `wg-eveo`, o `wg-mk` e o `sisreg-egress-verificar.timer` ficaram no droplet desligado e **não
  existem aqui** (`docs/sisreg-egress.md` é histórico).
- **Banco:** PostgreSQL 18 na VM **PSQL-01**, no mesmo datacenter (rede interna), database
  `smsmais` **exclusivo** do SMSMais (schemas `smsmarica` e `fhir`). Conexão e regras na skill
  `acessar-banco-no-servidor`.

**Trava da migração:** cada unit do SMSMais tem um drop-in `.../<unit>.service.d/00-trava-virada.conf`
com `ConditionPathExists=/etc/smsmais/liberado`. **Se esse arquivo sumir, nenhum serviço sobe** (o
`systemctl start` "funciona" e nada roda). Ele existe desde a virada; não apague.

## Autorização (regra dura)

**Alteração de código, commit, deploy, migration, mudança de dado e escrita no host são
EXCLUSIVOS do administrador Bernardo Almeida** (`usuario_id
019dc264-7de1-78cc-b6ff-0be0c0e8b714`) — confira o bloco "Operador desta sessão"/"Autorização"
do seu system prompt. Com qualquer outro operador, limite-se a diagnóstico e **ofereça abrir um
ticket** (skill `criar-ticket`) com o relato do que precisa mudar. Mesmo com o administrador,
cada ação de impacto exige confirmação explícita nesta conversa.

## Mapa do host

| Unit | Porta | Caminho |
|---|---|---|
| `smsmarica-server` | 5080 (0.0.0.0) | `/opt/smsmarica/server` — usuário `smsmarica` |
| `automais-fhir` | 5081 (0.0.0.0) | `/opt/automais-fhir/api` — usuário `automais` |
| `automais-assinador` | 5082 (**loopback**) | `/opt/automais-assinador/api` |
| `smsmarica-aiengine` | 5085 (**loopback**) | **você** — não se reinicie no meio de um turno |
| `smsmarica-secretario` | 5090 (**loopback**) | `/opt/smsmarica-secretario/api` — painel do secretário |
| `centralia-*` | — | **Não roda aqui.** O CentralIA (Falarmais) ficou no droplet desligado; os arquivos foram copiados mas as units estão desabilitadas — destino ainda em decisão. Não ligue. |

nginx serve `smsmarica.online` (painel), `app.smsmarica.online` (PWA cidadão),
`arquivos.smsmarica.online`, `secretario.smsmarica.online` e `api.smsmarica.online` (proxy → 5080),
atrás do proxy01. **Vhosts e certificados existem só nos servidores, nunca no git** — o certificado
que o público vê é o do proxy01; os desta VM são cópia do droplet e **não são renovados aqui**
(o proxy01 fala com esta VM sem conferir o certificado). Se mexer (só o admin), documente.

**API fora = 503 com CORS, não 502** (desde 30/09/2026). O vhost `api.smsmarica.online` tem
`error_page 502 504 = @api_indisponivel`: com a 5080 fora (deploy, reinício), o nginx responde
503 JSON `{"servidorIndisponivel":true,…}` com `Access-Control-Allow-Origin: *`, e 204 no
preflight OPTIONS. Sem isso o 502 saía sem CORS e o navegador o via como "falha de rede". O
painel lê a marca e mostra "O sistema está sendo atualizado" (`SMSMais.front/src/shared/api/conexao.ts`).
Consequências: no access log, a API fora aparece como **503** (não mais 502). O 503 do próprio
backend passa intacto (`proxy_intercept_errors` off). Backup antes da mudança em
`/root/backup-nginx/`. Ao recriar o vhost (instância nova, certbot), **repita o bloco**.

**Deploy derruba a API** (stop + start). Até 30/09/2026 eram 40–48s por deploy: o stop esperava
30s (teto padrão do .NET) pelos WebSockets dos agentes SQL, que não ouviam a parada. Agora o
endpoint dos agentes fecha na parada e o teto é 10s (`HostOptions.ShutdownTimeout` em
`Program.cs`); a partida leva ~10s. Para medir: `journalctl -u smsmarica-server`
(Stopping → Stopped → Started) e as rajadas de 503 no `/var/log/nginx/api.smsmarica.access.log`.

**PostgreSQL 18 na VM PSQL-01** (mesmo datacenter), database `smsmais`, só do SMSMais. O PSQL-01
hospeda **outros bancos** (de outros produtos da Automais) — query pesada ainda afeta vizinhos.
Conexão e regras na skill `acessar-banco-no-servidor`. O banco gerenciado antigo da DigitalOcean
ficou congelado no instante da virada (14/10/2026) e **não é mais a verdade** — não leia nem grave
nele.

Servidores **separados**, não confunda: PACS (`pacs.marica.automais.cloud`, dcm4chee — libera esta
VM pelo IP de saída da EVEO no `ufw` e no `fail2ban` dele), o relay **Automais.Zap** (WhatsApp; em
migração para a VM ZAP-01 da EVEO) e o hub de telefonia (`192.241.153.121`).

## O que você PODE fazer direto (diagnóstico — qualquer operador)

`systemctl status <unit>`, `journalctl -u <unit>`, logs do nginx, `ss -tlnp`, consulta de
LEITURA ao banco, `curl` contra `127.0.0.1:5080/health` (e 5081/5085). Nada disso muda estado.

## Regras de escrita (só o administrador, com OK por ação)

1. **Nunca edite os diretórios de deploy.** `/opt/smsmarica/server`, `/opt/automais-fhir/api`,
   `/opt/automais-assinador/api`, `/var/www/smsmarica-*` são **destruídos e recriados a cada
   publicação** (`find $APP_DIR -mindepth 1 -delete`). Editar ali é perder o trabalho no
   próximo deploy e divergir da produção em silêncio.
2. **Código muda pelo fluxo git**: trabalhe no clone (`sincronizar-antes-de-editar`), commite
   em pt-BR (`tipo(escopo): descrição`) e **deixe o GitHub Actions publicar** (cada workflow
   tem filtro `paths:`). **Nunca `git push --force`** — o repositório tem mais de um produtor.
3. **Migrations**: gere no repositório, commite e avise o operador — a aplicação em produção
   segue o runbook `docs/adr/0021-runbook-deploy.md`, não é automática. O AutoMigrate do
   startup NÃO aplica migrations: conferir `smsmarica.__migrations` depois.
4. **Variáveis de ambiente**: cada serviço tem `/etc/<serviço>/env`. O deploy do server só
   substitui as linhas que ele controla (connection string se o Secret existir, chave do Agente
   IA) e preserva o resto. Deploy desta VM = GitHub Environment **`automais`** (SSH pela WAN da
   EVEO, porta em `vars.DEPLOY_PORT`), que **não** tem `DB_CONNECTION` — a connection string desta
   VM (banco pela rede interna) é mantida como está no env.
5. **Reiniciar serviço** em horário de atendimento: explicar impacto e pedir confirmação antes,
   mesmo sendo o admin.
