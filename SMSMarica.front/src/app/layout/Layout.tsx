import { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Header } from '@/app/layout/Header';
import { MenuContextoBar } from '@/app/layout/MenuContextoBar';
import { Sidebar } from '@/app/layout/Sidebar';
import { useMenuPreferencias } from '@/app/layout/menuPreferencias';
import { obterPreferencias } from '@/shared/auth/preferenciasApi';

export function Layout() {
  const [colapsado, setColapsado] = useState(false);
  const [mobileAberto, setMobileAberto] = useState(false);
  const hidratar = useMenuPreferencias((s) => s.hidratar);

  // Hidrata as preferências do menu a partir do servidor (por usuário) ao entrar.
  // O localStorage já deu o valor imediato; aqui o servidor passa a ser a verdade.
  useEffect(() => {
    let ativo = true;
    obterPreferencias()
      .then((p) => {
        if (ativo) hidratar(p.menuDefaults ?? {});
      })
      .catch(() => {
        /* offline/erro — segue com o cache local. */
      });
    return () => {
      ativo = false;
    };
  }, [hidratar]);

  return (
    <div className="min-h-screen bg-gray-50">
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
    </div>
  );
}
