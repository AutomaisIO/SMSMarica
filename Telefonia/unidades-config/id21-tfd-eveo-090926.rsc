# =====================================================================
# TFD (id=21) — 2026-09-09 — saída de internet pelo túnel do DC (EVEO)
# MK: hEX RB750Gr3, MAC etiqueta 04f41cd8e1d4, serial HKF0AZ1PG9P
# Gestão: 10.35.0.44 · RouterOS 7.19.6 (mantido por decisão do usuário)
#
# CONTEXTO — a unidade MUDOU DE ENDEREÇO. O MK é o MESMO (MAC confere com
# o unidades.csv); o que mudou foi o link e o cabeamento:
#   - ether2 (único link) = 4G TEMPORÁRIO, CPE 192.168.10.254 (era 192.168.0.1)
#   - ether1, ether3 e ether4 SEM LINK
#   - ether5 é a única porta viva do lado da unidade, com UM host:
#     AP "EX220V2" (F0:09:0D:72:FD:7F) em 10.1.21.180
#   - uptime de 1h10m às 15:00 => religado ~13:49 do dia 09/09
#
# ⚠️ O `endereco` do unidades.csv ("Rua das Gaivotas, 12 - Camburi") está
#    DESATUALIZADO — a unidade mudou de lugar e o endereço novo não foi
#    informado. Atualizar quando souber.
#
# MOTIVO da virada (usuário, 09/09): subiu um FW no datacenter novo e o
# tráfego da unidade deve chegar nele. Ver "PENDÊNCIA" no fim do arquivo.
# =====================================================================

# ---------------------------------------------------------------------
# FASE 1 — correções do levante + túnel EVEO  (APLICADA 09/09 ~15:15)
# ---------------------------------------------------------------------

# 1. lixo da configuração antiga: IP órfão da rede do CPE anterior,
#    com comentário errado ("gw telefones"). ARP confirmou que nada vive
#    em 192.168.0.x (192.168.0.3 = failed) antes de remover.
/ip address remove [find address="192.168.0.10/24" interface=bridge-transparente]

# 2. rótulos das portas refletindo a realidade
/interface ethernet set [find name=ether1] comment="SEM LINK - TFD nao tem link da Prefeitura"
/interface ethernet set [find name=ether2] comment="LINK UNICO - 4G TEMPORARIO (CPE 192.168.10.254) - NAO CONFIAVEL"
/interface ethernet set [find name=ether5] comment="LAN da unidade (AP EX220V2) - ether4 dark em 09/09"

# 3. 🔴 DNS ESTAVA QUEBRADO. Os forwarders eram 8.8.8.8 e 9.9.9.9 e os
#    DOIS dão 100% de timeout neste link 4G (8.8.4.4, 1.1.1.1 e 1.0.0.1
#    respondem 0%). Como o MK é o DNS de toda a unidade, ninguém resolvia
#    nome — `/tool fetch` falhava com "resolving error".
/ip dns set servers=8.8.4.4,1.0.0.1

# 4. PMTU + MSS clamp (regra §4b: obrigatório em todo link de saída)
#    medido: DF 1500 falha (frag-needed do CPE), DF 1480 passa;
#    controle no próprio CPE com 1500 passa => estrangulamento é do link.
/interface ethernet set [find name=ether2] mtu=1480
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn out-interface=ether2 tcp-mss=1441-65535 action=change-mss new-mss=1440 comment="MSS clamp link 4G out (PMTU 1480 medido 2026-09-09)"
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn in-interface=ether2 tcp-mss=1441-65535 action=change-mss new-mss=1440 comment="MSS clamp link 4G in"

# 5. TÚNEL EVEO — faixa 10.203.0.<id+10> => TFD id=21 => 10.203.0.31
#    ⚠️ MTU 1380, NÃO os 1420 padrão: com PMTU 1480 a regra MTU_WG+60<=PMTU
#    daria exatamente 1480 — margem zero. 1380 foi validado com DF.
/interface wireguard add name=wg-eveo listen-port=51833 mtu=1380 comment="Relay EVEO CCR2116 - Datacenter Automais (saida de internet)"
/ip address add address=10.203.0.31/24 interface=wg-eveo comment="tunel DC Automais id=21"
/interface wireguard peers add interface=wg-eveo public-key="1rA+xJyPWWjaAy3P0avFNNnIhl/XNycsz9FDmNpUHlc=" endpoint-address=177.136.233.75 endpoint-port=51833 allowed-address=0.0.0.0/0 persistent-keepalive=25s comment="DC Automais CCR2116"

