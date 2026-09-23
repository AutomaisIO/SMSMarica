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
  /** Só o ícone (sem rótulo), para linhas de tabela densas — função no tooltip. */
  iconeApenas?: boolean;
  /**
   * Fora da tela de Solicitações a anamnese abre só para leitura e o botão fica
   * desabilitado quando não há anamnese salva (tooltip "Sem anamnese").
   */
  somenteLeitura?: boolean;
  /**
   * Anamnese já preenchida (vem no DTO da listagem — sem request extra por linha).
   * Muda a cor do botão: esmeralda = preenchida, indigo = pendente.
   */
  temAnamnese?: boolean;
  /**
   * Protocolo da requisição no SISCAN (vem no DTO da listagem). Presente = já foi enviada, e o
   * botão muda de cor: é a pergunta que se faz olhando a fila, sem abrir uma a uma.
   */
  siscanProtocolo?: string | null;
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
  iconeApenas = false,
  somenteLeitura = false,
  temAnamnese: temAnamneseProp,
  siscanProtocolo,
  className,
}: Props) {
  const navigate = useNavigate();
  const habilitado = Boolean(solicitacaoExameId || accessionNumber?.trim());

  // Em modo leitura, descobrimos se já existe anamnese salva para habilitar o
  // botão. Em edição (tela Solicitações) o botão é sempre ativo (é onde se cria)
  // e a flag vem pronta no DTO da lista — sem uma request por linha da tabela.
  const contexto = useContextoAnamnese({
    solicitacaoExameId: somenteLeitura ? (solicitacaoExameId ?? undefined) : undefined,
    accessionNumber: somenteLeitura ? (accessionNumber?.trim() || undefined) : undefined,
  });
  const temAnamnese = temAnamneseProp ?? Boolean(contexto.data?.anamnese);
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

  const enviadaAoSiscan = Boolean(siscanProtocolo);

  const titulo = semAnamnese
    ? 'Sem anamnese'
    : enviadaAoSiscan
      ? `Enviada ao SISCAN — protocolo ${siscanProtocolo} (somente leitura)`
      : temAnamnese
        ? 'Anamnese preenchida — clique para ver/editar'
        : 'Anamnese do paciente (pré-exame)';

  // Três estados, e a ordem importa: enviada ao SISCAN vence "preenchida", porque é o que muda
  // o que dá para FAZER com ela — dali em diante é só leitura.
  const corIcone = enviadaAoSiscan
    ? 'text-teal-600 hover:text-teal-800'
    : temAnamnese
      ? 'text-emerald-600 hover:text-emerald-800'
      : 'text-indigo-600 hover:text-indigo-800';

  const corBotao = enviadaAoSiscan
    ? 'border-teal-300 bg-teal-50 text-teal-700 hover:bg-teal-100 disabled:hover:bg-teal-50'
    : temAnamnese
      ? 'border-emerald-300 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 disabled:hover:bg-emerald-50'
      : 'border-indigo-300 bg-indigo-50 text-indigo-700 hover:bg-indigo-100 disabled:hover:bg-indigo-50';

  if (iconeApenas) {
    return (
      <button
        type="button"
        onClick={abrir}
        disabled={semAnamnese}
        title={titulo}
        aria-label="Anamnese do paciente"
        className={cn(
          'inline-flex items-center rounded p-0.5 transition-colors',
          corIcone,
          'disabled:cursor-not-allowed disabled:text-gray-300',
          className,
        )}
      >
        <ClipboardList className="h-3.5 w-3.5" />
      </button>
    );
  }

  return (
    <button
      type="button"
      onClick={abrir}
      disabled={semAnamnese}
      title={titulo}
      className={cn(
        variante === 'compacto'
          ? 'inline-flex items-center gap-1 rounded-md border px-2.5 py-1 text-xs font-medium'
          : 'inline-flex items-center gap-1.5 rounded-md border px-3 py-2 text-sm font-medium',
        corBotao,
        'disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
    >
      <ClipboardList className={variante === 'compacto' ? 'h-3.5 w-3.5' : 'h-4 w-4'} />
      Anamnese
    </button>
  );
}
