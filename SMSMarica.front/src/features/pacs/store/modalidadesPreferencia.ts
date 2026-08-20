import { create } from 'zustand';
import { salvarPreferencias } from '@/shared/auth/preferenciasApi';
import type { ModalidadeDicom } from '@/features/tipos-exame/types';

/**
 * Recorte de modalidade da tela de Exames de imagem — quem lauda só mamografia e densitometria
 * não quer abrir a tela num mar de ultrassom. Fica salvo no USUÁRIO (acompanha em qualquer
 * máquina), como a visão de solicitante e as larguras de tabela.
 *
 * É conveniência, não segurança: limpar a seleção devolve todas as modalidades — o que a pessoa
 * pode ver continua sendo decidido no servidor, pelo escopo de unidade. O localStorage é só
 * cache para a tela abrir já filtrada antes da hidratação chegar.
 */

const CHAVE = 'smsmarica.pacs.modalidades';

function carregar(): ModalidadeDicom[] {
  try {
    const bruto = localStorage.getItem(CHAVE);
    if (!bruto) return [];
    const lista = JSON.parse(bruto);
    return Array.isArray(lista) ? (lista.filter((m) => typeof m === 'string') as ModalidadeDicom[]) : [];
  } catch {
    return [];
  }
}

function cachearLocal(v: ModalidadeDicom[]) {
  try {
    localStorage.setItem(CHAVE, JSON.stringify(v));
  } catch {
    /* quota/serialização — cache é best-effort. */
  }
}

type Estado = {
  modalidades: ModalidadeDicom[];
  definir: (v: ModalidadeDicom[]) => void;
  /** Substitui pelo valor vindo do servidor (hidratação no login). */
  hidratar: (v: string[] | undefined) => void;
};

export const useModalidadesExames = create<Estado>((set) => ({
  modalidades: carregar(),
  definir: (v) => {
    set({ modalidades: v });
    cachearLocal(v);
    // Fire-and-forget: a lista já reagiu; falha de rede não trava a tela.
    void salvarPreferencias({ examesModalidades: v }).catch(() => {});
  },
  hidratar: (v) => {
    if (!Array.isArray(v)) return;
    const lista = v.filter((m) => typeof m === 'string') as ModalidadeDicom[];
    cachearLocal(lista);
    set({ modalidades: lista });
  },
}));
