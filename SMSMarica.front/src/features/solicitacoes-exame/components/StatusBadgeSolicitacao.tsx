import { cn } from '@/shared/lib/cn';
import type { StatusSolicitacao } from '@/features/solicitacoes-exame/types';

const ESTILOS: Record<StatusSolicitacao, string> = {
  Solicitada: 'bg-gray-100 text-gray-700 ring-1 ring-gray-200',
  Enviada: 'bg-sky-50 text-sky-700 ring-1 ring-sky-200',
  Agendada: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  EmExecucao: 'bg-amber-50 text-amber-800 ring-1 ring-amber-200',
  Realizada: 'bg-green-50 text-green-700 ring-1 ring-green-200',
  Laudada: 'bg-emerald-100 text-emerald-800 ring-1 ring-emerald-300',
  Cancelada: 'bg-red-50 text-red-700 ring-1 ring-red-200',
};

const ROTULOS: Record<StatusSolicitacao, string> = {
  Solicitada: 'Solicitada',
  Enviada: 'Enviada ao PACS',
  Agendada: 'Agendada',
  EmExecucao: 'Em execução',
  Realizada: 'Realizada',
  Laudada: 'Laudada',
  Cancelada: 'Cancelada',
};

export function StatusBadgeSolicitacao({ status }: { status: StatusSolicitacao }) {
  return (
    <span className={cn('inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium', ESTILOS[status])}>
      {ROTULOS[status]}
    </span>
  );
}
