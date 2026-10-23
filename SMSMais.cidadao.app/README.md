# SMSMais.cidadao.app

## O que é

Versão **nativa (Flutter, Android + iOS)** do **App do Cidadão** — o mesmo app que roda como PWA em
`app.smsmarica.online` ([`SMSMais.cidadao.pwa`](../SMSMais.cidadao.pwa/)). As duas versões têm
**a mesma cara e o mesmo contrato com a API**: o PWA é a referência; mudou lá, muda aqui.

> **Ainda não está em produção** (nem nas lojas). O PWA continua sendo o canal oficial.

## O que o app faz (igual ao PWA)

| Tela | Rota | O que mostra |
|---|---|---|
| Entrar | `/login` → `/login/telefone` → `/login/codigo` | CPF + data de nascimento; a pessoa escolhe o seu WhatsApp entre os números que a Saúde já conhece (só o final aparece) e recebe o código nele — nunca num número digitado. "Nenhum desses é meu" procura no e-SUS (se ligado para o canal) ou orienta ir ao posto ([ADR-0078](../docs/adr/0078-entrada-cidadao-cpf-nascimento.md)) |
| Termo LGPD | (antes de tudo) | Aceite do consentimento vigente — bloqueante |
| Início | `/` | Cartão do Cidadão + atalhos (com aviso de exames a confirmar); no dia da viagem, o convite para compartilhar a localização |
| Consultas / Exames agendados | `/agendados/consultas`, `/agendados/exames` | Agendados e na fila da regulação; confirmar presença / avisar ausência |
| Ticket do exame | `/agendados/exames/:id` | Local, solicitante, protocolo, chave de acesso (só no dia), QR |
| Meus atendimentos | `/atendimentos` | Histórico com documentos clínicos (receituário etc.) |
| Exames | `/exames?exame=:id` | Laudo assinado, PDF das imagens e anexos |
| Documentos | `/documentos` | Acervo completo + envio de foto/PDF (fica "em conferência") |
| Chat | `/chat` | Abre o WhatsApp oficial da Secretaria |
| Transporte (TFD) | `/transporte` | Viagens + "Meus acompanhantes"; no dia da viagem, compartilhar a localização com o motorista |
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
    localizacao/  localização no dia da viagem: o controle (espelho do store/localizacao.ts) e o GPS
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

No modo demonstração qualquer CPF válido (ex.: `529.982.247-25`) e qualquer data de nascimento
entram; aparecem dois números fictícios "do cadastro", e "Nenhum desses é meu" → "Procurar outros
números" acrescenta um "do e-SUS". Qualquer código serve, menos `000000` (simula código errado).

Toda chamada leva o cabeçalho `X-App-Canal: nativo` (o PWA manda `pwa`): é por ele que o servidor
sabe se a busca do telefone no e-SUS está ligada para este canal (Sistema → App do cidadão).

## Localização no dia da viagem

O paciente pode compartilhar a posição do celular com o motorista enquanto espera um embarque de hoje
([ADR-0082](../docs/adr/0082-localizacao-do-paciente-no-transporte.md)), igual ao PWA:

- **Quando aparece:** o servidor diz (`GET /auth/paciente/localizacao`) se há uma espera agora: a ida, a
  partir de 2 h antes da busca, ou o resgate, depois de descer na unidade. O app consulta a cada minuto,
  só com o app na tela. Fora disso, nada aparece.
- **Aceite por viagem:** o convite ("Ajude o motorista a achar você"), no Início e no Transporte, explica
  quem vê, quando para e que só envia com o app aberto. Só depois do **Compartilhar** o app registra o
  aceite (`POST`) e o Android/iOS pede a permissão.
- **Só com o app aberto:** permissão "durante o uso" (`ACCESS_FINE_LOCATION` e `ACCESS_COARSE_LOCATION`;
  no iOS, `NSLocationWhenInUseUsageDescription`). **Sem segundo plano:** não há
  `ACCESS_BACKGROUND_LOCATION`. O app saiu da tela, o GPS desliga; voltou, liga de novo sem perguntar.
