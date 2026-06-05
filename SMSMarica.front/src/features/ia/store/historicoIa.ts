import { create } from 'zustand';
import type { RespostaIa } from '@/features/ia/types';

/**
 * Histórico das últimas perguntas/respostas da IA, persistido em localStorage.
 * Sobrevive à navegação entre telas e ao reload — consulta rápida das últimas N.
 */
const CHAVE = 'smsmarica.ia.historico';
const CHAVE_FONTES = 'smsmarica.ia.fontesSelecionadas';
const MAX = 20;

export type InteracaoIa = {
  id: string;
  pergunta: string;
  fonteIds: string[];
  criadoEm: number;
  respostas: RespostaIa[];
};

type Estado = {
  interacoes: InteracaoIa[];
  /** Bases selecionadas, persistidas — o usuário não precisa reselecionar a cada vez. */
  fontesSelecionadas: string[];
  /** consultaId -> sinalizada como "resposta errada". */
  reportadas: Record<string, boolean>;
  adicionar: (interacao: InteracaoIa) => void;
  limpar: () => void;
  marcarReportada: (consultaId: string) => void;
  setFontesSelecionadas: (ids: string[]) => void;
};

function carregar(): InteracaoIa[] {
  try {
    const raw = localStorage.getItem(CHAVE);
    return raw ? (JSON.parse(raw) as InteracaoIa[]) : [];
  } catch {
    return [];
  }
}

function salvar(interacoes: InteracaoIa[]) {
  try {
    localStorage.setItem(CHAVE, JSON.stringify(interacoes.slice(0, MAX)));
  } catch {
    /* quota/serialização — histórico é best-effort. */
  }
}

function carregarFontes(): string[] {
  try {
    const raw = localStorage.getItem(CHAVE_FONTES);
    return raw ? (JSON.parse(raw) as string[]) : [];
  } catch {
    return [];
  }
}

export const useHistoricoIa = create<Estado>((set, get) => ({
  interacoes: carregar(),
  fontesSelecionadas: carregarFontes(),
  reportadas: {},
  adicionar: (interacao) => {
    const interacoes = [interacao, ...get().interacoes].slice(0, MAX);
    salvar(interacoes);
    set({ interacoes });
  },
  limpar: () => {
    salvar([]);
    set({ interacoes: [] });
  },
  marcarReportada: (consultaId) =>
    set((s) => ({ reportadas: { ...s.reportadas, [consultaId]: true } })),
  setFontesSelecionadas: (ids) => {
    try {
      localStorage.setItem(CHAVE_FONTES, JSON.stringify(ids));
    } catch {
      /* best-effort */
    }
    set({ fontesSelecionadas: ids });
  },
}));
