import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ChevronLeft,
  ChevronRight,
  Copy,
  Download,
  ExternalLink,
  Minus,
  Plus,
  RotateCcw,
  X,
} from 'lucide-react';
import { notificar } from '@/shared/ui/Notificacoes';

export type ImagemVisualizador = {
  url: string;
  legenda?: string;
  /** Nome sugerido ao salvar (Salvar como). Se ausente, deriva da legenda ou da URL. */
  nomeArquivo?: string;
};

type Props = {
  imagens: ImagemVisualizador[];
  /** Índice inicial (0). */
  indiceInicial?: number;
  aoFechar: () => void;
};

const ESCALA_MIN = 1;
const ESCALA_MAX = 8;
const PASSO = 0.5;

/**
 * Lightbox de imagem em tela cheia com zoom (scroll / botões / duplo-clique) e
 * arraste (pan) quando ampliada. Fecha no ESC, no X ou clicando no fundo. Se
 * receber mais de uma imagem, mostra setas de navegação e o contador.
 */
export function VisualizadorImagem({ imagens, indiceInicial = 0, aoFechar }: Props) {
  const [indice, setIndice] = useState(indiceInicial);
  const [escala, setEscala] = useState(1);
  const [pos, setPos] = useState({ x: 0, y: 0 });
  const arrastando = useRef<{ x: number; y: number } | null>(null);

  const total = imagens.length;
  const atual = imagens[indice];

  const resetar = useCallback(() => {
    setEscala(1);
    setPos({ x: 0, y: 0 });
  }, []);

  const irPara = useCallback(
    (delta: number) => {
      setIndice((i) => (i + delta + total) % total);
      setEscala(1);
      setPos({ x: 0, y: 0 });
    },
    [total],
  );

  // Teclado: ESC fecha, setas navegam, +/- ajustam o zoom.
  useEffect(() => {
    function aoTeclar(e: KeyboardEvent) {
      if (e.key === 'Escape') aoFechar();
      else if (e.key === 'ArrowLeft' && total > 1) irPara(-1);
      else if (e.key === 'ArrowRight' && total > 1) irPara(1);
      else if (e.key === '+' || e.key === '=') setEscala((s) => Math.min(ESCALA_MAX, s + PASSO));
      else if (e.key === '-') setEscala((s) => Math.max(ESCALA_MIN, s - PASSO));
    }
    window.addEventListener('keydown', aoTeclar);
    return () => window.removeEventListener('keydown', aoTeclar);
  }, [aoFechar, irPara, total]);

  function aoRolar(e: React.WheelEvent) {
    const delta = e.deltaY < 0 ? PASSO : -PASSO;
    setEscala((s) => {
      const nova = Math.min(ESCALA_MAX, Math.max(ESCALA_MIN, s + delta));
      if (nova === 1) setPos({ x: 0, y: 0 });
      return nova;
    });
  }

  function aoPressionar(e: React.MouseEvent) {
    if (escala === 1) return;
    arrastando.current = { x: e.clientX - pos.x, y: e.clientY - pos.y };
  }
  function aoMover(e: React.MouseEvent) {
    if (!arrastando.current) return;
    setPos({ x: e.clientX - arrastando.current.x, y: e.clientY - arrastando.current.y });
  }
  function aoSoltar() {
    arrastando.current = null;
  }

  const zoomIn = () => setEscala((s) => Math.min(ESCALA_MAX, s + PASSO));
  const zoomOut = () =>
    setEscala((s) => {
      const nova = Math.max(ESCALA_MIN, s - PASSO);
      if (nova === 1) setPos({ x: 0, y: 0 });
      return nova;
    });

  // Salvar como: baixa via blob (funciona cross-origin, força o nome do arquivo). Se falhar
  // — rede/CORS —, cai para abrir em nova aba, deixando o próprio browser oferecer o salvamento.
  async function salvarComo() {
    if (!atual) return;
    const nome = nomeSugerido(atual);
    try {
      const resp = await fetch(atual.url);
      if (!resp.ok) throw new Error(String(resp.status));
      const blob = await resp.blob();
      const href = URL.createObjectURL(blob);
      baixarLink(href, nome);
      URL.revokeObjectURL(href);
    } catch {
      window.open(atual.url, '_blank', 'noopener,noreferrer');
    }
  }

  function abrirNovaAba() {
    if (atual) window.open(atual.url, '_blank', 'noopener,noreferrer');
  }

  async function copiarLink() {
    if (!atual) return;
    try {
      await navigator.clipboard.writeText(atual.url);
      notificar('Link copiado para a área de transferência.', 'sucesso');
    } catch {
      notificar('Não foi possível copiar o link.', 'erro');
    }
  }

  if (!atual) return null;

  return (
    <div
      className="fixed inset-0 z-[60] flex flex-col bg-black/90 backdrop-blur-sm"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) aoFechar();
      }}
      onMouseMove={aoMover}
      onMouseUp={aoSoltar}
      role="dialog"
      aria-modal="true"
    >
      {/* Barra superior: contador + controles */}
      <div className="flex items-center justify-between gap-2 px-4 py-3 text-white">
        <span className="text-sm text-white/80">
          {total > 1 ? `${indice + 1} / ${total}` : ''}
          {atual.legenda ? <span className="ml-2 text-white/60">{atual.legenda}</span> : null}
        </span>
        <div className="flex items-center gap-1">
          <BotaoBarra titulo="Diminuir zoom (-)" aoClicar={zoomOut} disabled={escala <= ESCALA_MIN}>
            <Minus className="h-5 w-5" />
          </BotaoBarra>
          <span className="w-12 text-center text-xs tabular-nums text-white/70">
            {Math.round(escala * 100)}%
          </span>
          <BotaoBarra titulo="Aumentar zoom (+)" aoClicar={zoomIn} disabled={escala >= ESCALA_MAX}>
            <Plus className="h-5 w-5" />
          </BotaoBarra>
          <BotaoBarra titulo="Redefinir" aoClicar={resetar} disabled={escala === 1 && pos.x === 0 && pos.y === 0}>
            <RotateCcw className="h-5 w-5" />
          </BotaoBarra>
          <span className="mx-1 h-5 w-px bg-white/20" aria-hidden />
          <BotaoBarra titulo="Salvar como…" aoClicar={salvarComo}>
            <Download className="h-5 w-5" />
          </BotaoBarra>
          <BotaoBarra titulo="Abrir em nova aba" aoClicar={abrirNovaAba}>
            <ExternalLink className="h-5 w-5" />
          </BotaoBarra>
          <BotaoBarra titulo="Copiar link" aoClicar={copiarLink}>
            <Copy className="h-5 w-5" />
          </BotaoBarra>
          <span className="mx-1 h-5 w-px bg-white/20" aria-hidden />
          <BotaoBarra titulo="Fechar (Esc)" aoClicar={aoFechar}>
            <X className="h-5 w-5" />
          </BotaoBarra>
        </div>
      </div>

      {/* Área da imagem */}
      <div
        className="relative flex flex-1 items-center justify-center overflow-hidden"
        onWheel={aoRolar}
        onMouseDown={(e) => {
          // Clicar no vazio da área (não na imagem) fecha.
          if (e.target === e.currentTarget) aoFechar();
        }}
      >
        {total > 1 ? (
          <BotaoSeta lado="esq" aoClicar={() => irPara(-1)}>
            <ChevronLeft className="h-7 w-7" />
          </BotaoSeta>
        ) : null}

        <img
          src={atual.url}
          alt={atual.legenda ?? 'Imagem'}
          draggable={false}
          onMouseDown={aoPressionar}
          onDoubleClick={() => (escala === 1 ? setEscala(2) : resetar())}
          style={{
            transform: `translate(${pos.x}px, ${pos.y}px) scale(${escala})`,
            cursor: escala > 1 ? (arrastando.current ? 'grabbing' : 'grab') : 'zoom-in',
          }}
          className="max-h-full max-w-full select-none object-contain transition-transform duration-75"
        />

        {total > 1 ? (
          <BotaoSeta lado="dir" aoClicar={() => irPara(1)}>
            <ChevronRight className="h-7 w-7" />
          </BotaoSeta>
        ) : null}
      </div>
    </div>
  );
}

