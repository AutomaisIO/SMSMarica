import { http } from '@/shared/api/httpClient';
import type {
  NotificacaoDetalhe,
  NotificacaoFiltro,
  PaginaNotificacoes,
} from '@/features/notificacoes-agendamento/types';

export async function listarNotificacoes(filtro: NotificacaoFiltro): Promise<PaginaNotificacoes> {
  const params: Record<string, string | number> = {};
  for (const [k, v] of Object.entries(filtro)) {
    if (v !== undefined && v !== null && v !== '') params[k] = v as string | number;
  }
  const { data } = await http.get<PaginaNotificacoes>('/comunicacoes-paciente', { params });
  return data;
}

export async function obterNotificacao(id: string): Promise<NotificacaoDetalhe> {
  const { data } = await http.get<NotificacaoDetalhe>(`/comunicacoes-paciente/${id}`);
  return data;
}

export async function reenviarNotificacao(id: string): Promise<void> {
  await http.post(`/comunicacoes-paciente/${id}/reenviar`);
}
