import { useEffect, useMemo, useRef, useState } from 'react';
import { AlertTriangle, Building2, Check, Globe2, Loader2, Search, X } from 'lucide-react';

import { Input } from '@/shared/ui/Input';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { cn } from '@/shared/lib/cn';

import { TAMANHO_MINIMO_BUSCA, useBuscaProcedimentos } from '../api/queries';
import type { RegulacaoProcedimentoItem, TipoProcedimentoRegulacao } from '../types';

type Props = {
  value: RegulacaoProcedimentoItem | null;
  onChange: (item: RegulacaoProcedimentoItem | null) => void;
  tipo?: TipoProcedimentoRegulacao;
  autoFocus?: boolean;
};

const ROTULO_SISTEMA: Record<string, string> = {
  Sisreg: 'SISREG',
  Ser: 'SER',
  Sernit: 'SERNIT',
  Esus: 'eSUS',
};

/**
 * Busca do procedimento por texto livre — o primeiro passo do wizard de solicitação.
 *
 * <p>Mostra, em cada resultado, **onde o procedimento é executado**: as unidades de Maricá com
 * vaga no SISREG (o lado Interno) e em quais sistemas externos ele existe. É essa informação
 * que decide o destino no passo seguinte, e trazê-la aqui evita o vaivém de escolher primeiro e
 * descobrir depois que não há oferta.</p>
 *
 * <p>O catálogo é plano (D-10): buscar "endocrinologia" devolve tanto o balde do SERNIT quanto
 * as subespecialidades do SER, e está certo — quem desempata é o agente regulador na triagem.</p>
 */
