import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Inbox, MessageSquare, Settings2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { notificar } from '@/shared/ui/Notificacoes';
import { formatarInstante } from '@/shared/lib/datas';
import { usePermissao } from '@/shared/auth/authStore';
import {
  useAtualizarVisibilidade,
  useConfiguracaoTickets,
  useTodosTickets,
} from '@/features/tickets/api/queries';
import { PrioridadeBadge, StatusTicketBadge, TipoBadge } from '@/features/tickets/components/badges';
import type {
  TicketListItem,
  TicketStatus,
  TicketTipo,
  TicketVisibilidade,
} from '@/features/tickets/types';
import { ROTULO_STATUS, ROTULO_TIPO, ROTULO_VISIBILIDADE } from '@/features/tickets/types';

const STATUS: TicketStatus[] = ['Aberto', 'EmAnalise', 'Concluido', 'Negado'];
const TIPOS: TicketTipo[] = ['Bug', 'Mudanca', 'Sugestao', 'Duvida'];
const VISIBILIDADES: TicketVisibilidade[] = ['Privado', 'PorUnidade', 'Publico'];

export function GestaoTicketsPage() {
  const [incluirArquivados, setIncluirArquivados] = useState(false);
  const [fStatus, setFStatus] = useState<'' | TicketStatus>('');
  const [fTipo, setFTipo] = useState<'' | TicketTipo>('');
  const [busca, setBusca] = useState('');
  const navigate = useNavigate();

  const { data: tickets = [], isLoading } = useTodosTickets(incluirArquivados);
  const podeConfigurar = usePermissao('Ticket', 'Edicao');

  const filtrados = useMemo(() => {
    const b = busca.trim().toLowerCase();
    // Buscar por "#42" ou "42" acha o ticket pelo número exato.
    const numero = /^#?\d+$/.test(b) ? Number(b.replace('#', '')) : null;
    return tickets.filter(
      (t) =>
        (!fStatus || t.status === fStatus) &&
        (!fTipo || t.tipo === fTipo) &&
        (!b ||
          t.numero === numero ||
          t.titulo.toLowerCase().includes(b) ||
          (t.autorNome ?? '').toLowerCase().includes(b)),
    );
  }, [tickets, fStatus, fTipo, busca]);

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
    { chave: 'autor', cabecalho: 'Autor', className: 'text-sm text-slate-600', render: (t) => t.autorNome ?? '—' },
    { chave: 'prioridade', cabecalho: 'Prioridade', render: (t) => <PrioridadeBadge prioridade={t.prioridade} /> },
    { chave: 'status', cabecalho: 'Status', render: (t) => <StatusTicketBadge status={t.status} /> },
    {
      chave: 'atualizado',
      cabecalho: 'Atualizado',
      className: 'whitespace-nowrap text-sm text-slate-500',
      render: (t) => formatarInstante(t.atualizadoEm ?? t.criadoEm),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-xl font-semibold text-slate-800">
            <Inbox className="h-5 w-5 text-red-600" />
            Gestão de Tickets
          </h1>
          <p className="text-sm text-slate-500">Veja, responda e triê todos os tickets de suporte.</p>
        </div>
        {podeConfigurar && <VisibilidadeControle />}
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <div className="w-56">
          <Input placeholder="Buscar por nº, título ou autor…" value={busca} onChange={(e) => setBusca(e.target.value)} />
        </div>
        <Select value={fStatus} onChange={(e) => setFStatus(e.target.value as '' | TicketStatus)} className="w-40">
          <option value="">Todos os status</option>
          {STATUS.map((s) => (
            <option key={s} value={s}>{ROTULO_STATUS[s]}</option>
          ))}
        </Select>
        <Select value={fTipo} onChange={(e) => setFTipo(e.target.value as '' | TicketTipo)} className="w-40">
          <option value="">Todos os tipos</option>
          {TIPOS.map((t) => (
            <option key={t} value={t}>{ROTULO_TIPO[t]}</option>
          ))}
        </Select>
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={incluirArquivados} onChange={(e) => setIncluirArquivados(e.target.checked)} />
          Arquivados
        </label>
      </div>

      <Tabela
        colunas={colunas}
        dados={filtrados}
        chaveLinha={(t) => t.id}
        carregando={isLoading}
        aoClicarLinha={(t) => navigate(`/app/tickets/gestao/${t.id}`)}
        dicaLinha="Abrir ticket"
        vazio={<div className="py-8 text-center text-sm text-slate-400">Nenhum ticket encontrado.</div>}
      />
    </div>
  );
}

function VisibilidadeControle() {
  const { data } = useConfiguracaoTickets();
  const atualizar = useAtualizarVisibilidade();

  async function mudar(v: TicketVisibilidade) {
    try {
      await atualizar.mutateAsync(v);
      notificar('Visibilidade atualizada.', 'sucesso');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  return (
    <div className="flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
      <Settings2 className="h-4 w-4 text-slate-500" />
      <div>
        <label htmlFor="tk-vis" className="block text-xs font-medium text-slate-500">Quem vê os tickets</label>
        <Select
          id="tk-vis"
          value={data?.visibilidade ?? 'Privado'}
          disabled={atualizar.isPending}
          onChange={(e) => mudar(e.target.value as TicketVisibilidade)}
          className="mt-0.5 text-sm"
        >
          {VISIBILIDADES.map((v) => (
            <option key={v} value={v}>{ROTULO_VISIBILIDADE[v]}</option>
          ))}
        </Select>
      </div>
    </div>
  );
}
