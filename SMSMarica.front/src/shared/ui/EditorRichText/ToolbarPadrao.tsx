import {
  AlignCenter,
  AlignLeft,
  AlignRight,
  Bold,
  Heading1,
  Heading2,
  Heading3,
  Italic,
  List,
  ListOrdered,
  Redo,
  Table as TableIcon,
  Trash2,
  Underline as UnderlineIcon,
  Undo,
} from 'lucide-react';
import type { Editor } from '@tiptap/react';
import { cn } from '@/shared/lib/cn';

type Props = { editor: Editor | null; desabilitado?: boolean };

export function ToolbarPadrao({ editor, desabilitado }: Props) {
  if (!editor) return null;

  function botao(
    icone: React.ReactNode,
    titulo: string,
    aoClicar: () => void,
    ativo?: boolean,
  ) {
    return (
      <button
        type="button"
        title={titulo}
        aria-label={titulo}
        disabled={desabilitado}
        onMouseDown={(e) => e.preventDefault()}
        onClick={aoClicar}
        className={cn(
          'inline-flex h-8 w-8 items-center justify-center rounded-md text-gray-600 hover:bg-gray-100 hover:text-gray-900 disabled:cursor-not-allowed disabled:opacity-50',
          ativo && 'bg-primary-50 text-primary-700',
        )}
      >
        {icone}
      </button>
    );
  }

  return (
    <div className="flex flex-wrap items-center gap-0.5 border-b border-gray-200 bg-gray-50 px-2 py-1.5">
      {botao(<Bold className="h-4 w-4" />, 'Negrito (Ctrl+B)', () => editor.chain().focus().toggleBold().run(), editor.isActive('bold'))}
      {botao(<Italic className="h-4 w-4" />, 'Itálico (Ctrl+I)', () => editor.chain().focus().toggleItalic().run(), editor.isActive('italic'))}
      {botao(<UnderlineIcon className="h-4 w-4" />, 'Sublinhado (Ctrl+U)', () => editor.chain().focus().toggleUnderline().run(), editor.isActive('underline'))}

      <span className="mx-1 h-5 w-px bg-gray-300" />

      {botao(<Heading1 className="h-4 w-4" />, 'Título 1', () => editor.chain().focus().toggleHeading({ level: 1 }).run(), editor.isActive('heading', { level: 1 }))}
      {botao(<Heading2 className="h-4 w-4" />, 'Título 2', () => editor.chain().focus().toggleHeading({ level: 2 }).run(), editor.isActive('heading', { level: 2 }))}
      {botao(<Heading3 className="h-4 w-4" />, 'Título 3', () => editor.chain().focus().toggleHeading({ level: 3 }).run(), editor.isActive('heading', { level: 3 }))}

      <span className="mx-1 h-5 w-px bg-gray-300" />

      {botao(<List className="h-4 w-4" />, 'Lista', () => editor.chain().focus().toggleBulletList().run(), editor.isActive('bulletList'))}
      {botao(<ListOrdered className="h-4 w-4" />, 'Lista numerada', () => editor.chain().focus().toggleOrderedList().run(), editor.isActive('orderedList'))}

      <span className="mx-1 h-5 w-px bg-gray-300" />

      {botao(<AlignLeft className="h-4 w-4" />, 'Alinhar à esquerda', () => editor.chain().focus().setTextAlign('left').run(), editor.isActive({ textAlign: 'left' }))}
      {botao(<AlignCenter className="h-4 w-4" />, 'Centralizar', () => editor.chain().focus().setTextAlign('center').run(), editor.isActive({ textAlign: 'center' }))}
      {botao(<AlignRight className="h-4 w-4" />, 'Alinhar à direita', () => editor.chain().focus().setTextAlign('right').run(), editor.isActive({ textAlign: 'right' }))}

      <span className="mx-1 h-5 w-px bg-gray-300" />

      {botao(<TableIcon className="h-4 w-4" />, 'Inserir tabela 3x3', () =>
        editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run())}
      {editor.isActive('table')
        ? botao(<Trash2 className="h-4 w-4" />, 'Excluir tabela', () => editor.chain().focus().deleteTable().run())
        : null}

      <span className="mx-1 h-5 w-px bg-gray-300" />

      {botao(<Undo className="h-4 w-4" />, 'Desfazer (Ctrl+Z)', () => editor.chain().focus().undo().run())}
      {botao(<Redo className="h-4 w-4" />, 'Refazer (Ctrl+Y)', () => editor.chain().focus().redo().run())}
    </div>
  );
}
