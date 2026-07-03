import { Bug, HelpCircle, Lightbulb, Wrench } from 'lucide-react';
import type { TicketPrioridade, TicketStatus, TicketTipo } from '@/features/tickets/types';
import { ROTULO_PRIORIDADE, ROTULO_STATUS, ROTULO_TIPO } from '@/features/tickets/types';

const CLASSE_TIPO: Record<TicketTipo, string> = {
  Bug: 'bg-red-50 text-red-700 ring-red-600/20',
  Mudanca: 'bg-blue-50 text-blue-700 ring-blue-600/20',
  Sugestao: 'bg-amber-50 text-amber-700 ring-amber-600/20',
  Duvida: 'bg-violet-50 text-violet-700 ring-violet-600/20',
};

const ICONE_TIPO = {
  Bug,
  Mudanca: Wrench,
  Sugestao: Lightbulb,
  Duvida: HelpCircle,
} as const;

const CLASSE_STATUS: Record<TicketStatus, string> = {
  Aberto: 'bg-slate-100 text-slate-700 ring-slate-600/20',
  EmAnalise: 'bg-blue-50 text-blue-700 ring-blue-600/20',
  Concluido: 'bg-green-50 text-green-700 ring-green-600/20',
  Negado: 'bg-red-50 text-red-700 ring-red-600/20',
};

const CLASSE_PRIORIDADE: Record<TicketPrioridade, string> = {
  Baixa: 'bg-slate-100 text-slate-600 ring-slate-500/20',
  Normal: 'bg-slate-100 text-slate-700 ring-slate-600/20',
  Alta: 'bg-orange-50 text-orange-700 ring-orange-600/20',
};

const base = 'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset';

export function TipoBadge({ tipo }: { tipo: TicketTipo }) {
  const Icone = ICONE_TIPO[tipo];
  return (
    <span className={`${base} ${CLASSE_TIPO[tipo]}`}>
      <Icone className="h-3 w-3" />
      {ROTULO_TIPO[tipo]}
    </span>
  );
}

export function StatusTicketBadge({ status }: { status: TicketStatus }) {
  return <span className={`${base} ${CLASSE_STATUS[status]}`}>{ROTULO_STATUS[status]}</span>;
}

export function PrioridadeBadge({ prioridade }: { prioridade: TicketPrioridade }) {
  return <span className={`${base} ${CLASSE_PRIORIDADE[prioridade]}`}>{ROTULO_PRIORIDADE[prioridade]}</span>;
}
