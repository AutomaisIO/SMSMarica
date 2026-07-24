import type { ReactNode } from 'react';
import clsx from 'clsx';

interface Props {
  children: ReactNode;
  className?: string;
}

/** Superfície padrão: papel, borda linha, hover com um sopro de vermelho-marica. */
export function Cartao({ children, className }: Props) {
  return (
    <div
      className={clsx(
        'rounded-2xl border border-linha bg-papel shadow-cartao',
        'transition-colors duration-200 hover:border-vermelho-marica/30',
        className,
      )}
    >
      {children}
    </div>
  );
}
