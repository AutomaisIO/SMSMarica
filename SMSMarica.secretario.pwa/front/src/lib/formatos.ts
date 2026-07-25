/** Formatação pt-BR centralizada — nenhum número cru na UI. */

const fmtInteiro = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 });
const fmtDecimal = new Intl.NumberFormat('pt-BR', {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
});

export function formatarInteiro(n: number): string {
  return fmtInteiro.format(Math.round(n));
}

export function formatarDecimal(n: number): string {
  return fmtDecimal.format(n);
}

/** "86,3" → "86%" (arredondado — leitura executiva). */
export function formatarPct(n: number): string {
  return `${fmtInteiro.format(Math.round(n))}%`;
}

/**
 * Percentual que NÃO some quando é pequeno: abaixo de 10% mantém uma casa, para
 * "27 de 6.039" não virar "0%" ao lado do próprio 27 e parecer contradição.
 */
export function formatarPctFino(n: number): string {
  if (n > 0 && n < 10) return `${fmtDecimal.format(n)}%`;
  return formatarPct(n);
}

/** Minutos legíveis: 42 → "42 min" · 116 → "1h56" · 1440 → "24h". */
export function minutosLegiveis(min: number): string {
  const m = Math.round(min);
  if (m < 60) return `${m} min`;
  const h = Math.floor(m / 60);
  const resto = m % 60;
  return resto === 0 ? `${h}h` : `${h}h${String(resto).padStart(2, '0')}`;
}

/** Fuso ÚNICO de exibição — regra "Brasília fixo" do monorepo. */
const FUSO_BRASILIA = 'America/Sao_Paulo';

/** ISO com offset → "16:20" (sempre em America/Sao_Paulo, independente do fuso do aparelho). */
export function horaMinuto(iso: string): string {
  return new Date(iso).toLocaleTimeString('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
    timeZone: FUSO_BRASILIA,
  });
}

/** Date → "16:20:31" (sempre em America/Sao_Paulo). */
export function horaMinutoSegundo(data: Date): string {
  return data.toLocaleTimeString('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    timeZone: FUSO_BRASILIA,
  });
}

/** Data corrente em America/Sao_Paulo no formato ISO "yyyy-mm-dd". */
export function diaHojeBrasilia(agora: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: FUSO_BRASILIA,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(agora);
}

/** "2026-07-24" → Date local (sem deslocamento de fuso). */
export function parseDia(diaIso: string): Date {
  const [ano, mes, dia] = diaIso.split('-').map(Number);
  return new Date(ano, mes - 1, dia);
}

/** "2026-07-24" → "quinta-feira, 24 de julho". */
export function diaPorExtenso(diaIso: string): string {
  return parseDia(diaIso).toLocaleDateString('pt-BR', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  });
}

export function ehFimDeSemana(diaIso: string): boolean {
  const dow = parseDia(diaIso).getDay();
  return dow === 0 || dow === 6;
}

export function diaDoMes(diaIso: string): number {
  return parseDia(diaIso).getDate();
}

/** Idade de um instante ISO em minutos, relativa a `agora`. */
export function idadeEmMinutos(iso: string, agora: Date): number {
  return (agora.getTime() - new Date(iso).getTime()) / 60_000;
}

/** "julho/2026" → "julho" (rótulo curto do segmented control, vindo do contrato). */
export function nomeDoMes(rotulo: string): string {
  return rotulo.split('/')[0] ?? rotulo;
}
