import { http } from '@/shared/api/httpClient';
import type {
  AtualizarPerfilPayload,
  CadastrarPerfilPayload,
  Perfil,
  PerfilListItem,
} from '@/features/perfis/types';

export async function listarPerfis(): Promise<PerfilListItem[]> {
  const { data } = await http.get<PerfilListItem[]>('/perfis');
  return data;
}

export async function obterPerfilPorId(id: string): Promise<Perfil> {
  const { data } = await http.get<Perfil>(`/perfis/${id}`);
  return data;
}

export async function cadastrarPerfil(payload: CadastrarPerfilPayload): Promise<string> {
  const { data } = await http.post<string>('/perfis', payload);
  return data;
}

export async function atualizarPerfil(id: string, payload: AtualizarPerfilPayload): Promise<void> {
  await http.put(`/perfis/${id}`, payload);
}

export async function desativarPerfil(id: string): Promise<void> {
  await http.delete(`/perfis/${id}`);
}
