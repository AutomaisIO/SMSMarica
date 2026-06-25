# ADR-0019 — Anexos de exame via PWA com ponte por QR + abstração de armazenamento

- **Status:** Aceito
- **Data:** 2026-06-25
- **Decisores:** Bernardo (product/eng)
- **Relaciona-se com:** [ADR-0001](./0001-schema-isolation.md) (schemas isolados), [ADR-0004](./0004-arquitetura-tres-projetos.md) (arquitetura 3-projetos), [ADR-0007](./0007-schema-fhir-separado.md) (separação `smsmarica`/`fhir`), [ADR-0011](./0011-modulo-ia-consulta-linguagem-natural.md) (config/credenciais cifradas geridas por tela), [ADR-0018](./0018-identidade-e-sessao-do-cidadao.md) (identidade/sessão do cidadão)

## Contexto

Muito exame do paciente ainda chega **em papel** (laudos antigos, resultados de outras unidades,
atestados). Na tela de **Anamnese** do painel principal (`SMSMarica.front`, médico autenticado), o
médico precisa **digitalizar e anexar** esses documentos ao atendimento. O celular do
paciente/atendente tem **câmera muito melhor** do que a webcam de um consultório, e já está na mão.

Três restrições moldam a decisão:

1. **Quem fotografa é o celular do cidadão/atendente, sem login.** Não queremos exigir conta,
   instalação ou autenticação no aparelho que vai só tirar a foto. Mas o upload precisa ser
   **seguro e escopado** a um atendimento específico, sem expor a API clínica autenticada.
2. **O painel principal é desktop.** Pedir ao médico para conectar a câmera do PC é fricção; a
   ponte natural é **levar o token para o celular via QR**.
3. **Armazenamento de binário não pode acoplar a uma nuvem específica agora.** Em produção o
   destino provável é o **DigitalOcean Spaces (S3)** — reaproveitando o bucket que já hospeda o
   PACS — mas a primeira entrega roda com **disco local**. O domínio não pode saber onde o byte mora.

## Decisão

### 1. Ponte por QR com token de upload (PWA sem login)

Um PWA leve novo — **`SMSMarica.arquivos.pwa`** (`https://arquivos.smsmarica.online`, React+Vite,
deploy `deploy-arquivos.yml` → `/var/www/smsmarica-arquivos`) — recebe o token pela URL do QR
(`?t={token}`) e **não tem autenticação própria**. Fluxo:

1. Médico clica **"Adicionar Exame"** → front chama `POST /anamneses/{solicitacaoExameId}/anexos/tokens`
   (autenticado, mesma permissão de Anamnese) → recebe `{ token, url, expiraEm, paciente }`.
2. Front exibe um **QR** que codifica a `url`.
3. Celular lê o QR → abre o PWA já com o token → PWA valida via `GET /anexos/sessao/{token}`
   (mostra o nome do paciente).
4. PWA fotografa página a página, processa **no aparelho** e envia 1 PDF via
   `POST /anexos/sessao/{token}` (multipart).
5. O modal do médico faz **polling** em `GET /anamneses/{solicitacaoExameId}/anexos`; ao chegar o
   documento (status `Pendente`), o médico revisa e **Salva** (`Pendente → Salvo`).

### 2. Token multi-uso, escopado e auditável (segurança sem login)

- O token **só nasce de uma sessão autenticada do médico** — o QR aparece exclusivamente na tela
  dele. Quem não tem acesso à Anamnese não consegue gerar token.
- **Multi-uso dentro de um TTL curto** (15–20 min): durante a sessão de digitalização o médico
  pode pedir vários "Novo Documento" sem regerar QR. Cada upload **não consome** o token; apenas
  atualiza `UltimoUsoEm`. **Escopo de 1 `solicitacaoExame`.**
- **Revogável** (ao fechar o modal) e **auditável** em tabela própria. Os endpoints anônimos
  validam o token **a cada chamada**.
- **Rate-limit básico** em `/anexos/sessao/*` contra abuso.

Isso é deliberadamente **diferente** da identidade/sessão do cidadão do [ADR-0018](./0018-identidade-e-sessao-do-cidadao.md):
aqui **não há cidadão logado**; é uma credencial de upload efêmera, escopada e descartável — por
isso os endpoints `/anexos/sessao/*` são **anônimos e isentos de consentimento**.

### 3. Modelo de dados em `smsmarica` (pt-BR)

Duas entidades novas em `smsmarica.*`, com auditoria/soft-delete do [ADR-0006](./0006-papel-derivado-e-auditoria-explicita.md)
e **FK apenas na direção `smsmarica → fhir`** ([ADR-0001](./0001-schema-isolation.md)/[ADR-0007](./0007-schema-fhir-separado.md)):

- **`documento_exame`** (`DocumentoExame`): `Id`, `SolicitacaoExameId` (FK
  `smsmarica.solicitacao_exame`), `Nome`, `Descricao?`, `MimeType`, `TamanhoBytes`, `HashSha256`,
  `ChaveArmazenamento`, `Status` (enum `Pendente`/`Salvo`), `Origem?` (`"pwa-scanner"`), `Paginas?`,
  `AnexoUploadTokenId?` (FK) + auditoria. `CriadoPor` é **NULL** no upload anônimo.
