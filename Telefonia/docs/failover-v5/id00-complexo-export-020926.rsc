# 2026-09-02 16:09:48 by RouterOS 7.23.2
# software id = 9UP1-FYT0
#
# model = CCR1009-7G-1C-1S+
# serial number = D56A0C5D41FE
/interface bridge
add comment="L2 transparente ONU<->switch L3 - passa todas as VLANs" name=\
    bridge-transparente port-cost-mode=short protocol-mode=none
/interface ethernet
set [ find default-name=combo1 ] combo-mode=copper comment=\
    "Prefeitura / ONU - link PRIMARIO (ambiente seguro)"
set [ find default-name=ether1 ] name=ether1-GESTAO
set [ find default-name=ether2 ] comment="Switch L3 da unidade (LAN cliente)"
set [ find default-name=ether3 ] comment=\
    "CONNECT - provedor internet (failover + wifi) - NAO CONFIAVEL"
set [ find default-name=ether7 ] name=ether7-STARLINK
/interface wireguard
add comment="Automais.IO VPN" listen-port=13299 mtu=1420 name=automais-vpn
add comment="Relay EVEO CCR2116 (IP publico) - saida internet" listen-port=\
    51833 mtu=1420 name=wg-eveo
add comment="VOIP hub SMS Marica" listen-port=51820 mtu=1420 name=wg-voip
/interface list
add comment="AUTOMAIS.IO NAO APAGAR: lista de interfaces da VPN Automais.IO" \
    name=automais-io-vpn-source
add comment="gestao CCR" name=MGMT
/interface lte apn
set [ find default=yes ] ip-type=ipv4 use-network-apn=no
/interface wireless security-profiles
set [ find default=yes ] supplicant-identity=MikroTik
/ip pool
add name=pool-failover-lan ranges=10.3.74.10-10.3.74.200
/ip dhcp-server
add address-pool=pool-failover-lan authoritative=no comment=\
    "FAILOVER dhcp pmm.local" delay-threshold=5s disabled=yes interface=\
    bridge-transparente lease-time=1d name=FAILOVER-dhcp
