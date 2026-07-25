import type { CorTriagem } from '@/types/painel';

/**
 * Semânticas de triagem Manchester — entidade = cor, SEMPRE acompanhada do nome
 * escrito (nunca cor sozinha). Paleta validada com o validador do dataviz
 * (resultado no front/README.md). O tom "forte" marca o trecho que excede a meta
 * nas pulseiras.
 *
 * O laranja entrou em 25/07/2026 com a UPA: o Manchester do Salux, no Conde, tem
 * quatro cores e não usa laranja. Por isso o back manda `coresUsadas` em cada
 * unidade — vermelho/laranja/amarelo são vizinhos de matiz, e é o NOME ao lado que
 * carrega o significado, não o pigmento.
 */
export interface EstiloTriagem {
  nome: string;
  cor: string;
  corForte: string;
}

export const TRIAGEM: Record<CorTriagem, EstiloTriagem> = {
  VERMELHO: { nome: 'Vermelho', cor: '#D62828', corForte: '#A31414' },
  LARANJA: { nome: 'Laranja', cor: '#E2620F', corForte: '#A8430A' },
  AMARELO: { nome: 'Amarelo', cor: '#E9A400', corForte: '#B37E00' },
  VERDE: { nome: 'Verde', cor: '#2E9E5B', corForte: '#1F7A43' },
  AZUL: { nome: 'Azul', cor: '#2F6FDE', corForte: '#1F51AB' },
  SEM_CLASSIFICACAO: { nome: 'Sem classificação', cor: '#8494A8', corForte: '#5F7186' },
};

export const ORDEM_TRIAGEM: CorTriagem[] = [
  'VERMELHO',
  'LARANJA',
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

/**
 * Descarta as cores que a unidade não usa. Mostrar "Laranja — 0 pacientes" na aba
 * do Conde seria inventar uma fila vazia para uma cor que não existe no protocolo
 * dele; some do jeito certo, que é sumindo.
 */
export function apenasCoresDaUnidade<T extends { cor: CorTriagem }>(
  itens: T[],
  coresUsadas: CorTriagem[] | undefined,
): T[] {
  if (!coresUsadas || coresUsadas.length === 0) return itens;
  return itens.filter((item) => coresUsadas.includes(item.cor));
}
