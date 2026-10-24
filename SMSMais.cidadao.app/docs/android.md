# Android — projeto, build, assinatura e Google Play

O que é só do Android no app do cidadão ("Saúde Maricá", pacote `io.automais.smsmais.cidadao`). O que
vale para as duas plataformas — telas, push, conta de demonstração, exclusão da conta, versão — está
no [README](../README.md). O iPhone está em [`ios.md`](ios.md).

**Situação (24/10/2026):** a versão **1.0.4 (7)** está no **teste interno** da Play. A produção espera
só a declaração "Apps governamentais", que depende da Secretaria.

## Projeto nativo (`android/`)

- **Versão:** vem do `pubspec.yaml` (ver [Versão](../README.md#versão) no README).
- **Permissões** (`android/app/src/main/AndroidManifest.xml`): `INTERNET`, `ACCESS_NETWORK_STATE`
  (faixa "Sem conexão") e `ACCESS_FINE_LOCATION` com `ACCESS_COARSE_LOCATION` (localização no dia da
  viagem). **Sem segundo plano:** não há `ACCESS_BACKGROUND_LOCATION` nem serviço em primeiro plano.
- **GPS opcional:** `android.hardware.location`, `.gps` e `.network` declarados com
  `required="false"` (ver [Localização](#localização)).
- **Sem backup** (`allowBackup`, `fullBackupContent`, `dataExtractionRules`): o cache offline tem
  dados de saúde e não vai ao backup do Google nem passa para outro aparelho.
- **Só HTTPS:** o `network_security_config.xml` aceita só certificados do sistema nas bibliotecas
  nativas. O debug libera HTTP e certificados do usuário. O cliente HTTP do Dart não obedece a esse
  arquivo; a trava dele é a `exigirHttpsNoBuildDeLoja()` (ver o README).
- **Links do WhatsApp** (`/entrar/` e `/documento/`): abrem no app com `autoVerify`. A verificação
  automática espera o `assetlinks.json` (ver [Pendências](#pendências-do-android)).
- **Ícone e abertura:** saem do símbolo "MA RI CÁ" ([`loja/marca/`](../loja/marca/README.md)).
  - O ícone adaptativo tem as letras no primeiro plano e o `@color/laranja_marca` no fundo.
  - O ícone legado (48 a 192 px) é o símbolo com os cantos arredondados.
  - Antes do Android 12, a abertura é o `splash_logo` sobre o laranja. Do 12 em diante, o sistema
    mostra o ícone sobre o laranja.

## Localização

O comportamento (quando aparece, aceite por viagem, envio, quando para) está no
[README](../README.md#localização-no-dia-da-viagem). Aqui, o que é do Android:

- **GPS não é exigido do aparelho.** Só com as permissões, a Play entende que o app precisa de GPS
  (`android.hardware.location`) e o esconde de aparelho sem ele: na 1.0.2, 7 aparelhos deixaram de
  ser compatíveis. Por isso o manifesto declara `android.hardware.location`, `.gps` e `.network` com
  `required="false"`. Sem localização, o app funciona igual e a faixa diz que ela está desligada.
  - **Como conferir:** depois de mexer no manifesto, veja na revisão da versão, na Play, que nenhum
    aparelho deixou de ser compatível. Na 1.0.4, os 7 voltaram, com 19.346 compatíveis.
  - **O que não serve para isso:** a lista "Recursos" dos detalhes do pacote, porque ela mostra também
    os recursos opcionais.
- **Duas armadilhas do plugin `geolocator`**, achadas no emulador (os testes de unidade não pegam):
  - o `GeolocatorLocationService` **tem de ficar** no manifesto: sem ele o fluxo de posições não
    começa (no logcat: `Location background service has not started correctly`). Ele fica sem o
    `foregroundServiceType` (`tools:remove`): só é vinculado, nunca vai para o primeiro plano, e a
    Play não pede a declaração de serviço em primeiro plano. Confira no manifesto final
    (`build/app/intermediates/merged_manifest/`): nenhum `foregroundServiceType` nem
    `FOREGROUND_SERVICE`;
  - com a "Precisão do local" do Google desligada, o cliente do Google Play Services mostra o aviso do
    Google ao ligar o GPS. Recusado, o plugin devolve erro e o app passaria a mostrar o aviso a cada
    consulta. Por isso o `GpsDoAparelho` segue pelo LocationManager do Android
    (`forceLocationManager`) até o app fechar.

## Push

A configuração do Firebase (o app Android no projeto e o `firebase.json`) está no
[README](../README.md#notificações-push). Aqui, o que é do Android:

- **App aberto:** a mensagem vira notificação local no canal "Avisos".
- **App fechado:** o sistema mostra sozinho, com ícone pequeno branco e a cor da marca.
- **Testar com o app fechado (Samsung):** em *Bateria* do app, deixe **Sem restrições** e feche o app
  pelos recentes. **Não** use *Forçar parada*: app parado à força não recebe push.

## Assinatura

A Play Store guarda a chave que assina o app para os aparelhos (**Assinatura de apps do Google
Play**, ligada no primeiro envio). A nossa é só a **chave de upload**, que prova à Play que o pacote
veio de nós. Ela fica **fora do repositório**, com cópia num cofre de senhas. Sem ela não sai versão
nova; perder a chave se resolve pelo Play Console, com espera.

Gere num PowerShell aberto por você. O `keytool` pede a senha na hora, e ela não vai para lugar
nenhum:

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

Sem o `key.properties` (ou as variáveis `CIDADAO_*`), o build de release **para com erro**, de
propósito: pacote assinado com a chave de debug a loja recusa. O CI usa as `CIDADAO_*` com uma chave
descartável, criada a cada execução, só para validar o build. O pacote da loja continua saindo daqui.

## Gerar o pacote da loja (`.aab`)

```powershell
flutter build appbundle --release --dart-define-from-file=firebase.json --obfuscate --split-debug-info=build/app/simbolos
```

Sai em `build/app/outputs/bundle/release/app-release.aab`.

- **Conferência no fim do build:** o Flutter procura o `libflutter.so.sym`, para saber se os símbolos
  das bibliotecas nativas saíram do pacote. Para isso ele usa o `apkanalyzer` do **Android SDK
  Command-line Tools**.
  - Sem esse componente, o build acusa "Release app bundle failed to strip debug symbols" e termina
    com erro, mesmo com o pacote certo.
  - Instale pelo SDK Manager do Android Studio: aba SDK Tools → "Android SDK Command-line Tools
    (latest)". O `flutter doctor` aponta quando ele falta.
- **`--obfuscate`:** embaralha os nomes do código Dart no pacote, o que dificulta a engenharia reversa.
- **Símbolos:** ficam em `build/app/simbolos` e servem para ler uma pilha de erro. **Guarde a pasta
  de cada versão** fora do repositório, junto da cópia da chave; sem ela, a pilha daquela versão não
  se lê. O comando é
  `flutter symbolize -i <pilha> -d build/app/simbolos/app.android-arm64.symbols`.

## CI

O job **Android** do [`ci-cidadao-app.yml`](../../.github/workflows/ci-cidadao-app.yml) faz o build de
release (com R8 e o manifesto final) a cada PR que mexe no app, assinado com a chave descartável.
Ele não publica nada. Por enquanto, o envio à Play é manual: o pacote sai daqui e sobe pelo Play
Console.

## Google Play

### Teste interno

- **Convite:** os testadores abrem, no celular, o link
  `https://play.google.com/apps/internaltest/4701732834498633978`. A conta Google precisa estar na
  lista **Equipe AutoMais** (Teste interno → Testadores).
- **Nome provisório:** até a revisão do Google, o app aparece na loja como
  "io.automais.smsmais.cidadao (unreviewed)". No aparelho, é "Saúde Maricá".
- **"Item não encontrado":** quase sempre é outra conta ativa na Play Store do celular. Troque para a
  conta da lista e abra o link de novo. Logo depois de aceitar o convite, pode levar alguns minutos.
- **O que publica e o que não publica:** o "Salvar e publicar" do teste interno publica **só a
  versão**. As declarações salvas continuam esperando o "Enviar para revisão" da Visão geral da
  publicação, que é decisão de quem responde pelo app.

### Versão nova

1. **Pacote:** suba o `pubspec.yaml` (ver [Versão](../README.md#versão)) e gere o `.aab`.
2. **Upload:** em Teste interno → "Criar nova versão", envie o pacote. O arquivo tem uns 60 MB e sobe
   pelo navegador.
3. **Revisão:** confira a tabela de aparelhos compatíveis (nenhum pode sair) e preencha as notas da
   versão em pt-BR.
4. **Publicação:** "Salvar e publicar".

### Segurança dos dados

O declarado é o que o app **manda** ao servidor:

- CPF e nascimento, os seus e os dos acompanhantes;
- e-mail e telefones do Perfil;
- foto;
- exames enviados (arquivo e foto);
- confirmação e cancelamento de exame;
- avaliação da viagem;
- o token do push;
- desde a 1.0.2, a **localização aproximada e exata**: coletada, não compartilhada, opcional, para a
  funcionalidade do app. Só é usada no dia da viagem, depois do Compartilhar, e é apagada no embarque.

Nada é "compartilhado" no sentido da Play: WhatsApp e Firebase são prestadores de serviço, e o
motorista é o próprio serviço de transporte. Não há ID de anúncio nem relatório de falhas. Dependência
nova que colete dado (ex.: Analytics, Crashlytics) muda essa declaração. A URL de exclusão de contas é
a página `/exclusao-de-conta/` do PWA (ver [Exclusão da conta](../README.md#exclusão-da-conta) no
README).

### Página da loja

Os textos, os prints, o ícone e o gráfico estão em [`loja/google-play/`](../loja/google-play/README.md),
do jeito que foram salvos no Play Console.

## Pendências do Android

- **Play Integrity** conferido no servidor, no lugar de detecção caseira de root/emulador. Precisa
  ligar um projeto do Google Cloud no Play Console.
- **Links do WhatsApp abrirem direto no app:** publicar `/.well-known/assetlinks.json` em
  `app.smsmarica.online` com a impressão digital da chave de assinatura da Play. O PR #73 está retido
  até o app ser publicado. Sem ele, o Android pergunta "abrir com".
- **Envio automático à Play:** dá para o GitHub enviar o `.aab` ao teste interno, com uma conta de
  serviço do Google Cloud ligada ao Play Console. Por decisão de 24/10/2026, o envio continua manual.
