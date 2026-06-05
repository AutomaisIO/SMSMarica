import { useEffect, useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import {
  Activity,
  Bus,
  Building2,
  CalendarClock,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  BookOpen,
  ClipboardCheck,
  ClipboardList,
  HeartPulse,
  FileCog,
  FileText,
  Image as ImageIcon,
  Folder,
  KeyRound,
  Layers,
  LayoutDashboard,
  LogOut,
  Route,
  ScanLine,
  Settings2,
  Sparkles,
  Star,
  Stethoscope,
  Truck,
  UserCog,
  Users,
  Wrench,
  X,
  type LucideIcon,
} from 'lucide-react';
import { useAuth, type ModuloPermissao } from '@/shared/auth/authStore';
import { BrandLogo } from '@/shared/ui/BrandLogo';
import { cn } from '@/shared/lib/cn';

type ItemMenu = {
  rotulo: string;
  to: string;
  icone: LucideIcon;
  modulo?: ModuloPermissao;
  end?: boolean;
  /** Estado passado pra rota — usado p/ ações automáticas (ex.: abrir modal). */
  state?: unknown;
};

type SecaoMenu = {
  id: string;
  titulo?: string;
  icone?: LucideIcon;
  itens: ItemMenu[];
};

const SECOES: SecaoMenu[] = [
  {
    id: 'inicio',
    itens: [{ rotulo: 'Início', to: '/app', icone: LayoutDashboard, end: true }],
  },
  {
    id: 'cadastros',
    titulo: 'Cadastros',
    icone: Folder,
    itens: [
      { rotulo: 'Pacientes', to: '/app/pacientes', icone: Users, modulo: 'Pacientes' },
      { rotulo: 'Médicos', to: '/app/medicos', icone: Stethoscope, modulo: 'Medicos' },
      { rotulo: 'Profissionais', to: '/app/profissionais', icone: HeartPulse, modulo: 'Medicos' },
      { rotulo: 'Unidades', to: '/app/unidades', icone: Building2, modulo: 'Unidades' },
      { rotulo: 'Veículos', to: '/app/veiculos', icone: Bus, modulo: 'Veiculos' },
      { rotulo: 'Motoristas', to: '/app/motoristas', icone: Truck, modulo: 'Motoristas' },
      { rotulo: 'Usuários', to: '/app/usuarios', icone: UserCog, modulo: 'Usuarios' },
      { rotulo: 'Tipos de tratamento', to: '/app/tipos-tratamento', icone: ClipboardList, modulo: 'TiposTratamento' },
      { rotulo: 'Perfis', to: '/app/perfis', icone: KeyRound, modulo: 'Perfis' },
    ],
  },
  {
    id: 'operacao',
    titulo: 'Transporte Pacientes',
    icone: Wrench,
    itens: [
      { rotulo: 'Tratamentos', to: '/app/tratamentos', icone: CalendarClock, modulo: 'Tratamentos' },
      { rotulo: 'Translados', to: '/app/translados', icone: Route, modulo: 'Translados' },
      { rotulo: 'Rastreamento', to: '/app/rastreamento', icone: Activity, modulo: 'Rastreamento' },
      { rotulo: 'Avaliações', to: '/app/avaliacoes', icone: Star, modulo: 'Avaliacoes' },
    ],
  },
  {
    id: 'agendamento',
    titulo: 'Agendamento',
    icone: CalendarClock,
    itens: [
      { rotulo: 'Agendas', to: '/app/agendas', icone: CalendarClock, modulo: 'Agendamentos' },
      { rotulo: 'Especialidades', to: '/app/especialidades', icone: Stethoscope, modulo: 'Especialidades' },
    ],
  },
  {
    id: 'sisreg',
    titulo: 'SISREG',
    icone: ClipboardList,
    itens: [
      { rotulo: 'Consultar SISREG', to: '/app/sisreg', icone: ClipboardList, modulo: 'Sisreg', end: true },
      {
        rotulo: 'Configuração SISREG',
        to: '/app/sisreg/configuracao',
        icone: Settings2,
        modulo: 'SisregConfiguracao',
      },
    ],
  },
  {
    id: 'solicitacoes',
    titulo: 'Solicitações de Exame',
    icone: ClipboardCheck,
    itens: [
      {
        rotulo: 'Solicitações',
        to: '/app/solicitacoes-exame',
        icone: ClipboardCheck,
        modulo: 'SolicitacoesExame',
      },
      {
        rotulo: 'Tipos de Exame',
        to: '/app/tipos-exame',
        icone: Layers,
        modulo: 'TiposExame',
      },
      {
        rotulo: 'Catálogo SIGTAP',
        to: '/app/procedimentos-sigtap',
        icone: BookOpen,
        modulo: 'ProcedimentosSigtap',
      },
    ],
  },
  {
    id: 'imagens',
    titulo: 'Exames de Imagem',
    icone: ImageIcon,
    itens: [
      {
        rotulo: 'Abrir Exame',
        to: '/app/pacs',
        icone: ScanLine,
        modulo: 'Pacs',
      },
      {
        rotulo: 'Laudos',
        to: '/app/laudos',
        icone: FileText,
        modulo: 'Laudos',
      },
      {
        rotulo: 'Templates de Laudo',
        to: '/app/laudo-templates',
        icone: FileCog,
        modulo: 'LaudosTemplates',
      },
    ],
  },
  {
    id: 'inteligencia',
    titulo: 'Inteligência',
    icone: Sparkles,
    itens: [
      { rotulo: 'IA', to: '/app/ia', icone: Sparkles, modulo: 'Inteligencia', end: true },
      {
        rotulo: 'Configuração IA',
        to: '/app/ia/configuracao',
        icone: Settings2,
        modulo: 'InteligenciaConfiguracao',
      },
      {
        rotulo: 'Melhorias da IA',
        to: '/app/ia/melhorias',
        icone: Sparkles,
        modulo: 'InteligenciaAprendizado',
      },
    ],
  },
];

const CHAVE_SECAO_ABERTA = 'smsmarica.menu.secaoAberta';

function lerSecaoAberta(): string | null {
  try {
    return localStorage.getItem(CHAVE_SECAO_ABERTA);
  } catch {
    return null;
  }
}

type Props = {
  isCollapsed: boolean;
  onToggleCollapsed: () => void;
  isMobileOpen: boolean;
  onCloseMobile: () => void;
};

export function Sidebar({ isCollapsed, onToggleCollapsed, isMobileOpen, onCloseMobile }: Props) {
  const navigate = useNavigate();
  const usuario = useAuth((s) => s.usuario);
  const permissoes = useAuth((s) => s.permissoes);
  const sair = useAuth((s) => s.sair);

  const [secaoAberta, setSecaoAberta] = useState<string | null>(() => {
    const armazenado = lerSecaoAberta();
    if (armazenado) return armazenado;
    const path = typeof window !== 'undefined' ? window.location.pathname : '';
    return (
      SECOES.find((s) => s.titulo && s.itens.some((i) => path.startsWith(i.to)))?.id ?? null
    );
  });
  useEffect(() => {
    try {
      if (secaoAberta) localStorage.setItem(CHAVE_SECAO_ABERTA, secaoAberta);
      else localStorage.removeItem(CHAVE_SECAO_ABERTA);
    } catch {
      // ignore
    }
  }, [secaoAberta]);

  function alternarSecao(id: string) {
    setSecaoAberta((atual) => (atual === id ? null : id));
  }

  function temAcesso(item: ItemMenu): boolean {
    if (!item.modulo) return true;
    return (permissoes[item.modulo] ?? []).includes('Consulta');
  }

  function aoSair() {
    sair();
    navigate('/login', { replace: true });
  }

  const secoesVisiveis = SECOES.map((s) => ({ ...s, itens: s.itens.filter(temAcesso) }))
    .filter((s) => s.itens.length > 0);

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
              <div className="truncate text-xs text-white/70">{usuario?.email}</div>
            </div>
          )}
        </div>
      </div>

      <nav className={cn('flex-1 space-y-3 py-4', isCollapsed && !mobile ? 'px-2' : 'px-3')}>
        {secoesVisiveis.map((secao) => {
          const colapsado = secao.titulo ? secaoAberta !== secao.id : false;
          const mostrarTitulo = secao.titulo && (!isCollapsed || mobile);

          return (
            <div key={secao.id} className="space-y-1">
              {mostrarTitulo ? (
                <button
                  type="button"
                  onClick={() => alternarSecao(secao.id)}
                  className="flex w-full items-center justify-between gap-2 px-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-white/60 hover:text-white"
                >
                  <span>{secao.titulo}</span>
                  <ChevronDown
                    className={cn('w-3.5 h-3.5 transition-transform', colapsado && '-rotate-90')}
                  />
                </button>
              ) : null}
              {(!mostrarTitulo || !colapsado) &&
                secao.itens.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    end={item.end}
                    state={item.state}
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
          );
        })}
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
