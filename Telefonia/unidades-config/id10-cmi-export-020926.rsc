# 2026-09-02 22:01:37 by RouterOS 7.19.6
# software id = VTII-XSTE
#
# model = RB750Gr3
# serial number = HKE0ATPM35R
/interface bridge
add admin-mac=04:F4:1C:D5:DF:57 auto-mac=no comment="gestao OOB (ether3)" \
    name=bridge-gestao
add comment="L2 transparente Prefeitura(e1)<->SwitchCore(e4)" name=\
    bridge-transparente protocol-mode=none
/interface ethernet
set [ find default-name=ether1 ] comment="Prefeitura/ONU - PRIMARIO (seguro)"
set [ find default-name=ether2 ] comment=\
    "CONNECT - internet failover - NAO CONFIAVEL" mtu=1480
set [ find default-name=ether3 ] comment=\
    "gestao OOB (futuro: internet reserva)"
set [ find default-name=ether4 ] comment="LAN unidade - uplink switch core"
set [ find default-name=ether5 ] comment="circuito Wi-Fi"
/interface wireguard
add comment="Automais.IO VPN" listen-port=13333 mtu=1420 name=automais-vpn
add comment="Relay EVEO CCR2116 (IP publico) - saida internet" listen-port=\
    51833 mtu=1420 name=wg-eveo
add comment="VOIP hub SMS Marica" listen-port=51820 mtu=1420 name=wg-voip
/interface vlan
add comment="Automais.IO: guest SMS-PACIENTE" interface=bridge-transparente \
    name=vlan20-guest vlan-id=20
/interface list
add comment=defconf name=WAN
add comment=defconf name=LAN
add comment="interfaces de gestao" name=MGMT
add comment="AUTOMAIS.IO NAO APAGAR: lista de interfaces da VPN Automais.IO" \
    name=automais-io-vpn-source
add comment="portas da LAN da unidade" name=LAN-PORTS
/ip pool
add name=pool-failover-lan ranges=10.1.18.10-10.1.18.200
add name=pool-guest ranges=172.20.20.100-172.20.20.240
/ip dhcp-server
add address-pool=pool-failover-lan authoritative=no comment="FAILOVER dhcp" \
    delay-threshold=5s disabled=yes interface=bridge-transparente lease-time=\
    8h name=FAILOVER-dhcp
add address-pool=pool-guest interface=vlan20-guest lease-time=1h name=\
    guest-dhcp
/routing table
add fib name=guest
/disk settings
set auto-media-interface=bridge-gestao auto-media-sharing=yes \
    auto-smb-sharing=yes
/interface bridge nat
add action=redirect chain=dstnat comment=FO-DNS-UDP disabled=yes \
    dst-mac-address=94:3F:C2:E7:B1:73/FF:FF:FF:FF:FF:FF dst-port=53 \
    in-interface-list=LAN-PORTS ip-protocol=udp mac-protocol=ip src-address=\
    10.1.18.0/24
add action=accept chain=dstnat comment=FO-INTERNO-A disabled=yes dst-address=\
    10.0.0.0/8 dst-mac-address=94:3F:C2:E7:B1:73/FF:FF:FF:FF:FF:FF \
    in-interface-list=LAN-PORTS mac-protocol=ip src-address=10.1.18.0/24
add action=accept chain=dstnat comment=FO-INTERNO-B disabled=yes dst-address=\
    172.16.0.0/12 dst-mac-address=94:3F:C2:E7:B1:73/FF:FF:FF:FF:FF:FF \
    in-interface-list=LAN-PORTS mac-protocol=ip src-address=10.1.18.0/24
add action=redirect chain=dstnat comment=FO-CAPTURA disabled=yes \
    dst-mac-address=94:3F:C2:E7:B1:73/FF:FF:FF:FF:FF:FF in-interface-list=\
    LAN-PORTS mac-protocol=ip src-address=10.1.18.0/24
