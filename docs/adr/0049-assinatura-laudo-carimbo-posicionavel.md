# ADR-0049 — Assinatura de laudo: carimbo posicionável pela médica (campo PAdES único)

**Status:** proposto · **Data:** 2026-08-26
**Relacionado:** módulo Laudos/Assinatura (`SMSMais.Core/Laudos`), serviço externo
`Automais.Assinador` (PAdES/iText), painel `SMSMais.front` (feature assinatura).
**Motivado por:** ticket #113 ("Laudo de densitometria: assinatura cai em página em branco",
desdobrado do #110).

## Contexto

O carimbo da assinatura do laudo é aplicado hoje em **posição fixa**: o
`Automais.Assinador` (`PadesSigner.MontarRect`) estampa a rubrica num quadrado de 130pt,
centralizado, a 28pt do pé da **última página** (`SetPageNumber(ultimaPagina)`). Para o texto
nunca cair sob esse retângulo, o `LaudoPdfRenderer` (modo `PreparandoAssinatura`) insere um
bloco vazio inquebrável de `ReservaCarimboPt` (100pt) no fim do fluxo do conteúdo.

Quando as NOTAS/OBSERVAÇÕES do laudo ficam longas, esse bloco de reserva não cabe no pé da
página e o QuestPDF quebra a página, empurrando o carimbo para uma **segunda página
praticamente vazia** — podendo gerar impressão sem a assinatura visível na página principal
(ticket #113). É o preço de uma zona de carimbo **fixa** protegida por reserva **rígida**.

O pedido original do #110 ("assinatura em posição fixa em todas as páginas") era, no fundo, um
*contorno* desse overflow.

## Decisão

**Trocar a zona de carimbo fixa por um campo de assinatura PAdES único, cuja posição (página +
retângulo) é escolhida pela médica sobre o PDF-base antes de assinar.** O carimbo continua sendo
a **aparência do próprio campo de assinatura** (não é queimado como conteúdo, nem há assinatura
invisível separada).

**1. Campo único, não desacoplado.** A alternativa de queimar o carimbo no *conteúdo* do PDF +
assinatura invisível/rodapé foi **descartada**. Campo único é mais robusto e mais eficiente:
- **Robustez jurídica (CFM 2.299/2021):** o retângulo que a médica posiciona é exatamente o que
  o validador ITI/Adobe destaca como assinatura, já carregando nome/CRM/RQE. Identidade e
  assinatura são o mesmo objeto — não há via impressa em que o carimbo e a assinatura "não
  batem". No desacoplado, o carimbo vira conteúdo comum e a fé pública migra para uma assinatura
  invisível, exigindo nota extra "assinado digitalmente" para não parecer um logo.
- **Menos superfície de falha:** desacoplar exige dois mecanismos (estampar imagem no stream de
  conteúdo **e** assinar). Campo único reaproveita o caminho de *appearance* que já está em
  produção (`SignatureFieldAppearance.SetContent(imagem)`).
- **Menos código no serviço externo:** parametrizar `MontarRect`/`SetPageNumber` (~15 linhas)
  em vez de escrever estampagem de conteúdo multipágina no `Automais.Assinador`.

**2. "Todas as páginas" (origem do #110) fica obsoleto, não perdido.** Com posição livre + campo
visível no validador, repetir a rubrica em toda página é cosmético — e cosmético perigoso: N
marcas que **não** são assinaturas dão falsa impressão de N assinaturas. Robustez pede **uma**
marca que é a assinatura de verdade.

**3. PDF-base fixado (pin) no job.** O ponto crítico não é mover o retângulo, é garantir que a
médica **assine exatamente o PDF que ela posicionou**. O `ResolverDataExameAsync` pode consultar
o PACS ao vivo e mudar a paginação entre dois renders. Por isso o PDF-base é **renderizado uma
vez** (ao abrir o posicionamento), tem o hash calculado e é **guardado no job**; preparação e
assinatura consomem esse mesmo artefato, sem re-renderizar. Mais robusto (sem drift) e mais
eficiente (uma renderização).

**4. Contrato de coordenadas único.** `posição = { pagina: 1-based (igual iText), origem:
inferior-esquerda, unidade: pontos PDF, x, y, largura, altura }`. O **front converte** de
viewer(px, topo-esquerda, zoom) para PDF(pt, base-esquerda); o **servidor valida** (página no
intervalo, retângulo dentro da página, tamanho mín/máx) e rejeita com `ValidacaoException`.

**5. Remoção da reserva rígida.** O bloco `ReservaCarimboPt` no modo `PreparandoAssinatura` é
removido — some a causa raiz do #113. O número de página à direita no rodapé permanece.

## Fluxo

1. Médica clica "Assinar" no painel.
2. Painel busca o PDF-base (endpoint read-only que renderiza `PreparandoAssinatura` **sem** o
   bloco de reserva). Servidor renderiza uma vez, calcula o hash e **fixa o PDF-base no job**.
3. Painel exibe o PDF com overlay arrastável/redimensionável (pdf.js); default num canto de
   espaço em branco da última página; a médica ajusta ou aceita.
4. `IniciarAsync` recebe a posição escolhida `(pagina, x, y, largura, altura)` em pontos PDF →
   **persistida no job**, junto com o hash do PDF-base fixado.
5. Fluxo Web PKI/agente: `Reivindicar` → `Preparar` usa o **PDF-base fixado** e a posição do job
   ao chamar `pades/preparar` (assinador põe o appearance ali) → cliente assina → `Concluir`
   injeta o CMS. A trava de CPF do `ConcluirAsync` **não muda**.
6. `AguardandoAprovacao` → a médica confere o PDF assinado final e aprova.

## Consequências

- **Escopo multi-repo + migration.** Toca:
  - `SMSMais.server` — `LaudoPdfRenderer` (remover reserva); `LaudoAssinaturaService`/
    `IAssinadorPdfPades`/`AssinadorPdfHttpClient` + DTOs (posição + PDF-base fixado);
    `LaudoAssinatura` ganha campos (`CarimboPagina`, `CarimboX/Y/Larg/Alt`, `PdfBaseFixado`,
    `PdfBaseHash`) → **migration nova** (imutável, pasta única).
  - `Automais.Assinador` — `PreparacaoRequisicao`/`PrepararRequest` ganham `Pagina` + `Retangulo`
    opcionais; `MontarRect`/`SetPageNumber` os usam quando presentes. **Backward-compatible**:
    chamador sem posição mantém o comportamento atual. Deploy próprio (`automais-assinador.service`).
  - `SMSMais.front` (feature assinatura) — viewer pdf.js + caixa arrastável/redimensionável +
    conversão de coordenadas. É o grosso do trabalho, porém contido.
- **Guarda de sobreposição:** posição livre permite cobrir texto clínico. MVP: aviso na UI se o
  retângulo intersecta texto (o renderer sabe onde o conteúdo termina). Guarda dura (impedir)
  fica para v2.
- **Re-assinatura/rejeição:** posição e PDF-base ficam no job; ao reassinar, reusa (ou repõe). O
  hardening de digest-mismatch do `Concluir` continua valendo, agora sobre o PDF fixado.
- **Testes:** `LaudoPdfRodapeTests` já dá o harness — adicionar "NOTAS longas não geram página em
  branco" e "sem bloco de reserva no modo assinatura".
- **Sem mudança na semântica médico-legal:** identidade do médico segue **exclusivamente** no
  carimbo da assinatura digital (agora móvel), nunca em texto solto; a validação de autoria por
  CPF do certificado permanece intacta.
- **Sem reinstalação do agente no PC da médica.** O agente local (`Automais.Assinador.Agente`) só
  assina um *hash* — nunca vê o PDF, o carimbo ou coordenadas. A posição é decidida no painel e
  aplicada no servidor durante o `preparar`, antes de o hash chegar ao agente. O contrato do
  agente (`reivindicar`/`preparar`/`concluir`) é mantido **idêntico** por desenho: a posição entra
  pelo `IniciarAsync` (painel → servidor), nunca pelo agente. O `.exe` e o registro do protocolo
  `automais-assinador://` permanecem como estão; para a médica, o único novo é um passo na tela
  (arrastar/redimensionar o carimbo), entregue pelo deploy do front.
- **Reversão:** restaurar `ReservaCarimboPt` no renderer e a posição fixa em `MontarRect`;
  ignorar os campos de posição no job. O contrato do assinador é aditivo, então versões antigas
  do chamador seguem funcionando.
