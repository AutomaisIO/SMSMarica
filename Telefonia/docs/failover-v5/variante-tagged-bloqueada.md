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
