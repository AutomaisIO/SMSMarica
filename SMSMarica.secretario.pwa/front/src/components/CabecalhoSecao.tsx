import type { ReactNode } from 'react';

interface Props {
  eyebrow: string;
  titulo: string;
  tituloId: string;
  sub?: string;
  /** Conteúdo à direita (segmented control, hora de atualização…). */
  direita?: ReactNode;
}

/** Cabeçalho padrão de seção: eyebrow + título display + apoio, ações à direita. */
export function CabecalhoSecao({ eyebrow, titulo, tituloId, sub, direita }: Props) {
  return (
    <div className="mb-4 flex flex-wrap items-end justify-between gap-x-4 gap-y-3">
      <div>
        <p className="eyebrow">{eyebrow}</p>
        <h2
          id={tituloId}
          className="mt-0.5 font-display text-[21px] font-bold leading-tight tracking-tight text-tinta sm:text-[23px]"
        >
          {titulo}
        </h2>
        {sub && <p className="mt-1 text-[13.5px] text-grafite">{sub}</p>}
      </div>
      {direita && <div className="shrink-0">{direita}</div>}
    </div>
  );
}