- **Duas armadilhas do Android (achadas no emulador, os testes de unidade não pegam):**
  - o `GeolocatorLocationService` do plugin **tem de ficar** no manifesto: sem ele o fluxo de posições
    não começa (no logcat: `Location background service has not started correctly`). Ele fica sem o
    `foregroundServiceType` (`tools:remove`); só é vinculado, nunca vai para o primeiro plano, e a Play
    não pede a declaração de serviço em primeiro plano. Conferir no manifesto final
    (`build/app/intermediates/merged_manifest/`): nenhum `foregroundServiceType` nem `FOREGROUND_SERVICE`;
  - com a "Precisão do local" do Google desligada, o cliente do Google Play Services mostra o aviso do
    Google ao ligar o GPS. Recusado, o plugin devolve erro e o app passaria a mostrar o aviso a cada
    consulta. Por isso o `GpsDoAparelho` segue pelo LocationManager do Android
    (`forceLocationManager`) até o app fechar.
- **Envio:** no máximo a cada 15 s, ou antes disso se andou 20 m, e nunca antes de 10 s (`PUT`). Vai
  junto o aviso do Android de posição simulada (app de GPS falso). Com a permissão só aproximada, envia
  assim mesmo e a faixa avisa.
- **Para:** no embarque (o servidor responde 409 e o app desliga sozinho), no **Parar** da faixa
  (`DELETE`) e no Sair. Permissão negada ou localização desligada: a faixa explica e oferece **Permitir**
  ou as configurações.
- **Não atrapalha o resto:** servidor sem a função (404) esconde tudo e para de consultar; qualquer
  outro erro só esconde ou espera a próxima consulta.
- **Demonstração:** o `DemoAdapter` simula o resgate de hoje na janela e guarda as posições no próprio
  aparelho (nada vai à rede). O pedido de permissão é o de verdade, para a revisão das lojas ver.

O controle fica em `lib/shared/localizacao/` (o GPS atrás da interface `FonteGps`, que os testes trocam
por um falso); o cartão e a faixa, em `lib/features/transporte/`. Testes: `test/localizacao_test.dart`.

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

## Publicar na Play Store

### Chave de upload (uma vez)

A Play Store guarda a chave que assina o app para os aparelhos (**Assinatura de apps do Google
Play**, ligada no primeiro envio); a nossa é só a **chave de upload**, que prova à Play que o pacote
veio de nós. Ela fica **fora do repositório** e com cópia num cofre de senhas — sem ela não sai
versão nova (perder se resolve pelo Play Console, com espera).

Gere num PowerShell aberto por você (o `keytool` pede a senha na hora; ela não vai para lugar nenhum):

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\Documents\Chaves" | Out-Null
& "C:\Program Files\Android\Android Studio\jbr\bin\keytool.exe" -genkeypair -v `
  -keystore "$env:USERPROFILE\Documents\Chaves\smsmais-cidadao-upload.jks" -storetype PKCS12 `
  -keyalg RSA -keysize 2048 -validity 10000 -alias upload `
  -dname "CN=AutoMais Automacao Industrial, O=AutoMais, C=BR"
```

E crie `android/key.properties` (no `.gitignore`; PKCS12 usa a mesma senha nos dois campos):

```properties
storeFile=C:/Users/<você>/Documents/Chaves/smsmais-cidadao-upload.jks
keyAlias=upload
storePassword=<a senha>
keyPassword=<a senha>
```

Sem o `key.properties` (ou as variáveis `CIDADAO_*`), o build de release **para com erro** — de
propósito: pacote assinado com a chave de debug a loja recusa.

### Acesso para a revisão das lojas ("Detalhes do login")

O revisor da Google Play (e da App Store) não tem CPF nem WhatsApp do cadastro. Para ele, a tela de
entrada aceita uma **entrada de demonstração**, que liga o modo demonstração (`DemoAdapter`: dados
fictícios, sem rede, sem tocar a produção) — o mesmo do `--dart-define=DEMO=true`:

| Campo | Valor |
|---|---|
| CPF | `111.111.111-11` |
| Data de nascimento | `01/01/2000` |
| Número | qualquer um da lista |
| Código | qualquer código de 6 dígitos (ex.: `123456`; só `000000` é recusado) |

O CPF tem os 11 dígitos iguais: é inválido por regra, então nunca é de ninguém, e o app não o manda
ao servidor. Sair da conta desliga a demonstração. Estes dados podem ir no Play Console sem risco: com
eles não se chega a nada real.

