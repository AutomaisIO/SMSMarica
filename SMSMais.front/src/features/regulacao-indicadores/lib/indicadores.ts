import { hojeSP, numero } from '@/shared/lib/estatisticasPeriodo';
import type {
  Fonte,
  FonteIndicadores,
  Formato,
  IndicadoresRegulacao,
  PeriodoMeses,
  SecaoIndicador,
  Selo,
  SerieIndicador,
} from '../types';

/** Como cada sistema se apresenta no título — fixo, para o título não piscar enquanto carrega. */
export const FONTES_INDICADORES: Record<FonteIndicadores, { sigla: string; fonte: Fonte }> = {
  sisreg: { sigla: 'SISREG', fonte: 'Sisreg' },
  ser: { sigla: 'SER', fonte: 'Ser' },
  sernit: { sigla: 'SERNIT', fonte: 'Sernit' },
  esussg: { sigla: 'ESUS SG', fonte: 'EsusSg' },
};

/** Teto do backend: mais que isso vira 400. */
export const MAXIMO_MESES = 24;

// ------------------------------------------------------------------------------------ selos
export const SELOS: { selo: Selo; rotulo: string; descricao: string }[] = [
  {
    selo: 'Oficial',
    rotulo: 'Oficial',
    descricao: 'Número lido do próprio sistema de origem (ou do espelho fiel dele mantido pela Secretaria).',
  },
  {
    selo: 'Calculado',
    rotulo: 'Calculado',
    descricao: 'Derivado a partir de dados oficiais; a regra do cálculo está na nota do indicador.',
  },
  {
    selo: 'Parcial',
    rotulo: 'Parcial',
    descricao: 'Sabidamente incompleto — o valor é um piso; a nota diz o que falta.',
  },
  {
    selo: 'Indisponivel',
    rotulo: 'Indisponível',
    descricao: 'O sistema de origem não fornece esse dado ao município.',
  },
];

export function rotuloSelo(selo: Selo): string {
  return SELOS.find((s) => s.selo === selo)?.rotulo ?? selo;
}

// ------------------------------------------------------------------------------------ meses
const MESES_ABR = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

/** `'2026-03'` → `'mar'`. */
export function mesAbrev(mes: string): string {
  return MESES_ABR[Number(mes.slice(5, 7)) - 1] ?? mes;
}

/** `'2026-03'` → `'mar/26'`. */
export function mesAno(mes: string): string {
  return `${mesAbrev(mes)}/${mes.slice(2, 4)}`;
}

/** `'2026-03'` → `'mar/2026'`. */
export function mesAnoLongo(mes: string): string {
  return `${mesAbrev(mes)}/${mes.slice(0, 4)}`;
}

/** Soma `n` meses a `'AAAA-MM'`. */
export function somarMeses(mes: string, n: number): string {
  const a = Number(mes.slice(0, 4));
  const m = Number(mes.slice(5, 7)) - 1 + n;
  const ano = a + Math.floor(m / 12);
  const mm = ((m % 12) + 12) % 12;
  return `${ano}-${String(mm + 1).padStart(2, '0')}`;
}

/** Quantos meses de `inicio` a `fim`, contando os dois. */
export function contarMeses(inicio: string, fim: string): number {
  const a = Number(inicio.slice(0, 4)) * 12 + Number(inicio.slice(5, 7));
  const b = Number(fim.slice(0, 4)) * 12 + Number(fim.slice(5, 7));
  return b - a + 1;
}

/** Mês corrente no relógio de Brasília, `'AAAA-MM'`. */
export function mesAtual(): string {
  return hojeSP().slice(0, 7);
}

/** Último mês fechado (o anterior ao corrente). */
export function ultimoMesFechado(): string {
  return somarMeses(mesAtual(), -1);
}

