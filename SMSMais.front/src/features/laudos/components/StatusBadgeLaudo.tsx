import { ShieldCheck, Stamp } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import type { StatusLaudo } from '@/features/laudos/types';

type Props = { status: StatusLaudo; assinado?: boolean; semCertificado?: boolean };

/**
 * Badge de estado do laudo. A régua de cor reflete o que importa juridicamente:
 * - **Assinado** (verde + cadeado): laudo com assinatura digital ICP-Brasil válida.
 * - **Carimbado** (âmbar + carimbo): liberado pelo médico sem certificado — oficial, sem ICP-Brasil.
 * - **Finalizado** (azul): fechado, mas ainda sem assinatura.
 * - **Rascunho** (âmbar): em edição.
 */
export function StatusBadgeLaudo({ status, assinado = false, semCertificado = false }: Props) {
  // Liberado só com carimbo (médico sem certificado, ADR-0061): oficial, mas SEM ICP-Brasil.
  // Não pode ter a mesma cara do assinado digitalmente.
  if (assinado && semCertificado) {
    return (
      <span
        className="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-800 ring-1 ring-amber-200"
        title="Liberado com o carimbo do médico, sem assinatura digital ICP-Brasil"
      >
        <Stamp className="h-3.5 w-3.5" />
        Carimbado
      </span>
    );
  }

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
