import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarUsuario,
  cadastrarUsuario,
  desativarUsuario,
  listarUsuarios,
  obterUsuarioPorId,
} from '@/features/usuarios/api/usuariosApi';
import type {
  AtualizarUsuarioPayload,
  CadastrarUsuarioPayload,
} from '@/features/usuarios/types';

export const usuariosKeys = {
  lista: () => ['usuarios', 'lista'] as const,
  porId: (id: string) => ['usuarios', 'detalhe', id] as const,
};

export function useListarUsuarios() {
  return useQuery({ queryKey: usuariosKeys.lista(), queryFn: listarUsuarios });
}

export function useUsuarioPorId(id: string | null) {
  return useQuery({
    queryKey: id ? usuariosKeys.porId(id) : ['usuarios', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterUsuarioPorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarUsuario() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarUsuarioPayload) => cadastrarUsuario(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: usuariosKeys.lista() }),
  });
}

export function useAtualizarUsuario() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarUsuarioPayload }) =>
      atualizarUsuario(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: usuariosKeys.lista() });
      client.invalidateQueries({ queryKey: usuariosKeys.porId(v.id) });
    },
  });
}

export function useDesativarUsuario() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarUsuario(id),
    onSuccess: () => client.invalidateQueries({ queryKey: usuariosKeys.lista() }),
  });
}
