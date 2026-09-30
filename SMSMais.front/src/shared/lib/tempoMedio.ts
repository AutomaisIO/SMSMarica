/** Mesmo teto do backend: o tratamento cabe num dia. */
export const TEMPO_MEDIO_MAXIMO_MIN = 24 * 60;

/** 240 → "4h00"; 45 → "45 min"; null → "—". */
export function formatarDuracao(minutos: number | null | undefined): string {
  if (minutos == null) return '—';
  if (minutos < 60) return `${minutos} min`;
  const h = Math.floor(minutos / 60);
  const m = minutos % 60;
  return `${h}h${String(m).padStart(2, '0')}`;
}

/** Campos "horas" e "minutos" do formulário → total em minutos (null se vazio ou inválido). */
export function paraMinutos(horas: string, minutos: string): number | null {
  if (horas.trim() === '' && minutos.trim() === '') return null;
  const h = Number(horas || 0);
  const m = Number(minutos || 0);
  if (!Number.isInteger(h) || !Number.isInteger(m) || h < 0 || m < 0 || m > 59) return null;
  const total = h * 60 + m;
  return total >= 1 && total <= TEMPO_MEDIO_MAXIMO_MIN ? total : null;
}

/** Total em minutos → campos do formulário. */
export function deMinutos(total: number | null | undefined): { horas: string; minutos: string } {
  if (total == null) return { horas: '', minutos: '' };
  return { horas: String(Math.floor(total / 60)), minutos: String(total % 60).padStart(2, '0') };
}
