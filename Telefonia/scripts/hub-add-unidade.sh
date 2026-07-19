#!/usr/bin/env bash
# =============================================================================
# hub-add-unidade.sh  —  registra o peer de UMA unidade no hub WireGuard
# Rodar como root no servidor VOIP, DEPOIS do hub-install.sh.
#
# Uso:
#   hub-add-unidade.sh --id 6 --pubkey <PUBLIC_KEY_DO_MK> [--nome "CDT"]
#
# Deriva os IPs pela fórmula do plano de endereçamento:
#   tunnel_ip = 10.201.0.<id+10>   |   lan = 10.200.<id>.0/24
# AllowedIPs do peer = <tunnel_ip>/32 + <lan>  → o hub roteia a LAN de volta
# pelo túnel certo e sabe a unidade pelo IP de origem.
#
# Idempotente: se o pubkey já existir, atualiza os AllowedIPs.
# =============================================================================
set -euo pipefail

WG_IF="wg0"
WG_DIR="/etc/wireguard"
WG_CONF="${WG_DIR}/${WG_IF}.conf"
REG="${WG_DIR}/unidades.tsv"

ID="" ; PUBKEY="" ; NOME=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --id)     ID="$2"; shift 2;;
    --pubkey) PUBKEY="$2"; shift 2;;
    --nome)   NOME="$2"; shift 2;;
    *) echo "arg desconhecido: $1" >&2; exit 1;;
  esac
done

[[ -n "${ID}" && -n "${PUBKEY}" ]] || { echo "uso: $0 --id <N> --pubkey <KEY> [--nome ..]" >&2; exit 1; }
[[ "${ID}" =~ ^[0-9]+$ ]] || { echo "id inválido: ${ID}" >&2; exit 1; }
[[ "${ID}" -le 245 ]] || { echo "id fora da faixa (0-245): ${ID}" >&2; exit 1; }

TUN_IP="10.201.0.$((ID+10))"
LAN="10.200.${ID}.0/24"

echo "==> unidade id=${ID}  nome='${NOME:-?}'"
echo "    tunnel = ${TUN_IP}   lan = ${LAN}"
echo "    pubkey = ${PUBKEY}"

# --- 1) registro TSV é a fonte da verdade dos peers (upsert por id) ---
[[ -f "${REG}" ]] || printf 'id\tnome\tpubkey\ttunnel_ip\tlan\n' > "${REG}"
{
  head -n1 "${REG}"                                                  # cabeçalho
  { tail -n +2 "${REG}" | awk -F'\t' -v id="${ID}" '$1 != id'        # demais unidades (remove a atual)
    printf '%s\t%s\t%s\t%s\t%s\n' "${ID}" "${NOME:-}" "${PUBKEY}" "${TUN_IP}" "${LAN}"
  } | sort -n
} > "${REG}.tmp"
mv "${REG}.tmp" "${REG}"

# --- 2) regenera wg0.conf: [Interface] preservado + TODOS os peers do TSV ---
# (sem parsing frágil de blocos; robusto em mawk/gawk)
tmp="$(mktemp)"
awk '/^\[Peer\]/{exit} {print}' "${WG_CONF}" > "${tmp}"      # bloco [Interface]
while IFS=$'\t' read -r rid rnome rkey rtun rlan; do
  [[ -n "${rkey}" ]] || continue
  printf '\n[Peer]\n# unidade id=%s %s\nPublicKey  = %s\nAllowedIPs = %s/32, %s\n' \
         "${rid}" "${rnome}" "${rkey}" "${rtun}" "${rlan}" >> "${tmp}"
done < <(tail -n +2 "${REG}")
mv "${tmp}" "${WG_CONF}"
chmod 600 "${WG_CONF}"

# --- 3) aplica ao vivo, sem derrubar túneis existentes ---
wg syncconf "${WG_IF}" <(wg-quick strip "${WG_IF}")

# --- 4) rotas de kernel p/ as LANs (CRÍTICO: syncconf NÃO cria rotas; só o wg-quick
#         no boot cria, a partir dos AllowedIPs do wg0.conf. Aqui garantimos ao vivo). ---
while IFS=$'\t' read -r rid rnome rkey rtun rlan; do
  [[ -n "${rlan}" ]] || continue
  ip route replace "${rlan}" dev "${WG_IF}"
done < <(tail -n +2 "${REG}")

echo "==> peer registrado e aplicado (wg syncconf + rota de kernel)."
echo "    peers ativos: $(wg show "${WG_IF}" peers | wc -l)"
echo
echo "  Confira o handshake em alguns segundos:  wg show ${WG_IF} latest-handshakes"
echo "  Lembre de atualizar registro/unidades.csv (pubkey + status ATIVA)."
