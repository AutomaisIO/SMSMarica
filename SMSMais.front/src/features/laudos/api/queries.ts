import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  aprovarAssinatura,
  atualizarLaudo,
  cadastrarLaudo,
  criarNovaVersaoLaudo,
  encerrarSessaoNuvem,
  excluirLaudo,
  finalizarLaudo,
  iniciarAssinatura,
  listarHistorico,
  listarLaudos,
  listarLaudosPorStudies,
  obterLaudo,
  obterSessaoNuvem,
  obterStatusAssinatura,
  rejeitarAssinatura,
} from '@/features/laudos/api/laudosApi';
import type {
  AtualizarLaudoPayload,
  CadastrarLaudoPayload,
  CarimboPosicao,
  EscolhaSessaoNuvem,
  FiltroLaudos,
  FinalizarLaudoPayload,
} from '@/features/laudos/types';

export const laudosKeys = {
  raiz: ['laudos'] as const,
  lista: (filtro: FiltroLaudos) => ['laudos', 'lista', filtro] as const,
  porId: (id: string) => ['laudos', 'detalhe', id] as const,
  historico: (id: string) => ['laudos', 'historico', id] as const,
  porStudies: (uids: string[]) => ['laudos', 'por-studies', [...uids].sort()] as const,
};

export function useListarLaudos(filtro: FiltroLaudos) {
  return useQuery({
    queryKey: laudosKeys.lista(filtro),
    // `signal`: React Query aborta a busca anterior ao mudar o filtro (busca ao vivo).
    queryFn: ({ signal }) => listarLaudos(filtro, signal),
    // Mantém a página anterior enquanto a nova carrega — sem piscar.
    placeholderData: keepPreviousData,
  });
}

export function useLaudoPorId(id: string | null) {
  return useQuery({
    queryKey: id ? laudosKeys.porId(id) : ['laudos', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterLaudo(id);
    },
    enabled: Boolean(id),
  });
}

export function useHistoricoLaudo(id: string | null) {
  return useQuery({
    queryKey: id ? laudosKeys.historico(id) : ['laudos', 'historico', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return listarHistorico(id);
    },
    enabled: Boolean(id),
  });
}

export function useLaudosPorStudyUIDs(uids: string[]) {
  return useQuery({
    queryKey: laudosKeys.porStudies(uids),
    queryFn: () => listarLaudosPorStudies(uids),
    enabled: uids.length > 0,
    staleTime: 30_000,
  });
}

export function useCadastrarLaudo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarLaudoPayload) => cadastrarLaudo(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: laudosKeys.raiz }),
  });
}

export function useAtualizarLaudo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarLaudoPayload }) =>
      atualizarLaudo(id, payload),
    onSuccess: (_d, vars) => {
      client.invalidateQueries({ queryKey: laudosKeys.raiz });
      client.invalidateQueries({ queryKey: laudosKeys.porId(vars.id) });
    },
  });
}

export function useFinalizarLaudo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: FinalizarLaudoPayload }) =>
      finalizarLaudo(id, payload),
    onSuccess: (_d, vars) => {
      client.invalidateQueries({ queryKey: laudosKeys.raiz });
      client.invalidateQueries({ queryKey: laudosKeys.porId(vars.id) });
    },
  });
}

export function useCriarNovaVersao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => criarNovaVersaoLaudo(id),
    onSuccess: () => client.invalidateQueries({ queryKey: laudosKeys.raiz }),
  });
}

export function useExcluirLaudo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirLaudo(id),
    onSuccess: () => client.invalidateQueries({ queryKey: laudosKeys.raiz }),
  });
}

// ---- Assinatura digital ----

export const assinaturaKey = (id: string) => ['laudos', 'assinatura', id] as const;

/** Status da assinatura, com polling enquanto o agente não conclui. */
export function useStatusAssinatura(id: string | null, ativo: boolean) {
  return useQuery({
    queryKey: id ? assinaturaKey(id) : ['laudos', 'assinatura', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterStatusAssinatura(id);
    },
    enabled: Boolean(id) && ativo,
    refetchInterval: (query) => {
      const s = query.state.data?.status;
      return s === 'Iniciada' || s === 'AguardandoAssinatura' ? 3000 : false;
    },
  });
}

export function useIniciarAssinatura() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (v: { id: string; posicao?: CarimboPosicao; sessaoNuvem?: EscolhaSessaoNuvem }) =>
      iniciarAssinatura(v.id, v.posicao, v.sessaoNuvem),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: assinaturaKey(v.id) });
      client.invalidateQueries({ queryKey: laudosKeys.porId(v.id) });
      client.invalidateQueries({ queryKey: sessaoNuvemKey });
    },
  });
}

// ---- Autorização VIDaaS mantida (ADR-0061 §2.1) ----

export const sessaoNuvemKey = ['laudos', 'assinatura', 'sessao-nuvem'] as const;

/** Autorização VIDaaS deste login + preferência do médico. Só para quem assina em nuvem. */
export function useSessaoAssinaturaNuvem(ativo: boolean) {
  return useQuery({
    queryKey: sessaoNuvemKey,
    queryFn: obterSessaoNuvem,
    enabled: ativo,
  });
}

/** Encerra a autorização mantida: o próximo laudo volta a pedir aprovação no app. */
export function useEncerrarSessaoNuvem() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: encerrarSessaoNuvem,
    onSuccess: () => client.invalidateQueries({ queryKey: sessaoNuvemKey }),
  });
}

/** Aprova o documento assinado (conferência) — oficializa e avisa o paciente. */
export function useAprovarAssinatura() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => aprovarAssinatura(id),
    onSuccess: (_d, id) => {
      client.invalidateQueries({ queryKey: assinaturaKey(id) });
      client.invalidateQueries({ queryKey: laudosKeys.porId(id) });
      client.invalidateQueries({ queryKey: laudosKeys.raiz });
    },
  });
}

/** Rejeita na conferência — cancela a assinatura e libera assinar de novo. */
export function useRejeitarAssinatura() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => rejeitarAssinatura(id),
    onSuccess: (_d, id) => {
      client.invalidateQueries({ queryKey: assinaturaKey(id) });
      client.invalidateQueries({ queryKey: laudosKeys.porId(id) });
      client.invalidateQueries({ queryKey: laudosKeys.raiz });
    },
  });
}
