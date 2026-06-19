import { useCallback, useEffect, useState } from 'react';
import type { LucideIcon } from 'lucide-react';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { EmptyState, ErroCard, SectionHeader, Skeleton } from '@/components/ui';

/**
 * Scaffold das telas de lista do app (atendimentos/exames/laudos/transporte):
 * cuida de carregar, loading com skeleton, estado vazio e erro com "tentar de novo".
 */
export function Lista<T>({
  eyebrow,
  titulo,
  carregar,
  emptyIcon,
  emptyTitulo,
  emptyDescricao,
  renderItem,
}: {
  eyebrow?: string;
  titulo: string;
  carregar: () => Promise<T[]>;
  emptyIcon: LucideIcon;
  emptyTitulo: string;
  emptyDescricao: string;
  renderItem: (item: T, index: number) => React.ReactNode;
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
      ) : (
        <ul className="space-y-3">{itens.map((item, i) => <li key={i}>{renderItem(item, i)}</li>)}</ul>
      )}
    </div>
  );
}
