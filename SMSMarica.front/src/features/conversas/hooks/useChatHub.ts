import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { apiBaseAbsoluto } from '@/shared/api/httpClient';
import { obterToken, useAuth } from '@/shared/auth/authStore';
import { useChat } from '@/features/conversas/store/chatStore';
import { useNotificacoesNavegador } from '@/features/conversas/hooks/useNotificacoesNavegador';
import { abrirJanelaChat, ehJanelaChat, janelaChatAberta } from '@/features/conversas/lib/janelaChat';
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

// --- Assinatura da thread aberta (grupo conversa:{id} no hub) --------------------------------
// A conexão vive no useChatHub (montado uma vez no ChatWidget), mas quem sabe qual thread está
// aberta é o ThreadMensagens (widget E página). Registro module-level: o componente declara a
// conversa que está olhando e o hook (re)assina no servidor, inclusive após reconexão — grupos
// SignalR são por conexão e se perdem quando o socket cai.
let connAtual: HubConnection | null = null;
const conversasAssinadas = new Set<string>();

function invocarSeguro(metodo: 'AssinarConversa' | 'DesassinarConversa', conversaId: string) {
  if (connAtual?.state === HubConnectionState.Connected) {
    connAtual.invoke(metodo, conversaId).catch(() => {
      /* melhor esforço — o poll de fallback cobre */
    });
  }
}

export function assinarConversaRealtime(conversaId: string) {
  conversasAssinadas.add(conversaId);
  invocarSeguro('AssinarConversa', conversaId);
}

export function desassinarConversaRealtime(conversaId: string) {
  conversasAssinadas.delete(conversaId);
  invocarSeguro('DesassinarConversa', conversaId);
}

/** Hook para o ThreadMensagens: assina a conversa aberta enquanto o componente viver. */
export function useAssinaturaConversa(conversaId: string | null) {
  useEffect(() => {
    if (!conversaId) return;
    assinarConversaRealtime(conversaId);
    return () => desassinarConversaRealtime(conversaId);
  }, [conversaId]);
}

/**
 * Conecta ao ConversasHub (SignalR) e traduz os eventos server→client em invalidações do
 * react-query + alerta/som quando o operador não está olhando a conversa. O socket é apenas o
 * "invalidador"; a fonte de verdade continua sendo o react-query (com poll de fallback).
 * `habilitado` = usuário tem o módulo Conversas (sem ele, nem conecta).
 */
export function useChatHub(habilitado: boolean) {
  const queryClient = useQueryClient();
  const token = useAuth((s) => s.token);
  const total = useChat((s) => s.totalNaoLidas);
  const { notificar } = useNotificacoesNavegador();

  const notificarRef = useRef(notificar);
  useEffect(() => { notificarRef.current = notificar; }, [notificar]);

  // Conexão (uma vez por sessão autenticada).
  useEffect(() => {
    if (!token || !habilitado) return;
    const base = apiBaseAbsoluto.replace(/\/api$/, '');
    const conn = new HubConnectionBuilder()
      // withCredentials:false — a autenticação é o JWT (accessTokenFactory → header no
      // negotiate, access_token na query no WebSocket), nunca cookie. O default (true)
      // manda credentials:include e o browser rejeita o `Access-Control-Allow-Origin: *`
      // do CORS do backend (negotiate bloqueado em produção).
      .withUrl(`${base}/hubs/conversas`, {
        accessTokenFactory: () => obterToken() ?? '',
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .build();
    connAtual = conn;

    const invalidarLista = () => queryClient.invalidateQueries({ queryKey: ['conversas', 'lista'] });
    const invalidarConversa = (evt: ConversaEventoRealtime) => {
      invalidarLista();
      queryClient.invalidateQueries({ queryKey: ['conversas', 'mensagens', evt.conversaId] });
      queryClient.invalidateQueries({ queryKey: ['conversas', 'detalhe', evt.conversaId] });
    };

    // Uma conexão pode estar em mais de um grupo alvo do evento (unidade + supervisão +
    // conversa aberta) e o SignalR entrega uma cópia por grupo — dedup para não bipar 2×.
    let ultimoEvento = '';
    const ehDuplicado = (evt: ConversaEventoRealtime) => {
      const chave = `${evt.conversaId}:${evt.ocorridoEm ?? ''}:${evt.naoLidas}`;
      if (chave === ultimoEvento) return true;
      ultimoEvento = chave;
      return false;
    };

    conn.on('mensagemRecebida', (evt: ConversaEventoRealtime) => {
      if (ehDuplicado(evt)) return;
      invalidarConversa(evt);

      const st = useChat.getState();
      const olhando = st.widget === 'aberto' && st.conversaAtivaId === evt.conversaId && !document.hidden;
      if (olhando) return;
      // Com a janela separada do chat aberta, quem bipa/notifica é ELA (evita alerta
      // em dobro — cada janela tem seu próprio hub).
      if (!ehJanelaChat() && janelaChatAberta()) return;

      tocarBip();
      if (st.alertasAtivos) {
        notificarRef.current(
          evt.nomeContato || evt.telefoneCanonical,
          evt.preview || 'Nova mensagem',
          `conversa:${evt.conversaId}`,
          // Na própria janela do chat troca a conversa in-place; na principal
          // abre/foca a janela separada já na conversa (ticket #18).
          () =>
            ehJanelaChat()
              ? useChat.getState().abrirConversa(evt.conversaId)
              : abrirJanelaChat(evt.conversaId),
        );
      }
    });

    conn.on('mensagemEnviada', (evt: ConversaEventoRealtime) => {
      if (ehDuplicado(evt)) return;
      invalidarConversa(evt);
    });

    conn.on('conversaAtualizada', () => invalidarLista());

    const reassinar = () => {
      for (const id of conversasAssinadas) invocarSeguro('AssinarConversa', id);
    };
    conn.onreconnected(() => {
      queryClient.invalidateQueries({ queryKey: ['conversas'] });
      reassinar(); // grupos são por conexão — se perdem na queda do socket
    });

    conn.start().then(reassinar).catch(() => {
      /* Se o socket falhar, o refetchInterval das queries mantém a tela viva. */
    });

    return () => {
      if (connAtual === conn) connAtual = null;
      conn.stop().catch(() => {});
    };
  }, [token, habilitado, queryClient]);

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
