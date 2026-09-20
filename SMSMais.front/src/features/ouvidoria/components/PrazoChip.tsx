import { CalendarClock } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { formatarWallClock } from '@/shared/lib/datas';
import { diasAte } from '@/features/ouvidoria/lib/rotulos';
import { ehStatusFinal } from '@/features/ouvidoria/lib/regras';
import type { OuvidoriaStatus } from '@/features/ouvidoria/types';

type Props = {
  /** Data-limite "aaaa-mm-dd". */
  prazo: string | null | undefined;
  /** Com o status, o chip fica neutro em manifestação já encerrada. */
  status?: OuvidoriaStatus;
  /** Rótulo curto antes da data (ex.: "Cidadão", "Área"). */
  rotulo?: string;
  className?: string;
};

/** Verde no prazo · amarelo ≤ 5 dias · vermelho atrasada — sempre com os dias e a data. */
export function PrazoChip({ prazo, status, rotulo, className }: Props) {
  const dias = diasAte(prazo);
  if (dias === null) return <span className="text-xs text-slate-400">—</span>;

  const encerrada = status ? ehStatusFinal(status) || status === 'Respondida' : false;

  let classe = 'bg-green-50 text-green-700 ring-green-600/20';
  let texto = dias === 0 ? 'vence hoje' : dias === 1 ? 'vence amanhã' : `${dias} dias`;
  if (encerrada) {
    classe = 'bg-slate-100 text-slate-600 ring-slate-500/20';
    texto = 'encerrada';
  } else if (dias < 0) {
    classe = 'bg-red-50 text-red-700 ring-red-600/20';
    texto = `atrasada ${-dias} ${-dias === 1 ? 'dia' : 'dias'}`;
  } else if (dias <= 5) {
    classe = 'bg-amber-50 text-amber-800 ring-amber-600/20';
  }

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset',
        classe,
        className,
      )}
      title={`Prazo: ${formatarWallClock(prazo)}`}
    >
      <CalendarClock className="h-3 w-3" aria-hidden="true" />
      {rotulo ? <span className="font-normal opacity-80">{rotulo}:</span> : null}
      {formatarWallClock(prazo)} · {texto}
    </span>
  );
}
