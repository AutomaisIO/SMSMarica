import { useState } from 'react';
import { Bell, BellOff, MessageCircle, Minus, Plus, X } from 'lucide-react';
import { useTemConsulta } from '@/shared/auth/authStore';
import { useChat } from '@/features/conversas/store/chatStore';
import { useChatHub } from '@/features/conversas/hooks/useChatHub';
import { useNotificacoesNavegador } from '@/features/conversas/hooks/useNotificacoesNavegador';
import { ListaConversas } from '@/features/conversas/components/ListaConversas';
import { ThreadMensagens } from '@/features/conversas/components/ThreadMensagens';
import { NovaConversaDialog } from '@/features/conversas/components/NovaConversaDialog';

/**
 * Widget flutuante global do chat (montado no Layout autenticado). Mostra uma bolha com o
 * total de não-lidas quando fechado; ao abrir, exibe a lista + a thread. Ativa o hub SignalR
 * e os alertas do navegador. Só aparece para quem tem o módulo Conversas.
 */
export function ChatWidget() {
  const podeVer = useTemConsulta('Conversas');
  const podeSupervisao = useTemConsulta('ConversasSupervisao');
  const widget = useChat((s) => s.widget);
  const conversaAtivaId = useChat((s) => s.conversaAtivaId);
  const totalNaoLidas = useChat((s) => s.totalNaoLidas);
  const alertasAtivos = useChat((s) => s.alertasAtivos);
  const { abrir, minimizar, fechar, setConversaAtiva } = useChat.getState();
  const { permissao, solicitar } = useNotificacoesNavegador();
  const [novaAberta, setNovaAberta] = useState(false);

  // Hub sempre ativo enquanto o operador estiver logado (mesmo com o painel fechado),
  // para alimentar o badge e as notificações. Deve rodar antes de qualquer early-return.
  useChatHub(podeVer);

  if (!podeVer) return null;

  async function alternarAlertas() {
    if (alertasAtivos) {
      useChat.getState().setAlertas(false);
      return;
    }
    const r = permissao === 'granted' ? 'granted' : await solicitar();
    useChat.getState().setAlertas(r === 'granted');
  }

  if (widget !== 'aberto') {
    return (
      <button
        type="button"
        onClick={abrir}
        className="fixed bottom-4 right-4 z-40 flex h-14 w-14 items-center justify-center rounded-full bg-primary-600 text-white shadow-lg hover:bg-primary-700"
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

  return (
    <>
      <div className="fixed bottom-4 right-4 z-40 flex h-[min(80vh,560px)] w-[min(94vw,760px)] flex-col overflow-hidden rounded-lg border border-gray-200 bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-200 bg-primary-600 px-3 py-2 text-white">
          <span className="text-sm font-semibold">Central de Atendimento</span>
          <div className="flex items-center gap-1">
            <button
              type="button"
              onClick={() => setNovaAberta(true)}
              className="flex items-center gap-1 rounded px-2 py-1 text-xs hover:bg-white/15"
            >
              <Plus className="h-3.5 w-3.5" /> Nova
            </button>
            <button
              type="button"
              onClick={() => void alternarAlertas()}
              className="rounded p-1.5 hover:bg-white/15"
              title={alertasAtivos ? 'Alertas ativados' : 'Ativar alertas do navegador'}
              aria-label="Alertas"
            >
              {alertasAtivos ? <Bell className="h-4 w-4" /> : <BellOff className="h-4 w-4" />}
            </button>
            <button type="button" onClick={minimizar} className="rounded p-1.5 hover:bg-white/15" aria-label="Minimizar">
              <Minus className="h-4 w-4" />
            </button>
            <button type="button" onClick={fechar} className="rounded p-1.5 hover:bg-white/15" aria-label="Fechar">
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>

        <div className="flex min-h-0 flex-1">
          <div className="w-64 shrink-0 border-r border-gray-200">
            <ListaConversas
              conversaAtivaId={conversaAtivaId}
              onSelecionar={setConversaAtiva}
              podeSupervisao={podeSupervisao}
              alimentarTotalGlobal
            />
          </div>
          <div className="min-w-0 flex-1">
            {conversaAtivaId ? (
              <ThreadMensagens conversaId={conversaAtivaId} />
            ) : (
              <div className="flex h-full items-center justify-center p-6 text-center text-sm text-gray-400">
                Selecione uma conversa ou clique em <b className="mx-1">Nova</b> para iniciar.
              </div>
            )}
          </div>
        </div>
      </div>

      {novaAberta && (
        <NovaConversaDialog
          onFechar={() => setNovaAberta(false)}
          onCriada={(id) => {
            setNovaAberta(false);
            setConversaAtiva(id);
          }}
        />
      )}
    </>
  );
}
