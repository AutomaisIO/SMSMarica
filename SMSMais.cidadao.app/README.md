# SMSMais.cidadao.app

## O que é

Versão **nativa (Flutter)** do **App do Cidadão**, o "Saúde Maricá", para **Android e iPhone**. É o
mesmo app que roda como PWA em `app.smsmarica.online` ([`SMSMais.cidadao.pwa`](../SMSMais.cidadao.pwa/)).
As versões têm **a mesma cara e o mesmo contrato com a API**: o PWA é a referência; mudou lá, muda
aqui.

**Um código só para as duas plataformas** (`lib/`). O que é de cada uma (projeto nativo, assinatura,
build, loja) fica separado:

| | Android | iPhone |
|---|---|---|
| Projeto nativo | [`android/`](android/) | [`ios/`](ios/) |
| Build, assinatura e publicação | [`docs/android.md`](docs/android.md) | [`docs/ios.md`](docs/ios.md) |
| Página da loja | [`loja/google-play/`](loja/google-play/README.md) | [`loja/app-store/`](loja/app-store/README.md) |
| Situação (24/10/2026) | **Teste interno** da Play, versão 1.0.4 (7) | **Começando**: conta Apple em aprovação |

Até o app sair nas lojas, o PWA continua sendo o canal oficial.

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
lib/              código Dart, comum às duas plataformas
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
test/             testes (valem para as duas plataformas)
assets/           fontes e logo da entrada
android/          projeto nativo Android (manifesto, Gradle, assinatura) → docs/android.md
ios/              projeto nativo iOS (Info.plist, Xcode, entitlements) → docs/ios.md
docs/             o passo a passo de cada plataforma: android.md e ios.md
loja/             as páginas das lojas e a marca: marca/ (comum), google-play/ e app-store/
```

**Código de uma plataforma só:** as telas são as mesmas nas duas. Quando o comportamento muda (os
ajustes do GPS, as opções do Firebase, o canal de notificação do Android), a escolha pela plataforma
(`defaultTargetPlatform`) fica dentro de `lib/shared/` (por exemplo, `shared/localizacao/gps.dart` e
`shared/push/push.dart`), nunca em `features/`.

**CI:** o [`ci-cidadao-app.yml`](../.github/workflows/ci-cidadao-app.yml) roda a cada PR que mexe no
app, com três jobs: análise e testes, build de Android e build de iOS. Os três são gratuitos, porque
o repositório é público, e nenhum publica nada (detalhes em cada `docs/`).

Fontes: **Bricolage Grotesque** (títulos) e **Inter** (texto), empacotadas em `assets/fonts/`
(OFL). Ícones: **Lucide**, os mesmos do PWA.

**Marca:** o logo da tela de entrada (`assets/brand/marica_logo.png`) é o da **Secretaria de Saúde**,
versão colorida, que a Comunicação mandou em 21/10/2026. O ícone do app e a abertura usam o símbolo
**"MA RI CÁ"** sobre o laranja `#FF2A00`. O símbolo veio de Sistema → Instituição e foi corrigido; ele
e o gerador dos ícones das duas plataformas (e do PWA) ficam em
[`loja/marca/`](loja/marca/README.md).

## Versão

Uma versão para as duas plataformas, no `pubspec.yaml`: `version: <nome>+<código>` (ex.:
`1.0.4+7`). O nome é o que a pessoa vê; o código precisa ser **maior a cada envio** em cada loja.
Mudou o app, sobe a versão, mesmo que o envio a uma das lojas fique para depois.

## Rodar

```bash
flutter pub get
flutter run                                   # API de PRODUÇÃO (padrão, como o PWA) — ver o aviso abaixo
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5080   # backend local — só no EMULADOR Android
flutter run --dart-define=DEMO=true           # dados fictícios, sem rede — para conferir telas
flutter analyze && flutter test
```

**Atenção: `flutter run` sem `API_BASE_URL` fala com a produção.** O login manda um código **de
verdade** pelo WhatsApp do cadastro, e o que se faz no app vale. Para conferir telas, use `DEMO=true`.

`10.0.2.2` é o PC visto **de dentro do emulador**. Num **celular Android físico** (USB, depuração
ligada), encaminhe a porta e use `localhost`:

```bash
adb reverse tcp:5080 tcp:5080
flutter run --dart-define=API_BASE_URL=http://localhost:5080
```

