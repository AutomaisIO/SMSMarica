# Péricles (id 1) "sem internet" — 08/09/2026

> **Causa raiz: a `ether1` (link da Prefeitura) está sem link desde 06/09 05:42:21.**
> O failover v5 entrou em contingência corretamente e está lá há 54 h. O que fez a unidade
> ficar "sem internet" não foi o failover não entrar — foi a **contingência estar incompleta**,
> por itens do projeto-base F3 que nunca foram aplicados no `id01-pericles-v5.rsc`.
>
> **Estado após a correção (08/09 ~12h50):** internet restabelecida, DNS resolvendo, unidade
> operando 100 % pelo túnel do DC. Pendências no fim do documento.

## Como o "sem internet" se produzia

### 1. Resolver do MK com zero servidores alcançáveis

`/ip dns` tinha `servers=10.135.16.119,10.135.16.17,1.1.1.1,8.8.8.8`. Os quatro estavam inalcançáveis:

| Servidor | Por quê |
|---|---|
| `10.135.16.119`, `.17` | DNS interno da PMM, atrás da `ether1` morta |
| `1.1.1.1`, `8.8.8.8` | **são alvos de sonda** — as rotas `/32` do v5 os prendem em `gateway=10.1.19.253` (alias ARP do gw HPE), que é justamente a perna caída |

Medição no equipamento:

```
:resolve www.google.com          ->  failure: dns server failure
ping 1.1.1.1 / 8.8.8.8 / 9.9.9.10 ->  100% loss   (presos na perna morta)
ping 1.0.0.1 / 8.8.4.4 / 9.9.9.9  ->  0% loss, ~10 ms  (saem pelo tunel, saudaveis)
```

E o MK **sequestra todo o DNS dos clientes** para dentro desse resolver: a regra `FO-DNS-UDP`
acumulava **3.107.284 pacotes / 214 MB** — volume de retry, não de uso.

**Isto estava previsto.** `docs/failover-v5/projeto-base-F3.md:557` diz literalmente:

```
# (8.8.8.8 e 9.9.9.9 NAO sao alvos: sao forwarders do /ip dns — licao da skill)
```

O `.rsc` do Péricles usou `1.1.1.1`, `8.8.8.8` e `9.9.9.10` como sonda **e** `1.1.1.1`/`8.8.8.8`
como forwarder do resolver. A lição estava escrita e foi violada.

### 2. Rota default em ECMP — metade das conexões morria sem NAT

```
0  As + ;;; FO default via EVEO   gateway=10.203.0.1   distance=1
   DAd + 0.0.0.0/0                gateway=192.168.0.1  distance=1   (dhcp-client da Connect)
```

O `+` é ECMP. O único `masquerade` existente é `out-interface=wg-eveo`, então o ramo que saía
pela `ether2` ia **sem NAT**, com origem `10.1.19.x`, para a LAN CGNAT da Connect — e morria.
Flagrante na tabela de conexões, mesma máquina e mesmo serviço:

```
10.1.19.178 -> 57.145.6.1    replydst=10.203.0.11   st=close      (NATeada, completou)
10.1.19.178 -> 57.145.7.32   replydst=10.1.19.178   st=syn-sent   (vazou, morreu)
10.1.19.178 -> 157.240.226.1 replydst=10.1.19.178   st=syn-sent   (vazou, morreu)
```

**Também estava previsto**: `projeto-base-F3.md:530` e `:692` mandam
`/ip dhcp-client set [find interface=ether2] use-peer-dns=no use-peer-ntp=no default-route-distance=2`.
No Péricles só o `use-peer-dns=no` foi aplicado (onda 1); a **distância nunca foi**.

E o número de validação de 03/09 já denunciava o vazamento sem que ninguém lesse assim:
*"427 conexões de clientes, NAT via EVEO com 191 pacotes"* — 45 %.

### 3. Captura de DNS só em UDP

O F3 (`:589-590`, `:638-639`) e o próprio Complexo (`FAILOVER dns udp` **e** `dns tcp`, linhas
344/347 do export de 02/09) capturam `:53` em UDP **e** TCP. O Péricles só tinha a regra UDP.

