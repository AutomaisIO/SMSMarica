import { useEffect, useRef, useState } from 'react';
import { AlertTriangle, Send } from 'lucide-react';
import { useEnviarMensagem } from '@/features/conversas/api/queries';
import { useChat } from '@/features/conversas/store/chatStore';
import { useComposerPreferencias } from '@/features/conversas/store/composerPreferencias';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

type Props = {
  conversaId: string;
  podeTextoLivre: boolean;
};

export function ComposerMensagem({ conversaId, podeTextoLivre }: Props) {
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const enviar = useEnviarMensagem();

  const altura = useComposerPreferencias((s) => s.altura);
  const enviarComEnter = useComposerPreferencias((s) => s.enviarComEnter);
  const definirAltura = useComposerPreferencias((s) => s.definirAltura);
  const definirEnviarComEnter = useComposerPreferencias((s) => s.definirEnviarComEnter);
  const taRef = useRef<HTMLTextAreaElement>(null);

  // Aplica a altura preferida no textarea (uncontrolled height): o usuário arrasta a alça
  // nativa e, ao soltar, gravamos a altura resultante nas preferências (persiste no servidor).
  useEffect(() => {
    if (taRef.current) taRef.current.style.height = `${altura}px`;
  }, [altura]);

  function aoTerminarResize() {
    const h = taRef.current?.offsetHeight;
    if (h && Math.abs(h - altura) > 1) definirAltura(h);
  }

  // Resposta rápida escolhida no painel: o texto já resolvido cai aqui para o operador
  // revisar. Nunca enviamos por ele — o clique é no atalho, o envio continua sendo dele.
  const rascunho = useChat((s) => s.rascunho);
  useEffect(() => {
    if (!rascunho || rascunho.conversaId !== conversaId) return;
    setTexto((atual) => (atual.trim() ? `${atual.trimEnd()}\n${rascunho.texto}` : rascunho.texto));
    useChat.getState().consumirRascunho();
  }, [rascunho, conversaId]);

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
          ref={taRef}
          value={texto}
          onChange={(e) => setTexto(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey && enviarComEnter) {
              e.preventDefault();
              void aoEnviar();
            }
          }}
          onMouseUp={aoTerminarResize}
          placeholder={
            enviarComEnter
              ? 'Escreva uma mensagem…  (Enter envia, Shift+Enter quebra linha)'
              : 'Escreva uma mensagem…  (Enter quebra linha — envie pelo botão)'
          }
          className="flex-1 resize-y overflow-y-auto rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
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
      <label className="mt-1 flex w-fit cursor-pointer select-none items-center gap-1.5 px-1 text-xs text-gray-500">
        <input
          type="checkbox"
          checked={enviarComEnter}
          onChange={(e) => definirEnviarComEnter(e.target.checked)}
          className="h-3.5 w-3.5 rounded border-gray-300 text-primary-600 focus:ring-primary-400"
        />
        Enviar com Enter
      </label>
    </div>
  );
}
