import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  adicionarSessao,
  atualizarSessao,
  atualizarTratamento,
  cadastrarTratamento,
  cancelarSessao,
  confirmarSessao,
  encerrarTratamento,
  expandirPeriodicidade,
  listarTiposTratamento,
  listarTratamentos,
  obterTratamentoPorId,
} from '@/features/tratamentos/api/tratamentosApi';
import type {
  AdicionarSessaoPayload,
  AtualizarSessaoPayload,
  AtualizarTratamentoPayload,
  CadastrarTratamentoPayload,
  ConfirmarSessaoPayload,
  ExpandirPeriodicidadePayload,
} from '@/features/tratamentos/types';

export type FiltrosTratamentosHook = {
  pacienteId?: string;
  unidadeId?: string;
};

export const tratamentosKeys = {
  raiz: ['tratamentos'] as const,
  lista: (f: FiltrosTratamentosHook = {}) =>
    ['tratamentos', 'lista', f.pacienteId ?? null, f.unidadeId ?? null] as const,
  detalhe: (id: string) => ['tratamentos', 'detalhe', id] as const,
  tipos: () => ['tratamentos', 'tipos'] as const,
};

export function useListarTratamentos(filtros: FiltrosTratamentosHook = {}) {
  return useQuery({
    queryKey: tratamentosKeys.lista(filtros),
    queryFn: () => listarTratamentos(filtros),
  });
}

export function useTratamentoPorId(id: string | null) {
  return useQuery({
    queryKey: id ? tratamentosKeys.detalhe(id) : ['tratamentos', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID ausente.');
      return obterTratamentoPorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useTiposTratamento() {
  return useQuery({
    queryKey: tratamentosKeys.tipos(),
    queryFn: listarTiposTratamento,
    staleTime: 5 * 60 * 1000,
  });
}

export function useExpandirPeriodicidade() {
  return useMutation({
    mutationFn: (payload: ExpandirPeriodicidadePayload) => expandirPeriodicidade(payload),
  });
}

export function useCadastrarTratamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarTratamentoPayload) => cadastrarTratamento(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: tratamentosKeys.raiz }),
  });
}

export function useAtualizarTratamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarTratamentoPayload }) =>
      atualizarTratamento(id, payload),
    onSuccess: (_d, vars) => {
      client.invalidateQueries({ queryKey: tratamentosKeys.raiz });
      client.invalidateQueries({ queryKey: tratamentosKeys.detalhe(vars.id) });
    },
  });
}

export function useEncerrarTratamento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => encerrarTratamento(id),
    onSuccess: () => client.invalidateQueries({ queryKey: tratamentosKeys.raiz }),
  });
}

export function useAdicionarSessao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AdicionarSessaoPayload }) =>
      adicionarSessao(id, payload),
    onSuccess: (_d, vars) => {
      client.invalidateQueries({ queryKey: tratamentosKeys.detalhe(vars.id) });
      client.invalidateQueries({ queryKey: tratamentosKeys.raiz });
    },
  });
}

export function useAtualizarSessao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, sessaoId, payload }: { id: string; sessaoId: string; payload: AtualizarSessaoPayload }) =>
      atualizarSessao(id, sessaoId, payload),
    onSuccess: (_d, vars) => {
      client.invalidateQueries({ queryKey: tratamentosKeys.detalhe(vars.id) });
    },
  });
}

export function useCancelarSessao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, sessaoId }: { id: string; sessaoId: string }) => cancelarSessao(id, sessaoId),
    onSuccess: (_d, vars) => {
      client.invalidateQueries({ queryKey: tratamentosKeys.detalhe(vars.id) });
      client.invalidateQueries({ queryKey: tratamentosKeys.raiz });
    },
  });
}

export function useConfirmarSessao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, sessaoId, payload }: { id: string; sessaoId: string; payload: ConfirmarSessaoPayload }) =>
      confirmarSessao(id, sessaoId, payload),
    onSuccess: (_d, vars) => {
      client.invalidateQueries({ queryKey: tratamentosKeys.detalhe(vars.id) });
      client.invalidateQueries({ queryKey: tratamentosKeys.raiz });
    },
  });
}
