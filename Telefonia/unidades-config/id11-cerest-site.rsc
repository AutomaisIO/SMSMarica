# =====================================================================
# FASE SITE - id 11 - CEREST
# MK-BORDA-CEREST (hEX RB750Gr3, MAC etiqueta 04f41cd8db86)
# Aplicado remoto em 2026-08-10 via automais-vpn (10.35.0.34)
# RouterOS subido de 7.19.6 -> 7.23.3 DEPOIS da fase site, com a unidade ja viva (janela
# autorizada pelo usuario; MK fora ~1min). Config sobreviveu integra: script conferido
# byte a byte contra este .rsc, 0 objetos FAILOVER ligados, FAILOVER-dhcp com 0 leases,
# device-mode preservado (scheduler=yes fetch=yes), dois tuneis com handshake em <3min.
# Firmware do routerboard permanece 7.19.6 (so o pacote sobe) - igual ao resto da frota.
# Unico erro no log e o one-off de boot ja conhecido: o scheduler de 5s dispara antes da
# ether1 existir e o script morre com "no such item (/interface/get)". Benigno.
#
# *** LAN TAGGED: a LAN da unidade chega em VLAN 1100. ***
# Terceira unidade do rollout nesse cenario (CDT/1092, CAPS AD/1102, CEREST/1100).
# Todo o L3 do MK na LAN mora na interface `vlan1100-lan` sobre a bridge-transparente -
# inclusive o gateway dos telefones, o DHCP de retaguarda e os redirects de DNS do
# failover. A bridge continua SEM vlan-filtering (protocol-mode=none), entao os frames
# tagged seguem atravessando ether1<->ether4 de forma transparente.
#
# Deteccao do tag (o survey na bridge-transparente falha em silencio):
#   /tool torch interface=ether1 src-address=0.0.0.0/0 duration=12
#   -> coluna VLAN-ID = 1100 em 100% dos fluxos, enderecos 10.1.100.x
#
# Dados L3 levantados no site (dhcp-client TEMP na vlan1100-lan):
#   VLAN da LAN ..........: 1100
#   LAN da unidade .......: 10.1.100.0/24
#   Gateway real .........: 10.1.100.1        (ping 2-5ms, 0% perda)
#   IP do MK na LAN ......: 10.1.100.254      (verificado livre - sem resposta ARP/ICMP)
#   DHCP real (central) ..: 10.1.201.254      (o MESMO da Regulacao/Boqueirao/Pericles/CDT/CAPS)
#   DNS reais ............: 10.135.16.119 , 10.135.16.17
#   Dominio ..............: pmm.local         (padrao PMM)
#   Lease real ...........: 8h
#   Lease de survey ......: 10.1.100.150      (pool real confirmado em uso na faixa .1xx)
#
# ARMADILHA (CAPS AD 31/07): logo apos criar a interface VLAN, a primeira ARP request
# ao gateway falha e o RouterOS CACHEIA o "failed". `/ip arp remove [find
# interface=vlan1100-lan]` antes de concluir qualquer coisa. Aqui o gateway respondeu
# de primeira apos o flush.
#
# NOTA: `/ping 10.1.201.254` nao responde da LAN (servidor DHCP central atras do relay,
# nao alcancavel por ICMP) - esperado, nao impede o takeover, que e ARP/L2 local.
# =====================================================================

# --- 0. Interface VLAN da LAN da unidade -----------------------------
/interface vlan set [find name=vlan1100-lan] comment="LAN unidade id=11 (VLAN 1100)"

# --- 0b. Remover o dhcp-client TEMP do survey ------------------------
/ip dhcp-client remove [find comment="TEMP survey LAN"]

# --- 1. Identidade do MK na LAN da unidade ---------------------------
/ip address add address=10.1.100.254/24 interface=vlan1100-lan comment="MK LAN id=11"

# --- 1b. Gateway dos telefones migra da bridge (untagged) para a VLAN 1100
/ip address remove [find comment="gw telefones id=11"]
/ip address add address=10.200.11.1/24 interface=vlan1100-lan comment="gw telefones id=11"

