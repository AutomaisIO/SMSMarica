# Página da Google Play

O que está salvo no Play Console para o **Saúde Maricá** (`io.automais.smsmais.cidadao`), na página
"Detalhes do app" padrão (pt-BR), em 24/10/2026. As regras comuns às duas lojas estão no
[índice da loja](../README.md). O build e a publicação estão em [`docs/android.md`](../../docs/android.md).

## Configurações da loja

- **Categoria:** Medicina. A Play não tem categoria de governo, cidadão ou saúde pública. Medicina é
  a categoria do Meu SUS Digital.
- **Tag:** Medicina (a única de saúde que não é de academia).
- **Contato:** dev@automais.com. A Play publica contato e tags na hora, sem revisão; enquanto o app
  for rascunho, ninguém vê.

## Textos

**Nome (30):** Saúde Maricá

**Descrição curta (80):** Seus agendamentos, exames e transporte do SUS em Maricá, no celular

**Descrição completa (4000):**

```text
O Saúde Maricá reúne no celular os serviços digitais da Saúde de Maricá para o cidadão: consultas e exames agendados, resultados, documentos de saúde e o transporte para tratamento (TFD).

O que você pode fazer:
• Ver as consultas e os exames agendados e os pedidos que ainda estão na fila da regulação.
• Confirmar a presença ou avisar que não vai, para a vaga ir para outra pessoa.
• Abrir o comprovante do exame, com local, protocolo e QR Code.
• Ver os laudos assinados, o PDF das imagens e os anexos dos seus exames.
• Consultar o histórico de atendimentos, com receitas e outros documentos.
• Guardar seus documentos de saúde e enviar foto ou PDF de exames para a equipe conferir.
• Acompanhar as viagens do transporte para tratamento (TFD), indicar acompanhantes, avisar quando estiver pronto para a volta e avaliar o motorista.
• No dia da viagem, se quiser, compartilhar a sua localização com o motorista enquanto espera o carro.
• Receber avisos no celular e falar com a Secretaria pelo WhatsApp oficial.
• Manter o seu perfil em dia: foto, contatos e número de celular.

Entrada segura, sem senha
Você entra com o CPF e a data de nascimento e recebe um código no WhatsApp de um número que já está no seu cadastro da Saúde. O código nunca vai para um número digitado na hora.

Funciona até sem internet
As listas e os documentos que você já abriu ficam guardados no aparelho e podem ser consultados sem conexão. Ao sair da conta, esses dados são apagados do celular.

Sua privacidade
A localização só é usada no dia da viagem, depois que você toca em Compartilhar, e só com o app aberto. Ela para sozinha quando você embarca. O app não tem anúncios.

O serviço é gratuito e de caráter público, oferecido pela Secretaria Municipal de Saúde de Maricá. O aplicativo é desenvolvido e mantido pela AutoMais.

Política de Privacidade: https://app.smsmarica.online/privacidade/
```

## Imagens (`imagens/`)

| Arquivo | Onde vai | Como foi feito |
|---|---|---|
| `icone-512.png` | Ícone do app (512×512) | O símbolo "MA RI CÁ" sobre um quadrado cheio `#FF2A00`, sem cantos arredondados (a Play arredonda). Sai do [`marca/gerar_icones.ps1`](../marca/README.md). |
| `grafico-1024x500.png` | Recurso gráfico | O `grafico/montar_grafico.py` monta o HTML (fontes do app, logo da Secretaria, dois prints) e o Edge sem janela tira a foto. Ver abaixo. |
| `01-inicio.png` … `06-perfil.png` | Capturas do telefone, nesta ordem | Modo demonstração (`DEMO=true`, dados fictícios) no emulador a 1080×1920, PNG sem transparência. |

A Play embaralha a ordem quando vários prints sobem juntos. Para escolher a ordem, adicione um a um
pela biblioteca do painel lateral: seta "Ver detalhes" → "Adicionar".

### Refazer os prints

```powershell
emulator -avd <avd> -skin 1080x1920 -read-only -no-snapshot     # 9:16, sem mexer no AVD salvo
flutter build apk --release --dart-define=DEMO=true --target-platform android-x64
adb -s emulator-5554 install build/app/outputs/flutter-apk/app-release.apk
```

Entre com qualquer CPF válido (ex.: 529.982.247-25) e qualquer código.

- **Barra de status limpa** (9:00, bateria cheia, sem notificações): use o modo demonstração do
  SystemUI. Primeiro `adb shell settings put global sysui_demo_allowed 1`, depois os
  `am broadcast -a com.android.systemui.demo` (`enter`, `clock -e hhmm 0900`, `battery -e level 100`,
  `notifications -e visible false`).
- **Captura:** `adb exec-out screencap -p > tela.png`.
- **Moldura verde:** não navegue com Tab e Enter do teclado antes da captura. No emulador apareceu
  uma moldura verde de 4 px em volta da tela, que some com um toque.

### Refazer o gráfico

Rode da pasta `SMSMais.cidadao.app`:

```powershell
python loja/google-play/grafico/montar_grafico.py    # gera loja/google-play/grafico/grafico.html
& "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless=new --hide-scrollbars `
  --force-device-scale-factor=1 --window-size=1024,500 --user-data-dir="$env:TEMP\edge-grafico" `
  --screenshot="$PWD\loja\google-play\imagens\grafico-1024x500.png" "file:///$PWD/loja/google-play/grafico/grafico.html"
```

A Play quer PNG sem transparência ou JPEG. Confira o tamanho (1024×500) antes de subir.
