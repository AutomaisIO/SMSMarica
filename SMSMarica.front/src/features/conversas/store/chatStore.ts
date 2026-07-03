import { create } from 'zustand';

export type EstadoWidget = 'fechado' | 'minimizado' | 'aberto';

type ChatState = {
  widget: EstadoWidget;
  conversaAtivaId: string | null;
  /** Alertas do navegador (Notification API) habilitados pelo operador. */
  alertasAtivos: boolean;
  /** Total agregado de não-lidas visíveis (alimenta o sino do Header e o título da aba). */
  totalNaoLidas: number;
  abrir: () => void;
  minimizar: () => void;
  fechar: () => void;
  abrirConversa: (id: string) => void;
  setConversaAtiva: (id: string | null) => void;
  setAlertas: (v: boolean) => void;
  setTotalNaoLidas: (n: number) => void;
};

export const useChat = create<ChatState>((set) => ({
  widget: 'fechado',
  conversaAtivaId: null,
  alertasAtivos: false,
  totalNaoLidas: 0,
  abrir: () => set({ widget: 'aberto' }),
  minimizar: () => set({ widget: 'minimizado' }),
  fechar: () => set({ widget: 'fechado', conversaAtivaId: null }),
  abrirConversa: (id) => set({ widget: 'aberto', conversaAtivaId: id }),
  setConversaAtiva: (id) => set({ conversaAtivaId: id }),
  setAlertas: (v) => set({ alertasAtivos: v }),
  setTotalNaoLidas: (n) => set({ totalNaoLidas: n }),
}));
