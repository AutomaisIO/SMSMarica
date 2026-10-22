# Navegação assistida do motorista (TFD) — Google Navigation SDK

> Documento de **decisão + interface**: como o app do motorista
> (`SMSMais.agente.app`) navega os translados com o **Google Navigation SDK
> para Flutter**, a estratégia de **custo**, e a UX pensada para o motorista.
> Complementa [`backend-app-motorista.md`](./backend-app-motorista.md).

## Decisão

- O turn-by-turn dentro do app usa o **Google Navigation SDK para Flutter**
  (`google_navigation_flutter`) — navegação embarcada, não um *intent* para
  Waze/Maps externo. Motivo: manter o motorista dentro do app, sobrepor as
  informações do paciente ao mapa e comutar para confirmação a cada chegada.
- O SDK é **pago por uso** (cobrança por **sessão de navegação*/transações do
  Navigation SDK + Routes API). **Pricing é uma preocupação de primeira ordem**
  deste módulo — ver estratégia abaixo.

## ⚠️ Pricing — estratégia para não gastar à toa

Regras que o código **deve** respeitar (custo é requisito, não detalhe):

1. **Uma rota por translado, com paradas (waypoints).** Pegar os pacientes e
   deixá-los no destino é **uma única rota** com várias paradas — não N rotas
   separadas. Coletas + destinos entram como *waypoints* da mesma rota. Assim
   paga-se **uma** otimização/rota, não uma por trecho.
2. **Otimização no servidor, uma vez.** A ordem das paradas (menor percurso /
   mais eficiente) é resolvida **pelo backend** via Routes API
   (`optimizeWaypointOrder`) **antes** de lançar no SDK. O app recebe a rota já
   otimizada e só navega. Não pedir reotimização a cada parada.
3. **Não recriar a sessão de navegação a cada parada.** A mesma sessão segue
   para o próximo waypoint. Recriar = nova cobrança. Ao chegar, o app comuta
   para a tela de confirmação e, ao concluir, **continua** a mesma sessão.
4. **Recalcular só quando necessário.** Reroute apenas em desvio real do
   motorista. Evitar recalcular por jitter de GPS / atualizações redundantes.
5. **Cache de geocoding.** Endereço → lat/long é resolvido e **persistido** no
   cadastro do paciente/unidade; não geocodificar o mesmo endereço a cada rota.
6. **Encerrar a sessão ao fim do translado** (liberar o SDK) para não manter
   navegação ativa ociosa.
7. **Chave restrita e monitorada.** Chave de API restrita ao app (Android
   package + SHA-1) e com **orçamento/alertas de billing** no Google Cloud.

## Fluxo

```
Rota do dia ──[Iniciar navegação do translado]──► Tela de Navegação
   │                                                    │
   │  backend já entregou a rota otimizada              │  (1 sessão do SDK)
   │  (coletas + destinos como waypoints)               │
   ▼                                                    ▼
GET /rotas/{id}  ──►  otimização (Routes API)  ──►  navega waypoint a waypoint
                                                        │
                          a cada chegada ──► comuta p/ Confirmação da parada
                          (assentos + presença) ──► retoma a MESMA sessão
                                                        │
                                                  fim ► encerra sessão
```

## UX do motorista (na tela de navegação)

Pensada para uso ao volante — pouco texto, alvos grandes, sem roubar o mapa:

- **Mapa em tela cheia** (Navigation SDK) ao fundo.
- **Painel compacto do passageiro** sobreposto embaixo, só com o essencial:
  - tipo da parada (EMBARQUE/DESEMBARQUE) + ETA;
  - nome do paciente e endereço;
  - **confirmação** do **paciente** e do **acompanhante** (chips:
    confirmado / aguardando / ausente) — alimentado pelo app do cidadão;
  - **observação** relevante em destaque (ex.: "cadeirante — precisa de rampa");
  - botão **Falar** (chat objetivo paciente ↔ motorista, com respostas rápidas
    de 1 toque e atalho de ligação) e botão **Cheguei**.