add action=arp-reply arp-dst-address=10.1.18.1/32 arp-opcode=request chain=\
    dstnat comment=FO-ARPREPLY disabled=yes in-interface-list=LAN-PORTS \
    mac-protocol=arp to-arp-reply-mac-address=94:3F:C2:E7:B1:73
add action=accept chain=dstnat comment="SHIM excecao MK" dst-address=\
    10.1.18.254/32 in-interface-list=LAN-PORTS mac-protocol=ip src-address=\
    10.1.18.0/24
add action=accept chain=dstnat comment="SHIM excecao telefones gw" \
    dst-address=10.200.10.1/32 in-interface-list=LAN-PORTS mac-protocol=ip \
    src-address=10.1.18.0/24
add action=dst-nat chain=dstnat comment="SHIM retorno MK-MAC -> HPE" \
    dst-mac-address=04:F4:1C:D5:DF:56/FF:FF:FF:FF:FF:FF in-interface-list=\
    LAN-PORTS mac-protocol=ip src-address=10.1.18.0/24 to-dst-mac-address=\
    94:3F:C2:E7:B1:73
/interface bridge port
add bridge=bridge-gestao comment=defconf interface=ether3
add bridge=bridge-transparente interface=ether1
add bridge=bridge-transparente interface=ether4
add bridge=bridge-transparente comment=\
    "Automais.IO: AP uplink (nativo=LAN, tag20=guest)" interface=ether5
/ip neighbor discovery-settings
set discover-interface-list=MGMT
/interface bridge vlan
add bridge=bridge-transparente untagged=\
    ether1,ether4,ether5,bridge-transparente vlan-ids=1
add bridge=bridge-transparente comment="Automais.IO: guest" tagged=\
    ether5,bridge-transparente vlan-ids=20
/interface list member
add comment=defconf interface=bridge-gestao list=LAN
add comment=defconf interface=ether1 list=WAN
add interface=bridge-gestao list=MGMT
add interface=automais-vpn list=MGMT
add comment="AUTOMAIS.IO NAO APAGAR" interface=automais-vpn list=\
    automais-io-vpn-source
add comment="LAN local - MAC/discovery" interface=ether4 list=MGMT
add interface=ether4 list=LAN-PORTS
add interface=ether5 list=LAN-PORTS
/interface wireguard peers
add allowed-address=10.201.0.0/24 comment="hub asterisk" endpoint-address=\
    192.241.153.121 endpoint-port=51820 interface=wg-voip name=peer1 \
    persistent-keepalive=25s public-key=\
    "b0Da/MUl+tmetIjBN/OKfQqGCKuqtsZNsmx2Glmh9wo="
add allowed-address=10.35.0.0/24,10.30.30.0/24 endpoint-address=automais.io \
    endpoint-port=51825 interface=automais-vpn name=peer2 \
    persistent-keepalive=25s public-key=\
    "b9wFAOFy1KAbumlOox7dpUiWEWFhgAb+vnFvKXFqyFA="
add allowed-address=0.0.0.0/0 comment="EVEO CCR2116 server" endpoint-address=\
    177.136.233.75 endpoint-port=51833 interface=wg-eveo name=peer3 \
    persistent-keepalive=25s public-key=\
    "1rA+xJyPWWjaAy3P0avFNNnIhl/XNycsz9FDmNpUHlc="
/ip address
add address=173.20.20.1/24 comment="gestao OOB" interface=bridge-gestao \
    network=173.20.20.0
add address=10.200.10.1/24 comment="gw telefones id=10" interface=\
    bridge-transparente network=10.200.10.0
add address=10.201.0.20/24 comment="tunel voip id=10" interface=wg-voip \
    network=10.201.0.0
add address=10.35.0.33/24 interface=automais-vpn network=10.35.0.0
add address=10.1.18.254/24 comment="MK LAN id=10" interface=\
    bridge-transparente network=10.1.18.0
add address=172.20.20.1/24 comment="Automais.IO: gw guest" interface=\
    vlan20-guest network=172.20.20.0
