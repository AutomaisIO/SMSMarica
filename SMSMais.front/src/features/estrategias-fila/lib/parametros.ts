import type { LinhaQuadro, Objetivo, ParametrosEstrategia, Projecao, RodadaResumo, StatusEstrategia } from '@/features/estrategias-fila/types';

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

/** Turnos, vagas e capacidade — a mesma fórmula do backend, para a tela reagir antes da resposta. */
export function turnosLinha(l: LinhaQuadro): number {
  return new Set(l.dias.filter((d) => d >= 0 && d <= 6)).size;
}

export function turnosSemanais(p: ParametrosEstrategia): number {
  return p.quadro.reduce((s, l) => s + turnosLinha(l), 0);
}

export function vagasSemanais(p: ParametrosEstrategia): number {
  return p.quadro.reduce((s, l) => s + turnosLinha(l) * Math.max(0, l.atendimentosPorTurno), 0);
}

export function capacidadeSemanal(p: ParametrosEstrategia): number {
  return vagasSemanais(p) * Math.min(1, Math.max(0, p.aproveitamento.valor));
}

/** Atendimentos por turno para um médico novo: a média do quadro, ou 10 sem oferta. */
export function atendimentosPorTurnoPadrao(p: ParametrosEstrategia): number {
  const comTurno = p.quadro.filter((l) => turnosLinha(l) > 0);
  if (comTurno.length === 0) return 10;
  const turnos = comTurno.reduce((s, l) => s + turnosLinha(l), 0);
  const vagas = comTurno.reduce((s, l) => s + turnosLinha(l) * l.atendimentosPorTurno, 0);
  return Math.round((vagas / turnos) * 100) / 100;
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

/** O que mudou entre dois conjuntos de parâmetros, em frases curtas — linha a linha do quadro. */
export function diferencas(antes: ParametrosEstrategia, depois: ParametrosEstrategia): string[] {
  const out: string[] = [];
  const antesPorId = new Map(antes.quadro.map((l) => [l.id, l]));
  for (const l of depois.quadro) {
    const a = antesPorId.get(l.id);
    if (!a) {
      out.push(`+ ${l.nome} (${l.unidade}): ${l.dias.map((d) => DIAS_CURTOS[d]).join(',') || 'sem dia'} × ${n(l.atendimentosPorTurno, 1)}`);
      continue;
    }
    const acesos = l.dias.filter((d) => !a.dias.includes(d)).map((d) => `+${DIAS_CURTOS[d]}`);
    const apagados = a.dias.filter((d) => !l.dias.includes(d)).map((d) => `−${DIAS_CURTOS[d]}`);
    const partes = [...acesos, ...apagados];
    if (Math.abs(a.atendimentosPorTurno - l.atendimentosPorTurno) > 1e-6) partes.push(`${n(a.atendimentosPorTurno, 1)}→${n(l.atendimentosPorTurno, 1)}/turno`);
    if (partes.length > 0) out.push(`${l.nome}: ${partes.join(' ')}`);
  }
  for (const a of antes.quadro) if (!depois.quadro.some((l) => l.id === a.id)) out.push(`− ${a.nome}`);
  if (Math.abs(antes.aproveitamento.valor - depois.aproveitamento.valor) > 1e-6)
    out.push(`Aproveitamento: ${Math.round(antes.aproveitamento.valor * 100)}% → ${Math.round(depois.aproveitamento.valor * 100)}%`);
  if (Math.abs(antes.entradaSemanal.valor - depois.entradaSemanal.valor) > 1e-6)
    out.push(`Entrada: ${n(antes.entradaSemanal.valor, 1)} → ${n(depois.entradaSemanal.valor, 1)}/sem`);
  const ma = antes.mutiroes.reduce((s, m) => s + m.vagas, 0);
  const mb = depois.mutiroes.reduce((s, m) => s + m.vagas, 0);
  if (ma !== mb || antes.mutiroes.length !== depois.mutiroes.length)
    out.push(`Mutirões: ${antes.mutiroes.length} (${n(ma)} vagas) → ${depois.mutiroes.length} (${n(mb)} vagas)`);
  if (antes.objetivo !== depois.objetivo) out.push(`Objetivo: ${antes.objetivo} → ${depois.objetivo}`);
  if ((antes.prazoAlvoSemanas ?? null) !== (depois.prazoAlvoSemanas ?? null))
    out.push(`Prazo: ${antes.prazoAlvoSemanas ?? '—'} → ${depois.prazoAlvoSemanas ?? '—'} sem.`);
  return out;
}
