import { create } from 'zustand';

/**
 * Estado da Comunicação interna NESTA janela (a principal e a janela solta têm cada uma o seu).
 * O que é do servidor (conversas, mensagens, resumo) vive no react-query; aqui fica só o que é
 * de tela e o que o hub empurra sem consulta (presença, "digitando…").
 */
type InternoState = {
  /** Painel encaixado no canto (só na janela principal; a janela solta é sempre "aberta"). */
  painelAberto: boolean;
  /**
   * O painel abriu SOZINHO por mensagem que chegou: o composer não pega o foco (a pessoa pode
   * estar digitando em outro campo da tela). Volta a false no primeiro gesto dentro do painel.
   */
  abertoSozinho: boolean;
  conversaAtivaId: string | null;
  /** Tela da coluna esquerda/corpo: lista de conversas ou a escolha de unidade/pessoa. */
  modo: 'lista' | 'nova';
  /** Mensagem que a busca mandou mostrar (a thread rola até ela e destaca). */
  mensagemAlvoId: string | null;
  /** Incrementa a cada "pulo" pedido — o painel/balão animam quando muda. */
  pulo: number;
  /** Som de mensagem nova ligado? Persistido no usuário (preferência própria, não o bip da Central). */
  somAtivo: boolean;
  /** Abrir o painel sozinho quando chega mensagem? Persistido no usuário. */
  abrirSozinho: boolean;
  totalNaoLidas: number;
  /** Presença empurrada pelo hub — sobrepõe o `online` que veio nas listas. */
  presenca: Record<string, boolean>;
  /** Quem está digitando, por conversa (expira sozinho em `ate`). */
  digitando: Record<string, { usuarioId: string; nome: string; ate: number }>;

  abrirPainel: (conversaId?: string | null) => void;
  abrirPainelSozinho: (conversaId: string) => void;
  fecharPainel: () => void;
  confirmarGesto: () => void;
  setConversaAtiva: (id: string | null, mensagemAlvoId?: string | null) => void;
  setModo: (m: 'lista' | 'nova') => void;
  limparMensagemAlvo: () => void;
  pular: () => void;
  setSom: (v: boolean) => void;
  setAbrirSozinho: (v: boolean) => void;
  setTotalNaoLidas: (n: number) => void;
  setPresenca: (usuarioId: string, online: boolean) => void;
  limparPresenca: () => void;
  setDigitando: (conversaId: string, usuarioId: string, nome: string) => void;
};

export const useInterno = create<InternoState>((set) => ({
  painelAberto: false,
  abertoSozinho: false,
  conversaAtivaId: null,
  modo: 'lista',
  mensagemAlvoId: null,
  pulo: 0,
  somAtivo: true,
  abrirSozinho: true,
  totalNaoLidas: 0,
  presenca: {},
  digitando: {},

  abrirPainel: (conversaId) =>
    set((s) => ({
      painelAberto: true,
      abertoSozinho: false,
      conversaAtivaId: conversaId === undefined ? s.conversaAtivaId : conversaId,
      modo: conversaId ? 'lista' : s.modo,
    })),
  abrirPainelSozinho: (conversaId) =>
    set((s) => ({
      painelAberto: true,
      abertoSozinho: true,
      conversaAtivaId: conversaId,
      modo: 'lista',
      pulo: s.pulo + 1,
    })),
  fecharPainel: () => set({ painelAberto: false, abertoSozinho: false }),
  confirmarGesto: () => set({ abertoSozinho: false }),
  setConversaAtiva: (id, mensagemAlvoId = null) => set({ conversaAtivaId: id, mensagemAlvoId, modo: 'lista' }),
  setModo: (modo) => set({ modo }),
  limparMensagemAlvo: () => set({ mensagemAlvoId: null }),
  pular: () => set((s) => ({ pulo: s.pulo + 1 })),
  setSom: (somAtivo) => set({ somAtivo }),
  setAbrirSozinho: (abrirSozinho) => set({ abrirSozinho }),
  setTotalNaoLidas: (totalNaoLidas) => set({ totalNaoLidas }),
  setPresenca: (usuarioId, online) => set((s) => ({ presenca: { ...s.presenca, [usuarioId]: online } })),
  limparPresenca: () => set({ presenca: {} }),
  setDigitando: (conversaId, usuarioId, nome) =>
    set((s) => ({ digitando: { ...s.digitando, [conversaId]: { usuarioId, nome, ate: Date.now() + 4_000 } } })),
}));

/** Online segundo o hub, ou o valor que veio da API quando o hub ainda não falou desta pessoa. */
export function useOnline(usuarioId: string | null | undefined, daApi: boolean): boolean {
  return useInterno((s) => (usuarioId ? (s.presenca[usuarioId] ?? daApi) : daApi));
}
