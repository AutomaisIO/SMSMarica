import { cn } from '@/shared/lib/cn';
import type { StatusConfirmacaoPaciente, StatusSolicitacao } from '@/features/solicitacoes-exame/types';

type Situacao = { rotulo: string; classe: string };

/** Status operacionais em que a "situação de fora" cede lugar ao status real (o exame já andou). */
const OPERACIONAL = new Set<StatusSolicitacao>(['Recebida', 'EmExecucao', 'Realizada', 'Laudada', 'Cancelada', 'Agendada']);

/**
 * Situação "de fora" (o que a recepção vê na lista), derivada de confirmação + autorização +
 * tempo. Retorna null quando o exame já chegou/andou — aí usa o StatusBadge operacional.
 * Prioridade: cancelado → falha → autorizado → falta (após 18h) → atrasado (1h) → confirmado → aguardando.
 */
export function derivarSituacao(s: {
  status: StatusSolicitacao;
  statusConfirmacao: StatusConfirmacaoPaciente;
  autorizadoEm: string | null;
  erroIntegracaoPacs: string | null;
  dataAgendada: string | null;
}): Situacao | null {
  if (OPERACIONAL.has(s.status)) return null;

  const agora = Date.now();

  if (s.statusConfirmacao === 'Cancelada')
    return { rotulo: 'Cancelado', classe: 'bg-gray-100 text-gray-500 ring-1 ring-gray-200' };
  if (s.erroIntegracaoPacs)
    return { rotulo: 'Falha', classe: 'bg-red-50 text-red-700 ring-1 ring-red-200' };
  if (s.autorizadoEm)
    return { rotulo: 'Autorizado', classe: 'bg-orange-50 text-orange-700 ring-1 ring-orange-200' };

  if (s.dataAgendada) {
    const d = new Date(s.dataAgendada);
    if (!Number.isNaN(d.getTime())) {
      const dezoito = new Date(d);
      dezoito.setHours(18, 0, 0, 0);
      if (agora > dezoito.getTime())
        return { rotulo: 'Falta', classe: 'bg-purple-50 text-purple-700 ring-1 ring-purple-200' };
      if (agora > d.getTime() + 60 * 60 * 1000)
        return { rotulo: 'Atrasado', classe: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200' };
    }
  }

  if (s.statusConfirmacao === 'Confirmada')
    return { rotulo: 'Confirmado', classe: 'bg-amber-50 text-amber-800 ring-1 ring-amber-200' };

  return { rotulo: 'Aguardando', classe: 'bg-gray-100 text-gray-600 ring-1 ring-gray-200' };
}

export function SituacaoBadge({ situacao }: { situacao: Situacao }) {
  return (
    <span className={cn('inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium', situacao.classe)}>
      {situacao.rotulo}
    </span>
  );
}
