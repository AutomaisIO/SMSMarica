# Variante TAGGED do failover v5 — BLOQUEADA pelo RouterOS

> Medido no **CDT (id 6, VLAN 1092)** em 03/09/2026, ao vivo e sem alterar nada.
> **Conclusão: o v5 como está NÃO pode ser aplicado a unidade tagged.** Não é falta de cuidado —
> é restrição do RouterOS. O CDT ficou exatamente como estava.

## 1. A restrição, na mensagem do próprio RouterOS

```
failure: ip matchers are valid only when mac-protocol is set to ip
```

Sob `mac-protocol=vlan`, o RouterOS aceita **só matchers de nível MAC**. Tudo que é IP é recusado.

| Matcher sob `mac-protocol=vlan` | Resultado |
|---|---|
| `vlan-id`, `vlan-encap` | ✅ aceito |
| `dst-mac-address` / `src-mac-address` | ✅ aceito |
| `src-address` / `dst-address` | 🔴 **recusado** |
| `ip-protocol`, `dst-port` | 🔴 **recusado** |
| `action=arp-reply` (+ `arp-dst-address`) | 🔴 **recusado** |

## 2. O que isso derruba, peça por peça

O v5 tem quatro pilares. **Três são impossíveis sob VLAN:**

| Peça do v5 | Precisa de | Sob VLAN |
|---|---|---|
| `FO-CAPTURA` | `dst-mac` do HPE | ✅ **funciona** (cega — pega tudo) |
| `FO-INTERNO-A/B` — manter `10/8` e `172.16/12` em ponte | `dst-address` | 🔴 impossível |
| `FO-ARPREPLY` — responder ARP pelo gateway morto | `action=arp-reply` | 🔴 impossível |
| `SHIM` + suas 2 exceções — não sequestrar tráfego do próprio MK | `dst-address` | 🔴 impossível |

O **`arp-reply` é o mais grave**: é ele que atende os clientes quando o roteador da Prefeitura
está morto. Sem ele, a contingência só funciona enquanto o gateway real ainda responde ARP — ou
seja, justamente **não funciona no cenário em que é necessária**.

E a captura cega, sozinha, é pior que nada: ela sequestraria também o tráfego para as redes
internas da Prefeitura (AD, DNS, SNMP, impressão) e para o próprio MK, sem forma de excluí-los
na bridge.

## 3. Medições de apoio (CDT, 03/09)

Contadores `action=passthrough` (só contam, não alteram), 40 s:

| | pacotes |
|---|---|
| qualquer VLAN | 77.923 |
| VLAN 1092 | 76.659 |
| VLAN 1092 + encap IP | 76.131 |
| VLAN 1092 + ARP | 302 |
| **IP SEM tag (controle)** | **25** |

⇒ **praticamente 100% do tráfego de cliente é tagged** — não existe atalho "quase tudo é untagged".
E há ~1.264 pacotes/40 s em **outras VLANs**, que precisam continuar passando (gerência do switch
core da Prefeitura é requisito).

Bridge do CDT: `bridge-transparente` = `ether1` (PMM) + `ether4` (switch core), `vlan-filtering=no`;
`vlan1092-lan` é uma interface VLAN **sobre a bridge**, onde moram `10.1.92.254` e `10.200.6.1`.
CPU durante os testes: 8% → 3%. 63 hosts na bridge.

## 4. NÃO é versão do RouterOS

Testado com a mesma regra em **7.19.6 (hEX/mmips)**, **7.23.2 (hEX e CCR/tile)** e **7.23.3** —
recusa idêntica nas três. É restrição de arquitetura do bridge (herança do ebtables), não bug.
Existe **7.24.2** disponível, mas **nenhuma evidência** de que mude isso; não subir versão em
produção apostando nisso.

## 5. Alternativa que NÃO exige mexer nas bridges: `use-ip-firewall-for-vlan`

```
/interface bridge settings
              use-ip-firewall: no
     use-ip-firewall-for-vlan: no     <<< existe, e está desligado
```

