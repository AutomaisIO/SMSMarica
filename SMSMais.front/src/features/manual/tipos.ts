import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';

/**
 * Uma seção do artigo. É a unidade de tudo: vira âncora (`#id`), entra no índice lateral
 * e é o que a busca encontra. Conteúdo é JSX de propósito — o manual precisa de simulação
 * e animação, não só de texto.
 */
export type SecaoArtigo = {
  /** Âncora estável. Muda = link antigo quebra; escolha e não mexa mais. */
  id: string;
  titulo: string;
  conteudo: ReactNode;
  /**
   * Termos extras para a busca. O texto dentro do JSX NÃO é indexável (é árvore de
   * componentes, não string), então o que precisa ser achado por palavra se declara aqui.
   */
  busca?: string;
};

export type Artigo = {
  /** Pedaço final da URL: `/app/manual/<slug>`. */
  slug: string;
  titulo: string;
  /** Uma frase — aparece no card do índice e no resultado da busca. */
  resumo: string;
  /** Grupo do índice (ver `GRUPOS`), normalmente a seção do menu onde a tela vive. */
  grupo: string;
  icone: LucideIcon;
  /**
   * Tela que este artigo documenta. É o que liga o "?" do título da tela ao artigo e o que
   * põe o botão "Abrir a tela" no topo.
   */
  rota?: string;
  /** Para quem é. Ex.: "Quem atende o telefone na unidade". */
  publico?: string;
  /** `AAAA-MM-DD` da última revisão do conteúdo — o manual envelhece e precisa mostrar isso. */
  atualizadoEm: string;
  palavrasChave: string[];
  /**
   * Seções do artigo. É função porque o conteúdo contém componentes vivos (simulações):
   * construir no render mantém tudo com estado limpo a cada abertura.
   */
  secoes: () => SecaoArtigo[];
};

export type GrupoManual = {
  id: string;
  titulo: string;
  descricao: string;
  icone: LucideIcon;
};
