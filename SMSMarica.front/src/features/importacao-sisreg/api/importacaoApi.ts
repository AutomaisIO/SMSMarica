import { http } from '@/shared/api/httpClient';
import type { ImportacaoPreviewResultado } from '@/features/importacao-sisreg/types';

/** Preview do período (só leitura) — o diff das marcações SISREG vs a nossa base. */
export async function previewImportacao(
  inicio: string,
  fim: string,
): Promise<ImportacaoPreviewResultado> {
  const { data } = await http.get<ImportacaoPreviewResultado>('/sisreg/importacao/preview', {
    params: { inicio, fim },
  });
  return data;
}