/ip smb users
set [ find default=yes ] disabled=yes
/port
set 0 baud-rate=auto
set 1 baud-rate=auto
/routing bgp template
set default disabled=no output.network=bgp-networks
/routing ospf instance
add disabled=no name=default-v2
/routing ospf area
add disabled=yes instance=default-v2 name=backbone-v2
/system logging action
set 0 memory-lines=10000
/system script
add dont-require-permissions=no name=FO-tick owner=admin policy=\
    read,write,policy,test source="# FO-tick v5 (Motor B) - Complexo Regulador\
    \_id=0\r\
    \n# Estado = flag disabled da regra bridge nat \"FO-CAPTURA\". Sem estado \
    em RAM.\r\
    \n# Normal    : FO-* off, SHIM on, redirect DNS off  -> ponte transparente\
    \_(PMM dona)\r\
    \n# Contingencia: FO-* on, SHIM off, redirect DNS on -> captura L2 + saida\
    \_por wg-eveo\r\
    \n:global foDown; :global foUp\r\
    \n:if ([:typeof \$foDown] != \"num\") do={ :set foDown 0 }\r\
    \n:if ([:typeof \$foUp] != \"num\") do={ :set foUp 0 }\r\
    \n:local upS ([:tonsec [/system resource get uptime]] / 1000000000)\r\
    \n:if (\$upS < 180) do={ :log info \"FO: graca pos-boot\"; :error \"graca\
    \" }\r\
    \n:local anc [/interface bridge nat find comment=\"FO-CAPTURA\"]\r\
    \n:if ([:len \$anc] = 0) do={ :log error \"FO: ancora ausente\"; :error \"\
    sem ancora\" }\r\
    \n:local emFO (![/interface bridge nat get \$anc disabled])\r\
    \n# --- sondas ---\r\
    \n:local pmmUp 0\r\
    \n:foreach n in=[/tool netwatch find comment~\"FO-PMM\"] do={\r\
    \n  :if ([/tool netwatch get \$n status] = \"up\") do={ :set pmmUp (\$pmmU\
    p + 1) }\r\
    \n}\r\
    \n:local linkUp false\r\
    \n:do { :set linkUp [/interface get [find name=combo1] running] } on-error\
    ={}\r\
    \n:local pmmOK ((\$pmmUp >= 2) && \$linkUp)\r\
    \n:local pmmDOWN ((\$pmmUp = 0) || (!\$linkUp))\r\
    \n# --- debounce ---\r\
    \n:if (\$pmmDOWN) do={ :set foDown (\$foDown + 1) } else={ :set foDown 0 }\
    \r\
    \n:if (\$pmmOK)   do={ :set foUp   (\$foUp + 1) }   else={ :set foUp 0 }\r\
    \n# --- decisao --- (entra: 6 ticks=30s ; SAI: 24 ticks=120s de sondas boa\
    s)\r\
    \n:local want \$emFO\r\
    \n:if (\$emFO) do={\r\
    \n  :if (\$foUp >= 24) do={ :set want false }\r\
    \n} else={\r\
    \n  :local entrar ((\$foDown >= 6) || ((!\$linkUp) && (\$foDown >= 3)))\r\
    \n  :if (\$entrar) do={ :set want true }\r\
    \n}\r\
    \n# --- reconciliacao idempotente (roda TODO tick) ---\r\
    \n:local err 0\r\
    \n:foreach r in=[/interface bridge nat find comment~\"^FO-\"] do={\r\
    \n  :do { :if ([/interface bridge nat get \$r disabled] = \$want) do={ /in\
    terface bridge nat set \$r disabled=(!\$want) } } on-error={ :set err (\$e\
    rr + 1) }\r\
    \n}\r\
    \n:foreach r in=[/interface bridge nat find comment~\"SHIM retorno\"] do={\
    \r\
    \n  :do { :if ([/interface bridge nat get \$r disabled] = (!\$want)) do={ \
    /interface bridge nat set \$r disabled=\$want } } on-error={ :set err (\$e\
    rr + 1) }\r\
    \n}\r\
    \n:foreach r in=[/ip firewall nat find comment~\"FAILOVER dns\"] do={\r\
    \n  :do { :if ([/ip firewall nat get \$r disabled] = \$want) do={ /ip fire\
    wall nat set \$r disabled=(!\$want) } } on-error={ :set err (\$err + 1) }\
    \r\
    \n}\r\
    \n# --- DHCP do MK = espelho do estado do failover (decisao 02/09) ---\r\
    \n# Contingencia: liga (authoritative=yes, responde na hora). Normal: desl\
    iga (PMM e a dona).\r\
    \n:foreach d in=[/ip dhcp-server find name=\"FAILOVER-dhcp\"] do={\r\
    \n  :do {\r\
    \n    :if ([/ip dhcp-server get \$d disabled] = \$want) do={ /ip dhcp-serv\
    er set \$d disabled=(!\$want) }\r\
    \n    :if (\$want) do={ /ip dhcp-server set \$d authoritative=yes delay-th\
    reshold=0s } else={ /ip dhcp-server set \$d authoritative=no delay-thresho\
    ld=5s }\r\
    \n  } on-error={ :set err (\$err + 1) }\r\
    \n}\r\
    \n# --- transicao ---\r\
    \n:if (\$want != \$emFO) do={\r\
    \n  :foreach c in=[/ip firewall connection find src-address~\"10.3.74\"] d\
    o={ :do { /ip firewall connection remove \$c } on-error={} }\r\
    \n  :do { /ip dns cache flush } on-error={}\r\
    \n  :set foDown 0; :set foUp 0\r\
    \n  :log warning (\"FO-v5 \" . [:tostr \$want] . \" pmmUp=\" . \$pmmUp . \
    \"/3 link=\" . [:tostr \$linkUp] . \" err=\" . \$err)\r\
    \n}\r\
    \n"
add dont-require-permissions=no name=FO-harvest owner=admin policy=\
    read,write,test source="# FO-harvest - grava o IP atual de cada host como \
    lease estatica\
    \n:local n 0\
    \n:local j 0\
    \n:foreach a in=[/ip arp find interface=bridge-transparente] do={\
    \n  :local ip [/ip arp get \$a address]\
    \n  :local mac [/ip arp get \$a mac-address]\
    \n  :if ([:len \$mac] > 0) do={\
    \n    :if ([:typeof [:find \$ip \"10.3.74.\"]] != \"nil\") do={\
    \n      :if ([:len [/ip dhcp-server lease find address=\$ip]] = 0) do={\
    \n        :do {\
    \n          /ip dhcp-server lease add server=FAILOVER-dhcp address=\$ip ma\
    c-address=\$mac comment=\"harvest 020926\"\
    \n          :set n (\$n + 1)\
    \n        } on-error={ :set j (\$j + 1) }\
    \n      }\
    \n    }\
    \n  }\
    \n}\
    \n:log warning (\"FO-harvest: criadas=\" . \$n . \" falhas=\" . \$j)\
    \n:put (\"criadas=\" . \$n . \" falhas=\" . \$j)\
    \n"
