---
name: operar-servidor
description: Referência operacional do servidor de produção smsmarica.online (droplet DigitalOcean) para o Agente IA que roda nele — units systemd, portas, nginx, diretórios de deploy, migrations, variáveis de ambiente e limites de autorização. Use SEMPRE que a tarefa tocar infraestrutura do host — "reiniciar/status de serviço", "ver logs", "journalctl", "nginx", "deploy", "migration", "porta", "env do serviço", "certificado" — antes de rodar qualquer comando que não seja pura leitura de código.
---

# Operar o servidor smsmarica.online

Você roda **dentro** do servidor de produção (droplet DigitalOcean, Ubuntu). Este documento é
o mapa e as regras. Na dúvida entre dois caminhos, escolha o reversível.

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
| `centralia-server` / `centralia-front` / `centralia-agent-runner` | 5083, 5084 | **OUTRO produto (CentralIA/Falarmais). Não é seu — não reinicie, não mexa.** |
| `wg-quick@wg-eveo` | — (disca o CCR Eveo) | Túnel de saída do SISREG pelo Brasil (`docs/sisreg-egress.md`). O SISREG **troca de IP** (09/10/2026 → F5 `159.60.146.75`) |
| `sisreg-egress-verificar.timer` | — | A cada 1 min põe no `wg-eveo` o IP que o DNS do SISREG devolver. Log: `journalctl -t sisreg-egress`. "Não foi possível autenticar no SISREG" em massa = olhar aqui antes de suspeitar da senha |
| `wg-quick@wg-mk` | UDP 51830 | Túnel antigo (MikroTik do escritório); **não carrega mais o SISREG** desde 14/09/2026 |

nginx serve `smsmarica.online` (painel), `app.smsmarica.online` (PWA cidadão),
`arquivos.smsmarica.online` e `api.smsmarica.online` (proxy → 5080). **Vhosts e certificados
existem só no servidor, nunca no git** — se mexer (só o admin), documente.

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

**PostgreSQL gerenciado na DigitalOcean**, porta 25060, database `defaultdb`, cluster
**compartilhado com outros produtos da Prefeitura**. Schemas `smsmarica` e `fhir` no mesmo
banco. Query pesada aqui afeta sistemas que não são seus — conexão e regras na skill
`acessar-banco-no-servidor`.

Servidores **separados**, não confunda: PACS (`pacs.marica.automais.cloud`, dcm4chee) e o hub
de telefonia (`192.241.153.121`).

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
4. **Variáveis de ambiente**: cada serviço tem `/etc/<serviço>/env`, **reescrito pelo GitHub
   Actions a cada deploy** a partir dos Secrets. Editar à mão é perda temporária — o certo é
   ajustar o Secret.
5. **Reiniciar serviço** em horário de atendimento: explicar impacto e pedir confirmação antes,
   mesmo sendo o admin.