add address=10.203.0.20/24 comment="tunel eveo id=10" interface=wg-eveo \
    network=10.203.0.0
/ip arp
add address=10.1.18.253 comment="FO alias next-hop do gw real (nao remover)" \
    interface=bridge-transparente mac-address=94:3F:C2:E7:B1:73
/ip dhcp-client
add comment="Connect - internet do MK" interface=ether2 use-peer-dns=no \
    use-peer-ntp=no
/ip dhcp-server lease
add address=10.1.18.200 comment="harvest 020926" mac-address=\
    00:9E:EE:10:24:BE server=FAILOVER-dhcp
add address=10.1.18.1 comment="harvest 020926" mac-address=94:3F:C2:E7:B1:73 \
    server=FAILOVER-dhcp
add address=10.1.18.199 comment="harvest 020926" mac-address=\
    94:C6:91:C5:8C:B5 server=FAILOVER-dhcp
add address=10.1.18.198 comment="harvest 020926" mac-address=\
    A8:A1:59:81:2C:8F server=FAILOVER-dhcp
add address=10.1.18.155 comment="harvest 020926" mac-address=\
    D0:27:88:C2:02:8F server=FAILOVER-dhcp
add address=10.1.18.197 comment="harvest 020926" mac-address=\
    B0:BE:76:D1:FC:DF server=FAILOVER-dhcp
add address=10.1.18.109 comment="harvest 020926" mac-address=\
    84:69:93:DD:A9:AA server=FAILOVER-dhcp
add address=10.1.18.196 comment="harvest 020926" mac-address=\
    BA:A8:84:9D:A8:F9 server=FAILOVER-dhcp
add address=10.1.18.107 comment="harvest 020926" mac-address=\
    AC:C5:1B:6A:2C:92 server=FAILOVER-dhcp
add address=10.1.18.177 comment="harvest 020926" mac-address=\
    64:1C:67:31:66:49 server=FAILOVER-dhcp
add address=10.1.18.125 comment="harvest 020926" mac-address=\
    24:5A:4C:29:20:0F server=FAILOVER-dhcp
add address=10.1.18.183 comment="harvest 020926" mac-address=\
    2A:3F:DA:2B:77:02 server=FAILOVER-dhcp
add address=10.1.18.152 comment="harvest 020926" mac-address=\
    18:0D:2C:B8:92:E5 server=FAILOVER-dhcp
add address=10.1.18.151 comment="harvest 020926" mac-address=\
    D8:5E:D3:F6:9C:98 server=FAILOVER-dhcp
add address=10.1.18.150 comment="harvest 020926" mac-address=\
    1C:69:7A:E1:ED:E1 server=FAILOVER-dhcp
add address=10.1.18.179 comment="harvest 020926" mac-address=\
    C8:D3:FF:00:18:DB server=FAILOVER-dhcp
add address=10.1.18.148 comment="harvest 020926" mac-address=\
    AA:B5:67:EB:8B:9C server=FAILOVER-dhcp
add address=10.1.18.146 comment="harvest 020926" mac-address=\
    26:DA:AF:1F:0C:BD server=FAILOVER-dhcp
add address=10.1.18.102 comment="harvest 020926" mac-address=\
    00:9E:EE:10:24:90 server=FAILOVER-dhcp
add address=10.1.18.105 comment="harvest 020926" mac-address=\
    64:1C:67:A4:29:DB server=FAILOVER-dhcp
add address=10.1.18.113 comment="harvest 020926" mac-address=\
    74:56:3C:F2:84:1C server=FAILOVER-dhcp
add address=10.1.18.187 comment="harvest 020926" mac-address=\
    1C:69:7A:E1:F1:1F server=FAILOVER-dhcp
add address=10.1.18.168 comment="harvest 020926" mac-address=\
    D0:27:88:5D:9B:FB server=FAILOVER-dhcp
add address=10.1.18.159 comment="harvest 020926" mac-address=\
    4C:72:B9:6B:04:A1 server=FAILOVER-dhcp
