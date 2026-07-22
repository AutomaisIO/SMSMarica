# Pendência — whitelist dinâmica do firewall do PACS, controlada por login na plataforma

**Status:** ideia aprovada, sem implementação. Registrada em 2026-07-22.

## O que é

Uma **API de segurança no PACS** que abre exceção no firewall (`nftables`/`ipset`)
para um IP **por X dias**, acionada pelo **nosso backend** (`smsmarica.online`) quando
um usuário se autentica de forma válida na plataforma. A exceção **expira sozinha**
(entrada `ipset` com `timeout`), sem limpeza manual.

```
usuário valida no smsmarica.online
   → backend (10.35.0.10 / IP público fixo 146.190.65.73) chama a API de segurança do PACS
   → PACS adiciona o IP na whitelist do firewall por X dias (ipset timeout)
```

## Cadeia de confiança

- A API de segurança do PACS **confia no nosso backend** — ele é peer conhecido
  (IP público fixo `146.190.65.73` **e** VPN `10.35.0.10`), então chama **sem
  autenticação própria** (autorização por origem). Insurance barata: somar um
  segredo compartilhado no header (defense-in-depth; IP TCP já é difícil de forjar).
- A decisão de "quem pode" fica onde já existe: o **login da plataforma**. A API do
  PACS só executa o que o backend mandar.
- **Nível global do servidor**, não por unidade/equipamento (decisão do usuário
  2026-07-22): a exceção é "este IP alcança o PACS", ponto — sem escopo por unidade
  ou modalidade.

## Onde encaixa (e onde NÃO)

| Consumidor | Bate no PACS direto? | Whitelist dinâmica ajuda? |
|---|---|---|
| Front (navegador) | não — passa pelo proxy `/pacs/rs` do backend | não precisa (backend já liberado) |
| Backend → PACS | sim, IP fixo | não precisa (já na whitelist estática) |
| **Weasis / visualizador direto (humano)** | sim, de IPs que **rotacionam** | **SIM — é o caso que justifica** |
| Equipamento (mamógrafo/US) | sim, mas **não faz login** | não — ver nota |

**Nota do equipamento:** mamógrafo e console Fuji não se autenticam na plataforma,
então nunca "ganham" exceção por login. Para eles a defesa é o **filtro por AE**
(`dcmAcceptedCallingAETitle`, independe de IP) e/ou a VPN por unidade. A whitelist
dinâmica é para **cliente humano direto de qualquer lugar** — hoje, o Weasis da Dra.
(que apareceu de dezenas de IPs/ISPs diferentes; whitelist estática não escala).

> Se a Dra. migrar do Weasis-direto para o visualizador da própria plataforma
> (Cornerstone, via proxy do backend já liberado), a necessidade some — vale
> confirmar antes de construir.

## Esboço técnico

- **No PACS:** serviço pequeno (systemd) escutando **só na VPN/localhost** (nunca
  público), endpoint `POST /whitelist {ip, dias}` autorizado por origem (backend) +
  segredo. Ação: `ipset add pacs-allow <ip> timeout <dias*86400>`; o `ufw`/`nftables`
  aceita 8080/11112 de quem está no set `pacs-allow`.
- **No backend:** ao autenticar o usuário (ou ao abrir um exame que exige acesso
  direto ao PACS), chamar a API com o IP do cliente + validade. Idempotente (renova o
  timeout se já existe).
- **Expiração:** nativa do `ipset timeout` — não precisa de cron de limpeza.
- **Auditoria:** logar quem/qual IP/quando foi liberado (LGPD).

## Pré-requisitos / decisões em aberto

1. Confirmar se o Weasis continua em uso ou se a Dra. vai para o visualizador da
   plataforma (muda a prioridade disto).
2. Definir X (dias de validade da exceção).
3. Definir se o front passará a buscar imagem **direto** do PACS (offload do proxy) —
   nesse caso a whitelist dinâmica passa a valer para IP de usuário final também, e o
   browser precisa de token/credencial escopada para o PACS.
4. Segredo compartilhado backend↔API-PACS (guardar no cofre/credenciais).

## Relação com o que já existe

- Firewall estático (`ufw`) e fail2ban já no ar — ver [`../pacs.md`](../pacs.md) §10.4.
- PACS na VPN `10.35.0.16`, backend em `10.35.0.10`.
- Esta API seria a camada **dinâmica** por cima do firewall estático.
