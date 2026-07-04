import { useEffect, useRef, useState } from 'react';
import { Download, Loader2, X } from 'lucide-react';
import * as pdfjsLib from 'pdfjs-dist';
// Worker empacotado pelo Vite como .js separado (classic worker) — evita o .mjs servido
// com MIME errado pelo nginx ("Failed to fetch dynamically imported module"), sem inchar o
// bundle principal. Fica em chunk próprio (lazy) e é precacheado pelo Workbox.
import PdfWorker from 'pdfjs-dist/build/pdf.worker.min.mjs?worker';
import { usePdfViewer } from '@/store/pdfViewer';

pdfjsLib.GlobalWorkerOptions.workerPort = new PdfWorker();

/**
 * Visualizador de PDF em tela cheia, renderizado no próprio app com pdf.js (canvas por
 * página). Some a necessidade de um leitor de PDF externo. Botão de baixar sempre presente.
 */
export function VisualizadorPdf() {
  const { aberto, dados, nome, fechar } = usePdfViewer();
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState(false);
  const [erroDetalhe, setErroDetalhe] = useState<string | null>(null);

  useEffect(() => {
    if (!aberto || !dados) return;
    let cancelado = false;
    const container = containerRef.current;
    if (container) container.innerHTML = '';
    setCarregando(true);
    setErro(false);
    setErroDetalhe(null);

    (async () => {
      try {
        // Diagnóstico: primeiros bytes devem ser "%PDF". Se não, o backend mandou outra coisa.
        const cabecalho = new TextDecoder().decode(new Uint8Array(dados.slice(0, 5)));
        // Cópia: o pdf.js "detaches" o buffer; guardamos o original no store para o download.
        const doc = await pdfjsLib.getDocument({ data: dados.slice(0) }).promise;
        void cabecalho;
        const larguraAlvo = Math.min(container?.clientWidth ?? 720, 900);
        const dpr = Math.min(window.devicePixelRatio || 1, 2);

        for (let n = 1; n <= doc.numPages; n++) {
          if (cancelado) return;
          const page = await doc.getPage(n);
          const base = page.getViewport({ scale: 1 });
          const escala = larguraAlvo / base.width;
          const viewport = page.getViewport({ scale: escala });

          const canvas = document.createElement('canvas');
          canvas.width = Math.floor(viewport.width * dpr);
          canvas.height = Math.floor(viewport.height * dpr);
          canvas.style.width = '100%';
          canvas.style.height = 'auto';
          canvas.className = 'mx-auto mb-3 rounded-lg bg-white shadow';
          const ctx = canvas.getContext('2d');
          if (!ctx) continue;
          container?.appendChild(canvas);
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

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-tinta/95">
      <header className="flex items-center justify-between gap-3 px-4 pb-3 pt-[calc(env(safe-area-inset-top)+0.75rem)] text-white">
        <p className="min-w-0 flex-1 truncate text-sm font-medium">{nome}</p>
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

      <div ref={containerRef} className="flex-1 overflow-auto px-3 pb-[calc(env(safe-area-inset-bottom)+1rem)]" />

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
