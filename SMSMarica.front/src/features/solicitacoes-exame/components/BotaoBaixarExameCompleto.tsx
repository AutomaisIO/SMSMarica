import { useState } from 'react';
import { FileDown, Loader2 } from 'lucide-react';
import { baixarExameCompleto } from '@/features/solicitacoes-exame/api/solicitacoesExameApi';
import { cn } from '@/shared/lib/cn';

/**
 * Botão (ícone) para baixar o exame completo num PDF único: capa com os dados,
 * imagens do PACS e, por último, o laudo. Pode demorar (baixa as imagens do PACS).
 */
export function BotaoBaixarExameCompleto({ solicitacaoId }: { solicitacaoId: string }) {
  const [carregando, setCarregando] = useState(false);

  async function baixar() {
    if (carregando) return;
    setCarregando(true);
    try {
      await baixarExameCompleto(solicitacaoId);
    } catch {
      alert('Não foi possível gerar o PDF do exame completo. Verifique se as imagens já estão no PACS.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <button
      type="button"
      onClick={baixar}
      disabled={carregando}
      title="Baixar exame completo (capa + imagens + laudo)"
      aria-label="Baixar exame completo"
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
