import type { ReactNode } from 'react';
import { AlertTriangle, Lightbulb, Scale, ShieldCheck } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

type Tipo = 'dica' | 'atencao' | 'regra' | 'lgpd';

const ESTILO: Record<Tipo, { classe: string; icone: typeof Lightbulb; rotulo: string }> = {
  dica: { classe: 'border-sky-200 bg-sky-50 text-sky-900', icone: Lightbulb, rotulo: 'Dica' },
  atencao: { classe: 'border-amber-200 bg-amber-50 text-amber-900', icone: AlertTriangle, rotulo: 'Atenção' },
  regra: { classe: 'border-primary-200 bg-primary-50 text-primary-900', icone: ShieldCheck, rotulo: 'Regra do sistema' },
  lgpd: { classe: 'border-emerald-200 bg-emerald-50 text-emerald-900', icone: Scale, rotulo: 'LGPD' },
};

/**
 * Caixa de destaque. Usada com parcimônia: tudo destacado é nada destacado — `regra` é para o
 * que o sistema impõe (e a pessoa não consegue contornar), `atencao` para o que dá retrabalho.
 */
export function Callout({ tipo = 'dica', titulo, children }: { tipo?: Tipo; titulo?: string; children: ReactNode }) {
  const e = ESTILO[tipo];
  const Icone = e.icone;
  return (
    <div className={cn('max-w-3xl rounded-theme-md border px-4 py-3', e.classe)}>
      <div className="flex items-start gap-2.5">
        <Icone className="mt-0.5 h-4 w-4 shrink-0" />
        <div className="min-w-0 space-y-1 text-sm leading-relaxed">
          <p className="font-semibold">{titulo ?? e.rotulo}</p>
          <div className="[&_strong]:font-semibold">{children}</div>
        </div>
      </div>
    </div>
  );
}
