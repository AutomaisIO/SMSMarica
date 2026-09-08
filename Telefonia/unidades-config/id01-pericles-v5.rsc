# =====================================================================
# TUNEL DC + FAILOVER v5 (Motor B) — id 1 · Péricles
# MK-BORDA-PERICLES (hEX RB750Gr3, gestão 10.35.0.24, RouterOS 7.23.2)
# Aplicado remoto em 2026-09-03 · backups no MK: antes-v5-030926 / v5-ok-030926
#
# LAN untagged 10.1.19.0/24 · gw real 10.1.19.1 (HPE 94:3F:C2:E7:B1:74)
# MK na LAN 10.1.19.254 · MAC da bridge 04:F4:1C:D8:DE:DC
# ether1 = Prefeitura · ether2 = Connect · ether4 = switch core · ether5 = Wi-Fi (fora da bridge)
# Onda 1 já estava aplicada desde 02/09 (PMTU 1480 + MSS 1440, DNS, NTP, essenciais).
# =====================================================================

# --- 1. TUNEL DO DATACENTER AUTOMAIS (padrao da frota, plano 2.5b) ---
# No CCR2116 da Eveo (10.30.50.26):
#   /interface wireguard peers add interface=wg-unidades \
#       public-key="fD/lT7Pn6XwhTiMpfAKIEXCfujzd63GOWNixw2PXvhg=" \
#       allowed-address=10.203.0.11/32 comment="id=1 PERICLES"
# Faixa: DC=.1 · unidade=.<id+10>  =>  Pericles = 10.203.0.11
/interface wireguard add name=wg-eveo mtu=1420 listen-port=51833 comment="Relay EVEO CCR2116 - Datacenter Automais"
/ip address add address=10.203.0.11/24 interface=wg-eveo comment="tunel DC Automais id=1"
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
/ip arp add address=10.1.19.253 mac-address=94:3F:C2:E7:B1:74 interface=bridge-transparente comment="FO alias next-hop do gw real (nao remover)"
/ip route add dst-address=1.1.1.1/32  gateway=10.1.19.253 comment="FO sonda PMM 1.1.1.1"
/ip route add dst-address=9.9.9.10/32 gateway=10.1.19.253 comment="FO sonda PMM 9.9.9.10"
/ip route add dst-address=8.8.8.8/32  gateway=10.1.19.253 comment="FO sonda PMM 8.8.8.8"
/ip route add dst-address=10.135.16.0/24 gateway=10.1.19.1 comment="PMM internas (DNS/AD) pelo gateway real"
/ip route add dst-address=0.0.0.0/0 gateway=10.203.0.1 distance=1 disabled=yes comment="FO default via EVEO (contingencia)"
/tool netwatch add comment=FO-PMM host=1.1.1.1  type=icmp src-address=10.1.19.254 interval=10s packet-count=3 timeout=1s thr-loss-percent=99% startup-delay=45s
/tool netwatch add comment=FO-PMM host=9.9.9.10 type=icmp src-address=10.1.19.254 interval=10s packet-count=3 timeout=1s thr-loss-percent=99% startup-delay=45s
/tool netwatch add comment=FO-PMM host=8.8.8.8  type=icmp src-address=10.1.19.254 interval=10s packet-count=3 timeout=1s thr-loss-percent=99% startup-delay=45s
/tool netwatch add comment=FO-PMM-TCP host=1.1.1.1 type=tcp-conn port=443 src-address=10.1.19.254 timeout=3s interval=10s startup-delay=45s
/tool netwatch add comment=FO-PMM-DNS host=www.google.com type=dns dns-server=10.135.16.119 interval=15s startup-delay=45s
# startup-delay=45s e OBRIGATORIO: o padrao e 5 MIN, maior que a graca pos-boot
# de 3 min do tick — abriria janela de decisao sem sonda pronta.

# --- 3. DNS do MK (precisa resolver para os clientes em contingencia) ---
/ip dns set allow-remote-requests=yes servers=10.135.16.119,10.135.16.17,1.1.1.1,8.8.8.8 cache-max-ttl=1h

