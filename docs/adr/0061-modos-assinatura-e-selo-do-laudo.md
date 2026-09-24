# ADR-0061 — Modos de assinatura por médico e selo de verificação do laudo

**Status:** aceito (implementado em 24/09/2026; modo Nuvem pendente de validação com certificado real)
**Data:** 2026-09-24
**Relacionado:** [ADR-0015](./0015-assinatura-laudos-agente-itext.md) (agente + Assinador iText),
[ADR-0049](./0049-assinatura-laudo-carimbo-posicionavel.md) (carimbo posicionável, PDF-base fixado).

## Contexto

Até aqui havia um caminho só para oficializar um laudo: o agente Windows (`Automais.Assinador.Agente`)
assinando pelo certificado na loja do Windows (ADR-0015). A rede de médicos é mais variada:

- médicos com certificado na máquina (VIDaaS Connect, token A3, A1) — o caminho atual;
- médicos com **VIDaaS em nuvem** que não querem ou não podem instalar nada, e assinam pelo celular;
- médicos **sem certificado digital**, cujo laudo hoje fica parado em "Finalizado", sem nome do
  médico, sem liberação e sem aviso ao paciente.

Além disso, o laudo impresso não tinha como ser conferido por terceiros. A declaração de
comparecimento já resolve isso com um QR Code que abre uma página pública de autenticidade.

## Decisão

### 1. O modo é do médico, escolhido pelo administrador

Nova tabela `smsmarica.medico_config_assinatura` (`medico_id` PK, `modo`, auditoria), separada da
rubrica (`assinatura_medico`): o administrador escolhe o modo antes ou depois de enviar a imagem, e
remover a rubrica não apaga a escolha. Três valores (`ModoAssinaturaMedico`):

| Modo | Como assina | O que o médico faz |
|---|---|---|
| `Desktop` (padrão) | Agente local + loja do Windows (ADR-0015) | Confirma no VIDaaS Connect |
| `Nuvem` | API IntegraICP v3 (canal com clearance VIDaaS) | Aprova no app do celular |
| `SemCertificado` | Carimbo desenhado pelo servidor, sem CMS | Nada além de posicionar o carimbo |

Sem linha gravada vale `Desktop`, para nenhum médico em uso mudar de fluxo sozinho. O modo é
**gravado no job** (`laudo_assinatura.modo`) no "iniciar": trocar no cadastro não muda job em curso.

Os três modos compartilham tudo o que já existia: rubrica obrigatória, exame associado a pedido,
só o autor assina, carimbo posicionado pela médica sobre o PDF-base fixado (ADR-0049) e a
**conferência** (`AguardandoAprovacao` → aprovar libera e avisa o paciente).

### 2. Nuvem pela IntegraICP, conferindo o RAW antes de embutir

Fluxo: "iniciar" gera PKCE (`code_verifier` cifrado com Data Protection no job) e um `state`
opaco (só o hash fica no job) → a IntegraICP devolve a URL de autorização → o médico aprova no app
→ o navegador volta para `GET /assinatura/nuvem/retorno?state=…` → o servidor busca o certificado,
monta a cadeia, chama o Assinador `preparar`, pede a assinatura RAW do hash e chama `concluir`.
A trava de autoria (CPF do certificado == CPF do autor) é a mesma do agente.

Dois cuidados por causa do que o spike de 04/08/2026 não conseguiu fechar:

- **A semântica do RAW nunca foi exercitada com certificado real.** Antes de embutir, o servidor
  verifica a assinatura com a chave pública do certificado como RSASSA-PKCS1-v1_5 sobre SHA-256
  (o que o CMS do iText exige). Se não conferir, o job falha com mensagem clara e nada é gravado —
  nunca um PDF que abre mas é criptograficamente inválido.
- **A IntegraICP não valida a URL de retorno.** O retorno só é aceito com o `state` que geramos,
  que é de uso único e expira com o job (10 min).

