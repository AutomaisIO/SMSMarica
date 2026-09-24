import { useState } from 'react';
import { FileText, Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { abrirLaudoDoExame } from '@/features/solicitacoes-exame/api/solicitacoesExameApi';
import { cn } from '@/shared/lib/cn';

/**
 * Botão (ícone) para abrir o laudo do exame (PDF) em nova aba, separado das imagens.
 *
 * Só sai o documento OFICIAL — assinado e aprovado pelo médico. Quem garante é o servidor
 * (`/solicitacoes-exame/{id}/laudo-pdf` responde 409 antes disso); a tela só antecipa:
 * - `assinado` informado (listagem): desabilitado enquanto o laudo não foi liberado;
 * - `assinado` ausente (detalhe, que não carrega o laudo): clicável, e o servidor explica
 *   se ainda não há laudo liberado.
 */
export function BotaoVisualizarLaudo({
  solicitacaoId,
  assinado,
}: {
  solicitacaoId: string;
  assinado?: boolean;
}) {
  const [carregando, setCarregando] = useState(false);
  const bloqueado = assinado === false;

  async function abrir() {
    if (bloqueado || carregando) return;
    setCarregando(true);
    try {
      await abrirLaudoDoExame(solicitacaoId);
    } catch (err) {
      alert(extrairMensagemDeErro(err));
    } finally {
      setCarregando(false);
    }
  }

  return (
    <button
      type="button"
      onClick={abrir}
      disabled={bloqueado || carregando}
      title={
        bloqueado
          ? 'Laudo ainda não liberado — fica disponível depois de assinado e aprovado pelo médico'
          : 'Ver/imprimir o laudo (documento separado das imagens)'
      }
      aria-label="Ver laudo"
      className={cn(
        'inline-flex items-center rounded p-0.5 transition-colors',
        bloqueado ? 'text-gray-300' : 'text-violet-600 hover:text-violet-800',
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
