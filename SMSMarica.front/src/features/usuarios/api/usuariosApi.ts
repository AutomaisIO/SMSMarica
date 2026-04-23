import { http } from '@/shared/api/httpClient';
import type {
  AtualizarUsuarioPayload,
  CadastrarUsuarioPayload,
  Usuario,
  UsuarioListItem,
} from '@/features/usuarios/types';

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
