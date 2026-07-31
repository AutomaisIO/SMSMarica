# =====================================================================
# FASE SITE - id 3 - CAPS III
# MK-BORDA-CAPS-III (hEX RB750Gr3 r4, MAC etiqueta 04f41cd58d20, serial HKE0AHQYWGJ)
# Aplicado remoto em 2026-07-31 via automais-vpn (10.35.0.26)
# RouterOS subido de 7.19.6 -> 7.23.2 ANTES da fase site, na janela dark (ether4 ainda
# sem link e ether1 ocioso) - custo zero, exatamente o timing recomendado na skill.
#
# LAN UNTAGGED (como Pericles/Boqueirao/CMI; NAO e o caso tagged do CDT/CAPS AD).
# Confirmado por `/tool torch interface=ether4` - nenhum frame com VLAN-ID.
# Por isso todo o L3 mora direto na bridge-transparente, inclusive o gateway dos
# telefones (10.200.3.1/24), que ja estava correto desde a bancada.
#
# Dados L3 levantados no site (dhcp-client TEMP na bridge-transparente):
#   LAN da unidade .......: 10.1.55.0/24
#   Gateway real .........: 10.1.55.1        (2,9ms 0% perda; MAC 94:3F:C2:E7:B1:78)
#   IP do MK na LAN ......: 10.1.55.254      (verificado livre - ARP failed)
#   DHCP real (central) ..: 10.1.201.254     (o MESMO das demais unidades)
#   DNS reais ............: 10.135.16.119 , 10.135.16.17
#   Dominio ..............: pmm.local        (padrao PMM)
#   Lease real ...........: 8h
#
# ############ NOTA DE DIAGNOSTICO: o "192.168.1.x" era CABO TROCADO ############
# Durante o survey, com a ether4 ainda sem link E o cabo da Prefeitura conectado na
# porta errada, o DISCOVER do MK foi atendido por um 192.168.1.1 (gw+DHCP+DNS no mesmo
# IP, MAC 00:67:62:3A:DD:B0, lease 2h) e o quadro parecia "esta unidade nao tem link
# corporativo". Cheguei a documentar isso como anomalia da unidade - NAO era.
# Depois do cabo corrigido e da ether4 plugada, o survey trouxe o real (10.1.55.0/24,
# gw 10.1.55.1, DHCP 10.1.201.254, DNS 10.135.16.119/.17) em 4 de 4 tentativas.
# Sobra trafego residual 192.168.1.x no segmento (um KMS em :1688), sem impacto
# conhecido. Se voltar a aparecer CONCESSAO 192.168.1.x para cliente, ai virou problema.
# Licao: dado de survey so vale com o cabeamento confirmado (ver §0b da skill).
# ###############################################################################
# =====================================================================

# --- 1. Identidade do MK na LAN da unidade ---------------------------
/ip address add address=10.1.55.254/24 interface=bridge-transparente comment="MK LAN id=3"

# --- 2. Regras de FAILOVER (criadas DESABILITADAS; o script liga/desliga)
/ip address add address=10.1.55.1/32 interface=bridge-transparente comment="FAILOVER gw takeover" disabled=yes
/ip firewall nat add chain=srcnat action=masquerade src-address=10.1.55.0/24 out-interface=ether2 comment="FAILOVER nat clientes connect" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=udp dst-port=53 src-address=10.1.55.0/24 in-interface=bridge-transparente to-ports=53 comment="FAILOVER dns udp" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=tcp dst-port=53 src-address=10.1.55.0/24 in-interface=bridge-transparente to-ports=53 comment="FAILOVER dns tcp" disabled=yes