# 6. PIN dos endpoints pela FÍSICA — sem isto o túnel se engole quando
#    virar default, e a gestão e o VOIP caem junto.
/ip route add dst-address=177.136.233.75/32 gateway=192.168.10.254 comment="PIN endpoint wg-eveo pela fisica (4G)"
/ip route add dst-address=198.211.104.55/32 gateway=192.168.10.254 comment="PIN endpoint gestao automais-vpn pela fisica (4G)"
/ip route add dst-address=192.241.153.121/32 gateway=192.168.10.254 comment="PIN endpoint voip wg-voip pela fisica (4G)"

# 7. MSS do túnel + blindagem libera a porta 51833 na entrada do link
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn out-interface=wg-eveo tcp-mss=1341-65535 action=change-mss new-mss=1340 comment="MSS clamp wg-eveo out"
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn in-interface=wg-eveo tcp-mss=1341-65535 action=change-mss new-mss=1340 comment="MSS clamp wg-eveo in"
/ip firewall filter add chain=input action=accept protocol=udp in-interface=ether2 dst-port=51833 comment="WireGuard eveo" place-before=0

# --- no CCR2116 da Eveo (10.30.50.26), peer do TFD:
#   /interface wireguard peers add interface=wg-unidades \
#       public-key="iLkwMV9po2mnYecdC+C6ZX1cNEKLqrEzMTdOGbX+5Uo=" \
#       allowed-address=10.203.0.31/32 comment="id=21 TFD"
#
# VALIDADO: handshake 25 s · ping 10.203.0.1 0% perda / ~36 ms ·
#           DF 1380 passa inteiro pelo túnel (1400 falha, como esperado).

# ---------------------------------------------------------------------
# FASE 2 — toda a internet sai pelo túnel  (APLICADA 09/09 ~15:22)
# ---------------------------------------------------------------------

# NAT dos clientes passa a sair pelo túnel
/ip firewall nat set [find comment="NAT LAN->Connect (permanente - unico link)"] out-interface=wg-eveo comment="NAT clientes via EVEO (saida de internet da unidade)"

# escape hatch DESABILITADA: para reverter, habilite esta e remova a default do túnel
/ip firewall nat add chain=srcnat action=masquerade src-address=10.1.21.0/24 out-interface=ether2 comment="ESCAPE: saida direta pelo 4G (habilitar so se o tunel morrer)" disabled=yes

# 🔴 FIM DO ECMP (lição do incidente Péricles 08/09). Com as duas defaults
# em distance=1 o RouterOS forma ECMP e metade das conexões sai pelo 4G
# SEM NAT (o masquerade é out-interface=wg-eveo) e morre em syn-sent.
/ip dhcp-client set [find interface=ether2] default-route-distance=2

/ip route add dst-address=0.0.0.0/0 gateway=10.203.0.1 distance=1 comment="default via EVEO (saida de internet da unidade)"

# Aplicado com scheduler SAFETY-revert-eveo (8 min) armado, removido após
# a validação. VALIDADO:
#   - traceroute 1.1.1.1: 10.203.0.1 -> 177.136.233.74 (uplink Eveo) -> ... -> 1.1.1.1
#     ou seja, TODA a saída passa pelo Datacenter.
#   - :resolve google.com volta 172.217.162.174 (DNS consertado)
#   - os TRÊS túneis com handshake recente após a virada:
#     wg-voip 52 s · automais-vpn 1 m07 s · wg-eveo 1 m17 s
#   - wg-eveo com 98,5 MiB rx => tráfego real de cliente atravessando

# ---------------------------------------------------------------------
# O QUE **NÃO** FOI FEITO
# ---------------------------------------------------------------------
# - upgrade de RouterOS: fica em 7.19.6 por decisão do usuário (09/09).
#   A frota está em 7.23.3. Pendência conhecida.
# - failover: não existe segundo caminho. O scheduler FAILOVER-check segue
#   DESABILITADO e o script v1 inerte no disco, como na exceção do TFD.
# - DOH-BLOCK / ENJAULA: criados por terceiros em 09/09 14:16 (25 min antes
#   do levante), NÃO mexidos. São complementares ao bloqueio por DNS —
#   impedem o cliente de furar o filtro por DoH/DoT.

