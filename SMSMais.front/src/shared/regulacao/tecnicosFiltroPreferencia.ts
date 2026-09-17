import { create } from 'zustand';

import { salvarPreferencias } from '@/shared/auth/preferenciasApi';

/**
 * Técnicos marcados no filtro das Notificações do SER e do SERNIT.
 *
 * Fica salvo no USUÁRIO (acompanha em qualquer máquina): o técnico que filtra "o que é meu" não
 * quer refazer a seleção a cada login. Um recorte por sistema — os nomes e as filas são de
 * sistemas diferentes. O localStorage é só cache para a tela abrir já filtrada antes de a
 * hidratação do login chegar.
 */

export type SistemaNotificacao = 'ser' | 'sernit';

const CHAVE: Record<SistemaNotificacao, string> = {
  ser: 'smsmarica.notificacoes.ser.tecnicos',
  sernit: 'smsmarica.notificacoes.sernit.tecnicos',
};

function carregar(sistema: SistemaNotificacao): string[] {
  try {
    const bruto = localStorage.getItem(CHAVE[sistema]);
    if (!bruto) return [];
    const lista: unknown = JSON.parse(bruto);
    return Array.isArray(lista) ? lista.filter((t): t is string => typeof t === 'string') : [];
  } catch {
    return [];
  }
}

function cachear(sistema: SistemaNotificacao, v: string[]) {
  try {
    localStorage.setItem(CHAVE[sistema], JSON.stringify(v));
  } catch {
    /* quota/serialização — cache é best-effort. */
  }
}

type Estado = {
  ser: string[];
  sernit: string[];
  definir: (sistema: SistemaNotificacao, chaves: string[]) => void;
  hidratar: (p: { ser?: string[]; sernit?: string[] }) => void;
};

export const useTecnicosFiltro = create<Estado>((set) => ({
  ser: carregar('ser'),
  sernit: carregar('sernit'),
  definir: (sistema, chaves) => {
    set({ [sistema]: chaves } as Pick<Estado, SistemaNotificacao>);
    cachear(sistema, chaves);
    // Fire-and-forget: a lista já reagiu; falha de rede não trava a tela.
    void salvarPreferencias(
      sistema === 'ser' ? { notificacoesSerTecnicos: chaves } : { notificacoesSernitTecnicos: chaves },
    ).catch(() => {});
  },
  hidratar: ({ ser, sernit }) => {
    const parcial: Partial<Pick<Estado, SistemaNotificacao>> = {};
    if (Array.isArray(ser)) {
      parcial.ser = ser.filter((t) => typeof t === 'string');
      cachear('ser', parcial.ser);
    }
    if (Array.isArray(sernit)) {
      parcial.sernit = sernit.filter((t) => typeof t === 'string');
      cachear('sernit', parcial.sernit);
    }
    if (Object.keys(parcial).length > 0) set(parcial);
  },
}));
