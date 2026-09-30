import { http } from '@/shared/api/httpClient';
import type {
  AdicionarSessaoPayload,
  AtualizarSessaoPayload,
  AtualizarTratamentoPayload,
  CadastrarTratamentoPayload,
  AgendaPayload,
  ConfirmarSessaoPayload,
  PreviaAgenda,
  TipoTratamento,
  Tratamento,
  TratamentoListItem,
  UnidadeAtendimentoOpcao,
} from '@/features/tratamentos/types';

export type FiltrosTratamentos = {
  pacienteId?: string;
  unidadeAtendimentoId?: string;
};

export async function listarTratamentos(filtros: FiltrosTratamentos = {}): Promise<TratamentoListItem[]> {
  const params: Record<string, string> = {};
  if (filtros.pacienteId) params.pacienteId = filtros.pacienteId;
  if (filtros.unidadeAtendimentoId) params.unidadeAtendimentoId = filtros.unidadeAtendimentoId;
  const { data } = await http.get<TratamentoListItem[]>('/tratamentos', {
    params: Object.keys(params).length ? params : undefined,
  });
  return data;
}

export async function listarTiposTratamento(): Promise<TipoTratamento[]> {
  const { data } = await http.get<TipoTratamento[]>('/tratamentos/tipos');
  return data;
}

/** Destinos ativos — sob a permissão de Tratamentos, sem exigir o módulo do cadastro das unidades. */
export async function listarOpcoesUnidadesAtendimento(): Promise<UnidadeAtendimentoOpcao[]> {
  const { data } = await http.get<UnidadeAtendimentoOpcao[]>('/tratamentos/unidades-atendimento');
  return data;
}

export async function obterTratamentoPorId(id: string): Promise<Tratamento> {
  const { data } = await http.get<Tratamento>(`/tratamentos/${id}`);
  return data;
}

/** As datas que a agenda geraria — o servidor é a única fonte das datas. */
export async function preverAgenda(payload: AgendaPayload): Promise<PreviaAgenda> {
  const { data } = await http.post<PreviaAgenda>('/tratamentos/agenda/previa', payload);
  return data;
}

/** Troca a agenda a partir da data de início nova (hoje ou depois). */
export async function alterarAgenda(id: string, payload: AgendaPayload): Promise<void> {
  await http.put(`/tratamentos/${id}/agenda`, payload);
}

/** Quem vai acompanhar o paciente nesta viagem (da lista dele, até o limite). */
export async function definirAcompanhantesSessao(id: string, sessaoId: string, acompanhanteIds: string[]): Promise<void> {
  await http.put(`/tratamentos/${id}/sessoes/${sessaoId}/acompanhantes`, { acompanhanteIds });
}

export async function cadastrarTratamento(payload: CadastrarTratamentoPayload): Promise<string> {
  const { data } = await http.post<string>('/tratamentos', payload);
  return data;
}

export async function atualizarTratamento(id: string, payload: AtualizarTratamentoPayload): Promise<void> {
  await http.put(`/tratamentos/${id}`, payload);
}

export async function encerrarTratamento(id: string): Promise<void> {
  await http.delete(`/tratamentos/${id}`);
}

export async function adicionarSessao(id: string, payload: AdicionarSessaoPayload): Promise<string> {
  const { data } = await http.post<string>(`/tratamentos/${id}/sessoes`, payload);
  return data;
}

export async function atualizarSessao(
  id: string,
  sessaoId: string,
  payload: AtualizarSessaoPayload,
): Promise<void> {
  await http.put(`/tratamentos/${id}/sessoes/${sessaoId}`, payload);
}

export async function cancelarSessao(id: string, sessaoId: string): Promise<void> {
  await http.delete(`/tratamentos/${id}/sessoes/${sessaoId}`);
}

export async function confirmarSessao(
  id: string,
  sessaoId: string,
  payload: ConfirmarSessaoPayload,
): Promise<void> {
  await http.post(`/tratamentos/${id}/sessoes/${sessaoId}/confirmar`, payload);
}
