# ADR-0003 — App do motorista (`agente.app`) Android-only nesta fase

- **Status**: Aceito
- **Data**: 2026-04-22
- **Decisores**: Bruno (product/eng)

## Contexto

O `SMSMarica.agente.app` é usado por motoristas contratados/parceiros do programa de transporte sanitário. Funcionalidades críticas:

- Postagem contínua de GPS em background.
- Geofencing local para detectar chegada em residências e unidades.
- Integração com apps de navegação externa (Waze, Google Maps) via intents.

O `SMSMarica.cidadao.app`, por outro lado, é para o paciente final e precisa rodar no dispositivo que o cidadão **já tem** — sem limitação de plataforma.

## Decisão

`SMSMarica.agente.app` é entregue **apenas para Android** durante todo o M1..M4. iOS fica fora de escopo.

Implementação:
- `flutter create --platforms=android` (não gerar pasta `ios/`).
- Permissões de localização em foreground e background configuradas para Android via `AndroidManifest.xml` + plugins apropriados.
- Foreground service para postagem GPS contínua (requisito Android 10+).
- Build distribuído via APK/AAB interno — sem App Store/Play Store inicialmente (a definir depois).

## Motivações

1. **Superfície de teste reduzida**: postagem de GPS em background, foreground service, geofencing têm APIs e políticas bem diferentes em iOS (background location mais restritivo, exige justificativa no App Review). Lançar nas duas plataformas simultaneamente dobraria o esforço de teste em algo já delicado.
2. **Perfil do usuário**: motoristas contratados recebem equipamento ou usam Android pessoal; a base Android é esmagadoramente maior no público-alvo.
3. **Integrações nativas**: Waze e Google Maps lidam melhor com intents no Android; no iOS o fluxo é mais limitado.
4. **Políticas de distribuição**: App Store Review tem reviewers sensíveis a background location; o processo atrasaria o lançamento.
5. **Orçamento de Apple Developer Program**: evitável neste estágio.

## Alternativas consideradas

### A. Flutter iOS + Android desde o início

**Prós:** cobertura total; código compartilhado.
**Contras:** custo de lidar com background location dual, App Review incerto, pouco retorno no público-alvo. **Rejeitada para M1..M4.**

### B. Kotlin nativo (sem Flutter)

**Prós:** controle pleno sobre Android lifecycle e foreground services.
**Contras:** nega reuso de código e design com `cidadao.app`; time precisa manter duas stacks. **Rejeitada.**

### C. PWA instalável no Android

**Prós:** instantâneo, sem store.
**Contras:** PWA **não** consegue background location nem geofencing confiável. Fatalmente não atende ao requisito funcional. **Rejeitada.**

## Consequências

- Um único target de build para `agente.app`.
- Manifesto, permissões e plugins escolhidos com Android em mente desde o dia zero — sem condicionais `Platform.isAndroid` inúteis.
- Qualquer PR que adicione pasta/config `ios/` no `agente.app` é rejeitado até esta ADR ser superada.
- Time pode focar em qualidade do Android (bateria, conformidade Play Store futura) em vez de abrir frente em iOS prematuramente.

## Condições para revisitar

- Motoristas demandarem em volume relevante que usam iOS.
- Mudança regulatória/trabalhista que obrigue iOS.
- A plataforma estabilizar e a equipe ter banda para o porte.
- Aparecer requisito de paridade total entre apps (pouco provável).

Quando revisitar: novo ADR (ex.: `0010-agente-app-ios-support.md`) documentando a mudança.
