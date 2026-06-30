import { useState } from 'react';
import { FileCheck, Loader2 } from 'lucide-react';
import { abrirDeclaracaoComparecimento } from '@/features/solicitacoes-exame/api/solicitacoesExameApi';
import { cn } from '@/shared/lib/cn';

/**
 * Ícone de "recibo" ao lado do badge Realizada/Laudada. Abre a declaração de
 * comparecimento (PDF) da solicitação em nova aba.
 */
export function BotaoDeclaracaoComparecimento({ solicitacaoId }: { solicitacaoId: string }) {
  const [carregando, setCarregando] = useState(false);

  async function abrir() {
    if (carregando) return;
    setCarregando(true);
    try {
      await abrirDeclaracaoComparecimento(solicitacaoId);
    } catch {
      alert('Não foi possível gerar a declaração de comparecimento.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <button
      type="button"
      onClick={abrir}
      disabled={carregando}
      title="Declaração de comparecimento (recibo)"
      aria-label="Declaração de comparecimento"
      className={cn(
        'inline-flex items-center rounded p-0.5 text-emerald-600 transition-colors hover:text-emerald-800',
        carregando && 'opacity-60',
      )}
    >
      {carregando ? (
        <Loader2 className="h-3.5 w-3.5 animate-spin" />
      ) : (
        <FileCheck className="h-3.5 w-3.5" />
      )}
    </button>
  );
}
