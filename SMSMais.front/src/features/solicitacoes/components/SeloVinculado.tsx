import { Link2 } from 'lucide-react';

/**
 * Selo "vinculado a" (ADR-0074): o pedido do SISREG trouxe mais de um código interno (ex.: joelho
 * direito e esquerdo com o mesmo nº) e virou um exame por código — mesmo pedido, accessions com
 * sufixo (`261016019` e `261016019-1`). Cada um tem a sua worklist e o seu laudo; receber,
 * cancelar, presença e falta valem para todos, como no SISREG.
 */
export function SeloVinculado({ accessions }: { accessions: string[] | null | undefined }) {
  if (!accessions || accessions.length === 0) return null;
  return (
    <span
      className="inline-flex items-center gap-1 rounded bg-indigo-50 px-1.5 py-0.5 text-[11px] font-medium text-indigo-700"
      title={
        'Mesmo pedido do SISREG, com mais de um procedimento: cada código interno é um exame, com a sua '
        + 'worklist e o seu laudo. Receber e cancelar valem para todos.'
      }
    >
      <Link2 className="h-3 w-3" />
      vinculado a {accessions.join(', ')}
    </span>
  );
}
