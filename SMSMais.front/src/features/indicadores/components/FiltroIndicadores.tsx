import { CalendarRange, Loader2, RefreshCw } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import type { FiltroIndicador } from '@/features/indicadores/types';

type Props = {
  filtro: FiltroIndicador;
  onChange: (filtro: FiltroIndicador) => void;
  onApurar: () => void;
  apurando: boolean;
};

/** Atalhos de período — o contrato é apurado por mês de competência e fechado por trimestre. */
const ATALHOS: { rotulo: string; calcular: () => { inicio: string; fim: string } }[] = [
  {
    rotulo: 'Mês passado',
    calcular: () => {
      const hoje = new Date();
      const ini = new Date(hoje.getFullYear(), hoje.getMonth() - 1, 1);
      const fim = new Date(hoje.getFullYear(), hoje.getMonth(), 0);
      return { inicio: iso(ini), fim: iso(fim) };
    },
  },
  {
    rotulo: 'Mês atual',
    calcular: () => {
      const hoje = new Date();
      const ini = new Date(hoje.getFullYear(), hoje.getMonth(), 1);
      return { inicio: iso(ini), fim: iso(hoje) };
    },
  },
  {
    rotulo: 'Último trimestre',
    calcular: () => {
      const hoje = new Date();
      const ini = new Date(hoje.getFullYear(), hoje.getMonth() - 3, 1);
      const fim = new Date(hoje.getFullYear(), hoje.getMonth(), 0);
      return { inicio: iso(ini), fim: iso(fim) };
    },
  },
];

function iso(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function FiltroIndicadores({ filtro, onChange, onApurar, apurando }: Props) {
  return (
    <div className="sticky top-0 z-10 mb-6 rounded-xl border border-slate-200 bg-white/95 p-4 shadow-sm backdrop-blur">
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-[160px]">
          <Campo label="De" htmlFor="ind-de">
            <Input
              type="date"
              value={filtro.inicio}
              onChange={(e) => onChange({ ...filtro, inicio: e.target.value })}
            />
          </Campo>
        </div>

        <div className="w-[160px]">
          <Campo label="Até" htmlFor="ind-ate">
            <Input
              type="date"
              value={filtro.fim}
              onChange={(e) => onChange({ ...filtro, fim: e.target.value })}
            />
          </Campo>
        </div>

        <Button onClick={onApurar} disabled={apurando}>
          {apurando ? (
            <>
              <Loader2 className="h-4 w-4 animate-spin" /> Apurando…
            </>
          ) : (
            <>
              <RefreshCw className="h-4 w-4" /> Apurar período
            </>
          )}
        </Button>
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <CalendarRange className="h-3.5 w-3.5 text-slate-400" />
        {ATALHOS.map((a) => (
          <button
            key={a.rotulo}
            type="button"
            onClick={() => onChange({ ...filtro, ...a.calcular() })}
            className="rounded-full border border-slate-200 px-3 py-1 text-xs text-slate-600 transition hover:border-slate-300 hover:text-slate-900"
          >
            {a.rotulo}
          </button>
        ))}
        <span className="ml-auto text-xs text-slate-400">
          A apuração consulta a base do hospital em tempo real — pode levar alguns minutos.
        </span>
      </div>
    </div>
  );
}
