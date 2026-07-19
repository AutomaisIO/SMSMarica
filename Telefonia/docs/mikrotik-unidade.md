# Plugar uma unidade nova (MikroTik RouterOS v7)

> **Padrão completo do projeto:** a skill `configurar-mikrotik-unidade`
> (`.claude/skills/configurar-mikrotik-unidade/SKILL.md`) cobre o fluxo ponta-a-ponta —
> perguntas iniciais (unidade + portas Prefeitura/unidade/Connect/gestão), bridge transparente,
> VPN VOIP, **failover pela Connect** e **blindagem de segurança**. Este arquivo é o passo-a-passo
> só da parte VOIP; a skill é a fonte de verdade do padrão. Mudou o padrão → atualiza os dois.

> Pré-requisitos: hub já instalado ([`servidor-wireguard-hub.md`](servidor-wireguard-hub.md)),
> **public-key do hub** em mãos, e o `id` da unidade em [`../registro/unidades.csv`](../registro/unidades.csv).
> O MikroTik precisa de **RouterOS v7+** (WireGuard).

## Cenário (como os telefones ficam)

- Os aparelhos usam **IP fixo** (ou DHCP) na faixa `10.200.<id>.10–.200`.
- O **gateway** dos aparelhos é o MikroTik em `10.200.<id>.1`.
- O MK empurra o tráfego dos telefones **para o túnel** (VOIP-only) até o hub `10.201.0.1`,
  que é o **SIP server** (Asterisk). Sem NAT → o hub vê o IP real e sabe a unidade.

## Passo a passo

### 1. Gere o script da unidade
No seu PC (bash), com a public-key do hub:

```bash
SERVER_PUBKEY="<public-key-do-hub>" \
  scripts/mikrotik-gen.sh <id> [nome-da-interface-dos-telefones]
```

- `<id>` → o da tabela (ex.: `6` = CDT).
- A interface dos telefones default é `bridge-telefones`. **Ajuste** pro nome real no MK
  (a bridge/VLAN onde os aparelhos estão pendurados).

### 2. Cole no MikroTik
Cole a saída no terminal do RouterOS (WinBox → New Terminal, ou SSH). Ele:
1. cria a interface `wg-voip` (o **MK gera a chave privada** sozinho);
2. dá o IP do túnel `10.201.0.<id+10>/24`;
3. cadastra o **peer = hub**;
4. põe o gateway `10.200.<id>.1` na interface dos telefones;
5. (opcional) sobe DHCP `.10–.200`;
6. cria as regras de firewall **VOIP-only**;
7. imprime a **public-key do MK**.

### 3. Registre o peer no hub
Copie a public-key do MK (passo 8 do script) e, no servidor:

```bash
sudo /opt/telefonia/hub-add-unidade.sh --id <id> --pubkey "<PUBKEY_DO_MK>" --nome "<nome>"
```

### 4. Valide
- No **MK**: `/interface/wireguard/peers/print` → deve haver `last-handshake` recente.
- No **hub**: `wg show wg0 latest-handshakes` → linha do peer com handshake < 2 min.
- Ponha um telefone com IP `10.200.<id>.10`, gw `10.200.<id>.1`, **SIP server `10.201.0.1`**,
  e veja registrar: `asterisk -rx "pjsip show contacts"` (ou `sip show peers` no chan_sip).

### 5. Atualize o registro
Edite [`../registro/unidades.csv`](../registro/unidades.csv): preencha `mk_pubkey`,
`mk_lan_atual` (IP do MK dentro da rede local da unidade) e `status = ATIVA`.

## Armadilhas comuns

| Sintoma | Causa provável | Correção |
|---------|----------------|----------|
| Handshake não sobe | firewall do provedor bloqueia UDP 51820 de saída, ou endpoint errado | testar `/ping 192.241.153.121`; conferir `endpoint-port=51820` |
| Handshake OK, telefone não registra | telefone sem SIP server = `10.201.0.1`, ou gw errado | ajustar provisionamento do aparelho |
| Hub vê IP `10.200.<id>.1` no lugar do aparelho | há **masquerade** cobrindo `wg-voip` no MK | remover srcnat/masquerade em `out-interface=wg-voip` (passo 7) |
| Áudio mudo (registra mas sem voz) | RTP não atravessa; media direta entre unidades (isoladas) | no Asterisk usar `directmedia=no` (mídia via PBX) |
| Colisão de IP | interface dos telefones e `wg-voip` na mesma faixa | são faixas distintas (`10.200.<id>` × `10.201.0`); revisar digitação |

## Interface dos telefones: bridge dedicada (recomendado)

Se os aparelhos ainda não têm uma bridge própria, crie uma e prenda as portas físicas
onde eles ligam (ex.: `ether3`), pra separar do resto da LAN da unidade:

```
/interface/bridge/add name=bridge-telefones comment="telefones VOIP"
/interface/bridge/port/add bridge=bridge-telefones interface=ether3
```
Depois rode o `mikrotik-gen.sh <id> bridge-telefones`.
