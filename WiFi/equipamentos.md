# Catálogo de equipamentos — enlace de rádio SMS Maricá

Todos os modelos avaliados para o projeto, com especificações, prós/contras e compatibilidade. Valores de banda são **reais/agregados aproximados** (não o número de marketing PHY); preços são **tabela EUA sem impostos** (importado no Brasil costuma dar 2–2,5×). Specs de Wave conferidas em techspecs.ui.com em 29/07/2026.

## Como a chuva afeta cada faixa

| Faixa | Atenuação em chuva tropical forte (~100 mm/h) | Efeito prático |
|---|---|---|
| 5 GHz | < 0,1 dB/km — desprezível | Enlace estável em qualquer clima |
| 11 GHz (licenciada) | ~1,5–2 dB/km | Projetável com margem; padrão de operadora |
| 60 GHz | ~10–30 dB/km **+ ~15 dB/km de absorção de oxigênio (sempre)** | Enlace curto (≤ 2–3 km) sente pouco; enlace longo **cai para o rádio backup** durante o temporal |

Regra de bolso: **60 GHz = banda enorme e espectro vazio, mas só é "seguro" sozinho em salto curto**. Os equipamentos Wave contornam isso com um segundo rádio 5 GHz embutido que assume automaticamente (o enlace degrada, não cai).

---

## Família Ubiquiti Wave (60 GHz + backup 5 GHz integrado)

Plataforma proprietária. Todos têm failover automático para 5 GHz WiFi 6, gestão UISP e GPS.

### Wave Pro — ~US$ 999
PtP de longa distância. 60 GHz (57–71 GHz), canal até 2160 MHz, antena integrada 46 dBi com feixe de 1,3°.
- **Banda:** 5,4 Gbps agregado (2,5 Gbps full-duplex) · backup 5 GHz 22 dBi (centenas de Mbps)
- **Alcance:** PtP até 15 km · como cliente PtMP até 8 km
- ✅ O rádio não-licenciado mais rápido da linha; feixe fino = imune a interferência; failover embutido
- ❌ Caro; em salto de 9–12 km a chuva forte derruba para o backup com frequência; feixe de 1,3° exige alinhamento e torre rígida (oscilação de mastro desalinha)

### Wave AP — ~US$ 1.299
AP PtMP de setor **estreito (30° azimute)**, 5,4 Gbps agregado, até 31 clientes.
- **Alcance por cliente:** Wave LR/Pro 8 km · Nano 5 km · Pico 1,3 km
- ✅ Maior alcance PtMP da família
- ❌ Setor de 30° cobre pouco — para arcos largos multiplica APs; para este projeto o AP Micro serve melhor

### Wave AP Micro — ~US$ 499
AP PtMP de setor **largo (90°)**, antena 20 dBi. 5 Gbps agregado (2,5 duplex), 15–31 clientes (firmware atual).
- **Alcance por cliente:** Wave LR/Pro 6 km · Nano 4 km
- **Backup:** 5 GHz WiFi 6, 800+ Mbps
- ✅ Melhor custo/cobertura para célula urbana; gigabit por ponta a ≤ 4 km; chuva quase não afeta em célula curta
- ❌ Alcance menor que o Wave AP; 60 GHz exige visada totalmente limpa (nem galho de árvore)

### Wave LR — ~US$ 599
Estação de longo alcance; funciona como **cliente PtMP (até 8 km)** ou **PtP par-a-par** (desde o firmware 3.2).
- **Banda:** 2 Gbps agregado · backup 5 GHz
- ✅ Resolve saltos de 4–8 km onde o Nano não chega; serve de PtP dedicado barato
- ❌ Em PtP longo (6–8 km) a chuva forte joga para o backup; menos banda que o Wave Pro

### Wave Nano — ~US$ 249
CPE padrão da família. Antena 60 GHz 41 dBi + backup 5 GHz 19 dBi.
- **Banda:** 2 Gbps agregado · **Alcance:** até 5 km (4 km com AP Micro)
- ✅ Gigabit real na ponta por preço de CPE; instalação simples (alinhamento assistido no app)
- ❌ Só fala com APs Wave; sem visada perfeita não fecha enlace

