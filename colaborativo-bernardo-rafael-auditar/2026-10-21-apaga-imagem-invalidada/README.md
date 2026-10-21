# APAGA-IMAGEM-INVALIDADA — a médica retira imagens ruins do que vai ao paciente

Preparado em 20–21/10/2026 pelo Rafael com o Claude, para auditoria do Bernardo. **O código está no
próprio branch `APAGA-IMAGEM-INVALIDADA`** (não é patch). Nada foi para a `main` nem para produção.

> ⚠ **Não compilado, não testado e SEM MIGRATION.** A máquina onde isto foi escrito não tem .NET
> SDK nem Node.

## Para o Claude de quem for aplicar

1. `git fetch && git checkout APAGA-IMAGEM-INVALIDADA`.
2. **Gerar a migration** (a entidade e a configuração estão prontas):
   `dotnet ef migrations add ImagemInvalidada --project src/SMSMais.Data --startup-project src/SMSMais.Api`
   (dentro de `SMSMais.server`). Conferir que ela cria só a tabela `smsmarica.imagem_invalidada`, a
   FK para `usuario`, o índice único **filtrado** `(study_instance_uid, sop_instance_uid) WHERE
   excluido_em IS NULL` e o índice por `study_instance_uid`.
3. Compilar e testar:
   - `SMSMais.server`: `dotnet build` (0 warnings) e `dotnet test`, que usa Docker/Testcontainers.
     Teste novo: `Exames/ImagensInvalidadasServiceTests`.
   - `SMSMais.front`: `npm run build` e `npm run lint`.
4. **Confrontar o Manual** (skill `confrontar-manual`): o artigo do visualizador PACS ganha o botão
   novo e a marcação na lista de imagens. Isso **não foi feito**.
5. Revisar e commitar/mergear pelo fluxo normal.

## O problema (Rafael)

