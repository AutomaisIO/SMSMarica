import { http } from '@/shared/api/httpClient';
import type {
  AtualizarPacientePayload,
  CadastrarPacientePayload,
  Paciente,
  PacienteListItem,
} from '@/features/pacientes/types';

export async function listarPacientes(): Promise<PacienteListItem[]> {
  const { data } = await http.get<PacienteListItem[]>('/pacientes');
  return data;
}

export async function obterPacientePorId(id: string): Promise<Paciente> {
  const { data } = await http.get<Paciente>(`/pacientes/${id}`);
  return data;
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
