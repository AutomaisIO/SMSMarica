# Controle de uso da internet — Complexo Regulador (id=0)

> **Data:** 29/07/2026 · **Equipamento:** CCR1009 `MK-BORDA-ONU`
> **Acesso:** `10.35.0.23` (interface `automais-vpn`), usuário `admin`
> **Motivo:** o Starlink da unidade consumiu **50 GB num dia** e estourou a franquia.
> **Resultado:** consumo caiu de **8,85 GB/h para ~1,4 GB/h** (−84%), medido em blocos de 15 min.

## ⚠️ ESTADO ATUAL: TUDO REMOVIDO (29/07/2026, 16h)

**Às 16:10 o link da Prefeitura voltou** (`combo1` up), o script `failover-eth3` desativou o
failover sozinho em 45 s, e o CCR voltou a ser **bridge L2 transparente**. Com isso o tráfego dos
clientes deixou de passar pelo firewall IP do MK e **todos os filtros ficaram inertes
automaticamente** — eles são escopados a `src-address=10.3.74.0/24` na chain `forward`.

Por decisão do operador (*"a Prefeitura cuida do proxy"*), **os filtros foram removidos por
completo**: 100 regras `BLOQ`, 12 regras `WL/WL2`, 78 entradas de DNS estático e 669 IPs da
address-list `LIBERADOS`. Sobraram apenas as 3 regras de VOIP originais.

**O que permanece no equipamento:** as rotas `OFFLOAD` (EDR e `tcp/7615` pela Connect), inertes
enquanto a Prefeitura estiver de pé e reativadas sozinhas em caso de novo failover.

**Este documento continua valendo como playbook**: se a unidade cair em failover de novo e o
consumo do Starlink voltar a ser problema, é só reaplicar o que está descrito abaixo. Os backups
`antes-offload-agentes`, `antes-bloqueio-social`, `antes-whitelist`, `antes-deny-ip` e
`antes-blacklist` estão salvos no MK.

> **Lição principal do dia:** a lista branca **foi abandonada em produção** porque gerou reclamação
> demais — um site autorizado carrega assets, uploads e downloads de **outros** domínios (CDN), e
> cada um vira um bloqueio invisível. Para reaplicar, prefira **lista negra por domínio**
> (redes sociais, vídeo, backup em nuvem), que foi o desenho final antes da remoção.

## Contexto — por que o CCR está no caminho

A unidade está **fora do link da Prefeitura** (`combo1` inativo) e navega por **Starlink**
(`ether7`, gw `192.168.1.1`), com a **Connect** (`ether3`) como segundo link. Nesse modo o CCR
deixa de ser bridge L2 transparente e vira o **roteador real**: assumiu `10.3.74.1` (gateway),
`10.1.201.254` (DHCP) e intercepta todo `:53` da LAN. Por isso o tráfego dos ~64 dispositivos
passa pela chain `forward` e pode ser filtrado — **não foi preciso mexer em `use-ip-firewall`**.

Decisão do operador: *"quando estiver fora da Prefeitura, será sempre assim."* Todas as regras
são escopadas a `src-address=10.3.74.0/24`, então ficam **naturalmente inertes** se a Prefeitura
voltar e o CCR sair do caminho.

## Desenho em quatro camadas

| # | Camada | Comentário no MK | O que faz |
|---|--------|------------------|-----------|
| 1 | Offload de agentes | `OFFLOAD` | tira tráfego de máquina do Starlink, joga na Connect |
| 2 | Bloqueio dirigido | `BLOQ` | redes sociais e vídeo, por DNS + SNI + QUIC |
| 3 | Whitelist (fonte) | `WL liberado` | entradas de DNS que **alimentam** a address-list `LIBERADOS` |
| 4 | Deny por IP | `WL2` | aceita `LIBERADOS` e rejeita o resto, com log |

### 1. Offload — o que não precisa de Starlink sai pela Connect

A Connect é **ilimitada** e estava ociosa. Agentes de máquina (upload contínuo, sem usuário
esperando) foram desviados para lá — preserva o serviço e libera o link caro.

| Fluxo | Como é roteado |
|---|---|
| Agente **Wazuh** (`52.2.196.40:1514`, 21 estações) | rota `/32` via `192.168.0.1`, `check-gateway=ping` |
| Agente não identificado `tcp/7615` (8 estações, destinos variados) | tabela `via-connect` + `mark-routing` por porta |

