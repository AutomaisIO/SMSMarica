# PROJETO F3-bridge-v5

## Tese
BRIDGE TRANSPARENTE CORRIGIDA — failover v5. O MK continua ponte L2 e a PMM continua dona de gateway, DHCP, DNS e proxy em regime normal (identidade dos hosts preservada por construção, nada a decidir em P1 para o regime normal). O que muda é o failover, refeito em quatro planos: (1) DETECÇÃO por sonda de internet fim-a-fim pelo caminho da PMM (rotas /32 de sonda com pref-src + netwatch ICMP/TCP N-de-M) e pelo caminho da Connect, nunca por link/contador/ping do gateway; (2) COMUTAÇÃO sem dois donos do mesmo IP: o motor preferido ("Motor B — captura L2") não assume 10.3.74.1 — usa bridge NAT para redirecionar à CPU os frames que os clientes já mandam ao MAC do roteador real e para responder ARP de 10.3.74.1 com o MAC do roteador real quando ele some; os clientes nunca veem mudança de gateway (convergência 0 s, entrada e saída, sem GARP). Fallback documentado ("Motor A — takeover + cerca L2 por bridge filter só no failover + varredura ARP dirigida"), para o caso do lab refutar a captura; (3) ESTADO derivado da configuração (âncora = regra FO-CAPTURA), reconciliado a cada 5 s com :do on-error, trava de reentrância, graça de boot por uptime, hold-down/backoff persistidos em arquivo; DHCP de retaguarda DESLIGADO em normal e ligado só quando o detector de DHCP (/ip dhcp-server alert, que emite DISCOVER 1x/min) prova que o servidor real morreu, sempre authoritative=no e com leases estáticas colhidas; sem takeover de 10.1.201.254; (4) POLÍTICA explícita: internos da PMM nunca pela Connect, DNS condicional pmm.local→DCs, MSS clamp 1440, default-drop, anti-bypass reconciliado, telemetria externa. Limites assumidos: proxy da PMM é furado DURANTE o failover (decisão R23), sessões via Connect morrem na volta, modo Connect-primária só existe como "failover forçado permanente" (= I3), e nas unidades TAGGED a captura depende de os matchers internos funcionarem sob vlan-encap (claim de lab; há fallback).

## Resumo executivo
1. Hoje "uma parte funciona e outra não" porque a transição cria DOIS donos de 10.3.74.1 na mesma L2 e cada host decide sozinho, pelo seu cache ARP, para quem entrega o pacote; quem cai no MK navega, quem cai no roteador real morre; o redirect :53 e o NAT só valem para quem "ganhou" o sorteio; o DHCP de retaguarda renumera e colide; e a detecção só mede o trecho MK↔gateway, que não quebrou em 02/09.
2. A v5 mantém a bridge transparente (PMM dona de tudo no normal, proxy/filtro deles valendo, zero NAT, zero renumeração, identidade preservada) e troca só o failover.
3. Detecção: netwatch ICMP para 1.1.1.1, 9.9.9.10 e 208.67.222.222 pinados por rota /32 via 10.3.74.1 com pref-src=10.3.74.254 (ICMP comprovadamente volta da borda da PMM — V7), TCP/53 ao DC 10.135.16.119 (estado "interno PMM"), e o mesmo conjunto pela Connect. Regra: entra com 0/3 PMM por 30 s E Connect viva; sai com ≥2/3 PMM por janela com backoff (60 s → 15 min) e hold-down de 5 min. Se PMM e Connect caírem juntas, fica transparente e alarma (R31).
4. Motor B (preferido): 6 regras de /interface bridge nat, desabilitadas no normal. No failover: (a) frames IP da LAN dirigidos ao MAC 94:3F:C2:DF:49:D3 com destino NÃO interno são redirecionados à CPU (action=redirect) e saem por NAT pela Connect; (b) :53 sempre capturado; (c) destinos 10/8 e 172.16/12 seguem em ponte para o roteador real (identidade preservada, AD/SNMP/impressão continuam se a PMM interna estiver viva — o caso de 02/09); (d) ARP request por 10.3.74.1 é respondido com o MAC do roteador real (cobre roteador/ONU mortos). O cliente nunca vê outro MAC: convergência 0 s em Windows, Linux, Android, impressoras, sem GARP.
5. Motor A (fallback, se o lab refutar o redirect): takeover 10.3.74.1/32 + bridge filter que esconde só o ARP de origem 10.3.74.1 do roteador (gerência 172.20.1.x intacta) + alias ARP para sondar de volta (I2) + varredura ARP dirigida (ping src-address=10.3.74.1 a cada host) para convergir em ≤2 s onde hoje leva 20-55 s (Windows/Linux) ou minutos (impressoras).
6. DHCP: FAILOVER-dhcp fica DESLIGADO. /ip dhcp-server alert manda DISCOVER a cada minuto: enquanto o servidor real responder (via relay 10.3.74.1), a retaguarda não existe; 3 min sem resposta → liga com authoritative=no (nunca NAK), lease 10 m, leases estáticas colhidas (mesmo IP para o mesmo MAC); servidor real volta → desliga. Fim do dual-DHCP e do takeover de 10.1.201.254.
7. DNS: forwarders públicos via Connect, use-peer-dns=no, FWD pmm.local e reversos → DCs (rota pela PMM), flush de cache nas transições, resolvedor aberto só à LAN por firewall permanente.
8. Estado: âncora = flag disabled da regra FO-CAPTURA; contadores em RAM só para debounce; hold-down/backoff em arquivo; graça de 3 min pós-boot; watchdog de coerência (todas FO-* iguais à âncora, anti-bypass oposta, DHCP × alert) com alarme e autocorreção no tick seguinte.
9. Segurança/operação: input e forward default-drop, Connect→LAN proibido, MSS clamp 1440 permanente na Connect (P6), NTP, syslog remoto + POST na API automais.io por transição, drill mensal.
10. Cutover no Complexo sem janela de indisponibilidade: instalar tudo desabilitado, provar a captura com UM host (ARP estático no PC), ligar Motor B, desligar takeovers antigos, reabilitar combo1 (mesmo MAC nas duas respostas ARP → sem guerra), o próprio tick devolve a transparência quando a PMM voltar. Rollback = /system script run FO-rollback (reproduz o estado manual de hoje).
11. Onde a v5 é preferível às roteadas: unidades TAGGED (separação L2 exige duas bridges + trânsito da gerência untagged sem veth e sem porta sobrando), unidades com fluxos de entrada PMM→hosts, e onde a política é "a Prefeitura cuida do proxy". Onde perde: se a decisão for Connect-primária permanente, o que a v5 só oferece como estado forçado (= I3), com proxy da PMM furado 24/7 e hEX em bridging por CPU.
12. Riscos principais: comportamento do bridge NAT redirect/arp-reply (não provado; teste de 10 min em bancada decide entre Motor B e A), matchers internos sob VLAN (unidades tagged), troca de MAC do roteador da PMM durante um failover, CPU do hEX em failover longo.

## Desenho
# Desenho v5 — bridge transparente corrigida

## 1. Caso UNTAGGED (Complexo, CCR1009) — regime NORMAL (transparente)

```
 PMM (HPE/Aruba L3, 10.3.74.1 = 94:3F:C2:DF:49:D3, relay DHCP -> 10.1.201.254, DNS 10.135.16.119/.17)
   |
 [combo1]========= bridge-transparente (protocol-mode=none, vlan-filtering=no, admin-mac fixo) =========[ether2]--- switch core --- 67 hosts 10.3.74.0/24 + 7 telefones 10.200.0.x
   |                                     |                                                                                   |
   |        CPU do MK: 10.3.74.254/24 (LAN), 10.200.0.1/24 (telefones)                                                       |
   |        - bridge nat FO-* : DESABILITADAS  -> nenhum frame de cliente entra na pilha IP                                   |
   |        - anti-bypass (forward reject LAN->WAN): LIGADA (nada da LAN pode ser roteado)                                    |
   |        - netwatch FO-PMM-1..3 (ICMP 1.1.1.1 / 9.9.9.10 / 208.67.222.222) por rota /32 via 10.3.74.1 pref-src .254        |
   |        - netwatch FO-PMM-DC (tcp/53 -> 10.135.16.119) via 10.3.74.1                                                     |
   |        - netwatch FO-CONN-1..3 pela default (Connect)                                                                    |
   |        - dhcp-server alert: DISCOVER 1x/min; unknown-server = MAC do relay => "DHCP real vivo"                          |
   |        - FAILOVER-dhcp: DESABILITADO                                                                                     |
 [ether3] Connect 192.168.0.100 (default dist 2, use-peer-dns=no)  [ether7] Starlink  [wg-voip][automais-vpn][wg-becape]
```
Tráfego do cliente: L2 puro ether2↔combo1 (fast-path/fast-forward ativos no CCR; H no hEX). O MK só vê o que é endereçado aos SEUS IPs (telefones, Winbox, DNS dos telefones).

## 2. Caso UNTAGGED — regime FAILOVER, Motor B (captura L2)

```
 cliente 10.3.74.82 --> frame [dst-MAC 94:3F:C2:DF:49:D3][IP dst 142.250.x.x] --> ether2
      bridge nat dstnat (in-interface=ether2, src 10.3.74.0/24, dst-mac = GW_MAC):
        1. udp/tcp 53          -> action=redirect  -> CPU -> /ip firewall dstnat redirect :53 -> resolvedor do MK (FWD pmm.local -> DCs; resto -> 1.1.1.1/8.8.8.8 via Connect)
        2. dst 10.0.0.0/8      -> action=accept    -> segue em PONTE ao roteador real (AD, SNMP 10.11.x, impressao, DHCP renew unicast) - identidade preservada
        3. dst 172.16.0.0/12   -> action=accept    -> idem
        4. resto               -> action=redirect  -> CPU -> forward -> masquerade -> ether3 (Connect, MSS 1440)
        5. ARP request "who has 10.3.74.1" -> action=arp-reply to-arp-reply-mac-address=GW_MAC  (roteador vivo: 2 respostas IDENTICAS; roteador morto: so a do MK)
 retorno internet: Connect -> MK -> de-NAT -> frame [src-MAC do MK][IP src 142.250.x.x] -> cliente (cliente nao se importa com o MAC de quem entrega)
 cache ARP do cliente: 10.3.74.1 -> 94:3F:C2:DF:49:D3 ANTES, DURANTE e DEPOIS. Nada a invalidar.
 anti-bypass: DESLIGADA (reconciliada). FAILOVER-dhcp: so se o alert nao vir o DHCP real por 3 min.
```
Propriedades: nunca dois donos de IP (R9); nada a converger (R10 vira 0 s); DHCP real continua servindo se estiver vivo (R11) e os leases dele apontam para 10.3.74.1 = MAC capturado; DNS 100% capturado (R15) porque TODO frame ao gateway passa pela regra; internos nunca pela Connect (R18) porque nem entram na pilha IP.

## 3. Caso UNTAGGED — regime FAILOVER, Motor A (fallback)

```
 takeover 10.3.74.1/32 na bridge (MK responde ARP com o SEU MAC)
 bridge filter forward (ligadas so no failover):
   F1 in=combo1 out=ether2 mac-protocol=arp arp-src-address=10.3.74.1  -> drop  (roteador real some como "10.3.74.1"; ARP dele como 172.20.1.1 passa -> gerencia intacta)
   F2 in=ether2 out=combo1 mac-protocol=arp arp-dst-address=10.3.74.1  -> drop  (roteador nao e perguntado)
 /ip arp add address=10.3.74.253 mac=GW_MAC (alias I2) + rotas de sonda e 10.0.0.0/8 via 10.3.74.253  (next-hop != IP local)
 srcnat internos -> 10.3.74.254 (roteador so precisa resolver .254, que a CPU responde: input nao e cercado)
 varredura ARP dirigida: /ip arp remove dinamicos; :for i 2..253 /ping 10.3.74.$i src-address=10.3.74.1 count=1 (ARP request com sender 10.3.74.1 + MAC do MK -> todo host atualiza)
 saida: takeover OFF + F1/F2 OFF no mesmo tick; clientes convergem por NUD (15-55 s) ou pelo "shim de retorno" (bridge nat dst-nat MK_MAC -> GW_MAC por 10 min, a validar)
```

## 4. Caso TAGGED (hEX, ex. CDT VLAN 1092)

```
 [ether1 PMM]== bridge-transparente ==[ether4 switch core]     ether2 Connect   ether3 OOB 173.20.20.1   ether5 Wi-Fi (fora)
   trunk: VID 1092 (clientes 10.1.92.0/24, gw 10.1.92.1) + UNTAGGED residual = gerencia PMM (SNMP 10.135.16.x -> 172.20.1.x)
   L3 do MK: vlan1092-lan sobre a bridge (10.1.92.254, 10.200.6.1) - inalterado
   Motor B: mesmas 6 regras com mac-protocol=vlan vlan-id=1092 vlan-encap=ip|arp  (matchers internos sob VLAN = CLAIM C4)
   Se C4 falhar: captura "cega" (vlan-id=1092 + dst-mac=GW_MAC -> redirect, sem filtro de destino; internos viram hairpin pela CPU com raw notrack)
               + cerca ARP em bloco (vlan-id=1092 vlan-encap=arp entre ether1<->ether4 -> drop) + takeover na vlan1092-lan (Motor A-tagged)
   A gerencia untagged NAO e tocada por nenhuma regra (todas exigem vlan-id=1092): P2 atendida por construcao.
```

## 5. Papéis de porta e objetos por caso

