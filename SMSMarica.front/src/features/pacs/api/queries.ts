import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { buscarEstudos, excluirEstudo, listarSeries } from '@/features/pacs/api/pacsApi';
import {
  associarExame,
  desassociarExame,
  listarAssociacoesPorStudies,
  previewSolicitacaoPorAccession,
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

/** Busca de estudos via mutation (disparada pelo formulário do modal). */
export function useBuscarEstudos() {
  return useMutation({
    mutationFn: (filtro: FiltroBusca) => buscarEstudos(filtro),
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

/** Preview da solicitação por número SMS (só dispara em formato válido). */
export function usePreviewSolicitacao(accession: string) {
  const valido = /^SMS\d+$/i.test(accession.trim());
  return useQuery({
    queryKey: associacoesKeys.preview(accession.trim().toUpperCase()),
    queryFn: () => previewSolicitacaoPorAccession(accession.trim().toUpperCase()),
    enabled: valido,
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
