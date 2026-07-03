import { http } from '@/shared/api/httpClient';
import type {
  AbrirTicketPayload,
  AnexoRef,
  AtualizarGestaoPayload,
  ComentarPayload,
  Ticket,
  TicketConfiguracao,
  TicketListItem,
  TicketVisibilidade,
} from '@/features/tickets/types';

// ---- Self-service ----

export async function listarMeusTickets(incluirArquivados: boolean): Promise<TicketListItem[]> {
  const { data } = await http.get<TicketListItem[]>('/tickets', { params: { incluirArquivados } });
  return data;
}

export async function obterTicket(id: string): Promise<Ticket> {
  const { data } = await http.get<Ticket>(`/tickets/${id}`);
  return data;
}

export async function abrirTicket(payload: AbrirTicketPayload): Promise<string> {
  const { data } = await http.post<string>('/tickets', payload);
  return data;
}

export async function comentarTicket(id: string, payload: ComentarPayload): Promise<void> {
  await http.post(`/tickets/${id}/comentarios`, payload);
}

export async function arquivarTicket(id: string, arquivar: boolean): Promise<void> {
  await http.post(`/tickets/${id}/arquivar`, null, { params: { arquivar } });
}

export async function obterConfiguracao(): Promise<TicketConfiguracao> {
  const { data } = await http.get<TicketConfiguracao>('/tickets/configuracao');
  return data;
}

/** Envia um print/imagem e devolve a referência para anexar ao ticket/comentário. */
export async function enviarAnexo(arquivo: File): Promise<AnexoRef> {
  const form = new FormData();
  form.append('arquivo', arquivo);
  const { data } = await http.post<{ id: string; nomeArquivo: string }>('/tickets/anexos', form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return { midiaId: data.id, nomeArquivo: data.nomeArquivo };
}

// ---- Gestão ----

export async function listarTodosTickets(incluirArquivados: boolean): Promise<TicketListItem[]> {
  const { data } = await http.get<TicketListItem[]>('/tickets/gestao', { params: { incluirArquivados } });
  return data;
}

export async function obterTicketGestao(id: string): Promise<Ticket> {
  const { data } = await http.get<Ticket>(`/tickets/gestao/${id}`);
  return data;
}

export async function comentarTicketGestao(id: string, payload: ComentarPayload): Promise<void> {
  await http.post(`/tickets/gestao/${id}/comentarios`, payload);
}

export async function atualizarGestao(id: string, payload: AtualizarGestaoPayload): Promise<void> {
  await http.put(`/tickets/gestao/${id}`, payload);
}

export async function arquivarGestao(id: string, arquivar: boolean): Promise<void> {
  await http.post(`/tickets/gestao/${id}/arquivar`, null, { params: { arquivar } });
}

export async function excluirTicket(id: string): Promise<void> {
  await http.delete(`/tickets/gestao/${id}`);
}

export async function atualizarVisibilidade(visibilidade: TicketVisibilidade): Promise<void> {
  await http.put('/tickets/gestao/configuracao', { visibilidade });
}
