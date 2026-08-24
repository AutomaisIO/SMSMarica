import { useEffect, useRef, useState } from 'react';
import { LayoutGrid } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

const MAX = 6;

export type Layout = { linhas: number; colunas: number };

type Props = {
  valor: Layout;
  onSelecionar: (l: Layout) => void;
};

/**
 * Picker estilo "passar o mouse e escolher N×N" (até 6×6). Ao passar o mouse
 * sobre a célula (linha, coluna), realça todas de (1,1) até ela e mostra o
 * rótulo; o clique confirma o layout.
 */
export function SeletorLayoutGrade({ valor, onSelecionar }: Props) {
  const [aberto, setAberto] = useState(false);
  const [hover, setHover] = useState<Layout | null>(null);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!aberto) return;
    function aoClicarFora(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener('mousedown', aoClicarFora);
    return () => document.removeEventListener('mousedown', aoClicarFora);
  }, [aberto]);

  const ativo = hover ?? valor;

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        title={`Dividir a tela (atual ${valor.linhas}×${valor.colunas})`}
        onClick={() => setAberto((a) => !a)}
        className={cn(
          'flex items-center gap-1 rounded-md p-2 text-gray-300 transition-colors hover:bg-gray-700 hover:text-white',
          aberto && 'bg-gray-700 text-white',
        )}
      >
        <LayoutGrid className="h-5 w-5" />
        <span className="text-xs font-medium">
          {valor.linhas}×{valor.colunas}
        </span>
      </button>

      {aberto ? (
        <div
          className="absolute left-0 top-full z-20 mt-1 rounded-lg border border-gray-700 bg-gray-900 p-2 shadow-marca-lg"
          onMouseLeave={() => setHover(null)}
        >
          <div className="mb-1.5 text-center text-xs font-medium text-gray-200">
            {ativo.linhas} × {ativo.colunas}
          </div>
          <div className="grid gap-1" style={{ gridTemplateColumns: `repeat(${MAX}, 1.25rem)` }}>
            {Array.from({ length: MAX * MAX }, (_, idx) => {
              const linha = Math.floor(idx / MAX) + 1;
              const coluna = (idx % MAX) + 1;
              const dentro = linha <= ativo.linhas && coluna <= ativo.colunas;
              return (
                <button
                  key={idx}
                  type="button"
                  onMouseEnter={() => setHover({ linhas: linha, colunas: coluna })}
                  onClick={() => {
                    onSelecionar({ linhas: linha, colunas: coluna });
                    setAberto(false);
                    setHover(null);
                  }}
                  className={cn(
                    'h-5 w-5 rounded-sm border transition-colors',
                    dentro
                      ? 'border-primary-400 bg-primary-600/60'
                      : 'border-gray-700 bg-gray-800',
                  )}
                />
              );
            })}
          </div>
        </div>
      ) : null}
    </div>
  );
}
