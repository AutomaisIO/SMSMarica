import { create } from 'zustand';
import { salvarPreferencias } from '@/shared/auth/preferenciasApi';

/**
 * Larguras (px) das colunas das tabelas redimensionáveis, por tela (ticket #99).
 * Estrutura: id da tela → (chave da coluna → largura).
 *
 * Fonte da verdade é o servidor (por usuário, acompanha em qualquer browser/PC). O
 * localStorage é só um cache para resposta imediata antes da hidratação chegar.
 * O servidor faz merge por CAMPO, então gravamos sempre o mapa COMPLETO de `largurasTabela`.
 */

const CHAVE = 'smsmarica.tabela.larguras';

export const LARGURA_MIN = 60;
export const LARGURA_MAX = 900;

export type LargurasPorTela = Record<string, Record<string, number>>;

export function clampLargura(px: number): number {
  return Math.min(LARGURA_MAX, Math.max(LARGURA_MIN, Math.round(px)));
}

function carregar(): LargurasPorTela {
  try {
    const raw = localStorage.getItem(CHAVE);
    if (!raw) return {};
    const obj = JSON.parse(raw) as LargurasPorTela;
    return obj && typeof obj === 'object' ? obj : {};
  } catch {
    return {};
  }
}

function cachearLocal(mapa: LargurasPorTela) {
  try {
    localStorage.setItem(CHAVE, JSON.stringify(mapa));
  } catch {
    /* quota/serialização — cache é best-effort. */
  }
}

type Estado = {
  larguras: LargurasPorTela;
  /** Define a largura (px) de uma coluna de uma tela e persiste no perfil. */
  definir: (tela: string, chave: string, px: number) => void;
  /** Define de uma vez o mapa de larguras (chave→px) de uma tela e persiste no perfil (1 PUT). */
  definirVarias: (tela: string, colunas: Record<string, number>) => void;
  /** Substitui o mapa pelos valores vindos do servidor (hidratação no login). */
  hidratar: (mapa: LargurasPorTela | undefined) => void;
};

export const useTabelaPreferencias = create<Estado>((set, get) => ({
  larguras: carregar(),
  definir: (tela, chave, px) => {
    const largura = clampLargura(px);
    const atualTela = get().larguras[tela] ?? {};
    if (atualTela[chave] === largura) return;
    const proximo: LargurasPorTela = {
      ...get().larguras,
      [tela]: { ...atualTela, [chave]: largura },
    };
    set({ larguras: proximo });
    cachearLocal(proximo);
    // Fire-and-forget: a UI já refletiu; falha de rede não trava nada. Manda o mapa completo.
    void salvarPreferencias({ largurasTabela: proximo }).catch(() => {});
  },
  definirVarias: (tela, colunas) => {
    const limpas: Record<string, number> = {};
    for (const [chave, px] of Object.entries(colunas)) limpas[chave] = clampLargura(px);
    const proximo: LargurasPorTela = { ...get().larguras, [tela]: { ...(get().larguras[tela] ?? {}), ...limpas } };
    set({ larguras: proximo });
    cachearLocal(proximo);
    void salvarPreferencias({ largurasTabela: proximo }).catch(() => {});
  },
  hidratar: (mapa) => {
    if (!mapa || typeof mapa !== 'object') return;
    cachearLocal(mapa);
    set({ larguras: mapa });
  },
}));
