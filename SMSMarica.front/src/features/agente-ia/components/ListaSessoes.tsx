import { useState } from 'react';
import { Archive, ArchiveRestore, Pencil, Plus, Ticket, Users } from 'lucide-react';
import { Modal } from '@/shared/ui/Modal';
import { Button } from '@/shared/ui/Button';
import { formatarInstante } from '@/shared/lib/datas';
import {
  useArquivarSessao,
  useListarSessoes,
  useRenomearSessao,
  useRestaurarSessao,
} from '@/features/agente-ia/api/queries';
import type { SessaoResumo } from '@/features/agente-ia/types';

type Props = {
  sessaoAtivaId: string | null;
  onSelecionar: (id: string) => void;
  onNova: () => void;
  onArquivada: (id: string) => void;
  podeEditar: boolean;
  criando: boolean;
};

/** O motor guarda os tempos em epoch de segundos; o util central espera um instante ISO. */
function instante(epoch: number | null): string {
  if (!epoch) return '—';
  return formatarInstante(new Date(epoch * 1000).toISOString());
}

function Quem({ sessao }: { sessao: SessaoResumo }) {
  const autor = sessao.usuario_nome;
  const outros = sessao.participantes.filter((p) => p && p !== autor);
  if (!autor && outros.length === 0) return null;
  return (
    <span className="flex min-w-0 items-center gap-1 text-[11px] text-slate-500">
      <Users className="h-3 w-3 shrink-0" />
      <span className="truncate">
        {autor ?? 'desconhecido'}
        {outros.length > 0 && ` + ${outros.join(', ')}`}
      </span>
    </span>
  );
}

export function ListaSessoes({
  sessaoAtivaId,
  onSelecionar,
  onNova,
  onArquivada,
  podeEditar,
  criando,
}: Props) {
  const [arquivadas, setArquivadas] = useState(false);
  const [renomeando, setRenomeando] = useState<SessaoResumo | null>(null);
  const [titulo, setTitulo] = useState('');

  const { data: sessoes, isLoading } = useListarSessoes(arquivadas);
  const renomear = useRenomearSessao();
  const arquivar = useArquivarSessao();
  const restaurar = useRestaurarSessao();

  function abrirRenomear(s: SessaoResumo) {
    setRenomeando(s);
    setTitulo(s.title);
  }

  async function confirmarRenomear() {
    if (!renomeando || !titulo.trim()) return;
    await renomear.mutateAsync({ id: renomeando.id, titulo: titulo.trim() });
    setRenomeando(null);
  }

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center gap-2 border-b border-slate-200 p-2">
        <button
          type="button"
          onClick={onNova}
          disabled={!podeEditar || criando}
          className="flex flex-1 items-center justify-center gap-1.5 rounded-md bg-red-600 px-3 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
        >
          <Plus className="h-4 w-4" /> Nova conversa
        </button>
      </div>

      <div className="border-b border-slate-100 px-3 py-1.5">
        <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-500">
          <input
            type="checkbox"
            checked={arquivadas}
            onChange={(e) => setArquivadas(e.target.checked)}
            className="h-3.5 w-3.5 rounded border-slate-300"
          />
          Mostrar arquivadas
        </label>
      </div>

      <ul className="flex-1 divide-y divide-slate-100 overflow-y-auto">
        {isLoading && <li className="p-4 text-sm text-slate-500">Carregando…</li>}
        {!isLoading && (sessoes?.length ?? 0) === 0 && (
          <li className="p-4 text-sm text-slate-500">
            {arquivadas ? 'Nenhuma conversa arquivada.' : 'Nenhuma conversa aberta.'}
          </li>
        )}
        {sessoes?.map((s) => (
          <li key={s.id} className="group relative">
            <div
              role="button"
              tabIndex={0}
              onClick={() => onSelecionar(s.id)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') onSelecionar(s.id);
              }}
              className={`w-full cursor-pointer px-3 py-2.5 text-left hover:bg-slate-50 ${
                sessaoAtivaId === s.id ? 'bg-red-50' : ''
              }`}
            >
              <div className="flex items-start justify-between gap-2">
                <span className="truncate text-sm font-medium text-slate-900">
                  {s.title || 'Conversa sem título'}
                </span>
                <span className="shrink-0 text-[11px] text-slate-400">
                  {instante(s.last_used_at)}
                </span>
              </div>

              <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1">
                {s.ticket_numero && (
                  <span className="flex items-center gap-1 rounded bg-slate-100 px-1.5 py-0.5 text-[10px] text-slate-600">
                    <Ticket className="h-3 w-3" />#{s.ticket_numero}
                  </span>
                )}
                {s.running && (
                  <span className="flex items-center gap-1 rounded bg-amber-50 px-1.5 py-0.5 text-[10px] font-medium text-amber-700">
                    <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-amber-500" />
                    rodando
                  </span>
                )}
                {s.archived_at && (
                  <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[10px] text-slate-500">
                    arquivada
                  </span>
                )}
                <span className="text-[10px] text-slate-400">
                  {s.turn_count} {s.turn_count === 1 ? 'mensagem' : 'mensagens'}
                </span>
              </div>

              <div className="mt-1">
                <Quem sessao={s} />
              </div>
            </div>

            {podeEditar && (
              <div className="absolute right-2 top-2 hidden gap-1 group-hover:flex">
                <button
                  type="button"
                  title="Renomear"
                  onClick={() => abrirRenomear(s)}
                  className="rounded bg-white/90 p-1 text-slate-400 shadow-sm hover:text-slate-700"
                >
                  <Pencil className="h-3.5 w-3.5" />
                </button>
                {s.archived_at ? (
                  <button
                    type="button"
                    title="Restaurar"
                    onClick={() => restaurar.mutate(s.id)}
                    className="rounded bg-white/90 p-1 text-slate-400 shadow-sm hover:text-slate-700"
                  >
                    <ArchiveRestore className="h-3.5 w-3.5" />
                  </button>
                ) : (
                  <button
                    type="button"
                    title="Arquivar — o histórico fica guardado"
                    onClick={() =>
                      arquivar.mutate(s.id, { onSuccess: () => onArquivada(s.id) })
                    }
                    className="rounded bg-white/90 p-1 text-slate-400 shadow-sm hover:text-slate-700"
                  >
                    <Archive className="h-3.5 w-3.5" />
                  </button>
                )}
              </div>
            )}
          </li>
        ))}
      </ul>

      <Modal
        aberto={renomeando !== null}
        aoFechar={() => setRenomeando(null)}
        titulo="Renomear conversa"
        largura="sm"
      >
        <div className="space-y-4">
          <input
            value={titulo}
            onChange={(e) => setTitulo(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void confirmarRenomear();
            }}
            autoFocus
            maxLength={120}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-red-500"
          />
          <div className="flex justify-end gap-2">
            <Button variante="secundaria" onClick={() => setRenomeando(null)}>
              Cancelar
            </Button>
            <Button onClick={() => void confirmarRenomear()} disabled={!titulo.trim()}>
              Salvar
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