/interface bridge nat
add action=accept chain=dstnat comment="SHIM excecao MK .254" dst-address=\
    10.3.74.254/32 in-interface=ether2 mac-protocol=ip src-address=\
    10.3.74.0/24
add action=accept chain=dstnat comment="SHIM excecao telefones gw" \
    dst-address=10.200.0.1/32 in-interface=ether2 mac-protocol=ip \
    src-address=10.3.74.0/24
add action=dst-nat chain=dstnat comment="SHIM retorno MK-MAC -> HPE" \
    dst-mac-address=48:8F:5A:8E:45:E4/FF:FF:FF:FF:FF:FF in-interface=ether2 \
    mac-protocol=ip src-address=10.3.74.0/24 to-dst-mac-address=\
    94:3F:C2:DF:49:D3
add action=redirect chain=dstnat comment=FO-DNS-UDP disabled=yes \
    dst-mac-address=94:3F:C2:DF:49:D3/FF:FF:FF:FF:FF:FF dst-port=53 \
    in-interface=ether2 ip-protocol=udp mac-protocol=ip src-address=\
    10.3.74.0/24
add action=accept chain=dstnat comment=FO-INTERNO-A disabled=yes dst-address=\
    10.0.0.0/8 dst-mac-address=94:3F:C2:DF:49:D3/FF:FF:FF:FF:FF:FF \
    in-interface=ether2 mac-protocol=ip src-address=10.3.74.0/24
add action=accept chain=dstnat comment=FO-INTERNO-B disabled=yes dst-address=\
    172.16.0.0/12 dst-mac-address=94:3F:C2:DF:49:D3/FF:FF:FF:FF:FF:FF \
    in-interface=ether2 mac-protocol=ip src-address=10.3.74.0/24
add action=redirect chain=dstnat comment=FO-CAPTURA disabled=yes \
    dst-mac-address=94:3F:C2:DF:49:D3/FF:FF:FF:FF:FF:FF in-interface=ether2 \
    mac-protocol=ip src-address=10.3.74.0/24
add action=arp-reply arp-dst-address=10.3.74.1/32 arp-opcode=request chain=\
    dstnat comment=FO-ARPREPLY disabled=yes in-interface=ether2 mac-protocol=\
    arp to-arp-reply-mac-address=94:3F:C2:DF:49:D3
/interface bridge port
add bridge=bridge-transparente ingress-filtering=no interface=combo1 \
    internal-path-cost=10 path-cost=10
add bridge=bridge-transparente ingress-filtering=no interface=ether2 \
    internal-path-cost=10 path-cost=10
/ip firewall connection tracking
set udp-timeout=10s
/ip neighbor discovery-settings
set discover-interface-list=MGMT
/ip settings
set max-neighbor-entries=8192
/ipv6 settings
set disable-ipv6=yes max-neighbor-entries=8192 soft-max-neighbor-entries=8191
/interface list member
add comment="AUTOMAIS.IO NAO APAGAR" interface=automais-vpn list=\
    automais-io-vpn-source
add interface=ether1-GESTAO list=MGMT
add interface=automais-vpn list=MGMT
/interface ovpn-server server
add auth=sha1,md5 mac-address=FE:76:2F:EA:FD:68 name=ovpn-server1
/interface wireguard peers
add allowed-address=10.35.0.0/24 endpoint-address=automais.io endpoint-port=\
    51825 interface=automais-vpn name=peer1 persistent-keepalive=25s \
    public-key="b9wFAOFy1KAbumlOox7dpUiWEWFhgAb+vnFvKXFqyFA="
add allowed-address=10.201.0.0/24 comment="hub asterisk" endpoint-address=\
    192.241.153.121 endpoint-port=51820 interface=wg-voip name=peer2 \
    persistent-keepalive=25s public-key=\
    "b0Da/MUl+tmetIjBN/OKfQqGCKuqtsZNsmx2Glmh9wo="
