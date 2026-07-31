# Failover pela Connect (ether2) - deteccao v2 EM CAMADAS + DHCP v3 SEMPRE LIGADO
#
# DETECCAO (v2, validada na Regulacao 20/07/2026):
#   ENTRAR (estado off, 3 ciclos ~15s de "morto"): ether1 down  OU  (RX parado E gateway real sem ping)
#     - o ping usa o IP do objeto "FAILOVER gw takeover" (criado NO SITE); sem o objeto = so fisico (no-op bancada)
#     - delta rx-packet >= 50/ciclo = trafego real fluindo -> vivo sem ping (gw que corta ICMP sob carga)
#     - e a camada de ping que pega FIBRA PARTIDA ATRAS DA ONU com porta ON (incidente Regulacao 20/07)
#   SAIR (estado on, 6 ciclos ~30s): ether1 up E delta rx-packet >= 2/ciclo (chatter do head-end voltou).
#     Ping nao serve na saida: o MK e dono do gw durante o takeover (self-ping).
# ⚠ Conferir que [/interface get ether1 rx-packet] CRESCE com trafego atravessando a bridge; se ficar
#   parado, PRIMEIRO confira se o cabo esta no lugar (/interface ethernet monitor ether1 once) - so depois
#   suspeite de hw-offload (nenhum hEX do rollout precisou de hw=no ate agora).
#
# DHCP (v3, decisao 2026-07-29): o DHCP do MK fica LIGADO 24/7 como RETAGUARDA, nao mais so no failover.
#   Estado normal ....: delay-threshold=5s (ignora DISCOVER com secs<5s -> so responde se o DHCP da
#                       Prefeitura nao respondeu) + authoritative=no (nunca NAK, nao briga por lease alheio).
#                       Na pratica fica calado; cobre de imediato quem liga o PC durante uma queda.
#   Estado failover ..: o script inverte para authoritative=yes + delay-threshold=0s -> responde na hora e
#                       manda NAK em lease velho, fazendo o cliente pegar IP novo em segundos (em vez de
#                       esperar as 8h de lease da Prefeitura vencerem).
#   DNS entregue .....: SEMPRE os DNS REAIS da unidade. Quem troca o DNS na queda e o redirect :53 (vale
#                       instantaneamente pra todo mundo); trocar o dns-server do escopo so afetaria lease
#                       novo e deixaria DNS publico na mao do cliente por ate 1 lease DEPOIS da volta.
#   conflict-detection: ARP-probe antes de oferecer - e o que evita IP duplicado convivendo com o DHCP real.
#
# CONNTRACK (v4, licao da Regulacao 2026-07-29): nas DUAS transicoes o script apaga as conexoes da LAN.
#   Sem isso, na VOLTA do link o failover devolve IP/DHCP/DNS/NAT mas as sessoes NAO migram:
#     - cache ARP do PC ainda aponta 10.x.y.1 -> MAC do MK, e o MK (que ainda tem rota default) segue
#       roteando pela Connect, FURANDO o proxy/filtro da Prefeitura sem ninguem perceber;
#     - conexao TCP longa (push do Google :5228, agente de EDR :1514) NAO PODE migrar - foi estabelecida
#       com o IP publico da Connect; se o pacote passar a sair pela Prefeitura o IP muda e o outro lado
#       rejeita. Ela fica grudada no caminho antigo pra sempre;
#     - pior: o NAT ja foi desligado, entao conexao NOVA de PC com ARP velho sai com origem privada e
#       o provedor descarta. Sintoma: "o que estava aberto funciona, o que eu abro agora nao".
#   Medido na Regulacao: 341 conexoes persistiram apos a volta do link; so normalizou ao apagar todas.
# ⚠ <LAN3> = primeiros tres octetos da LAN da unidade, SEM ponto final (ex.: 10.1.18 para 10.1.18.0/24).
#   Substituir antes de aplicar, junto com a porta automais 133xx e o id da unidade.
#
# Regras FAILOVER (nat/takeover/dhcp) e o proprio DHCP sao criados NO SITE com os dados L3 reais da LAN;
# o script enable/disable por comentario/nome - sem regras, e no-op seguro.
/ip dhcp-client add interface=ether2 comment="Connect - internet do MK" disabled=no use-peer-ntp=no use-peer-dns=no
/system script add name=FAILOVER-ether1 policy=read,write,test source=":global foState; :global foCnt; :global foRx; :if ([:typeof \$foState] = \"nothing\") do={ :set foState \"off\" }; :if ([:typeof \$foCnt] = \"nothing\") do={ :set foCnt 0 }; :local up [/interface get [find name=ether1] running]; :local rx [/interface get [find name=ether1] rx-packet]; :local delta 999999; :if ([:typeof \$foRx] = \"num\") do={ :set delta (\$rx - \$foRx) }; :set foRx \$rx; :if (\$delta < 0) do={ :set delta 999999 }; :local dsrv [/ip dhcp-server find name=\"FAILOVER-dhcp\"]; :if (\$foState = \"off\") do={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 50) do={ :set alive true } else={ :local gwids [/ip address find comment~\"FAILOVER gw takeover\"]; :if ([:len \$gwids] = 0) do={ :set alive true } else={ :local a [/ip address get [:pick \$gwids 0] address]; :local gw [:pick \$a 0 [:find \$a \"/\"]]; :if ([/ping \$gw count=2] > 0) do={ :set alive true } } } }; :if (\$alive = false) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 3) do={ /ip firewall nat enable [find comment~\"FAILOVER\"]; /ip dns set allow-remote-requests=yes; /ip address enable [find comment~\"FAILOVER gw takeover\"]; /ip address enable [find comment~\"FAILOVER dhcp takeover\"]; :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=yes delay-threshold=0s }; /ip firewall connection remove [find src-address~\"<LAN3>\"]; :set foState \"on\"; :set foCnt 0; :log warning (\"FAILOVER-ether1 ATIVADO (link=\" . [:tostr \$up] . \" rxDelta=\" . [:tostr \$delta] . \")\") } } else={ :set foCnt 0 } } else={ :local alive false; :if (\$up = true) do={ :if (\$delta >= 2) do={ :set alive true } }; :if (\$alive = true) do={ :set foCnt (\$foCnt + 1); :if (\$foCnt >= 6) do={ :if ([:len \$dsrv] > 0) do={ /ip dhcp-server set \$dsrv authoritative=no delay-threshold=5s }; /ip address disable [find comment~\"FAILOVER dhcp takeover\"]; /ip address disable [find comment~\"FAILOVER gw takeover\"]; /ip firewall nat disable [find comment~\"FAILOVER\"]; /ip firewall connection remove [find src-address~\"<LAN3>\"]; /ip dns set allow-remote-requests=no; :set foState \"off\"; :set foCnt 0; :log warning \"FAILOVER-ether1 DESATIVADO (link up + trafego RX)\" } } else={ :set foCnt 0 } }"
/system scheduler add name=FAILOVER-check interval=5s on-event="/system script run FAILOVER-ether1" comment="failover Connect"
# --- DHCP de retaguarda (criar NO SITE) ------------------------------------------------------------
# Copia FIEL do DHCP real da unidade (mesma faixa, gw, DNS REAIS, dominio) + takeover do IP do servidor
# real (so no failover: responde renovacao unicast -> ninguem perde o IP que ja tem).
# /ip pool add name=pool-failover-lan ranges=<POOL_INI>-<POOL_FIM>
# /ip dhcp-server network add address=<LAN>/24 gateway=<GW_REAL> dns-server=<DNS_REAL_1>,<DNS_REAL_2> domain=<DOMINIO> comment="FAILOVER rede (copia fiel DHCP real)"
# /ip address add address=<IP_DHCP_REAL>/32 interface=bridge-transparente comment="FAILOVER dhcp takeover" disabled=yes
# /ip dhcp-server add name=FAILOVER-dhcp interface=bridge-transparente address-pool=pool-failover-lan lease-time=10m authoritative=no delay-threshold=5s conflict-detection=yes comment="FAILOVER dhcp" disabled=no
