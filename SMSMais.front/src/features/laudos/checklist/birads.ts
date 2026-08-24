import type { CategoriaBiRads } from './types';

// Espelho do motor do backend (CalculadoraBiRads). Usado SÓ para preview ao vivo —
// o valor gravado é o que o servidor recalcula. Regra: "achado mais suspeito" (MAX),
// não soma de pontos. 0 (incompleto) é ortogonal: só vence se o mais alto for ≤ 2.

export const CATEGORIAS_BIRADS: CategoriaBiRads[] = ['0', '1', '2', '3', '4', '4A', '4B', '4C', '5', '6'];

const RANK: Record<string, number> = {
  '1': 10,
  '2': 20,
  '3': 30,
  '4': 40,
  '4A': 41,
  '4B': 42,
  '4C': 43,
  '5': 50,
  '6': 60,
};

export function normalizarBiRads(c?: string | null): CategoriaBiRads | null {
  if (!c) return null;
  const n = c.trim().toUpperCase();
  return (CATEGORIAS_BIRADS as string[]).includes(n) ? (n as CategoriaBiRads) : null;
}

export function ehCategoriaValida(c?: string | null): boolean {
  return normalizarBiRads(c) !== null;
}

/** Sugere a categoria a partir das contribuições marcadas. null = nada pontuado. */
export function sugerirBiRads(contribuicoes: Array<string | null | undefined>): CategoriaBiRads | null {
  const validas = contribuicoes
    .map(normalizarBiRads)
    .filter((c): c is CategoriaBiRads => c !== null);

  if (validas.length === 0) return null;

  const temIncompleto = validas.includes('0');
  const comRank = validas.filter((c) => RANK[c] !== undefined);

  if (comRank.length === 0) return temIncompleto ? '0' : null;

  const maisSuspeito = comRank.reduce((a, b) => (RANK[b] > RANK[a] ? b : a));

  if (temIncompleto && RANK[maisSuspeito] <= RANK['2']) return '0';
  return maisSuspeito;
}

const ROTULOS: Record<CategoriaBiRads, string> = {
  '0': '0 — Incompleto',
  '1': '1 — Negativo',
  '2': '2 — Benigno',
  '3': '3 — Provavelmente benigno',
  '4': '4 — Suspeito',
  '4A': '4A — Baixa suspeita',
  '4B': '4B — Suspeita intermediária',
  '4C': '4C — Suspeita moderada/alta',
  '5': '5 — Altamente suspeito',
  '6': '6 — Malignidade comprovada',
};

export function rotuloBiRads(c?: string | null): string {
  const n = normalizarBiRads(c);
  return n ? ROTULOS[n] : '';
}

export function condutaBiRads(c?: string | null): string {
  switch (normalizarBiRads(c)) {
    case '0':
      return 'Avaliação incompleta — necessita de incidências adicionais, ultrassonografia complementar ou comparação com exames anteriores.';
    case '1':
      return 'Mamografia negativa. Rastreamento de rotina conforme a faixa etária/risco.';
    case '2':
      return 'Achados benignos. Rastreamento de rotina conforme a faixa etária/risco.';
    case '3':
      return 'Achado provavelmente benigno. Recomenda-se controle por imagem em 6 meses.';
    case '4':
    case '4A':
    case '4B':
    case '4C':
      return 'Achado suspeito. Recomenda-se correlação com estudo histopatológico (biópsia).';
    case '5':
      return 'Achado altamente suspeito de malignidade. Recomenda-se biópsia/conduta apropriada.';
    case '6':
      return 'Malignidade comprovada por biópsia.';
    default:
      return '';
  }
}

/** Cor de destaque por faixa de suspeição (para badges na UI). */
export function corBiRads(c?: string | null): string {
  const n = normalizarBiRads(c);
  if (!n) return 'bg-gray-100 text-gray-600 border-gray-200';
  if (n === '0') return 'bg-amber-50 text-amber-800 border-amber-200';
  if (n === '1' || n === '2') return 'bg-emerald-50 text-emerald-800 border-emerald-200';
  if (n === '3') return 'bg-yellow-50 text-yellow-800 border-yellow-200';
  if (n === '5' || n === '6') return 'bg-red-50 text-red-800 border-red-200';
  return 'bg-orange-50 text-orange-800 border-orange-200'; // 4 / 4A / 4B / 4C
}
