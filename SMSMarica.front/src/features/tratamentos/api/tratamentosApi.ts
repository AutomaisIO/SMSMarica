import { http } from '@/shared/api/httpClient';
import type { TratamentoListItem } from '@/features/tratamentos/types';

export async function listarTratamentos(): Promise<TratamentoListItem[]> {
  const { data } = await http.get<TratamentoListItem[]>('/tratamentos');
  return data;
}