add address=10.1.18.126 comment="harvest 020926" mac-address=\
    00:9E:EE:10:26:97 server=FAILOVER-dhcp
add address=10.1.18.186 comment="harvest 020926" mac-address=\
    D0:94:66:B2:56:36 server=FAILOVER-dhcp
add address=10.1.18.100 comment="harvest 020926" mac-address=\
    AC:C5:1B:6B:5B:0B server=FAILOVER-dhcp
add address=10.1.18.98 comment="harvest 020926" mac-address=FE:DA:A3:FA:79:6E \
    server=FAILOVER-dhcp
/ip dhcp-server network
add address=10.1.18.0/24 comment="FAILOVER rede (copia fiel DHCP real)" \
    dns-server=10.135.16.119,10.135.16.17 domain=pmm.local gateway=10.1.18.1
add address=172.20.20.0/24 comment="Automais.IO: guest" dns-server=\
    1.1.1.1,8.8.8.8 gateway=172.20.20.1
/ip dns
set allow-remote-requests=yes servers=\
    10.135.16.119,10.135.16.17,1.1.1.1,8.8.8.8
/ip firewall address-list
add address=189.28.130.13 comment="SISREG DATASUS" list=SERVICOS-ESSENCIAIS
add address=200.166.238.18 comment=SER list=SERVICOS-ESSENCIAIS
add address=146.190.65.73 comment=smsmarica.online list=SERVICOS-ESSENCIAIS
/ip firewall filter
add action=accept chain=input dst-port=5060-5061 protocol=tcp
add action=accept chain=input dst-port=10000-20000 protocol=udp
add action=accept chain=input dst-port=10000-20000 protocol=tcp
add action=accept chain=input dst-port=5060-5061 protocol=udp
add action=accept chain=input comment=\
    "Automais.IO: API/SSH/ICMP origem servidor VPN" in-interface=automais-vpn \
    protocol=icmp src-address=10.35.0.1
add action=accept chain=input comment=\
    "Automais.IO: API/SSH/ICMP origem servidor VPN" dst-port=22 in-interface=\
    automais-vpn protocol=tcp src-address=10.35.0.1
add action=accept chain=input comment=\
    "Automais.IO: API/SSH/ICMP origem servidor VPN" dst-port=8728 \
    in-interface=automais-vpn protocol=tcp src-address=10.35.0.1
add action=accept chain=forward comment="VOIP hub->tel id=10" dst-address=\
    10.200.10.0/24 in-interface=wg-voip
add action=accept chain=forward comment="VOIP tel->hub id=10" out-interface=\
    wg-voip src-address=10.200.10.0/24
add action=drop chain=forward comment="VOIP tel fora do tunel id=10" \
    src-address=10.200.10.0/24
add action=accept chain=input comment="input estado" connection-state=\
    established,related,untracked
add action=drop chain=input comment="input invalid" connection-state=invalid
add action=accept chain=input comment="excecao SERVER VOIP" dst-port=51820 \
    in-interface=ether2 protocol=udp src-address=192.241.153.121
add action=accept chain=input comment="WireGuard eveo" dst-port=51833 \
    in-interface=ether2 protocol=udp
add action=accept chain=input comment="WireGuard VPNs" dst-port=51820,13333 \
    in-interface=ether2 protocol=udp
add action=accept chain=input comment="dhcp-client Connect" dst-port=68 \
    in-interface=ether2 protocol=udp
add action=drop chain=input comment="BLINDAGEM Connect (bloqueia wifi/ISP)" \
    in-interface=ether2
add action=accept chain=forward comment="Automais.IO: guest->portal" \
    dst-address=10.30.30.0/24 src-address=172.20.20.0/24
add action=drop chain=forward comment="Automais.IO: guest iso 10/8" \
    dst-address=10.0.0.0/8 src-address=172.20.20.0/24
add action=drop chain=forward comment="Automais.IO: guest iso 172.16/12" \
    dst-address=172.16.0.0/12 src-address=172.20.20.0/24