Em densitometria e raio-X, o aparelho manda imagens ruins junto: de 5 recebidas, 1 é laudada. Ao
assinar, o paciente recebia **as 5** no PDF. A médica precisa, no visualizador ("Abrir
visualizador" no laudo → `/pacs/janela`), poder **retirar** a imagem indesejada. É um **soft delete
só da entrega**: a imagem continua no PACS e no S3, mas não entra no PDF montado para o paciente.

## Regras decididas (Rafael)

| | Quem pode |
|---|---|
| **Retirar** | quem lauda (permissão **Laudos / Edição**) ou usuário com **acesso global** — **também depois de o laudo ser assinado** |
| **Devolver** | **quem retirou** ou usuário com **acesso global** |
| **Arquivo do paciente** | é **substituído na hora**: os caches são apagados e o PDF é regerado sem a imagem |

Interpretação: "só a médica restaura" foi implementado como **"quem retirou"**. Se a intenção era
"a médica responsável pelo laudo do estudo", isso pede o vínculo usuário↔Practitioner (hoje por CPF
no `LaudosService`).

## Como as imagens chegam ao paciente (o que foi tocado)

As imagens vêm do **PACS (dcm4chee)**: a lista vem do QIDO e cada JPEG do WADO `/rendered`, sempre
pelo `ExamePacsImagensReader`. O S3 guarda **caches**:

- `render-cache/{study}.zip` — JPEGs do estudo, usado pelos dois PDFs;
- `imagens-exame/{paciente}/{study}.pdf` — PDF das imagens, usado pelo **app do cidadão** e pelo **acervo**.

O "exame completo" (link de download / painel) não tem cache próprio.

## O que muda

| Onde | Mudança |
|---|---|
| `Data/Entities/ImagemInvalidada` + `Configurations/ImagemInvalidadaConfiguration` + `DbSet ImagensInvalidadas` | Tabela `imagem_invalidada` (estudo + SOP, motivo, quem/quando, `excluido_em`/`excluido_por` = devolução). Uma vigente por imagem, e as devolvidas ficam como trilha. |
| `ExamePacsImagensReader` | `ObterImagensAsync(..., sopsExcluidos, ...)`. O render-cache passa a gravar `{ordem:D4}_{SOP}.jpg` e guarda o estudo **inteiro**; o filtro é feito na leitura. Cache antigo (sem SOP) é refeito do PACS na primeira leitura que precisar filtrar. A contagem do cache não muda, então a re-validação de fundo segue igual. |
| `Exames/ImagensInvalidadas/SopsInvalidados` | Consulta única "o que está fora deste estudo", usada pelos dois PDFs. |
| `ExameCompletoPdfService`, `ExameImagensPdfService` | Passam as imagens invalidadas para o leitor. |
| `Exames/ImagensInvalidadas/ImagensInvalidadasService` | Listar / retirar / devolver, com as regras acima. É idempotente. **Recusa retirar a última imagem** (o PDF sairia vazio; com o PACS fora do ar a checagem é pulada). Depois de cada mudança, `InvalidarAsync` + `GerarOuObterAsync` em cada exame ligado ao estudo (worklist ou associação); falha ao regerar só registra log (o próximo acesso regera). |
| `Api/Controllers/ImagensInvalidadasController` | `GET/PUT/DELETE /estudos/{study}/imagens-invalidadas[/{sop}]`. O atributo pede só `Pacs/Consulta`; a regra fina fica no serviço, que devolve `podeInvalidar` e, por imagem, `podeRestaurar`. |
| Front `pacs/api/imagensInvalidadasApi.ts` + `queries.ts` | `useImagensInvalidadas`, `useAlternarImagemInvalidada`. |
| Front `PacsViewport` | Botão novo na barra, ao lado de "Resetar": **imagem riscada** = "Retirar esta imagem do exame do paciente"; **imagem com +** = "Devolver". É diferente da lixeira, que continua sendo só das **marcas**. Só aparece para quem pode. |
| Front `PacsImagensSidebar` | A miniatura retirada fica esmaecida, com o nome riscado e a etiqueta "Não vai ao paciente"; o tooltip diz quem retirou e o motivo. |
| Front `PacsViewerPage` | Liga tudo e passa o SOP de cada imagem para a lista. A confirmação usa o `Modal`, nunca `alert`/`confirm` (travaria a janela do visualizador), com motivo opcional. |
| Testes | `ImagensInvalidadasServiceTests`: quem lauda retira e o PDF é refeito; sem permissão não retira; acesso global retira; idempotência; não retira a última; só quem retirou ou acesso global devolve, e a trilha fica. |

## Pontos para o Bernardo olhar

- **Quem já baixou o PDF antes** continua com a versão antiga: não há como "desenviar". A partir da
  retirada, link, app e acervo entregam a versão nova.
- O **laudo** (PDF assinado) não é tocado: ele não contém imagens (decisão de 24/09/2026 no
  `ExameCompletoPdfService`).
- A regeração do PDF roda **dentro da requisição**. Para DX/DO são poucas imagens, mas, se pesar, dá
  para mover para o `PreparadorImagensExameService`.
- O SOP não é validado contra o estudo no PACS: um SOP que não existe só criaria uma linha inócua.

## Aplicação (21/10/2026, branch `feat/imagem-invalidada`)

O commit do Rafael entrou sem alteração em cima da `main` (nenhum arquivo dele tinha mudado desde
a base `bc2935a`). Na aplicação:

- **Migration `ImagemInvalidada`** gerada com `dotnet ef`: cria só `smsmarica.imagem_invalidada`,
  a FK para `usuario` (`Restrict`), o índice único filtrado `(study_instance_uid, sop_instance_uid)
  WHERE excluido_em IS NULL`, o índice por `study_instance_uid` e o da FK. O snapshot ganhou só
  esse bloco.
- **Build:** `SMSMais.server` com 0 erros e 0 avisos; `SMSMais.front` passou no `npm run build`.
- **Testes** (Testcontainers, Docker local): `ImagensInvalidadasServiceTests` mais as classes que
  usam o leitor e os PDFs de imagens (`AgendaDoPacienteTests`, `DocumentosPacienteServiceTests`,
  `DownloadPublicoGateCpfTests`, `FilaGuiasTests`, `LiberacaoExamePacienteTests`): 58/58. A suíte
  inteira roda no CI do deploy.
- **Manual:** o artigo `pacs` ganhou a seção "Retirar imagem do exame do paciente", o item em
  "Quem pode o quê" e duas dúvidas; o artigo `pacientes` diz que o PDF de imagens sai sem as
  retiradas.
