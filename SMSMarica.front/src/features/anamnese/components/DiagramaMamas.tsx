import type { MouseEvent } from 'react';
import type { MarcacaoMama } from '@/features/anamnese/types';

type Props = {
  marcacoes: MarcacaoMama[];
  aoMudar: (marcacoes: MarcacaoMama[]) => void;
  somenteLeitura?: boolean;
};

/** Distância (em % do desenho) abaixo da qual um clique remove a marcação existente. */
const RAIO_REMOCAO = 7;

/**
 * Diagrama das mamas (direita/esquerda) espelhando o formulário em papel:
 * contorno com quadrantes tracejados e aréola central. Clique adiciona uma
 * marcação (achado clínico); clique sobre uma marcação a remove.
 */
export function DiagramaMamas({ marcacoes, aoMudar, somenteLeitura = false }: Props) {
  function aoClicar(mama: MarcacaoMama['mama'], e: MouseEvent<SVGSVGElement>) {
    if (somenteLeitura) return;
    const svg = e.currentTarget;
    const rect = svg.getBoundingClientRect();
    const x = ((e.clientX - rect.left) / rect.width) * 100;
    const y = ((e.clientY - rect.top) / rect.height) * 120;

    const proxima = marcacoes.find(
      (m) => m.mama === mama && Math.hypot(m.x - x, m.y - y) < RAIO_REMOCAO,
    );
    if (proxima) {
      aoMudar(marcacoes.filter((m) => m !== proxima));
    } else {
      aoMudar([...marcacoes, { mama, x: Math.round(x * 10) / 10, y: Math.round(y * 10) / 10 }]);
    }
  }

  function desenharMama(mama: MarcacaoMama['mama']) {
    const doLado = marcacoes.filter((m) => m.mama === mama);
    return (
      <div className="flex flex-col items-center">
        <span className="mb-1 text-xs font-semibold tracking-wide text-sky-700">
          {mama === 'direita' ? 'MAMA DIREITA' : 'MAMA ESQUERDA'}
        </span>
        <svg
          viewBox="0 0 100 120"
          onClick={(e) => aoClicar(mama, e)}
          className={`h-48 w-40 rounded-md border border-orange-100 bg-white ${
            somenteLeitura ? '' : 'cursor-crosshair'
          }`}
          role="img"
          aria-label={`Diagrama da mama ${mama} — clique para marcar achados`}
        >
          {/* contorno da mama (perfil aproximado do formulário) */}
          <path
            d="M 50 8 C 22 10 8 36 8 62 C 8 92 26 112 50 112 C 74 112 92 92 92 62 C 92 36 78 10 50 8 Z"
            fill="none"
            stroke="#f59e0b"
            strokeWidth="1.6"
            opacity="0.8"
          />
          {/* quadrantes tracejados */}
          <line x1="50" y1="8" x2="50" y2="112" stroke="#fb923c" strokeWidth="0.8" strokeDasharray="3 3" />
          <line x1="8" y1="60" x2="92" y2="60" stroke="#fb923c" strokeWidth="0.8" strokeDasharray="3 3" />
          {/* aréola central */}
          <circle cx="50" cy="60" r="6" fill="none" stroke="#ea580c" strokeWidth="1" />
          <circle cx="50" cy="60" r="2.4" fill="#ea580c" />
          {/* marcações de achados */}
          {doLado.map((m, i) => (
            <g key={`${m.x}-${m.y}-${i}`}>
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
          Clique no diagrama para marcar um achado; clique na marcação para removê-la.
        </p>
      ) : null}
    </div>
  );
}
