# iPhone — projeto, build, assinatura e App Store

O que é só do iPhone no app do cidadão ("Saúde Maricá", bundle `io.automais.smsmais.cidadao`). O que
vale para as duas plataformas — telas, push, conta de demonstração, exclusão da conta, versão — está
no [README](../README.md). O Android está em [`android.md`](android.md).

**Situação (24/10/2026):** começando.

- **Código:** é o mesmo do Android, mas nunca rodou num iPhone.
- **Build:** compila sem assinatura na nuvem, a cada PR (ver [Build na nuvem](#build-na-nuvem-sem-mac)).
- **Conta Apple Developer da AutoMais:** inscrição feita como organização (D-U-N-S 902782620), esperando
  a aprovação da Apple.
- **Teste:** não há iPhone para testar por enquanto.

## Projeto nativo (`ios/`)

- **Só iPhone:** `TARGETED_DEVICE_FAMILY = 1`. No iPad, o app abre em modo de compatibilidade, e por
  isso não precisa de prints nem das exigências de iPad (paisagem, multitarefa). Só retrato, como no
  Android.
- **iOS 15 ou mais novo:** é exigência do Firebase.
- **Versão:** vem do `pubspec.yaml` (ver [Versão](../README.md#versão) no README).
- **Criptografia:** o `Info.plist` declara `ITSAppUsesNonExemptEncryption = false`. O app só usa a
  criptografia do sistema (HTTPS), que é isenta, e assim a Apple não pergunta a cada envio.
- **Permissões** (textos do `Info.plist`, que a pessoa lê no pedido):
  - câmera (`NSCameraUsageDescription`), para a foto do perfil e para fotografar documentos;
  - galeria (`NSPhotoLibraryUsageDescription`), para escolher a foto;
  - salvar na galeria (`NSPhotoLibraryAddUsageDescription`), para os exames baixados;
  - localização durante o uso (`NSLocationWhenInUseUsageDescription`), só no dia da viagem. **Sem
    segundo plano.**
- **Push:** `Runner/Runner.entitlements` com `aps-environment = development` (o Xcode troca para
  `production` ao distribuir) e *Background Modes → Remote notifications*.
- **WhatsApp:** `LSApplicationQueriesSchemes` tem `whatsapp` e `https`, para o app abrir o WhatsApp
  oficial.
- **Ícone e abertura:** o `AppIcon` (todos os tamanhos) e a `LaunchImage` saem do símbolo "MA RI CÁ"
  ([`loja/marca/`](../loja/marca/README.md)). A cor da `LaunchScreen` é o laranja `#FF2A00`.

## Build na nuvem (sem Mac)

Compilar para iPhone exige um Mac com Xcode. Rodar macOS numa máquina virtual num PC que não é da
Apple **viola a licença da Apple**, então não é opção. O Mac vem da nuvem:

- O job **iOS** do [`ci-cidadao-app.yml`](../../.github/workflows/ci-cidadao-app.yml) compila
  (`flutter build ios --release --no-codesign`) a cada PR que mexe no app. Ele usa o Xcode estável
  mais novo do runner, que é o que a App Store exige, e o Flutter da mesma versão dos pacotes de loja.
- É **grátis**, porque o repositório é público: runner padrão não gasta minutos do plano. Se o
  repositório virar privado, os minutos de Mac passam a contar 10 vezes na cota do plano Free. Fora
  dela, a alternativa é a Codemagic, com 500 min/mês grátis em conta pessoal.
- Em repositório público, o log e os artefatos ficam visíveis a qualquer um. Por isso segredo fica
  só em GitHub Secrets, nunca vai para o `echo`, e o `.ipa` e os símbolos não sobem como artefato
  aberto.

## Push

O projeto do Firebase, o `firebase.json` e o comportamento estão no
[README](../README.md#notificações-push). O que é só do iPhone:

1. **App iOS no Firebase:** adicione um app **iOS** com o bundle `io.automais.smsmais.cidadao`. **Não**
   use o `GoogleService-Info.plist`. Anote a chave de API e o ID do app iOS (`1:…:ios:…`), que vão
   para `FIREBASE_IOS_API_KEY` e `FIREBASE_IOS_APP_ID` no `firebase.json`.
2. **Chave da Apple (APNs):** no Apple Developer, em *Certificates, Identifiers & Profiles → Keys*,
   crie a chave com *Apple Push Notifications service (APNs)*.
   - A Apple emite a chave *team-scoped* para **Sandbox ou Production**, não as duas. Crie uma de
     cada, baixe cada `.p8` (a Apple só deixa baixar uma vez) e anote os Key IDs e o Team ID.
   - No Firebase, em *Configurações do projeto → Cloud Messaging → app iOS*, envie **as duas**: a de
     Sandbox como *desenvolvimento* e a de Production como *produção*, com o Key ID e o Team ID.
   - Sem isso o iPhone não recebe nada, e o painel mostra "O Firebase recusou a chave da Apple
     (APNs)".
3. **App ID:** precisa da capability *Push Notifications*.
4. **Teste:** o **simulador não recebe push**, porque não tem token APNs. Só num iPhone.

Com o app aberto, quem mostra a notificação é o próprio iPhone. O sistema só pergunta a permissão uma
vez.

## Conta Apple e assinatura

Quando a conta for aprovada:

1. **Apple Developer:** anote o **Team ID**. Ele vai no `DEVELOPMENT_TEAM` do projeto e nas chaves
   APNs.
2. **App Store Connect:**
   - Crie o app: nome "Saúde Maricá", idioma pt-BR, bundle `io.automais.smsmais.cidadao`. O nome
     precisa estar livre na App Store.
   - Crie a **chave da API** (`.p8`, com o papel *App Manager*). Com ela, a nuvem gera o certificado
     e o perfil e envia ao TestFlight sem Mac. A chave vai só para os GitHub Secrets.
3. **Certificado de distribuição e perfil da App Store:** gerados pela nuvem com a chave da API.
4. **Workflow de envio:** um workflow próprio, `release-cidadao-ios.yml`, roda **só à mão** e envia
   ao TestFlight. Os segredos ficam num ambiente protegido do GitHub (`ios-release`), separado do
   Android.

## TestFlight

- **Testadores internos:** são pessoas do time no App Store Connect, até 100. Recebem as versões sem
  revisão da Apple, pelo app TestFlight no iPhone, com o próprio Apple ID.
- **Testadores externos:** precisam de uma revisão beta da Apple.
- **Sem iPhone:** os prints da loja podem sair do simulador na nuvem. O teste de verdade (push,
  localização, câmera, PDF, WhatsApp) espera um iPhone.

## App Store

- **Página:** em [`loja/app-store/`](../loja/app-store/README.md), com os textos no formato da Apple.
- **Privacidade do app:** é o equivalente da Segurança dos dados da Play. Declara-se o que o app
  manda ao servidor (a lista está em [`android.md`](android.md#segurança-dos-dados)), ligado à
  identidade da pessoa, sem rastreamento e sem anúncios.
- **Conta de demonstração para o revisor:** está no [README](../README.md#revisão-das-lojas).
- **Notas da revisão:**
  - A diretriz **5.1.1(ix)** exige que app de saúde venha de quem presta o serviço ou de quem tem
    autorização dele. A declaração da Secretaria, que já cobre a App Store (PR #76,
    `docs/publicacao-lojas/`), vai anexada.
  - Explicar que a entrada é por CPF e código no WhatsApp, por isso a conta de demonstração.
- **Exclusão da conta** dentro do app (diretriz 5.1.1(v)): **Meu perfil → Excluir minha conta**, que
  abre a página de pedido. Ver [Exclusão da conta](../README.md#exclusão-da-conta).
- **URL de suporte:** é obrigatória, e a página precisa ter um contato. Falta definir.

## Pendências do iPhone

- **Cache fora do backup do iCloud.** É o equivalente ao `allowBackup` desligado do Android: o cache
  offline tem dados de saúde.
- **Links do WhatsApp abrirem direto no app:** *Associated Domains* no projeto e o
  `apple-app-site-association` em `app.smsmarica.online`. Vai junto com o `assetlinks.json` do
  Android, depois de o app sair. Sem isso, o link abre no navegador (o PWA).
- **Ícone de 1024** a partir do arquivo vetorial do símbolo, quando vier. Hoje ele é ampliado do PNG
  de 512.
- **Manifesto de privacidade** (`PrivacyInfo.xcprivacy`): conferir no primeiro envio. A Apple avisa
  por e-mail se faltar a declaração de alguma API.
