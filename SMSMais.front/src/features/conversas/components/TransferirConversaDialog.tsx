import { useMemo, useState } from 'react';
import { Building2, Loader2, Search, X } from 'lucide-react';
import { useTransferirConversa, useUnidadesDestino } from '@/features/conversas/api/queries';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

type Props = {
  conversaId: string;
  /** Unidade atual da conversa — aparece desabilitada na lista. */
  unidadeAtualId: string | null;
  onFechar: () => void;
  onTransferida: () => void;
};

/**
 * Transfere a conversa para outra unidade: ela entra na FILA GERAL de lá, sem responsável —
 * qualquer atendente da unidade destino pode puxar. A lista traz só unidades ativas da rede
 * (externas ficam de fora: ninguém atende chat por elas); o backend revalida.
 */
export function TransferirConversaDialog({ conversaId, unidadeAtualId, onFechar, onTransferida }: Props) {
  const { data: unidades, isLoading } = useUnidadesDestino(true);
  const transferir = useTransferirConversa();
  const [selecionada, setSelecionada] = useState<string | null>(null);
  const [filtro, setFiltro] = useState('');
  const [observacao, setObservacao] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  const filtradas = useMemo(() => {
    const termo = filtro.trim().toLowerCase();
    if (!termo) return unidades ?? [];
    return (unidades ?? []).filter((u) => u.nome.toLowerCase().includes(termo));
  }, [unidades, filtro]);

  async function aoTransferir() {
    if (!selecionada) return;
    setErro(null);
    try {
      await transferir.mutateAsync({
        id: conversaId,
        paraUnidadeId: selecionada,
        observacao: observacao.trim() || undefined,
      });
      onTransferida();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 p-4">
      <div className="flex max-h-[85vh] w-full max-w-md flex-col rounded-lg bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3">
          <h2 className="text-sm font-semibold text-gray-900">Transferir para outra unidade</h2>
          <button type="button" onClick={onFechar} className="rounded p-1 text-gray-400 hover:bg-gray-100" aria-label="Fechar">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-4">
          <p className="text-xs text-gray-500">
            A conversa entra na fila geral da unidade destino, <b>sem responsável</b> — qualquer
            atendente de lá pode puxar.
          </p>

          <div className="flex items-center gap-2 rounded-md border border-gray-200 px-2">
            <Search className="h-4 w-4 shrink-0 text-gray-400" />
            <input
              value={filtro}
              onChange={(e) => setFiltro(e.target.value)}
              placeholder="Filtrar unidade"
              className="w-full bg-transparent py-1.5 text-sm outline-none"
            />
          </div>

          {isLoading && (
            <p className="flex items-center gap-2 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" /> Carregando unidades…
            </p>
          )}
          {!isLoading && filtradas.length === 0 && (
            <p className="text-sm text-gray-500">Nenhuma unidade encontrada.</p>
          )}

          <ul className="max-h-56 divide-y divide-gray-100 overflow-y-auto rounded-md ring-1 ring-gray-200 empty:hidden">
            {filtradas.map((u) => {
              const atual = u.id === unidadeAtualId;
              return (
                <li key={u.id}>
                  <button
                    type="button"
                    onClick={() => setSelecionada(u.id)}
                    disabled={atual}
                    className={`flex w-full items-center gap-2 px-3 py-2 text-left text-sm disabled:opacity-40 ${
                      selecionada === u.id ? 'bg-primary-50 text-primary-700' : 'text-gray-800 hover:bg-gray-50'
                    }`}
                  >
                    <Building2 className="h-4 w-4 shrink-0 text-gray-400" />
                    <span className="truncate">{u.nome}</span>
                    {atual && <span className="ml-auto shrink-0 text-[10px] text-gray-400">atual</span>}
                  </button>
                </li>
              );
            })}
          </ul>

          <div>
            <label htmlFor="obs-transferir" className="mb-1 block text-xs font-medium text-gray-600">
              Observação (opcional)
            </label>
            <textarea
              id="obs-transferir"
              value={observacao}
              onChange={(e) => setObservacao(e.target.value)}
              rows={2}
              maxLength={1000}
              placeholder="Contexto para a unidade destino"
              className="w-full rounded-md border border-gray-200 px-2 py-1.5 text-sm outline-none focus:border-primary-400"
            />
          </div>

          {erro && <p className="text-sm text-error-600">{erro}</p>}
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-200 px-4 py-3">
          <button
            type="button"
            onClick={onFechar}
            className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={() => void aoTransferir()}
            disabled={!selecionada || transferir.isPending}
            className="rounded-md bg-primary-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
          >
            {transferir.isPending ? 'Transferindo…' : 'Transferir'}
          </button>
        </div>
      </div>
    </div>
  );
}
