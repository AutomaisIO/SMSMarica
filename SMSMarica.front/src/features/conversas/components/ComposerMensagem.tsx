import { useState } from 'react';
import { AlertTriangle, Send } from 'lucide-react';
import { useEnviarMensagem } from '@/features/conversas/api/queries';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

type Props = {
  conversaId: string;
  podeTextoLivre: boolean;
};

export function ComposerMensagem({ conversaId, podeTextoLivre }: Props) {
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const enviar = useEnviarMensagem();

  async function aoEnviar() {
    const t = texto.trim();
    if (!t) return;
    setErro(null);
    try {
      await enviar.mutateAsync({ id: conversaId, texto: t });
      setTexto('');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  if (!podeTextoLivre) {
    return (
      <div className="flex items-start gap-2 border-t border-gray-200 bg-amber-50 p-3 text-xs text-amber-800">
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
        <span>
          A janela de 24h expirou. Para reabrir, inicie uma nova conversa por <b>modelo</b> (template)
          aprovado — o cidadão precisa responder para liberar o texto livre.
        </span>
      </div>
    );
  }

  return (
    <div className="border-t border-gray-200 p-2">
      {erro && <p className="px-1 pb-1 text-xs text-red-600">{erro}</p>}
      <div className="flex items-end gap-2">
        <textarea
          value={texto}
          onChange={(e) => setTexto(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault();
              void aoEnviar();
            }
          }}
          rows={2}
          placeholder="Escreva uma mensagem…  (Enter envia, Shift+Enter quebra linha)"
          className="max-h-32 flex-1 resize-none rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
        />
        <button
          type="button"
          onClick={() => void aoEnviar()}
          disabled={enviar.isPending || !texto.trim()}
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary-600 text-white hover:bg-primary-700 disabled:opacity-40"
          aria-label="Enviar"
        >
          <Send className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