add allowed-address=0.0.0.0/0 comment="EVEO CCR2116 server" endpoint-address=\
    177.136.233.75 endpoint-port=51833 interface=wg-eveo name=peer4 \
    persistent-keepalive=25s public-key=\
    "1rA+xJyPWWjaAy3P0avFNNnIhl/XNycsz9FDmNpUHlc="
/ip address
add address=173.20.20.1/24 interface=ether1-GESTAO network=173.20.20.0
add address=10.35.0.23/24 interface=automais-vpn network=10.35.0.0
add address=10.3.74.254/24 comment="MK LAN ex-RB750r2 id=0" interface=\
    bridge-transparente network=10.3.74.0
add address=10.200.0.1/24 comment="gw telefones id=0" interface=\
    bridge-transparente network=10.200.0.0
add address=10.201.0.10/24 comment="tunel voip id=0" interface=wg-voip \
    network=10.201.0.0
add address=10.203.0.10/24 comment="tunel eveo id=0" interface=wg-eveo \
    network=10.203.0.0
/ip arp
add address=10.3.74.253 comment="FO alias next-hop do gw real (nao remover)" \
    interface=bridge-transparente mac-address=94:3F:C2:DF:49:D3
/ip dhcp-client
add comment=CONNECT default-route-distance=2 interface=ether3 name=ether3 \
    use-peer-dns=no
# Interface not active
add comment=STARLINK default-route-distance=8 interface=ether7-STARLINK name=\
    client1
/ip dhcp-server lease
add address=10.3.74.45 comment="harvest 020926" mac-address=AC:84:C6:A4:70:39 \
    server=FAILOVER-dhcp
add address=10.3.74.89 comment="harvest 020926" mac-address=E6:AC:CD:96:30:D7 \
    server=FAILOVER-dhcp
add address=10.3.74.124 comment="harvest 020926" mac-address=\
    22:01:B6:BD:22:89 server=FAILOVER-dhcp
add address=10.3.74.43 comment="harvest 020926" mac-address=98:E5:5B:89:B9:1D \
    server=FAILOVER-dhcp
add address=10.3.74.71 comment="harvest 020926" mac-address=D0:94:66:B0:78:78 \
    server=FAILOVER-dhcp
add address=10.3.74.85 comment="harvest 020926" mac-address=30:C5:99:33:BF:B2 \
    server=FAILOVER-dhcp
add address=10.3.74.115 comment="harvest 020926" mac-address=\
    F6:20:8B:13:42:91 server=FAILOVER-dhcp
add address=10.3.74.251 comment="harvest 020926" mac-address=\
    94:C6:91:C5:7C:27 server=FAILOVER-dhcp
add address=10.3.74.39 comment="harvest 020926" mac-address=D2:9E:EA:18:D7:27 \
    server=FAILOVER-dhcp
add address=10.3.74.14 comment="harvest 020926" mac-address=AC:C5:1B:65:FA:26 \
    server=FAILOVER-dhcp
add address=10.3.74.58 comment="harvest 020926" mac-address=76:5D:0C:E1:20:95 \
    server=FAILOVER-dhcp
add address=10.3.74.51 comment="harvest 020926" mac-address=74:56:3C:F2:81:1B \
    server=FAILOVER-dhcp
add address=10.3.74.130 comment="harvest 020926" mac-address=\
    E0:3E:CB:E1:4B:96 server=FAILOVER-dhcp
add address=10.3.74.95 comment="harvest 020926" mac-address=E0:3E:CB:E1:6C:50 \
    server=FAILOVER-dhcp
add address=10.3.74.84 comment="harvest 020926" mac-address=D0:94:66:B4:E3:4F \
    server=FAILOVER-dhcp
add address=10.3.74.68 comment="harvest 020926" mac-address=92:6B:74:47:0A:EA \
    server=FAILOVER-dhcp
add address=10.3.74.13 comment="harvest 020926" mac-address=AC:C5:1B:6B:5C:02 \
    server=FAILOVER-dhcp
add address=10.3.74.11 comment="harvest 020926" mac-address=AC:C5:1B:6A:2D:FE \
    server=FAILOVER-dhcp
add address=10.3.74.12 comment="harvest 020926" mac-address=42:4C:DA:39:9D:62 \
    server=FAILOVER-dhcp
add address=10.3.74.26 comment="harvest 020926" mac-address=02:1E:77:4F:B7:97 \
    server=FAILOVER-dhcp
add address=10.3.74.19 comment="harvest 020926" mac-address=BE:8E:76:96:DB:34 \
    server=FAILOVER-dhcp
