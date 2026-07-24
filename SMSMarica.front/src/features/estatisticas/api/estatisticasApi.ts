import { http } from '@/shared/api/httpClient';
import type { EstatisticasWhatsApp } from '@/features/estatisticas/types';

export async function obterEstatisticasWhatsApp(
  de?: string,
  ate?: string,
): Promise<EstatisticasWhatsApp> {
  const { data } = await http.get<EstatisticasWhatsApp>('/estatisticas/whatsapp', {
    params: { de: de || undefined, ate: ate || undefined },
  });
  return data;
}
