---
name: configurar-mikrotik-unidade
description: Padrão ponta-a-ponta para configurar o MikroTik (hEX RB750Gr3 novo padrão do rollout; CCR no Complexo) de uma unidade da SMS Maricá — bridge transparente entre o uplink da Prefeitura e a LAN da unidade, VPN VOIP WireGuard até o hub Asterisk, failover de internet pela Connect, registro do router no automais.io (tenant Saúde Maricá) com bootstrap, e blindagem de segurança. Use quando o usuário disser "configurar o mikrotik da unidade X", "plugar unidade nova na telefonia", "subir o MK do <unidade>", "failover da unidade", "blindar/endurecer o MK", "cadastrar o MK no automais.io", "criar as VPNs multi-link / VPN por link (A/B/C) do MK". SEMPRE começa perguntando a unidade (id+nome); as portas do hEX seguem convenção FIXA (ether1=Prefeitura, ether2=Connect, ether3=reserva/gestão, ether4=LAN unidade, ether5=Wi-Fi) — só pergunte portas se o modelo NÃO for o hEX padrão. Qualquer mudança no procedimento atualiza ESTA skill + a documentação em Telefonia/docs + registro/unidades.csv.
---

# Configurar MikroTik de uma unidade (padrão SMS Maricá)

Este é o **padrão do projeto**. Consolidado a partir da migração do Complexo Regulador
(id=0, RB750r2 → CCR1009). Documentos de apoio (fonte de verdade dos escopos):
`Telefonia/docs/plano-enderecamento.md`, `servidor-wireguard-hub.md`, `mikrotik-unidade.md`;
scripts `Telefonia/scripts/mikrotik-gen.sh` e `hub-add-unidade.sh`; registro
`Telefonia/registro/unidades.csv`. Exemplo real documentado em
`Telefonia/docs/failover-v5/` (projeto, export, scripts e análises).

> **Regra de manutenção:** mudou algo no procedimento → atualize ESTA skill, a doc em
> `Telefonia/docs/` e o `unidades.csv`. A skill e a doc andam juntas.

## 0. PERGUNTAS OBRIGATÓRIAS (antes de tocar em qualquer coisa)

1. **Unidade** (id + nome) — conferir em `plano-enderecamento.md` / `unidades.csv`.
2. **Modelo e RouterOS** — o rollout usa **hEX (RB750Gr3) novos e idênticos** (convenção de
   portas fixa abaixo). Se for OUTRO modelo (CCR do Complexo, RB antigo), aí sim perguntar as
   portas. **A checagem de versão é obrigatória em TODO MK — ver §0.1 logo abaixo.**
3. **Dados L3 da LAN da unidade** (levantar com sniffer se não souber — ver skill de
   levantamento / `Telefonia/docs/failover-v5/README.md`):
   - Sub-rede da LAN (ex.: `10.3.74.0/24`), **gateway real** (ex.: `10.3.74.1`),
     e **IP que o MK assume na LAN** (ex.: `10.3.74.254`, herdado do MK antigo).

### §0.1 Checagem de versão do RouterOS — OBRIGATÓRIA em todo MK

Vale para **qualquer** MK que a skill tocar: bancada, fase site, ou visita a um MK já ativo.
Fazer **antes** de aplicar configuração, e registrar a versão no relato.

```
/system resource print                       # installed-version + board-name
/system package update set channel=stable
/system package update check-for-updates once
/system package update print                 # latest-version + status
```

Decisão pela versão instalada:

| Versão instalada | Ação | Quem decide |
|---|---|---|
| **< 7.x** (v6) | **UPGRADE OBRIGATÓRIO, antes de qualquer coisa.** WireGuard não existe em v6 — sem isso não há nem VOIP nem gestão. Não seguir com o resto da skill sem resolver. | Não é opcional. Só a **janela** se combina (⚠️ reboot derruba a bridge por ~2-5 min; ver §1) |
| **≥ 7.x mas não é a `latest-version`** | **SUGERIR** o upgrade, com a versão atual, a disponível e o custo (1 reboot, ~2-5 min de bridge fora). | **Do usuário.** Se ele não quiser, seguir normalmente e registrar a versão como pendência na tabela de estado dos MKs |
| **= `latest-version`** | Nada a fazer; só registrar. | — |

Upgrade (baixa e reinicia sozinho — **não** existe `download-and-install` nesta versão):
```
/system package update install
```
Depois do reboot: confirmar `version` nova, os **dois** túneis com handshake recente
(`/interface wireguard peers print detail`) e o `/system device-mode print` intacto.

⚠️ **A melhor janela é quando ether1/ether4 ainda estão sem link** — MK já alcançável pela
Connect/automais-vpn mas ainda não carregando a produção da unidade. Foi assim no CDT
(30/07: 7.19.6 → 7.23.2 com impacto zero). Se a unidade já está viva, o upgrade **exige
janela combinada** e vira decisão do usuário mesmo que a versão esteja bem atrasada.

📏 **Custo medido com a unidade viva** (CAPS AD, 31/07, 7.19.6 → 7.23.2): download ~10 MiB +
reboot, **MK fora ~2 min** (SSH voltou entre 105s e 120s), config íntegra, device-mode
preservado, os dois túneis com handshake em menos de 3 min e nenhum reflexo reportado pela
unidade. Ou seja: é caro o suficiente para pedir OK, barato o suficiente para não virar
pendência eterna. Sempre `/export` + `/system backup save` e puxar os dois por SFTP antes.

⚠️ Precisa de internet no MK (`fetch=yes` no device-mode — ver §3b) e de saída pela Connect.

### Convenção FIXA de portas do hEX (rollout 2026-07, todos os MKs novos idênticos)

| Porta | Papel | Placeholder |
|---|---|---|
| `ether1` | **Link Prefeitura** (uplink primário / ONU) | `$UP` |
| `ether2` | **Connect** (internet de failover) | `$CONN` |
| `ether3` | **Internet reserva** (futuro; ainda não existe) — **hoje gestão OOB** | `$GEST` |
| `ether4` | **LAN da unidade** — uplink pro switch core (outra ponta da bridge transparente) | `$LAN` |
| `ether5` | **Circuito de Wi-Fi** — segmento próprio, FORA da bridge transparente (não-confiável; não enxerga LAN nem telefones) | `$WIFI` |

Bridge transparente = `ether1 ↔ ether4`. Quando o link reserva chegar na ether3, a gestão OOB
migra (gestão remota já é pelo `automais-vpn` do automais.io).

### Identidade e credenciais (padrão do rollout)

- **Usuário `becape` (full) substitui o admin** em TODOS os MKs (decisão 2026-07-18 — o
  RouterOS de fábrica marca o admin como EXPIRED e força troca no 1º login interativo):
  `/user add name=becape group=full password="\$180816\$Be8654" comment="admin Becape/Automais"`
  → testar login com `becape` → `/user remove [find name=admin]`.
  ⚠️ **PEGADINHA `\$` também aqui**: a senha tem `$` — via CLI/SSH, dentro de aspas duplas o
  RouterOS EXPANDE `$var`; sempre escapar `\$` (senão a senha grava errada/vazia).
- A senha da **etiqueta** só vale pro primeiro acesso (admin de fábrica) — registrar mesmo
  assim no `unidades.csv` (rastreabilidade do aparelho).
- O automais.io cria o usuário dele (`automais-api`) no bootstrap com senha própria.
- **Usuário somente-leitura da Prefeitura** (igual em TODOS os MKs):
  `/user add name=Prefeitura group=read password="rTz34!xP437" comment="acesso leitura Prefeitura"`
- **Referência única do MK na documentação = MAC da etiqueta** (parte de baixo do aparelho;
  corresponde ao MAC da `ether1`). Sempre registrar esse MAC (formato da etiqueta, sem
  separadores, ex. `04f41cd8dedc`) + a senha da etiqueta no `unidades.csv`.

## Derivação determinística (pelo id — do plano de endereçamento)

- Túnel (IP do MK): `10.201.0.<ID+10>/24` · Hub SIP: `10.201.0.1`
- LAN telefones: `10.200.<ID>.0/24` · gateway `10.200.<ID>.1` · aparelhos `.10–.200` (IP fixo manual)
- Hub Asterisk: `192.241.153.121:51820` · pubkey `b0Da/MUl+tmetIjBN/OKfQqGCKuqtsZNsmx2Glmh9wo=`
- Servidor VOIP (root): `192.241.153.121` (senha na memória `project_telefonia_vpn_wireguard`).

## FLUXO EM 2 FASES (rollout hEX — validado no MK #1 / id 1 Péricles, 2026-07-18)

Templates `.rsc` validados: `Telefonia/scripts/templates/hardening-hex.rsc` e
`failover-hex.rsc` (trocar porta automais 133xx e id da unidade).

### FASE BANCADA (MK novo de fábrica, PC na ether3 com IP 173.20.20.20/24)

1. **Descobrir o MK**: MNDP (broadcast udp/5678) acha o MK em `192.168.88.1` de fábrica;
   dar IP secundário `192.168.88.2/24` no PC pra alcançar. Login inicial: `admin` + senha
   da **etiqueta**. Anotar MAC da etiqueta. **Checar a versão do RouterOS (§0.1)** — na
   bancada não há produção nenhuma para derrubar, então é o momento mais barato de subir.
2. **Criar device no automais.io** (se não existe — o lote 2026-07-18 já criou todos) e
   `bootstrap-activate`; baixar o `.rsc` no PC.
3. **Gestão**: `add address=173.20.20.1/24 interface=bridge` (a bridge defconf) e migrar o
   acesso pra `173.20.20.1` ANTES de mexer no resto.
4. **Limpar defconf**: dhcp-server/pool/dhcp-client/dns-static/nat/filter defconf; tirar
   ether2/4/5 da bridge (ether3 FICA); renomear bridge→`bridge-gestao`; identity
   `MK-BORDA-<UNIDADE>`.
5. **Bridge transparente** ether1↔ether4 + IP telefones `10.200.$ID.1/24` (§1–2).
6. **VOIP**: wg-voip + peer + firewall VOIP-only (§3); registrar pubkey no hub (§3).
7. **Bootstrap automais.io**: subir o `.rsc` por **SFTP** + `/import` (fetch é bloqueado
   pelo device-mode); conferir `automais-vpn` + user `automais-api` (§3b).