add address=10.3.74.10 comment="harvest 020926" mac-address=AC:C5:1B:6B:5B:92 \
    server=FAILOVER-dhcp
add address=10.3.74.98 comment="harvest 020926" mac-address=C0:25:2F:00:DA:93 \
    server=FAILOVER-dhcp
add address=10.3.74.57 comment="harvest 020926" mac-address=56:CC:BE:13:69:55 \
    server=FAILOVER-dhcp
add address=10.3.74.41 comment="harvest 020926" mac-address=A6:9B:93:2A:F2:67 \
    server=FAILOVER-dhcp
add address=10.3.74.75 comment="harvest 020926" mac-address=5A:57:2B:5E:8D:BD \
    server=FAILOVER-dhcp
add address=10.3.74.111 comment="harvest 020926" mac-address=\
    B6:62:F7:85:4C:DA server=FAILOVER-dhcp
add address=10.3.74.18 comment="harvest 020926" mac-address=CC:3D:82:1B:BD:22 \
    server=FAILOVER-dhcp
add address=10.3.74.77 comment="harvest 020926" mac-address=28:2E:89:C9:B1:76 \
    server=FAILOVER-dhcp
add address=10.3.74.28 comment="harvest 020926" mac-address=5C:C5:D4:C7:30:BC \
    server=FAILOVER-dhcp
add address=10.3.74.76 comment="harvest 020926" mac-address=CC:3D:82:E4:35:9F \
    server=FAILOVER-dhcp
add address=10.3.74.80 comment="harvest 020926" mac-address=7A:30:14:63:E6:F8 \
    server=FAILOVER-dhcp
add address=10.3.74.90 comment="harvest 020926" mac-address=DE:19:34:6E:BC:3B \
    server=FAILOVER-dhcp
/ip dhcp-server network
add address=10.3.74.0/24 comment="escopo REAL da PMM capturado 02/09" \
    dns-server=10.135.16.18,10.135.16.119,10.135.16.17,1.1.1.1,8.8.4.4 \
    gateway=10.3.74.1
/ip dns
set allow-remote-requests=yes servers=10.135.16.18,10.135.16.119
/ip firewall address-list
add address=189.28.130.13 comment="SISREG DATASUS" list=SERVICOS-ESSENCIAIS
add address=200.166.238.18 comment="SER ser.saude.rj.gov.br" list=\
    SERVICOS-ESSENCIAIS
add address=146.190.65.73 comment="smsmarica.online (droplet)" list=\
    SERVICOS-ESSENCIAIS
/ip firewall filter
add action=accept chain=input dst-port=8291 protocol=tcp src-port=""
add action=accept chain=forward comment="VOIP: hub -> telefones id=0" \
    dst-address=10.200.0.0/24 in-interface=wg-voip
add action=accept chain=forward comment="VOIP: telefones -> hub id=0" \
    out-interface=wg-voip src-address=10.200.0.0/24
add action=drop chain=forward comment=\
    "VOIP: telefones bloqueados fora do tunel id=0" src-address=10.200.0.0/24
add action=accept chain=input comment="HRD established" connection-state=\
    established,related,untracked
add action=drop chain=input comment="HRD invalid" connection-state=invalid
add action=accept chain=input comment="HRD excecao SERVER VOIP (hub)" \
    dst-port=51820 in-interface=ether3 protocol=udp src-address=\
    192.241.153.121
add action=accept chain=input comment="HRD WireGuard VPNs (voip+automais)" \
    dst-port=51820,13299 in-interface=ether3 protocol=udp
add action=accept chain=input comment="HRD dhcp-client Connect" dst-port=68 \
    in-interface=ether3 protocol=udp
add action=drop chain=input comment=\
    "BLINDAGEM Connect eth3 (bloqueia wifi/ISP)" in-interface=ether3
/ip firewall mangle
add action=mark-connection chain=prerouting comment=\
    "AUTOMAIS.IO NAO APAGAR: vpn-src-connmark" in-interface-list=\
    automais-io-vpn-source new-connection-mark=automais-io-vpn
add action=change-mss chain=forward comment="MSS clamp wg-eveo out" new-mss=\
    1380 out-interface=wg-eveo protocol=tcp tcp-flags=syn tcp-mss=1381-65535
add action=change-mss chain=forward comment="MSS clamp wg-eveo in" \
    in-interface=wg-eveo new-mss=1380 protocol=tcp tcp-flags=syn tcp-mss=\
    1381-65535
