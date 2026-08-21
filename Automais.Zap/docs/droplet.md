# Droplet do Automais.Zap

Servidor dedicado do relay. **Não é o droplet do SMSMarica** — nada aqui compartilha banco,
nginx ou systemd com a produção do município.

> Credenciais (senha de root, senha do Postgres, verify token, senha do painel) **não vivem neste
> arquivo nem em lugar nenhum do repositório**. Estão na memória local da sessão e no
> `/etc/automais-zap/env` do próprio host (`chmod 600`, dono `zap`).

## Identificação

| | |
|---|---|
| IP | `147.182.218.159` |
| Hostname | `Automais-Whatsapp` |
| Provedor | DigitalOcean |
| SO | Ubuntu 24.04.4 LTS |
| Recursos | 1 vCPU · 961 MB RAM · 24 GB · **swap de 2 GB adicionado** (1 GB sem swap não tem folga para pico com Postgres + .NET) |
| Acesso | `ssh root@147.182.218.159` (senha) |

## Domínios

| Host | Para quê | Observação |
|---|---|---|
| `api.smsmais.automais.com` | **Webhook da Meta** e `/health`, `/docs` | `/admin` devolve **404** aqui de propósito: a tela de gestão não precisa estar exposta no host publicado à Meta |
| `smsmais.automais.com` | Tela de gestão (`/admin`) | `/` redireciona para `/admin` |

Ambos apontam para o mesmo processo em `127.0.0.1:5086`. Certificado Let's Encrypt **único**
cobrindo os dois (`certbot certificates` → nome `api.smsmais.automais.com`), renovação por
`certbot.timer` (ativo).

Host desconhecido cai no `000-default`, que responde `444` (fecha a conexão sem resposta).

## Mapa do host

| | Caminho |
|---|---|
| Aplicação | `/opt/automais-zap/api` — **esvaziado a cada deploy** |
| Env do serviço | `/etc/automais-zap/env` (`600`, dono `zap`) |
| Chaves Data Protection | `/var/lib/automais-zap/chaves` (`700`, dono `zap`) — **fora do diretório de deploy de propósito** |
| Unit | `automais-zap` (`User=zap`, bind `127.0.0.1:5086`) |
| Runtime .NET | `/opt/dotnet` + symlink `/usr/local/bin/dotnet` (ASP.NET Core 10.0.11) |
| nginx | `/etc/nginx/sites-available/{000-default,api.smsmais.automais.com,smsmais.automais.com}` |
| Postgres | 16.15, `listen_addresses = localhost`, database `zap`, role `zap`, schema `zap` |

Firewall `ufw` ativo: só **22**, **80** e **443**. Postgres nunca sai do loopback.

## Variáveis do serviço

| Chave | O que é |
|---|---|
| `ConnectionStrings__ZapDb` | Postgres local |
| `Meta__AppSecret` | Confere o HMAC da Meta. **Vazio → webhook responde 503** (falha fechado, de propósito) |
| `Meta__VerifyToken` | Handshake do webhook |
| `DataProtection__CaminhoChaves` | `/var/lib/automais-zap/chaves` |
| `Relay__UrlWebhookPublica` | `https://api.smsmais.automais.com/meta/webhook` — preenche a Callback URL na tela |
| `Legal__*` | Dados institucionais das páginas públicas de privacidade/termos/exclusão |

O **token do System User** não fica no env: é salvo cifrado no banco, pela tela `/admin/meta`.

`Admin__Email`/`Admin__SenhaInicial` **não ficam no env**: entram só no boot em que o primeiro
operador é semeado, e saem em seguida.

## Operação

```bash
systemctl status automais-zap
journalctl -u automais-zap -n 100 --no-pager
curl -s http://127.0.0.1:5086/health

# banco
sudo -u postgres psql -d zap -c "\dt zap.*"
sudo -u postgres psql -d zap -c "SELECT * FROM zap.numero;"

# nginx
nginx -t && systemctl reload nginx
certbot certificates
```

## Deploy

Pelo `.github/workflows/deploy-zap.yml`, em push a `main` que toque `Automais.Zap/**`.
Secrets necessários no GitHub:

| Secret | Valor |
|---|---|
| `HOST_ZAP` | `147.182.218.159` |
| `USER_ZAP` | `root` |
| `PASS_ZAP` | senha de root |
| `DB_CONNECTION_ZAP` | connection string do Postgres local |
| `META_APP_SECRET` | App Secret do App da Meta |
| `META_VERIFY_TOKEN` | verify token |

Se `META_APP_SECRET`/`META_VERIFY_TOKEN` não estiverem nos secrets, o deploy **preserva** o que já
está no host em vez de apagar — um deploy não pode desconfigurar o webhook. Só `DB_CONNECTION_ZAP`
é fatal quando ausente.

O primeiro deploy foi manual (publish local + SFTP), porque os secrets ainda não existiam.

## Pendências

- **Trocar senha de root por chave SSH** e desligar `PasswordAuthentication`. O workflow usa senha
  hoje só por seguir o padrão dos outros deploys do monorepo.
- **Trocar a senha inicial do operador** do painel.
- **Backup**: não há. O banco é pequeno (rotas e log operacional), mas perder `zap.numero` é perder
  o roteamento de todos os clientes. Um `pg_dump` diário para fora do droplet resolve.
  Vale também guardar `/var/lib/automais-zap/chaves` — sem ela, todo mundo é deslogado do painel.
- **Monitoramento**: o relay é ponto único de falha do inbound de todos os clientes, e os recibos
  de status (`delivered`/`failed`) passam por ele. Ninguém é avisado hoje se ele cair.
