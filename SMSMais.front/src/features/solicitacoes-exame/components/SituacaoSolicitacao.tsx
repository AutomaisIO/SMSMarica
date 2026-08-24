import { cn } from '@/shared/lib/cn';
import type { StatusConfirmacaoPaciente, StatusSolicitacao } from '@/features/solicitacoes-exame/types';

type Situacao = { rotulo: string; classe: string };

/** Status operacionais em que a "situação de fora" cede lugar ao status real (o exame já andou). */
const OPERACIONAL = new Set<StatusSolicitacao>(['Recebida', 'EmExecucao', 'Realizada', 'Laudada', 'Cancelada', 'Agendada']);

const HORA_MS = 60 * 60 * 1000;
const DIA_MS = 24 * HORA_MS;
/** Brasília é UTC-3 FIXO (regra única do sistema — ver shared/lib/datas.ts). */
const OFFSET_BRASILIA_MS = 3 * HORA_MS;

/**
 * "18h de Brasília do dia do exame", como instante em ms UTC — SEM depender do fuso do
 * navegador: converte o instante do exame para wall-clock de Brasília (-3h), acha a meia-noite
 * desse dia e soma 18h, voltando para UTC (+3h).
 */
function dezoitoHorasBrasiliaDoDia(exameMs: number): number {
  const brasiliaMs = exameMs - OFFSET_BRASILIA_MS;
  const meiaNoiteBrasilia = Math.floor(brasiliaMs / DIA_MS) * DIA_MS;
  return meiaNoiteBrasilia + 18 * HORA_MS + OFFSET_BRASILIA_MS;
}

/**
 * Situação "de fora" (o que a recepção vê na lista), derivada de confirmação + autorização +
 * tempo. Retorna null quando o exame já chegou/andou — aí usa o StatusBadge operacional.
 * Prioridade: cancelado → falha → autorizado → falta → atrasado → confirmado → aguardando.
 * Falta = passou das 18h (Brasília) do dia do exame E o exame já estourou 1h de atraso —
 * a 2ª condição evita marcar "Falta" antes da hora em exames agendados após as 18h.
 */
export function derivarSituacao(
  s: {
    status: StatusSolicitacao;
    statusConfirmacao: StatusConfirmacaoPaciente;
    autorizadoEm: string | null;
    erroIntegracaoPacs: string | null;
    dataAgendada: string | null;
  },
  agoraMs: number = Date.now(),
): Situacao | null {
  if (OPERACIONAL.has(s.status)) return null;

  if (s.statusConfirmacao === 'Cancelada')
    return { rotulo: 'Cancelado', classe: 'bg-gray-100 text-gray-500 ring-1 ring-gray-200' };
  if (s.erroIntegracaoPacs)
    return { rotulo: 'Falha', classe: 'bg-red-50 text-red-700 ring-1 ring-red-200' };
  if (s.autorizadoEm)
    return { rotulo: 'Autorizado', classe: 'bg-orange-50 text-orange-700 ring-1 ring-orange-200' };

  if (s.dataAgendada) {
    const exameMs = Date.parse(s.dataAgendada);
    if (!Number.isNaN(exameMs)) {
      const atrasado = agoraMs > exameMs + HORA_MS;
      if (atrasado && agoraMs > dezoitoHorasBrasiliaDoDia(exameMs))
        return { rotulo: 'Falta', classe: 'bg-purple-50 text-purple-700 ring-1 ring-purple-200' };
      // Rose (não azul): alerta visual e distante de Recebida (indigo) e Agendada (blue),
      // que dividem a mesma coluna na lista.
      if (atrasado)
        return { rotulo: 'Atrasado', classe: 'bg-rose-100 text-rose-700 ring-1 ring-rose-300' };
    }
  }

  if (s.statusConfirmacao === 'Confirmada')
    return { rotulo: 'Confirmado', classe: 'bg-amber-50 text-amber-800 ring-1 ring-amber-200' };

  return { rotulo: 'Aguardando', classe: 'bg-gray-100 text-gray-600 ring-1 ring-gray-200' };
}

export function SituacaoBadge({ situacao }: { situacao: Situacao }) {
  return (
    <span className={cn('inline-flex items-center whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium', situacao.classe)}>
      {situacao.rotulo}
    </span>
  );
}
