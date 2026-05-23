import { useEffect, useRef, useState } from 'react';
import { Bell, ChevronDown, KeyRound, LogOut, Menu, User as UserIcon } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/shared/auth/authStore';

type Props = {
  onToggleMobileSidebar: () => void;
};

export function Header({ onToggleMobileSidebar }: Props) {
  const navigate = useNavigate();
  const usuario = useAuth((s) => s.usuario);
  const sair = useAuth((s) => s.sair);
  const [aberto, setAberto] = useState(false);
  const ref = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    function aoClicarFora(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setAberto(false);
      }
    }
    if (aberto) {
      document.addEventListener('mousedown', aoClicarFora);
    }
    return () => document.removeEventListener('mousedown', aoClicarFora);
  }, [aberto]);

  function aoSair() {
    sair();
    navigate('/login', { replace: true });
  }

  function abrirMeuPerfil() {
    setAberto(false);
    navigate('/app/meu-perfil');
  }

  function abrirAlterarSenha() {
    setAberto(false);
    navigate('/app/alterar-senha');
  }

  const iniciais = (usuario?.nome ?? 'U').slice(0, 2).toUpperCase();

  return (
    <header className="sticky top-0 z-40 border-b border-gray-200 bg-white shadow-sm">
      <div className="flex h-16 items-center justify-between px-4 sm:px-6 lg:px-8">
        <button
          type="button"
          onClick={onToggleMobileSidebar}
          className="rounded-md p-2 text-gray-600 hover:bg-gray-100 lg:hidden"
          aria-label="Abrir menu lateral"
        >
          <Menu className="w-6 h-6" />
        </button>

        <div className="flex-1" />

        <div className="flex items-center gap-3">
          <button
            type="button"
            className="relative rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Notificações"
          >
            <Bell className="w-5 h-5" />
            <span className="absolute right-1.5 top-1.5 h-2 w-2 rounded-full bg-error-500" />
          </button>

          <div className="relative" ref={ref}>
            <button
              type="button"
              onClick={() => setAberto((v) => !v)}
              className="flex items-center gap-2 rounded-md p-1.5 pr-3 hover:bg-gray-100"
            >
              <div className="avatar avatar-gradient avatar-bordered w-8 h-8 text-sm font-semibold">
                {iniciais}
              </div>
              <span className="hidden text-sm font-medium text-gray-700 sm:block">
                {usuario?.nome}
              </span>
              <ChevronDown className="w-4 h-4 text-gray-500" />
            </button>

            {aberto && (
              <div className="absolute right-0 mt-2 w-56 rounded-md border border-gray-200 bg-white py-1 shadow-lg">
                <button
                  type="button"
                  onClick={abrirMeuPerfil}
                  className="block w-full border-b border-gray-200 px-4 py-2 text-left hover:bg-gray-50"
                >
                  <p className="text-sm font-medium text-gray-900">{usuario?.nome}</p>
                  <p className="truncate text-xs text-gray-500">{usuario?.email}</p>
                  <p className="mt-0.5 text-[10px] uppercase tracking-wider text-primary-600">
                    Ver meu perfil
                  </p>
                </button>
                <button
                  type="button"
                  onClick={abrirMeuPerfil}
                  className="flex w-full items-center gap-2 px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
                >
                  <UserIcon className="w-4 h-4" />
                  Meu perfil
                </button>
                <button
                  type="button"
                  onClick={abrirAlterarSenha}
                  className="flex w-full items-center gap-2 px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
                >
                  <KeyRound className="w-4 h-4" />
                  Alterar senha
                </button>
                <div className="my-1 border-t border-gray-200" />
                <button
                  type="button"
                  onClick={aoSair}
                  className="flex w-full items-center gap-2 px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
                >
                  <LogOut className="w-4 h-4" />
                  Sair
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
