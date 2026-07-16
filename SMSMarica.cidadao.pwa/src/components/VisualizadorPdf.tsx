import { useEffect, useRef, useState } from 'react';
import { Download, Loader2, Minus, Plus, Share2, X } from 'lucide-react';
import * as pdfjsLib from 'pdfjs-dist';
// Worker empacotado pelo Vite como .js separado (classic worker) — evita o .mjs servido
// com MIME errado pelo nginx ("Failed to fetch dynamically imported module"), sem inchar o
// bundle principal. Fica em chunk próprio (lazy) e é precacheado pelo Workbox.
import PdfWorker from 'pdfjs-dist/build/pdf.worker.min.mjs?worker';
import { usePdfViewer } from '@/store/pdfViewer';

pdfjsLib.GlobalWorkerOptions.workerPort = new PdfWorker();

const ESCALA_MIN = 1;
const ESCALA_MAX = 4;

function limitar(v: number) {
  return Math.min(ESCALA_MAX, Math.max(ESCALA_MIN, v));
}

/**
 * Visualizador de PDF em tela cheia, renderizado no próprio app com pdf.js (canvas por
 * página). Some a necessidade de um leitor de PDF externo. Botão de baixar sempre presente.
 *
 * Zoom por GESTO: pinça (dois dedos) e duplo-toque ampliam/reduzem o documento; os botões
 * −/+ fazem o mesmo. A ampliação é feita crescendo a LARGURA do conteúdo, então o próprio
 * scroll nativo do container cuida do arraste (pan) em qualquer direção — inclusive rolar
 * entre as páginas.
 */
