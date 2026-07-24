import { useQuery } from '@tanstack/react-query';
import { obterEstatisticasWhatsApp } from '@/features/estatisticas/api/estatisticasApi';

export const estatisticasKeys = {
  whatsapp: (de: string, ate: string) => ['estatisticas', 'whatsapp', de, ate] as const,
};

export function useEstatisticasWhatsApp(de: string, ate: string) {
  return useQuery({
    queryKey: estatisticasKeys.whatsapp(de, ate),
    queryFn: () => obterEstatisticasWhatsApp(de, ate),
    enabled: Boolean(de) && Boolean(ate),
  });
}
