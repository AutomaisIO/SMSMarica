import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { BellDot, Check, ExternalLink } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useReconhecerTicket, useResumoAutorTickets } from '@/features/tickets/api/queries';
import { StatusTicketBadge } from '@/features/tickets/components/badges';

/**
 * Modal global que "explode" no centro da tela quando a equipe respondeu/concluiu um ticket do
 * usuário e ele ainda não reconheceu. Mostra o status e o que foi respondido, e obriga uma ação:
 * "Marcar como visto" (baixa a bandeira) ou "Ver ticket inteiro" (abre o detalhe — que também
 * reconhece no backend). O dado vem do resumo do autor, com polling leve (~20s), então aparece
 * quase em tempo real em qualquer tela do painel.
 *
 * Fecha (X/Esc/fundo) apenas adia o ticket nesta sessão — ele reaparece no próximo acesso
 * enquanto não for reconhecido. Uma pendência de cada vez; ao resolver uma, a próxima surge.
 */
export function ModalTicketRespondido() {
  const { data } = useResumoAutorTickets();
  const reconhecer = useReconhecerTicket();
  const navigate = useNavigate();
  const [adiados, setAdiados] = useState<Set<string>>(() => new Set());

  const pendentes = (data?.pendentes ?? []).filter((p) => !adiados.has(p.id));
  const atual = pendentes[0];

  if (!atual) return null;

  const restantes = pendentes.length;

  async function marcarVisto() {
    try {
      await reconhecer.mutateAsync(atual.id);
      // O onSuccess da mutação invalida o resumo; o refetch remove esta pendência da lista.
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  function verTicket() {
    // Abrir o detalhe já reconhece a resposta no backend (baixa a bandeira).
    setAdiados((s) => new Set(s).add(atual.id));
    navigate(`/app/tickets/${atual.id}`);
  }

  function adiar() {
    setAdiados((s) => new Set(s).add(atual.id));
  }

  return (
    <Modal
      aberto
      aoFechar={adiar}
      titulo="A equipe respondeu seu ticket"
      descricao="Veja o retorno abaixo e confirme a leitura."
      largura="md"
    >
      <div className="space-y-4">
        <div className="flex items-start gap-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 ring-1 ring-red-200">
          <BellDot className="mt-0.5 h-4 w-4 flex-shrink-0" />
          <span>
            {restantes > 1
              ? `Você tem ${restantes} tickets com resposta nova. Veja um de cada vez.`
              : 'Há uma resposta nova para você conferir.'}
          </span>
        </div>

        <div className="rounded-xl border border-slate-200 p-4">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-normal text-slate-400">#{atual.numero}</span>
            <StatusTicketBadge status={atual.status} />
          </div>
          <h3 className="mt-1.5 font-semibold text-slate-800">{atual.titulo}</h3>

          {atual.respostaFinal ? (
            <div className="mt-3 rounded-lg border border-green-200 bg-green-50 p-3">
              <p className="text-xs font-semibold text-green-800">Retorno da equipe</p>
              <p className="mt-1 whitespace-pre-wrap text-sm text-green-900">{atual.respostaFinal}</p>
            </div>
          ) : (
            <p className="mt-3 text-sm text-slate-600">
              A equipe respondeu neste ticket. Abra o ticket para ver a conversa completa.
            </p>
          )}
        </div>

        <div className="flex flex-wrap justify-end gap-2 pt-1">
          <Button variante="outline" onClick={verTicket}>
            <ExternalLink className="h-4 w-4" />
            Ver ticket inteiro
          </Button>
          <Button onClick={marcarVisto} disabled={reconhecer.isPending}>
            <Check className="h-4 w-4" />
            {reconhecer.isPending ? 'Confirmando…' : 'Marcar como visto'}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
