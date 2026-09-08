# =====================================================================
# TUNEL DC + FAILOVER v5 (Motor B) — id 7 · CEO Boqueirao (Odonto)
# MK-BORDA-BOQUEIRAO (hEX RB750Gr3, gestão 10.35.0.30, RouterOS 7.19.6)
# Aplicado remoto em 2026-09-03 · backups no MK: antes-onda1-030926 / v5-ok-030926
#
# LAN untagged 10.1.108.0/24 · gw real 10.1.108.1 (HPE 94:3F:C2:E7:B1:8D)
# MK na LAN 10.1.108.254 · MAC da bridge 04:F4:1C:D8:DC:05
# ether1 = Prefeitura · ether2 = Connect · ether4 = switch core · ether5 = Wi-Fi (fora da bridge)
# Onda 1 aplicada JUNTO em 03/09: PMTU 1480 medido (1484 falha, CPE aceita 1500) + MSS 1440,
# use-peer-dns=no, lease 8h, NTP, 3 essenciais. Escopo da PMM CAPTURADO com dhcp-client
# temporario: gw 10.1.108.1, servidor 10.1.201.254, DNS 10.135.16.119/.17, lease 8h.
# =====================================================================

# --- 1. TUNEL DO DATACENTER AUTOMAIS (padrao da frota, plano 2.5b) ---
# No CCR2116 da Eveo (10.30.50.26):
#   /interface wireguard peers add interface=wg-unidades \
#       public-key="4qb+uuO3DbpET0xZxq1UKPHNHJn/uAPqpqk0xMVOi20=" \
#       allowed-address=10.203.0.17/32 comment="id=7 BOQUEIRAO"
# Faixa: DC=.1 · unidade=.<id+10>  =>  Pericles = 10.203.0.17
/interface wireguard add name=wg-eveo mtu=1420 listen-port=51833 comment="Relay EVEO CCR2116 - Datacenter Automais"
/ip address add address=10.203.0.17/24 interface=wg-eveo comment="tunel DC Automais id=7"
/interface wireguard peers add interface=wg-eveo public-key="1rA+xJyPWWjaAy3P0avFNNnIhl/XNycsz9FDmNpUHlc=" \
    endpoint-address=177.136.233.75 endpoint-port=51833 allowed-address=0.0.0.0/0 persistent-keepalive=25s comment="DC Automais CCR2116"
/ip route add dst-address=177.136.233.75/32 gateway=192.168.0.1 comment="PIN endpoint wg-eveo pela fisica (Connect)"
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn out-interface=wg-eveo tcp-mss=1381-65535 action=change-mss new-mss=1380 comment="MSS clamp tunel DC out"
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn in-interface=wg-eveo tcp-mss=1381-65535 action=change-mss new-mss=1380 comment="MSS clamp tunel DC in"
# validado: handshake 13 s · ping ao DC 14 ms 0% · internet PELO tunel 127 ms 0%

# --- 2. SONDAS FIM-A-FIM PELA PERNA DA PREFEITURA --------------------
# E o coracao do v5: em 02/09 o failover nao entrou porque nenhuma camada
# testava INTERNET — so porta, contador e ping ao gateway.
/interface list add name=LAN-PORTS
/interface list member add interface=ether4 list=LAN-PORTS
/ip arp add address=10.1.108.253 mac-address=94:3F:C2:E7:B1:8D interface=bridge-transparente comment="FO alias next-hop do gw real (nao remover)"
/ip route add dst-address=1.1.1.1/32  gateway=10.1.108.253 comment="FO sonda PMM 1.1.1.1"
/ip route add dst-address=9.9.9.10/32 gateway=10.1.108.253 comment="FO sonda PMM 9.9.9.10"
/ip route add dst-address=8.8.8.8/32  gateway=10.1.108.253 comment="FO sonda PMM 8.8.8.8"
/ip route add dst-address=10.135.16.0/24 gateway=10.1.108.1 comment="PMM internas (DNS/AD) pelo gateway real"
/ip route add dst-address=0.0.0.0/0 gateway=10.203.0.1 distance=1 disabled=yes comment="FO default via EVEO (contingencia)"
/tool netwatch add comment=FO-PMM host=1.1.1.1  type=icmp src-address=10.1.108.254 interval=10s packet-count=3 timeout=1s thr-loss-percent=99% startup-delay=45s
/tool netwatch add comment=FO-PMM host=9.9.9.10 type=icmp src-address=10.1.108.254 interval=10s packet-count=3 timeout=1s thr-loss-percent=99% startup-delay=45s
/tool netwatch add comment=FO-PMM host=8.8.8.8  type=icmp src-address=10.1.108.254 interval=10s packet-count=3 timeout=1s thr-loss-percent=99% startup-delay=45s
/tool netwatch add comment=FO-PMM-TCP host=1.1.1.1 type=tcp-conn port=443 src-address=10.1.108.254 timeout=3s interval=10s startup-delay=45s
/tool netwatch add comment=FO-PMM-DNS host=www.google.com type=dns dns-server=10.135.16.119 interval=15s startup-delay=45s
# startup-delay=45s e OBRIGATORIO: o padrao e 5 MIN, maior que a graca pos-boot
# de 3 min do tick — abriria janela de decisao sem sonda pronta.

# --- 3. DNS do MK (precisa resolver para os clientes em contingencia) ---
/ip dns set allow-remote-requests=yes servers=10.135.16.119,10.135.16.17,1.1.1.1,8.8.8.8 cache-max-ttl=1h

