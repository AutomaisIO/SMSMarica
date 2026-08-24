import { useState, type PointerEvent } from 'react';
import type { LadoMama, MarcacaoMama, PontoTraco, TracoMama } from '@/features/anamnese/types';

type Props = {
  marcacoes: MarcacaoMama[];
  tracos: TracoMama[];
  aoMudar: (marcacoes: MarcacaoMama[]) => void;
  aoMudarTracos: (tracos: TracoMama[]) => void;
  somenteLeitura?: boolean;
};

/** Distância (em % do desenho) abaixo da qual um clique remove a marcação/traço existente. */
const RAIO_REMOCAO = 7;
/** Comprimento mínimo (em unidades do viewBox) para tratar o gesto como desenho, não clique. */
const LIMIAR_ARRASTO = 4;

type Desenho = { mama: LadoMama; pontos: PontoTraco[] };

/**
 * Diagrama das mamas (direita/esquerda) espelhando o formulário em papel:
 * contorno com quadrantes tracejados e aréola central.
 * - Clique: adiciona uma marcação pontual (achado clínico). Clicar sobre uma
 *   marcação ou um traço o remove.
 * - Clicar e arrastar: desenha um traço livre ("brush"), ex.: cicatriz de cirurgia.
 */
export function DiagramaMamas({ marcacoes, tracos, aoMudar, aoMudarTracos, somenteLeitura = false }: Props) {
  const [desenho, setDesenho] = useState<Desenho | null>(null);

  function coords(e: PointerEvent<SVGSVGElement>): PontoTraco {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = clamp(((e.clientX - rect.left) / rect.width) * 100, 0, 100);
    const y = clamp(((e.clientY - rect.top) / rect.height) * 120, 0, 120);
    return { x: arred(x), y: arred(y) };
  }

  function aoPressionar(mama: LadoMama, e: PointerEvent<SVGSVGElement>) {
    if (somenteLeitura || e.button !== 0) return;
    e.currentTarget.setPointerCapture(e.pointerId);
    setDesenho({ mama, pontos: [coords(e)] });
  }

  function aoMover(mama: LadoMama, e: PointerEvent<SVGSVGElement>) {
    if (!desenho || desenho.mama !== mama) return;
    const p = coords(e);
    const ultimo = desenho.pontos[desenho.pontos.length - 1];
    if (Math.hypot(p.x - ultimo.x, p.y - ultimo.y) < 0.8) return; // amostra
    setDesenho({ mama, pontos: [...desenho.pontos, p] });
  }

  function aoSoltar(mama: LadoMama, e: PointerEvent<SVGSVGElement>) {
    if (!desenho || desenho.mama !== mama) return;
    try {
      e.currentTarget.releasePointerCapture(e.pointerId);
    } catch {
      /* ponteiro já liberado */
    }
    const { pontos } = desenho;
    setDesenho(null);

    // Arrastou o suficiente? Então é um traço; senão, é um clique.
    if (comprimento(pontos) >= LIMIAR_ARRASTO) {
      aoMudarTracos([...tracos, { mama, pontos }]);
    } else {
      aoClicar(mama, pontos[0]);
    }
  }

  /** Clique simples: remove ponto/traço próximo, ou adiciona um novo ponto. */
  function aoClicar(mama: LadoMama, p: PontoTraco) {
    const marcacaoProxima = marcacoes.find(
      (m) => m.mama === mama && Math.hypot(m.x - p.x, m.y - p.y) < RAIO_REMOCAO,
    );
    if (marcacaoProxima) {
      aoMudar(marcacoes.filter((m) => m !== marcacaoProxima));
      return;
    }

    const tracoProximo = tracos.find(
      (t) => t.mama === mama && distanciaAoTraco(p, t) < RAIO_REMOCAO,
    );
    if (tracoProximo) {
      aoMudarTracos(tracos.filter((t) => t !== tracoProximo));
      return;
    }

    aoMudar([...marcacoes, { mama, x: p.x, y: p.y }]);
  }

  function desenharMama(mama: LadoMama) {
    const pontosDoLado = marcacoes.filter((m) => m.mama === mama);
    const tracosDoLado = tracos.filter((t) => t.mama === mama);
    const emDesenho = desenho?.mama === mama ? desenho.pontos : null;
    return (
      <div className="flex flex-col items-center">
        <span className="mb-1 text-xs font-semibold tracking-wide text-sky-700">
          {mama === 'direita' ? 'MAMA DIREITA' : 'MAMA ESQUERDA'}
        </span>
        <svg
          viewBox="0 0 100 120"
          onPointerDown={(e) => aoPressionar(mama, e)}
          onPointerMove={(e) => aoMover(mama, e)}
          onPointerUp={(e) => aoSoltar(mama, e)}
          onPointerCancel={() => setDesenho(null)}
          style={{ touchAction: 'none' }}
          className={`h-48 w-40 rounded-md border border-gray-200 bg-white ${
            somenteLeitura ? '' : 'cursor-crosshair'
          }`}
          role="img"
          aria-label={`Diagrama da mama ${mama} — clique para marcar; clique e arraste para desenhar`}
        >
          {/* contorno da mama (perfil aproximado do formulário) */}
          <path
            d="M 50 8 C 22 10 8 36 8 62 C 8 92 26 112 50 112 C 74 112 92 92 92 62 C 92 36 78 10 50 8 Z"
            fill="none"
            stroke="#6b7280"
            strokeWidth="1.6"
            opacity="0.8"
          />
          {/* quadrantes tracejados */}
          <line x1="50" y1="8" x2="50" y2="112" stroke="#9ca3af" strokeWidth="0.8" strokeDasharray="3 3" />
          <line x1="8" y1="60" x2="92" y2="60" stroke="#9ca3af" strokeWidth="0.8" strokeDasharray="3 3" />
          {/* aréola central */}
          <circle cx="50" cy="60" r="6" fill="none" stroke="#6b7280" strokeWidth="1" />
          <circle cx="50" cy="60" r="2.4" fill="#6b7280" />
          {/* traços livres (cicatrizes etc.) */}
          {tracosDoLado.map((t, i) => (
            <polyline
              key={`t-${i}`}
              points={t.pontos.map((p) => `${p.x},${p.y}`).join(' ')}
              fill="none"
              stroke="#dc2626"
              strokeWidth="2.2"
              strokeLinecap="round"
              strokeLinejoin="round"
              opacity="0.85"
            />
          ))}
          {/* traço em andamento */}
          {emDesenho && emDesenho.length > 1 ? (
            <polyline
              points={emDesenho.map((p) => `${p.x},${p.y}`).join(' ')}
              fill="none"
              stroke="#dc2626"
              strokeWidth="2.2"
              strokeLinecap="round"
              strokeLinejoin="round"
              opacity="0.6"
            />
          ) : null}
          {/* marcações pontuais */}
          {pontosDoLado.map((m, i) => (
            <g key={`m-${m.x}-${m.y}-${i}`}>
              <circle cx={m.x} cy={m.y} r="4.5" fill="#dc2626" opacity="0.85" />
              <circle cx={m.x} cy={m.y} r="7" fill="none" stroke="#dc2626" strokeWidth="1" opacity="0.5" />
            </g>
          ))}
        </svg>
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-start justify-center gap-6">
        {desenharMama('direita')}
        {desenharMama('esquerda')}
      </div>
      {!somenteLeitura ? (
        <p className="mt-1 text-center text-xs text-gray-500">
          Clique para marcar um achado; <strong>clique e arraste para desenhar</strong> (ex.: cicatriz).
          Clique sobre uma marcação ou traço para removê-lo.
        </p>
      ) : null}
    </div>
  );
}

function clamp(v: number, min: number, max: number) {
  return Math.min(max, Math.max(min, v));
}

function arred(v: number) {
  return Math.round(v * 10) / 10;
}

function comprimento(pontos: PontoTraco[]) {
  let total = 0;
  for (let i = 1; i < pontos.length; i++) {
    total += Math.hypot(pontos[i].x - pontos[i - 1].x, pontos[i].y - pontos[i - 1].y);
  }
  return total;
}

/** Menor distância de um ponto aos vértices de um traço (suficiente p/ a remoção por clique). */
function distanciaAoTraco(p: PontoTraco, traco: TracoMama) {
  let min = Infinity;
  for (const q of traco.pontos) {
    min = Math.min(min, Math.hypot(p.x - q.x, p.y - q.y));
  }
  return min;
}
