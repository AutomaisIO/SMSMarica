import type { Necessidades } from '@/features/tratamentos/types';

/** Condição preenchida o bastante para salvar (a ajuda marcada precisa de descrição). */
export function necessidadesValidas(n: Necessidades): boolean {
  return !n.necessitaAjuda || Boolean(n.ajudaDescricao?.trim());
}

/** Limpa a descrição da ajuda antes de mandar (vazia vira null). */
export function paraNecessidadesPayload(n: Necessidades): Necessidades {
  return { ...n, ajudaDescricao: n.necessitaAjuda ? n.ajudaDescricao?.trim() || null : null };
}
