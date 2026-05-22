import { useMutation, useQuery } from '@tanstack/react-query';
import { buscarEstudos, listarSeries } from '@/features/pacs/api/pacsApi';
import type { FiltroBusca } from '@/features/pacs/types';

export const pacsKeys = {
  series: (studyUID: string) => ['pacs', 'series', studyUID] as const,
};

/** Busca de estudos via mutation (disparada pelo formulário do modal). */
export function useBuscarEstudos() {
  return useMutation({
    mutationFn: (filtro: FiltroBusca) => buscarEstudos(filtro),
  });
}

export function useSeriesDoEstudo(studyUID: string | null) {
  return useQuery({
    queryKey: studyUID ? pacsKeys.series(studyUID) : ['pacs', 'series', 'nenhum'],
    queryFn: () => {
      if (!studyUID) throw new Error('Estudo não informado.');
      return listarSeries(studyUID);
    },
    enabled: Boolean(studyUID),
  });
}