Ligada, entrega o tráfego **tagged** da bridge ao **firewall IP**, onde `src-address`/`dst-address`
e portas funcionam normalmente — resolveria `FO-INTERNO` e as exceções do SHIM **sem** reestruturar
bridge nenhuma. Duas ressalvas, nenhuma pequena:

1. **`arp-reply` continua fora** — ARP não é IP. A saída seria **ARP publicado** na `vlan1092-lan`
   (`/ip arp ... published=yes`), que responde com o **MAC do próprio MK** ⇒ reintroduz a troca de
   MAC que o v5 existe para evitar. É um desenho **diferente** (mais perto do "Plano C" do plano),
   não o v5 portado.
2. **Custo de CPU** — todo o tráfego da bridge passa a atravessar o firewall IP, matando o
   fast-path. No CDT são ~76 mil pacotes/40 s num hEX.

⇒ Candidata legítima **para bancada**, não para produção. É chave global: afeta a unidade inteira.

## 6. Caminho alternativo (exige bancada e presença no local)

**Terminar a VLAN 1092 dentro do MK, para que os quadros fiquem untagged no domínio onde as
regras rodam** — aí o v5 validado funciona **sem nenhuma alteração**:

```
bridgeA {ether1}  vlan-filtering=yes protocol-mode=none
bridgeB {ether4}  vlan-filtering=yes protocol-mode=none
vlan1092@bridgeA  +  vlan1092@bridgeB   ->  bridge-1092 (untagged, é aqui que o v5 roda)
demais VLANs (incl. gerência da PMM) seguem passando entre A e B
```

Por que **não** dá para simplesmente criar `/interface vlan` sobre `ether1`/`ether4`: porta escrava
de bridge não entrega quadros a uma interface VLAN — a bridge captura antes. Seria preciso tirar
as portas da bridge, o que derruba as outras VLANs.

**Isto muda o caminho de dados da unidade inteira.** Não se improvisa remotamente numa unidade com
63 hosts: exige bancada (teste T-C do plano) e alguém no local.

## 7. Unidades afetadas

CDT (1092), CAPS AD (1102), CEREST (1100), SAE (1512) — **as 4 tagged da frota**.
Enquanto isso, elas seguem no v2, com takeover de IP e detecção cega a falha a jusante.

---

# Revisão de 08/09/2026 — o problema encolheu de três bloqueios para um

> Medido no **CEREST (id 11, VLAN 1100, RouterOS 7.23.3)** — segunda unidade e segunda versão.
> A unidade foi **integralmente revertida** ao estado original ao fim dos testes.

## R1. A restrição está reconfirmada, e `vlan-encap=ip` não é atalho

O §1 deixava dúvida sobre a combinação `mac-protocol=vlan` **com** `vlan-encap=ip` **e** matcher IP
ao mesmo tempo. Testado com `action=passthrough` (inerte):

| Teste | Resultado |
|---|---|
| `mac-protocol=vlan vlan-id=1100 vlan-encap=ip src-address=10.1.100.0/24` | 🔴 **RECUSADO** |
| `mac-protocol=vlan vlan-id=1100 vlan-encap=ip ip-protocol=udp dst-port=53` | 🔴 **RECUSADO** |
| `mac-protocol=vlan vlan-id=1100 vlan-encap=arp` (sem matcher IP) | ✅ aceito |
| `action=arp-reply mac-protocol=vlan vlan-encap=arp arp-opcode=request` | 🔴 **RECUSADO** |

`vlan-encap` seleciona o *payload* da tag, mas não muda o fato de que `mac-protocol=vlan` habilita
apenas matchers de nível MAC. Fecha a dúvida: **não há atalho por matcher.**

## R2. Correção do §5 — `use-ip-firewall-for-vlan` sozinho NÃO faz nada

O §5 apresentava a chave como se bastasse. **Não basta**: ela é *modificador* de
`use-ip-firewall`. Provado no CEREST:

| Estado | Regra ampla `chain=forward src-address=10.1.100.0/24 action=passthrough` |
|---|---|
| `use-ip-firewall=no` + `use-ip-firewall-for-vlan=yes` | **0 pacotes** |
| `use-ip-firewall=yes` + `use-ip-firewall-for-vlan=yes` | **3 pacotes** (funciona) |

