import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarPerfil,
  cadastrarPerfil,
  desativarPerfil,
  listarPerfis,
  obterPerfilPorId,
} from '@/features/perfis/api/perfisApi';
import type { AtualizarPerfilPayload, CadastrarPerfilPayload } from '@/features/perfis/types';

export const perfisKeys = {
  lista: () => ['perfis', 'lista'] as const,
  porId: (id: string) => ['perfis', 'detalhe', id] as const,
};

export function useListarPerfis() {
  return useQuery({ queryKey: perfisKeys.lista(), queryFn: listarPerfis });
}

export function usePerfilPorId(id: string | null) {
  return useQuery({
    queryKey: id ? perfisKeys.porId(id) : ['perfis', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterPerfilPorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarPerfil() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarPerfilPayload) => cadastrarPerfil(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: ['perfis'] }),
  });
}

export function useAtualizarPerfil() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarPerfilPayload }) =>
      atualizarPerfil(id, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: ['perfis'] }),
  });
}

export function useDesativarPerfil() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarPerfil(id),
    onSuccess: () => client.invalidateQueries({ queryKey: ['perfis'] }),
  });
}
