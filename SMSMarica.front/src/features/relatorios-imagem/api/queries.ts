import { useQuery } from '@tanstack/react-query';
import { obterEstatisticasExamesImagem } from '@/features/relatorios-imagem/api/relatoriosImagemApi';

export const relatoriosImagemKeys = {
  dashboard: (de: string, ate: string, unidadeId: string) =>
    ['relatorios-imagem', 'dashboard', de, ate, unidadeId] as const,
};

export function useEstatisticasExamesImagem(de: string, ate: string, unidadeId: string) {
  return useQuery({
    queryKey: relatoriosImagemKeys.dashboard(de, ate, unidadeId),
    queryFn: () => obterEstatisticasExamesImagem(de, ate, unidadeId || undefined),
    enabled: Boolean(de) && Boolean(ate),
  });
}
