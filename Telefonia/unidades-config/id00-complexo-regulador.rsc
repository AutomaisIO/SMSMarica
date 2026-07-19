# =====================================================================
#  RouterOS v7 — UNIDADE id=0
#  Túnel: MK=10.201.0.10/24  |  Telefones: 10.200.0.0/24 (gw 10.200.0.1)
#  Interface dos aparelhos: bridge-telefones   (AJUSTE se o nome for outro!)
#  Hub Asterisk (SIP server dos ramais): 10.201.0.1
# =====================================================================

# 1) Interface WireGuard (o MK gera a chave privada sozinho)
/interface/wireguard/add name=wg-voip listen-port=51820 comment="VOIP hub SMS Marica"

# 2) Endereço do MK no túnel
/ip/address/add address=10.201.0.10/24 interface=wg-voip comment="tunel voip id=0"

# 3) Peer = hub (endpoint público do servidor Asterisk)
/interface/wireguard/peers/add \
    interface=wg-voip \
    public-key="b0Da/MUl+tmetIjBN/OKfQqGCKuqtsZNsmx2Glmh9wo=" \
    endpoint-address=192.241.153.121 endpoint-port=51820 \
    allowed-address=10.201.0.0/24 \
    persistent-keepalive=25s \
    comment="hub asterisk"

# 4) Gateway dos telefones (na interface que atende os aparelhos)
/ip/address/add address=10.200.0.1/24 interface=bridge-telefones comment="gw telefones id=0"

# 5) DHCP dos telefones na faixa .10-.200 (opcional; use se não for IP fixo no aparelho)
/ip/pool/add name=pool-tel-0 ranges=10.200.0.10-10.200.0.200
/ip/dhcp-server/add name=dhcp-tel-0 interface=bridge-telefones address-pool=pool-tel-0 lease-time=1d disabled=no
/ip/dhcp-server/network/add address=10.200.0.0/24 gateway=10.200.0.1 comment="telefones id=0"

# 6) Firewall: telefones são VOIP-only (só falam com o hub pelo túnel)
/ip/firewall/filter/add chain=forward in-interface=wg-voip dst-address=10.200.0.0/24 \
    action=accept comment="VOIP: hub -> telefones id=0"
/ip/firewall/filter/add chain=forward src-address=10.200.0.0/24 out-interface=wg-voip \
    action=accept comment="VOIP: telefones -> hub id=0"
/ip/firewall/filter/add chain=forward src-address=10.200.0.0/24 \
    action=drop comment="VOIP: telefones bloqueados fora do tunel id=0"

# 7) IMPORTANTE: NÃO mascarar o tráfego que sai por wg-voip.
#    Confira que nenhuma regra srcnat/masquerade cobre out-interface=wg-voip
#    (o hub PRECISA ver o IP real 10.200.0.x pra saber a unidade).
/ip/firewall/nat/print where action=masquerade

# 8) Pegue a PUBLIC KEY que o MK gerou e mande pro hub:
:put ("PUBLIC-KEY DESTE MK -> registrar no hub:")
/interface/wireguard/print detail where name=wg-voip
# no hub:  hub-add-unidade.sh --id 0 --pubkey <a public-key acima> --nome "<nome>"
