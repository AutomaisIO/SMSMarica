# =====================================================================
# FASE SITE - id 1 - Ambulatorio Pericles Siqueira Pereira
# MK-BORDA-AMB-PERICLES (hEX RB750Gr3, MAC etiqueta 04f41cd8dedc)
# Aplicado remoto em 2026-07-29 via automais-vpn (10.35.0.24)
#
# NIVELADO EM 2026-07-31 (delta em `id01-amb-pericles-nivelamento.rsc`). O MK ficou
# inalcancavel entre 29/07 e 31/07 e foi o unico da frota que nao recebeu a atualizacao
# de 29/07 por completo: estava com `delay-threshold=3s` (o valor antigo) e SEM o
# conntrack v4. Este arquivo ja reflete o estado final (5s + conntrack), entao reimportar
# ele inteiro em um MK zerado produz o alvo correto.
#
# Dados L3 levantados no site (dhcp-client TEMP na bridge-transparente):
#   LAN da unidade .......: 10.1.19.0/24
#   Gateway real .........: 10.1.19.1         (ping ~1,8ms)
#   IP do MK na LAN ......: 10.1.19.254       (verificado livre)
#   DHCP real (central) ..: 10.1.201.254      (mesmo servidor da Regulacao/Boqueirao)
#   DNS reais ............: 10.135.16.119 , 10.135.16.17
#   Dominio ..............: pmm.local         (padrao PMM, mesmo servidor DHCP)
# =====================================================================

# --- 1. Identidade do MK na LAN da unidade ---------------------------
/ip address add address=10.1.19.254/24 interface=bridge-transparente comment="MK LAN id=1"

# --- 2. Regras de FAILOVER (criadas DESABILITADAS; o script liga/desliga)
/ip address add address=10.1.19.1/32 interface=bridge-transparente comment="FAILOVER gw takeover" disabled=yes
/ip firewall nat add chain=srcnat action=masquerade src-address=10.1.19.0/24 out-interface=ether2 comment="FAILOVER nat clientes connect" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=udp dst-port=53 src-address=10.1.19.0/24 in-interface=bridge-transparente to-ports=53 comment="FAILOVER dns udp" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=tcp dst-port=53 src-address=10.1.19.0/24 in-interface=bridge-transparente to-ports=53 comment="FAILOVER dns tcp" disabled=yes

# --- 3. DHCP de RETAGUARDA (v3, 2026-07-29) - DHCP da unidade e CENTRAL: 10.1.201.254
# Fica LIGADO 24/7 com delay-threshold=5s + authoritative=no: so responde se o DHCP da
# Prefeitura nao respondeu. No failover o script inverte para authoritative=yes + delay=0s.
# DNS entregue e SEMPRE o real; quem troca na queda e o redirect :53. O takeover do IP do
# servidor DHCP real continua so no failover.
/ip pool add name=pool-failover-lan ranges=10.1.19.10-10.1.19.200
/ip dhcp-server network add address=10.1.19.0/24 gateway=10.1.19.1 dns-server=10.135.16.119,10.135.16.17 domain=pmm.local comment="FAILOVER rede (copia fiel DHCP real)"
/ip address add address=10.1.201.254/32 interface=bridge-transparente comment="FAILOVER dhcp takeover" disabled=yes
/ip dhcp-server add name=FAILOVER-dhcp interface=bridge-transparente address-pool=pool-failover-lan lease-time=10m authoritative=no delay-threshold=5s conflict-detection=yes comment="FAILOVER dhcp" disabled=no

# --- 4. Script de failover: deteccao v2 + DHCP v3 + conntrack v4 (<LAN3>=10.1.19)
# Substitui o v1 (so estado fisico) que veio da bancada de 18/07.
# Contador rx-packet da ether1 validado crescendo (~1160 pkt/s) com a bridge fechada.
/system script remove [find name="FAILOVER-ether1"]
/system script add name=FAILOVER-ether1 policy=read,write,test source=":global foState; :global foCnt; :global foRx; :if ([:typeof \$foState] = \"nothing\") do={ :set foState \"off\" }; :if ([:typeof \$foCnt] = \"nothing\") do={ :set foCnt 0 }; :local up [/interface get [find name=ether1] running]; :local rx [/interface get [find name=ether1] rx-packet]; :local delta 999999; :if ([:typeof \$foRx] = \"num\") do={ :set delta (\$rx - \$foRx) }; :set foRx \$rx; :if (\$delta < 0) do={ :set delta 999999 }; :local dsrv [/ip dhcp-server find name=\"FAILOVER-dhcp\"]; :if (\$foState = \"off\") do={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 50) do={ :set alive true } else={ :local gwids [/ip address find comment~\"FAILOVER gw takeover\"]; :if ([:len \$gwids] = 0) do={ :set alive true } else={ :local a [/ip address get [:pick \$gwids 0] address]; :local gw [:pick \$a 0 [:find \$a \"/\"]]; :if ([/ping \$gw count=2] > 0) do={ :set alive true } } } }; :if (\$alive = false) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 3) do={ /ip firewall nat enable [find comment~\"FAILOVER\"]; /ip dns set allow-remote-requests=yes; /ip address enable [find comment~\"FAILOVER gw takeover\"]; /ip address enable [find comment~\"FAILOVER dhcp takeover\"]; :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=yes delay-threshold=0s }; /ip firewall connection remove [find src-address~\"10.1.19\"]; :set foState \"on\"; :set foCnt 0; :log warning (\"FAILOVER-ether1 ATIVADO (link=\" . [:tostr \$up] . \" rxDelta=\" . [:tostr \$delta] . \")\") } } else={ :set foCnt 0 } } else={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 2) do={ :set alive true } }; :if (\$alive = true) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 6) do={ :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=no delay-threshold=5s }; /ip address disable [find comment~\"FAILOVER dhcp takeover\"]; /ip address disable [find comment~\"FAILOVER gw takeover\"]; /ip firewall nat disable [find comment~\"FAILOVER\"]; /ip firewall connection remove [find src-address~\"10.1.19\"]; /ip dns set allow-remote-requests=no; :set foState \"off\"; :set foCnt 0; :log warning \"FAILOVER-ether1 DESATIVADO (link up + trafego RX)\" } } else={ :set foCnt 0 } }"

# --- 5. Acesso local (padrao fase site desde 2026-07-19) -------------
/ip service set ssh address=173.20.20.0/24,10.35.0.0/24,10.1.19.0/24
/ip service set winbox address=173.20.20.0/24,10.35.0.0/24,10.1.19.0/24
/interface list member add list=MGMT interface=ether4 comment="LAN local - MAC/discovery"
