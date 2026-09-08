# Failover v5 "Motor B" — captura L2 + contingência pelo Datacenter Automais

> **Status:** EM PRODUÇÃO no Complexo Regulador (id=0) desde 02/09/2026. Piloto único — a frota só recebe depois de validado.
> **Substitui:** o failover v2/v3/v4 (takeover de IP + ping ao gateway), removido do CCR em 02/09.

## 1. Por que mudou

Em 02/09/2026 a internet da Prefeitura caiu **acima do gateway** e o failover não entrou. A detecção antiga olhava só o enlace local:

| Camada da v2 | O que mediu naquele dia |
|---|---|
| `combo1 running` | **true** — porta ON |
| delta `rx-packet` ≥ 50/ciclo | **39.035 em 5 s** → declarou "vivo" e nem executou a camada 3 |
| `ping 10.3.74.1` | responderia 75% — o gateway estava vivo |

E `ping 8.8.8.8` pela bridge: **100% de perda**. Nenhuma camada testava internet.

Pior: a v2 resolvia a queda **tomando o IP do gateway** (`10.3.74.1`) enquanto o roteador real continuava vivo na mesma L2 → dois donos do mesmo IP → guerra de ARP → "uma parte funciona, outra não, e oscila". Só estabilizava desligando a porta na mão.

**Causa raiz do mecanismo** (verificada no código do kernel Linux 5.6.3 que o RouterOS 7 usa): possuir o IP do gateway deixa o MK **surdo** ao roteador real — `fib_validate_source()` descarta pacotes com origem local como *martian* e `arp_process()` não responde a ARP cujo sender é um IP local (`accept_local=0`, não exposto no RouterOS). Por isso o takeover só "funcionava" com a `combo1` desabilitada.

## 2. O desenho novo, em uma frase

**O MK nunca assume o IP do gateway. Ele intercepta em L2 os quadros que os clientes já endereçam ao MAC do roteador da Prefeitura** (`94:3F:C2:DF:49:D3`), e responde ARP por `10.3.74.1` **com o MAC do roteador deles**. Do ponto de vista de qualquer cliente, o gateway nunca muda de IP nem de MAC — muda apenas quem pega o pacote no meio do caminho.

| | Regime NORMAL | CONTINGÊNCIA |
|---|---|---|
| Papel do MK | ponte transparente | roteador invisível |
| Gateway, DHCP, DNS, proxy | **Prefeitura** | gateway/DNS pelo MK; internos continuam na PMM |
| Saída de internet | Prefeitura | **túnel `wg-eveo` → Datacenter Automais** (nunca Connect crua — é CGNAT) |
| Destinos internos `10/8` e `172.16/12` | Prefeitura | **Prefeitura** (seguem em ponte — AD, SNMP, impressão) |
| Convergência para o cliente | — | **0 s** (não vê IP nem MAC mudar) |

## 3. Objetos no equipamento

### Bridge NAT (`/interface bridge nat`) — ordem importa
| Comentário | Ação | Quando |
|---|---|---|
| `SHIM excecao MK .254` / `SHIM excecao telefones gw` | accept | sempre (protege o L3 do próprio MK) |
| `SHIM retorno MK-MAC -> HPE` | dst-nat → MAC do HPE | **ligado em NORMAL** — reescreve para o roteador real quem ainda tiver o MK em cache |
| `FO-DNS-UDP` | redirect | contingência — captura UDP/53 |
| `FO-INTERNO-A` / `FO-INTERNO-B` | accept | contingência — `10/8` e `172.16/12` seguem em ponte |
| `FO-CAPTURA` | redirect | contingência — **âncora de estado** |
| `FO-ARPREPLY` | arp-reply com MAC do HPE | contingência — cobre roteador/ONU mortos |

Todas as regras `FO-*` exigem `in-interface=ether2`, `src-address=10.3.74.0/24` e `dst-mac-address=<MAC do HPE>` — é isso que garante que **DHCP dos clientes (broadcast) nunca é capturado**.

### Sondas (`/tool netwatch`, comentário `FO-PMM*`) — todas pinadas pela perna da Prefeitura
`1.1.1.1`, `9.9.9.10`, `8.8.8.8` (ICMP, `src-address=10.3.74.254`), `1.1.1.1:443` (TCP) e `www.google.com` resolvido pelo DC `10.135.16.18` (prova que o DC tem internet). Caminho provado por traceroute: `10.1.200.37 → 10.1.200.22 → 10.10.28.11 → 186.193.250.37 → Google`.

