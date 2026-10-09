# SISREG — egress de rede em produção (túnel WireGuard até o Datacenter Automais)

> Como o backend em produção (`api.smsmarica.online`, DigitalOcean) alcança o
> **SISREG III** (`sisregiii.saude.gov.br`) — consulta de paciente por CNS (CADSUS /
> `cadweb50`), ofertas, agenda e demais chamadas do motor. Ver também o motor da integração em
> `SMSMais.Core/Integracoes/SisregWeb/` e o laboratório `Automais.SISREG/`.

> **Mudança de 14/09/2026:** o egress saiu do MikroTik do escritório (link **Mundivox**,
> cancelado) e passou para o **CCR2116 do Datacenter Automais (Eveo/SP)**. O túnel antigo
> (`wg-mk`) continua de pé, mas **não carrega mais o SISREG** — ver §Histórico.

> **Mudança de 09/10/2026 — o SISREG trocou de IP sem aviso.** `sisregiii.saude.gov.br` passou a
> ser CNAME de `ves-io-0e89aecd-….ac.vh.ves.io` (**F5 Distributed Cloud**, nó `sp4-sao`), IP
> **`159.60.146.75`**; o `189.28.130.13` passou a **recusar** conexão. Como o túnel só desviava o
> `/32` antigo, a VM saiu para o IP novo pela DigitalOcean/EUA e ficou sem resposta — o coletor
> logava "Não foi possível autenticar no SISREG", o que **parecia** bloqueio do IP da Eveo e não
> era (da Eveo, HTTP 200). Correção (12:48 UTC): IP novo no `AllowedIPs`; o CCR passou a liberar
> **TCP 443 para qualquer destino** (quem decide o que entra no túnel é a VM); e a VM ganhou o
> **verificador** `sisreg-egress-verificar` (timer de 1 min) que põe sozinho no túnel o IP que o
> DNS devolver — ver §Verificador.

## Problema

O SISREG/DATASUS **recusa conexões vindas do IP da DigitalOcean**. Sintoma original: a chamada
`GET /integracoes/cns` retornava `500 — Connection refused (sisregiii.saude.gov.br:443)`.

- `sisregiii.saude.gov.br` resolvia para um **único IP**: **`189.28.130.13`** (DATASUS, Brasil).
  Desde 09/10/2026 resolve para **`159.60.146.75`** (F5 Distributed Cloud) — o IP **muda**, por
  isso a VM tem o verificador.
- Do IP da VM (`146.190.65.73`, datacenter nos EUA) a conexão TCP 443 é recusada.
- **O bloqueio é geográfico, não "de datacenter"**: o M-EGRESS de 02/09/2026 mostrou o SISREG
  respondendo HTTP 200 ao IP público da Eveo (datacenter em SP).

## Solução: túnel WireGuard **dedicado** VM → CCR2116 Eveo

Um túnel WireGuard **exclusivo para o SISREG**, num tenant próprio do CCR do datacenter
(como já são IPR `10.204` e escritório `10.205`). Apenas o tráfego destinado ao(s) IP(s) do
SISREG entra no túnel; sai pela internet da Eveo (`177.136.233.75`, SP). Todo o
resto do tráfego da VM continua saindo normalmente pela DigitalOcean.

### Topologia

```
[VM smsmarica.online]              WireGuard dedicado                 [CCR2116 Datacenter Eveo]
 146.190.65.73 (público)  ── wg-eveo ⇄ wg-smsmarica (10.206.0.0/24) ──  177.136.233.75 (público)
 wg-eveo = 10.206.0.2  ─────── VM disca o CCR (keepalive 25s) ───────►  wg-smsmarica = 10.206.0.1
        │                                                                     │
        │ rota: <IP do SISREG>/32 dev wg-eveo                                 │ main → ether1 (EVEO-UPLINK)
        │ (mantida pelo verificador)                                          │ masquerade out ether1
        ▼                                                                     ▼
   só o SISREG entra no túnel                                SISREG 159.60.146.75 (F5 sp4-sao, HTTP 200)
```

- **Direção:** a **VM disca** o CCR (o CCR é o *listener* em UDP `51836`), mesmo padrão dos
  outros tenants do datacenter. A VM não precisa de porta de entrada.
- **Isolamento no CCR:** desde 09/10/2026 o túnel só pode sair por **TCP 443** (qualquer
  destino); o resto que entre por `wg-smsmarica` é descartado, e o túnel não gerencia o router
  (só ICMP em `10.206.0.1`). O CCR **não precisa saber** o IP do SISREG — quem escolhe o que
  entra no túnel é o `AllowedIPs` da VM. Antes era só `dst=189.28.130.13`, e foi isso que
  quebrou quando o IP mudou.
- **NAT:** o masquerade genérico `EVEO-UPLINK` (`out-interface=ether1`) já cobre; nenhuma regra
  de NAT nova. O tráfego segue pela tabela `main` (não passa pelo Automais.FW — `via-fw` só é
  usada pelo TFD).

### Endereçamento

