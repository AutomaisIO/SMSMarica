# Automais.Zap

Relay do webhook do WhatsApp. Recebe o webhook único do App da Automais na Meta e entrega
cada evento à aplicação dona do número. Nada além disso.

Decisão: [ADR-0044](../docs/adr/0044-app-meta-unico-e-roteador-whatsapp.md) — o App da Meta tem
**uma** URL de callback, mas número, nome de exibição e selo são por WABA. Para um App servir N
prefeituras, alguém precisa distribuir esse webhook.

## O que ele NÃO faz

- **Não guarda conversa, mensagem nem telefone de cidadão.** Encaminha envelope e conta o
  resultado. É o que o mantém fora do alcance da LGPD dos municípios: ele é *operador*, cada
  prefeitura é *controladora*.
- **Não envia mensagem.** O envio sai direto de cada instância para
  `graph.facebook.com/v21.0/{phone_number_id}/messages`.
- **Não licencia nem gerencia instância.** Suspender o canal de um cliente é desligar o destino
  na tela; nenhuma instância consulta o relay para saber se pode funcionar.

## Como decide o destino

Pelo `entry[].changes[].value.metadata.phone_number_id`, e só por ele. Eventos sem número (status
de template, por exemplo) caem no destino dono do WABA — e se o WABA tiver números de mais de um
destino, nada é entregue, porque ambíguo é pior que atrasado.

### O relay é transparente

Quando o POST inteiro é de um destino só — o caso normal — o corpo segue **byte a byte** com a
assinatura original da Meta. A aplicação de destino valida o HMAC exatamente como já valida hoje:
**nenhuma mudança de código do lado da instância**.

Quando um POST traz eventos de mais de um dono, o relay **recorta** o payload por destino e
reassina com o **mesmo App Secret**. Entregar o corpo inteiro a cada um faria o município A ver
mensagem do B; e como sob o App único o App Secret é o mesmo dos dois lados, o destino valida o
recorte sem saber que houve recorte.

### Confirmação sem buffer

O 200 só vai para a Meta depois da entrega. Falhou algum destino → 502, e a Meta reentrega o lote.
Isso substitui uma tabela de buffer aqui dentro. Um destino que já havia recebido vê a duplicata,
absorvida pela idempotência por `wamid` que a instância já tem.

`phone_number_id` desconhecido responde **200** de propósito: reentregar não faria o número passar
a existir. Aparece em *Entregas* como "sem rota cadastrada".

## Arquitetura

Três camadas (ADR-0004): `Data ← nada`, `Core ← Data`, `Api ← Core + Data`.

| | |
|---|---|
| Banco | Postgres **local do droplet**, database próprio, schema `zap` |
| Porta | `127.0.0.1:5086` — quem fala com a internet é o nginx |
| Host | `zap.automais.io` |
| Unit | `automais-zap` |
| Deploy | `.github/workflows/deploy-zap.yml` |

O banco é local, e não no cluster gerenciado das instâncias, porque o relay é ponto único de falha
do inbound de **todos** os clientes: não pode herdar o destino de um vizinho barulhento num cluster
compartilhado.

## Endpoints

| Rota | Para quê |
|---|---|
| `GET /meta/webhook` | Handshake `hub.challenge` da Meta |
| `POST /meta/webhook` | Recebe o evento e entrega — é esta a Callback URL do App |
| `GET /admin` | Tela de rotas (login) |
| `GET /admin/entregas` | Últimas 200 tentativas |
| `GET /health` | Liveness + banco |
| `GET /docs` | Scalar |

## Configuração

Tudo por env var; nada de segredo em `appsettings.json`.

| Env | Para quê |
|---|---|
| `ConnectionStrings__ZapDb` | Postgres local |
| `Meta__AppSecret` | Confere o HMAC da Meta e assina o recorte. **Vazio faz o webhook responder 503** — falha fechado, de propósito. |
| `Meta__VerifyToken` | Handshake |
| `Admin__Email` / `Admin__SenhaInicial` | Semeia o **primeiro** operador, e só se a tabela estiver vazia |
| `Relay__TimeoutSegundos` | Teto por entrega (padrão 10) |
| `Relay__RetencaoLogDias` | Retenção do `entrega_log` (padrão 30) |

## Comandos

```bash
cd Automais.Zap
dotnet build                                    # 0 erros, 0 warnings
dotnet test                                     # precisa de Docker (Testcontainers)
ZAP_TESTS_CONNECTION="<bancada>" dotnet test    # sem Docker, contra Postgres real
dotnet run --project src/Automais.Zap.Api       # http://localhost:5086

dotnet ef migrations add <Nome> \
  --project src/Automais.Zap.Data \
  --startup-project src/Automais.Zap.Api
```

## Bootstrap do droplet (uma vez, manual)

O workflow cuida do serviço. Estes passos são do host:

1. `apt install postgresql`; criar database e role do relay; deixar em loopback.
2. DNS **A** de `zap.automais.io` → IP do droplet (`dig +short zap.automais.io` para conferir).
3. nginx: vhost com `proxy_pass http://127.0.0.1:5086`, e `certbot --nginx -d zap.automais.io`.
4. Secrets no GitHub: `HOST_ZAP`, `USER_ZAP`, `PASS_ZAP`, `DB_CONNECTION_ZAP`,
   `META_APP_SECRET`, `META_VERIFY_TOKEN`.
5. Primeiro deploy; depois semear o operador (`Admin__Email`/`Admin__SenhaInicial` no env, subir
   uma vez, remover do env) e trocar a senha.

> O hostname próprio desde o dia 1 é deliberado: a Callback URL na Meta é a coisa mais cara de
> mudar. Com `zap.automais.io`, trocar de droplet depois é alteração de DNS — a Meta não fica
> sabendo.

## Cutover

1. Cadastrar o destino e o `phone_number_id` na tela.
2. Com o App ainda apontando para a produção, testar contra o relay com POSTs assinados.
3. Trocar a Callback URL do App da Meta para `https://zap.automais.io/meta/webhook`.
4. Reversão: apontar de volta.

Confirmar entrando uma mensagem real **e** um envio recebendo `delivered` — os recibos de status
voltam pelo webhook, então é o status que prova o caminho inteiro.
