import { useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import {
  Activity,
  Bus,
  Building2,
  CalendarClock,
  ChevronLeft,
  ChevronRight,
  LayoutDashboard,
  LogOut,
  Route,
  ScanLine,
  Settings,
  Star,
  Truck,
  UserCog,
  Users,
  X,
  type LucideIcon,
} from 'lucide-react';
import { useAuth, type Perfil } from '@/shared/auth/authStore';
import { BrandLogo } from '@/shared/ui/BrandLogo';
import { cn } from '@/shared/lib/cn';

type ItemMenu = {
  rotulo: string;
  to: string;
  icone: LucideIcon;
  end?: boolean;
};

type SecaoMenu = {
  titulo?: string;
  itens: ItemMenu[];
};

const menusPorPerfil: Record<Perfil, SecaoMenu[]> = {
  operador: [
    {
      itens: [{ rotulo: 'Início', to: '/operador', icone: LayoutDashboard, end: true }],
    },
    {
      titulo: 'Cadastros',
      itens: [
        { rotulo: 'Pacientes', to: '/operador/pacientes', icone: Users },
        { rotulo: 'Unidades', to: '/operador/unidades', icone: Building2 },
        { rotulo: 'Veículos', to: '/operador/veiculos', icone: Bus },
        { rotulo: 'Motoristas', to: '/operador/motoristas', icone: Truck },
        { rotulo: 'Usuários', to: '/operador/usuarios', icone: UserCog },
      ],
    },
    {
      titulo: 'Operação',
      itens: [
        { rotulo: 'Tratamentos', to: '/operador/tratamentos', icone: CalendarClock },
        { rotulo: 'Translados', to: '/operador/translados', icone: Route },
        { rotulo: 'Rastreamento', to: '/operador/rastreamento', icone: Activity },
        { rotulo: 'Avaliações', to: '/operador/avaliacoes', icone: Star },
      ],
    },
    {
      titulo: 'Imagens',
      itens: [{ rotulo: 'PACS', to: '/operador/pacs', icone: ScanLine }],
    },
  ],
  gestor: [
    {
      itens: [{ rotulo: 'Visão geral', to: '/gestor', icone: LayoutDashboard, end: true }],
    },
    {
      titulo: 'Imagens',
      itens: [{ rotulo: 'PACS', to: '/gestor/pacs', icone: ScanLine }],
    },
  ],
};

type Props = {
  perfil: Perfil;
  isCollapsed: boolean;
  onToggleCollapsed: () => void;
  isMobileOpen: boolean;
  onCloseMobile: () => void;
};

export function Sidebar({
  perfil,
  isCollapsed,
  onToggleCollapsed,
  isMobileOpen,
  onCloseMobile,
}: Props) {
  const navigate = useNavigate();
  const usuario = useAuth((s) => s.usuario);
  const sair = useAuth((s) => s.sair);

  const [secoes] = useState<SecaoMenu[]>(menusPorPerfil[perfil]);

  function aoSair() {
    sair();
    navigate('/login', { replace: true });
  }

  const conteudo = (mobile: boolean) => (
    <div
      className="flex h-full flex-col overflow-y-auto shadow-marica-lg"
      style={{ background: 'var(--theme-menu-background)' }}
    >
      <div className="relative flex justify-center px-3 py-5 border-b border-white/10 bg-white">
        {!mobile && (
          <button
            type="button"
            onClick={onToggleCollapsed}
            className="absolute right-2 top-2 rounded-md p-1.5 text-gray-500 hover:bg-gray-100 hover:text-gray-900"
            title={isCollapsed ? 'Expandir' : 'Recolher'}
            aria-label={isCollapsed ? 'Expandir menu' : 'Recolher menu'}
          >
            {isCollapsed ? <ChevronRight className="w-4 h-4" /> : <ChevronLeft className="w-4 h-4" />}
          </button>
        )}
        {mobile && (
          <button
            type="button"
            onClick={onCloseMobile}
            className="absolute right-2 top-2 rounded-md p-1.5 text-gray-500 hover:bg-gray-100"
            aria-label="Fechar menu"
          >
            <X className="w-4 h-4" />
          </button>
        )}

        <BrandLogo
          compact={isCollapsed && !mobile}
          className={cn(!isCollapsed || mobile ? 'h-14 w-auto max-w-[220px]' : '')}
        />
      </div>

      <div className={cn('px-4 py-4 bg-white/10', isCollapsed && !mobile && 'px-2')}>
        <div className={cn('flex items-center', isCollapsed && !mobile ? 'justify-center' : 'gap-3')}>
          <div className="avatar avatar-gradient avatar-bordered w-10 h-10 text-sm font-semibold">
            {(usuario?.nome ?? 'U').slice(0, 2).toUpperCase()}
          </div>
          {(!isCollapsed || mobile) && (
            <div className="flex-1 min-w-0">
              <div className="truncate text-sm font-medium text-white">{usuario?.nome}</div>
              <div className="text-xs text-white/70 capitalize">{perfil}</div>
            </div>
          )}
        </div>
      </div>

      <nav className={cn('flex-1 space-y-4 py-4', isCollapsed && !mobile ? 'px-2' : 'px-3')}>
        {secoes.map((secao, i) => (
          <div key={i} className="space-y-1">
            {secao.titulo && (!isCollapsed || mobile) ? (
              <div className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-white/50">
                {secao.titulo}
              </div>
            ) : null}
            {secao.itens.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                onClick={mobile ? onCloseMobile : undefined}
                title={isCollapsed && !mobile ? item.rotulo : undefined}
                className={({ isActive }) =>
                  cn(
                    'flex items-center rounded-md px-3 py-2.5 text-sm font-medium transition-all duration-200',
                    isCollapsed && !mobile ? 'justify-center' : 'gap-3',
                    isActive ? 'bg-white text-primary-700 shadow-md' : 'text-white/90 hover:bg-white/10',
                  )
                }
              >
                <item.icone className="w-5 h-5 flex-shrink-0" />
                {(!isCollapsed || mobile) && <span>{item.rotulo}</span>}
              </NavLink>
            ))}
          </div>
        ))}
      </nav>

      <div
        className={cn('pt-4 pb-4 space-y-1', isCollapsed && !mobile ? 'px-2' : 'px-3')}
        style={{
          borderTopColor: 'color-mix(in srgb, var(--theme-menu-text) 15%, transparent)',
          borderTopWidth: '1px',
        }}
      >
        <button
          type="button"
          className={cn(
            'flex w-full items-center rounded-md px-3 py-2.5 text-sm font-medium text-white/80 hover:bg-white/10',
            isCollapsed && !mobile ? 'justify-center' : 'gap-3',
          )}
          title={isCollapsed && !mobile ? 'Configurações' : undefined}
          disabled
        >
          <Settings className="w-5 h-5" />
          {(!isCollapsed || mobile) && <span>Configurações</span>}
        </button>
        <button
          type="button"
          onClick={aoSair}
          className={cn(
            'flex w-full items-center rounded-md px-3 py-2.5 text-sm font-medium text-white/90 hover:bg-white/10',
            isCollapsed && !mobile ? 'justify-center' : 'gap-3',
          )}
          title={isCollapsed && !mobile ? 'Sair' : undefined}
        >
          <LogOut className="w-5 h-5" />
          {(!isCollapsed || mobile) && <span>Sair</span>}
        </button>
      </div>
    </div>
  );

  return (
    <>
      <div
        className={cn(
          'hidden lg:fixed lg:inset-y-0 lg:z-50 lg:flex lg:flex-col transition-all duration-300',
          isCollapsed ? 'lg:w-20' : 'lg:w-72',
        )}
      >
        {conteudo(false)}
      </div>

      {isMobileOpen && (
        <div className="fixed inset-0 z-50 lg:hidden flex">
          <div className="absolute inset-0 bg-black/50" onClick={onCloseMobile} aria-hidden="true" />
          <div className="relative w-72 max-w-[85vw] h-full">{conteudo(true)}</div>
        </div>
      )}
    </>
  );
}
