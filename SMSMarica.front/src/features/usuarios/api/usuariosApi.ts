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

/** Retorna o usuário existente com este CPF, ou null se não houver. */
export async function consultarUsuarioPorCpf(cpf: string): Promise<Usuario | null> {
  const cpfLimpo = cpf.replace(/\D/g, '');
  if (cpfLimpo.length !== 11) return null;
  const r = await http.get<Usuario>(`/usuarios/cpf/${cpfLimpo}`, {
    validateStatus: (s) => s === 200 || s === 204,
  });
  return r.status === 200 ? r.data : null;
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

/** Vínculo usuário↔unidade (usuario_unidade) — base do multitenant e das filas do chat. */
export type VinculoUnidadeApi = { unidadeId: string; unidadeNome: string; principal: boolean };

export async function obterUnidadesDoUsuario(id: string): Promise<VinculoUnidadeApi[]> {
  const { data } = await http.get<VinculoUnidadeApi[]>(`/usuarios/${id}/unidades`);
  return data;
}

export async function atualizarUnidadesDoUsuario(
  id: string,
  unidades: { unidadeId: string; principal: boolean }[],
): Promise<void> {
  await http.put(`/usuarios/${id}/unidades`, { unidades });
}

export async function alterarSenhaDoUsuario(
  id: string,
  payload: { senhaNova: string; deveTrocarNoProximoLogin: boolean },
): Promise<void> {
  await http.put(`/usuarios/${id}/senha`, payload);
}

export type SenhaGeradaDto = { senhaGerada: string; deveTrocarNoProximoLogin: boolean };

export async function gerarNovaSenhaDoUsuario(id: string): Promise<SenhaGeradaDto> {
  const { data } = await http.post<SenhaGeradaDto>(`/usuarios/${id}/senha/gerar`);
  return data;
}

export async function alterarMinhaSenha(payload: { senhaAtual: string; senhaNova: string }): Promise<void> {
  await http.put('/identidade/me/senha', payload);
}

export async function obterMeuPerfil(): Promise<Usuario> {
  const { data } = await http.get<Usuario>('/identidade/me');
  return data;
}

export async function atualizarMinhaConta(payload: {
  telefone?: string;
  endereco: import('@/features/usuarios/types').EnderecoDto | null;
  fotoBase64?: string | null;
}): Promise<void> {
  await http.put('/identidade/me', payload);
}