8. **Blindagem** (§5, template hardening) + testar conexão nova pela gestão.
9. **Usuários**: criar `Prefeitura` (read) e `becape` (full, escapar `\$`!), testar becape,
   **remover admin**; remover `192.168.88.1` e os `.rsc` do disco do MK.
10. **Device-mode**: `update scheduler=yes fetch=yes` → **alguém aperta o reset/power-cycle**
    (o MK reboota; config persiste). Conferir flags depois.
11. **Failover esqueleto** (template failover): script `FAILOVER-ether1` + scheduler 5s +
    dhcp-client ether2. Sem regras FAILOVER ainda = no-op seguro.
12. **NTP**: `ntp client set enabled=yes` + `pool.ntp.org` + timezone America/Sao_Paulo.
13. Registrar no `unidades.csv` (status `PRE_CONFIGURADA`, mk_mac, senha etiqueta, pubkey).
    Com internet na bancada dá pra validar os túneis (handshake + ping 10.201.0.1 + device
    Online no painel) — no MK #1 os dois subiram.

### FASE SITE (instalação na unidade — MK acessível remoto via 10.35.0.x)

0. **Checar a versão do RouterOS (§0.1)** antes de qualquer coisa. Se ether1/ether4 ainda
   estiverem sem link, essa é a janela ideal para o upgrade — aproveite-a antes do survey.

0b. 🛑 **CONFIRMAR QUE A `ether4` ESTÁ `RUNNING` ANTES DE SURVEYAR.** `/interface ethernet
   print where name~"ether"` — se a `ether4` aparecer só com `S` (sem `R`), ou se ela estiver
   `I` (inactive) na `/interface bridge port print`, **a LAN da unidade não está plugada e o
   survey vai MENTIR**. Sem clientes gerando tráfego, o `torch` na ether1 volta **vazio** (não
   dá para detectar VLAN) e o `dhcp-client` pode ser atendido por **qualquer outro DHCP no
   segmento** em vez do da Prefeitura.
   **Comprovado no CAPS III (31/07):** com a ether4 dark **e o cabo da Prefeitura na porta
   errada**, o survey trouxe `192.168.1.3/24 gw/DHCP/DNS=192.168.1.1` e o quadro parecia "esta
   unidade não tem link corporativo" — cheguei a documentar uma anomalia que não existia.
   Depois do cabo corrigido e da ether4 plugada, o mesmo survey trouxe o real
   (`10.1.55.0/24`, gw `10.1.55.1`, DHCP `10.1.201.254`, DNS `10.135.16.119`/`.17`) em
   **4 de 4** tentativas, e o torch mostrou a LAN inteira. Se tivéssemos aplicado o padrão
   sobre os dados errados, o failover ficaria apontado para o gateway errado.
   ⇒ **ether4 dark = pare, faça só o upgrade (§0.1, janela de graça) e volte depois.**
   ⇒ E se o survey trouxer algo que **não** é `10.1.x.0/24` com DHCP `10.1.201.254`,
   desconfie de **cabeamento** antes de concluir que a unidade é exceção.

0c. 🔌 **UNIDADE SEM LINK DA PREFEITURA — "cabos dobrados" (padrão desde 2026-08-10).**
   Algumas unidades simplesmente não têm link corporativo; têm **um único link**. Nesses
   casos **não se muda a arquitetura do MK**: dobra-se o cabo, ou seja, `ether1` **e**
   `ether2` vão os dois para o roteador desse único link. A bridge transparente
   `ether1↔ether4` continua existindo e o roteador do link segue sendo gateway/DHCP/DNS
   da unidade, como se fosse a Prefeitura. O MK fica só como ponte + nó de VOIP.
   Isso **substitui o desenho do TFD** (id=21), onde o MK virou gateway+DHCP+DNS da LAN
   local: aquele desenho muda a LAN da unidade e dá trabalho pra desfazer; este não muda
   nada, e no dia em que a Prefeitura chegar basta trocar o cabo da `ether1` e rodar a
   FASE SITE normal. (O TFD fica como está — não vale mexer numa unidade viva só por
   uniformidade.)

   **Como reconhecer no survey** (validado no SRT I, 10/08): o torch da `ether1` volta com
   a coluna `VLAN-ID` **vazia** e **zero** endereços `10.1.x.x` / `10.135.16.x` — só a
   faixa do roteador do link; e o dhcp-client TEMP na `bridge-transparente` fecha no
   **mesmo servidor/gateway que a `ether2` já usa**. Essa coincidência é a prova: as duas
   portas estão no mesmo L2. ⚠️ Só concluir isso **depois** de confirmar o cabeamento
   (§0b) — foi exatamente esse quadro que o CAPS III produziu com o cabo na porta errada.

   **O que NÃO se aplica nessas unidades:**
   - **Sem regras FAILOVER e sem DHCP de retaguarda** — não existe segundo caminho; o
     "failover" seria do link para ele mesmo. Deixar o scheduler `FAILOVER-check`
     **desabilitado** (com comentário dizendo o porquê) e o script no disco, inerte.
   - **Sem IP do MK na LAN** (o `<IP_MK_LAN>` = `.254` do §2). O MK **já tem** endereço
     nessa sub-rede pela `ether2`; um segundo IP da mesma rede na bridge poria duas
     interfaces do MK no mesmo domínio de broadcast, com ambiguidade de ARP/rota e zero
     ganho.
   - **Sem liberar ssh/winbox por IP vindo da LAN.** Como a LAN *é* a rede do link
     não-confiável, o pacote chega pela `ether2` e morre na `BLINDAGEM Connect` — e
     afrouxar essa regra exporia a gestão ao wifi/ISP compartilhado. Gestão fica por
     `automais-vpn`, `ether3` e **MAC-Winbox pela ether4** (L2, não depende de IP).

   **O que continua valendo normalmente:** bridge transparente, túnel `wg-voip`, gateway
   dos telefones `10.200.<ID>.1` na `bridge-transparente` (LAN untagged), blindagem §5 e
   os dois WireGuards.
1. Levantar a LAN local: sub-rede, **gateway real**, IP que o MK assume. Truque validado
   (CMI): **dhcp-client temporário na bridge-transparente** (`add-default-route=no
   use-peer-dns=no use-peer-ntp=no comment="TEMP survey LAN"`) revela sub-rede/gateway/DNS;
   remover depois. Conferir `.254` livre (`/ping`) antes de assumir.
   ⚠️ **Se o dhcp-client ficar eternamente em `status=searching...` com a bridge passando
   tráfego, a LAN provavelmente chega TAGGED** (validado no CDT 30/07/2026 — VLAN `1092`).
   Diagnóstico em 1 comando: `/tool torch interface=ether1 src-address=0.0.0.0/0 duration=10`
   — a coluna `VLAN-ID` mostra o tag e as colunas de endereço a faixa real. (`/tool sniffer`
   NÃO serve: é bloqueado pelo device-mode dos hEX e não vê o fast path da bridge.) Nesse caso
   crie `/interface vlan add name=vlan<ID>-lan vlan-id=<VLAN> interface=bridge-transparente` e
   refaça o survey nela. **A bridge continua sem `vlan-filtering`** — os frames tagged seguem
   atravessando ether1↔ether4 transparentes; a interface VLAN só dá ao MK um pé no L2 certo.
2. `add address=<IP_MK_LAN>/24 interface=bridge-transparente` (§2) + ping no gateway real
   com `src-address=<IP_MK_LAN>`. **LAN tagged:** trocar `bridge-transparente` por
   `vlan<ID>-lan` em **tudo** que fala com a LAN — IP do MK, takeover do gw, takeover do IP
   do DHCP real, `in-interface` dos redirects `:53`, `interface` do `FAILOVER-dhcp` **e o
   gateway dos telefones `10.200.<ID>.1`** (os aparelhos plugam em portas de acesso da mesma
   VLAN; se algum dia ficarem em voice-vlan própria, reavaliar).
3. Criar as regras FAILOVER **desabilitadas** com os valores reais (§4: takeover gw /32 +
   masquerade `<LAN>/24` out ether2 + 2 redirects DNS + takeover do IP do DHCP real) — o script
   da bancada assume dali. E criar o **DHCP de retaguarda JÁ HABILITADO** (§4: `authoritative=no`
   + `delay-threshold=5s`, DNS reais no escopo). Trocar o script da bancada (v1, só físico) pelo
   do template (detecção v2 + inversão de DHCP v3) — **as unidades pré-configuradas em 18/07
   estão todas com a v1**.
4. **Acesso local (PADRÃO, decisão 2026-07-19)**: liberar gestão local pela LAN da unidade
   e pela porta de gestão, mantendo Prefeitura/Connect bloqueadas:
   - `/ip service set ssh address=173.20.20.0/24,10.35.0.0/24,<LAN>/24` (idem `winbox`);
   - `/interface list member add list=MGMT interface=ether4 comment="LAN local - MAC/discovery"`
     → MNDP/CDP/LLDP + MAC-Winbox/MAC-Telnet só por ether4 (LAN), ether3 (gestão) e
     automais-vpn; **ether1/ether2 ficam FORA** (sem anúncio, sem MAC).
   - Nuance: filtro por porta vale pro MAC/discovery; acesso IP é por origem `<LAN>/24`
     (ether1↔ether4 são a mesma L2 na bridge).
5. Testar failover (puxar cabo ether1 → log ATIVADO → navegar → recolocar → DESATIVADO).
6. Validar: handshakes, telefone registrando no Asterisk, NTP `synchronized`.
7. `unidades.csv` → status `ATIVA` + `mk_lan_atual`; doc da unidade em
   `Infraestrutura/<Unidade>/`.

## Como falar com o MK (helpers paramiko no scratchpad)

RouterOS não faz port-forward em SSH. Se o MK não for alcançável direto: dar IP temporário ao
CCR na LAN (`/ip address add .../bridge`) + `masquerade` e usar como salto, OU acessá-lo pela
gestão. Comando via SSH RouterOS = **com barra inicial** (`/system identity print`).
Senha admin: hEX novos = **senha da etiqueta** (registrada no `unidades.csv`); MKs antigos
(Complexo) = memória `project_telefonia_vpn_wireguard`. MK de fábrica: `192.168.88.1`,
descoberta via MNDP (broadcast udp/5678 — helper `mndp_listen.py` no scratchpad).

## 1. Bridge transparente (uplink Prefeitura ↔ LAN da unidade)

