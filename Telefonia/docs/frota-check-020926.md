# Check geral da frota — 02/09/2026

Levantamento por SSH em todos os MKs alcançáveis pela VPN de gestão (`10.35.0.0/24`), comparados com o **padrão v5** validado no Complexo (`docs/failover-v5/README.md`).

**12 MikroTiks de Maricá online e coletados** (varredura completa de `10.35.0.2-254`). Os `10.35.0.4/.5/.6` respondem SSH mas recusam as credenciais do rollout — são de outro tenant, não da SMS Maricá.

> 🟡 **Péricles (id 1), 03/09:** onda 1 confirmada completa; incidente de 02/09 encerrado — a causa foi
> instabilidade física da `ether2` (384 flaps), **não** o comando de MTU. Ver `incidente-pericles-020926.md`.
> Pendência física: patch cord + CPE da Connect.

> 🔴 **Divergência do registro:** o **SRT I (id 18)** está como `ATIVA` no `unidades.csv` mas **não responde em lugar nenhum** da VPN de gestão — está fora do ar, ou o túnel `automais-vpn` dele caiu. Precisa de verificação presencial.
>
> No sentido inverso, o **CAPSI (id 5)** está `PRE_CONFIGURADA` no registro mas **está online** em `10.35.0.28` — só que sem IP na LAN e sem DHCP, ou seja, ainda em estado de bancada. Confirmar se o equipamento já foi para a unidade.

## 1. Situação por unidade

| id | Unidade | Gestão | RouterOS | Failover | Sondas | Takeover | Túnel DC | Onda 1 | NTP | admin |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | **Complexo** (CCR1009) | `.23` | 7.23.2 | **v5** ✅ | 5 | não ✅ | **sim** ✅ | ✅ | ⚠️ OFF | ⚠️ existe |
| 1 | **Péricles** | `.24` | 7.23.2 | **v5** ✅ | 5 | não ✅ | **`.11`** ✅ | ✅ | on | não |
| 10 | **CMI** | `.33` | ⚠️ 7.19.6 | **v5** ✅ | 5 | não ✅ | **`.20`** ✅ | ✅ | on | não |
| 6 | CDT (VLAN 1092) | `.29` | 7.23.2 | 🔴 v2 *(v5 **bloqueado**)* | 0 | **SIM** | **`.16`** ✅ | ✅ | on | não |
| 3 | **CAPS III** | `.26` | 7.23.2 | **v5** ✅ | 5 | não ✅ | **`.13`** ✅ | ✅ | on | não |
| 4 | CAPS AD (VLAN 1102) | `.27` | 7.23.2 | v2 | 0 | **SIM** | não | não | on | não |
| 7 | **Boqueirão** | `.30` | ⚠️ 7.19.6 | **v5** ✅ | 5 | não ✅ | **`.17`** ✅ | ✅ | on | não |
| 11 | CEREST (VLAN 1100) | `.34` | 7.23.3 | v2 | 0 | **SIM** | não | não | on | não |
| 17 | SAE (VLAN 1512) | `.40` | 7.23.3 | v2 | 0 | **SIM** | não | não | on | não |
| 5 | CAPSI *(bancada)* | `.28` | ⚠️ 7.19.6 | v2 | 0 | não | não | não | on | não |
| 21 | TFD *(sem link PMM)* | `.44` | ⚠️ 7.19.6 | v2 inerte ✅ | 0 | não ✅ | não | não | on | não |
| 30 | SAMU Ponta Negra (3011) | `.49` | 7.23.2 | **nenhum** | 0 | não | não | não | ⚠️ OFF | ⚠️ existe |

