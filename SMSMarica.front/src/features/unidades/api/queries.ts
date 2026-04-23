import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarUnidade,
  cadastrarUnidade,
  desativarUnidade,
  listarUnidades,
  obterUnidadePorId,
} from '@/features/unidades/api/unidadesApi';
import type { SalvarUnidadePayload } from '@/features/unidades/types';

export const unidadesKeys = {
  lista: () => ['unidades', 'lista'] as const,
  porId: (id: string) => ['unidades', 'detalhe', id] as const,
};

export function useListarUnidades() {
  return useQuery({ queryKey: unidadesKeys.lista(), queryFn: listarUnidades });
}

export function useUnidadePorId(id: string | null) {
  return useQuery({
    queryKey: id ? unidadesKeys.porId(id) : ['unidades', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterUnidadePorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarUnidade() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarUnidadePayload) => cadastrarUnidade(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: unidadesKeys.lista() }),
  });
}

export function useAtualizarUnidade() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarUnidadePayload }) =>
      atualizarUnidade(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: unidadesKeys.lista() });
      client.invalidateQueries({ queryKey: unidadesKeys.porId(v.id) });
    },
  });
}

export function useDesativarUnidade() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarUnidade(id),
    onSuccess: () => client.invalidateQueries({ queryKey: unidadesKeys.lista() }),
  });
}
