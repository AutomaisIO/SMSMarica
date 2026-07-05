import { AlertTriangle, Check, CheckCheck, Clock3 } from 'lucide-react';
import type { ComunicacaoChip } from '@/features/solicitacoes-exame/types';

const ROTULO: Record<string, string> = {
  ExameLiberado: 'Aviso "exame liberado"',
  LaudoPronto: 'Aviso "laudo pronto"',
};

/**
 * Checks estilo WhatsApp para a comunicação ao paciente:
 *   ✓ cinza  = enviado · ✓✓ cinza = entregue · ✓✓ AZUL = lida/visualizada · ⚠ = falha/sem número.
 * Pendente (na fila) mostra um reloginho discreto. null = sem comunicação (nada).
 */
export function ChecksComunicacao({
  chip,
  finalidade,
}: {
  chip: ComunicacaoChip | null | undefined;
  finalidade: 'ExameLiberado' | 'LaudoPronto';
}) {
  if (!chip) return null;
  const rotulo = ROTULO[finalidade];

  if (chip.status === 'Falha' || chip.status === 'SemTelefoneValido') {
    return (
      <span title={`${rotulo}: falha${chip.motivo ? ` — ${chip.motivo}` : ''}`} className="inline-flex">
        <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" />
      </span>
    );
  }
  if (chip.visualizado || chip.status === 'Lida') {
    return (
      <span title={`${rotulo}: ${chip.visualizado ? 'visualizada pelo paciente' : 'lida'}`} className="inline-flex">
        <CheckCheck className="h-4 w-4 shrink-0 text-sky-500" />
      </span>
    );
  }
  if (chip.status === 'Entregue') {
    return (
      <span title={`${rotulo}: entregue`} className="inline-flex">
        <CheckCheck className="h-4 w-4 shrink-0 text-gray-400" />
      </span>
    );
  }
  if (chip.status === 'Enviada') {
    return (
      <span title={`${rotulo}: enviada`} className="inline-flex">
        <Check className="h-4 w-4 shrink-0 text-gray-400" />
      </span>
    );
  }
  // Pendente — na fila (ex.: aguardando template aprovado na Meta).
  return (
    <span title={`${rotulo}: na fila de envio`} className="inline-flex">
      <Clock3 className="h-3.5 w-3.5 shrink-0 text-gray-300" />
    </span>
  );
}
