# =====================================================================
# ONDA 1 + REDIRECT UNIFI — id 10 · Centro Materno Infantil (CMI)
# MK-BORDA-CMI (hEX RB750Gr3, gestão 10.35.0.33, RouterOS 7.19.6)
# Aplicado remoto em 2026-09-02 · backup no MK: antes-onda1-020926
#
# LAN untagged 10.1.18.0/24 · gw real 10.1.18.1 (HPE 94:3F:C2:E7:B1:73)
# MK na LAN 10.1.18.254 · Connect em ether2 · antena Ubiquiti em ether5
#
# TUDO abaixo foi aplicado com SAFETY-revert armado ANTES (ver §0) e
# cancelado só depois de validado. Nenhum cliente caiu.
# =====================================================================

# --- §0. REDE DE SEGURANÇA (armar ANTES de qualquer coisa) -----------
# Lição do Péricles (02/09): mudança na interface por onde passa a própria
# gestão SEM reversão armada = MK inacessível e telefonia fora.
# O scheduler desfaz tudo sozinho se o operador perder o acesso.
#   /system scheduler add name=SAFETY-MTU interval=3m policy=read,write,test,policy \
#       on-event="/interface ethernet set [find name=ether2] mtu=1500; /system scheduler remove [find name=\"SAFETY-MTU\"]"
#   ... aplica a mudança ...
#   ... ABRE UMA CONEXÃO NOVA para provar que não se trancou ...
#   /system scheduler remove [find name="SAFETY-MTU"]     <- só então cancela

# --- §1. MEDIDO (só leitura, antes de aplicar) -----------------------
#   PMTU da Connect .....: 1480   (1484 falha, 1480 passa; CPE aceita 1500)
#   escopo real do DHCP..: gw 10.1.18.1 · server-id 10.1.201.254 (central, via relay)
#                          DNS 10.135.16.119, 10.135.16.17 · lease 8 h
#   ⇒ o DNS do escopo do MK JÁ ESTAVA CORRETO aqui (≠ Complexo, que tem 5 DNS e lease 24 h)
#   ⇒ `domain` NÃO foi tocado: o sniffer é bloqueado pelo device-mode no hEX,
#      então não há evidência do que a PMM entrega. Só se altera o que se mede.

# --- §2. PMTU e MSS na Connect ---------------------------------------
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn out-interface=ether2 \
    tcp-mss=1441-65535 action=change-mss new-mss=1440 \
    comment="MSS clamp Connect (PMTU 1480 medido 2026-09-02)"
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn in-interface=ether2 \
    tcp-mss=1441-65535 action=change-mss new-mss=1440 comment="MSS clamp Connect in"
/interface ethernet set [find name=ether2] mtu=1480
# validado: conexão NOVA ok, wg-voip e automais-vpn com handshake fresco,
# download de 5 MB pela Connect em 1 s (antes: travava)

# --- §3. lease real ---------------------------------------------------
/ip dhcp-server set [find name="FAILOVER-dhcp"] lease-time=8h

# --- §4. resolvedor do ISP fora --------------------------------------
/ip dhcp-client set [find interface=ether2] use-peer-dns=no

# --- §5. NTP ----------------------------------------------------------
/system ntp client set enabled=yes mode=unicast servers=pool.ntp.br,a.st1.ntp.br
/system clock set time-zone-name=America/Sao_Paulo
# resultado: status=synchronized

# --- §6. lista de serviços essenciais (INERTE) -----------------------
/ip firewall address-list add list=SERVICOS-ESSENCIAIS address=189.28.130.13 comment="SISREG DATASUS"
/ip firewall address-list add list=SERVICOS-ESSENCIAIS address=200.166.238.18 comment="SER"
/ip firewall address-list add list=SERVICOS-ESSENCIAIS address=146.190.65.73 comment="smsmarica.online"

# --- §7. REDIRECT DA ANTENA UBIQUITI (TEMPORÁRIO) --------------------
# Problema: o `inform_url` fica gravado DENTRO da AP (não no controlador) e
# aponta para 10.30.30.23 — o controlador antigo, do escritório, que não existe
# mais. Além disso a antena é BRIDGEADA: o gateway dela é o da Prefeitura, então
# o pacote nem passa pela pilha IP do MK e morre na borda deles (SYN eterno).
#
# Desenho (validado 02/09): a unidade só ROTEIA o IP antigo pelo túnel do
# datacenter; QUEM TRADUZ é o próprio CCR2116 da Eveo, que já tem:
#     dstnat: dst=10.30.30.23 in=wg-unidades tcp/8080 -> 10.90.40.23   (regra 11)
#     dstnat: dst=10.30.30.23 in=wg-unidades udp/3478 -> 10.90.40.23   (regra 12)
#     filter: accept tcp/8080 e udp/3478 para 10.90.40.23              (regras 6 e 7)
#     filter: drop do resto (REDES-PRIVADAS) e do input                (regras 8 e 15)
# ⇒ a unidade NÃO precisa saber o IP do controlador. Se ele mudar, muda só no DC.
# ⚠ ICMP para o DC é bloqueado de propósito — testar com tcp-conn, nunca com ping.

