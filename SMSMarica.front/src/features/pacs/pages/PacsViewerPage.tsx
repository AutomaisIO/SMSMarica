import { useCallback, useState } from 'react';
import { Search } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { PacsBuscaModal } from '@/features/pacs/components/PacsBuscaModal';
import { PacsSeriesSidebar } from '@/features/pacs/components/PacsSeriesSidebar';
import { PacsViewport } from '@/features/pacs/components/PacsViewport';
import { obterMetadadosSerie } from '@/features/pacs/api/pacsApi';
import {
  construirImageId,
  registrarMetadados,
} from '@/features/pacs/lib/cornerstone';
import { Tag, garantirPixelSpacing, valorNumero, valorTexto } from '@/features/pacs/lib/dicomJson';
import type { Estudo } from '@/features/pacs/types';

export function PacsViewerPage() {
  const [modalAberto, setModalAberto] = useState(false);
  const [estudo, setEstudo] = useState<Estudo | null>(null);
  const [serieUID, setSerieUID] = useState<string | null>(null);
  const [imageIds, setImageIds] = useState<string[]>([]);
  const [carregando, setCarregando] = useState(false);

  function selecionarEstudo(e: Estudo) {
    setEstudo(e);
    setSerieUID(null);
    setImageIds([]);
    setModalAberto(false);
  }

  const selecionarSerie = useCallback(
    async (seriesUID: string) => {
      if (!estudo) return;
      setSerieUID(seriesUID);
      setCarregando(true);
      setImageIds([]);
      try {
        const instancias = await obterMetadadosSerie(estudo.studyInstanceUID, seriesUID);
        instancias.sort(
          (a, b) =>
            (valorNumero(a, Tag.InstanceNumber) ?? 0) - (valorNumero(b, Tag.InstanceNumber) ?? 0),
        );
        const ids = instancias
          .map((inst) => {
            const sop = valorTexto(inst, Tag.SOPInstanceUID);
            if (!sop) return null;
            const id = construirImageId(estudo.studyInstanceUID, seriesUID, sop);
            registrarMetadados(id, garantirPixelSpacing(inst));
            return id;
          })
          .filter((id): id is string => id !== null);
        setImageIds(ids);
      } finally {
        setCarregando(false);
      }
    },
    [estudo],
  );

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">PACS — Visualizador</h1>
          <p className="mt-1 text-sm text-gray-600">
            {estudo
              ? `${estudo.patientName || 'Paciente'} · ${estudo.patientAge || '—'}/${estudo.patientSex || '—'} · ${estudo.studyDescription || estudo.modalidade} · ${estudo.studyDateFormatado}`
              : 'Imagens DICOM via PACS.'}
          </p>
        </div>
        <Button onClick={() => setModalAberto(true)}>
          <Search className="mr-2 h-4 w-4" />
          Buscar exame
        </Button>
      </header>

      <div className="flex h-[calc(100vh-13rem)] overflow-hidden rounded-xl border border-gray-700 bg-gray-900 shadow-marica-lg">
        <PacsSeriesSidebar
          studyUID={estudo?.studyInstanceUID ?? null}
          serieSelecionadaUID={serieUID}
          aoSelecionar={selecionarSerie}
        />
        <PacsViewport imageIds={imageIds} carregando={carregando} />
      </div>

      <PacsBuscaModal
        aberto={modalAberto}
        aoFechar={() => setModalAberto(false)}
        aoSelecionar={selecionarEstudo}
      />
    </div>
  );
}
