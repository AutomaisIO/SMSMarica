import { useState } from 'react';
import { Check, Copy } from 'lucide-react';

/**
 * Célula com um código (ex.: nº da solicitação) que copia ao clicar — evita o
 * operador selecionar texto na tela. Mostra um "copiado!" por ~1,5s.
 */
export function CodigoCopiavel({ codigo }: { codigo: string }) {
  const [copiado, setCopiado] = useState(false);

  if (!codigo) return <span className="text-xs text-gray-400">—</span>;

  async function copiar() {
    try {
      await navigator.clipboard.writeText(codigo);
    } catch {
      // Fallback p/ contextos sem Clipboard API (http antigo / permissão).
      const ta = document.createElement('textarea');
      ta.value = codigo;
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
      title="Clique para copiar o código da solicitação"
      className="group relative inline-flex w-full items-center justify-between gap-1.5 rounded px-1.5 py-1 font-mono text-xs text-gray-700 hover:bg-gray-100"
    >
      <span className="truncate">{codigo}</span>
      {copiado ? (
        <Check className="h-3.5 w-3.5 shrink-0 text-emerald-600" />
      ) : (
        <Copy className="h-3.5 w-3.5 shrink-0 text-gray-400 group-hover:text-gray-600" />
      )}
      {copiado ? (
        <span className="absolute -top-6 left-1/2 z-10 -translate-x-1/2 whitespace-nowrap rounded bg-gray-900 px-2 py-0.5 font-sans text-[10px] text-white shadow">
          copiado!
        </span>
      ) : null}
    </button>
  );
}
