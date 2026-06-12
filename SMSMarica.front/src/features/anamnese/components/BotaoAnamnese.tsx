import { useNavigate } from 'react-router-dom';
import { ClipboardList } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

type Props = {
  /** Id da solicitação (preferencial, quando a tela de origem o tem). */
  solicitacaoExameId?: string | null;
  /** Accession do pedido/exame (fluxo da linha do exame no PACS). */
  accessionNumber?: string | null;
  /** Mantido por compatibilidade de chamada; o nome real vem do contexto da tela. */
  pacienteNome?: string | null;
  /** 'compacto' = botão pequeno de linha de tabela; 'normal' = botão padrão. */
  variante?: 'compacto' | 'normal';
  className?: string;
};

/**
 * Botão de Anamnese — abre o questionário pré-exame (hoje: mamografia) da
 * solicitação. Preenchido/reaberto pela enfermagem/atendimento; consultado
 * pelo médico ao laudar. Navega por solicitação (preferência) ou accession.
 */
export function BotaoAnamnese({
  solicitacaoExameId,
  accessionNumber,
  variante = 'compacto',
  className,
}: Props) {
  const navigate = useNavigate();
  const habilitado = Boolean(solicitacaoExameId || accessionNumber?.trim());
  if (!habilitado) return null;

  function abrir() {
    const destino = solicitacaoExameId
      ? `/app/anamnese?solicitacaoId=${encodeURIComponent(solicitacaoExameId)}`
      : `/app/anamnese?accession=${encodeURIComponent(accessionNumber!.trim())}`;
    navigate(destino);
  }

  return (
    <button
      type="button"
      onClick={abrir}
      title="Anamnese do paciente (pré-exame)"
      className={cn(
        variante === 'compacto'
          ? 'inline-flex items-center gap-1 rounded-md border border-indigo-300 bg-indigo-50 px-2.5 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-100'
          : 'inline-flex items-center gap-1.5 rounded-md border border-indigo-300 bg-indigo-50 px-3 py-2 text-sm font-medium text-indigo-700 hover:bg-indigo-100',
        className,
      )}
    >
      <ClipboardList className={variante === 'compacto' ? 'h-3.5 w-3.5' : 'h-4 w-4'} />
      Anamnese
    </button>
  );
}
