import { useLayoutEffect, useRef, useState, type ReactNode } from 'react';
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
  /**
   * Barra de rolagem horizontal FLUTUANTE: quando a tabela é mais larga que a
   * área visível, uma barra fica grudada na base da viewport (enquanto a tabela
   * estiver à vista), para o operador não precisar rolar até o fim para arrastá-la.
   */
  scrollXFlutuante?: boolean;
  /**
   * Torna a LINHA inteira clicável (cursor "dedinho" + tooltip). Cliques em
   * botões/links/inputs dentro da linha são ignorados (as ações da linha seguem
   * funcionando), então não é preciso stopPropagation em cada botão.
   */
  aoClicarLinha?: (item: T) => void;
  /** Tooltip da linha clicável. Default: "Clique para visualizar". */
  dicaLinha?: string;
};

export function Tabela<T>({ colunas, dados, chaveLinha, vazio, carregando, scrollXFlutuante, aoClicarLinha, dicaLinha }: Props<T>) {
  // Defensivo: se a API retornar algo não-array (HTML por URL errada, erro
  // serializado, etc.), renderiza vazio em vez de derrubar a tela toda.
  const dadosSeguros: T[] = Array.isArray(dados) ? dados : [];

  const scrollerRef = useRef<HTMLDivElement>(null);
  const barraRef = useRef<HTMLDivElement>(null);
  const [larguraConteudo, setLarguraConteudo] = useState(0);
  const [precisaBarra, setPrecisaBarra] = useState(false);

  useLayoutEffect(() => {
    if (!scrollXFlutuante) return;
    const el = scrollerRef.current;
    if (!el) return;
    const medir = () => {
      setLarguraConteudo(el.scrollWidth);
      setPrecisaBarra(el.scrollWidth > el.clientWidth + 1);
    };
    medir();
    const ro = new ResizeObserver(medir);
    ro.observe(el);
    window.addEventListener('resize', medir);
    return () => {
      ro.disconnect();
      window.removeEventListener('resize', medir);
    };
  }, [scrollXFlutuante, dadosSeguros.length, colunas.length]);

  const sincronizarDaBarra = () => {
    if (scrollerRef.current && barraRef.current) scrollerRef.current.scrollLeft = barraRef.current.scrollLeft;
  };
  const sincronizarDoScroller = () => {
    if (scrollerRef.current && barraRef.current) barraRef.current.scrollLeft = scrollerRef.current.scrollLeft;
  };

  const escondeBarraNativa = scrollXFlutuante && precisaBarra;

  return (
    <div className="rounded-lg border border-gray-200 bg-white shadow-sm">
      <div
        ref={scrollerRef}
        onScroll={scrollXFlutuante ? sincronizarDoScroller : undefined}
        className={cn(
          'overflow-x-auto rounded-lg',
          // Esconde a barra nativa (embaixo, longe) — a flutuante assume o papel.
          escondeBarraNativa && '[scrollbar-width:none] [&::-webkit-scrollbar]:hidden',
        )}
      >
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
            ) : dadosSeguros.length === 0 ? (
              <tr>
                <td colSpan={colunas.length} className="px-4 py-8 text-center text-sm text-gray-500">
                  {vazio ?? 'Nenhum registro encontrado.'}
                </td>
              </tr>
            ) : (
              dadosSeguros.map((item) => (
                <tr
                  key={chaveLinha(item)}
                  className={cn('hover:bg-gray-50', aoClicarLinha && 'cursor-pointer')}
                  title={aoClicarLinha ? (dicaLinha ?? 'Clique para visualizar') : undefined}
                  onClick={
                    aoClicarLinha
                      ? (e) => {
                          // Ignora cliques em elementos interativos (botões/links/inputs).
                          if ((e.target as HTMLElement).closest('button, a, input, select, [role="button"]')) return;
                          aoClicarLinha(item);
                        }
                      : undefined
                  }
                >
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

      {escondeBarraNativa ? (
        <div
          ref={barraRef}
          onScroll={sincronizarDaBarra}
          aria-hidden="true"
          className={cn(
            'sticky bottom-0 z-10 overflow-x-auto border-t border-primary-100 bg-white/90 backdrop-blur',
            // Barra VERMELHA (tema Maricá) e mais visível — a cinza padrão do SO ficava discreta.
            '[scrollbar-width:auto] [scrollbar-color:#C8102E_transparent]',
            '[&::-webkit-scrollbar]:h-3',
            '[&::-webkit-scrollbar-track]:rounded-full [&::-webkit-scrollbar-track]:bg-primary-50',
            '[&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-primary-600',
            '[&::-webkit-scrollbar-thumb:hover]:bg-primary-700',
          )}
        >
          <div style={{ width: larguraConteudo }} className="h-3" />
        </div>
      ) : null}
    </div>
  );
}
