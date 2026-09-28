import { http } from '@/shared/api/httpClient';
import type {
  CredencialSoftphone,
  DefinirSoftphonePayload,
  RamaisLivres,
  SoftphoneUsuario,
} from '@/features/telefonia/types';

export async function obterSoftphoneDoUsuario(usuarioId: string): Promise<SoftphoneUsuario> {
  const { data } = await http.get<SoftphoneUsuario>(`/usuarios/${usuarioId}/softphone`);
  return data;
}

export async function definirSoftphone(usuarioId: string, payload: DefinirSoftphonePayload): Promise<SoftphoneUsuario> {
  const { data } = await http.put<SoftphoneUsuario>(`/usuarios/${usuarioId}/softphone`, payload);
  return data;
}

export async function removerSoftphone(usuarioId: string): Promise<void> {
  await http.delete(`/usuarios/${usuarioId}/softphone`);
}

export async function listarRamaisLivres(): Promise<RamaisLivres> {
  const { data } = await http.get<RamaisLivres>('/telefonia/ramais/livres');
  return data;
}

export async function obterMeuSoftphone(): Promise<SoftphoneUsuario> {
  const { data } = await http.get<SoftphoneUsuario>('/identidade/me/softphone');
  return data;
}

/** Busca a credencial na hora de registrar. Não passa pelo react-query: não deve ficar em cache. */
export async function obterMinhaCredencial(): Promise<CredencialSoftphone> {
  const { data } = await http.get<CredencialSoftphone>('/identidade/me/softphone/credencial');
  return data;
}
