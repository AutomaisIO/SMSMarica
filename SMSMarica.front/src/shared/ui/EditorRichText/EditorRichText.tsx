import { useEffect } from 'react';
import { EditorContent, useEditor, type Editor } from '@tiptap/react';
import { cn } from '@/shared/lib/cn';
import { extensoesPadrao } from './extensoesPadrao';
import { ToolbarPadrao } from './ToolbarPadrao';

type Props = {
  /** HTML controlado (vem do form ou template). */
  valorHtml: string;
  /** Recebe tanto o HTML (para PDF) quanto o JSON (para re-edição) toda vez que o documento muda. */
  aoMudar: (valor: { html: string; json: string }) => void;
  placeholder?: string;
  somenteLeitura?: boolean;
  className?: string;
  /** Permite expor a instância para o pai (ex.: carregar template substituindo o conteúdo). */
  refEditor?: (editor: Editor | null) => void;
  /** Altura mínima da área de edição. */
  alturaMinima?: string;
};

export function EditorRichText({
  valorHtml,
  aoMudar,
  placeholder,
  somenteLeitura,
  className,
  refEditor,
  alturaMinima = '320px',
}: Props) {
  const editor = useEditor({
    extensions: extensoesPadrao(placeholder),
    content: valorHtml || '',
    editable: !somenteLeitura,
    onUpdate: ({ editor }) => {
      aoMudar({ html: editor.getHTML(), json: JSON.stringify(editor.getJSON()) });
    },
  });

  // Sincroniza valor externo (ex.: carregar template, hidratar form) sem disparar onUpdate em loop.
  useEffect(() => {
    if (!editor) return;
    const atual = editor.getHTML();
    if ((valorHtml || '') !== atual) {
      editor.commands.setContent(valorHtml || '', false);
    }
  }, [editor, valorHtml]);

  useEffect(() => {
    if (!editor) return;
    editor.setEditable(!somenteLeitura);
  }, [editor, somenteLeitura]);

  useEffect(() => {
    refEditor?.(editor);
    return () => refEditor?.(null);
  }, [editor, refEditor]);

  return (
    <div
      className={cn(
        'overflow-hidden rounded-lg border border-gray-300 bg-white shadow-sm focus-within:border-primary-400 focus-within:ring-2 focus-within:ring-primary-100',
        className,
      )}
    >
      {!somenteLeitura ? <ToolbarPadrao editor={editor} /> : null}
      <EditorContent
        editor={editor}
        className="prose prose-sm max-w-none px-4 py-3 focus:outline-none [&_.ProseMirror]:min-h-[var(--editor-min-h)] [&_.ProseMirror]:outline-none [&_.ProseMirror_table]:border [&_.ProseMirror_td]:border [&_.ProseMirror_th]:border [&_.ProseMirror_td]:px-2 [&_.ProseMirror_th]:px-2 [&_.ProseMirror_td]:py-1 [&_.ProseMirror_th]:py-1 [&_.ProseMirror_p.is-editor-empty:first-child]:before:text-gray-400 [&_.ProseMirror_p.is-editor-empty:first-child]:before:content-[attr(data-placeholder)] [&_.ProseMirror_p.is-editor-empty:first-child]:before:pointer-events-none [&_.ProseMirror_p.is-editor-empty:first-child]:before:float-left [&_.ProseMirror_p.is-editor-empty:first-child]:before:h-0"
        style={{ ['--editor-min-h' as never]: alturaMinima } as React.CSSProperties}
      />
    </div>
  );
}
