# Push do App do Cidadão — estado e próximos passos

**Status:** implementado e verificado em teste automatizado, **não publicado**. Falta o projeto no
Firebase e o teste de ponta a ponta num celular. Atualizado em 2026-10-09.
**Branch:** `feat/push-cidadao`, publicada em `origin` (nada disso está na `main`).
**Relacionado:** [ADR-0070](../adr/0070-push-do-app-do-cidadao.md) (as decisões) ·
[`SMSMais.cidadao.app/README.md`](../../SMSMais.cidadao.app/README.md) (configurar o Firebase no app) ·
[ADR-0018](../adr/0018-identidade-e-sessao-do-cidadao.md) (sessão do cidadão)

Este documento existe para retomar o trabalho de outro computador. O que está no código se lê no
código; aqui fica o que **não** se vê nele: o que foi provado, o que está fora do git e a ordem
dos próximos passos. **O repositório é público:** endereços de servidor, senhas e o JSON do
Firebase nunca entram aqui.

## Começar num PC novo

Pré-requisitos: Windows (o script da pilha usa PowerShell 5.1), .NET SDK 10 (`global.json`), Node
e npm, Flutter 3.41 ou mais novo com Android SDK (`flutter doctor`), e
`dotnet tool install --global dotnet-ef`. Docker é opcional (com ele, os testes de banco rodam sem a
bancada).

```powershell
git fetch origin
git switch feat/push-cidadao
cd SMSMais.front; npm install; cd ..
cd SMSMais.cidadao.app; flutter pub get; cd ..
cd SMSMais.server; dotnet restore; cd ..
```

Os comandos abaixo rodam na raiz do clone.

## O que é

O operador abre a ficha do paciente no painel e manda uma notificação para o celular dele, pelo app
nativo (`SMSMais.cidadao.app`). Vai pelo Firebase Cloud Messaging (FCM), que **não cobra** por
mensagem (o limite é de 600 mil por minuto por projeto).

- Na ficha do paciente, aba **Histórico de Acesso**: o bloco **"Notificações no app"** (acima do
  "Histórico de acesso ao app") mostra os aparelhos com notificações ativas e as últimas
  notificações enviadas. **Enviar notificação** pede título (65), mensagem (240) e a tela que abre
  no app (lista fixa). O resultado de cada aparelho aparece no próprio modal.
- Aparelho = **sessão** do login do cidadão. Sair do app tira o aparelho; quem entra em dois
  celulares recebe nos dois.
- **Sem dado de saúde no texto** (aparece na tela bloqueada e passa pelo Google). O modal avisa.
- Permissão nova: módulo **81 — Notificações no app do cidadão** (`NotificacaoAppCidadao`; Consulta
  vê, Edição envia). O perfil Admin recebe pelo seed; os outros perfis precisam marcar.
- A chave do Firebase fica em Integrações, no card **"Firebase — notificações do app do cidadão"**,
  cifrada, com **Testar**.

## Onde está cada parte

| Parte | Caminho |
|---|---|
| Servidor | `SMSMais.server/src/SMSMais.Core/Cidadao/Push/` |
| Endpoints | `PUT /auth/paciente/dispositivo` (`CidadaoController`) · `GET /pacientes/{id}/app-cidadao` e `POST /pacientes/{id}/app-cidadao/notificacoes` (`PacientesController`) · `POST /integracoes/credenciais/fcm/testar` (`IntegracaoCredencialController`) |
| Banco | migration `20261009035717_PushDoAppDoCidadao`: 3 colunas `push_*` em `cidadao_sessao` + tabela `cidadao_notificacao` |
| Testes do servidor | `SMSMais.server/tests/SMSMais.Tests/Cidadao/Push/` |
| Painel | `SMSMais.front/src/features/pacientes/components/AppCidadaoNotificacoes.tsx`, `ModalEnviarNotificacao.tsx` · `features/integracoes/components/FirebaseCard.tsx` |
| Manual | artigo novo `manual/conteudo/integracoes.tsx`; seções `app-notificacoes*` em `manual/conteudo/pacientes.tsx` |
| App | `SMSMais.cidadao.app/lib/shared/push/` (a branch também traz a reescrita do app como espelho do PWA, commitada junto) |
| Pilha local de teste | [`scripts/push-cidadao/pilha-local.ps1`](../../scripts/push-cidadao/pilha-local.ps1) |

## O que já foi provado (09/10/2026)

