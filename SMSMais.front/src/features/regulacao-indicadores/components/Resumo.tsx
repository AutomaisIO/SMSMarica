import { agregar, formatarValor, itensResumo, mesesPorAno, rotuloAno } from '../lib/indicadores';
import type { IndicadoresRegulacao } from '../types';
import { SeloOrigem } from './SeloOrigem';

/**
 * Os seis números do topo, um valor por ano do período — a mesma escolha do relatório em PDF.
 * Contagem soma; percentual é razão do ano; tempo é mediana de todos os casos; fila é a posição
 * no último mês do bloco.
 */
export function Resumo({ dados }: { dados: IndicadoresRegulacao }) {
  const anos = mesesPorAno(dados.meses);
  const itens = itensResumo(dados);
  if (itens.length === 0) return null;
  return (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      {itens.map((item) => (
        <div key={item.rotulo} className="flex flex-col rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
          <p className="text-xs text-gray-500">{item.rotulo}</p>
          <div className="mt-1 flex flex-wrap gap-x-6 gap-y-2">
            {anos.map((a) => {
              const v = item.serie && !item.indisponivel ? agregar(item.serie, a.meses, a.ano).valor : null;
              return (
                <div key={a.ano}>
                  <p className="text-2xl font-semibold tabular-nums text-gray-900">
                    {formatarValor(v, item.serie?.formato ?? 'Inteiro')}
                  </p>
                  <p className="text-[11px] text-gray-500">{rotuloAno(a.ano, a.meses)}</p>
                </div>
              );
            })}
          </div>
          <div className="mt-auto pt-2">
            <SeloOrigem selo={item.indisponivel ? 'Indisponivel' : (item.serie?.selo ?? 'Calculado')} />
          </div>
        </div>
      ))}
    </div>
  );
}
