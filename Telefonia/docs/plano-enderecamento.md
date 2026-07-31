# Plano de endereçamento — Telefonia VOIP

> **Fonte de verdade legível.** O arquivo processável é [`../registro/unidades.csv`](../registro/unidades.csv).
> A regra de alocação é **determinística**: sabendo o `id` da unidade, você deriva TODOS os IPs.

## Regra de alocação (decorar isto basta)

| Plano | Fórmula | Exemplo (id = 6, CDT) |
|-------|---------|------------------------|
| **LAN dos telefones** | `10.200.<id>.0/24` | `10.200.6.0/24` |
| **Gateway (o MikroTik)** | `10.200.<id>.1` | `10.200.6.1` |
| **Faixa dos aparelhos** | `10.200.<id>.10` … `10.200.<id>.200` | `10.200.6.10–.200` |
| **IP do MK no túnel** | `10.201.0.<id + 10>` | `10.201.0.16` |
| ↳ logo, `id = (4º octeto do túnel) − 10` | | `16 − 10 = 6` |

- **`.2`–`.9`** de cada LAN ficam reservados (impressoras/ATA/infra local, se precisar).
- **Hub (servidor Asterisk):** `wg0 = 10.201.0.1` — é o SIP server que os telefones registram.
- **Sub-rede do túnel:** `10.201.0.0/24` (comporta 244 unidades, `.10`–`.253`).
- **Espaço das LANs:** `10.200.0.0/16` (comporta `id` 0–255).

### Por que assim
- **Sem NAT** telefone→PBX: o Asterisk vê `10.200.<id>.x` e sabe a unidade pelo 3º octeto.
- **`id` único** nos dois planos (LAN e túnel) → ao abrir um MK novo, o técnico deduz tudo.
- Não colide com as redes já usadas no servidor (`10.10.0.0/16`, `10.116.0.0/20`, `192.241.153.0/24`).

## Faixas reservadas (não usar pra unidade)

| Faixa | Uso |
|-------|-----|
| `10.201.0.1` | Hub / Asterisk (túnel) |
| `10.201.0.2`–`10.201.0.9` | Reserva de infra do hub (futuros serviços centrais) |
| `10.200.255.0/24` | Reserva (testes/laboratório) |

## Tabela "quem é quem"

> Grupos: **ESPECIALIZADA** (ambulatórios/centros) e **RUE** (rede de urgência: hospital + UPAs).
> `status` e `mk_pubkey` reais ficam no CSV; aqui é o mapa de rede.