⚠️ **NUNCA romper** — carrega a produção do cliente. Adicionar IPs/WG/firewall é aditivo e não
afeta o L2 (a bridge tem `use-ip-firewall=no`; tráfego bridgeado não passa no firewall IP).

```
/interface bridge add name=bridge-transparente protocol-mode=none comment="L2 transparente ONU<->switch L3"
/interface bridge port add bridge=bridge-transparente interface=$UP
/interface bridge port add bridge=bridge-transparente interface=$LAN
# CCR: fixar combo em cobre se for o caso: /interface ethernet set $UP combo-mode=copper
/system identity set name=MK-BORDA-<UNIDADE>
```
Upgrade de RouterOS com a bridge **já fechada** (v6→v7 obrigatório, ou o opcional que o usuário
aprovou — ver §0.1) roda `/system package update install` **em janela combinada**: o reboot
derruba a bridge por ~2-5 min e isso é a produção do cliente. Confirmar depois
`/system resource print` = 7.x + `/interface wireguard print`.

## 2. Gestão + identidade na LAN da unidade

```
/ip address add address=173.20.20.1/24 interface=$GEST comment="gestao OOB"
/ip address add address=<IP_MK_LAN>/24 interface=bridge-transparente comment="MK LAN id=$ID"
/ip address add address=10.200.$ID.1/24 interface=bridge-transparente comment="gw telefones id=$ID"
```

## 3. VPN VOIP (WireGuard até o hub) — usar `mikrotik-gen.sh $ID` como base

```
/interface wireguard add name=wg-voip listen-port=51820 comment="VOIP hub SMS Marica"
/ip address add address=10.201.0.<ID+10>/24 interface=wg-voip comment="tunel voip id=$ID"
/interface wireguard peers add interface=wg-voip \
  public-key="b0Da/MUl+tmetIjBN/OKfQqGCKuqtsZNsmx2Glmh9wo=" \
  endpoint-address=192.241.153.121 endpoint-port=51820 \
  allowed-address=10.201.0.0/24 persistent-keepalive=25s comment="hub asterisk"
# firewall VOIP-only dos telefones:
/ip firewall filter add chain=forward in-interface=wg-voip dst-address=10.200.$ID.0/24 action=accept comment="VOIP hub->tel id=$ID"
/ip firewall filter add chain=forward src-address=10.200.$ID.0/24 out-interface=wg-voip action=accept comment="VOIP tel->hub id=$ID"
/ip firewall filter add chain=forward src-address=10.200.$ID.0/24 action=drop comment="VOIP tel fora do tunel id=$ID"
```
Pegar a pubkey do MK (`/interface wireguard print detail where name=wg-voip`) e **registrar no hub**:
```
ssh root@192.241.153.121  ->  /opt/telefonia/hub-add-unidade.sh --id $ID --pubkey <PUB> --nome "<UNIDADE>"
```
(idempotente por id — substitui a chave anterior; não reinicia o Asterisk). Validar handshake:
MK `/interface wireguard peers print detail` (last-handshake) + `/ping 10.201.0.1 interface=wg-voip`.
**Sem NAT** no wg-voip (o hub precisa ver o IP real `10.200.$ID.x`). Atualizar `unidades.csv`
(mk_pubkey + status ATIVA).

## 3b. Registrar o router no automais.io (tenant Saúde Maricá) + bootstrap

Todo MK de unidade vira um **ManagedDevice** (`Kind="MikrotikRouter"`) no automais.io.
Repo: `C:\Projetos GIT\Automais.IO` (ler `docs/FOR_AI_AGENTS_MANAGED_DEVICES.md`).
API prod: `https://api.automais.io/api` (JWT admin via `POST /api/auth/login`).

1. **Criar o device** (idempotente por nome — conferir antes se já existe):
   `POST /api/tenants/{tenantId}/managed-devices` com
   `{ "name": "MK-BORDA-<UNIDADE>", "kind": "MikrotikRouter", "vpnNetworkId": "<guid>" }`
   → já provisiona o peer WireGuard (chaves + IP na `automais-vpn`).
2. **Ativar bootstrap**: `POST /api/managed-devices/{id}/bootstrap-activate`
   → devolve URL de um `.rsc` (token TTL 60min) + senha temporária do usuário `automais-api`.
3. **No MK**: colar os 2 comandos (`/tool/fetch` da URL + `/import`). O `.rsc` é idempotente:
   cria a interface WG `automais-vpn`, usuário `automais-api group=full`, firewall (API 8728 +
   SSH só da origem VPN) e faz callback de conclusão. Exige RouterOS v7 (aborta em v6).
4. **Validar**: device fica online no painel; serial/MAC/modelo/firmware são detectados
   sozinhos pelo `routeros.service` (loop 60s pela RouterOS API via VPN).

⚠️ **Porta WG do automais-vpn é dinâmica por device**: `13300 + último octeto do IP VPN`
(evita colisão de NAT entre MKs do mesmo site). Anotar a porta — a blindagem (§5) precisa
liberá-la na entrada da Connect.

⚠️ **PEGADINHA device-mode (hEX novos, RouterOS 7.19+): vêm em `mode=home` com
`scheduler=no` e `fetch=no`.** Consequências: (a) o `/tool fetch` do fluxo oficial de
bootstrap NÃO funciona — alternativa validada: baixar o `.rsc` no PC e subir por **SFTP**
(paramiko `sftp.put`) + `/import file-name=...`; (b) o scheduler do FAILOVER (§4) e o
SAFETY-revert (§5) não podem ser criados. **Habilitar no setup de bancada:**
`/system device-mode update scheduler=yes fetch=yes` → o comando fica aguardando
**confirmação física** (apertar o botão reset OU desligar/ligar o aparelho) em ~5 min —
precisa de alguém junto do MK. Sem isso o failover não roda.

Cada MK termina com **pelo menos 2 WireGuards**: `wg-voip` (51820, hub Asterisk) + `automais-vpn`
(133xx, gestão automais.io) — e, com o multi-link do §3c, até mais 3 auxiliares
(`automais-vpn-a/b/c`, um por link WAN).

## 3c. VPN multi-link no automais.io — um túnel de gestão POR LINK (A/B/C)

Recurso da plataforma (2026-07-31, `docs/FOR_AI_AGENTS_VPN.md` §Multi-link no repo Automais.IO).
Além do `automais-vpn` (peer primário), o device pode ter até **3 túneis auxiliares**, um por
link WAN, criados **direto no automais.io** — painel (device → aba Network → "VPN por link") ou
API `POST /api/managed-devices/{id}/vpn-links` com `{ "linkRole": "A|B|C", "label": "...",
"mikrotikGateway": "<gw do link ou null>" }`. Cada link ganha peer/keypair/IP VPN próprios;
**re-rodar o bootstrap** (§3b — SFTP+`/import` por causa do device-mode) aplica tudo no MK.

**Por que**: gestão sobrevive à queda de qualquer link (todos os túneis ficam de pé, sem
failover de VPN), e o handshake/ping POR túnel no painel é telemetria POR LINK — túnel A morto
+ túnel B vivo = perna da Prefeitura caída, sem investigação.

⚠️ **Não é o que teria resolvido o incidente Péricles 29/07** (correção de 31/07 — a redação
anterior prometia isso). Lá **os dois túneis saíam pelo MESMO link**: a única rota default do
MK é via a Connect, e o link da Prefeitura é só bridgeado — o MK não tem rota por ele. O
`wg-voip` ficou de pé o tempo todo e só o `automais-vpn` caiu, então não foi "uma perna que
sumiu", foi o peer do automais.io sobre um caminho que estava funcionando. O que o multi-link
acrescenta nesse cenário é outra coisa, e é útil: se os auxiliares ficarem de pé e só o
primário cair, o problema fica **localizado no peer primário** em vez de virar mistério.

**Convenção SMS Maricá (papel do link é FIXO, espelha as portas do hEX):**

| Role | Link | `mikrotikGateway` | Observação |
|---|---|---|---|
| `A` | **Prefeitura** (ether1 → bridge **ou** interface VLAN da LAN, se a unidade for tagged) | `<GATEWAY_REAL>` da LAN da unidade | O MK JÁ tem L3 nesse caminho (`<IP_MK_LAN>`, §2) e a rota resolve certo — medido no CAPS AD: `immediate-gw=10.1.96.1%vlan1102-lan`, rota `As`. 🔴 **PORTÃO — sonda NEGATIVA, ver aviso abaixo** |
| `B` | **Connect** (ether2) | gw do dhcp-client da ether2 (tipicamente `192.168.0.1`) | Pinado à Connect por rota própria — segue medindo a Connect mesmo se a default route mudar um dia |
| `C` | **Reserva** (ether3, futuro) | **null** (sem gateway) | **Criar JÁ** — peer/chaves/IP ficam provisionados; sem gateway o túnel sai pela default route (sobe e handshaka via Connect, telemetria duplica a B até existir link). Quando o link chegar: `PATCH .../vpn-links/{linkId}` com o gateway + re-bootstrap → pina na ether3 |

**O que o bootstrap gera no MK por link** (tudo taggeado `Automais.IO multilink`, idempotente,
papéis removidos no painel são limpos no re-bootstrap):
- interface `automais-vpn-<role>` com keypair próprio e porta determinística:
  **A = `14300+octeto`, B = `15300+octeto`, C = `16300+octeto`** (octeto = último octeto do IP
  VPN do túnel auxiliar, visível no painel);
- endereço **/32** (só o primário leva /24 — evita rota conectada duplicada cuja resposta
  assimétrica o servidor dropa por crypto-key routing);
- routing table `automais-link-<role>` (CIDR da VPN via a própria interface + default via o
  gateway do link com `check-gateway=ping`) + mangles (src-port pina o UDP cifrado ao link;
  mark-connection faz a resposta sair pela interface por onde entrou);
- regras de firewall input (ICMP/22/8728 da origem do servidor VPN) em CADA interface aux —
  gestão funciona por qualquer túnel vivo.

🔴 **O link A NÃO se sustenta pela Prefeitura — a IDA funciona, a VOLTA não.** Testado de
verdade no CAPS AD em 31/07: link A criado no automais.io (`vpnIp 10.35.0.50`, porta `14350`,
gw `10.1.96.1`), bootstrap importado, interface `automais-vpn-a` RUNNING e **handshake
completou**. E aí parou. Números, medidos em janelas de 30s nos dois lados:

