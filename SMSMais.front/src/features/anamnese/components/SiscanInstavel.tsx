import { AlertTriangle, Loader2 } from 'lucide-react';

import { Button } from '@/shared/ui/Button';
import {
  SISCAN_MAX_TENTATIVAS,
  type EstadoRetrySiscan,
} from '@/features/anamnese/lib/useRetrySiscan';

/**
 * O que a pessoa vê enquanto o SISCAN está instável e a tela re-tenta sozinha: spinner com a
 * tentativa atual e a contagem até a próxima, e — esgotadas as tentativas — o recado para tentar
 * mais tarde. Renderiza nada quando `ocioso`.
 */
export function SiscanInstavel({
  estado,
  aoTentarDeNovo,
  aoFechar,
}: {
  estado: EstadoRetrySiscan;
  /** Recomeça as tentativas do zero (só faz sentido no estado esgotado). */
  aoTentarDeNovo: () => void;
  aoFechar: () => void;
}) {
  if (estado.fase === 'ocioso') return null;

  if (estado.fase === 'esgotado') {
    return (
      <div className="space-y-4">
        <div className="flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-3 text-sm text-amber-900">
          <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" />
          <div>
            <p className="font-semibold">O sistema do SISCAN está instável.</p>
            <p className="mt-1">
              Tentamos {SISCAN_MAX_TENTATIVAS} vezes e ele não respondeu. É instabilidade do lado do
              SISCAN — comum em horário de pico. Por favor, tente de novo mais tarde.
            </p>
          </div>
        </div>
        <div className="flex justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar}>
            Fechar
          </Button>
          <Button onClick={aoTentarDeNovo}>Tentar de novo</Button>
        </div>
      </div>
    );
  }

  const tentativa = estado.fase === 'tentando' ? estado.tentativa : estado.tentativa;
  return (
    <div className="flex flex-col items-center gap-3 px-3 py-8 text-center">
      <Loader2 className="h-7 w-7 animate-spin text-teal-600" />
      <div className="text-sm text-gray-700">
        <p className="font-medium">O SISCAN está lento ou com problemas.</p>
        {estado.fase === 'aguardando' ? (
          <p className="mt-1">
            Tentando de novo em <span className="font-semibold tabular-nums">{estado.segundos}</span>
            {estado.segundos === 1 ? ' segundo' : ' segundos'}…
          </p>
        ) : (
          <p className="mt-1">Tentando conectar…</p>
        )}
        <p className="mt-1 text-xs text-gray-500">
          Tentativa {tentativa} de {SISCAN_MAX_TENTATIVAS}
        </p>
      </div>
    </div>
  );
}
