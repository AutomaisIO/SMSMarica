# Automais.Pabx — gestão de ramais SMS Maricá

Serviço .NET que roda **dentro do servidor VOIP da FalarMais** (`192.241.153.121`) e expõe
API + página local para gerenciar os ramais das unidades de saúde conectadas pelo hub
WireGuard (`Telefonia/`). É o "agente" que o painel SMSMarica consumirá no futuro
(mesmo padrão do Automais.Fhir: serviço autônomo, API-only para os consumidores).

## O que faz (v1)

| Capacidade | Como |
|---|---|
| CRUD de ramais | Gera `/etc/asterisk/sip_smsmarica.conf` (arquivo exclusivo nosso) + `sip reload` via AMI |
| Provisionamento | Publica XML por MAC/modelo em `/var/lib/tftpboot` (Intelbras `<MAC>.xml`, Cisco `SEP<MAC>.cnf.xml`, Grandstream `cfg<mac>.xml`) |
| Status em tempo real | AMI `SIPpeers` + `CoreShowChannels`; o IP `10.200.<id>.x` identifica a unidade (VPN sem NAT) |
| CDR | Lê o MySQL local do Asterisk (`asterisk.cdr`) — alicerce do futuro histórico de chamadas por paciente |
| Adoção | Importa ramais pré-existentes do `sip_custom.conf` (somente leitura) para o inventário |

## Regras de convivência (servidor compartilhado)

- O servidor é da FalarMais e atende outros clientes. O serviço **só escreve** em:
  `sip_smsmarica.conf`, arquivos do TFTP **registrados por ele** (`arquivo_gerenciado`) e `/opt/automais-pabx`.
- Toda sobrescrita gera backup com timestamp em `/opt/automais-pabx/backups`.
- Arquivo de MAC já existente no TFTP que não é nosso → **409, não sobrescreve**.
- Recarga sempre com `sip reload` (nunca `core restart`).

## Rodar local (dev)

```bash
cd src/Automais.Pabx.Api
dotnet run          # http://localhost:5090 — página local; /docs — Scalar
```

Em Development tudo aponta para `./sandbox` (arquivos fake) e o AMI fica desligado.
API key de dev: `dev-key`. O SQLite (`pabx.db`) e as chaves Data Protection (`keys/`) são locais.

## Documentação

- [`docs/arquitetura.md`](./docs/arquitetura.md) — desenho, decisões e API
- [`docs/runbook-deploy.md`](./docs/runbook-deploy.md) — publicar no servidor VOIP
- [`docs/runbook-unidade.md`](./docs/runbook-unidade.md) — ligar um telefone novo do zero