| | MK | Servidor |
|---|---|---|
| Enviado | `tx` 15,0 → 15,9 KiB (cresce) | `sent` 10,27 → 10,81 KiB (cresce) |
| Recebido | `rx` **284 B — congelado** | `received` 13,16 → 14,03 KiB (cresce) |

O servidor **recebe** os pacotes do MK (contador sobe) e o endpoint aparece como
`186.193.246.225` — o NAT público da Prefeitura, provando que o tráfego saiu mesmo por lá e
não pela Connect (que é `179.42.148.53`). Mas o `rx` do MK ficou travado nos 284 bytes do
primeiro handshake e **nunca mais subiu**: a borda deixou passar uma janela inicial e depois
fechou o retorno. Handshake envelhece indefinidamente, ping do painel a `10.35.0.50` dá 100%
de perda nos dois sentidos.

**Descartado o que parecia culpado:** MK, rotas (`As`, `immediate-gw` correto), mangles
(contadores subindo), AllowedIPs (só o peer A reivindica `.50`, sem sobreposição), rota e
nftables do servidor (`.50` salta para a mesma chain de `.27`). Está tudo certo dos dois lados
— **o problema é a borda da Prefeitura**. Também não adianta criar redes/segmentos separados
por link no automais.io: o escopo compartilhado `10.35.0.0/24` não é o problema.

Sondas auxiliares (mangle `chain=output` + routing table temporária, com `pref-src` correto):
**ICMP volta** (~3ms, TTL 118); **TCP 443 e UDP 53 saem e não voltam**. Coerente com o achado
do túnel: saída NATeada normalmente, retorno de sessão negado para esse host.

**Pedido concreto para a TI da Prefeitura:** liberar, para o host `<IP_MK_LAN>` (MikroTik de
borda da unidade), **retorno de sessão estabelecida** em TCP e UDP — destino necessário
`api.automais.io` (`198.211.104.55`) **UDP 51825**. Enquanto isso não existir, **não criar o
link A**: ele só gera keepalive inútil e fica vermelho no painel. Os links **B (Connect)** e
**C** não têm esse problema.

⚠️ **Duas armadilhas de MEDIÇÃO que me fizeram concluir errado antes** (a redação anterior
desta seção dizia que o MK nem saía — estava errada):
1. **`pref-src` é obrigatório na rota de teste.** Sem ele, o MK escolhe origem pela interface
   e pode sair com `10.200.<ID>.1` (a rede dos telefones), que a borda descarta com razão —
   dá 100% de timeout e imita "portão fechado". Fixe `pref-src=<IP_MK_LAN>`.
2. **Escolha do alvo.** `/ping` do RouterOS 7.23 **não** aceita `routing-table=` (por isso o
   mangle). Use um IP que nada no MK use: `1.1.1.1` serve, `8.8.8.8`/`9.9.9.9` **não** (são os
   forwarders do `/ip dns`). E **nunca** mangleie `198.211.104.55` nem `192.241.153.121` —
   desviar esses derruba a própria gestão e o VOIP.

⚠️ **Interação com o failover (§4) — verificada, e é benigna.** Durante o failover o MK
**assume o `<GATEWAY_REAL>`** (`FAILOVER gw takeover`), que é justamente o `mikrotikGateway`
do link A. A dúvida era se o `check-gateway=ping` passaria a acertar o próprio MK (self-ping)
e reportar o link A como vivo com a Prefeitura morta — **não acontece**. Testado no CAPS AD:
rota com next-hop num IP que o próprio MK possui fica **`Is` (inativa)** com
`immediate-gw=""`; o RouterOS recusa resolver endereço local como next-hop. Então o túnel A
cai durante o failover, que é o sinal semanticamente correto. **Mas registre o limite:** o
túnel A fica inativo *por construção* enquanto o failover durar, então **o painel não serve
para detectar a VOLTA do link da Prefeitura** — quem detecta a volta continua sendo o delta de
`rx-packet` do script (§4). Não trocar um pelo outro.

⚠️ **Blindagem (§5)**: a regra `WireGuard VPNs` na entrada da Connect precisa incluir as portas
dos túneis que saem pela Connect — além da `<PORTA_AUTOMAIS_VPN>` (133xx), somar a do túnel B
(`153xx`) e a do C enquanto ele não estiver pinado (`163xx`). O túnel A entra pela
Prefeitura/bridge e não passa por essa regra.

⚠️ **O que o multi-link NÃO faz (fase 2 pendente)**: a LAN da unidade (RemoteNetworks) continua
roteada pelo peer primário. Os auxiliares são gestão+telemetria; mover o payload de LAN pra um
túnel sobrevivente é escopo futuro na plataforma.

**Fluxo resumido pra "pegar um MK e criar as VPNs dos 3 links"**: device já registrado (§3b) →
criar links A (gw real da LAN), B (gw da Connect) e C (sem gw) no painel/API →
`bootstrap-activate` → SFTP+`/import` → validar: 4 handshakes
(`/interface wireguard peers print detail`), ping do painel nos 3 IPs auxiliares, e painel
mostrando os 3 links verdes. Piloto natural: Complexo Regulador (3 links reais).

## 4. Failover de internet pela Connect (detecção v2 em camadas + DHCP de contingência)

> 🔴 **SUPERADO PELO v5 (02/09/2026) — leia `Telefonia/docs/failover-v5/README.md` ANTES de usar esta seção.**
>
> O desenho descrito abaixo (v2/v3/v4) **falhou em produção em 02/09/2026**: a internet da Prefeitura
> caiu **acima do gateway** e o failover não entrou, porque as três camadas de detecção só medem o
> enlace local (porta up, contador de pacotes, ping ao gateway). Naquele dia: porta ON, 39.035 pacotes
> em 5 s e o gateway respondendo — enquanto `ping 8.8.8.8` dava 100 % de perda.
>
> Pior, a comutação por **takeover do IP do gateway** é estruturalmente quebrada: possuir o IP do
> gateway deixa o MK **surdo** ao roteador real (o kernel descarta pacotes com origem local como
> *martian* e não responde ARP cujo sender é IP local). Por isso o takeover só "funciona" com a porta
> da Prefeitura desabilitada, e é a causa do "uma parte funciona, outra não, e oscila".
>
> **O que o v5 faz no lugar:** o MK **nunca** assume o IP do gateway — intercepta em L2 os quadros que
> os clientes já endereçam ao **MAC do roteador da Prefeitura** (`bridge nat action=redirect`) e
> responde ARP com **o MAC deles** (`arp-reply`). Convergência 0 s. Detecção por **sonda de internet
> fim-a-fim pela perna da Prefeitura** (ICMP + TCP + DNS externo pelo DC), volta com **2 min** de
> estabilidade, estado ancorado em objeto de configuração (não em variável de RAM). Validado em
> produção no Complexo em 02/09, ida e volta, sem derrubar a unidade.
>
> Esta seção fica como **referência histórica** e porque 10 das 12 unidades ainda rodam o v2 —
> ver o estado real em `Telefonia/docs/frota-check-020926.md`.

### §4b. PMTU e MSS por link de saída — OBRIGATÓRIO em todo link que não seja o da Prefeitura

**Regra de ouro: "o link está instável" é hipótese PROIBIDA enquanto o teste de DF não tiver sido feito.**

Descoberto no Complexo em 02/09: a "instabilidade da Connect" era **buraco negro de PMTU** — o caminho
tem PMTU **1480**, pacotes de 1484+ somem em silêncio e o ICMP *fragmentation needed* não volta. Ping
pequeno dá 0 % de perda e os contadores da porta ficam limpos, então o link *parece* perfeito.

```
# medir (size= no RouterOS e o pacote IP TOTAL, nao o payload)
/ping 8.8.8.8 size=1500 do-not-fragment count=2 interface=<link>   # falha
/ping 8.8.8.8 size=1480 do-not-fragment count=2 interface=<link>   # passa -> PMTU=1480
/ping <gw do link> size=1500 do-not-fragment count=2               # controle: CPE aceita 1500

# corrigir (os DOIS mecanismos)
/interface ethernet set <link> mtu=<PMTU>
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn out-interface=<link> \
    tcp-mss=<PMTU-39>-65535 action=change-mss new-mss=<PMTU-40> comment="MSS clamp <link> (PMTU <v> medido <data>)"
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn in-interface=<link> \
    tcp-mss=<PMTU-39>-65535 action=change-mss new-mss=<PMTU-40> comment="MSS clamp <link> in"
```

Túnel WireGuard sobre o link: **`MTU_WG + 60 ≤ PMTU`**. Com PMTU 1480 o padrão 1420 fica com margem
zero — considerar 1380. Registrar em `unidades.csv` (`pmtu_connect`, `pmtu_medido_em`).
Doc: `Telefonia/docs/pmtu-links-de-saida.md`.

### §4c. Saída pela Connect é SEMPRE por túnel ao Datacenter Automais

A Connect **é CGNAT** (`100.64/10`) com CPE compartilhado por dezenas de terceiros — o bloco de portas
esgota e derruba **conexão nova** (medido no Complexo: 126-147 `syn-sent` sustentados). **NAT direto na
Connect está proibido.**

Padrão: interface `wg-eveo` sempre de pé para o **CCR2116 da Eveo** (IP público `177.136.233.75`), com
o endpoint **pinado por rota `/32` pela física**, MSS clamp e faixa `10.203.0.0/24` (DC = `.1`,
unidade = `.<id+10>`). Gestão, VOIP e demais túneis **não** passam por ele.
Destinos internos da Prefeitura (`10.0.0.0/8`, `172.16.0.0/12`) **nunca** vão pelo túnel.

Serviços essenciais (SISREG, SER, smsmarica) **seguem o link ativo**, sem exceção de rota — existe a
address-list `SERVICOS-ESSENCIAIS` preparada e inerte, caso um dia seja preciso desviar só eles.
M-EGRESS 02/09: SISREG responde **200** pelo IP da Eveo (o bloqueio antigo era **geográfico**, EUA).

Assume gateway+DNS(+DHCP) e sai pela Connect. **Detecção v2 EM CAMADAS** (lição do incidente
Regulação 20/07/2026 — porta ON com **fibra partida atrás da ONU** e o failover v1 não entrou):
- **ENTRAR** (estado normal, 3 ciclos ~15s de "morto"): `$UP running=false` **OU** (delta
  `rx-packet` do $UP < 50/ciclo **E** `ping <GATEWAY_REAL> count=2` sem resposta). Sem risco de
  self-ping: no estado normal o takeover está desabilitado. RX alto = vivo sem ping (gw que
  corta ICMP sob carga).