/** Meses do período agrupados por ano civil, na ordem. */
export function mesesPorAno(meses: string[]): { ano: string; meses: string[] }[] {
  const grupos: { ano: string; meses: string[] }[] = [];
  for (const m of meses) {
    const ano = m.slice(0, 4);
    const g = grupos[grupos.length - 1];
    if (g && g.ano === ano) g.meses.push(m);
    else grupos.push({ ano, meses: [m] });
  }
  return grupos;
}

/** `2025` quando o ano está inteiro no período; `2026 (jan–ago)` quando está pela metade. */
export function rotuloAno(ano: string, meses: string[]): string {
  if (meses.length === 12 || meses.length === 0) return ano;
  const ini = mesAbrev(meses[0]);
  const fim = mesAbrev(meses[meses.length - 1]);
  return ini === fim ? `${ano} (${ini})` : `${ano} (${ini}–${fim})`;
}

// ------------------------------------------------------------------------------------ presets
export type PresetPeriodo = { id: string; rotulo: string; dica: string; periodo: () => PeriodoMeses };

/**
 * Atalhos de período. "Últimos 12 meses" manda o período vazio de propósito: quem decide o que são
 * os 12 meses fechados é o backend, e a tela mostra o que ele escolheu.
 */
export function presetsPeriodo(): PresetPeriodo[] {
  const ano = Number(mesAtual().slice(0, 4));
  const fechado = ultimoMesFechado();
  const inicioAno = `${ano}-01`;
  return [
    {
      id: '12m',
      rotulo: 'Últimos 12 meses',
      dica: 'Os 12 últimos meses já fechados.',
      periodo: () => ({ inicio: '', fim: '' }),
    },
    {
      id: 'ano-anterior',
      rotulo: String(ano - 1),
      dica: `Janeiro a dezembro de ${ano - 1}.`,
      periodo: () => ({ inicio: `${ano - 1}-01`, fim: `${ano - 1}-12` }),
    },
    {
      id: 'ano-atual',
      rotulo: `${ano} até agora`,
      dica: `De janeiro de ${ano} até o último mês fechado.`,
      // Em janeiro ainda não há mês fechado no ano: fica o próprio janeiro (parcial).
      periodo: () => ({ inicio: inicioAno, fim: fechado < inicioAno ? inicioAno : fechado }),
    },
  ];
}

/** Por que o período não pode ser pedido (ou nulo, se pode). */
export function problemaPeriodo(p: PeriodoMeses): string | null {
  if (!p.inicio && !p.fim) return null;
  if (!p.inicio || !p.fim) return 'Escolha o mês inicial e o final.';
  if (p.inicio > p.fim) return 'O mês inicial é depois do mês final.';
  if (contarMeses(p.inicio, p.fim) > MAXIMO_MESES) return `Escolha no máximo ${MAXIMO_MESES} meses.`;
  return null;
}

// ------------------------------------------------------------------------------------ valores
/** Número de uma célula conforme o formato da série. */
export function formatarValor(v: number | null | undefined, formato: Formato): string {
  if (v === null || v === undefined) return '—';
  if (formato === 'Percentual') return `${numero(v, 1)}%`;
  return numero(v, Number.isInteger(v) ? 0 : 1);
}

export type Agregado = { valor: number | null; tipo: string };

/**
 * Fecha a série num bloco de meses (um ano do período).
 *
 * Quando o backend manda `anual[ano]`, vale ele — percentual é razão das somas e tempo é mediana de
 * todos os casos, e nenhum dos dois sai somando ou tirando média dos meses. Sem ele, a série diz
 * como fechar: contagem soma, estoque (fila) fica com o último mês, o resto é média.
 */
