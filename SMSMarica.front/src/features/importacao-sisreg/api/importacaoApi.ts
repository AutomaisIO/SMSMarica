import { http } from '@/shared/api/httpClient';
import type {
  ImportacaoExecucao,
  ImportacaoExecucaoResultado,
  ImportacaoFalha,
  ImportacaoFalhaDetalhe,
  ImportacaoFalhaReprocessoResultado,
  ImportacaoLoteAceito,
  ImportacaoPreviewResultado,
  StatusLote,
} from '@/features/importacao-sisreg/types';

/** Preview a partir do upload do export de agendamentos do SISREG (TXT ou CSV). Só leitura. */
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

/** Linhas que não viraram solicitação. Só leitura. */
export async function listarFalhasImportacao(somentePendentes: boolean): Promise<ImportacaoFalha[]> {
  const { data } = await http.get<ImportacaoFalha[]>('/sisreg/importacao/falhas', {
    params: { somentePendentes },
  });
  return data;
}

/** "Validar": reimporta a linha a partir do RAW guardado — dispensa reenviar o arquivo. ESCRITA. */
export async function reprocessarFalhaImportacao(
  id: string,
): Promise<ImportacaoFalhaReprocessoResultado> {
  const { data } = await http.post<ImportacaoFalhaReprocessoResultado>(
    `/sisreg/importacao/falhas/${id}/reprocessar`,
  );
  return data;
}

/** Tira a linha da lista sem importar (inválida na origem, registro cancelado…). */
export async function descartarFalhaImportacao(id: string, nota?: string): Promise<void> {
  await http.post(`/sisreg/importacao/falhas/${id}/descartar`, { nota: nota ?? null });
}

/** Detalhe da falha para o modal: RAW + parse (campos nomeados + unidades). */
export async function obterFalhaDetalhe(id: string): Promise<ImportacaoFalhaDetalhe> {
  const { data } = await http.get<ImportacaoFalhaDetalhe>(`/sisreg/importacao/falhas/${id}/detalhe`);
  return data;
}

/** Envia vários arquivos (seleção múltipla, pasta ou .zip) para importação em lote no servidor. */
export async function importarLote(arquivos: File[]): Promise<ImportacaoLoteAceito> {
  const form = new FormData();
  for (const f of arquivos) form.append('arquivos', f);
  const { data } = await http.post<ImportacaoLoteAceito>('/sisreg/importacao/lote', form, {
    headers: { 'Content-Type': undefined },
  });
  return data;
}

/** Progresso do lote. O backend responde 204 (corpo vazio) quando nunca houve importação. */
export async function obterStatusLote(): Promise<StatusLote | null> {
  const { data } = await http.get<StatusLote | ''>('/sisreg/importacao/lote/status');
  return data ? data : null;
}

export async function cancelarLote(): Promise<void> {
  await http.post('/sisreg/importacao/lote/cancelar');
}

/** Aba de rastreio: uma linha por arquivo importado. */
export async function listarExecucoesImportacao(limite = 100): Promise<ImportacaoExecucao[]> {
  const { data } = await http.get<ImportacaoExecucao[]>('/sisreg/importacao/execucoes', {
    params: { limite },
  });
  return data;
}