function BotaoBarra({
  children,
  titulo,
  aoClicar,
  disabled,
}: {
  children: React.ReactNode;
  titulo: string;
  aoClicar: () => void;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      title={titulo}
      aria-label={titulo}
      onClick={aoClicar}
      disabled={disabled}
      className="rounded-md p-2 text-white/90 transition hover:bg-white/15 disabled:opacity-40 disabled:hover:bg-transparent"
    >
      {children}
    </button>
  );
}

/** Nome sugerido para o download: nomeArquivo > legenda > último segmento da URL > padrão. */
function nomeSugerido(img: ImagemVisualizador): string {
  if (img.nomeArquivo) return img.nomeArquivo;
  if (img.legenda) return img.legenda;
  try {
    const caminho = new URL(img.url, window.location.href).pathname;
    const ultimo = caminho.split('/').filter(Boolean).pop();
    if (ultimo) return decodeURIComponent(ultimo);
  } catch {
    /* URL inválida: usa o padrão abaixo */
  }
  return 'imagem';
}

/** Dispara o download de um href via âncora temporária. */
function baixarLink(href: string, nome: string) {
  const a = document.createElement('a');
  a.href = href;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
}

function BotaoSeta({
  children,
  lado,
  aoClicar,
}: {
  children: React.ReactNode;
  lado: 'esq' | 'dir';
  aoClicar: () => void;
}) {
  return (
    <button
      type="button"
      onClick={aoClicar}
      aria-label={lado === 'esq' ? 'Anterior' : 'Próxima'}
      className={`absolute top-1/2 z-10 -translate-y-1/2 rounded-full bg-white/10 p-2 text-white transition hover:bg-white/25 ${
        lado === 'esq' ? 'left-3' : 'right-3'
      }`}
    >
      {children}
    </button>
  );
}