add action=drop chain=forward comment="Automais.IO: guest iso 192.168/16" \
    dst-address=192.168.0.0/16 src-address=172.20.20.0/24
/ip firewall mangle
add action=mark-connection chain=prerouting comment=\
    "AUTOMAIS.IO NAO APAGAR: vpn-src-connmark" in-interface-list=\
    automais-io-vpn-source new-connection-mark=automais-io-vpn
add action=mark-connection chain=prerouting comment=\
    "Automais.IO: marca VPN->antena 10.1.18" dst-address=10.1.18.0/24 \
    in-interface=automais-vpn new-connection-mark=automais-to-antena
add action=accept chain=prerouting comment=\
    "Automais.IO: guest->portal usa main (automais-vpn)" dst-address=\
    10.30.30.0/24 src-address=172.20.20.0/24
add action=accept chain=prerouting comment="Automais.IO: guest intra" \
    dst-address=172.20.20.0/24 src-address=172.20.20.0/24
add action=mark-routing chain=prerouting comment=\
    "Automais.IO: guest internet" new-routing-mark=guest passthrough=no \
    src-address=172.20.20.0/24
add action=change-mss chain=forward comment=\
    "MSS clamp Connect (PMTU 1480 medido 2026-09-02)" new-mss=1440 \
    out-interface=ether2 protocol=tcp tcp-flags=syn tcp-mss=1441-65535
add action=change-mss chain=forward comment="MSS clamp Connect in" \
    in-interface=ether2 new-mss=1440 protocol=tcp tcp-flags=syn tcp-mss=\
    1441-65535
add action=change-mss chain=forward comment="MSS clamp wg-eveo out" new-mss=\
    1380 out-interface=wg-eveo protocol=tcp tcp-flags=syn tcp-mss=1381-65535
add action=change-mss chain=forward comment="MSS clamp wg-eveo in" \
    in-interface=wg-eveo new-mss=1380 protocol=tcp tcp-flags=syn tcp-mss=\
    1381-65535
/ip firewall nat
add action=masquerade chain=srcnat comment=\
    "Automais.IO: NAT Marica->10.30.30 (UniFi)" dst-address=10.30.30.0/24 \
    out-interface=automais-vpn
add action=masquerade chain=srcnat comment=\
    "Automais.IO: NAT VPN->antena (retorno on-subnet)" connection-mark=\
    automais-to-antena out-interface=bridge-transparente
add action=masquerade chain=srcnat comment="Automais.IO: guest NAT primario" \
    out-interface=bridge-transparente src-address=172.20.20.0/24
add action=masquerade chain=srcnat comment="Automais.IO: guest NAT failover" \
    out-interface=ether2 src-address=172.20.20.0/24
add action=masquerade chain=srcnat comment=\
    "UNIFI nat saida pelo tunel (destino novo)" dst-address=10.90.40.23 \
    out-interface=wg-eveo
add action=masquerade chain=srcnat comment="FO NAT clientes via EVEO" \
    out-interface=wg-eveo src-address=10.1.18.0/24
add action=redirect chain=dstnat comment="FO dns udp" disabled=yes dst-port=\
    53 protocol=udp src-address=10.1.18.0/24 to-ports=53
/ip route
add comment="Automais.IO: rota Marica->10.30.30 (UniFi)" dst-address=\
    10.30.30.0/24 gateway=automais-vpn
add check-gateway=ping comment="Automais.IO: guest egress primario/.1" \
    dst-address=0.0.0.0/0 gateway=10.1.18.1 routing-table=guest
add comment="PIN endpoint wg-eveo pela fisica" dst-address=177.136.233.75/32 \
    gateway=192.168.0.1
add comment="UNIFI controlador unifi.automais.cloud via tunel EVEO" \
    dst-address=10.90.40.23/32 gateway=wg-eveo
add comment="PMM internas (DNS/AD) pelo gateway real" dst-address=\
    10.135.16.0/24 gateway=10.1.18.1
