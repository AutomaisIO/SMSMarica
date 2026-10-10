import { useCallback, useEffect, useState } from 'react';
import type { LucideIcon } from 'lucide-react';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { EmptyState, ErroCard, SectionHeader, Skeleton } from '@/components/ui';

/**
 * Scaffold das telas de lista do app (atendimentos/exames/laudos/transporte):
 * cuida de carregar, loading com skeleton, estado vazio e erro com "tentar de novo".
 * `secao` (opcional) separa a lista em blocos com título — os itens já vêm na ordem dos blocos.
 */
export function Lista<T>({
  eyebrow,
  titulo,
  carregar,
  emptyIcon,
  emptyTitulo,
  emptyDescricao,
  renderItem,
  secao,
}: {
  eyebrow?: string;
  titulo: string;
  carregar: () => Promise<T[]>;
  emptyIcon: LucideIcon;
  emptyTitulo: string;
  emptyDescricao: string;
  renderItem: (item: T, index: number) => React.ReactNode;
  secao?: (item: T) => string;
}) {
  const [itens, setItens] = useState<T[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  const buscar = useCallback(() => {
    setErro(null);
    setItens(null);
    carregar()
      .then(setItens)
      .catch((e) => setErro(extrairMensagemDeErro(e)));
  }, [carregar]);

  useEffect(() => buscar(), [buscar]);

  return (
    <div className="animate-rise">
      <SectionHeader eyebrow={eyebrow} title={titulo} />

      {erro ? (
        <ErroCard mensagem={erro} aoTentar={buscar} />
      ) : itens === null ? (
        <div className="space-y-3">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-20 w-full" />
          ))}
        </div>
      ) : itens.length === 0 ? (
        <EmptyState icon={emptyIcon} titulo={emptyTitulo} descricao={emptyDescricao} />
      ) : secao ? (
        <div className="space-y-6">
          {emBlocos(itens, secao).map((bloco, b) => (
            <section key={b}>
              <h2 className="mb-2 text-xs font-semibold uppercase tracking-widest text-tinta-mute">
                {bloco.titulo}
              </h2>
              <ul className="space-y-3">
                {bloco.itens.map(({ item, i }) => (
                  <li key={i}>{renderItem(item, i)}</li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      ) : (
        <ul className="space-y-3">{itens.map((item, i) => <li key={i}>{renderItem(item, i)}</li>)}</ul>
      )}
    </div>
  );
}

/** Agrupa itens consecutivos do mesmo bloco, preservando a ordem e o índice original. */
function emBlocos<T>(itens: T[], secao: (item: T) => string) {
  const blocos: { titulo: string; itens: { item: T; i: number }[] }[] = [];
  itens.forEach((item, i) => {
    const titulo = secao(item);
    const ultimo = blocos[blocos.length - 1];
    if (ultimo?.titulo === titulo) ultimo.itens.push({ item, i });
    else blocos.push({ titulo, itens: [{ item, i }] });
  });
  return blocos;
}
