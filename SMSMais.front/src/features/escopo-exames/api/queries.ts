import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  adicionarEscopo,
  atualizarEscopo,
  listarEscopoDaUnidade,
  listarPendenciasEscopo,
  listarUnidadesDoTipo,
  removerEscopo,
  rodarBackfillEscopo,
} from '@/features/escopo-exames/api/escopoExamesApi';
import type {
  AdicionarEscopoPayload,
  AtualizarEscopoPayload,
} from '@/features/escopo-exames/types';

export const escopoExamesKeys = {
  raiz: ['escopo-exames'] as const,
  daUnidade: (unidadeId: string, incluirInativos?: boolean) =>
    ['escopo-exames', 'unidade', unidadeId, { incluirInativos: !!incluirInativos }] as const,
  doTipo: (tipoExameId: string) => ['escopo-exames', 'tipo', tipoExameId] as const,
  pendencias: (unidadeId?: string) =>
    ['escopo-exames', 'pendencias', { unidadeId: unidadeId ?? null }] as const,
};

export function useEscopoDaUnidade(unidadeId: string, incluirInativos = false) {
  return useQuery({
    queryKey: escopoExamesKeys.daUnidade(unidadeId, incluirInativos),
    queryFn: () => listarEscopoDaUnidade(unidadeId, incluirInativos),
    enabled: !!unidadeId,
  });
}

export function useUnidadesDoTipo(tipoExameId: string) {
  return useQuery({
    queryKey: escopoExamesKeys.doTipo(tipoExameId),
    queryFn: () => listarUnidadesDoTipo(tipoExameId),
    enabled: !!tipoExameId,
  });
}

export function usePendenciasEscopo(unidadeId?: string) {
  return useQuery({
    queryKey: escopoExamesKeys.pendencias(unidadeId),
    queryFn: () => listarPendenciasEscopo(unidadeId),
  });
}

export function useAdicionarEscopo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AdicionarEscopoPayload) => adicionarEscopo(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: escopoExamesKeys.raiz }),
  });
}

export function useAtualizarEscopo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarEscopoPayload }) =>
      atualizarEscopo(id, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: escopoExamesKeys.raiz }),
  });
}

export function useRemoverEscopo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => removerEscopo(id),
    onSuccess: () => client.invalidateQueries({ queryKey: escopoExamesKeys.raiz }),
  });
}

export function useBackfillEscopo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (simular: boolean) => rodarBackfillEscopo(simular),
    onSuccess: (_, simular) => {
      if (!simular) client.invalidateQueries({ queryKey: escopoExamesKeys.raiz });
    },
  });
}
