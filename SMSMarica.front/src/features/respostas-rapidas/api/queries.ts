import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarRespostaRapida,
  criarRespostaRapida,
  excluirRespostaRapida,
  listarRespostasRapidas,
  listarTagsAutomaticas,
  obterRespostaRapida,
} from '@/features/respostas-rapidas/api/respostasRapidasApi';
import type { SalvarRespostaRapidaPayload } from '@/features/respostas-rapidas/types';

export const respostasRapidasKeys = {
  raiz: ['respostas-rapidas'] as const,
  lista: (incluirInativas: boolean) => ['respostas-rapidas', 'lista', incluirInativas] as const,
  detalhe: (id: string) => ['respostas-rapidas', 'detalhe', id] as const,
  tags: () => ['respostas-rapidas', 'tags'] as const,
};

export function useRespostasRapidas(incluirInativas = false, habilitado = true) {
  return useQuery({
    queryKey: respostasRapidasKeys.lista(incluirInativas),
    queryFn: () => listarRespostasRapidas(incluirInativas),
    enabled: habilitado,
    staleTime: 5 * 60_000,
  });
}

export function useRespostaRapida(id: string | null) {
  return useQuery({
    queryKey: respostasRapidasKeys.detalhe(id ?? 'nenhuma'),
    queryFn: () => obterRespostaRapida(id!),
    enabled: Boolean(id),
  });
}

export function useTagsAutomaticas() {
  return useQuery({
    queryKey: respostasRapidasKeys.tags(),
    queryFn: listarTagsAutomaticas,
    staleTime: Infinity, // catálogo fixo do backend
  });
}

export function useCriarRespostaRapida() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarRespostaRapidaPayload) => criarRespostaRapida(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: respostasRapidasKeys.raiz }),
  });
}

export function useAtualizarRespostaRapida() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarRespostaRapidaPayload }) =>
      atualizarRespostaRapida(id, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: respostasRapidasKeys.raiz }),
  });
}

export function useExcluirRespostaRapida() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirRespostaRapida(id),
    onSuccess: () => client.invalidateQueries({ queryKey: respostasRapidasKeys.raiz }),
  });
}