# --- 2. Regras de FAILOVER (criadas DESABILITADAS; o script liga/desliga)
/ip address add address=10.1.100.1/32 interface=vlan1100-lan comment="FAILOVER gw takeover" disabled=yes
/ip firewall nat add chain=srcnat action=masquerade src-address=10.1.100.0/24 out-interface=ether2 comment="FAILOVER nat clientes connect" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=udp dst-port=53 src-address=10.1.100.0/24 in-interface=vlan1100-lan to-ports=53 comment="FAILOVER dns udp" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=tcp dst-port=53 src-address=10.1.100.0/24 in-interface=vlan1100-lan to-ports=53 comment="FAILOVER dns tcp" disabled=yes

# --- 3. DHCP de RETAGUARDA (v3) - DHCP da unidade e CENTRAL: 10.1.201.254
/ip pool add name=pool-failover-lan ranges=10.1.100.10-10.1.100.200
/ip dhcp-server network add address=10.1.100.0/24 gateway=10.1.100.1 dns-server=10.135.16.119,10.135.16.17 domain=pmm.local comment="FAILOVER rede (copia fiel DHCP real)"
/ip address add address=10.1.201.254/32 interface=vlan1100-lan comment="FAILOVER dhcp takeover" disabled=yes
/ip dhcp-server add name=FAILOVER-dhcp interface=vlan1100-lan address-pool=pool-failover-lan lease-time=10m authoritative=no delay-threshold=5s conflict-detection=yes comment="FAILOVER dhcp" disabled=no

# --- 4. Script de failover: deteccao v2 + DHCP v3 + conntrack v4 (<LAN3>=10.1.100)
# Substitui o v1 (so estado fisico) que veio da bancada de 18/07.
/system script remove [find name="FAILOVER-ether1"]
/system script add name=FAILOVER-ether1 policy=read,write,test source=":global foState; :global foCnt; :global foRx; :if ([:typeof \$foState] = \"nothing\") do={ :set foState \"off\" }; :if ([:typeof \$foCnt] = \"nothing\") do={ :set foCnt 0 }; :local up [/interface get [find name=ether1] running]; :local rx [/interface get [find name=ether1] rx-packet]; :local delta 999999; :if ([:typeof \$foRx] = \"num\") do={ :set delta (\$rx - \$foRx) }; :set foRx \$rx; :if (\$delta < 0) do={ :set delta 999999 }; :local dsrv [/ip dhcp-server find name=\"FAILOVER-dhcp\"]; :if (\$foState = \"off\") do={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 50) do={ :set alive true } else={ :local gwids [/ip address find comment~\"FAILOVER gw takeover\"]; :if ([:len \$gwids] = 0) do={ :set alive true } else={ :local a [/ip address get [:pick \$gwids 0] address]; :local gw [:pick \$a 0 [:find \$a \"/\"]]; :if ([/ping \$gw count=2] > 0) do={ :set alive true } } } }; :if (\$alive = false) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 3) do={ /ip firewall nat enable [find comment~\"FAILOVER\"]; /ip dns set allow-remote-requests=yes; /ip address enable [find comment~\"FAILOVER gw takeover\"]; /ip address enable [find comment~\"FAILOVER dhcp takeover\"]; :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=yes delay-threshold=0s }; /ip firewall connection remove [find src-address~\"10.1.100\"]; :set foState \"on\"; :set foCnt 0; :log warning (\"FAILOVER-ether1 ATIVADO (link=\" . [:tostr \$up] . \" rxDelta=\" . [:tostr \$delta] . \")\") } } else={ :set foCnt 0 } } else={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 2) do={ :set alive true } }; :if (\$alive = true) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 6) do={ :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=no delay-threshold=5s }; /ip address disable [find comment~\"FAILOVER dhcp takeover\"]; /ip address disable [find comment~\"FAILOVER gw takeover\"]; /ip firewall nat disable [find comment~\"FAILOVER\"]; /ip firewall connection remove [find src-address~\"10.1.100\"]; /ip dns set allow-remote-requests=no; :set foState \"off\"; :set foCnt 0; :log warning \"FAILOVER-ether1 DESATIVADO (link up + trafego RX)\" } } else={ :set foCnt 0 } }"

# --- 5. Estado normal do resolvedor ----------------------------------
/ip dns set allow-remote-requests=no

# --- 6. Acesso local (padrao fase site desde 2026-07-19) -------------
/ip service set ssh address=173.20.20.0/24,10.35.0.0/24,10.1.100.0/24
/ip service set winbox address=173.20.20.0/24,10.35.0.0/24,10.1.100.0/24
/interface list member add list=MGMT interface=ether4 comment="LAN local - MAC/discovery"
