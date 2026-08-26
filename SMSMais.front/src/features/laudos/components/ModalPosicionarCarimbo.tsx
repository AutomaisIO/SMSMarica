import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import * as pdfjsLib from 'pdfjs-dist';
import type { PDFDocumentProxy } from 'pdfjs-dist';
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';
import { AlertTriangle, Loader2, PenLine } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { obterPdfBaseAssinatura } from '@/features/laudos/api/laudosApi';
import type { CarimboPosicao } from '@/features/laudos/types';

// Worker do pdfjs empacotado pelo Vite (?url) — resolve o carregamento do PDF-base
// em produção sem depender de CDN externa. (ADR-0049 / #113)
pdfjsLib.GlobalWorkerOptions.workerSrc = workerUrl;

/** Retângulo do carimbo em frações [0..1] da página (origem superior-esquerda, como a tela). */
type Caixa = { left: number; top: number; larg: number; alt: number };

/** Lado do carimbo (~130pt) e recuo do pé (~28pt) — espelham o padrão legado do assinador. */
const LADO_PT = 130;
const RECUO_PE_PT = 28;
const MIN_FRAC = 0.04;

/** Caixa padrão: quadrado ~130pt centralizado, recuado ~28pt do pé da página. */
function caixaPadrao(pageWpt: number, pageHpt: number): Caixa {
  const larg = Math.min(LADO_PT / pageWpt, 1);
  const alt = Math.min(LADO_PT / pageHpt, 1);
  return {
    left: Math.max((1 - larg) / 2, 0),
    top: Math.max(1 - alt - RECUO_PE_PT / pageHpt, 0),
    larg,
    alt,
  };
}

function clamp01(v: number): number {
  return Math.min(1, Math.max(0, v));
}

/**
 * Posicionamento do carimbo da assinatura sobre o PDF-base (ADR-0049). A médica escolhe
 * a página e arrasta/redimensiona o carimbo; ao confirmar, convertemos a caixa (frações
 * da página, origem no topo) para pontos PDF (origem inferior-esquerda) e disparamos a
 * assinatura. Resolve o #113: a posição deixou de ser fixa no rodapé.
 */
