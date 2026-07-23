import { useEffect, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { RefreshCw } from 'lucide-react';
import { Header } from '@/app/layout/Header';
import { MenuContextoBar } from '@/app/layout/MenuContextoBar';
import { Sidebar } from '@/app/layout/Sidebar';
import { useMenuPreferencias } from '@/app/layout/menuPreferencias';
import { useComposerPreferencias } from '@/features/conversas/store/composerPreferencias';
import { obterPreferencias } from '@/shared/auth/preferenciasApi';
import { useVersaoApp } from '@/shared/hooks/useVersaoApp';
import { CANAL_NAVEGACAO } from '@/shared/lib/janela';
import { ChatWidget } from '@/features/conversas/components/ChatWidget';

export function Layout() {
  const [colapsado, setColapsado] = useState(false);
  const [mobileAberto, setMobileAberto] = useState(false);
  const hidratar = useMenuPreferencias((s) => s.hidratar);
  const hidratarComposer = useComposerPreferencias((s) => s.hidratar);
  const { novaVersao, atualizar } = useVersaoApp();
  const { pathname } = useLocation();
  const navigate = useNavigate();

  // Navegação pedida por uma JANELA SOLTA (chat/PACS): o clique lá (ex.: última
  // solicitação no resumo do paciente) navega AQUI, na janela principal. Só rotas
  // internas de /app — o canal é same-origin, mas a checagem evita rota arbitrária.
  useEffect(() => {
    if (!('BroadcastChannel' in window)) return;
    const canal = new BroadcastChannel(CANAL_NAVEGACAO);
    canal.onmessage = (e) => {
      if (e.data?.tipo === 'navegar' && typeof e.data.rota === 'string' && e.data.rota.startsWith('/app')) {
        navigate(e.data.rota);
        window.focus(); // best-effort: nem todo browser deixa levantar a janela sem gesto
      }
    };
    return () => canal.close();
  }, [navigate]);

  // Com versão nova pendente, a troca de rota é um momento seguro para atualizar:
  // a navegação já descarta o estado da tela anterior, então o reload é transparente.
  useEffect(() => {
    if (novaVersao) window.location.reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname]);

  // Hidrata as preferências do menu a partir do servidor (por usuário) ao entrar.
  // O localStorage já deu o valor imediato; aqui o servidor passa a ser a verdade.
  useEffect(() => {
    let ativo = true;
    obterPreferencias()
      .then((p) => {
        if (!ativo) return;
        hidratar(p.menuDefaults ?? {});
        hidratarComposer({ altura: p.alturaComposerChat, enviarComEnter: p.enviarComEnter });
      })
      .catch(() => {
        /* offline/erro — segue com o cache local. */
      });
    return () => {
      ativo = false;
    };
  }, [hidratar, hidratarComposer]);

  return (
    <div className="min-h-screen bg-gray-50">
      {novaVersao && (
        <div className="fixed inset-x-0 top-0 z-[60] flex items-center justify-center gap-3 bg-primary-700 px-4 py-2 text-sm text-white shadow-md">
          <span>Nova versão do sistema disponível.</span>
          <button
            type="button"
            onClick={atualizar}
            className="flex items-center gap-1.5 rounded-md bg-white px-3 py-1 text-xs font-semibold text-primary-700 hover:bg-primary-50"
          >
            <RefreshCw className="h-3.5 w-3.5" /> Atualizar agora
          </button>
        </div>
      )}

      <Sidebar
        isCollapsed={colapsado}
        onToggleCollapsed={() => setColapsado((v) => !v)}
        isMobileOpen={mobileAberto}
        onCloseMobile={() => setMobileAberto(false)}
      />

      <div className={colapsado ? 'lg:pl-20' : 'lg:pl-72'}>
        <Header onToggleMobileSidebar={() => setMobileAberto(true)} />
        <main className="px-4 py-8 sm:px-6 lg:px-8">
          <div className="mx-auto max-w-7xl">
            <MenuContextoBar />
            <Outlet />
          </div>
        </main>
      </div>

      {/* Chat flutuante global (só aparece para quem tem o módulo Conversas). */}
      <ChatWidget />
    </div>
  );
}
