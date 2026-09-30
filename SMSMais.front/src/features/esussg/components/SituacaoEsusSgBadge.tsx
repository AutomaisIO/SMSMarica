import { cn } from '@/shared/lib/cn';
import {
  DICA_SITUACAO_ESUSSG,
  ROTULO_SITUACAO_ESUSSG,
  type SituacaoEsusSg,
} from '@/features/esussg/types';

/**
 * Cores por situação, na régua das telas irmãs (SER/SERNIT): âmbar = esperando, laranja =
 * pendência, azul = andando. "Saiu da fila" é cinza tracejado e NÃO vermelho: não sabemos se foi
 * cancelado — a conta do município não vê o motivo —, e vermelho afirmaria isso.
 */
const CLASSE: Record<SituacaoEsusSg, string> = {
  EmFila: 'bg-amber-50 text-amber-700 border-amber-200',
  Pendente: 'bg-orange-50 text-orange-700 border-orange-200',
  Agendada: 'bg-blue-50 text-blue-700 border-blue-200',
  SaiuDaFila: 'bg-slate-50 text-slate-600 border-slate-300 border-dashed',
};

type Props = { situacao: SituacaoEsusSg; className?: string };

export function SituacaoEsusSgBadge({ situacao, className }: Props) {
  return (
    <span
      title={DICA_SITUACAO_ESUSSG[situacao]}
      className={cn(
        'inline-flex items-center whitespace-nowrap rounded-full border px-2 py-0.5 text-xs font-medium',
        CLASSE[situacao],
        className,
      )}
    >
      {ROTULO_SITUACAO_ESUSSG[situacao]}
    </span>
  );
}
