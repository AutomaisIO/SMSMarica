import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTemConsulta } from '@/shared/auth/authStore';
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
  marcarEnviadoIa,
  obterConfiguracao,
  obterResumoAutor,
  obterResumoGestao,
  obterTicket,
  obterTicketGestao,
  reconhecerTicket,
} from '@/features/tickets/api/ticketsApi';

/** Intervalo de atualização dos resumos/badges (sem realtime — polling leve). */
const INTERVALO_RESUMO = 60_000;

export const ticketsKeys = {
  raiz: ['tickets'] as const,
  meus: (arq: boolean) => ['tickets', 'meus', arq] as const,
  todos: (arq: boolean) => ['tickets', 'todos', arq] as const,
  detalhe: (id: string, gestao: boolean) => ['tickets', 'detalhe', gestao, id] as const,
  config: ['tickets', 'config'] as const,
  resumoAutor: ['tickets', 'resumo', 'autor'] as const,
  resumoGestao: ['tickets', 'resumo', 'gestao'] as const,
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

/** Resumo do autor (bandeira/badge de "Meus Tickets"). Todo usuário autenticado. */
export function useResumoAutorTickets() {
  return useQuery({
    queryKey: ticketsKeys.resumoAutor,
    queryFn: obterResumoAutor,
    refetchInterval: INTERVALO_RESUMO,
  });
}

/**
 * Resumo da gestão (badge do menu + cabeçalho). Só habilita para quem tem o módulo,
 * senão o endpoint devolve 403.
 */
export function useResumoGestaoTickets(habilitado: boolean) {
  return useQuery({
    queryKey: ticketsKeys.resumoGestao,
    queryFn: obterResumoGestao,
    enabled: habilitado,
    refetchInterval: INTERVALO_RESUMO,
  });
}

export function useReconhecerTicket() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reconhecerTicket(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ticketsKeys.raiz }),
  });
}

/** Contadores consolidados para os badges do menu e o resumo do header. */
export function useTicketsBadges() {
  const ehGestor = useTemConsulta('Ticket');
  const autor = useResumoAutorTickets();
  const gestao = useResumoGestaoTickets(ehGestor);
  return {
    ehGestor,
    meusNaoReconhecidos: autor.data?.naoReconhecidos ?? 0,
    gestaoNovos: gestao.data?.novos ?? 0,
    gestaoAbertos: gestao.data?.abertos ?? 0,
    gestaoEmAnalise: gestao.data?.emAnalise ?? 0,
  };
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

/** Marca o ticket como "Enviado à IA" (chamado ao encaminhar ao Agente IA). */
export function useMarcarEnviadoIa() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => marcarEnviadoIa(id),
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
