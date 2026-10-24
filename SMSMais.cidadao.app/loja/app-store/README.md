# Página da App Store

**Rascunho (24/10/2026):** a conta Apple da AutoMais ainda está em aprovação, então nada disto foi
salvo no App Store Connect. As regras comuns às duas lojas estão no [índice da loja](../README.md). O
build, o TestFlight e a publicação estão em [`docs/ios.md`](../../docs/ios.md).

## Informações do app

| Campo | Valor |
|---|---|
| Nome (30) | Saúde Maricá. Precisa estar livre na App Store. |
| Bundle ID | `io.automais.smsmais.cidadao` |
| Idioma principal | Português (Brasil) |
| Categoria | Medicina (a mesma da Play) |
| Preço | Grátis |
| Disponibilidade | Brasil |
| Aparelhos | Só iPhone |
| Copyright | 2026 AutoMais Comércio e Serviços de Automação Ltda. |
| URL da Política de Privacidade | `https://app.smsmarica.online/privacidade/` |
| URL de suporte (obrigatória) | **Falta definir.** A página precisa ter um contato. |
| Classificação etária | Preencher o questionário da Apple no App Store Connect |

## Textos

**Subtítulo (30):** Consultas, exames e transporte

**Palavras-chave (100, separadas por vírgula, sem repetir as do nome e do subtítulo):**
`sus,agendamento,laudo,resultado,tfd,prefeitura,regulação,receita,documentos,cidadão,ubs,hospital`

**Texto promocional (170; dá para trocar sem nova versão):** Agendamentos, resultados de exames e o
transporte para tratamento da Saúde de Maricá, no seu iPhone. Entrada com CPF e código no WhatsApp,
sem senha.

**Descrição (4000):** a mesma da Play ([`google-play/`](../google-play/README.md#textos)).

## Privacidade do app

É o equivalente da Segurança dos dados da Play (a lista está em
[`docs/android.md`](../../docs/android.md#segurança-dos-dados)). Tudo é coletado ligado à identidade
da pessoa e só para a funcionalidade do app. Não há rastreamento nem anúncios.

- **Contato:** e-mail e telefone.
- **Identificadores:** CPF e o token do push.
- **Saúde:** exames e documentos enviados, confirmações e cancelamentos.
- **Fotos:** a foto do perfil e as fotos de documentos.
- **Localização exata e aproximada:** só no dia da viagem.
- **Conteúdo do usuário:** a avaliação da viagem.

Confira cada categoria com a lista da Apple na hora de preencher.

## Notas para a revisão (App Review)

Em inglês, com a declaração da Secretaria (PR #76, `docs/publicacao-lojas/`) anexada:

```text
This app gives patients of the public health network of Maricá (Brazil) access to their appointments,
exam results, health documents and the health transport service. The service is provided by the
Municipal Health Department (Secretaria Municipal de Saúde de Maricá); the app is developed and
maintained by AutoMais, which publishes it under the Department's written authorization (attached).

Sign-in: real users enter their CPF (Brazilian taxpayer ID) and date of birth and receive a one-time
code on the WhatsApp number already on file. For review, use the demo entry: CPF 111.111.111-11,
date of birth 01/01/2000, choose any phone number on the list and type any 6-digit code (except
000000). Demo mode uses fictitious data and does not contact our servers.

Location: on the home screen, tap "Compartilhar minha localização" and then "Compartilhar" to see the
When-In-Use permission. Location is only used on the trip day, while the patient waits for the
health transport, with the app open.

Account deletion: Meu perfil → Excluir minha conta.
```

## Imagens

Ainda não feitas. A App Store pede prints de iPhone de **6,9"** (1320×2868), tirados do simulador na
nuvem, no modo demonstração, com as mesmas telas da Play. O ícone de 1024 sai do
[`marca/gerar_icones.ps1`](../marca/README.md).
