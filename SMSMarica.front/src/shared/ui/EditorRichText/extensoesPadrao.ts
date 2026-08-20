import Image from '@tiptap/extension-image';
import Placeholder from '@tiptap/extension-placeholder';
import Table from '@tiptap/extension-table';
import TableCell from '@tiptap/extension-table-cell';
import TableHeader from '@tiptap/extension-table-header';
import TableRow from '@tiptap/extension-table-row';
import TextAlign from '@tiptap/extension-text-align';
import Underline from '@tiptap/extension-underline';
import StarterKit from '@tiptap/starter-kit';
import type { Extensions } from '@tiptap/react';

/**
 * Imagem que PRESERVA `width`/`height`. A extensão padrão só guarda src/alt/title,
 * então o `width` era descartado ao parsear o HTML — o que (1) fazia as logos
 * renderizarem no tamanho natural (grandes) no editor e (2) descartava ajustes de
 * tamanho feitos no modo HTML puro (`</>`) ao alternar para o visual. Com os
 * atributos mapeados, o browser dimensiona a imagem e o tamanho sobrevive ao
 * round-trip. O PDF já honra `width`.
 */
const ImagemComDimensoes = Image.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      width: {
        default: null,
        parseHTML: (el: HTMLElement) => el.getAttribute('width'),
        renderHTML: (attrs: { width?: string | null }) => (attrs.width ? { width: attrs.width } : {}),
      },
      height: {
        default: null,
        parseHTML: (el: HTMLElement) => el.getAttribute('height'),
        renderHTML: (attrs: { height?: string | null }) => (attrs.height ? { height: attrs.height } : {}),
      },
    };
  },
});

/**
 * Tabela que PRESERVA a `class`. O renderizador de PDF usa `class="laudo-tabela"`
 * para decidir entre tabela de dados (grade, cabeçalho, larguras) e tabela de
 * layout (o cabeçalho institucional logo|texto|logo). A extensão padrão não
 * guarda `class`, então o round-trip pelo editor apagava a marca e a tabela do
 * checklist voltava a sair como caixinhas empilhadas no PDF.
 *
 * Tabela sem classe continua sem classe — o cabeçalho institucional não muda.
 */
const TabelaComClasse = Table.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      class: {
        default: null,
        parseHTML: (el: HTMLElement) => el.getAttribute('class'),
        renderHTML: (attrs: { class?: string | null }) =>
          attrs.class ? { class: attrs.class } : {},
      },
    };
  },
});

export function extensoesPadrao(placeholder?: string): Extensions {
  return [
    StarterKit.configure({
      heading: { levels: [1, 2, 3] },
    }),
    Underline,
    // Imagens são enviadas para o store de mídia (/midias) e referenciadas por URL —
    // nunca base64, para manter o HTML leve e dedupável. Preserva width/height.
    ImagemComDimensoes.configure({ inline: false, allowBase64: false }),
    TextAlign.configure({ types: ['heading', 'paragraph'] }),
    TabelaComClasse.configure({ resizable: true }),
    TableRow,
    TableHeader,
    TableCell,
    Placeholder.configure({
      placeholder: placeholder ?? 'Escreva aqui…',
    }),
  ];
}
