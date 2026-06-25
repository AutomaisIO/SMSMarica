# Pendência — Passos de produção dos Anexos de Exame (Arquivos Saúde Maricá)

- **Aberta em**: 2026-06-25
- **Contexto**: feature "Arquivos Saúde Maricá" — digitalizar exames em papel pelo celular (PWA
  `SMSMarica.arquivos.pwa`, ponte por QR) e anexar à anamnese. Ver
  [ADR-0019](../adr/0019-anexos-exame-pwa-qr-armazenamento.md).

Os itens abaixo **não** foram executados nesta entrega — são passos de **produção**, sob
confirmação explícita (ver [regra de produção](../../CLAUDE.md): nada de escrita no DB/deploy sem
OK por ação).

## 1. Aplicar a migration `AnexosExame` no Postgres de produção

A migration foi **criada** (`dotnet ef migrations add AnexosExame ...`) mas **não aplicada**.
Cria apenas as tabelas `smsmarica.documento_exame` e `smsmarica.anexo_upload_token` (não
destrutiva). Aplicar com `dotnet ef database update` (ou pipeline de deploy do server) após
confirmação.

## 2. nginx vhost + certbot para `arquivos.smsmarica.online`

Passo manual, uma vez (detalhado no `SMSMarica.arquivos.pwa/README.md`, seção "Provisionamento no
servidor"):

- DNS `arquivos.smsmarica.online` → IP do servidor.
- vhost nginx servindo `/var/www/smsmarica-arquivos` com SPA fallback (`try_files ... /index.html`)
  e `client_max_body_size 30m`. Proxy `/api` é opcional (o PWA chama `api.smsmarica.online`
  direto) — incluído só por paridade.
- `sudo certbot --nginx -d arquivos.smsmarica.online` (TLS).

## 3. Bucket/credenciais DigitalOcean Spaces + migração storage local → S3

A primeira entrega roda com **disco local** (`ArmazenamentoLocalDisco`, `appsettings:
Armazenamento:Local:Diretorio`). Para produção:

- Criar **bucket** no DigitalOcean Spaces (reaproveitar o bucket do PACS) e **access/secret key**.
- Cadastrar as credenciais pelo provedor de integração **`digitalocean_spaces`** (store cifrado,
  tela de integrações) — `clientId=accessKey`, `clientSecret=secretKey`,
  `parametrosJson={endpoint, region, bucket}`.
- Implementar `ArmazenamentoSpacesS3 : IArmazenamentoArquivos` (lê do store cifrado; **adicionar
  AWSSDK só aqui**) e trocar o registro no DI.
- **Migrar os binários** já gravados em disco local para o bucket, preservando a
  `ChaveArmazenamento` (`exames/{yyyy}/{MM}/{guid}.pdf` já é formato de key S3).

## Encaminhamento

Ordem natural: (2) vhost+TLS para o PWA subir → (1) migration para os endpoints funcionarem em
prod → (3) S3 quando o volume justificar (até lá, disco local com backup). Tudo sob confirmação
por ação.
