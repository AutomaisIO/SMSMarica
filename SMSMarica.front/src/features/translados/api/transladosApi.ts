import { http } from '@/shared/api/httpClient';
import type { TransladoListItem } from '@/features/translados/types';

export async function listarTranslados(): Promise<TransladoListItem[]> {
  const { data } = await http.get<TransladoListItem[]>('/translados');
  return data;
}
