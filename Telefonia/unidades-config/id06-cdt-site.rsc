# =====================================================================
# FASE SITE - id 6 - Centro de Diagnostico e Tratamento (CDT)
# MK-BORDA-CDT (hEX RB750Gr3, MAC etiqueta 04f41cd5775a, serial HKE0APF04FM)
# Aplicado remoto em 2026-07-30 via automais-vpn (10.35.0.29)
# RouterOS subido de 7.19.6 -> 7.23.2 antes da fase site (janela com ether1/ether4 dark)
#
# *** EXCECAO AO PADRAO: a LAN da unidade chega TAGGED em VLAN 1092. ***
# Nas unidades anteriores (Regulacao, Pericles, Boqueirao, CMI) o trafego da LAN era
# untagged e o L3 do MK morava direto na bridge-transparente. No CDT, ~99% dos frames
# em ether1/ether4 vem com tag 1092 (o untagged residual e a VLAN de gerencia da PMM:
# SNMP de 10.135.16.x para switches em 172.20.1.x). Por isso TODO o L3 do MK na LAN
# mora na interface `vlan1092-lan` sobre a bridge-transparente - inclusive o gateway
# dos telefones, o DHCP de retaguarda e os redirects de DNS do failover.
# A bridge continua SEM vlan-filtering (protocol-mode=none), entao os frames tagged
# seguem atravessando ether1<->ether4 de forma transparente, exatamente como antes.
# Sintoma que denuncia isso: o dhcp-client TEMP do survey na bridge-transparente fica
# eternamente em `status=searching...`; na vlan1092-lan pega lease em segundos.
#
# Dados L3 levantados no site (dhcp-client TEMP na vlan1092-lan):
#   VLAN da LAN ..........: 1092
#   LAN da unidade .......: 10.1.92.0/24
#   Gateway real .........: 10.1.92.1         (ping 2ms)
#   IP do MK na LAN ......: 10.1.92.254       (verificado livre - sem resposta ARP/ICMP)
#   DHCP real (central) ..: 10.1.201.254      (o MESMO da Regulacao/Boqueirao/Pericles)
#   DNS reais ............: 10.135.16.119 , 10.135.16.17
#   Dominio ..............: pmm.local         (padrao PMM)
#   Lease real ...........: 8h
# =====================================================================

# --- 0. Interface VLAN da LAN da unidade -----------------------------
/interface vlan add name=vlan1092-lan vlan-id=1092 interface=bridge-transparente comment="LAN unidade id=6 (VLAN 1092)"

# --- 1. Identidade do MK na LAN da unidade ---------------------------
/ip address add address=10.1.92.254/24 interface=vlan1092-lan comment="MK LAN id=6"

# --- 1b. Gateway dos telefones migra da bridge (untagged) para a VLAN 1092
# Os telefones plugam em portas de acesso do switch core, que entrega tagged 1092 no MK.
# Se algum dia um telefone ficar em porta untagged/voice-vlan propria, reavaliar aqui.
/ip address remove [find comment="gw telefones id=6"]
/ip address add address=10.200.6.1/24 interface=vlan1092-lan comment="gw telefones id=6"

# --- 2. Regras de FAILOVER (criadas DESABILITADAS; o script liga/desliga)
/ip address add address=10.1.92.1/32 interface=vlan1092-lan comment="FAILOVER gw takeover" disabled=yes
/ip firewall nat add chain=srcnat action=masquerade src-address=10.1.92.0/24 out-interface=ether2 comment="FAILOVER nat clientes connect" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=udp dst-port=53 src-address=10.1.92.0/24 in-interface=vlan1092-lan to-ports=53 comment="FAILOVER dns udp" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=tcp dst-port=53 src-address=10.1.92.0/24 in-interface=vlan1092-lan to-ports=53 comment="FAILOVER dns tcp" disabled=yes

