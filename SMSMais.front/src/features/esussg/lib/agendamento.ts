import { formatarWallClock } from '@/shared/lib/datas';

/**
 * Data/hora do agendamento para exibir. O ESUS manda o texto pronto ("06/10/2026 13:15:00", hora
 * de Brasília — wall-clock, nunca instante); aqui só some com os segundos. Sem o texto, cai para a
 * data (DateOnly) sem hora.
 */
export function dataHoraAgendada(
  texto: string | null | undefined,
  data: string | null | undefined,
): string | null {
  const t = (texto ?? '').trim();
  const m = t.match(/^(\d{2}\/\d{2}\/\d{4})(?:\s+(\d{2}:\d{2})(?::\d{2})?)?$/);
  if (m) return m[2] && m[2] !== '00:00' ? `${m[1]} ${m[2]}` : m[1];
  if (t) return t;
  return data ? formatarWallClock(data) : null;
}
