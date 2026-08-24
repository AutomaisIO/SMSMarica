import { useQuery } from '@tanstack/react-query';
import { obterEstatisticasExamesImagem } from '@/features/relatorios-imagem/api/relatoriosImagemApi';
import type { ModalidadeDicom } from '@/features/tipos-exame/types';

export const relatoriosImagemKeys = {
  dashboard: (de: string, ate: string, unidadeId: string, modalidade: string, tipoExameId: string) =>
    ['relatorios-imagem', 'dashboard', de, ate, unidadeId, modalidade, tipoExameId] as const,
};

export function useEstatisticasExamesImagem(
  de: string,
  ate: string,
  unidadeId: string,
  modalidade: ModalidadeDicom | '',
  tipoExameId: string,
) {
  return useQuery({
    queryKey: relatoriosImagemKeys.dashboard(de, ate, unidadeId, modalidade, tipoExameId),
    queryFn: () =>
      obterEstatisticasExamesImagem(
        de,
        ate,
        unidadeId || undefined,
        modalidade || undefined,
        tipoExameId || undefined,
      ),
    enabled: Boolean(de) && Boolean(ate),
  });
}
