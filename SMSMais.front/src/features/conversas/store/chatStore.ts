import { create } from 'zustand';

export type EstadoWidget = 'fechado' | 'minimizado' | 'aberto';

type ChatState = {
  widget: EstadoWidget;
  conversaAtivaId: string | null;
  /** Alertas do navegador (Notification API) habilitados pelo operador. */
  alertasAtivos: boolean;
  /**
   * Bip sonoro de mensagem nova ligado? PERSISTIDO no usuário (ticket #127): silenciou,
   * continua silenciado entre sessões/máquinas até reativar — substitui o comportamento
   * só-de-sessão do ticket #44. Hidratado no login a partir das preferências do servidor
   * (`bipChatSilenciado`) e gravado por `definirSomChat`. Sincronizado entre a janela
   * principal e a do chat pelo BroadcastChannel para eco imediato. O default `true` aqui
   * é só o valor até a hidratação chegar (conta nova = bip ligado).
   */
  somAtivo: boolean;
  /** Total agregado de não-lidas visíveis (alimenta o sino do Header e o título da aba). */
  totalNaoLidas: number;
  /**
   * Texto que uma resposta rápida jogou no campo de digitação (o operador ainda revisa e
   * envia). Carrega a conversa junto para não cair no composer errado se ele trocar de
   * thread enquanto preenchia as variáveis.
   */
  rascunho: { conversaId: string; texto: string } | null;
  inserirRascunho: (conversaId: string, texto: string) => void;
  consumirRascunho: () => void;
  abrir: () => void;
  minimizar: () => void;
  fechar: () => void;
  abrirConversa: (id: string) => void;
  setConversaAtiva: (id: string | null) => void;
  setAlertas: (v: boolean) => void;
  setSom: (v: boolean) => void;
  setTotalNaoLidas: (n: number) => void;
};

export const useChat = create<ChatState>((set) => ({
  widget: 'fechado',
  conversaAtivaId: null,
  alertasAtivos: false,
  somAtivo: true,
  totalNaoLidas: 0,
  rascunho: null,
  inserirRascunho: (conversaId, texto) => set({ rascunho: { conversaId, texto } }),
  consumirRascunho: () => set({ rascunho: null }),
  abrir: () => set({ widget: 'aberto' }),
  minimizar: () => set({ widget: 'minimizado' }),
  fechar: () => set({ widget: 'fechado', conversaAtivaId: null }),
  abrirConversa: (id) => set({ widget: 'aberto', conversaAtivaId: id }),
  setConversaAtiva: (id) => set({ conversaAtivaId: id }),
  setAlertas: (v) => set({ alertasAtivos: v }),
  setSom: (v) => set({ somAtivo: v }),
  setTotalNaoLidas: (n) => set({ totalNaoLidas: n }),
}));
