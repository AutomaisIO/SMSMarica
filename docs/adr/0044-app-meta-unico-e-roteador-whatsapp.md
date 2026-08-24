# ADR-0044 — Um App único na Meta para N prefeituras, com roteador de WhatsApp na frente

**Status:** aceito · **Data:** 2026-08-17
**Implantação:** concluída em 2026-08-22 para Maricá (`Automais.Zap`, ver `Automais.Zap/README.md`). O App próprio do município foi desativado; só o App da Automais está inscrito no WABA. A instância não tem mais credencial da Meta.
**Relacionado:** [ADR-0043](./0043-instancia-por-municipio.md) (uma instância por município),
[ADR-0038](./0038-mensageria-e-geo-fora-do-tfd.md) (mensageria é serviço de domínio, não do TFD),
[ADR-0045](./0045-criterio-para-extrair-servico.md) (por que este é o *único* serviço novo).

## Contexto

Com a decisão de entregar o produto como **uma instância por município** (ADR-0043), o WhatsApp
passou a ser o único ponto do sistema em que instâncias diferentes disputam um recurso externo
compartilhado.

O motivo é uma característica da plataforma da Meta, não uma escolha nossa:

- O **App** da Meta é onde se configura **um** webhook — uma URL de callback, uma só.
- O **WABA** (WhatsApp Business Account), o **número** e o **nome de exibição** são de nível
  abaixo: um App pode servir vários WABAs, cada um com seu número e sua identidade visual.

Ou seja: **a marca é por número, o webhook é por App.** É possível — e é o que queremos — que
cada prefeitura tenha número, nome ("Secretaria Municipal de Saúde de X"), foto e selo próprios,
enquanto o App é da Automais. O cidadão nunca vê o App.

O que isso quebra é o webhook. Hoje, `SMSMarica.Api/Controllers/WhatsAppWebhookController.cs:43`
lê o corpo cru da requisição, valida o HMAC `X-Hub-Signature-256` com o App Secret e entrega
tudo a `IWhatsAppWebhookService.ProcessarAsync(raw, …)`. **Ele não olha para
`entry[].changes[].value.metadata.phone_number_id`** — assume que todo evento que chega é dele.
Essa premissa é verdadeira enquanto existir um App por instância, e falsa no primeiro dia de um
App compartilhado.

### As duas alternativas, e por que esta

**(a) Um App por prefeitura.** Webhook aponta direto para a instância; o código de hoje funciona
sem mudança nenhuma. Custo: cada venda espera a verificação de negócio da Meta, e a prefeitura
precisa abrir e manter uma conta — entra no caminho crítico comercial, e depende de terceiro.

**(b) Um App da Automais, N WABAs (modelo Tech Provider).** A verificação sai do caminho crítico
da venda; o onboarding de um município novo vira trabalho nosso. Custo: um componente novo,
porque o webhook único precisa ser distribuído.

Escolhemos **(b)**. O custo é um serviço pequeno e sem dado clínico; o ganho é não depender da
Meta a cada venda.

## Decisão

**Um App da Automais serve todas as prefeituras. Cada prefeitura mantém WABA, número, nome de
exibição, foto e selo próprios.** Entre a Meta e as instâncias entra o **`Automais.Zap`** (nome final; o ADR original dizia `ZapRouter`).

### O que o roteador é

Um serviço próprio, na estrutura de `Automais.Fhir` (Data + Core + Api), com uma
responsabilidade só: **entregar cada evento da Meta à instância dona do número.**

- **Entrada.** Recebe o webhook único, valida o HMAC `X-Hub-Signature-256` com o App Secret,
  lê `entry[].changes[].value.metadata.phone_number_id`, encontra a instância dona e encaminha
  o corpo **cru**, reassinando com um segredo próprio daquela instância.
- **Saída (fase 2).** Media o envio. Sob um App compartilhado, o token do System User pode
  enviar por qualquer número do business; mediar evita que esse token viva em N droplets. Na
  fase 1 cada instância continua enviando direto, com credencial própria.
- **Estado.** Uma tabela de rota (`phone_number_id → URL da instância + segredo`) e um buffer
  curto de reentrega para quando a instância estiver fora do ar.

### O que o roteador NÃO é

**Não é a mensageria.** `Conversa`, `MensagemWhatsApp`, `ComunicacaoPaciente`,
`ContatoRegistro` e o vínculo com o cidadão continuam no banco de cada município, como hoje.