| O quê | Resultado |
|---|---|
| `dotnet build` (servidor) | 0 avisos, 0 erros |
| Testes do push sem banco | 68/68 |
| Testes do push com banco, na bancada (`PushCidadaoServiceTests`) | 13/13 |
| Suítes vizinhas na bancada (Cidadão, Integrações, Unificação) | 1035/1036 — a única falha é `BackfillExecutanteTests`, que estoura o tempo na bancada e não tem relação |
| `dotnet ef migrations has-pending-model-changes` | sem mudanças |
| Painel `npx tsc -b` | sem erros (o `npm run build` pede `VITE_API_BASE_URL`) |
| App `flutter analyze` / `flutter test` | sem problemas / 14/14 |
| App `flutter build apk --debug` | gera o APK |

Para repetir:

```powershell
cd SMSMais.server
dotnet test tests/SMSMais.Tests --filter "FullyQualifiedName~Cidadao.Push&FullyQualifiedName!~PushCidadaoServiceTests"
dotnet test tests/SMSMais.Tests --filter "FullyQualifiedName~PushCidadaoServiceTests"   # Docker ou SMSMARICA_TESTS_CONNECTION
dotnet ef migrations has-pending-model-changes --project src/SMSMais.Data --startup-project src/SMSMais.Api
```

As respostas do Firebase nos testes são **simuladas** a partir do formato documentado.

## O que ainda não foi provado

- **Nenhuma chamada real ao Firebase.** O projeto ainda não existe.
- Teste em celular: permissão, receber com o app aberto, em segundo plano e fechado, o toque abrindo
  a tela certa.
- A pilha local nunca subiu de verdade. A trava contra a produção foi testada com conexões hostis,
  mas as janelas subindo, não.
- iOS: não foi compilado (precisa de Mac).

## Estado fora do git

Coisas que um clone novo não mostra.

**Bancada** é o Postgres de testes compartilhado, num cluster separado da produção. A conexão **não
está em nenhum git**: no PC original ela fica fora do repositório. Num PC novo, copie de lá (ou do
painel do provedor do banco) e grave no seu usuário, abrindo um terminal novo depois:

```powershell
[Environment]::SetEnvironmentVariable('SMSMARICA_TESTS_CONNECTION', 'Host=<host>;Port=25060;Database=defaultdb;Username=<usuario>;Password=<senha>;SSL Mode=Require', 'User')
[Environment]::SetEnvironmentVariable('SMSMAIS_BANCADA_HOST', '<host>', 'User')
```

Se a conexão der tempo esgotado, o IP do PC precisa entrar na lista de origens confiáveis do
cluster.

- **Bancada:** a migration do push **já está aplicada** lá, com o índice `ix_cidadao_sessao_push_token`
  como b-tree (o arquivo foi trocado para hash depois; em produção ele nasce hash). Por isso a
  migration **não pode ser renomeada nem regerada** com outro timestamp: a bancada quebraria com
  "column already exists". O banco `fhir_testes` do hub já existe no mesmo cluster.
- **Bancada:** já existe uma credencial `fcm` em Integrações, desativada e cifrada com a chave de
  outro computador. No painel local, use **Limpar** antes de colar o JSON.
- **Bancada:** o perfil Admin só ganha o módulo 81 na primeira subida da API desta branch (o seed
  roda junto com as migrations).
- **Firebase:** nenhum projeto criado.
- **Celular de teste (Samsung):** tem um APK antigo do app, sem push.
- **Produção:** nada. As colunas nem existem lá.
- **PC original:** a pasta principal do repositório tem uma cópia não commitada, idêntica, de
  `SMSMais.cidadao.app` (o app foi escrito lá e copiado para a branch). Depois do merge desta
  branch, descarte essa cópia lá antes do `git pull` da `main`.

## Armadilhas

1. **`dotnet run` da API com a configuração padrão pode cair na PRODUÇÃO.** No PC original o
   user-secrets `smsmarica-api-dev` aponta para a produção, e o `AutoMigrate` está ligado: subir
   assim aplica a migration, roda o seed e liga as rotinas de fundo na produção. Num PC novo o
   user-secrets não existe; **não** o crie com a conexão da produção. Para testar, use o
   `pilha-local.ps1`: ele só aceita a bancada, sobe fora de Development (o user-secrets nem carrega)
   e desliga as rotinas.
