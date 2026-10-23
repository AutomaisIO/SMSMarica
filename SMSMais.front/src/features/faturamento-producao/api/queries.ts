import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  definirUnidadeHabilitada,
  listarConfiguracaoFaturamento,
  listarUnidadesHabilitadas,
  obterPreviewBpa,
} from '@/features/faturamento-producao/api/faturamentoProducaoApi';

export const faturamentoProducaoKeys = {
  unidadesHabilitadas: () => ['faturamento-producao', 'unidades-habilitadas'] as const,
  preview: (unidadeId: string, competencia: number) =>
    ['faturamento-producao', 'preview', unidadeId, competencia] as const,
  config: () => ['faturamento-producao', 'config'] as const,
};

export function useUnidadesHabilitadas() {
  return useQuery({
    queryKey: faturamentoProducaoKeys.unidadesHabilitadas(),
    queryFn: listarUnidadesHabilitadas,
  });
}

export function usePreviewBpa(unidadeId: string | null, competencia: number | null) {
  return useQuery({
    queryKey:
      unidadeId && competencia
        ? faturamentoProducaoKeys.preview(unidadeId, competencia)
        : ['faturamento-producao', 'preview', 'nenhum'],
    queryFn: () => obterPreviewBpa(unidadeId as string, competencia as number),
    enabled: Boolean(unidadeId && competencia),
    placeholderData: keepPreviousData,
  });
}

export function useConfiguracaoFaturamento() {
  return useQuery({
    queryKey: faturamentoProducaoKeys.config(),
    queryFn: listarConfiguracaoFaturamento,
  });
}

export function useDefinirUnidadeHabilitada() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ unidadeId, habilitada }: { unidadeId: string; habilitada: boolean }) =>
      definirUnidadeHabilitada(unidadeId, habilitada),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: faturamentoProducaoKeys.config() });
      client.invalidateQueries({ queryKey: faturamentoProducaoKeys.unidadesHabilitadas() });
    },
  });
}
