import { create } from 'zustand';

/**
 * Preferências do menu lateral: a tela "default" de cada seção.
 * Quando uma seção tem default, clicar nela no menu abre direto aquela tela;
 * sem default, abre a página-hub com os botões das opções.
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

function salvar(defaults: Defaults) {
  try {
    localStorage.setItem(CHAVE, JSON.stringify(defaults));
  } catch {
    /* quota/serialização — preferência é best-effort. */
  }
}

type Estado = {
  defaults: Defaults;
  /** Define (ou alterna) a tela default de uma seção. */
  definir: (secaoId: string, to: string) => void;
  /** Remove o default da seção (volta a abrir a página-hub). */
  limpar: (secaoId: string) => void;
};

export const useMenuPreferencias = create<Estado>((set, get) => ({
  defaults: carregar(),
  definir: (secaoId, to) => {
    const proximo = { ...get().defaults, [secaoId]: to };
    salvar(proximo);
    set({ defaults: proximo });
  },
  limpar: (secaoId) => {
    const proximo = { ...get().defaults };
    delete proximo[secaoId];
    salvar(proximo);
    set({ defaults: proximo });
  },
}));
