import { useEffect, useRef } from 'react';
import { utilities } from '@cornerstonejs/core';
import { Image as ImageIcon, Loader2 } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useSeriesDoEstudo } from '@/features/pacs/api/queries';
import { inicializarCornerstone } from '@/features/pacs/lib/cornerstone';

type Props = {
  studyUID: string | null;
  serieSelecionadaUID: string | null;
  aoSelecionar: (seriesUID: string) => void;
  /** Mapa de seriesUID → imageIds prefetchados, vindo da página. */
  imageIdsPorSerie: Record<string, string[]>;
};

export function PacsSeriesSidebar({
  studyUID,
  serieSelecionadaUID,
  aoSelecionar,
  imageIdsPorSerie,
}: Props) {
  const series = useSeriesDoEstudo(studyUID);

  return (
    <aside className="flex w-56 flex-shrink-0 flex-col border-r border-gray-700 bg-gray-900 text-gray-100">
      <div className="border-b border-gray-700 px-3 py-2 text-[11px] font-semibold uppercase tracking-wide text-gray-400">
        Séries
      </div>
      <div className="flex-1 overflow-y-auto p-2">
        {!studyUID ? (
          <p className="px-2 py-4 text-sm text-gray-500">Busque e selecione um exame.</p>
        ) : series.isLoading ? (
          <div className="flex items-center gap-2 px-2 py-4 text-sm text-gray-400">
            <Loader2 className="h-4 w-4 animate-spin" /> Carregando séries...
          </div>
        ) : series.isError ? (
          <p className="px-2 py-4 text-sm text-red-400">{extrairMensagemDeErro(series.error)}</p>
        ) : (series.data ?? []).length === 0 ? (
          <p className="px-2 py-4 text-sm text-gray-500">Nenhuma série neste estudo.</p>
        ) : (
          <ul className="space-y-1.5">
            {series.data!.map((serie) => {
              const ativa = serie.seriesInstanceUID === serieSelecionadaUID;
              const ids = imageIdsPorSerie[serie.seriesInstanceUID];
              const primeiroId = ids?.[0];
              return (
                <li key={serie.seriesInstanceUID}>
                  <button
                    type="button"
                    onClick={() => aoSelecionar(serie.seriesInstanceUID)}
                    className={cn(
                      'flex w-full items-center gap-2 rounded-md p-1.5 text-left transition-colors',
                      ativa
                        ? 'bg-primary-600/20 ring-2 ring-primary-500'
                        : 'hover:bg-gray-800',
                    )}
                  >
                    <ThumbnailSerie imageId={primeiroId} ativa={ativa} />
                    <span className="min-w-0 flex-1 text-xs">
                      <span className="block truncate font-semibold text-gray-200">
                        Série {serie.seriesNumber || '?'}
                      </span>
                      <span className="block truncate text-gray-400">
                        {serie.seriesDescription || serie.modalidade || '—'}
                      </span>
                      <span className="block text-[10px] text-gray-500">
                        {serie.numeroInstancias} img
                      </span>
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </aside>
  );
}

const TAMANHO_THUMB = 72;

function ThumbnailSerie({ imageId, ativa }: { imageId?: string; ativa: boolean }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    if (!imageId) return;
    const canvas = canvasRef.current;
    if (!canvas) return;

    let cancelado = false;
    void (async () => {
      await inicializarCornerstone();
      if (cancelado) return;
      try {
        await utilities.loadImageToCanvas({
          canvas,
          imageId,
          thumbnail: true,
          imageAspect: true,
        });
      } catch {
        // Falhou — mantemos o placeholder vazio sem erro visível.
      }
    })();

    return () => {
      cancelado = true;
    };
  }, [imageId]);

  return (
    <div
      className={cn(
        'flex flex-shrink-0 items-center justify-center overflow-hidden rounded border bg-black',
        ativa ? 'border-primary-400' : 'border-gray-700',
      )}
      style={{ width: TAMANHO_THUMB, height: TAMANHO_THUMB }}
    >
      {imageId ? (
        <canvas ref={canvasRef} width={TAMANHO_THUMB} height={TAMANHO_THUMB} className="h-full w-full" />
      ) : (
        <ImageIcon className="h-5 w-5 text-gray-700" />
      )}
    </div>
  );
}