# --- 3. DHCP de RETAGUARDA (v3) - DHCP da unidade e CENTRAL: 10.1.201.254
# Fica LIGADO 24/7 com delay-threshold=5s + authoritative=no: so responde se o DHCP da
# Prefeitura nao respondeu. No failover o script inverte para authoritative=yes + delay=0s.
# DNS entregue e SEMPRE o real; quem troca na queda e o redirect :53. O takeover do IP do
# servidor DHCP real continua so no failover.
/ip pool add name=pool-failover-lan ranges=10.1.55.10-10.1.55.200
/ip dhcp-server network add address=10.1.55.0/24 gateway=10.1.55.1 dns-server=10.135.16.119,10.135.16.17 domain=pmm.local comment="FAILOVER rede (copia fiel DHCP real)"
/ip address add address=10.1.201.254/32 interface=bridge-transparente comment="FAILOVER dhcp takeover" disabled=yes
/ip dhcp-server add name=FAILOVER-dhcp interface=bridge-transparente address-pool=pool-failover-lan lease-time=10m authoritative=no delay-threshold=5s conflict-detection=yes comment="FAILOVER dhcp" disabled=no

# --- 4. Script de failover: deteccao v2 + DHCP v3 + conntrack v4 (<LAN3>=10.1.55)
# Substitui o v1 (so estado fisico) que veio da bancada de 18/07.
/system script remove [find name="FAILOVER-ether1"]
/system script add name=FAILOVER-ether1 policy=read,write,test source=":global foState; :global foCnt; :global foRx; :if ([:typeof \$foState] = \"nothing\") do={ :set foState \"off\" }; :if ([:typeof \$foCnt] = \"nothing\") do={ :set foCnt 0 }; :local up [/interface get [find name=ether1] running]; :local rx [/interface get [find name=ether1] rx-packet]; :local delta 999999; :if ([:typeof \$foRx] = \"num\") do={ :set delta (\$rx - \$foRx) }; :set foRx \$rx; :if (\$delta < 0) do={ :set delta 999999 }; :local dsrv [/ip dhcp-server find name=\"FAILOVER-dhcp\"]; :if (\$foState = \"off\") do={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 50) do={ :set alive true } else={ :local gwids [/ip address find comment~\"FAILOVER gw takeover\"]; :if ([:len \$gwids] = 0) do={ :set alive true } else={ :local a [/ip address get [:pick \$gwids 0] address]; :local gw [:pick \$a 0 [:find \$a \"/\"]]; :if ([/ping \$gw count=2] > 0) do={ :set alive true } } } }; :if (\$alive = false) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 3) do={ /ip firewall nat enable [find comment~\"FAILOVER\"]; /ip dns set allow-remote-requests=yes; /ip address enable [find comment~\"FAILOVER gw takeover\"]; /ip address enable [find comment~\"FAILOVER dhcp takeover\"]; :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=yes delay-threshold=0s }; /ip firewall connection remove [find src-address~\"10.1.55\"]; :set foState \"on\"; :set foCnt 0; :log warning (\"FAILOVER-ether1 ATIVADO (link=\" . [:tostr \$up] . \" rxDelta=\" . [:tostr \$delta] . \")\") } } else={ :set foCnt 0 } } else={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 2) do={ :set alive true } }; :if (\$alive = true) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 6) do={ :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=no delay-threshold=5s }; /ip address disable [find comment~\"FAILOVER dhcp takeover\"]; /ip address disable [find comment~\"FAILOVER gw takeover\"]; /ip firewall nat disable [find comment~\"FAILOVER\"]; /ip firewall connection remove [find src-address~\"10.1.55\"]; /ip dns set allow-remote-requests=no; :set foState \"off\"; :set foCnt 0; :log warning \"FAILOVER-ether1 DESATIVADO (link up + trafego RX)\" } } else={ :set foCnt 0 } }"

# --- 5. Estado normal do resolvedor ----------------------------------
/ip dns set allow-remote-requests=no

# --- 6. Acesso local (padrao fase site desde 2026-07-19) -------------
/ip service set ssh address=173.20.20.0/24,10.35.0.0/24,10.1.55.0/24
/ip service set winbox address=173.20.20.0/24,10.35.0.0/24,10.1.55.0/24
/interface list member add list=MGMT interface=ether4 comment="LAN local - MAC/discovery"
