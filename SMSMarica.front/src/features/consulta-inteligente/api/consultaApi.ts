import { http } from '@/shared/api/httpClient';
import type { FonteConsulta, SessaoDetalhe, SessaoResumo, TurnoView } from '../types';

/**
 * Chat de dados: o trabalho roda no servidor (motor local por assinatura), num sandbox
 * restrito a consultar a base. Cria-se o turno, recebe-se um id e faz-se polling incremental
 * por cursor. Cada sessão é presa a UMA base (fonte) e é do próprio usuário.
 */

export async function listarFontes(): Promise<FonteConsulta[]> {
  const { data } = await http.get<FonteConsulta[]>('/ia/fontes');
  return data;
}

export async function criarSessao(fonteId: string) {
  const { data } = await http.post<{ sessionId: string; baseSlug: string | null; model: string }>(
    '/ia/chat/sessions',
    { fonteId },
  );
  return data;
}

export async function listarSessoes(arquivadas = false): Promise<SessaoResumo[]> {
  const { data } = await http.get<{ sessions: SessaoResumo[] }>('/ia/chat/sessions', {
    params: { arquivadas },
  });
  return data.sessions;
}

export async function obterSessao(sessionId: string) {
  const { data } = await http.get<SessaoDetalhe>(`/ia/chat/sessions/${sessionId}`);
  return data;
}

export async function arquivarSessao(sessionId: string) {
  const { data } = await http.delete<{ archived: boolean }>(`/ia/chat/sessions/${sessionId}`);
  return data;
}

export async function criarTurno(
  sessionId: string,
  fonteId: string,
  prompt: string,
  modoDev: boolean,
) {
  const { data } = await http.post<{ turnId: string; sessionId: string; status: string }>(
    `/ia/chat/sessions/${sessionId}/turns`,
    { fonteId, prompt, modoDev },
  );
  return data;
}

export async function obterTurno(turnId: string, cursor = 0) {
  const { data } = await http.get<TurnoView>(`/ia/chat/turns/${turnId}`, { params: { cursor } });
  return data;
}

export async function cancelarTurno(turnId: string) {
  const { data } = await http.post<{ cancelled: boolean }>(`/ia/chat/turns/${turnId}/cancel`);
  return data;
}

/** Avaliação de uma resposta (👍/👎). Um 👎 vai para Melhorias de IA. */
export async function enviarFeedback(payload: {
  fonteId: string;
  pergunta: string;
  resposta: string;
  util: boolean;
  comentario?: string;
}): Promise<void> {
  await http.post('/ia/chat/feedback', payload);
}