# --- 4. CAPTURA L2 — a ORDEM das regras E o desenho ------------------
# O MK NUNCA assume 10.1.108.1. Ele intercepta os quadros que o cliente ja
# endereca ao MAC do HPE e responde ARP com o MAC DELES => convergencia 0 s.
/interface bridge nat add chain=dstnat comment=FO-DNS-UDP   disabled=yes action=redirect in-interface-list=LAN-PORTS src-address=10.1.108.0/24 dst-mac-address=94:3F:C2:E7:B1:8D/FF:FF:FF:FF:FF:FF mac-protocol=ip ip-protocol=udp dst-port=53
/interface bridge nat add chain=dstnat comment=FO-INTERNO-A disabled=yes action=accept   in-interface-list=LAN-PORTS src-address=10.1.108.0/24 dst-mac-address=94:3F:C2:E7:B1:8D/FF:FF:FF:FF:FF:FF mac-protocol=ip dst-address=10.0.0.0/8
/interface bridge nat add chain=dstnat comment=FO-INTERNO-B disabled=yes action=accept   in-interface-list=LAN-PORTS src-address=10.1.108.0/24 dst-mac-address=94:3F:C2:E7:B1:8D/FF:FF:FF:FF:FF:FF mac-protocol=ip dst-address=172.16.0.0/12
/interface bridge nat add chain=dstnat comment=FO-CAPTURA   disabled=yes action=redirect in-interface-list=LAN-PORTS src-address=10.1.108.0/24 dst-mac-address=94:3F:C2:E7:B1:8D/FF:FF:FF:FF:FF:FF mac-protocol=ip
/interface bridge nat add chain=dstnat comment=FO-ARPREPLY  disabled=yes action=arp-reply in-interface-list=LAN-PORTS mac-protocol=arp arp-opcode=request arp-dst-address=10.1.108.1/32 to-arp-reply-mac-address=94:3F:C2:E7:B1:8D
# as DUAS excecoes TEM de vir antes do SHIM, senao o retorno reescreve
# trafego legitimo para o proprio MK e para o gateway dos telefones
/interface bridge nat add chain=dstnat comment="SHIM excecao MK"            action=accept  in-interface-list=LAN-PORTS src-address=10.1.108.0/24 mac-protocol=ip dst-address=10.1.108.254/32
/interface bridge nat add chain=dstnat comment="SHIM excecao telefones gw"  action=accept  in-interface-list=LAN-PORTS src-address=10.1.108.0/24 mac-protocol=ip dst-address=10.200.7.1/32
/interface bridge nat add chain=dstnat comment="SHIM retorno MK-MAC -> HPE" action=dst-nat in-interface-list=LAN-PORTS src-address=10.1.108.0/24 mac-protocol=ip dst-mac-address=04:F4:1C:D8:DC:05/FF:FF:FF:FF:FF:FF to-dst-mac-address=94:3F:C2:E7:B1:8D
/ip firewall nat add chain=srcnat comment="FO NAT clientes via EVEO" action=masquerade src-address=10.1.108.0/24 out-interface=wg-eveo
/ip firewall nat add chain=dstnat comment="FO dns udp" disabled=yes action=redirect protocol=udp dst-port=53 src-address=10.1.108.0/24 to-ports=53

# --- 5. MAC da bridge FIXADO -----------------------------------------
# auto-mac=yes deriva o MAC da bridge da porta da PREFEITURA (ether1) — a
# porta que cai justamente no evento de failover. O SHIM grava esse MAC.
# Fixar no MESMO valor = mudanca nula hoje, e tira a incerteza de vez.
# Aplicado nas 5 unidades do v5 em 03/09.
/interface bridge set [find name=bridge-transparente] auto-mac=no admin-mac=04:F4:1C:D8:DC:05

# --- 6. MOTOR ---------------------------------------------------------
# scripts via SFTP + [/file get ... contents] (inline pelo SSH o RouterOS
# junta tudo numa linha e quebra :foreach/:local)
/system script add name=FO-tick    policy=read,write,test,policy source=[/file get "fo-tick.rsc" contents]
/system script add name=FO-harvest policy=read,write,test,policy source=[/file get "fo-harvest.rsc" contents]
/system scheduler add name=FO-check interval=5s policy=read,write,test,policy on-event="/system script run FO-tick" comment="failover v5 Motor B"
/system scheduler add name=FO-harvest-diario start-time=03:15:00 interval=1d policy=read,write,test,policy on-event="/system script run FO-harvest" comment="colhe leases estaticas"
# 26 leases estaticas colhidas na 1a execucao

# --- 7. REMOCAO DO v2 -------------------------------------------------
# ordem importa: PARAR o motor antigo antes de mexer nos objetos dele
/system scheduler remove [find name~"FAILOVER"]
/system script remove [find name~"FAILOVER"]
/ip address remove [find comment~"takeover"]
/ip firewall nat remove [find comment~"FAILOVER"]
# o NAT v2 mandava cliente direto pela Connect — proibido pelo padrao do tunel DC

# =====================================================================
# VALIDACAO — ciclo completo forcado em producao, 08:24-08:27 de 03/09
#   20:07:22  FO-v5 true   pmmUp=0/3 link=true err=0
#   20:10:17  FO-v5 false  pmmUp=5/3 link=true err=0
# Metodo: 4 regras `chain=output action=drop` para os alvos das sondas —
# simula "link da PMM VIVO, sem internet", que e o cenario exato de 02/09
# e o ponto cego do v2. SAFETY armado para desfazer sozinho em 6 min.
# Em contingencia: FO-CAPTURA capturou 122 pacotes, 24 conexoes de clientes,
# NAT via EVEO com 5 pacotes => o cliente ATRAVESSA o tunel.
# Volta automatica apos a histerese de 120 s, sem residuo.
# =====================================================================
