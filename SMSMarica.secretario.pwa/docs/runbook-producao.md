# Runbook de produção — Painel do Secretário (secretario.smsmarica.online)

> **NADA neste runbook roda automaticamente.** Toda ação em produção (DNS, nginx,
> certbot, env file, deploy, restart) exige **OK explícito do operador** por ação
> — regra do projeto ([memória: produção — confirmar antes de tocar]). Este
> documento é o passo a passo para o operador executar por SSH, na ordem.
>
> **Única exceção (pré-autorizada, padrão do repo):** uma vez o workflow em `main`,
> **todo push que toque `SMSMarica.secretario.pwa/**` reimplanta back e front em
> produção** — igual ao `smsmarica-server`. Alterações apenas em
> `SMSMarica.secretario.pwa/docs/**` estão excluídas do gatilho e não reimplantam.

## Estado atual (24/07/2026)

Os passos **1 a 4 já foram executados** — DNS resolvendo, vhost no ar, certificado
Let's Encrypt emitido e env file com a credencial Oracle provisionado (`chmod 600`).
`https://secretario.smsmarica.online` serve hoje um placeholder da marca; `/api/painel`
responde **502** até o primeiro deploy do back. Os passos abaixo ficam registrados
para reconstrução do zero e para o passo 5 em diante.

**Contexto**: droplet DigitalOcean de `smsmarica.online` (mesmo host dos secrets
`HOST_SMSMARICA`/`USER_SMSMARICA`/`PASS_SMSMARICA`). Já rodam nele:
`smsmarica-server` (5080), `automais-fhir` (5081), `aiengine` (5085). O painel
adiciona: **back** `SMSMarica.Secretario.Api` na porta **5090** (systemd
`smsmarica-secretario`, app em `/opt/smsmarica-secretario/api`, env em
`/etc/smsmarica-secretario/env`) + **front** estático em
`/var/www/smsmarica-secretario`. O host alcança o Oracle do hospital
(`10.50.0.18`) pelo túnel WireGuard existente.

---

## 1. DNS

Criar registro **A** no provedor de DNS do domínio `smsmarica.online`:

| Tipo | Nome | Valor |
|------|------|-------|
| A | `secretario` | mesmo IP de `smsmarica.online` (IP do droplet) |

Conferir propagação antes de seguir (o certbot precisa do DNS resolvendo):

```bash
dig +short secretario.smsmarica.online
dig +short smsmarica.online   # os dois devem devolver o MESMO IP
```

## 2. nginx — vhost

Criar o server block (HTTP; o certbot converte para HTTPS no passo 3):

```bash
cat > /etc/nginx/sites-available/secretario.smsmarica.online <<'NGINX'
server {
    listen 80;
    listen [::]:80;
    server_name secretario.smsmarica.online;

    root /var/www/smsmarica-secretario;
    index index.html;

    gzip on;
    gzip_types text/plain text/css application/javascript application/json
               image/svg+xml application/manifest+json font/woff2;

    # Painel público sem auth: não deve ser emoldurável por terceiro.
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "SAMEORIGIN" always;

    # index.html sempre revalida (o PWA se atualiza a cada deploy)
    location = /index.html {
        add_header Cache-Control "no-cache";
    }

    # assets com hash no nome — cache imutável de 1 ano
    location /assets/ {
        add_header Cache-Control "public, max-age=31536000, immutable";
        try_files $uri =404;
    }

    # API do painel — proxy para o back local (5090, só loopback).
    # Prefixo SEM barra final: `/api` puro também vai ao back (com barra, cairia
    # no fallback SPA e devolveria index.html com 200 — diagnóstico enganoso).
    location /api {
        proxy_pass http://127.0.0.1:5090;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 30s;
    }

    # health do back exposto para diagnóstico
    location = /health {
        proxy_pass http://127.0.0.1:5090;
        proxy_set_header Host $host;
    }

    # SPA fallback
    location / {
        try_files $uri $uri/ /index.html;
    }
}
NGINX

ln -sf /etc/nginx/sites-available/secretario.smsmarica.online /etc/nginx/sites-enabled/
nginx -t && systemctl reload nginx
```

## 3. Certificado TLS

```bash
certbot --nginx -d secretario.smsmarica.online
```

Escolher redirect HTTP→HTTPS quando o certbot perguntar. Conferir:

```bash
curl -sI https://secretario.smsmarica.online | head -5
```

## 4. Env file — credencial Oracle (manual, 1x)

