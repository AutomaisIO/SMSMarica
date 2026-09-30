/**
 * Dias da semana como bitmask: bit 0 = domingo, 1 = segunda … 6 = sábado — a mesma convenção do
 * DayOfWeek do .NET, que é como o backend grava (ex.: agenda do atendimento do transporte).
 */
export const DIAS_SEMANA: { bit: number; curto: string; label: string }[] = [
  { bit: 1 << 0, curto: 'Dom', label: 'Domingo' },
  { bit: 1 << 1, curto: 'Seg', label: 'Segunda-feira' },
  { bit: 1 << 2, curto: 'Ter', label: 'Terça-feira' },
  { bit: 1 << 3, curto: 'Qua', label: 'Quarta-feira' },
  { bit: 1 << 4, curto: 'Qui', label: 'Quinta-feira' },
  { bit: 1 << 5, curto: 'Sex', label: 'Sexta-feira' },
  { bit: 1 << 6, curto: 'Sáb', label: 'Sábado' },
];

/** Máscara → "Seg, Qua e Sex" (vazio se nenhum dia). Todos os 7 → "Todos os dias". */
export function descreverDias(mascara: number): string {
  if ((mascara & 127) === 127) return 'Todos os dias';
  const dias = DIAS_SEMANA.filter((d) => (mascara & d.bit) !== 0).map((d) => d.curto);
  if (dias.length <= 1) return dias.join('');
  return `${dias.slice(0, -1).join(', ')} e ${dias[dias.length - 1]}`;
}
