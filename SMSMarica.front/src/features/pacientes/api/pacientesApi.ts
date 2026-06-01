import { http } from '@/shared/api/httpClient';
import type {
  AtualizarPacientePayload,
  CadastrarPacientePayload,
  Paciente,
  PacienteExistencia,
  PacienteListItem,
} from '@/features/pacientes/types';

export async function buscarPacientes(termo: string): Promise<PacienteListItem[]> {
  const t = termo.trim();
  // Sem termo o backend devolve os 10 últimos cadastros; com termo, busca por
  // nome (qualquer parte) ou CPF.
  const { data } = await http.get<PacienteListItem[]>('/pacientes', {
    params: t ? { termo: t } : undefined,
  });
  return data;
}

export async function obterPacientePorId(id: string): Promise<Paciente> {
  const { data } = await http.get<Paciente>(`/pacientes/${id}`);
  return data;
}

export async function obterPacientePorCpf(cpf: string): Promise<PacienteExistencia | null> {
  const limpo = cpf.replace(/\D/g, '');
  if (limpo.length !== 11) return null;
  const resposta = await http.get<PacienteExistencia>(`/pacientes/por-cpf/${limpo}`, {
    validateStatus: (s) => s === 200 || s === 404,
  });
  return resposta.status === 404 ? null : resposta.data;
}

export async function cadastrarPaciente(payload: CadastrarPacientePayload): Promise<string> {
  const { data } = await http.post<string>('/pacientes', payload);
  return data;
}

export async function atualizarPaciente(
  id: string,
  payload: AtualizarPacientePayload,
): Promise<void> {
  await http.put(`/pacientes/${id}`, payload);
}

export async function desativarPaciente(id: string): Promise<void> {
  await http.delete(`/pacientes/${id}`);
}

export async function reativarPaciente(id: string): Promise<void> {
  await http.post(`/pacientes/${id}/reativar`);
}
