import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarAvaliacao,
  deletarAvaliacao,
  listarAvaliacoes,
  obterAvaliacaoPorId,
  registrarAvaliacao,
} from '@/features/avaliacoes/api/avaliacoesApi';
import type {
  AtualizarAvaliacaoPayload,
  RegistrarAvaliacaoPayload,
} from '@/features/avaliacoes/types';

export const avaliacoesKeys = {
  lista: () => ['avaliacoes', 'lista'] as const,
  porId: (id: string) => ['avaliacoes', 'detalhe', id] as const,
};

export function useListarAvaliacoes() {
  return useQuery({ queryKey: avaliacoesKeys.lista(), queryFn: listarAvaliacoes });
}

export function useAvaliacaoPorId(id: string | null) {
  return useQuery({
    queryKey: id ? avaliacoesKeys.porId(id) : ['avaliacoes', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterAvaliacaoPorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useRegistrarAvaliacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: RegistrarAvaliacaoPayload) => registrarAvaliacao(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: avaliacoesKeys.lista() }),
  });
}

export function useAtualizarAvaliacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarAvaliacaoPayload }) =>
      atualizarAvaliacao(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: avaliacoesKeys.lista() });
      client.invalidateQueries({ queryKey: avaliacoesKeys.porId(v.id) });
    },
  });
}

export function useDeletarAvaliacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deletarAvaliacao(id),
    onSuccess: () => client.invalidateQueries({ queryKey: avaliacoesKeys.lista() }),
  });
}
