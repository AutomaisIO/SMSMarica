import { http } from '@/shared/api/httpClient';
import type {
  AtualizarMedicoPayload,
  CadastrarMedicoPayload,
  FiltroConselho,
  Medico,
  MedicoListItem,
} from '@/features/medicos/types';

export async function buscarMedicos(
  termo = '',
  filtro: FiltroConselho = {},
): Promise<MedicoListItem[]> {
  const t = termo.trim();
  // Sem termo o backend devolve os 10 últimos cadastros; com termo, busca por
  // nome (qualquer parte) ou CPF. `conselho`/`conselhoExceto` separam médicos
  // (CRM) dos demais profissionais.
  const params: Record<string, string> = {};
  if (t) params.termo = t;
  if (filtro.conselho) params.conselho = filtro.conselho;
  if (filtro.conselhoExceto) params.conselhoExceto = filtro.conselhoExceto;
  const { data } = await http.get<MedicoListItem[]>('/medicos', {
    params: Object.keys(params).length > 0 ? params : undefined,
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

export async function atualizarMedico(
  id: string,
  payload: AtualizarMedicoPayload,
): Promise<void> {
  await http.put(`/medicos/${id}`, payload);
}

export async function desativarMedico(id: string): Promise<void> {
  await http.delete(`/medicos/${id}`);
}