# --- 3. DHCP de RETAGUARDA (v3) - DHCP da unidade e CENTRAL: 10.1.201.254
# Fica LIGADO 24/7 com delay-threshold=5s + authoritative=no: so responde se o DHCP da
# Prefeitura nao respondeu. No failover o script inverte para authoritative=yes + delay=0s.
# DNS entregue e SEMPRE o real; quem troca na queda e o redirect :53. O takeover do IP do
# servidor DHCP real continua so no failover.
/ip pool add name=pool-failover-lan ranges=10.1.92.10-10.1.92.200
/ip dhcp-server network add address=10.1.92.0/24 gateway=10.1.92.1 dns-server=10.135.16.119,10.135.16.17 domain=pmm.local comment="FAILOVER rede (copia fiel DHCP real)"
/ip address add address=10.1.201.254/32 interface=vlan1092-lan comment="FAILOVER dhcp takeover" disabled=yes
/ip dhcp-server add name=FAILOVER-dhcp interface=vlan1092-lan address-pool=pool-failover-lan lease-time=10m authoritative=no delay-threshold=5s conflict-detection=yes comment="FAILOVER dhcp" disabled=no

# --- 4. Script de failover: deteccao v2 em camadas + DHCP v3 + conntrack v4 (<LAN3>=10.1.92)
# Substitui o v1 (so estado fisico) que veio da bancada de 18/07.
/system script remove [find name="FAILOVER-ether1"]
/system script add name=FAILOVER-ether1 policy=read,write,test source=":global foState; :global foCnt; :global foRx; :if ([:typeof \$foState] = \"nothing\") do={ :set foState \"off\" }; :if ([:typeof \$foCnt] = \"nothing\") do={ :set foCnt 0 }; :local up [/interface get [find name=ether1] running]; :local rx [/interface get [find name=ether1] rx-packet]; :local delta 999999; :if ([:typeof \$foRx] = \"num\") do={ :set delta (\$rx - \$foRx) }; :set foRx \$rx; :if (\$delta < 0) do={ :set delta 999999 }; :local dsrv [/ip dhcp-server find name=\"FAILOVER-dhcp\"]; :if (\$foState = \"off\") do={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 50) do={ :set alive true } else={ :local gwids [/ip address find comment~\"FAILOVER gw takeover\"]; :if ([:len \$gwids] = 0) do={ :set alive true } else={ :local a [/ip address get [:pick \$gwids 0] address]; :local gw [:pick \$a 0 [:find \$a \"/\"]]; :if ([/ping \$gw count=2] > 0) do={ :set alive true } } } }; :if (\$alive = false) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 3) do={ /ip firewall nat enable [find comment~\"FAILOVER\"]; /ip dns set allow-remote-requests=yes; /ip address enable [find comment~\"FAILOVER gw takeover\"]; /ip address enable [find comment~\"FAILOVER dhcp takeover\"]; :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=yes delay-threshold=0s }; /ip firewall connection remove [find src-address~\"10.1.92\"]; :set foState \"on\"; :set foCnt 0; :log warning (\"FAILOVER-ether1 ATIVADO (link=\" . [:tostr \$up] . \" rxDelta=\" . [:tostr \$delta] . \")\") } } else={ :set foCnt 0 } } else={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 2) do={ :set alive true } }; :if (\$alive = true) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 6) do={ :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=no delay-threshold=5s }; /ip address disable [find comment~\"FAILOVER dhcp takeover\"]; /ip address disable [find comment~\"FAILOVER gw takeover\"]; /ip firewall nat disable [find comment~\"FAILOVER\"]; /ip firewall connection remove [find src-address~\"10.1.92\"]; /ip dns set allow-remote-requests=no; :set foState \"off\"; :set foCnt 0; :log warning \"FAILOVER-ether1 DESATIVADO (link up + trafego RX)\" } } else={ :set foCnt 0 } }"

# --- 5. Estado normal do resolvedor ----------------------------------
# (o MK ficou com allow-remote-requests=yes de heranca da bancada; no estado normal e "no")
/ip dns set allow-remote-requests=no

# --- 6. Acesso local (padrao fase site desde 2026-07-19) -------------
/ip service set ssh address=173.20.20.0/24,10.35.0.0/24,10.1.92.0/24
/ip service set winbox address=173.20.20.0/24,10.35.0.0/24,10.1.92.0/24
/interface list member add list=MGMT interface=ether4 comment="LAN local - MAC/discovery"
