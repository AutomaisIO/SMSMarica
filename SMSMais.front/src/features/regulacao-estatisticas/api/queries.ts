import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { FonteExterna, Periodo } from '../types';
import {
  obterConfiguracaoOperadores,
  obterEquipe,
  obterIndividual,
  salvarOperadoresHabilitados,
} from './estatisticasApi';

export const estatisticasKeys = {
  todas: (f: FonteExterna) => ['regulacao', f, 'estatisticas'] as const,
  equipe: (f: FonteExterna, p: Periodo) => ['regulacao', f, 'estatisticas', 'equipe', p.de, p.ate] as const,
  individual: (f: FonteExterna, chave: string, p: Periodo) =>
    ['regulacao', f, 'estatisticas', 'individual', chave, p.de, p.ate] as const,
  configuracao: (f: FonteExterna) => ['regulacao', f, 'estatisticas', 'configuracao'] as const,
};

/** A leitura pode levar alguns segundos num período longo: não refaz a cada foco de janela. */
const TEMPO_FRESCO = 5 * 60_000;

export function useEquipe(fonte: FonteExterna, periodo: Periodo) {
  return useQuery({
    queryKey: estatisticasKeys.equipe(fonte, periodo),
    queryFn: () => obterEquipe(fonte, periodo),
    staleTime: TEMPO_FRESCO,
  });
}

export function useIndividual(fonte: FonteExterna, chave: string | null, periodo: Periodo) {
  return useQuery({
    queryKey: estatisticasKeys.individual(fonte, chave ?? '', periodo),
    queryFn: () => obterIndividual(fonte, chave!, periodo),
    enabled: Boolean(chave),
    staleTime: TEMPO_FRESCO,
  });
}

export function useConfiguracaoOperadores(fonte: FonteExterna, habilitado: boolean) {
  return useQuery({
    queryKey: estatisticasKeys.configuracao(fonte),
    queryFn: () => obterConfiguracaoOperadores(fonte),
    enabled: habilitado,
  });
}

export function useSalvarOperadoresHabilitados(fonte: FonteExterna) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (nomes: string[]) => salvarOperadoresHabilitados(fonte, nomes),
    onSuccess: (dados) => {
      qc.setQueryData(estatisticasKeys.configuracao(fonte), dados);
      // Quem entra mudou: todos os números da equipe e do individual mudam junto.
      void qc.invalidateQueries({ queryKey: estatisticasKeys.todas(fonte) });
    },
  });
}
