# SMSMais.arquivos.pwa

**Arquivos Saúde Maricá** — PWA leve para **digitalizar exames antigos em papel** (laudos,
resultados, atestados) direto do celular e **anexá-los à anamnese** do paciente, via uma
**ponte por QR code**.

> **Como entra no fluxo:** na tela de Anamnese do painel principal (`SMSMarica.front`, médico
> autenticado), o médico clica **"Adicionar Exame"**. O backend gera um **token de upload** e o
> front exibe um **QR code**. O paciente/atendente lê o QR no celular → abre este PWA já com o
> token → fotografa o documento página a página → o app **recorta, corrige perspectiva e melhora
> o contraste no próprio aparelho** (processamento client-side) → monta **1 PDF** → envia. O
> documento aparece no modal do médico para revisar e **Salvar**, ficando anexado à anamnese e ao
> histórico do paciente.

Domínio de produção: **https://arquivos.smsmarica.online**

## Sem login (ponte por QR)

Este PWA **não tem autenticação própria**. Toda a sessão é carregada do **token** que vem na URL
do QR (`https://arquivos.smsmarica.online/?t={token}`):

- O token só pode ser criado a partir de uma sessão autenticada do médico (o QR aparece apenas na
  tela dele).
- O token é **multi-uso dentro de um TTL curto** (15–20 min) e tem **escopo de uma única
  solicitação de exame**; cada "Novo Documento" gera um arquivo novo.
- É **revogável** (ao fechar o modal do médico) e **auditável** (tabela própria no backend).
- Os endpoints anônimos (`/anexos/sessao/*`) validam o token a cada chamada e são **isentos de
  consentimento** (não há cidadão logado). Ver contrato e [ADR-0019](../docs/adr/0019-anexos-exame-pwa-qr-armazenamento.md).

A API clínica é sempre **rede** (nunca cache). O PWA chama `https://api.smsmarica.online`
diretamente.

## Comandos (desenvolvimento)

```bash
npm install
npm run dev       # Vite (http://localhost:5175), proxy /api -> http://localhost:5080
npm run build     # tsc -b && vite build  (gera dist/)
npm run preview
```

## Deploy

Automático via `.github/workflows/deploy-arquivos.yml`: ao dar push em `main` tocando
`SMSMais.arquivos.pwa/**`, o workflow buila e publica o `dist/` em **`/var/www/smsmarica-arquivos`**
(mesmo servidor do `SMSMarica.front` / `SMSMarica.cidadao.pwa`), com swap atômico `.new`/`.old`.
Reusa os secrets `HOST_SMSMARICA` / `USER_SMSMARICA` / `PASS_SMSMARICA` e o env
`VITE_API_BASE_URL_SMSMARICA`.

## Provisionamento no servidor (manual)

> **Passos manuais, uma única vez, em produção.** Não fazem parte do deploy automático. Executar
> com cuidado (servidor de produção). O deploy do CI só copia arquivos estáticos para
> `/var/www/smsmarica-arquivos` — ele **não** cria vhost nem emite certificado.

### 1. Vhost nginx para `arquivos.smsmarica.online`

`/etc/nginx/sites-available/smsmarica-arquivos`:

```nginx
server {
    listen 80;
    server_name arquivos.smsmarica.online;
    # Após o certbot, este bloco redireciona para HTTPS (o --nginx ajusta isto).
    root /var/www/smsmarica-arquivos;

    location / {
        # SPA/PWA: tudo que não for arquivo cai no index.html.
        try_files $uri $uri/ /index.html;
    }

    # O PWA chama https://api.smsmarica.online diretamente, então o proxy /api
    # NÃO é estritamente necessário. Mantido aqui só por paridade com os demais
    # vhosts (caso queira servir a API pelo mesmo domínio no futuro).
    location /api/ {
        proxy_pass http://127.0.0.1:5080/;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Permite upload de PDFs digitalizados (limite generoso; o backend valida ~25 MB).
    client_max_body_size 30m;
}
```

```bash
sudo ln -s /etc/nginx/sites-available/smsmarica-arquivos /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

### 2. Certificado TLS (certbot)

```bash
# Caminho recomendado: deixar o certbot ajustar o vhost para HTTPS automaticamente.
sudo certbot --nginx -d arquivos.smsmarica.online

# Alternativa (somente emitir o cert, sem mexer no nginx):
# sudo certbot certonly --nginx -d arquivos.smsmarica.online
```

Pré-requisito: o DNS `arquivos.smsmarica.online` deve apontar para o IP do servidor antes de
emitir o certificado.
