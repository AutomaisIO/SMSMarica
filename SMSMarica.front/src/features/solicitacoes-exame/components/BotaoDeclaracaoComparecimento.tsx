import { useState } from 'react';
import { Loader2, Receipt } from 'lucide-react';
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
        'inline-flex items-center rounded-md border border-emerald-300 bg-emerald-50 px-1.5 py-1 text-emerald-700 transition-colors hover:bg-emerald-100',
        carregando && 'opacity-60',
      )}
    >
      {carregando ? (
        <Loader2 className="h-3.5 w-3.5 animate-spin" />
      ) : (
        <Receipt className="h-3.5 w-3.5" />
      )}
    </button>
  );
}
