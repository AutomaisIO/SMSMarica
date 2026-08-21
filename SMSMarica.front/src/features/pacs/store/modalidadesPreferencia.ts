import { create } from 'zustand';
import { salvarPreferencias } from '@/shared/auth/preferenciasApi';
import type { ModalidadeDicom } from '@/features/tipos-exame/types';

/**
 * Recorte da tela de Exames de imagem — modalidades e, dentro delas, os tipos. Quem lauda
 * mamografia e densitometria não quer abrir a tela num mar de ultrassom, e dentro de MG e OT
 * ainda pode haver procedimento que não é dela.
 *
 * Fica salvo no USUÁRIO (acompanha em qualquer máquina), como a visão de solicitante e as
 * larguras de tabela. É conveniência, não segurança: limpar a seleção devolve tudo — o que a
 * pessoa PODE ver continua sendo decidido no servidor, pelo escopo de unidade. O localStorage é
 * só cache para a tela abrir já filtrada antes da hidratação chegar.
 */

const CHAVE_MODALIDADES = 'smsmarica.pacs.modalidades';
const CHAVE_TIPOS = 'smsmarica.pacs.tipos';

function carregar(chave: string): string[] {
  try {
    const bruto = localStorage.getItem(chave);
    if (!bruto) return [];
    const lista = JSON.parse(bruto);
    return Array.isArray(lista) ? lista.filter((m) => typeof m === 'string') : [];
  } catch {
    return [];
  }
}

function cachearLocal(chave: string, v: string[]) {
  try {
    localStorage.setItem(chave, JSON.stringify(v));
  } catch {
    /* quota/serialização — cache é best-effort. */
  }
}

type Estado = {
  modalidades: ModalidadeDicom[];
  tipos: string[];
  definirModalidades: (v: ModalidadeDicom[]) => void;
  definirTipos: (v: string[]) => void;
  /** Substitui pelos valores vindos do servidor (hidratação no login). */
  hidratar: (p: { modalidades?: string[]; tipos?: string[] }) => void;
};

export const useModalidadesExames = create<Estado>((set) => ({
  modalidades: carregar(CHAVE_MODALIDADES) as ModalidadeDicom[],
  tipos: carregar(CHAVE_TIPOS),
  definirModalidades: (v) => {
    set({ modalidades: v });
    cachearLocal(CHAVE_MODALIDADES, v);
    // Fire-and-forget: a lista já reagiu; falha de rede não trava a tela.
    void salvarPreferencias({ examesModalidades: v }).catch(() => {});
  },
  definirTipos: (v) => {
    set({ tipos: v });
    cachearLocal(CHAVE_TIPOS, v);
    void salvarPreferencias({ examesTipos: v }).catch(() => {});
  },
  hidratar: ({ modalidades, tipos }) => {
    const parcial: Partial<Estado> = {};
    if (Array.isArray(modalidades)) {
      const lista = modalidades.filter((m) => typeof m === 'string') as ModalidadeDicom[];
      cachearLocal(CHAVE_MODALIDADES, lista);
      parcial.modalidades = lista;
    }
    if (Array.isArray(tipos)) {
      const lista = tipos.filter((t) => typeof t === 'string');
      cachearLocal(CHAVE_TIPOS, lista);
      parcial.tipos = lista;
    }
    if (Object.keys(parcial).length > 0) set(parcial);
  },
}));
