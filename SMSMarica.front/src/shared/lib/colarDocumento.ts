import type { ClipboardEvent } from 'react';

/**
 * Handler de colagem para campos de busca: quando o texto colado parece um
 * documento formatado (CPF/CNS/telefone — só dígitos e separadores `.`, `-`, `/`,
 * `()`, espaço), insere apenas os DÍGITOS (some com `.` e `-`). Se o colado tiver
 * letras (ex.: um nome), deixa a colagem normal acontecer.
 *
 * Uso: `<Input onPaste={aoColarSoDigitosSeDocumento((v) => setBusca(v))} />`.
 */
export function aoColarSoDigitosSeDocumento(definir: (valor: string) => void) {
  return (e: ClipboardEvent<HTMLInputElement>) => {
    const texto = e.clipboardData.getData('text');
    if (!texto || !/^[\d.\-/()\s]+$/.test(texto)) return; // tem letra → colagem normal
    const digitos = texto.replace(/\D/g, '');
    if (!digitos) return;
    e.preventDefault();
    const input = e.currentTarget;
    const inicio = input.selectionStart ?? input.value.length;
    const fim = input.selectionEnd ?? input.value.length;
    definir(input.value.slice(0, inicio) + digitos + input.value.slice(fim));
  };
}
