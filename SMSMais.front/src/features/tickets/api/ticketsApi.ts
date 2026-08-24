import { http } from '@/shared/api/httpClient';
import type {
  AbrirTicketPayload,
  AnexoRef,
  AtualizarGestaoPayload,
  ComentarPayload,
  Ticket,
  TicketConfiguracao,
  TicketContexto,
  TicketListItem,
  TicketResumoAutor,
  TicketResumoGestao,
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

/** Resumo do autor: nº de respostas ainda não reconhecidas. */
export async function obterResumoAutor(): Promise<TicketResumoAutor> {
  const { data } = await http.get<TicketResumoAutor>('/tickets/resumo');
  return data;
}

/** Autor reconhece a resposta (baixa a bandeira) sem abrir o ticket. */
export async function reconhecerTicket(id: string): Promise<void> {
  await http.post(`/tickets/${id}/reconhecer`);
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

/** Resumo da gestão (badge do menu + cabeçalho): novos, abertos, em análise. */
export async function obterResumoGestao(): Promise<TicketResumoGestao> {
  const { data } = await http.get<TicketResumoGestao>('/tickets/gestao/resumo');
  return data;
}

export async function obterTicketGestao(id: string): Promise<Ticket> {
  const { data } = await http.get<Ticket>(`/tickets/gestao/${id}`);
  return data;
}

/** Contexto enxuto por número (título/tipo/status) para a faixa do Agente IA. */
export async function obterContextoTicketPorNumero(numero: number): Promise<TicketContexto> {
  const { data } = await http.get<TicketContexto>(`/tickets/gestao/por-numero/${numero}`);
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

/** Registra que o ticket foi encaminhado ao Agente IA (marca "Enviado à IA" na lista da gestão). */
export async function marcarEnviadoIa(id: string): Promise<void> {
  await http.post(`/tickets/gestao/${id}/enviar-ia`);
}

export async function excluirTicket(id: string): Promise<void> {
  await http.delete(`/tickets/gestao/${id}`);
}

export async function atualizarVisibilidade(visibilidade: TicketVisibilidade): Promise<void> {
  await http.put('/tickets/gestao/configuracao', { visibilidade });
}
