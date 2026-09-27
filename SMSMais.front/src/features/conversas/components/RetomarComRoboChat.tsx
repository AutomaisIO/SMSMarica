import { useEffect, useRef, useState } from 'react';
import { Bot, Loader2 } from 'lucide-react';
import { retomarConversaComRobo } from '@/features/conversas/api/conversasApi';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

/**
 * "Retomar com o robô": para conversas que MORRERAM sem resposta (código legado, erro, atendente
 * que não voltou), o robô manda um resumo do ponto em que a conversa parou e pergunta como a
 * pessoa quer seguir — aproveitando a janela de 24h enquanto ela está aberta. A conversa não muda
 * de dono. O desfecho (pedido feito / por que não deu) aparece AQUI no popover, não em toast.
 */
export function RetomarComRoboChat({ conversaId }: { conversaId: string }) {
  const [aberto, setAberto] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [feito, setFeito] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const ref = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!aberto) return;
    const fechar = (e: MouseEvent) => {
      if (!ref.current?.contains(e.target as Node)) setAberto(false);
    };
    document.addEventListener('mousedown', fechar);
    return () => document.removeEventListener('mousedown', fechar);
  }, [aberto]);

  useEffect(() => {
    setAberto(false);
    setFeito(false);
    setErro(null);
  }, [conversaId]);

  async function retomar() {
    setEnviando(true);
    setErro(null);
    try {
      await retomarConversaComRobo(conversaId);
      setFeito(true);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setAberto((v) => !v)}
        className="flex items-center gap-1 rounded-md border border-indigo-300 bg-indigo-50 px-2 py-1 text-[11px] font-medium text-indigo-700 hover:bg-indigo-100"
        title="O robô resume o ponto em que a conversa parou e pergunta como a pessoa quer seguir"
        aria-expanded={aberto}
      >
        <Bot className="h-3.5 w-3.5" /> Retomar c/ robô
      </button>
      {aberto && (
        <div className="absolute right-0 z-30 mt-1 w-80 rounded-md bg-white p-3 text-xs shadow-lg ring-1 ring-gray-200">
          {feito ? (
            <p className="text-emerald-700">
              <strong>Pedido feito.</strong> O robô envia nesta conversa, em instantes, um resumo do
              ponto em que ela parou e pergunta como a pessoa quer seguir.
            </p>
          ) : (
            <>
              <p className="text-gray-700">
                O robô vai enviar <strong>nesta conversa</strong> um resumo do ponto em que ela parou
                e perguntar como a pessoa quer seguir. Funciona enquanto a <strong>janela de 24h</strong>{' '}
                está aberta e com o robô ligado.
              </p>
              <div className="mt-2 flex gap-2">
                <button
                  type="button"
                  disabled={enviando}
                  onClick={retomar}
                  className="flex items-center gap-1.5 rounded-md bg-indigo-600 px-3 py-1.5 font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
                >
                  {enviando ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Bot className="h-3.5 w-3.5" />}
                  Enviar agora
                </button>
                <button
                  type="button"
                  onClick={() => setAberto(false)}
                  className="rounded-md px-3 py-1.5 text-gray-600 hover:bg-gray-100"
                >
                  Voltar
                </button>
              </div>
            </>
          )}
          {erro && <p className="mt-2 text-red-600">{erro}</p>}
        </div>
      )}
    </div>
  );
}
