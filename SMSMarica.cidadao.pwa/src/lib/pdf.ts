import { http } from './httpClient';
import { usePdfViewer } from '@/store/pdfViewer';

/**
 * Abre um PDF protegido por JWT DENTRO do app (visualizador embutido com pdf.js), com
 * botão de baixar — sem depender do leitor de PDF do celular (muitos idosos não têm).
 * Busca os bytes autenticados e entrega ao <VisualizadorPdf/> montado no AppShell.
 */
export async function abrirPdf(url: string, nomePadrao = 'documento.pdf'): Promise<void> {
  const resp = await http.get(url, { responseType: 'arraybuffer' });
  const nome = nomeDoHeader(resp.headers?.['content-disposition']) ?? nomePadrao;
  usePdfViewer.getState().abrir(resp.data as ArrayBuffer, nome);
}

/** Baixa um PDF protegido por JWT como arquivo. */
export async function baixarPdf(url: string, nomePadrao = 'documento.pdf'): Promise<void> {
  const resp = await http.get(url, { responseType: 'blob' });
  const blobUrl = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));
  baixarBlob(blobUrl, nomeDoHeader(resp.headers?.['content-disposition']) ?? nomePadrao);
  setTimeout(() => URL.revokeObjectURL(blobUrl), 10_000);
}

function baixarBlob(blobUrl: string, nome: string): void {
  const a = document.createElement('a');
  a.href = blobUrl;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
}

function nomeDoHeader(disposition?: string): string | null {
  if (!disposition) return null;
  const m = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  return m?.[1] ? decodeURIComponent(m[1]) : null;
}