Ambos têm **fallback automático para o Starlink** se a Connect cair. Ganho medido: **−3,5 Mbps de
upload** no Starlink (~1,4 GB/h).

### 2. Bloqueio dirigido de redes sociais e vídeo

Três camadas somadas, porque cada uma sozinha vaza:

- **DNS sinkhole** (`/ip dns static` → `127.0.0.1`): 32 domínios;
- **SNI** (`tls-host` → `reject tcp-reset`): 64 regras, pega quem usa DNS externo;
- **QUIC** (`drop udp/443`): sem isso o YouTube ignora o SNI — a regra descartou **39 mil pacotes**
  na primeira hora.

> **Armadilha:** bloquear `facebook.com` não cobre `facebook.net`, e bloquear `googlevideo.com`
> não cobre os servidores de vídeo em `gvt1.com`. Os dois furos apareceram em produção. O `gvt1`
> é tratado por **regexp** `rr.*gvt1.com` — bloqueia o vídeo sem derrubar `redirector.gvt1.com`
> nem `dl.google.com`, que servem atualização do Chrome.

### 3 + 4. Whitelist: DNS alimenta, firewall decide

O ponto central do desenho, e o que levou mais tentativa e erro:

```
/ip dns static  name=<dominio> match-subdomain=yes type=FWD forward-to=8.8.8.8 address-list=LIBERADOS
        ↓ (o MK adiciona sozinho os IPs que responde às consultas dos clientes)
/ip firewall address-list  list=LIBERADOS   (dinâmica, ~700 IPs)
        ↓
/ip firewall filter  accept dst-address-list=LIBERADOS ... reject o resto + log
```

Ordem das regras `WL2` na chain `forward` (todas com `src-address=10.3.74.0/24`):

1. `accept` `connection-state=established,related`
2. `accept` `dst-address-list=RFC1918` — rede interna, impressoras
3. `accept` `protocol=icmp`
4. `accept` `udp/123` — NTP; sem relógio, TLS começa a falhar
5. `accept` `dst-address=52.2.196.40` — agente Wazuh
6. `accept` `dst-address-list=LIBERADOS`
7. **`reject` + `log-prefix="WL-DENY"`** — o deny

## Lista liberada vigente

| Grupo | Entradas |
|---|---|
| Saúde / governo | `gov.br` (cobre SISREG, SER, painel, sistemas, cadastro, e-SUS Maricá, Niterói) |
| | `esusmais.com.br` (e-SUS Mais São Gonçalo, **porta 8000** — o deny é por destino, não por porta) |
| | `smsmarica.online` |
| WhatsApp | `whatsapp.com`, `whatsapp.net` |
| IA | `chatgpt.com`, `openai.com`, `oaistatic.com`, `oaiusercontent.com` |
| Google (só Gmail + Gemini) | `mail.google.com`, `accounts.google.com`, `gemini.google.com`, `gstatic.com`, `googleusercontent.com`, `googleapis.com` |
| Infraestrutura | `ntp.br`, `pool.ntp.org`, `digicert.com`, `sectigo.com`, `lencr.org`, `globalsign.com`, `entrust.net`, `amazontrust.com`, `pmm.local`, `in-addr.arpa` |

**Fora por decisão explícita:** Microsoft (inclusive **Windows Update e Defender**), Google amplo,
redes sociais, vídeo, Apple/iCloud, anúncios.

> ⚠️ Sem os domínios da Microsoft, as estações **não recebem mais correção de segurança do Windows**.
> Foi decisão consciente do operador em 29/07 — reavaliar.

## Bypass fechado

Canais que furam qualquer filtro e foram bloqueados na chain `forward`:

| Porta | O que é | Observado |
|---|---|---|
| `tcp/853` | DNS over TLS | 2 estações; uma usava **AdGuard DNS** (`94.140.14.14`) — provável causa de "bloqueios indesejados e instáveis" relatados |
| `udp/51820` | WireGuard | 1 celular com VPN pessoal para `datapacket.com` |
| `udp/500,4500` | IPsec | preventivo |
| `tcp/1194` | OpenVPN | preventivo |

