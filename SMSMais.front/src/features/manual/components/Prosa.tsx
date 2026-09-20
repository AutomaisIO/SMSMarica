import type { ReactNode } from 'react';
import { cn } from '@/shared/lib/cn';

/** Parágrafo do manual. Uma medida de leitura só, em todo o manual. */
export function P({ children, className }: { children: ReactNode; className?: string }) {
  return <p className={cn('max-w-3xl text-[15px] leading-relaxed text-gray-700', className)}>{children}</p>;
}

/** Subtítulo dentro de uma seção (não entra no índice lateral — é detalhe, não destino). */
export function Sub({ children }: { children: ReactNode }) {
  return <h3 className="mt-6 text-base font-semibold text-gray-900">{children}</h3>;
}

export function Lista({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <ul className={cn('max-w-3xl space-y-1.5 text-[15px] leading-relaxed text-gray-700', className)}>
      {children}
    </ul>
  );
}

export function Item({ children }: { children: ReactNode }) {
  return (
    <li className="flex gap-2">
      <span aria-hidden className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-primary-400" />
      <span className="min-w-0">{children}</span>
    </li>
  );
}

/**
 * Lista "termo → o que significa". É o formato certo para legenda de selo e de status, que é
 * metade das dúvidas de quem está aprendendo a tela.
 */
export function ListaDefinicoes({ itens }: { itens: { termo: ReactNode; descricao: ReactNode }[] }) {
  return (
    <dl className="max-w-3xl divide-y divide-gray-100 rounded-theme-md border border-gray-200 bg-white">
      {itens.map((i, idx) => (
        <div key={idx} className="grid gap-1 px-4 py-3 sm:grid-cols-[minmax(0,13rem)_1fr] sm:gap-4">
          <dt className="text-sm font-medium text-gray-900">{i.termo}</dt>
          <dd className="text-sm leading-relaxed text-gray-600">{i.descricao}</dd>
        </div>
      ))}
    </dl>
  );
}