- **Confirmação da parada** (ao tocar "Cheguei"): mostra a **alocação dos
  assentos** do paciente e do acompanhante (conforme o planejamento) e permite
  marcar **embarcou / ausente** por pessoa. Concluída a parada, retoma a
  navegação para o próximo waypoint.

### Interação paciente ↔ motorista

O paciente usa o **PWA do cidadão**; o motorista, o app. As mensagens, o nome e
as infos básicas do paciente aparecem na própria tela de navegação (painel
compacto). O paciente pode confirmar presença / do acompanhante e mandar recados
curtos; o motorista responde com respostas prontas. Transporte real das
mensagens: backend + Web Push (ver skill `pwa-push`).

## Implementação no app (estado atual)

Pasta `lib/features/navegacao/`:

| Arquivo | Papel |
|---|---|
| `domain/translado_navegacao.dart` | Modelos: `TransladoNavegacao`, `ParadaNavegacao` (waypoint), `PassageiroTranslado`, `Acompanhante`, `MensagemTranslado`, enums (`TipoParada`, `StatusConfirmacao`). |
| `data/navegacao_repository.dart` | Fonte do translado (**mock** A1; A2 = `GET /rotas/{id}` + otimização). |
| `presentation/navegacao_page.dart` | Tela; mapa em tela cheia + painel + comutação para confirmação. |
| `presentation/widgets/mapa_navegacao.dart` | **Placeholder** do `GoogleMapsNavigationView` (a trocar quando o SDK for habilitado). |
| `presentation/widgets/painel_passageiro.dart` | Painel compacto do passageiro. |
| `presentation/widgets/confirmacao_parada_sheet.dart` | Confirmação na chegada + assentos. |
| `presentation/widgets/chat_translado_sheet.dart` | Chat objetivo com respostas rápidas. |
| `presentation/widgets/status_confirmacao_chip.dart` | Chip de confirmação. |

Rota `'/navegacao'` no `router.dart`; entrada pelo botão "Iniciar navegação do
translado" e pelo ícone de navegar na Rota do dia.

### SDK habilitado (feito — testado no tablet)

Implementado com `google_navigation_flutter: ^0.9.4` e validado turn-by-turn no
Galaxy Tab A (SM-T290). Configuração nativa em vigor:

1. **Chave**: `android/local.properties` → `MAPS_API_KEY=...` (**gitignored, NÃO
   commitar**), injetada por `manifestPlaceholders` no
   `com.google.android.geo.API_KEY` do `AndroidManifest`.
2. **Desugaring**: `isCoreLibraryDesugaringEnabled = true` +
   `coreLibraryDesugaring("com.android.tools:desugar_jdk_libs_nio:2.0.4")`
   (exigido pelo SDK porque `minSdk 26 < 34`).
3. **Package**: `applicationId`/`namespace` = **`io.automais.smsmais.agente`** (era
   `online.smsmarica.agente` até o commit 759b4f7) — a chave do GCP é restrita a esse package +
   SHA-1 do certificado de **release** (`86:C8:C5:64:60:32:63:A5:E3:30:0C:77:72:F1:98:C4:B7:B9:6F:97`, chave recriada em 15/10/2026;
   keystore em `SMSMais.agente.app/android/keystore/`, ver README "Distribuição pelo MDM").
   Restrição antiga (package velho / SHA-1 de debug) faz a navegação recusar a chave. **Android-only**
   ([ADR-0003](../../adr/0003-flutter-android-only-agente.md)).
4. **GCP**: Navigation SDK + Routes API habilitados; chave restrita; manter
   orçamento + alertas de billing.

`MapaNavegacao` (placeholder) foi removido; a tela usa `GoogleMapsNavigationView`
real, com o **rodapé do SDK desligado** para dar lugar ao painel do passageiro, e
mantém **1 sessão** (waypoints + `continueToNextDestination` a cada chegada).
Há um botão **Simular** (usa `GoogleMapsNavigator.simulator`) para testar com o
tablet parado.