| Item | Complexo (CCR) | hEX untagged | hEX tagged |
|---|---|---|---|
| Porta PMM / LAN / Connect | combo1 / ether2 / ether3 | ether1 / ether4 / ether2 | ether1 / ether4 / ether2 |
| Interface L3 da LAN | bridge-transparente | bridge-transparente | vlan<VID>-lan |
| in-interface das regras bridge nat | ether2 | ether4 | ether4 (+vlan-id) |
| Sondas PMM (rota /32 via GW real, pref-src IP do MK) | 3 ICMP + 1 TCP/53 DC | idem | idem |
| Âncora de estado | bridge nat "FO-CAPTURA" | idem | idem |
| Objetos alternados pelo tick | 6 bridge nat + 1 filter anti-bypass (+ DHCP por sonda própria) | idem | idem |
| Objetos PERMANENTES | masquerade (dst !RFC1918), dstnat :53, MSS clamp, rotas internas, FWD DNS, firewall default-drop | idem | idem |
| hw-offload | n/a (CCR sem switch chip) | H em normal; CPU em failover | idem |

## 6. Tabelas de decisão

| Sinal | Fonte | Peso |
|---|---|---|
| PMM internet | ≥2 de 3 netwatch ICMP pinados via GW real (status + since) | decide entrar/sair |
| PMM interno | netwatch tcp/53 → DC | informa (log/alerta "internet fora, AD vivo"); não decide |
| Link físico PMM | running da porta | acelera entrada (15 s) e bloqueia saída |
| Connect internet | ≥1 de 3 netwatch pela default | pré-condição para entrar (R31) |
| DHCP real | dhcp-server alert unknown-server ≠ vazio | decide só o FAILOVER-dhcp |
| Boot | uptime < 3 min | congela transições |

| Estado | FO-* bridge nat | anti-bypass | FAILOVER-dhcp | Quem é gateway "de fato" |
|---|---|---|---|---|
| NORMAL | off | on | off (salvo DHCP real morto) | roteador PMM |
| FAILOVER | on | off | por sonda | MK (invisível: mesmo IP e mesmo MAC vistos pelo cliente) |
| BOOT (<3 min) | como persistido | como persistido | off | como persistido, sem transição |
| TRAVADO (>6 transições/h) | como está | como está | por sonda | como está + alarme; destrava operador ou 2 h |

## Failover
# Failover v5 — detecção, comutação, retorno, estados, scripts, tempos

## 1. Por que hoje "uma parte funciona e outra não" — e o que a v5 muda, mecanismo a mecanismo

| Mecanismo | Hoje (v2/v3/v4) | Efeito observado | v5 (Motor B) | v5 (Motor A, fallback) |
|---|---|---|---|---|
| Detecção | link running + delta rx + ping 10.3.74.1 (só no else) | cego a falha acima do gw (02/09); 10 pps de ruído = "vivo" | 3 ICMP externos pinados pela PMM + tcp/53 DC + 3 pela Connect; link só acelera | idem |
| Saída | delta rx ≥ 2/5 s | "cabo plugado" = sai para link morto; equipe teve de desabilitar combo1 | mesma sonda externa, ≥2/3 UP por janela com backoff | idem, sondas via alias I2 (next-hop ≠ IP local) |
| Dono do IP do gw | MK e roteador real, ambos | guerra de ARP; cada host sorteia | MK nunca possui 10.3.74.1; captura frames ao MAC do roteador | MK possui .1, mas bridge filter esconde o ARP do roteador só na VLAN de clientes |
| Convergência | passiva, por cache de cada SO | oscila por horas | 0 s (cliente não vê mudança) | varredura ARP dirigida ≤2 s; NUD como plano B |
| Redirect :53 | só pega quem entrega ao MK | metade sem DNS | todo :53 ao gw é capturado na L2 | idem após a cerca |
| DHCP | retaguarda 24/7 com delay 5 s; takeover de 10.1.201.254 | captura em normal, NAK, colisões .10-.14, sequestro | desligada; ligada só com prova de morte do real (alert 1x/min); authoritative=no; harvest; sem takeover | idem |
| DNS | 8.8.8.8/9.9.9.9 + resolvedor do ISP; NXDOMAIN de pmm.local | AD morre, NXDOMAIN cacheado | FWD pmm.local→DCs pela PMM; use-peer-dns=no; flush nas transições | idem |
| Internos PMM | mascarados para a Connect | SNMP/impressão/AD mortos, vazamento | seguem em ponte ao roteador real (identidade preservada) | src-nat .254 pelo alias |
| Estado | $foState em RAM | split-brain após reboot/abort | derivado da regra FO-CAPTURA; reconciliação a cada tick | derivado do takeover |
| Script | sequência sem on-error | abortou em 09:18:08 | cada operação em :do on-error; lock por :jobname; graça de boot | idem |
| Volta com ARP velho | fura o proxy pela Connect | 341 conexões em 29/07 | não existe ARP velho | anti-bypass reject + NUD + shim de retorno (a validar) |
| Telemetria | :log em memória | incidente perdido no reboot | syslog remoto + POST API + disco | idem |

## 2. Detecção (R1-R3)

- **Sondas PMM** (`comment=FO-PMM`): ICMP a 1.1.1.1, 9.9.9.10, 208.67.222.222, cada uma pinada por rota /32 `gateway=10.3.74.1 pref-src=10.3.74.254`. Base: a borda da PMM devolve ICMP para o IP estático do MK (V7, TTL 118). `packet-count=3 timeout=1s interval=10s thr-loss-percent=99` → `down` só com 3/3 perdidos; uma resposta = `up`.
- **Sonda PMM interna** (`FO-PMMDC`): tcp-conn 10.135.16.119:53. Não decide o failover; classifica o incidente ("internet fora, AD vivo" = caso 02/09 → log/alerta diferenciado) e é pré-condição para ligar a captura seletiva de internos (se DC morto, os internos continuam indo ao roteador — não há o que fazer; documenta-se).
- **Sondas Connect** (`FO-CONN`): 1.0.0.1, 208.67.220.220 (ICMP) e 198.211.104.55:443 (tcp) pela default. `connOK = ≥1 up`.
- **Diversidade de protocolo (R3)**: TCP/HTTP pela PMM a partir do .254 NÃO volta (V7). Duas opções, decididas por medição (lab T7): (a) dhcp-client de sonda na bridge com IP de cliente → `src-address` dessa lease em netwatch tcp-conn 1.1.1.1:443 pinado; (b) aceitar ICMP×3 + TCP/53 DC como conjunto mínimo. Nunca usar 8.8.8.8/9.9.9.9 como alvo.
- **Agregação no FO-tick** (5 s): `pmmDOWN = (0/3 up) ou porta não running`; `pmmOK = ≥2/3 up e porta running`; 1/3 = indeterminado (mantém estado). Entrada: `pmmDOWN` por 6 ticks (30 s) **e** `connOK` **e** fora da graça de boot **e** ≥120 s desde a última transição. Porta física caída: entra em 3 ticks (15 s).
- **Volume de tráfego (R2)**: não é usado. Opcional como sinal negativo de diagnóstico (`rx-packet` parado por 60 s → log "silêncio absoluto").

## 3. Comutação — ENTRAR (Motor B)

Ordem dentro do mesmo tick, cada passo em `:do{} on-error={:set err ($err+1)}`:
1. `/ip firewall filter set FO-ANTIBYPASS disabled=yes` (antes de qualquer frame ser capturado).
2. `/interface bridge nat enable` FO-CAPTURA-DNS-U/T, FO-PONTE-INTERNO-A/B, FO-CAPTURA, FO-ARPREPLY (a âncora FO-CAPTURA por último: estado só "vira" quando o resto já está de pé).
3. Flush de conntrack da LAN por id: `:foreach c in=[/ip firewall connection find src-address~"^10\\.3\\.74\\."] do={ :do { /ip firewall connection remove $c } on-error={} }` (elimina o "no such item" de 09:18:08).
4. `/ip dns cache flush`.
5. Persistir `fo-meta-last.txt=<uptime s>`, incrementar `fo-meta-n.txt` (janela 1 h), log + POST API.
Efeito no cliente: o próximo frame ao MAC 94:3F:… já cai na CPU; ARP não muda; DHCP real (se vivo) continua servindo com gw 10.3.74.1 (que é o MAC capturado). Sem GARP, sem NAK, sem renumeração.

## 4. Retorno (Motor B)

Condições: `pmmOK` por `win` ticks (inicial 12 = 60 s; ×2 a cada saída dentro de 1 h, teto 180 = 15 min) **e** ≥300 s em failover (hold-down) **e** `FO-PMM-DC` não é exigida (AD pode voltar depois).
Ordem: âncora FO-CAPTURA OFF primeiro (frames voltam a ser bridgeados ao roteador real — que já está respondendo, provado pela sonda) → demais FO-* OFF → FO-ANTIBYPASS ON → flush conntrack + dns cache → persistir/alertar. Janela de coexistência: nenhuma (o MAC de destino sempre foi o do roteador). Sessões estabelecidas via Connect morrem (R17, declarado): TCP longas (push :5228, Wazuh :1514) reconectam sozinhas; navegador reabre.
Teto: `n ≥ 6` em 1 h → estado TRAVADO (sem transições), alarme crítico a cada 10 min; destrava com `/file set fo-meta-n.txt contents=0` ou após 2 h.

## 5. Estados e persistência (R6/R8)

- **Verdade** = `[/interface bridge nat get [find comment="FO-CAPTURA"] disabled]`. Nunca há `$foState`.
- **Contadores** `foDown/foUp` em globals: só debounce; zerados no boot; graça `uptime < 3m` cobre a convergência das portas (combo1 subiu 36 s após o boot em 02/09; A8 desaparece).
- **Meta persistida** em arquivos (`fo-meta-win/n/t0/last.txt`), escritos só em transição (flash: no hEX usar `flash/`).
- **Watchdog de coerência a cada tick**: (i) todas FO-* com o mesmo `disabled` da âncora; (ii) FO-ANTIBYPASS oposta; (iii) `FAILOVER-dhcp` só habilitado se `unknown-server` vazio; (iv) nenhuma sonda com alvo que o MK possa possuir (alvos são IPs públicos; teste de configuração no FO-selftest). Divergência → corrige no mesmo tick + `:log error "FO-INCOERENTE ..."` + POST.
- **Forçado** (`fo-force.txt=on`): tick trata como `want=true` até `auto`; é o "modo Connect-primária" (ver §8).

## 6. Script FO-tick (corpo; gravar em `fo-tick.rsc` e criar com `source=[/file get fo-tick.rsc contents]`)

