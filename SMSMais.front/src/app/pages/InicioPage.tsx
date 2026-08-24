import { useEffect, useMemo, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { Star } from 'lucide-react';
import { useAuth } from '@/shared/auth/authStore';
import { SECOES, resolverDestinoMenu } from '@/app/layout/menuConfig';
import { PainelInicio } from '@/features/painel-inicio/components/PainelInicio';
import {
  alternarFavorito,
  jaRedirecionouNaSessao,
  lerFavorito,
  marcarRedirecionadoNaSessao,
} from '@/shared/menuFavorito/menuFavorito';

/** Seções que não viram card na home: a própria home e as ações que não são "tela de módulo". */
const SECOES_OCULTAS = new Set(['inicio']);

export function InicioPage() {
  const usuario = useAuth((s) => s.usuario);
  const permissoes = useAuth((s) => s.permissoes);

  /**
   * Os cards saem do MESMO `menuConfig` que alimenta a sidebar. Antes eram uma lista paralela
   * escrita à mão, que envelheceu (faltavam Solicitações, Laudos, Conversas, SISREG…): duas
   * verdades para "quais módulos existem" sempre divergem. Agora só há uma.
   */
  const cardsVisiveis = useMemo(
    () =>
      SECOES.filter((s) => !SECOES_OCULTAS.has(s.id))
        .flatMap((s) => s.itens)
        // Só telas de módulo: item sem `modulo` é navegação auxiliar, e `acao` é a janela do chat.
        .filter((i) => i.modulo && !i.acao)
        .filter((i) => (permissoes[i.modulo!] ?? []).includes('Consulta'))
        // Um mesmo módulo pode ter várias telas (ex.: Médicos e Profissionais): a chave é a rota.
        .filter((i, idx, todos) => todos.findIndex((o) => o.to === i.to) === idx),
    [permissoes],
  );

  const [favorito, setFavorito] = useState<string | null>(() => lerFavorito(usuario?.id));

  // Sincroniza o favorito quando o usuário logado muda.
  useEffect(() => {
    setFavorito(lerFavorito(usuario?.id));
  }, [usuario?.id]);

  // Redirect de entrada: computa uma única vez (idempotente sob StrictMode) se, ao abrir o app,
  // há favorito e ainda não redirecionamos nesta sessão do navegador.
  //
  // O redirect é MANTIDO de propósito: uma tela de entrada que muda sozinha conforme o estado do
  // banco seria imprevisível. Quem é redirecionado enxerga o painel pelo contador no item "Início"
  // do menu (ADR-0033 §8) — é o que impede o painel de nascer invisível para o usuário frequente.
  const [destinoRedirect] = useState<string | null>(() => {
    const fav = lerFavorito(usuario?.id);
    if (fav && !jaRedirecionouNaSessao()) {
      marcarRedirecionadoNaSessao();
      return resolverDestinoMenu(fav);
    }
    return null;
  });
  if (destinoRedirect) {
    return <Navigate to={destinoRedirect} replace />;
  }

  function toggleFavorito(to: string) {
    setFavorito(alternarFavorito(to, usuario?.id));
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-gray-900">
          Bem-vindo(a){usuario?.nome ? `, ${usuario.nome}` : ''}
        </h1>
      </header>

      <PainelInicio />

      {cardsVisiveis.length === 0 ? (
        <div className="card p-6 text-sm text-gray-500">
          Você ainda não tem permissões atribuídas. Peça a um administrador para vincular um perfil ao seu usuário.
        </div>
      ) : (
        <section className="space-y-3">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">Módulos</h2>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
            {cardsVisiveis.map((c) => (
              <Link key={c.to} to={c.to} className="card card-hover group relative p-5">
                <button
                  type="button"
                  aria-label={favorito === c.to ? 'Remover dos favoritos' : 'Definir como favorito'}
                  aria-pressed={favorito === c.to}
                  title={
                    favorito === c.to
                      ? 'Menu favorito — ao abrir o app você vai direto para cá. Clique para remover.'
                      : 'Favoritar: ao abrir o app você vai direto para este menu.'
                  }
                  onClick={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    toggleFavorito(c.to);
                  }}
                  className="absolute right-3 top-3 rounded-full p-1.5 text-gray-300 hover:bg-gray-100 hover:text-amber-500"
                >
                  <Star
                    className="h-5 w-5"
                    fill={favorito === c.to ? 'currentColor' : 'none'}
                    color={favorito === c.to ? '#f59e0b' : 'currentColor'}
                  />
                </button>
                <div className="flex items-start gap-4">
                  <div className="icon-box icon-box-default">
                    <c.icone className="w-5 h-5" />
                  </div>
                  <div className="min-w-0 flex-1 pr-6">
                    <h3 className="text-base font-semibold text-gray-900 group-hover:text-primary-700">
                      {c.rotulo}
                    </h3>
                    {c.descricao && <p className="mt-1 text-sm text-gray-600">{c.descricao}</p>}
                  </div>
                </div>
              </Link>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
