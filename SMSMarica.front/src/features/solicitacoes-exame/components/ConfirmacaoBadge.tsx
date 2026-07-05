import { CheckCircle2, XCircle } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import type { StatusConfirmacaoPaciente } from '@/features/solicitacoes-exame/types';

/** Ícone compacto ao lado do paciente: ✓ verde = confirmou, ✗ vermelho = não vai. Nada se pendente. */
export function ConfirmacaoIcone({ status }: { status: StatusConfirmacaoPaciente }) {
  if (status === 'Confirmada')
    return (
      <span title="Paciente confirmou a presença" className="inline-flex">
        <CheckCircle2 className="h-4 w-4 shrink-0 text-green-600" aria-label="Paciente confirmou" />
      </span>
    );
  if (status === 'Cancelada')
    return (
      <span title="Paciente informou que não vai comparecer" className="inline-flex">
        <XCircle className="h-4 w-4 shrink-0 text-red-600" aria-label="Paciente não vai comparecer" />
      </span>
    );
  return null;
}

const ESTILOS: Record<StatusConfirmacaoPaciente, string> = {
  Pendente: 'bg-amber-50 text-amber-800 ring-1 ring-amber-200',
  Confirmada: 'bg-green-50 text-green-700 ring-1 ring-green-200',
  Cancelada: 'bg-red-50 text-red-700 ring-1 ring-red-200',
};

const ROTULOS: Record<StatusConfirmacaoPaciente, string> = {
  Pendente: 'Sem resposta',
  Confirmada: 'Paciente confirmou',
  Cancelada: 'Paciente não vai',
};

/** Badge da resposta do paciente à notificação. Some ("—") quando pendente e compacto=true. */
export function ConfirmacaoBadge({
  status,
  compacto = false,
}: {
  status: StatusConfirmacaoPaciente;
  compacto?: boolean;
}) {
  if (compacto && status === 'Pendente') return <span className="text-xs text-gray-300">—</span>;
  return (
    <span className={cn('inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium', ESTILOS[status])}>
      {ROTULOS[status]}
    </span>
  );
}

const ROTULO_CANAL: Record<string, string> = {
  app: 'pelo aplicativo',
  'whatsapp-link': 'pelo link do WhatsApp',
  'whatsapp-quickreply': 'pelo WhatsApp',
  ligacao: 'por ligação',
  telefone: 'por ligação',
  sandbox: 'teste (sandbox)',
};

export function canalConfirmacaoTexto(canal: string | null): string {
  if (!canal) return '';
  return ROTULO_CANAL[canal] ?? `por ${canal}`;
}
