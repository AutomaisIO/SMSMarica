import type { CorTriagem } from '@/types/painel';

/**
 * Semânticas de triagem Manchester — entidade = cor, SEMPRE acompanhada do nome
 * escrito (nunca cor sozinha). Paleta validada com o validador do dataviz
 * (resultado no front/README.md). O tom "forte" marca o trecho que excede a meta
 * nas pulseiras.
 */
export interface EstiloTriagem {
  nome: string;
  cor: string;
  corForte: string;
}

export const TRIAGEM: Record<CorTriagem, EstiloTriagem> = {
  VERMELHO: { nome: 'Vermelho', cor: '#D62828', corForte: '#A31414' },
  AMARELO: { nome: 'Amarelo', cor: '#E9A400', corForte: '#B37E00' },
  VERDE: { nome: 'Verde', cor: '#2E9E5B', corForte: '#1F7A43' },
  AZUL: { nome: 'Azul', cor: '#2F6FDE', corForte: '#1F51AB' },
  SEM_CLASSIFICACAO: { nome: 'Sem classificação', cor: '#8494A8', corForte: '#5F7186' },
};

export const ORDEM_TRIAGEM: CorTriagem[] = [
  'VERMELHO',
  'AMARELO',
  'VERDE',
  'AZUL',
  'SEM_CLASSIFICACAO',
];

/** Ordena itens tipados por cor na ordem clínica (não confia na ordem do JSON). */
export function ordenarPorTriagem<T extends { cor: CorTriagem }>(itens: T[]): T[] {
  return [...itens].sort(
    (a, b) => ORDEM_TRIAGEM.indexOf(a.cor) - ORDEM_TRIAGEM.indexOf(b.cor),
  );
}