O túnel VOIP **não é afetado** — ele sai do próprio roteador (chain `output`), não da LAN.

## Operação

**Liberar um domínio quando alguém reclamar:**

```
/ip dns static add name=<dominio> match-subdomain=yes type=FWD forward-to=8.8.8.8 \
    address-list=LIBERADOS comment="WL liberado"
```

O IP entra na lista assim que o primeiro cliente resolver o nome. Não precisa de `place-before` —
não existe mais catch-all de DNS; quem decide é a address-list no firewall.

**Ver o que está sendo negado:**

```
/log print where message~"WL-DENY"
```

O log mostra **IP e MAC de origem e o destino real**. O nome sai cruzando o destino com
`/ip dns cache`.

**Reverter:**

```
/ip firewall filter disable [find comment~"WL2"]     # solta o deny
/ip firewall filter disable [find comment~"BLOQ"]    # solta redes sociais/vídeo
/ip route disable [find comment~"OFFLOAD"]           # devolve agentes ao Starlink
```

Backups no equipamento: `antes-offload-agentes`, `antes-bloqueio-social`, `antes-whitelist`,
`antes-deny-ip` (`.backup` + `.rsc`).

## Resultado medido

Blocos de 15 min, contador da interface (exato):

| Janela | Download | Upload | Estado |
|---|---|---|---|
| 12:15 | 605 MB | 443 MB | sem nada |
| 12:45 | 787 MB | 85 MB | offload dos agentes |
| 14:19 | 923 MB | 817 MB | pico — dois iPhones em backup |
| 14:49 | 335 MB | 129 MB | deny por IP ativo |
| 15:04 | 335 MB | 129 MB | regime estável |

**8,85 GB/h → 1,86 GB/h.** Numa jornada de 8h: **~70 GB → ~15 GB**.

## Armadilhas encontradas (não repetir)

1. **`tls-host` não serve para lista branca.** Ele só casa no pacote do `ClientHello`, que chega
   *depois* do handshake TCP — o SYN seria rejeitado antes. Serve muito bem para lista **negra**.
2. **`:resolve` do console não popula a `address-list`.** Testes com `:resolve` deram 0/5 e 2/12,
   sugerindo que o mecanismo não funcionava. Com **tráfego real de cliente** a lista chegou a
   **732 IPs de 348 nomes**, cobrindo CDN de Akamai, msedge e cloudapp.azure. Validar sempre com
   tráfego real.
3. **Ordem importa no `/ip dns static`.** Entrada específica só vence um `regexp` catch-all se
   estiver **antes** dele na lista.
4. **Escape no RouterOS:** `$` em `regexp=` quebra (`expected regexp value`). Usar padrão sem
   âncora — `rr.*gvt1.com` em vez de `^rr.*\.gvt1\.com$`.
5. **Sessões SSH:** o MK encerra sessão ociosa e tem `max-sessions=20`. Agrupar comandos com `;`
   num único canal; nunca dormir dentro de uma sessão aberta.
6. **A tabela de conexões traz bytes acumulados** desde a abertura da conexão (timeout 24h). Para
   medir uma janela é preciso o **delta** entre duas amostras — somar o valor cru infla ~4×.

## Pendências

- **Cabo do Starlink**: `ether7` acumula **126 erros de FCS** (crescendo) e **63 quedas de link**
  em 4 dias, contra 1 da `ether3`. Negocia a 100 Mbps porque o lado do Starlink só anuncia 100M.
  Trocar o cabo e reassentar conectores; se os erros persistirem, o problema é o adaptador.
  É a causa provável da instabilidade relatada — **não é MTU** (medido 1500 limpo nos dois links).
- **Agentes não identificados**: `tcp/7615` (8 estações) e `tcp/28575`. Sem PTR e sem nome no DNS;
  só dá para identificar olhando uma das estações.
- **Dono do Wazuh**: 21 estações enviam log para um EC2 na AWS. Presumido serviço da Prefeitura,
  não confirmado. Volume alto (~1,4 GB/h) sugere coleta verbosa demais.
- **Inversão de roteamento** (Connect como saída padrão, Starlink só para os sistemas de trabalho):
  discutida e não aplicada. Resolveria o consumo sem depender de classificar tráfego.
