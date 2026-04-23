import type { ReactNode } from 'react';
import { cn } from '@/shared/lib/cn';

export type Coluna<T> = {
  chave: string;
  cabecalho: string;
  render: (item: T) => ReactNode;
  className?: string;
};

type Props<T> = {
  colunas: Coluna<T>[];
  dados: T[];
  chaveLinha: (item: T) => string;
  vazio?: ReactNode;
  carregando?: boolean;
};

export function Tabela<T>({ colunas, dados, chaveLinha, vazio, carregando }: Props<T>) {
  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white shadow-sm">
      <table className="min-w-full divide-y divide-gray-200">
        <thead className="bg-gray-50">
          <tr>
            {colunas.map((c) => (
              <th
                key={c.chave}
                className={cn(
                  'px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500',
                  c.className,
                )}
              >
                {c.cabecalho}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {carregando ? (
            <tr>
              <td colSpan={colunas.length} className="px-4 py-8 text-center text-sm text-gray-500">
                Carregando…
              </td>
            </tr>
          ) : dados.length === 0 ? (
            <tr>
              <td colSpan={colunas.length} className="px-4 py-8 text-center text-sm text-gray-500">
                {vazio ?? 'Nenhum registro encontrado.'}
              </td>
            </tr>
          ) : (
            dados.map((item) => (
              <tr key={chaveLinha(item)} className="hover:bg-gray-50">
                {colunas.map((c) => (
                  <td
                    key={c.chave}
                    className={cn('px-4 py-3 text-sm text-gray-700', c.className)}
                  >
                    {c.render(item)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