> **Credencial de LEITURA do Salux — nunca no git.** O arquivo fica só no
> servidor, `chmod 600 root:root`. O workflow de deploy **nunca sobrescreve**
> este arquivo se ele já existir (se não existir, cria vazio com aviso).

```bash
mkdir -p /etc/smsmarica-secretario
cat > /etc/smsmarica-secretario/env <<'ENV'
# Credencial Oracle do Salux (LEITURA) — nunca commitar, nunca copiar pro git.
Salux__Usuario=
Salux__Senha=
Salux__Host=10.50.0.18
Salux__Porta=1521
Salux__Servico=ORASX01
ENV
chmod 600 /etc/smsmarica-secretario/env
chown root:root /etc/smsmarica-secretario/env
```

Preencher `Salux__Usuario` e `Salux__Senha` com a conta de leitura do schema
`INFOSAUDE` (editar com `nano`/`vi` direto no servidor). Depois de qualquer
alteração no env: `systemctl restart smsmarica-secretario`.

## 5. Primeiro deploy

No GitHub: **Actions → Deploy SMSMarica.secretario.pwa → Run workflow** (branch
`main`). O workflow tem 2 jobs independentes (back e front); os dois devem
terminar verdes. O job do back cria usuário `secretario`, unit systemd e
diretórios na primeira execução (idempotente).

## 6. Validação

```bash
# back vivo (direto no loopback)
curl -s http://127.0.0.1:5090/health

# contrato do painel — deve devolver JSON com "geradoEm" e "oracle": {"ok": true}
curl -s http://127.0.0.1:5090/api/painel | head -c 400

# ponta a ponta pelo nginx/TLS
curl -s https://secretario.smsmarica.online/api/painel | head -c 400
```

No navegador: abrir `https://secretario.smsmarica.online` — o painel deve
carregar com números reais e carimbo de atualização recente. Conferir que o PWA
é **instalável** (Chrome: menu ⋮ → "Instalar app"; DevTools → Application →
Manifest sem erros).

Se `oracle.ok` vier `false` logo após o primeiro deploy: normal por alguns
segundos até o primeiro tick; se persistir, ver troubleshooting (§8).

## 7. Rollback

**Front** (o deploy atômico preserva a versão anterior como `.old`):

```bash
mv /var/www/smsmarica-secretario /var/www/smsmarica-secretario.broken
mv /var/www/smsmarica-secretario.old /var/www/smsmarica-secretario
# estático — não precisa reload do nginx
```

**Back**: re-rodar o workflow a partir do commit anterior (Actions → escolher o
run verde anterior → Re-run), ou manualmente com um tarball anterior:

```bash
systemctl stop smsmarica-secretario
find /opt/smsmarica-secretario/api -mindepth 1 -delete
tar -xzf /caminho/do/tarball-anterior.tar.gz -C /opt/smsmarica-secretario/api
chown -R secretario:secretario /opt/smsmarica-secretario/api
systemctl start smsmarica-secretario
systemctl is-active smsmarica-secretario
```

O env file e o snapshot (`/var/lib/smsmarica-secretario/snapshot-painel.json`)
não são tocados pelo rollback.

## 8. Troubleshooting

| Sintoma | Diagnóstico / ação |
|---------|--------------------|
| Serviço não sobe, porta 5090 ocupada | `fuser 5090/tcp` para ver o PID; `fuser -k 5090/tcp` para liberar; `systemctl restart smsmarica-secretario`. **Nunca** `pkill -f` com o nome da DLL (mata a própria sessão SSH). |
| `oracle.ok: false` no JSON | Oracle inalcançável. Checar o túnel WireGuard: `wg show` e `ping -c 3 10.50.0.18`. Se o túnel estiver ok, conferir credencial no env (`journalctl -u smsmarica-secretario -n 50` mostra o erro Oracle em `ultimoErro`). O painel continua servindo o último snapshot — degradação, não indisponibilidade. |
| Snapshot velho (`geradoEm` parado) | `journalctl -u smsmarica-secretario -n 100 --no-pager` — procurar erro no tick de atualização. Conferir permissão de escrita em `/var/lib/smsmarica-secretario/` (StateDirectory do systemd, dono `secretario`). |
| 502 no `/api/painel` | Back caiu: `systemctl status smsmarica-secretario`; journal para causa raiz. |
| Painel branco / assets 404 | Deploy do front incompleto: `ls /var/www/smsmarica-secretario` deve ter `index.html` + `assets/`. Re-rodar o job do front ou rollback (§7). |
| Certificado expirando | `certbot renew --dry-run`; timer padrão do certbot cuida da renovação. |