export function VisualizadorPdf() {
  const { aberto, dados, nome, fechar } = usePdfViewer();
  const containerRef = useRef<HTMLDivElement | null>(null);
  const conteudoRef = useRef<HTMLDivElement | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState(false);
  const [erroDetalhe, setErroDetalhe] = useState<string | null>(null);
  const [escala, setEscala] = useState(1);
  // Espelho da escala p/ os handlers de toque (nativos, sem re-render) lerem o valor atual.
  const escalaRef = useRef(1);
  escalaRef.current = escala;

  useEffect(() => {
    if (!aberto || !dados) return;
    let cancelado = false;
    const container = containerRef.current;
    const conteudo = conteudoRef.current;
    if (conteudo) conteudo.innerHTML = '';
    setCarregando(true);
    setErro(false);
    setErroDetalhe(null);
    setEscala(1); // cada documento abre em 100%

    (async () => {
      try {
        // Diagnóstico: primeiros bytes devem ser "%PDF". Se não, o backend mandou outra coisa.
        const cabecalho = new TextDecoder().decode(new Uint8Array(dados.slice(0, 5)));
        // Cópia: o pdf.js "detaches" o buffer; guardamos o original no store para o download.
        const doc = await pdfjsLib.getDocument({ data: dados.slice(0) }).promise;
        void cabecalho;
        const larguraAlvo = Math.min(container?.clientWidth ?? 720, 900);
        // Folga leve de resolução p/ o zoom por gesto (pinça/duplo-toque) não ficar tão borrado,
        // sem estourar a memória em PDFs de imagem com muitas páginas (cap total 2.5x).
        const dpr = Math.min((window.devicePixelRatio || 1) * 1.4, 2.5);

        for (let n = 1; n <= doc.numPages; n++) {
          if (cancelado) return;
          const page = await doc.getPage(n);
          const base = page.getViewport({ scale: 1 });
          const escalaRender = larguraAlvo / base.width;
          const viewport = page.getViewport({ scale: escalaRender });

          const canvas = document.createElement('canvas');
          canvas.width = Math.floor(viewport.width * dpr);
          canvas.height = Math.floor(viewport.height * dpr);
          canvas.style.width = '100%';
          canvas.style.height = 'auto';
          canvas.className = 'mx-auto mb-3 rounded-lg bg-white shadow';
          const ctx = canvas.getContext('2d');
          if (!ctx) continue;
          conteudo?.appendChild(canvas);
          await page.render({ canvasContext: ctx, viewport, transform: dpr !== 1 ? [dpr, 0, 0, dpr, 0, 0] : undefined }).promise;
        }
        if (!cancelado) setCarregando(false);
      } catch (e) {
        if (!cancelado) {
          const cab = (() => {
            try {
              return new TextDecoder().decode(new Uint8Array(dados.slice(0, 8)));
            } catch {
              return '?';
            }
          })();
          setErroDetalhe(`${(e as Error)?.name ?? 'Erro'}: ${(e as Error)?.message ?? String(e)} · início="${cab}" · ${dados.byteLength}B`);
          setErro(true);
          setCarregando(false);
        }
      }
    })();

    return () => {
      cancelado = true;
    };
  }, [aberto, dados]);

  // Gestos de zoom: pinça (2 dedos) e duplo-toque. Listeners nativos com passive:false para
  // poder cancelar o comportamento padrão do navegador durante a pinça.
  useEffect(() => {
    const cont = containerRef.current;
    if (!cont || !aberto) return;

    let pinchDist0 = 0;
    let pinchEscala0 = 1;
    let ultimoToque = 0;

    const distancia = (t: TouchList) =>
      Math.hypot(t[0].clientX - t[1].clientX, t[0].clientY - t[1].clientY);

    const aoIniciar = (e: TouchEvent) => {
      if (e.touches.length === 2) {
        pinchDist0 = distancia(e.touches);
        pinchEscala0 = escalaRef.current;
      } else if (e.touches.length === 1) {
        const agora = Date.now();
        if (agora - ultimoToque < 300) {
          // Duplo-toque: alterna entre 100% e 2,5x.
          setEscala((s) => (s > 1 ? 1 : 2.5));
          ultimoToque = 0;
        } else {
          ultimoToque = agora;
        }
      }
    };
    const aoMover = (e: TouchEvent) => {
      if (e.touches.length === 2 && pinchDist0 > 0) {
        e.preventDefault(); // impede o zoom nativo da página durante a pinça
        setEscala(limitar((pinchEscala0 * distancia(e.touches)) / pinchDist0));
      }
    };
    const aoTerminar = (e: TouchEvent) => {
      if (e.touches.length < 2) pinchDist0 = 0;
    };

    cont.addEventListener('touchstart', aoIniciar, { passive: false });
    cont.addEventListener('touchmove', aoMover, { passive: false });
    cont.addEventListener('touchend', aoTerminar);
    return () => {
      cont.removeEventListener('touchstart', aoIniciar);
      cont.removeEventListener('touchmove', aoMover);
      cont.removeEventListener('touchend', aoTerminar);
    };
  }, [aberto]);

  if (!aberto) return null;

  function baixar() {
    if (!dados) return;
    const blobUrl = URL.createObjectURL(new Blob([dados.slice(0)], { type: 'application/pdf' }));
    const a = document.createElement('a');
    a.href = blobUrl;
    a.download = nome;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(blobUrl), 10_000);
  }

  // Compartilhamento nativo do celular (WhatsApp, e-mail, etc.). Só aparece se o
  // aparelho suportar compartilhar arquivos; senão, o botão Baixar já cobre.
  const arquivo = dados ? new File([dados.slice(0)], nome, { type: 'application/pdf' }) : null;
  const podeCompartilhar =
    typeof navigator !== 'undefined' &&
    !!navigator.canShare &&
    !!arquivo &&
    navigator.canShare({ files: [arquivo] });

  async function compartilhar() {
    if (!dados) return;
    const file = new File([dados.slice(0)], nome, { type: 'application/pdf' });
    try {
      await navigator.share({ files: [file], title: nome });
    } catch {
      /* usuário cancelou ou não suportado — silencioso */
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-tinta/95">
      <header className="flex items-center justify-between gap-3 px-4 pb-3 pt-[calc(env(safe-area-inset-top)+0.75rem)] text-white">
        <p className="min-w-0 flex-1 truncate text-sm font-medium">{nome}</p>
        {podeCompartilhar ? (
          <button
            type="button"
            onClick={compartilhar}
            className="flex items-center gap-1.5 rounded-lg bg-white/15 px-3 py-1.5 text-sm font-medium transition active:scale-95"
          >
            <Share2 className="h-4 w-4" /> Compartilhar
          </button>
        ) : null}
        <button
          type="button"
          onClick={baixar}
          className="flex items-center gap-1.5 rounded-lg bg-white/15 px-3 py-1.5 text-sm font-medium transition active:scale-95"
        >
          <Download className="h-4 w-4" /> Baixar
        </button>
        <button
          type="button"
          onClick={fechar}
          aria-label="Fechar"
          className="grid h-9 w-9 place-items-center rounded-lg text-white transition active:bg-white/15"
        >
          <X className="h-5 w-5" />
        </button>
      </header>

      {/* touch-action pan-x pan-y: deixa o scroll nativo (1 dedo) e libera a pinça (2 dedos)
          para o nosso handler, sem o zoom nativo da página atrapalhar. */}
      <div
        ref={containerRef}
        className="flex-1 overflow-auto px-3 pb-[calc(env(safe-area-inset-bottom)+1rem)]"
        style={{ touchAction: 'pan-x pan-y' }}
      >
        <div
          ref={conteudoRef}
          style={{ width: `${escala * 100}%`, margin: '0 auto', transition: 'width 80ms ease-out' }}
        />
      </div>

      {/* Controle de zoom (também serve no desktop e como affordance do gesto). */}
      {!carregando && !erro ? (
        <div className="pointer-events-none absolute inset-x-0 bottom-[calc(env(safe-area-inset-bottom)+1rem)] flex justify-center">
          <div className="pointer-events-auto flex items-center gap-1 rounded-full bg-tinta/80 px-1.5 py-1 text-white shadow-lg backdrop-blur">
            <button
              type="button"
              onClick={() => setEscala((s) => limitar(s - 0.5))}
              disabled={escala <= ESCALA_MIN}
              aria-label="Reduzir"
              className="grid h-9 w-9 place-items-center rounded-full transition active:bg-white/15 disabled:opacity-40"
            >
              <Minus className="h-5 w-5" />
            </button>
            <button
              type="button"
              onClick={() => setEscala(1)}
              className="min-w-[3rem] text-center text-sm font-medium tabular-nums"
            >
              {Math.round(escala * 100)}%
            </button>
            <button
              type="button"
              onClick={() => setEscala((s) => limitar(s + 0.5))}
              disabled={escala >= ESCALA_MAX}
              aria-label="Ampliar"
              className="grid h-9 w-9 place-items-center rounded-full transition active:bg-white/15 disabled:opacity-40"
            >
              <Plus className="h-5 w-5" />
            </button>
          </div>
        </div>
      ) : null}

      {carregando ? (
        <div className="absolute inset-0 flex items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-white" />
        </div>
      ) : null}
      {erro ? (
        <div className="absolute inset-x-0 bottom-24 mx-auto max-w-xs rounded-xl bg-white px-4 py-3 text-center text-sm text-tinta shadow-lg">
          Não foi possível exibir aqui. Toque em <strong>Baixar</strong> para salvar o arquivo.
          {erroDetalhe ? (
            <p className="mt-2 break-words text-left font-mono text-[10px] leading-tight text-tinta-mute">{erroDetalhe}</p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