> **Pegadinha de layout (resolvida):** o `Stack` da tela precisa de
> `fit: StackFit.expand` (e FABs como `Positioned`), senão encolhe para a altura
> do maior filho não-posicionado e o mapa renderiza só numa faixa no topo.

### No tablet da frota (14–15/10/2026)

Validado no aparelho da frota (Qualcomm msm8953, Android 9, **sem Play Services
funcionando**, quiosque do MDM, sempre em paisagem). O Navigation SDK funciona sem
Play Services (`client version: 0`).

- **Builds de teste** (debug e profile) usam o pacote `io.automais.smsmais.agente.debug`
  (instala ao lado do app do MDM) com a assinatura de debug. A chave do GCP ganhou
  uma 2ª restrição para esse pacote + SHA-1 de debug — **tirar depois da
  homologação**. Para medir desempenho no tablet, `--profile` (o debug tem JIT lento
  e dá ANR). A release falha no build se faltar `MAPS_API_KEY`.
- **Voz em pt-BR**: o SDK escolhe o idioma da narração no início do processo, então o
  locale é fixado na `AgenteApplication` (não basta na Activity).
- **Termos do Google e chave**: se a janela de termos não responde em 20 s ou falta a
  chave, a tela avisa e oferece Voltar / Tentar de novo (antes ficava presa).
- **Rota não calculada (`networkError`)**: acontece na primeira tentativa depois de
  muito tempo parado; "Tentar de novo" resolve (visto em 15/10, com internet normal).
- **Confirmação por pessoa**: paciente e acompanhante conferidos separadamente
  (Embarcou / Ausente); a rota segue para a próxima parada e quem ficou ausente sai do
  desembarque (`paradasComAndamento`). Cada marcação vai à central
  (`POST /rastreamento/dispositivos/viagem/eventos`, token do veículo) por uma fila cifrada
  que espera a rede: **início** ao abrir a navegação, **embarque/ausência** na coleta,
  **desembarque** no destino e **conclusão** na última parada. O andamento fica no tablet e
  também volta na rota do dia (`situacao` por pessoa, `concluida` por parada): reiniciar o app
  retoma de onde parou.
- **O que conta é o translado** (decisão de 16/10): o desembarque do paciente marca a sessão
  como **realizada** e a contabiliza no faturamento; a ausência na coleta, como **não
  realizada**. O painel corrige em Atendimentos ▸ Confirmar — e o que a central confirmou o
  tablet não muda mais (`ConfirmadaPorUsuarioId`); o evento atrasado fica registrado como
  "não aplicado". Acompanhante só registra (não muda a sessão nem o faturamento).
- **Quem está dirigindo**: antes de abrir a navegação, o tablet confere de novo o rosto
  do motorista da sessão ([ADR-0073](../../adr/0073-login-facial-motorista-tablet.md)).
- O contador de distância/tempo do Deslocamento liga sozinho com a sessão de navegação
  e para no fim.

## Integrações

A chave do Navigation SDK / Routes API é registrada na tela
**Integrações & credenciais** do painel (`/app/integracoes`, seção *Transporte
de pacientes (TFD)*). O card **Google Maps** já existe para a Routes API
(otimização server-side); o **Navigation SDK** (chave mobile do app) entra como
item da mesma seção. Ver [memória de integrações de credenciais].

## Pendências / decisões para o backend

- ~~`GET /rotas/{id}` com lat/long, assentos e confirmação por pessoa~~ — feito como
  `GET /rastreamento/dispositivos/rotas-do-dia` (etapa 1).
- Endpoint/serviço de **otimização de rota** (Routes API) server-side,
  devolvendo a ordem dos waypoints já resolvida.
- Canal de **mensagens** translado (paciente ↔ motorista) + push.
- ~~Marcar **embarque/ausência** por pessoa na parada (persistência)~~ — feito (etapa 2,
  `translado_evento_viagem`).
