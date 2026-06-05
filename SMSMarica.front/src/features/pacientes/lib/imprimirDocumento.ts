/**
 * Geração da view de impressão / "Salvar como PDF" de um documento de atendimento.
 *
 * O navegador é a fonte do PDF (Salvar como PDF na caixa de impressão): texto
 * vetorial, sem dependência extra. O mesmo CSS (`EDOC_CSS`) estiliza o modal no
 * app e a janela de impressão — fonte única, nada de "prose" do Tailwind.
 */

/** CSS dos documentos remontados (classes .edoc-* emitidas pelo importador). Escopo `.edoc-render`. */
export const EDOC_CSS = `
.edoc-render { color:#1f2937; font-size:14px; line-height:1.55; }
.edoc-render .edoc-doc + .edoc-doc { margin-top:1.5rem; border-top:1px solid #e5e7eb; padding-top:1rem; }
.edoc-render .edoc-titulo { font-size:15px; font-weight:600; color:#b91c1c; margin:0 0 .75rem; padding-bottom:.35rem; border-bottom:2px solid #fca5a5; }
.edoc-render .edoc-campo { display:grid; grid-template-columns:minmax(130px,28%) 1fr; gap:.2rem 1.25rem; padding:.4rem 0; border-bottom:1px solid #f3f4f6; }
.edoc-render .edoc-campo:last-child { border-bottom:0; }
.edoc-render .edoc-rotulo { font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.03em; color:#6b7280; }
.edoc-render .edoc-valor { color:#111827; white-space:pre-wrap; word-break:break-word; }
.edoc-render .edoc-linha + .edoc-linha { margin-top:.15rem; }
.edoc-render .edoc-vazio { color:#9ca3af; font-style:italic; }
`;

export type MedicamentoImpressao = {
  descricao: string;
  posologia?: string | null;
  urgente: boolean;
};

export type DadosImpressao = {
  pacienteNome: string;
  pacienteCpf?: string | null;
  atendimentoTipo?: string;
  atendimentoData?: string | null;
  medicoNome?: string | null;
  documentoTitulo: string;
  /** HTML já remontado do documento (vem do hub, valores escapados na origem). */
  conteudoHtml: string;
  medicamentos?: MedicamentoImpressao[];
};

function escapar(v: string): string {
  return v
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function nomeArquivo(d: DadosImpressao): string {
  const base = `${d.documentoTitulo}_${d.pacienteNome}`
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .replace(/[^a-zA-Z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '');
  return base || 'documento';
}

function blocoMedicamentos(meds?: MedicamentoImpressao[]): string {
  if (!meds || meds.length === 0) return '';
  const linhas = meds
    .map((m) => {
      const urg = m.urgente ? '<span class="urg">Urgente</span> ' : '';
      const pos = m.posologia ? `<div class="pos">${escapar(m.posologia)}</div>` : '';
      return `<li>${urg}<strong>${escapar(m.descricao)}</strong>${pos}</li>`;
    })
    .join('');
  return `
    <section class="edoc-doc">
      <h3 class="edoc-titulo">Prescrição / Medicamentos</h3>
      <ul class="meds">${linhas}</ul>
    </section>`;
}

/** Monta o documento HTML completo e imprimível (cabeçalho institucional + conteúdo). */
export function montarDocumentoImprimivel(d: DadosImpressao): string {
  const metaLinhas = [
    d.atendimentoTipo && `Atendimento: ${escapar(d.atendimentoTipo)}`,
    d.atendimentoData && `Data: ${escapar(d.atendimentoData)}`,
    d.medicoNome && `Profissional: ${escapar(d.medicoNome)}`,
  ]
    .filter(Boolean)
    .join(' &nbsp;·&nbsp; ');

  return `<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="utf-8" />
<title>${escapar(nomeArquivo(d))}</title>
<style>
  @page { size:A4; margin:16mm; }
  * { box-sizing:border-box; }
  body { margin:0; font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif; color:#111827; }
  .cabecalho { border-bottom:2px solid #b91c1c; padding-bottom:10px; margin-bottom:16px; }
  .cabecalho .org { font-size:13px; font-weight:700; color:#b91c1c; text-transform:uppercase; letter-spacing:.03em; }
  .cabecalho .doc-titulo { font-size:18px; font-weight:700; margin:6px 0 8px; }
  .cabecalho .paciente { font-size:14px; font-weight:600; }
  .cabecalho .meta { font-size:12px; color:#6b7280; margin-top:2px; }
  .meds { list-style:none; padding:0; margin:0; }
  .meds li { padding:.4rem 0; border-bottom:1px solid #f3f4f6; }
  .meds li:last-child { border-bottom:0; }
  .meds .pos { font-size:12px; color:#4b5563; margin-top:2px; }
  .meds .urg { display:inline-block; font-size:10px; font-weight:700; text-transform:uppercase; color:#b91c1c; border:1px solid #fca5a5; border-radius:4px; padding:0 4px; margin-right:4px; }
  .rodape { margin-top:24px; padding-top:8px; border-top:1px solid #e5e7eb; font-size:11px; color:#9ca3af; text-align:center; }
  ${EDOC_CSS}
</style>
</head>
<body>
  <div class="cabecalho">
    <div class="org">Secretaria Municipal de Saúde de Maricá</div>
    <div class="doc-titulo">${escapar(d.documentoTitulo)}</div>
    <div class="paciente">${escapar(d.pacienteNome)}${d.pacienteCpf ? ` — CPF ${escapar(d.pacienteCpf)}` : ''}</div>
    ${metaLinhas ? `<div class="meta">${metaLinhas}</div>` : ''}
  </div>
  <div class="edoc-render">
    ${d.conteudoHtml}
    ${blocoMedicamentos(d.medicamentos)}
  </div>
  <div class="rodape">Documento gerado pelo SMS Maricá a partir do histórico clínico (origem: Salux).</div>
  <script>window.onload = function () { window.focus(); window.print(); };</script>
</body>
</html>`;
}

/** Abre a janela de impressão (serve tanto para imprimir quanto para "Salvar como PDF"). */
export function abrirImpressaoDocumento(d: DadosImpressao): void {
  const win = window.open('', '_blank', 'width=900,height=720');
  if (!win) {
    alert('Permita pop-ups para este site para imprimir ou baixar o PDF.');
    return;
  }
  win.document.open();
  win.document.write(montarDocumentoImprimivel(d));
  win.document.close();
}