| id | Unidade | Grupo | LAN telefones (/24) | Gateway (MK) | Faixa aparelhos | IP túnel (MK) |
|---:|---------|:-----:|---------------------|--------------|-----------------|---------------|
| 0 | COMPLEXO REGULADOR | ESPECIALIZADA | `10.200.0.0/24` | `10.200.0.1` | `10.200.0.10 - 10.200.0.200` | `10.201.0.10` |
| 1 | Ambulatório Péricles Siqueira Pereira | ESPECIALIZADA | `10.200.1.0/24` | `10.200.1.1` | `10.200.1.10 - 10.200.1.200` | `10.201.0.11` |
| 2 | Central de Assistência Farmacêutica - CAF | ESPECIALIZADA | `10.200.2.0/24` | `10.200.2.1` | `10.200.2.10 - 10.200.2.200` | `10.201.0.12` |
| 3 | CAPS III | ESPECIALIZADA | `10.200.3.0/24` | `10.200.3.1` | `10.200.3.10 - 10.200.3.200` | `10.201.0.13` |
| 4 | CAPS AD (Álcool e Drogas) | ESPECIALIZADA | `10.200.4.0/24` | `10.200.4.1` | `10.200.4.10 - 10.200.4.200` | `10.201.0.14` |
| 5 | CAPSI (Infantojuvenil) | ESPECIALIZADA | `10.200.5.0/24` | `10.200.5.1` | `10.200.5.10 - 10.200.5.200` | `10.201.0.15` |
| 6 | Centro de Diagnóstico e Tratamento - CDT | ESPECIALIZADA | `10.200.6.0/24` | `10.200.6.1` | `10.200.6.10 - 10.200.6.200` | `10.201.0.16` |
| 7 | CEO Boqueirão (Odonto) | ESPECIALIZADA | `10.200.7.0/24` | `10.200.7.1` | `10.200.7.10 - 10.200.7.200` | `10.201.0.17` |
| 8 | CEO Itaipuaçu (Odonto) | ESPECIALIZADA | `10.200.8.0/24` | `10.200.8.1` | `10.200.8.10 - 10.200.8.200` | `10.201.0.18` |
| 9 | CRAD (Reabilitação) | ESPECIALIZADA | `10.200.9.0/24` | `10.200.9.1` | `10.200.9.10 - 10.200.9.200` | `10.201.0.19` |
| 10 | Centro Materno Infantil - CMI | ESPECIALIZADA | `10.200.10.0/24` | `10.200.10.1` | `10.200.10.10 - 10.200.10.200` | `10.201.0.20` |
| 11 | CEREST | ESPECIALIZADA | `10.200.11.0/24` | `10.200.11.1` | `10.200.11.10 - 10.200.11.200` | `10.201.0.21` |
| 12 | CIEVIS | ESPECIALIZADA | `10.200.12.0/24` | `10.200.12.1` | `10.200.12.10 - 10.200.12.200` | `10.201.0.22` |
| 13 | CVI | ESPECIALIZADA | `10.200.13.0/24` | `10.200.13.1` | `10.200.13.10 - 10.200.13.200` | `10.201.0.23` |
| 14 | Gerência Administrativa | ESPECIALIZADA | `10.200.14.0/24` | `10.200.14.1` | `10.200.14.10 - 10.200.14.200` | `10.201.0.24` |
| 15 | Núcleo de Imunização | ESPECIALIZADA | `10.200.15.0/24` | `10.200.15.1` | `10.200.15.10 - 10.200.15.200` | `10.201.0.25` |
| 16 | SAD (Atendimento Domiciliar) | ESPECIALIZADA | `10.200.16.0/24` | `10.200.16.1` | `10.200.16.10 - 10.200.16.200` | `10.201.0.26` |
| 17 | SAE (Atendimento Especializado) | ESPECIALIZADA | `10.200.17.0/24` | `10.200.17.1` | `10.200.17.10 - 10.200.17.200` | `10.201.0.27` |
| 18 | SRT I - Araçatiba | ESPECIALIZADA | `10.200.18.0/24` | `10.200.18.1` | `10.200.18.10 - 10.200.18.200` | `10.201.0.28` |
| 19 | SRT II - Centro | ESPECIALIZADA | `10.200.19.0/24` | `10.200.19.1` | `10.200.19.10 - 10.200.19.200` | `10.201.0.29` |
| 20 | SRT III - Centro | ESPECIALIZADA | `10.200.20.0/24` | `10.200.20.1` | `10.200.20.10 - 10.200.20.200` | `10.201.0.30` |
| 21 | TFD (Transporte Fora do Domicílio) | ESPECIALIZADA | `10.200.21.0/24` | `10.200.21.1` | `10.200.21.10 - 10.200.21.200` | `10.201.0.31` |
| 22 | Transporte Sanitário | ESPECIALIZADA | `10.200.22.0/24` | `10.200.22.1` | `10.200.22.10 - 10.200.22.200` | `10.201.0.32` |
| 23 | Unidade Móvel de Odontologia (itinerante) | ESPECIALIZADA | `10.200.23.0/24` | `10.200.23.1` | `10.200.23.10 - 10.200.23.200` | `10.201.0.33` |
| 24 | Unidade Móvel de Odontologia - Clube Maricá | ESPECIALIZADA | `10.200.24.0/24` | `10.200.24.1` | `10.200.24.10 - 10.200.24.200` | `10.201.0.34` |
| 25 | Vigilância Epidemiológica/Sanitária/Ambiental | ESPECIALIZADA | `10.200.25.0/24` | `10.200.25.1` | `10.200.25.10 - 10.200.25.200` | `10.201.0.35` |
| 26 | Hospital Municipal Conde Modesto Leal - HMCML | RUE | `10.200.26.0/24` | `10.200.26.1` | `10.200.26.10 - 10.200.26.200` | `10.201.0.36` |
| 27 | UPA Inoã | RUE | `10.200.27.0/24` | `10.200.27.1` | `10.200.27.10 - 10.200.27.200` | `10.201.0.37` |
| 28 | UPA Ponta Negra | RUE | `10.200.28.0/24` | `10.200.28.1` | `10.200.28.10 - 10.200.28.200` | `10.201.0.38` |
| 29 | Posto Santa Rita | RUE | `10.200.29.0/24` | `10.200.29.1` | `10.200.29.10 - 10.200.29.200` | `10.201.0.39` |
| 30 | SAMU Ponta Negra | RUE | `10.200.30.0/24` | `10.200.30.1` | `10.200.30.10 - 10.200.30.200` | `10.201.0.40` |

