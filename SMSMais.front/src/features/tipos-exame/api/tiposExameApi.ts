import { http } from '@/shared/api/httpClient';
import type {
  ModalidadeDicom,
  SalvarTipoExamePayload,
  TipoExame,
  TipoExameListItem,
} from '@/features/tipos-exame/types';

export async function listarTiposExame(
  modalidade?: ModalidadeDicom,
  incluirInativos = false,
): Promise<TipoExameListItem[]> {
  const { data } = await http.get<TipoExameListItem[]>('/tipos-exame', {
    params: {
      modalidade: modalidade || undefined,
      incluirInativos: incluirInativos ? 'true' : undefined,
    },
  });
  return data;
}

export async function obterTipoExame(id: string): Promise<TipoExame> {
  const { data } = await http.get<TipoExame>(`/tipos-exame/${id}`);
  return data;
}

export async function cadastrarTipoExame(payload: SalvarTipoExamePayload): Promise<string> {
  const { ativo: _ativo, ...resto } = payload;
  const { data } = await http.post<string>('/tipos-exame', resto);
  return data;
}

export async function atualizarTipoExame(id: string, payload: SalvarTipoExamePayload): Promise<void> {
  await http.put(`/tipos-exame/${id}`, { ...payload, ativo: payload.ativo ?? true });
}

export async function excluirTipoExame(id: string): Promise<void> {
  await http.delete(`/tipos-exame/${id}`);
}
