import { useEffect, useRef, useState } from 'react';
import { Crop, Maximize, RotateCcw, X } from 'lucide-react';
import { carregarImagem, detectarCantos, paraCanvas } from '@/lib/scanner';
import { corrigirPerspectiva, type Cantos, type Ponto } from '@/lib/perspectiva';
import { GhostButton, PrimaryButton, Spinner } from '@/components/ui';
import { cn } from '@/lib/cn';

type Canto = keyof Cantos; // 'tl' | 'tr' | 'br' | 'bl'
const CANTOS: Canto[] = ['tl', 'tr', 'br', 'bl'];

/** Lado da lupa de precisão (px de tela) e fator de zoom relativo ao que aparece. */
const LADO_LUPA = 132;
const ZOOM_LUPA = 2.5;

/**
 * Editor manual de recorte estilo CamScanner: 4 cantos arrastáveis sobre a foto,
 * com lupa de precisão. Pré-preenche os cantos com a detecção automática (jscanify)
 * e, ao confirmar, planifica a página com `corrigirPerspectiva` (lib pronta).
 *
 * Coordenadas: tudo é mantido em coordenadas do `srcCanvas` (resolução de
 * processamento, máx. 1600px). O overlay SVG usa `viewBox` = dimensões do srcCanvas,
 * então mapeia 1:1 com a imagem exibida; o ponteiro é convertido de coords de tela
 * → srcCanvas via o boundingRect da <img> (a "escala de exibição").
 *
 * Touch/mouse: usa Pointer Events (funciona em ambos) + `touch-action: none` nas
 * alças para não rolar/dar zoom na página durante o arraste.
 */