# --- 4. CAPTURA L2 — a ORDEM das regras E o desenho ------------------
# O MK NUNCA assume 10.1.19.1. Ele intercepta os quadros que o cliente ja
# endereca ao MAC do HPE e responde ARP com o MAC DELES => convergencia 0 s.
/interface bridge nat add chain=dstnat comment=FO-DNS-UDP   disabled=yes action=redirect in-interface-list=LAN-PORTS src-address=10.1.19.0/24 dst-mac-address=94:3F:C2:E7:B1:74/FF:FF:FF:FF:FF:FF mac-protocol=ip ip-protocol=udp dst-port=53
/interface bridge nat add chain=dstnat comment=FO-INTERNO-A disabled=yes action=accept   in-interface-list=LAN-PORTS src-address=10.1.19.0/24 dst-mac-address=94:3F:C2:E7:B1:74/FF:FF:FF:FF:FF:FF mac-protocol=ip dst-address=10.0.0.0/8
/interface bridge nat add chain=dstnat comment=FO-INTERNO-B disabled=yes action=accept   in-interface-list=LAN-PORTS src-address=10.1.19.0/24 dst-mac-address=94:3F:C2:E7:B1:74/FF:FF:FF:FF:FF:FF mac-protocol=ip dst-address=172.16.0.0/12
/interface bridge nat add chain=dstnat comment=FO-CAPTURA   disabled=yes action=redirect in-interface-list=LAN-PORTS src-address=10.1.19.0/24 dst-mac-address=94:3F:C2:E7:B1:74/FF:FF:FF:FF:FF:FF mac-protocol=ip
/interface bridge nat add chain=dstnat comment=FO-ARPREPLY  disabled=yes action=arp-reply in-interface-list=LAN-PORTS mac-protocol=arp arp-opcode=request arp-dst-address=10.1.19.1/32 to-arp-reply-mac-address=94:3F:C2:E7:B1:74
# as DUAS excecoes TEM de vir antes do SHIM, senao o retorno reescreve
# trafego legitimo para o proprio MK e para o gateway dos telefones
/interface bridge nat add chain=dstnat comment="SHIM excecao MK"            action=accept  in-interface-list=LAN-PORTS src-address=10.1.19.0/24 mac-protocol=ip dst-address=10.1.19.254/32
/interface bridge nat add chain=dstnat comment="SHIM excecao telefones gw"  action=accept  in-interface-list=LAN-PORTS src-address=10.1.19.0/24 mac-protocol=ip dst-address=10.200.1.1/32
/interface bridge nat add chain=dstnat comment="SHIM retorno MK-MAC -> HPE" action=dst-nat in-interface-list=LAN-PORTS src-address=10.1.19.0/24 mac-protocol=ip dst-mac-address=04:F4:1C:D8:DE:DC/FF:FF:FF:FF:FF:FF to-dst-mac-address=94:3F:C2:E7:B1:74
/ip firewall nat add chain=srcnat comment="FO NAT clientes via EVEO" action=masquerade src-address=10.1.19.0/24 out-interface=wg-eveo
/ip firewall nat add chain=dstnat comment="FO dns udp" disabled=yes action=redirect protocol=udp dst-port=53 src-address=10.1.19.0/24 to-ports=53

# --- 5. MAC da bridge FIXADO -----------------------------------------
# auto-mac=yes deriva o MAC da bridge da porta da PREFEITURA (ether1) — a
# porta que cai justamente no evento de failover. O SHIM grava esse MAC.
# Fixar no MESMO valor = mudanca nula hoje, e tira a incerteza de vez.
# Aplicado tambem no Complexo (48:8F:5A:8E:45:E4) e no CMI (04:F4:1C:D5:DF:56) em 03/09.
/interface bridge set [find name=bridge-transparente] auto-mac=no admin-mac=04:F4:1C:D8:DE:DC

# --- 6. MOTOR ---------------------------------------------------------
# scripts via SFTP + [/file get ... contents] (inline pelo SSH o RouterOS
# junta tudo numa linha e quebra :foreach/:local)
/system script add name=FO-tick    policy=read,write,test,policy source=[/file get "fo-tick.rsc" contents]
/system script add name=FO-harvest policy=read,write,test,policy source=[/file get "fo-harvest.rsc" contents]
/system scheduler add name=FO-check interval=5s policy=read,write,test,policy on-event="/system script run FO-tick" comment="failover v5 Motor B"
/system scheduler add name=FO-harvest-diario start-time=03:15:00 interval=1d policy=read,write,test,policy on-event="/system script run FO-harvest" comment="colhe leases estaticas"
# 37 leases estaticas colhidas na 1a execucao

# --- 7. REMOCAO DO v2 -------------------------------------------------
# ordem importa: PARAR o motor antigo antes de mexer nos objetos dele
/system scheduler remove [find name~"FAILOVER"]
/system script remove [find name~"FAILOVER"]
/ip address remove [find comment~"takeover"]
/ip firewall nat remove [find comment~"FAILOVER"]
# o NAT v2 mandava cliente direto pela Connect — proibido pelo padrao do tunel DC

# =====================================================================
# VALIDACAO — ciclo completo forcado em producao, 08:24-08:27 de 03/09
#   08:24:34  FO-v5 true   pmmUp=0/3 link=true err=0
#   08:27:46  FO-v5 false  pmmUp=5/3 link=true err=0
# Metodo: 4 regras `chain=output action=drop` para os alvos das sondas —
# simula "link da PMM VIVO, sem internet", que e o cenario exato de 02/09
# e o ponto cego do v2. SAFETY armado para desfazer sozinho em 6 min.
# Em contingencia: FO-CAPTURA capturou 3.470 pacotes/685 KB, 427 conexoes
# de clientes, NAT via EVEO com 191 pacotes => o cliente ATRAVESSA o tunel.
# Volta automatica apos a histerese de 120 s, sem residuo.
# =====================================================================

