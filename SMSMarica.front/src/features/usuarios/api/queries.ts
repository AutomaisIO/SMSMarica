import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarOverridesDoUsuario,
  atualizarPerfisDoUsuario,
  atualizarUsuario,
  cadastrarUsuario,
  desativarUsuario,
  listarUsuarios,
  obterPermissoesDoUsuario,
  obterUsuarioPorId,
} from '@/features/usuarios/api/usuariosApi';
import type { PermissaoModuloApi } from '@/features/perfis/types';
import type {
  AtualizarUsuarioPayload,
  CadastrarUsuarioPayload,
} from '@/features/usuarios/types';

export const usuariosKeys = {
  lista: () => ['usuarios', 'lista'] as const,
  porId: (id: string) => ['usuarios', 'detalhe', id] as const,
  permissoes: (id: string) => ['usuarios', 'permissoes', id] as const,
};

export function useUsuarioPermissoes(id: string | null) {
  return useQuery({
    queryKey: id ? usuariosKeys.permissoes(id) : ['usuarios', 'permissoes', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterPermissoesDoUsuario(id);
    },
    enabled: Boolean(id),
  });
}

export function useAtualizarPerfisDoUsuario() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, perfilIds }: { id: string; perfilIds: string[] }) =>
      atualizarPerfisDoUsuario(id, perfilIds),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: usuariosKeys.porId(v.id) });
      client.invalidateQueries({ queryKey: usuariosKeys.permissoes(v.id) });
    },
  });
}

export function useAtualizarOverridesDoUsuario() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, overrides }: { id: string; overrides: PermissaoModuloApi[] }) =>
      atualizarOverridesDoUsuario(id, overrides),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: usuariosKeys.permissoes(v.id) });
    },
  });
}

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
