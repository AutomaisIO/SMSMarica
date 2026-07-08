import { useEffect } from 'react';
import { MessageCircle } from 'lucide-react';
import { useTemConsulta } from '@/shared/auth/authStore';
import { useChat } from '@/features/conversas/store/chatStore';
import { useChatHub } from '@/features/conversas/hooks/useChatHub';
import { useNotificacoesNavegador } from '@/features/conversas/hooks/useNotificacoesNavegador';
import { abrirJanelaChat } from '@/features/conversas/lib/janelaChat';

/**
 * Bolha global do chat (montada no Layout autenticado): badge com o total de não-lidas;
 * o clique abre/foca a Central de Atendimento em JANELA SEPARADA do navegador
 * (ticket #18 — mesma mecânica das imagens do PACS; minimizar/fechar são os controles
 * nativos da janela). Mantém o hub SignalR ativo para alimentar badge e notificações
 * mesmo com a janela do chat fechada.
 */
export function ChatWidget() {
  const podeVer = useTemConsulta('Conversas');
  const totalNaoLidas = useChat((s) => s.totalNaoLidas);
  const { permissao } = useNotificacoesNavegador();

  // Hub sempre ativo enquanto o operador estiver logado. Deve rodar antes do early-return.
  useChatHub(podeVer);

  // Alertas do navegador seguem a permissão já concedida (o pedido/toggle vive na janela
  // do chat) — o clique na notificação abre/foca a janela na conversa certa.
  useEffect(() => {
    if (permissao === 'granted') useChat.getState().setAlertas(true);
  }, [permissao]);

  if (!podeVer) return null;

  return (
    <button
      type="button"
      onClick={() => abrirJanelaChat()}
      className="fixed bottom-4 right-4 z-40 flex h-14 w-14 items-center justify-center rounded-full bg-primary-600 text-white shadow-lg hover:bg-primary-700"
      title="Abrir a Central de Atendimento (janela separada)"
      aria-label="Abrir chat"
    >
      <MessageCircle className="h-6 w-6" />
      {totalNaoLidas > 0 && (
        <span className="absolute -right-0.5 -top-0.5 inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-amber-500 px-1 text-[11px] font-bold text-white">
          {totalNaoLidas > 99 ? '99+' : totalNaoLidas}
        </span>
      )}
    </button>
  );
}
