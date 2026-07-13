import { http } from '@/shared/api/httpClient';
import type {
  AbaConversas,
  ContatoConversa,
  ConversaListItem,
  IniciarConversaPayload,
  Mensagem,
  TemplateWhatsApp,
} from '@/features/conversas/types';

export async function listarConversas(aba: AbaConversas, busca?: string): Promise<ConversaListItem[]> {
  const { data } = await http.get<ConversaListItem[]>('/conversas', {
    params: { aba, busca: busca?.trim() || undefined },
  });
  return data;
}

export async function obterConversa(id: string): Promise<ConversaListItem> {
  const { data } = await http.get<ConversaListItem>(`/conversas/${id}`);
  return data;
}

export async function obterMensagens(id: string): Promise<Mensagem[]> {
  const { data } = await http.get<Mensagem[]>(`/conversas/${id}/mensagens`);
  return data;
}

export async function listarTemplates(): Promise<TemplateWhatsApp[]> {
  const { data } = await http.get<TemplateWhatsApp[]>('/conversas/templates');
  return data;
}

export async function buscarContatos(termo: string): Promise<ContatoConversa[]> {
  const { data } = await http.get<ContatoConversa[]>('/conversas/contatos', { params: { termo } });
  return data;
}

export async function iniciarConversa(payload: IniciarConversaPayload): Promise<string> {
  const { data } = await http.post<string>('/conversas', payload);
  return data;
}

export async function enviarMensagem(id: string, texto: string): Promise<void> {
  await http.post(`/conversas/${id}/mensagens`, { texto });
}

export async function marcarLida(id: string): Promise<void> {
  await http.post(`/conversas/${id}/lida`, {});
}
