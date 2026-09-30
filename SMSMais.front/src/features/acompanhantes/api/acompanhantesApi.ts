import { http } from '@/shared/api/httpClient';
import type {
  Acompanhante,
  AdicionarAcompanhantePayload,
  ConsultaAcompanhante,
} from '@/features/acompanhantes/types';

export async function listarAcompanhantes(pacienteId: string): Promise<Acompanhante[]> {
  const { data } = await http.get<Acompanhante[]>(`/pacientes/${pacienteId}/acompanhantes`);
  return data;
}

/** Confere CPF + nascimento e devolve o nome (base de pacientes primeiro; senão, consulta de CPF). */
export async function consultarAcompanhante(
  pacienteId: string,
  payload: { cpf: string; dataNascimento: string },
): Promise<ConsultaAcompanhante> {
  const { data } = await http.post<ConsultaAcompanhante>(`/pacientes/${pacienteId}/acompanhantes/consulta`, payload);
  return data;
}

export async function adicionarAcompanhante(
  pacienteId: string,
  payload: AdicionarAcompanhantePayload,
): Promise<Acompanhante> {
  const { data } = await http.post<Acompanhante>(`/pacientes/${pacienteId}/acompanhantes`, payload);
  return data;
}

export async function removerAcompanhante(pacienteId: string, acompanhanteId: string): Promise<void> {
  await http.delete(`/pacientes/${pacienteId}/acompanhantes/${acompanhanteId}`);
}
