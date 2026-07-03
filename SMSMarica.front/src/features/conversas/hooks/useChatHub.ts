import { useEffect, useRef } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { apiBaseAbsoluto } from '@/shared/api/httpClient';
import { obterToken, useAuth } from '@/shared/auth/authStore';
import { useChat } from '@/features/conversas/store/chatStore';
import { useNotificacoesNavegador } from '@/features/conversas/hooks/useNotificacoesNavegador';
import type { ConversaEventoRealtime } from '@/features/conversas/types';

let audioCtx: AudioContext | null = null;
function tocarBip() {
  try {
    const Ctx = window.AudioContext
      ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return;
    audioCtx ??= new Ctx();
    const osc = audioCtx.createOscillator();
    const gain = audioCtx.createGain();
    osc.connect(gain);
    gain.connect(audioCtx.destination);
    osc.frequency.value = 880;
    gain.gain.value = 0.05;
    osc.start();
    osc.stop(audioCtx.currentTime + 0.15);
  } catch {
    /* som é best-effort */
  }
}

/**
 * Conecta ao ConversasHub (SignalR) e traduz os eventos server→client em invalidações do
 * react-query + alerta/som quando o operador não está olhando a conversa. O socket é apenas o
 * "invalidador"; a fonte de verdade continua sendo o react-query (com poll de fallback).
 */
export function useChatHub() {
  const queryClient = useQueryClient();
  const token = useAuth((s) => s.token);
  const total = useChat((s) => s.totalNaoLidas);
  const { notificar } = useNotificacoesNavegador();

  const notificarRef = useRef(notificar);
  useEffect(() => { notificarRef.current = notificar; }, [notificar]);

  // Conexão (uma vez por sessão autenticada).
  useEffect(() => {
    if (!token) return;
    const base = apiBaseAbsoluto.replace(/\/api$/, '');
    const conn = new HubConnectionBuilder()
      .withUrl(`${base}/hubs/conversas`, { accessTokenFactory: () => obterToken() ?? '' })
      .withAutomaticReconnect()
      .build();

    const invalidarLista = () => queryClient.invalidateQueries({ queryKey: ['conversas', 'lista'] });

    conn.on('mensagemRecebida', (evt: ConversaEventoRealtime) => {
      invalidarLista();
      queryClient.invalidateQueries({ queryKey: ['conversas', 'mensagens', evt.conversaId] });

      const st = useChat.getState();
      const olhando = st.widget === 'aberto' && st.conversaAtivaId === evt.conversaId && !document.hidden;
      if (olhando) return;

      tocarBip();
      if (st.alertasAtivos) {
        notificarRef.current(
          evt.nomeContato || evt.telefoneCanonical,
          evt.preview || 'Nova mensagem',
          `conversa:${evt.conversaId}`,
          () => useChat.getState().abrirConversa(evt.conversaId),
        );
      }
    });

    conn.on('mensagemEnviada', (evt: ConversaEventoRealtime) => {
      invalidarLista();
      queryClient.invalidateQueries({ queryKey: ['conversas', 'mensagens', evt.conversaId] });
    });

    conn.on('conversaAtualizada', () => invalidarLista());
    conn.onreconnected(() => queryClient.invalidateQueries({ queryKey: ['conversas'] }));

    conn.start().catch(() => {
      /* Se o socket falhar, o refetchInterval das queries mantém a tela viva. */
    });

    return () => {
      conn.stop().catch(() => {});
    };
  }, [token, queryClient]);

  // Título da aba piscando com o contador quando há não-lidas e a aba/chat não está em foco.
  useEffect(() => {
    const base = document.title.replace(/^\(\d+\)\s*/, '');
    function aplicar() {
      const foraDeFoco = document.hidden || useChat.getState().widget !== 'aberto';
      document.title = total > 0 && foraDeFoco ? `(${total}) ${base}` : base;
    }
    aplicar();
    document.addEventListener('visibilitychange', aplicar);
    window.addEventListener('focus', aplicar);
    return () => {
      document.removeEventListener('visibilitychange', aplicar);
      window.removeEventListener('focus', aplicar);
      document.title = base;
    };
  }, [total]);
}
