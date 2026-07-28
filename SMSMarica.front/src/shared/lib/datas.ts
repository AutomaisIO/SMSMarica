/**
 * Utilitários centrais de data/hora — REGRA ÚNICA do sistema:
 *  (a) INSTANTE UTC (timestamptz do servidor, ISO com 'Z') → exibir SEMPRE convertendo
 *      para America/Sao_Paulo FIXO (nunca o fuso do browser/SO).
 *  (b) WALL-CLOCK LOCAL (data_estudo do DICOM, datas-só) → exibir AS-IS, sem tocar no fuso.
 *  (c) ENTRADA de formulário (input datetime-local / "hoje") → interpretar como Brasília e
 *      converter para UTC antes de enviar.
 *
 * Brasil (SP/RJ) é UTC-3 fixo (sem horário de verão desde 2019).
 */
export const TZ_BR = 'America/Sao_Paulo';

/** Uma string ISO carrega fuso (é um instante) se termina com Z ou tem offset ±hh:mm. */
function temFuso(iso: string): boolean {
  return /[zZ]$/.test(iso) || /[+-]\d{2}:\d{2}$/.test(iso);
}

/**
 * (a) Formata data/hora com AUTO-DETECÇÃO de fuso:
 *  - string COM fuso (Z/offset) = instante UTC → converte para America/Sao_Paulo FIXO.
 *  - string SEM fuso = wall-clock local → exibe AS-IS (sem conversão de zona).
 * Assim o mesmo util serve p/ instantes UTC (timestamptz) e wall-clock (data_estudo, agenda).
 */
export function formatarInstante(
  iso: string | null | undefined,
  opts: Intl.DateTimeFormatOptions = { dateStyle: 'short', timeStyle: 'short' },
): string {
  if (!iso) return '—';
  const s = String(iso);
  const d = new Date(s);
  if (Number.isNaN(d.getTime())) return s;
  // Sem fuso na string: new Date() interpreta no fuso do browser e toLocaleString exibe no mesmo
  // fuso → mostra o wall-clock literal (cancela). Com fuso: força America/Sao_Paulo.
  return d.toLocaleString('pt-BR', temFuso(s) ? { timeZone: TZ_BR, ...opts } : { ...opts });
}

/** (a) Só a data, com a mesma auto-detecção de fuso do {@link formatarInstante}. */
export function formatarInstanteData(iso: string | null | undefined): string {
  if (!iso) return '—';
  const s = String(iso);
  const d = new Date(s);
  if (Number.isNaN(d.getTime())) return s;
  return d.toLocaleDateString('pt-BR', temFuso(s) ? { timeZone: TZ_BR } : {});
}

/** (b) Wall-clock local (ISO sem fuso, ou data-só) → "dd/mm/aaaa[ hh:mm]" AS-IS (regex, sem Date). */
export function formatarWallClock(iso: string | null | undefined): string {
  if (!iso) return '—';
  const s = String(iso);
  const m = s.match(/^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2}))?/);
  if (!m) return s;
  const data = `${m[3]}/${m[2]}/${m[1]}`;
  return m[4] ? `${data} ${m[4]}:${m[5]}` : data;
}

/** (c) Valor de <input datetime-local> (wall-clock Brasília) → ISO UTC (com Z). */
export function paraUtcDeLocal(inputLocal: string | null | undefined): string | null {
  if (!inputLocal) return null;
  const m = inputLocal.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/);
  if (!m) return null;
  const [, y, mo, d, h, mi] = m;
  // wall-clock em Brasília (-03:00) → instante UTC.
  return new Date(`${y}-${mo}-${d}T${h}:${mi}:00-03:00`).toISOString();
}

const MS_HORA = 3_600_000;
/** Brasil (SP/RJ) é UTC-3 fixo — sem horário de verão desde 2019. */
const OFFSET_BR_HORAS = -3;

/**
 * (a→planilha) ISO → `Date` pronto para virar célula de data no Excel.
 *
 * O Excel guarda data como número absoluto, sem fuso, e o `exceljs` serializa os campos **UTC**
 * do `Date`. Gravar o instante cru, portanto, mostraria a hora em UTC na célula — três horas
 * adiantada. Aqui o instante é deslocado para que seus campos UTC sejam o relógio de Brasília;
 * wall-clock (ISO sem fuso) passa direto, porque já é o relógio local.
 */
export function paraDataPlanilha(iso: string | null | undefined): Date | null {
  if (!iso) return null;
  const s = String(iso);
  const m = s.match(/^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2}))?)?/);
  if (!m) return null;

  if (!temFuso(s)) {
    return new Date(
      Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3]), Number(m[4] ?? 0), Number(m[5] ?? 0), Number(m[6] ?? 0)),
    );
  }

  const d = new Date(s);
  return Number.isNaN(d.getTime()) ? null : new Date(d.getTime() + OFFSET_BR_HORAS * MS_HORA);
}

/** Partes de uma data em Brasília, no formato estável en-CA. */
function partesSP(d: Date): { data: string; hora: string } {
  const dp = new Intl.DateTimeFormat('en-CA', {
    timeZone: TZ_BR, year: 'numeric', month: '2-digit', day: '2-digit',
  }).format(d);
  const hp = new Intl.DateTimeFormat('en-GB', {
    timeZone: TZ_BR, hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).format(d);
  return { data: dp, hora: hp };
}

/** (c) Instante UTC → "aaaa-mm-ddThh:mm" em Brasília, para preencher <input datetime-local>. */
export function paraInputLocalDeUtc(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const { data, hora } = partesSP(d);
  return `${data}T${hora}`;
}

/** (c) "Hoje" em Brasília como "aaaa-mm-dd" (substitui new Date().toISOString().slice(0,10)). */
export function hojeSP(): string {
  return partesSP(new Date()).data;
}

/** (c) "Agora" em Brasília como "aaaa-mm-ddThh:mm" (para default de <input datetime-local>). */
export function agoraInputLocalSP(): string {
  return paraInputLocalDeUtc(new Date().toISOString());
}
