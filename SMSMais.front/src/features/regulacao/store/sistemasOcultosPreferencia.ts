import { create } from 'zustand';

import { salvarPreferencias } from '@/shared/auth/preferenciasApi';
import type { SistemaRegulacao } from '../types';

/**
 * Quais sistemas reguladores a tela de Regras de elegibilidade deixa de listar.
 *
 * <p>Quem cuida só do estadual não quer abrir a tela num mar de procedimento do SISREG, e o
 * contrário também vale. Fica salvo no <b>usuário</b> (acompanha em qualquer máquina), como as
 * modalidades do PACS e as larguras de tabela.</p>
 *
 * <p><b>É conveniência, não regra:</b> omitir o SISREG some com ele da listagem, mas as regras do
 * SISREG continuam valendo no wizard exatamente como antes. Quem desmarca volta a ver tudo. O
 * `localStorage` é só cache, para a tela abrir já filtrada antes de a hidratação do login
 * chegar.</p>
 */

const CHAVE = 'smsmarica.regulacao.sistemasOcultos';

export const SISTEMAS_REGULADORES: { valor: SistemaRegulacao; rotulo: string }[] = [
  { valor: 'Sisreg', rotulo: 'SISREG' },
  { valor: 'Ser', rotulo: 'SER' },
  { valor: 'Sernit', rotulo: 'SERNIT' },
];

function carregar(): SistemaRegulacao[] {
  try {
    const bruto = localStorage.getItem(CHAVE);
    if (!bruto) return [];
    const lista: unknown = JSON.parse(bruto);
    return Array.isArray(lista)
      ? (lista.filter((s) => typeof s === 'string') as SistemaRegulacao[])
      : [];
  } catch {
    return [];
  }
}

function cachear(v: SistemaRegulacao[]) {
  try {
    localStorage.setItem(CHAVE, JSON.stringify(v));
  } catch {
    /* quota/serialização — cache é best-effort. */
  }
}

type Estado = {
  ocultos: SistemaRegulacao[];
  alternar: (sistema: SistemaRegulacao) => void;
  hidratar: (ocultos?: string[]) => void;
};

export const useSistemasOcultos = create<Estado>((set, get) => ({
  ocultos: carregar(),
  alternar: (sistema) => {
    const atual = get().ocultos;
    const novo = atual.includes(sistema)
      ? atual.filter((s) => s !== sistema)
      : [...atual, sistema];

    set({ ocultos: novo });
    cachear(novo);
    // Fire-and-forget: a lista já reagiu; falha de rede não trava a tela.
    void salvarPreferencias({ regulacaoSistemasOcultos: novo }).catch(() => {});
  },
  hidratar: (ocultos) => {
    if (!Array.isArray(ocultos)) return;
    const lista = ocultos.filter((s) => typeof s === 'string') as SistemaRegulacao[];
    cachear(lista);
    set({ ocultos: lista });
  },
}));

/** Os sistemas que a tela deve pedir ao servidor — o complemento dos ocultos. */
export function sistemasVisiveis(ocultos: SistemaRegulacao[]): SistemaRegulacao[] {
  return SISTEMAS_REGULADORES.map((s) => s.valor).filter((s) => !ocultos.includes(s));
}
