import { http } from '@/shared/api/httpClient';
import type { RastreamentoListItem } from '@/features/rastreamento/types';

export async function listarRastreamento(): Promise<RastreamentoListItem[]> {
  const { data } = await http.get<RastreamentoListItem[]>('/rastreamento');
  return data;
}
