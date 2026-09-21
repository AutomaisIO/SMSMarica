import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowRight, BookOpen, LifeBuoy, Search, Sparkles } from 'lucide-react';
import { instituicao } from '@/shared/tema/instituicao';
import { Input } from '@/shared/ui/Input';
import { buscar } from '@/features/manual/lib/busca';
import { GRUPOS, artigosDoGrupo, artigosRecentes } from '@/features/manual/registro';
import type { Artigo } from '@/features/manual/tipos';

const DIAS_NOVIDADE = 30;

function ehRecente(artigo: Artigo): boolean {
  const dias = (Date.now() - new Date(`${artigo.atualizadoEm}T00:00:00`).getTime()) / 86_400_000;
  return dias >= 0 && dias <= DIAS_NOVIDADE;
}

/**
 * Porta de entrada do manual: busca em cima, índice embaixo.
 *
 * A busca vem primeiro de propósito — quem abre o manual quase sempre está com uma dúvida
 * específica na mão, não com vontade de navegar por categorias.
 */
export function ManualIndicePage() {
  const navigate = useNavigate();
  const [termo, setTermo] = useState('');
  const resultados = useMemo(() => buscar(termo), [termo]);
  const buscando = termo.trim().length >= 2;
  const inst = instituicao();

  const gruposComArtigos = GRUPOS.map((g) => ({ grupo: g, artigos: artigosDoGrupo(g.id) })).filter(
    (x) => x.artigos.length > 0,
  );

  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <div className="icon-box icon-box-default">
          <BookOpen className="h-5 w-5" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Manual</h1>
          <p className="mt-1 max-w-3xl text-sm text-gray-600">
            Como usar o painel da {inst.nomeSecretaria}, tela por tela — com simulações em que você pode clicar sem medo.
            Em qualquer tela documentada, o <strong>?</strong> ao lado do título traz você direto para a explicação dela.
          </p>
        </div>
      </header>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          const primeiro = resultados[0];
          if (primeiro) navigate(`/app/manual/${primeiro.artigo.slug}`);
        }}
        className="relative max-w-2xl"
        role="search"
      >
        <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
        <Input
          value={termo}
          onChange={(e) => setTermo(e.target.value)}
          placeholder="Buscar no manual: confirmar presença, contato errado, cancelar…"
          className="pl-9"
          aria-label="Buscar no manual"
          autoFocus
        />
      </form>

      {buscando ? (
        <section className="space-y-2">
          <p className="text-sm text-gray-600">
            {resultados.length === 0
              ? 'Nada encontrado.'
              : `${resultados.length} ${resultados.length === 1 ? 'resultado' : 'resultados'}`}
          </p>
          {resultados.length === 0 ? (
            <div className="card max-w-2xl p-5 text-sm text-gray-600">
              <p>
                Este manual está sendo escrito tela por tela — pode ser que a sua ainda não tenha entrado. Diga qual você
                precisa e ela entra na fila.
              </p>
              <Link to="/app/tickets" className="mt-3 inline-flex items-center gap-1.5 text-primary-700 hover:underline">
                <LifeBuoy className="h-4 w-4" /> Pedir esta documentação por ticket
              </Link>
            </div>
          ) : (
            <ul className="space-y-2">
              {resultados.map(({ artigo, secoes }) => (
                <li key={artigo.slug}>
                  <Link to={`/app/manual/${artigo.slug}`} className="card card-hover group block p-4">
                    <div className="flex items-start gap-3">
                      <div className="icon-box icon-box-default">
                        <artigo.icone className="h-5 w-5" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <h2 className="text-base font-semibold text-gray-900 group-hover:text-primary-700">
                          {artigo.titulo}
                        </h2>
                        <p className="mt-0.5 text-sm text-gray-600">{artigo.resumo}</p>
                        {secoes.length > 0 ? (
                          <div className="mt-2 flex flex-wrap gap-1.5">
                            {secoes.map((s) => (
                              <span
                                key={s.id}
                                className="rounded-full bg-primary-50 px-2 py-0.5 text-xs text-primary-800"
                              >
                                {s.titulo}
                              </span>
                            ))}
                          </div>
                        ) : null}
                      </div>
                      <ArrowRight className="mt-1 h-4 w-4 shrink-0 text-gray-300 group-hover:text-primary-600" />
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>
      ) : (
        <>
          <section className="space-y-3">
            <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-gray-500">
              <Sparkles className="h-4 w-4" /> Comece por aqui
            </h2>
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
              {artigosRecentes().map((a) => (
                <CardArtigo key={a.slug} artigo={a} />
              ))}
            </div>
          </section>

          {gruposComArtigos.map(({ grupo, artigos }) => (
            <section key={grupo.id} className="space-y-3">
              <div className="flex items-center gap-2">
                <grupo.icone className="h-4 w-4 text-gray-500" />
                <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">{grupo.titulo}</h2>
              </div>
              <p className="-mt-2 text-sm text-gray-600">{grupo.descricao}</p>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
                {artigos.map((a) => (
                  <CardArtigo key={a.slug} artigo={a} />
                ))}
              </div>
            </section>
          ))}
        </>
      )}
    </div>
  );
}

function CardArtigo({ artigo }: { artigo: Artigo }) {
  return (
    <Link to={`/app/manual/${artigo.slug}`} className="card card-hover group block p-5">
      <div className="flex items-start gap-4">
        <div className="icon-box icon-box-default">
          <artigo.icone className="h-5 w-5" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-start gap-2">
            <h3 className="text-base font-semibold text-gray-900 group-hover:text-primary-700">{artigo.titulo}</h3>
            {ehRecente(artigo) ? (
              <span className="mt-0.5 shrink-0 rounded-full bg-emerald-100 px-2 py-0.5 text-[11px] font-medium text-emerald-700">
                novo
              </span>
            ) : null}
          </div>
          <p className="mt-1 text-sm text-gray-600">{artigo.resumo}</p>
          {artigo.publico ? <p className="mt-2 text-xs text-gray-500">Para: {artigo.publico}</p> : null}
        </div>
      </div>
    </Link>
  );
}
