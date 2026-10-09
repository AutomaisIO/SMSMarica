# SMSMais.cidadao.app

## O que é

Versão **nativa (Flutter, Android + iOS)** do **App do Cidadão** — o mesmo app que roda como PWA em
`app.smsmarica.online` ([`SMSMais.cidadao.pwa`](../SMSMais.cidadao.pwa/)). As duas versões têm
**a mesma cara e o mesmo contrato com a API**: o PWA é a referência; mudou lá, muda aqui.

> **Ainda não está em produção** (nem nas lojas). O PWA continua sendo o canal oficial.

## O que o app faz (igual ao PWA)

| Tela | Rota | O que mostra |
|---|---|---|
| Entrar | `/login` → `/login/verificacao` → `/login/codigo` | CPF + código pelo WhatsApp do cadastro; quem não tem WhatsApp verificado confirma nascimento + nº da solicitação |
| Termo LGPD | (antes de tudo) | Aceite do consentimento vigente — bloqueante |
| Início | `/` | Cartão do Cidadão + atalhos (com aviso de exames a confirmar) |
| Consultas / Exames agendados | `/agendados/consultas`, `/agendados/exames` | Agendados e na fila da regulação; confirmar presença / avisar ausência |
| Ticket do exame | `/agendados/exames/:id` | Local, solicitante, protocolo, chave de acesso (só no dia), QR |
| Meus atendimentos | `/atendimentos` | Histórico com documentos clínicos (receituário etc.) |
| Exames | `/exames?exame=:id` | Laudo assinado, PDF das imagens e anexos |
| Documentos | `/documentos` | Acervo completo + envio de foto/PDF (fica "em conferência") |
| Chat | `/chat` | Abre o WhatsApp oficial da Secretaria |
| Transporte (TFD) | `/transporte` | Viagens + "Meus acompanhantes" |
| Meu perfil | `/perfil` | Foto (com recorte), cadastro oficial, contatos, troca de celular por código |
| Links do WhatsApp | `/entrar/:token`, `/documento/:token` | Magic link (com desafio de CPF para resultados) e download de uso único |

Também como o PWA: **offline-first** (listas e documentos ficam no aparelho; rede sempre primeiro,
cache só quando a rede falha; faixa "Sem conexão"), **visualizador de PDF/foto embutido** (zoom,
Baixar, Compartilhar) e **limpeza dos dados do aparelho ao sair** (LGPD).

## Como está organizado

```
lib/
  app/            app, tema, rotas (as mesmas do PWA)
  features/       uma pasta por tela (auth, inicio, agendados, exames, documentos, …)
  shared/
    api/          cliente HTTP, modelos (espelho de src/lib/api.ts) e o modo demonstração
    cache/        cache de dados (shared_preferences) e de documentos (arquivos, LRU 40)
    push/         notificações (Firebase): registro do aparelho, toque → tela, lista fixa de destinos
    sessao/       sessão do cidadão (JWT no armazenamento seguro) e saída com limpeza
    theme/        cores, raios, sombras e tipografia — os tokens do tailwind.config.ts do PWA
    widgets/      kit visual (espelho de src/components/ui.tsx) e o esqueleto de listas
    visualizador/ visualizador de PDF/foto
```

Fontes: **Bricolage Grotesque** (títulos) e **Inter** (texto), empacotadas em `assets/fonts/`
(OFL). Ícones: **Lucide** — os mesmos do PWA.

## Rodar

```bash
flutter pub get
flutter run                                   # API de PRODUÇÃO (padrão, como o PWA) — ver o aviso abaixo
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5080   # backend local — só no EMULADOR Android
flutter run --dart-define=DEMO=true           # dados fictícios, sem rede — para conferir telas
flutter analyze && flutter test
```

**Atenção: `flutter run` sem `API_BASE_URL` fala com a produção** — o login manda um código **de
verdade** pelo WhatsApp do cadastro e o que se faz no app vale. Para conferir telas, `DEMO=true`.

`10.0.2.2` é o PC visto **de dentro do emulador**; num **celular físico** (USB, depuração ligada)
encaminhe a porta e use `localhost`:

```bash
adb reverse tcp:5080 tcp:5080
flutter run --dart-define=API_BASE_URL=http://localhost:5080
```

No modo demonstração qualquer CPF válido entra (ex.: `529.982.247-25`) e qualquer código serve,
menos `000000` (simula código errado).

## Notificações (push)

