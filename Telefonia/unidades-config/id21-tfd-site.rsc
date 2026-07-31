# =====================================================================
# FASE SITE - id 21 - TFD (Transporte Fora do Domicilio)
# MK-BORDA-TFD (hEX RB750Gr3, MAC etiqueta 04f41cd8e1d4)
# Aplicado remoto em 2026-07-30 via automais-vpn (10.35.0.44)
#
# ⚠ UNIDADE FORA DO PADRAO: NAO EXISTE link da Prefeitura no TFD.
#   A Connect (ether2) e o UNICO link de internet — a unidade opera em
#   "failover permanente". Decisao do usuario 2026-07-30:
#     - o MK e o gateway + DHCP + DNS da LAN local;
#     - faixa no padrao PMM derivada do id: 10.1.21.0/24 (id=21), gw 10.1.21.1;
#     - NAT permanente LAN -> Connect (comentario SEM "FAILOVER" de proposito:
#       o script de failover alterna objetos por comment~"FAILOVER");
#     - scheduler FAILOVER-check DESABILITADO (nao ha link primario a vigiar;
#       se a Prefeitura chegar um dia: survey L3 real, recriar regras FAILOVER
#       padrao e reabilitar o scheduler).
#   ether1 fica vago aguardando um futuro link da Prefeitura.
#
#   LAN da unidade .......: 10.1.21.0/24      (local, servida pelo MK)
#   Gateway / DNS ........: 10.1.21.1         (o proprio MK)
#   Pool DHCP ............: 10.1.21.10-200    (lease 30m, authoritative)
#   Forwarders DNS .......: 8.8.8.8 , 9.9.9.9
#   Telefones ............: 10.200.21.0/24 (IP fixo, gw 10.200.21.1, inalterado)
# =====================================================================

# --- 1. MK como gateway da LAN local ---------------------------------
/ip address add address=10.1.21.1/24 interface=bridge-transparente comment="MK LAN id=21 - gateway local (sem link Prefeitura)"

# --- 2. DHCP local (unico servidor da LAN — sempre autoritativo) -----
/ip pool add name=pool-lan ranges=10.1.21.10-10.1.21.200
/ip dhcp-server network add address=10.1.21.0/24 gateway=10.1.21.1 dns-server=10.1.21.1 comment="LAN local TFD (MK e gw+DNS; Connect e o unico link)"
/ip dhcp-server add name=LAN-dhcp interface=bridge-transparente address-pool=pool-lan lease-time=30m authoritative=yes conflict-detection=yes comment="DHCP local TFD" disabled=no

# --- 3. DNS do MK como resolvedor da LAN -----------------------------
/ip dns set servers=8.8.8.8,9.9.9.9 allow-remote-requests=yes

# --- 4. NAT permanente pela Connect ----------------------------------
/ip firewall nat add chain=srcnat action=masquerade src-address=10.1.21.0/24 out-interface=ether2 comment="NAT LAN->Connect (permanente - unico link)"

# --- 5. Failover neutralizado (nao ha link primario) -----------------
/system scheduler disable [find name="FAILOVER-check"]

# --- 6. Acesso local (padrao fase site desde 2026-07-19) -------------
/ip service set ssh address=173.20.20.0/24,10.35.0.0/24,10.1.21.0/24
/ip service set winbox address=173.20.20.0/24,10.35.0.0/24,10.1.21.0/24
/interface list member add list=MGMT interface=ether4 comment="LAN local - MAC/discovery"
