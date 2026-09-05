import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  comunicarAlteracaoAgenda,
  listarAlteracoesAgenda,
  tratarAlteracaoAgenda,
} from '@/features/alteracoes-agenda/api/alteracoesApi';

export const alteracoesAgendaKeys = {
  lista: (apenasPendentes: boolean) => ['alteracoes-agenda', apenasPendentes] as const,
};

export function useAlteracoesAgenda(apenasPendentes: boolean) {
  return useQuery({
    queryKey: alteracoesAgendaKeys.lista(apenasPendentes),
    queryFn: () => listarAlteracoesAgenda(apenasPendentes),
  });
}

/** Invalida as duas listas: tratar move a linha de "pendentes" para "todas". */
function useInvalidar() {
  const client = useQueryClient();
  return () => client.invalidateQueries({ queryKey: ['alteracoes-agenda'] });
}

export function useTratarAlteracao() {
  const invalidar = useInvalidar();
  return useMutation({ mutationFn: tratarAlteracaoAgenda, onSuccess: invalidar });
}

export function useComunicarAlteracao() {
  const invalidar = useInvalidar();
  return useMutation({ mutationFn: comunicarAlteracaoAgenda, onSuccess: invalidar });
}
