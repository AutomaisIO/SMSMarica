import { useCallback, useEffect, useRef, useState } from 'react';
import { ExternalLink, Search } from 'lucide-react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Button } from '@/shared/ui/Button';
import { cn } from '@/shared/lib/cn';
import { PacsBuscaModal } from '@/features/pacs/components/PacsBuscaModal';
import {
  PacsImagensSidebar,
  type ImagemLista,
} from '@/features/pacs/components/PacsImagensSidebar';
import { PacsViewport } from '@/features/pacs/components/PacsViewport';
import type { Layout } from '@/features/pacs/components/SeletorLayoutGrade';
import { listarSeries, obterMetadadosSerie } from '@/features/pacs/api/pacsApi';
import {
  construirImageId,
  prefetchImagens,
  registrarMetadados,
  type ProgressoPrefetch,
} from '@/features/pacs/lib/cornerstone';
import {
  Tag,
  garantirPixelSpacing,
  rotuloImagemMG,
  valorNumero,
  valorTexto,
} from '@/features/pacs/lib/dicomJson';
import type { Estudo } from '@/features/pacs/types';

type Props = {
  /**
   * Quando true, renderiza a página em tela cheia (sem o Layout do app),
   * já carrega o estudo serializado no hash da URL e oculta o botão
   * "Abrir em janela separada" (que abriria recursivamente).
   */
  janela?: boolean;
};

function lerEstudoDoHash(): Estudo | null {
  try {
    const hash = window.location.hash;
    if (!hash || hash.length < 2) return null;
    return JSON.parse(decodeURIComponent(hash.slice(1))) as Estudo;
  } catch {
    return null;
  }
}

