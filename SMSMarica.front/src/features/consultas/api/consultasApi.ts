import { http } from '@/shared/api/httpClient';
import type { ConsultaDetalhe, ConsultaListItem, FiltroConsultas } from '@/features/consultas/types';

export async function listarConsultas(filtro: FiltroConsultas = {}): Promise<ConsultaListItem[]> {
  const { data } = await http.get<ConsultaListItem[]>('/consultas', {
    params: {
      pacienteId: filtro.pacienteId || undefined,
      busca: filtro.busca || undefined,
      dataInicial: filtro.dataInicial || undefined,
      dataFinal: filtro.dataFinal || undefined,
      limite: filtro.limite || undefined,
    },
  });
  return data;
}

export async function obterConsulta(id: string): Promise<ConsultaDetalhe> {
  const { data } = await http.get<ConsultaDetalhe>(`/consultas/${id}`);
  return data;
}
