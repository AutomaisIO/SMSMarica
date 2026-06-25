import { abrirJanelaSolta } from '@/shared/lib/janela';
import { http } from '@/shared/api/httpClient';

/**
 * Abre o PDF de um documento anexado em janela separada. Como o endpoint
 * `/anexos/{id}/conteudo` exige bearer no header (e popups não carregam o token
 * do interceptor axios), baixamos o PDF como blob e abrimos uma blob URL.
 */
export async function abrirPdfAnexo(anexoId: string): Promise<void> {
  const resp = await http.get(`/anexos/${anexoId}/conteudo`, { responseType: 'blob' });
  const url = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));
  const ok = abrirJanelaSolta(url, `anexo-${anexoId}`, 1000, 900);
  if (!ok) {
    alert('A janela do documento foi bloqueada pelo navegador. Libere os popups para este site.');
    URL.revokeObjectURL(url);
    return;
  }
  // Libera o blob URL depois que o navegador tem tempo de carregar.
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/** Formata um tamanho em bytes para exibição (KB/MB). */
export function formatarTamanhoBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '—';
  if (bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb.toFixed(0)} KB`;
  return `${(kb / 1024).toFixed(1)} MB`;
}
