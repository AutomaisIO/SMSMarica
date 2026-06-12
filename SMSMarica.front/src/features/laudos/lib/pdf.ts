import { abrirJanelaSolta } from '@/shared/lib/janela';
import { http } from '@/shared/api/httpClient';

/**
 * Abre o PDF do laudo em janela separada. Como o endpoint exige bearer no
 * header, e popups não carregam o token do interceptor axios, baixamos o
 * PDF via axios + abrimos um blob URL na janela popup.
 */
export async function abrirPdfLaudo(laudoId: string): Promise<void> {
  const resp = await http.get(`/laudos/${laudoId}/pdf`, { responseType: 'blob' });
  const url = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));
  const ok = abrirJanelaSolta(url, `laudo-${laudoId}`, 1200, 900);
  if (!ok) {
    alert('A janela do PDF foi bloqueada pelo navegador. Libere os popups para este site.');
    URL.revokeObjectURL(url);
    return;
  }
  // Libera o blob URL depois que o navegador tem tempo de carregar.
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/**
 * Baixa o PDF do laudo como arquivo (download direto, sem abrir janela).
 * Usa o nome vindo do Content-Disposition do backend (ex.: laudo-...-assinado.pdf);
 * fallback para um nome local se o header não vier.
 */
export async function baixarPdfLaudo(laudoId: string): Promise<void> {
  const resp = await http.get(`/laudos/${laudoId}/pdf`, { responseType: 'blob' });

  const disposition: string = resp.headers['content-disposition'] ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const nome = match?.[1] ?? `laudo-${laudoId}.pdf`;

  const url = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
