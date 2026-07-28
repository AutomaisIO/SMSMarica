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
| `wg-quick@wg-mk` | UDP 51830 | Túnel para MikroTik no Brasil — rota só para o SISREG (`189.28.130.13/32`) |

nginx serve `smsmarica.online` (painel), `app.smsmarica.online` (PWA cidadão),
`arquivos.smsmarica.online` e `api.smsmarica.online` (proxy → 5080). **Vhosts e certificados
existem só no servidor, nunca no git** — se mexer (só o admin), documente.

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