> Levantamento ao vivo em **03/09/2026 ~09h**, por SSH nas 12 unidades simultaneamente.
> **Atualizado 03/09 ~20h: 5 em v5 completo** (0, 1, 3, 7, 10) · 1 (CDT) com onda 1 + túnel,
> failover **bloqueado** por ser tagged · 6 intocadas.
> 🔴 **As 4 unidades tagged (CDT, CAPS AD, CEREST, SAE) não podem receber o v5 como está** —
> restrição do RouterOS provada no CDT em 03/09; ver `failover-v5/variante-tagged-bloqueada.md`.
>
> **4 unidades ainda com takeover de IP** (4, 6, 11, 17) — **todas tagged** — é o desenho que provoca guerra
> de ARP e que o v5 elimina; enquanto ele existir, a contingência dessas unidades é a antiga.

## 2. Os cinco desvios que valem para quase toda a frota

### 2.1 🔴 Escopo de DHCP errado — **8 unidades, idêntico**
Todas entregam:
```
dns-server = 10.135.16.119, 10.135.16.17      domain = pmm.local      lease = 10 min
```
Mas o real da Prefeitura (capturado por sniffer no Complexo em 02/09) é:
```
dns-server = 10.135.16.18, 10.135.16.119, 10.135.16.17, 1.1.1.1, 8.8.4.4    (sem domain)    lease = 24 h
```
⇒ **falta o DNS primário `.18`**, sobra um `domain` que a Prefeitura não entrega, e o lease é 144× menor.
*A confirmar por unidade* — o escopo real pode variar por sub-rede; repetir o teste de captura em cada uma.

### 2.2 🔴 DHCP do MK ligado 24/7 — **8 unidades**
`disabled=false` com `delay-threshold`, competindo com o da Prefeitura em regime normal. Já está provado que o `delay-threshold` **não segura** (a renovação é unicast): no Complexo, 50 hosts tinham sido capturados silenciosamente. As mesmas 8 unidades estão nessa condição agora.

### 2.3 🔴 Failover v2 com takeover de IP — **7 unidades**
`FAILOVER gw takeover` presente e o script antigo ativo. É o desenho que **falhou em 02/09** (detecção só local) e que cria dois donos do mesmo IP na L2 (guerra de ARP). Nenhuma dessas unidades tem sonda de internet (`netwatch` = 0 em todas).

### 2.4 🔴 Sem MSS clamp — **11 de 12**
Nenhuma unidade (exceto o Complexo) trata o buraco negro de PMTU da Connect. Toda saída pela Connect nessas unidades está sujeita ao "abre o site e cai". Ver `docs/pmtu-links-de-saida.md`.

### 2.5 🔴 Sem túnel para o Datacenter Automais — **11 de 12**
A Connect é CGNAT com CPE compartilhado. Em contingência, essas unidades ainda fazem NAT direto na Connect — o modo que derruba conexão nova por esgotamento de portas.

## 3. Pontos individuais

- **Complexo (0)**: NTP **desligado** e usuário `admin` ainda existe — dois desvios do próprio padrão (o rollout manda `becape` e remover o `admin`). Corrigir.
- **CAPSI (5)**: online e alcançável, mas **sem IP na LAN e sem DHCP** — está em fase bancada, não fase site. O `unidades.csv` diz `PRE_CONFIGURADA`, coerente. Confirmar se o equipamento já está fisicamente na unidade.
- **CMI (10)**: tem uma **VLAN 20 "guest" + DHCP próprio `172.20.20.0/24`** com DNS público — configuração extra **não documentada** em lugar nenhum. Levantar o porquê antes de padronizar.
- **TFD (21)**: correto por design — sem link da Prefeitura, o MK é gateway/DHCP/DNS e o scheduler de failover está desabilitado de propósito.
- **SAMU Ponta Negra (30)**: RB3011, fora do padrão — DHCP próprio `10.106.0.0/24` com DNS público, sem failover, NTP off, `admin` presente. É a unidade atrás do MK do Conde (L2TP).
- **Versões**: 4 unidades em **7.19.6** (CAPSI, Boqueirão, CMI, TFD), o resto em 7.23.2/7.23.3.