Sonda informativa separada (`AD-vivo`): `pmm.local` no DC — classifica "internet fora, AD vivo" sem influenciar a decisão.

**Alias de next-hop** (`/ip arp`): `10.3.74.253 → MAC do HPE`, com rotas `/32` das sondas e de `10.135.16.0/24` apontando para ele. Necessário porque o MK não pode usar `10.3.74.1` como next-hop.

### Script `FO-tick` (scheduler `FO-check`, 5 s)
- **Estado = flag `disabled` da regra `FO-CAPTURA`** — nunca em variável de RAM (o bug do desenho antigo).
- Entra: **6 ticks (30 s)** com 0 sondas up, ou 3 ticks se a porta cair.
- Sai: **24 ticks (120 s)** com ≥2 sondas up — exigência explícita de 2 min de estabilidade.
- Reconciliação **idempotente todo tick**: `FO-*` = âncora, `SHIM` e DHCP = coerentes, cada operação em `:do{} on-error={}`.
- Graça pós-boot de 3 min (evita o failover espúrio que o desenho antigo dava a cada reboot).
- Na transição: limpa conntrack da LAN por id (fim do `no such item` que abortava o script antigo) e o cache de DNS.

### DHCP — espelho do estado do failover (decisão 02/09)
Desligado em NORMAL (a Prefeitura é a dona); o tick liga junto com a contingência (`authoritative=yes`, `delay-threshold=0s`) e desliga na volta. **Não depende** do `dhcp-server alert`.

Limitação consciente: se o link estiver OK mas o servidor DHCP deles morrer, o failover não entra e ninguém entrega IP. Raro, e quem tem lease de 24 h não percebe.

### Escopo — cópia FIEL, capturada por sniffer em 02/09
| Opção | Valor real da Prefeitura |
|---|---|
| gateway | `10.3.74.1` |
| DNS | `10.135.16.18`, `.119`, `.17`, `1.1.1.1`, `8.8.4.4` (**cinco**, com públicos) |
| lease | **24 h** (T1 12 h, T2 21 h) |
| server-id | `10.3.74.1` — **o switch HPE é o próprio servidor**, `giaddr=0.0.0.0` (não é relay) |
| opção 121/249 (rotas classless) | **não existe** |
| opção 252 (WPAD) | **não existe** |
| opção 15 (domain) | **não existe** |

⚠️ O escopo antigo do MK entregava só `.119` e `.17` (**faltava o primário `.18`**), lease de 10 min e inventava `domain=pmm.local`. Corrigido em 02/09.

### `FO-harvest` (scheduler diário 03:15)
Varre `10.3.74.2-253`, lê o ARP e grava **lease estática MAC↔IP** de cada host vivo — é a "memória" que substitui as reservas da Prefeitura, já que não existe protocolo de compartilhamento entre servidores DHCP diferentes. 33 leases colhidas na primeira execução.

## 4. Validação em produção (02/09/2026)

Tudo medido no equipamento, **sem derrubar a unidade** (1.100+ conexões ativas durante todo o processo):

| Teste | Resultado |
|---|---|
| **Captura L2 roteia** (claim mais crítico) | 🟢 70.558 pacotes / 53 MB capturados; conntrack com flag `s` (srcnat) — o MK roteia e NATeia de verdade |
| **`arp-reply` com MAC alheio** | 🟢 21 respostas entregando o MAC do HPE |
| **Internos em ponte** | 🟢 destinos `10/8` sem NAT, saindo pela Prefeitura |
| **IDA automática** | 🟢 5 sondas caem → entra em ~30 s → 16,8 Mbps migram da PMM para o túnel |
| **Histerese** | 🟢 sondas voltam em 40 s e o tick **segura** |
| **VOLTA automática** | 🟢 após 2 min estáveis → 27,9 ↓ / 6,6 ↑ Mbps de volta pela PMM |
| **Shim na volta** | 🟢 **0 pacotes** — como o cliente nunca viu outro MAC, não há cache velho a corrigir |
| **DHCP acompanha** | 🟢 liga na contingência, desliga na volta |
| **DCs recursam** | 🟢 resolvem internet e `pmm.local` (era incerto) |
| **Borda devolve TCP** | 🟢 TCP 443 pela PMM funciona com IP estático **e** de lease — **refuta** a hipótese de julho (skill §3c) |
| **VLANs** | 🟢 untagged nos **dois** lados (o lado da Prefeitura nunca tinha sido observado) |

