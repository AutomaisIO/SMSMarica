/** Só os dígitos do CPF. */
export function digitosCpf(valor: string): string {
  return (valor ?? '').replace(/\D/g, '').slice(0, 11);
}

/** 000.000.000-00 enquanto digita. */
export function mascararCpf(valor: string): string {
  const d = digitosCpf(valor);
  if (d.length <= 3) return d;
  if (d.length <= 6) return `${d.slice(0, 3)}.${d.slice(3)}`;
  if (d.length <= 9) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6)}`;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

/** CPF válido pelos dígitos verificadores (rejeita sequências repetidas). */
export function cpfValido(valor: string): boolean {
  const d = digitosCpf(valor);
  if (d.length !== 11 || /^(\d)\1{10}$/.test(d)) return false;
  const digito = (ate: number) => {
    let soma = 0;
    for (let i = 0; i < ate; i += 1) soma += Number(d[i]) * (ate + 1 - i);
    const resto = (soma * 10) % 11;
    return resto === 10 ? 0 : resto;
  };
  return digito(9) === Number(d[9]) && digito(10) === Number(d[10]);
}

/** Mostra só o meio do CPF: ***.456.789-** */
export function cpfParcial(valor: string): string {
  const d = digitosCpf(valor);
  return d.length === 11 ? `***.${d.slice(3, 6)}.${d.slice(6, 9)}-**` : valor;
}