export function ModalPosicionarCarimbo({
  laudoId,
  aberto,
  enviando,
  aoCancelar,
  aoConfirmar,
}: {
  laudoId: string;
  aberto: boolean;
  enviando: boolean;
  aoCancelar: () => void;
  aoConfirmar: (posicao: CarimboPosicao) => void;
}) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const areaRef = useRef<HTMLDivElement | null>(null);
  const docRef = useRef<PDFDocumentProxy | null>(null);
  // Dimensões da página atual em PONTOS PDF (viewport na escala 1).
  const dimsRef = useRef<{ wpt: number; hpt: number }>({ wpt: 595, hpt: 842 });

  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [totalPaginas, setTotalPaginas] = useState(1);
  const [pagina, setPagina] = useState(1); // 1-based
  const [caixa, setCaixa] = useState<Caixa>({ left: 0.35, top: 0.8, larg: 0.3, alt: 0.15 });
  const [sobrepoe, setSobrepoe] = useState(false);

  // Carrega o PDF-base uma vez por abertura.
  useEffect(() => {
    if (!aberto) return;
    let vivo = true;
    setCarregando(true);
    setErro(null);
    obterPdfBaseAssinatura(laudoId)
      .then(async (buf) => {
        const doc = await pdfjsLib.getDocument({ data: new Uint8Array(buf) }).promise;
        if (!vivo) {
          void doc.destroy();
          return;
        }
        docRef.current = doc;
        setTotalPaginas(doc.numPages);
        setPagina(doc.numPages); // carimbo tende a ir na última página
      })
      .catch((e) => {
        if (vivo) setErro(extrairMensagemDeErro(e));
      })
      .finally(() => {
        if (vivo) setCarregando(false);
      });
    return () => {
      vivo = false;
      const doc = docRef.current;
      docRef.current = null;
      if (doc) void doc.destroy();
    };
  }, [aberto, laudoId]);

  // Renderiza a página selecionada e reposiciona a caixa no padrão daquela página.
  useEffect(() => {
    const doc = docRef.current;
    const canvas = canvasRef.current;
    if (!aberto || !doc || !canvas || carregando) return;
    let cancelado = false;
    let tarefa: pdfjsLib.RenderTask | null = null;

    (async () => {
      try {
        const page = await doc.getPage(pagina);
        if (cancelado) return;
        const larguraAlvo = areaRef.current?.clientWidth ?? 640;
        const base = page.getViewport({ scale: 1 });
        dimsRef.current = { wpt: base.width, hpt: base.height };
        const escala = Math.max(0.2, larguraAlvo / base.width);
        const viewport = page.getViewport({ scale: escala });
        const ctx = canvas.getContext('2d');
        if (!ctx) return;
        canvas.width = Math.floor(viewport.width);
        canvas.height = Math.floor(viewport.height);
        tarefa = page.render({ canvasContext: ctx, viewport });
        await tarefa.promise;
        if (!cancelado) setCaixa(caixaPadrao(base.width, base.height));
      } catch (e) {
        if (!cancelado && (e as { name?: string })?.name !== 'RenderingCancelledException') {
          setErro(extrairMensagemDeErro(e));
        }
      }
    })();

    return () => {
      cancelado = true;
      tarefa?.cancel();
    };
  }, [aberto, carregando, pagina]);

  // Detecta sobreposição a conteúdo: amostra os pixels do canvas sob a caixa; se houver
  // muito pixel não-branco, avisa (o carimbo cobriria texto). Guarda "leve" do ADR-0049.
  useLayoutEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || carregando) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    const x = Math.floor(caixa.left * canvas.width);
    const y = Math.floor(caixa.top * canvas.height);
    const w = Math.max(1, Math.floor(caixa.larg * canvas.width));
    const h = Math.max(1, Math.floor(caixa.alt * canvas.height));
    if (x < 0 || y < 0 || x + w > canvas.width || y + h > canvas.height) return;
    try {
      const { data } = ctx.getImageData(x, y, w, h);
      let escuros = 0;
      const passo = 4 * 7; // amostragem esparsa (performance)
      let amostras = 0;
      for (let i = 0; i < data.length; i += passo) {
        amostras++;
        if (data[i] < 235 || data[i + 1] < 235 || data[i + 2] < 235) escuros++;
      }
      setSobrepoe(amostras > 0 && escuros / amostras > 0.02);
    } catch {
      setSobrepoe(false); // canvas "tainted" — ignora a checagem
    }
  }, [caixa, carregando, pagina]);

  const arrastar = useCallback(
    (e: React.PointerEvent, modo: 'mover' | 'redim') => {
      e.preventDefault();
      e.stopPropagation();
      const area = areaRef.current;
      if (!area) return;
      const rect = area.getBoundingClientRect();
      const inicial = { ...caixa };
      const x0 = e.clientX;
      const y0 = e.clientY;

      function mover(ev: PointerEvent) {
        const dx = (ev.clientX - x0) / rect.width;
        const dy = (ev.clientY - y0) / rect.height;
        setCaixa(() => {
          if (modo === 'mover') {
            return {
              ...inicial,
              left: clamp01(Math.min(inicial.left + dx, 1 - inicial.larg)),
              top: clamp01(Math.min(inicial.top + dy, 1 - inicial.alt)),
            };
          }
          const larg = Math.min(Math.max(inicial.larg + dx, MIN_FRAC), 1 - inicial.left);
          const alt = Math.min(Math.max(inicial.alt + dy, MIN_FRAC), 1 - inicial.top);
          return { ...inicial, larg, alt };
        });
      }
      function soltar() {
        window.removeEventListener('pointermove', mover);
        window.removeEventListener('pointerup', soltar);
      }
      window.addEventListener('pointermove', mover);
      window.addEventListener('pointerup', soltar);
    },
    [caixa],
  );

  function confirmar() {
    const { wpt, hpt } = dimsRef.current;
    // Frações (origem topo) → pontos PDF (origem base-esquerda).
    const largura = caixa.larg * wpt;
    const altura = caixa.alt * hpt;
    const x = caixa.left * wpt;
    const y = (1 - caixa.top - caixa.alt) * hpt;
    aoConfirmar({
      pagina,
      x: Math.round(x * 100) / 100,
      y: Math.round(y * 100) / 100,
      largura: Math.round(largura * 100) / 100,
      altura: Math.round(altura * 100) / 100,
    });
  }

  return (
    <Modal
      aberto={aberto}
      aoFechar={enviando ? () => {} : aoCancelar}
      titulo="Posicione a assinatura"
      descricao="Arraste e redimensione o carimbo sobre o laudo. É onde a sua assinatura digital será aplicada."
      largura="lg"
    >
      <div className="space-y-3">
        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </div>
        ) : null}

        <div className="flex items-center justify-between gap-3">
          <label className="flex items-center gap-2 text-sm text-gray-600">
            Página
            <select
              className="rounded-md border border-gray-300 px-2 py-1 text-sm"
              value={pagina}
              disabled={carregando || totalPaginas <= 1}
              onChange={(e) => setPagina(Number(e.target.value))}
            >
              {Array.from({ length: totalPaginas }, (_, i) => i + 1).map((n) => (
                <option key={n} value={n}>
                  {n} de {totalPaginas}
                </option>
              ))}
            </select>
          </label>
          {sobrepoe ? (
            <span className="flex items-center gap-1 text-xs text-amber-700">
              <AlertTriangle className="h-3.5 w-3.5" />
              O carimbo parece cobrir texto — considere movê-lo.
            </span>
          ) : null}
        </div>

        <div className="max-h-[58vh] overflow-auto rounded-md border border-gray-200 bg-gray-100 p-3">
          <div ref={areaRef} className="relative mx-auto w-full max-w-[640px]">
            {carregando ? (
              <div className="flex h-[50vh] items-center justify-center gap-2 text-sm text-gray-500">
                <Loader2 className="h-4 w-4 animate-spin" /> Carregando o laudo…
              </div>
            ) : (
              <>
                <canvas ref={canvasRef} className="block w-full rounded-sm shadow-sm" />
                <div
                  role="button"
                  tabIndex={0}
                  onPointerDown={(e) => arrastar(e, 'mover')}
                  className="absolute cursor-move rounded-sm border-2 border-red-500 bg-red-500/15"
                  style={{
                    left: `${caixa.left * 100}%`,
                    top: `${caixa.top * 100}%`,
                    width: `${caixa.larg * 100}%`,
                    height: `${caixa.alt * 100}%`,
                  }}
                >
                  <span className="pointer-events-none absolute -top-5 left-0 flex items-center gap-1 whitespace-nowrap text-[11px] font-medium text-red-600">
                    <PenLine className="h-3 w-3" /> Assinatura
                  </span>
                  <div
                    onPointerDown={(e) => arrastar(e, 'redim')}
                    className="absolute -bottom-1.5 -right-1.5 h-3.5 w-3.5 cursor-se-resize rounded-sm border-2 border-red-500 bg-white"
                  />
                </div>
              </>
            )}
          </div>
        </div>

        <div className="flex items-center justify-between gap-3 border-t border-gray-100 pt-4">
          <Button variante="outline" onClick={aoCancelar} disabled={enviando}>
            Cancelar
          </Button>
          <Button onClick={confirmar} disabled={enviando || carregando || !!erro}>
            {enviando ? <Loader2 className="h-4 w-4 animate-spin" /> : <PenLine className="h-4 w-4" />}
            Assinar aqui
          </Button>
        </div>
      </div>
    </Modal>
  );
}