export function PacsViewerPage({ janela = false }: Props = {}) {
  const location = useLocation();
  const navigate = useNavigate();
  const [modalAberto, setModalAberto] = useState(false);
  const [estudo, setEstudo] = useState<Estudo | null>(() => (janela ? lerEstudoDoHash() : null));
  const [imagens, setImagens] = useState<ImagemLista[]>([]);
  const [layout, setLayout] = useState<Layout>({ linhas: 1, colunas: 1 });
  const [celulas, setCelulas] = useState<(string | null)[]>([null]);
  const [focado, setFocado] = useState(0);
  const [carregandoMeta, setCarregandoMeta] = useState(false);
  const [progresso, setProgresso] = useState<ProgressoPrefetch | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const imageIdFocado = celulas[focado] ?? null;

  useEffect(() => {
    return () => abortRef.current?.abort();
  }, []);

  // Em modo janela popup, força confirmação nativa do browser antes do
  // fechamento — protege contra clique acidental no "X" enquanto está
  // analisando. O texto exibido é o padrão do browser (não dá pra customizar).
  useEffect(() => {
    if (!janela) return;
    function aoFechar(e: BeforeUnloadEvent) {
      e.preventDefault();
      e.returnValue = '';
    }
    window.addEventListener('beforeunload', aoFechar);
    return () => window.removeEventListener('beforeunload', aoFechar);
  }, [janela]);

  // Abre o modal automaticamente quando o usuário clica em "Abrir Exame" no
  // menu (passa state.abrirBusca = true). Limpa o state pra refresh não repetir.
  useEffect(() => {
    if (janela) return;
    const estadoLocacao = location.state as { abrirBusca?: boolean } | null;
    if (estadoLocacao?.abrirBusca) {
      abrirBusca();
      navigate(location.pathname, { replace: true });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [janela, location.state]);

  // Em modo janela, dispara o carregamento do estudo recebido no hash assim que
  // o componente monta (não passa pela busca/modal).
  useEffect(() => {
    if (!janela) return;
    const e = lerEstudoDoHash();
    if (e) void carregarTudoDoEstudo(e);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [janela]);

  async function carregarTudoDoEstudo(e: Estudo) {
    abortRef.current?.abort();
    const ctrl = new AbortController();
    abortRef.current = ctrl;
    setCarregandoMeta(true);

    try {
      const series = await listarSeries(e.studyInstanceUID);
      if (ctrl.signal.aborted) return;

      const metaPorSerie = await Promise.all(
        series.map((s) =>
          obterMetadadosSerie(e.studyInstanceUID, s.seriesInstanceUID).catch(() => []),
        ),
      );
      if (ctrl.signal.aborted) return;

      // Lista plana de todas as imagens do estudo (achata as séries).
      const lista: ImagemLista[] = [];
      const todosImageIds: string[] = [];
      metaPorSerie.forEach((instancias, i) => {
        const uid = series[i].seriesInstanceUID;
        instancias.sort(
          (a, b) =>
            (valorNumero(a, Tag.InstanceNumber) ?? 0) - (valorNumero(b, Tag.InstanceNumber) ?? 0),
        );
        for (const inst of instancias) {
          const sop = valorTexto(inst, Tag.SOPInstanceUID);
          if (!sop) continue;
          const id = construirImageId(e.studyInstanceUID, uid, sop);
          registrarMetadados(id, garantirPixelSpacing(inst, id));
          lista.push({ imageId: id, rotulo: rotuloImagemMG(inst) });
          todosImageIds.push(id);
        }
      });

      setImagens(lista);
      // Semeia o primeiro quadrado se nada estiver carregado ainda.
      setCelulas((atual) => {
        if (atual.some(Boolean) || lista.length === 0) return atual;
        const n = [...atual];
        n[0] = lista[0].imageId;
        return n;
      });
      setCarregandoMeta(false);

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
      // helpers já são silenciosos; abort cai aqui também.
    } finally {
      if (!ctrl.signal.aborted) setCarregandoMeta(false);
    }
  }

  function limparGrade() {
    setCelulas((atual) => atual.map(() => null));
    setFocado(0);
    setImagens([]);
  }

  function selecionarEstudo(e: Estudo) {
    setEstudo(e);
    limparGrade();
    setProgresso(null);
    setModalAberto(false);
    void carregarTudoDoEstudo(e);
  }

  const selecionarImagem = useCallback(
    (imageId: string) => {
      setCelulas((atual) => {
        const n = [...atual];
        n[focado] = imageId;
        return n;
      });
    },
    [focado],
  );

  function mudarLayout(novo: Layout) {
    const total = novo.linhas * novo.colunas;
    setCelulas((atual) => {
      const n = atual.slice(0, total);
      while (n.length < total) n.push(null);
      return n;
    });
    setFocado((f) => (f < total ? f : 0));
    setLayout(novo);
  }

  function abrirBusca() {
    abortRef.current?.abort();
    setEstudo(null);
    limparGrade();
    setProgresso(null);
    setCarregandoMeta(false);
    setModalAberto(true);
  }

  function abrirEmJanelaSeparada() {
    if (!estudo) return;
    const hash = encodeURIComponent(JSON.stringify(estudo));
    window.open(`/pacs/janela#${hash}`, '_blank', 'noopener,noreferrer');
  }

  return (
    <div
      className={cn(
        'flex flex-col overflow-hidden bg-gray-900 shadow-marica-lg',
        janela ? 'h-screen w-screen' : 'h-[calc(100vh-7rem)] rounded-xl border border-gray-700',
      )}
    >
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

        {!janela && estudo ? (
          <Button tamanho="sm" variante="outline" onClick={abrirEmJanelaSeparada}>
            <ExternalLink className="mr-2 h-4 w-4" />
            Abrir em janela separada
          </Button>
        ) : null}

        {!janela && !estudo ? (
          <Button tamanho="sm" onClick={abrirBusca}>
            <Search className="mr-2 h-4 w-4" />
            Buscar exame
          </Button>
        ) : null}
      </header>

      <div className="flex flex-1 overflow-hidden">
        <PacsImagensSidebar
          imagens={imagens}
          imageIdFocado={imageIdFocado}
          aoSelecionar={selecionarImagem}
          carregando={carregandoMeta}
          temEstudo={Boolean(estudo)}
        />
        <PacsViewport
          imagensPorCelula={celulas}
          layout={layout}
          focado={focado}
          aoFocar={setFocado}
          aoMudarLayout={mudarLayout}
          carregando={carregandoMeta}
          progresso={progresso}
          studyInstanceUID={estudo?.studyInstanceUID ?? null}
        />
      </div>

      {!janela ? (
        <PacsBuscaModal
          aberto={modalAberto}
          aoFechar={() => setModalAberto(false)}
          aoSelecionar={selecionarEstudo}
        />
      ) : null}
    </div>
  );
}
