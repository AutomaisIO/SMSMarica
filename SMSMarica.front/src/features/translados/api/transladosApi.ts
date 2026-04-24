import { http } from '@/shared/api/httpClient';
import type { RotaDiaria, RotaDiariaListItem } from '@/features/translados/types';

export async function listarRotas(data?: string): Promise<RotaDiariaListItem[]> {
  const params = data ? { data } : undefined;
  const { data: lista } = await http.get<RotaDiariaListItem[]>('/rotas', { params });
  return lista;
}

export async function obterRotaPorId(id: string): Promise<RotaDiaria> {
  const { data } = await http.get<RotaDiaria>(`/rotas/${id}`);
  return data;
}

export async function iniciarRota(id: string): Promise<void> {
  await http.post(`/rotas/${id}/iniciar`);
}

export async function concluirRota(id: string): Promise<void> {
  await http.post(`/rotas/${id}/concluir`);
}

export async function cancelarRota(id: string): Promise<void> {
  await http.delete(`/rotas/${id}`);
}
