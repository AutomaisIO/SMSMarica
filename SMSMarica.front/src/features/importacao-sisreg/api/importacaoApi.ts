import { http } from '@/shared/api/httpClient';
import type { ImportacaoPreviewResultado } from '@/features/importacao-sisreg/types';

/** Preview a partir do upload do TXT (Arquivo Agendamento do SISREG). Só leitura. */
export async function previewImportacaoTxt(arquivo: File): Promise<ImportacaoPreviewResultado> {
  const form = new FormData();
  form.append('arquivo', arquivo);
  const { data } = await http.post<ImportacaoPreviewResultado>('/sisreg/importacao/preview', form);
  return data;
}
