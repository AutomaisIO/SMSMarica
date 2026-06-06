import { http } from '@/shared/api/httpClient';
import type {
  BasePep,
  ExecucaoImportacao,
  IniciarImportacaoPayload,
  StatusImportacao,
} from '@/features/pep-sincronizacao/types';

export async function listarBasesPep(): Promise<BasePep[]> {
  const { data } = await http.get<BasePep[]>('/pep-sincronizacao/bases');
  return data;
}

export async function iniciarImportacaoPep(
  payload: IniciarImportacaoPayload,
): Promise<{ execucaoId: string }> {
  const { data } = await http.post<{ execucaoId: string }>('/pep-sincronizacao/importar', payload);
  return data;
}

export async function obterStatusPep(): Promise<StatusImportacao> {
  const { data } = await http.get<StatusImportacao>('/pep-sincronizacao/status');
  return data;
}

export async function listarExecucoesPep(fonteId?: string): Promise<ExecucaoImportacao[]> {
  const { data } = await http.get<ExecucaoImportacao[]>('/pep-sincronizacao/execucoes', {
    params: fonteId ? { fonteId } : undefined,
  });
  return data;
}
