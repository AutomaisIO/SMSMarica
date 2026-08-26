import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ignorarPendencia,
  listarPendencias,
  resolverPendencia,
} from '@/features/pendencias-cadastro/api/pendenciasApi';
import type { StatusPendenciaCadastro } from '@/features/pendencias-cadastro/types';

export const pendenciasKeys = {
  raiz: ['pendencias-cadastro'] as const,
  lista: (status?: StatusPendenciaCadastro) =>
    ['pendencias-cadastro', 'lista', { status: status ?? 'todas' }] as const,
};

export function useListarPendencias(status?: StatusPendenciaCadastro) {
  return useQuery({
    queryKey: pendenciasKeys.lista(status),
    queryFn: () => listarPendencias(status),
  });
}

export function useResolverPendencia() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, nota }: { id: string; nota: string | null }) => resolverPendencia(id, nota),
    onSuccess: () => client.invalidateQueries({ queryKey: pendenciasKeys.raiz }),
  });
}

export function useIgnorarPendencia() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, nota }: { id: string; nota: string | null }) => ignorarPendencia(id, nota),
    onSuccess: () => client.invalidateQueries({ queryKey: pendenciasKeys.raiz }),
  });
}
