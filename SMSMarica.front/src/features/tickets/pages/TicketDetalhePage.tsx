import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Archive, ArchiveRestore, ArrowLeft, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Select } from '@/shared/ui/Select';
import { notificar } from '@/shared/ui/Notificacoes';
import { formatarInstante } from '@/shared/lib/datas';
import {
  useArquivar,
  useAtualizarGestao,
  useExcluirTicket,
  useTicket,
} from '@/features/tickets/api/queries';
import { AnexosGaleria } from '@/features/tickets/components/AnexosInput';
import { ConversaTicket } from '@/features/tickets/components/ConversaTicket';
import { PrioridadeBadge, StatusTicketBadge, TipoBadge } from '@/features/tickets/components/badges';
import type { TicketPrioridade, TicketStatus } from '@/features/tickets/types';
import { ROTULO_PRIORIDADE, ROTULO_STATUS } from '@/features/tickets/types';

const STATUS: TicketStatus[] = ['Aberto', 'EmAnalise', 'Concluido', 'Negado'];
const PRIORIDADES: TicketPrioridade[] = ['Baixa', 'Normal', 'Alta'];

export function TicketDetalhePage({ gestao = false }: { gestao?: boolean }) {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const { data: ticket, isLoading, isError } = useTicket(id, gestao);
  const arquivar = useArquivar(gestao);
  const excluir = useExcluirTicket();
  const [confirmarExcluir, setConfirmarExcluir] = useState(false);

  if (isLoading) return <p className="p-6 text-sm text-slate-500">Carregando…</p>;
  if (isError || !ticket) return <p className="p-6 text-sm text-red-600">Ticket não encontrado.</p>;

  const arquivado = gestao ? ticket.arquivadoPeloAdmin : ticket.arquivadoPeloAutor;

  async function alternarArquivo() {
    try {
      await arquivar.mutateAsync({ id, arquivar: !arquivado });
      notificar(arquivado ? 'Ticket desarquivado.' : 'Ticket arquivado.', 'sucesso');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  async function confirmarExclusao() {
    try {
      await excluir.mutateAsync(id);
      notificar('Ticket excluído.', 'sucesso');
      navigate('/app/tickets/gestao');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <button
        onClick={() => navigate(gestao ? '/app/tickets/gestao' : '/app/tickets')}
        className="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-800"
      >
        <ArrowLeft className="h-4 w-4" /> Voltar
      </button>

      <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex flex-wrap items-center gap-2">
          <TipoBadge tipo={ticket.tipo} />
          <StatusTicketBadge status={ticket.status} />
          <PrioridadeBadge prioridade={ticket.prioridade} />
          {arquivado && <span className="text-xs text-slate-400">(arquivado)</span>}
        </div>
        <h1 className="mt-3 text-xl font-semibold text-slate-800">{ticket.titulo}</h1>
        <p className="mt-1 text-xs text-slate-500">
          Aberto por {ticket.autorNome ?? 'usuário'} · {formatarInstante(ticket.criadoEm)}
        </p>
        <p className="mt-4 whitespace-pre-wrap text-sm text-slate-800">{ticket.descricao}</p>
        {ticket.anexos.length > 0 && (
          <div className="mt-4">
            <AnexosGaleria anexos={ticket.anexos} />
          </div>
        )}

        <div className="mt-4 flex flex-wrap gap-2">
          <Button variante="outline" tamanho="sm" onClick={alternarArquivo} disabled={arquivar.isPending}>
            {arquivado ? <ArchiveRestore className="h-4 w-4" /> : <Archive className="h-4 w-4" />}
            {arquivado ? 'Desarquivar' : 'Arquivar'}
          </Button>
          {gestao && (
            <Button variante="danger" tamanho="sm" onClick={() => setConfirmarExcluir(true)}>
              <Trash2 className="h-4 w-4" /> Excluir
            </Button>
          )}
        </div>
      </div>

      {/* Retorno da equipe (visível ao autor) */}
      {ticket.respostaFinal && (
        <div className="rounded-xl border border-green-200 bg-green-50 p-4">
          <h3 className="text-sm font-semibold text-green-800">Retorno da equipe</h3>
          <p className="mt-1 whitespace-pre-wrap text-sm text-green-900">{ticket.respostaFinal}</p>
        </div>
      )}

      {gestao && <PainelTriagem ticket={ticket} id={id} />}

      <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <ConversaTicket ticketId={id} gestao={gestao} comentarios={ticket.comentarios} />
      </div>

      <ConfirmDialog
        aberto={confirmarExcluir}
        titulo="Excluir ticket"
        mensagem="Esta ação remove o ticket da gestão. Deseja continuar?"
        rotuloConfirmar="Excluir"
        destrutivo
        carregando={excluir.isPending}
        aoConfirmar={confirmarExclusao}
        aoCancelar={() => setConfirmarExcluir(false)}
      />
    </div>
  );
}

function PainelTriagem({ ticket, id }: { ticket: { status: TicketStatus; prioridade: TicketPrioridade; respostaFinal: string | null }; id: string }) {
  const [status, setStatus] = useState<TicketStatus>(ticket.status);
  const [prioridade, setPrioridade] = useState<TicketPrioridade>(ticket.prioridade);
  const [resposta, setResposta] = useState(ticket.respostaFinal ?? '');
  const atualizar = useAtualizarGestao(id);

  useEffect(() => {
    setStatus(ticket.status);
    setPrioridade(ticket.prioridade);
    setResposta(ticket.respostaFinal ?? '');
  }, [ticket.status, ticket.prioridade, ticket.respostaFinal]);

  const exigeResposta = (status === 'Concluido' || status === 'Negado') && !resposta.trim();
  const rotuloResposta = status === 'Negado' ? 'Justificativa (por que foi negado)' : 'Feedback ao concluir';

  async function salvar() {
    try {
      await atualizar.mutateAsync({
        status,
        prioridade,
        respostaFinal: resposta.trim() || undefined,
      });
      notificar('Ticket atualizado.', 'sucesso');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-slate-50 p-5">
      <h3 className="text-sm font-semibold text-slate-700">Triagem</h3>
      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        <Campo label="Status" htmlFor="tri-status">
          <Select id="tri-status" value={status} onChange={(e) => setStatus(e.target.value as TicketStatus)}>
            {STATUS.map((s) => (
              <option key={s} value={s}>{ROTULO_STATUS[s]}</option>
            ))}
          </Select>
        </Campo>
        <Campo label="Prioridade" htmlFor="tri-prioridade">
          <Select id="tri-prioridade" value={prioridade} onChange={(e) => setPrioridade(e.target.value as TicketPrioridade)}>
            {PRIORIDADES.map((p) => (
              <option key={p} value={p}>{ROTULO_PRIORIDADE[p]}</option>
            ))}
          </Select>
        </Campo>
      </div>
      <Campo
        label={rotuloResposta}
        htmlFor="tri-resposta"
        className="mt-3"
        dica={
          status === 'Concluido' || status === 'Negado'
            ? 'Obrigatório para concluir ou negar — este texto é mostrado ao autor.'
            : 'Opcional. Mostrado ao autor como "Retorno da equipe".'
        }
      >
        <textarea
          id="tri-resposta"
          value={resposta}
          rows={3}
          maxLength={5000}
          onChange={(e) => setResposta(e.target.value)}
          className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-red-500 focus:outline-none focus:ring-1 focus:ring-red-500"
        />
      </Campo>
      <div className="mt-3 flex justify-end">
        <Button onClick={salvar} disabled={atualizar.isPending || exigeResposta} tamanho="sm">
          {atualizar.isPending ? 'Salvando…' : 'Salvar triagem'}
        </Button>
      </div>
    </div>
  );
}
