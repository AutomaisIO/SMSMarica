import { http } from '@/shared/api/httpClient';
import type { ApiTokenCriado, ApiTokenListItem } from '@/features/api-tokens/types';

export async function listarApiTokens(): Promise<ApiTokenListItem[]> {
  const { data } = await http.get<ApiTokenListItem[]>('/api-tokens');
  return data;
}

export async function criarApiToken(nome: string): Promise<ApiTokenCriado> {
  const { data } = await http.post<ApiTokenCriado>('/api-tokens', { nome });
  return data;
}

export async function revogarApiToken(id: string): Promise<void> {
  await http.delete(`/api-tokens/${id}`);
}