/ip firewall nat
add action=redirect chain=dstnat comment="FAILOVER dns udp" disabled=yes \
    dst-port=53 in-interface=bridge-transparente protocol=udp src-address=\
    10.3.74.0/24 to-ports=53
add action=redirect chain=dstnat comment="FAILOVER dns tcp" disabled=yes \
    dst-port=53 in-interface=bridge-transparente protocol=tcp src-address=\
    10.3.74.0/24 to-ports=53
add action=masquerade chain=srcnat comment="NAT clientes via EVEO" \
    out-interface=wg-eveo src-address=10.3.74.0/24
/ip ipsec profile
set [ find default=yes ] dpd-interval=2m dpd-maximum-failures=5
/ip route
add comment="PIN hub VOIP pela fisica" dst-address=192.241.153.121/32 \
    gateway=192.168.0.1
add comment="PIN automais.io pela fisica" dst-address=198.211.104.55/32 \
    gateway=192.168.0.1
add check-gateway=ping comment=\
    "AUTOMAIS geral via Starlink (failover offload)" distance=5 dst-address=\
    0.0.0.0/0 gateway=192.168.1.1
add comment="PIN endpoint EVEO pela fisica" dst-address=177.136.233.75/32 \
    gateway=192.168.0.1
add check-gateway=ping comment="DEFAULT via EVEO (full tunnel)" distance=1 \
    dst-address=0.0.0.0/0 gateway=10.203.0.1
add comment="FO sonda PMM 1.1.1.1" dst-address=1.1.1.1/32 gateway=10.3.74.253 \
    pref-src=10.3.74.254
add comment="FO sonda PMM 1.0.0.1" dst-address=1.0.0.1/32 gateway=10.3.74.253 \
    pref-src=10.3.74.83
add comment="FO internas PMM sempre pelo gw real" dst-address=10.135.16.0/24 \
    gateway=10.3.74.253
add comment="FO sonda PMM 9.9.9.10" dst-address=9.9.9.10/32 gateway=\
    10.3.74.253
add comment="FO sonda PMM 8.8.8.8" dst-address=8.8.8.8/32 gateway=10.3.74.253
/ip service
set ftp disabled=yes
set ssh address=173.20.20.0/24,10.35.0.0/24
set telnet disabled=yes
set www disabled=yes
set winbox address=173.20.20.0/24,10.35.0.0/24,10.3.74.0/24
set api address=10.35.0.1/32
set api-ssl disabled=yes
/ip ssh
set strong-crypto=yes
/routing bfd configuration
add disabled=no interfaces=all min-rx=200ms min-tx=200ms multiplier=5
/system clock
set time-zone-name=America/Sao_Paulo
/system identity
set name=MK-BORDA-ONU
/system scheduler
add comment="failover v5 Motor B" interval=5s name=FO-check on-event=\
    "/system script run FO-tick" policy=read,write,policy,test start-date=\
    2026-09-02 start-time=15:14:07
add comment="colhe MAC-IP dos hosts vivos (memoria de leases)" interval=1d \
    name=FO-harvest-diario on-event="/system script run FO-harvest" policy=\
    read,write,test start-date=2026-09-02 start-time=03:15:00
/tool bandwidth-server
set enabled=no
/tool mac-server
set allowed-interface-list=MGMT
/tool mac-server mac-winbox
set allowed-interface-list=MGMT
/tool mac-server ping
set enabled=no
/tool netwatch
add comment=FO-PMM host=1.1.1.1 interval=10s packet-count=3 src-address=\
    10.3.74.254 thr-loss-percent=99% timeout=1s type=icmp
add comment=FO-PMM host=9.9.9.10 interval=10s packet-count=3 src-address=\
    10.3.74.254 thr-loss-percent=99% timeout=1s type=icmp
add comment=FO-PMM-TCP host=1.1.1.1 interval=10s port=443 src-address=\
    10.3.74.254 timeout=3s type=tcp-conn
add comment=FO-PMM-DNS dns-server=10.135.16.18 host=www.google.com interval=\
    15s type=dns
add comment=AD-vivo dns-server=10.135.16.18 host=pmm.local interval=15s type=\
    dns
add comment=FO-PMM host=8.8.8.8 interval=10s packet-count=3 src-address=\
    10.3.74.254 thr-loss-percent=99% timeout=1s type=icmp
/tool sniffer
set file-limit=2000KiB memory-limit=2000KiB