## 3b. 🔴 Antenas Ubiquiti órfãs do controlador UniFi

Duas antenas Ubiquiti (OUI `24:5A:4C`) na frota, ambas **tentando alcançar um controlador que não responde**:

| Unidade | IP da antena | Porta do MK | Destino que busca |
|---|---|---|---|
| **CMI (10)** | `10.1.18.125` | `ether5` (circuito Wi-Fi) | `10.30.30.23:8080` (inform) + `:5514` (syslog) |
| **CDT (6)** | `10.1.92.98` | `ether4`, na VLAN 1092 | `10.30.30.23:8080` + `:5514` |

`10.30.30.0/24` é a LAN do escritório Becape/Automais e `10.30.30.23` seria o **controlador UniFi**.

**Diagnóstico:**
- Só **CDT e CMI** têm a rota `10.30.30.0/24 via automais-vpn` (criada pelo Automais.IO, comentário `Automais.IO: rota Marica->10.30.30 (UniFi)`). As outras 8 unidades não têm rota nenhuma.
- **Nenhum MK alcança `10.30.30.23`** — 100 % de perda, inclusive a partir do próprio IP da VPN de gestão. Ou o controlador está fora/mudou de IP, ou o servidor da `automais-vpn` não roteia de volta para `10.30.30.0/24`.
- A porta de origem da antena do CMI se repetia em todas as amostras (`33788`) — é retransmissão de SYN: **a sessão nunca completa**. As antenas ficam num ciclo eterno de tentativa.

**A decidir:** subir o controlador de novo (e corrigir a rota de volta), migrar para um controlador no Datacenter Automais, ou adotar as antenas localmente. Enquanto isso elas funcionam como AP mas ficam **sem gestão, sem estatística e sem push de configuração**.

⚠️ **Correção de OUI:** o prefixo `94:C6:91` (visto no Complexo `10.3.74.251`, Boqueirão `10.1.108.198` e CMI `10.1.18.199`) **não é Ubiquiti — é EliteGroup (ECS)**, ou seja, PCs. O tráfego deles confirma (443 e 5222, típico de estação). O inventário anterior que falava em "APs Ubiquiti" no Complexo estava errado por esse motivo.

## 4. Ordem sugerida de padronização

Aplicar **uma a uma**, com validação entre cada uma:

| Onda | Unidades | Por quê |
|---|---|---|
| **1 — correções sem risco** | todas | escopo de DHCP correto, MSS clamp + `mtu` da Connect, `use-peer-dns=no`, NTP, remover `admin` onde houver. Não muda arquitetura, não derruba ninguém. |
| **2 — túnel DC** | todas com Connect | `wg-eveo` + PIN `/32` + NAT dos clientes pelo túnel. Prepara a contingência sem ativá-la. |
| **3 — failover v5 (untagged)** | ✅ **CONCLUÍDA** — 1 Péricles, 3 CAPS III, 7 Boqueirão, 10 CMI | mesmo caso do Complexo, já validado. |
| **4 — failover v5 (tagged)** | 6 CDT (1092), 4 CAPS AD (1102), 11 CEREST (1100), 17 SAE (1512) | **exige o teste de bancada** dos matchers sob 802.1Q antes. |
| **5 — casos próprios** | 5 CAPSI, 21 TFD, 30 SAMU | avaliar individualmente; TFD provavelmente só recebe a onda 1. |

Antes das ondas 3 e 4, subir o RouterOS das 4 unidades em 7.19.6 (janela combinada — o reboot derruba a bridge por ~2 min).

## 5. O que ainda falta medir em cada unidade (barato, sem impacto)

Repetir o que foi feito no Complexo: PMTU do link Connect (busca binária com DF), escopo real do DHCP (dhcp-client temporário + sniffer), VLANs nos dois lados da bridge, e o MAC do gateway real. Sem esses quatro valores o `.rsc` da unidade não pode ser gerado.
