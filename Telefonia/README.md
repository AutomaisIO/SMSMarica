# Telefonia SMS Maricá — VPN VOIP (WireGuard hub-and-spoke)

Rede privada que liga os **MikroTik de cada unidade** a um **servidor central Asterisk**
(`192.241.153.121`, host `FALARMAIS-HOSPITAIS`) por túneis **WireGuard**. Cada unidade
recebe uma **faixa /24 exclusiva** para os telefones, de forma que, na central, o IP de
origem já diz **de qual unidade** é cada aparelho.

```
   Telefones (IP fixo 10.200.<id>.x)          ┌──────────────────────────────┐
   gw = 10.200.<id>.1  ──►  MikroTik ─wg─►    │  HUB  192.241.153.121        │
                            (10.201.0.<id+10>)│  Asterisk (SIP 5060)         │
                                              │  wg0 = 10.201.0.1            │
   ...outra unidade... ──► MikroTik ─wg─►     │  rota 10.200.<id>.0/24 → peer│
                                              └──────────────────────────────┘
```

## Princípios de projeto

1. **Uma /24 por unidade** em `10.200.<id>.0/24`. O `id` é o mesmo em todo lugar
   (LAN dos telefones **e** IP do túnel), então "quem é quem" é dedutível de cabeça.
2. **Sem NAT no caminho do telefone → PBX.** O Asterisk enxerga o **IP real** do aparelho
   (`10.200.<id>.x`) → dá pra saber a unidade pelo 3º octeto. Este é o requisito central.
3. **Unidades isoladas entre si.** O hub **não** roteia unidade↔unidade (`FORWARD wg0→wg0
   DROP`). Chamada interna entre unidades passa pelo PBX (mídia via Asterisk,
   `directmedia=no`). Mais seguro e mais simples.
4. **Telefones são VOIP-only.** O MikroTik só deixa a faixa dos telefones falar com o hub
   pelo túnel; o resto é bloqueado (não navegam na internet).
5. **Aditivo e não-destrutivo.** O WireGuard sobe ao lado do Asterisk existente sem tocar
   em SIP, fail2ban ou rotas atuais.

## Estrutura

| Caminho | O quê |
|---------|-------|
| [`PABX/`](PABX/) | **Automais.Pabx** — serviço .NET no servidor VOIP: CRUD de ramais, provisionamento XML (TFTP), status via AMI e CDR. API + página local; futuro menu Telefonia do SMSMarica consome esta API |
| [`docs/plano-enderecamento.md`](docs/plano-enderecamento.md) | **Plano de IP + tabela "quem é quem"** (a fonte pra leitura humana) |
| [`docs/servidor-wireguard-hub.md`](docs/servidor-wireguard-hub.md) | Como o hub foi montado e como operar |
| [`docs/mikrotik-unidade.md`](docs/mikrotik-unidade.md) | Passo a passo pra plugar **uma** unidade nova (RouterOS v7) |
| [`registro/unidades.csv`](registro/unidades.csv) | **Registro-mestre** (id, nome, LAN, IP túnel, chave pública do MK, status) |
| [`scripts/hub-install.sh`](scripts/hub-install.sh) | Instala/configura o WireGuard no servidor (rodar 1× no hub) |
| [`scripts/hub-add-unidade.sh`](scripts/hub-add-unidade.sh) | Registra o peer de uma unidade nova no hub |
| [`scripts/mikrotik-gen.sh`](scripts/mikrotik-gen.sh) | Gera o script RouterOS pronto de uma unidade (colar no MK) |

## Fluxo pra ligar uma unidade nova (resumo)

1. Escolha o `id` da unidade em [`registro/unidades.csv`](registro/unidades.csv) (já pré-alocado).
2. No **MikroTik**: `scripts/mikrotik-gen.sh <id>` → cole o script no RouterOS → copie a
   **public-key** que o MK gerou.
3. No **hub**: `hub-add-unidade.sh --id <id> --pubkey <PUBKEY_DO_MK>` → registra o peer.
4. Confira `wg show` no hub (handshake) e um telefone registrando no Asterisk.
5. Atualize a linha da unidade no `registro/unidades.csv` (pubkey + status `ATIVA`).

> Detalhe completo em cada doc. **Chaves privadas nunca saem do dispositivo** — o MK gera a
> dele, o hub gera a dele; só as **públicas** trafegam.
