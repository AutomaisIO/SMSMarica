import { useState, type ReactNode } from 'react';
import { cn } from '@/shared/lib/cn';

export type Aba = {
  id: string;
  rotulo: string;
  conteudo: ReactNode;
  badge?: string | number;
};

type Props = {
  abas: Aba[];
  inicial?: string;
  abaAtiva?: string;
  aoTrocarAba?: (id: string) => void;
};

export function Tabs({ abas, inicial, abaAtiva, aoTrocarAba }: Props) {
  const [interna, setInterna] = useState(inicial ?? abas[0]?.id);
  const ativa = abaAtiva ?? interna;
  const aba = abas.find((a) => a.id === ativa) ?? abas[0];

  function trocar(id: string) {
    if (aoTrocarAba) aoTrocarAba(id);
    else setInterna(id);
  }

  return (
    <div>
      <div className="border-b border-gray-200">
        <nav className="-mb-px flex flex-wrap gap-1" role="tablist">
          {abas.map((a) => (
            <button
              type="button"
              role="tab"
              aria-selected={a.id === ativa}
              key={a.id}
              onClick={() => trocar(a.id)}
              className={cn(
                'whitespace-nowrap px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors',
                a.id === ativa
                  ? 'border-red-600 text-red-700'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300',
              )}
            >
              {a.rotulo}
              {a.badge !== undefined ? (
                <span className="ml-2 rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
                  {a.badge}
                </span>
              ) : null}
            </button>
          ))}
        </nav>
      </div>
      <div className="pt-4">{aba?.conteudo}</div>
    </div>
  );
}
