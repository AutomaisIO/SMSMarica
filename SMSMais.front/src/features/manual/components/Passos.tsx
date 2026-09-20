import type { ReactNode } from 'react';
import { Check } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

export type Passo = {
  titulo: ReactNode;
  detalhe?: ReactNode;
};

/**
 * Receita numerada. Serve tanto para leitura ("faça assim") quanto para roteiro de simulação
 * — por isso aceita `atual`/`concluidos`, que acendem o passo em que a pessoa está.
 */
export function Passos({
  itens,
  atual,
  concluidos = 0,
}: {
  itens: Passo[];
  /** Índice (0-based) do passo em foco. Sem ele, a lista é só leitura. */
  atual?: number;
  /** Quantos passos já foram cumpridos. */
  concluidos?: number;
}) {
  return (
    <ol className="max-w-3xl space-y-2">
      {itens.map((p, i) => {
        const feito = i < concluidos;
        const emFoco = atual === i;
        return (
          <li
            key={i}
            className={cn(
              'flex gap-3 rounded-theme-md border px-3 py-2.5 transition-colors',
              emFoco ? 'border-primary-300 bg-primary-50/60' : 'border-gray-200 bg-white',
              feito && !emFoco ? 'opacity-70' : null,
            )}
          >
            <span
              className={cn(
                'mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold',
                feito
                  ? 'bg-emerald-100 text-emerald-700'
                  : emFoco
                    ? 'bg-primary-600 text-white'
                    : 'bg-gray-100 text-gray-600',
              )}
            >
              {feito ? <Check className="h-3.5 w-3.5" /> : i + 1}
            </span>
            <div className="min-w-0 space-y-0.5">
              <p className={cn('text-sm', emFoco ? 'font-semibold text-gray-900' : 'font-medium text-gray-800')}>
                {p.titulo}
              </p>
              {p.detalhe ? <p className="text-sm leading-relaxed text-gray-600">{p.detalhe}</p> : null}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