No **iPhone**, sem Mac aqui, o build é na nuvem: ver [`docs/ios.md`](docs/ios.md).

No modo demonstração qualquer CPF válido (ex.: `529.982.247-25`) e qualquer data de nascimento
entram; aparecem dois números fictícios "do cadastro", e "Nenhum desses é meu" → "Procurar outros
números" acrescenta um "do e-SUS". Qualquer código serve, menos `000000` (simula código errado).

Toda chamada leva o cabeçalho `X-App-Canal: nativo` (o PWA manda `pwa`): é por ele que o servidor
sabe se a busca do telefone no e-SUS está ligada para este canal (Sistema → App do cidadão).

## Localização no dia da viagem

O paciente pode compartilhar a posição do celular com o motorista enquanto espera um embarque de hoje
([ADR-0082](../docs/adr/0082-localizacao-do-paciente-no-transporte.md)), igual ao PWA:

- **Quando aparece:** o servidor diz (`GET /auth/paciente/localizacao`) se há uma espera agora: a ida,
  a partir de 2 h antes da busca, ou o resgate, depois de descer na unidade. O app consulta a cada
  minuto, só com o app na tela. Fora disso, nada aparece.
- **Aceite por viagem:** o convite ("Ajude o motorista a achar você"), no Início e no Transporte,
  explica quem vê, quando para e que só envia com o app aberto. Só depois do **Compartilhar** o app
  registra o aceite (`POST`) e o Android/iOS pede a permissão.
- **Só com o app aberto:** permissão "durante o uso" (no Android, `ACCESS_FINE_LOCATION` e
  `ACCESS_COARSE_LOCATION`; no iOS, `NSLocationWhenInUseUsageDescription`). **Sem segundo plano.** O
  app saiu da tela, o GPS desliga; voltou, liga de novo sem perguntar.
- **Envio:** no máximo a cada 15 s, ou antes disso se andou 20 m, e nunca antes de 10 s (`PUT`). Vai
  junto o aviso do Android de posição simulada (app de GPS falso). Com a permissão só aproximada,
  envia assim mesmo e a faixa avisa.
- **Para:** no embarque (o servidor responde 409 e o app desliga sozinho), no **Parar** da faixa
  (`DELETE`) e no Sair. Permissão negada ou localização desligada: a faixa explica e oferece
  **Permitir** ou as configurações.
- **Não atrapalha o resto:** servidor sem a função (404) esconde tudo e para de consultar; qualquer
  outro erro só esconde ou espera a próxima consulta.
- **Demonstração:** o `DemoAdapter` simula o resgate de hoje na janela e guarda as posições no próprio
  aparelho (nada vai à rede). O pedido de permissão é o de verdade, para a revisão das lojas ver.