export function EditorRecorte({
  fotoCrua,
  aoConfirmar,
  aoRefazerFoto,
  aoCancelar,
}: {
  /** dataURL crua da foto (frame da câmera ou arquivo). */
  fotoCrua: string;
  /** Recebe a página já recortada (dataURL) e se houve recorte manual (`true`) ou "página inteira". */
  aoConfirmar: (dataUrl: string, recortado: boolean) => void;
  aoRefazerFoto: () => void;
  aoCancelar: () => void;
}) {
  const [srcCanvas, setSrcCanvas] = useState<HTMLCanvasElement | null>(null);
  const [srcUrl, setSrcUrl] = useState<string | null>(null);
  const [cantos, setCantos] = useState<Cantos | null>(null);
  const [detectando, setDetectando] = useState(false);
  const [arrastando, setArrastando] = useState<Canto | null>(null);
  const [ladoLupa, setLadoLupa] = useState<'esq' | 'dir'>('dir');

  const imgRef = useRef<HTMLImageElement>(null);
  const lupaRef = useRef<HTMLCanvasElement>(null);

  // Carrega a foto → canvas de processamento; pré-preenche os cantos (detecção async).
  useEffect(() => {
    let vivo = true;
    setSrcCanvas(null);
    setSrcUrl(null);
    setCantos(null);
    (async () => {
      let canvas: HTMLCanvasElement;
      try {
        const img = await carregarImagem(fotoCrua);
        if (!vivo) return;
        canvas = paraCanvas(img, 1600);
      } catch {
        return;
      }
      if (!vivo) return;
      setSrcCanvas(canvas);
      setSrcUrl(canvas.toDataURL('image/jpeg', 0.92));
      // Margens internas padrão (~12%) até a detecção responder (ou se ela falhar).
      const mx = canvas.width * 0.12;
      const my = canvas.height * 0.12;
      setCantos({
        tl: { x: mx, y: my },
        tr: { x: canvas.width - mx, y: my },
        br: { x: canvas.width - mx, y: canvas.height - my },
        bl: { x: mx, y: canvas.height - my },
      });

      setDetectando(true);
      try {
        const det = await detectarCantos(canvas);
        if (vivo && det) setCantos(det);
      } finally {
        if (vivo) setDetectando(false);
      }
    })();
    return () => {
      vivo = false;
    };
  }, [fotoCrua]);

  /** Converte coords de tela do ponteiro → coords do srcCanvas (limitadas à imagem). */
  function pontoDoEvento(e: React.PointerEvent): Ponto | null {
    const img = imgRef.current;
    if (!img || !srcCanvas) return null;
    const rect = img.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return null;
    const x = ((e.clientX - rect.left) / rect.width) * srcCanvas.width;
    const y = ((e.clientY - rect.top) / rect.height) * srcCanvas.height;
    return {
      x: Math.max(0, Math.min(srcCanvas.width, x)),
      y: Math.max(0, Math.min(srcCanvas.height, y)),
    };
  }

  function desenharLupa(p: Ponto, clientX: number) {
    const lc = lupaRef.current;
    const img = imgRef.current;
    if (!lc || !img || !srcCanvas) return;
    const ctx = lc.getContext('2d');
    if (!ctx) return;
    const rect = img.getBoundingClientRect();
    const escala = rect.width / srcCanvas.width; // escala de exibição (display)
    const regiao = LADO_LUPA / (ZOOM_LUPA * escala); // região da fonte mostrada (src px)

    ctx.fillStyle = '#fff';
    ctx.fillRect(0, 0, LADO_LUPA, LADO_LUPA);
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(srcCanvas, p.x - regiao / 2, p.y - regiao / 2, regiao, regiao, 0, 0, LADO_LUPA, LADO_LUPA);
    // Mira no centro (onde o canto está).
    ctx.strokeStyle = 'rgba(200,16,46,0.9)';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(LADO_LUPA / 2, LADO_LUPA / 2 - 12);
    ctx.lineTo(LADO_LUPA / 2, LADO_LUPA / 2 + 12);
    ctx.moveTo(LADO_LUPA / 2 - 12, LADO_LUPA / 2);
    ctx.lineTo(LADO_LUPA / 2 + 12, LADO_LUPA / 2);
    ctx.stroke();
    // Posiciona a lupa no lado oposto ao dedo, para não cobrir o ponto.
    setLadoLupa(clientX > window.innerWidth / 2 ? 'esq' : 'dir');
  }

  function aoDescer(canto: Canto) {
    return (e: React.PointerEvent) => {
      e.preventDefault();
      (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
      setArrastando(canto);
      const p = pontoDoEvento(e);
      if (p) {
        setCantos((c) => (c ? { ...c, [canto]: p } : c));
        desenharLupa(p, e.clientX);
      }
    };
  }
  function aoMover(canto: Canto) {
    return (e: React.PointerEvent) => {
      if (arrastando !== canto) return;
      const p = pontoDoEvento(e);
      if (p) {
        setCantos((c) => (c ? { ...c, [canto]: p } : c));
        desenharLupa(p, e.clientX);
      }
    };
  }
  function aoSubir(e: React.PointerEvent) {
    (e.currentTarget as HTMLElement).releasePointerCapture?.(e.pointerId);
    setArrastando(null);
  }

  function aplicarRecorte() {
    if (!srcCanvas || !cantos) return;
    const out = corrigirPerspectiva(srcCanvas, cantos);
    aoConfirmar(out.toDataURL('image/jpeg', 0.85), true);
  }
  function paginaInteira() {
    if (!srcCanvas) return;
    aoConfirmar(srcCanvas.toDataURL('image/jpeg', 0.9), false);
  }

  const pronto = !!srcCanvas && !!srcUrl && !!cantos;

  return (
    <div className="fixed inset-0 z-50 mx-auto flex max-w-[460px] flex-col bg-tinta">
      <div className="flex items-center justify-between px-3 pb-2 pt-[calc(env(safe-area-inset-top)+0.75rem)]">
        <button
          type="button"
          onClick={aoCancelar}
          aria-label="Fechar editor"
          className="grid h-11 w-11 place-items-center rounded-xl text-white transition active:bg-white/15"
        >
          <X className="h-6 w-6" />
        </button>
        <span className="text-sm font-medium text-white/80">Ajuste os cantos do documento</span>
        <span className="h-11 w-11" />
      </div>

      <div className="relative flex flex-1 items-center justify-center overflow-hidden p-3">
        {!pronto ? (
          <div className="flex flex-col items-center gap-3 text-white/90">
            <Spinner className="text-white" />
            <p className="text-sm">Preparando a imagem…</p>
          </div>
        ) : (
          <div className="relative inline-flex max-h-full max-w-full touch-none">
            <img
              ref={imgRef}
              src={srcUrl!}
              alt="Foto do documento"
              draggable={false}
              className="block max-h-full max-w-full select-none rounded-lg"
            />

            {/* Máscara escura fora do quadrilátero + contorno (escala 1:1 com a imagem). */}
            <svg
              className="pointer-events-none absolute inset-0 h-full w-full"
              viewBox={`0 0 ${srcCanvas!.width} ${srcCanvas!.height}`}
              preserveAspectRatio="none"
            >
              <path
                d={`M0 0 H${srcCanvas!.width} V${srcCanvas!.height} H0 Z M${cantos!.tl.x} ${cantos!.tl.y} L${cantos!.tr.x} ${cantos!.tr.y} L${cantos!.br.x} ${cantos!.br.y} L${cantos!.bl.x} ${cantos!.bl.y} Z`}
                fillRule="evenodd"
                fill="rgba(20,15,16,0.5)"
              />
              <polygon
                points={`${cantos!.tl.x},${cantos!.tl.y} ${cantos!.tr.x},${cantos!.tr.y} ${cantos!.br.x},${cantos!.br.y} ${cantos!.bl.x},${cantos!.bl.y}`}
                fill="none"
                stroke="#C8102E"
                strokeWidth={2.5}
                vectorEffect="non-scaling-stroke"
              />
            </svg>

            {/* Alças arrastáveis (grandes, touch-friendly). */}
            {CANTOS.map((k) => {
              const p = cantos![k];
              return (
                <div
                  key={k}
                  onPointerDown={aoDescer(k)}
                  onPointerMove={aoMover(k)}
                  onPointerUp={aoSubir}
                  onPointerCancel={aoSubir}
                  aria-label={`Canto ${k}`}
                  className={cn(
                    'absolute z-[5] grid h-11 w-11 -translate-x-1/2 -translate-y-1/2 cursor-grab touch-none place-items-center rounded-full border-2 border-white bg-marica/70 shadow-lg transition-transform',
                    arrastando === k && 'scale-110 bg-marica',
                  )}
                  style={{
                    left: `${(p.x / srcCanvas!.width) * 100}%`,
                    top: `${(p.y / srcCanvas!.height) * 100}%`,
                  }}
                >
                  <span className="h-3 w-3 rounded-full bg-white" />
                </div>
              );
            })}
          </div>
        )}

        {detectando && (
          <span className="absolute left-1/2 top-3 -translate-x-1/2 rounded-full bg-black/60 px-3 py-1 text-xs font-medium text-white">
            Detectando bordas…
          </span>
        )}

        {/* Lupa de precisão: sempre montada (ref estável); visível só ao arrastar.
            Ancorada no topo da área de preview, no lado oposto ao dedo. */}
        <div
          className={cn(
            'pointer-events-none absolute top-2 z-10 transition-opacity',
            arrastando ? 'opacity-100' : 'opacity-0',
          )}
          style={ladoLupa === 'esq' ? { left: '0.5rem' } : { right: '0.5rem' }}
        >
          <canvas
            ref={lupaRef}
            width={LADO_LUPA}
            height={LADO_LUPA}
            className="block rounded-full border-4 border-white bg-white shadow-2xl"
            style={{ width: LADO_LUPA, height: LADO_LUPA }}
          />
        </div>
      </div>

      <div className="space-y-2 px-4 pb-[calc(env(safe-area-inset-bottom)+1rem)] pt-2">
        <PrimaryButton onClick={aplicarRecorte} disabled={!pronto}>
          <Crop className="h-5 w-5" /> Aplicar recorte
        </PrimaryButton>
        <div className="flex gap-2">
          <GhostButton className="flex-1" onClick={paginaInteira} disabled={!pronto}>
            <Maximize className="h-4 w-4" /> Página inteira
          </GhostButton>
          <GhostButton className="flex-1" onClick={aoRefazerFoto}>
            <RotateCcw className="h-4 w-4" /> Refazer foto
          </GhostButton>
        </div>
      </div>
    </div>
  );
}