⇒ o custo é **maior** do que o §5 estimava: não é "o tráfego tagged passa a atravessar o firewall
IP", é **todo o tráfego bridgeado**, tagged ou não.

**O custo de CPU continua NÃO medido.** No CEREST a `ether4` recebeu **16 pacotes em 25 s** (~0,6
pacote/s) — a unidade estava praticamente parada, e CPU 3% → 1% não significa nada. Para valer, a
medição tem de ser no **CDT ou no CAPS AD** (~36 M pacotes/dia, ~420 pacote/s), que são justamente
as unidades onde não se experimenta remotamente.

## R3. O relé PMM elimina o bloqueio do `FO-INTERNO`

Ver [`../rele-pmm.md`](../rele-pmm.md).

O §2 listava `FO-INTERNO-A/B` como peça impossível sob VLAN, e a captura cega como "pior que nada"
porque sequestraria o tráfego para as redes internas da Prefeitura.

**Isso deixou de ser verdade em 08/09.** O `FO-INTERNO` existia para manter `10/8` em ponte até o
roteador da Prefeitura — e o incidente do Péricles provou que, com a perna fisicamente morta, isso
é um **buraco negro**, não preservação de identidade. A resposta certa passou a ser capturar esse
tráfego e roteá-lo pelo **relé**. Ou seja: sob VLAN, a captura cega deixa de ser defeito e vira o
comportamento desejado — e **não precisa de matcher IP nenhum**, porque o escopo já vem do
`vlan-id` + `dst-mac` do gateway, ambos aceitos.

Pelo mesmo motivo o **SHIM** deixa de ser necessário: ele é resíduo da migração v2→v3, e no v5 não
existe takeover, então nenhum cliente chega a aprender o MAC do MK como gateway.

## R4. O que sobrou: um bloqueio só

**Responder ARP pelo gateway morto.** É o que atende o cliente exatamente no cenário em que a
contingência é necessária, e `action=arp-reply` continua recusado sob VLAN.

A substituição candidata é ARP publicado na interface VLAN:

```
/ip arp add address=<gw real> mac-address=<MAC do HPE> interface=vlan<X>-lan published=yes
```

**A pergunta aberta é com qual MAC o RouterOS responde** — o configurado ou o da própria interface.
Se for o da interface, os clientes que re-resolverem passam a apontar para o MK (troca de MAC), o
que é um desenho diferente do v5 puro — mas **não necessariamente inaceitável**, porque:

- quem ainda tem o MAC do HPE em cache continua sendo atendido pela captura cega;
- quem re-resolve passa a falar direto com o MK, que roteia normalmente;
- **não há guerra de ARP**, porque o ARP publicado só é ligado quando as sondas fim-a-fim já
  provaram que o gateway real está morto — diferente do v2, que assumia o IP com o gateway vivo.

**Este teste não é possível remotamente**: exige um host na LAN daquela VLAN emitindo ARP request.
Fazer em bancada, ou numa janela combinada com alguém no local.

## R5. Escolha da unidade de teste

| Unidade | VLAN | Pacotes/dia (bridge fast-forward) | ROS | Uptime |
|---|---|---|---|---|
| CDT (6) | 1092 | ~36,6 M | 7.23.2 | 2d13h |
| CAPS AD (4) | 1102 | ~35,6 M | 7.23.2 | 5d |
| **CEREST (11)** | 1100 | **~6,3 M** | 7.23.3 | 4w1d |
| SAE (17) | 1512 | ~25,6 M | 7.23.3 | 1d07h |

**CEREST é a unidade de teste** — a mais quieta e a mais estável. Com a ressalva de R2: ela prova
*mecanismo*, nunca *custo*.

## R6. Estado após os testes

CEREST devolvido ao original: `use-ip-firewall=no`, `use-ip-firewall-for-vlan=no`, `bridge nat`
vazio, 12 regras de filtro, nenhum scheduler de teste. O `FAILOVER-check` v2 dele não foi tocado.

As 4 unidades tagged seguem no v2.
