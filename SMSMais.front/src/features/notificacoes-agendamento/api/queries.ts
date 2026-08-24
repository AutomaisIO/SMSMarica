import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  listarNotificacoes,
  obterNotificacao,
  reenviarNotificacao,
} from '@/features/notificacoes-agendamento/api/notificacoesApi';
import type { NotificacaoFiltro } from '@/features/notificacoes-agendamento/types';

export const notificacoesKeys = {
  raiz: ['notificacoes-agendamento'] as const,
  lista: (filtro: NotificacaoFiltro) => ['notificacoes-agendamento', 'lista', filtro] as const,
  detalhe: (id: string) => ['notificacoes-agendamento', 'detalhe', id] as const,
};

export function useNotificacoes(filtro: NotificacaoFiltro) {
  return useQuery({
    queryKey: notificacoesKeys.lista(filtro),
    queryFn: () => listarNotificacoes(filtro),
    placeholderData: (anterior) => anterior,
  });
}

export function useNotificacaoDetalhe(id: string | null) {
  return useQuery({
    queryKey: id ? notificacoesKeys.detalhe(id) : ['notificacoes-agendamento', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('Id não informado.');
      return obterNotificacao(id);
    },
    enabled: Boolean(id),
  });
}

export function useReenviarNotificacao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reenviarNotificacao(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: notificacoesKeys.raiz }),
  });
}
