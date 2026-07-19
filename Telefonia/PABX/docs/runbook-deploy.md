# Runbook — deploy do Automais.Pabx no servidor VOIP

Servidor: `192.241.153.121` (FALARMAIS-HOSPITAIS, Ubuntu 22.04, **compartilhado** — cada
passo que toca `/etc/asterisk` ou serviços exige OK prévio; sempre com backup).

## Pré-requisitos (1x, coordenar com a FalarMais)

1. **Include no sip.conf** (backup antes):
   ```bash
   cp /etc/asterisk/sip.conf /etc/asterisk/sip.conf.bak-$(date +%Y%m%d)
   # adicionar ao lado dos outros #include:
   #   #include sip_smsmarica.conf
   touch /etc/asterisk/sip_smsmarica.conf
   asterisk -rx 'sip reload'
   ```
2. **Usuário AMI próprio** em `/etc/asterisk/manager.conf` (backup antes):
   ```ini
   [automais_pabx]
   secret=<GERAR-SECRET-FORTE>
   deny=0.0.0.0/0.0.0.0
   permit=127.0.0.1/255.255.255.255
   read = system,call,command
   write = command
   displayconnects = no
   ```
   `asterisk -rx 'manager reload'`
3. **Faixa de numeração** acordada: proposta `2000–2299` (livre conforme
   [`servidor-existente.md`](./servidor-existente.md); o dialplan `_XXXX` já disca qualquer
   peer de 4 dígitos — sem mudança no extensions.conf).

## Publicar

No Windows (repo):
```powershell
.\deploy\publish.ps1     # gera .\publish (self-contained linux-x64, single file)
```

Enviar e instalar (1º deploy):
```bash
ssh root@192.241.153.121 'mkdir -p /opt/automais-pabx/{backups,keys}'
scp -r publish/* root@192.241.153.121:/opt/automais-pabx/
scp deploy/automais-pabx.service root@192.241.153.121:/etc/systemd/system/
ssh root@192.241.153.121 'chmod +x /opt/automais-pabx/Automais.Pabx.Api'
```

Criar `/opt/automais-pabx/appsettings.Production.json` (NUNCA commitar):
```json
{
  "Pabx": { "ApiKey": "<GERAR-KEY-FORTE>" },
  "Asterisk": { "Ami": { "Secret": "<secret do automais_pabx>" } },
  "ConnectionStrings": {
    "CdrDb": "Server=localhost;Database=asterisk;User=root;Password=<senha do cdr_mysql.conf>"
  }
}
```

Ativar:
```bash
systemctl daemon-reload
systemctl enable --now automais-pabx
curl -s http://127.0.0.1:5090/health          # → Healthy
```

## Firewall

Liberar 5090 apenas para gestão (e futuramente o IP do SMSMarica prod):
```bash
iptables -A INPUT -p tcp --dport 5090 -s <IP-gestao> -j ACCEPT
iptables -A INPUT -p tcp --dport 5090 -j DROP
```
(persistir conforme o mecanismo da caixa; conferir antes qual firewall a FalarMais usa.)

## Atualizações

```powershell
.\deploy\publish.ps1
scp -r publish/* root@192.241.153.121:/opt/automais-pabx/
ssh root@192.241.153.121 'systemctl restart automais-pabx && sleep 2 && curl -s http://127.0.0.1:5090/health'
```

O SQLite (`pabx.db`), as chaves (`keys/`) e o `appsettings.Production.json` ficam fora da
pasta publicada — sobrescrever o binário não os afeta.

## Smoke test pós-deploy

1. `curl -s -H "X-Api-Key: $KEY" http://127.0.0.1:5090/api/unidades | head`
2. Adotar a faixa existente 1000–1010 → `GET /api/ramais/status` deve mostrar os que estão
   online via VPN da unidade 0 (na auditoria: 1003–1006 em 10.200.0.11–14; 1001/1010 registram
   de IPs públicos — conferir a quem pertencem antes de atribuir unidade).
3. `GET /api/cdr?tamanhoPagina=3` retorna chamadas reais.
4. Criar ramal de teste → conferir `sip_smsmarica.conf`, `asterisk -rx 'sip show peer <n>'`.
5. `sip show peers` deve manter os peers da FalarMais intactos.

## Rollback

```bash
systemctl stop automais-pabx
# restaurar o backup mais recente se preciso:
ls /opt/automais-pabx/backups | tail
cp /opt/automais-pabx/backups/sip_smsmarica.conf.<stamp> /etc/asterisk/sip_smsmarica.conf
asterisk -rx 'sip reload'
```
Remover completamente = apagar a linha `#include sip_smsmarica.conf` do sip.conf (com backup) + `sip reload`.
