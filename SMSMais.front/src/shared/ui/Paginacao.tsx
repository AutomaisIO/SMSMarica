import { ChevronLeft, ChevronRight } from 'lucide-react';

export const TAMANHOS_PAGINA = [100, 200, 500, 1000] as const;

type Props = {
  /** Página atual, a partir de 1. */
  pagina: number;
  tamanho: number;
  total: number;
  aoMudarPagina: (pagina: number) => void;
  aoMudarTamanho: (tamanho: number) => void;
  tamanhos?: readonly number[];
};

/** 1 … 4 5 6 … 20 — a primeira, a última e as vizinhas da atual. */
function paginasVisiveis(atual: number, ultima: number): (number | '…')[] {
  const escolhidas = [...new Set([1, ultima, atual - 1, atual, atual + 1])]
    .filter((p) => p >= 1 && p <= ultima)
    .sort((a, b) => a - b);
  const saida: (number | '…')[] = [];
  escolhidas.forEach((p, i) => {
    if (i > 0 && p - escolhidas[i - 1] > 1) saida.push('…');
    saida.push(p);
  });
  return saida;
}

/**
 * Barra de paginação: quantos por página (100/200/500/1000), "1–100 de 1.234" e as páginas.
 * Não guarda estado — quem usa decide página e tamanho (e volta à página 1 ao trocar filtro).
 */
export function Paginacao({
  pagina,
  tamanho,
  total,
  aoMudarPagina,
  aoMudarTamanho,
  tamanhos = TAMANHOS_PAGINA,
}: Props) {
  const ultima = Math.max(1, Math.ceil(total / tamanho));
  const de = total === 0 ? 0 : (pagina - 1) * tamanho + 1;
  const ate = Math.min(pagina * tamanho, total);
  const fmt = (n: number) => n.toLocaleString('pt-BR');

  const botao =
    'inline-flex h-8 min-w-8 items-center justify-center rounded-md border px-2 text-sm transition disabled:cursor-not-allowed disabled:opacity-40';

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 text-sm text-slate-600">
      <label className="flex items-center gap-2">
        Mostrar
        <select
          value={tamanho}
          onChange={(e) => aoMudarTamanho(Number(e.target.value))}
          className="rounded-md border border-slate-300 bg-white px-2 py-1 text-sm text-slate-800 focus:border-red-700 focus:outline-none"
        >
          {tamanhos.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
        por página
      </label>

      <span className="tabular-nums">
        {fmt(de)}–{fmt(ate)} de {fmt(total)}
      </span>

      <nav className="flex items-center gap-1" aria-label="Paginação">
        <button
          type="button"
          className={`${botao} border-slate-300 bg-white hover:bg-slate-50`}
          onClick={() => aoMudarPagina(pagina - 1)}
          disabled={pagina <= 1}
          aria-label="Página anterior"
        >
          <ChevronLeft className="size-4" />
        </button>
        {paginasVisiveis(pagina, ultima).map((p, i) =>
          p === '…' ? (
            <span key={`r${i}`} className="px-1 text-slate-400">
              …
            </span>
          ) : (
            <button
              key={p}
              type="button"
              onClick={() => aoMudarPagina(p)}
              aria-current={p === pagina ? 'page' : undefined}
              className={`${botao} tabular-nums ${
                p === pagina
                  ? 'border-red-700 bg-red-700 font-semibold text-white'
                  : 'border-slate-300 bg-white hover:bg-slate-50'
              }`}
            >
              {p}
            </button>
          ),
        )}
        <button
          type="button"
          className={`${botao} border-slate-300 bg-white hover:bg-slate-50`}
          onClick={() => aoMudarPagina(pagina + 1)}
          disabled={pagina >= ultima}
          aria-label="Próxima página"
        >
          <ChevronRight className="size-4" />
        </button>
      </nav>
    </div>
  );
}
