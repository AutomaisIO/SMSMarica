import { useState } from 'react';
import { ChevronDown, Navigation } from 'lucide-react';

/**
 * Card informativo do Google Navigation SDK (turn-by-turn no app do motorista).
 *
 * Diferente do card "Google Maps" (Routes/Geocoding, chave server-side): a chave
 * do Navigation SDK é **mobile**, embarcada no build do app Android e restrita
 * por package + SHA-1 — não é gravada aqui. Este card existe para tornar a
 * dependência visível e lembrar das salvaguardas de custo.
 * Detalhes: docs/modulos/tfd/navegacao-motorista.md.
 */
export function NavigationSdkCard() {
  const [aberto, setAberto] = useState(false);

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <button
        type="button"
        onClick={() => setAberto((a) => !a)}
        className="flex w-full items-center justify-between gap-3 text-left"
      >
        <span className="flex items-center gap-2">
          <Navigation className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">
            Google Navigation SDK (app do motorista)
          </span>
        </span>
        <span className="flex items-center gap-2">
          <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-medium text-amber-700">
            Planejado
          </span>
          <ChevronDown
            className={`h-4 w-4 text-gray-400 transition-transform ${aberto ? 'rotate-180' : ''}`}
          />
        </span>
      </button>

      {aberto ? (
        <div className="mt-4 space-y-3 border-t border-gray-100 pt-4 text-sm text-gray-600">
          <p>
            Navegação turn-by-turn embarcada no app do motorista. A chave é{' '}
            <strong>mobile</strong> (no build Android, restrita por package +
            SHA-1) — por isso não é configurada nesta tela. O que se configura
            aqui é a <strong>Routes API</strong> (card Google Maps), usada pelo
            servidor para otimizar a ordem das paradas.
          </p>
          <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-amber-800">
            <p className="font-medium">Atenção a custo (cobrado por uso):</p>
            <ul className="mt-1 list-disc space-y-0.5 pl-4 text-[13px]">
              <li>Uma rota por translado, com as paradas como waypoints.</li>
              <li>Otimização no servidor (Routes API), uma vez por translado.</li>
              <li>Não recriar a sessão de navegação a cada parada.</li>
              <li>Geocoding em cache; orçamento + alertas de billing no GCP.</li>
            </ul>
          </div>
        </div>
      ) : null}
    </div>
  );
}