```
# FO-tick v5 Motor B — 5s. Estado = ancora FO-CAPTURA. Sem globals de estado.
:if ([/system script job print count-only where script=[:jobname]] > 1) do={ :error "FO-tick: ja rodando" }
:local UP "combo1"; :local LANRE "^10\\.3\\.74\\."; :local ANC "FO-CAPTURA"; :local FD ""
:local upS ([:tonsec [/system resource get uptime]] / 1000000000)
:local grace ($upS < 180)
:local anc [/interface bridge nat find comment=$ANC]
:if ([:len $anc] = 0) do={ :log error "FO: ancora FO-CAPTURA ausente"; :error "sem ancora" }
:local emFO (![/interface bridge nat get $anc disabled])
# ---- sondas ----
:local pmmUp 0; :foreach n in=[/tool netwatch find comment="FO-PMM"] do={ :if ([/tool netwatch get $n status] = "up") do={ :set pmmUp ($pmmUp + 1) } }
:local connUp 0; :foreach n in=[/tool netwatch find comment="FO-CONN"] do={ :if ([/tool netwatch get $n status] = "up") do={ :set connUp ($connUp + 1) } }
:local dcUp ([/tool netwatch get [find comment="FO-PMMDC"] status] = "up")
:local linkUp false; :do { :set linkUp [/interface get [find name=$UP] running] } on-error={}
:local pmmOK (($pmmUp >= 2) && $linkUp)
:local pmmDOWN (($pmmUp = 0) || (!$linkUp))
:local connOK ($connUp >= 1)
# ---- debounce (RAM) ----
:global foDown; :global foUp
:if ([:typeof $foDown] != "num") do={ :set foDown 0 }; :if ([:typeof $foUp] != "num") do={ :set foUp 0 }
:if ($pmmDOWN) do={ :set foDown ($foDown + 1) } else={ :set foDown 0 }
:if ($pmmOK) do={ :set foUp ($foUp + 1) } else={ :set foUp 0 }
# ---- meta persistida ----
:local win 12; :local n 0; :local t0 0; :local last 0
:do { :set win [:tonum [/file get ($FD . "fo-meta-win.txt") contents]]; :set n [:tonum [/file get ($FD . "fo-meta-n.txt") contents]]; :set t0 [:tonum [/file get ($FD . "fo-meta-t0.txt") contents]]; :set last [:tonum [/file get ($FD . "fo-meta-last.txt") contents]] } on-error={ :log warning "FO: meta ilegivel, defaults" }
:if (($upS - $t0) > 3600) do={ :set n 0; :set t0 $upS; :set win 12; :do { /file set ($FD . "fo-meta-n.txt") contents="0"; /file set ($FD . "fo-meta-t0.txt") contents=[:tostr $upS]; /file set ($FD . "fo-meta-win.txt") contents="12" } on-error={} }
:local force "auto"; :do { :set force [/file get ($FD . "fo-force.txt") contents] } on-error={}
:local travado ($n >= 6)
# ---- decisao ----
:local want $emFO
:if ((!$grace) && (!$travado)) do={
  :if ($force = "on") do={ :set want true } else={
    :if ($emFO) do={
      :if (($foUp >= $win) && (($upS - $last) >= 300)) do={ :set want false }
    } else={
      :local entrar (($foDown >= 6) || ((!$linkUp) && ($foDown >= 3)))
      :if ($entrar && $connOK && (($upS - $last) >= 120)) do={ :set want true }
      :if ($entrar && (!$connOK)) do={ :log warning "FO: PMM caida e Connect caida - permaneco transparente (R31)" }
    }
  }
}
:if ($travado && (($upS % 600) < 5)) do={ :log error "FO: TRAVADO (>=6 transicoes/h). Destravar: /file set fo-meta-n.txt contents=0" }
# ---- reconciliacao (idempotente; corre TODO tick) ----
:local err 0
:if ($want) do={ :do { /ip firewall filter set [find comment="FO-ANTIBYPASS"] disabled=yes } on-error={ :set err ($err + 1) } }
:foreach r in=[/interface bridge nat find comment~"^FO-" comment!=$ANC] do={ :do { :if ([/interface bridge nat get $r disabled] = $want) do={ /interface bridge nat set $r disabled=(!$want) } } on-error={ :set err ($err + 1) } }
:do { :if ([/interface bridge nat get $anc disabled] = $want) do={ /interface bridge nat set $anc disabled=(!$want) } } on-error={ :set err ($err + 1) }
:if (!$want) do={ :do { /ip firewall filter set [find comment="FO-ANTIBYPASS"] disabled=no } on-error={ :set err ($err + 1) } }
:if ($want != $emFO) do={
  :foreach c in=[/ip firewall connection find src-address~$LANRE] do={ :do { /ip firewall connection remove $c } on-error={} }
  :do { /ip dns cache flush } on-error={}
  :do { /file set ($FD . "fo-meta-last.txt") contents=[:tostr $upS]; /file set ($FD . "fo-meta-n.txt") contents=[:tostr ($n + 1)] } on-error={}
  :if (!$want) do={ :local nw ($win * 2); :if ($nw > 180) do={ :set nw 180 }; :do { /file set ($FD . "fo-meta-win.txt") contents=[:tostr $nw] } on-error={} }
  :set foDown 0; :set foUp 0
  :local msg ("FO-v5 " . [:tostr $want] . " pmmUp=" . $pmmUp . "/3 dc=" . $dcUp . " conn=" . $connUp . "/3 link=" . $linkUp . " err=" . $err . " n=" . ($n + 1))
  :log warning $msg
  :do { /tool fetch url="<API_URL>" http-method=post http-header-field="Authorization: Bearer <API_TOKEN>,Content-Type: application/json" http-data=("{\"event\":\"failover\",\"active\":" . [:tostr $want] . ",\"msg\":\"" . $msg . "\"}") keep-result=no } on-error={ :log warning "FO: POST API falhou" }
}
# ---- DHCP de retaguarda por sonda (R11): unknown-server vazio por 3min (alert-timeout) = servidor real morto ----
:local realVivo false; :do { :set realVivo ([:len [/ip dhcp-server alert get [find interface=bridge-transparente] unknown-server]] > 0) } on-error={}
:local dsrv [/ip dhcp-server find name="FAILOVER-dhcp"]
:if ([:len $dsrv] > 0) do={
  :local dsOff [/ip dhcp-server get $dsrv disabled]
  :if ($realVivo && (!$dsOff)) do={ :do { /ip dhcp-server disable $dsrv; :log warning "FO-DHCP: servidor real voltou -> retaguarda OFF" } on-error={} }
  :if ((!$realVivo) && $dsOff && ($upS > 300)) do={ :do { /ip dhcp-server enable $dsrv; :log warning "FO-DHCP: servidor real mudo ha 3min -> retaguarda ON (authoritative=no)" } on-error={} }
}
# ---- em NORMAL: aprende o MAC do gw e atualiza as regras (troca de roteador da PMM) ----
:if ((!$emFO) && $linkUp) do={ :do {
  :local m [/ip arp get [find address=10.3.74.1 interface=bridge-transparente !invalid dynamic] mac-address]
  :if ([:len $m] = 17) do={
    :local cur [/interface bridge nat get [find comment="FO-ARPREPLY"] to-arp-reply-mac-address]
    :if ($cur != $m) do={ /interface bridge nat set [find comment="FO-ARPREPLY"] to-arp-reply-mac-address=$m; /interface bridge nat set [find comment~"^FO-CAPTURA|^FO-PONTE"] dst-mac-address=($m . "/FF:FF:FF:FF:FF:FF"); :log warning ("FO: MAC do gw real mudou -> " . $m) }
  } } on-error={} }
# ---- coerencia (R8) ----
:local inco 0; :foreach r in=[/interface bridge nat find comment~"^FO-"] do={ :if ([/interface bridge nat get $r disabled] != (!$want)) do={ :set inco ($inco + 1) } }
:if ([/ip firewall filter get [find comment="FO-ANTIBYPASS"] disabled] != $want) do={ :set inco ($inco + 1) }
:if (($inco > 0) || ($err > 0)) do={ :log error ("FO-INCOERENTE objetos=" . $inco . " erros=" . $err . " (corrige no proximo tick)") }
```
Notas: `:tonsec` existe no 7.x; `!invalid dynamic` filtra a entrada aprendida; `$upS % 600` limita o alarme de trava a 1/10 min; o Motor A usa o mesmo esqueleto trocando a âncora (`/ip address find comment="FAILOVER gw takeover"`), a lista de objetos (takeover + FO-CERCA-* + FO-A src-nat) e acrescentando a varredura ARP após ligar.

## 7. Tempos realistas de convergência por família de cliente

| Cliente | Motor A sem varredura (NUD/cache) | Motor A com varredura ARP dirigida | Motor B |
|---|---|---|---|
| Windows 10/11 (NUD RFC 4861: ReachableTime 15-45 s + Delay 5 s + 3 probes) | entrada 20-55 s; saída 20-55 s (+ erro ANTI-BYPASS imediato) | ≤2 s (Windows atualiza entrada existente ao receber ARP de broadcast — doc MS) | 0 s |
| Linux/Android/Tizen (base_reachable 30 s ±50 %, gc_stale 60 s) | 20-60 s | ≤2 s (ARP request dirigido atualiza entrada existente; reply gratuito honrado desde 4.x) | 0 s |
| Impressoras Pantum / lwIP (ARP_MAXAGE 5 min; muitas 10-20 min; sem NUD) | 5-20 min | ≤2 s (lwIP atualiza entrada existente com qualquer ARP do IP) | 0 s |
| Displays Samsung, APs Ubiquiti | como Linux | ≤2 s | 0 s |
| Telefones IP 10.200.0.x | n/a (gateway é o MK sempre) | n/a | n/a |
| Hosts atrás dos roteadores domésticos .45/.98 | herdam o comportamento do TP-Link/Mercusys (5-20 min) | ≤2 s (o roteador é o cliente) | 0 s |

Detecção: 30-45 s (queda acima do gw), 15 s (porta caída). Retorno: 60 s a 15 min conforme backoff + hold-down de 5 min. Total "internet de volta pela Connect" em Motor B: ≈35-50 s após a queda, para 100 % dos hosts ao mesmo tempo.

## 8. O que continua NÃO resolvido por construção

- **Proxy/filtro da PMM furado durante o failover** (C6/R23): tráfego sai pela Connect sem política. Mitigação opcional: reativar em failover a camada DNS do playbook de 29/07 (blacklist por FWD para 0.0.0.0) — decisão do cliente, não técnica.
- **Sessões via Connect morrem na volta** e vice-versa (R17): inevitável com NAT distinto; comunicar.
- **Dual-DHCP residual**: se o alert perder 3 rodadas por congestionamento e o real estiver vivo, por até 1 lease (10 min) haverá dois servidores — ambos com o mesmo gw/DNS e authoritative=no (sem NAK): efeito = host fora do IPAM da PMM por 10 min.
- **IPv6**: RA do roteador real continua chegando (bridge); em falha acima do gw, IPv6 dos clientes fica preto até Happy Eyeballs desistir (centenas de ms por conexão). Opcional em failover: `bridge filter forward in=combo1 out=ether2 mac-protocol=ipv6 drop` (custa hw-offload no hEX e não expira RAs já recebidos).
- **hEX em failover roda bridging por CPU** (regras de bridge nat ativas tiram o H). Aceitável: em failover a internet já passa pela CPU; medir no lab (T12).
- **Troca de MAC do roteador da PMM durante um failover** (Motor B): a captura mira o MAC gravado; o tick só reaprende em NORMAL. Mitigação: regra `FO-WATCH` de log + alarme; operador atualiza.
- **Modo Connect-primária (P5/I6)**: compatível apenas como `fo-force=on` permanente. Aí a bridge deixa de ser o regime normal: proxy da PMM furado 24/7, hEX sem offload 24/7, e o ganho de "detecção" some (só resta perda de DNS interno). Tecnicamente é o I3 usando os objetos da v5 — fica disponível como ferramenta, não como desenho.
- **Hosts capturados pela retaguarda** enquanto ela esteve ligada permanecem com lease do MK por ≤10 min após o real voltar (renovam unicast a .254 que já não responde → REBIND broadcast → real). Aceito.
- **Dependência de globals**: só `foDown/foUp` (debounce); um reboot os zera sem consequência (graça de 3 min).

## DHCP/DNS
# DHCP e DNS na v5

## DHCP — princípio: um único servidor por instante, provado por sonda (R11)

1. **Estado normal**: `FAILOVER-dhcp disabled=yes`. Zero leases em normal (o critério da skill "lease print vazio" volta a valer de fato — hoje há 46).
2. **Sonda do servidor real**: `/ip dhcp-server alert interface=bridge valid-server=<MK_MAC> alert-timeout=3m`. Pela doc, o detector "acts as a DHCP client as well — it sends out DHCP discover requests once a minute"; toda OFFER do servidor real (via relay em 10.3.74.1, MAC do roteador) o mantém em `unknown-server`. Lista vazia = 3 rodadas sem resposta = servidor real morto. Funciona igual em normal e em failover, sem depender do campo `secs` do cliente.
3. **Servidor real morto** (qualquer estado, uptime > 5 min): `FAILOVER-dhcp enable` com `authoritative=no delay-threshold=0s conflict-detection=yes lease-time=10m`, network idêntica (gw 10.3.74.1, DNS 10.135.16.119/.17, pmm.local). `authoritative=no` = nunca NAK (R12): host em INIT-REBOOT/RENEW de endereço que não conhecemos fica com o endereço que já tem (válido por até 8 h) — só DISCOVER genuíno recebe oferta.
4. **Servidor real volta**: `disable` imediato; hosts com lease nosso renovam unicast a .254 (silêncio) → REBIND broadcast em ≤10 min → real responde → de volta ao IPAM da PMM.
5. **Harvest (I7, R12)**: `FO-harvest` diário às 03:15 (e antes do cutover): varre 10.3.74.2-253 com `/ping ... src-address=10.3.74.254 count=1` (a resolução ARP acontece mesmo com ICMP bloqueado), lê `/ip arp` dinâmicos completos e cria `lease add server=FAILOVER-dhcp address=X mac-address=M comment="harvest <data>"` para quem não tem; nunca sobrescreve; hosts fora do pool (ex. .251) entram como estáticos (o servidor honra estático fora do pool). Resultado: mesmo host → mesmo IP mesmo na pior hipótese (real morto + lease vencido).
6. **Pool**: `.30-.200` (`.10-.29` reservado: 4 Pantum fixas .10/.11/.13/.14 e margem). `.251` estático colhido.
7. **Takeover de 10.1.201.254**: REMOVIDO (R13). Não é necessário: renovação unicast ao real (vivo) segue em ponte (Motor B) ou é roteada; ao real morto, o cliente aguarda T2 e faz REBIND broadcast, que a retaguarda atende com o mesmo IP (harvest). Sem sequestro de serviço de terceiro.
8. **Opção 252 (WPAD)**: não configurada — não há WPAD no retrato (hosts.md §6). Se a PMM usar, copiar via `/ip dhcp-server option add code=252`.
9. **use-reconfigure/FORCERENEW**: não usado (clientes Windows ignoram).

## DNS — política única nos dois estados (R14/R15/R16)

| Item | Normal | Failover |
|---|---|---|
| Quem resolve para os clientes | DCs 10.135.16.x direto (ponte) | MK (captura :53 na L2 + dstnat redirect udp/tcp 53) |
| pmm.local, *.pmm.local, reversos 74.3.10 e 16.135.10 | DCs | FWD → forwarder `pmm-dc` (rota 10/8 via gw real). DC morto ⇒ SERVFAIL/timeout, **nunca NXDOMAIN** cacheável |
| Resto | proxy/DNS da PMM | 1.1.1.1 / 8.8.8.8 via Connect; `use-peer-dns=no` na Connect (resolvedor do ISP proibido) |
| allow-remote-requests | yes (permanente) | yes |
| Quem pode perguntar ao MK | firewall input: só 10.3.74.0/24 e 10.200.0.0/24 vindos da bridge | idem |
| Cache | `cache-max-ttl=1h`; flush em toda transição | idem |
| DoT 853 / DoQ | passam pela ponte (política da PMM) | Motor B: seguem ao gw real → morrem com a internet da PMM (equivale a bloqueio). Opcional: bridge nat `ip-protocol=tcp dst-port=853 action=drop` para falha rápida. DoH em 443 indistinguível — registrado como limite |
| Telefones 10.200.0.x | MK (10.200.0.1) sempre | idem |

Teste de aceite: em failover com DC vivo, `nslookup dc01.pmm.local` num PC devolve o A real; com DC morto devolve "server failed" (não "non-existent domain"); `/ip dns cache print where name~"pmm"` vazio após a volta.

## NAT
# NAT — masquerade vs identidade, e o teste que decide (P1)

## Posição da v5

