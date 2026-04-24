import { http } from '@/shared/api/httpClient';
import type {
  AdicionarSessaoPayload,
  AtualizarSessaoPayload,
  AtualizarTratamentoPayload,
  CadastrarTratamentoPayload,
  ConfirmarSessaoPayload,
  ExpandirPeriodicidadePayload,
  TipoTratamento,
  Tratamento,
  TratamentoListItem,
} from '@/features/tratamentos/types';

export async function listarTratamentos(pacienteId?: string): Promise<TratamentoListItem[]> {
  const params = pacienteId ? { pacienteId } : undefined;
  const { data } = await http.get<TratamentoListItem[]>('/tratamentos', { params });
  return data;
}

export async function listarTiposTratamento(): Promise<TipoTratamento[]> {
  const { data } = await http.get<TipoTratamento[]>('/tratamentos/tipos');
  return data;
}

export async function obterTratamentoPorId(id: string): Promise<Tratamento> {
  const { data } = await http.get<Tratamento>(`/tratamentos/${id}`);
  return data;
}

export async function expandirPeriodicidade(payload: ExpandirPeriodicidadePayload): Promise<string[]> {
  const { data } = await http.post<string[]>('/tratamentos/periodicidade/expandir', payload);
  return data;
}

export async function cadastrarTratamento(payload: CadastrarTratamentoPayload): Promise<string> {
  const { data } = await http.post<string>('/tratamentos', payload);
  return data;
}

export async function atualizarTratamento(id: string, payload: AtualizarTratamentoPayload): Promise<void> {
  await http.put(`/tratamentos/${id}`, payload);
}

export async function encerrarTratamento(id: string): Promise<void> {
  await http.delete(`/tratamentos/${id}`);
}

export async function adicionarSessao(id: string, payload: AdicionarSessaoPayload): Promise<string> {
  const { data } = await http.post<string>(`/tratamentos/${id}/sessoes`, payload);
  return data;
}

export async function atualizarSessao(
  id: string,
  sessaoId: string,
  payload: AtualizarSessaoPayload,
): Promise<void> {
  await http.put(`/tratamentos/${id}/sessoes/${sessaoId}`, payload);
}

export async function cancelarSessao(id: string, sessaoId: string): Promise<void> {
  await http.delete(`/tratamentos/${id}/sessoes/${sessaoId}`);
}

export async function confirmarSessao(
  id: string,
  sessaoId: string,
  payload: ConfirmarSessaoPayload,
): Promise<void> {
  await http.post(`/tratamentos/${id}/sessoes/${sessaoId}/confirmar`, payload);
}
