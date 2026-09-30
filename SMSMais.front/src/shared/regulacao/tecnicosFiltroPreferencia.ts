import { create } from 'zustand';

import { salvarPreferencias, type PreferenciasUi } from '@/shared/auth/preferenciasApi';

/**
 * Técnicos marcados no filtro das Notificações do SER, do SERNIT e do ESUS de São Gonçalo.
 *
 * Fica salvo no USUÁRIO (acompanha em qualquer máquina): o técnico que filtra "o que é meu" não
 * quer refazer a seleção a cada login. Um recorte por sistema — os nomes e as filas são de
 * sistemas diferentes. O localStorage é só cache para a tela abrir já filtrada antes de a
 * hidratação do login chegar.
 */

export type SistemaNotificacao = 'ser' | 'sernit' | 'esussg';

const CHAVE: Record<SistemaNotificacao, string> = {
  ser: 'smsmarica.notificacoes.ser.tecnicos',
  sernit: 'smsmarica.notificacoes.sernit.tecnicos',
  esussg: 'smsmarica.notificacoes.esussg.tecnicos',
};

/** Campo das preferências do usuário (no servidor) onde cada recorte mora. */
type CampoTecnicos = 'notificacoesSerTecnicos' | 'notificacoesSernitTecnicos' | 'notificacoesEsusSgTecnicos';

const CAMPO_PREFERENCIA: Record<SistemaNotificacao, CampoTecnicos> = {
  ser: 'notificacoesSerTecnicos',
  sernit: 'notificacoesSernitTecnicos',
  esussg: 'notificacoesEsusSgTecnicos',
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
  esussg: string[];
  definir: (sistema: SistemaNotificacao, chaves: string[]) => void;
  hidratar: (p: Partial<Record<SistemaNotificacao, string[] | undefined>>) => void;
};

export const useTecnicosFiltro = create<Estado>((set) => ({
  ser: carregar('ser'),
  sernit: carregar('sernit'),
  esussg: carregar('esussg'),
  definir: (sistema, chaves) => {
    set({ [sistema]: chaves } as Pick<Estado, SistemaNotificacao>);
    cachear(sistema, chaves);
    // Fire-and-forget: a lista já reagiu; falha de rede não trava a tela.
    const parcial: Partial<PreferenciasUi> = {};
    parcial[CAMPO_PREFERENCIA[sistema]] = chaves;
    void salvarPreferencias(parcial).catch(() => {});
  },
  hidratar: (p) => {
    const parcial: Partial<Pick<Estado, SistemaNotificacao>> = {};
    for (const sistema of Object.keys(CHAVE) as SistemaNotificacao[]) {
      const lista = p[sistema];
      if (!Array.isArray(lista)) continue;
      const limpa = lista.filter((t) => typeof t === 'string');
      parcial[sistema] = limpa;
      cachear(sistema, limpa);
    }
    if (Object.keys(parcial).length > 0) set(parcial);
  },
}));