*(Existe ainda o Wave Pico, cliente econômico de até 1,3 km — não considerado: as pontas do projeto ficam melhor servidas pelo Nano.)*

---

## Ubiquiti 5 GHz TDMA (airFiber / LTU)

Rádios proprietários em 5 GHz — **imunes a chuva** — com TDMA (sem colisão, latência estável com muitos clientes). Gestão UISP.

### airFiber 5XHD — ~US$ 649
PtP "carrier class" em 5 GHz com tecnologia LTU, canal até 100 MHz, GPS sync (permite reuso de frequência entre setores).
- **Banda:** ~1 Gbps real · **Alcance:** até 100 km (fecha 9–12 km com folga enorme)
- ✅ Tronco que **não degrada nunca** com clima; filtragem forte contra interferência; a escolha "dorme tranquilo"
- ❌ Teto de ~1 Gbps; usa espectro 5 GHz compartilhado com Wi-Fi da cidade (mitigado por GPS sync + canal limpo); PtP apenas

### LTU Rocket — ~US$ 299 (+ setorial airMAX ~US$ 150)
AP PtMP 5 GHz da linha LTU, antena conectorizada (usa setorial airMAX de 60°/90°/120°).
- **Banda:** até ~600 Mbps por cliente (canal 50 MHz) · TDMA proprietário
- ✅ PtMP 5 GHz mais rápido do mercado não-licenciado; aguenta dezenas de clientes; imune a chuva
- ❌ Não interopera com airMAX AC nem com Wave; precisa comprar a setorial separada

### LTU LR — ~US$ 179
CPE de longo alcance da linha LTU, prato integrado de alto ganho.
- **Banda:** 300–600 Mbps · **Alcance:** dezenas de km (cobre de 0,12 a 7,6 km deste projeto com folga)
- ✅ Uma CPE única para todas as distâncias do projeto; imune a chuva; barata
- ❌ Sem gigabit; só fala com APs LTU (Rocket)

---

## MikroTik (opção econômica)

RouterOS/RouterBOARD — integra com o automais.io já usado no rollout. TDMA próprio (Nv2).

### NetMetal ax + prato mANT30 — ~US$ 249 + US$ 115
Rádio PtP 802.11ax conectorizado + prato 30 dBi.
- **Banda:** ~400–700 Mbps reais em canal 80 MHz · **Alcance:** 10–15+ km
- ✅ Melhor PtP MikroTik; imune a chuva; RouterOS completo (o rádio é também roteador)
- ❌ Metade da banda do AF-5XHD na prática; 802.11 mesmo com Nv2 sofre mais com interferência que LTU

### mANTBox 19s — ~US$ 149
AP setorial integrado: antena 120° 19 dBi + rádio 5 GHz ac.
- **Banda:** ~200–400 Mbps agregado por setor (dividido entre clientes) · Nv2 TDMA
- ✅ Setor largo e barato; um produto só (antena+rádio)
- ❌ Geração ac; agregado modesto para 17 clientes com tráfego de imagem

### LHG 5 ac — ~US$ 89 · LHG XL 5 ac — ~US$ 99
CPE/PtP de grade: 24,5 dBi (LHG) / 27 dBi (XL).
- **Banda:** ~200–300 Mbps · **Alcance:** LHG até ~10 km, XL um pouco mais
- ✅ Muito barato; leve; ganho alto ajuda em folga apertada
- ❌ Banda limitada; grade aberta acumula menos vento mas exige bom aterramento

### SXTsq 5 ac — ~US$ 49
CPE painel 16 dBi para curta distância (≤ 2–3 km).
- ✅ O menor custo por ponta do mercado
- ❌ ~200 Mbps; só para célula curta

### Cube Pro 60 (Wireless Wire Cube Pro) — ~US$ 249
PtP 60 GHz (802.11ad) com failover 5 GHz embutido.
- **Banda:** ~1 Gbps agregado · **Alcance:** até ~2 km
- ✅ Gigabit barato em salto curto; espectro vazio
- ❌ Só PtP curto; 802.11ad não fala com a família Wave da Ubiquiti

