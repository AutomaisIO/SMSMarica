import { http } from '@/shared/api/httpClient';
import type { ProcedimentoSigtap } from '@/features/procedimentos-sigtap/types';

export async function listarProcedimentos(
  busca?: string,
  grupo?: string,
  limite = 50,
): Promise<ProcedimentoSigtap[]> {
  const { data } = await http.get<ProcedimentoSigtap[]>('/procedimentos-sigtap', {
    params: {
      busca: busca?.trim() || undefined,
      grupo: grupo?.trim() || undefined,
      limite,
    },
  });
  return data;
}

export async function obterProcedimento(id: string): Promise<ProcedimentoSigtap> {
  const { data } = await http.get<ProcedimentoSigtap>(`/procedimentos-sigtap/${id}`);
  return data;
}
