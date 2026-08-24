/**
 * Nomes de pessoa para EXIBIÇÃO. As bases de origem (SISREG, CADWEB, importações) gravam em
 * CAIXA ALTA; jogado cru numa mensagem de WhatsApp isso lê como grito. Só a apresentação
 * muda — o nome oficial no cadastro continua como veio.
 */

/** Partículas que ficam em minúscula no meio do nome (nunca na primeira palavra). */
const PARTICULAS = new Set([
  'de', 'da', 'do', 'das', 'dos', 'e', 'di', 'du', 'del', 'della', 'van', 'von', 'y',
]);

/** Hífen e apóstrofo capitalizam o pedaço seguinte (Jean-Pierre, D'Ávila). */
function capitalizarPalavra(palavra: string): string {
  return palavra
    .toLocaleLowerCase('pt-BR')
    .replace(/(^|[-'’])(\p{L})/gu, (_m, sep: string, letra: string) => sep + letra.toLocaleUpperCase('pt-BR'));
}

/** "MARIA DAS DORES DA SILVA" → "Maria das Dores da Silva". */
export function formatarNomeProprio(nome: string | null | undefined): string {
  if (!nome?.trim()) return '';
  return nome
    .trim()
    .split(/\s+/)
    .map((p, i) => (i > 0 && PARTICULAS.has(p.toLocaleLowerCase('pt-BR')) ? p.toLocaleLowerCase('pt-BR') : capitalizarPalavra(p)))
    .join(' ');
}

/** Primeiro nome, já capitalizado — é como o paciente é tratado na mensagem. */
export function primeiroNomeProprio(nome: string | null | undefined): string {
  const primeiro = (nome ?? '').trim().split(/\s+/)[0];
  return primeiro ? capitalizarPalavra(primeiro) : '';
}