2. **O app sem `--dart-define=API_BASE_URL` fala com a produção.** O login manda código de verdade
   pelo WhatsApp para o paciente do CPF digitado, e o registro do aparelho dá 404 calado.
3. **Painel:** suba o desta branch. Um `.env*` em `SMSMais.front` apontando para outro backend faz o
   painel pular a API local (o script aborta se encontrar).
4. **Celular físico:** `10.0.2.2` só funciona no emulador. No celular, `adb reverse tcp:5080 tcp:5080`
   e `API_BASE_URL=http://localhost:5080`.
5. **Não rode `flutterfire configure` na pasta do app:** ele grava um `firebase.json` por cima do
   nosso e gera arquivos que não usamos.
6. **App e chave de projetos diferentes:** o envio mostra "Este aparelho está ligado a outro projeto
   do Firebase". O Testar não pega esse caso (ele confere a chave, não o app).
7. **Samsung:** para receber com o app fechado, Bateria do app → Sem restrições. App parado com
   "Forçar parada" não recebe push (e sair do `flutter run` com `q` também força a parada) — feche
   pela lista de recentes.

## Próximos passos

### 1. Firebase (dono da conta Google)

1. console.firebase.google.com → projeto novo, **sem Analytics**.
2. Adicionar app **Android** com o pacote `io.automais.smsmais.cidadao` (sem SHA-1, sem baixar nada).
3. Configurações → Contas de serviço → **Gerar nova chave privada**. Se a conta for de uma
   organização (Google Workspace, ou criada depois de maio de 2024), a política
   `iam.managed.disableServiceAccountKeyCreation` bloqueia ("Key creation is not allowed"); quem for
   administrador de políticas da organização abre exceção só para o projeto. Guarde o JSON **fora
   do repositório** (num cofre de senhas): ele é colado duas vezes (local e produção), porque cada
   ambiente cifra com a própria chave. Se ele não estiver à mão noutro PC, gere outra chave na mesma
   conta e apague a antiga.
4. Anotar ID do projeto, número do projeto, ID do app Android e a **chave de API do Android** (ela
   não aparece no card do app: Google Cloud → APIs e serviços → Credenciais → "Android key (auto
   created by Firebase)"). Preencher `SMSMais.cidadao.app/firebase.json` a partir do
   `firebase.exemplo.json` (o `firebase.json` é ignorado pelo git). Guarde esses valores junto com o
   JSON, para montar o arquivo em outro PC.
5. Conferir na aba Cloud Messaging que a "Firebase Cloud Messaging API (V1)" está ativada.

### 2. Pilha local na bancada

Com as duas variáveis da seção "Estado fora do git" definidas:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\push-cidadao\pilha-local.ps1
powershell -ExecutionPolicy Bypass -File scripts\push-cidadao\pilha-local.ps1 -Parar
```

O script sobe hub FHIR (banco `fhir_testes`, porta 5081), API (5080, com rotinas desligadas, código
de login na tela e WhatsApp simulado) e painel (5173), e liga o `adb reverse` no celular conectado
(com mais de um, passe `-Aparelho <serial do adb devices>`). Confira **"Migrations OK."** na janela
da API: o `/health` responde mesmo se a migration falhar.

No painel local (`http://localhost:5173`), entre com o admin do seed (login `admin`; a senha inicial
é a constante `AdminSenhaInicial` em `SMSMais.server/src/SMSMais.Api/Auth/DbSeeder.cs`, se ninguém
a trocou na bancada):
- Integrações → card **Firebase — notificações do app do cidadão** → **Limpar** → colar o JSON →
  marcar **Envio de notificações ativo** → **Salvar** → **Testar**. Sem a permissão de Exclusão o
  Limpar não aparece; colar por cima e salvar também resolve. O Testar faz um envio de validação
  (não entrega nada): prova a chave, a API ligada e a permissão.
- Criar um paciente de teste com CPF válido, **data de nascimento** e sem WhatsApp verificado, com
  uma solicitação que tenha número. No primeiro acesso o app pede nascimento, celular e esse
  número. Sem nascimento no cadastro, o servidor confere o CPF na Receita (que localmente não tem
  credencial).

### 3. Celular

```powershell
cd SMSMais.cidadao.app
flutter run -d <serial> --dart-define-from-file=firebase.json --dart-define=API_BASE_URL=http://localhost:5080
```

