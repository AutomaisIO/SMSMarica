/**
 * Formatação dos campos do espelho do ESUS. O ESUS entrega tudo como texto, no formato dele —
 * aqui só se traduz para exibição, sem reinterpretar o dado.
 */

/**
 * A cor da prioridade vem como o ESUS guarda (`0xd02224`). Devolve uma cor CSS (`#d02224`) ou
 * `null` quando o valor não é reconhecível — aí a prioridade aparece sem cor, e não com uma cor
 * inventada.
 */
export function corDaPrioridade(cor: string | null | undefined): string | null {
  const v = (cor ?? '').trim();
  const m = v.match(/^(?:0x|#)?([0-9a-f]{6})$/i);
  return m ? `#${m[1]}` : null;
}

/** CPF chega só com dígitos do banco; formatamos na exibição. */
export function formatarCpf(valor: string | null | undefined): string | null {
  const d = (valor ?? '').replace(/\D/g, '');
  if (d.length === 11) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
  return valor && valor.trim() !== '' ? valor : null;
}

export function simNao(v: boolean | null | undefined): string | null {
  if (v == null) return null;
  return v ? 'Sim' : 'Não';
}

/** "ABRAE  2297523" — o ESUS escreve o CNES colado ao nome; espaço duplo vira um só. */
export function limparTexto(v: string | null | undefined): string | null {
  const t = (v ?? '').replace(/\s+/g, ' ').trim();
  return t === '' ? null : t;
}
