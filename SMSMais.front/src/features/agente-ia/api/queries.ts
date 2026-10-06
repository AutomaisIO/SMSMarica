import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  arquivarSessao,
  listarSessoes,
  renomearSessao,
  restaurarSessao,
} from '@/features/agente-ia/api/agenteIaApi';
import type { KindSessao } from '@/features/agente-ia/types';

export const agenteIaKeys = {
  raiz: ['agente-ia'] as const,
  sessoes: (arquivadas: boolean, kind: KindSessao) => ['agente-ia', 'sessoes', kind, arquivadas] as const,
};

/**
 * A lista se atualiza sozinha: um turno disparado numa conversa continua no servidor mesmo
 * com o operador olhando outra, e o selo "rodando" precisa refletir isso.
 */
export function useListarSessoes(arquivadas: boolean, kind: KindSessao = 'agente') {
  return useQuery({
    queryKey: agenteIaKeys.sessoes(arquivadas, kind),
    queryFn: () => listarSessoes(arquivadas, kind),
    refetchInterval: 5_000,
  });
}

function useInvalidarSessoes() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: agenteIaKeys.raiz });
}

export function useRenomearSessao() {
  const invalidar = useInvalidarSessoes();
  return useMutation({
    mutationFn: ({ id, titulo }: { id: string; titulo: string }) => renomearSessao(id, titulo),
    onSuccess: invalidar,
  });
}

export function useArquivarSessao() {
  const invalidar = useInvalidarSessoes();
  return useMutation({ mutationFn: arquivarSessao, onSuccess: invalidar });
}

export function useRestaurarSessao() {
  const invalidar = useInvalidarSessoes();
  return useMutation({ mutationFn: restaurarSessao, onSuccess: invalidar });
}
