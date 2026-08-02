import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, type LucideIcon } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

export type TomRaia = 'vermelho' | 'ambar' | 'roxo';

const TONS: Record<TomRaia, { faixa: string; icone: string; titulo: string }> = {
  vermelho: { faixa: 'border-l-error-500', icone: 'bg-error-50 text-error-600', titulo: 'text-error-700' },
  ambar: { faixa: 'border-l-amber-500', icone: 'bg-amber-50 text-amber-600', titulo: 'text-amber-700' },
  roxo: { faixa: 'border-l-purple-500', icone: 'bg-purple-50 text-purple-600', titulo: 'text-purple-700' },
};

type Props = {
  titulo: string;
  /** Frase curta que explica a raia. Fica ao lado do título, em cinza. */
  subtitulo?: string;
  total: number;
  tom: TomRaia;
  icone: LucideIcon;
  /** Destino do "ver todos" — sempre uma tela QUE JÁ EXISTE, com filtro na URL (ADR-0033 §4). */
  verTodosPara: string;
  children: ReactNode;
};

/**
 * A casca de uma raia do painel. Só existem três (ADR-0033 §2) e **raia com zero não renderiza**
 * (§3): dia calmo = painel quase vazio, e é assim que ele continua sendo lido nos dias em que
 * tem algo. Vazio é estado de sucesso, não bug.
 */
export function Raia({ titulo, subtitulo, total, tom, icone: Icone, verTodosPara, children }: Props) {
  if (total <= 0) return null;
  const cores = TONS[tom];

  return (
    <section className={cn('card border-l-4 p-4', cores.faixa)}>
      <header className="mb-3 flex items-center gap-3">
        <span className={cn('flex h-8 w-8 shrink-0 items-center justify-center rounded-lg', cores.icone)}>
          <Icone className="h-4 w-4" />
        </span>
        <h3 className={cn('text-sm font-semibold', cores.titulo)}>
          {titulo} <span className="tabular-nums">· {total}</span>
        </h3>
        {subtitulo && <span className="hidden text-xs text-gray-500 sm:inline">{subtitulo}</span>}
        <Link
          to={verTodosPara}
          className="ml-auto inline-flex shrink-0 items-center gap-1 text-xs font-medium text-primary-700 hover:underline"
        >
          ver todos <ArrowRight className="h-3 w-3" />
        </Link>
      </header>
      <ul className="divide-y divide-gray-100">{children}</ul>
    </section>
  );
}