## 5. Achados colaterais que valem para a frota

- **A Connect é CGNAT** (`100.64/10`, saltos `10.255.255.140 → 10.100.2.89`) com CPE compartilhado por 39 dispositivos de terceiros → esgotamento de portas derruba conexão nova (126-147 `syn-sent` sustentados). **NAT direto na Connect está proibido**; a saída passa a ser sempre por túnel para o Datacenter Automais.
- **PMTU do caminho Connect = 1480** (1484 com DF some em silêncio; o CPE aceita 1500 e o ICMP *frag needed* não volta). Explica "site abre e cai". Ver `docs/pmtu-links-de-saida.md`.
- **O HPE fala OSPF** (`10.3.74.1 → 224.0.0.5`) — não perturbar.
- **`psu1-state=FAIL`** no CCR e `fcs error`/flaps na `combo1` — chamado de hardware pendente.
- Dois roteadores domésticos não autorizados na LAN (`10.3.74.45` TP-Link, `10.3.74.98` Mercusys) — ponto cego aceito e documentado.

## 6. Operação

```
# ver estado (true = regime normal)
/interface bridge nat get [find comment="FO-CAPTURA"] disabled

# forçar contingência / voltar ao automático
/interface bridge nat enable  [find comment~"^FO-"]     # o tick reconcilia no próximo ciclo
/system scheduler disable FO-check                       # congela o estado atual

# reverter tudo para ponte pura
/system scheduler disable FO-check
/interface bridge nat disable [find comment~"^FO-"]
/interface bridge nat enable  [find comment~"SHIM"]
/ip dhcp-server disable [find name="FAILOVER-dhcp"]

# backups no equipamento
antes-medicao-020926.backup   # antes de tudo
depois-v5-020926.backup       # estado final validado
```

## 6b. Política de saída dos serviços essenciais (decisão 02/09)

**SISREG, SER e afins seguem pelo link ATIVO** — sem exceção de rota. Normal → Prefeitura; contingência → túnel Eveo. Todas as rotas `/32` especiais que existiam (`saida SISREG via Becape` etc.) foram **removidas**.

**O relay pela Becape foi removido por completo** (interface `wg-becape`, peer, endereço, NAT, MSS clamp, rotas e o PIN do endpoint): a Mundivox foi cancelada e não haverá mais link dedicado lá.

Fica preparada a address-list **`SERVICOS-ESSENCIAIS`** — hoje sem nenhuma regra usando-a. Serve para, se um dia for preciso, desviar **apenas** esses destinos para um link de contingência alternativo:

| Serviço | IP |
|---|---|
| SISREG DATASUS | `189.28.130.13` |
| SER `ser.saude.rj.gov.br` | `200.166.238.18` |
| `smsmarica.online` (droplet) | `146.190.65.73` |

Para desviar (quando/se necessário): `/ip route add dst-address-list=SERVICOS-ESSENCIAIS gateway=<link> distance=1`.

**M-EGRESS — resultado 02/09 (pelo IP público da Eveo):**

| Serviço | Resultado |
|---|---|
| SISREG | 🟢 **HTTP 200** — o bloqueio antigo era **geográfico** (datacenter nos EUA); com SP não há bloqueio |
| `gov.br` | 🟢 200 |
| `smsmarica.online` | 🟢 200 |
| CADSUS | 🟢 conectou |
| SER RJ | 503 **pelos dois caminhos** (Eveo e Prefeitura) = indisponibilidade do próprio SER, não bloqueio |

⇒ **Sair pelo Datacenter Automais não quebra os sistemas de saúde.** Pendência removida.

## 7. Pendências antes de espalhar para a frota

1. ~~**M-EGRESS**~~ — **resolvido em 02/09** (ver §6b).
2. **MTU do túnel**: hoje 1420 sobre PMTU 1480 = **margem zero**. Avaliar baixar para 1380.
3. **Variante TAGGED** (CDT 1092, CAPS AD 1102, CEREST 1100, SAE 1512): matchers de bridge sob `802.1Q` não são documentados — validar em bancada antes de aplicar.
4. **hEX**: medir custo de CPU com as regras ativas (o CCR não tem hw-offload; o hEX tem e vai perdê-lo em contingência).
5. Observar 7 dias, então atualizar a skill `configurar-mikrotik-unidade`, os templates e o `unidades.csv`.
