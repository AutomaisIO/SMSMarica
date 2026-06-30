import { create } from 'zustand';
import { salvarPreferencias, type PreferenciasUi } from '@/shared/auth/preferenciasApi';

/**
 * Preferências do menu lateral: a tela "default" de cada seção.
 * Quando uma seção tem default, clicar nela no menu abre direto aquela tela;
 * sem default, abre a página-hub com os botões das opções.
 *
 * Fonte da verdade é o servidor (por usuário). O localStorage é só um cache para
 * resposta imediata enquanto a hidratação do servidor não chega.
 */

const CHAVE = 'smsmarica.menu.defaults';

type Defaults = Record<string, string>; // secaoId -> rota (item.to)

function carregar(): Defaults {
  try {
    const raw = localStorage.getItem(CHAVE);
    if (!raw) return {};
    const obj = JSON.parse(raw) as Defaults;
    return obj && typeof obj === 'object' ? obj : {};
  } catch {
    return {};
  }
}

function cachearLocal(defaults: Defaults) {
  try {
    localStorage.setItem(CHAVE, JSON.stringify(defaults));
  } catch {
    /* quota/serialização — cache é best-effort. */
  }
}

function persistirNoServidor(defaults: Defaults) {
  const payload: PreferenciasUi = { menuDefaults: defaults };
  // Fire-and-forget: a UI já refletiu a mudança; falha de rede não trava nada.
  void salvarPreferencias(payload).catch(() => {
    /* silencioso — próxima alteração tenta de novo. */
  });
}

type Estado = {
  defaults: Defaults;
  /** Define (ou alterna) a tela default de uma seção. */
  definir: (secaoId: string, to: string) => void;
  /** Remove o default da seção (volta a abrir a página-hub). */
  limpar: (secaoId: string) => void;
  /** Substitui o estado pelos defaults vindos do servidor (hidratação no login). */
  hidratar: (defaults: Defaults) => void;
};

export const useMenuPreferencias = create<Estado>((set, get) => ({
  defaults: carregar(),
  definir: (secaoId, to) => {
    const proximo = { ...get().defaults, [secaoId]: to };
    cachearLocal(proximo);
    set({ defaults: proximo });
    persistirNoServidor(proximo);
  },
  limpar: (secaoId) => {
    const proximo = { ...get().defaults };
    delete proximo[secaoId];
    cachearLocal(proximo);
    set({ defaults: proximo });
    persistirNoServidor(proximo);
  },
  hidratar: (defaults) => {
    cachearLocal(defaults);
    set({ defaults });
  },
}));
