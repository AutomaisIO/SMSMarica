# Marca do app (comum às duas plataformas)

## Símbolo "MA RI CÁ"

O símbolo quadrado veio do favicon de 512 cadastrado em Sistema → Instituição (22/10/2026), com dois
defeitos que o usuário apontou em 24/10:

- o "A" de "MA" estava cortado por uma linha vertical e tinha perdido a ponta da perna direita. Ele é
  o mesmo desenho do "A" de "CÁ", que está inteiro, e foi completado com a metade direita dele;
- o vão entre "MA" e "RI" (19px) era menor que entre "RI" e "CÁ" (50px, com o acento no meio). "MA"
  subiu até os dois ficarem com 50px, e o bloco foi reduzido para manter a altura original.

| Arquivo | O quê |
|---|---|
| `simbolo-512.png` | O símbolo corrigido: quadrado arredondado laranja (`#FF2A00`) e letras brancas |
| `simbolo-letras-512.png` | Só as letras, com alfa, na mesma posição e escala |
| `gerar_icones.ps1` | Gera todos os ícones a partir dos dois arquivos acima |

O favicon de Sistema → Instituição continua com os defeitos até alguém trocar lá pelo
`simbolo-512.png`.

## Gerar os ícones

Rode da pasta `SMSMais.cidadao.app`:

```powershell
pwsh loja/marca/gerar_icones.ps1
```

| Onde | O que sai |
|---|---|
| Android (`android/app/src/main/res`) | Ícone legado (48–192, cantos arredondados), primeiro plano do ícone adaptativo (só as letras, a 74%) e abertura antes do Android 12 |
| iOS (`ios/Runner/Assets.xcassets`) | `AppIcon` em todos os tamanhos (quadrado cheio, o iOS arredonda) e `LaunchImage` |
| Google Play | `google-play/imagens/icone-512.png` (quadrado cheio, a Play arredonda) |
| PWA (`SMSMais.cidadao.pwa/public`) | favicon, apple-touch, 192, 512 e maskable (símbolo a 80%) |

Trocou a marca, rode de novo e confira o diff: os quatro lugares mudam juntos.

## Logo da entrada

A tela de entrada (no app e no PWA) usa o logo horizontal da **Secretaria de Saúde**, versão
colorida, que a Comunicação mandou em 21/10/2026: `assets/brand/marica_logo.png` no app e
`public/marica_logo.png` no PWA. O PNG não tem margem em cima. Como o hero cobre os 12px de cima da
folha, os dois põem um respiro acima do logo (`Padding(top: 20)` no app e `mt-5` no PWA).
