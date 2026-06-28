import { useEffect } from 'react';
import { CheckCircle2, Info, X, XCircle } from 'lucide-react';
import { create } from 'zustand';

export type TipoNotificacao = 'sucesso' | 'erro' | 'info';

type Notificacao = {
  id: number;
  mensagem: string;
  tipo: TipoNotificacao;
};

type NotificacoesState = {
  itens: Notificacao[];
  remover: (id: number) => void;
  adicionar: (mensagem: string, tipo: TipoNotificacao) => void;
};

let proximoId = 1;

const useNotificacoesStore = create<NotificacoesState>((set) => ({
  itens: [],
  remover: (id) => set((s) => ({ itens: s.itens.filter((n) => n.id !== id) })),
  adicionar: (mensagem, tipo) =>
    set((s) => ({ itens: [...s.itens, { id: proximoId++, mensagem, tipo }] })),
}));

/**
 * Dispara uma notificação flutuante (toast). Pode ser chamada de qualquer lugar,
 * inclusive logo antes de navegar para outra rota — o viewport vive no topo da app
 * e sobrevive à troca de página.
 */
export function notificar(mensagem: string, tipo: TipoNotificacao = 'sucesso') {
  useNotificacoesStore.getState().adicionar(mensagem, tipo);
}

const ESTILO: Record<TipoNotificacao, { borda: string; icone: typeof CheckCircle2; cor: string }> = {
  sucesso: { borda: 'border-green-200 bg-green-50 text-green-800', icone: CheckCircle2, cor: 'text-green-600' },
  erro: { borda: 'border-red-200 bg-red-50 text-red-800', icone: XCircle, cor: 'text-red-600' },
  info: { borda: 'border-sky-200 bg-sky-50 text-sky-800', icone: Info, cor: 'text-sky-600' },
};

function ItemNotificacao({ item }: { item: Notificacao }) {
  const remover = useNotificacoesStore((s) => s.remover);
  const { borda, icone: Icone, cor } = ESTILO[item.tipo];

  useEffect(() => {
    const t = setTimeout(() => remover(item.id), 4000);
    return () => clearTimeout(t);
  }, [item.id, remover]);

  return (
    <div
      role="status"
      className={`pointer-events-auto flex items-start gap-2 rounded-md border px-3 py-2 text-sm shadow-md ${borda}`}
    >
      <Icone className={`mt-0.5 h-4 w-4 shrink-0 ${cor}`} />
      <span className="flex-1">{item.mensagem}</span>
      <button
        type="button"
        onClick={() => remover(item.id)}
        className="shrink-0 rounded p-0.5 opacity-60 hover:opacity-100"
        aria-label="Fechar"
      >
        <X className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

/** Viewport das notificações — montar uma única vez na raiz da aplicação. */
export function Notificacoes() {
  const itens = useNotificacoesStore((s) => s.itens);
  if (itens.length === 0) return null;
  return (
    <div className="pointer-events-none fixed right-4 top-4 z-50 flex w-full max-w-sm flex-col gap-2">
      {itens.map((item) => (
        <ItemNotificacao key={item.id} item={item} />
      ))}
    </div>
  );
}
