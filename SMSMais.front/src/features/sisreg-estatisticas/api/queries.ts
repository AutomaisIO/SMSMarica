import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { Periodo } from '../types';
import {
  obterConfiguracaoOperadores,
  obterEquipe,
  obterIndividual,
  salvarOperadoresHabilitados,
} from './estatisticasApi';

export const estatisticasKeys = {
  todas: ['sisreg', 'estatisticas'] as const,
  equipe: (p: Periodo) => ['sisreg', 'estatisticas', 'equipe', p.de, p.ate] as const,
  individual: (chave: string, p: Periodo) => ['sisreg', 'estatisticas', 'individual', chave, p.de, p.ate] as const,
  configuracao: ['sisreg', 'estatisticas', 'configuracao'] as const,
};

/** A leitura pode levar alguns segundos num período longo: não refaz a cada foco de janela. */
const TEMPO_FRESCO = 5 * 60_000;

export function useEquipe(periodo: Periodo) {
  return useQuery({
    queryKey: estatisticasKeys.equipe(periodo),
    queryFn: () => obterEquipe(periodo),
    staleTime: TEMPO_FRESCO,
  });
}

export function useIndividual(chave: string | null, periodo: Periodo) {
  return useQuery({
    queryKey: estatisticasKeys.individual(chave ?? '', periodo),
    queryFn: () => obterIndividual(chave!, periodo),
    enabled: Boolean(chave),
    staleTime: TEMPO_FRESCO,
  });
}

export function useConfiguracaoOperadores(habilitado: boolean) {
  return useQuery({
    queryKey: estatisticasKeys.configuracao,
    queryFn: obterConfiguracaoOperadores,
    enabled: habilitado,
  });
}

export function useSalvarOperadoresHabilitados() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: salvarOperadoresHabilitados,
    onSuccess: (dados) => {
      qc.setQueryData(estatisticasKeys.configuracao, dados);
      // Quem entra mudou: todos os números da equipe e do individual mudam junto.
      void qc.invalidateQueries({ queryKey: estatisticasKeys.todas });
    },
  });
}
