/**
 * Normaliza o celular digitado para a forma canônica 55 + DDD + número (Maricá = DDD 21).
 * Anti-burro: aceita com/sem DDI, com 0 de tronco (021), com/sem DDD e com qualquer pontuação.
 *
 *  "999990000"        → 5521999990000   (sem DDD → assume 21)
 *  "21999990000"      → 5521999990000
 *  "021 99999-0000"   → 5521999990000   (tira o 0 de tronco)
 *  "+55 (21) 99999-0000" → 5521999990000
 *  "5521999990000"    → 5521999990000
 */
export function normalizarCelularBr(entrada: string): string {
  let d = (entrada || '').replace(/\D/g, '');
  if (!d) return '';
  // Já veio com DDI 55 (12 díg = fixo 8; 13 díg = celular 9) → aceita.
  if (d.startsWith('55') && (d.length === 12 || d.length === 13)) return d;
  // Remove 0(s) de tronco (021, 0xx).
  d = d.replace(/^0+/, '');
  // DDD + número (10 = fixo, 11 = celular) → só falta o DDI.
  if (d.length === 10 || d.length === 11) return '55' + d;
  // Só o número, sem DDD (8 fixo / 9 celular) → assume Maricá (21).
  if (d.length === 8 || d.length === 9) return '5521' + d;
  // Fallback: qualquer outra coisa que não tenha DDI recebe 5521.
  return d.startsWith('55') ? d : '5521' + d;
}

/** Formata a forma canônica para exibição: (DDD) 9XXXX-XXXX. */
export function formatarCelularBr(canon: string): string {
  const d = (canon || '').replace(/\D/g, '');
  const nac = d.startsWith('55') ? d.slice(2) : d;
  if (nac.length < 10) return canon;
  const ddd = nac.slice(0, 2);
  const num = nac.slice(2);
  const noveDigitos = num.length === 9;
  const meio = noveDigitos ? num.slice(0, 5) : num.slice(0, 4);
  const fim = noveDigitos ? num.slice(5) : num.slice(4);
  return `(${ddd}) ${meio}-${fim}`;
}