- **`anexo_upload_token`** (`AnexoUploadToken`): `Id`, `Token` (string única, indexada),
  `SolicitacaoExameId` (FK), `PatientId` (Guid FHIR **denormalizado**, sem FK cross-system),
  `PacienteNome` (denormalizado p/ exibir no PWA), `CriadoPor` (usuario), `CriadoEm`, `ExpiraEm`,
  `RevogadoEm?`, `UltimoUsoEm?`. O token é **random URL-safe ≥32 chars** via `RandomNumberGenerator`.

O **histórico** do paciente (`GET /pacientes/{id}/anexos-exame`) agrega os `DocumentoExame` de
status `Salvo` via `SolicitacaoExame.PacienteId`. **Sem projeção FHIR** dos anexos nesta fase
(coerente com a régua "SolicitacaoExame/Laudo ficam só no smsmarica").

### 4. Abstração de armazenamento `IArmazenamentoArquivos` (local agora, S3 depois)

O domínio **não conhece o destino do byte**. Introduzimos `IArmazenamentoArquivos` em
`Core/Armazenamento`, com a única implementação atual **`ArmazenamentoLocalDisco`** lendo
`appsettings: Armazenamento:Local:Diretorio`. A `ChaveArmazenamento` segue o padrão
**`exames/{yyyy}/{MM}/{guid}.pdf`** — agnóstico de backend, já no formato de "key" de bucket, para
que a migração local→S3 seja transparente. Registrada no DI.

Fica **TODO/nota** para a implementação **S3 (DigitalOcean Spaces)** reaproveitando o bucket do
PACS — mas **sem adicionar `AWSSDK` agora**. A troca será só mais uma impl da interface + registro
no DI; nada no domínio muda.

### 5. Credenciais do DigitalOcean Spaces como provedor de integração

Seguindo a tela de credenciais de integração já existente, adicionamos
**`digitalocean_spaces`** a `ProvedoresIntegracao.Suportados` (rótulo
`"DigitalOcean Spaces (S3)"`): `clientId=accessKey`, `clientSecret=secretKey`,
`parametrosJson={endpoint, region, bucket}`. Assim, quando a impl S3 entrar, ela **lê as
credenciais do mesmo store cifrado** (padrão do [ADR-0011](./0011-modulo-ia-consulta-linguagem-natural.md)),
geridas por tela — sem segredo em `appsettings`.

### 6. Processamento de imagem no cliente (PWA), não no servidor

O recorte de borda, correção de perspectiva e melhora de contraste rodam **no aparelho** (ex.:
jscanify/OpenCV.js) e a montagem do PDF também (pdf-lib). O servidor recebe **1 PDF já pronto**
(`application/pdf`, ≤ ~25 MB). Motivos: o celular tem CPU/câmera de sobra, evitamos tráfego de
imagens cruas, e o backend não precisa de pipeline de imagem nem dependências nativas. O servidor
valida MIME, tamanho e hash; não re-processa.

### 7. CORS

`Program.cs` libera a origem `https://arquivos.smsmarica.online` para os endpoints
`/anexos/sessao/*` (os únicos que o PWA chama). Os endpoints autenticados do médico continuam no
CORS do front.

## Consequências

**Positivas**
- Digitalização usa a **melhor câmera disponível** (celular) sem exigir login/instalação no
  aparelho que fotografa.
- O byte fica atrás de uma **abstração**: começamos com disco local e migramos para Spaces/S3 sem
  tocar no domínio.
- O token efêmero, escopado e auditável dá **upload anônimo seguro** sem abrir a API clínica.
- Processamento client-side mantém o backend simples (sem pipeline de imagem).

**Negativas / a vigiar**
- Enquanto o storage for **disco local**, o binário não está no bucket replicado — backup e
  migração local→S3 ficam como passo de produção (ver pendência).
- Token multi-uso amplia a janela de uso indevido se vazar; mitigado por TTL curto, escopo de 1
  solicitação, revogação e rate-limit. Reavaliar TTL/limites na prática.
- `PatientId`/`PacienteNome` denormalizados no token são **dado de exibição** (podem defasar); a
  fonte autoritativa continua no hub FHIR.
- A migration **`AnexosExame`** é criada mas **não aplicada** nesta entrega (aplicação no banco é
  passo de produção, sob confirmação — [regra de produção](../../CLAUDE.md)).

## Alternativas consideradas

- **Capturar pela webcam do desktop do médico**: descartado — câmera pior e fricção de permissão
  no PC; o celular já está na mão e fotografa melhor.
- **Exigir login do cidadão no PWA (reusar ADR-0018)**: descartado — overhead desnecessário para
  quem só vai tirar uma foto; o token escopado resolve a segurança sem conta.
- **Processar a imagem no servidor**: descartado — exigiria pipeline/deps nativas e tráfego de
  imagens cruas; o cliente faz melhor e mais barato.
- **Gravar binário direto no Postgres (bytea)**: descartado — infla o banco e não reaproveita o
  bucket; a abstração + S3 é o caminho.
- **Já implementar S3 com AWSSDK agora**: adiado — sem credenciais/bucket definidos; a interface
  permite plugar depois sem refluxo.
