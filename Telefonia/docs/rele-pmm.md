# Relé PMM — entregar AD, DNS interno e file server por outra unidade

> **EM PRODUÇÃO desde 08/09/2026.** Primeiro par: **Péricles (id 1) → CAPS III (id 3)**.
> Nasceu do incidente de 08/09 (`incidente-pericles-080926.md`): com a `ether1` do Péricles fora
> há 54 h, a unidade tinha internet pelo túnel do DC mas estava **sem AD, sem GPO e sem file
> server** — porque as redes internas da Prefeitura só existem do outro lado da perna caída.

## 1. A ideia

Uma unidade que perdeu a perna da Prefeitura pede emprestada a perna de **outra unidade**, através
do túnel do Datacenter Automais que ambas já têm.

```
cliente 10.1.19.x  ──┐
                     │ captura L2 (FO-RELE-PMM)
              MK Péricles ── wg-eveo ──► CCR2116 (DC) ──► wg-eveo ── MK CAPS III
                                                                          │
                                                              masquerade → 10.1.55.254
                                                                          │
                                                                  LAN da Prefeitura
                                                                  10.135.16.0/24 (DCs)
```

Latência medida ponta a ponta: **20 ms**. Pelo caminho alternativo (VPN de gestão `automais.io`)
seriam **240 ms** — ver §6.

## 2. Por que NÃO resolver isso só no DNS

Foi a primeira tentativa e está **descartada**. Apontar o forwarder `pmm-dc` para o MK de outra
unidade funciona (provado: `:resolve pmm.local server=10.35.0.23` devolve `10.135.16.18`), mas
**resolver nome sem alcançar o IP é pior que não resolver**:

- hoje `pmm.local` dá SERVFAIL e o Windows vai **direto para credencial em cache** — logon rápido;
- com o nome resolvendo para um IP inalcançável, o cliente **tenta** falar com o DC e fica pendurado
  no timeout: logon lento, GPO travando, mapeamento de rede pendurado.

O DNS não é o primeiro passo, é **consequência**: construído o caminho IP, o forwarder local volta
a funcionar sozinho e nenhum relé de DNS é necessário.

## 3. As quatro peças

### 3.1 No DC (CCR2116, `10.30.50.26`)

O hub **isola as unidades de propósito** — duas regras que **permanecem**:

```
;;; unidades NAO acessam rede interna do DC
chain=forward action=drop dst-address-list=REDES-PRIVADAS in-interface=wg-unidades
;;; unidades isoladas entre si
chain=forward action=drop in-interface=wg-unidades out-interface=wg-unidades
```

Abre-se uma **exceção por par de endereços**, acima delas:

```
/ip firewall filter add chain=forward action=accept in-interface=wg-unidades out-interface=wg-unidades \
    src-address=10.203.0.11 dst-address=10.135.16.0/24 \
    comment="RELE PMM ida: Pericles (id=1) alcanca a PMM pelo CAPS III (id=3)" \
    place-before=[find comment~"unidades NAO acessam rede interna"]
/ip firewall filter add chain=forward action=accept in-interface=wg-unidades out-interface=wg-unidades \
    src-address=10.135.16.0/24 dst-address=10.203.0.11 \
    comment="RELE PMM volta: PMM responde ao Pericles (id=1)" \
    place-before=[find comment~"unidades NAO acessam rede interna"]

/ip route add dst-address=10.135.16.0/24 gateway=10.203.0.13 \
    comment="RELE PMM: PMM interna via CAPS III (id=3)"

/interface wireguard peers set [find comment~"id=3 CAPS III"] \
    allowed-address=10.203.0.13/32,10.135.16.0/24
```

⚠️ **As duas regras de retorno são obrigatórias.** O `accept established,related` do hub vem
**depois** das regras de isolamento, então a volta seria descartada sem uma regra própria.

⚠️ **`allowed-address` é filtro de cryptokey routing nas duas direções.** O peer do relé precisa
conter a faixa da PMM, senão o WireGuard descarta a resposta na entrada. O peer da unidade
**socorrida não é tocado** — ver §5.

