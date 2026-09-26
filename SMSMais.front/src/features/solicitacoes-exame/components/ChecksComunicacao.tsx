import { AlertTriangle, Check, CheckCheck, CircleMinus, Clock3 } from 'lucide-react';
import type { ComunicacaoChip } from '@/features/solicitacoes-exame/types';

const ROTULO: Record<string, string> = {
  ConfirmacaoAgendamento: 'Confirmação de agendamento',
  ExameLiberado: 'Aviso "exame liberado"',
  LaudoPronto: 'Aviso "laudo pronto"',
  LembreteAgendamento: 'Lembrete de agendamento',
  CancelamentoAgendamento: 'Aviso de cancelamento',
  ReforcoConfirmacao: 'Reforço da confirmação',
  OrientacaoPosto: 'Orientação ao posto',
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
  /// O histórico traz TODAS as finalidades — rótulo desconhecido cai no genérico, nunca no errado.
  finalidade: string;
}) {
  if (!chip) return null;
  const rotulo = ROTULO[finalidade] ?? 'Comunicação ao paciente';

  if (chip.status === 'AguardandoCorrecaoContato') {
    return (
      <span
        title={`${rotulo}: NÚMERO INVÁLIDO — quem atende disse que não conhece o paciente. Atualize o telefone.`}
        className="inline-flex"
      >
        <AlertTriangle className="h-4 w-4 shrink-0 text-red-600" />
      </span>
    );
  }
  if (chip.status === 'AguardandoVerificacaoCadastral' || chip.status === 'AguardandoTelefoneVerificado') {
    return (
      <span
        title={`${rotulo}: aguardando o paciente se identificar pelo WhatsApp (número ainda não verificado)`}
        className="inline-flex"
      >
        <Clock3 className="h-3.5 w-3.5 shrink-0 text-amber-500" />
      </span>
    );
  }
  // Terminais sem erro: uma pessoa assumiu, ou outra mensagem da mesma solicitação cobriu esta.
  // Sem este ramo cairiam no reloginho de "na fila" — que promete um envio que não vai acontecer.
  if (chip.status === 'SubstituidaPorAtendente' || chip.status === 'Dispensada') {
    return (
      <span
        title={`${rotulo}: ${chip.status === 'Dispensada' ? 'dispensada' : 'atendida por pessoa'}${chip.motivo ? ` — ${chip.motivo}` : ''}`}
        className="inline-flex"
      >
        <CircleMinus className="h-3.5 w-3.5 shrink-0 text-gray-400" />
      </span>
    );
  }
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