| Sentido / estado | Normal | Failover Motor B | Failover Motor A |
|---|---|---|---|
| LAN → internet | sem NAT (ponte; PMM vê cada host) | masquerade pela Connect (inevitável) | idem |
| LAN → internos PMM (10/8, 172.16/12) | sem NAT (ponte) | **sem NAT** — frames seguem em ponte ao roteador real; ele resolve os clientes normalmente | src-nat → 10.3.74.254 (a cerca impede o roteador de resolver clientes) |
| PMM → LAN (fluxos iniciados de fora: SCCM/Wazuh push, RDP de suporte, SNMP a impressoras, impressão por IP) | funcionam (identidade preservada) | funcionam (roteador entrega direto ao cliente; resposta do cliente ao MAC do roteador é PONTEADA pela regra FO-PONTE-INTERNO se o destino for interno) | quebram (cliente não é resolvível; NAT só cobre saídas) |
| Inbound 1:1 | não se aplica: não há NAT em normal | não se aplica | não se aplica |

Conclusão: na v5 a decisão P1 **não afeta o regime normal** (identidade é preservada por construção) e só distingue os motores no failover: Motor B preserva identidade também para internos; Motor A degrada para masquerade. Esse é um argumento a mais para o Motor B.

## Onde a medição de fluxos de entrada ainda importa

1. Escolher entre Motor B e A se o lab empatar.
2. Modo Connect-primária/roteado (I3/I4): ali a unidade vira 1 IP para a PMM e todo fluxo de entrada morre — a medição diz quantos são.
3. Sondas: se a borda só devolve TCP para hosts com lease DHCP (V7), a sonda TCP precisa de identidade de cliente.

## Teste de borda (executar em normal, janela de 24 h, uma unidade)

```
# 1) tornar o trafego em ponte visivel ao firewall IP (fast-path cai; CPU sobe — ok por 24h no CCR; no hEX medir)
/interface bridge settings set use-ip-firewall=yes use-ip-firewall-for-vlan=yes
# 2) contar conexoes NOVAS iniciadas do lado PMM para a LAN (SYN sem ACK e UDP novo), sem bloquear nada
/ip firewall raw add chain=prerouting action=passthrough in-bridge-port=combo1 dst-address=10.3.74.0/24 protocol=tcp tcp-flags=syn,!ack comment="MED inbound tcp"
/ip firewall mangle add chain=prerouting action=add-src-to-address-list address-list=MED-INBOUND-SRC address-list-timeout=2d in-bridge-port=combo1 dst-address=10.3.74.0/24 protocol=tcp tcp-flags=syn,!ack comment="MED quem inicia"
/ip firewall mangle add chain=prerouting action=add-dst-to-address-list address-list=MED-INBOUND-DST address-list-timeout=2d in-bridge-port=combo1 dst-address=10.3.74.0/24 protocol=tcp tcp-flags=syn,!ack
/ip firewall mangle add chain=prerouting action=add-src-to-address-list address-list=MED-INBOUND-SRC address-list-timeout=2d in-bridge-port=combo1 dst-address=10.3.74.0/24 protocol=udp connection-state=new dst-port=!53,!67,!68
# 3) apos 24h: /ip firewall address-list print where list~"MED"; /ip firewall raw print stats; depois REMOVER tudo e voltar use-ip-firewall=no
```
Critério: se `MED-INBOUND-SRC` contiver origens 10.135.x/10.1.x com portas de serviço (135/445/3389/161/9100/1514) para vários hosts → identidade preservada é requisito (Motor B; roteado só com 1:1 por host, inviável). Se vazio/só ruído → masquerade aceitável em qualquer desenho.

## Teste de identidade das sondas (V7, decide R3)
```
/ip dhcp-client add interface=bridge-transparente add-default-route=no use-peer-dns=no use-peer-ntp=no comment="MED sonda com IP de cliente" dhcp-options=hostname,clientid
# aguardar bound (relay pode levar 50s); X = lease obtida
/ip route add dst-address=1.1.1.1/32 gateway=10.3.74.1 pref-src=X comment=MED
/tool netwatch add type=tcp-conn host=1.1.1.1 port=443 src-address=X interval=10s comment=MED
/tool netwatch add type=https-get host=1.1.1.1 src-address=X comment=MED
# comparar com src-address=10.3.74.254. Se X passa e .254 nao: politica "so host com lease" -> manter dhcp-client de sonda e usar X nas sondas TCP.
```

## Regras finais de NAT (permanentes, ver config): masquerade só `out-interface-list=WAN` e `dst-address-list=!RFC1918` (R18); nunca NAT em wg-voip; MSS clamp <MSS_CONN> nos dois sentidos da WAN (P6); `send-redirects=no`.

## Cutover/Rollback
# Cutover no Complexo (CCR) — de "failover manual com combo1 desabilitada" para v5

**Ponto de partida (02/09 10:06)**: combo1 `disabled=yes`; `10.3.74.1/32` e `10.1.201.254/32` habilitados; 4 NAT FAILOVER ligadas; `allow-remote-requests=yes`; FAILOVER-dhcp authoritative=yes; scheduler `failover-eth3` a cada 5 s com `foState=failover`; PSU1 em FAIL.

**Janela**: 2 h, fora do horário de regulação (sugestão 18:30-20:30 ou sábado 08:00). **Presença**: 1 pessoa no site com notebook cabeado na LAN + celular no Wi-Fi da Connect (canal independente); operador remoto pela automais-vpn. Pré-condições: backup baixado, nobreak no CCR e no switch core confirmados (PSU1 FAIL torna o reboot provável), `FO-harvest` já executado em failover (todos os 67 hosts vivos viram lease estática).

## Fase 0 — instalar tudo INERTE (sem impacto)
1. `/system backup save name=antes-v5` + `/export file=antes-v5`.
2. Blocos 0-6 e 8-9 do `config_untagged_rsc` (bridge nat FO-* `disabled=yes`, FO-tick `disabled=yes`). O bloco 7 (firewall default-drop) por último, com `SAFETY-revert` armado e a sessão aberta pela automais-vpn; validar login por Winbox/SSH; então `/system scheduler remove SAFETY-revert`.
3. **Não** tocar ainda em: takeover gw, NAT antigas, combo1, scheduler antigo. Nada mudou para os clientes.
4. Verificar: `/tool netwatch print` (FO-CONN up; FO-PMM down — combo1 fechada), `/ip dhcp-server alert print` (unknown-server vazio — real inalcançável), `/ip dns cache print where name~"pmm"`.

## Fase 1 — provar a captura com UM host (10 min, reversível)
5. No notebook: `netsh interface ipv4 add neighbors "Ethernet" 10.3.74.1 94-3f-c2-df-49-d3` (ARP estático → MAC do roteador real, que está fora da L2). Sem a captura, o notebook perde a internet imediatamente (frames vão para porta morta).
6. `/interface bridge nat enable [find comment~"^FO-CAPTURA"]` (só as 3 de captura). Notebook navega e resolve `nslookup smsmarica.online`? → captura provada (Motor B viável). `/ip firewall connection print where src-address~"<IP notebook>"` mostra `s` (srcnat).
7. Se NÃO navegar: **parar aqui** — desabilitar as 3 regras, remover o neighbor estático, seguir com Motor A (bloco 10) em outra janela. Nada mudou para os demais.
8. `netsh interface ipv4 delete neighbors "Ethernet" 10.3.74.1`.

## Fase 2 — trocar o motor (clientes continuam no MK; zero indisponibilidade)
9. `/interface bridge nat enable [find comment~"^FO-"]` (todas as 6, ARPREPLY incluída).
10. `/ip address disable [find comment="FAILOVER gw takeover"]` — o MK para de responder ARP por 10.3.74.1 com o próprio MAC; a partir daí, quem re-resolver recebe 94:3F:… (arp-reply) e cai na captura; quem ainda tem o MAC do MK em cache continua funcionando (MK roteia). `/ip address remove [find comment="FAILOVER dhcp takeover"]` (já feito no bloco 5).
11. `/ip firewall nat remove [find comment~"^FAILOVER"]` (as permanentes FO-MASQ/FO-DNSREDIR já cobrem). `/system scheduler remove failover-eth3; /system script remove failover-eth3-check`.
12. Observar 10 min: amostra de 6 hosts (Windows FEMAR, Android, Pantum .10, display .55, AP .53, notebook) navegando; `arp -a` no notebook mostra 10.3.74.1 → 94-3f-c2-df-49-d3; `/ip firewall filter print stats where comment=FO-ANTIBYPASS` sem hits (está desabilitada).
13. `/system scheduler set FO-tick disabled=no`. Log deve mostrar nada (estado derivado = failover, coerente); `/log print where message~"FO"`.

## Fase 3 — devolver o roteador real à L2 e deixar o tick decidir
14. `/interface enable combo1`. Efeitos esperados em 60 s: `/ip arp print where address=10.3.74.1` aprende 94:3F:…; FO-PMM-DC `up` (se a PMM interna estiver viva); FO-PMM-1..3 `up` se a internet da PMM voltou. Nenhuma guerra de ARP: roteador e MK respondem com o MESMO MAC.
15. Se a internet da PMM está de volta: após `win=12` ticks (60 s) + hold-down (300 s desde `fo-meta-last`) o tick desliga as FO-*, liga o anti-bypass, flusha conntrack → unidade transparente. Confirmar: `/interface bridge nat print` tudo `X`; `/ip firewall connection print where src-address~"^10\\.3\\.74\\."` esvazia em ≤60 s; sessões novas dos PCs saem pelo proxy da PMM; `FO-ANTIBYPASS` com 0 hits (Motor B não deixa ARP velho).
16. Se a internet da PMM continua fora: permanece em failover, agora **automático e com sonda de retorno** — não é preciso ninguém no site quando ela voltar. Registrar no ticket.

## Critérios de sucesso (todos obrigatórios)
- Em failover: 100 % de 6 hosts amostrados navegam; `pmm.local` resolve se FO-PMM-DC up; zero leases novas no FAILOVER-dhcp enquanto `unknown-server` ≠ vazio; syslog remoto recebeu o evento; `FO-INCOERENTE` ausente do log.
- Na volta: transição automática única (sem flap) em ≤ 6 min após FO-PMM up; `ANTI-BYPASS` sem hits; `/ip dns cache` sem pmm.local; nenhum host precisou de intervenção.
- Reboot controlado (fim da janela, opcional): `/system reboot` → em 3 min nenhum evento FO; objetos no mesmo estado de antes; `/ip dhcp-server lease print` sem leases dinâmicas novas (A8 eliminado).

## Rollback — 1 comando
`/system script run FO-rollback` → desliga FO-tick, desabilita FO-*, reabilita takeover gw, FAILOVER-dhcp authoritative=yes, `combo1 disabled=yes`, flush conntrack. Reproduz exatamente o estado manual das 09:41 de 02/09 (sem o takeover de 10.1.201.254 e sem o resolvedor do ISP — ambos dispensáveis, ver dhcp_dns). Tempo: < 5 s; clientes com ARP → MAC do roteador re-resolvem para o MK em 15-55 s (NUD) — é a única janela de degradação do rollback, e só se ele for acionado com combo1 já fechada.
Rollback total (raro): `/system backup load name=antes-v5` (reboot ~2 min).

## Lab
# Bancada — reproduzir PMM + switch + clientes e provar os claims

## Montagem
```
[MK-PMM]  hEX ou qualquer RB: "roteador da Prefeitura"
   ether1 -> internet do lab (NAT). ether2 = 10.3.74.1/24 (MAC anotado = GW_MAC) + 172.20.1.1/24 (gerencia)
   dhcp-relay em ether2 -> servidor DHCP em 10.1.201.254 (uma VM/ether3 do MK-PMM, escopo 10.3.74.10-200, gw .1, DNS 10.135.16.119)
   VM "DC": 10.135.16.119 com dnsmasq (zona pmm.local + tcp/53) atras de ether3 do MK-PMM
   Botoes de falha: (a) /interface disable ether1 (internet morta, gw vivo = 02/09); (b) /ip firewall filter drop out ether1 so TCP/UDP (proxy morto); (c) desligar MK-PMM (ONU morta); (d) parar o DHCP; (e) trocar MAC de ether2 (roteador substituido)
[MK-UUT] hEX RB750Gr3 com a config v5 (untagged) — depois com switch gerenciado entre ether4 e clientes para o caso TAGGED (trunk VID 1092 + untagged gerencia)
[Connect] roteador doméstico com MTU forcado 1480 no WAN (reproduz P6) + Wi-Fi com celular como "cliente do ISP" (teste R21)
Clientes: Windows 11, Ubuntu, Android (USB-ether ou Wi-Fi do AP de lab), impressora Pantum ou ESP32/lwIP, telefone IP (10.200.x), display/TV Android
Ferramentas: Wireshark no Windows (filtro arp || dhcp), `arp -a`/`ip neigh` a cada 5 s (loop), ping continuo a 1.1.1.1 com timestamp por cliente
```

## Testes e resultado esperado

