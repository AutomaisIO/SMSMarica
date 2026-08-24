import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarEspecialidade,
  cadastrarEspecialidade,
  excluirEspecialidade,
  listarEspecialidades,
} from '@/features/especialidades/api/especialidadesApi';
import type { SalvarEspecialidadePayload } from '@/features/especialidades/types';

export const especialidadesKeys = {
  raiz: ['especialidades'] as const,
  lista: (incluirInativas?: boolean) =>
    ['especialidades', 'lista', { incluirInativas: !!incluirInativas }] as const,
};

export function useListarEspecialidades(incluirInativas = false) {
  return useQuery({
    queryKey: especialidadesKeys.lista(incluirInativas),
    queryFn: () => listarEspecialidades(incluirInativas),
  });
}

export function useCadastrarEspecialidade() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarEspecialidadePayload) => cadastrarEspecialidade(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: especialidadesKeys.raiz }),
  });
}

export function useAtualizarEspecialidade() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarEspecialidadePayload }) =>
      atualizarEspecialidade(id, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: especialidadesKeys.raiz }),
  });
}

export function useExcluirEspecialidade() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirEspecialidade(id),
    onSuccess: () => client.invalidateQueries({ queryKey: especialidadesKeys.raiz }),
  });
}
