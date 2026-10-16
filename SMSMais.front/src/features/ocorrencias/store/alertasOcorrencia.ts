import { create } from 'zustand';
import type { OcorrenciaRegistradaEvento } from '@/features/ocorrencias/types';

export type AlertaOcorrencia = OcorrenciaRegistradaEvento & { recebidoEm: number };

type Estado = {
  /** Ocorrências que chegaram nesta sessão e o operador ainda não dispensou do aviso. */
  alertas: AlertaOcorrencia[];
  adicionar: (evento: OcorrenciaRegistradaEvento) => void;
  dispensar: (ocorrenciaId: string) => void;
  dispensarTodos: () => void;
};

export const useAlertasOcorrencia = create<Estado>((set) => ({
  alertas: [],
  adicionar: (evento) =>
    set((s) =>
      s.alertas.some((a) => a.ocorrenciaId === evento.ocorrenciaId)
        ? s
        : { alertas: [{ ...evento, recebidoEm: Date.now() }, ...s.alertas] },
    ),
  dispensar: (ocorrenciaId) =>
    set((s) => ({ alertas: s.alertas.filter((a) => a.ocorrenciaId !== ocorrenciaId) })),
  dispensarTodos: () => set({ alertas: [] }),
}));
