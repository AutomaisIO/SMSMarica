import { Link, Navigate, useParams } from 'react-router-dom';
import { Star } from 'lucide-react';
import { encontrarSecaoPorId, type ItemMenu } from '@/app/layout/menuConfig';
import { useMenuPreferencias } from '@/app/layout/menuPreferencias';
import { useAuth } from '@/shared/auth/authStore';
import { cn } from '@/shared/lib/cn';

export function MenuHubPage() {
  const { secaoId } = useParams<{ secaoId: string }>();
  const permissoes = useAuth((s) => s.permissoes);
  const defaults = useMenuPreferencias((s) => s.defaults);
  const definir = useMenuPreferencias((s) => s.definir);
  const limpar = useMenuPreferencias((s) => s.limpar);

  const secao = encontrarSecaoPorId(secaoId);
  if (!secao || !secao.titulo) return <Navigate to="/app" replace />;

  function temAcesso(item: ItemMenu): boolean {
    if (!item.modulo) return true;
    return (permissoes[item.modulo] ?? []).includes('Consulta');
  }

  const itens = secao.itens.filter(temAcesso);
  const Icone = secao.icone;
  const padraoAtual = defaults[secao.id];

  function alternarPadrao(to: string) {
    if (padraoAtual === to) limpar(secao!.id);
    else definir(secao!.id, to);
  }

  return (
    <div className="space-y-6">
      <header>
        <div className="flex items-center gap-3">
          {Icone && (
            <div className="icon-box icon-box-default">
              <Icone className="w-5 h-5" />
            </div>
          )}
          <h1 className="text-2xl font-semibold text-gray-900">{secao.titulo}</h1>
        </div>
        <p className="mt-1 text-sm text-gray-600">
          Selecione uma opção. Marque a estrela (
          <Star className="inline w-3.5 h-3.5 -mt-0.5 text-amber-500" />) para definir a tela que abre
          direto ao clicar em <strong>{secao.titulo}</strong> no menu.
        </p>
      </header>

      {itens.length === 0 ? (
        <div className="card p-6 text-sm text-gray-500">
          Você não tem permissão para nenhuma opção desta seção.
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {itens.map((item) => {
            const ehPadrao = padraoAtual === item.to;
            return (
              <div key={item.to} className="relative">
                <Link to={item.to} state={item.state} className="card card-hover group block p-5">
                  <div className="flex items-start gap-4">
                    <div className="icon-box icon-box-default">
                      <item.icone className="w-5 h-5" />
                    </div>
                    <div className="min-w-0 flex-1 pr-8">
                      <h2 className="text-base font-semibold text-gray-900 group-hover:text-primary-700">
                        {item.rotulo}
                      </h2>
                      {item.descricao && <p className="mt-1 text-sm text-gray-600">{item.descricao}</p>}
                    </div>
                  </div>
                </Link>
                <button
                  type="button"
                  onClick={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    alternarPadrao(item.to);
                  }}
                  className={cn(
                    'absolute right-3 top-3 rounded-md p-1.5 transition-colors',
                    ehPadrao
                      ? 'text-amber-500 hover:bg-amber-50'
                      : 'text-gray-300 hover:bg-gray-100 hover:text-gray-500',
                  )}
                  title={ehPadrao ? 'Tela default desta seção (clique p/ remover)' : 'Definir como tela default'}
                  aria-label={ehPadrao ? 'Remover tela default' : 'Definir como tela default'}
                  aria-pressed={ehPadrao}
                >
                  <Star className={cn('w-5 h-5', ehPadrao && 'fill-current')} />
                </button>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
