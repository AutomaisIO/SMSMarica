import { useState } from 'react';
import { Bot, Loader2, UserRoundCheck, X } from 'lucide-react';
import {
  useAtendentesElegiveis,
  useEncaminharConversa,
  useEncaminharConversaParaRobo,
} from '@/features/conversas/api/queries';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

/** Sentinela de seleção: encaminhar ao robô em vez de um atendente humano. */
const ALVO_ROBO = '__robo__';

type Props = {
  conversaId: string;
  onFechar: () => void;
  onEncaminhada: () => void;
};

/**
 * Encaminha a conversa para outro atendente: ele vira o responsável e a conversa vai para a
 * lista pessoal DELE (sai da minha e da fila). A lista só traz quem pode receber — ativo, com
 * o módulo Conversas e vinculado à unidade da conversa; o backend revalida tudo.
 */
export function EncaminharConversaDialog({ conversaId, onFechar, onEncaminhada }: Props) {
  const { data: atendentes, isLoading } = useAtendentesElegiveis(conversaId);
  const encaminhar = useEncaminharConversa();
  const encaminharRobo = useEncaminharConversaParaRobo();
  const [selecionado, setSelecionado] = useState<string | null>(null);
  const [observacao, setObservacao] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  const elegiveis = atendentes?.filter((a) => !a.responsavelAtual) ?? [];
  const paraRobo = selecionado === ALVO_ROBO;
  const enviando = encaminhar.isPending || encaminharRobo.isPending;

  async function aoEncaminhar() {
    if (!selecionado) return;
    setErro(null);
    try {
      if (paraRobo) {
        await encaminharRobo.mutateAsync(conversaId);
      } else {
        await encaminhar.mutateAsync({
          id: conversaId,
          paraUsuarioId: selecionado,
          observacao: observacao.trim() || undefined,
        });
      }
      onEncaminhada();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 p-4">
      <div className="flex max-h-[85vh] w-full max-w-md flex-col rounded-lg bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3">
          <h2 className="text-sm font-semibold text-gray-900">Encaminhar para colega</h2>
          <button type="button" onClick={onFechar} className="rounded p-1 text-gray-400 hover:bg-gray-100" aria-label="Fechar">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-4">
          <p className="text-xs text-gray-500">
            O colega escolhido vira o responsável: a conversa passa para a lista pessoal dele.
          </p>

          {/* Atendente Virtual (robô): retoma de onde parou, sem virar responsável humano. */}
          <button
            type="button"
            onClick={() => setSelecionado(ALVO_ROBO)}
            className={`flex w-full items-start gap-2 rounded-md px-3 py-2 text-left text-sm ring-1 transition ${
              paraRobo
                ? 'bg-indigo-50 text-indigo-800 ring-indigo-300'
                : 'text-gray-800 ring-gray-200 hover:bg-gray-50'
            }`}
          >
            <Bot className="mt-0.5 h-4 w-4 shrink-0 text-indigo-500" />
            <span>
              <span className="block font-medium">Atendente Virtual</span>
              <span className="block text-xs text-gray-500">
                Devolve à fila e o robô retoma a conversa de onde parou.
              </span>
            </span>
          </button>

          <p className="pt-1 text-xs font-medium uppercase tracking-wide text-gray-400">Ou um colega</p>

          {isLoading && (
            <p className="flex items-center gap-2 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" /> Carregando atendentes…
            </p>
          )}
          {!isLoading && elegiveis.length === 0 && (
            <p className="text-sm text-gray-500">
              Nenhum outro atendente elegível nesta unidade (ativo e com acesso à Central).
            </p>
          )}

          <ul className="divide-y divide-gray-100 overflow-hidden rounded-md ring-1 ring-gray-200 empty:hidden">
            {elegiveis.map((a) => (
              <li key={a.usuarioId}>
                <button
                  type="button"
                  onClick={() => setSelecionado(a.usuarioId)}
                  className={`flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-gray-50 ${
                    selecionado === a.usuarioId ? 'bg-primary-50 text-primary-700' : 'text-gray-800'
                  }`}
                >
                  {selecionado === a.usuarioId && <UserRoundCheck className="h-4 w-4 shrink-0" />}
                  <span className="truncate">{a.nome}</span>
                </button>
              </li>
            ))}
          </ul>

          {!paraRobo && (
            <div>
              <label htmlFor="obs-encaminhar" className="mb-1 block text-xs font-medium text-gray-600">
                Observação (opcional)
              </label>
              <textarea
                id="obs-encaminhar"
                value={observacao}
                onChange={(e) => setObservacao(e.target.value)}
                rows={2}
                maxLength={1000}
                placeholder="Contexto para quem vai atender"
                className="w-full rounded-md border border-gray-200 px-2 py-1.5 text-sm outline-none focus:border-primary-400"
              />
            </div>
          )}

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
            onClick={() => void aoEncaminhar()}
            disabled={!selecionado || enviando}
            className="rounded-md bg-primary-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
          >
            {enviando ? 'Encaminhando…' : paraRobo ? 'Encaminhar ao robô' : 'Encaminhar'}
          </button>
        </div>
      </div>
    </div>
  );
}
