import { http } from '@/shared/api/httpClient';
import type {
  PendenteMapeamento,
  TipoExameOpcao,
  VincularResultado,
} from '@/features/mapeamento-sigtap/types';

export async function listarPendentes(): Promise<PendenteMapeamento[]> {
  const { data } = await http.get<PendenteMapeamento[]>('/mapeamento-sigtap/pendentes');
  return data;
}

export async function vincularMapeamento(
  sigtapCodigo: string,
  tipoExameId: string,
): Promise<VincularResultado> {
  const { data } = await http.post<VincularResultado>('/mapeamento-sigtap/vincular', {
    sigtapCodigo,
    tipoExameId,
  });
  return data;
}

export async function listarTiposExameOpcoes(): Promise<TipoExameOpcao[]> {
  const { data } = await http.get<TipoExameOpcao[]>('/tipos-exame');
  return data.map((t) => ({ id: t.id, nome: t.nome }));
}
