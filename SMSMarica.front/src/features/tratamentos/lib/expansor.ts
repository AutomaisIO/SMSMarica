/**
 * Espelha o algoritmo de ExpansorDePeriodicidade do backend. Usado para
 * mostrar a prévia das datas no wizard antes de salvar. O servidor sempre
 * re-valida/recalcula antes de persistir.
 */

export type RegraPeriodicidade = {
  tipo: 'Diaria' | 'IntervaloDias' | 'SemanaDiasFixos' | 'Manual';
  intervaloDias: number | null;
  diasSemanaMascara: number | null;
  dataInicio: string;
  quantidadeSessoes: number;
};

export const LIMITE_SESSOES = 365;

// Bit 0 = domingo, 1 = segunda, … 6 = sábado — mesma convenção de DayOfWeek do .NET.
export const DIAS_SEMANA: { bit: number; curto: string; label: string }[] = [
  { bit: 1 << 0, curto: 'Dom', label: 'Domingo' },
  { bit: 1 << 1, curto: 'Seg', label: 'Segunda-feira' },
  { bit: 1 << 2, curto: 'Ter', label: 'Terça-feira' },
  { bit: 1 << 3, curto: 'Qua', label: 'Quarta-feira' },
  { bit: 1 << 4, curto: 'Qui', label: 'Quinta-feira' },
  { bit: 1 << 5, curto: 'Sex', label: 'Sexta-feira' },
  { bit: 1 << 6, curto: 'Sáb', label: 'Sábado' },
];

function parseDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

function toIso(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${dd}`;
}

function addDays(d: Date, n: number): Date {
  const r = new Date(d);
  r.setDate(r.getDate() + n);
  return r;
}

export function expandir(regra: RegraPeriodicidade): string[] {
  const qtd = regra.quantidadeSessoes;
  if (qtd <= 0 || qtd > LIMITE_SESSOES) return [];
  if (!regra.dataInicio) return [];

  const inicio = parseDate(regra.dataInicio);
  if (Number.isNaN(inicio.getTime())) return [];

  if (regra.tipo === 'Diaria') {
    return Array.from({ length: qtd }, (_, i) => toIso(addDays(inicio, i)));
  }

  if (regra.tipo === 'IntervaloDias') {
    const intervalo = regra.intervaloDias ?? 0;
    if (intervalo < 1) return [];
    return Array.from({ length: qtd }, (_, i) => toIso(addDays(inicio, i * intervalo)));
  }

  if (regra.tipo === 'SemanaDiasFixos') {
    const mask = regra.diasSemanaMascara ?? 0;
    if (mask < 1 || mask > 127) return [];
    const datas: string[] = [];
    let d = new Date(inicio);
    const limite = qtd * 7 + 7;
    for (let i = 0; datas.length < qtd && i < limite; i++) {
      const bit = 1 << d.getDay();
      if ((mask & bit) !== 0) datas.push(toIso(d));
      d = addDays(d, 1);
    }
    return datas;
  }

  return [];
}

export function formatarDataBr(iso: string): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

export function diaSemanaCurto(iso: string): string {
  const d = parseDate(iso);
  return DIAS_SEMANA[d.getDay()]?.curto ?? '';
}
