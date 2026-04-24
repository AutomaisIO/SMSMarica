import { http } from '@/shared/api/httpClient';
import type {
  AtualizarVeiculoPayload,
  CadastrarVeiculoPayload,
  Veiculo,
  VeiculoListItem,
} from '@/features/veiculos/types';

export async function listarVeiculos(): Promise<VeiculoListItem[]> {
  const { data } = await http.get<VeiculoListItem[]>('/veiculos');
  return data;
}

export async function obterVeiculoPorId(id: string): Promise<Veiculo> {
  const { data } = await http.get<Veiculo>(`/veiculos/${id}`);
  return data;
}

export async function cadastrarVeiculo(payload: CadastrarVeiculoPayload): Promise<string> {
  const { data } = await http.post<string>('/veiculos', payload);
  return data;
}

export async function atualizarVeiculo(
  id: string,
  payload: AtualizarVeiculoPayload,
): Promise<void> {
  await http.put(`/veiculos/${id}`, payload);
}

export async function desativarVeiculo(id: string): Promise<void> {
  await http.delete(`/veiculos/${id}`);
}
