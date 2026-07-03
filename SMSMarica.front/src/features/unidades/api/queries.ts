import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  adicionarUsuarioNaUnidade,
  atualizarUnidade,
  cadastrarUnidade,
  desativarUnidade,
  listarUnidades,
  listarUsuariosDaUnidade,
  obterUnidadePorId,
  removerUsuarioDaUnidade,
} from '@/features/unidades/api/unidadesApi';
import type { SalvarUnidadePayload } from '@/features/unidades/types';

export const unidadesKeys = {
  lista: () => ['unidades', 'lista'] as const,
  porId: (id: string) => ['unidades', 'detalhe', id] as const,
  usuarios: (id: string) => ['unidades', 'usuarios', id] as const,
};

export function useUsuariosDaUnidade(unidadeId: string | null) {
  return useQuery({
    queryKey: unidadeId ? unidadesKeys.usuarios(unidadeId) : ['unidades', 'usuarios', 'nenhum'],
    queryFn: () => {
      if (!unidadeId) throw new Error('ID não informado.');
      return listarUsuariosDaUnidade(unidadeId);
    },
    enabled: Boolean(unidadeId),
  });
}

export function useAdicionarUsuarioNaUnidade() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ unidadeId, usuarioId }: { unidadeId: string; usuarioId: string }) =>
      adicionarUsuarioNaUnidade(unidadeId, usuarioId),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: unidadesKeys.usuarios(v.unidadeId) });
      // A tela de edição do usuário também mostra esses vínculos.
      client.invalidateQueries({ queryKey: ['usuarios', 'unidades', v.usuarioId] });
    },
  });
}

export function useRemoverUsuarioDaUnidade() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ unidadeId, usuarioId }: { unidadeId: string; usuarioId: string }) =>
      removerUsuarioDaUnidade(unidadeId, usuarioId),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: unidadesKeys.usuarios(v.unidadeId) });
      client.invalidateQueries({ queryKey: ['usuarios', 'unidades', v.usuarioId] });
    },
  });
}

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
