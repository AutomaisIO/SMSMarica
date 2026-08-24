import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  adicionarAvulso,
  adicionarBloqueio,
  adicionarRecorrencia,
  agendar,
  atualizarAgenda,
  cadastrarAgenda,
  cancelarAgendamento,
  confirmarAgendamento,
  excluirAgenda,
  horariosLivres,
  horariosLivresPorEspecialidade,
  listarAgendamentos,
  listarAgendas,
  listarDisponibilidades,
  obterAgenda,
  realizarAgendamento,
  registrarFalta,
  removerAvulso,
  removerBloqueio,
  removerRecorrencia,
  type FiltroAgendas,
} from '@/features/agendamentos/api/agendamentosApi';
import type {
  AdicionarAvulsoPayload,
  AdicionarBloqueioPayload,
  AdicionarRecorrenciaPayload,
  AtualizarAgendaPayload,
  CadastrarAgendaPayload,
  AgendarPayload,
} from '@/features/agendamentos/types';

export const agendasKeys = {
  raiz: ['agendas'] as const,
  lista: (filtro: FiltroAgendas) => ['agendas', 'lista', filtro] as const,
  porId: (id: string) => ['agendas', 'detalhe', id] as const,
  disponibilidades: (id: string, inicio: string, fim: string) =>
    ['agendas', id, 'disponibilidades', inicio, fim] as const,
  livres: (id: string, inicio: string, fim: string) => ['agendas', id, 'livres', inicio, fim] as const,
  agendamentos: (id: string, inicio: string, fim: string) =>
    ['agendas', id, 'agendamentos', inicio, fim] as const,
};

export function useListarAgendas(filtro: FiltroAgendas = {}) {
  return useQuery({ queryKey: agendasKeys.lista(filtro), queryFn: () => listarAgendas(filtro) });
}

export function useAgenda(id: string | null) {
  return useQuery({
    queryKey: id ? agendasKeys.porId(id) : ['agendas', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterAgenda(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarAgenda() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarAgendaPayload) => cadastrarAgenda(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: agendasKeys.raiz }),
  });
}

export function useAtualizarAgenda(id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AtualizarAgendaPayload) => atualizarAgenda(id, payload),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: agendasKeys.raiz });
      client.invalidateQueries({ queryKey: agendasKeys.porId(id) });
    },
  });
}

export function useExcluirAgenda() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirAgenda(id),
    onSuccess: () => client.invalidateQueries({ queryKey: agendasKeys.raiz }),
  });
}

export function useAdicionarRecorrencia(agendaId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AdicionarRecorrenciaPayload) => adicionarRecorrencia(agendaId, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: agendasKeys.porId(agendaId) }),
  });
}

export function useRemoverRecorrencia(agendaId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (recorrenciaId: string) => removerRecorrencia(agendaId, recorrenciaId),
    onSuccess: () => client.invalidateQueries({ queryKey: agendasKeys.porId(agendaId) }),
  });
}

export function useDisponibilidades(agendaId: string, inicio: string, fim: string) {
  return useQuery({
    queryKey: agendasKeys.disponibilidades(agendaId, inicio, fim),
    queryFn: () => listarDisponibilidades(agendaId, inicio, fim),
  });
}

export function useAdicionarAvulso(agendaId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AdicionarAvulsoPayload) => adicionarAvulso(agendaId, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: ['agendas', agendaId] }),
  });
}

export function useRemoverAvulso(agendaId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (avulsoId: string) => removerAvulso(agendaId, avulsoId),
    onSuccess: () => client.invalidateQueries({ queryKey: ['agendas', agendaId] }),
  });
}

export function useAdicionarBloqueio(agendaId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AdicionarBloqueioPayload) => adicionarBloqueio(agendaId, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: ['agendas', agendaId] }),
  });
}

export function useRemoverBloqueio(agendaId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (bloqueioId: string) => removerBloqueio(agendaId, bloqueioId),
    onSuccess: () => client.invalidateQueries({ queryKey: ['agendas', agendaId] }),
  });
}

export function useHorariosLivres(agendaId: string, inicio: string, fim: string, ativo: boolean) {
  return useQuery({
    queryKey: agendasKeys.livres(agendaId, inicio, fim),
    queryFn: () => horariosLivres(agendaId, inicio, fim),
    enabled: ativo,
  });
}

/** Slots livres da especialidade (todas as agendas dos médicos dela), p/ a marcação guiada. */
export function useHorariosPorEspecialidade(
  especialidadeId: string,
  unidadeId: string | undefined,
  de: string,
  ate: string,
) {
  return useQuery({
    queryKey: ['agendamentos', 'livres-especialidade', especialidadeId, unidadeId ?? '', de, ate] as const,
    queryFn: () => horariosLivresPorEspecialidade(especialidadeId, unidadeId, de, ate),
    enabled: Boolean(especialidadeId && de && ate),
  });
}

export function useAgendamentosDaAgenda(agendaId: string, inicio: string, fim: string) {
  return useQuery({
    queryKey: agendasKeys.agendamentos(agendaId, inicio, fim),
    queryFn: () => listarAgendamentos(agendaId, inicio, fim),
  });
}

export function useAgendar(agendaId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AgendarPayload) => agendar(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: ['agendas', agendaId] }),
  });
}

export function useTransicaoAgendamento(agendaId: string) {
  const client = useQueryClient();
  const invalidar = () => client.invalidateQueries({ queryKey: ['agendas', agendaId] });
  return {
    confirmar: useMutation({ mutationFn: (id: string) => confirmarAgendamento(id), onSuccess: invalidar }),
    realizar: useMutation({ mutationFn: (id: string) => realizarAgendamento(id), onSuccess: invalidar }),
    falta: useMutation({ mutationFn: (id: string) => registrarFalta(id), onSuccess: invalidar }),
    cancelar: useMutation({
      mutationFn: ({ id, motivo }: { id: string; motivo?: string | null }) => cancelarAgendamento(id, motivo),
      onSuccess: invalidar,
    }),
  };
}
