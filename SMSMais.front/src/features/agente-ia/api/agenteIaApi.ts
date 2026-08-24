import { http } from '@/shared/api/httpClient';
import type {
  CriarSessaoPayload,
  SessaoDetalhe,
  SessaoResumo,
  TurnoView,
} from '../types';

/**
 * O trabalho roda no servidor: cria-se o turno, recebe-se um id e faz-se polling
 * incremental por cursor. Fechar a aba NÃO interrompe o turno — ao reabrir,
 * `obterSessao` devolve o histórico completo para reatar.
 */

export async function criarSessao(payload: CriarSessaoPayload = {}) {
  const { data } = await http.post<{ sessionId: string; ticketNumero: number | null; model: string }>(
    '/agente-ia/sessions',
    payload,
  );
  return data;
}

export async function listarSessoes(arquivadas = false) {
  const { data } = await http.get<{ sessions: SessaoResumo[] }>('/agente-ia/sessions', {
    params: { arquivadas },
  });
  return data.sessions;
}

export async function renomearSessao(sessionId: string, title: string) {
  const { data } = await http.patch<{ renamed: boolean }>(`/agente-ia/sessions/${sessionId}`, {
    title,
  });
  return data;
}

export async function restaurarSessao(sessionId: string) {
  const { data } = await http.post<{ unarchived: boolean }>(
    `/agente-ia/sessions/${sessionId}/unarchive`,
  );
  return data;
}

export async function obterSessao(sessionId: string) {
  const { data } = await http.get<SessaoDetalhe>(`/agente-ia/sessions/${sessionId}`);
  return data;
}

export async function arquivarSessao(sessionId: string) {
  const { data } = await http.delete<{ archived: boolean }>(`/agente-ia/sessions/${sessionId}`);
  return data;
}

export async function criarTurno(sessionId: string, prompt: string) {
  const { data } = await http.post<{ turnId: string; sessionId: string; status: string }>(
    `/agente-ia/sessions/${sessionId}/turns`,
    { prompt },
  );
  return data;
}

export async function obterTurno(turnId: string, cursor = 0) {
  const { data } = await http.get<TurnoView>(`/agente-ia/turns/${turnId}`, { params: { cursor } });
  return data;
}

export async function cancelarTurno(turnId: string) {
  const { data } = await http.post<{ cancelled: boolean }>(`/agente-ia/turns/${turnId}/cancel`);
  return data;
}
