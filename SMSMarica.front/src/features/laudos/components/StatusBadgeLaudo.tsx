import { cn } from '@/shared/lib/cn';
import type { StatusLaudo } from '@/features/laudos/types';

type Props = { status: StatusLaudo };

export function StatusBadgeLaudo({ status }: Props) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium',
        status === 'Finalizado'
          ? 'bg-green-50 text-green-700 ring-1 ring-green-200'
          : 'bg-amber-50 text-amber-800 ring-1 ring-amber-200',
      )}
    >
      {status === 'Finalizado' ? 'Finalizado' : 'Rascunho'}
    </span>
  );
}
