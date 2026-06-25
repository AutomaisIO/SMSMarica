import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import jsQR from 'jsqr';

/**
 * Leitor de QR code dentro do app (útil quando o PWA já está instalado/aberto e o
 * cidadão não tem como reabrir a URL `?t=`). Abre a câmera traseira em tela cheia,
 * decodifica o QR e devolve o token via `onResultado`.
 *
 * CUIDADOS DE iPHONE (iOS Safari, inclusive instalado/standalone):
 *  - `playsInline` no <video> é ESSENCIAL: sem ele o iOS abre o vídeo em tela cheia
 *    nativa e o preview embutido não funciona. `muted` + `autoPlay` liberam o play
 *    automático sem novo gesto.
 *  - O stream é iniciado a partir do gesto do usuário: este componente só é montado
 *    após o clique no botão "Escanear QR code", e o getUserMedia roda no efeito de
 *    montagem (mesmo padrão da câmera do scanner, que já funciona no iOS).
 *  - `BarcodeDetector` NÃO existe no iOS Safari — caímos para o jsQR (decodificador
 *    puro em JS). No Android Chrome usamos o BarcodeDetector nativo (mais rápido).
 */

/** Maior lado do frame analisado: limita o custo do jsQR/getImageData no celular. */
const MAX_LADO_DET = 720;

/**
 * Extrai o token do conteúdo do QR. O QR aponta para
 * `https://arquivos.smsmarica.online/?t=TOKEN` — pegamos o parâmetro `t`. Se o
 * conteúdo não for URL mas parecer um token cru, usamos o texto como está.
 */
function extrairToken(texto: string): string | null {
  const t = texto.trim();
  if (!t) return null;
  try {
    const url = new URL(t);
    const param = url.searchParams.get('t')?.trim();
    return param || null; // É URL: só aceitamos se tiver `?t=` (senão não é o nosso QR).
  } catch {
    // Não é URL — aceita como token cru se parecer um (sem espaços, tamanho razoável).
    if (!/\s/.test(t) && t.length >= 8 && t.length <= 256) return t;
    return null;
  }
}

export function LeitorQr({
  onResultado,
  aoFechar,
}: {
  onResultado: (token: string) => void;
  aoFechar: () => void;
}) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [erro, setErro] = useState<string | null>(null);
  // Mantém a callback estável dentro do efeito sem reiniciar a câmera a cada render.
  const onResultadoRef = useRef(onResultado);
  onResultadoRef.current = onResultado;

  useEffect(() => {
    let stream: MediaStream | null = null;
    let raf = 0;
    let parado = false;
    let detector: BarcodeDetectorLike | null = null;
    let detectando = false; // evita chamadas sobrepostas ao BarcodeDetector (assíncrono).

    function parar() {
      parado = true;
      if (raf) cancelAnimationFrame(raf);
      stream?.getTracks().forEach((t) => t.stop());
    }

    function concluir(texto: string) {
      const token = extrairToken(texto);
      if (!token) return; // QR de outro app/sem `t`: ignora e segue escaneando.
      parar();
      onResultadoRef.current(token);
    }

    function tick() {
      if (parado) return;
      const v = videoRef.current;
      const canvas = canvasRef.current;
      if (v && canvas && v.videoWidth) {
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        if (ctx) {
          const escala = Math.min(1, MAX_LADO_DET / Math.max(v.videoWidth, v.videoHeight));
          const w = Math.max(1, Math.round(v.videoWidth * escala));
          const h = Math.max(1, Math.round(v.videoHeight * escala));
          canvas.width = w;
          canvas.height = h;
          ctx.drawImage(v, 0, 0, w, h);

          if (detector) {
            // Android: BarcodeDetector nativo (assíncrono) — uma detecção por vez.
            if (!detectando) {
              detectando = true;
              detector
                .detect(canvas)
                .then((codigos) => {
                  detectando = false;
                  if (codigos[0]?.rawValue) concluir(codigos[0].rawValue);
                })
                .catch(() => {
                  detectando = false;
                });
            }
          } else {
            // iOS (e fallback geral): jsQR sobre o ImageData.
            const img = ctx.getImageData(0, 0, w, h);
            const r = jsQR(img.data, w, h, { inversionAttempts: 'dontInvert' });
            if (r?.data) concluir(r.data);
          }
        }
      }
      raf = requestAnimationFrame(tick);
    }

    (async () => {
      try {
        // BarcodeDetector existe e suporta QR? (Android Chrome). Senão, jsQR (iOS).
        const BD = window.BarcodeDetector;
        if (BD) {
          try {
            const formatos = (await BD.getSupportedFormats?.()) ?? null;
            if (!formatos || formatos.includes('qr_code')) {
              detector = new BD({ formats: ['qr_code'] });
            }
          } catch {
            detector = null;
          }
        }

        stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: { ideal: 'environment' } },
          audio: false,
        });
        if (parado) {
          stream.getTracks().forEach((t) => t.stop());
          return;
        }
        const v = videoRef.current;
        if (v) {
          v.srcObject = stream;
          try {
            await v.play(); // playsInline + muted permitem o autoplay no iOS.
          } catch {
            /* play() pode ser interrompido ao desmontar — ignorar */
          }
        }
        raf = requestAnimationFrame(tick);
      } catch (err) {
        const nome = (err as DOMException | undefined)?.name;
        setErro(
          nome === 'NotAllowedError' || nome === 'SecurityError'
            ? 'Permissão de câmera negada. Libere a câmera nas configurações do navegador e tente de novo.'
            : nome === 'NotFoundError'
              ? 'Nenhuma câmera encontrada neste aparelho.'
              : 'Não foi possível abrir a câmera. Tente novamente.',
        );
      }
    })();

    return () => {
      parar();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="fixed inset-0 z-50 mx-auto flex max-w-[460px] flex-col bg-black">
      <div className="flex items-center justify-between px-3 pb-2 pt-[calc(env(safe-area-inset-top)+0.75rem)]">
        <button
          type="button"
          onClick={aoFechar}
          aria-label="Fechar leitor"
          className="grid h-11 w-11 place-items-center rounded-xl text-white transition active:bg-white/15"
        >
          <X className="h-6 w-6" />
        </button>
        <span className="text-sm font-medium text-white/80">Ler QR code do exame</span>
        <span className="h-11 w-11" />
      </div>

      <div className="relative flex-1 overflow-hidden">
        <video ref={videoRef} playsInline muted autoPlay className="h-full w-full object-cover" />
        {/* Canvas oculto: recebe o frame para a decodificação (BarcodeDetector/jsQR). */}
        <canvas ref={canvasRef} className="hidden" />

        {/* Moldura-guia central. */}
        <div className="pointer-events-none absolute inset-0 grid place-items-center">
          <div className="aspect-square w-[68%] max-w-[300px] rounded-3xl border-2 border-white/70 shadow-[0_0_0_9999px_rgba(0,0,0,0.35)]" />
        </div>
        <p className="pointer-events-none absolute inset-x-0 bottom-10 text-center text-sm font-medium text-white drop-shadow">
          Aponte para o QR code
        </p>

        {erro && (
          <div className="absolute inset-x-4 top-1/2 -translate-y-1/2 rounded-2xl bg-black/75 p-4 text-center text-sm text-white">
            {erro}
            <button
              type="button"
              onClick={aoFechar}
              className="mx-auto mt-4 block rounded-xl bg-white/15 px-4 py-2 text-sm font-semibold text-white transition active:scale-95"
            >
              Voltar
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