Isso não é preferência de estilo — é o que mantém o roteador **fora do alcance da LGPD dos
municípios**. Ele encaminha um envelope; não interpreta, não armazena e não correlaciona
conteúdo clínico. Um componente que guardasse conversas de N prefeituras seria um repositório
central de dado de saúde de vários entes — exatamente o que o ADR-0043 decidiu não construir.

### Mudança na instância

`WhatsAppWebhookController` passa a:

1. Validar a assinatura **do roteador**, não a da Meta (o App Secret deixa de estar em N hosts).
2. **Recusar payload cujo `phone_number_id` não seja o seu** — comparando com
   `WhatsAppConfiguracao.PhoneNumberId`, que já existe. Hoje ele aceita qualquer coisa que passe
   no HMAC; com App compartilhado isso significaria uma instância processando mensagem de outro
   município.

O item 2 vale a pena mesmo antes do roteador existir: é uma verificação barata contra uma
configuração errada.

## Consequências

**Positivas**

- A venda deixa de esperar a Meta. O onboarding de um município novo é trabalho nosso, não
  dele.
- O App Secret e (na fase 2) o token do System User ficam num lugar só.
- O cidadão vê a prefeitura dele: número, nome, foto e selo são do WABA de cada município.

**Custos assumidos**

- **Mais um deployable**, com CI, porta e monitoramento próprios. Aceito porque o critério do
  [ADR-0045](./0045-criterio-para-extrair-servico.md) é satisfeito: existe um **dono externo**
  (a Meta) impondo a fronteira.
- **Ponto único de falha para o inbound.** Roteador fora do ar = nenhuma prefeitura recebe
  mensagem. Daí o buffer de reentrega e o bind/monitoramento serem parte do desenho, não
  detalhe. O outbound (fase 1) não passa por ele e continua funcionando.
- **Um problema de App atinge todos** — App restrito ou token revogado é incidente de N
  clientes. Qualidade e limites de envio continuam por número.
- **Templates são aprovados por WABA.** Um App compartilhado não dá reuso de aprovação: cada
  município aprova os seus. Automatizável por API, mas não é ganho do modelo.

## Propriedade do WABA (revisado em 2026-08-22)

A versão original deste ADR tratava a transferência de WABA como "a pendência mais
importante". Ao implantar, ficou claro que isso depende de **onde o WABA nasce**, e que o caso
de Maricá já estava no lado bom:

- **WABA no Business Manager da prefeitura** (o caso de Maricá, `2099272540984687`): a
  prefeitura é dona do WABA, do número, do nome de exibição e dos templates. A Automais é dona
  só do App e do System User. A única ligação é uma **autorização** que a prefeitura concede ao
  App — e pode revogar a qualquer momento. A prefeitura pode tirar a Automais; a Automais não
  pode tirar a prefeitura. **Não há o que transferir na saída.** O que vale no contrato é o
  inverso: "o contratante concede acesso ao App da contratada e pode revogá-lo; a contratada não
  detém propriedade sobre WABA, número ou templates".

- **WABA no business da Automais** (Tech Provider puro): aí sim a prefeitura depende de nós
  para levar o número embora, a Meta exige um processo de transferência, e a cláusula
  contratual prévia é obrigatória.

**Decisão:** o modelo padrão para clientes novos é o primeiro — replicar Maricá. O custo é a
prefeitura precisar de Business Manager verificado, o que em ente público é trabalhoso; mas
elimina a cláusula, o processo de transferência e o constrangimento jurídico. O segundo modelo
fica para quando a prefeitura não conseguir a verificação — e aí a cláusula entra **antes da
primeira ativação**.

## Pendências técnicas herdadas

Registradas aqui porque afetam o desenho e ainda não foram resolvidas:

1. **`WhatsAppConfiguracao` é lida via `ITfdConfigService`** (`Core/Tfd/Configuracao/`) — nome
   legado: esse serviço gerencia Google Maps e WhatsApp, não TFD, e é consumido por 6 pontos
   fora do transporte. O roteador não depende disso, mas quem mexer no webhook vai esbarrar.
2. **O `/health` do monolito não conhece serviços externos** (`Api/Program.cs:174-177` só checa o
   DbContext). Com o roteador, "WhatsApp parou" precisa ser observável — hoje não seria.