| # | Teste | Como | Esperado (aprovação) |
|---|---|---|---|
| T1 | Captura L2 (CLAIM C1) | FO-CAPTURA on; PC com ARP normal (gw real vivo); botão (a) | PC navega pela Connect em ≤5 s; `arp -a` inalterado (GW_MAC); `/ip firewall connection` no UUT mostra o PC com `s` |
| T2 | arp-reply com MAC alheio (C2) | botão (c) (MK-PMM desligado); FO-ARPREPLY on; `arp -d *` no PC | PC re-resolve 10.3.74.1 → GW_MAC (Wireshark mostra reply vindo do UUT); navega |
| T3 | Ponte seletiva de internos (C3) | failover on; PC acessa 172.20.1.1 e 10.135.16.119:53 | tráfego não aparece no conntrack do UUT; DC responde; SNMP do MK-PMM ao "switch" 172.20.1.2 continua (P2) |
| T4 | Matchers internos sob VLAN (C4) e arp-reply tagged (C5) | montagem TAGGED; repetir T1-T3 | idem; Wireshark no PC mostra arp-reply com tag 1092. Se falhar → variante T2 (captura cega) e medir hairpin |
| T5 | hw-offload com regras desabilitadas (C6) | hEX: `/interface bridge port print` antes/depois de criar as FO-* `disabled=yes`; depois habilitá-las | H presente com regras desabilitadas (senão o tick passa a add/remove); H some com regras ativas; throughput iperf entre portas em normal ≈ linha, em failover medir (meta ≥ 150 Mbps) |
| T6 | Detecção 02/09 | botão (a) | FO-PMM-1..3 down em ≤20 s; entrada em 30-45 s; log com pmmUp=0 dc=true; FO-PMM-DC segue up |
| T7 | Identidade das sondas (V7/R3) | no lab a borda não filtra; teste real: repetir procedimento do nat_markdown numa unidade | decide se sondas TCP usam IP de lease |
| T8 | Retorno com backoff | botão (a) off/on 4 vezes em 30 min | 1ª saída após 60 s + hold 5 min; janelas 120 s, 240 s, 480 s; 6ª transição/h → TRAVADO + log a cada 10 min |
| T9 | Ambos caídos (R31) | botão (a) + cabo da Connect fora | permanece transparente; log "permaneco transparente"; ao voltar a Connect, entra |
| T10 | Boot (A8) | `/system reboot` em normal e em failover | nenhuma transição nos primeiros 180 s; estado igual ao anterior; zero leases novas |
| T11 | Abort no meio (A9) | inserir `:error` artificial num passo do tick (cópia de teste) | tick seguinte completa; log FO-INCOERENTE uma vez; nunca dois donos de IP |
| T12 | CPU/PMTU | failover on, 10 clientes iperf/HTTP via Connect com MTU 1480 | sem buraco negro (MSS 1440); CPU hEX < 80 %; sites "abrem e não caem" |
| T13 | DHCP por sonda (R11) | botão (d) por 4 min; depois religar | `unknown-server` esvazia em ≤3 min → FAILOVER-dhcp ON; PC novo recebe IP do pool .30+; PC antigo mantém IP; ao religar, OFF em ≤60 s; PC "capturado" volta ao real em ≤10 min |
| T14 | Nunca NAK (R12) | com retaguarda ON, PC com IP .251 fixo/estático reinicia | nenhum NAK no Wireshark; PC mantém .251 |
| T15 | DNS interno em failover (R14) | botão (a): `nslookup dc.pmm.local` e `nslookup naoexiste.pmm.local` | primeiro resolve; segundo NXDOMAIN vindo do DC (autoritativo, correto). Com botão (c): "server failed", nunca NXDOMAIN; após volta `cache print` sem pmm |
| T16 | Troca de MAC do roteador (risco) | botão (e) em normal; depois em failover | em normal: log "MAC do gw real mudou" e regras atualizadas em ≤10 s; em failover: PC com cache do MAC novo perde internet → documenta alarme FO-WATCH (Motor A) / procedimento manual |
| T17 | Segurança (R20/R21) | do celular no Wi-Fi da Connect: `route add 10.3.74.0/24 via <IP do UUT>`; nmap | 0 portas; drop Connect→LAN; Winbox de 10.3.74.x com 3 senhas erradas → FO-BRUTE por 1 h |
| T18 | Motor A completo (fallback) | aplicar bloco 10; repetir T1/T2/T6/T8 com medição de `arp -a` a cada 5 s por família | com varredura: todos ≤2 s; sem varredura: tabela de tempos do failover_markdown §7 confirmada/ajustada; shim de retorno (opcional) reduz saída a 0 s |
| T19 | Telemetria (R24) | qualquer transição | evento no syslog remoto e HTTP 2xx do POST em ≤5 s; reboot preserva log em disco |
| T20 | Rollback | `/system script run FO-rollback` em failover e em normal | estado manual reproduzido em <5 s; clientes voltam em ≤55 s (NUD) |

Saída do lab: preencher a coluna "Resultado" e decidir Motor B × A por unidade (untagged/tagged) antes do cutover no Complexo.

## Riscos
- **bridge nat action=redirect nao entrega o frame a pilha IP (ou entrega sem rotear) — Motor B inviavel** (p=media (documentado como 'redirect packet to bridge itself', mas nunca provado neste projeto; forum inconclusivo), i=alto no plano, nulo em producao (descoberto na Fase 1 do cutover com 1 host ou no lab T1)) → teste de 10 min com ARP estatico num unico PC antes de qualquer mudanca; fallback Motor A pronto (bloco 10) usando so takeover + bridge filter + varredura ARP, cujas primitivas estao documentadas
- **Nas unidades TAGGED os matchers IP/ARP nao valem sob vlan-encap (doc: 'ARP matchers only valid if mac-protocol is arp')** (p=media-alta, i=medio: perde-se a ponte seletiva de internos e o arp-reply seguro na VLAN) → variante T2 (captura cega + hairpin com notrack) e Motor A-tagged com cerca ARP em bloco na VID; validar em T4 antes de tocar CDT/CAPS AD/CEREST/SAE
- **Roteador da PMM trocado (MAC novo) durante um failover** (p=baixa, i=alto: clientes que re-resolverem recebem o MAC novo e saem da captura) → tick reaprende MAC em normal; regra FO-WATCH (bridge filter log) + alarme; procedimento manual de 1 comando (set dst-mac/to-arp-reply-mac)
- **hEX em bridging por CPU durante failover longo (regras de bridge nat tiram o H)** (p=certa quando em failover, i=medio: throughput LAN<->PMM interno e CPU; se regra desabilitada tambem tirar o H, impacto permanente) → T5 mede; se necessario o tick faz add/remove das regras; em failover a internet ja passa pela CPU (NAT) de qualquer forma
- **Borda da PMM passa a filtrar ICMP do .254 (sondas cegas -> nunca sai do failover ou nunca entra)** (p=baixa-media, i=alto) → 3 alvos distintos + sonda TCP/53 ao DC como sinal de 'PMM interna viva'; alarme se FO-PMM 3/3 down por >24h com FO-PMM-DC up (sugere filtro, nao queda); opcao de sonda com IP de lease (T7)
- **dhcp-server alert nao ve a OFFER do relay (unicast a chaddr do MK, deveria ver) ou o relay ignora DISCOVER com hostname do MK** (p=baixa, i=medio: retaguarda ligaria com o real vivo (dual-DHCP sem NAK) ou nunca ligaria) → T13; fallback = dhcp-client de sonda com /ip dhcp-client renew a cada 10 min como segundo sinal
- **Firewall default-drop derruba a gestao remota na aplicacao** (p=media, i=alto (viagem ao site)) → SAFETY-revert 5 min; regras de accept automais-vpn/OOB antes do drop; aplicar com pessoa no site na janela
- **Queda de energia (PSU1 FAIL) durante o cutover** (p=media, i=medio: reboot no meio da Fase 2) → estado e persistido em objetos; graca de boot; nobreak confirmado como pre-condicao; se cair antes do passo 13 o roteador fica como estava (takeover off, FO-* on = failover coerente)
- **Proxy/filtro da PMM furado durante failover sem ciencia formal do cliente (R23)** (p=certa, i=medio (politica/compliance)) → registrar decisao por escrito; opcao de reativar em failover a blacklist DNS do playbook de 29/07 via FWD
- **Conntrack loose/notrack e assimetria nos internos causam drop por 'invalid' em hairpin (variante T2)** (p=media so na T2, i=baixo-medio) → raw notrack para LAN->PMM-INTERNO + accept untracked; T3/T4 validam
- **Operador confia no painel e ninguem le o syslog/API** (p=media, i=medio (estado travado ou incoerente por dias)) → POST na API gera notificacao humana; drill mensal FO-drill com relatorio; alarme repetido em TRAVADO

## Requisitos
- [sim] R1 alcancabilidade fim-a-fim por caminho, multiplos destinos — 3 netwatch ICMP pinados por rota /32 via gw real + tcp/53 DC (PMM) e 3 pela Connect; link/contador/ping ao gw nao decidem; prova: T6
- [sim] R2 volume de trafego nao e prova positiva — rx-packet removido da decisao; so opcional como log de silencio
- [parcial] R3 N-de-M, timeout, retry, diversidade de protocolo imune a ICMP rate-limit — N-de-M (2/3) com packet-count=3 e 6 ticks; ICMP + TCP/53 DC + TCP/443 pela Connect; TCP/HTTP pela PMM depende de a borda devolver (V7) — resolvido por T7 (sonda com IP de lease) ou aceito como limite
- [sim] R4 retorno prova internet pela PMM sem recolocar o roteador na L2 dos clientes — Motor B: roteador nunca sai da L2 e nunca disputa IP; sondas continuam via 10.3.74.1 em failover. Motor A: alias ARP I2 (next-hop != IP local); combo1 nunca e desabilitada
- [sim] R5 histerese assimetrica, hold-down, backoff, teto com trava e alarme — entrada 30 s (15 s fisico), saida 60 s x2 ate 15 min, hold-down 5 min, min 2 min entre transicoes, teto 6/h -> TRAVADO + alarme 10/10 min; T8
- [sim] R6 estado derivado da config, sobrevive a reboot, boot como estado especial — ancora = disabled da regra FO-CAPTURA; meta em arquivos; graca uptime<180 s; T10
- [sim] R7 transicao idempotente por reconciliacao, on-error em tudo, lock — reconciliacao a cada tick para cada objeto em :do on-error; flush por id; lock por :jobname; T11
- [sim] R8 auto-teste de coerencia e alvo de sonda nunca possuivel pelo MK — bloco de coerencia no tick (FO-* vs ancora, anti-bypass, DHCP vs alert) com log error + POST; alvos sao IPs publicos; no Motor B o MK nunca possui 10.3.74.1
- [sim] R9 nunca dois donos do mesmo IP na mesma L2 — Motor B: sem takeover; arp-reply usa o MAC do proprio roteador (respostas identicas). Motor A: takeover so com cerca ARP ligada no mesmo tick; janela declarada < 1 tick
- [sim] R10 invalidacao ativa de cache com prazo medido por SO — Motor B: nao ha troca de MAC (0 s). Motor A: varredura ARP dirigida em 0/10/30 s; tabela por SO em failover_markdown §7 a confirmar em T18
- [sim] R11 um unico DHCP autoritativo por instante, provado por sonda ao servidor real — FAILOVER-dhcp desligado; dhcp-server alert emite DISCOVER 1x/min; liga so com 3 min sem OFFER do real; desliga ao primeiro sinal; T13
- [parcial] R12 identidade de endereco, sem NAK fora do pool, respeita reservas — authoritative=no sempre (zero NAK); harvest diario cria leases estaticas IP<->MAC inclusive fora do pool; pool .30-.200; reservas do servidor real nao sao importaveis sem a PMM (P3) — coberto pelo harvest do que esta vivo; T14
- [sim] R13 proibido takeover persistente de IP de terceiro — 10.1.201.254/32 removido do desenho; renovacoes seguem em ponte ao real ou caem em REBIND atendido com o mesmo IP
- [sim] R14 politica DNS coerente, sem NXDOMAIN cacheavel interno, forwarders registrados, sem resolvedor do ISP — FWD pmm.local/reversos -> DCs; servers=1.1.1.1,8.8.8.8; use-peer-dns=no; cache-max-ttl=1h; flush nas transicoes; T15
- [parcial] R15 captura 53 udp+tcp, DoT/DoH tratados, todas as faixas do segmento — udp e tcp 53 capturados na L2 (todo frame ao gw) + dstnat; DoT 853 morre com a PMM (opcao de drop rapido); DoH em 443 indistinguivel — limite registrado; faixa = 10.3.74.0/24 + telefones (10.200) ja resolvem no MK; outras faixas untagged nao sao clientes (gerencia 172.20.1.x deve ficar fora)
- [sim] R16 resolvedor so para origens necessarias por firewall permanente — input accept udp/tcp 53 so de 10.3.74.0/24 e 10.200.0.0/24 vindos da bridge; drop default
- [sim] R17 politica de sessoes em transicao, flush completo com on-error, vale para ativacao manual — flush por id em ambas as direcoes de estado; FO-force passa pelo mesmo tick (mesmo flush); sessoes longas declaradas como nao migraveis
- [sim] R18 nenhum trafego interno NATeado para a Connect — Motor B: internos nem entram na pilha (ponte); masquerade com dst-address-list=!RFC1918; rotas 10/8 e 172.16/12 via gw real; T3
- [sim] R19 anti-bypass permanente reconciliado, nunca ligado em failover — FO-ANTIBYPASS reject+log; reconciliada a cada tick como oposto da ancora; desligada ANTES de ligar a captura
- [sim] R20 default-drop input e forward incl. WireGuard — bloco 7: drops finais; regra 8291 solta removida; peers WG so com regras especificas; T17
- [sim] R21 LAN inalcancavel do segmento da Connect — forward drop in-interface-list=WAN + input drop ether3; T17 com celular no Wi-Fi do ISP
- [parcial] R22 gestao sem contas genericas, sem policies perigosas, dupla barreira, bloqueio por tentativas — grupo 'leitura' sem sensitive/sniff/reboot/romon; FO-BRUTE em 3 tentativas; input por interface E ACL de servico; remocao do admin condicionada a teste do usuario becape (nao automatizada)
- [parcial] R23 politica de uso durante failover decidida e registrada — o desenho torna a decisao explicita (proxy furado em failover) e oferece a camada DNS de bloqueio opcional; a decisao em si e do cliente
- [sim] R24 telemetria e alarme fora do equipamento — syslog remoto (script/warning/error), log em disco, POST na API automais.io a cada transicao/incoerencia; T19
- [sim] R25 relogio sincronizado e log persistente — ntp client ntp.br; logging action=disk para info
- [parcial] R26 teste sintetico periodico com relatorio — scheduler FO-drill mensal previsto (script forca failover 10 min e reverte, publicando metricas via POST); corpo do drill a escrever apos homologacao
- [nao] R27 autonomia de energia e alarme de PSU — fora do escopo do MK; recomendado nobreak cobrindo CCR+ONU+switch core e alerta de /system health psu1-state via tick (uma linha a acrescentar)
- [nao] R28 gestao independente do link de failover — continua pela Connect (link A pela PMM bloqueado pela borda — V7). Opcao: LTE USB no hEX como OOB; nao resolvido por este desenho
- [parcial] R29 configuracao como codigo com verificacao de conformidade — config em blocos com placeholders e comentarios FO-*/v5 auditaveis por comment; script FO-selftest (exportar objetos FO-* e comparar) sugerido; deriva ainda nao automatizada na frota
- [sim] R30 mudanca observavel e explicavel, reversivel por um comando — log de transicao carrega pmmUp/dc/conn/link/err/n; FO-rollback e FO-force
- [sim] R31 degradacao segura: preferir estado sem conflito na L2 — PMM+Connect caidas -> permanece transparente e alarma; Motor B nunca assume IP de terceiro; TRAVADO congela em vez de oscilar

