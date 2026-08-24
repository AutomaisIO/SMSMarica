import { useState } from 'react';
import { Check, Copy } from 'lucide-react';

/**
 * Célula com um código (ex.: nº da solicitação, CPF, CNS) que copia ao clicar — evita o
 * operador selecionar texto na tela. Mostra um "copiado!" por ~1,5s.
 * `valorCopiar` permite exibir formatado mas copiar cru (ex.: CPF sem pontuação).
 */
export function CodigoCopiavel({
  codigo,
  valorCopiar,
  dica,
}: {
  codigo: string;
  /** O que vai para a área de transferência (default: o próprio código exibido). */
  valorCopiar?: string;
  /** Tooltip do botão (default: o texto do código da solicitação). */
  dica?: string;
}) {
  const [copiado, setCopiado] = useState(false);

  if (!codigo) return <span className="text-xs text-gray-400">—</span>;

  async function copiar() {
    const valor = valorCopiar ?? codigo;
    try {
      await navigator.clipboard.writeText(valor);
    } catch {
      // Fallback p/ contextos sem Clipboard API (http antigo / permissão).
      const ta = document.createElement('textarea');
      ta.value = valorCopiar ?? codigo;
      ta.style.position = 'fixed';
      ta.style.opacity = '0';
      document.body.appendChild(ta);
      ta.select();
      try {
        document.execCommand('copy');
      } catch {
        /* ignora */
      }
      document.body.removeChild(ta);
    }
    setCopiado(true);
    window.setTimeout(() => setCopiado(false), 1500);
  }

  return (
    <button
      type="button"
      onClick={copiar}
      title={dica ?? 'Clique para copiar o código da solicitação'}
      className="group relative inline-flex items-center gap-1.5 rounded px-1.5 py-1 font-mono hover:bg-gray-100"
    >
      <span className="truncate">{codigo}</span>
      {copiado ? (
        <Check className="h-3.5 w-3.5 shrink-0 text-emerald-600" />
      ) : (
        <Copy className="h-3.5 w-3.5 shrink-0 text-gray-400 group-hover:text-gray-600" />
      )}
      {copiado ? (
        <span className="absolute -top-6 left-1/2 z-10 -translate-x-1/2 whitespace-nowrap rounded bg-gray-900 px-2 py-0.5 text-[10px] text-white shadow">
          copiado!
        </span>
      ) : null}
    </button>
  );
}
