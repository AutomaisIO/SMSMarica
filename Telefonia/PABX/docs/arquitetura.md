# Arquitetura — Automais.Pabx

## Contexto

O PABX das unidades de saúde é um **Asterisk 16.25.3 (chan_sip)** operado pela FalarMais em
`192.241.153.121` — servidor **compartilhado** com outros clientes (tronco ALGAR, ponte
FM-HOSPITAIS-01, TFTP com provisionamento de terceiros). As unidades chegam pelo hub
WireGuard (ver `Telefonia/README.md`): telefones em `10.200.<id>.0/24`, Asterisk visto
pelos aparelhos como `10.201.0.1`, **sem NAT** — o IP de origem identifica a unidade.

O Automais.Pabx roda nessa caixa e é a **única porta de entrada programática** para os
ramais da SMS Maricá. O painel SMSMarica (menu Telefonia, futuro) consome esta API — nunca
mexe no Asterisk diretamente. Decisão espelha o Automais.Fhir (ADR-0010): serviço autônomo
com contrato estável; a página local embutida existe só para operar/testar antes do menu.

## Componentes

```
SMSMarica.front (futuro menu Telefonia)
        │  HTTPS + X-Api-Key
        ▼
Automais.Pabx (porta 5090, systemd, self-contained)
 ├─ Ramais/            CRUD + adoção + status (inventário no SQLite local)
 ├─ Asterisk/          gerador chan_sip (IGeradorConfigSip) + cliente AMI próprio
 ├─ Provisionamento/   templates Intelbras/Cisco/Grandstream → /var/lib/tftpboot
 ├─ Cdr/               leitura MySQL asterisk.cdr (só-leitura)
 └─ wwwroot/           página local (HTML+JS puro)
```

- **SQLite local** (`/opt/automais-pabx/pabx.db`): inventário (ramal, unidade, MAC/modelo,
  arquivos gerenciados). O agente funciona sozinho na caixa, sem depender do Postgres da DO.
- **Secrets SIP cifrados** em repouso (ASP.NET Data Protection, chaves em
  `/opt/automais-pabx/keys`). Secret só sai em claro na criação/reset (uma vez) e nos
  artefatos que o telefone precisa (conf/XML).
- **Unidades** são seed do registro-mestre `Telefonia/registro/unidades.csv` (upsert por id
  em todo startup — atualizar o CSV + redeploy sincroniza).

## Fluxo de escrita no Asterisk

1. Mutação de ramal (POST/PUT/DELETE/reset-secret) →
2. Regenera **`/etc/asterisk/sip_smsmarica.conf` inteiro** a partir do inventário
   (template = bloco real do ramal 1002; `context=PLANO`, alaw/ulaw/gsm, nat=force_rport,comedia) →
3. Backup timestampado do arquivo anterior em `/opt/automais-pabx/backups` →
4. `sip reload` via AMI (action `Command`).

O `sip.conf` da FalarMais recebe **uma única linha** (feita 1x no deploy, com OK deles):
`#include sip_smsmarica.conf`.

Ramais **adotados** (1002/1003, pré-existentes) ficam no inventário para status/CDR, mas o
bloco deles continua no `sip_custom.conf` legado — nunca tocamos nesse arquivo (parser
somente-leitura em `SipCustomParser`).

### chan_sip → PJSIP

chan_sip foi removido no Asterisk 21. O gerador é a interface `IGeradorConfigSip`
(implementação atual `GeradorConfigChanSip`); migrar = nova implementação, domínio intacto.

## Provisionamento

TFTP (`tftpd-hpa`) já existia no servidor servindo XMLs de outros clientes. Convenções de
nome que cada fabricante busca ao ligar:

| Marca | Arquivo | Template (origem) |
|---|---|---|
| Intelbras TIP | `<MAC maiúsculo>.xml` | copiado de XML real em produção (TIP125) |
| Cisco 3905 | `SEP<MAC maiúsculo>.cnf.xml` | copiado de XML real em produção (CP3905) |
| Grandstream GXP | `cfg<mac minúsculo>.xml` | P-values padrão — **validar com aparelho real** |

Placeholders: `{{RAMAL}}`, `{{SECRET}}`, `{{SERVIDOR}}` (=`10.201.0.1`), `{{MODELO}}`, `{{LABEL}}`.
Proteção: arquivo de MAC que já existe e **não** está em `arquivo_gerenciado` → 409
(`arquivo_alheio`) **antes** de persistir o ramal.

## API (contrato para o futuro menu no SMSMarica)

Autenticação: header `X-Api-Key` em tudo sob `/api`. OpenAPI em `/openapi/v1.json`, Scalar em `/docs`.

| Endpoint | Uso |
|---|---|
| `GET /api/unidades` | unidades + contagem de ramais |
| `GET /api/ramais?unidadeId=` | inventário |
| `GET /api/ramais/status` | tempo real: online/latência/IP/unidade detectada/em chamada |
| `POST /api/ramais` | cria; retorna `{ramal, secret}` — secret exibido uma única vez |
| `PUT /api/ramais/{numero}` | atualiza (inclui `ativo`) |
| `DELETE /api/ramais/{numero}` | remove do inventário + conf + XML |
| `POST /api/ramais/{numero}/reset-secret` | rotaciona secret (conf + XML juntos) |
| `POST /api/ramais/adotar` | importa ramais do sip_custom.conf |
| `POST /api/ramais/aplicar` | força regeneração + sip reload |
| `GET /api/cdr?ramal=&numero=&de=&ate=&pagina=` | histórico de chamadas (paginação) |

Erros no padrão do monorepo: ProblemDetails com 400 (validação), 404, 409 (com `codigo`), 503 (CDR sem config).

### CDR e o histórico por paciente (fase SMSMarica)

`calldate` é wall-clock do PABX (Brasília) e sai como está (regra única de fuso do projeto).
O filtro `numero` normaliza para dígitos e casa por **sufixo** (`LIKE '%<digitos>'`), cobrindo
prefixo de operadora/DDD. O SMSMarica correlacionará chamadas ao paciente pelo telefone do
cadastro consumindo este endpoint (ou uma futura sincronização push).

## Segurança

- Porta 5090; restringir no firewall da caixa aos IPs de gestão + IP do SMSMarica prod.
- `appsettings.Production.json` (fora do git): `Pabx:ApiKey`, `Asterisk:Ami:Secret`
  (usuário AMI próprio `automais_pabx`, sem permissão `originate` por ora),
  `ConnectionStrings:CdrDb` (mesmas credenciais do `cdr_mysql.conf`).
- Roda como root (escrita em `/etc/asterisk` + TFTP); superfície mitigada por API key +
  firewall + escrita restrita a arquivos gerenciados.

## Pendências / próximas fases

- **Plano de numeração por unidade**: auditoria de 2026-07-08 (ver
  [`servidor-existente.md`](./servidor-existente.md)) confirmou que o dialplan `_XXXX` torna
  qualquer peer de 4 dígitos discável sem tocar no `extensions.conf`, e que `2000–2299`
  (`2<id:2díg><seq:1díg>`) está livre (ocupados: 1000–1010 nossos, 200–210 IA, 4000–4199
  Hospital Conde, 400–599 conferência). Falta só o de-acordo da FalarMais.
- Menu Telefonia no SMSMarica + correlação chamada↔paciente.
- Click-to-dial (AMI `originate`) e webhook de eventos em tempo real.
- Validar template Grandstream com aparelho físico.
