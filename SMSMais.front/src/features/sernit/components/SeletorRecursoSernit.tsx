import { useMemo, useRef, useState } from 'react';
import { Check, ChevronDown, Search, X } from 'lucide-react';

import type { CatalogoRecursoSernit } from '@/features/sernit/types';

/**
 * Seletor de recurso com busca por digitação.
 *
 * <p>O SERNIT tem 120 recursos em CONSULTA e 83 em EXAME, com nomes longos e cheios de subdivisão
 * ("Ambulatório 1ª vez em Cardiologia - Cardiopatia Congênita (Adulto)"). Um `select` comum com
 * essa lista é inutilizável — a própria tela do SERNIT oferece um campo de digitação ao lado do
 * combo pelo mesmo motivo.</p>
 *
 * <p>Aqui a busca é <b>local</b>, contra o catálogo espelhado: filtra enquanto se digita, sem ida
 * ao servidor. No SERNIT, cada tecla é uma requisição.</p>
 */
export function SeletorRecursoSernit({
  recursos,
  valor,
  onChange,
  desabilitado,
}: {
  recursos: CatalogoRecursoSernit[];
  valor: string;
  onChange: (r: CatalogoRecursoSernit | null) => void;
  desabilitado?: boolean;
}) {
  const [aberto, setAberto] = useState(false);
  const [termo, setTermo] = useState('');
  const caixa = useRef<HTMLDivElement>(null);

  const escolhido = recursos.find((r) => r.valor === valor) ?? null;

  const filtrados = useMemo(() => {
    const t = normalizar(termo);
    if (!t) return recursos.slice(0, 200);
    // Todas as palavras têm de aparecer, em qualquer ordem: quem procura "cardio congenita"
    // acha "Ambulatório 1ª vez em Cardiologia - Cardiopatia Congênita".
    const palavras = t.split(/\s+/).filter(Boolean);
    return recursos
      .filter((r) => {
        const alvo = normalizar(r.rotulo);
        return palavras.every((p) => alvo.includes(p));
      })
      .slice(0, 200);
  }, [recursos, termo]);

  function escolher(r: CatalogoRecursoSernit) {
    onChange(r);
    setAberto(false);
    setTermo('');
  }

  return (
    <div ref={caixa} className="relative">
      <button
        type="button"
        disabled={desabilitado}
        onClick={() => setAberto((a) => !a)}
        className="flex w-full items-center justify-between gap-2 rounded-md border border-slate-300 bg-white px-3 py-2 text-left text-sm disabled:bg-slate-100 disabled:text-slate-400"
      >
        <span className={escolhido ? 'text-slate-900' : 'text-slate-400'}>
          {escolhido?.rotulo ?? 'Selecione o recurso…'}
        </span>
        <span className="flex shrink-0 items-center gap-1">
          {escolhido && !desabilitado && (
            <X
              className="size-4 text-slate-400 hover:text-slate-700"
              onClick={(e) => {
                e.stopPropagation();
                onChange(null);
              }}
            />
          )}
          <ChevronDown className="size-4 text-slate-400" />
        </span>
      </button>

      {aberto && !desabilitado && (
        <div className="absolute z-20 mt-1 w-full rounded-md border border-slate-300 bg-white shadow-lg">
          <div className="flex items-center gap-2 border-b border-slate-200 px-3 py-2">
            <Search className="size-4 shrink-0 text-slate-400" />
            <input
              autoFocus
              value={termo}
              onChange={(e) => setTermo(e.target.value)}
              placeholder="digite parte do nome…"
              className="w-full text-sm outline-none"
            />
            {termo && (
              <span className="shrink-0 text-xs text-slate-400">{filtrados.length}</span>
            )}
          </div>

          <ul className="max-h-72 overflow-y-auto py-1">
            {filtrados.length === 0 && (
              <li className="px-3 py-4 text-center text-sm text-slate-500">
                Nenhum recurso com “{termo}”.
              </li>
            )}
            {filtrados.map((r) => (
              <li key={r.valor}>
                <button
                  type="button"
                  onClick={() => escolher(r)}
                  className="flex w-full items-start gap-2 px-3 py-1.5 text-left text-sm hover:bg-slate-50"
                >
                  {r.valor === valor ? (
                    <Check className="mt-0.5 size-4 shrink-0 text-red-700" />
                  ) : (
                    <span className="w-4 shrink-0" />
                  )}
                  <span className="min-w-0">
                    {r.rotulo}
                    {/* Recurso sem campos copiados renderiza formulário incompleto — melhor
                        avisar aqui do que deixar o operador descobrir preenchendo. */}
                    {!r.camposLidos && (
                      <span className="ml-2 rounded bg-amber-100 px-1.5 py-0.5 text-[11px] text-amber-800">
                        campos não copiados
                      </span>
                    )}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

/** Sem acento e em minúsculas: ninguém digita "Ambulatório" com acento numa busca. */
function normalizar(s: string): string {
  return s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim();
}
