# IPR (IPRecreio) — acesso ao controlador UniFi pelo Datacenter Automais

> Aplicado em 02/09/2026. **3 APs reconectadas, sem resquício do IP antigo.**
> ⚠️ Tenant **separado** da SMS Maricá — VPN, faixa e política próprias.

## 1. Contexto

O MK do IPR (`IPRecreio`, hEX, ROS 7.23.1, gestão `10.40.0.2`, usuário `admin`) alcançava o controlador antigo por uma rota `10.30.30.0/24 via AUTOMAIS` (L2TP para o escritório). Com o controlador migrado para o datacenter Eveo e o link Mundivox cancelado, **essa rota virou legado morto** — as 3 APs ficaram órfãs.

Os tenants são isolados: o CCR2116 da Eveo **não alcança** `10.40.0.2`, e não havia túnel entre eles.

## 2. Topologia da unidade

| Interface | Papel |
|---|---|
| `ether1` | INTERNET OI (`192.168.1.7`, gw `192.168.1.254`) |
| `ether2` | INTERNET VIVO (`192.168.15.2`, gw `192.168.15.1`) — **default ativo** |
| `ether3` | switch do escritório (na `Bridge-IPR`) |
| `Bridge-IPR` | LAN `10.100.0.1/20` — as APs vivem em `10.100.2.x` |
| `automais-vpn` | gestão `10.40.0.2` |
| `AUTOMAIS` (l2tp-out) | túnel para o escritório (`172.20.0.3`) — mantido, serve `172.19.10.0/24` |
| `wg-eveo` | **novo** — `10.204.0.2`, túnel ao datacenter |

Failover de link já existente: default por VIVO (dist 1) com probe `1.0.0.1`, OI (dist 2) com probe `8.8.4.4`, mais tabelas `via-oi` / `via-vivo` para streaming RTMP. **Não foi tocado.**

## 3. O que foi feito

### No CCR2116 da Eveo (servidor) — tenant próprio
```
/interface wireguard add name=wg-ipr listen-port=51834 mtu=1420 comment="Server VPN IPR - acesso ao controlador UniFi"
/ip address add address=10.204.0.1/24 interface=wg-ipr comment="server tunel IPR"
/ip firewall filter add chain=input action=accept protocol=udp in-interface=ether1 dst-port=51834 comment="WireGuard wg-ipr"
/interface wireguard peers add interface=wg-ipr public-key="<pubkey do IPR>" allowed-address=10.204.0.2/32 comment="IPR - IPRecreio"
```
Política de firewall espelhando a das unidades de Maricá (só o controlador, nada mais):
```
forward accept tcp -> 10.90.40.23:8080  in=wg-ipr      ;;; inform
forward accept udp -> 10.90.40.23:3478  in=wg-ipr      ;;; STUN
forward drop   dst-address-list=REDES-PRIVADAS in=wg-ipr
input   drop   in=wg-ipr                                ;;; IPR não gerencia o router
```

### No MK do IPR (cliente)
```
/interface wireguard add name=wg-eveo listen-port=51834 mtu=1420 comment="Relay EVEO CCR2116 - controlador UniFi"
/ip address add address=10.204.0.2/24 interface=wg-eveo comment="tunel eveo IPR"
/interface wireguard peers add interface=wg-eveo public-key="spCnblV2GNMsmRfZABwDFeCe5R0HEx5r0I0P8dLN9TA=" \
    endpoint-address=177.136.233.75 endpoint-port=51834 allowed-address=0.0.0.0/0 persistent-keepalive=25s
/ip route add dst-address=177.136.233.75/32 gateway=192.168.15.1 comment="PIN endpoint wg-eveo pela fisica (VIVO)"
/ip route add dst-address=10.90.40.23/32 gateway=wg-eveo comment="UNIFI controlador unifi.automais.cloud via tunel EVEO"
/ip firewall nat add chain=srcnat dst-address=10.90.40.23 out-interface=wg-eveo action=masquerade comment="UNIFI nat saida pelo tunel"
/ip firewall mangle add ... change-mss 1380 in/out wg-eveo
```
**Legado removido:** a rota `10.30.30.0/24 via AUTOMAIS`. (A rota `172.19.10.0/24 via AUTOMAIS` foi **mantida** — tem outra função.)

### Ponte temporária (já removida)
As APs tinham o IP morto gravado. Um `dst-nat` local traduzia `10.30.30.23:8080/3478 → 10.90.40.23` para elas conseguirem conectar e receber a URL nova. Depois do `force-provision`, a ponte foi removida.

## 4. DNS — já estava correto

O DHCP do MK (`Principal`, escopo `10.100.0.0/20`) entrega **o próprio MK como DNS** (`10.100.0.1`), e o MK tem `allow-remote-requests=yes` com forwarders públicos. As APs resolvem `unifi.automais.cloud` sem nenhum ajuste.

> Diferente de Maricá, onde as APs tinham **IP estático com `dns1=8.8.8.8`** (inalcançável) e precisaram de correção no controlador.

## 5. Resultado

| AP | IP | Estado |
|---|---|---|
| Templo-Radio2 | `10.100.2.17` | 🟢 CONECTADA · `unifi.automais.cloud` |
| AC Pro | `10.100.2.5` | 🟢 CONECTADA · `unifi.automais.cloud` |
| AC Pro | `10.100.2.20` | 🟢 CONECTADA · `unifi.automais.cloud` |

Verificação: `rotas 10.30.30 = 0`, `nat legado = 0`, conexões com `SEEN-REPLY` em 8080 e 3478.

## 6. Faixas reservadas do datacenter

| Faixa | Uso | Servidor |
|---|---|---|
| `10.203.0.0/24` | unidades **SMS Maricá** (`.1` DC, `.<id+10>` unidade) | `wg-unidades` :51833 |
| `10.204.0.0/24` | **IPR** (`.1` DC, `.2` IPR) | `wg-ipr` :51834 |

Backups: `antes-wg-ipr-020926` (CCR2116) e `antes-unifi-020926` (MK do IPR).

## 7. Pendência

As **6 APs do escritório** (`10.30.30.x`, site Default) seguem órfãs — mesma causa, e a solução seria a mesma: um caminho até `10.90.40.23`. Escopo da outra frente.
