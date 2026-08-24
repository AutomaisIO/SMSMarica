import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { buscarEstudos, excluirEstudo, listarSeries } from '@/features/pacs/api/pacsApi';
import {
  associarExame,
  desassociarExame,
  listarAssociacoesPorStudies,
  listarOrigemPorStudies,
  previewSolicitacaoPorAccession,
  resincronizarExames,
} from '@/features/pacs/api/associacaoExameApi';
import type { FiltroBusca } from '@/features/pacs/types';

export const pacsKeys = {
  series: (studyUID: string) => ['pacs', 'series', studyUID] as const,
};

export const associacoesKeys = {
  raiz: ['exames', 'associacoes'] as const,
  porStudies: (uids: string[]) => ['exames', 'associacoes', 'lote', [...uids].sort()] as const,
  preview: (accession: string) => ['exames', 'associacoes', 'preview', accession] as const,
};

/** Busca de estudos via mutation (disparada pelo formulário do modal de associação). */
export function useBuscarEstudos() {
  return useMutation({
    mutationFn: (filtro: FiltroBusca) => buscarEstudos(filtro),
  });
}

/**
 * Busca de estudos AO VIVO (listagem PACS): reage ao filtro (debounced na página) e paginação.
 * `signal` deixa o React Query abortar a busca anterior ao mudar o filtro; keepPreviousData
 * evita a tabela piscar entre buscas/páginas.
 */
export function usePesquisaEstudos(filtro: FiltroBusca) {
  return useQuery({
    queryKey: ['pacs', 'estudos', filtro] as const,
    queryFn: ({ signal }) => buscarEstudos(filtro, signal),
    placeholderData: keepPreviousData,
  });
}

export function useSeriesDoEstudo(studyUID: string | null) {
  return useQuery({
    queryKey: studyUID ? pacsKeys.series(studyUID) : ['pacs', 'series', 'nenhum'],
    queryFn: () => {
      if (!studyUID) throw new Error('Estudo não informado.');
      return listarSeries(studyUID);
    },
    enabled: Boolean(studyUID),
  });
}

/** Exclui um estudo no PACS (reject + delete permanente no dcm4chee). */
export function useExcluirEstudo() {
  return useMutation({
    mutationFn: (studyUID: string) => excluirEstudo(studyUID),
  });
}

/** Vínculos por StudyInstanceUID (lote) — espelha useLaudosPorStudyUIDs. */
export function useAssociacoesPorStudyUIDs(uids: string[]) {
  return useQuery({
    queryKey: associacoesKeys.porStudies(uids),
    queryFn: () => listarAssociacoesPorStudies(uids),
    enabled: uids.length > 0,
    staleTime: 15_000,
  });
}

/**
 * Origem (equipamento/unidade) dos estudos ÓRFÃOS da página. Chamar só com os UIDs sem
 * associação: cada UID custa uma consulta ao PACS. staleTime alto — o AE de um estudo não muda.
 */
export function useOrigemPorStudyUIDs(uids: string[]) {
  return useQuery({
    queryKey: ['pacs', 'origem', [...uids].sort()] as const,
    queryFn: () => listarOrigemPorStudies(uids),
    enabled: uids.length > 0,
    staleTime: 10 * 60_000,
  });
}

/** Preview da solicitação pelo número do pedido (dígitos; ex.: 260625002). */
export function usePreviewSolicitacao(accession: string) {
  const limpo = accession.trim();
  return useQuery({
    queryKey: associacoesKeys.preview(limpo),
    queryFn: () => previewSolicitacaoPorAccession(limpo),
    enabled: /^\d{4,}$/.test(limpo),
    staleTime: 15_000,
  });
}

export function useAssociarExame() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: associarExame,
    onSuccess: () => {
      client.invalidateQueries({ queryKey: associacoesKeys.raiz });
    },
  });
}

export function useDesassociarExame() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (studyUID: string) => desassociarExame(studyUID),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: associacoesKeys.raiz });
    },
  });
}

/** Resincronização sob demanda (rede de segurança) — varre órfãos e associa. */
export function useResincronizarExames() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: resincronizarExames,
    onSuccess: () => {
      client.invalidateQueries({ queryKey: associacoesKeys.raiz });
    },
  });
}
