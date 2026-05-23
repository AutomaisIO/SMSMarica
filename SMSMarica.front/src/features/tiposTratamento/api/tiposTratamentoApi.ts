import { http } from '@/shared/api/httpClient';
import type {
  AtualizarTipoTratamentoPayload,
  CadastrarTipoTratamentoPayload,
  TipoTratamento,
  TipoTratamentoListItem,
} from '@/features/tiposTratamento/types';

export async function listarTiposTratamento(somenteAtivos = false): Promise<TipoTratamentoListItem[]> {
  const { data } = await http.get<TipoTratamentoListItem[]>('/tipos-tratamento', {
    params: { somenteAtivos },
  });
  return data;
}

export async function obterTipoTratamentoPorId(id: string): Promise<TipoTratamento> {
  const { data } = await http.get<TipoTratamento>(`/tipos-tratamento/${id}`);
  return data;
}

export async function cadastrarTipoTratamento(payload: CadastrarTipoTratamentoPayload): Promise<string> {
  const { data } = await http.post<string>('/tipos-tratamento', payload);
  return data;
}

export async function atualizarTipoTratamento(
  id: string,
  payload: AtualizarTipoTratamentoPayload,
): Promise<void> {
  await http.put(`/tipos-tratamento/${id}`, payload);
}

export async function desativarTipoTratamento(id: string): Promise<void> {
  await http.delete(`/tipos-tratamento/${id}`);
}
