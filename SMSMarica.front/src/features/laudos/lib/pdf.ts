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
