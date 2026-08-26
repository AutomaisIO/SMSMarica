import { http } from '@/shared/api/httpClient';
import type { PendenciaCadastro, StatusPendenciaCadastro } from '@/features/pendencias-cadastro/types';

export async function listarPendencias(status?: StatusPendenciaCadastro): Promise<PendenciaCadastro[]> {
  const { data } = await http.get<PendenciaCadastro[]>('/pendencias-cadastro', {
    params: { status: status ?? undefined },
  });
  return data;
}

export async function resolverPendencia(id: string, nota: string | null): Promise<void> {
  await http.post(`/pendencias-cadastro/${id}/resolver`, { nota });
}

export async function ignorarPendencia(id: string, nota: string | null): Promise<void> {
  await http.post(`/pendencias-cadastro/${id}/ignorar`, { nota });
}