### 3.2 No relé (CAPS III, `10.35.0.26`)

Uma regra. A origem `10.203.0.11` não existe na LAN dele, então o impacto na unidade é nulo:

```
/ip firewall nat add chain=srcnat action=masquerade src-address=10.203.0.11 \
    dst-address=10.135.16.0/24 out-interface=bridge-transparente \
    comment="RELE PMM: Pericles (id=1) -> LAN da PMM"
```

Pré-requisitos que o relé já tinha: rota `10.135.16.0/24` pelo gateway real, `chain=forward` sem
default-drop, e `10.203.0.0/24` conectada pelo `wg-eveo`.

### 3.3 Na unidade socorrida (Péricles, `10.35.0.24`)

**Nenhuma rota nova.** Duas linhas:

```
/ip route set [find comment~"PMM internas"] check-gateway=ping
/interface bridge nat add chain=dstnat comment=FO-RELE-PMM action=redirect \
    in-interface-list=LAN-PORTS src-address=10.1.19.0/24 \
    dst-mac-address=<MAC_DO_GW_REAL>/FF:FF:FF:FF:FF:FF mac-protocol=ip \
    dst-address=10.135.16.0/24 place-before=2
```

**`check-gateway=ping` é a peça que dispensa script.** A rota `10.135.16.0/24 → <gw real>` fica
`Is` (inativa) enquanto o gateway não responde, e o tráfego cai no default pelo `wg-eveo`. Quando
a perna da Prefeitura volta, ela **reativa sozinha** e a PMM volta a ser local. Sem estado, sem
tick, auto-cicatrizante nos dois sentidos.

**`FO-RELE-PMM` é obrigatória e não é óbvia.** Sem ela o tráfego do cliente **nem chega na tabela
de rotas**: a regra `FO-INTERNO-A` (`accept` para `10.0.0.0/8`) ponteia tudo para o roteador da
Prefeitura — que é justamente o que está morto. Por isso a captura precisa vir **antes** dela.
O comentário começa com `FO-` de propósito: o `FO-tick` já a gerencia (liga em contingência,
desliga em normal) sem nenhuma alteração no motor.

O NAT já existe: `FO NAT clientes via EVEO` (`masquerade src-address=<LAN> out-interface=wg-eveo`)
converte o cliente em `10.203.0.11`, que é o endereço que as regras do DC esperam.

### 3.4 DNS: nada a fazer

Com o caminho IP de pé, o forwarder `pmm-dc` (`/ip dns forwarders`, ver `incidente-pericles-080926.md`)
volta a alcançar os DCs sozinho. Medido no Péricles logo após:

```
udp 10.203.0.11:34390 -> 10.135.16.17:53   repl-packets=1 repl-bytes=125
udp 10.203.0.11:36250 -> 10.135.16.119:53  repl-packets=1 repl-bytes=188
:resolve pmm.local -> 10.135.16.18
```

## 4. Validação (Péricles, 08/09/2026)

| Prova | Resultado |
|---|---|
| Rota primária desativou sozinha | `4 Is ... 10.135.16.0/24 gateway=10.1.19.1 check-gateway=ping` |
| MK alcança o DC | `10.135.16.119` — **0% loss, 20 ms** |
| DNS interno responde | `repl-packets=1` nos dois DCs; `pmm.local → 10.135.16.18` |
| **Cliente com sessão real** | `tcp 10.1.19.164:60815 → 10.135.16.136:55823 established, orig=11 repl=9` |

## 5. Propriedade de segurança do desenho

**O peer da unidade socorrida no DC não é alterado.** No caso do Péricles, o `peer7` carregava
516 MB de tráfego — *toda* a internet da unidade. Mexer no `allowed-address` dele durante uma
contingência derrubaria a unidade inteira. Todo o ajuste fica no peer do **relé**, que por
definição **não está em contingência** e portanto tem a perna da Prefeitura como caminho vivo.

