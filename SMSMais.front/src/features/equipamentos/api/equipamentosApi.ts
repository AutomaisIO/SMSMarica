import { http } from '@/shared/api/httpClient';
import type {
  Equipamento,
  EquipamentoListItem,
  SalvarEquipamentoPayload,
} from '@/features/equipamentos/types';

export async function listarEquipamentos(
  unidadeId?: string,
  incluirInativos = false,
): Promise<EquipamentoListItem[]> {
  const { data } = await http.get<EquipamentoListItem[]>('/equipamentos', {
    params: {
      unidadeId: unidadeId || undefined,
      incluirInativos: incluirInativos ? 'true' : undefined,
    },
  });
  return data;
}

export async function obterEquipamento(id: string): Promise<Equipamento> {
  const { data } = await http.get<Equipamento>(`/equipamentos/${id}`);
  return data;
}

export async function cadastrarEquipamento(payload: SalvarEquipamentoPayload): Promise<string> {
  const { data } = await http.post<string>('/equipamentos', {
    nome: payload.nome,
    unidadeId: payload.unidadeId,
    modalidadeDicom: payload.modalidadeDicom,
    identificadorDicom: payload.identificadorDicom || null,
  });
  return data;
}

export async function atualizarEquipamento(id: string, payload: SalvarEquipamentoPayload): Promise<void> {
  await http.put(`/equipamentos/${id}`, {
    nome: payload.nome,
    unidadeId: payload.unidadeId,
    modalidadeDicom: payload.modalidadeDicom,
    identificadorDicom: payload.identificadorDicom || null,
    ativo: payload.ativo ?? true,
  });
}

export async function excluirEquipamento(id: string): Promise<void> {
  await http.delete(`/equipamentos/${id}`);
}