add comment="FO sonda PMM 1.1.1.1" dst-address=1.1.1.1/32 gateway=10.1.18.253
add comment="FO sonda PMM 9.9.9.10" dst-address=9.9.9.10/32 gateway=\
    10.1.18.253
add comment="FO sonda PMM 8.8.8.8" dst-address=8.8.8.8/32 gateway=10.1.18.253
add comment="FO default via EVEO (contingencia)" disabled=yes distance=1 \
    dst-address=0.0.0.0/0 gateway=10.203.0.1
/ip service
set ftp disabled=yes
set ssh address=173.20.20.0/24,10.35.0.0/24,10.1.18.0/24
set telnet disabled=yes
set www disabled=yes
set winbox address=173.20.20.0/24,10.35.0.0/24,10.1.18.0/24
set api address=10.35.0.1/32
set api-ssl disabled=yes
/ip ssh
set strong-crypto=yes
/ipv6 firewall address-list
add address=::/128 comment="defconf: unspecified address" list=bad_ipv6
add address=::1/128 comment="defconf: lo" list=bad_ipv6
add address=fec0::/10 comment="defconf: site-local" list=bad_ipv6
add address=::ffff:0.0.0.0/96 comment="defconf: ipv4-mapped" list=bad_ipv6
add address=::/96 comment="defconf: ipv4 compat" list=bad_ipv6
add address=100::/64 comment="defconf: discard only " list=bad_ipv6
add address=2001:db8::/32 comment="defconf: documentation" list=bad_ipv6
add address=2001:10::/28 comment="defconf: ORCHID" list=bad_ipv6
add address=3ffe::/16 comment="defconf: 6bone" list=bad_ipv6
/ipv6 firewall filter
add action=accept chain=input comment=\
    "defconf: accept established,related,untracked" connection-state=\
    established,related,untracked
add action=drop chain=input comment="defconf: drop invalid" connection-state=\
    invalid
add action=accept chain=input comment="defconf: accept ICMPv6" protocol=\
    icmpv6
add action=accept chain=input comment="defconf: accept UDP traceroute" \
    dst-port=33434-33534 protocol=udp
add action=accept chain=input comment=\
    "defconf: accept DHCPv6-Client prefix delegation." dst-port=546 protocol=\
    udp src-address=fe80::/10
add action=accept chain=input comment="defconf: accept IKE" dst-port=500,4500 \
    protocol=udp
add action=accept chain=input comment="defconf: accept ipsec AH" protocol=\
    ipsec-ah
add action=accept chain=input comment="defconf: accept ipsec ESP" protocol=\
    ipsec-esp
add action=accept chain=input comment=\
    "defconf: accept all that matches ipsec policy" ipsec-policy=in,ipsec
add action=drop chain=input comment=\
    "defconf: drop everything else not coming from LAN" in-interface-list=\
    !LAN
add action=fasttrack-connection chain=forward comment="defconf: fasttrack6" \
    connection-state=established,related
add action=accept chain=forward comment=\
    "defconf: accept established,related,untracked" connection-state=\
    established,related,untracked
add action=drop chain=forward comment="defconf: drop invalid" \
    connection-state=invalid
add action=drop chain=forward comment=\
    "defconf: drop packets with bad src ipv6" src-address-list=bad_ipv6
add action=drop chain=forward comment=\
    "defconf: drop packets with bad dst ipv6" dst-address-list=bad_ipv6
add action=drop chain=forward comment="defconf: rfc4890 drop hop-limit=1" \
    hop-limit=equal:1 protocol=icmpv6
add action=accept chain=forward comment="defconf: accept ICMPv6" protocol=\
    icmpv6
add action=accept chain=forward comment="defconf: accept HIP" protocol=139
add action=accept chain=forward comment="defconf: accept IKE" dst-port=\
    500,4500 protocol=udp
add action=accept chain=forward comment="defconf: accept ipsec AH" protocol=\
    ipsec-ah
