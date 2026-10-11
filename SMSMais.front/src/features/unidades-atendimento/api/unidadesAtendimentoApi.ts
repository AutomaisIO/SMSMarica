import { http } from '@/shared/api/httpClient';
import type {
  SalvarUnidadeAtendimentoPayload,
  UnidadeAtendimento,
  UnidadeAtendimentoListItem,
} from '@/features/unidades-atendimento/types';

export async function listarUnidadesAtendimento(incluirInativas: boolean): Promise<UnidadeAtendimentoListItem[]> {
  const { data } = await http.get<UnidadeAtendimentoListItem[]>('/unidades-atendimento', {
    params: incluirInativas ? { incluirInativas: true } : undefined,
  });
  return data;
}

export async function obterUnidadeAtendimento(id: string): Promise<UnidadeAtendimento> {
  const { data } = await http.get<UnidadeAtendimento>(`/unidades-atendimento/${id}`);
  return data;
}

export async function cadastrarUnidadeAtendimento(payload: SalvarUnidadeAtendimentoPayload): Promise<string> {
  const { data } = await http.post<string>('/unidades-atendimento', payload);
  return data;
}

export async function atualizarUnidadeAtendimento(id: string, payload: SalvarUnidadeAtendimentoPayload): Promise<void> {
  await http.put(`/unidades-atendimento/${id}`, payload);
}

export async function desativarUnidadeAtendimento(id: string): Promise<void> {
  await http.delete(`/unidades-atendimento/${id}`);
}

export async function reativarUnidadeAtendimento(id: string): Promise<void> {
  await http.post(`/unidades-atendimento/${id}/reativar`);
}