### 4. Loop de escrita no DHCP a cada 5 s — este é regressão nova

```
11:12:29 dhcp server FAILOVER-dhcp changed by scheduler:FO-check/script:FO-tick/action:38711
11:12:24 dhcp server FAILOVER-dhcp changed by ...
```

**909 das 1000 linhas do log** eram isso. O `FO-tick` faz:

```
:local aut [:tostr [/ip dhcp-server get $d authoritative]]
:if ($aut != "yes") do={ /ip dhcp-server set $d authoritative=yes delay-threshold=0s }
```

Mas **`authoritative` e `delay-threshold` não existem mais em `/ip dhcp-server` no RouterOS
7.23.2** — o `get` devolve vazio, `"" != "yes"` é sempre verdadeiro, e o `set` não erra. Logo o
tick reescreve a config do DHCP em **todo tick, para sempre**. Consequências:

- **O log do MK cobria 4 minutos** (11:08→11:12). É o mesmo cegamento do incidente de 02/09 —
  perdemos o registro da queda da `ether1`.
- **Desgaste de flash**: `write-sect-since-reboot` subiu 377 setores em 307 s entre duas leituras
  (**1,23 setor/s** ≈ 106 mil setores/dia) num hEX de 16 MB com 4,9 MB livres.

### 5. Vazamento para a LAN compartilhada da Connect

Clientes `10.1.19.x` abrindo conexão para `192.168.0.x` (`.188`, `.250`, `.131`, `.15`) — a LAN
CGNAT do CPE da Connect, compartilhada com outros assinantes. A ARP da `ether2` estava cheia de
resolução falhada. `192.168.0.0/24` é rota conectada da `ether2`, então o MK encaminha. **Não
corrigido ainda** (item aberto).

## O que foi aplicado em 08/09

Backup no MK antes de tudo: `antes-lacuna-f3-080926`.

| # | Comando | Por quê |
|---|---|---|
| 1 | `/ip route add dst-address=198.211.104.55/32 gateway=192.168.0.1 comment="PIN endpoint gestao..."` | gestão sai pela física, não por dentro do `wg-eveo` |
| 2 | `/ip route add dst-address=192.241.153.121/32 gateway=192.168.0.1 comment="PIN endpoint voip..."` | idem para o hub VOIP (evita latência dobrada) |
| 3 | `/ip dns set servers=1.0.0.1,8.8.4.4,9.9.9.9` | forwarders **que não são alvo de sonda** |
| 4 | `/ip dhcp-client set [find interface=ether2] default-route-distance=2` | **mata o ECMP** — F3:530/692 |
| 5 | `/ip dns forwarders add name=pmm-dc dns-servers=10.135.16.119,10.135.16.17` | F3:571 |
| 6 | `/ip dns static add type=FWD name=pmm.local match-subdomain=yes forward-to=pmm-dc` (+ reversos `19.1.10.in-addr.arpa` e `16.135.10.in-addr.arpa`) | F3:572-574 — zona AD sempre pelos DCs, nunca NXDOMAIN público |
| 7 | `/interface bridge nat add comment=FO-DNS-TCP ... ip-protocol=tcp dst-port=53 place-before=1` | captura TCP/53 antes das regras `FO-INTERNO-*` |
| 8 | `/ip firewall nat add comment="FO dns tcp" action=redirect protocol=tcp dst-port=53 ...` | par IP da regra acima |
| 9 | `/ip dhcp-server network set 0 dns-server=10.135.16.18,10.135.16.119,10.135.16.17,1.0.0.1,8.8.4.4` | escopo com o DNS primário `.18` que faltava + fallback público (espelha o Complexo) |

Os passos 1-4 foram aplicados com `SAFETY-FO` armado (auto-revert em 5 min), validados por
**conexão SSH nova**, e o scheduler removido em seguida.

### Efeito medido

