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

// Os campos da worklist (limite de descrição e código do procedimento) VIAJAM SEMPRE que o
// formulário os tem. Até 16/10/2026 o limite não era enviado e cada edição gravava 64 por cima
// do 16 do mamógrafo Fuji; o servidor agora trata ausente como "mantém", e o painel manda o valor.
function camposDaWorklist(payload: SalvarEquipamentoPayload) {
  return {
    descricaoMaxCaracteres: payload.descricaoMaxCaracteres,
    codigoNoProcedimentoSolicitado: payload.codigoNoProcedimentoSolicitado,
    codigoNoProtocoloAgendado: payload.codigoNoProtocoloAgendado,
    codigoProcedimentoEsquema: payload.codigoProcedimentoEsquema?.trim() || null,
  };
}

export async function cadastrarEquipamento(payload: SalvarEquipamentoPayload): Promise<string> {
  const { data } = await http.post<string>('/equipamentos', {
    nome: payload.nome,
    unidadeId: payload.unidadeId,
    modalidadeDicom: payload.modalidadeDicom,
    identificadorDicom: payload.identificadorDicom || null,
    ...camposDaWorklist(payload),
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
    ...camposDaWorklist(payload),
  });
}

export async function excluirEquipamento(id: string): Promise<void> {
  await http.delete(`/equipamentos/${id}`);
}