Para a revisão ver a localização: no Início, **Ajude o motorista a achar você → Compartilhar minha
localização → Compartilhar**. O aparelho pede a permissão e a faixa "Compartilhando sua localização
com o motorista" aparece, com o **Parar**. Na demonstração, a posição não sai do aparelho.

### Exclusão da conta e Segurança dos dados

A Play exige, de app em que se entra com conta, um jeito de **pedir a exclusão** dentro do app e fora
dele. Fora: a página pública
[`/exclusao-de-conta/`](../SMSMais.cidadao.pwa/public/exclusao-de-conta/index.html) do PWA (é a "URL para
exclusão de contas" da Segurança dos dados). Dentro: **Meu perfil → Excluir minha conta**, no app e no
PWA, que abre essa página. O pedido vai por e-mail ao DPO (`contato@smsmarica.online`), com resposta em
até 15 dias; apaga o acesso ao app (sessões e aparelhos do push), a foto e os acompanhantes, e o
prontuário fica, por obrigação legal. Mudou o processo, mudam a página e a declaração juntas.

Na Segurança dos dados, o declarado é o que o app **manda** ao servidor: CPF e nascimento (seus e dos
acompanhantes), e-mail e telefones do Perfil, foto, exames enviados (arquivo e foto), confirmação e
cancelamento de exame, avaliação da viagem, o token do push e, desde a 1.0.2, a **localização exata**
(coletada, não compartilhada, opcional, para a funcionalidade do app: só no dia da viagem, depois do
Compartilhar, e apagada no embarque). Nada é "compartilhado" no sentido da Play (WhatsApp e Firebase são
prestadores de serviço; o motorista é o próprio serviço de transporte); não há ID de anúncio nem
relatório de falhas. Dependência nova que colete dado (ex.: Analytics, Crashlytics) muda essa
declaração.

### Gerar o pacote da loja

```powershell
flutter build appbundle --release --dart-define-from-file=firebase.json --obfuscate --split-debug-info=build/app/simbolos
```

Sai em `build/app/outputs/bundle/release/app-release.aab`. A versão vem do `pubspec.yaml`
(`version: <nome>+<código>`): cada envio à Play precisa de um **código maior** que o anterior.

O `--obfuscate` embaralha os nomes do código Dart no pacote (dificulta a engenharia reversa). Os
símbolos para ler uma pilha de erro ficam em `build/app/simbolos`: **guarde a pasta de cada versão**
fora do repositório, junto da cópia da chave. Sem ela, a pilha daquela versão não se lê
(`flutter symbolize -i <pilha> -d build/app/simbolos/app.android-arm64.symbols`).

### Segurança do app

Do pentest VULTUS (fev/2026, projeto Aeromais) e da revisão de 23/10/2026:

- **Backup desligado** (`allowBackup`, `fullBackupContent`, `dataExtractionRules`): o cache offline
  tem dados de saúde e não vai ao backup do Google nem passa para outro aparelho.
- **Só HTTPS no build de loja:** `exigirHttpsNoBuildDeLoja()` (em `lib/shared/api/http.dart`) impede
  o app de abrir se a API não for `https`. Essa trava é necessária porque o cliente HTTP do Dart não
  obedece ao `network_security_config.xml`. O config continua valendo para as bibliotecas nativas e
  aceita só certificados do sistema (o debug libera HTTP e certificados do usuário).
- **Sessão no cofre do aparelho** (`flutter_secure_storage`). O cache é limpo ao sair da conta.
- **Pendente:**
  - **Play Integrity** conferido no servidor, no lugar de detecção caseira de root/emulador. Precisa
    ligar um projeto do Google Cloud no Play Console.
  - **iOS:** excluir o cache do backup do iCloud antes da App Store.

## Pendências para publicar

- **Links do WhatsApp abrirem direto no app:** publicar `/.well-known/assetlinks.json` (Android)
  e `apple-app-site-association` (iOS, com Associated Domains no projeto) em
  `app.smsmarica.online`, com a impressão digital da chave de assinatura. Sem isso, o Android
  pergunta "abrir com" e o iOS abre no navegador (o PWA).
- **Push:** criar o projeto no Firebase e montar o `firebase.json` do build de loja (seção acima). O Firebase está trocando o token de registro pelo FID (Firebase Installation ID); o app ainda usa `getToken`/`onTokenRefresh` e o servidor envia em `message.token`, que aceita os dois. Quando o FlutterFire publicar `register()`/`onRegistered`, migrar.