- **No Android**, o GPS não é exigido do aparelho, e o plugin tem duas armadilhas: ver
  [`docs/android.md`](docs/android.md#localização).

O controle fica em `lib/shared/localizacao/` (o GPS atrás da interface `FonteGps`, que os testes
trocam por um falso); o cartão e a faixa, em `lib/features/transporte/`. Testes:
`test/localizacao_test.dart`.

## Notificações (push)

A equipe manda uma mensagem pela **ficha do paciente** no painel e ela chega ao celular pelo
**Firebase Cloud Messaging** (no iPhone, o Firebase entrega pela Apple, o APNs). O aparelho pertence
à **sessão**: depois do login e do termo aceito o app registra o aparelho
(`PUT /auth/paciente/dispositivo`); ao sair, o servidor apaga o token junto com a sessão e o app pede
ao Firebase que esqueça o aparelho (`deleteToken`).

O Firebase é iniciado **só pelo Dart**, com os valores passados no build. O projeto **não** usa
`google-services.json`, `GoogleService-Info.plist` nem o plugin gradle `google-services`. Cada
instância (prefeitura) tem o seu projeto no Firebase: nada disso fica no código. Por isso **não rode
`flutterfire configure`** na pasta do app. A FlutterFire CLI grava um `firebase.json` próprio na raiz
(sobrescreve o nosso), gera o `lib/firebase_options.dart` e o `google-services.json` (os três estão
no `.gitignore`) e liga o plugin `google-services` nos arquivos gradle. Se rodou por engano, apague o
que ela gerou e desfaça a mudança nos gradle.

### 1. Projeto no Firebase (uma vez por instância)

1. Em `console.firebase.google.com`, crie o projeto (o Google Analytics não é necessário).
2. Adicione um app **Android** com o pacote `io.automais.smsmais.cidadao`. **Não** use o `google-services.json` que ele oferece: anote o **ID do app** em *Configurações do projeto → Seus apps* e, na aba *Geral*, o **ID do projeto** e o **número do projeto** (o código do remetente). A **chave de API Android não aparece no cartão do app** (a "Chave de API da Web" da aba *Geral* é outra). Pegue-a por um destes caminhos: Google Cloud → *APIs e serviços → Credenciais* → **Android key (auto created by Firebase)**; ou `firebase apps:sdkconfig ANDROID <ID do app>` (só imprime na tela); ou baixe o `google-services.json` **fora do repositório**, copie os valores e apague o arquivo.
3. **iPhone:** o app iOS no Firebase e as chaves da Apple (APNs) estão em [`docs/ios.md`](docs/ios.md#push).
4. **Conta de serviço (para o servidor enviar).** Em *Configurações do projeto → Contas de serviço*, clique em *Gerar nova chave privada*: baixa um JSON — do **mesmo projeto** do `firebase.json` (senão o envio acusa "outro projeto do Firebase"). No painel, em **Integrações**, abra o cartão **Firebase — notificações do app do cidadão**, cole o JSON **inteiro** (o ID do projeto é lido dele), deixe marcado *Envio de notificações ativo*, clique em **Salvar** e depois em **Testar** (confere se o servidor autentica no Firebase, sem enviar nada). O JSON é a única peça secreta: não vai para o repositório nem para conversa; depois de colar, apague o arquivo baixado.

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
```

O pacote de cada loja sai com o mesmo `--dart-define-from-file=firebase.json` (ver
[`docs/android.md`](docs/android.md) e [`docs/ios.md`](docs/ios.md)).

| Chave | De onde vem |
|---|---|
| `FIREBASE_PROJECT_ID` | ID do projeto |
| `FIREBASE_SENDER_ID` | Número do projeto (código do remetente) |
| `FIREBASE_ANDROID_API_KEY`, `FIREBASE_ANDROID_APP_ID` | App Android: chave de API e ID do app (`1:…:android:…`) |
| `FIREBASE_IOS_API_KEY`, `FIREBASE_IOS_APP_ID` | App iOS: chave de API e ID do app (`1:…:ios:…`) |
| `FIREBASE_IOS_BUNDLE_ID` | Opcional: o bundle do app iOS |

Esses valores são identificadores públicos (vão dentro do app instalado), não segredos. Dá para
combinar com os outros defines (`--dart-define=API_BASE_URL=…`).

### 3. O que fica desligado sem configuração

Sem o `firebase.json` (ou com valores vazios), no **modo demonstração** (`DEMO=true`) e na **web**, o
push fica desligado: o app não inicia o Firebase, não pede permissão de notificação e funciona igual.
No painel, o paciente aparece sem aparelho com notificações ativas.

### Como o app se comporta

- **Permissão:** pedida depois do login e do termo aceito, uma vez por abertura do app (e de novo se o cidadão sair e entrar). Se o registro falhar (API fora, sem rede), o app tenta de novo quando volta ao primeiro plano. Se o cidadão negar, o aparelho não é registrado e o pedido não se repete até a próxima abertura ou o próximo login (e aí quem decide se mostra o diálogo é o sistema: o iPhone só pergunta uma vez; o Android, no máximo duas); se ele liberar nas configurações do celular e voltar ao app, o registro sai na hora. Se ele desligar as notificações depois, nas configurações do celular, na próxima abertura o app pede ao Firebase que esqueça o token: o envio seguinte volta como "app desinstalado" e o servidor tira o aparelho da sessão (até o app ser aberto de novo, o painel ainda mostra o aparelho e o envio aparece como entregue).
- **App aberto e fechado:** cada plataforma mostra do seu jeito (ver [`docs/android.md`](docs/android.md#push) e [`docs/ios.md`](docs/ios.md#push)).
- **Toque:** abre a tela escolhida em "Abrir no app" **só se ela estiver na lista fixa** (`lib/shared/push/destinos.dart` — a mesma do servidor e do painel); sem tela escolhida (o "Início" do painel), abre o Início; com uma rota fora da lista, só abre o app.
- **Falha de push nunca derruba o app** nem mostra erro ao cidadão: tudo é best-effort.

### Privacidade (ler antes de escrever uma notificação)

O texto da notificação aparece na **tela bloqueada** e passa pelos servidores do Google (e da Apple,
no iPhone) sem criptografia de ponta a ponta. **Nunca** escreva dado de saúde: exame, resultado,
diagnóstico, medicamento, especialidade. Use texto genérico ("Você tem uma atualização no app") e
deixe o conteúdo dentro do app, atrás do login. A mensagem leva só a rota e o id da notificação; o
servidor guarda o histórico de cada envio (quem, quando, para quem, o texto).

## Revisão das lojas

O revisor da Google Play e o da App Store não têm CPF nem WhatsApp do cadastro. Para eles, a tela de
entrada aceita uma **entrada de demonstração**, que liga o modo demonstração (`DemoAdapter`: dados
fictícios, sem rede, sem tocar a produção), o mesmo do `--dart-define=DEMO=true`:

| Campo | Valor |
|---|---|
| CPF | `111.111.111-11` |
| Data de nascimento | `01/01/2000` |
| Número | qualquer um da lista |
| Código | qualquer código de 6 dígitos (ex.: `123456`; só `000000` é recusado) |

O CPF tem os 11 dígitos iguais: é inválido por regra, então nunca é de ninguém, e o app não o manda
ao servidor. Sair da conta desliga a demonstração. Estes dados podem ir para o Play Console e o App
Store Connect sem risco: com eles não se chega a nada real.

Para a revisão ver a localização: no Início, **Ajude o motorista a achar você → Compartilhar minha
localização → Compartilhar**. O aparelho pede a permissão e a faixa "Compartilhando sua localização
com o motorista" aparece, com o **Parar**. Na demonstração, a posição não sai do aparelho.

## Exclusão da conta

As duas lojas exigem, de app em que se entra com conta, um jeito de **pedir a exclusão** dentro do
app. A Play exige também fora dele.

- **Fora do app:** a página pública
  [`/exclusao-de-conta/`](../SMSMais.cidadao.pwa/public/exclusao-de-conta/index.html) do PWA.
- **Dentro do app:** **Meu perfil → Excluir minha conta**, no app e no PWA, que abre essa página.
- **O pedido:** vai por e-mail ao DPO (`contato@smsmarica.online`), com resposta em até 15 dias.
  - **O que apaga:** o acesso ao app (sessões e aparelhos do push), a foto e os acompanhantes.
  - **O que fica:** o prontuário, por obrigação legal.

Mudou o processo, mudam a página e as declarações de cada loja: a Segurança dos dados da Play
([`docs/android.md`](docs/android.md#segurança-dos-dados)) e a Privacidade do app da Apple
([`docs/ios.md`](docs/ios.md#app-store)).

## Segurança do app

Do pentest VULTUS (fev/2026, projeto Aeromais) e da revisão de 23/10/2026. Vale para as duas
plataformas:

- **Só HTTPS no build de loja:** `exigirHttpsNoBuildDeLoja()` (em `lib/shared/api/http.dart`) impede
  o app de abrir se a API não for `https`. Essa trava é necessária porque o cliente HTTP do Dart não
  obedece à configuração de rede de cada plataforma.
- **Sessão no cofre do aparelho** (`flutter_secure_storage`). O cache é limpo ao sair da conta.
- **Ofuscação** do código Dart nos pacotes de loja (`--obfuscate`), com os símbolos guardados fora
  do repositório.

Cada plataforma tem a sua parte:

- **Android:** backup desligado e configuração de rede ([`docs/android.md`](docs/android.md)). Pendente:
  Play Integrity.
- **iPhone:** pendente tirar o cache do backup do iCloud ([`docs/ios.md`](docs/ios.md)).

## Pendências comuns

- **Links do WhatsApp abrirem direto no app:** `assetlinks.json` (Android) e
  `apple-app-site-association` (iOS) em `app.smsmarica.online`, depois de o app sair nas lojas.
  Detalhes em cada `docs/`.
- **Push:** o Firebase está trocando o token de registro pelo FID (Firebase Installation ID). O app
  ainda usa `getToken`/`onTokenRefresh`, e o servidor envia em `message.token`, que aceita os dois.
  Quando o FlutterFire publicar `register()`/`onRegistered`, migrar.
