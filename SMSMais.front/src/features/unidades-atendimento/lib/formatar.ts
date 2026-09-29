import type { EnderecoUnidadeAtendimento } from '@/features/unidades-atendimento/types';

export function semAcento(texto: string): string {
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

export function cidadeUf(cidade: string | null, uf: string | null): string {
  if (!cidade) return '—';
  return uf ? `${cidade}/${uf}` : cidade;
}

/** "Rua X, 123 — Compl. — Bairro, Cidade/UF — CEP 00000-000" (omite o que faltar). */
export function enderecoEmLinha(e: EnderecoUnidadeAtendimento | null): string {
  if (!e) return '—';
  const rua = [e.logradouro, e.numero].filter(Boolean).join(', ');
  const cep = e.cep && e.cep.length === 8 ? `CEP ${e.cep.slice(0, 5)}-${e.cep.slice(5)}` : null;
  return [rua, e.complemento, [e.bairro, cidadeUf(e.cidade, e.uf)].filter(Boolean).join(', '), cep]
    .filter(Boolean)
    .join(' — ');
}
