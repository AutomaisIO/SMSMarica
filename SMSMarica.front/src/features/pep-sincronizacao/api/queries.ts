import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  iniciarImportacaoPep,
  listarBasesPep,
  listarExecucoesPep,
  obterStatusPep,
} from '@/features/pep-sincronizacao/api/pepApi';
import type { IniciarImportacaoPayload } from '@/features/pep-sincronizacao/types';

export const pepKeys = {
  bases: ['pep', 'bases'] as const,
  status: ['pep', 'status'] as const,
  execucoes: (fonteId?: string) => ['pep', 'execucoes', fonteId ?? 'todas'] as const,
};

export function useBasesPep() {
  return useQuery({ queryKey: pepKeys.bases, queryFn: listarBasesPep });
}

/** Status com polling em tempo real: 1s enquanto há run vivo, senão a cada 15s. */
export function useStatusPep() {
  return useQuery({
    queryKey: pepKeys.status,
    queryFn: obterStatusPep,
    refetchInterval: (query) => (query.state.data?.emExecucao ? 1000 : 15000),
  });
}

export function useExecucoesPep(fonteId?: string) {
  return useQuery({
    queryKey: pepKeys.execucoes(fonteId),
    queryFn: () => listarExecucoesPep(fonteId),
  });
}

export function useIniciarImportacaoPep() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: IniciarImportacaoPayload) => iniciarImportacaoPep(payload),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: pepKeys.status });
      client.invalidateQueries({ queryKey: ['pep', 'execucoes'] });
    },
  });
}
