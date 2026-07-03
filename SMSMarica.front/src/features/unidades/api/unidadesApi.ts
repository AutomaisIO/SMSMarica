import { http } from '@/shared/api/httpClient';
import type {
  SalvarUnidadePayload,
  Unidade,
  UnidadeListItem,
} from '@/features/unidades/types';

export async function listarUnidades(): Promise<UnidadeListItem[]> {
  const { data } = await http.get<UnidadeListItem[]>('/unidades');
  return data;
}

export async function obterUnidadePorId(id: string): Promise<Unidade> {
  const { data } = await http.get<Unidade>(`/unidades/${id}`);
  return data;
}

export async function cadastrarUnidade(payload: SalvarUnidadePayload): Promise<string> {
  const { data } = await http.post<string>('/unidades', payload);
  return data;
}

export async function atualizarUnidade(id: string, payload: SalvarUnidadePayload): Promise<void> {
  await http.put(`/unidades/${id}`, payload);
}

export async function desativarUnidade(id: string): Promise<void> {
  await http.delete(`/unidades/${id}`);
}

/** Usuário vinculado à unidade (visão reversa unidade→usuários). */
export type UsuarioDaUnidade = {
  usuarioId: string;
  nomeCompleto: string;
  email: string | null;
  ativo: boolean;
  principal: boolean;
};

export async function listarUsuariosDaUnidade(unidadeId: string): Promise<UsuarioDaUnidade[]> {
  const { data } = await http.get<UsuarioDaUnidade[]>(`/unidades/${unidadeId}/usuarios`);
  return data;
}

export async function adicionarUsuarioNaUnidade(unidadeId: string, usuarioId: string): Promise<void> {
  await http.post(`/unidades/${unidadeId}/usuarios/${usuarioId}`);
}

export async function removerUsuarioDaUnidade(unidadeId: string, usuarioId: string): Promise<void> {
  await http.delete(`/unidades/${unidadeId}/usuarios/${usuarioId}`);
}
