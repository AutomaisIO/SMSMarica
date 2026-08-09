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
 * Status do motor: 1s enquanto há varredura viva, 15s em repouso — o mesmo ritmo das telas
 * irmãs (Importação SISREG e Sincronização PEP), que acompanham job longo do mesmo jeito.
 *
 * Antes eram 3s/30s "para não martelar o servidor", mas o problema real nunca foi o intervalo:
 * era o backend só gravar os contadores quando uma SITUAÇÃO INTEIRA terminava, então a tela
 * ficava minutos exibindo o mesmo número e parecia travada. Corrigido isso (contadores por
 * lote), 1s dá a sensação de vivo que o operador espera.
 */
export function useStatusMotorSer() {
  return useQuery({
    queryKey: serKeys.status,
    queryFn: obterStatusMotorSer,
    refetchInterval: (query) => (query.state.data?.varreduraEmAndamento ? 1000 : 15000),
  });
}

/**
 * A lista de rodadas é o que mostra fase, cursor e pendentes da execução corrente — ela também
 * precisa andar durante a varredura, senão o "Progresso" congela enquanto o cabeçalho atualiza.
 */
export function useExecucoesSer(limite = 20, emAndamento = false) {
  return useQuery({
    queryKey: [...serKeys.execucoes, limite],
    queryFn: () => listarExecucoesSer(limite),
    refetchInterval: emAndamento ? 1000 : false,
  });
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
