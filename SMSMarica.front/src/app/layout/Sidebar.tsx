import { useEffect, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import { ChevronDown, ChevronLeft, ChevronRight, LogOut, X } from 'lucide-react';
import { useAuth } from '@/shared/auth/authStore';
import {
  caminhoHub,
  encontrarSecaoPorPath,
  ROTA_HUB,
  SECOES,
  type ItemMenu,
} from '@/app/layout/menuConfig';
import { useMenuPreferencias } from '@/app/layout/menuPreferencias';
import { abrirJanelaChat } from '@/features/conversas/lib/janelaChat';
import { useTicketsBadges } from '@/features/tickets/api/queries';
import { usePainelBadge } from '@/features/painel-inicio/api/queries';
import { BrandLogo } from '@/shared/ui/BrandLogo';
import { CounterBadge } from '@/shared/ui/CounterBadge';
import { cn } from '@/shared/lib/cn';

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
  const { pathname } = useLocation();
  const usuario = useAuth((s) => s.usuario);
  const permissoes = useAuth((s) => s.permissoes);
  const sair = useAuth((s) => s.sair);
  const defaults = useMenuPreferencias((s) => s.defaults);
  const badges = useTicketsBadges();
  const painelCritico = usePainelBadge();

  // Contador do badge de um item de menu (0 = não renderiza).
  function contadorBadge(item: ItemMenu): number {
    if (item.badge === 'meusTickets') return badges.meusNaoReconhecidos;
    if (item.badge === 'ticketsGestao') return badges.gestaoNovos;
    if (item.badge === 'painelInicio') return painelCritico;
    return 0;
  }

  // Soma dos badges dos itens visíveis de uma seção (mostrado no cabeçalho quando recolhida).
  function contadorSecao(itens: ItemMenu[]): number {
    return itens.reduce((total, item) => total + contadorBadge(item), 0);
  }

  // Qual seção está "ativa" pela rota atual (inclui a própria página-hub).
  const hubMatch = pathname.match(new RegExp(`^${ROTA_HUB}/([^/]+)`));
  const secaoAtivaId = hubMatch ? hubMatch[1] : encontrarSecaoPorPath(pathname)?.id ?? null;

  const [secaoAberta, setSecaoAberta] = useState<string | null>(
    () => lerSecaoAberta() ?? secaoAtivaId,
  );
  useEffect(() => {
    try {
      if (secaoAberta) localStorage.setItem(CHAVE_SECAO_ABERTA, secaoAberta);
      else localStorage.removeItem(CHAVE_SECAO_ABERTA);
    } catch {
      // ignore
    }
  }, [secaoAberta]);

  function temAcesso(item: ItemMenu): boolean {
    if (!item.modulo) return true;
    return (permissoes[item.modulo] ?? []).includes('Consulta');
  }

  function aoSair() {
    sair();
    navigate('/login', { replace: true });
  }

  const secoesVisiveis = SECOES.map((s) => ({ ...s, itens: s.itens.filter(temAcesso) })).filter(
    (s) => s.itens.length > 0,
  );

  const conteudo = (mobile: boolean) => {
    const compacto = isCollapsed && !mobile;

    const renderItem = (item: ItemMenu, indentado: boolean) => {
      const contador = contadorBadge(item);
      return item.acao === 'chat' ? (
        // Central de Atendimento abre em JANELA SEPARADA do navegador (como o PACS):
        // minimizada/atrás, o clique traz para frente; fechada, reabre (ticket #18).
        <button
          key={item.to}
          type="button"
          onClick={() => {
            abrirJanelaChat();
            if (mobile) onCloseMobile();
          }}
          title={compacto ? item.rotulo : 'Abrir a Central de Atendimento (janela separada)'}
          className={cn(
            'relative flex w-full items-center rounded-md py-2.5 text-sm font-medium transition-all duration-200',
            compacto ? 'justify-center px-3' : indentado ? 'gap-3 pl-9 pr-3' : 'gap-3 px-3',
            'text-white/90 hover:bg-white/10',
          )}
        >
          <item.icone className="w-5 h-5 flex-shrink-0" />
          {!compacto && <span>{item.rotulo}</span>}
          {compacto ? (
            <CounterBadge valor={contador} variante="branco" className="absolute right-1 top-1" />
          ) : (
            <CounterBadge valor={contador} variante="branco" className="ml-auto" />
          )}
        </button>
      ) : (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.end}
          state={item.state}
          onClick={mobile ? onCloseMobile : undefined}
          title={compacto ? item.rotulo : undefined}
          className={({ isActive }) =>
            cn(
              'relative flex items-center rounded-md py-2.5 text-sm font-medium transition-all duration-200',
              compacto ? 'justify-center px-3' : indentado ? 'gap-3 pl-9 pr-3' : 'gap-3 px-3',
              isActive ? 'bg-white text-primary-700 shadow-md' : 'text-white/90 hover:bg-white/10',
            )
          }
        >
          <item.icone className="w-5 h-5 flex-shrink-0" />
          {!compacto && <span>{item.rotulo}</span>}
          {compacto ? (
            <CounterBadge valor={contador} variante="branco" className="absolute right-1 top-1" />
          ) : (
            <CounterBadge valor={contador} variante="branco" className="ml-auto" />
          )}
        </NavLink>
      );
    };

    return (
      <div
        className="flex h-full flex-col overflow-y-auto shadow-marca-lg"
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

          <BrandLogo compact={compacto} className={cn(!compacto ? 'h-14 w-auto max-w-[220px]' : '')} />
        </div>

        <div className={cn('px-4 py-4 bg-white/10', compacto && 'px-2')}>
          <div className={cn('flex items-center', compacto ? 'justify-center' : 'gap-3')}>
            <div className="avatar avatar-gradient avatar-bordered w-10 h-10 text-sm font-semibold">
              {(usuario?.nome ?? 'U').slice(0, 2).toUpperCase()}
            </div>
            {!compacto && (
              <div className="flex-1 min-w-0">
                <div className="truncate text-sm font-medium text-white">{usuario?.nome}</div>
                <div className="truncate text-xs text-white/70">{usuario?.email}</div>
              </div>
            )}
          </div>
        </div>

        <nav className={cn('flex-1 space-y-1.5 py-4', compacto ? 'px-2' : 'px-3')}>
          {secoesVisiveis.map((secao) => {
            // Seção sem título (Início): renderiza os itens diretamente.
            if (!secao.titulo) {
              return secao.itens.map((item) => renderItem(item, false));
            }

            // Seção com 1 item visível: vira um link direto (sem hub/expansão),
            // exceto quando marcada para manter o grupo (crescerá no futuro).
            if (secao.itens.length === 1 && !secao.manterGrupo) {
              return renderItem(secao.itens[0], false);
            }

            // Seção com título e múltiplos itens: o cabeçalho leva à página-hub
            // (ou direto à tela default, quando definida e ainda visível) e
            // expande os sub-itens na própria lateral.
            const Icone = secao.icone;
            const padrao = defaults[secao.id];
            const padraoValido = padrao && secao.itens.some((i) => i.to === padrao) ? padrao : undefined;
            const destino = padraoValido ?? caminhoHub(secao.id);
            const ativa = secaoAtivaId === secao.id;
            const aberta = !compacto && secaoAberta === secao.id;
            // Recolhida/compacta: o badge do cabeçalho resume os itens; aberta, cada item mostra o seu.
            const totalSecao = aberta ? 0 : contadorSecao(secao.itens);

            return (
              <div key={secao.id} className="space-y-1">
                <div
                  className={cn(
                    'flex items-center rounded-md text-sm font-medium transition-all duration-200',
                    ativa ? 'bg-white text-primary-700 shadow-md' : 'text-white/90 hover:bg-white/10',
                  )}
                >
                  <Link
                    to={destino}
                    onClick={() => {
                      setSecaoAberta(secao.id);
                      if (mobile) onCloseMobile();
                    }}
                    title={compacto ? secao.titulo : undefined}
                    className={cn(
                      'relative flex flex-1 items-center py-2.5',
                      compacto ? 'justify-center px-3' : 'gap-3 pl-3',
                    )}
                  >
                    {Icone && <Icone className="w-5 h-5 flex-shrink-0" />}
                    {!compacto && <span className="flex-1 truncate">{secao.titulo}</span>}
                    {compacto ? (
                      <CounterBadge valor={totalSecao} variante="branco" className="absolute right-1 top-1" />
                    ) : (
                      <CounterBadge valor={totalSecao} variante="branco" className="ml-auto mr-1" />
                    )}
                  </Link>
                  {!compacto && (
                    <button
                      type="button"
                      onClick={() => setSecaoAberta((atual) => (atual === secao.id ? null : secao.id))}
                      className="px-2.5 py-2.5"
                      aria-label={aberta ? 'Recolher seção' : 'Expandir seção'}
                      aria-expanded={aberta}
                    >
                      <ChevronDown
                        className={cn('w-4 h-4 transition-transform', !aberta && '-rotate-90')}
                      />
                    </button>
                  )}
                </div>

                {aberta && <div className="space-y-1">{secao.itens.map((item) => renderItem(item, true))}</div>}
              </div>
            );
          })}
        </nav>

        <div
          className={cn('pt-4 pb-4 space-y-1', compacto ? 'px-2' : 'px-3')}
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
              compacto ? 'justify-center' : 'gap-3',
            )}
            title={compacto ? 'Sair' : undefined}
          >
            <LogOut className="w-5 h-5" />
            {!compacto && <span>Sair</span>}
          </button>
        </div>
      </div>
    );
  };

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
