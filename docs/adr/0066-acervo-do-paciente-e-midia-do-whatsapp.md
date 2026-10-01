# ADR-0066 — Acervo do paciente ("Exames anexados" perene) e mídia do WhatsApp pendente até alguém aceitar

**Status:** aceito (implementado localmente em 2026-10-01; deploy depende de OK) · **Data:** 2026-10-01
**Relacionado:** [ADR-0019](./0019-anexos-exame-pwa-qr-armazenamento.md) (anexos da anamnese por QR,
armazenamento no Spaces) · [ADR-0052](./0052-fila-pre-regulacao-e-agente-regulador.md) (caixinhas de
exigência da solicitação de regulação) · [ADR-0044](./0044-app-meta-unico-e-roteador-whatsapp.md)
(só o Automais.Zap tem o token da Meta) · [ADR-0007](./0007-schema-fhir-separado.md)

## Contexto

Pedido do Bernardo (01/10/2026):

- Anexar um documento numa solicitação já funcionava, mas o anexo não abria em lugar nenhum.
- O documento devia pedir nome e descrição e ficar **perene** no cadastro do paciente, em
  "Exames anexados".
- Foto ou PDF que o paciente manda pelo WhatsApp não aparecia na conversa. Devia dar para
  visualizar e "adicionar ao cadastro do paciente".
- O que está no cadastro, incluindo laudo **assinado** e PDF das imagens dos exames, devia ficar
  disponível para anexar em qualquer solicitação.
- O app do cidadão devia ganhar "anexar documento" e a lista de tudo.
- Mídia do chat e envio do app ficam **pendentes até alguém aceitar**. Com 10 pendentes, trava,
  contra quem polui o armazenamento.

O que existia:

**Anexos: três sistemas que não se falavam**

| Sistema | Onde guarda | Ligação com o paciente |
|---|---|---|
| Anamnese por QR (`documento_exame`) | Spaces | Indireta, pelo `ExameImagem` |
| Caixinhas da regulação (`regulacao_exigencia_arquivo`) | Spaces | Pela solicitação |
| Rascunhos SER/SERNIT (`*_rascunho_anexo`) | `midia`, dentro do Postgres | Só pelo CNS digitado |

- A aba "Exames anexados" mostrava só o primeiro.
- Nenhum dos três abria o arquivo na tela.

**WhatsApp**
- O webhook gravava só o *tipo* da mensagem (`TipoMensagem.Imagem`). O id da mídia era jogado
  fora, e a bolha ficava vazia.
- Só o Automais.Zap tem o token da Meta para baixar a mídia.
- O relay espera a resposta do SMSMais por ~10 s.
- A Meta guarda a mídia por ~30 dias.

## Decisão

### 1. Acervo do paciente: entidade própria, `smsmarica.documento_paciente`

O documento do acervo tem estes dados:
- paciente (id do hub FHIR);
- título e descrição;
- nome do arquivo, tipo, tamanho e SHA-256;
- chave no Spaces;
- origem: `Painel` | `Solicitação` | `WhatsApp` | `AppCidadão`, mais uma referência de origem;
- situação: `Pendente` | `Aceito`, com quem aceitou e quando;
- auditoria e exclusão lógica (ADR-0006).

- **Cada fluxo guarda a própria cópia.** A caixinha da regulação apaga o conteúdo quando o anexo
  é retirado, e o acervo não pode sumir junto. Por isso anexar numa solicitação grava **também**
  uma cópia no acervo, com dedup por hash dentro do paciente: o mesmo PDF em três solicitações vira
  um documento só. Copiar arquivo custa pouco; amarrar ciclos de vida diferentes custaria caro.
- **A cópia no acervo é consequência e nunca derruba o anexo** (`RegistrarCopiaAsync`). Se falhar,
  fica no log, e o anexo da solicitação vale.
