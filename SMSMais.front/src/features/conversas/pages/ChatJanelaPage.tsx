import { useEffect, useState } from 'react';
import { Bell, BellOff, Plus } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { useTemConsulta } from '@/shared/auth/authStore';
import { useChat } from '@/features/conversas/store/chatStore';
import { useComposerPreferencias } from '@/features/conversas/store/composerPreferencias';
import { obterPreferencias } from '@/shared/auth/preferenciasApi';
import { useChatHub } from '@/features/conversas/hooks/useChatHub';
import { useNotificacoesNavegador } from '@/features/conversas/hooks/useNotificacoesNavegador';
import { useTotalNaoLidas } from '@/features/conversas/hooks/useTotalNaoLidas';
import { ListaConversas } from '@/features/conversas/components/ListaConversas';
import { ThreadMensagens } from '@/features/conversas/components/ThreadMensagens';
import { NovaConversaDialog } from '@/features/conversas/components/NovaConversaDialog';
import { CANAL_CHAT } from '@/features/conversas/lib/janelaChat';
import { PainelRespostasRapidas } from '@/features/respostas-rapidas/components/PainelRespostasRapidas';

/**
 * Central de Atendimento em JANELA SEPARADA do navegador (ticket #18), aberta por
 * abrirJanelaChat — sem sidebar/header do app; minimizar/fechar são os controles
 * NATIVOS da janela. Mesmo conteúdo do painel: lista + thread + Nova + alertas.
 */
export function ChatJanelaPage() {
  const podeVer = useTemConsulta('Conversas');
  const podeSupervisao = useTemConsulta('ConversasSupervisao');
  const conversaAtivaId = useChat((s) => s.conversaAtivaId);
  const alertasAtivos = useChat((s) => s.alertasAtivos);
  const { setConversaAtiva } = useChat.getState();
  const { permissao, solicitar } = useNotificacoesNavegador();
  const hidratarComposer = useComposerPreferencias((s) => s.hidratar);
  const [novaAberta, setNovaAberta] = useState(false);
  const [params] = useSearchParams();

  useChatHub(podeVer);
  useTotalNaoLidas(podeVer); // título da aba desta janela usa o mesmo total do resumo

  // A janela solta não tem o Layout, que é quem hidrata as preferências do servidor.
  // Sem isto, a altura da caixa (e o "Enviar com Enter") não acompanham o usuário ao
  // (re)abrir esta janela — voltavam ao padrão. Servidor é a fonte da verdade.
  useEffect(() => {
    let ativo = true;
    obterPreferencias()
      .then((p) => {
        if (ativo) hidratarComposer({ altura: p.alturaComposerChat, enviarComEnter: p.enviarComEnter });
      })
      .catch(() => {
        /* offline/erro — segue com o cache local. */
      });
    return () => {
      ativo = false;
    };
  }, [hidratarComposer]);

  // Esta janela É o chat: marca 'aberto' no store DESTE contexto (a conversa em tela
  // não bipa/notifica) e seleciona a conversa pedida na URL da abertura.
  useEffect(() => {
    document.title = 'Central de Atendimento — SMS Maricá';
    useChat.getState().abrir();
    const inicial = params.get('conversa');
    if (inicial) useChat.getState().setConversaAtiva(inicial);
    // Intencional: só na montagem — a troca posterior vem pelo BroadcastChannel.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Troca de conversa pedida pela janela principal (clique em notificação/atalho).
  useEffect(() => {
    if (!('BroadcastChannel' in window)) return;
    const canal = new BroadcastChannel(CANAL_CHAT);
    canal.onmessage = (e) => {
      if (e.data?.tipo === 'abrir-conversa' && typeof e.data.id === 'string') {
        useChat.getState().setConversaAtiva(e.data.id);
      }
    };
    return () => canal.close();
  }, []);

  async function alternarAlertas() {
    if (alertasAtivos) {
      useChat.getState().setAlertas(false);
      return;
    }
    const r = permissao === 'granted' ? 'granted' : await solicitar();
    useChat.getState().setAlertas(r === 'granted');
  }

  if (!podeVer) {
    return (
      <div className="flex h-screen items-center justify-center text-sm text-gray-500">
        Sem acesso à Central de Atendimento.
      </div>
    );
  }

  return (
    <div className="flex h-screen flex-col overflow-hidden bg-white">
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
        </div>
      </div>

      <div className="flex min-h-0 flex-1">
        <div className="w-64 shrink-0 border-r border-gray-200">
          <ListaConversas
            conversaAtivaId={conversaAtivaId}
            onSelecionar={setConversaAtiva}
            podeSupervisao={podeSupervisao}
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

        {/* Atalhos de mensagens prontas — só fazem sentido com uma conversa aberta. */}
        {conversaAtivaId ? (
          <div className="w-60 shrink-0 border-l border-gray-200">
            <PainelRespostasRapidas conversaId={conversaAtivaId} />
          </div>
        ) : null}
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
    </div>
  );
}
