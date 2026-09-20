import type { FonteExterna } from '../types';

/** Como cada fonte se apresenta na tela. */
export const FONTES: Record<FonteExterna, { sigla: string; nome: string; configuracaoRotulo: string }> = {
  ser: {
    sigla: 'SER',
    nome: 'SER (Sistema Estadual de Regulação, SES-RJ)',
    configuracaoRotulo: 'Regulação — Configuração',
  },
  sernit: {
    sigla: 'SERNIT',
    nome: 'SERNIT (SER de Niterói)',
    configuracaoRotulo: 'Regulação — Configuração',
  },
};

/** Hora decimal (8,5) → "8h30". */
export function hora(h: number | null | undefined): string {
  if (h === null || h === undefined) return '—';
  const inteira = Math.floor(h);
  const minutos = Math.round((h - inteira) * 60);
  return `${inteira}h${String(minutos).padStart(2, '0')}`;
}

/** Rótulo do eixo de horas: "8h". */
export function horaCurta(h: number): string {
  return `${h}h`;
}