export function BuscaProcedimento({ value, onChange, tipo, autoFocus }: Props) {
  const [termo, setTermo] = useState('');
  const [destacado, setDestacado] = useState(0);
  const debounced = useDebounce(termo, 300);
  const busca = useBuscaProcedimentos(debounced, tipo);

  const itens = useMemo(() => busca.data?.itens ?? [], [busca.data]);
  const listaRef = useRef<HTMLUListElement>(null);

  const termoValido = debounced.trim().length >= TAMANHO_MINIMO_BUSCA;
  // "Ainda buscando" cobre também a janela do debounce, senão a tela parece parada.
  const buscando = termoValido && (termo.trim() !== debounced.trim() || busca.isFetching);

  useEffect(() => setDestacado(0), [debounced, tipo]);

  if (value) {
    return (
      <div className="flex items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50/60 p-3">
        <div className="min-w-0">
          <p className="truncate font-medium text-slate-900">{value.nome}</p>
          <p className="mt-0.5 text-xs text-slate-600">{value.tipo}</p>
          <LinhaOferta item={value} />
        </div>
        <button
          type="button"
          onClick={() => onChange(null)}
          className="shrink-0 rounded p-1 text-slate-500 hover:bg-red-100 hover:text-slate-800"
          aria-label="Trocar procedimento"
        >
          <X className="size-4" />
        </button>
      </div>
    );
  }

  function aoTeclar(e: React.KeyboardEvent<HTMLInputElement>) {
    if (itens.length === 0) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setDestacado((i) => Math.min(i + 1, itens.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setDestacado((i) => Math.max(i - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const escolhido = itens[destacado];
      if (escolhido) onChange(escolhido);
    }
  }

  return (
    <div className="relative">
      <div className="relative">
        <Search className="pointer-events-none absolute left-3 top-2.5 size-4 text-gray-400" />
        <Input
          value={termo}
          onChange={(e) => setTermo(e.target.value)}
          onKeyDown={aoTeclar}
          placeholder={`Procedimento, especialidade ou exame — mín. ${TAMANHO_MINIMO_BUSCA} caracteres`}
          className="pl-9 pr-9"
          autoFocus={autoFocus}
        />
        {buscando ? (
          <Loader2 className="pointer-events-none absolute right-3 top-2.5 size-4 animate-spin text-gray-400" />
        ) : null}
      </div>

      {busca.data?.degradada ? (
        <p className="mt-2 flex items-start gap-1.5 text-xs text-amber-700">
          <AlertTriangle className="mt-0.5 size-3.5 shrink-0" />
          <span>
            Busca por semelhança indisponível no momento — só o casamento por texto respondeu.
            Se não achar o que procura, tente outras palavras do nome.
          </span>
        </p>
      ) : null}

      {termoValido && !busca.isLoading && itens.length === 0 ? (
        <p className="mt-2 text-sm text-slate-500">
          Nenhum procedimento encontrado para “{debounced.trim()}”.
        </p>
      ) : null}

      {itens.length > 0 ? (
        <ul
          ref={listaRef}
          className="mt-2 max-h-96 divide-y divide-slate-100 overflow-auto rounded-md border border-slate-200 bg-white shadow"
        >
          {itens.map((item, i) => (
            <li key={item.id}>
              <button
                type="button"
                onClick={() => onChange(item)}
                onMouseEnter={() => setDestacado(i)}
                className={cn(
                  'flex w-full flex-col items-start gap-1 px-3 py-2 text-left',
                  i === destacado ? 'bg-red-50' : 'hover:bg-slate-50',
                )}
              >
                <div className="flex w-full items-center justify-between gap-2">
                  <span className="min-w-0 truncate font-medium text-slate-900">{item.nome}</span>
                  <span className="shrink-0 text-[11px] uppercase tracking-wide text-slate-400">
                    {item.tipo}
                  </span>
                </div>
                <div className="flex flex-wrap gap-1">
                  {[...new Set(item.origens.map((o) => o.sistema))].map((s) => (
                    <span
                      key={s}
                      className="rounded bg-slate-100 px-1.5 py-0.5 text-[11px] font-medium text-slate-600"
                    >
                      {ROTULO_SISTEMA[s] ?? s}
                    </span>
                  ))}
                </div>
                <LinhaOferta item={item} />
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

/** As duas linhas que respondem "onde isso é feito?" — Interno (com vagas) e Externo. */
function LinhaOferta({ item }: { item: RegulacaoProcedimentoItem }) {
  const { executantesInternos: internos, existeExterno: externo } = item;
  const temExterno = externo.ser || externo.sernit;

  return (
    <div className="mt-1 space-y-0.5 text-xs">
      <p className="flex items-start gap-1.5">
        <Building2 className="mt-0.5 size-3.5 shrink-0 text-slate-400" />
        {internos.length === 0 ? (
          <span className="text-slate-400">Interno: sem oferta no SISREG</span>
        ) : (
          <span className="text-slate-600">
            Interno:{' '}
            {internos.slice(0, 2).map((u, i) => (
              <span key={u.unidadeId}>
                {i > 0 ? '; ' : ''}
                {u.nome} ({u.vagasTotal} {u.vagasTotal === 1 ? 'vaga' : 'vagas'})
              </span>
            ))}
            {internos.length > 2 ? ` e mais ${internos.length - 2}` : ''}
          </span>
        )}
      </p>
      <p className="flex items-start gap-1.5">
        <Globe2 className="mt-0.5 size-3.5 shrink-0 text-slate-400" />
        {temExterno ? (
          <span className="text-slate-600">
            Externo:{' '}
            {[
              externo.ser ? (externo.serAmbulatorioEstadual ? 'SER (amb. estadual)' : 'SER') : null,
              externo.sernit ? 'SERNIT' : null,
            ]
              .filter(Boolean)
              .join(' · ')}
          </span>
        ) : (
          <span className="text-slate-400">Externo: não ofertado</span>
        )}
      </p>
      {item.score >= 1 ? (
        <p className="flex items-center gap-1 text-[11px] text-emerald-700">
          <Check className="size-3" /> casou pelo nome
        </p>
      ) : null}
    </div>
  );
}
