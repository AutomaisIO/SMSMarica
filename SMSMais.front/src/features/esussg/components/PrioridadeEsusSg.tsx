import { corDaPrioridade } from '@/features/esussg/lib/formatacao';

/**
 * A prioridade como o ESUS mostra ("A REGULAR", "URGENTE", "MANDADO JUDICIAL"…), com a cor que o
 * próprio ESUS dá a ela. A cor vira uma bolinha ao lado do texto — o texto continua legível em
 * qualquer cor.
 */
export function PrioridadeEsusSg({
  prioridade,
  cor,
}: {
  prioridade: string | null | undefined;
  cor?: string | null;
}) {
  if (!prioridade) return <span className="text-slate-400">—</span>;
  const css = corDaPrioridade(cor);
  return (
    <span className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-700">
      {css && (
        <span
          aria-hidden
          className="inline-block size-2.5 shrink-0 rounded-full ring-1 ring-black/10"
          style={{ backgroundColor: css }}
        />
      )}
      {prioridade}
    </span>
  );
}
