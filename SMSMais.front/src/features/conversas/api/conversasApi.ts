import { http } from '@/shared/api/httpClient';
import type {
  AbaConversas,
  AtendenteElegivel,
  ContatoConversa,
  ConversaListItem,
  IniciarConversaPayload,
  Mensagem,
  PacienteDoTelefone,
  ResumoConversas,
  TemplateWhatsApp,
  UnidadeDestino,
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

export async function listarPacientesDoTelefone(id: string): Promise<PacienteDoTelefone[]> {
  const { data } = await http.get<PacienteDoTelefone[]>(`/conversas/${id}/pacientes`);
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

/** Claim: o operador vira o responsável e a conversa sai da fila para a lista pessoal dele. */
export async function assumirConversa(id: string): Promise<void> {
  await http.post(`/conversas/${id}/assumir`, {});
}

/** Devolve à fila da unidade (limpa o responsável). */
export async function devolverConversa(id: string): Promise<void> {
  await http.post(`/conversas/${id}/devolver`, {});
}

export async function encaminharConversa(
  id: string,
  payload: { paraUsuarioId: string; observacao?: string | null },
): Promise<void> {
  await http.post(`/conversas/${id}/encaminhar`, payload);
}

/** Devolve a conversa ao robô ("Atendente Virtual"): volta à fila e o robô retoma. */
export async function encaminharConversaParaRobo(id: string): Promise<void> {
  await http.post(`/conversas/${id}/encaminhar-robo`, {});
}

/** Para o robô nesta conversa (bloqueio forte) e assume para o operador corrigir. */
export async function pararRoboConversa(id: string): Promise<void> {
  await http.post(`/conversas/${id}/parar-robo`, {});
}

/** Marca uma resposta do robô como errada (para treinamento). */
export async function marcarRoboErro(
  id: string,
  body: { mensagemWhatsAppId?: string | null; nota?: string | null },
): Promise<void> {
  await http.post(`/conversas/${id}/robo-erro`, body);
}

/**
 * Crítica a uma resposta do robô, a partir da própria bolha: abre (ou reaproveita) um item de
 * treinamento e devolve o id — quem tem o módulo Robô consegue abrir e mandar treinar.
 */
export async function abrirTreinamentoRobo(
  id: string,
  body: { mensagemWhatsAppId?: string | null; critica: string },
): Promise<{ itemId: string }> {
  const { data } = await http.post<{ itemId: string }>(`/conversas/${id}/robo-treinamento`, body);
  return data;
}

export async function transferirConversa(
  id: string,
  payload: { paraUnidadeId: string; observacao?: string | null },
): Promise<void> {
  await http.post(`/conversas/${id}/transferir`, payload);
}

export async function listarAtendentesElegiveis(id: string): Promise<AtendenteElegivel[]> {
  const { data } = await http.get<AtendenteElegivel[]>(`/conversas/${id}/atendentes-elegiveis`);
  return data;
}

export async function listarUnidadesDestino(): Promise<UnidadeDestino[]> {
  const { data } = await http.get<UnidadeDestino[]>('/conversas/unidades-destino');
  return data;
}

export type SituacaoContato = {
  telefoneCanonical: string | null;
  conversaId: string | null;
  janelaExpiraEm: string | null;
  podeTextoLivre: boolean;
  contatoNegado: boolean;
};

/** Há conversa viva com janela de 24h aberta para este paciente/telefone? (não cria nada) */
export async function obterSituacaoContato(pacienteId?: string, telefone?: string): Promise<SituacaoContato> {
  const { data } = await http.get<SituacaoContato>('/conversas/situacao', {
    params: { pacienteId: pacienteId || undefined, telefone: telefone || undefined },
  });
  return data;
}

export async function obterResumoConversas(): Promise<ResumoConversas> {
  const { data } = await http.get<ResumoConversas>('/conversas/resumo');
  return data;
}
