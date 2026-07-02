import { http } from '@/shared/api/httpClient';
import type {
  ImportacaoExecucaoResultado,
  ImportacaoPreviewResultado,
} from '@/features/importacao-sisreg/types';

/** Preview a partir do upload do TXT (Arquivo Agendamento do SISREG). Só leitura. */
export async function previewImportacaoTxt(arquivo: File): Promise<ImportacaoPreviewResultado> {
  const form = new FormData();
  form.append('arquivo', arquivo);
  // Remove o Content-Type application/json padrão do client para o axios setar
  // multipart/form-data + boundary a partir do FormData (senão o backend responde 415).
  const { data } = await http.post<ImportacaoPreviewResultado>('/sisreg/importacao/preview', form, {
    headers: { 'Content-Type': undefined },
  });
  return data;
}

/** Importa UMA marcação (por código) — roda o fluxo inteiro daquele registro. ESCRITA. */
export async function executarImportacaoTxt(
  arquivo: File,
  codigo: string,
): Promise<ImportacaoExecucaoResultado> {
  const form = new FormData();
  form.append('arquivo', arquivo);
  form.append('codigo', codigo);
  const { data } = await http.post<ImportacaoExecucaoResultado>('/sisreg/importacao/executar', form, {
    headers: { 'Content-Type': undefined },
  });
  return data;
}
