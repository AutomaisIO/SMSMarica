# =====================================================================
# FASE SITE - id 18 - SRT I - Aracatiba
# MK-BORDA-SRT-I (hEX RB750Gr3, MAC etiqueta 04f41cd64d8a)
# Aplicado remoto em 2026-08-10 via automais-vpn (10.35.0.41)
# RouterOS subido de 7.19.6 -> 7.23.3 DEPOIS da fase site (janela autorizada; MK fora
# ~1min). Config integra, device-mode preservado, dois tuneis com handshake em <3min,
# scheduler FAILOVER-check continua desabilitado apos o reboot. Firmware do routerboard
# permanece 7.19.6 (so o pacote sobe). ZERO erro no log - por nao ter scheduler ligado,
# esta unidade nem produz o one-off de boot que as demais produzem.
#
# *** UNIDADE SEM LINK DA PREFEITURA - "CABOS DOBRADOS" ***
# Segunda unidade do rollout nesse cenario (a primeira foi o TFD / id 21), mas com um
# desenho DIFERENTE do TFD - e este aqui e o padrao a partir de agora (decisao do
# usuario, 2026-08-10):
#
#   Quando a unidade tem UM UNICO link, a gente NAO muda a arquitetura do MK. Dobra-se
#   o cabo: ether1 (uplink) E ether2 (Connect) vao os DOIS para o roteador do unico
#   link da unidade. A bridge transparente ether1<->ether4 continua existindo e o
#   roteador do link segue sendo gateway/DHCP/DNS da unidade, exatamente como se fosse
#   o link da Prefeitura. O MK fica so como ponte + no de VOIP.
#
#   Vantagem sobre o desenho do TFD (onde o MK virou gateway+DHCP+DNS da LAN local):
#   nada muda na LAN da unidade, e no dia em que o link da Prefeitura chegar basta
#   trocar o cabo da ether1 e rodar a FASE SITE normal.
#
# Evidencia levantada no site:
#   /tool torch interface=ether1 src-address=0.0.0.0/0 duration=12
#     -> coluna VLAN-ID VAZIA (untagged) e ZERO enderecos 10.1.x.x / 10.135.16.x.
#        So trafego 192.168.0.x + IGMP/SSDP + UDP entrante de IPs publicos.
#   dhcp-client TEMP na bridge-transparente
#     -> bound 192.168.0.116/24, gateway 192.168.0.1, dhcp-server 192.168.0.1,
#        primary-dns 192.168.0.1, lease 2h
#   dhcp-client permanente da ether2 (Connect)
#     -> bound 192.168.0.115/24, gateway 192.168.0.1, dhcp-server 192.168.0.1
#   => ether1 e ether2 estao no MESMO L2, o da Connect. Nao ha link corporativo.
#   Link fisico: ether1 negocia 1Gbps full, ether2 100Mbps full - ambos link-ok.
#
# CONSEQUENCIAS DE CONFIGURACAO (o que NAO se aplica aqui):
#
# 1. SEM regras de FAILOVER e SEM DHCP de retaguarda. Nao existe segundo caminho para
#    onde cair - o "failover" seria da Connect para a propria Connect. O scheduler
#    FAILOVER-check fica DESABILITADO (mesma decisao do TFD); o script v1 da bancada
#    permanece no disco, inerte, para o dia em que a Prefeitura chegar.
#
# 2. SEM IP do MK na LAN (o "<IP_MK_LAN> = .254" do §2 da skill). A LAN da unidade E a
#    rede 192.168.0.0/24, na qual o MK JA tem endereco pela ether2 (192.168.0.115).
#    Colocar um segundo IP da MESMA sub-rede na bridge-transparente poria duas
#    interfaces do MK no mesmo dominio de broadcast com IPs distintos - ambiguidade de
#    ARP/rota sem nenhum ganho.
#
# 3. SEM liberacao de ssh/winbox por IP vindo da LAN. Como a LAN e a rede da Connect, o
#    pacote chega pela ether2 e morre na regra "BLINDAGEM Connect" - e afrouxar essa
#    regra aqui equivaleria a expor a gestao ao wifi/ISP compartilhado. Os caminhos de
#    gestao ficam sendo: automais-vpn (10.35.0.41), ether3 (173.20.20.1) e MAC-Winbox
#    pela ether4, que e L2 e nao depende de IP.
#
# O QUE CONTINUA VALENDO NORMALMENTE:
#   - bridge transparente ether1<->ether4 (a produção da unidade atravessa o MK);
#   - tunel wg-voip ate o hub Asterisk e o gateway dos telefones 10.200.18.1/24, que
#     fica na bridge-transparente (LAN untagged) - os aparelhos plugam no switch da
#     unidade e falam com o hub pelo tunel;
#   - blindagem de seguranca da §5 e os dois tuneis WireGuard.
# =====================================================================

# --- 0. Remover o dhcp-client TEMP do survey -------------------------
/ip dhcp-client remove [find comment="TEMP survey LAN"]

# --- 1. Failover desabilitado (link unico) ---------------------------
# Script v1 permanece no disco, inerte. Sem regras FAILOVER, ele ja seria no-op; o
# scheduler e desligado para deixar a intencao explicita e nao gastar tick de 5s.
/system scheduler set [find name="FAILOVER-check"] disabled=yes comment="failover Connect - DESABILITADO: unidade sem link da Prefeitura (cabos dobrados)"

# --- 2. Estado normal do resolvedor ----------------------------------
/ip dns set allow-remote-requests=no

# --- 3. Acesso local por L2 (MAC-Winbox/discovery pela LAN da unidade)
# Nao ha liberacao por IP - ver item 3 do cabecalho.
/interface list member add list=MGMT interface=ether4 comment="LAN local - MAC/discovery"
