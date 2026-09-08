# =====================================================================
# ONDA 1 — CORREÇÕES SEGURAS (não muda arquitetura, não derruba ninguém)
# Padrão SMS Maricá — criado 02/09/2026 a partir dos achados do Complexo
#
# Aplicar em TODA unidade com link da Prefeitura. Reversível item a item.
# NÃO mexe em: bridge, takeover, script de failover, rotas de clientes.
#
# PLACEHOLDERS a substituir:
#   <ID>        id da unidade
#   <LAN_IF>    interface L3 da LAN: bridge-transparente (untagged) ou vlan<VID>-lan (tagged)
#   <CONN>      porta da Connect: ether2 no hEX, ether3 no CCR
#   <PMTU>      PMTU medido no passo 1 (ex.: 1480)
#   <MSS>       <PMTU> - 40
#   <DNS_REAIS> DNS capturados no passo 2, na ORDEM em que a PMM entrega
#   <LEASE>     lease real capturado no passo 2 (ex.: 1d)
# =====================================================================

# --- PASSO 1: MEDIR o PMTU do link Connect (só leitura, faça ANTES) --
#   /ping 8.8.8.8 size=1500 do-not-fragment count=2 interface=<CONN>   -> deve FALHAR
#   /ping 8.8.8.8 size=1480 do-not-fragment count=2 interface=<CONN>   -> deve PASSAR
#   /ping 8.8.8.8 size=1484 do-not-fragment count=2 interface=<CONN>   -> confirma o limite
#   /ping <gw da Connect> size=1500 do-not-fragment count=2            -> CONTROLE: deve passar
#   (se o controle falhar, o estrangulamento é local, não do provedor — investigar antes)

# --- PASSO 2: CAPTURAR o escopo REAL do DHCP da Prefeitura ----------
#   Só leitura, ~40 s. Desliga o DHCP do MK para o DISCOVER chegar ao servidor real.
#   /ip dhcp-server disable [find name="FAILOVER-dhcp"]
#   /ip dhcp-client add interface=<LAN_IF> add-default-route=no use-peer-dns=no use-peer-ntp=no comment="TEMP captura escopo"
#   :delay 25s
#   /ip dhcp-client print detail where comment~"TEMP captura"     <-- anote gateway, DNS e lease
#   /ip dhcp-client remove [find comment~"TEMP captura"]
#   /ip dhcp-server enable [find name="FAILOVER-dhcp"]
#   ⚠ Para ver TODAS as opções (121/249 rotas classless, 252 WPAD, 119 domain-search) é preciso
#     sniffer de bootp + parse do pcap — mas o **`/tool sniffer` é BLOQUEADO pelo device-mode nos
#     hEX**; só funciona no CCR. Nos hEX o `dhcp-client` entrega gateway, DNS e lease, e mais nada.
#   ⇒ REGRA: **só altere o que você MEDIU**. Sem captura completa, NÃO mexa no `domain` nem
#     invente opções — corrija apenas DNS e lease, que o dhcp-client mostra.
#
#   ⚠ O ESCOPO VARIA POR UNIDADE — não replique o de outra. Medido em 02/09:
#       Complexo : server-id 10.3.74.1 (o switch HPE é o servidor) · 5 DNS (.18,.119,.17,1.1.1.1,8.8.4.4) · lease 24 h
#       Péricles : server-id 10.1.201.254 (central, via relay)     · 2 DNS (.119,.17)                     · lease 8 h
#     São arquiteturas de DHCP diferentes na mesma rede. Medir SEMPRE.

# --- PASSO 3: PMTU e MSS clamp na Connect ---------------------------
# O ICMP "fragmentation needed" do provedor não é confiável: usar OS DOIS mecanismos.
/interface ethernet set [find name=<CONN>] mtu=<PMTU>
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn out-interface=<CONN> \
    tcp-mss=<MSS+1>-65535 action=change-mss new-mss=<MSS> \
    comment="MSS clamp Connect (PMTU <PMTU> medido 2026-09-02)"
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn in-interface=<CONN> \
    tcp-mss=<MSS+1>-65535 action=change-mss new-mss=<MSS> \
    comment="MSS clamp Connect in"

# --- PASSO 4: escopo do DHCP = cópia FIEL do real -------------------
# Corrige o defeito encontrado em 8 de 8 unidades: DNS incompleto, domain inventado, lease 144x menor.
/ip dhcp-server network set numbers=0 dns-server=<DNS_REAIS> domain="" \
    comment="escopo REAL da PMM (capturado 2026-09-02)"
/ip dhcp-server set [find name="FAILOVER-dhcp"] lease-time=<LEASE>
# ⚠ `[find address=...]` NÃO casa em /ip dhcp-server network no ROS 7.23 — usar numbers=

# --- PASSO 5: resolvedor do provedor fora ---------------------------
# Sem isso o MK herda o DNS do ISP da Connect, contra o padrão.
/ip dhcp-client set [find interface=<CONN>] use-peer-dns=no

# --- PASSO 6: NTP -------------------------------------------------
# Relógio errado tornou os logs do incidente de 02/09 não-monotônicos e ilegíveis.
/system ntp client set enabled=yes mode=unicast servers=pool.ntp.br,a.st1.ntp.br
/system clock set time-zone-name=America/Sao_Paulo

# --- PASSO 7: lista de serviços essenciais (inerte) -----------------
# Nenhuma regra usa esta lista. Existe para, se um dia for preciso, desviar
# SÓ estes destinos para um link de contingência alternativo.
/ip firewall address-list add list=SERVICOS-ESSENCIAIS address=189.28.130.13 comment="SISREG DATASUS"
/ip firewall address-list add list=SERVICOS-ESSENCIAIS address=200.166.238.18 comment="SER ser.saude.rj.gov.br"
/ip firewall address-list add list=SERVICOS-ESSENCIAIS address=146.190.65.73 comment="smsmarica.online"

# --- VERIFICAÇÃO ---------------------------------------------------
#   /interface ethernet print where name=<CONN>            -> mtu=<PMTU>
#   /ip firewall mangle print stats where comment~"MSS"    -> contador cresce com SYN de cliente
#   /ip dhcp-server network print detail                   -> DNS reais, sem domain
#   /system ntp client print                               -> status=synchronized
#   /ip dhcp-client print where interface=<CONN>           -> use-peer-dns=no
#
# --- REVERSÃO ------------------------------------------------------
#   /ip firewall mangle remove [find comment~"MSS clamp Connect"]
#   /interface ethernet set [find name=<CONN>] mtu=1500
#   (escopo/NTP/lista: reverter só se houver motivo — são correções, não mudanças)

# =====================================================================
# O QUE **NÃO** ENTRA NA ONDA 1 (fica para as ondas seguintes)
#  - desligar o DHCP do MK: nas unidades ainda no failover v2 o DHCP é a
#    ÚNICA retaguarda na contingência. Só desligar junto com o v5, que
#    amarra o DHCP ao estado do failover.
#  - túnel wg-eveo, captura L2 (FO-*), sondas, FO-tick: ondas 2 e 3/4.
#  - remoção do usuário admin: exige criar/testar o becape antes.
# =====================================================================
