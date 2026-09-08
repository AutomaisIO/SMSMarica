# Incidente — Péricles (id 1) inacessível em 02/09/2026

> **Status: ENCERRADO em 03/09/2026 ~07:57.** MK voltou sozinho, sem reboot e sem visita para "desfazer" nada.
> **Internet da unidade nunca foi afetada** (o MK é ponte transparente; os clientes seguem pela Prefeitura).
> ⚠️ **A causa registrada na 1ª versão deste documento estava ERRADA** — ver §"Correção da causa".

## Linha do tempo

| Quando | O quê |
|---|---|
| 02/09 ~16:35 | Sessão SSH trava durante a onda 1; MK some da gestão e o VOIP cai |
| 02/09 ~16:40-18:00 | 3 tentativas de recuperação remota falham |
| 03/09 07:26-07:57 | Log mostra **384 quedas de link na `ether2` (Connect)**; técnico no local (`becape` via Winbox de `10.1.19.156`) move o cabo da Connect para a `ether5`, depois devolve para a `ether2` |
| 03/09 ~07:57 | Link estabiliza; `automais-vpn` e `wg-voip` reconectam sozinhos |

## Correção da causa

A primeira versão deste documento culpou o comando `/interface ethernet set ether2 mtu=1480`. **Isso foi refutado por três evidências:**

1. **O MK nunca reiniciou** — `uptime` de `1d04:41` em 03/09 08:00 cobre todo o período do incidente. Nada foi "desfeito".
2. **`mtu=1480` continua aplicado** e os dois túneis estão de pé **por essa mesma porta**, agora mesmo (ping ao hub VOIP: 0% de perda, 115 ms).
3. **Toda a onda 1 foi executada** — `mtu` 1480, 2 regras de MSS clamp, `use-peer-dns=no`, NTP+timezone, 3 endereços em `SERVICOS-ESSENCIAIS`, backup `onda1`. O que se perdeu foi só a **resposta** do SSH, não os comandos.

**Causa real: a `ether2` (link Connect) estava fisicamente instável.** 384 quedas de link no que sobrou do buffer de log — e o buffer só alcança 31 minutos, porque os próprios flaps o encheram. Como **os dois túneis do Péricles saem pela Connect**, o link caindo derruba gestão e VOIP juntos, sem tocar nos clientes.

`rx-fcs-error = 0` e `tx-collision = 0`: o cabo até o MK está eletricamente limpo. Link caindo inteiro com FCS zerado aponta para **o CPE/ONU da Connect reiniciando ou contato intermitente no conector**, não para cabo ruim no lance.

## O que continua verdadeiro (e vale como regra)

A lição de método **permanece**, mesmo com a causa sendo outra: mexer na interface por onde passa a própria gestão exige reversão armada antes. Não foi armada aqui, e por isso o incidente ficou indistinguível de um autobloqueio por 15 horas — não dava para saber, de fora, se a culpa era do comando ou do link.

```
/system scheduler add name=SAFETY-X interval=3m policy=read,write,test,policy \
    on-event="<comandos que desfazem>; /system scheduler remove [find name=\"SAFETY-X\"]"
... aplica a mudança ...
... ABRE UMA CONEXÃO NOVA (o passo crítico) ...
/system scheduler remove [find name="SAFETY-X"]
```

E o corolário novo: **um MK que some não é prova de que o último comando o derrubou.** Antes de assumir culpa do comando, conferir `uptime` e o log de link — os dois responderam a pergunta aqui em 30 segundos.

## Estado do Péricles em 03/09 08:00

| Item | Estado |
|---|---|
| Onda 1 (PMTU/MSS, DNS, NTP, essenciais) | ✅ completa |
| Túnel DC (`wg-eveo`) | ✅ `10.203.0.11`, aplicado 03/09 |
| Failover | ✅ **v5** aplicado e validado em produção 03/09 (ver `unidades-config/id01-pericles-v5.rsc`) |
| Link Connect | 🟡 estável agora, **mas com histórico de 384 flaps hoje** |

## Pendência aberta

**A `ether2` precisa de atenção física** — trocar o patch cord e conferir o CPE da Connect. Enquanto isso não for feito, o Péricles perde gestão e VOIP a cada flap, e o failover v5 (quando aplicado) vai depender justamente desse link para a contingência.
