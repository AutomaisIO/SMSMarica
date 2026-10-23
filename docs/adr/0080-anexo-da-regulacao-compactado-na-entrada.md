# ADR-0080 — Anexo da regulação compactado na entrada: só a cópia enxuta é guardada, depois de quem anexou conferir

- **Status:** Aceito
- **Data:** 2026-10-23
- **Contexto módulo:** Regulação — anexos da solicitação ([ADR-0052](./0052-fila-pre-regulacao-e-agente-regulador.md))
- **Relacionado:** [ADR-0066](./0066-acervo-do-paciente-e-midia-do-whatsapp.md) (acervo do paciente),
  [ADR-0069](./0069-envio-automatico-ao-ser.md) §6 (compressão no envio ao SER/SERNIT)

## Contexto

Até aqui o anexo da solicitação era guardado **como chegou** — foto de celular de 3–5 MB, PDF
escaneado de 10 MB — e a compressão só acontecia no envio ao SER/SERNIT, sobre uma cópia
(ADR-0069 §6). O arquivo inteiro ficava no Spaces e, de novo, no cadastro do paciente (a cópia do
acervo, ADR-0066). O pedido do Bernardo (23/10/2026): usar o mesmo compactador **já na entrada**,
para limitar e preservar o espaço, e mostrar o resultado para a pessoa aceitar ou recusar.

## Decisão

1. **Todo upload na solicitação da regulação é compactado antes de guardar** — o assistente da
   unidade e o "Anexar documento para a unidade" da regulação. O original **não é guardado**.
   - Foto (JPEG, PNG, WebP): JPEG com lado maior de 2400 px e qualidade 75, **de pé** (a marca de
     orientação do EXIF é aplicada — sem isso a foto do celular era regravada deitada).
   - PDF: Ghostscript em 200 dpi, qualidade média (o degrau medido em 21/10 que levou 10,4 MB a
     2,32 MB legíveis).
   - O que não diminuiu ao menos 10% vai como chegou (PDF digital costuma cair aqui); falha da
     compactação nunca bloqueia o anexo.
2. **Um degrau fixo, não a escada até um limite.** O objetivo aqui é não guardar peso à toa; caber
   no limite do destino continua sendo do envio, que só comprime de novo se o arquivo, mesmo
   enxuto, ainda passar dele.
3. **Quem anexa confere antes.** O upload vira uma **prévia**: o servidor compacta e guarda o
   resultado só em memória por 30 minutos; a pessoa vê o arquivo no visualizador, com o tamanho de
   antes e depois, e escolhe "Está legível" (aceita) ou "Não está legível — descartar". Fechar a
   prévia é descartar. Nada entra na solicitação, no Spaces ou no cadastro do paciente sem o aceite.
   A prévia é amarrada à solicitação e a quem a preparou; a de outra pessoa responde como expirada.
4. **O limite de tamanho da configuração vale para o arquivo como chegou**, como antes. Aceitar um
   arquivo maior porque ele compactaria fica para outra decisão.

## Consequências

- O que vai para o regulador e para o cadastro do paciente é exatamente o que a pessoa conferiu.
- Sem o original, não há como "voltar à versão boa" se a compactação estragar um documento — por
  isso a prévia é obrigatória e o degrau é folgado. Se aparecer documento que fica ilegível em
  200 dpi / 2400 px, o remédio é um botão "enviar sem compactar", não descer o degrau.
- A prévia mora na memória do processo da API: reiniciar o serviço no meio perde as prévias em
  aberto (a pessoa anexa de novo). Cabe numa instância por município ([ADR-0043](./0043-instancia-por-municipio.md)); não cabe se a API
  um dia rodar em mais de uma réplica.
- Fora do escopo: anexos de rascunho do SER/SERNIT (telas próprias), anamnese, PWA de arquivos e
  app do cidadão continuam guardando o que chega. Anexos já guardados não são recompactados.

## Implementação

`CompactadorAnexo` (`SMSMais.Core/Regulacao/Anexos/`), `CompressorImagem.CompactarNaEntrada` e
`ICompressorPdf.ComprimirNoDegrauAsync` (`SMSMais.Core/Regulacao/EnvioSer/`); prévia em
`RegulacaoExigenciaService` (`PrepararAsync` / `ObterPreparadoAsync` / `AceitarPreparadoAsync` /
`DescartarPreparadoAsync`) com os endpoints
`POST …/exigencias/{exigenciaId}/arquivos/preparar`, `GET …/exigencias/preparados/{token}/conteudo`,
`POST …/exigencias/preparados/{token}/aceitar` e `DELETE …/exigencias/preparados/{token}`. No front,
`shared/acervo/PreviaCompactacao` e a prop `compactar` do `UploadAnexo`.
