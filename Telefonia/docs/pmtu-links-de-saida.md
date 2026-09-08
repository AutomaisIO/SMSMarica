# PMTU e MSS por link de saída — regra do padrão

> **Regra de ouro: "o link está instável" é hipótese PROIBIDA enquanto o teste de DF não tiver sido feito.**

Descoberto no Complexo Regulador em 02/09/2026, investigando uma reclamação antiga ("abre um site e pouco depois cai, de forma aleatória") que se atribuía à qualidade da Connect.

## O que acontece

O caminho da Connect tem **PMTU de 1480 bytes** (20 bytes de encapsulamento no provedor), mas:
- o ping normal e o download pequeno passam → o link **parece perfeito** (0 % de perda, 3-10 ms);
- pacotes de 1484 bytes ou mais **somem em silêncio**;
- o ICMP *fragmentation needed* **não volta** → o cliente nunca aprende o limite.

Resultado: o TCP abre a conexão (pacotes pequenos), a página começa a carregar e **morre quando chega o primeiro segmento cheio**. Para o usuário, é "instabilidade aleatória".

## Como medir (obrigatório em todo link que não seja o da Prefeitura)

```
# 1) busca binária — no RouterOS, size= é o tamanho TOTAL do pacote IP
/ping 8.8.8.8 size=1500 do-not-fragment count=2 interface=<link>   # falha
/ping 8.8.8.8 size=1480 do-not-fragment count=2 interface=<link>   # passa  -> PMTU = 1480
/ping 8.8.8.8 size=1484 do-not-fragment count=2 interface=<link>   # confirma que N+4 falha

# 2) controle: o CPE do próprio link deve aceitar 1500
/ping <gw do link> size=1500 do-not-fragment count=2
#    passa => o estrangulamento está ADIANTE do CPE (é do provedor)
```

⚠️ `size=` no RouterOS **não é payload** (diferente do `ping -s` do Linux). Prova: `size=1500` para o CPE passa; se fosse payload seriam 1528 bytes e não caberiam na Ethernet.

## Como corrigir (as duas coisas, não uma só)

```
/interface ethernet set <link> mtu=<PMTU>
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn out-interface=<link> \
    tcp-mss=<PMTU-39>-65535 action=change-mss new-mss=<PMTU-40> comment="MSS clamp <link> (PMTU <valor> medido <data>)"
/ip firewall mangle add chain=forward protocol=tcp tcp-flags=syn in-interface=<link> \
    tcp-mss=<PMTU-39>-65535 action=change-mss new-mss=<PMTU-40> comment="MSS clamp <link> in"
```

Dois mecanismos porque o ICMP do provedor não é confiável: o `mtu=` faz o próprio MK devolver *frag needed* aos clientes (cobre TCP e UDP), e o clamp resolve TCP sem depender de PMTUD nenhum.

## Túneis WireGuard sobre o link

Regra: **`MTU_WG + 60 ≤ PMTU`** (60 = overhead WireGuard IPv4: 20 IP + 8 UDP + 32 do cabeçalho de dados).

Com PMTU 1480, o padrão 1420 passa **exatamente no limite — margem zero**. Se o PMTU do provedor variar um byte, o túnel inteiro cai. Onde o PMTU for < 1480, usar `MTU_WG = PMTU − 60` e clamp interno de `MTU_WG − 40`.

## Vigilância

Scheduler (6 h) com dois pings DF: `size=<PMTU>` deve passar e `size=<PMTU+4>` deve falhar. Qualquer inversão → `:log warning` + alerta: o provedor mudou o encapsulamento, re-medir.

## Registro por unidade

Colunas em `registro/unidades.csv`: `pmtu_connect`, `pmtu_connect_medido_em` (+ `pmtu_starlink` onde houver). Sem valor = unidade fora do padrão.

| Unidade | Link | PMTU | Medido em |
|---|---|---|---|
| id=0 Complexo Regulador | Connect | **1480** | 02/09/2026 |
| demais | — | *pendente* | — |

## Sintomas que denunciam PMTU (e não "link ruim")

- ping pequeno com 0 % de perda e latência boa;
- contadores físicos da porta sem erro algum;
- páginas que **começam** a carregar e travam; downloads que param sempre no mesmo ponto;
- sessões longas (SSH, RDP) que caem ao transferir volume;
- **funciona em um site e não em outro**, sem padrão aparente.
