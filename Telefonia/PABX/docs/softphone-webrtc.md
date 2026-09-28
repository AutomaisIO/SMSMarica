# Softphone no navegador (WebRTC) — o que o servidor precisa

O softphone do SMSMais é um cliente SIP **dentro do navegador** (JsSIP) que fala **direto com o
Asterisk**:

```
Navegador (SMSMais)                                   VM de telefonia
 ├─ sinalização SIP ── wss://telefonia.smsmarica.online/ws ──► nginx :443 ──► Asterisk http :8088/ws
 └─ áudio DTLS-SRTP (UDP, ICE) ────────────────────────────────────────────► Asterisk RTP 10000-20000
```

Por que assim, e não um gateway de mídia nosso: o Asterisk já faz WebRTC, já grava, e é o único
que alcança os aparelhos das unidades pela VPN. Um B2BUA próprio acrescentaria latência, um ponto
único de falha e código de mídia para manter. Decisão registrada em 23/09/2026.

## Credencial: por que não é CPF + senha do SMSMais

- O digest SIP exige a senha **em claro** no Asterisk; o SMSMais só guarda hash PBKDF2.
- CPF como usuário SIP apareceria em cabeçalho SIP, log do Asterisk e CDR.

Então cada softphone tem **um segredo aleatório** (20 caracteres, cifrado com Data Protection no
Pabx). O SMSMais pede `GET /api/ramais/{n}/credencial` com a chave de serviço e repassa **só ao
próprio usuário logado**. O usuário não digita nada. Usuário SIP = número do ramal.

## Auditoria de 23/09/2026 (192.241.153.121, só leitura)

| Item | Achado | O que falta |
|---|---|---|
| Asterisk | 16.25.3, `chan_sip` | — |
| `res_http_websocket` / `res_srtp` | carregados; URI `/ws` registrada | — |
| `http.conf` | `bindaddr=127.0.0.1`, **servidor HTTP desligado** | `enabled=yes`, `bindport=8088` |
| `rtp.conf` | 10000–20000, sem ICE/STUN | `icesupport=yes`, `stunaddr` |
| `sip.conf [general]` | sem `transport=` (só UDP) | `transport=udp,wss` |
| `/etc/asterisk/keys` | vazio | certificado DTLS |
| `#include sip_smsmarica.conf` | ausente; Pabx não deployado | incluir + deploy |
| Contexto dos ramais | **`PLANO_HOSPITAIS`** (único; `_XXXX → Dial(SIP/…)` + MixMonitor) | defaults do Pabx corrigidos |
| Numeração existente | prefixos 10xx–21xx, 28xx, 31xx (`3100/3101` = TFD), 4000–4199 Conde, 70xx | **softphone = 6000–6999** (vazio); físicos sem faixa fixa |
| nginx `telefonia.smsmarica.online` | `/` → :5090 (página de espera) | `location /ws` |
| AMI 5038 | escuta em `0.0.0.0`, iptables policy ACCEPT | depende do cloud firewall da DO — **conferir** |
| Recursos | **1 vCPU, 957 MB RAM (≈320 MB livres)** | Whisper **não cabe** aqui: só após a migração para o datacenter |

## Checklist do servidor (cada item com OK — é produção)

1. **Auditoria (só leitura)**
   ```
   asterisk -rx "module show like websocket"     # res_http_websocket
   asterisk -rx "module show like srtp"          # res_srtp
   asterisk -rx "http show status"
   grep -v '^;' /etc/asterisk/http.conf /etc/asterisk/rtp.conf
   grep -n '^\[6[0-9][0-9][0-9]\]' /etc/asterisk/sip*.conf   # faixa 6000–6999 livre?
   ```
2. **http.conf** — só em loopback (o nginx é quem expõe):
   ```ini
   [general]
   enabled=yes
   bindaddr=127.0.0.1
   bindport=8088
   ```
3. **sip.conf [general]** — aceitar WSS (não afeta os ramais UDP):
   ```ini
   transport=udp,wss
   ```
   Hoje o `[general]` não declara `transport=` (auditoria 23/09); se alguém tiver acrescentado depois, só somar `wss`.
4. **rtp.conf**
   ```ini
   icesupport=yes
   stunaddr=stun.l.google.com:19302
   ```
5. **Certificado DTLS** em `/etc/asterisk/keys/asterisk.pem` + `.key` (autoassinado serve:
   `dtlsverify=fingerprint` confere a impressão digital trocada no SDP, não a cadeia).
   ```
   mkdir -p /etc/asterisk/keys
   openssl req -x509 -newkey rsa:2048 -nodes -days 3650 \
     -subj "/CN=telefonia.smsmarica.online" \
     -keyout /etc/asterisk/keys/asterisk.key -out /etc/asterisk/keys/asterisk.pem
   chown asterisk: /etc/asterisk/keys/*
   ```
6. **nginx** (`telefonia.smsmarica.online`, bloco 443 que já existe):
   ```nginx
   location /ws {
       proxy_pass http://127.0.0.1:8088/ws;
       proxy_http_version 1.1;
       proxy_set_header Upgrade $http_upgrade;
       proxy_set_header Connection "upgrade";
       proxy_set_header X-Real-IP $remote_addr;
       proxy_read_timeout 3600s;
       proxy_send_timeout 3600s;
   }
   ```
7. **Firewall** — RTP UDP 10000–20000 aberto para a internet (cloud firewall da DO **e** iptables);
   o navegador não passa pela VPN. Conferir que o CrowdSec/fail2ban não bane registro WSS.
8. **Ramal de teste** — `POST /api/ramais` com `"tipo":"Softphone"` na faixa 6000–6999 →
   `asterisk -rx "sip show peer 6001"` deve mostrar `Transport: WSS` e `Encryption: Yes`.
9. **Teste do lado de fora**: `wscat -c wss://telefonia.smsmarica.online/ws -s sip` conecta; o
   softphone registra; chamada 6001 ↔ ramal físico com áudio nos dois sentidos.

**Plano B** se o chan_sip se mostrar instável com WebRTC: endpoint PJSIP só-WSS
(`res_pjsip_transport_websocket`) + `_6XXX → Dial(PJSIP/${EXTEN})` num `extensions_smsmarica.conf`
incluído no `PLANO_HOSPITAIS`. O `IGeradorConfigSip` é plugável para isso; o resto da API não muda.

## Endpoints da API usados pelo SMSMais

| Verbo | Rota | Uso |
|---|---|---|
| GET | `/api/ramais/faixas/livres?tipo=Softphone` | sugerir número ao admin |
| POST | `/api/ramais` (`tipo=Softphone`, `donoSistema=smsmais`, `donoId=<usuarioId>`) | habilitar |
| PUT | `/api/ramais/{n}` | nome de exibição (`callerId`), ativo |
| GET/PUT | `/api/ramais/{n}/config` | contexto, codecs, call-limit |
| PUT | `/api/ramais/{n}/dono` | vincular/desvincular usuário |
| GET | `/api/ramais/{n}/credencial` | credencial para o navegador (no-store) |
| GET | `/api/ramais/{n}/peer` | diagnóstico (SIPshowpeer) |
| GET | `/api/ramais?donoSistema=smsmais&donoId=…` | reconciliação |
