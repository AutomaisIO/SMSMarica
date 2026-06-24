import { useRef, useState } from 'react';
import { Code2, Eye, Image as ImageIcon, Loader2 } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { enviarMidia, urlMidiaAbsoluta } from '@/shared/api/midiaApi';

type Props = {
  /** HTML controlado pelo pai. */
  valorHtml: string;
  /** Emite o HTML a cada mudança (json fica vazio — este editor é HTML puro). */
  aoMudar: (valor: { html: string; json: string }) => void;
  somenteLeitura?: boolean;
  /** Habilita o upload de imagem (envia para /midias e insere a <img>). */
  permitirImagem?: boolean;
  categoriaImagem?: string;
  alturaMinima?: string;
  placeholder?: string;
};

/**
 * Editor de HTML cru com pré-visualização FIEL. Diferente do EditorRichText
 * (TipTap/WYSIWYG), aqui o "Visual" é o HTML renderizado pelo próprio browser
 * (dangerouslySetInnerHTML) — então tabelas, estilos, tamanho de imagem etc.
 * aparecem exatamente como no código, e nada é descartado ao alternar entre
 * Código e Visual. Indicado para conteúdo institucional (cabeçalho/rodapé do
 * laudo), onde o controle do HTML importa e o WYSIWYG limitado atrapalhava.
 * O conteúdo é sanitizado no backend ao salvar; aqui é o próprio admin
 * pré-visualizando o que digitou.
 */
export function EditorHtml({
  valorHtml,
  aoMudar,
  somenteLeitura,
  permitirImagem,
  categoriaImagem,
  alturaMinima = '320px',
  placeholder,
}: Props) {
  const [modo, setModo] = useState<'visual' | 'codigo'>('visual');
  const [enviandoImagem, setEnviandoImagem] = useState(false);
  const inputImagem = useRef<HTMLInputElement>(null);

  function emitir(html: string) {
    aoMudar({ html, json: '{}' });
  }

  async function aoEscolherImagem(arquivo: File | undefined) {
    if (!arquivo) return;
    setEnviandoImagem(true);
    try {
      const midia = await enviarMidia(arquivo, categoriaImagem);
      const src = urlMidiaAbsoluta(midia.url);
      const tag = `<p style="text-align: center"><img src="${src}" width="120"></p>`;
      emitir(`${valorHtml ?? ''}\n${tag}`);
      setModo('codigo'); // mostra onde a imagem entrou para o usuário posicionar
    } finally {
      setEnviandoImagem(false);
      if (inputImagem.current) inputImagem.current.value = '';
    }
  }

  const botao =
    'inline-flex items-center gap-1.5 rounded px-2.5 py-1 text-xs font-medium text-gray-600 hover:bg-gray-200';
  const ativo = 'bg-white text-primary-700 shadow-sm ring-1 ring-gray-200';

  return (
    <div className="overflow-hidden rounded-lg border border-gray-300 bg-white shadow-sm focus-within:border-primary-400 focus-within:ring-2 focus-within:ring-primary-100">
      {!somenteLeitura ? (
        <div className="flex items-center gap-1 border-b border-gray-200 bg-gray-50 px-2 py-1.5">
          <button type="button" onClick={() => setModo('visual')} className={cn(botao, modo === 'visual' && ativo)}>
            <Eye className="h-4 w-4" /> Visual
          </button>
          <button type="button" onClick={() => setModo('codigo')} className={cn(botao, modo === 'codigo' && ativo)}>
            <Code2 className="h-4 w-4" /> HTML
          </button>
          {permitirImagem ? (
            <>
              <span className="mx-1 h-5 w-px bg-gray-300" />
              <button
                type="button"
                onClick={() => inputImagem.current?.click()}
                disabled={enviandoImagem}
                className={cn(botao, 'disabled:opacity-60')}
              >
                {enviandoImagem ? <Loader2 className="h-4 w-4 animate-spin" /> : <ImageIcon className="h-4 w-4" />}
                Imagem
              </button>
              <input
                ref={inputImagem}
                type="file"
                accept="image/png,image/jpeg,image/gif,image/webp,image/svg+xml"
                className="hidden"
                onChange={(e) => void aoEscolherImagem(e.target.files?.[0])}
              />
            </>
          ) : null}
          <span className="ml-auto text-[11px] text-gray-400">
            {modo === 'codigo' ? 'Editando o HTML — Visual mostra o resultado fiel' : 'Pré-visualização fiel do HTML'}
          </span>
        </div>
      ) : null}

      {modo === 'codigo' && !somenteLeitura ? (
        <textarea
          value={valorHtml}
          onChange={(e) => emitir(e.target.value)}
          spellCheck={false}
          placeholder={placeholder}
          className="block w-full resize-y bg-gray-900 px-4 py-3 font-mono text-xs leading-relaxed text-gray-100 focus:outline-none"
          style={{ minHeight: alturaMinima }}
        />
      ) : (
        <div
          className="prose prose-sm max-w-none px-4 py-3 [&_img]:inline [&_table]:my-0 [&_table]:w-full [&_table]:border-collapse [&_td]:align-middle [&_td]:p-1"
          style={{ minHeight: alturaMinima }}
          // eslint-disable-next-line react/no-danger -- HTML institucional do próprio admin; sanitizado no backend ao salvar.
          dangerouslySetInnerHTML={{
            __html: valorHtml?.trim() ? valorHtml : '<p style="color:#9ca3af">Sem conteúdo. Clique em HTML para editar.</p>',
          }}
        />
      )}
    </div>
  );
}