# =====================================================================
# CORRECOES DE 08/09/2026 — lacuna do projeto-base F3 + regressao 7.23.2
# Contexto completo: docs/incidente-pericles-080926.md
# Backup no MK antes: antes-lacuna-f3-080926
#
# ether1 (Prefeitura) sem link desde 06/09 05:42 -> 54h em contingencia.
# A contingencia estava incompleta e a unidade ficou "sem internet".
# =====================================================================

# --- A. PIN dos endpoints pela fisica (gestao e VOIP nao entram no wg-eveo) ---
/ip route add dst-address=198.211.104.55/32 gateway=192.168.0.1 comment="PIN endpoint gestao automais-vpn pela fisica (Connect)"
/ip route add dst-address=192.241.153.121/32 gateway=192.168.0.1 comment="PIN endpoint voip wg-voip pela fisica (Connect)"

# --- B. DNS: forwarders que NAO sao alvo de sonda -----------------------
# 1.1.1.1 e 8.8.8.8 eram sonda E forwarder ao mesmo tempo: as rotas /32 de
# sonda os prendem em 10.1.19.253 (perna da PMM). Com a perna morta, o
# resolver ficou com ZERO servidores alcancaveis -> "dns server failure"
# em TODO cliente, porque o MK sequestra o :53 de todos eles.
# projeto-base-F3.md:557 ja avisava: "8.8.8.8 e 9.9.9.9 NAO sao alvos".
/ip dns set servers=1.0.0.1,8.8.4.4,9.9.9.9

# --- C. Zona AD sempre pelos DCs (F3:571-574) ---------------------------
/ip dns forwarders add name=pmm-dc dns-servers=10.135.16.119,10.135.16.17 comment="v5 DCs pmm.local (rota pela PMM)"
/ip dns static add type=FWD name=pmm.local match-subdomain=yes forward-to=pmm-dc comment="v5 zona AD: nunca NXDOMAIN publico"
/ip dns static add type=FWD name=19.1.10.in-addr.arpa match-subdomain=yes forward-to=pmm-dc comment="v5 reverso LAN Pericles"
/ip dns static add type=FWD name=16.135.10.in-addr.arpa match-subdomain=yes forward-to=pmm-dc comment="v5 reverso DCs"

# --- D. FIM DO ECMP na default (F3:530 e :692) --------------------------
# A rota FO via EVEO (distance=1) e a default do dhcp-client da Connect
# (distance=1) formavam ECMP. So existe masquerade out-interface=wg-eveo,
# entao o ramo da ether2 saia SEM NAT com origem 10.1.19.x e morria.
# Medido: 2 de 13 conexoes TCP NATeadas antes; 511 de 522 depois.
/ip dhcp-client set [find interface=ether2] default-route-distance=2

# --- E. Captura de DNS tambem em TCP (F3:589-590 / :638-639) ------------
# TEM de vir antes de FO-INTERNO-A/B, senao :53 para 10.135.16.x cai no
# accept de 10.0.0.0/8 e e bridgeado para o roteador morto.
/interface bridge nat add chain=dstnat comment=FO-DNS-TCP action=redirect in-interface-list=LAN-PORTS src-address=10.1.19.0/24 dst-mac-address=94:3F:C2:E7:B1:74/FF:FF:FF:FF:FF:FF mac-protocol=ip ip-protocol=tcp dst-port=53 place-before=1
/ip firewall nat add chain=dstnat comment="FO dns tcp" action=redirect protocol=tcp dst-port=53 src-address=10.1.19.0/24 to-ports=53

# --- F. Escopo do DHCP: DNS primario .18 que faltava + fallback publico --
# ARMADILHA: com o CIDR SEM aspas o find nao casa e o set falha EM SILENCIO
# (retorna sem erro e nada muda). Usar aspas, ou o indice, e conferir com print.
/ip dhcp-server network set [find address="10.1.19.0/24"] dns-server=10.135.16.18,10.135.16.119,10.135.16.17,1.0.0.1,8.8.4.4

# --- G. PENDENTE: publicar o FO-tick corrigido --------------------------
# Fonte ja corrigida em docs/failover-v5/fo-tick-pericles.rsc:
#   (1) removido o bloco authoritative/delay-threshold — essas propriedades
#       NAO existem em /ip dhcp-server no RouterOS 7.23.2, o get devolve
#       vazio, a comparacao nunca fecha e o tick reescrevia a config do
#       DHCP a CADA 5s: 909 de 1000 linhas do log eram isso (log do MK
#       reduzido a 4 min) e 1,23 setor/s de escrita em flash.
#   (2) regex do redirect DNS de "FO dns udp" para "FO dns", para gerenciar
#       tambem a regra TCP criada no item E. Bloco duplicado removido.
# Publicar com:
#   sftp fo-tick-pericles.rsc -> fo-tick.rsc
#   /system script set [find name="FO-tick"] source=[/file get "fo-tick.rsc" contents]
#   /system script run FO-tick   (validar antes de deixar o scheduler assumir)
