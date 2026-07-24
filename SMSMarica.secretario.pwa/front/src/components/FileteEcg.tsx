import clsx from 'clsx';

/**
 * Filete de eletrocardiograma no pé da faixa vermelha — a marca viva do painel.
 * Traçado branco 1.5px sobre o vermelho, rolagem contínua e discreta; pausa
 * quando os dados estão velhos e vira estático com prefers-reduced-motion.
 */

// Unidade de 160 x-units (baseline y=13, viewBox 0 0 1120 20): P — QRS — T.
const SEGMENTO =
  'h32 c3 0 4.5 -4 7.5 -4 c3 0 4.5 4 7.5 4 h14 l3 2.5 l4.5 -13.5 l4.5 15.5 l3 -4.5 h12 c4 0 6 -6.5 10 -6.5 c4 0 6 6.5 10 6.5 h52 ';
const CAMINHO = `M0 13 ${SEGMENTO.repeat(7)}`;

function Onda() {
  return (
    <svg
      viewBox="0 0 1120 20"
      preserveAspectRatio="none"
      className="h-full w-1/2 shrink-0"
      fill="none"
    >
      <path
        d={CAMINHO}
        stroke="rgba(255,255,255,0.85)"
        strokeWidth={1.5}
        strokeLinecap="round"
        strokeLinejoin="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}

export function FileteEcg({ pausado }: { pausado: boolean }) {
  return (
    <div className="relative h-[18px] overflow-hidden" aria-hidden="true">
      <div className={clsx('anima-ecg flex h-full w-[200%]', pausado && 'ecg-pausado')}>
        <Onda />
        <Onda />
      </div>
      {/* fade nas bordas para o traçado nascer e morrer suave */}
      <div className="pointer-events-none absolute inset-y-0 left-0 w-12 bg-gradient-to-r from-vermelho-marica to-transparent" />
      <div className="pointer-events-none absolute inset-y-0 right-0 w-12 bg-gradient-to-l from-vermelho-marica to-transparent" />
    </div>
  );
}