- **SAIR** (estado failover, 6 ciclos ~30s): `running=true` **E** delta `rx-packet` ≥ 2/ciclo
  (chatter do head-end voltando). **Ping NÃO serve na saída** — o MK é dono do gw (self-ping).
- ⚠ hEX: **validar na bancada** que `rx-packet` da $UP cresce com tráfego na bridge
  hw-offloaded; se ficar parado, `hw=no` na bridge port da $UP.

Regras **pré-criadas desabilitadas**; script liga/desliga. Template pronto:
`Telefonia/scripts/templates/failover-hex.rsc` (o script descobre o IP do gw pelo próprio
objeto `FAILOVER gw takeover` → sem o objeto, degrada p/ detecção só-física = no-op de bancada).
Ver desenho completo em `Telefonia/docs/failover-v5/README.md`.

```
/ip dns set servers=8.8.8.8,9.9.9.9         # forwarders (NÃO usar o alvo de sonda)
/ip address add address=<GATEWAY_REAL>/32 interface=bridge-transparente comment="FAILOVER gw takeover" disabled=yes
/ip firewall nat add chain=srcnat action=masquerade src-address=<LAN>/24 out-interface=$CONN comment="FAILOVER nat clientes connect" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=udp dst-port=53 src-address=<LAN>/24 in-interface=bridge-transparente to-ports=53 comment="FAILOVER dns udp" disabled=yes
/ip firewall nat add chain=dstnat action=redirect protocol=tcp dst-port=53 src-address=<LAN>/24 in-interface=bridge-transparente to-ports=53 comment="FAILOVER dns tcp" disabled=yes
```

