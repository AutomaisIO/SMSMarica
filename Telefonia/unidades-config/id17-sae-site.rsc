# =====================================================================
# FASE SITE - id 17 - SAE (Atendimento Especializado)
# MK-BORDA-SAE (hEX RB750Gr3, MAC etiqueta 04f41cd8d6ca)
# Aplicado remoto em 2026-08-10 via automais-vpn (10.35.0.40)
# RouterOS subido de 7.19.6 -> 7.23.3 DEPOIS da fase site, com a unidade ja viva (janela
# autorizada pelo usuario; MK fora ~2min). Config sobreviveu integra: script conferido
# byte a byte contra este .rsc, 0 objetos FAILOVER ligados, FAILOVER-dhcp com 0 leases,
# device-mode preservado (scheduler=yes fetch=yes), dois tuneis com handshake em <3min.
# Firmware do routerboard permanece 7.19.6 (so o pacote sobe) - igual ao resto da frota.
# Unico erro no log e o one-off de boot ja conhecido (scheduler antes da ether1 existir).
#
# *** LAN TAGGED: a LAN da unidade chega em VLAN 1512. ***
# Quarta unidade do rollout nesse cenario (CDT/1092, CAPS AD/1102, CEREST/1100, SAE/1512).
# Todo o L3 do MK na LAN mora na interface `vlan1512-lan` sobre a bridge-transparente -
# inclusive o gateway dos telefones, o DHCP de retaguarda e os redirects de DNS do
# failover. A bridge continua SEM vlan-filtering (protocol-mode=none).
#
# Deteccao do tag:
#   /tool torch interface=ether1 src-address=0.0.0.0/0 duration=20
#   -> coluna VLAN-ID = 1512 (unica VLAN vista), enderecos 10.1.162.x,
#      DNS para 10.135.16.119 e OSPF originado em 10.1.162.1 (o gateway).
#
# Dados L3 levantados no site (dhcp-client TEMP na vlan1512-lan):
#   VLAN da LAN ..........: 1512
#   LAN da unidade .......: 10.1.162.0/24
#   Gateway real .........: 10.1.162.1        (ping 1-3ms, 0% perda)
#   IP do MK na LAN ......: 10.1.162.254      (verificado livre - sem resposta ARP/ICMP)
#   DHCP real (central) ..: 10.1.201.254      (o MESMO das demais unidades)
#   DNS reais ............: 10.135.16.119 , 10.135.16.17
#   Dominio ..............: pmm.local         (padrao PMM)
#   Lease real ...........: 8h
#   Lease de survey ......: 10.1.162.62
#
# NOTA sobre o survey: o dhcp-client na VLAN 1512 levou ~50s para sair de
# "searching..."/"requesting..." e fechar em "bound" - bem mais lento que nas demais
# unidades (CEREST fechou em <35s). Coerente com a lentidao do relay da Prefeitura ja
# documentada no Boqueirao. NAO concluir "nao tem DHCP nessa VLAN" antes de ~1 minuto.
#
# NOTA: o torch tambem mostrou trafego SSDP/DO (portas 1900 e 7680) envolvendo
# 192.168.0.x na ether1. E a rede da Connect vazando broadcast; sem impacto no L3 da
# unidade, que e integralmente 10.1.162.0/24 tagged 1512.
# =====================================================================

# --- 0. Interface VLAN da LAN da unidade -----------------------------
/interface vlan set [find name=vlan1512-lan] comment="LAN unidade id=17 (VLAN 1512)"

# --- 0b. Remover o dhcp-client TEMP do survey ------------------------
/ip dhcp-client remove [find comment="TEMP survey LAN"]

# --- 1. Identidade do MK na LAN da unidade ---------------------------
/ip address add address=10.1.162.254/24 interface=vlan1512-lan comment="MK LAN id=17"

# --- 1b. Gateway dos telefones migra da bridge (untagged) para a VLAN 1512
/ip address remove [find comment="gw telefones id=17"]
/ip address add address=10.200.17.1/24 interface=vlan1512-lan comment="gw telefones id=17"

