import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type {
  AbrirTicketPayload,
  AtualizarGestaoPayload,
  ComentarPayload,
  TicketVisibilidade,
} from '@/features/tickets/types';
import {
  abrirTicket,
  arquivarGestao,
  arquivarTicket,
  atualizarGestao,
  atualizarVisibilidade,
  comentarTicket,
  comentarTicketGestao,
  excluirTicket,
  listarMeusTickets,
  listarTodosTickets,
  obterConfiguracao,
  obterTicket,
  obterTicketGestao,
} from '@/features/tickets/api/ticketsApi';

export const ticketsKeys = {
  raiz: ['tickets'] as const,
  meus: (arq: boolean) => ['tickets', 'meus', arq] as const,
  todos: (arq: boolean) => ['tickets', 'todos', arq] as const,
  detalhe: (id: string, gestao: boolean) => ['tickets', 'detalhe', gestao, id] as const,
  config: ['tickets', 'config'] as const,
};

export function useMeusTickets(incluirArquivados: boolean) {
  return useQuery({
    queryKey: ticketsKeys.meus(incluirArquivados),
    queryFn: () => listarMeusTickets(incluirArquivados),
  });
}

export function useTodosTickets(incluirArquivados: boolean) {
  return useQuery({
    queryKey: ticketsKeys.todos(incluirArquivados),
    queryFn: () => listarTodosTickets(incluirArquivados),
  });
}

export function useTicket(id: string, gestao: boolean) {
  return useQuery({
    queryKey: ticketsKeys.detalhe(id, gestao),
    queryFn: () => (gestao ? obterTicketGestao(id) : obterTicket(id)),
    enabled: !!id,
  });
}

export function useConfiguracaoTickets() {
  return useQuery({ queryKey: ticketsKeys.config, queryFn: obterConfiguracao });
}

export function useAbrirTicket() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (p: AbrirTicketPayload) => abrirTicket(p),
    onSuccess: () => qc.invalidateQueries({ queryKey: ticketsKeys.raiz }),
  });
}

export function useComentar(id: string, gestao: boolean) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (p: ComentarPayload) => (gestao ? comentarTicketGestao(id, p) : comentarTicket(id, p)),
    onSuccess: () => qc.invalidateQueries({ queryKey: ticketsKeys.raiz }),
  });
}

export function useAtualizarGestao(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (p: AtualizarGestaoPayload) => atualizarGestao(id, p),
    onSuccess: () => qc.invalidateQueries({ queryKey: ticketsKeys.raiz }),
  });
}

export function useArquivar(gestao: boolean) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, arquivar }: { id: string; arquivar: boolean }) =>
      gestao ? arquivarGestao(id, arquivar) : arquivarTicket(id, arquivar),
    onSuccess: () => qc.invalidateQueries({ queryKey: ticketsKeys.raiz }),
  });
}

export function useExcluirTicket() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirTicket(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ticketsKeys.raiz }),
  });
}

export function useAtualizarVisibilidade() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: TicketVisibilidade) => atualizarVisibilidade(v),
    onSuccess: () => qc.invalidateQueries({ queryKey: ticketsKeys.config }),
  });
}
