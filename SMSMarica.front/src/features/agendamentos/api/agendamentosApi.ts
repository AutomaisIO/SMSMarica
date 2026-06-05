import { http } from '@/shared/api/httpClient';
import type {
  AdicionarAvulsoPayload,
  AdicionarBloqueioPayload,
  AdicionarRecorrenciaPayload,
  Agenda,
  AgendaListItem,
  Agendamento,
  AgendamentoListItem,
  AgendarPayload,
  AtualizarAgendaPayload,
  CadastrarAgendaPayload,
  DisponibilidadesAgenda,
  SlotLivre,
} from '@/features/agendamentos/types';

export type FiltroAgendas = {
  unidadeId?: string;
  especialidadeId?: string;
  medicoId?: string;
  incluirInativas?: boolean;
};

export async function listarAgendas(filtro: FiltroAgendas = {}): Promise<AgendaListItem[]> {
  const { data } = await http.get<AgendaListItem[]>('/agendas', {
    params: {
      unidadeId: filtro.unidadeId || undefined,
      especialidadeId: filtro.especialidadeId || undefined,
      medicoId: filtro.medicoId || undefined,
      incluirInativas: filtro.incluirInativas ? 'true' : undefined,
    },
  });
  return data;
}

export async function obterAgenda(id: string): Promise<Agenda> {
  const { data } = await http.get<Agenda>(`/agendas/${id}`);
  return data;
}

export async function cadastrarAgenda(payload: CadastrarAgendaPayload): Promise<string> {
  const { data } = await http.post<string>('/agendas', payload);
  return data;
}

export async function atualizarAgenda(id: string, payload: AtualizarAgendaPayload): Promise<void> {
  await http.put(`/agendas/${id}`, payload);
}

export async function excluirAgenda(id: string): Promise<void> {
  await http.delete(`/agendas/${id}`);
}

export async function adicionarRecorrencia(
  agendaId: string,
  payload: AdicionarRecorrenciaPayload,
): Promise<string> {
  const { data } = await http.post<string>(`/agendas/${agendaId}/recorrencias`, payload);
  return data;
}

export async function removerRecorrencia(agendaId: string, recorrenciaId: string): Promise<void> {
  await http.delete(`/agendas/${agendaId}/recorrencias/${recorrenciaId}`);
}

export async function listarDisponibilidades(
  agendaId: string,
  inicio: string,
  fim: string,
): Promise<DisponibilidadesAgenda> {
  const { data } = await http.get<DisponibilidadesAgenda>(`/agendas/${agendaId}/disponibilidades`, {
    params: { inicio, fim },
  });
  return data;
}

export async function adicionarAvulso(agendaId: string, payload: AdicionarAvulsoPayload): Promise<string> {
  const { data } = await http.post<string>(`/agendas/${agendaId}/avulsos`, payload);
  return data;
}

export async function removerAvulso(agendaId: string, avulsoId: string): Promise<void> {
  await http.delete(`/agendas/${agendaId}/avulsos/${avulsoId}`);
}

export async function adicionarBloqueio(agendaId: string, payload: AdicionarBloqueioPayload): Promise<string> {
  const { data } = await http.post<string>(`/agendas/${agendaId}/bloqueios`, payload);
  return data;
}

export async function removerBloqueio(agendaId: string, bloqueioId: string): Promise<void> {
  await http.delete(`/agendas/${agendaId}/bloqueios/${bloqueioId}`);
}

export async function horariosLivres(agendaId: string, inicio: string, fim: string): Promise<SlotLivre[]> {
  const { data } = await http.get<SlotLivre[]>(`/agendamentos/agendas/${agendaId}/horarios-livres`, {
    params: { inicio, fim },
  });
  return data;
}

export async function listarAgendamentos(
  agendaId: string,
  inicio: string,
  fim: string,
): Promise<AgendamentoListItem[]> {
  const { data } = await http.get<AgendamentoListItem[]>(`/agendamentos/agendas/${agendaId}`, {
    params: { inicio, fim },
  });
  return data;
}

export async function obterAgendamento(id: string): Promise<Agendamento> {
  const { data } = await http.get<Agendamento>(`/agendamentos/${id}`);
  return data;
}

export async function agendar(payload: AgendarPayload): Promise<string> {
  const { data } = await http.post<string>('/agendamentos', payload);
  return data;
}

export async function confirmarAgendamento(id: string): Promise<void> {
  await http.post(`/agendamentos/${id}/confirmar`);
}

export async function realizarAgendamento(id: string): Promise<void> {
  await http.post(`/agendamentos/${id}/realizar`);
}

export async function registrarFalta(id: string): Promise<void> {
  await http.post(`/agendamentos/${id}/falta`);
}

export async function cancelarAgendamento(id: string, motivo?: string | null): Promise<void> {
  await http.post(`/agendamentos/${id}/cancelar`, { motivo: motivo || null });
}
