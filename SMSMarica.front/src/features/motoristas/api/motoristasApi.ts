import { http } from '@/shared/api/httpClient';
import type {
  AtualizarMotoristaPayload,
  CadastrarMotoristaPayload,
  Motorista,
  MotoristaListItem,
} from '@/features/motoristas/types';

export async function listarMotoristas(): Promise<MotoristaListItem[]> {
  const { data } = await http.get<MotoristaListItem[]>('/motoristas');
  return data;
}

export async function obterMotoristaPorId(id: string): Promise<Motorista> {
  const { data } = await http.get<Motorista>(`/motoristas/${id}`);
  return data;
}

export async function cadastrarMotorista(payload: CadastrarMotoristaPayload): Promise<string> {
  const { data } = await http.post<string>('/motoristas', payload);
  return data;
}

export async function atualizarMotorista(
  id: string,
  payload: AtualizarMotoristaPayload,
): Promise<void> {
  await http.put(`/motoristas/${id}`, payload);
}

export async function desativarMotorista(id: string): Promise<void> {
  await http.delete(`/motoristas/${id}`);
}
