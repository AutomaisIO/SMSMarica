import type { ReactNode } from 'react';
import { MousePointerClick, RotateCcw } from 'lucide-react';

/**
 * Moldura de tudo que é clicável no manual. A tarja existe por uma razão específica: a simulação
 * é uma cópia fiel da tela real, e quem chega no meio dela PRECISA saber, sem ler nada, que ali
 * ninguém é avisado, nada é cancelado e nenhum paciente existe.
 */
export function TelaSimulada({
  titulo = 'Simulação',
  descricao = 'Clique à vontade: os dados são inventados e nada aqui sai do seu navegador.',
  aoReiniciar,
  children,
}: {
  titulo?: string;
  descricao?: string;
  aoReiniciar?: () => void;
  children: ReactNode;
}) {
  return (
    <div className="overflow-hidden rounded-theme-lg border-2 border-dashed border-primary-300 bg-primary-50/40">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-dashed border-primary-200 bg-primary-50 px-4 py-2">
        <div className="flex items-center gap-2 text-primary-900">
          <MousePointerClick className="h-4 w-4 shrink-0" />
          <span className="text-sm font-semibold">{titulo}</span>
          <span className="hidden text-xs text-primary-800/80 sm:inline">· {descricao}</span>
        </div>
        {aoReiniciar ? (
          <button
            type="button"
            onClick={aoReiniciar}
            className="inline-flex items-center gap-1.5 rounded-md border border-primary-300 bg-white px-2.5 py-1 text-xs font-medium text-primary-800 hover:bg-primary-50"
          >
            <RotateCcw className="h-3.5 w-3.5" /> Recomeçar
          </button>
        ) : null}
      </div>
      <p className="px-4 pt-3 text-xs text-primary-900/80 sm:hidden">{descricao}</p>
      <div className="p-3 sm:p-4">{children}</div>
    </div>
  );
}
