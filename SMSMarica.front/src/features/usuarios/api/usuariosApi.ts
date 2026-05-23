import { http } from '@/shared/api/httpClient';
import type { PermissaoModuloApi } from '@/features/perfis/types';
import type {
  AtualizarUsuarioPayload,
  CadastrarUsuarioPayload,
  Usuario,
  UsuarioListItem,
} from '@/features/usuarios/types';

export type PermissoesUsuarioApi = {
  herdadas: PermissaoModuloApi[];
  overrides: PermissaoModuloApi[];
  resolvidas: PermissaoModuloApi[];
};

export async function listarUsuarios(): Promise<UsuarioListItem[]> {
  const { data } = await http.get<UsuarioListItem[]>('/usuarios');
  return data;
}

export async function obterUsuarioPorId(id: string): Promise<Usuario> {
  const { data } = await http.get<Usuario>(`/usuarios/${id}`);
  return data;
}

export async function cadastrarUsuario(payload: CadastrarUsuarioPayload): Promise<string> {
  const { data } = await http.post<string>('/usuarios', payload);
  return data;
}

export async function atualizarUsuario(id: string, payload: AtualizarUsuarioPayload): Promise<void> {
  await http.put(`/usuarios/${id}`, payload);
}

export async function desativarUsuario(id: string): Promise<void> {
  await http.delete(`/usuarios/${id}`);
}

export async function obterPermissoesDoUsuario(id: string): Promise<PermissoesUsuarioApi> {
  const { data } = await http.get<PermissoesUsuarioApi>(`/usuarios/${id}/permissoes`);
  return data;
}

export async function atualizarPerfisDoUsuario(id: string, perfilIds: string[]): Promise<void> {
  await http.put(`/usuarios/${id}/perfis`, { perfilIds });
}

export async function atualizarOverridesDoUsuario(
  id: string,
  overrides: PermissaoModuloApi[],
): Promise<void> {
  await http.put(`/usuarios/${id}/overrides`, { overrides });
}
