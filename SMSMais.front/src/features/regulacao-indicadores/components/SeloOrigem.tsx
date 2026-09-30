import { cn } from '@/shared/lib/cn';
import { rotuloSelo, SELOS } from '../lib/indicadores';
import type { Selo } from '../types';

const ESTILO: Record<Selo, string> = {
  Oficial: 'bg-blue-50 text-blue-800',
  Calculado: 'bg-gray-100 text-gray-700',
  Parcial: 'bg-amber-100 text-amber-800',
  Indisponivel: 'border border-dashed border-gray-300 bg-gray-50 text-gray-500',
};

/** Selo de origem de um número: Oficial, Calculado, Parcial ou Indisponível. */
export function SeloOrigem({ selo, className }: { selo: Selo; className?: string }) {
  const descricao = SELOS.find((s) => s.selo === selo)?.descricao;
  return (
    <span
      title={descricao}
      className={cn(
        'inline-flex items-center whitespace-nowrap rounded-full px-2 py-0.5 text-[11px] font-semibold leading-tight',
        ESTILO[selo] ?? ESTILO.Calculado,
        className,
      )}
    >
      {rotuloSelo(selo)}
    </span>
  );
}

/** Legenda dos quatro selos — o que cada um promete sobre o número. */
export function LegendaSelos() {
  return (
    <dl className="grid gap-x-8 gap-y-2 sm:grid-cols-2">
      {SELOS.map((s) => (
        <div key={s.selo} className="flex items-baseline gap-3">
          <dt className="w-24 shrink-0">
            <SeloOrigem selo={s.selo} />
          </dt>
          <dd className="text-xs leading-relaxed text-gray-600">{s.descricao}</dd>
        </div>
      ))}
    </dl>
  );
}
