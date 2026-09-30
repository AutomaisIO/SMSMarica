import type { FonteExterna } from '../types';

/** Como cada fonte se apresenta na tela. */
export const FONTES: Record<
  FonteExterna,
  {
    sigla: string;
    nome: string;
    configuracaoRotulo: string;
    /** O que muda na leitura da trilha desta fonte — aparece no cabeçalho da tela. */
    nota?: string;
  }
> = {
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
  // ADR-0063: o ESUS não mostra histórico por pedido — a trilha é montada pela varredura.
  esussg: {
    sigla: 'ESUS São Gonçalo',
    nome: 'ESUS São Gonçalo (regulação de São Gonçalo, PPI)',
    configuracaoRotulo: 'Regulação — Configuração',
    nota:
      'No ESUS a trilha é montada pela varredura, com os marcos que as listas trazem: quem incluiu o pedido na fila (servidores do município) e quem agendou (operadores de São Gonçalo). O ESUS não tem FollowUP, e a data de inclusão e de agendamento vem sem hora.',
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