## Perguntas abertas
- O `action=redirect` do bridge NAT entrega o frame a pilha IP do RouterOS com dst MAC ≠ MAC da bridge, e a pilha roteia (nao descarta como 'nao e para mim')? (T1 decide Motor B x A — e o teste mais barato de todo o projeto: 1 PC, 10 minutos)
- Nas unidades tagged, os matchers src-address/dst-address/ip-protocol/arp-dst-address funcionam com mac-protocol=vlan vlan-encap=ip|arp? A resposta de arp-reply sai com a tag do request? (T4)
- A borda da PMM devolve ICMP para 10.3.74.254 tambem no Complexo (V7 foi medido no CAPS AD)? E TCP/443 volta para um IP obtido por DHCP? (T7 — define se a sonda TCP existe)
- Regras de bridge filter/nat DESABILITADAS mantem o H (hw-offload) no MT7621 do hEX? (T5 — define enable/disable vs add/remove no tick)
- O dhcp-server alert do MK recebe a OFFER do servidor real via relay do HPE (unicast ao chaddr do MK) e a lista unknown-server reflete isso em ate 1 min? (T13)
- Qual e a politica formal do cliente para conteudo durante o failover (R23): furar o proxy da PMM e aceitavel? Por quanto tempo? Reativar a blacklist DNS de 29/07?
- Existe segundo esquema de enderecamento acima de .200 (o .251) ou reservas no DHCP real que o harvest nao ve por estarem desligadas na hora da varredura?
- O switch core da unidade tagged exige que o arp-reply/redirect preservem a prioridade 802.1p ou algum voice-VLAN para os telefones? (hoje telefones estao na mesma VID)
- A PMM usa HSRP/VRRP no 10.3.74.1 (MAC virtual estavel) ou um unico HPE (MAC fisico, muda em troca de hardware)? Muda o peso do risco 'MAC do roteador'
- Ha objecao da PMM a um DISCOVER por minuto por unidade (alert) e a um ping por 10 s a 3 IPs publicos por unidade?
- Para R28: aceita-se LTE USB no hEX (porta USB livre) como gestao OOB, ja que ether3 esta reservada e o link A pela PMM esta bloqueado?

## Claims a verificar
- **C1 — `/interface bridge nat chain=dstnat action=redirect` (ou dst-nat para o MAC da bridge) sobre frames IP unicast dirigidos ao MAC do roteador real faz o frame ser entregue a CPU e ROTEADO pelo /ip (forward, NAT), inclusive em hEX com CPU bridging** — e a fundacao do Motor B; se falhar, cai-se para o Motor A (takeover + cerca + varredura)
- **C2 — `action=arp-reply to-arp-reply-mac-address=<GW_MAC>` responde a ARP request por 10.3.74.1 com o MAC do roteador real e os clientes (Windows/Linux/lwIP) aceitam e usam essa resposta quando o roteador esta morto** — cobre o caso ONU/fibra morta (20/07) sem takeover de IP
- **C3 — a ordem das regras de bridge nat e respeitada (accept encerra a chain) de modo que internos (10/8, 172.16/12) sigam em ponte e :53 seja capturado antes** — identidade preservada para internos e DNS 100% capturado (R15/R18)
- **C4 — matchers IP (src-address, dst-address, ip-protocol, dst-port) e ARP (arp-opcode, arp-dst-address) funcionam sob `mac-protocol=vlan vlan-id=X vlan-encap=ip|arp` (a doc so garante 'valid if mac-protocol is arp/ip')** — decide se as 4 unidades tagged usam Motor B pleno (T1) ou captura cega/Motor A-tagged (T2)
- **C5 — a resposta gerada por arp-reply a um request tagged sai tagged com a mesma VID** — sem isso o cliente na VLAN nunca ve a resposta
- **C6 — regras de bridge filter/nat DESABILITADAS nao removem o H do MT7621; regras ativas removem (e o tick tolera o blip de reprogramacao do switch chip)** — throughput e CPU do hEX em regime normal; define enable/disable vs add/remove
- **C7 — `/ip dhcp-server alert` emite DISCOVER 1x/min (doc), a OFFER do servidor real via relay chega ao MK e popula `unknown-server`; `alert-timeout=3m` esvazia a lista 3 min apos a ultima OFFER** — e a prova de vida do DHCP real exigida por R11
- **C8 — com `authoritative=no` o servidor RouterOS ACKa um REQUEST (REBIND/INIT-REBOOT) por endereco livre dentro do pool ou coberto por lease estatica, e fica em silencio (sem NAK) para os demais** — continuidade de IP sem NAK (R12) quando o real morre com leases de 8 h
- **C9 — a borda da PMM devolve ICMP para o IP estatico do MK do Complexo (V7 medido no CAPS AD) e o faz de forma estavel (sem rate-limit que derrube 3/3 sondas)** — sem isso a deteccao pela PMM depende de sonda com IP de lease (T7)
- **C10 — netwatch 7.23: `status` e `since` sao legiveis por `get`, `src-address` fixa a origem e `thr-loss-percent=99` com `packet-count=3` so da down com 3/3 perdidos** — semantica do N-de-M e do debounce
- **C11 — `/ping <host> src-address=10.3.74.1 count=1` (Motor A) gera ARP request com sender-IP 10.3.74.1 e MAC do MK, e Windows/Linux/lwIP atualizam a entrada existente ao receber ARP request dirigido** — convergencia ≤2 s do Motor A; sem isso vale a tabela NUD (15-55 s, minutos em impressoras)
- **C12 — bridge filter `arp-src-address=10.3.74.1` em forward combo1->ether2 esconde o roteador como 10.3.74.1 sem afetar seu ARP como 172.20.1.1 (gerencia), e ARP dirigido a CPU (input) nao e afetado** — cerca L2 seletiva do Motor A + P2
- **C13 — RouterOS aceita pacotes com src IP igual a um endereco local vindo da bridge? (Motor A: OFFER do relay com src 10.3.74.1 enquanto o MK possui 10.3.74.1) — presumido NAO (martian); por isso o Motor A nao usa dhcp-client de sonda em failover** — limita as sondas DHCP no Motor A
- **C14 — `/file add name=X contents=Y` e `/file set X contents=` persistem no CCR na raiz e no hEX em flash/, e `[:tonsec [/system resource get uptime]]` retorna nanossegundos no 7.23** — persistencia de hold-down/backoff e aritmetica monotonica do tick
- **C15 — `/ip dns forwarders` + static `type=FWD match-subdomain=yes` no 7.23 devolvem SERVFAIL (nao NXDOMAIN) quando o forwarder nao responde, e `cache flush` limpa negativos** — R14: nenhum NXDOMAIN cacheavel para pmm.local
- **C16 — hEX RB750Gr3 sustenta em failover, com bridging por CPU + NAT + MSS clamp, ≥150 Mbps agregados sem exceder 80% de CPU** — dimensiona a experiencia do usuario em failover nas unidades hEX
- **C17 — a PMTU de 1480 e propriedade da Connect do Complexo e nao de todas as Connect; MSS 1440 elimina o buraco negro medido em 02/09** — P6: sem isso 'site abre e cai' persiste em failover

