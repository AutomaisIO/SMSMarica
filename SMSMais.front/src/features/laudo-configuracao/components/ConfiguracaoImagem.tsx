import { useRef, useState } from 'react';
import { ImagePlus, Loader2, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { enviarMidia, urlMidiaAbsoluta } from '@/shared/api/midiaApi';

type Props = {
  /** HTML atual (esperado: um único `<img>`). */
  valorHtml: string;
  aoMudar: (valor: { html: string; json: string }) => void;
  /** Categoria da mídia (ex.: `laudo-cabecalho`). */
  categoria: string;
  /** Rótulo para mensagens (ex.: "cabeçalho"). */
  rotulo: string;
  somenteLeitura?: boolean;
};

/** Extrai o `src` da primeira `<img>` do HTML (ou null). */
function extrairSrc(html: string): string | null {
  const m = html.match(/<img[^>]+src="([^"]+)"/i);
  return m ? m[1] : null;
}

/**
 * Configuração de cabeçalho/rodapé do laudo como UMA imagem (timbre). Mais
 * confiável que HTML editável: o que você envia é exatamente o que sai no PDF
 * (sem divergência entre editor TipTap e o renderer). A imagem ocupa a largura
 * da página (`width="100%"`); a altura é proporcional.
 */
export function ConfiguracaoImagem({ valorHtml, aoMudar, categoria, rotulo, somenteLeitura }: Props) {
  const src = extrairSrc(valorHtml || '');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  async function aoEnviar(arquivo: File | undefined) {
    if (!arquivo) return;
    setErro(null);
    setEnviando(true);
    try {
      const midia = await enviarMidia(arquivo, categoria);
      const url = urlMidiaAbsoluta(midia.url);
      aoMudar({ html: `<p style="text-align: center"><img src="${url}" width="100%"></p>`, json: '{}' });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
      if (inputRef.current) inputRef.current.value = '';
    }
  }

  return (
    <div className="space-y-3">
      <div className="rounded-lg border border-gray-200 bg-gray-50 p-4">
        {src ? (
          <img
            src={src}
            alt={rotulo}
            className="mx-auto max-h-48 max-w-full rounded border border-gray-300 bg-white"
          />
        ) : (
          <p className="py-8 text-center text-sm text-gray-400">
            Nenhuma imagem de {rotulo} enviada.
          </p>
        )}
      </div>

      {!somenteLeitura ? (
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={enviando}
            className="inline-flex items-center gap-2 rounded-md bg-primary-600 px-3 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-60"
          >
            {enviando ? <Loader2 className="h-4 w-4 animate-spin" /> : <ImagePlus className="h-4 w-4" />}
            {src ? 'Trocar imagem' : 'Enviar imagem'}
          </button>
          {src ? (
            <button
              type="button"
              onClick={() => aoMudar({ html: '', json: '{}' })}
              className="inline-flex items-center gap-2 rounded-md border border-red-300 bg-white px-3 py-2 text-sm font-medium text-red-700 hover:bg-red-50"
            >
              <Trash2 className="h-4 w-4" />
              Remover
            </button>
          ) : null}
          <input
            ref={inputRef}
            type="file"
            accept="image/png,image/jpeg,image/webp,image/svg+xml"
            className="hidden"
            onChange={(e) => void aoEnviar(e.target.files?.[0])}
          />
        </div>
      ) : null}

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      <p className="text-xs text-gray-500">
        A imagem ocupa a <strong>largura inteira da página</strong> no PDF; a altura é proporcional. Para boa
        nitidez, envie uma imagem <strong>larga</strong> — recomendado ~1400&nbsp;px de largura, em proporção
        de faixa (ex.: 1400×200). PNG ou JPG.
      </p>
    </div>
  );
}
