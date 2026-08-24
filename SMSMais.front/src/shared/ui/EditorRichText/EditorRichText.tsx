import { useEffect, useRef, useState } from 'react';
import { EditorContent, useEditor, type Editor } from '@tiptap/react';
import { cn } from '@/shared/lib/cn';
import { enviarMidia, urlMidiaAbsoluta } from '@/shared/api/midiaApi';
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
  /** Habilita o botão de inserir imagem (upload para /midias). */
  permitirImagem?: boolean;
  /** Categoria gravada nas imagens enviadas (ex.: `laudo-cabecalho`). */
  categoriaImagem?: string;
};

export function EditorRichText({
  valorHtml,
  aoMudar,
  placeholder,
  somenteLeitura,
  className,
  refEditor,
  alturaMinima = '320px',
  permitirImagem,
  categoriaImagem,
}: Props) {
  const [modoSource, setModoSource] = useState(false);
  const [htmlSource, setHtmlSource] = useState(valorHtml || '');
  const [enviandoImagem, setEnviandoImagem] = useState(false);
  const inputImagem = useRef<HTMLInputElement>(null);

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
      if (modoSource) setHtmlSource(valorHtml || '');
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

  function alternarSource() {
    if (!editor) return;
    if (modoSource) {
      // Volta para o visual aplicando o que foi editado no source.
      editor.commands.setContent(htmlSource, false);
      aoMudar({ html: editor.getHTML(), json: JSON.stringify(editor.getJSON()) });
      setModoSource(false);
    } else {
      setHtmlSource(editor.getHTML());
      setModoSource(true);
    }
  }

  function aoEditarSource(valor: string) {
    setHtmlSource(valor);
    // Emite direto o HTML cru (o backend sanitiza); o JSON fica vazio até voltar ao visual.
    aoMudar({ html: valor, json: '{}' });
  }

  async function aoEscolherImagem(arquivo: File | undefined) {
    if (!arquivo || !editor) return;
    setEnviandoImagem(true);
    try {
      const midia = await enviarMidia(arquivo, categoriaImagem);
      const src = urlMidiaAbsoluta(midia.url);
      editor.chain().focus().setImage({ src, alt: midia.nomeArquivo }).run();
    } finally {
      setEnviandoImagem(false);
      if (inputImagem.current) inputImagem.current.value = '';
    }
  }

  return (
    <div
      className={cn(
        'overflow-hidden rounded-lg border border-gray-300 bg-white shadow-sm focus-within:border-primary-400 focus-within:ring-2 focus-within:ring-primary-100',
        className,
      )}
    >
      {!somenteLeitura ? (
        <ToolbarPadrao
          editor={editor}
          desabilitado={modoSource}
          modoSource={modoSource}
          aoAlternarSource={alternarSource}
          permitirImagem={permitirImagem}
          enviandoImagem={enviandoImagem}
          aoClicarImagem={() => inputImagem.current?.click()}
        />
      ) : null}

      {permitirImagem ? (
        <input
          ref={inputImagem}
          type="file"
          accept="image/png,image/jpeg,image/gif,image/webp,image/svg+xml"
          className="hidden"
          onChange={(e) => void aoEscolherImagem(e.target.files?.[0])}
        />
      ) : null}

      {modoSource ? (
        <textarea
          value={htmlSource}
          onChange={(e) => aoEditarSource(e.target.value)}
          spellCheck={false}
          className="block w-full resize-y bg-gray-900 px-4 py-3 font-mono text-xs leading-relaxed text-gray-100 focus:outline-none"
          style={{ minHeight: alturaMinima }}
        />
      ) : (
        <EditorContent
          editor={editor}
          className="prose prose-sm max-w-none px-4 py-3 focus:outline-none [&_.ProseMirror]:min-h-[var(--editor-min-h)] [&_.ProseMirror]:outline-none [&_.ProseMirror_img]:max-w-full [&_.ProseMirror_table]:border [&_.ProseMirror_td]:border [&_.ProseMirror_th]:border [&_.ProseMirror_td]:px-2 [&_.ProseMirror_th]:px-2 [&_.ProseMirror_td]:py-1 [&_.ProseMirror_th]:py-1 [&_.ProseMirror_p.is-editor-empty:first-child]:before:text-gray-400 [&_.ProseMirror_p.is-editor-empty:first-child]:before:content-[attr(data-placeholder)] [&_.ProseMirror_p.is-editor-empty:first-child]:before:pointer-events-none [&_.ProseMirror_p.is-editor-empty:first-child]:before:float-left [&_.ProseMirror_p.is-editor-empty:first-child]:before:h-0"
          style={{ ['--editor-min-h' as never]: alturaMinima } as React.CSSProperties}
        />
      )}
    </div>
  );
}
