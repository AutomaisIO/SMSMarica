import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  buscarSolicitacoesSer,
  dispararVarreduraSer,
  listarExecucoesSer,
  obterResumoSer,
  obterSolicitacaoSer,
  obterStatusMotorSer,
  salvarCredencialSer,
  testarCredencialSer,
} from '@/features/ser/api/serApi';
import type { BuscaSerFiltro, DispararVarreduraPayload } from '@/features/ser/types';

export const serKeys = {
  busca: (filtro: BuscaSerFiltro) => ['ser', 'busca', filtro] as const,
  resumo: ['ser', 'resumo'] as const,
  solicitacao: (id: string) => ['ser', 'solicitacao', id] as const,
  status: ['ser', 'status'] as const,
  execucoes: ['ser', 'execucoes'] as const,
};

export function useBuscaSer(filtro: BuscaSerFiltro) {
  return useQuery({
    queryKey: serKeys.busca(filtro),
    queryFn: () => buscarSolicitacoesSer(filtro),
    placeholderData: (anterior) => anterior,
  });
}

export function useResumoSer() {
  return useQuery({ queryKey: serKeys.resumo, queryFn: obterResumoSer });
}

export function useSolicitacaoSer(id: string | undefined) {
  return useQuery({
    queryKey: serKeys.solicitacao(id ?? ''),
    queryFn: () => obterSolicitacaoSer(id!),
    enabled: Boolean(id),
  });
}

/**
 * Status do motor com polling: 3s enquanto há varredura viva, 30s em repouso.
 * A rodada é longa (15 min a ~1 h), então não adianta martelar o servidor.
 */
export function useStatusMotorSer() {
  return useQuery({
    queryKey: serKeys.status,
    queryFn: obterStatusMotorSer,
    refetchInterval: (query) => (query.state.data?.varreduraEmAndamento ? 3000 : 30000),
  });
}

export function useExecucoesSer(limite = 20) {
  return useQuery({ queryKey: serKeys.execucoes, queryFn: () => listarExecucoesSer(limite) });
}

export function useDispararVarreduraSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: DispararVarreduraPayload) => dispararVarreduraSer(payload),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: serKeys.status });
      void qc.invalidateQueries({ queryKey: serKeys.execucoes });
    },
  });
}

export function useTestarCredencialSer() {
  return useMutation({
    mutationFn: ({ usuario, senha }: { usuario: string; senha: string }) =>
      testarCredencialSer(usuario, senha),
  });
}

export function useSalvarCredencialSer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ usuario, senha }: { usuario: string; senha: string }) =>
      salvarCredencialSer(usuario, senha),
    onSuccess: () => void qc.invalidateQueries({ queryKey: serKeys.status }),
  });
}
