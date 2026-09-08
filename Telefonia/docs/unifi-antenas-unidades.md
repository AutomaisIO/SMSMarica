# Antenas UniFi nas unidades de Maricá

> **Resolvido em 02/09/2026.** As duas antenas estão online no controlador novo, pelo nome DNS, **sem nenhum resquício do IP antigo**.

## 1. Estado final

| Unidade | AP | Porta | Estado |
|---|---|---|---|
| **CMI (id 10)** | `10.1.18.125` | `ether5` | 🟢 CONECTADA · `inform=http://unifi.automais.cloud:8080/inform` |
| **CDT (id 6)** | `10.1.92.98` | `ether4`, VLAN 1092 | 🟢 CONECTADA · idem |

Controlador: VM 108 `wifi01` no datacenter Eveo — `10.90.40.23` (= `unifi.automais.cloud`), também alcançável em `10.35.0.55` pela VPN do automais.io. Site UniFi: **SMSMARICA** (`m6b8mpvx`).

## 2. O que estava errado (três camadas)

1. **O controlador se identificava pelo IP antigo.** `super_identity.hostname = 10.30.30.23` com `override_inform_host = True` — ou seja, ele **empurrava ativamente** o IP morto para toda AP que conectasse. Não era só "URL velha gravada na antena".
2. **As APs tinham DNS inalcançável.** IP estático com `dns1 = 8.8.8.8`, e a Prefeitura não deixa sair para DNS externo. Elas nunca resolveram nome nenhum.
3. **O MikroTik não tinha rota para as redes internas da Prefeitura.** Como o gateway das antenas é o próprio MK, o DNS delas (`10.135.16.119`) caía no default do MK e morria.

## 3. A correção

### No controlador (uma vez, global)
```
super_identity.hostname : 10.30.30.23  ->  unifi.automais.cloud
```
⚠️ É **global aos 3 sites**. As 9 APs do escritório/IPR receberão o nome ao reconectar — comportamento correto, mas avisar a outra frente.

### No controlador, site SMSMARICA (por AP)
```
config_network.dns1 : 8.8.8.8  ->  <IP do MK na LAN>     (CMI 10.1.18.254 · CDT 10.1.92.254)
config_network.dns2 :          ->  10.135.16.119
```
**Por que o MK e não o DNS da Prefeitura:** assim a antena é **indiferente ao estado do link**. Se a Prefeitura cair, o MK resolve pelos forwarders públicos através do túnel e a antena continua achando o controlador — inclusive em unidade que não tem link da Prefeitura (TFD, SRT I).

### No MikroTik da unidade — **3 objetos, só isso**
```
/ip dns set allow-remote-requests=yes servers=10.135.16.119,10.135.16.17,1.1.1.1,8.8.8.8
/ip route add dst-address=10.135.16.0/24 gateway=<GW_REAL_DA_PMM> comment="PMM internas (DNS/AD) pelo gateway real"
/ip route add dst-address=10.90.40.23/32 gateway=wg-eveo comment="UNIFI controlador unifi.automais.cloud via tunel EVEO"
/ip firewall nat add chain=srcnat dst-address=10.90.40.23 out-interface=wg-eveo action=masquerade comment="UNIFI nat saida pelo tunel"
```
Requisito: o túnel `wg-eveo` da unidade (ver `docs/failover-v5/README.md` §2.5b) e as regras 6/7 do CCR2116, que já liberam `10.90.40.23:8080` e `:3478` vindos de `wg-unidades`.

**Não é preciso captura L2 (`bridge nat`).** Testado e removido nas duas: o gateway das antenas já é o MK, então o tráfego entra na pilha IP naturalmente. As duas seguiram conectadas com zero regras de bridge NAT.

## 4. Armadilhas que custaram tempo (não repetir)

1. **Não teste com `ping`.** O firewall do CCR2116 libera só `8080` e `3478` para o controlador (regra 8 dropa o resto; regra 15 dropa input). ICMP falhando ali **não** significa caminho quebrado — use `netwatch type=tcp-conn`. O mesmo vale para o DNS da Prefeitura: `ping 10.135.16.119` falha, mas UDP/53 responde.
2. **Cuidado ao validar com uma gambiarra ligada.** Cheguei a "confirmar" que o DNS da Prefeitura respondia — mas era o MK interceptando (eu tinha criado captura de DNS). Só ao remover a captura a verdade apareceu. **Desligue o contorno antes de validar a causa.**
3. **Sniffer não funciona no hEX** (bloqueado pelo device-mode) — só no CCR do Complexo.
4. **OUI `94:C6:91` não é Ubiquiti**, é EliteGroup (PCs). Inventários que falavam em "APs" no Complexo e no Boqueirão estavam errados.
5. **`/api/s/<site>/rest/device/<id>` exige `PUT`**, não POST (POST devolve 404).

## 5. Acessos

- Controlador (API e web): `https://10.35.0.55:8443` — usuário `becape`
- SSH nas APs: habilitado no site, usuário `becape` (senha em `mgmt.x_ssh_password` via API)
- CCR2116 Eveo: `10.30.50.26`, usuário `becape`
- Salto SSH até uma AP, se preciso: `dst-nat` temporário no MK (porta 2222 → AP:22) — remover depois

Backups: `unifi-super_identity-antes.json`, `unifi-mgmt-antes.json`, `unifi-aps-config-antes.json` (scratchpad).

## 6. Pendências

- [ ] Avisar a outra frente sobre o hostname global alterado (afeta escritório e IPR ao reconectarem)
- [ ] Syslog das APs (`udp/5514`) não passa — o CCR2116 libera só 8080 e 3478. Decidir se vale liberar.
- [ ] As 9 APs de escritório/IPR seguem órfãs (escopo da outra frente); elas estão em **DHCP**, então não têm o problema de DNS fixo das de Maricá.
