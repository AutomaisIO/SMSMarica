#!/usr/bin/env bash
# =============================================================================
# mikrotik-gen.sh  —  gera o script RouterOS (v7) pronto pra UMA unidade
# Rodar em qualquer lugar (bash). Cole a saída no terminal do MikroTik.
#
# Uso:
#   SERVER_PUBKEY="<pubkey_do_hub>" ./mikrotik-gen.sh <id> [iface_telefones]
#
#   <id>              id da unidade (ver registro/unidades.csv)
#   iface_telefones   interface/bridge que atende os aparelhos (default: bridge-telefones)
#
# Requisitos no MK: RouterOS v7+ (WireGuard). O MK GERA a própria chave privada;
# só a public-key dele volta pro hub (hub-add-unidade.sh).
# =============================================================================
set -euo pipefail

ID="${1:-}"
IFACE="${2:-bridge-telefones}"
HUB_ENDPOINT="192.241.153.121"
HUB_PORT="51820"
# public-key do hub (gerada em 2026-07-07 pelo hub-install.sh). Sobrescreva com
# SERVER_PUBKEY=... se o hub for reinstalado/rotacionado.
SRV_PUB="${SERVER_PUBKEY:-b0Da/MUl+tmetIjBN/OKfQqGCKuqtsZNsmx2Glmh9wo=}"

[[ -n "${ID}" && "${ID}" =~ ^[0-9]+$ ]] || { echo "uso: SERVER_PUBKEY=.. $0 <id> [iface]" >&2; exit 1; }

TUN="10.201.0.$((ID+10))"
LAN3="10.200.${ID}"

cat <<EOF
# =====================================================================
#  RouterOS v7 — UNIDADE id=${ID}
#  Túnel: MK=${TUN}/24  |  Telefones: ${LAN3}.0/24 (gw ${LAN3}.1)
#  Interface dos aparelhos: ${IFACE}   (AJUSTE se o nome for outro!)
#  Hub Asterisk (SIP server dos ramais): 10.201.0.1
# =====================================================================

# 1) Interface WireGuard (o MK gera a chave privada sozinho)
/interface/wireguard/add name=wg-voip listen-port=${HUB_PORT} comment="VOIP hub SMS Marica"

# 2) Endereço do MK no túnel
/ip/address/add address=${TUN}/24 interface=wg-voip comment="tunel voip id=${ID}"

# 3) Peer = hub (endpoint público do servidor Asterisk)
/interface/wireguard/peers/add \\
    interface=wg-voip \\
    public-key="${SRV_PUB}" \\
    endpoint-address=${HUB_ENDPOINT} endpoint-port=${HUB_PORT} \\
    allowed-address=10.201.0.0/24 \\
    persistent-keepalive=25s \\
    comment="hub asterisk"

# 4) Gateway dos telefones (na interface que atende os aparelhos)
/ip/address/add address=${LAN3}.1/24 interface=${IFACE} comment="gw telefones id=${ID}"

# 5) SEM DHCP — os telefones são configurados MANUALMENTE (IP fixo). Em cada aparelho:
#      IP ......: ${LAN3}.10 a ${LAN3}.200   (um por telefone, sem repetir)
#      Máscara .: 255.255.255.0   (/24)
#      Gateway .: ${LAN3}.1        (este MK)
#      SIP srv .: 10.201.0.1       (Asterisk, via túnel)

# 6) Firewall: telefones são VOIP-only (só falam com o hub pelo túnel)
/ip/firewall/filter/add chain=forward in-interface=wg-voip dst-address=${LAN3}.0/24 \\
    action=accept comment="VOIP: hub -> telefones id=${ID}"
/ip/firewall/filter/add chain=forward src-address=${LAN3}.0/24 out-interface=wg-voip \\
    action=accept comment="VOIP: telefones -> hub id=${ID}"
/ip/firewall/filter/add chain=forward src-address=${LAN3}.0/24 \\
    action=drop comment="VOIP: telefones bloqueados fora do tunel id=${ID}"

# 7) IMPORTANTE: NÃO mascarar o tráfego que sai por wg-voip.
#    Confira que nenhuma regra srcnat/masquerade cobre out-interface=wg-voip
#    (o hub PRECISA ver o IP real ${LAN3}.x pra saber a unidade).
/ip/firewall/nat/print where action=masquerade

# 8) Pegue a PUBLIC KEY que o MK gerou e mande pro hub:
:put ("PUBLIC-KEY DESTE MK -> registrar no hub:")
/interface/wireguard/print detail where name=wg-voip
# no hub:  hub-add-unidade.sh --id ${ID} --pubkey <a public-key acima> --nome "<nome>"
EOF
