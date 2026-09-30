import { DIAS_SEMANA } from '@/shared/lib/diasSemana';
import type { AgendaPayload } from '@/features/tratamentos/types';

/** Mesmo teto do backend (AgendaDeSessoes.LimiteDeSessoes). */
export const LIMITE_SESSOES = 365;

/** Estado do formulário da agenda (o número de sessões é texto enquanto se digita). */
export type EstadoAgenda = {
  dataInicio: string;
  diasSemanaMascara: number;
  continuo: boolean;
  quantidade: string;
};

/** Estado → corpo da API; null enquanto a regra está incompleta (sem dia, sem início, N fora da faixa). */
export function paraAgendaPayload(e: EstadoAgenda): AgendaPayload | null {
  if (!e.dataInicio || e.diasSemanaMascara < 1 || e.diasSemanaMascara > 127) return null;
  if (e.continuo) {
    return { dataInicio: e.dataInicio, diasSemanaMascara: e.diasSemanaMascara, quantidadeSessoes: null, continuo: true };
  }
  const n = Number(e.quantidade);
  if (!Number.isInteger(n) || n < 1 || n > LIMITE_SESSOES) return null;
  return { dataInicio: e.dataInicio, diasSemanaMascara: e.diasSemanaMascara, quantidadeSessoes: n, continuo: false };
}

export function formatarDataBr(iso: string): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

export function diaSemanaCurto(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number);
  return DIAS_SEMANA[new Date(y, m - 1, d).getDay()]?.curto ?? '';
}
