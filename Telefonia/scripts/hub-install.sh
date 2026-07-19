#!/usr/bin/env bash
# =============================================================================
# hub-install.sh  —  instala e configura o hub WireGuard no servidor VOIP
# Rodar UMA vez, como root, no host FALARMAIS-HOSPITAIS (192.241.153.121).
#
# É idempotente e ADITIVO: não mexe no Asterisk, no fail2ban nem nas rotas.
# Sobe a interface wg0 = 10.201.0.1/24, porta UDP 51820, e isola as unidades
# entre si (FORWARD wg0->wg0 DROP). NÃO faz masquerade (PBX enxerga IP real
# do telefone).
# =============================================================================
set -euo pipefail

WG_IF="wg0"
WG_DIR="/etc/wireguard"
WG_CONF="${WG_DIR}/${WG_IF}.conf"
HUB_ADDR="10.201.0.1/24"
LISTEN_PORT="51820"
REG="${WG_DIR}/unidades.tsv"   # registro no servidor: id<TAB>nome<TAB>pubkey<TAB>tunnel<TAB>lan

echo "==> [1/6] instalando wireguard..."
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq wireguard wireguard-tools

echo "==> [2/6] habilitando ip_forward (persistente)..."
sysctl -w net.ipv4.ip_forward=1 >/dev/null
if ! grep -q '^net.ipv4.ip_forward=1' /etc/sysctl.d/99-wg-voip.conf 2>/dev/null; then
  echo 'net.ipv4.ip_forward=1' > /etc/sysctl.d/99-wg-voip.conf
fi

echo "==> [3/6] chaves do servidor..."
umask 077
mkdir -p "${WG_DIR}"
if [[ ! -f "${WG_DIR}/server_private.key" ]]; then
  wg genkey | tee "${WG_DIR}/server_private.key" | wg pubkey > "${WG_DIR}/server_public.key"
  echo "    chaves geradas."
else
  echo "    chaves já existem (mantidas)."
fi
SRV_PRIV="$(cat "${WG_DIR}/server_private.key")"
SRV_PUB="$(cat "${WG_DIR}/server_public.key")"

echo "==> [4/6] escrevendo ${WG_CONF} (peers preservados se já houver)..."
if [[ -f "${WG_CONF}" ]]; then
  # Preserva os blocos [Peer] já existentes; regrava só o [Interface].
  awk '/^\[Peer\]/{p=1} p{print}' "${WG_CONF}" > "${WG_DIR}/.peers.tmp" || true
else
  : > "${WG_DIR}/.peers.tmp"
fi
cat > "${WG_CONF}" <<EOF
# ============================================================================
# Hub VOIP SMS Maricá — NÃO editar [Interface] à mão; use os scripts.
# Peers das unidades são adicionados por hub-add-unidade.sh
# ============================================================================
[Interface]
Address    = ${HUB_ADDR}
ListenPort = ${LISTEN_PORT}
PrivateKey = ${SRV_PRIV}

# Libera a porta do túnel e ISOLA unidades entre si (chamada interna passa pelo PBX).
# NÃO há masquerade: o Asterisk enxerga o IP real 10.200.<id>.x de cada telefone.
PostUp   = iptables -C INPUT   -p udp --dport ${LISTEN_PORT} -j ACCEPT 2>/dev/null || iptables -I INPUT   -p udp --dport ${LISTEN_PORT} -j ACCEPT
PostUp   = iptables -C FORWARD -i ${WG_IF} -o ${WG_IF} -j DROP        2>/dev/null || iptables -I FORWARD -i ${WG_IF} -o ${WG_IF} -j DROP
PostDown = iptables -D INPUT   -p udp --dport ${LISTEN_PORT} -j ACCEPT 2>/dev/null || true
PostDown = iptables -D FORWARD -i ${WG_IF} -o ${WG_IF} -j DROP        2>/dev/null || true

EOF
cat "${WG_DIR}/.peers.tmp" >> "${WG_CONF}"
rm -f "${WG_DIR}/.peers.tmp"
chmod 600 "${WG_CONF}"

# registro de unidades (cabeçalho se novo)
[[ -f "${REG}" ]] || printf 'id\tnome\tpubkey\ttunnel_ip\tlan\n' > "${REG}"

echo "==> [5/6] habilitando serviço wg-quick@${WG_IF}..."
systemctl enable "wg-quick@${WG_IF}" >/dev/null 2>&1 || true
if systemctl is-active --quiet "wg-quick@${WG_IF}"; then
  # já rodando: recarrega só a config (sem derrubar túneis existentes)
  wg syncconf "${WG_IF}" <(wg-quick strip "${WG_IF}")
  echo "    wg0 já ativo — config recarregada (syncconf)."
else
  systemctl start "wg-quick@${WG_IF}"
  echo "    wg0 iniciado."
fi

echo "==> [6/6] pronto."
echo
echo "  Interface: ${WG_IF} = ${HUB_ADDR}  (porta udp/${LISTEN_PORT})"
echo "  PUBLIC KEY DO SERVIDOR (cole nos MikroTik):"
echo "      ${SRV_PUB}"
echo
echo "  Confira:  wg show ${WG_IF}"
echo "  Próximo:  hub-add-unidade.sh --id <N> --pubkey <PUBKEY_DO_MK>"
