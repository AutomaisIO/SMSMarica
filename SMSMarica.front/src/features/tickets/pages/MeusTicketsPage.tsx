import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { BellDot, Check, LifeBuoy, MessageSquare, Plus } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { notificar } from '@/shared/ui/Notificacoes';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante } from '@/shared/lib/datas';
import { useMeusTickets, useReconhecerTicket } from '@/features/tickets/api/queries';
import { AbrirTicketModal } from '@/features/tickets/components/AbrirTicketModal';
import { StatusTicketBadge, TipoBadge } from '@/features/tickets/components/badges';
import type { TicketListItem } from '@/features/tickets/types';

export function MeusTicketsPage() {
  const [incluirArquivados, setIncluirArquivados] = useState(false);
  const [modalAberto, setModalAberto] = useState(false);
  const navigate = useNavigate();
  const { data: tickets = [], isLoading } = useMeusTickets(incluirArquivados);
  const reconhecer = useReconhecerTicket();
  const totalNovos = tickets.filter((t) => t.respostaNaoReconhecida).length;

  async function aoReconhecer(id: string) {
    try {
      await reconhecer.mutateAsync(id);
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  const colunas: Coluna<TicketListItem>[] = [
    {
      chave: 'numero',
      cabecalho: '#',
      className: 'whitespace-nowrap text-sm font-medium text-slate-500',
      render: (t) => `#${t.numero}`,
    },
    { chave: 'tipo', cabecalho: 'Tipo', render: (t) => <TipoBadge tipo={t.tipo} /> },
    {
      chave: 'titulo',
      cabecalho: 'Título',
      render: (t) => (
        <div className="flex items-center gap-2">
          <span className="font-medium text-slate-800">{t.titulo}</span>
          {t.respostaNaoReconhecida && (
            <span className="inline-flex items-center gap-1 rounded-full bg-red-50 px-2 py-0.5 text-xs font-semibold text-red-600 ring-1 ring-red-200">
              <BellDot className="h-3 w-3" />
              Respondido
            </span>
          )}
          {t.qtdComentarios > 0 && (
            <span className="inline-flex items-center gap-0.5 text-xs text-slate-400">
              <MessageSquare className="h-3 w-3" />
              {t.qtdComentarios}
            </span>
          )}
          {t.arquivado && <span className="text-xs text-slate-400">(arquivado)</span>}
        </div>
      ),
    },
    { chave: 'status', cabecalho: 'Status', render: (t) => <StatusTicketBadge status={t.status} /> },
    {
      chave: 'atualizado',
      cabecalho: 'Atualizado',
      className: 'whitespace-nowrap text-sm text-slate-500',
      render: (t) => formatarInstante(t.atualizadoEm ?? t.criadoEm),
    },
    {
      chave: 'acoes',
      cabecalho: '',
      className: 'whitespace-nowrap text-right',
      render: (t) =>
        t.respostaNaoReconhecida ? (
          <Button
            variante="outline"
            tamanho="sm"
            disabled={reconhecer.isPending}
            onClick={(e) => {
              e.stopPropagation();
              void aoReconhecer(t.id);
            }}
          >
            <Check className="h-4 w-4" />
            Reconhecer
          </Button>
        ) : null,
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-xl font-semibold text-slate-800">
            <LifeBuoy className="h-5 w-5 text-red-600" />
            Meus Tickets
          </h1>
          <p className="text-sm text-slate-500">Acompanhe seus bugs, mudanças, sugestões e dúvidas.</p>
        </div>
        <Button onClick={() => setModalAberto(true)}>
          <Plus className="h-4 w-4" />
          Abrir ticket
        </Button>
      </div>

      {totalNovos > 0 && (
        <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <BellDot className="h-4 w-4 flex-shrink-0" />
          <span>
            Você tem <strong>{totalNovos}</strong>{' '}
            {totalNovos === 1 ? 'ticket respondido' : 'tickets respondidos'} pela equipe. Abra o
            ticket para ver a resposta ou clique em <strong>Reconhecer</strong>.
          </span>
        </div>
      )}

      <label className="flex w-fit items-center gap-2 text-sm text-slate-600">
        <input
          type="checkbox"
          checked={incluirArquivados}
          onChange={(e) => setIncluirArquivados(e.target.checked)}
        />
        Mostrar arquivados
      </label>

      <Tabela
        colunas={colunas}
        dados={tickets}
        chaveLinha={(t) => t.id}
        carregando={isLoading}
        aoClicarLinha={(t) => navigate(`/app/tickets/${t.id}`)}
        dicaLinha="Abrir ticket"
        vazio={
          <div className="py-8 text-center text-sm text-slate-400">
            Você ainda não abriu nenhum ticket. Clique em <strong>Abrir ticket</strong> para começar.
          </div>
        }
      />

      <AbrirTicketModal
        aberto={modalAberto}
        aoFechar={() => setModalAberto(false)}
        aoCriar={(id) => navigate(`/app/tickets/${id}`)}
      />
    </div>
  );
}
