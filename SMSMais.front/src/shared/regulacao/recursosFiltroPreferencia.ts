import { create } from 'zustand';

import { salvarPreferencias, type PreferenciasUi } from '@/shared/auth/preferenciasApi';

/**
 * Recursos marcados no filtro por recurso das Notificações da regulação.
 *
 * Fica salvo no USUÁRIO (acompanha em qualquer máquina), como o filtro por técnico: cada um
 * configura a própria tela e não quer refazer a seleção a cada login. O localStorage é só cache
 * para a tela abrir já filtrada antes de a hidratação do login chegar.
 *
 * Por enquanto só o SER tem o filtro; quando o SERNIT e o ESUS de São Gonçalo ganharem, acrescenta-se
 * a chave e o campo de preferência aqui, espelhando o store de técnicos.
 */

export type SistemaRecursoNotificacao = 'ser';

const CHAVE: Record<SistemaRecursoNotificacao, string> = {
  ser: 'smsmarica.notificacoes.ser.recursos',
};

/** Campo das preferências do usuário (no servidor) onde cada recorte mora. */
type CampoRecursos = 'notificacoesSerRecursos';

const CAMPO_PREFERENCIA: Record<SistemaRecursoNotificacao, CampoRecursos> = {
  ser: 'notificacoesSerRecursos',
};

function carregar(sistema: SistemaRecursoNotificacao): string[] {
  try {
    const bruto = localStorage.getItem(CHAVE[sistema]);
    if (!bruto) return [];
    const lista: unknown = JSON.parse(bruto);
    return Array.isArray(lista) ? lista.filter((r): r is string => typeof r === 'string') : [];
  } catch {
    return [];
  }
}

function cachear(sistema: SistemaRecursoNotificacao, v: string[]) {
  try {
    localStorage.setItem(CHAVE[sistema], JSON.stringify(v));
  } catch {
    /* quota/serialização — cache é best-effort. */
  }
}

type Estado = {
  ser: string[];
  definir: (sistema: SistemaRecursoNotificacao, recursos: string[]) => void;
  hidratar: (p: Partial<Record<SistemaRecursoNotificacao, string[] | undefined>>) => void;
};

export const useRecursosFiltro = create<Estado>((set) => ({
  ser: carregar('ser'),
  definir: (sistema, recursos) => {
    set({ [sistema]: recursos } as Pick<Estado, SistemaRecursoNotificacao>);
    cachear(sistema, recursos);
    // Fire-and-forget: a lista já reagiu; falha de rede não trava a tela.
    const parcial: Partial<PreferenciasUi> = {};
    parcial[CAMPO_PREFERENCIA[sistema]] = recursos;
    void salvarPreferencias(parcial).catch(() => {});
  },
  hidratar: (p) => {
    const parcial: Partial<Pick<Estado, SistemaRecursoNotificacao>> = {};
    for (const sistema of Object.keys(CHAVE) as SistemaRecursoNotificacao[]) {
      const lista = p[sistema];
      if (!Array.isArray(lista)) continue;
      const limpa = lista.filter((r) => typeof r === 'string');
      parcial[sistema] = limpa;
      cachear(sistema, limpa);
    }
    if (Object.keys(parcial).length > 0) set(parcial);
  },
}));
