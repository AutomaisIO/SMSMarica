import { useEffect, useRef, useState } from 'react';
import { Bell, Building2, Check, ChevronDown, KeyRound, LogOut, Menu, User as UserIcon } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth/authStore';
import { useChat } from '@/features/conversas/store/chatStore';

type Props = {
  onToggleMobileSidebar: () => void;
};

export function Header({ onToggleMobileSidebar }: Props) {
  const navigate = useNavigate();
  const usuario = useAuth((s) => s.usuario);
  const sair = useAuth((s) => s.sair);
  const totalNaoLidas = useChat((s) => s.totalNaoLidas);
  const abrirChat = useChat((s) => s.abrir);
  const unidades = useAuth((s) => s.unidades);
  const unidadeAtivaId = useAuth((s) => s.unidadeAtivaId);
  const definirUnidadeAtiva = useAuth((s) => s.definirUnidadeAtiva);
  const queryClient = useQueryClient();
  const [aberto, setAberto] = useState(false);
  const ref = useRef<HTMLDivElement | null>(null);
  const [unidadeAberto, setUnidadeAberto] = useState(false);
  const unidadeRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    function aoClicarFora(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setAberto(false);
      }
      if (unidadeRef.current && !unidadeRef.current.contains(e.target as Node)) {
        setUnidadeAberto(false);
      }
    }
    if (aberto || unidadeAberto) {
      document.addEventListener('mousedown', aoClicarFora);
    }
    return () => document.removeEventListener('mousedown', aoClicarFora);
  }, [aberto, unidadeAberto]);

  function aoTrocarUnidade(id: string | null) {
    setUnidadeAberto(false);
    if (id === unidadeAtivaId) return;
    definirUnidadeAtiva(id);
    // Tudo que veio do servidor pode depender da unidade ativa — refaz as queries.
    void queryClient.invalidateQueries();
  }

  const unidadeAtiva = unidades.find((u) => u.id === unidadeAtivaId) ?? null;

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
          {unidades.length > 1 && (
            <div className="relative" ref={unidadeRef}>
              <button
                type="button"
                onClick={() => setUnidadeAberto((v) => !v)}
                className="flex items-center gap-2 rounded-md border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-100"
                title="Trocar de unidade"
              >
                <Building2 className="w-4 h-4 text-gray-500" />
                <span className="hidden max-w-48 truncate sm:block">
                  {unidadeAtiva?.nome ?? 'Todas as unidades'}
                </span>
                <ChevronDown className="w-4 h-4 text-gray-500" />
              </button>

              {unidadeAberto && (
                <div className="absolute right-0 mt-2 w-64 rounded-md border border-gray-200 bg-white py-1 shadow-lg">
                  <p className="border-b border-gray-200 px-4 py-2 text-xs font-medium uppercase text-gray-400">
                    Unidade ativa
                  </p>
                  <button
                    type="button"
                    onClick={() => aoTrocarUnidade(null)}
                    className="flex w-full items-center gap-2 px-4 py-2 text-left text-sm text-gray-700 hover:bg-gray-100"
                  >
                    <span className="flex-1 truncate">Todas as unidades</span>
                    {unidadeAtivaId === null && <Check className="w-4 h-4 text-primary-600" />}
                  </button>
                  {/* Mais de 3 unidades: rola em vez de esticar o menu. */}
                  <div className="max-h-32 overflow-y-auto">
                    {unidades.map((u) => (
                      <button
                        key={u.id}
                        type="button"
                        onClick={() => aoTrocarUnidade(u.id)}
                        className="flex w-full items-center gap-2 px-4 py-2 text-left text-sm text-gray-700 hover:bg-gray-100"
                      >
                        <span className="flex-1 truncate">{u.nome}</span>
                        {u.id === unidadeAtivaId && <Check className="w-4 h-4 text-primary-600" />}
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}

          <button
            type="button"
            onClick={abrirChat}
            className="relative rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Abrir atendimento"
            title="Central de Atendimento"
          >
            <Bell className="w-5 h-5" />
            {totalNaoLidas > 0 && (
              <span className="absolute -right-0.5 -top-0.5 inline-flex h-4 min-w-4 items-center justify-center rounded-full bg-error-500 px-1 text-[10px] font-bold text-white">
                {totalNaoLidas > 99 ? '99+' : totalNaoLidas}
              </span>
            )}
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
                <div className="border-b border-gray-200 px-4 py-2">
                  <p className="text-sm font-medium text-gray-900">{usuario?.nome}</p>
                  <p className="truncate text-xs text-gray-500">{usuario?.email}</p>
                </div>
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
