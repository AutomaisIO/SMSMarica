import { http } from '@/shared/api/httpClient';
import type {
  ContextoComunicacaoInterna,
  ConversaInternaDetalhe,
  ConversaInternaItem,
  MensagemInterna,
  PaginaMensagensInternas,
  ResultadoBuscaInterna,
  ResumoComunicacaoInterna,
  UnidadeInterna,
  UsuarioInterno,
} from '@/features/comunicacao-interna/types';

const BASE = '/comunicacao-interna';

/** Teto do anexo (o backend recusa acima disso — ADR-0079 §4). */
export const TETO_ANEXO_BYTES = 25 * 1024 * 1024;

export async function obterContexto(): Promise<ContextoComunicacaoInterna> {
  const { data } = await http.get<ContextoComunicacaoInterna>(`${BASE}/contexto`);
  return data;
}

export async function obterResumo(): Promise<ResumoComunicacaoInterna> {
  const { data } = await http.get<ResumoComunicacaoInterna>(`${BASE}/resumo`);
  return data;
}

export async function listarConversas(): Promise<ConversaInternaItem[]> {
  const { data } = await http.get<ConversaInternaItem[]>(`${BASE}/conversas`);
  return data;
}

export async function obterConversa(id: string): Promise<ConversaInternaDetalhe> {
  const { data } = await http.get<ConversaInternaDetalhe>(`${BASE}/conversas/${id}`);
  return data;
}

export async function abrirConversaUnidade(
  unidadeDestinoId: string,
  unidadeOrigemId: string | null,
): Promise<ConversaInternaItem> {
  const { data } = await http.post<ConversaInternaItem>(`${BASE}/conversas/unidade`, {
    unidadeDestinoId,
    unidadeOrigemId,
  });
  return data;
}

export async function abrirConversaDireta(usuarioId: string): Promise<ConversaInternaItem> {
  const { data } = await http.post<ConversaInternaItem>(`${BASE}/conversas/direta`, { usuarioId });
  return data;
}

export async function listarMensagens(
  conversaId: string,
  antesDe?: string,
  limite = 50,
): Promise<PaginaMensagensInternas> {
  const { data } = await http.get<PaginaMensagensInternas>(`${BASE}/conversas/${conversaId}/mensagens`, {
    params: { limite, ...(antesDe ? { antesDe } : {}) },
  });
  return data;
}

export type NovaMensagemInterna = {
  conversaId: string;
  texto?: string;
  arquivo?: File;
  /** Áudio gravado no navegador: vai com `ehAudio=true` e a duração em segundos. */
  audio?: { duracaoSegundos: number };
};

export async function enviarMensagem(m: NovaMensagemInterna): Promise<MensagemInterna> {
  const form = new FormData();
  if (m.texto) form.append('texto', m.texto);
  if (m.arquivo) {
    form.append('arquivo', m.arquivo, m.arquivo.name);
    // Diagnóstico: o servidor compara com o que recebeu e registra se não bater (áudio de 23/10/2026).
    form.append('tamanhoBytes', String(m.arquivo.size));
  }
  if (m.audio) {
    form.append('ehAudio', 'true');
    form.append('duracaoSegundos', String(m.audio.duracaoSegundos));
  }
  const { data } = await http.post<MensagemInterna>(`${BASE}/conversas/${m.conversaId}/mensagens`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data;
}

/** Transcreve o áudio (ElevenLabs, a mesma dos áudios do WhatsApp); se alguém já pediu, vem o guardado. */
export async function transcreverMensagem(mensagemId: string): Promise<MensagemInterna> {
  const { data } = await http.post<MensagemInterna>(`${BASE}/mensagens/${mensagemId}/transcricao`);
  return data;
}

export async function marcarLida(conversaId: string): Promise<void> {
  await http.post(`${BASE}/conversas/${conversaId}/lida`);
}

export async function silenciarConversa(conversaId: string, silenciada: boolean): Promise<void> {
  await http.put(`${BASE}/conversas/${conversaId}/silenciar`, { silenciada });
}

/** Caminho do conteúdo do anexo — baixar com `baixarArquivo` (exige o token; vira blob URL). */
export function caminhoAnexo(mensagemId: string): string {
  return `${BASE}/mensagens/${mensagemId}/anexo`;
}

export async function listarUnidades(): Promise<UnidadeInterna[]> {
  const { data } = await http.get<UnidadeInterna[]>(`${BASE}/unidades`);
  return data;
}

export async function listarUsuariosDaUnidade(unidadeId: string): Promise<UsuarioInterno[]> {
  const { data } = await http.get<UsuarioInterno[]>(`${BASE}/unidades/${unidadeId}/usuarios`);
  return data;
}

export async function buscar(q: string): Promise<ResultadoBuscaInterna[]> {
  const { data } = await http.get<ResultadoBuscaInterna[]>(`${BASE}/busca`, { params: { q } });
  return data;
}
