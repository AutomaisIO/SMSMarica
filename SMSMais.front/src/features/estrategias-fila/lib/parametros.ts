import type {
  ChaveNumerica,
  Objetivo,
  ParametrosEstrategia,
  Projecao,
  RodadaResumo,
  StatusEstrategia,
} from '@/features/estrategias-fila/types';

export const CHAVES_NUMERICAS: ChaveNumerica[] = [
  'profissionais',
  'diasPorSemana',
  'horasPorDia',
  'atendimentosPorHora',
  'aproveitamento',
  'unidades',
  'entradaSemanal',
];

export const ROTULOS: Record<ChaveNumerica, { rotulo: string; dica: string; passo: number; unidade?: string }> = {
  profissionais: {
    rotulo: 'Profissionais',
    dica: 'Quantos profissionais atendem o procedimento na rede regulada.',
    passo: 1,
  },
  diasPorSemana: {
    rotulo: 'Dias por semana',
    dica: 'Média de dias por semana em que cada profissional atende (0 a 7).',
    passo: 0.5,
  },
  horasPorDia: {
    rotulo: 'Horas por dia',
    dica: 'Média de horas de atendimento por dia, por profissional.',
    passo: 0.5,
    unidade: 'h',
  },
  atendimentosPorHora: {
    rotulo: 'Atendimentos por hora',
    dica: 'Vagas de regulação que cada profissional rende por hora.',
    passo: 0.5,
  },
  aproveitamento: {
    rotulo: 'Aproveitamento',
    dica: 'Fração das vagas ofertadas que viram atendimento (medido nas últimas 8 semanas). Escala viva com vaga morta aparece aqui.',
    passo: 0.05,
  },
  unidades: {
    rotulo: 'Unidades executantes',
    dica: 'Quantas unidades executam. Não entra na conta da capacidade — é restrição e rótulo para as ações.',
    passo: 1,
  },
  entradaSemanal: {
    rotulo: 'Entrada por semana',
    dica: 'Pessoas novas por semana. Vem travada na média medida; destrave para simular a demanda crescendo ou caindo.',
    passo: 1,
  },
};

export const OBJETIVOS: { id: Objetivo; rotulo: string; dica: string }[] = [
  { id: 'zerar_em_semanas', rotulo: 'Zerar a fila até um prazo', dica: 'Menor acréscimo de recursos que zera no prazo.' },
  { id: 'equilibrio', rotulo: 'Parar de crescer', dica: 'Capacidade igual à entrada: a fila para de aumentar.' },
  { id: 'minimo_recursos', rotulo: 'Zerar o mais rápido com o que está travado', dica: 'Prazo mínimo com os recursos que você fixou.' },
];

export const STATUS_ROTULO: Record<StatusEstrategia, { rotulo: string; classe: string }> = {
  Rascunho: { rotulo: 'Rascunho', classe: 'bg-gray-100 text-gray-700' },
  Pronta: { rotulo: 'Pronta', classe: 'bg-sky-100 text-sky-800' },
  Aplicada: { rotulo: 'Aplicada', classe: 'bg-emerald-100 text-emerald-800' },
  Arquivada: { rotulo: 'Arquivada', classe: 'bg-gray-100 text-gray-500' },
};

export const DIAS_CURTOS = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'];

export function n(v: number | null | undefined, casas = 0): string {
  if (v === null || v === undefined || Number.isNaN(v)) return '—';
  return v.toLocaleString('pt-BR', { maximumFractionDigits: casas, minimumFractionDigits: 0 });
}

export function pct(v: number | null | undefined): string {
  if (v === null || v === undefined) return '—';
  return `${Math.round(v * 100)}%`;
}

export function usd(v: number | null | undefined): string {
  if (v === null || v === undefined) return '—';
  return `US$ ${v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 3 })}`;
}

export function dataHoraBr(iso: string | null | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

/** Capacidade semanal — a mesma fórmula do backend, para a tela reagir antes de simular. */
export function capacidadeSemanal(p: ParametrosEstrategia): number {
  return (
    Math.max(0, p.profissionais.valor) *
    Math.max(0, p.diasPorSemana.valor) *
    Math.max(0, p.horasPorDia.valor) *
    Math.max(0, p.atendimentosPorHora.valor) *
    Math.min(1, Math.max(0, p.aproveitamento.valor))
  );
}

/** Frase-resumo da projeção, a que vai no cartão e na lista. */
export function fraseProjecao(p: Projecao | null | undefined, prazo?: number | null): string {
  if (!p) return '—';
  if (p.zera && p.semanaZera !== null) {
    const semanas = p.semanaZera;
    const meses = semanas / 4.33;
    const base = semanas <= 8 ? `zera em ${semanas} semana${semanas === 1 ? '' : 's'}` : `zera em ${meses.toFixed(1).replace('.', ',')} meses (${semanas} sem.)`;
    if (prazo && semanas > prazo) return `${base} — passa do prazo de ${prazo}`;
    return base;
  }
  if (p.crescimentoSemanal > 0) return `não zera: cresce ${n(p.crescimentoSemanal, 1)}/semana`;
  return `não zera em ${p.horizonteSemanas} semanas (sobram ${n(p.filaFinal)})`;
}

/** A mesma frase, a partir do resumo de rodada da lista (que não carrega a série). */
export function fraseRodada(r: RodadaResumo | null): string {
  if (!r) return '—';
  if (r.falha) return 'falhou';
  if (r.zera && r.semanaZera !== null) {
    const meses = r.semanaZera / 4.33;
    return r.semanaZera <= 8
      ? `zera em ${r.semanaZera} semana${r.semanaZera === 1 ? '' : 's'}`
      : `zera em ${meses.toFixed(1).replace('.', ',')} meses (${r.semanaZera} sem.)`;
  }
  return 'não zera';
}

/** O que mudou entre dois conjuntos de parâmetros, em frases curtas. */
export function diferencas(antes: ParametrosEstrategia, depois: ParametrosEstrategia): string[] {
  const out: string[] = [];
  for (const chave of CHAVES_NUMERICAS) {
    const a = antes[chave].valor;
    const b = depois[chave].valor;
    if (Math.abs(a - b) > 1e-6) out.push(`${ROTULOS[chave].rotulo}: ${n(a, 2)} → ${n(b, 2)}`);
  }
  const ma = antes.mutiroes.reduce((s, m) => s + m.vagas, 0);
  const mb = depois.mutiroes.reduce((s, m) => s + m.vagas, 0);
  if (ma !== mb || antes.mutiroes.length !== depois.mutiroes.length)
    out.push(`Mutirões: ${antes.mutiroes.length} (${n(ma)} vagas) → ${depois.mutiroes.length} (${n(mb)} vagas)`);
  if (antes.objetivo !== depois.objetivo) out.push(`Objetivo: ${antes.objetivo} → ${depois.objetivo}`);
  if ((antes.prazoAlvoSemanas ?? null) !== (depois.prazoAlvoSemanas ?? null))
    out.push(`Prazo: ${antes.prazoAlvoSemanas ?? '—'} → ${depois.prazoAlvoSemanas ?? '—'} sem.`);
  return out;
}
