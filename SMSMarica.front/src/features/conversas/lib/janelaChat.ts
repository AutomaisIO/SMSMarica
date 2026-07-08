import { abrirJanelaSolta, janelaSoltaAberta } from '@/shared/lib/janela';

/** Nome fixo da janela — reabrir com o mesmo nome traz a existente para frente. */
export const NOME_JANELA_CHAT = 'smsmarica-chat-janela';

/** Canal de troca de conversa entre a janela principal e a janela do chat. */
export const CANAL_CHAT = 'smsmarica-chat';

/**
 * Abre a Central de Atendimento em JANELA SEPARADA do navegador (ticket #18 — mesma
 * mecânica do visualizador do PACS, via abrirJanelaSolta): minimizada/atrás, o clique
 * traz para frente; fechada, reabre. Com `conversaId`, a janela NOVA seleciona pela
 * URL e a JÁ ABERTA troca via BroadcastChannel (a reabertura por nome não navega).
 */
export function abrirJanelaChat(conversaId?: string) {
  const url = conversaId ? `/chat/janela?conversa=${conversaId}` : '/chat/janela';
  abrirJanelaSolta(url, NOME_JANELA_CHAT, 1100, 720);
  if (conversaId && 'BroadcastChannel' in window) {
    const canal = new BroadcastChannel(CANAL_CHAT);
    canal.postMessage({ tipo: 'abrir-conversa', id: conversaId });
    canal.close();
  }
}

/** True quando o código roda DENTRO da janela do chat (decide trocar in-place vs. focar). */
export function ehJanelaChat(): boolean {
  return window.name === NOME_JANELA_CHAT;
}

/** A janela do chat (aberta a partir DESTA janela) está aberta? Suprime bip/alerta duplicado. */
export function janelaChatAberta(): boolean {
  return janelaSoltaAberta(NOME_JANELA_CHAT);
}
