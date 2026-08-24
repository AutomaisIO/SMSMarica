import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  criarApiToken,
  listarApiTokens,
  revogarApiToken,
} from '@/features/api-tokens/api/apiTokensApi';

export const apiTokensKeys = {
  raiz: ['api-tokens'] as const,
  lista: () => ['api-tokens', 'lista'] as const,
};

export function useListarApiTokens() {
  return useQuery({
    queryKey: apiTokensKeys.lista(),
    queryFn: listarApiTokens,
  });
}

export function useCriarApiToken() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (nome: string) => criarApiToken(nome),
    onSuccess: () => client.invalidateQueries({ queryKey: apiTokensKeys.raiz }),
  });
}

export function useRevogarApiToken() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => revogarApiToken(id),
    onSuccess: () => client.invalidateQueries({ queryKey: apiTokensKeys.raiz }),
  });
}
