import { ARTIGOS } from '@/features/manual/registro';
import type { Artigo } from '@/features/manual/tipos';

export type ResultadoBusca = {
  artigo: Artigo;
  /** Quanto maior, mais em cima. */
  pontos: number;
  /** Seções do artigo que casaram — viram atalho direto para a âncora. */
  secoes: { id: string; titulo: string }[];
};

/**
 * Tira acento e caixa. Sem isso, "confirmacao" não acha "confirmação" — e é exatamente assim que
 * as pessoas digitam com pressa, no meio de um telefonema.
 */
export function normalizar(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim();
}

type Indice = {
  artigo: Artigo;
  titulo: string;
  resumo: string;
  chaves: string;
  secoes: { id: string; titulo: string; texto: string }[];
};

/**
 * Índice construído uma vez por carga da página. O conteúdo das seções é JSX (não é string), então
 * o que se indexa é o que o artigo DECLARA: título, resumo, palavras-chave e o campo `busca` de
 * cada seção. Artigo cuja seção não declara termos é achável pelo título dela — e por nada mais.
 */
const INDICE: Indice[] = ARTIGOS.map((artigo) => ({
  artigo,
  titulo: normalizar(artigo.titulo),
  resumo: normalizar(artigo.resumo),
  chaves: normalizar(artigo.palavrasChave.join(' ')),
  secoes: artigo.secoes().map((s) => ({
    id: s.id,
    titulo: s.titulo,
    texto: normalizar(`${s.titulo} ${s.busca ?? ''}`),
  })),
}));

/**
 * Busca por todos os termos (E, não OU): quem digita duas palavras está estreitando, não somando.
 * Devolve vazio para termo com menos de 2 caracteres — uma letra casa com tudo e não ajuda ninguém.
 */
export function buscar(termo: string): ResultadoBusca[] {
  const alvo = normalizar(termo);
  if (alvo.length < 2) return [];
  const partes = alvo.split(/\s+/).filter(Boolean);

  const resultados: ResultadoBusca[] = [];
  for (const item of INDICE) {
    let pontos = 0;
    const secoesCasadas = new Map<string, { id: string; titulo: string }>();

    for (const parte of partes) {
      let achouNaParte = false;
      if (item.titulo.includes(parte)) {
        pontos += 10;
        achouNaParte = true;
      }
      if (item.chaves.includes(parte)) {
        pontos += 6;
        achouNaParte = true;
      }
      if (item.resumo.includes(parte)) {
        pontos += 4;
        achouNaParte = true;
      }
      for (const s of item.secoes) {
        if (s.texto.includes(parte)) {
          pontos += 3;
          achouNaParte = true;
          secoesCasadas.set(s.id, { id: s.id, titulo: s.titulo });
        }
      }
      // Um termo que não aparece em lugar nenhum derruba o artigo inteiro.
      if (!achouNaParte) {
        pontos = 0;
        break;
      }
    }

    if (pontos > 0) {
      resultados.push({ artigo: item.artigo, pontos, secoes: [...secoesCasadas.values()].slice(0, 4) });
    }
  }

  return resultados.sort((a, b) => b.pontos - a.pontos || a.artigo.titulo.localeCompare(b.artigo.titulo, 'pt-BR'));
}
