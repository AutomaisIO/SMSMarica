import { Info } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { agregar, formatarValor, mesAbrev, pareceNumero, rotuloAno, rotuloLimpo } from '../lib/indicadores';
import type { SerieIndicador, TabelaIndicador } from '../types';
import { SeloOrigem } from './SeloOrigem';

/**
 * Série mensal de um ano do período: Indicador | Origem | meses | No período.
 *
 * "No período" fecha o ano do jeito certo para cada série (ver `agregar`) e diz qual foi: total,
 * último mês, média ou "no ano" (o valor do ano que o backend calculou sobre todos os casos).
 */
export function TabelaSerieAno({
  series,
  ano,
  meses,
}: {
  series: SerieIndicador[];
  ano: string;
  meses: string[];
}) {
  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200">
      <table className="w-full min-w-max border-collapse text-xs">
        <thead>
          <tr className="border-b border-gray-300 bg-gray-50 text-gray-600">
            <th className="sticky left-0 z-[1] min-w-[14rem] bg-gray-50 px-3 py-2 text-left font-semibold text-gray-900">
              {rotuloAno(ano, meses)}
            </th>
            <th className="px-2 py-2 text-center font-semibold">Origem</th>
            {meses.map((m) => (
              <th key={m} className="px-2 py-2 text-right font-semibold">
                {mesAbrev(m)}
              </th>
            ))}
            <th className="bg-gray-100 px-3 py-2 text-right font-semibold text-gray-900">No período</th>
          </tr>
        </thead>
        <tbody>
          {series.map((s) => {
            const tot = agregar(s, meses, ano);
            return (
              <tr
                key={s.rotulo}
                className={cn('border-b border-gray-100 last:border-b-0', s.destaque ? 'font-semibold text-gray-900' : 'text-gray-700')}
              >
                <td
                  className={cn(
                    'sticky left-0 z-[1] max-w-[22rem] bg-white py-1.5 pr-3 text-left',
                    s.subitem ? 'pl-7 font-normal text-gray-500' : 'pl-3',
                  )}
                >
                  <span className="inline-flex items-start gap-1">
                    <span className="whitespace-normal">{rotuloLimpo(s.rotulo)}</span>
                    {s.nota ? (
                      <span title={s.nota} className="mt-0.5 shrink-0 cursor-help text-gray-400">
                        <Info className="h-3 w-3" aria-label={s.nota} />
                      </span>
                    ) : null}
                  </span>
                </td>
                <td className="px-2 py-1.5 text-center">
                  <SeloOrigem selo={s.selo} />
                </td>
                {meses.map((m) => (
                  <td key={m} className="whitespace-nowrap px-2 py-1.5 text-right tabular-nums">
                    {formatarValor(s.valores[m], s.formato)}
                  </td>
                ))}
                <td className="whitespace-nowrap bg-gray-50 px-3 py-1.5 text-right tabular-nums font-semibold text-gray-900">
                  {formatarValor(tot.valor, s.formato)}
                  {tot.tipo ? <span className="ml-1 text-[10px] font-normal text-gray-500">{tot.tipo}</span> : null}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

/** Categorias genéricas vão para o fim — o topo da tabela é para motivo que diz alguma coisa. */
const MOTIVOS_NO_FIM = ['Outros', 'Sem motivo informado'];

export function ordenarMotivos(t: TabelaIndicador): TabelaIndicador {
  const fim = (l: (string | null)[]) => MOTIVOS_NO_FIM.includes((l[0] ?? '').trim());
  return { ...t, linhas: [...t.linhas.filter((l) => !fim(l)), ...t.linhas.filter(fim)] };
}

/**
 * Tabela pronta do backend (células já formatadas). Número à direita, texto à esquerda; nulo vira
 * "—". Tabela comprida ganha altura máxima com cabeçalho fixo, para não empurrar a página.
 */
export function TabelaSimples({ tabela }: { tabela: TabelaIndicador }) {
  if (tabela.linhas.length === 0) return null;
  const colunaNumerica = tabela.colunas.map(
    (_, i) => i > 0 && tabela.linhas.every((l) => pareceNumero(l[i] ?? null)),
  );
  const comprida = tabela.linhas.length > 16;
  return (
    <div className="min-w-0">
      <h4 className="mb-1.5 text-sm font-semibold text-gray-900">{tabela.titulo}</h4>
      <div
        className={cn('overflow-x-auto rounded-lg border border-gray-200', comprida && 'max-h-[30rem] overflow-y-auto')}
      >
        <table className="w-full border-collapse text-xs">
          <thead className={cn(comprida && 'sticky top-0 z-[1]')}>
            <tr className="border-b border-gray-300 bg-gray-50 text-gray-600">
              {tabela.colunas.map((c, i) => (
                <th
                  key={i}
                  className={cn('whitespace-nowrap px-3 py-2 font-semibold', colunaNumerica[i] ? 'text-right' : 'text-left')}
                >
                  {c}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {tabela.linhas.map((l, n) => (
              <tr key={n} className="border-b border-gray-100 text-gray-700 last:border-b-0">
                {l.map((v, i) => {
                  const numerica = i > 0 && pareceNumero(v);
                  return (
                    <td
                      key={i}
                      className={cn(
                        'px-3 py-1.5',
                        numerica ? 'whitespace-nowrap text-right tabular-nums' : 'text-left',
                        i === 0 && 'text-gray-900',
                      )}
                    >
                      {v ?? '—'}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {tabela.nota ? <p className="mt-1.5 text-[11px] leading-snug text-gray-500">{tabela.nota}</p> : null}
    </div>
  );
}
