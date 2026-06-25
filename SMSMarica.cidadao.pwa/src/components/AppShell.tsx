import { useEffect, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  CalendarHeart,
  FileText,
  FlaskConical,
  Home,
  LogOut,
  Menu,
  Stethoscope,
  User,
  X,
} from 'lucide-react';
import { cn } from '@/lib/cn';
import { api } from '@/lib/api';
import { useAuth } from '@/store/auth';
import { usePerfil } from '@/store/perfil';
import { Avatar } from '@/components/ui';

const NAV = [
  { to: '/', label: 'Início', icon: Home, end: true },
  { to: '/atendimentos', label: 'Meus atendimentos', icon: Stethoscope },
  { to: '/exames', label: 'Exames', icon: FlaskConical },
  { to: '/laudos', label: 'Laudos', icon: FileText },
  { to: '/transporte', label: 'Transporte (TFD)', icon: CalendarHeart },
  { to: '/perfil', label: 'Meu perfil', icon: User },
];

export function AppShell() {
  const [aberto, setAberto] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const sair = useAuth((s) => s.sair);
  const sessao = useAuth((s) => s.paciente);
  const perfil = usePerfil((s) => s.perfil);
  const carregar = usePerfil((s) => s.carregar);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  // Fecha o menu ao trocar de rota.
  useEffect(() => setAberto(false), [location.pathname]);

  // Reseta o scroll para o topo a cada navegação — sem isso o iOS restaura a
  // posição anterior e o conteúdo do topo (ex.: banner de instalar) nasce escondido.
  useEffect(() => {
    window.scrollTo(0, 0);
  }, [location.pathname]);

  const nome = perfil?.nomeSocial || perfil?.nome || sessao?.nome || 'Cidadão';
  const foto = perfil?.fotoBase64 ?? null;

  async function fazerLogout() {
    try {
      await api.logout();
    } catch {
      /* mesmo se falhar no servidor, encerra a sessão local */
    }
    sair();
    navigate('/login', { replace: true });
  }

  return (
    <div className="mx-auto flex min-h-dvh max-w-[460px] flex-col bg-papel shadow-2xl">
      {/* Barra superior (civismo Maricá) */}
      <header className="sticky top-0 z-30 flex items-center gap-2 bg-marica px-3 pb-3 pt-[calc(env(safe-area-inset-top)+0.75rem)] text-white shadow-topo">
        <button
          type="button"
          onClick={() => setAberto(true)}
          aria-label="Abrir menu"
          className="grid h-11 w-11 place-items-center rounded-xl transition active:bg-white/15"
        >
          <Menu className="h-6 w-6" />
        </button>
        <div className="flex flex-1 flex-col leading-tight">
          <span className="font-display text-[15px] font-semibold tracking-tight">Saúde Maricá</span>
          <span className="text-[11px] font-medium text-white/80">App do Cidadão</span>
        </div>
        <button
          type="button"
          onClick={() => navigate('/perfil')}
          aria-label="Meu perfil"
          className="rounded-full ring-2 ring-white/30 transition active:scale-95"
        >
          <Avatar src={foto} nome={nome} size={40} className="bg-white/15 text-white ring-0" />
        </button>
      </header>

      <main className="flex-1 px-4 pb-10 pt-5">
        <Outlet />
      </main>

      {/* Drawer */}
      {aberto && (
        <div className="fixed inset-0 z-40 max-w-[460px] mx-auto" role="dialog" aria-modal="true">
          <button
            type="button"
            aria-label="Fechar menu"
            onClick={() => setAberto(false)}
            className="absolute inset-0 animate-fade-in bg-tinta/40 backdrop-blur-[1px]"
          />
          <nav className="absolute inset-y-0 left-0 flex w-[78%] max-w-[320px] animate-slide-in flex-col bg-papel shadow-2xl">
            {/* Cabeçalho do drawer com mini-cartão */}
            <div className="guilloche bg-vinho px-5 pb-5 pt-[calc(env(safe-area-inset-top)+1.5rem)] text-white">
              <div className="mb-4 flex justify-end">
                <button
                  type="button"
                  onClick={() => setAberto(false)}
                  aria-label="Fechar menu"
                  className="grid h-9 w-9 place-items-center rounded-lg transition active:bg-white/15"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>
              <Avatar src={foto} nome={nome} size={56} className="bg-white/15 text-white ring-white/20" />
              <p className="mt-3 font-display text-lg font-semibold leading-tight">{nome}</p>
              {(perfil?.cpf ?? sessao?.cpf) && (
                <p className="text-xs text-white/70">CPF {formatarCpf(perfil?.cpf ?? sessao!.cpf)}</p>
              )}
            </div>

            <div className="flex-1 overflow-y-auto px-3 py-4">
              {NAV.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  className={({ isActive }) =>
                    cn(
                      'mb-1 flex min-h-[52px] items-center gap-3 rounded-2xl px-3 text-[15px] font-medium transition',
                      isActive
                        ? 'bg-marica/10 text-marica'
                        : 'text-tinta active:bg-areia/60',
                    )
                  }
                >
                  <item.icon className="h-5 w-5 shrink-0" />
                  {item.label}
                </NavLink>
              ))}
            </div>

            <div className="border-t border-areia p-3">
              <button
                type="button"
                onClick={fazerLogout}
                className="flex min-h-[52px] w-full items-center gap-3 rounded-2xl px-3 text-[15px] font-medium text-marica transition active:bg-marica/10"
              >
                <LogOut className="h-5 w-5" />
                Sair
              </button>
            </div>
          </nav>
        </div>
      )}
    </div>
  );
}

export function formatarCpf(cpf: string): string {
  const d = cpf.replace(/\D/g, '');
  if (d.length !== 11) return cpf;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}