`<serial>` é o que aparece em `adb devices`. Entrar com o paciente de teste (o código aparece na
tela), aceitar o termo e **Permitir** notificações. No console do `flutter run` deve aparecer o
`PUT .../auth/paciente/dispositivo` com 204, e o aparelho aparece na ficha.

### 4. Testes, do mais simples ao completo

1. Console do Firebase → Messaging → **Send test message** com o token do log: prova
   Firebase → celular sem o nosso servidor.
2. Ficha do paciente → **Enviar notificação** escolhendo uma tela: prova tudo junto.
3. Repetir com o app aberto, em segundo plano e fechado pelos recentes.
4. Repetir com `flutter run --release`.

### 5. Produção — cada passo com OK explícito

1. **Rebase** da branch sobre a `origin/main`. Veja o que entrou com
   `git log --oneline HEAD..origin/main`. Em 09/10 à tarde eram 9 commits, e dois mexem nos mesmos
   arquivos:
   - `81dae1e` "Entrar como paciente": `CidadaoSessao`, `CidadaoSessaoConfiguration`,
     `CidadaoSessaoService`, `CidadaoController`, `SmsMaisDbContext`, `DependencyInjection`,
     snapshot; no painel, `PacienteDetalhePage`, `pacientesApi.ts` e o manual `pacientes.tsx`;
   - `8cff73f` verificador do endereço do SISREG: snapshot, `SmsMaisDbContext`,
     `DependencyInjection`, `integracoes/api.ts` e `integracoes/types.ts`.

   A `main` ganhou duas migrations com data **posterior** à nossa
   (`20261009035748_PersonificacaoPacienteSandbox`, já em produção, e `20261009133201_SisregEnderecoIp`).
   Mantenha o timestamp da nossa mesmo assim: o EF aplica uma migration pendente mesmo com outra
   mais nova já aplicada. No snapshot, junte os dois lados e confirme com
   `dotnet ef migrations has-pending-model-changes` (tem de dizer que não há mudanças); depois rode
   os testes `Cidadao.Push`.
2. **Banco:** o banco de produção está sendo migrado para o datacenter da EVEO, e o **psql01** de lá
   recebe uma réplica lógica (publicação `smsmais_eveo`) que **não leva DDL**. Confirme no dia o
   procedimento da migração. Até 09/10 a regra era aplicar o DDL **antes** no psql01
   (`cidadao_sessao` é replicada), incluir `cidadao_notificacao` na publicação e conferir a
   replicação depois.
3. **Publicar:** push na `main` dispara `deploy-server.yml` e `deploy-front.yml` para as duas
   instâncias de produção, **Maricá e CCDTI** (ADR-0043): a migration roda nas duas. Conferir
   `smsmarica.__migrations` e as linhas "Migrations OK." / "Seed do Admin OK." no log do serviço no
   servidor (`journalctl`). O AutoMigrate falha calado, e o log do GitHub Actions só mostra o
   journal quando o deploy falha.
4. Colar o JSON no card do Firebase **da produção** e Testar.
5. Dar o módulo 81 aos perfis que vão enviar.
6. App apontado para a produção, entrar com o **próprio** CPF de quem testa e enviar pela própria ficha.
7. Publicar o app nas lojas é outro projeto (conta Google Play, conta Apple).

### 6. iOS (por último)

Conta Apple Developer paga, Mac com Xcode, iPhone de verdade. Chave APNs `.p8` no Firebase
(Configurações → Cloud Messaging); a Apple agora emite chave separada para Sandbox e Production —
enviar as duas. Preencher `FIREBASE_IOS_*` no `firebase.json`.

## Pendências conhecidas (não bloqueiam o teste)

- No envio de verdade, a API do FCM desligada aparece no aparelho como "credencial recusada" (o
  Testar distingue o caso; o manual lista as causas).
- Resposta do Google que não é JSON vai cortada em 200 caracteres para o detalhe do aparelho.
- O número reserva do WhatsApp no chat do app está no código, copiado do PWA (`chat_page.dart`).
  Pela regra de instância por município (ADR-0043) ele deveria vir da configuração.
- Avisos automáticos (laudo pronto, exame agendado, transporte) não existem: cada gatilho é decisão
  à parte, para não duplicar o WhatsApp (ADR-0070).
- PWA sem push (Web Push pode vir depois com o mesmo modelo).
- O FCM está trocando o token pelo FID; o app migra quando o `firebase_messaging` publicar o
  `register()`, sem mexer no servidor.
