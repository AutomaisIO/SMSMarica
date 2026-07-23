import { useEffect, useRef, useState } from 'react';
import { AlertTriangle, GripHorizontal, Send } from 'lucide-react';
import { useEnviarMensagem } from '@/features/conversas/api/queries';
import { useChat } from '@/features/conversas/store/chatStore';
import {
  ALTURA_MAX,
  ALTURA_MIN,
  useComposerPreferencias,
} from '@/features/conversas/store/composerPreferencias';
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

  // A altura é aplicada de forma DECLARATIVA (style={{ height }} no textarea) — vale sempre
  // que o textarea é renderizado, inclusive ao remontar (troca de conversa) ou quando ele só
  // aparece depois que a conversa carrega. Durante o arrasto mexemos direto no style (sem
  // re-render) e, ao soltar, gravamos a altura final na preferência (persiste no servidor);
  // o re-render seguinte sincroniza o style declarativo com o novo valor.

  // Alça de redimensionar no topo: arrastar para cima aumenta a caixa; para baixo diminui.
  const arrasteRef = useRef<{ y0: number; h0: number } | null>(null);

  function aoIniciarArraste(e: React.PointerEvent<HTMLDivElement>) {
    if (!taRef.current) return;
    e.preventDefault();
    arrasteRef.current = { y0: e.clientY, h0: taRef.current.offsetHeight };
    e.currentTarget.setPointerCapture(e.pointerId);
  }

  function aoArrastar(e: React.PointerEvent<HTMLDivElement>) {
    const a = arrasteRef.current;
    if (!a || !taRef.current) return;
    const nova = Math.min(ALTURA_MAX, Math.max(ALTURA_MIN, a.h0 + (a.y0 - e.clientY)));
    taRef.current.style.height = `${nova}px`;
  }

  function aoSoltarArraste(e: React.PointerEvent<HTMLDivElement>) {
    if (!arrasteRef.current || !taRef.current) return;
    arrasteRef.current = null;
    e.currentTarget.releasePointerCapture(e.pointerId);
    definirAltura(taRef.current.offsetHeight);
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
      {/* Alça de redimensionar: cursor de resize vertical; arraste p/ cima ou p/ baixo. */}
      <div
        onPointerDown={aoIniciarArraste}
        onPointerMove={aoArrastar}
        onPointerUp={aoSoltarArraste}
        role="separator"
        aria-orientation="horizontal"
        aria-label="Redimensionar a caixa de mensagem (arraste para cima ou para baixo)"
        title="Arraste para cima ou para baixo para redimensionar"
        className="group flex touch-none cursor-ns-resize items-center justify-center py-0.5"
      >
        <GripHorizontal className="h-3.5 w-5 text-gray-300 group-hover:text-gray-500" />
      </div>
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
          placeholder={
            enviarComEnter
              ? 'Escreva uma mensagem…  (Enter envia, Shift+Enter quebra linha)'
              : 'Escreva uma mensagem…  (Enter quebra linha — envie pelo botão)'
          }
          style={{ height: `${altura}px` }}
          className="flex-1 resize-none overflow-y-auto rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
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
