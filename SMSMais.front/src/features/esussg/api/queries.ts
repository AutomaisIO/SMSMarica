import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  buscarSolicitacoesEsusSg,
  dispararVarreduraEsusSg,
  listarExecucoesEsusSg,
  listarNotificacoesEsusSg,
  listarTecnicosNotificacoesEsusSg,
  marcarNotificacaoEsusSgVista,
  marcarNotificacoesDaSolicitacaoEsusSgVistas,
  obterResumoEsusSg,
  obterResumoNotificacoesEsusSg,
  obterSolicitacaoEsusSg,
  obterStatusMotorEsusSg,
  obterVarreduraAutomaticaEsusSg,
  salvarCredencialEsusSg,
  salvarVarreduraAutomaticaEsusSg,
  sincronizarCatalogoEsusSg,
  testarCredencialEsusSg,
} from '@/features/esussg/api/esussgApi';
import type {
  BuscaEsusSgFiltro,
  CredencialEsusSg,
  DispararVarreduraEsusSgPayload,
  NotificacoesEsusSgFiltro,
  VarreduraAutomaticaEsusSg,
} from '@/features/esussg/types';

/**
 * Chaves no mesmo formato do SER/SERNIT (`[sistema, 'solicitacao', id]`…) — é isso que deixa o
 * painel compartilhado da análise de regras trocar a análise no cache sem conhecer a feature.
 */
export const esussgKeys = {
  busca: (filtro: BuscaEsusSgFiltro) => ['esussg', 'busca', filtro] as const,
  resumo: ['esussg', 'resumo'] as const,
  solicitacao: (id: string) => ['esussg', 'solicitacao', id] as const,
  status: ['esussg', 'status'] as const,
  execucoes: ['esussg', 'execucoes'] as const,
  varreduraAutomatica: ['esussg', 'varredura-automatica'] as const,
};

export function useBuscaEsusSg(filtro: BuscaEsusSgFiltro) {
  return useQuery({
    queryKey: esussgKeys.busca(filtro),
    queryFn: () => buscarSolicitacoesEsusSg(filtro),
    placeholderData: (anterior) => anterior,
  });
}

export function useResumoEsusSg() {
  return useQuery({ queryKey: esussgKeys.resumo, queryFn: obterResumoEsusSg });
}

export function useSolicitacaoEsusSg(id: string | undefined) {
  return useQuery({
    queryKey: esussgKeys.solicitacao(id ?? ''),
    queryFn: () => obterSolicitacaoEsusSg(id!),
    enabled: Boolean(id),
  });
}

// ---------------------------------------------------------------- motor

/** Status do motor: 1s enquanto há varredura viva, 15s em repouso — o ritmo das telas irmãs. */
export function useStatusMotorEsusSg() {
  return useQuery({
    queryKey: esussgKeys.status,
    queryFn: obterStatusMotorEsusSg,
    refetchInterval: (query) => (query.state.data?.varreduraEmAndamento ? 1000 : 15000),
  });
}

/** A lista de rodadas mostra fase, cursor e sinal da execução corrente — anda junto com o status. */
export function useExecucoesEsusSg(limite = 20, emAndamento = false) {
  return useQuery({
    queryKey: [...esussgKeys.execucoes, limite],
    queryFn: () => listarExecucoesEsusSg(limite),
    refetchInterval: emAndamento ? 1000 : false,
  });
}

export function useDispararVarreduraEsusSg() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: DispararVarreduraEsusSgPayload) => dispararVarreduraEsusSg(payload),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: esussgKeys.status });
      void qc.invalidateQueries({ queryKey: esussgKeys.execucoes });
    },
  });
}

export function useTestarCredencialEsusSg() {
  return useMutation({ mutationFn: (c: CredencialEsusSg) => testarCredencialEsusSg(c) });
}

export function useSalvarCredencialEsusSg() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (c: CredencialEsusSg) => salvarCredencialEsusSg(c),
    onSuccess: () => void qc.invalidateQueries({ queryKey: esussgKeys.status }),
  });
}

export function useVarreduraAutomaticaEsusSg() {
  return useQuery({
    queryKey: esussgKeys.varreduraAutomatica,
    queryFn: obterVarreduraAutomaticaEsusSg,
  });
}

export function useSalvarVarreduraAutomaticaEsusSg() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (c: VarreduraAutomaticaEsusSg) => salvarVarreduraAutomaticaEsusSg(c),
    onSuccess: () => void qc.invalidateQueries({ queryKey: esussgKeys.varreduraAutomatica }),
  });
}

export function useSincronizarCatalogoEsusSg() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: sincronizarCatalogoEsusSg,
    onSuccess: () => void qc.invalidateQueries({ queryKey: esussgKeys.status }),
  });
}

// ---------------------------------------------------------------- notificações

export const notificacaoEsusSgKeys = {
  resumo: (tecnicos: string[]) => ['esussg', 'notificacoes', 'resumo', tecnicos] as const,
  tecnicos: ['esussg', 'notificacoes', 'tecnicos'] as const,
  lista: (f: NotificacoesEsusSgFiltro) => ['esussg', 'notificacoes', 'lista', f] as const,
};

/** Com técnicos marcados, o resumo reconta só o que é deles — abas e situações batem com a lista. */
export function useResumoNotificacoesEsusSg(tecnicos: string[] = []) {
  return useQuery({
    queryKey: notificacaoEsusSgKeys.resumo(tecnicos),
    queryFn: () => obterResumoNotificacoesEsusSg(tecnicos),
    refetchInterval: 10_000,
  });
}

export function useTecnicosNotificacoesEsusSg() {
  return useQuery({
    queryKey: notificacaoEsusSgKeys.tecnicos,
    queryFn: listarTecnicosNotificacoesEsusSg,
    refetchInterval: 30_000,
  });
}

export function useNotificacoesEsusSg(filtro: NotificacoesEsusSgFiltro) {
  return useQuery({
    queryKey: notificacaoEsusSgKeys.lista(filtro),
    queryFn: () => listarNotificacoesEsusSg(filtro),
    refetchInterval: 10_000,
  });
}

/** Marcar como visto invalida lista E resumo: o contador da aba cai junto com a linha. */
export function useMarcarNotificacaoEsusSgVista() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => marcarNotificacaoEsusSgVista(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['esussg', 'notificacoes'] });
      void qc.invalidateQueries({ queryKey: esussgKeys.status });
    },
  });
}

export function useMarcarSolicitacaoEsusSgVista() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (idEsusSg: string) => marcarNotificacoesDaSolicitacaoEsusSgVistas(idEsusSg),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['esussg', 'notificacoes'] });
      void qc.invalidateQueries({ queryKey: esussgKeys.status });
    },
  });
}
