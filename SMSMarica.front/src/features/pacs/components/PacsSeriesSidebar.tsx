import { Layers, Loader2 } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useSeriesDoEstudo } from '@/features/pacs/api/queries';

type Props = {
  studyUID: string | null;
  serieSelecionadaUID: string | null;
  aoSelecionar: (seriesUID: string) => void;
};

export function PacsSeriesSidebar({ studyUID, serieSelecionadaUID, aoSelecionar }: Props) {
  const series = useSeriesDoEstudo(studyUID);

  return (
    <aside className="flex w-64 flex-shrink-0 flex-col border-r border-gray-700 bg-gray-900 text-gray-100">
      <div className="border-b border-gray-700 px-4 py-3 text-sm font-semibold uppercase tracking-wide text-gray-300">
        Séries
      </div>
      <div className="flex-1 overflow-y-auto p-2">
        {!studyUID ? (
          <p className="px-2 py-4 text-sm text-gray-400">Busque e selecione um exame.</p>
        ) : series.isLoading ? (
          <div className="flex items-center gap-2 px-2 py-4 text-sm text-gray-400">
            <Loader2 className="h-4 w-4 animate-spin" /> Carregando séries...
          </div>
        ) : series.isError ? (
          <p className="px-2 py-4 text-sm text-red-400">{extrairMensagemDeErro(series.error)}</p>
        ) : (series.data ?? []).length === 0 ? (
          <p className="px-2 py-4 text-sm text-gray-400">Nenhuma série neste estudo.</p>
        ) : (
          <ul className="space-y-1">
            {series.data!.map((serie) => (
              <li key={serie.seriesInstanceUID}>
                <button
                  type="button"
                  onClick={() => aoSelecionar(serie.seriesInstanceUID)}
                  className={cn(
                    'flex w-full items-center gap-3 rounded-md px-2 py-2 text-left text-sm transition-colors',
                    serie.seriesInstanceUID === serieSelecionadaUID
                      ? 'bg-primary-600 text-white'
                      : 'hover:bg-gray-800',
                  )}
                >
                  <span className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded bg-black/40 text-gray-400">
                    <Layers className="h-5 w-5" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium">
                      Série {serie.seriesNumber || '?'} · {serie.modalidade || '—'}
                    </span>
                    <span className="block truncate text-xs text-gray-400">
                      {serie.seriesDescription || 'Sem descrição'} ({serie.numeroInstancias} img)
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </aside>
  );
}
