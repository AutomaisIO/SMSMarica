import { http } from './httpClient';
import { usePdfViewer } from '@/store/pdfViewer';
import { obterPdfCache, salvarPdfCache } from './pdfCache';

/**
 * Abre um PDF protegido por JWT DENTRO do app (visualizador embutido com pdf.js), com
 * botão de baixar — sem depender do leitor de PDF do celular (muitos idosos não têm).
 * Materializa o arquivo localmente (IndexedDB): a 1ª abertura baixa e salva; as próximas
 * abrem direto do aparelho, sem rede.
 */
export async function abrirPdf(url: string, nomePadrao = 'documento.pdf'): Promise<void> {
  const cache = await obterPdfCache(url);
  if (cache) {
    usePdfViewer.getState().abrir(cache, nomePadrao);
    return;
  }

  const resp = await http.get(url, { responseType: 'arraybuffer' });
  const dados = resp.data as ArrayBuffer;
  const nome = nomeDoHeader(resp.headers?.['content-disposition']) ?? nomePadrao;
  usePdfViewer.getState().abrir(dados, nome);
  // Salva uma cópia (o pdf.js pode "detachar" o buffer aberto) — best-effort.
  void salvarPdfCache(url, dados.slice(0));
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