| | antes | depois |
|---|---|---|
| `:resolve www.google.com` | `dns server failure` | responde |
| conexões TCP de clientes NATeadas pelo túnel | 2 de 13 | **511 de 522** |
| conexões TCP presas em `syn-sent` sem NAT | 12 | 3 |
| `wg-eveo` `rx` | 168 MiB | 647 MiB em minutos |

## Armadilha nova descoberta no caminho

**`/ip dhcp-server network set [find address=10.1.19.0/24] ...` com o CIDR sem aspas não casa e
falha em silêncio** — o comando retorna sem erro e nada muda. Só funcionou com
`find address="10.1.19.0/24"` (com aspas) ou pelo índice. Sempre conferir com `print` depois de
um `set` que usa `find` com valor CIDR.

## Pendências

1. 🔴 **`ether1` sem link há 54 h** — causa raiz, física. Chamado para a Prefeitura / visita.
   Enquanto isso a unidade depende inteiramente da Connect + túnel do DC.
2. ✅ **`FO-tick` corrigido e publicado** (08/09 ~13:38). Bloco `authoritative` removido; regex do
   redirect DNS passou de `"FO dns udp"` para `"FO dns"`, cobrindo a regra TCP nova. Publicado por
   SFTP + `/system script set FO-tick source=[/file get "fo-tick.rsc" contents]` — inline pelo SSH
   o RouterOS junta as linhas e quebra os `:foreach`/`:local`. Rodado manualmente antes de deixar o
   scheduler assumir; estado preservado (`emFO=true`, DHCP ligado, rota FO ativa).
   **Efeito medido em 90 s:** escrita em flash caiu de **1,23 para 0,055 setor/s** (22× menos) e o
   buffer de log passou a cobrir **74 minutos** em vez de 4.
3. 🟡 **Vazamento `10.1.19.0/24 → 192.168.0.0/24`** (LAN compartilhada da Connect) — sem regra de
   bloqueio ainda.
4. 🟡 **`lease-time=8h` no DHCP de retaguarda.** O F3 (`:281`) pede `10m`, para os hosts voltarem
   ao IPAM da PMM logo após a recuperação. **Deliberadamente não alterado** durante a contingência
   ativa: um lease longo é mais seguro enquanto o link primário está fora. Revisar quando voltar.
5. ✅ **`pmm.local` voltou a resolver** — não por causa do link, mas porque construímos o **relé
   PMM** no mesmo dia: os DCs passaram a ser alcançáveis pelo túnel do DC via CAPS III. Ver
   [`rele-pmm.md`](rele-pmm.md). `:resolve pmm.local → 10.135.16.18`, e um cliente do Péricles já
   com sessão TCP estabelecida contra um host da Prefeitura.

## Sequência do dia

| Hora | O quê |
|---|---|
| ~12:30 | PINs de endpoint, DNS corrigido, ECMP eliminado — **internet volta** |
| ~12:50 | Forwarders FWD, captura DNS em TCP, escopo do DHCP |
| ~13:20 | Relé PMM: exceção no CCR2116 + masquerade no CAPS III + `check-gateway` no Péricles — **AD volta** |
| ~13:38 | `FO-tick` corrigido publicado — loop de escrita em flash morre |

## O que isto implica para o resto da frota

O export do Complexo de 02/09 tem `/ip dns set allow-remote-requests=yes servers=10.135.16.18,10.135.16.119`
— **os dois internos, nenhum público** — e as mesmas sondas `1.1.1.1`/`8.8.8.8`/`9.9.9.10`.
Ou seja: **o Complexo tem o mesmo buraco de DNS em contingência**, e só não apareceu porque nunca
passou horas nesse estado. O `servers=1.1.1.1,8.8.8.8` + forwarders do F3 também não foi aplicado lá.

Compensa em parte: o escopo DHCP do Complexo já entrega públicos (`...,1.1.1.1,8.8.4.4`).

A verificar nas 5 unidades em v5 (0, 1, 3, 7, 10), uma a uma:
`default-route-distance` da Connect, colisão sonda × forwarder do `/ip dns`, captura DNS em TCP,
e o loop de escrita do `FO-tick` (que é do script, portanto **está em todas**).
