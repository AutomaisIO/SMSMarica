import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Header } from '@/app/layout/Header';
import { Sidebar } from '@/app/layout/Sidebar';

export function Layout() {
  const [colapsado, setColapsado] = useState(false);
  const [mobileAberto, setMobileAberto] = useState(false);

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
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