add action=accept chain=forward comment="defconf: accept ipsec ESP" protocol=\
    ipsec-esp
add action=accept chain=forward comment=\
    "defconf: accept all that matches ipsec policy" ipsec-policy=in,ipsec
add action=drop chain=forward comment=\
    "defconf: drop everything else not coming from LAN" in-interface-list=\
    !LAN
/system clock
set time-zone-name=America/Sao_Paulo
/system identity
set name=MK-BORDA-CMI
/system ntp client
set enabled=yes
/system ntp client servers
add address=pool.ntp.br
add address=a.st1.ntp.br
/system scheduler
add comment="failover v5 Motor B" interval=5s name=FO-check on-event=\
    "/system script run FO-tick" policy=read,write,policy,test start-date=\
    2026-09-02 start-time=21:56:31
add comment="colhe MAC-IP dos hosts vivos" interval=1d name=FO-harvest-diario \
    on-event="/system script run FO-harvest" policy=read,write,test \
    start-date=2026-09-02 start-time=03:15:00
/system script
add dont-require-permissions=no name=FO-tick owner=becape policy=\
    read,write,policy,test source="# FO-tick v5 (Motor B) - CMI id=10\r\
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
    \n:do { :set linkUp [/interface get [find name=ether1] running] } on-error\
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
    \n:foreach r in=[/ip firewall nat find comment~\"FO dns udp\"] do={\r\
    \n  :do { :if ([/ip firewall nat get \$r disabled] = \$want) do={ /ip fire\
    wall nat set \$r disabled=(!\$want) } } on-error={ :set err (\$err + 1) }\
    \r\
    \n}\r\
    \n# --- rota default pelo tunel DC: so em contingencia ---\r\
    \n:foreach r in=[/ip route find comment~\"FO default via EVEO\"] do={\r\
    \n  :do { :if ([/ip route get \$r disabled] = \$want) do={ /ip route set \
    \$r disabled=(!\$want) } } on-error={ :set err (\$err + 1) }\r\
    \n}\r\
    \n:foreach r in=[/ip firewall nat find comment~\"FO dns udp\"] do={\r\
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
    \n  :foreach c in=[/ip firewall connection find src-address~\"10.1.18\"] d\
    o={ :do { /ip firewall connection remove \$c } on-error={} }\r\
    \n  :do { /ip dns cache flush } on-error={}\r\
    \n  :set foDown 0; :set foUp 0\r\
    \n  :log warning (\"FO-v5 \" . [:tostr \$want] . \" pmmUp=\" . \$pmmUp . \
    \"/3 link=\" . [:tostr \$linkUp] . \" err=\" . \$err)\r\
    \n}\r\
    \n"
add dont-require-permissions=no name=FO-harvest owner=becape policy=\
    read,write,test source="# FO-harvest - grava o IP atual de cada host como \
    lease estatica\
    \n:local n 0\
    \n:local j 0\
    \n:foreach a in=[/ip arp find interface=bridge-transparente] do={\
    \n  :local ip [/ip arp get \$a address]\
    \n  :local mac [/ip arp get \$a mac-address]\
    \n  :if ([:len \$mac] > 0) do={\
    \n    :if ([:typeof [:find \$ip \"10.1.18.\"]] != \"nil\") do={\
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
    10.1.18.254 thr-loss-percent=99% timeout=1s type=icmp
add comment=FO-PMM host=9.9.9.10 interval=10s packet-count=3 src-address=\
    10.1.18.254 thr-loss-percent=99% timeout=1s type=icmp
add comment=FO-PMM host=8.8.8.8 interval=10s packet-count=3 src-address=\
    10.1.18.254 thr-loss-percent=99% timeout=1s type=icmp
add comment=FO-PMM-TCP host=1.1.1.1 interval=10s port=443 src-address=\
    10.1.18.254 timeout=3s type=tcp-conn
add comment=FO-PMM-DNS dns-server=10.135.16.119 host=www.google.com interval=\
    15s type=dns
