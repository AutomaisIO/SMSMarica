# Migração do gateway VOIP do Complexo Regulador (id=0): RB750r2 → CCR1009

> **Data:** 13/07/2026
> **Motivo:** substituir o MikroTik RB750r2 antigo (`10.3.74.254`) pelo **CCR1009-7G-1C-1S+**
> que agora também faz a ponte transparente ONU↔switch da unidade.
> **Resultado:** túnel WireGuard VOIP no ar pelo CCR, com handshake e ping ao hub confirmados.

## Equipamentos

| | Antigo | Novo |
|---|--------|------|
| Modelo | RB750r2 | CCR1009-7G-1C-1S+ (serial `D56A0C5D41FE`) |
| RouterOS | 7.23.2 | 7.23.2 (upgrade de 6.49.10 feito antes da migração) |
| Estado | **desligado da rede** | ativo |
| pubkey WG (VOIP) | `Xi1xtKDBkkGfXfAYAJSwD6qeJEIJRtHt9szUvE49yXw=` (revogada) | `fbrHbrRVFWBIsJIIx+qwUk9OQG0A1d/tGYYcRtX7ZDc=` |

## Endereçamento assumido pelo CCR (id=0)

| Papel | IP | Interface |
|-------|----|-----------|
| Identidade na LAN da unidade (ex-RB) | `10.3.74.254/24` | `bridge-transparente` |
| **Gateway dos telefones** | `10.200.0.1/24` | `bridge-transparente` |
| IP do MK no túnel | `10.201.0.10/24` | `wg-voip` |
| Gestão out-of-band | `173.20.20.1/24` | `ether1-GESTAO` |

> Os telefones (IP fixo `10.200.0.10–.200`, gw `10.200.0.1`, SIP server `10.201.0.1`) estão no
> mesmo L2 do switch (lado `ether2` da bridge) — por isso `10.200.0.1` e `10.3.74.254`
> convivem como endereços secundários na `bridge-transparente`, **sem quebrar o L2 transparente**.

## WireGuard VOIP (wg-voip)

- Peer = hub Asterisk: pubkey `b0Da/MUl+tmetIjBN/OKfQqGCKuqtsZNsmx2Glmh9wo=`,
  endpoint `192.241.153.121:51820`, `allowed-address=10.201.0.0/24`, `persistent-keepalive=25s`.
- Firewall VOIP-only na chain `forward` para `10.200.0.0/24` (aceita ↔ `wg-voip`, dropa o resto).
- **Sem NAT** no `wg-voip` — o hub vê o IP real `10.200.0.x` e identifica a unidade pelo 3º octeto.

## Registro no hub

`hub-add-unidade.sh --id 0 --pubkey fbrHbrRVFWBIsJIIx+qwUk9OQG0A1d/tGYYcRtX7ZDc= --nome "COMPLEXO REGULADOR"`
(upsert idempotente por id — substituiu a chave do RB antigo; demais peers preservados; Asterisk não reiniciado).

## Verificação (13/07/2026)

- CCR → `ping 192.241.153.121` (endpoint): 0% loss.
- CCR → `ping 10.201.0.1` pelo `wg-voip` (SIP server): **0% loss, ~116 ms**.
- Peer no CCR: `last-handshake` recente, rx/tx fluindo.
- Hub `wg show wg0`: handshake e transfer com a pubkey nova.
- Asterisk SIP `0.0.0.0:5060` intacto.

## Pendências / observações

1. **Internet do CCR** hoje sai pelo `ether3` (DHCP `192.168.0.110`, default via `192.168.0.1`) —
   o túnel VOIP depende desse uplink. Avaliar se o caminho definitivo deve ser via `10.3.74.1`.
2. **Cabeamento dos telefones**: confirmar que os aparelhos estão pendurados no switch (lado `ether2`).
   Se estavam nas portas próprias do RB antigo, precisam ser recabeados para o switch.
3. **Bridge transparente ONU↔switch** (`combo1`↔`ether2`) permaneceu intocada durante toda a migração.
4. RB750r2 antigo desligado — pode ser recolhido; sua chave VOIP já foi revogada no hub.