| Elemento | Valor |
|---|---|
| Subnet do túnel (tenant) | `10.206.0.0/24` |
| VM (interface `wg-eveo`) | `10.206.0.2/24`, porta local efêmera |
| CCR (interface `wg-smsmarica`) | `10.206.0.1/24`, listen UDP `51836` |
| Endpoint que a VM disca | `177.136.233.75:51836` |
| IP com que o SISREG vê a produção | `177.136.233.75` (Eveo/SP) |
| Destino roteado pelo túnel | IP(s) do SISREG: **`159.60.146.75/32`** (desde 09/10/2026) + `189.28.130.13/32` (antigo) — acrescentados pelo verificador |
| Chave pública da VM (`wg-eveo`) | `/yuolOIvOADiEPqeLSrGGTZe1v26IUnIWGGI99WHdm0=` |
| Chave pública do CCR (`wg-smsmarica`) | `sB+1WLDoPZkbNW5FWHf6zoYngvMSit3gzJqjzFM3qnU=` |

> Chaves **privadas** ficam só nos respectivos equipamentos e **não** são versionadas.

## Configuração aplicada

### VM `smsmarica.online` (Ubuntu, `wg-quick`)

`/etc/wireguard/wg-eveo.conf` (chave privada em `/etc/wireguard/wg-eveo.key`, modo 600):

```ini
[Interface]
Address = 10.206.0.2/24
MTU = 1420
PrivateKey = <privada da VM — não versionar>

[Peer]
PublicKey = sB+1WLDoPZkbNW5FWHf6zoYngvMSit3gzJqjzFM3qnU=
Endpoint = 177.136.233.75:51836
AllowedIPs = 10.206.0.1/32, 189.28.130.13/32, 159.60.146.75/32
PersistentKeepalive = 25
```

Habilitado no boot: `systemctl enable --now wg-quick@wg-eveo`.

O `AllowedIPs = …, <IP>/32` faz o `wg-quick` criar a rota `<IP>/32 dev wg-eveo` — **só o
SISREG** entra no túnel. A lista **só cresce**: o verificador acrescenta IP novo e nunca remove.

⚠️ **O `/32` do SISREG só pode estar em UM dos `.conf`.** Se estiver no `wg-mk.conf` e no
`wg-eveo.conf` ao mesmo tempo, o segundo `wg-quick` a subir no boot falha ao criar a rota
duplicada. Hoje `wg-mk.conf` tem só `AllowedIPs = 172.31.99.2/32`.

### CCR2116 Eveo (`10.30.50.26` pela automais-vpn, usuário `becape`, RouterOS 7.16.2)

```routeros
/interface wireguard add name=wg-smsmarica listen-port=51836 mtu=1420 \
    comment="Server VPN smsmarica.online (VM DO) - egress SISREG"
/ip address add address=10.206.0.1/24 interface=wg-smsmarica comment="server tunel smsmarica.online"
/interface wireguard peers add interface=wg-smsmarica \
    public-key="/yuolOIvOADiEPqeLSrGGTZe1v26IUnIWGGI99WHdm0=" allowed-address=10.206.0.2/32 \
    comment="smsmarica.online VM 146.190.65.73 - SISREG"

# entrada do WireGuard (antes do drop geral da WAN)
/ip firewall filter add chain=input action=accept protocol=udp in-interface=ether1 dst-port=51836 \
    comment="WireGuard wg-smsmarica" place-before=[find comment="DROPP ALL UNCKNOW"]
# o túnel só sai por HTTPS (desde 09/10/2026; antes era dst-address=189.28.130.13)
/ip firewall filter add chain=forward action=accept in-interface=wg-smsmarica out-interface=ether1 \
    protocol=tcp dst-port=443 comment="SMSMARICA: server so sai por HTTPS (443) - destino decidido pela VM" \
    place-before=[find comment="fwd: established/related"]
/ip firewall filter add chain=forward action=drop in-interface=wg-smsmarica \
    comment="SMSMARICA: resto bloqueado" place-before=[find comment="fwd: established/related"]
# o túnel não gerencia o router
/ip firewall filter add chain=input action=accept protocol=icmp dst-address=10.206.0.1 \
    in-interface=wg-smsmarica comment="smsmarica: so ICMP no router"
/ip firewall filter add chain=input action=drop in-interface=wg-smsmarica \
    comment="smsmarica NAO gerencia o router"
```

Backup antes da mudança: `antes-wg-smsmarica-140926.rsc` no disco do CCR. Antes da mudança de
09/10/2026: `antes-sisreg-f5-091026.rsc`. A address-list **`SISREG`** (159.60.146.75,
189.28.130.13 e o nome `sisregiii.saude.gov.br`) ficou no CCR **sem regra usando** — reserva para
voltar a restringir por destino (`dst-address-list=SISREG`) se um dia for preciso.

## Verificador (VM) — o IP do SISREG muda sem aviso

O WireGuard só aceita IP fixo no `AllowedIPs`, nunca nome. Por isso a VM tem um timer de 1 min
que resolve `sisregiii.saude.gov.br` e, se o IP não estiver no túnel, **acrescenta** (`wg set`),
garante a rota `dev wg-eveo` e grava no `wg-eveo.conf`. Nunca remove IP.

