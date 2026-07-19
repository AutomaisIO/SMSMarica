# Hub WireGuard — servidor VOIP

Host **`FALARMAIS-HOSPITAIS`** — `192.241.153.121` (Ubuntu 22.04). Roda **Asterisk**
(SIP UDP 5060, AMI 5038) + Apache. O WireGuard sobe **ao lado**, sem tocar nesses serviços.

## O que o hub faz

| Item | Valor |
|------|-------|
| Interface | `wg0` = `10.201.0.1/24` |
| Porta | UDP `51820` |
| Sub-rede túnel | `10.201.0.0/24` (peers em `.10`–`.253`) |
| Encaminhamento | `net.ipv4.ip_forward=1` |
| Isolamento | `FORWARD wg0→wg0 DROP` (unidades não se enxergam; chamada interna via PBX) |
| NAT | **nenhum** — Asterisk vê o IP real `10.200.<id>.x` |
| Rota de volta | por peer: `AllowedIPs = 10.201.0.<id+10>/32, 10.200.<id>.0/24` |

O Asterisk já escuta em `0.0.0.0:5060`, então passa a atender os ramais pelo túnel em
`10.201.0.1:5060` automaticamente — **sem reconfigurar SIP**.

## Instalação (1×)

Copie os scripts pro servidor e rode o instalador:

```bash
# do seu PC:
scp scripts/hub-install.sh scripts/hub-add-unidade.sh root@192.241.153.121:/opt/telefonia/
# no servidor:
ssh root@192.241.153.121
mkdir -p /opt/telefonia && chmod +x /opt/telefonia/*.sh
/opt/telefonia/hub-install.sh
```

O instalador é **idempotente** e **preserva os peers** já cadastrados. Ao final imprime a
**public-key do servidor** — guarde: ela vai em todo MikroTik.

> **Instalado em 2026-07-07.** Public-key atual do hub:
> ```
> b0Da/MUl+tmetIjBN/OKfQqGCKuqtsZNsmx2Glmh9wo=
> ```
> (já embutida como default no `mikrotik-gen.sh`). A **private-key** fica só em
> `/etc/wireguard/server_private.key` no servidor.

## Operação

```bash
# estado dos túneis + handshakes
wg show wg0
wg show wg0 latest-handshakes

# adicionar uma unidade (peer)
/opt/telefonia/hub-add-unidade.sh --id 6 --pubkey "<PUBKEY_DO_MK>" --nome "CDT"

# registro local no servidor
cat /etc/wireguard/unidades.tsv

# ver quais IPs de telefone estão falando (identifica a unidade pelo 3o octeto)
wg show wg0 | grep -A3 transfer
```

## Segurança / notas

- **fail2ban** existente (chains `f2b-*`) não é tocado; continua protegendo SIP/SSH públicos.
- Só a porta **UDP 51820** é aberta a mais (regra idempotente no `PostUp`).
- Como não há masquerade e há `FORWARD wg0→wg0 DROP`, os telefones só alcançam **o próprio
  hub** (o PBX). Pra habilitar unidade↔unidade no futuro, remova o DROP e ajuste os
  `AllowedIPs` dos peers — exige novo desenho (ver README).
- **Backup:** guarde `/etc/wireguard/server_private.key` num cofre. Perdê-la obriga
  regerar a chave e reconfigurar **todos** os MikroTik.
- **Asterisk / mídia:** para chamadas entre unidades (isoladas) funcionarem com áudio, force
  a mídia pelo PBX (`directmedia=no` / `canreinvite=no` no perfil dos ramais).

## Reversão (se precisar remover tudo)

```bash
systemctl disable --now wg-quick@wg0
rm -f /etc/wireguard/wg0.conf /etc/sysctl.d/99-wg-voip.conf
# (as regras de PostUp saem sozinhas no PostDown ao parar o serviço)
sysctl -w net.ipv4.ip_forward=0
apt-get remove -y wireguard wireguard-tools   # opcional
```
