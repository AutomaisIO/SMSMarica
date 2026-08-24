import { http } from '@/shared/api/httpClient';
import type {
  AtualizarAvaliacaoPayload,
  Avaliacao,
  AvaliacaoListItem,
  RegistrarAvaliacaoPayload,
} from '@/features/avaliacoes/types';

export async function listarAvaliacoes(): Promise<AvaliacaoListItem[]> {
  const { data } = await http.get<AvaliacaoListItem[]>('/avaliacoes');
  return data;
}

export async function obterAvaliacaoPorId(id: string): Promise<Avaliacao> {
  const { data } = await http.get<Avaliacao>(`/avaliacoes/${id}`);
  return data;
}

export async function registrarAvaliacao(payload: RegistrarAvaliacaoPayload): Promise<string> {
  const { data } = await http.post<string>('/avaliacoes', payload);
  return data;
}

export async function atualizarAvaliacao(
  id: string,
  payload: AtualizarAvaliacaoPayload,
): Promise<void> {
  await http.put(`/avaliacoes/${id}`, payload);
}

export async function deletarAvaliacao(id: string): Promise<void> {
  await http.delete(`/avaliacoes/${id}`);
}
