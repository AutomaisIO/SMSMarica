import { useState } from 'react';
import { FileText, Loader2 } from 'lucide-react';
import { abrirPdfLaudo } from '@/features/laudos/lib/pdf';
import { cn } from '@/shared/lib/cn';

/**
 * Botão (ícone) para abrir o laudo (PDF) em nova aba, para visualização/impressão
 * — o download fica a cargo do visualizador de PDF do navegador. Fica DESABILITADO
 * enquanto o laudo não estiver assinado digitalmente; o tooltip avisa o motivo.
 */
export function BotaoVisualizarLaudo({
  laudoId,
  assinado,
}: {
  laudoId: string | null;
  assinado: boolean;
}) {
  const [carregando, setCarregando] = useState(false);
  const pronto = Boolean(laudoId) && assinado;

  async function abrir() {
    if (!pronto || !laudoId || carregando) return;
    setCarregando(true);
    try {
      await abrirPdfLaudo(laudoId);
    } catch {
      alert('Não foi possível abrir o laudo.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <button
      type="button"
      onClick={abrir}
      disabled={!pronto || carregando}
      title={
        pronto
          ? 'Ver/imprimir laudo (PDF assinado)'
          : 'Laudo ainda não está pronto — não foi assinado digitalmente'
      }
      aria-label="Ver laudo"
      className={cn(
        'inline-flex items-center rounded p-0.5 transition-colors',
        pronto ? 'text-violet-600 hover:text-violet-800' : 'text-gray-300',
        'disabled:cursor-not-allowed',
        carregando && 'opacity-60',
      )}
    >
      {carregando ? (
        <Loader2 className="h-3.5 w-3.5 animate-spin" />
      ) : (
        <FileText className="h-3.5 w-3.5" />
      )}
    </button>
  );
}
