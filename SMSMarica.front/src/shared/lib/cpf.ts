/**
 * Utilitários de CPF compartilhados (validação por dígito verificador + formatação).
 * Fonte única para evitar reimplementações divergentes pelas features.
 */

export function apenasDigitosCpf(valor: string): string {
  return (valor ?? '').replace(/\D/g, '');
}

/** Valida CPF pelos dígitos verificadores (rejeita tamanho ≠ 11 e sequências repetidas). */
export function cpfValido(valor: string): boolean {
  const digitos = apenasDigitosCpf(valor);
  if (digitos.length !== 11) return false;
  if (/^(\d)\1{10}$/.test(digitos)) return false;
  const calcDigito = (ate: number): number => {
    let soma = 0;
    for (let i = 0; i < ate; i += 1) soma += Number(digitos[i]) * (ate + 1 - i);
    const resto = (soma * 10) % 11;
    return resto === 10 ? 0 : resto;
  };
  return calcDigito(9) === Number(digitos[9]) && calcDigito(10) === Number(digitos[10]);
}

/** "12345678901" → "123.456.789-01"; devolve o valor original se não tiver 11 dígitos. */
export function formatarCpf(valor: string): string {
  const d = apenasDigitosCpf(valor);
  if (d.length !== 11) return valor;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

/** True quando o texto digitado tem cara de CPF completo (11 dígitos, só números/pontuação de CPF). */
export function pareceCpfCompleto(valor: string): boolean {
  const t = (valor ?? '').trim();
  if (t.length === 0) return false;
  if (!/^[\d.\s-]+$/.test(t)) return false; // tem letra → é busca por nome
  return apenasDigitosCpf(t).length === 11;
}