A cadeia vem do pacote oficial de ACs do ITI (`ACcompactado.zip`), baixado uma vez por processo:
a credencial traz só o certificado do médico e a AC VALID RFB v5 não publica AIA.

Configuração (variáveis de ambiente, nunca no repositório): `Assinatura__Nuvem__Canal` (vazio =
modo desligado, e o painel explica o motivo), `Assinatura__Nuvem__CabecalhoAutenticacao` e
`Assinatura__Nuvem__ValorAutenticacao` se o contrato exigir, `Assinatura__Nuvem__UrlPublicaApi`
(senão usa `Publico__BaseUrl`).

### 3. Sem certificado: carimbo como conteúdo, e o documento diz isso

O carimbo é desenhado **no próprio servidor** (`CarimboPdf`, com o PDFsharp que o servidor já usa
para juntar PDFs), sobre o PDF-base fixado, na página e no retângulo escolhidos, mantendo a
proporção e **sem campo de assinatura**. O `Automais.Assinador` não participa: ele existe para
assinar, e sem certificado não há o que assinar. O carimbo diz "Emitido em",
não "Assinado em". O job grava `formato = CARIMBO_SEM_ICP`, que é o que distingue, depois de
`Concluida`, um laudo assinado de um carimbado. O painel mostra **Carimbado** (âmbar), nunca
**Assinado**.

Para o resto do sistema (download, app do cidadão, aviso de laudo pronto) o carimbado é oficial:
`Assinado` no DTO continua significando "tem documento oficial aprovado". Quem precisa distinguir
lê `AssinaturaSemCertificado`.

### 4. Selo de verificação por QR Code

Nova tabela `smsmarica.laudo_verificacao` (`id` = código público, `laudo_id` único). O código é
UUID **v4** — não o v7 da declaração de comparecimento — porque o laudo carrega dado clínico e o
v7 expõe o instante de criação. O selo é criado ao gerar o PDF-base: o código precisa estar no
PDF antes de a assinatura travar os bytes.

O rodapé do documento oficial ganha o QR, o endereço de conferência e uma frase que depende do
modo ("assinado digitalmente com certificado ICP-Brasil" ou "emitido com carimbo do médico, SEM
assinatura digital ICP-Brasil"). O QR fica à esquerda e termina antes do carimbo padrão (centro).

`GET /publico/laudos/{codigo}` mostra se o laudo é válido, se é assinado ou carimbado, paciente,
exame, médico, data, titular do certificado, aviso de versão mais recente, e o botão de download
(`GET /publico/laudos/{codigo}/pdf`, o PDF aprovado). Rate limit `verificacao-publica`, 30/min por IP.

## Consequências

- **Migration** `ModosAssinaturaMedicoESeloLaudo`: duas tabelas novas e três colunas em
  `laudo_assinatura`. Nada existente muda de valor.
- **Deploy só do servidor e do painel.** O `Automais.Assinador` e o agente Windows não mudam.
- **Laudos já assinados não ganham QR.** O PDF assinado é byte-estável; só documentos assinados
  daqui em diante carregam o selo.
- **Médico-legal:** o laudo carimbado não tem validade jurídica plena (CFM 2.299/2021). A decisão
  de oferecer o modo é da gestão; o sistema garante que ninguém confunda os dois — no PDF, no
  painel e na página do QR.
- **Pendente:** exercitar o modo Nuvem com um CPF real com VIDaaS ativo. O nome do parâmetro da
  credencial no retorno não é documentado; o controller aceita os nomes prováveis e, sem casar,
  o primeiro valor com formato ULID (o mesmo critério do spike).

## Reversão

Pôr todos os médicos em `Desktop` devolve o comportamento anterior, salvo o QR no rodapé. Para
tirar o QR, `LaudoAssinaturaService.GerarPdfOficialAsync` volta a chamar `GerarAsync` no modo
`PreparandoAssinatura`. As tabelas podem ficar.