## Config UNTAGGED (Complexo)
```
# =====================================================================================
# v5 — COMPLEXO REGULADOR (CCR1009, RouterOS 7.23.x) — LAN UNTAGGED 10.3.74.0/24
# Motor B (captura L2). Blocos numerados; tudo que alterna estado e criado DESABILITADO.
# PLACEHOLDERS (substituir antes de aplicar):
#   <GW_MAC>      = 94:3F:C2:DF:49:D3   (P7; reconfirmar com /ip arp print where address=10.3.74.1 apos reabilitar combo1)
#   <MK_MAC>      = 48:8F:5A:8E:45:E4   (MAC atual da bridge; fixado em admin-mac)
#   <SYSLOG_IP>   = coletor syslog alcancavel pela automais-vpn (ex.: 10.35.0.1)
#   <API_URL>     = https://api.automais.io/api/managed-devices/<id>/events   <API_TOKEN> = token do device
#   <MSS_CONN>    = 1440  (PMTU 1480 medido em 02/09 — P6; remedir por unidade)
# Fixos desta unidade: UP=combo1  LANP=ether2  CONN=ether3  LAN=10.3.74.0/24  GW=10.3.74.1  MK=10.3.74.254
#                      DC1=10.135.16.119 DC2=10.135.16.17  DHCP_REAL=10.1.201.254 (NAO e mais assumido)
# =====================================================================================

# --- 0. Pre-requisitos e higiene ------------------------------------------------------
/system backup save name=antes-v5-020926
/interface bridge set bridge-transparente auto-mac=no admin-mac=<MK_MAC> comment="L2 transparente PMM<->switch core (v5)"
/ip settings set send-redirects=no rp-filter=no
/ip dhcp-client set [find interface=ether3] use-peer-dns=no use-peer-ntp=no default-route-distance=2 comment="CONNECT (v5: sem DNS do ISP)"
/system ntp client set enabled=yes servers=a.st1.ntp.br,b.st1.ntp.br
/system clock set time-zone-name=America/Sao_Paulo
/system logging action set remote remote=<SYSLOG_IP> remote-port=514 src-address=10.35.0.23
/system logging add topics=script,warning action=remote comment="v5 telemetria"
/system logging add topics=error,critical action=remote
/system logging add topics=info action=disk comment="v5 log persistente"
/tool sniffer set filter-interface="" filter-mac-protocol="" file-name=""
/interface list add name=WAN comment="saidas de internet do MK"
/interface list member add list=WAN interface=ether3
/interface list member add list=WAN interface=ether7-STARLINK

# --- 1. Listas de enderecos -----------------------------------------------------------
/ip firewall address-list
add list=RFC1918 address=10.0.0.0/8 comment="v5: nunca NAT para a Connect (R18)"
add list=RFC1918 address=172.16.0.0/12
add list=RFC1918 address=192.168.0.0/16
add list=PMM-INTERNO address=10.0.0.0/8 comment="v5: sempre pelo roteador real"
add list=PMM-INTERNO address=172.16.0.0/12

# --- 2. Rotas: internos e sondas pela PMM (next-hop = gw real; o MK NUNCA possui 10.3.74.1 no Motor B) ---
/ip route
add dst-address=10.0.0.0/8 gateway=10.3.74.1 distance=1 comment="v5 internos PMM (resolver FWD, sondas, hairpin)"
add dst-address=172.16.0.0/12 gateway=10.3.74.1 distance=1 comment="v5 internos PMM"
add dst-address=1.1.1.1/32 gateway=10.3.74.1 pref-src=10.3.74.254 comment="FO-SONDA-PMM-1"
add dst-address=9.9.9.10/32 gateway=10.3.74.1 pref-src=10.3.74.254 comment="FO-SONDA-PMM-2"
add dst-address=208.67.222.222/32 gateway=10.3.74.1 pref-src=10.3.74.254 comment="FO-SONDA-PMM-3"
# (8.8.8.8 e 9.9.9.9 NAO sao alvos: sao forwarders do /ip dns — licao da skill)

# --- 3. Sondas (netwatch 7.x). status/since sao lidos pelo FO-tick; scripts up/down so logam ---
/tool netwatch
add name=FO-PMM-1 type=icmp host=1.1.1.1 src-address=10.3.74.254 interval=10s timeout=1s packet-count=3 packet-interval=200ms thr-loss-percent=99 startup-delay=2m comment="FO-PMM" down-script=":log warning \"FO sonda PMM-1 DOWN\"" up-script=":log info \"FO sonda PMM-1 UP\""
add name=FO-PMM-2 type=icmp host=9.9.9.10 src-address=10.3.74.254 interval=10s timeout=1s packet-count=3 packet-interval=200ms thr-loss-percent=99 startup-delay=2m comment="FO-PMM"
add name=FO-PMM-3 type=icmp host=208.67.222.222 src-address=10.3.74.254 interval=10s timeout=1s packet-count=3 packet-interval=200ms thr-loss-percent=99 startup-delay=2m comment="FO-PMM"
add name=FO-PMM-DC type=tcp-conn host=10.135.16.119 port=53 src-address=10.3.74.254 interval=15s timeout=2s startup-delay=2m comment="FO-PMMDC"
add name=FO-CONN-1 type=icmp host=1.0.0.1 interval=10s timeout=1s packet-count=3 thr-loss-percent=99 startup-delay=2m comment="FO-CONN"
add name=FO-CONN-2 type=icmp host=208.67.220.220 interval=10s timeout=1s packet-count=3 thr-loss-percent=99 startup-delay=2m comment="FO-CONN"
add name=FO-CONN-3 type=tcp-conn host=198.211.104.55 port=443 interval=15s timeout=2s startup-delay=2m comment="FO-CONN"

# --- 4. DNS: resolvedor permanente, restrito por firewall; zona interna sempre para os DCs ---
/ip dns set allow-remote-requests=yes servers=1.1.1.1,8.8.8.8 cache-max-ttl=1h max-udp-packet-size=1232
/ip dns forwarders add name=pmm-dc dns-servers=10.135.16.119,10.135.16.17 comment="v5 DCs pmm.local (rota pela PMM)"
/ip dns static add type=FWD name=pmm.local match-subdomain=yes forward-to=pmm-dc comment="v5 zona AD: nunca NXDOMAIN publico"
/ip dns static add type=FWD name=74.3.10.in-addr.arpa match-subdomain=yes forward-to=pmm-dc
/ip dns static add type=FWD name=16.135.10.in-addr.arpa match-subdomain=yes forward-to=pmm-dc

# --- 5. DHCP de retaguarda: DESLIGADO por padrao, sem NAK, sem takeover de 10.1.201.254 ---
/ip address remove [find comment="FAILOVER dhcp takeover"]
/ip pool set pool-failover-lan ranges=10.3.74.30-10.3.74.200 comment="v5: .10-.29 reservado a impressoras/fixos (hosts.md 2.A)"
/ip dhcp-server set FAILOVER-dhcp disabled=yes authoritative=no delay-threshold=0s conflict-detection=yes lease-time=10m comment="v5 retaguarda: so quando o alert nao ve o DHCP real por 3min"
/ip dhcp-server network set [find address=10.3.74.0/24] gateway=10.3.74.1 dns-server=10.135.16.119,10.135.16.17 domain=pmm.local
/ip dhcp-server alert add interface=bridge-transparente valid-server=<MK_MAC> alert-timeout=3m on-alert=":log info (\"FO-DHCP real vivo via \" . \$mac)" comment="v5 sonda ativa do DHCP real (DISCOVER 1x/min)"
# leases estaticas colhidas (harvest) — script FO-harvest (bloco 9) cria uma por host visto; exemplo do que ele gera:
# /ip dhcp-server lease add server=FAILOVER-dhcp address=10.3.74.251 mac-address=94:C6:91:C5:7C:27 comment="harvest 2026-09-03 (fora do pool)"

# --- 6. NAT/mangle PERMANENTES (so agem quando o MK roteia = failover) ---
/ip firewall nat remove [find comment~"^FAILOVER"]
/ip firewall nat
add chain=srcnat action=masquerade src-address=10.3.74.0/24 out-interface-list=WAN dst-address-list=!RFC1918 comment="FO-MASQ LAN->Connect (internos nunca; R18)"
add chain=dstnat action=redirect protocol=udp dst-port=53 src-address=10.3.74.0/24 in-interface=bridge-transparente to-ports=53 comment="FO-DNSREDIR-U"
add chain=dstnat action=redirect protocol=tcp dst-port=53 src-address=10.3.74.0/24 in-interface=bridge-transparente to-ports=53 comment="FO-DNSREDIR-T"
/ip firewall mangle
add chain=forward action=change-mss new-mss=<MSS_CONN> protocol=tcp tcp-flags=syn out-interface-list=WAN tcp-mss=1441-65535 comment="FO-MSS Connect out (PMTU 1480, P6)"
add chain=forward action=change-mss new-mss=<MSS_CONN> protocol=tcp tcp-flags=syn in-interface-list=WAN tcp-mss=1441-65535 comment="FO-MSS Connect in"
/ip firewall raw
add chain=prerouting action=notrack in-interface=bridge-transparente src-address=10.3.74.0/24 dst-address-list=PMM-INTERNO comment="v5 internos sem conntrack (retorno assimetrico se houver hairpin)"

# --- 7. Firewall default-drop (R20/R21/R22). APLICAR COM SAFETY-revert ARMADO e sessao pela automais-vpn ---
/system scheduler add name=SAFETY-revert interval=5m on-event="/ip firewall filter disable [find comment~\"v5 DROP\"]" comment="desarmar apos validar: /system scheduler remove SAFETY-revert"
/ip firewall filter remove [find dst-port=8291 protocol=tcp chain=input in-interface=""]
/ip firewall filter
add chain=input action=accept connection-state=established,related,untracked comment="v5 input estado" place-before=0
add chain=input action=drop connection-state=invalid comment="v5 input invalid"
add chain=input action=drop src-address-list=FO-BRUTE comment="v5 brute-force bloqueado"
add chain=input action=accept in-interface=ether3 protocol=udp dst-port=51820,13299,51823 comment="v5 WireGuard pela Connect (voip, automais, becape)"
add chain=input action=accept in-interface=ether3 protocol=udp src-port=67 dst-port=68 comment="v5 dhcp-client Connect"
add chain=input action=drop in-interface=ether3 comment="BLINDAGEM Connect"
add chain=input action=accept in-interface=bridge-transparente protocol=icmp comment="v5 icmp LAN"
add chain=input action=accept in-interface=bridge-transparente src-address=10.3.74.0/24 protocol=udp dst-port=53 comment="v5 DNS so da LAN (R16)"
add chain=input action=accept in-interface=bridge-transparente src-address=10.3.74.0/24 protocol=tcp dst-port=53
add chain=input action=accept in-interface=bridge-transparente src-address=10.200.0.0/24 protocol=udp dst-port=53 comment="v5 DNS telefones"
add chain=input action=accept in-interface=bridge-transparente protocol=udp dst-port=67 comment="v5 DHCP retaguarda"
add chain=input action=add-src-to-address-list address-list=FO-BRUTE address-list-timeout=1h connection-state=new protocol=tcp dst-port=22,8291 src-address-list=FO-BRUTE-2 comment="v5 3a tentativa -> bloqueio"
add chain=input action=add-src-to-address-list address-list=FO-BRUTE-2 address-list-timeout=1m connection-state=new protocol=tcp dst-port=22,8291 src-address-list=FO-BRUTE-1
add chain=input action=add-src-to-address-list address-list=FO-BRUTE-1 address-list-timeout=1m connection-state=new protocol=tcp dst-port=22,8291
add chain=input action=accept in-interface=automais-vpn protocol=tcp dst-port=22,8291,8728 comment="v5 gestao automais.io"
add chain=input action=accept in-interface=ether1-GESTAO protocol=tcp dst-port=22,8291 comment="v5 gestao OOB"
add chain=input action=accept in-interface=bridge-transparente src-address=10.3.74.0/24 protocol=tcp dst-port=8291 comment="v5 winbox local (dupla barreira: ACL do servico)"
add chain=input action=accept in-interface=wg-voip protocol=icmp comment="v5 diagnostico hub"
add chain=input action=drop comment="v5 DROP input default (R20)"
add chain=forward action=accept connection-state=established,related,untracked comment="v5 fwd estado"
add chain=forward action=drop connection-state=invalid comment="v5 fwd invalid"
add chain=forward action=accept in-interface=wg-voip dst-address=10.200.0.0/24 comment="VOIP: hub -> telefones id=0"
add chain=forward action=accept src-address=10.200.0.0/24 out-interface=wg-voip comment="VOIP: telefones -> hub id=0"
add chain=forward action=drop src-address=10.200.0.0/24 comment="VOIP: telefones fora do tunel"
add chain=forward action=reject reject-with=icmp-net-unreachable in-interface=bridge-transparente src-address=10.3.74.0/24 out-interface-list=WAN log=yes log-prefix="ANTI-BYPASS" comment="FO-ANTIBYPASS (ON em normal, OFF em failover; reconciliada pelo FO-tick)"
add chain=forward action=accept in-interface=bridge-transparente src-address=10.3.74.0/24 out-interface-list=WAN comment="FO-FWD LAN->Connect"
add chain=forward action=accept in-interface=bridge-transparente out-interface=bridge-transparente src-address=10.3.74.0/24 dst-address-list=PMM-INTERNO comment="FO-FWD hairpin internos (usado so se a captura for cega)"
add chain=forward action=drop in-interface-list=WAN comment="v5 DROP Connect->LAN (R21)"
add chain=forward action=drop comment="v5 DROP forward default (R20)"
/ip service set winbox address=173.20.20.0/24,10.35.0.0/24,10.3.74.0/24
/ip service set ssh address=173.20.20.0/24,10.35.0.0/24
/user group add name=leitura policy=read,winbox,ssh,web,api,!sensitive,!sniff,!reboot,!romon,!write,!policy,!test,!password,!ftp,!local,!telnet,!rest-api comment="v5 somente-leitura de verdade"
/user set prefeitura group=leitura
# /user remove admin  -> so depois de confirmar login com 'becape' (padrao da skill)

# --- 8. MOTOR B: bridge NAT (DESABILITADAS = normal). Ordem importa: DNS -> internos -> resto -> ARP ---
/interface bridge nat
add chain=dstnat in-interface=ether2 mac-protocol=ip src-address=10.3.74.0/24 dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF ip-protocol=udp dst-port=53 action=redirect disabled=yes comment="FO-CAPTURA-DNS-U"
add chain=dstnat in-interface=ether2 mac-protocol=ip src-address=10.3.74.0/24 dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF ip-protocol=tcp dst-port=53 action=redirect disabled=yes comment="FO-CAPTURA-DNS-T"
add chain=dstnat in-interface=ether2 mac-protocol=ip src-address=10.3.74.0/24 dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF dst-address=10.0.0.0/8 action=accept disabled=yes comment="FO-PONTE-INTERNO-A (segue ao roteador real)"
add chain=dstnat in-interface=ether2 mac-protocol=ip src-address=10.3.74.0/24 dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF dst-address=172.16.0.0/12 action=accept disabled=yes comment="FO-PONTE-INTERNO-B"
add chain=dstnat in-interface=ether2 mac-protocol=ip src-address=10.3.74.0/24 dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF action=redirect disabled=yes comment="FO-CAPTURA"
add chain=dstnat in-interface=ether2 mac-protocol=arp arp-opcode=request arp-dst-address=10.3.74.1/32 action=arp-reply to-arp-reply-mac-address=<GW_MAC> disabled=yes comment="FO-ARPREPLY (responde pelo gw com o MAC do gw)"

# --- 9. Scripts: FO-tick (motor), FO-harvest, FO-force, FO-rollback ---
# Gravar os corpos em arquivos e criar os scripts a partir deles (evita o inferno do \$):
#   /file add name=fo-tick.rsc contents="<corpo em failover_markdown §6>"
#   /system script add name=FO-tick policy=read,write,test,policy source=[/file get fo-tick.rsc contents] comment="v5 motor B"
/system scheduler remove [find name=failover-eth3]
/system script remove [find name=failover-eth3-check]
/file add name=fo-meta-win.txt contents="12"
/file add name=fo-meta-n.txt contents="0"
/file add name=fo-meta-t0.txt contents="0"
/file add name=fo-meta-last.txt contents="0"
/system scheduler add name=FO-tick interval=5s on-event="/system script run FO-tick" policy=read,write,test,policy comment="v5 failover tick" disabled=yes
/system scheduler add name=FO-harvest start-time=03:15:00 interval=1d on-event="/system script run FO-harvest" policy=read,write,test comment="v5 colhe IP<->MAC vivos -> leases estaticas"
/system scheduler add name=FO-drill start-time=07:30:00 interval=30d on-event="/system script run FO-drill" policy=read,write,test disabled=yes comment="v5 teste sintetico mensal (habilitar apos homologar)"

# FO-force: liga/desliga o failover a mao (mesmos objetos; o tick respeita por 30 min via arquivo fo-force.txt)
/system script add name=FO-force policy=read,write,test source="# uso: /system script run FO-force  (alterna); grava fo-force.txt=on|off|auto\r\n:local f \"auto\"; :do { :set f [/file get fo-force.txt contents] } on-error={ /file add name=fo-force.txt contents=\"auto\" }\r\n:if (\$f=\"on\") do={ /file set fo-force.txt contents=\"auto\"; :log warning \"FO-force -> auto\" } else={ /file set fo-force.txt contents=\"on\"; :log warning \"FO-force -> ON (Connect-primaria temporaria)\" }"

# FO-rollback: reproduz o estado manual de 02/09 em UM comando
/system script add name=FO-rollback policy=read,write,test,policy source="/system scheduler set FO-tick disabled=yes\r\n/interface bridge nat set [find comment~\"^FO-\"] disabled=yes\r\n/ip firewall filter set [find comment=\"FO-ANTIBYPASS\"] disabled=yes\r\n/ip address set [find comment=\"FAILOVER gw takeover\"] disabled=no\r\n/ip dhcp-server set FAILOVER-dhcp disabled=no authoritative=yes delay-threshold=0s\r\n/interface set combo1 disabled=yes\r\n:do { /ip firewall connection remove [find src-address~\"^10\\\\.3\\\\.74\\\\.\"] } on-error={}\r\n:log warning \"FO-ROLLBACK: estado manual (takeover + combo1 OFF)\""
# (o objeto 'FAILOVER gw takeover' e MANTIDO desabilitado no equipamento exatamente para o rollback e para o Motor A)

# --- 10. Motor A (FALLBACK — aplicar SOMENTE se o lab reprovar o redirect/arp-reply) ---
# /ip arp add address=10.3.74.253 mac-address=<GW_MAC> interface=bridge-transparente comment="FO alias I2 (IP livre, nunca possuido pelo MK)"
# /ip route set [find comment~"^FO-SONDA-PMM|^v5 internos"] gateway=10.3.74.253
# /interface bridge filter
# add chain=forward in-interface=combo1 out-interface=ether2 mac-protocol=arp arp-src-address=10.3.74.1/32 action=drop disabled=yes comment="FO-CERCA-1 esconde ARP do gw real"
# add chain=forward in-interface=ether2 out-interface=combo1 mac-protocol=arp arp-dst-address=10.3.74.1/32 action=drop disabled=yes comment="FO-CERCA-2"
# add chain=forward in-interface=combo1 mac-protocol=arp arp-src-address=10.3.74.1/32 src-mac-address=!<GW_MAC>/FF:FF:FF:FF:FF:FF action=log log-prefix="GW-MAC-MUDOU" comment="FO-WATCH MAC do gw"
# /ip firewall nat add chain=srcnat action=src-nat to-addresses=10.3.74.254 src-address=10.3.74.0/24 dst-address-list=PMM-INTERNO out-interface=bridge-transparente comment="FO-A internos como .254 (roteador so resolve .254)"
# no FO-tick: ancora passa a ser 'FAILOVER gw takeover'; objetos alternados = takeover + FO-CERCA-* + FO-ANTIBYPASS; apos ligar, rodar a varredura ARP:
#   /ip arp remove [find interface=bridge-transparente dynamic]; :for i from=2 to=253 do={ :do { /ping ("10.3.74." . $i) src-address=10.3.74.1 count=1 interval=100ms } on-error={} }  (repetir em 0s, 10s, 30s)
```