- **A lista do cadastro é uma união, não uma tabela.** Ela junta:
  - os documentos do acervo;
  - os PDFs da anamnese já salvos;
  - os laudos **assinados** (laudo sem assinatura não entra);
  - o PDF das imagens de cada exame realizado, com a mesma regra do app do cidadão para achar o
    study efetivo.

  Cada item tem uma chave `tipo:id`. É ela que a solicitação manda de volta para anexar sem novo
  upload, e o backend relê o conteúdo conferindo que o item é do paciente.
- **Só o que está `Aceito` é anexável.** O que o paciente envia pelo app entra `Pendente`. A
  equipe vê e aceita, e pode corrigir o nome nesse momento. Teto: **10 pendentes por paciente**.
- **Acesso por contexto, não por um endpoint genérico.** A regulação lê o acervo pela solicitação,
  com o escopo dela. SER/SERNIT leem pelo rascunho (o CNS vira paciente), a anamnese pelo exame e o
  cadastro pelo módulo Pacientes. Quem tem só o módulo Regulação não precisa do módulo Pacientes para
  anexar do cadastro, e o alcance de cada tela continua valendo.
- Aceitos: PDF, JPEG, PNG, WEBP e GIF, até 25 MB.

### 2. Mídia do WhatsApp: o webhook anota, um worker baixa, a equipe decide

- **No webhook:** o `whatsapp_mensagem` ganha as colunas `midia_*`. O webhook guarda id, tipo, nome
  do arquivo e legenda, e marca `Recebendo`. Ele **não baixa nada**, porque o relay não espera.
- **No worker (`BaixadorMidiasWhatsAppWorker`):** acorda pelo sinal do webhook e, de todo jeito, a
  cada 30 s. Baixa a mídia pelo endpoint novo do Zap e guarda no Spaces como `Pendente`. Foto e PDF
  vão para a fila; áudio e vídeo ficam só registrados.
  - Falha passageira é tentada de novo por algumas horas; falha definitiva vira `Falhou`.
  - "Tentar de novo" funciona até 30 dias.
- **A equipe decide:**
  - **Aceitar** cria o documento no acervo do paciente escolhido e apaga a cópia da conversa. O
    paciente tem de ter ligação com a conversa: ser o da mensagem, o da conversa ou alguém com
    aquele telefone.
  - **Descartar** apaga o arquivo.
- **Trava:** com **10 mídias `Pendente` no mesmo número**, as próximas nem são baixadas
  (`Bloqueada`). Decidir as pendentes libera de novo.
- **Endpoint novo no Automais.Zap:** `GET v1/midias-recebidas/{mediaId}?phone_number_id=`. É
  autenticado pelo token do tenant, confere se o tenant pode usar o número e passa o
  `phone_number_id` à Meta. Ele **não guarda nada**: faz o `GET /{media-id}`, depois o `GET` na URL
  assinada, e devolve os bytes, com teto de tamanho.

## Consequências

- Uma tabela nova, `documento_paciente`.
- Colunas aditivas e nulas: `whatsapp_mensagem.midia_*`, e `titulo`/`descricao` nos anexos de
  regulação, SER e SERNIT.
- Uma migration (`AcervoDocumentosPaciente`), sem dado.
- **O deploy do Zap tem de ir antes ou junto do backend.** Sem o endpoint novo, o worker grava
  `Falhou` depois das horas de tentativa, e "Tentar de novo" recupera em até 30 dias.
- **Anexos antigos não são copiados para o acervo.** Os da regulação e do SER/SERNIT anteriores a
  esta mudança continuam só na solicitação, e não há backfill. Os PDFs da anamnese, os laudos e as
  imagens já aparecem pela união.
- **O PDF das imagens é gerado sob demanda** quando alguém o abre ou anexa. Isso pesa no PACS na
  primeira vez, e o cache do app do cidadão ajuda.
- **Quanto se guarda:** foto do celular vai para o Spaces até 25 MB por arquivo. A trava de 10
  pendentes limita o que entra sem ninguém decidir.