# --- 2. Regras de FAILOVER (criadas DESABILITADAS; o script liga/desliga)
/ip address add address=10.1.162.1/32 interface=vlan1512-lan comment="FAILOVER gw takeover" disabled=yes
/ip firewall nat add chain=srcnat action=masquerade src-address=10.1.162.0/24 out-interface=ether2 comment="FAILOVER nat clientes connect" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=udp dst-port=53 src-address=10.1.162.0/24 in-interface=vlan1512-lan to-ports=53 comment="FAILOVER dns udp" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=tcp dst-port=53 src-address=10.1.162.0/24 in-interface=vlan1512-lan to-ports=53 comment="FAILOVER dns tcp" disabled=yes

# --- 3. DHCP de RETAGUARDA (v3) - DHCP da unidade e CENTRAL: 10.1.201.254
/ip pool add name=pool-failover-lan ranges=10.1.162.10-10.1.162.200
/ip dhcp-server network add address=10.1.162.0/24 gateway=10.1.162.1 dns-server=10.135.16.119,10.135.16.17 domain=pmm.local comment="FAILOVER rede (copia fiel DHCP real)"
/ip address add address=10.1.201.254/32 interface=vlan1512-lan comment="FAILOVER dhcp takeover" disabled=yes
/ip dhcp-server add name=FAILOVER-dhcp interface=vlan1512-lan address-pool=pool-failover-lan lease-time=10m authoritative=no delay-threshold=5s conflict-detection=yes comment="FAILOVER dhcp" disabled=no

# --- 4. Script de failover: deteccao v2 + DHCP v3 + conntrack v4 (<LAN3>=10.1.162)
# Substitui o v1 (so estado fisico) que veio da bancada de 18/07.
/system script remove [find name="FAILOVER-ether1"]
/system script add name=FAILOVER-ether1 policy=read,write,test source=":global foState; :global foCnt; :global foRx; :if ([:typeof \$foState] = \"nothing\") do={ :set foState \"off\" }; :if ([:typeof \$foCnt] = \"nothing\") do={ :set foCnt 0 }; :local up [/interface get [find name=ether1] running]; :local rx [/interface get [find name=ether1] rx-packet]; :local delta 999999; :if ([:typeof \$foRx] = \"num\") do={ :set delta (\$rx - \$foRx) }; :set foRx \$rx; :if (\$delta < 0) do={ :set delta 999999 }; :local dsrv [/ip dhcp-server find name=\"FAILOVER-dhcp\"]; :if (\$foState = \"off\") do={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 50) do={ :set alive true } else={ :local gwids [/ip address find comment~\"FAILOVER gw takeover\"]; :if ([:len \$gwids] = 0) do={ :set alive true } else={ :local a [/ip address get [:pick \$gwids 0] address]; :local gw [:pick \$a 0 [:find \$a \"/\"]]; :if ([/ping \$gw count=2] > 0) do={ :set alive true } } } }; :if (\$alive = false) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 3) do={ /ip firewall nat enable [find comment~\"FAILOVER\"]; /ip dns set allow-remote-requests=yes; /ip address enable [find comment~\"FAILOVER gw takeover\"]; /ip address enable [find comment~\"FAILOVER dhcp takeover\"]; :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=yes delay-threshold=0s }; /ip firewall connection remove [find src-address~\"10.1.162\"]; :set foState \"on\"; :set foCnt 0; :log warning (\"FAILOVER-ether1 ATIVADO (link=\" . [:tostr \$up] . \" rxDelta=\" . [:tostr \$delta] . \")\") } } else={ :set foCnt 0 } } else={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 2) do={ :set alive true } }; :if (\$alive = true) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 6) do={ :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=no delay-threshold=5s }; /ip address disable [find comment~\"FAILOVER dhcp takeover\"]; /ip address disable [find comment~\"FAILOVER gw takeover\"]; /ip firewall nat disable [find comment~\"FAILOVER\"]; /ip firewall connection remove [find src-address~\"10.1.162\"]; /ip dns set allow-remote-requests=no; :set foState \"off\"; :set foCnt 0; :log warning \"FAILOVER-ether1 DESATIVADO (link up + trafego RX)\" } } else={ :set foCnt 0 } }"

# --- 5. Estado normal do resolvedor ----------------------------------
/ip dns set allow-remote-requests=no

# --- 6. Acesso local (padrao fase site desde 2026-07-19) -------------
/ip service set ssh address=173.20.20.0/24,10.35.0.0/24,10.1.162.0/24
/ip service set winbox address=173.20.20.0/24,10.35.0.0/24,10.1.162.0/24
/interface list member add list=MGMT interface=ether4 comment="LAN local - MAC/discovery"