Ao escolher o relé, prefira: menor tráfego, maior uptime, e `pmm.local` resolvendo. Verificar antes
com `:resolve pmm.local` — o **Boqueirão (id 7) devolve SERVFAIL** mesmo com `ether1` viva, então
não serve como relé.

## 6. O caminho descartado: VPN de gestão (`automais.io`)

Também funciona e não depende do CCR2116, mas é pior em três aspectos e ficou como plano B:

| | pelo DC (EVEO) | pela `automais.io` |
|---|---|---|
| Latência | **20 ms** | 240 ms |
| NAT | **único** (só no relé) | **duplo** |
| Papel do túnel | dados | **gestão** — não deve carregar dados |

O NAT duplo é obrigatório lá porque o peer da `automais.io` tem `allowed-address=10.35.0.0/24`: o
WireGuard descarta pacote com origem `10.1.19.x`, então a unidade precisa NATear para `10.35.0.24`
antes de entrar no túnel. Já o `wg-eveo` tem `allowed-address=0.0.0.0/0` nos dois lados.

## 7. Objeções que a Prefeitura pode levantar

**Não é broadcast.** É a primeira suposição e é infundada: o caminho é **roteado**, nada de camada 2
atravessa. Zero ARP, zero NetBIOS, zero DHCP, zero mDNS, zero risco de loop ou STP. Só unicast IP
para `10.135.16.0/24`.

As objeções legítimas são quatro:

1. **O NAT apaga a identidade das máquinas.** Todo host chega no DC como `10.1.55.254`. Quebra
   rastreabilidade em log, ACL por sub-rede, **AD Sites and Services** e inventário por IP.
   → **Solução barata, e vale pedir junto:** uma **rota estática** no roteador deles apontando
   `10.1.19.0/24` para `10.1.55.254` permite remover o masquerade, e cada máquina passa a chegar
   com o IP real. Paliativo enquanto isso: usar um IP dedicado no relé (ex. `10.1.55.253`) via
   `action=src-nat to-addresses=`, para o log ao menos distinguir "relé" do MK da unidade.
2. **Tráfego deles passa por infraestrutura de terceiro** e **fura o perímetro** — não passa pelo
   firewall/proxy/IPS da Prefeitura. É questão de política e LGPD, não técnica: precisa de aceite
   formal.
3. **Uma unidade vira porta de entrada para outra.** Mitigado pelo escopo: a exceção é um par de
   endereços e as regras de isolamento do hub continuam.
4. **O relé vira gargalo e ponto único** — consome banda dele, e se cair leva o AD da outra junto.

Detalhe operacional: na volta do link, sessões TCP longas abertas pelo relé morrem (a rota muda).
Navegador e mapeamento reconectam; avisar quem estiver com arquivo de rede aberto.

## 8. Como replicar para outra unidade

1. Escolher o relé (§5) e anotar em `registro/unidades.csv` (`rele_pmm` / `rele_de`).
2. DC: duas regras de filtro + rota + `allowed-address` do peer **do relé**.
3. Relé: um `masquerade`.
4. Unidade: `check-gateway=ping` + `FO-RELE-PMM`.
5. Validar na ordem: MK pinga o DC → `:resolve pmm.local` → conexão de cliente `established`.

Armar `SAFETY` com auto-revert no DC antes do passo 2 — é o equipamento que carrega a internet de
todas as unidades em contingência.

## 9. Escopo atual e como ampliar

Hoje é **só `10.135.16.0/24`** (os DCs) — que é o que o AD precisa, e é o limite natural porque é a
única faixa que o relé roteia (`10.135.16.0/24 → 10.1.55.1`).

Se o file server estiver fora dessa faixa, ampliar exige **quatro** ajustes coordenados: rota no
relé, `allowed-address` do peer do relé, rota no DC e as duas regras de filtro. Levantar o IP antes
— não ampliar "por precaução", porque cada faixa aberta é superfície a mais na LAN da Prefeitura.