A equipe manda uma mensagem pela **ficha do paciente** no painel e ela chega ao celular pelo
**Firebase Cloud Messaging** (no iPhone o Firebase entrega pela Apple, o APNs). O aparelho pertence
à **sessão**: depois do login e do termo aceito o app registra o aparelho
(`PUT /auth/paciente/dispositivo`); ao sair, o servidor apaga o token junto com a sessão e o app
pede ao Firebase que esqueça o aparelho (`deleteToken`).

O Firebase é iniciado **só pelo Dart**, com os valores passados no build — o projeto **não** usa
`google-services.json`, `GoogleService-Info.plist` nem o plugin gradle `google-services`. Cada
instância (prefeitura) tem o seu projeto no Firebase: nada disso fica no código. Por isso **não
rode `flutterfire configure`** na pasta do app: a FlutterFire CLI grava um `firebase.json` próprio
na raiz (sobrescreve o nosso), gera o `lib/firebase_options.dart` e o `google-services.json` (os
três estão no `.gitignore`) e liga o plugin `google-services` nos arquivos gradle — se rodou por
engano, apague o que ela gerou e desfaça a mudança nos gradle.

### 1. Projeto no Firebase (uma vez por instância)

1. Em `console.firebase.google.com`, crie o projeto (o Google Analytics não é necessário).
2. Adicione um app **Android** com o pacote `io.automais.smsmais.cidadao`. **Não** use o `google-services.json` que ele oferece: anote o **ID do app** em *Configurações do projeto → Seus apps* e, na aba *Geral*, o **ID do projeto** e o **número do projeto** (o código do remetente). A **chave de API Android não aparece no cartão do app** (a "Chave de API da Web" da aba *Geral* é outra). Pegue-a por um destes caminhos: Google Cloud → *APIs e serviços → Credenciais* → **Android key (auto created by Firebase)**; ou `firebase apps:sdkconfig ANDROID <ID do app>` (só imprime na tela); ou baixe o `google-services.json` **fora do repositório**, copie os valores e apague o arquivo.
3. Adicione um app **iOS** com o bundle `io.automais.smsmais.cidadao`. Do mesmo jeito: **não** use o `GoogleService-Info.plist`; anote a chave de API e o ID do app iOS.
4. **Chave da Apple (APNs), só para o iPhone.** No Apple Developer (*Certificates, Identifiers & Profiles → Keys*), crie a chave com *Apple Push Notifications service (APNs)*. Hoje a Apple emite a chave *team-scoped* para **Sandbox ou Production**, não as duas: crie uma de cada, baixe cada `.p8` (a Apple só deixa baixar uma vez) e anote os Key IDs e o Team ID. No Firebase, em *Configurações do projeto → Cloud Messaging → app iOS*, envie **as duas** — a de Sandbox como *desenvolvimento*, a de Production como *produção* — com o Key ID e o Team ID. Sem isso o iPhone não recebe nada e o painel mostra "O Firebase recusou a chave da Apple (APNs)".
5. **Conta de serviço (para o servidor enviar).** Em *Configurações do projeto → Contas de serviço*, clique em *Gerar nova chave privada*: baixa um JSON — do **mesmo projeto** do `firebase.json` (senão o envio acusa "outro projeto do Firebase"). No painel, em **Integrações**, abra o cartão **Firebase — notificações do app do cidadão**, cole o JSON **inteiro** (o ID do projeto é lido dele), deixe marcado *Envio de notificações ativo*, clique em **Salvar** e depois em **Testar** (confere se o servidor autentica no Firebase, sem enviar nada). O JSON é a única peça secreta: não vai para o repositório nem para conversa; depois de colar, apague o arquivo baixado.

