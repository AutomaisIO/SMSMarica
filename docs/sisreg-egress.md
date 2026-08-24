# SISREG — egress de rede em produção (túnel WireGuard)

> Como o backend em produção (`api.smsmarica.online`, DigitalOcean) alcança o
> **SISREG III** (`sisregiii.saude.gov.br`) para a consulta de paciente por CNS
> (CADSUS / `cadweb50`). Ver também o motor da integração em
> `SMSMais.Core/Integracoes/SisregWeb/` e o laboratório `Automais.SISREG/`.

## Problema

O SISREG/DATASUS **recusa conexões vindas do IP da DigitalOcean** (datacenter no
exterior). Sintoma: a chamada `GET /integracoes/cns` retornava
`500 — Connection refused (sisregiii.saude.gov.br:443)`.

- `sisregiii.saude.gov.br` resolve para um **único IP**: **`189.28.130.13`** (DATASUS, Brasil).
- Do **Brasil** o acesso é normal (HTTP 200 em ~0,3–0,8s); do IP da VM (`146.190.65.73`,
  DigitalOcean) a conexão TCP 443 é recusada.

## Solução: túnel WireGuard **dedicado** VM → MikroTik (Brasil)

Um túnel WireGuard **exclusivo para o SISREG**, separado da VPN de produção do
Automais.IO (que **não** é tocada). Apenas o tráfego destinado a `189.28.130.13`
é roteado pelo túnel; sai pela internet **brasileira** do MikroTik e chega ao
SISREG com um IP aceito. Todo o resto do tráfego da VM continua saindo normalmente.

### Topologia

```
[VM smsmarica.online]              WireGuard dedicado                [MikroTik AUTOMAIS-ROUTER]
 146.190.65.73 (público)   ── wg-mk ⇄ wg-smsreg (172.31.99.0/30) ──   10.30.50.8
 wg-mk = 172.31.99.1  ◄──────── MK disca a VM (keepalive) ────────  wg-smsreg = 172.31.99.2
        │                                                                    │
        │ rota: 189.28.130.13/32 dev wg-mk                                   │ WAN "MUNDIVOX"
        ▼                                                          201.76.184.254 (Brasil)
   só o SISREG entra no túnel                                               │
                                                                            ▼
                                                                 SISREG 189.28.130.13 (HTTP 200)
```

- **Direção:** o MikroTik **disca** para a VM (a VM é o *listener* na porta UDP `51830`).
  Escolhido assim porque a VM não tem firewall de host (`ufw` inativo) e o MK originar a
  conexão evita mexer no firewall de entrada do roteador.
- **NAT:** no MK, o tráfego do túnel saindo pela `MUNDIVOX` é mascarado pela regra
  `srcnat` genérica já existente (`action=masquerade out-interface=MUNDIVOX`). **Nenhuma
  regra de firewall/NAT nova foi criada.** A chain `forward` do MK aceita por padrão.

### Endereçamento

| Elemento | Valor |
|---|---|
| Subnet do túnel | `172.31.99.0/30` |
| VM (interface `wg-mk`) | `172.31.99.1/30`, listen UDP `51830` |
| MK (interface `wg-smsreg`) | `172.31.99.2/30` |
| Endpoint que o MK disca | `146.190.65.73:51830` (IP público da VM) |
| WAN de saída no MK | `MUNDIVOX` → `201.76.184.254` (Brasil) |
| Destino roteado pelo túnel | **`189.28.130.13/32`** (SISREG) |
| Chave pública da VM | `c7ARf4PLxtiKrri8iXte9ixJ2dbRbToYfrJ/HRFIMn4=` |
| Chave pública do MK | `8nWuHEFQe4W/qXdBagtRsI0sOyGxuS9j0zKoIhM5vj8=` |

> Chaves **privadas** ficam só nos respectivos equipamentos e **não** são versionadas.

## Configuração aplicada

### VM `smsmarica.online` (Ubuntu, `wg-quick`)

`/etc/wireguard/wg-mk.conf`:

```ini
[Interface]
Address = 172.31.99.1/30
ListenPort = 51830
PrivateKey = <privada da VM — não versionar>

[Peer]
PublicKey = 8nWuHEFQe4W/qXdBagtRsI0sOyGxuS9j0zKoIhM5vj8=
AllowedIPs = 172.31.99.2/32, 189.28.130.13/32
```

Subir e habilitar no boot:

```bash
wg-quick up wg-mk
systemctl enable wg-quick@wg-mk
```

O `AllowedIPs = …, 189.28.130.13/32` faz o `wg-quick` criar automaticamente a rota
`189.28.130.13/32 dev wg-mk src 172.31.99.1` — **só o SISREG** entra no túnel.

### MikroTik `AUTOMAIS-ROUTER` (`10.30.50.8`, RouterOS 7.x)

```routeros
/interface wireguard add name=wg-smsreg listen-port=51830 \
    comment="SISREG egress p/ smsmarica.online - dedicado"
/ip address add address=172.31.99.2/30 interface=wg-smsreg \
    comment="tunel SISREG smsmarica"
/interface wireguard peers add interface=wg-smsreg \
    public-key="c7ARf4PLxtiKrri8iXte9ixJ2dbRbToYfrJ/HRFIMn4=" \
    endpoint-address=146.190.65.73 endpoint-port=51830 \
    allowed-address=172.31.99.1/32 persistent-keepalive=25s \
    comment="smsmarica.online VM - SISREG"
```

Nenhuma alteração de firewall/NAT foi necessária (masquerade genérico já cobre; `forward` aceita).

## Verificação

Na VM:

```bash
wg show wg-mk        # deve mostrar "latest handshake" recente e transfer > 0
curl -sS -o /dev/null -w "%{http_code} %{remote_ip}\n" https://sisregiii.saude.gov.br/
# esperado: 200 189.28.130.13
```

Resultado do go-live (2026-07-01): handshake OK, 3 chamadas seguidas `HTTP 200` em ~0,6s,
saída sempre por `189.28.130.13`.

## Operação

- **Persistência:** VM habilitada no boot (`wg-quick@wg-mk`); config do MK é permanente
  (RouterOS grava automaticamente). Sobrevive a reboot dos dois lados.
- **Se o SISREG mudar de IP:** hoje é IP único (`189.28.130.13`). Se mudar, atualizar o
  `/32` nos **dois** lados — `AllowedIPs` da VM (`wg-mk.conf`) e, se necessário, a rota.
  Se passar a ter vários IPs, rotear o bloco (ex.: `189.28.130.0/24`) em vez de `/32`.
- **Reverter (sem efeito colateral):** remover `wg-smsreg` + peer no MK e `wg-mk` na VM
  (`wg-quick down wg-mk && systemctl disable wg-quick@wg-mk && rm /etc/wireguard/wg-mk.conf`).
- **Acesso administrativo:** SSH da VM e API/SSH do MikroTik (RouterOS). Credenciais **não**
  ficam neste repositório. O MK é gerenciado pela plataforma Automais.IO.

## Relação com a aplicação

O motor `SisregWebSessao` (backend) fala com `https://sisregiii.saude.gov.br` normalmente
— **não sabe** do túnel; o roteamento é transparente no kernel da VM. Logo, nenhuma
configuração de proxy é necessária no código. A credencial (usuário/senha do operador) e o
liga/desliga da reautenticação automática ficam na tela **Integrações → SISREG**.
