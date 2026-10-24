# Lojas

As páginas do Saúde Maricá (`io.automais.smsmais.cidadao`) nas lojas e a marca do app. O que está
aqui é o que está salvo (ou vai ser salvo) em cada console. Mudou aqui, muda lá, e vice-versa.

| Pasta | O quê |
|---|---|
| [`marca/`](marca/README.md) | **Comum:** o símbolo "MA RI CÁ" corrigido e o gerador dos ícones (Android, iOS, Play e PWA) |
| [`google-play/`](google-play/README.md) | Página da Google Play: configurações, textos, ícone, gráfico e prints |
| [`app-store/`](app-store/README.md) | Página da App Store: configurações e textos no formato da Apple |

O build, a assinatura e a publicação de cada plataforma estão em
[`docs/android.md`](../docs/android.md) e [`docs/ios.md`](../docs/ios.md).

## Regras que valem para as duas lojas

- **O texto só diz o que o app faz de verdade**, conferido no modo demonstração.
- **Quem oferece e quem desenvolve:** o texto repete o que os
  [Termos](../../SMSMais.cidadao.pwa/public/termos/index.html) dizem. O serviço é oferecido pela
  Secretaria Municipal de Saúde de Maricá, e o aplicativo é desenvolvido e mantido pela AutoMais.
  **Nunca escrever que o app é da Secretaria.**
- **Prints só do modo demonstração**, com dados fictícios. Nunca dado de paciente real.
- **Contato oficial do app:** dev@automais.com.
- **Política de Privacidade:** `https://app.smsmarica.online/privacidade/`.
- **Declaração de recursos de IA:** "Não rotular recursos". Os prints são telas do app, o ícone é a
  marca da Prefeitura e o gráfico é só a montagem dessas peças.