## Como adicionar uma unidade fora da lista (id ≥ 30)

1. Pegue o **próximo `id` livre** (30, 31, …) — os IPs saem da fórmula acima.
2. Acrescente a linha no [`../registro/unidades.csv`](../registro/unidades.csv).
3. Siga [`mikrotik-unidade.md`](mikrotik-unidade.md) + `hub-add-unidade.sh`.

### Caso especial: id 30 (SAMU Ponta Negra) — MK pré-existente, sem acesso direto

Primeiro caso de unidade **fora do rollout hEX**: o MK já existia antes do projeto Telefonia
(RouterBOARD **RB3011UiAS**, identity `SAMU-PTN`), atendendo outra rede local (`10.106.0.0/24`,
com telefones Cisco próprios via DHCP option 150 — sistema à parte, não mexido) e **sem IP
público próprio alcançável por nós**: só é acessível de fora através de um túnel **L2TP já
existente com o Router Conde** (`175.10.10.1` Conde ↔ `175.10.10.2` SAMU-PTN, interface
`l2tp-SAMUPTN`/`l2tp-out1`, caller-id do SAMU muda por ser round-trip via internet do próprio
SAMU). Para gerir esse MK pela primeira vez foi necessário, na plataforma automais.io:
- Registrar `175.10.10.2/32` como **Remote Network** do peer do Router Conde (rede
  `10.35.0.0/24`, VpnPeerId do Conde) — sem isso o server automais.io não tinha rota.
- Criar uma regra de **firewall Input** no device Conde (`TargetScope=SpecificRemoteIp`,
  `TargetValue=175.10.10.2`) liberando o tráfego de gestão.
- Adicionar no **Conde** (não no SAMU) uma regra `srcnat masquerade dst-address=175.10.10.2`
  **escopada só a esse destino** — o MK do SAMU só aceita tráfego cuja origem seja o próprio
  Conde (`175.10.10.1`); sem essa regra o forward chegava lá mas a resposta nunca voltava.

O MK estava em **RouterOS 6.49.20 (long-term)** — sem suporte a WireGuard. Upgrade em
**duas etapas** (pulo direto de v6 pra v7 mais recente não é confiável): primeiro
**7.1.5** (primeira release estável da série v7), reboot, valida; depois **7.23.2**
(mesma versão já usada no resto da frota hEX), reboot, valida. As duas etapas rodaram sem
perda de configuração (backup `/system backup save` tirado antes + cópia extra puxada para
fora do equipamento). Depois do upgrade: `wg-voip` criado normalmente (peer no hub, handshake OK 10.201.0.40) +
`hub-add-unidade.sh --id 30 --pubkey ... --nome "SAMU Ponta Negra"`; e `automais-vpn`
cadastrado no tenant Saúde Maricá como `ManagedDevice` Kind=MikrotikRouter ("Router SAMU
Ponta Negra"), bootstrap via `/tool fetch` + `/import` (funcionou normal, esse MK não tem a
restrição de device-mode dos hEX novos), VPN IP `10.35.0.49`, device Online e com API
RouterOS autenticando (`apiAuthStatus=Ok`) minutos depois. Nada mais no MK foi alterado
(LAN/DHCP/telefonia Cisco existentes ficaram intocados).