**DHCP de retaguarda — SEMPRE LIGADO** (v3, decisão 2026-07-29; substitui o "DHCP só no
failover"). O DHCP da unidade é central e fica atrás do link da Prefeitura; o lease real é de
**8 h**, então numa queda longa todo mundo cai de uma vez (foi o que parou a Regulação em
20/07). O MK roda um DHCP **cópia fiel** do real, **habilitado 24/7**, mas deferente:

| Estado | `delay-threshold` | `authoritative` | Efeito |
|---|---|---|---|
| Normal | `5s` | `no` | ignora DISCOVER com `secs`<5s → só responde depois que o cliente já tentou e não teve resposta a tempo. Cobre na hora quem liga o PC durante uma queda |
| Failover | `0s` | `yes` | responde na hora e manda NAK em lease velho → cliente pega IP novo em segundos em vez de esperar as 8 h vencerem |

O script inverte esses dois parâmetros nas transições (não habilita/desabilita mais o servidor).
Levantar antes: faixa do pool, IP do servidor DHCP, DNS e domínio reais (padrão PMM observado em
Regulação, Boqueirão e Péricles: pool `.10-.200`, DHCP `10.1.201.254`, DNS `10.135.16.119`+`.17`,
domínio `pmm.local` — confirmar só a sub-rede/gw pelo survey):

```
/ip pool add name=pool-failover-lan ranges=<POOL_INI>-<POOL_FIM>
/ip dhcp-server network add address=<LAN>/24 gateway=<GATEWAY_REAL> dns-server=<DNS_REAL_1>,<DNS_REAL_2> domain=<DOMINIO> comment="FAILOVER rede (copia fiel DHCP real)"
/ip address add address=<IP_DHCP_REAL>/32 interface=bridge-transparente comment="FAILOVER dhcp takeover" disabled=yes
/ip dhcp-server add name=FAILOVER-dhcp interface=bridge-transparente address-pool=pool-failover-lan lease-time=10m authoritative=no delay-threshold=5s conflict-detection=yes comment="FAILOVER dhcp" disabled=no
```

### Como o `delay-threshold` REALMENTE funciona (não confundir com detecção do outro servidor)

São 3 mecanismos independentes, sem comunicação entre si — importante entender os limites de
cada um antes de assumir que "o DHCP nosso só entra se o real cair":

1. **`delay-threshold` não escuta a rede pra ver se tem outro DHCP respondendo.** Ele só olha o
   campo `secs` que o próprio cliente manda no DISCOVER (segundos desde que começou a tentar
   pegar IP; a maioria começa em `secs=0`). Com `delay-threshold=5s`, ignoramos qualquer DISCOVER
   com `secs`<5 — no primeiro pedido ficamos calados, dando a vez ao servidor real. **A partir do
   retry seguinte (`secs`≥5), não tem mais discriminação nenhuma: é corrida pura de latência.**
   Quem responder primeiro ganha — o cliente aceita a primeira oferta que chega. Nosso servidor
   está na própria LAN (resposta em milissegundos); o real fica atrás de um **relay** no núcleo
   L3 da Prefeitura, que intercepta o broadcast e reencaminha unicast pro servidor central — esse
   caminho pode legitimamente passar dos 3-5s sob carga normal, sem que o servidor real esteja
   fora do ar. **Validado no Boqueirão (29/07):** um grupo de aparelhos (NVRs, impressoras Pantum
   `class-id=udhcp 1.22.1`, um roteador TL-WR940N) perdia essa corrida pro nosso lado
   sistematicamente, ciclo após ciclo a cada ~10 min (o lease curto), mesmo com `delay-threshold=3s`
   — não porque a Prefeitura estava fora do ar, mas porque nosso caminho é mais rápido que o dela
   pra esses hosts especificamente. Clientes DHCP embarcados baratos (`udhcp`) tendem a agravar
   isso: não fazem renovação unicast decente, reagem à expiração do lease com um DISCOVER novo —
   cada expiração vira uma corrida nova. **Se depois de subir pra 5s o mesmo padrão aparecer nos
   logs (`FAILOVER-dhcp assigned` se repetindo pros mesmos MACs a cada lease), é sinal de que o
   relay real está genuinamente lento nesse segmento, não que 5s ainda é pouco** — investigar a
   infra da Prefeitura antes de subir o valor de novo.
2. **A tabela ARP é o `conflict-detection`, e atua só no momento da entrega.** Antes de oferecer
   um IP, o RouterOS manda um ARP pra esse endereço e espera uma resposta curta; se alguém
   responder, marca como ocupado e pula pro próximo do pool (validado no Boqueirão: log
   `Detected conflict by ARP response for 10.1.108.148/.165`). Isso protege contra entregar o IP
   de quem está **ligado e respondendo agora** — não protege contra entregar o IP de um
   dispositivo com reserva estática no DHCP real que esteja **desligado** no momento (ARP não
   denuncia isso).
3. **Uma corrida perdida captura o host até ele reiniciar** (constatado no Péricles, 31/07).
   O `delay-threshold` só filtra **DISCOVER** — ou seja, broadcast. Depois que o cliente aceita
   nosso OFFER, a **renovação é unicast direto pro servidor que concedeu o lease** (T1, metade
   do lease): não passa por broadcast, não passa pelo `delay-threshold`, e o servidor real nunca
   fica sabendo. O cliente só volta a fazer broadcast se a gente parar de responder (rebind) ou
   se ele reiniciar / trocar de rede. **Efeito prático:** o DHCP de retaguarda acumula hosts
   capturados ao longo do tempo, em silêncio. No Péricles eram 3 estações (`DLM44W2`,
   `D5LLSY2`, `micro-PC` — Dell/Windows) renovando contra nós a cada 10 min, com só **4
   concessões em ~4 h de log** (padrão bem diferente do Boqueirão, onde os mesmos MACs
   reapareciam a cada ciclo). Não quebra nada — a gente entrega cópia fiel do escopo real
   (gw, DNS, domínio corretos) — mas esses hosts ficam **fora dos registros do DHCP da
   Prefeitura**: sem honrar reserva, com lease de 10 min em vez de 8 h, e invisíveis no IPAM
   deles. Para devolver um host ao servidor real não basta esperar: tem que reiniciar o
   cliente (ou dar `/ip dhcp-server lease remove` e forçar rebind).
4. **Não existe "tabela do IP" compartilhada entre os dois servidores.** Cada DHCP mantém sua
   própria base de leases isolada; o nosso não sabe quais IPs o real já reservou. Como copiamos o
   **mesmo pool** (`.10-.200`, igual ao real), qualquer IP que o real considere reservado pra um
   dispositivo específico, mas que não esteja ativo no momento, é IP livre do ponto de vista do
   nosso — só o ARP-probe do item 2 reduz esse risco, e só pra quem está ligado.

⚠️ **O DNS entregue é SEMPRE o real**, nunca `8.8.8.8`. Quem troca o DNS na queda é o **redirect
`:53`** — vale instantaneamente pra todo mundo, inclusive quem já tem IP. Mexer no `dns-server` do
escopo só afetaria lease novo e deixaria DNS público na mão do cliente por até 1 lease **depois**
da volta do link (era o caso da Regulação até julho; corrigido). O `conflict-detection` (ARP-probe antes de oferecer) é o que evita IP duplicado
convivendo com o DHCP real. O takeover do IP do servidor real continua **só no failover** — assumir
o IP dele permanentemente seria sequestrar o serviço.

**Sobre AD/autenticação:** durante a queda os DCs (`10.135.16.x`) estão atrás do link caído e a
Connect não chega neles — login de domínio só sobrevive por credencial em cache, e nenhum ajuste
de DHCP/DNS muda isso. O objetivo do failover é **internet**; o que dá pra garantir é que na
VOLTA ninguém fique com DNS público em mãos (por isso DNS real sempre).

Script (scheduler 5s). **PEGADINHA CRÍTICA:** ao criar `source="..."`, o RouterOS EXPANDE `$var`
dentro de aspas duplas → **escapar todo `$` como `\$`** (senão o script vira lixo e não funciona).
Globals (`$foState`/`$foCnt`/`$foRx`) são compartilhados entre sessões e o scheduler os enxerga.
Ordem no ON: nat FAILOVER → `allow-remote-requests=yes` → takeover gw → takeover dhcp → DHCP
`authoritative=yes/delay=0s` → **limpar conntrack da LAN**; no OFF: inversa (DHCP primeiro,
**conntrack depois do NAT**, DNS por último). Testar: puxar cabo do $UP (físico) E derrubar só o
upstream da ONU com cabo no lugar (camada 3).
> Seguro p/ AD: DNS substituto não quebra relação de confiança (isso é senha de conta de máquina,
> só contra DC); forward público dá NXDOMAIN p/ nomes internos (nunca resposta errada).
> ⚠ Se o failover ativar, o acesso a serviços remotos muda de rota: latência a `smsmarica.app.br`
> (DO/NYC) pode dobrar (ex. Connect/Forte→Cogent ~260ms vs ~120ms na rota boa) — degradação
> esperada, não é defeito.

### Limpar o conntrack nas DUAS transições (v4, lição da Regulação 29/07/2026)

**Obrigatório.** Em ambas as transições o script apaga as conexões da LAN:

```
/ip firewall connection remove [find src-address~"<LAN3>"]
```

`<LAN3>` = primeiros três octetos da LAN, sem ponto final (`10.1.18` para `10.1.18.0/24`).
No template `failover-hex.rsc` já está nos dois blocos, como placeholder a substituir.

**Por que devolver IP/DHCP/DNS/NAT não basta.** Na volta do link o script devolve tudo
corretamente e **as sessões não migram**, por três motivos que se somam:

1. **Cache ARP do cliente.** O PC aprendeu `<GW_REAL>` → MAC **do MK** durante o failover, e o MK
   não pode anunciar o MAC do roteador da Prefeitura de volta (não é IP dele). Enquanto o cache
   não expira, o PC continua entregando pacote ao MK — que, tendo rota default, **segue roteando
   pela Connect e fura o proxy/filtro da Prefeitura sem ninguém perceber**.
   *(Na entrada é diferente: ali o MK **adiciona** o endereço, e a suspeita é que o RouterOS o
   anuncie — mas isso **não foi confirmado**: 3 tentativas de captura falharam porque o
   `/tool sniffer` não vê tráfego da bridge em fast path, inclusive um controle positivo. Fica
   como pergunta aberta; medir do lado do cliente na próxima transição real.)*
2. **Conexão TCP longa não pode migrar.** Push do Google (`:5228`), agente de EDR (`:1514`) e
   afins foram estabelecidos com o IP público da Connect. Se o pacote passar a sair pela
   Prefeitura, o IP de origem muda e o outro lado rejeita — a conexão fica grudada no caminho
   antigo indefinidamente.
3. **O NAT já foi desligado.** Então conexão **nova** de um PC com ARP velho sai com origem
   privada (`10.x.y.z`) e o provedor descarta. Sintoma reportado: *"o que já estava aberto
   funciona, o que eu tento abrir agora não"* — parece queda de link, é meia-conexão.

**Evidência:** na Regulação, após a volta do link em 29/07, **341 conexões** continuaram sendo
roteadas pelo MK e saindo pela Connect/Starlink. Só normalizou quando todas foram apagadas — o
que o operador teve que fazer manualmente antes desta correção existir.

#### Reforço opcional: regra ANTI-BYPASS (preparada, NÃO aplicada)

Fecha o lado do ARP e garante que nada da unidade fure o proxy da Prefeitura:

```
/ip firewall filter add chain=forward src-address=<LAN>/24 action=reject \
    reject-with=icmp-net-unreachable log=yes log-prefix="ANTI-BYPASS" \
    comment="ANTI-BYPASS LAN fora do failover" disabled=yes
```

Com `use-ip-firewall=no` na bridge, a **única** coisa que chega na chain `forward` com origem na
LAN é tráfego que o MK está **roteando** — exatamente o que não deve existir fora do failover.
O PC de ARP velho leva erro na hora, reconsulta e se corrige em segundos.

⚠️ **Não amarrar ao evento da transição.** Se a regra ficar ligada durante um failover, a unidade
fica **sem internet nenhuma** — trocar "fura o proxy" por "não tem internet" é pior. O script deve
**reconciliar a cada tick** (5s), com guarda para o caso da regra não existir:

```
:local abComment "ANTI-BYPASS LAN fora do failover";
:if ([:len [/ip firewall filter find where comment=$abComment]] > 0) do={
  :local abId [/ip firewall filter find where comment=$abComment];
  :if ($foState="on") do={ :if ([/ip firewall filter get $abId disabled]=false) do={/ip firewall filter disable $abId} }
  else={ :if ([/ip firewall filter get $abId disabled]=true) do={/ip firewall filter enable $abId} } }
```

Assim, mesmo que uma transição falhe, o tick seguinte corrige em 5 segundos. Testar as **duas**
direções antes de deixar em produção.

### Estado dos MKs ativos

> ⚠️ **A tabela abaixo está DESATUALIZADA (julho/agosto).** O levantamento real da frota inteira
> foi refeito em 02/09/2026 e está em **`Telefonia/docs/frota-check-020926.md`** — use aquele.
>
> Resumo de 02/09: **12 MKs de Maricá online**; só o **Complexo (id 0) roda o v5**. As outras 11
> estão no v2, **7 com takeover de IP ativo**, **nenhuma com sonda de internet**, **8 com o DHCP do
> MK ligado 24/7 e escopo errado** (falta o DNS primário `10.135.16.18`, sobra `domain=pmm.local`
> que a Prefeitura não entrega, lease de 10 min em vez de 24 h), **11 sem MSS clamp** e **11 sem o
> túnel do Datacenter**. Além disso: **SRT I (id 18) está ATIVA no registro mas offline**, e
> **CAPSI (id 5) está online mas ainda em estado de bancada**.

Padrão-alvo **antigo** (v2/v3/v4) da tabela histórica: detecção v2 + DHCP sempre ligado com delay
(v3) + redirect `:53` alternado pelo script + conntrack limpo nas duas transições (v4).

| Item do padrão | id=0 Regulação (CCR) | id=1 Péricles | id=3 CAPS III | id=4 CAPS AD | id=6 CDT | id=7 Boqueirão | id=10 CMI | id=11 CEREST | id=17 SAE |
|---|---|---|---|---|---|---|---|---|---|
| VPN de gestão | `10.35.0.23` | `10.35.0.24` | `10.35.0.26` | `10.35.0.27` | `10.35.0.29` | `10.35.0.30` | `10.35.0.33` | `10.35.0.34` | `10.35.0.40` |
| LAN | `10.3.74.0/24` | `10.1.19.0/24` | `10.1.55.0/24` | `10.1.96.0/24` **VLAN 1102** | `10.1.92.0/24` **VLAN 1092** | `10.1.108.0/24` | `10.1.18.0/24` | `10.1.100.0/24` **VLAN 1100** | `10.1.162.0/24` **VLAN 1512** |
| Script | `failover-eth3-check` | `FAILOVER-ether1` | `FAILOVER-ether1` | `FAILOVER-ether1` | `FAILOVER-ether1` | `FAILOVER-ether1` | `FAILOVER-ether1` | `FAILOVER-ether1` | `FAILOVER-ether1` |
| Detecção v2 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ *(era v1)* | ✓ *(era v1)* | ✓ *(era v1)* |
| DHCP v3 (sempre ligado) | ✓ *(era liga/desliga)* | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ *(não existia)* | ✓ | ✓ |
| DNS reais no escopo | ✓ *(era `8.8.8.8,1.1.1.1`)* | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Redirect `:53` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Conntrack v4 | ✓ | ✓ *(31/07)* | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Anti-bypass (MAC antigo) | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| RouterOS | 7.23.2 | 7.23.2 | 7.23.2 | 7.23.2 | 7.23.2 | 7.23.2 | 7.19.6 ⚠ | **7.23.3** | **7.23.3** |

> **id=18 SRT I não entra nesta tabela**: é unidade **sem link da Prefeitura** (cabos
> dobrados, §0c) — não tem failover, nem DHCP de retaguarda, nem IP do MK na LAN. Ver a
> nota de exceção logo abaixo.

> **Firmware do routerboard ≠ versão do pacote.** Em todos os MKs que subiram de versão o
> `/system routerboard print` continua com `current-firmware: 7.19.6` e `upgrade-firmware`
> igual à versão nova — `/system package update install` **não** atualiza o bootloader.
> Verificado em CDT, CAPS AD, CEREST, SAE e SRT I. Está assim na frota inteira e foi
> mantido de propósito; se um dia valer subir, é `/system routerboard upgrade` + mais um
> reboot, e aí vale fazer em todos de uma vez.

> ⚠️ **A `latest-version` do canal stable ANDA.** Em 10/08 ela já era **7.23.3**, não a
> 7.23.2 em que a frota parou em julho. Ou seja: subir "para a latest" hoje deixa o MK um
> patch à frente dos que subiram antes. Isso é esperado e aceito (a §0.1 manda ir para a
> latest); não tente casar com a versão dos vizinhos. Sempre rodar
> `/system package update check-for-updates once` + `print` e **relatar a versão que
> realmente vai entrar** antes de instalar.

> **id=1 Péricles nivelado em 31/07/2026.** Voltou a responder (`10.35.0.24`) e foi auditado:
> estava melhor do que as células `?` sugeriam — já tinha detecção v2, DHCP v3 com DNS reais e
> os redirects `:53`. Faltavam só **`delay-threshold` em 3s** (o valor antigo) e o **conntrack
> v4**. Delta aplicado em `unidades-config/id01-amb-pericles-nivelamento` (gerado a partir do
> `id01-amb-pericles-site.rsc`, que já reflete o estado final). Não reiniciou em nenhum momento
> — uptime contínuo de 1d12h atravessando a janela em que ficou inalcançável.

Cada aplicação foi validada com: fonte do script conferido **byte a byte** após gravar, script
executado uma vez sem erro, zero objetos `FAILOVER` habilitados no estado normal, e ping ao hub
VOIP com 0 % de perda.

**Exceção ao padrão — id=6 CDT (ATIVA 30/07/2026, `10.35.0.29`): LAN chega TAGGED em
VLAN 1092.** Primeira unidade do rollout assim (as anteriores eram untagged). Todo o L3 do MK
na LAN mora em `vlan1092-lan` (VLAN sobre a `bridge-transparente`) — IP do MK `10.1.92.254`,
takeover do gw `10.1.92.1`, takeover do DHCP `10.1.201.254`, redirects `:53`, `FAILOVER-dhcp`
**e o gateway dos telefones `10.200.6.1/24`**, que saiu da bridge untagged. A bridge segue
sem `vlan-filtering`, então o L2 continua transparente. **Mover o gateway dos telefones para a
VLAN não era convenção — foi decisão de 30/07, e está COMPROVADA neste site:** o telefone
Cisco `10.200.6.18` só responde ARP/ICMP pela `vlan1092-lan`; untagged ele ficaria sem gateway.
Regra geral: *o gateway dos telefones mora no mesmo L2 da LAN da unidade* — o que muda por
unidade é se esse L2 é tagged ou não. Se um dia aparecer aparelho em voice-VLAN própria,
é outra interface VLAN, não a volta pro untagged. O untagged residual em ether1/ether4
é a VLAN de gerência da PMM (SNMP de `10.135.16.x` para switches `172.20.1.x`) — não é a LAN
dos clientes. Demais valores no padrão PMM de sempre (DHCP central `10.1.201.254`, DNS
`10.135.16.119`/`.17`, `pmm.local`). Config em `unidades-config/id06-cdt-site.rsc`.
Como o survey só falha em silêncio nesse cenário, a detecção virou passo 1 da FASE SITE.

**LAN tagged — id=4 CAPS AD (ATIVA 31/07/2026, `10.35.0.27`): VLAN 1102, `10.1.96.0/24`.**
Segunda unidade tagged, o que confirma que **tagged não é exceção do CDT** — é um dos dois
cenários possíveis, e o passo 1 da FASE SITE (torch antes de qualquer coisa) tem que valer
sempre. Mesmo desenho do CDT: todo o L3 na `vlan1102-lan` — IP do MK `10.1.96.254`, takeover
do gw `10.1.96.1`, takeover do DHCP `10.1.201.254`, redirects `:53`, `FAILOVER-dhcp` e o
gateway dos telefones `10.200.4.1/24`. **Razão de fundo (levantada pelo usuário em 31/07):**
a Prefeitura entrega tagged e o **switch core da unidade espera tagged** — então tudo que o
failover e o nosso DHCP entregarem tem que sair com a MESMA tag, senão o switch core não
aceita. Demais valores no padrão PMM (gw `.1`, DHCP central `10.1.201.254`, DNS
`10.135.16.119`/`.17`, `pmm.local`, lease 8h). Config em `unidades-config/id04-caps-ad-site.rsc`.

⚠️ **Armadilha nova (CAPS AD, 31/07): cache ARP negativo da interface VLAN recém-criada.**
Logo depois de criar a `vlan<ID>-lan` e adicionar o IP do MK, a primeira ARP request ao
gateway falha e o RouterOS **cacheia o `failed`** — o ping ao gateway dá 100% timeout e o
quadro imita *IP ocupado* ou *IP source guard na VLAN*. Não é nenhum dos dois. Antes de
concluir qualquer coisa: `/ip arp remove [find interface=vlan<ID>-lan]` e repetir o ping.
No CAPS AD isso custou uma rodada inteira de diagnóstico (teste com `.253`, comparação com
o IP vindo do DHCP) até cair a ficha.

**LAN tagged — id=11 CEREST (ATIVA 10/08/2026, `10.35.0.34`): VLAN 1100, `10.1.100.0/24`.**
**LAN tagged — id=17 SAE (ATIVA 10/08/2026, `10.35.0.40`): VLAN 1512, `10.1.162.0/24`.**
Terceira e quarta unidades tagged — com isso são **4 de 9**, e o cenário tagged deixa de
vez de ser "exceção": é um dos dois resultados possíveis do passo 1 da FASE SITE. Mesmo
desenho do CDT/CAPS AD em ambas: todo o L3 na interface VLAN (IP do MK `.254`, takeover
do gw, takeover do DHCP `10.1.201.254`, redirects `:53`, `FAILOVER-dhcp` e o gateway dos
telefones `10.200.<ID>.1`). Demais valores no padrão PMM (DHCP central `10.1.201.254`,
DNS `10.135.16.119`/`.17`, `pmm.local`, lease 8h). Configs em
`unidades-config/id11-cerest-site.rsc` e `id17-sae-site.rsc`.
⚠️ **Os números das VLANs não seguem nenhuma série** — 1092, 1100, 1102, 1512. Não tente
derivar do id nem "chutar pela vizinha": só o torch diz.
⏱️ **No SAE o dhcp-client do survey levou ~50 s** para sair de `searching...`/`requesting...`
e fechar em `bound` (o CEREST fechou em <35 s). É o mesmo relay lento da Prefeitura já
documentado no Boqueirão. **Não concluir "essa VLAN não tem DHCP" antes de ~1 minuto** —
com 15 s de espera o quadro é indistinguível de VLAN errada.

**Exceção ao padrão — id=18 SRT I - Araçatiba (ATIVA 10/08/2026, `10.35.0.41`): unidade
SEM link da Prefeitura, cabos dobrados.** Primeira unidade no desenho do §0c (que passa a
ser o padrão para link único, no lugar do desenho do TFD). `ether1` e `ether2` estão os
dois no roteador do único link (`192.168.0.1`, que segue sendo gateway/DHCP/DNS da
unidade); a bridge transparente e o `wg-voip` continuam normais, com o gateway dos
telefones `10.200.18.1/24` na `bridge-transparente` (LAN untagged). **Sem** regras
FAILOVER, **sem** DHCP de retaguarda, **sem** IP do MK na LAN, **sem** liberação de
ssh/winbox por IP local; scheduler `FAILOVER-check` **desabilitado** com o motivo no
comentário e o script v1 inerte no disco. Gestão por `automais-vpn`/`ether3`/MAC-Winbox
na ether4. Config em `unidades-config/id18-srt-i-site.rsc`.

**Exceção ao padrão — id=21 TFD (ATIVA 30/07/2026, `10.35.0.44`):** a unidade **NÃO tem
link da Prefeitura** — a Connect (ether2) é o único link ("failover permanente", decisão do
usuário 30/07). O MK é **gateway+DHCP+DNS da LAN local `10.1.21.0/24`** (faixa padrão-PMM
derivada do id; gw/DNS `10.1.21.1`, pool `.10-.200`, lease 30m, `authoritative=yes` — não há
DHCP real com quem competir), NAT permanente LAN→ether2 (comentário SEM "FAILOVER" de
propósito), forwarders `8.8.8.8,9.9.9.9`, **scheduler `FAILOVER-check` DESABILITADO** (script
v1 permanece no disco). Se a Prefeitura chegar um dia: survey L3 real, recriar regras
FAILOVER padrão e reabilitar o scheduler. Config em `unidades-config/id21-tfd-site.rsc`.
Telefones seguem o padrão (`10.200.21.0/24` fixo). Obs.: Transporte Sanitário (id=22) fica
no MESMO endereço (Rua das Gaivotas, 12 - Camburi) — avaliar na instalação dele se é a mesma
LAN física (pode não caber um segundo MK de borda).

**Pendências abertas:**

- ❓ **id=1 Péricles — por que a gestão caiu em 29/07 continua sem explicação.** Voltou sozinho
  em 31/07, **sem reiniciar** (uptime contínuo de 1d12h atravessando a janela). Os dois túneis
  saem pelo **mesmo link** (a Connect / ether2 — a única rota default é `0.0.0.0/0` via
  `192.168.0.1`, e o link da Prefeitura é só bridgeado, o MK não tem rota por ele). Como o
  `wg-voip` ficou de pé o tempo todo e só o `automais-vpn` caiu, **não foi o link**: sobra
  problema do lado do automais.io (peer/porta 133xx/NAT do endpoint). Se repetir, olhar por lá
  antes de ir ao site. Já nivelado no resto (ver nota da tabela acima).
- ⚠️ **id=7 Boqueirão e id=1 Péricles — o DHCP de retaguarda está capturando estações.**
  Boqueirão: 2 leases, incluindo `ESFCORD0435` (`class-id="MSFT 5.0"`), com log rotativo de
  NVRs/Pantum/TL-WR940N; já subiu 3s→5s **sem efeito**. Péricles: 3 estações Dell/Windows
  presas, mas com padrão diferente — poucas concessões, e depois renovação unicast (ver item 3
  de "Como o `delay-threshold` REALMENTE funciona"). **Subir o valor não resolve nenhum dos
  dois** e já foi tentado: no Boqueirão porque o relay real é genuinamente lento nesse
  segmento, no Péricles porque a renovação nem passa pelo `delay-threshold`. É assunto pra
  abrir com a TI da Prefeitura (lentidão do relay), não pra continuar ajustando o parâmetro.
- ⚠️ **id=10 CMI — firewall de entrada fora do padrão.** As 4 primeiras regras da chain `input`
  aceitam SIP (`5060-5061`) e RTP (`10000-20000`) de **qualquer** interface, incluindo a
  Connect — contradiz o modelo *Connect = NÃO confiável* (§5). Os telefones falam com o hub
  pelo túnel; não precisam dessas portas abertas.
- **id=6 CDT — teste de failover pendente + 1 ramal a registrar.** Regras criadas e validadas
  em repouso, mas o teste de cabo não foi feito (padrão desde 29/07: a equipe faz no local).
  Já existe **1 telefone Cisco em `10.200.6.18`** (`00:DF:1D:88:BF:6C`, IP fixo): rede
  **validada ponta a ponta** — responde na `vlan1092-lan` (0ms) e o hub Asterisk alcança pelo
  túnel (110ms, 0% perda) — mas **não está registrado** (`sip show peers` sem nenhum
  `10.200.6.x`); falta o provisionamento SIP. Demais aparelhos: IP fixo `.10-.200`, gw
  `10.200.6.1`, SIP `10.201.0.1`.
- **id=4 CAPS AD — teste de failover pendente + nenhum telefone ainda.** Regras criadas e
  validadas em repouso (script byte a byte, 0 objetos `FAILOVER` ligados, `FAILOVER-dhcp`
  com 0 leases, hub VOIP a 111ms/0% perda), mas o teste de cabo é da equipe no local. Não há
  nenhum aparelho em `10.200.4.0/24` — quando chegarem: IP fixo `.10-.200`, gw `10.200.4.1`,
  SIP `10.201.0.1`.
- ℹ️ **id=3 CAPS III — o "192.168.1.x" era CABO TROCADO, não anomalia da unidade.** Vale
  registrar porque me levou a um diagnóstico errado por um bom tempo. Durante a fase site
  (31/07) o survey trouxe `192.168.1.3/24` com gw/DHCP/DNS todos em `192.168.1.1` (MAC
  `00:67:62:3A:DD:B0`, lease 2h) e cheguei a documentar isso como "ONU da Prefeitura roteando
  com DHCP concorrente". **Não era:** o cabo da Prefeitura estava conectado na porta errada
  (informação do usuário). Depois de corrigido — e com a `ether4` plugada — o survey passou a
  trazer o real (`10.1.55.0/24`, gw `10.1.55.1`, DHCP `10.1.201.254`, DNS `10.135.16.119`/`.17`)
  em **4 de 4** tentativas, untagged, padrão PMM puro. Ainda se vê tráfego residual
  `192.168.1.x` no segmento (um KMS em `:1688`), sem impacto conhecido — se voltar a aparecer
  concessão `192.168.1.x` para cliente, aí sim virou problema real.
  **Lição que ficou:** dado de survey só vale com o cabeamento confirmado — ver §0b da FASE
  SITE. Config em `unidades-config/id03-caps-iii-site.rsc`.
- **id=3 CAPS III — teste de failover pendente + nenhum telefone ainda.** Validado em repouso
  (script byte a byte, 0 objetos `FAILOVER` ligados, `FAILOVER-dhcp` com 0 leases, hub VOIP a
  112ms/0% perda, gw local a 3,4ms/0%). Teste de cabo é da equipe no local. Quando os aparelhos
  chegarem: IP fixo `.10-.200`, gw `10.200.3.1`, SIP `10.201.0.1`.
- **id=11 CEREST e id=17 SAE — teste de failover pendente + nenhum telefone ainda.**
  Regras criadas e validadas em repouso (script conferido byte a byte contra o `.rsc`,
  0 objetos `FAILOVER` habilitados, `FAILOVER-dhcp` com 0 leases, `allow-remote-requests=no`,
  gateway local respondendo e hub VOIP a ~111 ms / 0 % de perda). O teste de cabo é da
  equipe no local. Quando os aparelhos chegarem: IP fixo `.10-.200`, gw `10.200.11.1` /
  `10.200.17.1`, SIP `10.201.0.1`.
- ✅ **id=18 SRT I — PRIMEIRO TELEFONE REGISTROU (13/08).** `Cisco CP-69xx`
  (`70:CA:9B:5C:86:A8`) em `10.200.18.15`, **ramal 1801**, contexto `PLANO_HOSPITAIS`,
  `sip show peers` → `OK (126 ms)`. É a primeira unidade do rollout hEX com ramal vivo
  ponta a ponta (as demais seguem sem aparelho). Provisionamento feito **pelo painel
  FalarMais durante a auditoria**: o `SEP70CA9B5C86A8.cnf.xml` nasceu às 10:52 no
  `/var/lib/tftpboot` (dono `www-data`) — antes disso o telefone tomava `NAK File not
  found` e subia no `XMLDefault.cnf.xml`. Nada de failover a testar (link único).
  Demais aparelhos, quando chegarem: IP fixo `.10-.200`, gw `10.200.18.1`, SIP
  `10.201.0.1`.
- **Todas:** anti-bypass, se aprovada. ROS 7.19.6 restante: **id=10 CMI** e **id=21 TFD**.
  (CEREST, SAE e SRT I subiram para 7.23.3 em 10/08, autorizados, logo apos a fase site —
  cada MK fora ~1-2 min, config integra nos tres, device-mode preservado, os dois tuneis
  com handshake em menos de 3 min.)
  **O upgrade com a unidade viva é mais barato do que a skill sugeria** — no CAPS AD (31/07) a
  janela dark tinha passado, o usuário autorizou subir no meio do expediente e o MK ficou fora
  **~2 min**, config íntegra, sem nenhum reflexo reportado. Continua valendo pegar a janela dark
  quando ela existir, mas não é motivo para deixar um MK atrasado indefinidamente.

## 5. Blindagem de segurança (hardening)

Modelo de confiança: **Prefeitura ($UP/LAN) = confiável; Connect ($CONN) = NÃO confiável**
(ISP compartilhado, wifi de terceiros). **Não** fazer default-drop global — blindar a **entrada
pela Connect**. Dois caminhos de gestão preservados: `$GEST` (173.20.20.0/24) e `automais-vpn`
(10.35.0.0/24). **Não mexer na senha do admin.** Sempre com **auto-revert anti-lockout** (scheduler
que desfaz em ~3 min) — remover só depois de testar os DOIS caminhos.

```
# rótulos de provedor nas interfaces
/interface ethernet set $UP comment="Prefeitura/ONU - PRIMARIO (seguro)"
/interface ethernet set $CONN comment="CONNECT - internet (failover+wifi) - NAO CONFIAVEL"
/interface list add name=MGMT ; /interface list member add list=MGMT interface=$GEST ; add list=MGMT interface=automais-vpn
# auto-revert (criar ANTES do drop): scheduler SAFETY-revert interval=3m que faz:
#   /ip firewall filter disable [find comment~"BLINDAGEM Connect"]; /ip ssh set strong-crypto=no; /ip service set ssh address=""; /ip service set winbox address=""
# firewall input (blindagem do Connect, com exceção do server VOIP):
/ip firewall filter add chain=input action=accept connection-state=established,related,untracked
/ip firewall filter add chain=input action=drop connection-state=invalid
/ip firewall filter add chain=input action=accept in-interface=$CONN protocol=udp dst-port=51820 src-address=192.241.153.121 comment="excecao SERVER VOIP"
# porta do automais-vpn é POR DEVICE (13300 + ultimo octeto do IP VPN — ver §3b); usar a porta real:
/ip firewall filter add chain=input action=accept in-interface=$CONN protocol=udp dst-port=51820,<PORTA_AUTOMAIS_VPN> comment="WireGuard VPNs"
/ip firewall filter add chain=input action=accept in-interface=$CONN protocol=udp dst-port=68 comment="dhcp-client Connect"
/ip firewall filter add chain=input action=drop in-interface=$CONN comment="BLINDAGEM Connect (bloqueia wifi/ISP)"
# serviços: desligar legado/texto-claro, restringir gestão
/ip service disable [find name=ftp] ; disable [find name=telnet] ; disable [find name=www] ; disable [find name=api-ssl]
/ip service set ssh address=173.20.20.0/24,10.35.0.0/24 ; set winbox address=173.20.20.0/24,10.35.0.0/24
# descoberta/MAC/bandwidth + SSH forte
/tool mac-server set allowed-interface-list=MGMT ; /tool mac-server mac-winbox set allowed-interface-list=MGMT ; /tool mac-server ping set enabled=no
/ip neighbor discovery-settings set discover-interface-list=MGMT ; /tool bandwidth-server set enabled=no
/ip ssh set strong-crypto=yes
```
Depois: testar SSH nos DOIS caminhos com conexão nova → se OK, remover `SAFETY-revert`.

## 6. Verificação final

- **Versão do RouterOS conferida (§0.1)** e registrada: ou é a `latest-version`, ou o usuário
  decidiu ficar na atual (aí vira linha na tabela de estado dos MKs).
- Bridge `RUNNING` (combo1+link), tráfego atravessando; VOIP `last-handshake` recente + ping 10.201.0.1.
- Failover: puxar cabo do $UP → ~15s → log `ATIVADO (link=false ...)`; teste de camada 3 (cabo no
  lugar, upstream da ONU fora) → `ATIVADO (link=true rxDelta=...)`; PC navega por nome/IP e
  **renova IP no FAILOVER-dhcp** (conferir que o lease traz os DNS REAIS, não 8.8.8.8);
  restaurar → ~30s → `DESATIVADO`.
- DHCP de retaguarda em estado normal: `/ip dhcp-server lease print` deve ficar **vazio** — se
  estiver entregando lease com o link bom, o `delay-threshold` não está pegando e ele está
  competindo com o DHCP da Prefeitura.
- Hardening: gestão OK pelos 2 caminhos; `SAFETY-revert` removido; VOIP intacto.

### Telefone não responde: desligado ou problema de rede? (1 comando)

```
/interface bridge host print where mac-address=<MAC do telefone>
```

**Vazio = o MK não está mais aprendendo o MAC ⇒ o aparelho está desligado/desplugado.**
Não é rede, não é firewall, não é o túnel — e não adianta investigar do lado do hub. Se o
MAC aparece mas o `/ping` falha, aí sim é L3 (IP/máscara/gateway do aparelho). Confirmado no
SRT I em 13/08: o telefone respondia a 0,8 ms às 10:49, foi reprovisionado pelo painel às
10:52, reiniciou e sumiu; `bridge host` vazio + `/ip arp` em `failed` fecharam o diagnóstico
sem precisar de ninguém no local. Complementos baratos: `/ip firewall connection` ainda
mostra a sessão SIP antiga (timeout de ~1 h — presença dela **não** prova telefone vivo) e
`/interface ethernet print stats-detail where name=ether4` confirma o cabo
(`rx-fcs-error=0`, `rx-drop=0`).

⚠️ **Perda de pacote na `automais-vpn` NÃO é sintoma da unidade.** Medindo do PC em 13/08, o
MK do SRT I deu **66 % de perda** e a vizinha 0 %; minutos depois o SRT I estava em 0 % e a
perda tinha migrado para outra unidade. É ruído do caminho PC→automais.io, e chega a derrubar
sessões SSH no meio da auditoria. **Antes de acusar o link de uma unidade, meça pelo
`wg-voip` a partir do hub** (`ping 10.201.0.<id+10>` do `192.241.153.121`): os dois túneis
saem pelo MESMO link físico, então um `wg-voip` limpo com `automais-vpn` sujo **inocenta a
unidade** e joga o problema no lado do automais.io. No SRT I o `wg-voip` estava em
0 % / 111 ms enquanto a gestão perdia 66 %. É a mesma assinatura da queda do Péricles em
29/07 que ficou sem explicação.

## 7. Registrar

- `Telefonia/registro/unidades.csv`: `mk_pubkey`, `mk_lan_atual`, `status=ATIVA`,
  **`mk_mac` (MAC da etiqueta, sem separadores)** e **`mk_senha_etiqueta`** — o MAC da
  etiqueta é a referência única do aparelho em toda a documentação.
- Doc da unidade em `Infraestrutura/<Unidade>/` (rede + migração + failover), espelhando o Complexo.
- Atualizar memória `project_telefonia_vpn_wireguard` com o estado da unidade.
