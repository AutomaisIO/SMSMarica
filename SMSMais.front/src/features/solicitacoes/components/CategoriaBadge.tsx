import { cn } from '@/shared/lib/cn';
import type { CategoriaSolicitacao } from '@/features/solicitacoes/types';

/** Rótulo de cada tipo de solicitação — o mesmo no selo e no filtro "Tipo". */
export const ROTULO_CATEGORIA: Record<CategoriaSolicitacao, string> = {
  Imagem: 'Exame de imagem',
  Consulta: 'Consulta',
  Laboratorio: 'Laboratório',
  GraficoFuncional: 'Gráfico/funcional',
  Endoscopia: 'Endoscopia',
  Cirurgia: 'Cirurgia',
  Outro: 'Outro',
};

const COR: Partial<Record<CategoriaSolicitacao, string>> = {
  Consulta: 'bg-blue-50 text-blue-700 ring-blue-200',
  Cirurgia: 'bg-red-50 text-red-700 ring-red-200',
};

/** Selo do tipo da solicitação (consulta, laboratório…). O exame de imagem mostra a modalidade. */
export function CategoriaBadge({ categoria }: { categoria: CategoriaSolicitacao }) {
  return (
    <span
      className={cn(
        'inline-flex rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset',
        COR[categoria] ?? 'bg-gray-100 text-gray-700 ring-gray-200',
      )}
    >
      {ROTULO_CATEGORIA[categoria] ?? categoria}
    </span>
  );
}
