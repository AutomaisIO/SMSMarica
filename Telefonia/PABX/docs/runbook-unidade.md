# Runbook — ligar um telefone novo numa unidade

Pré-condição: a unidade já está no hub WireGuard (MikroTik configurado — ver
`Telefonia/docs/mikrotik-unidade.md`) e o Automais.Pabx está no ar.

## 1. Criar o ramal

Na página local (`http://<servidor>:5090`) ou via API:

- **Número**: dentro da faixa da unidade (plano de numeração — ver `arquitetura.md`).
- **Unidade**: a unidade física do aparelho (valida depois contra o IP real).
- **MAC**: etiqueta atrás do aparelho, 12 hex (aceita com `:` ou `-`, é normalizado).
- **Marca/modelo**: Intelbras (TIP125…), Cisco (3905), Grandstream (GXP…).

Ao salvar, o serviço já:
1. escreveu o bloco no `sip_smsmarica.conf` + `sip reload`;
2. publicou o XML de provisionamento no TFTP com o secret embutido.

**Anote o secret exibido** só se for configurar o aparelho manualmente — com
auto-provisionamento ele não é necessário.

## 2. Configurar rede do aparelho (manual, sem DHCP)

Padrão do projeto (ver `Telefonia/scripts/mikrotik-gen.sh`): IP fixo na faixa da unidade.

| Campo | Valor |
|---|---|
| IP | `10.200.<id>.10` a `.200` (livre) |
| Máscara | `255.255.255.0` |
| Gateway | `10.200.<id>.1` (MikroTik) |
| Servidor de provisionamento (TFTP) | `10.201.0.1` |

## 3. Auto-provisionar

- **Intelbras TIP**: menu web do aparelho → Atualização/Provisionamento → protocolo TFTP,
  servidor `10.201.0.1` → salvar e reiniciar. Ele busca `<MAC>.xml`.
- **Cisco 3905**: configurar TFTP alternativo `10.201.0.1` (Settings → Admin) e reiniciar.
  Ele busca `SEP<MAC>.cnf.xml`.
- **Grandstream**: web → Maintenance → Upgrade/Provisioning → Config Server `10.201.0.1`
  (TFTP) e reiniciar. Ele busca `cfg<mac>.xml`. **Primeira instalação: validar o template
  com o aparelho e ajustar se necessário.**

## 4. Conferir

Na página local, aba Ramais: LED verde + IP `10.200.<id>.x`. Se aparecer ⚠️ de unidade
divergente, o aparelho está fisicamente em outra unidade (IP não bate com o cadastro).

Teste de voz: ligar para o `1002` (Complexo Regulador) e conferir áudio nos dois sentidos.

## Solução de problemas

| Sintoma | Verificar |
|---|---|
| LED vermelho, aparelho sem registro | VPN da unidade ativa? (`wg show` no hub; handshake do MikroTik) — telefone pinga `10.201.0.1`? |
| Registra mas sem áudio | Firewall do MikroTik (regras VOIP-only), RTP 10000–20000 UDP |
| Não baixa o XML | Nome do arquivo bate com o MAC? (`ls /var/lib/tftpboot \| grep -i <mac>`); TFTP acessível da LAN? |
| Registrado em outra unidade (⚠️) | Aparelho mudou de prédio — atualizar o cadastro do ramal |
