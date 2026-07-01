import { useState } from 'react';
import { FileDown, Loader2 } from 'lucide-react';
import { abrirExameCompleto } from '@/features/solicitacoes-exame/api/solicitacoesExameApi';
import { cn } from '@/shared/lib/cn';

/**
 * Botão (ícone) para abrir o exame completo num PDF único (capa + imagens do PACS +
 * laudo) em nova aba, para visualização/impressão no navegador — o download é a
 * opção nativa do visualizador. Pode demorar (baixa as imagens do PACS).
 */
export function BotaoBaixarExameCompleto({ solicitacaoId }: { solicitacaoId: string }) {
  const [carregando, setCarregando] = useState(false);

  async function abrir() {
    if (carregando) return;
    setCarregando(true);
    try {
      await abrirExameCompleto(solicitacaoId);
    } catch {
      alert('Não foi possível gerar o PDF do exame completo. Verifique se as imagens já estão no PACS.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <button
      type="button"
      onClick={abrir}
      disabled={carregando}
      title="Abrir exame completo (capa + imagens + laudo) para impressão/download"
      aria-label="Abrir exame completo"
      className={cn(
        'inline-flex items-center rounded p-0.5 text-primary-600 transition-colors hover:text-primary-800',
        carregando && 'opacity-60',
      )}
    >
      {carregando ? (
        <Loader2 className="h-3.5 w-3.5 animate-spin" />
      ) : (
        <FileDown className="h-3.5 w-3.5" />
      )}
    </button>
  );
}
