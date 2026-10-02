import { useEffect, useMemo, useRef, useState } from 'react';
import { Check, ChevronDown, Search, X } from 'lucide-react';

type Opcao = { valor: string; rotulo: string };

/** Sem acento e minúsculo — "joão" acha "JOAO". */
function normalizar(texto: string): string {
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

const PARTICULAS = new Set(['de', 'da', 'do', 'das', 'dos', 'e']);

function palavras(texto: string): string[] {
  return normalizar(texto)
    .split(/[^a-z0-9]+/)
    .filter((p) => p && !PARTICULAS.has(p));
}

/**
 * Cada palavra digitada tem de bater com alguma palavra do rótulo — pelo INÍCIO ("rod" acha
 * RODRIGUES) ou pela INICIAL ("andrade" acha o "A." de "LAURA BEATRIZ A. RODRIGUES"). A ordem não
 * importa e "de/da/dos" são ignorados. Ao menos uma palavra tem de bater com uma palavra INTEIRA
 * do rótulo — só iniciais trariam ruído ("laura" acharia "MAXIMILIANO L. ROSA").
 *
 * <p>Era busca por trecho contínuo, e o cadastro de médicos do SER é cheio de nome abreviado e
 * sobrenome a mais: quem digitava o nome completo não achava o médico, concluía que ele não existia
 * e o cadastrava de novo — no SER, onde não dá para editar nem apagar (01/10/2026).</p>
 */
/** A mesma busca do campo, para listas fora dele (ex.: a tela de Médicos de cada sistema). */
export function filtrarPorPalavras<T>(itens: T[], termo: string, rotulo: (item: T) => string): T[] {
  const t = normalizar(termo.trim());
  if (!t) return itens;
  const termoPalavras = palavras(termo);
  const porTrecho = itens.filter((i) => normalizar(rotulo(i)).includes(t));
  const jaTem = new Set(porTrecho);
  const porPalavra = termoPalavras.length
    ? itens.filter((i) => !jaTem.has(i) && casa(termoPalavras, palavras(rotulo(i))))
    : [];
  return [...porTrecho, ...porPalavra];
}

function casa(termo: string[], rotulo: string[]): boolean {
  const cadaUma = termo.every((t) =>
    rotulo.some((r) => r.startsWith(t) || (r.length === 1 && t.length > 1 && t[0] === r)),
  );
  const algumaInteira = termo.some((t) => rotulo.some((r) => r.length > 1 && r.startsWith(t)));
  return cadaUma && algumaInteira;
}

/**
 * Um `<select>` para listas longas (a de médicos do SER passa de 800 nomes): abre uma caixa
 * com busca por qualquer parte do rótulo, sem acento. Devolve o VALOR da opção, como o select.
 */
export function SelectComBusca({
  id,
  opcoes,
  valor,
  onChange,
  desabilitado,
  placeholder = 'Selecione…',
}: {
  id?: string;
  opcoes: Opcao[];
  valor: string;
  onChange: (valor: string) => void;
  desabilitado?: boolean;
  placeholder?: string;
}) {
  const [aberto, setAberto] = useState(false);
  const [termo, setTermo] = useState('');
  const caixa = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function fora(e: MouseEvent) {
      if (caixa.current && !caixa.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener('mousedown', fora);
    return () => document.removeEventListener('mousedown', fora);
  }, []);

  const filtradas = useMemo(() => {
    const t = normalizar(termo.trim());
    if (!t) return opcoes;
    const termoPalavras = palavras(termo);
    // O trecho contínuo continua valendo (e vem primeiro: é o acerto mais provável); a busca por
    // palavras acrescenta o nome abreviado ou com sobrenome a mais.
    const porTrecho = opcoes.filter((o) => normalizar(o.rotulo).includes(t));
    const jaTem = new Set(porTrecho.map((o) => o.valor));
    const porPalavra = termoPalavras.length
      ? opcoes.filter((o) => !jaTem.has(o.valor) && casa(termoPalavras, palavras(o.rotulo)))
      : [];
    return [...porTrecho, ...porPalavra];
  }, [opcoes, termo]);

  const escolhida = opcoes.find((o) => o.valor === valor);

  return (
    <div ref={caixa} className="relative">
      <button
        id={id}
        type="button"
        disabled={desabilitado}
        onClick={() => setAberto((a) => !a)}
        className="flex w-full items-center justify-between gap-2 rounded-md border border-slate-300 bg-white px-3 py-2 text-left text-sm disabled:bg-slate-100 disabled:text-slate-400"
      >
        <span className={escolhida ? 'truncate text-slate-900' : 'text-slate-400'}>
          {escolhida?.rotulo ?? (valor ? valor : placeholder)}
        </span>
        <span className="flex shrink-0 items-center gap-1">
          {valor && !desabilitado && (
            <X
              className="size-4 text-slate-400 hover:text-slate-700"
              onClick={(e) => {
                e.stopPropagation();
                onChange('');
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
              placeholder={`Procure entre ${opcoes.length}…`}
              className="w-full text-sm outline-none"
            />
          </div>
          <ul className="max-h-72 overflow-y-auto py-1">
            {filtradas.length === 0 && (
              <li className="px-3 py-4 text-center text-sm text-slate-500">Nada com “{termo.trim()}”.</li>
            )}
            {filtradas.map((o) => (
              <li key={o.valor}>
                <button
                  type="button"
                  onClick={() => {
                    onChange(o.valor);
                    setAberto(false);
                    setTermo('');
                  }}
                  className="flex w-full items-start gap-2 px-3 py-1.5 text-left text-sm hover:bg-slate-50"
                >
                  {o.valor === valor ? (
                    <Check className="mt-0.5 size-4 shrink-0 text-primary-700" />
                  ) : (
                    <span className="w-4 shrink-0" />
                  )}
                  <span className="min-w-0">{o.rotulo}</span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
