import { http } from '@/shared/api/httpClient';
import type {
  AtualizarMedicoPayload,
  CadastrarMedicoPayload,
  Medico,
  MedicoListItem,
  PromoverMedicoPayload,
} from '@/features/medicos/types';

export async function buscarMedicos(termo = ''): Promise<MedicoListItem[]> {
  const t = termo.trim();
  // Sem termo o backend devolve os 10 últimos cadastros; com termo, busca por
  // nome (qualquer parte) ou CPF. Mesma régua da busca de pacientes.
  const { data } = await http.get<MedicoListItem[]>('/medicos', {
    params: t ? { termo: t } : undefined,
  });
  return data;
}

export async function obterMedicoPorId(id: string): Promise<Medico> {
  const { data } = await http.get<Medico>(`/medicos/${id}`);
  return data;
}

export async function cadastrarMedico(payload: CadastrarMedicoPayload): Promise<string> {
  const { data } = await http.post<string>('/medicos', payload);
  return data;
}

export async function promoverMedico(payload: PromoverMedicoPayload): Promise<string> {
  const { data } = await http.post<string>('/medicos/promover', payload);
  return data;
}

export async function atualizarMedico(
  id: string,
  payload: AtualizarMedicoPayload,
): Promise<void> {
  await http.put(`/medicos/${id}`, payload);
}

export async function desativarMedico(id: string): Promise<void> {
  await http.delete(`/medicos/${id}`);
}
