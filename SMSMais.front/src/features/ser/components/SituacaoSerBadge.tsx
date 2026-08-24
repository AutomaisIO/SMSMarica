import { cn } from '@/shared/lib/cn';
import { ROTULO_SITUACAO, type SituacaoSer } from '@/features/ser/types';

/**
 * Cores por situação. A régua não é decorativa: verde = resolvido, âmbar = esperando
 * ação de alguém, vermelho = fora do fluxo, azul = andando.
 */
const CLASSE: Record<SituacaoSer, string> = {
  EmFila: 'bg-amber-50 text-amber-700 border-amber-200',
  Pendente: 'bg-orange-50 text-orange-700 border-orange-200',
  Agendada: 'bg-blue-50 text-blue-700 border-blue-200',
  ChegadaNaoConfirmada: 'bg-sky-50 text-sky-700 border-sky-200',
  ChegadaConfirmada: 'bg-teal-50 text-teal-700 border-teal-200',
  Cancelada: 'bg-red-50 text-red-700 border-red-200',
  Alta: 'bg-green-50 text-green-700 border-green-200',
};

type Props = { situacao: SituacaoSer; className?: string };

export function SituacaoSerBadge({ situacao, className }: Props) {
  return (
    <span
      className={cn(
        'inline-flex items-center whitespace-nowrap rounded-full border px-2 py-0.5 text-xs font-medium',
        CLASSE[situacao],
        className,
      )}
    >
      {ROTULO_SITUACAO[situacao]}
    </span>
  );
}
