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
    Table.configure({ resizable: true }),
    TableRow,
    TableHeader,
    TableCell,
    Placeholder.configure({
      placeholder: placeholder ?? 'Escreva aqui…',
    }),
  ];
}