# captura L2 (a antena é bridgeada; sem isto o tráfego não passa pelo MK)
/interface bridge nat add chain=dstnat in-interface=ether5 src-address=10.1.18.125/32     dst-address=10.30.30.23/32 mac-protocol=ip action=redirect comment="UNIFI captura antena"
# rota do destino pelo túnel do datacenter
/ip route add dst-address=10.30.30.23/32 gateway=wg-eveo     comment="UNIFI controlador via tunel EVEO (o DC traduz p/ 10.90.40.23)"
# NAT de saída: o DC só sabe voltar para 10.203.0.20
/ip firewall nat add chain=srcnat dst-address=10.30.30.23 out-interface=wg-eveo     action=masquerade comment="UNIFI nat saida pelo tunel"
#
# VALIDADO 02/09: TCP 8080 e UDP 3478 com flag SEEN-REPLY, ciclo repetindo.

# 🔴 ISTO É TEMPORÁRIO — O LEGADO 10.30.30.23 DEVE SUMIR
# Destino definitivo (decisão do usuário): mudar o inform das APs para
#     unifi.automais.cloud:8080
# Sequência para eliminar o legado SEM perder a antena (a ordem importa):
#   1. antena online no controlador  <- é o que estas regras garantem hoje
#   2. no controlador: Override inform host = unifi.automais.cloud
#      (ele empurra a URL nova para as 11 APs adotadas), ou `set-inform` por SSH na AP
#   3. confirmar que a AP fala com o destino novo
#   4. SÓ ENTÃO remover, nesta ordem:
#        no MK:      /ip firewall nat remove [find comment~"UNIFI"]
#                    /interface bridge nat remove [find comment~"UNIFI"]
#                    /ip route remove [find comment~"UNIFI"]
#        no CCR2116: as regras 11 e 12 ("UNIFI legado")
#   ⚠ Remover 4 antes de 2/3 devolve a antena ao estado órfão.
# ⚠ Pré-requisito do passo 2: a antena resolve DNS por 8.8.8.8 — então
#   `unifi.automais.cloud` precisa ser público e apontar para um IP que ela alcance.
#   Como o tráfego dela sai pela Prefeitura, o nome tem de resolver para um IP
#   público (ou a captura L2 passa a ser pelo nome resolvido, não por 10.30.30.23).
# NOTA: o syslog (udp/5514) NÃO é redirecionado — o DC só libera 8080 e 3478.

# --- O QUE **NÃO** FOI FEITO AQUI ------------------------------------
#  - desligar o DHCP do MK: esta unidade ainda roda o failover v2, onde o DHCP
#    é a única retaguarda na contingência. Só desligar junto com o v5.
#  - failover v5, túnel wg-eveo, remoção do admin, upgrade 7.19.6 -> 7.23.x
#  - VLAN 20 "guest SMS-PACIENTE" (172.20.20.0/24, criada pelo Automais.IO,
#    5 clientes ativos): não documentada em lugar nenhum, mantida como está.
# =====================================================================

# =====================================================================
# ONDA 2 — TÚNEL PARA O DATACENTER AUTOMAIS (aplicado 2026-09-02)
# Padrão §4c da skill: toda saída pela Connect passa por túnel ao DC.
# Convenção de endereço: 10.203.0.<id+10>  ->  CMI id=10  ->  10.203.0.20
# Servidor: CCR2116 Eveo, IP público 177.136.233.75:51833
#           interface `wg-unidades`, servidor em 10.203.0.1/24
#           pubkey 1rA+xJyPWWjaAy3P0avFNNnIhl/XNycsz9FDmNpUHlc=
#           acesso ao CCR2116: 10.30.50.26, usuário `becape`
# =====================================================================
/interface wireguard add name=wg-eveo listen-port=51833 mtu=1420 \
    comment="Relay EVEO CCR2116 (IP publico) - saida internet"
/ip address add address=10.203.0.20/24 interface=wg-eveo comment="tunel eveo id=10"
/interface wireguard peers add interface=wg-eveo \
    public-key="1rA+xJyPWWjaAy3P0avFNNnIhl/XNycsz9FDmNpUHlc=" \
    endpoint-address=177.136.233.75 endpoint-port=51833 \
    allowed-address=0.0.0.0/0 persistent-keepalive=25s comment="EVEO CCR2116 server"
# PIN do endpoint pela física — SEM isto o túnel se engole quando virar default
/ip route add dst-address=177.136.233.75/32 gateway=192.168.0.1 comment="PIN endpoint wg-eveo pela fisica"
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn out-interface=wg-eveo \
    tcp-mss=1381-65535 action=change-mss new-mss=1380 comment="MSS clamp wg-eveo out"
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn in-interface=wg-eveo \
    tcp-mss=1381-65535 action=change-mss new-mss=1380 comment="MSS clamp wg-eveo in"
# a blindagem só liberava 51820 (VOIP) e 13333 (automais-vpn)
/ip firewall filter add chain=input action=accept protocol=udp in-interface=ether2 \
    dst-port=51833 comment="WireGuard eveo" place-before=0

# pubkey gerada pelo MK: LlgVAsasm+G5rkgnaPDkSTiRbHtFs9yfdymsnT6J7iU=
# registrada no servidor (CCR2116 Eveo):
#   /interface wireguard peers add interface=wg-unidades \
#       public-key="LlgVAsasm+G5rkgnaPDkSTiRbHtFs9yfdymsnT6J7iU=" \
#       allowed-address=10.203.0.20/32 comment="id=10 CENTRO MATERNO INFANTIL - CMI"
#
# VALIDADO: handshake 36 s · ping 10.203.0.1 0% perda / 10 ms · DF 1400 B passa.
#
# ⚠ O túnel fica DE PÉ mas NÃO carrega tráfego de cliente ainda: esta unidade
#   roda o failover v2. A rota default pelo túnel entra junto com o v5.
