import { hojeSP } from '@/shared/lib/datas';

/**
 * "Hoje em Brasília" mais um deslocamento em dias, como `aaaa-mm-dd`.
 *
 * <p>Apoia-se em {@link hojeSP} de propósito, em vez de refazer a conta de fuso: as duas telas da
 * Agenda tinham cada uma a sua cópia de `hojeBrasilia`, e cópia de regra de fuso é como as telas
 * passam a discordar sobre que dia é hoje depois das 21h.</p>
 *
 * <p>O deslocamento é feito em UTC (`Date.UTC`) para não atravessar horário de verão nem depender
 * do fuso da máquina de quem abre a tela — o dia base já veio correto de {@link hojeSP}.</p>
 */
export function diaBrasilia(offsetDias = 0): string {
  const [a, m, d] = hojeSP().split('-').map(Number);
  return new Date(Date.UTC(a, m - 1, d + offsetDias)).toISOString().slice(0, 10);
}

/** `aaaa-mm-dd` (ou ISO completo) como `dd/mm/aaaa`. Devolve travessão quando não há data. */
export function diaBr(iso: string | null | undefined): string {
  if (!iso) return '—';
  const [a, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

const MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

/** `aaaa-mm-dd` como `mai/26` — rótulo curto de eixo de gráfico. */
export function mesCurto(iso: string): string {
  const [a, m] = iso.slice(0, 10).split('-');
  return `${MESES[Number(m) - 1]}/${a.slice(2)}`;
}

/** `aaaa-mm-dd` como `05/09` — rótulo curto de eixo diário. */
export function diaCurto(iso: string): string {
  const [, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}`;
}