## Config TAGGED (hEX)
```
# =====================================================================================
# v5 — hEX RB750Gr3 — LAN TAGGED (exemplo CDT id=6: VID 1092, 10.1.92.0/24, gw 10.1.92.1) + gerencia PMM UNTAGGED em transito
# Portas fixas (P4): ether1 PMM | ether2 Connect | ether3 OOB | ether4 switch core | ether5 Wi-Fi (fora da bridge)
# PLACEHOLDERS: <VID>=1092 <LAN>=10.1.92.0/24 <GW>=10.1.92.1 <MK>=10.1.92.254 <GW_MAC>=medir (/ip arp print where address=<GW> interface=vlan<VID>-lan)
#               <MK_MAC>=MAC da bridge (fixar) <LAN3>=10.1.92 <MSS_CONN>=medir (1440 se PMTU 1480) <SYSLOG_IP> <API_URL> <API_TOKEN>
# Premissa P2: NENHUMA regra toca frame untagged (todas exigem vlan-id=<VID>) -> SNMP 10.135.16.x -> 172.20.1.x passa sempre.
# =====================================================================================

# --- 0. Higiene (igual ao untagged, portas trocadas) ---
/system backup save name=antes-v5
/interface bridge set bridge-transparente auto-mac=no admin-mac=<MK_MAC>
/ip settings set send-redirects=no rp-filter=no
/ip dhcp-client set [find interface=ether2] use-peer-dns=no use-peer-ntp=no default-route-distance=2
/system ntp client set enabled=yes servers=a.st1.ntp.br,b.st1.ntp.br
/system logging action set remote remote=<SYSLOG_IP> src-address=<IP automais-vpn>
/system logging add topics=script,warning action=remote
/system logging add topics=error,critical action=remote
/interface list add name=WAN
/interface list member add list=WAN interface=ether2
/ip firewall address-list
add list=RFC1918 address=10.0.0.0/8
add list=RFC1918 address=172.16.0.0/12
add list=RFC1918 address=192.168.0.0/16
add list=PMM-INTERNO address=10.0.0.0/8
add list=PMM-INTERNO address=172.16.0.0/12

# --- 1. Rotas e sondas pela PMM (via vlan<VID>-lan; o MK NAO possui <GW>) ---
/ip route
add dst-address=10.0.0.0/8 gateway=<GW> comment="v5 internos PMM"
add dst-address=172.16.0.0/12 gateway=<GW> comment="v5 internos PMM"
add dst-address=1.1.1.1/32 gateway=<GW> pref-src=<MK> comment="FO-SONDA-PMM-1"
add dst-address=9.9.9.10/32 gateway=<GW> pref-src=<MK> comment="FO-SONDA-PMM-2"
add dst-address=208.67.222.222/32 gateway=<GW> pref-src=<MK> comment="FO-SONDA-PMM-3"
/tool netwatch
add name=FO-PMM-1 type=icmp host=1.1.1.1 src-address=<MK> interval=10s timeout=1s packet-count=3 packet-interval=200ms thr-loss-percent=99 startup-delay=2m comment="FO-PMM"
add name=FO-PMM-2 type=icmp host=9.9.9.10 src-address=<MK> interval=10s timeout=1s packet-count=3 packet-interval=200ms thr-loss-percent=99 startup-delay=2m comment="FO-PMM"
add name=FO-PMM-3 type=icmp host=208.67.222.222 src-address=<MK> interval=10s timeout=1s packet-count=3 packet-interval=200ms thr-loss-percent=99 startup-delay=2m comment="FO-PMM"
add name=FO-PMM-DC type=tcp-conn host=10.135.16.119 port=53 src-address=<MK> interval=15s timeout=2s startup-delay=2m comment="FO-PMMDC"
add name=FO-CONN-1 type=icmp host=1.0.0.1 interval=10s timeout=1s packet-count=3 thr-loss-percent=99 startup-delay=2m comment="FO-CONN"
add name=FO-CONN-2 type=icmp host=208.67.220.220 interval=10s timeout=1s packet-count=3 thr-loss-percent=99 startup-delay=2m comment="FO-CONN"
add name=FO-CONN-3 type=tcp-conn host=198.211.104.55 port=443 interval=15s timeout=2s startup-delay=2m comment="FO-CONN"

# --- 2. DNS, DHCP (na interface VLAN) ---
/ip dns set allow-remote-requests=yes servers=1.1.1.1,8.8.8.8 cache-max-ttl=1h
/ip dns forwarders add name=pmm-dc dns-servers=10.135.16.119,10.135.16.17
/ip dns static add type=FWD name=pmm.local match-subdomain=yes forward-to=pmm-dc
/ip dns static add type=FWD name=92.1.10.in-addr.arpa match-subdomain=yes forward-to=pmm-dc
/ip dns static add type=FWD name=16.135.10.in-addr.arpa match-subdomain=yes forward-to=pmm-dc
/ip address remove [find comment="FAILOVER dhcp takeover"]
/ip pool set pool-failover-lan ranges=<LAN3>.30-<LAN3>.200
/ip dhcp-server set FAILOVER-dhcp interface=vlan<VID>-lan disabled=yes authoritative=no delay-threshold=0s conflict-detection=yes lease-time=10m
/ip dhcp-server alert add interface=vlan<VID>-lan valid-server=<MK_MAC> alert-timeout=3m on-alert=":log info (\"FO-DHCP real vivo via \" . \$mac)" comment="v5 sonda do DHCP real"

# --- 3. NAT/mangle/raw permanentes ---
/ip firewall nat remove [find comment~"^FAILOVER"]
/ip firewall nat
add chain=srcnat action=masquerade src-address=<LAN> out-interface-list=WAN dst-address-list=!RFC1918 comment="FO-MASQ"
add chain=dstnat action=redirect protocol=udp dst-port=53 src-address=<LAN> in-interface=vlan<VID>-lan to-ports=53 comment="FO-DNSREDIR-U"
add chain=dstnat action=redirect protocol=tcp dst-port=53 src-address=<LAN> in-interface=vlan<VID>-lan to-ports=53 comment="FO-DNSREDIR-T"
/ip firewall mangle
add chain=forward action=change-mss new-mss=<MSS_CONN> protocol=tcp tcp-flags=syn out-interface-list=WAN tcp-mss=1441-65535 comment="FO-MSS out"
add chain=forward action=change-mss new-mss=<MSS_CONN> protocol=tcp tcp-flags=syn in-interface-list=WAN tcp-mss=1441-65535 comment="FO-MSS in"
/ip firewall raw add chain=prerouting action=notrack in-interface=vlan<VID>-lan src-address=<LAN> dst-address-list=PMM-INTERNO comment="v5 internos sem conntrack"

# --- 4. Firewall default-drop: mesmo bloco 7 do untagged trocando bridge-transparente -> vlan<VID>-lan e ether3 -> ether2,
#        portas WG = 51820,<PORTA_AUTOMAIS 133xx>; anti-bypass:
/ip firewall filter add chain=forward action=reject reject-with=icmp-net-unreachable in-interface=vlan<VID>-lan src-address=<LAN> out-interface-list=WAN log=yes log-prefix="ANTI-BYPASS" comment="FO-ANTIBYPASS"
/ip firewall filter add chain=forward action=accept in-interface=vlan<VID>-lan src-address=<LAN> out-interface-list=WAN comment="FO-FWD LAN->Connect"
/ip firewall filter add chain=forward action=accept in-interface=vlan<VID>-lan out-interface=vlan<VID>-lan src-address=<LAN> dst-address-list=PMM-INTERNO comment="FO-FWD hairpin internos"

# --- 5. MOTOR B TAGGED — variante T1 (requer CLAIM C4: matchers IP/ARP internos validos sob vlan-encap) ---
/interface bridge nat
add chain=dstnat in-interface=ether4 mac-protocol=vlan vlan-id=<VID> vlan-encap=ip src-address=<LAN> dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF ip-protocol=udp dst-port=53 action=redirect disabled=yes comment="FO-CAPTURA-DNS-U"
add chain=dstnat in-interface=ether4 mac-protocol=vlan vlan-id=<VID> vlan-encap=ip src-address=<LAN> dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF ip-protocol=tcp dst-port=53 action=redirect disabled=yes comment="FO-CAPTURA-DNS-T"
add chain=dstnat in-interface=ether4 mac-protocol=vlan vlan-id=<VID> vlan-encap=ip src-address=<LAN> dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF dst-address=10.0.0.0/8 action=accept disabled=yes comment="FO-PONTE-INTERNO-A"
add chain=dstnat in-interface=ether4 mac-protocol=vlan vlan-id=<VID> vlan-encap=ip src-address=<LAN> dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF dst-address=172.16.0.0/12 action=accept disabled=yes comment="FO-PONTE-INTERNO-B"
add chain=dstnat in-interface=ether4 mac-protocol=vlan vlan-id=<VID> vlan-encap=ip src-address=<LAN> dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF action=redirect disabled=yes comment="FO-CAPTURA"
add chain=dstnat in-interface=ether4 mac-protocol=vlan vlan-id=<VID> vlan-encap=arp arp-opcode=request arp-dst-address=<GW>/32 action=arp-reply to-arp-reply-mac-address=<GW_MAC> disabled=yes comment="FO-ARPREPLY"
# O redirect entrega o frame TAGGED a CPU -> a vlan<VID>-lan o recebe (mesmo caminho dos frames ao MAC do MK hoje). A resposta arp-reply precisa sair TAGGED (CLAIM C5).

# --- 5b. MOTOR B TAGGED — variante T2 "captura cega" (se C4 falhar): sem matchers internos; TUDO ao MAC do gw na VLAN vai a CPU ---
# /interface bridge nat add chain=dstnat in-interface=ether4 mac-protocol=vlan vlan-id=<VID> dst-mac-address=<GW_MAC>/FF:FF:FF:FF:FF:FF action=redirect disabled=yes comment="FO-CAPTURA"
#   -> internos viram hairpin CPU (rota 10/8 via <GW>, raw notrack, FO-FWD hairpin), DNS cai no FO-DNSREDIR, resto NAT pela Connect.
#   -> ARP: sem arp-dst-address confiavel NAO usar arp-reply (responderia por qualquer IP). Cobertura "roteador morto" = Motor A-tagged:
# /ip address add address=<GW>/32 interface=vlan<VID>-lan comment="FAILOVER gw takeover" disabled=yes   (ja existe nos .rsc de site)
# /interface bridge filter add chain=forward in-interface=ether1 out-interface=ether4 mac-protocol=vlan vlan-id=<VID> vlan-encap=arp action=drop disabled=yes comment="FO-CERCA-T (ARP da VLAN de clientes em bloco; gerencia untagged intacta)"
# /interface bridge filter add chain=forward in-interface=ether4 out-interface=ether1 mac-protocol=vlan vlan-id=<VID> vlan-encap=arp action=drop disabled=yes comment="FO-CERCA-T"
# /ip arp add address=<LAN3>.253 mac-address=<GW_MAC> interface=vlan<VID>-lan comment="FO alias I2"  + rotas de sonda/internos via <LAN3>.253
# /ip firewall nat add chain=srcnat action=src-nat to-addresses=<MK> src-address=<LAN> dst-address-list=PMM-INTERNO out-interface=vlan<VID>-lan comment="FO-A internos como .254"
#   + varredura ARP dirigida na entrada (/ping <LAN3>.$i src-address=<GW> count=1 interface=vlan<VID>-lan)

# --- 6. Scripts/scheduler: identicos ao untagged (FO-tick, FO-harvest, FO-force, FO-rollback), com UP=ether1, LANP=ether4, IFLAN=vlan<VID>-lan, CONN=ether2
# hw-offload: no hEX as regras de bridge nat/filter ativas tiram o H de ether1/ether4 (CPU) SO durante o failover. Se o lab mostrar que regra
# DESABILITADA tambem tira o H (CLAIM C6), o FO-tick passa a add/remove as regras em vez de enable/disable.
```
