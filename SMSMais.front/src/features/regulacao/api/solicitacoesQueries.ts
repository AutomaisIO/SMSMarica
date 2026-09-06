import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  anexarArquivo,
  atualizarSolicitacao,
  criarSolicitacao,
  enviarParaFila,
  listarExigencias,
  obterFormularioRegulacao,
  obterPendencias,
  obterSolicitacao,
  removerArquivo,
  type AtualizarSolicitacaoPayload,
} from './solicitacoesApi';
import type { FluxoRegulacao } from '../tiposSolicitacao';

const raiz = ['regulacao', 'solicitacoes'] as const;

export function useFormularioRegulacao(procedimentoId: string | null, fluxo: FluxoRegulacao | null) {
  return useQuery({
    queryKey: [...raiz, 'formulario', procedimentoId, fluxo],
    queryFn: () => obterFormularioRegulacao(procedimentoId!, fluxo!),
    enabled: !!procedimentoId && !!fluxo,
    // A definição só muda quando a SES recompila o catálogo — não vale rebuscar a cada foco.
    staleTime: 5 * 60_000,
  });
}

export function useSolicitacao(id: string | null) {
  return useQuery({
    queryKey: [...raiz, id],
    queryFn: () => obterSolicitacao(id!),
    enabled: !!id,
  });
}

export function usePendencias(id: string | null) {
  return useQuery({
    queryKey: [...raiz, id, 'pendencias'],
    queryFn: () => obterPendencias(id!),
    enabled: !!id,
  });
}

export function useExigencias(solicitacaoId: string | null) {
  return useQuery({
    queryKey: [...raiz, solicitacaoId, 'exigencias'],
    queryFn: () => listarExigencias(solicitacaoId!),
    enabled: !!solicitacaoId,
  });
}

function useInvalidar() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: raiz });
}

export function useCriarSolicitacao() {
  const invalidar = useInvalidar();
  return useMutation({ mutationFn: criarSolicitacao, onSuccess: invalidar });
}

export function useAtualizarSolicitacao() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: ({ id, ...p }: { id: string } & AtualizarSolicitacaoPayload) =>
      atualizarSolicitacao(id, p),
    onSuccess: invalidar,
  });
}

export function useEnviarParaFila() {
  const invalidar = useInvalidar();
  return useMutation({ mutationFn: enviarParaFila, onSuccess: invalidar });
}

export function useAnexarArquivo() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: (p: { solicitacaoId: string; exigenciaId: string; arquivo: File }) =>
      anexarArquivo(p.solicitacaoId, p.exigenciaId, p.arquivo),
    onSuccess: invalidar,
  });
}

export function useRemoverArquivo() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: (p: { solicitacaoId: string; arquivoId: string }) =>
      removerArquivo(p.solicitacaoId, p.arquivoId),
    onSuccess: invalidar,
  });
}