**Se o Google Cloud recusar a chave da conta de serviço** ("Key creation is not allowed on this
service account"): organizações criadas desde 03/05/2024 (e contas Workspace) aplicam a política
`iam.managed.disableServiceAccountKeyCreation`. Quem tem o papel *Organization Policy
Administrator* abre uma exceção só para este projeto.

### 2. Montar o `firebase.json` e buildar

O `firebase.json` fica **fora do git** (está no `.gitignore`); o modelo versionado é o
`firebase.exemplo.json`:

```bash
cp firebase.exemplo.json firebase.json        # e preencha com os valores do passo 1
flutter run --dart-define-from-file=firebase.json
flutter build apk --release --dart-define-from-file=firebase.json
flutter build ipa --dart-define-from-file=firebase.json        # no Mac
```

| Chave | De onde vem |
|---|---|
| `FIREBASE_PROJECT_ID` | ID do projeto |
| `FIREBASE_SENDER_ID` | Número do projeto (código do remetente) |
| `FIREBASE_ANDROID_API_KEY`, `FIREBASE_ANDROID_APP_ID` | App Android: chave de API e ID do app (`1:…:android:…`) |
| `FIREBASE_IOS_API_KEY`, `FIREBASE_IOS_APP_ID` | App iOS: chave de API e ID do app (`1:…:ios:…`) |
| `FIREBASE_IOS_BUNDLE_ID` | Opcional: o bundle do app iOS |

Esses valores são identificadores públicos (vão dentro do app instalado), não segredos. Dá para
combinar com os outros defines (`--dart-define=API_BASE_URL=…`).

No iOS o projeto já vem pronto: alvo mínimo **iOS 15** (exigência do Firebase), capability de push
(`Runner/Runner.entitlements` com `aps-environment = development` — o Xcode troca para
`production` ao distribuir) e *Background Modes → Remote notifications*. Na conta da Apple o App ID
precisa da capability *Push Notifications* (a assinatura automática do Xcode liga). O **simulador
não recebe push** (não tem token APNs): teste num iPhone.

**Testar com o app fechado (Samsung):** em *Bateria* do app, deixe **Sem restrições**, e feche o
app pelos recentes — **não** use *Forçar parada*: app parado à força não recebe push.

### 3. O que fica desligado sem configuração

Sem o `firebase.json` (ou com valores vazios), no **modo demonstração** (`DEMO=true`) e na **web**, o
push fica desligado: o app não inicia o Firebase, não pede permissão de notificação e funciona igual.
No painel, o paciente aparece sem aparelho com notificações ativas.

### Como o app se comporta

- **Permissão:** pedida depois do login e do termo aceito, uma vez por abertura do app (e de novo se o cidadão sair e entrar). Se o registro falhar (API fora, sem rede), o app tenta de novo quando volta ao primeiro plano. Se o cidadão negar, o aparelho não é registrado e o pedido não se repete até a próxima abertura ou o próximo login (e aí quem decide se mostra o diálogo é o sistema: o iPhone só pergunta uma vez; o Android, no máximo duas); se ele liberar nas configurações do celular e voltar ao app, o registro sai na hora. Se ele desligar as notificações depois, nas configurações do celular, na próxima abertura o app pede ao Firebase que esqueça o token: o envio seguinte volta como "app desinstalado" e o servidor tira o aparelho da sessão (até o app ser aberto de novo, o painel ainda mostra o aparelho e o envio aparece como entregue).
- **App aberto:** no Android a mensagem vira notificação local no canal "Avisos"; no iPhone o próprio sistema mostra. Com o app fechado, o sistema mostra sozinho (ícone pequeno branco, cor da marca, canal "Avisos").
- **Toque:** abre a tela escolhida em "Abrir no app" **só se ela estiver na lista fixa** (`lib/shared/push/destinos.dart` — a mesma do servidor e do painel); sem tela escolhida (o "Início" do painel), abre o Início; com uma rota fora da lista, só abre o app.
- **Falha de push nunca derruba o app** nem mostra erro ao cidadão: tudo é best-effort.

### Privacidade (ler antes de escrever uma notificação)

O texto da notificação aparece na **tela bloqueada** e passa pelos servidores do Google (e da Apple,
no iPhone) sem criptografia de ponta a ponta. **Nunca** escreva dado de saúde — exame, resultado,
diagnóstico, medicamento, especialidade. Use texto genérico ("Você tem uma atualização no app") e
deixe o conteúdo dentro do app, atrás do login. A mensagem leva só a rota e o id da notificação; o
servidor guarda o histórico de cada envio (quem, quando, para quem, o texto).

## Pendências para publicar

- **Links do WhatsApp abrirem direto no app:** publicar `/.well-known/assetlinks.json` (Android)
  e `apple-app-site-association` (iOS, com Associated Domains no projeto) em
  `app.smsmarica.online`, com a impressão digital da chave de assinatura. Sem isso, o Android
  pergunta "abrir com" e o iOS abre no navegador (o PWA).
- **Assinatura de release** (Android) e conta de desenvolvedor (lojas).
- **Push:** criar o projeto no Firebase e montar o `firebase.json` do build de loja (seção acima). O Firebase está trocando o token de registro pelo FID (Firebase Installation ID); o app ainda usa `getToken`/`onTokenRefresh` e o servidor envia em `message.token`, que aceita os dois. Quando o FlutterFire publicar `register()`/`onRegistered`, migrar.
