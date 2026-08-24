import { ShieldCheck } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import type { StatusLaudo } from '@/features/laudos/types';

type Props = { status: StatusLaudo; assinado?: boolean };

/**
 * Badge de estado do laudo. A régua de cor reflete o que importa juridicamente:
 * - **Assinado** (verde + cadeado): laudo com assinatura digital ICP-Brasil válida.
 * - **Finalizado** (azul): fechado, mas ainda sem assinatura.
 * - **Rascunho** (âmbar): em edição.
 */
export function StatusBadgeLaudo({ status, assinado = false }: Props) {
  if (assinado) {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-2 py-0.5 text-xs font-medium text-green-700 ring-1 ring-green-200">
        <ShieldCheck className="h-3.5 w-3.5" />
        Assinado
      </span>
    );
  }

  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium',
        status === 'Finalizado'
          ? 'bg-blue-50 text-blue-700 ring-1 ring-blue-200'
          : 'bg-amber-50 text-amber-800 ring-1 ring-amber-200',
      )}
    >
      {status === 'Finalizado' ? 'Finalizado' : 'Rascunho'}
    </span>
  );
}