# =====================================================================
# PENDÊNCIA 1 — o tráfego chega ao DC mas NÃO passa por firewall nenhum
# =====================================================================
# Objetivo do usuário: o tráfego deve chegar ao FW novo do datacenter.
# Estado real do CCR2116 hoje: a saída das unidades é
#     ;;; EVEO-UPLINK
#     chain=srcnat action=masquerade out-interface=ether1
# ou seja, 10.203.0.31 sai direto para a internet pelo uplink. Não há
# appliance nenhum no caminho, e nenhuma rota do CCR aponta para um FW.
# Varrido o DC: ARP da VL40-VMS e leases do DHCP não revelam candidato
# (proxy01, paracambi, assistente-01, wifi01, chp01, chplab-srv, PSQL-01,
# LEVITA-01). Falta saber o IP/desenho do FW para roteá-lo.
# ⚠️ Isso é mudança SÓ no DC — nada muda no MK do TFD quando for feito.

# =====================================================================
# PENDÊNCIA 2 — DNS da Prefeitura (relé PMM) — BLOQUEADO, NÃO APLICADO
# =====================================================================
# Pedido do usuário (09/09): enquanto o FW não está ativo, entregar o DNS
# da Prefeitura, que já bloqueia parte das coisas — "como fizemos o teste
# recente, usar DNS e infra da prefeitura passando pela VPN".
# Isso é exatamente o relé PMM (docs/rele-pmm.md).
#
# MEDIDO em 09/09:
#   - do TFD: 10.135.16.17/.18/.119 => 100% timeout (o DC dropa, como projetado)
#   - do CAPS III: os mesmos DCs respondem em 1-2 ms, ether1 RUNNING,
#     rota "PMM internas" ATIVA => o relé está saudável
#
# O TFD é o caso MAIS SIMPLES do padrão: como ele é ROTEADOR (não ponte) e
# a default já vai pelo wg-eveo, **nada muda no MK do TFD** — a regra
# "NAT clientes via EVEO" já converte o cliente em 10.203.0.31, que é o
# endereço que as regras do DC esperam. Não há FO-RELE-PMM, não há
# check-gateway: não existe perna da Prefeitura para voltar.
#
# Faltam TRÊS regras, em dois equipamentos:
#
# --- no CCR2116 do DC (10.30.50.26) ---
#   /ip firewall filter add chain=forward action=accept in-interface=wg-unidades \
#       out-interface=wg-unidades src-address=10.203.0.31 dst-address=10.135.16.0/24 \
#       comment="RELE PMM ida: TFD (id=21) alcanca a PMM pelo CAPS III (id=3)" \
#       place-before=[find comment~"unidades NAO acessam rede interna"]
#   /ip firewall filter add chain=forward action=accept in-interface=wg-unidades \
#       out-interface=wg-unidades src-address=10.135.16.0/24 dst-address=10.203.0.31 \
#       comment="RELE PMM volta: PMM responde ao TFD (id=21)" \
#       place-before=[find comment~"unidades NAO acessam rede interna"]
#   (a rota 10.135.16.0/24 -> 10.203.0.13 e o allowed-address do peer do
#    CAPS III JÁ EXISTEM, do relé do Péricles — nada a fazer neles)
#
# --- no CAPS III (10.35.0.26), o relé ---
#   /ip firewall nat add chain=srcnat action=masquerade src-address=10.203.0.31 \
#       dst-address=10.135.16.0/24 out-interface=bridge-transparente \
#       comment="RELE PMM: TFD (id=21) -> LAN da PMM"
#
# --- só DEPOIS de o ping 10.135.16.18 passar a responder do TFD ---
#   /ip dns set servers=10.135.16.18,10.135.16.119,10.135.16.17
#
# ⚠️ TROCA CONSCIENTE: com forwarders SÓ da Prefeitura, o bloqueio dela
#    passa a valer — que é o objetivo — mas a unidade fica sem resolução
#    de nome nenhuma se o relé cair (CAPS III fora, ou perna da Prefeitura
#    do CAPS III fora). Pôr um público de reserva anularia o bloqueio, por
#    isso NÃO foi posto. Reversão de uma linha:
#       /ip dns set servers=8.8.4.4,1.0.0.1
#
# ⚠️ Isso põe o DNS do TFD dependendo do CAPS III. Registrar o par no
#    unidades.csv (rele_pmm=3 no TFD, rele_de acumula 21 no CAPS III)
#    quando aplicar — sem isso não dá para alertar "o relé caiu junto".
