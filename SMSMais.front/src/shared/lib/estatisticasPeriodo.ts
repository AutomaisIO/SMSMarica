/**
 * Período e formatação das telas de estatísticas de operadores (SISREG, SER, SERNIT). Vive em
 * shared porque as três telas compartilham presets, formatos e a régua de datas — e cada uma é
 * uma feature separada.
 */
export type Periodo = { de: string; ate: string };

/** Hoje no relógio de Maricá, `aaaa-mm-dd`. */
export function hojeSP(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date());
}

export function somarDias(iso: string, dias: number): string {
  const [a, m, d] = iso.split('-').map(Number);
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

export type Preset = { id: string; rotulo: string; periodo: () => Periodo };

export const PRESETS: Preset[] = [
  { id: '7', rotulo: '7 dias', periodo: () => ({ de: somarDias(hojeSP(), -6), ate: hojeSP() }) },
  { id: '30', rotulo: '30 dias', periodo: () => ({ de: somarDias(hojeSP(), -29), ate: hojeSP() }) },
  { id: '90', rotulo: '90 dias', periodo: () => ({ de: somarDias(hojeSP(), -89), ate: hojeSP() }) },
  {
    id: 'mes',
    rotulo: 'Este mês',
    periodo: () => ({ de: `${hojeSP().slice(0, 8)}01`, ate: hojeSP() }),
  },
  {
    id: 'mes-anterior',
    rotulo: 'Mês anterior',
    periodo: () => {
      const primeiroDeste = `${hojeSP().slice(0, 8)}01`;
      const ultimoAnterior = somarDias(primeiroDeste, -1);
      return { de: `${ultimoAnterior.slice(0, 8)}01`, ate: ultimoAnterior };
    },
  },
  { id: '12m', rotulo: '12 meses', periodo: () => ({ de: somarDias(hojeSP(), -365), ate: hojeSP() }) },
];

export function numero(n: number | null | undefined, casas = 0): string {
  if (n === null || n === undefined) return '—';
  return n.toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas });
}

export function moeda(n: number | null | undefined): string {
  if (n === null || n === undefined) return '—';
  return n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 0 });
}

export function dias(n: number | null | undefined): string {
  return n === null || n === undefined ? '—' : `${numero(n, Number.isInteger(n) ? 0 : 1)} d`;
}

/** `dd/mm/aaaa`. */
export function diaBr(iso: string | null | undefined): string {
  if (!iso) return '—';
  const [a, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

/** `dd/mm` — eixo diário. */
export function diaCurto(iso: string): string {
  const [, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}`;
}

const MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

export function mesCurto(iso: string): string {
  const [a, m] = iso.slice(0, 10).split('-');
  return `${MESES[Number(m) - 1]}/${a.slice(2)}`;
}

/** Variação percentual do anterior para o atual; nulo quando não há base (anterior zero). */
export function variacao(atual: number, anterior: number): number | null {
  if (!anterior) return null;
  return Math.round(((atual - anterior) / anterior) * 1000) / 10;
}

export function mediana(valores: (number | null | undefined)[]): number | null {
  const v = valores.filter((x): x is number => typeof x === 'number').sort((a, b) => a - b);
  if (v.length === 0) return null;
  const meio = Math.floor(v.length / 2);
  return v.length % 2 ? v[meio] : Math.round(((v[meio - 1] + v[meio]) / 2) * 10) / 10;
}
