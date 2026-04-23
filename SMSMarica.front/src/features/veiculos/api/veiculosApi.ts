import { http } from '@/shared/api/httpClient';
import type { VeiculoListItem } from '@/features/veiculos/types';

export async function listarVeiculos(): Promise<VeiculoListItem[]> {
  const { data } = await http.get<VeiculoListItem[]>('/veiculos');
  return data;
}
