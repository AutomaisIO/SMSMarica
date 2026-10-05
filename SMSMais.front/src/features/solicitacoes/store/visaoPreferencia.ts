import { create } from 'zustand';
import { salvarPreferencias } from '@/shared/auth/preferenciasApi';

/**
 * Preferência da lista de Solicitações de Exame: ver por padrão a visão de SOLICITANTE
 * (o que a unidade pediu a outra) em vez de EXECUTANTE (o que ela realiza). Ticket #84.
 *
 * Fonte da verdade é o servidor (por usuário, acompanha em qualquer browser/PC). O
 * localStorage é só um cache para resposta imediata antes da hidratação chegar. Só faz
 * efeito quando há UMA unidade ativa — sem unidade única não há "solicitante vs executante".
 */

const CHAVE = 'smsmarica.solicitacoes.visao-solicitante';

function carregar(): boolean {
  try {
    return localStorage.getItem(CHAVE) === '1';
  } catch {
    return false;
  }
}

function cachearLocal(v: boolean) {
  try {
    localStorage.setItem(CHAVE, v ? '1' : '0');
  } catch {
    /* quota/serialização — cache é best-effort. */
  }
}

type Estado = {
  verComoSolicitante: boolean;
  /** Liga/desliga a visão de solicitante e persiste no usuário. */
  definir: (v: boolean) => void;
  /** Substitui o estado pelo valor vindo do servidor (hidratação no login). */
  hidratar: (v: boolean | undefined) => void;
};

export const useVisaoSolicitacoes = create<Estado>((set, get) => ({
  verComoSolicitante: carregar(),
  definir: (v) => {
    if (v === get().verComoSolicitante) return;
    set({ verComoSolicitante: v });
    cachearLocal(v);
    // Fire-and-forget: a UI já refletiu; falha de rede não trava nada.
    void salvarPreferencias({ verComoSolicitante: v }).catch(() => {});
  },
  hidratar: (v) => {
    if (typeof v !== 'boolean') return;
    cachearLocal(v);
    set({ verComoSolicitante: v });
  },
}));