export function agregar(serie: SerieIndicador, meses: string[], ano?: string): Agregado {
  const anual = ano ? serie.anual?.[ano] : undefined;
  if (anual !== undefined && anual !== null) return { valor: anual, tipo: 'no ano' };
  const vals = meses
    .map((m) => serie.valores[m])
    .filter((v): v is number => typeof v === 'number');
  if (vals.length === 0) return { valor: null, tipo: '' };
  switch (serie.agregacao) {
    case 'UltimoMes':
      return { valor: vals[vals.length - 1], tipo: 'último mês' };
    case 'Media':
      return { valor: Math.round((vals.reduce((a, b) => a + b, 0) / vals.length) * 10) / 10, tipo: 'média' };
    default:
      return { valor: vals.reduce((a, b) => a + b, 0), tipo: 'total' };
  }
}

/** Rótulo sem o recuo que o relatório antigo usava (dois espaços) — o recuo agora é `subitem`. */
export function rotuloLimpo(rotulo: string): string {
  return rotulo.trim();
}

/** Célula de tabela que é número (vai à direita). */
export function pareceNumero(v: string | null): boolean {
  if (v === null) return true;
  return /^[−-]?\d[\d.,]*\s?(%|d|dias)?$/.test(v.trim()) || v.trim() === '—';
}

// ------------------------------------------------------------------------------------ resumo
export type ItemResumo = {
  rotulo: string;
  serie: SerieIndicador | null;
  /** A seção existe mas o sistema não fornece (ex.: absenteísmo no ESUS). */
  indisponivel: boolean;
};

type Especificacao = { rotulo: string; secao: string; prefixos: string[] };

function especificacao(fonte: Fonte): Especificacao[] {
  const comuns = {
    agendamentos: { rotulo: 'Agendamentos', secao: 'vagas', prefixos: ['Vagas utilizadas'] },
    fila: {
      rotulo: 'Fila no fim do período',
      secao: 'fila',
      prefixos: ['Pacientes em fila no fim', 'Solicitações em fila no fim'],
    },
    absenteismo: { rotulo: 'Absenteísmo', secao: 'absenteismo', prefixos: ['Absenteísmo'] },
    canceladas: {
      rotulo: fonte === 'Sisreg' ? 'Marcações canceladas' : 'Canceladas',
      secao: 'desfechos',
      prefixos: ['Marcações canceladas', 'Canceladas'],
    },
    espera: {
      rotulo: fonte === 'Sisreg' || fonte === 'EsusSg' ? 'Espera até o atendimento (mediana, dias)' : 'Espera até agendar (mediana, dias)',
      secao: 'espera',
      prefixos: ['Espera até o atendimento — mediana', 'Espera — mediana'],
    },
  };
  // SISREG: o regulado é o número da regulação; nos espelhos, é o volume de pedidos que entrou.
  const segundo: Especificacao =
    fonte === 'Sisreg'
      ? { rotulo: 'Regulados', secao: 'regulados', prefixos: ['Total regulado'] }
      : { rotulo: 'Solicitações registradas', secao: 'fila', prefixos: ['Solicitações registradas'] };
  return [comuns.agendamentos, segundo, comuns.fila, comuns.absenteismo, comuns.canceladas, comuns.espera];
}

function acharSerie(secao: SecaoIndicador | undefined, prefixos: string[]): SerieIndicador | null {
  if (!secao) return null;
  for (const p of prefixos) {
    const s = secao.series.find((x) => rotuloLimpo(x.rotulo).startsWith(p));
    if (s) return s;
  }
  return null;
}

/** Os seis números do topo — a mesma escolha do relatório em PDF. */
export function itensResumo(dados: IndicadoresRegulacao): ItemResumo[] {
  return especificacao(dados.fonte)
    .map((e) => {
      const secao = dados.secoes.find((s) => s.id === e.secao);
      const serie = acharSerie(secao, e.prefixos);
      return { rotulo: e.rotulo, serie, indisponivel: Boolean(secao?.indisponivel) || serie?.selo === 'Indisponivel' };
    })
    .filter((i) => i.serie || i.indisponivel);
}

/** "30/09/2026 15:22" no relógio de Brasília; se não for data, devolve como veio. */
export function dataHoraBr(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('pt-BR', {
    timeZone: 'America/Sao_Paulo',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
