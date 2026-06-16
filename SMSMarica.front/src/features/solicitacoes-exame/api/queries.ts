import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarSolicitacao,
  cadastrarSolicitacao,
  cancelarSolicitacao,
  excluirSolicitacao,
  listarSolicitacoes,
  obterSolicitacao,
  obterSolicitacaoPorStudy,
  reenviarWorklist,
} from '@/features/solicitacoes-exame/api/solicitacoesExameApi';
import type {
  AtualizarSolicitacaoPayload,
  CadastrarSolicitacaoPayload,
  FiltroSolicitacoes,
} from '@/features/solicitacoes-exame/types';

export const solicitacoesKeys = {
  raiz: ['solicitacoes-exame'] as const,
  lista: (filtro: FiltroSolicitacoes) => ['solicitacoes-exame', 'lista', filtro] as const,
  porId: (id: string) => ['solicitacoes-exame', 'detalhe', id] as const,
  porStudy: (uid: string) => ['solicitacoes-exame', 'por-study', uid] as const,
};

export function useListarSolicitacoes(filtro: FiltroSolicitacoes) {
  return useQuery({
    queryKey: solicitacoesKeys.lista(filtro),
    queryFn: () => listarSolicitacoes(filtro),
    refetchInterval: filtro.status === 'Recebida' || filtro.status === 'EmExecucao' ? 30_000 : false,
  });
}

export function useSolicitacaoPorId(id: string | null) {
  return useQuery({
    queryKey: id ? solicitacoesKeys.porId(id) : ['solicitacoes-exame', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterSolicitacao(id);
    },
    enabled: Boolean(id),
    refetchInterval: 30_000,
  });
}

export function useSolicitacaoPorStudy(studyInstanceUID: string | null) {
  return useQuery({
    queryKey: studyInstanceUID
      ? solicitacoesKeys.porStudy(studyInstanceUID)
      : ['solicitacoes-exame', 'por-study', 'nenhum'],
    queryFn: () => {
      if (!studyInstanceUID) throw new Error('StudyInstanceUID não informado.');
      return obterSolicitacaoPorStudy(studyInstanceUID);
    },
    enabled: Boolean(studyInstanceUID),
  });
}

export function useCadastrarSolicitacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarSolicitacaoPayload) => cadastrarSolicitacao(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: solicitacoesKeys.raiz }),
  });
}

export function useAtualizarSolicitacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarSolicitacaoPayload }) =>
      atualizarSolicitacao(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: solicitacoesKeys.raiz });
      client.invalidateQueries({ queryKey: solicitacoesKeys.porId(v.id) });
    },
  });
}

export function useCancelarSolicitacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, motivo }: { id: string; motivo: string }) => cancelarSolicitacao(id, motivo),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: solicitacoesKeys.raiz });
      client.invalidateQueries({ queryKey: solicitacoesKeys.porId(v.id) });
    },
  });
}

export function useReenviarWorklist() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reenviarWorklist(id),
    onSuccess: (_d, id) => {
      client.invalidateQueries({ queryKey: solicitacoesKeys.raiz });
      client.invalidateQueries({ queryKey: solicitacoesKeys.porId(id) });
    },
  });
}

export function useExcluirSolicitacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, force }: { id: string; force?: boolean }) => excluirSolicitacao(id, force ?? false),
    onSuccess: () => client.invalidateQueries({ queryKey: solicitacoesKeys.raiz }),
  });
}
