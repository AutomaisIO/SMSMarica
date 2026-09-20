import type { ReactNode } from 'react';
import { cn } from '@/shared/lib/cn';

/**
 * Réplica inerte de um botão da tela, para citar no texto: "clique em <BotaoRef>Atender</BotaoRef>".
 * É desenho, não botão — não tem `onClick` de propósito, para ninguém achar que o manual age.
 */
export function BotaoRef({
  children,
  variante = 'primaria',
}: {
  children: ReactNode;
  variante?: 'primaria' | 'outline' | 'danger' | 'ghost';
}) {
  return (
    <span
      className={cn(
        'mx-0.5 inline-flex items-center gap-1 rounded-md px-2 py-0.5 align-baseline text-[0.8125rem] font-medium',
        variante === 'primaria' && 'bg-primary-600 text-white',
        variante === 'outline' && 'border border-gray-300 bg-white text-gray-700',
        variante === 'danger' && 'bg-red-600 text-white',
        variante === 'ghost' && 'text-gray-700 underline decoration-dotted underline-offset-2',
      )}
    >
      {children}
    </span>
  );
}

/** Réplica de uma aba, para citar filas no texto. */
export function AbaRef({ children }: { children: ReactNode }) {
  return (
    <span className="mx-0.5 inline-flex items-center rounded-full border border-primary-200 bg-primary-50 px-2 py-0.5 align-baseline text-xs font-medium text-primary-800">
      {children}
    </span>
  );
}

/** Réplica de um selo (badge) da tela, com a mesma cor que a pessoa vê lá. */
export function SeloRef({
  children,
  cor = 'gray',
}: {
  children: ReactNode;
  cor?: 'gray' | 'info' | 'sucesso' | 'alerta' | 'erro';
}) {
  return (
    <span
      className={cn(
        'mx-0.5 inline-flex items-center gap-1 rounded-full px-2 py-0.5 align-baseline text-xs font-medium',
        cor === 'gray' && 'bg-gray-100 text-gray-700',
        cor === 'info' && 'bg-sky-100 text-sky-800',
        cor === 'sucesso' && 'bg-emerald-100 text-emerald-700',
        cor === 'alerta' && 'bg-amber-100 text-amber-800',
        cor === 'erro' && 'bg-red-100 text-red-700',
      )}
    >
      {children}
    </span>
  );
}
