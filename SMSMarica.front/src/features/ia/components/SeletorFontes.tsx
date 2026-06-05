import { useEffect, useRef, useState } from 'react';
import { Check, ChevronDown, Database } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import type { FonteIa } from '@/features/ia/types';

type Props = {
  fontes: FonteIa[];
  selecionadas: string[];
  onChange: (ids: string[]) => void;
  carregando?: boolean;
  disabled?: boolean;
};

/** Dropdown de multi-seleção de bases. Exibe `nome · ambiente`. */
export function SeletorFontes({ fontes, selecionadas, onChange, carregando, disabled }: Props) {
  const [aberto, setAberto] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!aberto) return;
    function aoClicarFora(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener('mousedown', aoClicarFora);
    return () => document.removeEventListener('mousedown', aoClicarFora);
  }, [aberto]);

  function alternar(id: string) {
    onChange(
      selecionadas.includes(id)
        ? selecionadas.filter((x) => x !== id)
        : [...selecionadas, id],
    );
  }

  const rotulo =
    selecionadas.length === 0
      ? 'Selecione uma ou mais bases'
      : selecionadas.length === 1
        ? fontes.find((f) => f.id === selecionadas[0])?.nome ?? '1 base'
        : `${selecionadas.length} bases selecionadas`;

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        disabled={disabled || carregando}
        onClick={() => setAberto((a) => !a)}
        className={cn(
          'input flex w-full items-center justify-between gap-2 text-left',
          (disabled || carregando) && 'cursor-not-allowed opacity-60',
        )}
      >
        <span className="flex min-w-0 items-center gap-2">
          <Database className="h-4 w-4 flex-shrink-0 text-primary-600" />
          <span className={cn('truncate', selecionadas.length === 0 && 'text-gray-400')}>
            {carregando ? 'Carregando bases…' : rotulo}
          </span>
        </span>
        <ChevronDown className={cn('h-4 w-4 flex-shrink-0 text-gray-400 transition-transform', aberto && 'rotate-180')} />
      </button>

      {aberto ? (
        <div className="absolute z-20 mt-1 max-h-72 w-full overflow-y-auto rounded-md border border-gray-200 bg-white py-1 shadow-lg">
          {fontes.length === 0 ? (
            <div className="px-3 py-3 text-sm text-gray-500">Nenhuma base disponível.</div>
          ) : (
            fontes.map((f) => {
              const marcada = selecionadas.includes(f.id);
              return (
                <button
                  key={f.id}
                  type="button"
                  onClick={() => alternar(f.id)}
                  className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-gray-50"
                >
                  <span
                    className={cn(
                      'flex h-4 w-4 flex-shrink-0 items-center justify-center rounded border',
                      marcada ? 'border-primary-600 bg-primary-600 text-white' : 'border-gray-300',
                    )}
                  >
                    {marcada ? <Check className="h-3 w-3" /> : null}
                  </span>
                  <span className="min-w-0 flex-1 truncate text-gray-800">{f.nome}</span>
                  <span className="flex-shrink-0 rounded bg-gray-100 px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide text-gray-600">
                    {f.ambiente}
                  </span>
                </button>
              );
            })
          )}
        </div>
      ) : null}
    </div>
  );
}
