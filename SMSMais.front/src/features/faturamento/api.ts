import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';
import type {
  ConfigFaturamento,
  DimensaoFaturamento,
  RegistroFaturamento,
  ResumoFaturamento,
} from '@/features/faturamento/types';

const keys = {
  registros: (competencia?: number) => ['faturamento', 'registros', competencia ?? null] as const,
  resumo: (dimensao: string, competencia?: number) =>
    ['faturamento', 'resumo', dimensao, competencia ?? null] as const,
  config: ['faturamento', 'config'] as const,
};

export function useRegistrosFaturamento(competencia?: number) {
  return useQuery({
    queryKey: keys.registros(competencia),
    queryFn: async () =>
      (await http.get<RegistroFaturamento[]>('/faturamento/registros', {
        params: competencia ? { competencia } : undefined,
      })).data,
  });
}

export function useResumoFaturamento(dimensao: DimensaoFaturamento, competencia?: number) {
  return useQuery({
    queryKey: keys.resumo(dimensao, competencia),
    queryFn: async () =>
      (await http.get<ResumoFaturamento>('/faturamento/resumo', {
        params: { dimensao, ...(competencia ? { competencia } : {}) },
      })).data,
  });
}

export function useConfigFaturamento() {
  return useQuery({
    queryKey: keys.config,
    queryFn: async () => (await http.get<ConfigFaturamento>('/faturamento/config')).data,
  });
}

export function useSalvarConfigFaturamento() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: ConfigFaturamento) => http.put('/faturamento/config', payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.config }),
  });
}
