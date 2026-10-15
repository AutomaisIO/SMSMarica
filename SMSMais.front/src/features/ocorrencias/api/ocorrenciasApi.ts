import { http } from '@/shared/api/httpClient';
import type { Ocorrencia } from '@/features/ocorrencias/types';

/** Em aberto (aberta ou em atendimento) ou, com `encerradas`, as dos últimos 30 dias. */
export async function listarOcorrencias(encerradas: boolean): Promise<Ocorrencia[]> {
  const { data } = await http.get<Ocorrencia[]>('/rastreamento/ocorrencias', {
    params: encerradas ? { encerradas: true } : undefined,
  });
  return data;
}

export async function atenderOcorrencia(id: string): Promise<Ocorrencia> {
  const { data } = await http.post<Ocorrencia>(`/rastreamento/ocorrencias/${id}/atender`);
  return data;
}

export async function encerrarOcorrencia(id: string, observacao: string | null): Promise<Ocorrencia> {
  const { data } = await http.post<Ocorrencia>(`/rastreamento/ocorrencias/${id}/encerrar`, { observacao });
  return data;
}
