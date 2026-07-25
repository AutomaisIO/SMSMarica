import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarDocumento,
  criarDocumento,
  extrairModelo,
  gerarEmbeddings,
  listarDocumentos,
  obterDocumento,
  removerDocumento,
} from '@/features/ia/api/conhecimentoApi';

export const conhecimentoKeys = {
  documentos: (fonteId: string) => ['ia', 'conhecimento', fonteId, 'documentos'] as const,
  documento: (fonteId: string, docId: string) =>
    ['ia', 'conhecimento', fonteId, 'documento', docId] as const,
};

export function useDocumentosConhecimento(fonteId: string | undefined) {
  return useQuery({
    queryKey: conhecimentoKeys.documentos(fonteId ?? ''),
    queryFn: () => listarDocumentos(fonteId!),
    enabled: !!fonteId,
  });
}

export function useDocumentoConhecimento(fonteId: string | undefined, docId: string | undefined) {
  return useQuery({
    queryKey: conhecimentoKeys.documento(fonteId ?? '', docId ?? ''),
    queryFn: () => obterDocumento(fonteId!, docId!),
    enabled: !!fonteId && !!docId,
  });
}

export function useSalvarDocumento(fonteId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (v: { docId?: string; caminho: string; conteudo: string }) =>
      v.docId
        ? atualizarDocumento(fonteId, v.docId, { caminho: v.caminho, conteudo: v.conteudo })
        : criarDocumento(fonteId, { caminho: v.caminho, conteudo: v.conteudo }),
    onSuccess: () =>
      client.invalidateQueries({ queryKey: conhecimentoKeys.documentos(fonteId) }),
  });
}

export function useRemoverDocumento(fonteId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (docId: string) => removerDocumento(fonteId, docId),
    onSuccess: () =>
      client.invalidateQueries({ queryKey: conhecimentoKeys.documentos(fonteId) }),
  });
}

/** Extração do modelo é pesada (lê o schema inteiro) — sem retry automático. */
export function useExtrairModelo(fonteId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (maxTabelas: number) => extrairModelo(fonteId, maxTabelas),
    retry: false,
    onSuccess: () =>
      client.invalidateQueries({ queryKey: conhecimentoKeys.documentos(fonteId) }),
  });
}

/** Backfill de embeddings (RAG). Pesado e idempotente — sem retry automático. */
export function useGerarEmbeddings(fonteId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => gerarEmbeddings(fonteId),
    retry: false,
    onSuccess: () =>
      client.invalidateQueries({ queryKey: conhecimentoKeys.documentos(fonteId) }),
  });
}
