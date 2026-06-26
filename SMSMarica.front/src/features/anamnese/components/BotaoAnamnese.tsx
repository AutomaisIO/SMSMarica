import { useNavigate } from 'react-router-dom';
import { ClipboardList } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { useContextoAnamnese } from '@/features/anamnese/api/queries';

type Props = {
  /** Id da solicitação (preferencial, quando a tela de origem o tem). */
  solicitacaoExameId?: string | null;
  /** Accession do pedido/exame (fluxo da linha do exame no PACS). */
  accessionNumber?: string | null;
  /** Mantido por compatibilidade de chamada; o nome real vem do contexto da tela. */
  pacienteNome?: string | null;
  /** 'compacto' = botão pequeno de linha de tabela; 'normal' = botão padrão. */
  variante?: 'compacto' | 'normal';
  /**
   * Fora da tela de Solicitações a anamnese abre só para leitura e o botão fica
   * desabilitado quando não há anamnese salva (tooltip "Sem anamnese").
   */
  somenteLeitura?: boolean;
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
  somenteLeitura = false,
  className,
}: Props) {
  const navigate = useNavigate();
  const habilitado = Boolean(solicitacaoExameId || accessionNumber?.trim());

  // Em modo leitura, descobrimos se já existe anamnese salva para habilitar o
  // botão. Em edição (tela Solicitações) o botão é sempre ativo (é onde se cria).
  const contexto = useContextoAnamnese({
    solicitacaoExameId: somenteLeitura ? (solicitacaoExameId ?? undefined) : undefined,
    accessionNumber: somenteLeitura ? (accessionNumber?.trim() || undefined) : undefined,
  });
  const temAnamnese = Boolean(contexto.data?.anamnese);
  const semAnamnese = somenteLeitura && contexto.isSuccess && !temAnamnese;

  if (!habilitado) return null;

  function abrir() {
    if (semAnamnese) return;
    const sufixo = somenteLeitura ? '&leitura=1' : '';
    const destino = solicitacaoExameId
      ? `/app/anamnese?solicitacaoId=${encodeURIComponent(solicitacaoExameId)}${sufixo}`
      : `/app/anamnese?accession=${encodeURIComponent(accessionNumber!.trim())}${sufixo}`;
    navigate(destino);
  }

  return (
    <button
      type="button"
      onClick={abrir}
      disabled={semAnamnese}
      title={semAnamnese ? 'Sem anamnese' : 'Anamnese do paciente (pré-exame)'}
      className={cn(
        variante === 'compacto'
          ? 'inline-flex items-center gap-1 rounded-md border border-indigo-300 bg-indigo-50 px-2.5 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-100'
          : 'inline-flex items-center gap-1.5 rounded-md border border-indigo-300 bg-indigo-50 px-3 py-2 text-sm font-medium text-indigo-700 hover:bg-indigo-100',
        'disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-indigo-50',
        className,
      )}
    >
      <ClipboardList className={variante === 'compacto' ? 'h-3.5 w-3.5' : 'h-4 w-4'} />
      Anamnese
    </button>
  );
}
