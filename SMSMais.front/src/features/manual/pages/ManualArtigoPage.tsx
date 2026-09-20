import { useEffect, useMemo, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { ArrowLeft, BookOpen, ExternalLink, LifeBuoy, ListTree, Printer } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/Button';
import { artigoPorSlug, grupoPorId } from '@/features/manual/registro';

/**
 * Um artigo do manual.
 *
 * Duas decisões que valem explicação: (1) cada seção é uma âncora real (`#id`), porque o "?" de uma
 * tela pode apontar para o pedaço exato que interessa; (2) o índice lateral acompanha a rolagem —
 * artigo de manual é longo por natureza, e perder-se dentro dele é o jeito mais fácil de desistir.
 */
export function ManualArtigoPage() {
  const { slug } = useParams<{ slug: string }>();
  const [params] = useSearchParams();
  const voltarPara = params.get('de');
  const artigo = artigoPorSlug(slug);
  const secoes = useMemo(() => artigo?.secoes() ?? [], [artigo]);
  const [ativa, setAtiva] = useState<string | null>(null);

  // Âncora recebida por link (`#pendentes`): o React só põe as seções no DOM depois do primeiro
  // render, então o scroll nativo do navegador chega cedo demais e não acha nada.
  useEffect(() => {
    const id = window.location.hash.replace('#', '');
    if (!id) return;
    const alvo = document.getElementById(id);
    if (alvo) alvo.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [slug, secoes.length]);

  // Índice lateral acompanha a leitura.
  useEffect(() => {
    if (secoes.length === 0) return;
    const observador = new IntersectionObserver(
      (entradas) => {
        const visivel = entradas
          .filter((e) => e.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)[0];
        if (visivel) setAtiva(visivel.target.id);
      },
      { rootMargin: '-96px 0px -70% 0px', threshold: 0 },
    );
    for (const s of secoes) {
      const el = document.getElementById(s.id);
      if (el) observador.observe(el);
    }
    return () => observador.disconnect();
  }, [secoes]);

  if (!artigo) {
    return (
      <div className="card max-w-2xl p-6">
        <h1 className="text-lg font-semibold text-gray-900">Este assunto ainda não está no manual</h1>
        <p className="mt-2 text-sm text-gray-600">
          O manual está sendo escrito tela por tela. Volte ao índice para ver o que já existe — ou peça esta por ticket.
        </p>
        <div className="mt-4 flex flex-wrap gap-2">
          <Link to="/app/manual" className="btn btn-primary btn-sm">
            Ir para o índice
          </Link>
          <Link to="/app/tickets" className="btn btn-outline btn-sm">
            Pedir esta documentação
          </Link>
        </div>
      </div>
    );
  }

  const grupo = grupoPorId(artigo.grupo);
  const atualizado = new Date(`${artigo.atualizadoEm}T00:00:00`).toLocaleDateString('pt-BR');

  return (
    <div className="space-y-6">
      <nav className="flex flex-wrap items-center gap-1.5 text-sm text-gray-500" aria-label="Trilha">
        <Link to="/app/manual" className="inline-flex items-center gap-1 hover:text-primary-700">
          <BookOpen className="h-3.5 w-3.5" /> Manual
        </Link>
        {grupo ? (
          <>
            <span aria-hidden>›</span>
            <span>{grupo.titulo}</span>
          </>
        ) : null}
      </nav>

      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <div className="icon-box icon-box-default">
            <artigo.icone className="h-5 w-5" />
          </div>
          <div>
            <h1 className="text-2xl font-semibold text-gray-900">{artigo.titulo}</h1>
            <p className="mt-1 max-w-3xl text-sm text-gray-600">{artigo.resumo}</p>
            <p className="mt-1 text-xs text-gray-500">
              {artigo.publico ? <>Para: {artigo.publico} · </> : null}Atualizado em {atualizado}
            </p>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2 print:hidden">
          {voltarPara ? (
            <Link to={voltarPara} className="btn btn-outline btn-sm">
              <ArrowLeft className="mr-1 h-3.5 w-3.5" /> Voltar para a tela
            </Link>
          ) : null}
          {artigo.rota && artigo.rota !== voltarPara ? (
            <Link to={artigo.rota} className="btn btn-primary btn-sm">
              <ExternalLink className="mr-1 h-3.5 w-3.5" /> Abrir a tela
            </Link>
          ) : null}
          <Button variante="ghost" tamanho="sm" onClick={() => window.print()} title="Imprimir ou salvar em PDF">
            <Printer className="mr-1 h-3.5 w-3.5" /> Imprimir
          </Button>
        </div>
      </header>

      <div className="gap-8 lg:grid lg:grid-cols-[minmax(0,1fr)_15rem] lg:items-start">
        <article className="min-w-0 space-y-10">
          {secoes.map((s) => (
            <section key={s.id} id={s.id} className="scroll-mt-24 space-y-4">
              <h2 className="group flex items-baseline gap-2 text-xl font-semibold tracking-tight text-gray-900">
                {s.titulo}
                <a
                  href={`#${s.id}`}
                  className="text-sm font-normal text-gray-300 opacity-0 transition-opacity hover:text-primary-600 group-hover:opacity-100 print:hidden"
                  aria-label={`Link para "${s.titulo}"`}
                >
                  #
                </a>
              </h2>
              {s.conteudo}
            </section>
          ))}

          <footer className="border-t border-gray-200 pt-4 print:hidden">
            <p className="text-sm text-gray-600">
              Ficou faltando alguma coisa aqui? Isso é informação útil — o manual melhora justamente pelo que falta.
            </p>
            <div className="mt-3 flex flex-wrap gap-2">
              <Link to="/app/tickets" className="btn btn-outline btn-sm">
                <LifeBuoy className="mr-1 h-3.5 w-3.5" /> Abrir um ticket
              </Link>
              <Link to="/app/manual" className="btn btn-ghost btn-sm">
                Voltar ao índice do manual
              </Link>
            </div>
          </footer>
        </article>

        <aside className="mt-8 hidden lg:sticky lg:top-6 lg:mt-0 lg:block print:hidden">
          <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-gray-500">
            <ListTree className="h-3.5 w-3.5" /> Neste artigo
          </p>
          <ul className="mt-2 space-y-1 border-l border-gray-200">
            {secoes.map((s) => (
              <li key={s.id}>
                <a
                  href={`#${s.id}`}
                  className={cn(
                    '-ml-px block border-l-2 py-1 pl-3 text-sm transition-colors',
                    ativa === s.id
                      ? 'border-primary-600 font-medium text-primary-700'
                      : 'border-transparent text-gray-600 hover:border-gray-300 hover:text-gray-900',
                  )}
                >
                  {s.titulo}
                </a>
              </li>
            ))}
          </ul>
        </aside>
      </div>
    </div>
  );
}
