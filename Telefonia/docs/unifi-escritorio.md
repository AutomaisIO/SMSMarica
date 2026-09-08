# Escritório Automais — acesso ao controlador UniFi (interceptação do IP antigo)

> Aplicado em 02/09/2026. **Decisão do usuário: manter a interceptação por ora** — o roteador atende pelo IP antigo e encaminha para o controlador novo.

## 1. Contexto

O controlador UniFi vivia em `10.30.30.23`, **dentro da própria LAN do escritório**. Com a migração para o datacenter Eveo, o IP deixou de existir e as 6 APs ficaram órfãs.

Diferença crucial em relação a Maricá e ao IPR: aqui as APs estão **na mesma rede** do controlador antigo (`10.30.30.0/24`). Elas resolvem o destino por **ARP direto**, sem passar pelo roteador — então um `dst-nat` sozinho **nunca vê o tráfego** (ficou com 0 pacotes até o roteador assumir o endereço).

Havia ainda uma entrada **ARP estática legada** (`10.30.30.23 → BC:24:11:DC:ED:27`, o MAC da VM) — inútil, porque a VM não está mais nessa L2. Removida.

## 2. Equipamento

**AUTOMAIS-ROUTER** (CCR, ROS 7.23.1) — hub da empresa. Gestão: `10.30.50.8`, usuário **`becape`** (a senha do `admin` não vale aqui).

Carrega muita coisa: LAN `10.30.30.1/24`, MUNDIVOX `201.76.184.254/30` (default ativo), CEPH, FAILOVER-02, `wg-automais` (`10.30.50.8`), `wg-unidades` (`10.202.0.1`, relay antigo), `wg-voip` (`10.201.0.19`, CRAD id=9), `wg-smsreg` (túnel SISREG), `wg-maestro`, e vários L2TP (IPR, CONDE, LORASRV, Embavi, Sempre Verano, Datamais, Pedreira).

⚠️ **Nada disso foi tocado.** Só foram adicionados objetos novos.

## 3. O que foi feito

### No CCR2116 da Eveo (servidor) — tenant próprio
```
/interface wireguard add name=wg-escritorio listen-port=51835 mtu=1420
/ip address add address=10.205.0.1/24 interface=wg-escritorio
/ip firewall filter add chain=input accept udp in=ether1 dst-port=51835
/interface wireguard peers add interface=wg-escritorio public-key="<pubkey do AUTOMAIS-ROUTER>" allowed-address=10.205.0.2/32
```
Mesma política restritiva dos outros tenants:
```
forward accept tcp -> 10.90.40.23:8080  in=wg-escritorio
forward accept udp -> 10.90.40.23:3478  in=wg-escritorio
forward drop   dst-address-list=REDES-PRIVADAS in=wg-escritorio
input   drop   in=wg-escritorio
```

### No AUTOMAIS-ROUTER
```
/interface wireguard add name=wg-eveo listen-port=51835 mtu=1420
/ip address add address=10.205.0.2/24 interface=wg-eveo
/interface wireguard peers add interface=wg-eveo public-key="KMRBVCaOaAxoaee8xugj28YYvZIvKjbr+6rFRXj1qCs=" \
    endpoint-address=177.136.233.75 endpoint-port=51835 allowed-address=0.0.0.0/0 persistent-keepalive=25s
/ip route add dst-address=10.90.40.23/32 gateway=wg-eveo
/ip firewall nat add chain=srcnat dst-address=10.90.40.23 out-interface=wg-eveo action=masquerade
/ip firewall mangle add ... change-mss 1380 in/out wg-eveo

# INTERCEPTAÇÃO — o roteador atende pelo IP morto e encaminha
/ip address add address=10.30.30.23/24 interface=LAN comment="UNIFI: assume o IP do controlador antigo"
/ip firewall nat add chain=dstnat dst-address=10.30.30.23 protocol=tcp dst-port=8080 action=dst-nat to-addresses=10.90.40.23
/ip firewall nat add chain=dstnat dst-address=10.30.30.23 protocol=udp dst-port=3478 action=dst-nat to-addresses=10.90.40.23
```

**Sem PIN de endpoint**, de propósito: o túnel não é rota default (só um `/32`), então não há risco de se autoengolir — e assim sobrevive à troca de link, o que importa porque a **Mundivox foi cancelada**.

## 4. Resultado

| AP | IP | Estado |
|---|---|---|
| Cozinha | `10.30.30.140` | 🟢 CONECTADA · já migrada para `unifi.automais.cloud` |
| Administracao | `10.30.30.104` | 🟢 CONECTADA · provisionando a URL nova |
| Laboratorio | `10.30.30.161` | ⚫ não responde a ping — **desligada/fora da rede** |
| AC Pro | `10.30.30.152` | ⚫ idem |
| AC Pro | `10.30.30.178` | ⚫ idem |
| AC Pro | `10.30.30.180` | ⚫ idem |

As 4 que faltam **não é problema de configuração** — não respondem sequer a ping na LAN. Quando forem ligadas, conectam sozinhas pela interceptação e migram para o nome.

## 5. Por que a interceptação fica (por ora)

Enquanto houver AP com `inform_url = http://10.30.30.23:8080/inform` gravado — as 4 desligadas —, o IP precisa ser atendido por alguém. A interceptação é o que permite que elas voltem sem visita técnica.

**Quando remover:** depois que as 6 aparecerem como `CONECTADA` **e** com `inform=unifi.automais.cloud`. Aí:
```
/ip address remove [find comment~"UNIFI: assume o IP"]
/ip firewall nat remove [find comment~"UNIFI ponte legado"]
```

## 6. Faixas do datacenter

| Faixa | Tenant | Servidor |
|---|---|---|
| `10.203.0.0/24` | unidades SMS Maricá (`.<id+10>`) | `wg-unidades` :51833 |
| `10.204.0.0/24` | IPR | `wg-ipr` :51834 |
| `10.205.0.0/24` | Escritório Automais | `wg-escritorio` :51835 |

Backup: `antes-unifi-020926` no AUTOMAIS-ROUTER; `antes-wg-ipr-020926` no CCR2116.
