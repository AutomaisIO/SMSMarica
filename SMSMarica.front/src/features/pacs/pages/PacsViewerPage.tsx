import { useCallback, useEffect, useRef, useState } from 'react';
import { Search } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { PacsBuscaModal } from '@/features/pacs/components/PacsBuscaModal';
import { PacsSeriesSidebar } from '@/features/pacs/components/PacsSeriesSidebar';
import { PacsViewport } from '@/features/pacs/components/PacsViewport';
import { listarSeries, obterMetadadosSerie } from '@/features/pacs/api/pacsApi';
import {
  construirImageId,
  prefetchImagens,
  registrarMetadados,
  type ProgressoPrefetch,
} from '@/features/pacs/lib/cornerstone';
import { Tag, garantirPixelSpacing, valorNumero, valorTexto } from '@/features/pacs/lib/dicomJson';
import type { Estudo } from '@/features/pacs/types';

export function PacsViewerPage() {
  const [modalAberto, setModalAberto] = useState(false);
  const [estudo, setEstudo] = useState<Estudo | null>(null);
  const [serieUID, setSerieUID] = useState<string | null>(null);
  const [imageIdsPorSerie, setImageIdsPorSerie] = useState<Record<string, string[]>>({});
  const [carregandoMeta, setCarregandoMeta] = useState(false);
  const [progresso, setProgresso] = useState<ProgressoPrefetch | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  // imageIds vai sempre derivado da série selecionada — uma única fonte de
  // verdade evita o flash de "imagem anterior" durante a troca de série.
  const imageIds = serieUID ? imageIdsPorSerie[serieUID] ?? [] : [];

  useEffect(() => {
    return () => abortRef.current?.abort();
  }, []);

  async function carregarTudoDoEstudo(e: Estudo) {
    abortRef.current?.abort();
    const ctrl = new AbortController();
    abortRef.current = ctrl;
    setCarregandoMeta(true);

    try {
      const series = await listarSeries(e.studyInstanceUID);
      if (ctrl.signal.aborted) return;

      // Metadados de todas as séries em paralelo — uma única "espera" antes do prefetch.
      const metaPorSerie = await Promise.all(
        series.map((s) =>
          obterMetadadosSerie(e.studyInstanceUID, s.seriesInstanceUID).catch(() => []),
        ),
      );
      if (ctrl.signal.aborted) return;

      const mapa: Record<string, string[]> = {};
      const todosImageIds: string[] = [];
      metaPorSerie.forEach((instancias, i) => {
        const uid = series[i].seriesInstanceUID;
        instancias.sort(
          (a, b) =>
            (valorNumero(a, Tag.InstanceNumber) ?? 0) - (valorNumero(b, Tag.InstanceNumber) ?? 0),
        );
        const ids: string[] = [];
        for (const inst of instancias) {
          const sop = valorTexto(inst, Tag.SOPInstanceUID);
          if (!sop) continue;
          const id = construirImageId(e.studyInstanceUID, uid, sop);
          registrarMetadados(id, garantirPixelSpacing(inst));
          ids.push(id);
          todosImageIds.push(id);
        }
        mapa[uid] = ids;
      });

      setImageIdsPorSerie(mapa);
      // Auto-seleciona a primeira série com imagens, mas só se o usuário ainda
      // não clicou em nenhuma (caso clique durante a busca de metadados).
      const primeira = series.find((s) => (mapa[s.seriesInstanceUID]?.length ?? 0) > 0);
      if (primeira) {
        setSerieUID((atual) => atual ?? primeira.seriesInstanceUID);
      }
      setCarregandoMeta(false);

      // Prefetch das imagens em paralelo controlado — popula o cache do
      // Cornerstone pra que trocar de série depois seja instantâneo.
      if (todosImageIds.length > 0) {
        setProgresso({ carregadas: 0, total: todosImageIds.length });
        await prefetchImagens(todosImageIds, {
          concurrencia: 4,
          signal: ctrl.signal,
          onProgress: (p) => {
            if (!ctrl.signal.aborted) setProgresso(p);
          },
        });
        if (!ctrl.signal.aborted) setProgresso(null);
      }
    } catch {
      // Erros de rede já são silenciosos no nível do helper; aborto também cai aqui.
    } finally {
      if (!ctrl.signal.aborted) setCarregandoMeta(false);
    }
  }

  function selecionarEstudo(e: Estudo) {
    setEstudo(e);
    setSerieUID(null);
    setImageIdsPorSerie({});
    setProgresso(null);
    setModalAberto(false);
    void carregarTudoDoEstudo(e);
  }

  const selecionarSerie = useCallback((uid: string) => {
    setSerieUID(uid);
  }, []);

  // Limpa tudo de cara ao trocar de exame — usuário não pode ver o exame
  // anterior atrás do modal de busca. Também cancela qualquer prefetch em voo.
  function abrirBusca() {
    abortRef.current?.abort();
    setEstudo(null);
    setSerieUID(null);
    setImageIdsPorSerie({});
    setProgresso(null);
    setCarregandoMeta(false);
    setModalAberto(true);
  }

  return (
    <div className="flex h-[calc(100vh-7rem)] flex-col overflow-hidden rounded-xl border border-gray-700 bg-gray-900 shadow-marica-lg">
      {/* Header slim integrado ao visualizador — libera ~6rem de altura útil
          em relação ao header anterior (h1 + subtítulo). Tudo no tema escuro. */}
      <header className="flex flex-shrink-0 items-center justify-between gap-3 border-b border-gray-700 bg-gray-900 px-4 py-2">
        <div className="min-w-0 truncate text-sm text-gray-300">
          {estudo ? (
            <>
              <span className="font-medium text-white">{estudo.patientName || 'Paciente'}</span>
              <span className="text-gray-500"> · </span>
              {estudo.patientAge || '—'}/{estudo.patientSex || '—'}
              <span className="text-gray-500"> · </span>
              {estudo.studyDescription || estudo.modalidade}
              <span className="text-gray-500"> · </span>
              {estudo.studyDateFormatado}
            </>
          ) : (
            <span className="text-gray-500">PACS — Visualizador</span>
          )}
        </div>
        <Button tamanho="sm" onClick={abrirBusca}>
          <Search className="mr-2 h-4 w-4" />
          Buscar exame
        </Button>
      </header>

      <div className="flex flex-1 overflow-hidden">
        <PacsSeriesSidebar
          studyUID={estudo?.studyInstanceUID ?? null}
          serieSelecionadaUID={serieUID}
          aoSelecionar={selecionarSerie}
        />
        <PacsViewport imageIds={imageIds} carregando={carregandoMeta} progresso={progresso} />
      </div>

      <PacsBuscaModal
        aberto={modalAberto}
        aoFechar={() => setModalAberto(false)}
        aoSelecionar={selecionarEstudo}
      />
    </div>
  );
}