| Peça | Onde |
|---|---|
| Script | `/usr/local/sbin/sisreg-egress-verificar` (com `flock`, idempotente) |
| Units | `sisreg-egress-verificar.service` (oneshot) + `.timer` (`OnUnitActiveSec=1min`), habilitado |
| Log | `journalctl -t sisreg-egress` — só escreve quando mexe ("IP novo … incluido no tunel") |

Testado em 09/10/2026 simulando a troca (tirando o IP do túnel e a rota): o verificador
recolocou os dois em < 1 s e o SISREG respondeu 200. A **aplicação** observa o mesmo por conta
própria (DNS + `ip route get`) e mostra o IP atual, desde quando e se sai pelo túnel no card do
SISREG em **Integrações**, com aviso no celular quando o IP muda ou sai fora do túnel.

## Verificação

Na VM:

```bash
wg show wg-eveo                      # "latest handshake" recente e transfer > 0
getent hosts sisregiii.saude.gov.br  # IP atual (09/10/2026: 159.60.146.75)
ip route get 159.60.146.75           # esperado: dev wg-eveo src 10.206.0.2
ping -c3 10.206.0.1                  # CCR pelo túnel (~125 ms)
curl -sS -o /dev/null -w "%{http_code} %{remote_ip}\n" https://sisregiii.saude.gov.br/
# esperado: 200 159.60.146.75
systemctl list-timers sisreg-egress-verificar.timer
```

No CCR: `/interface wireguard peers print detail where interface=wg-smsmarica` (handshake,
`rx`/`tx` crescendo) e `/ip firewall filter print stats where comment~"SMSMARICA"`.

Resultado da virada (2026-09-14): handshake OK, ping ao CCR 0% de perda (125 ms), teste
forçado `curl --interface wg-eveo` HTTP 200 **antes** de trocar a rota, e depois 3 chamadas
seguidas HTTP 200 em 0,8–1,2 s já pelo caminho de produção.

## Operação

- **Persistência:** `wg-quick@wg-eveo` habilitado no boot; config do CCR é permanente.
- **Se o SISREG mudar de IP:** nada a fazer — o verificador põe o IP novo no túnel em até 1 min
  e o CCR já libera qualquer destino em 443. Conferir no card do SISREG em Integrações (ou
  `journalctl -t sisreg-egress`). Se o SISREG deixar de ser HTTPS/443, aí sim mexer no CCR.
- **Outro sistema bloqueado geograficamente (SER, CADSUS…):** só o lado da VM — `/32` no
  `AllowedIPs` (ao vivo com `wg set` + no `.conf`); o CCR já libera 443. Se o IP dele também
  mudar, generalizar o verificador para uma lista de nomes. Não rotear `0.0.0.0/0` pelo túnel.
- **Voltar para o caminho antigo (emergência, enquanto a Mundivox existir):**
  ```bash
  ip route replace 189.28.130.13/32 dev wg-mk
  wg set wg-mk peer 8nWuHEFQe4W/qXdBagtRsI0sOyGxuS9j0zKoIhM5vj8= allowed-ips 172.31.99.2/32,189.28.130.13/32
  ```
  (e, para persistir, mover o `/32` de volta de um `.conf` para o outro).
- **Remover o túnel novo (sem efeito colateral):** VM `systemctl disable --now wg-quick@wg-eveo`;
  CCR remover `wg-smsmarica`, peer, endereço e as 5 regras `smsmarica`/`SMSMARICA`.
- **Ponto de atenção:** o CCR2116 Eveo passa a ser dependência da produção (já é o concentrador
  das unidades em contingência — ver `Telefonia/docs/failover-v5/`).

## Relação com a aplicação

O motor `SisregWebSessao` (backend) fala com `https://sisregiii.saude.gov.br` normalmente
— **não sabe** do túnel; o roteamento é transparente no kernel da VM. Nenhuma configuração de
proxy no código. A credencial (usuário/senha do operador) e o liga/desliga da reautenticação
automática ficam na tela **Integrações → SISREG**.

## Histórico — túnel antigo pelo escritório (01/07 → 14/09/2026)

De 01/07 a 14/09/2026 o SISREG saía pelo túnel `wg-mk` (VM `172.31.99.1`, listener UDP `51830`)
até o MikroTik `AUTOMAIS-ROUTER` do escritório (`wg-smsreg`, `172.31.99.2`), que discava a VM
e saía pela WAN **MUNDIVOX** (`201.76.184.254`). Com a Mundivox cancelada, esse caminho ia cair
junto com o link. Estado atual: `wg-mk` **continua de pé** (MK ainda disca a VM), mas com
`AllowedIPs = 172.31.99.2/32` — não roteia mais o SISREG. Backup do `.conf` anterior em
`/etc/wireguard/wg-mk.conf.bak-140926`. Para desmontar de vez: remover `wg-smsreg` + peer no
MK e, na VM, `wg-quick down wg-mk && systemctl disable wg-quick@wg-mk && rm /etc/wireguard/wg-mk.*`.