### OmniTik 5 ac — ~US$ 95
AP omnidirecional 7,5 dBi com 5 portas Ethernet.
- ✅ 360° num produto só; serve célula pequena (≤ 2–3 km) com poucas pontas
- ❌ Ganho baixo + agregado dividido entre todos — não recomendado para 17 pontas com tráfego sério

---

## Matriz de compatibilidade

**Regra geral: rádio só fala com rádio da MESMA família/protocolo.** Entre famílias diferentes a integração é sempre via cabo Ethernet no mesmo site (ex.: um Wave Pro entrega o tronco num switch e um LTU Rocket distribui dali).

| | Wave (UI 60G) | airFiber 5XHD | LTU | airMAX AC | MikroTik 5G (Nv2) | MikroTik 60G (11ad) |
|---|---|---|---|---|---|---|
| **Wave (UI 60G)** | ✅ AP↔cliente e PtP dentro da família | ❌ | ❌ | ❌ | ❌ | ❌ |
| **airFiber 5XHD** | ❌ | ✅ par fechado (mesmo modelo nas 2 pontas) | ⚠️ mesma tecnologia LTU, mas trate cada enlace como par do mesmo modelo | ❌ | ❌ | ❌ |
| **LTU (Rocket/LR)** | ❌ | ⚠️ | ✅ Rocket (AP) ↔ LTU clientes | ❌ | ❌ | ❌ |
| **airMAX AC** | ❌ | ❌ | ❌ | ✅ | ⚠️ só em modo 802.11 padrão, perdendo TDMA | ❌ |
| **MikroTik 5G** | ❌ | ❌ | ❌ | ⚠️ | ✅ Nv2 entre MikroTiks | ❌ |
| **MikroTik 60G** | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

Gestão: toda a linha Ubiquiti (Wave, airFiber, LTU) entra no **UISP**; toda a linha MikroTik entra no **RouterOS/automais.io**. Misturar fabricantes = dois painéis de gestão.

## Resumo de decisão rápida

| Necessidade | Melhor escolha | Alternativa |
|---|---|---|
| Tronco 9–12 km com máxima banda | Wave Pro (5,4 Gbps, degrada na chuva) | — |
| Tronco 9–12 km que nunca degrada | airFiber 5XHD (~1 Gbps) | NetMetal ax + mANT30 (mais barato, ~½ da banda) |
| Célula urbana ≤ 4 km, gigabit por ponta | Wave AP Micro + Wave Nano | LTU Rocket + LTU LR (imune a chuva, sem gigabit) |
| Salto PtP 4–8 km | par Wave LR (2 Gbps c/ failover) | LTU (par Rocket+LR) ou LHG XL 5 ac |
| Salto PtP ≤ 2 km gigabit barato | Cube Pro 60 (MikroTik) | Wave Nano ↔ AP |
| Ponta de custo mínimo | SXTsq 5 ac | LHG 5 ac |

## Fontes

- [Wave Pro — Tech Specs](https://techspecs.ui.com/uisp/60ghz-wireless/wave-pro) · [Wave AP — Tech Specs](https://techspecs.ui.com/uisp/60ghz-wireless/wave-ap) · [Wave Nano — Tech Specs](https://techspecs.ui.com/uisp/60ghz-wireless/wave-nano)
- [Wave AP Micro — spec](https://www.amazon.com/Ubiquiti-Multipoint-Sectoral-Coverage-Symmetrical/dp/B0CZ7H6MK5) · [Wave LR — Ubiquiti Store](https://store.ui.com/us/en/products/wave-lr)
- [airFiber 5XHD — datasheet](https://dl.ubnt.com/datasheets/airfiber/airFiber_5XHD_DS.pdf)
- MikroTik: datasheets em mikrotik.com (NetMetal ax, mANTBox 19s, LHG, SXTsq, Cube Pro 60, OmniTik)
