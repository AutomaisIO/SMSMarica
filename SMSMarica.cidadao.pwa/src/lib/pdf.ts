import { http } from './httpClient';

/**
 * Abre um PDF protegido por JWT. Popups não herdam o token do interceptor axios,
 * então buscamos como blob e abrimos via object URL. Se o navegador bloquear a aba
 * (comum no iOS/standalone), cai para download direto do arquivo.
 */
export async function abrirPdf(url: string, nomePadrao = 'documento.pdf'): Promise<void> {
  const resp = await http.get(url, { responseType: 'blob' });
  const blobUrl = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));

  const aba = window.open(blobUrl, '_blank');
  if (!aba) baixarBlob(blobUrl, nomeDoHeader(resp.headers?.['content-disposition']) ?? nomePadrao);

  setTimeout(() => URL.revokeObjectURL(blobUrl), 60_000);
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
