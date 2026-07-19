# Mapa do servidor VOIP existente (auditoria read-only 2026-07-08)

Servidor `192.241.153.121` (FALARMAIS-HOSPITAIS, Ubuntu 22.04, DigitalOcean). **Compartilhado
e administrado pela FalarMais** — este mapa define o que é intocável e onde nosso serviço se
encaixa sem colidir. Reauditar antes de qualquer mudança estrutural (os arquivos são editados
à mão por eles; `extensions.conf` foi modificado no próprio dia da auditoria).

## Asterisk 16.25.3 (chan_sip) — arquivos e donos

| Arquivo | Conteúdo | Dono | Nós |
|---|---|---|---|
| `sip.conf` | geral + 3 includes | FalarMais | só a linha `#include sip_smsmarica.conf` (1x, coordenada) |
| `sip_custom.conf` | ramais **[1000]–[1010]** (faixa SMS Maricá atual) | FalarMais | **somente leitura** (adoção) |
| `sip_IA_falarmais.conf` | [200]–[210] + dezenas de [200] duplicados (sistema "IA" deles) | FalarMais | não tocar |
| `sip_ponte_falarmais_matriz.conf` | tronco FM-HOSPITAIS-01 (matriz, 159.65.47.183) | FalarMais | não tocar |
| `extensions.conf` | dialplan (contexto `PLANO`) — editado ativamente | FalarMais | **não tocar — e não precisa** (ver dialplan) |
| `manager.conf` | AMI [general] + [ami_conexao] | FalarMais | adicionar `[automais_pabx]` (1x, coordenado) |
| `cdr_mysql.conf` | CDR → MySQL local (db `asterisk`, tabela `cdr`) | FalarMais | só copiar credenciais p/ nosso appsettings |

Peers hoje (24): 1000–1010 (SMS Maricá — **1003–1006 online via VPN 10.200.0.11–14**, 1001 e
1010 registrados de IPs públicos), 200–210 (IA FalarMais, offline), tronco ALGAR-2137315313,
ponte FM-HOSPITAIS-01.

## Dialplan `PLANO` — o que descobrimos (bom pra nós)

- **`_XXXX → Dial(SIP/${EXTEN})`**: qualquer ramal de 4 dígitos criado no SIP fica **discável
  automaticamente** — não é preciso mexer no extensions.conf.
- **Toda chamada já é GRAVADA** (MixMonitor → `/var/www/html/gravacoes/asterisk_gravacoes/<mes>/`,
  arquivos `R-`/`S-` com origem-destino-uniqueid). Futuro: linkar gravação ao histórico do
  paciente via `uniqueid` do CDR.
- Entrada ALGAR (21) 3731-5313 toca **SIP/1002 e depois SIP/1001** (nossos).
- Saída PSTN: padrões `_[2-5]XXXXXXX`, `_[6-9]XXXXXXXX`, DDD `0XX…`, serviços `_1XX` — saem
  pelo tronco ALGAR com fallback na ponte da matriz.

### Faixas de numeração OCUPADAS/reservadas

| Faixa | Uso |
|---|---|
| `000`, `87`, `*8XXXX`, `*3` | teste de áudio, ouvir nº, captura, conferência |
| `_XXX` (3 díg.) | ramais 3 dígitos genérico; **400–599 = salas de conferência** |
| `200–210` | sistema IA FalarMais |
| `1000–1010` | SMS Maricá atual (Complexo Regulador + remotos) |
| `4000–4199` | **Hospital Conde** via tronco `SIP/Conde-01` (`_4[0-1]XX`) |
| 8+ dígitos | padrões de saída PSTN |

### Faixa proposta para as unidades SMS Maricá

**`2000–2299`**: `2` + `<id da unidade em 2 dígitos>` + `<sequencial 0–9>` (10 ramais/unidade,
30 unidades). Cai no `_XXXX` genérico, não colide com nada acima. Ex.: CDT (id 6) → 2060–2069.
Unidade 0 mantém os 1000–1010 adotados. **Confirmar com a FalarMais antes do primeiro lote.**

## Infra na caixa

- **Porta 5090 livre** (nosso serviço). Em uso: 80 (Apache — portal PHP da FalarMais:
  dashboard, gravações, filas; nenhum PHP escreve nos .conf do Asterisk), 5038 (AMI),
  5060 (SIP), 69 (tftpd-hpa, raiz `/var/lib/tftpboot`), 51820 (nosso WireGuard), MySQL local.
- Proteções deles: **CrowdSec + fail2ban** ativos — regra de firewall pro 5090 deve conviver
  com isso (conferir chains antes).
- `/opt/telefonia` = nossos scripts do hub WireGuard; `/opt/automais-pabx` será nosso.
- Sem cron/automação regenerando os .conf — mas **humanos da FalarMais editam** (mtimes
  recentes). Risco residual: um edit deles remover nosso include → monitorar (o serviço pode
  alertar se o include sumir; o status AMI zeraria de uma vez).

## AGI/PHP existentes (não tocar)

`agi-bin`: `gravacoes.php`, `permissao.php`, `agi_conferencia.php`, `gnosis_entrada_saida.php`,
`desvioExterno.php`, `acesso_banco.php`. Portal web em `/var/www/html` (login, dashBoard,
filas, gravacoes_analise…).
