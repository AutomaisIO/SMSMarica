import { AlertTriangle, Check, HelpCircle, Loader2, Pencil, Play, X } from 'lucide-react';
import {
  formatarResultado,
  type IndicadorResumo,
  type SituacaoIndicador,
} from '@/features/indicadores/types';

type Props = {
  indicadores: IndicadorResumo[];
  onEditar: (id: string) => void;
  onApurar: (id: string) => void;
  onVerRessalva: (indicador: IndicadorResumo) => void;
  apurandoId: string | null;
  podeEditar: boolean;
};

const SITUACOES: Record<SituacaoIndicador, { rotulo: string; classe: string }> = {
  Validado: { rotulo: 'Validado', classe: 'bg-emerald-50 text-emerald-700' },
  NaoValidado: { rotulo: 'Não validado', classe: 'bg-amber-50 text-amber-700' },
  SemMotor: { rotulo: 'Sem motor', classe: 'bg-slate-100 text-slate-500' },
  ForaDoBanco: { rotulo: 'Fora do banco', classe: 'bg-rose-50 text-rose-700' },
};

function numero(v: number | null | undefined): string {
  return v === null || v === undefined ? '—' : v.toLocaleString('pt-BR', { maximumFractionDigits: 2 });
}

/**
 * Espelha a planilha contratual: Numerador | Denominador | Resultado | Pontuação por período.
 * As linhas agrupadoras (item 3, item 10) somam os filhos, como o `=SUM()` da planilha.
 */
export function TabelaIndicadores({
  indicadores,
  onEditar,
  onApurar,
  onVerRessalva,
  apurandoId,
  podeEditar,
}: Props) {
  const filhosPorPai = new Map<string, IndicadorResumo[]>();
  for (const i of indicadores) {
    if (!i.indicadorPaiId) continue;
    const atual = filhosPorPai.get(i.indicadorPaiId) ?? [];
    atual.push(i);
    filhosPorPai.set(i.indicadorPaiId, atual);
  }

  function pesoDe(i: IndicadorResumo): number {
    const filhos = filhosPorPai.get(i.id)?.filter((f) => f.ativo);
    if (filhos?.length) return filhos.reduce((s, f) => s + (f.pontuacao ?? 0), 0);
    return i.pontuacao ?? 0;
  }

  function pontosDe(i: IndicadorResumo): number | null {
    const filhos = filhosPorPai.get(i.id)?.filter((f) => f.ativo);
    if (filhos?.length) {
      const apurados = filhos.filter((f) => f.resultado?.pontuacaoApurada != null);
      if (apurados.length === 0) return null;
      return apurados.reduce((s, f) => s + (f.resultado?.pontuacaoApurada ?? 0), 0);
    }
    return i.resultado?.pontuacaoApurada ?? null;
  }

  // Totais da aba: só as linhas de topo, para não contar o agrupador e os filhos duas vezes.
  // Indicadores desabilitados não entram em peso nem pontuação.
  const topo = indicadores.filter((i) => !i.indicadorPaiId && i.ativo);
  const pesoTotal = topo.reduce((s, i) => s + pesoDe(i), 0);
  const pontosTotal = topo.reduce((s, i) => s + (pontosDe(i) ?? 0), 0);
  const percentual = pesoTotal > 0 ? (pontosTotal / pesoTotal) * 100 : 0;

  return (
    <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
      <table className="w-full min-w-[1080px] text-sm">
        <thead>
          <tr className="border-b border-slate-200 text-[11px] uppercase tracking-wide text-slate-400">
            <th className="w-14 px-3 py-2.5 text-left font-medium">Nº</th>
            <th className="px-3 py-2.5 text-left font-medium">Indicador</th>
            <th className="w-32 px-3 py-2.5 text-left font-medium">Meta</th>
            <th className="w-16 px-3 py-2.5 text-right font-medium">Peso</th>
            <th className="w-28 px-3 py-2.5 text-right font-medium">Numerador</th>
            <th className="w-28 px-3 py-2.5 text-right font-medium">Denominador</th>
            <th className="w-28 px-3 py-2.5 text-right font-medium">Resultado</th>
            <th className="w-24 px-3 py-2.5 text-right font-medium">Pontuação</th>
            <th className="w-24 px-3 py-2.5" />
          </tr>
        </thead>
        <tbody>
          {indicadores.map((i) => {
            // Desabilitado: só o texto do indicador, esmaecido — sem badge, número, ressalva ou meta.
            // Mantém o lápis para poder reabilitar.
            if (!i.ativo) {
              return (
                <tr key={i.id} className="border-b border-slate-100 opacity-50 last:border-0">
                  <td className="px-3 py-2.5 font-mono text-xs text-slate-300">{i.numero}</td>
                  <td className={`px-3 py-2.5 ${i.indicadorPaiId ? 'pl-8' : ''}`} colSpan={7}>
                    <span className="italic text-slate-400">{i.nome}</span>
                  </td>
                  <td className="px-3 py-2.5">
                    <div className="flex items-center justify-end">
                      <button
                        type="button"
                        onClick={() => onEditar(i.id)}
                        title={podeEditar ? 'Editar indicador' : 'Ver indicador'}
                        className="rounded p-1.5 text-slate-300 transition hover:bg-slate-100 hover:text-slate-600"
                      >
                        <Pencil className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  </td>
                </tr>
              );
            }

            const r = i.resultado;
            const agrupador = i.tipoResultado === 'Agrupador' || !!filhosPorPai.get(i.id)?.length;
            const pontos = pontosDe(i);
            const situacao = SITUACOES[i.situacao];
            const apurando = apurandoId === i.id;

            return (
              <tr
                key={i.id}
                className={`border-b border-slate-100 last:border-0 hover:bg-slate-50/60 ${
                  agrupador ? 'bg-slate-50/40 font-medium' : ''
                }`}
              >
                <td className="px-3 py-2.5 font-mono text-xs text-slate-400">{i.numero}</td>

                <td className={`px-3 py-2.5 ${i.indicadorPaiId ? 'pl-8' : ''}`}>
                  <div className="flex items-center gap-2">
                    <span className="text-slate-800">{i.nome}</span>
                    {!agrupador && (
                      <span
                        className={`rounded px-1.5 py-0.5 text-[10px] font-medium ${situacao.classe}`}
                      >
                        {situacao.rotulo}
                      </span>
                    )}
                    {i.ressalva && (
                      <button
                        type="button"
                        title="Ver ressalva"
                        aria-label="Ver ressalva"
                        onClick={() => onVerRessalva(i)}
                        className="inline-flex shrink-0 items-center rounded p-0.5 text-amber-600 transition hover:bg-amber-50 hover:text-amber-700"
                      >
                        <HelpCircle className="h-4 w-4" />
                      </button>
                    )}
                  </div>
                  {r?.erro && (
                    <p className="mt-1 flex items-start gap-1 text-[11px] leading-snug text-rose-500">
                      <AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" />
                      {r.erro}
                    </p>
                  )}
                </td>

                <td className="px-3 py-2.5 text-xs text-slate-500">{i.meta ?? '—'}</td>
                <td className="px-3 py-2.5 text-right tabular-nums text-slate-500">
                  {pesoDe(i) || '—'}
                </td>

                {i.tipoResultado === 'Distribuicao' ? (
                  <td colSpan={3} className="px-3 py-2.5 text-xs text-slate-500">
                    {r?.distribuicao
                      ? `${r.distribuicao.length} linhas · ${numero(r.numerador)} atendimentos`
                      : '—'}
                  </td>
                ) : (
                  <>
                    <td className="px-3 py-2.5 text-right tabular-nums text-slate-600">
                      {agrupador ? '' : numero(r?.numerador)}
                    </td>
                    <td className="px-3 py-2.5 text-right tabular-nums text-slate-600">
                      {agrupador ? '' : numero(r?.denominador)}
                    </td>
                    <td className="px-3 py-2.5 text-right tabular-nums font-medium text-slate-900">
                      {agrupador ? '' : formatarResultado(r?.valor, i.unidadeMedida)}
                    </td>
                  </>
                )}

                <td className="px-3 py-2.5 text-right">
                  <div className="flex items-center justify-end gap-1.5 tabular-nums">
                    {r?.atingiuMeta === true && <Check className="h-3.5 w-3.5 text-emerald-600" />}
                    {r?.atingiuMeta === false && <X className="h-3.5 w-3.5 text-rose-500" />}
                    <span
                      className={
                        pontos === null
                          ? 'text-slate-300'
                          : pontos > 0
                            ? 'font-medium text-emerald-700'
                            : 'text-rose-600'
                      }
                    >
                      {pontos === null ? '—' : pontos.toLocaleString('pt-BR')}
                    </span>
                  </div>
                </td>

                <td className="px-3 py-2.5">
                  <div className="flex items-center justify-end gap-0.5">
                    {i.temMotor && (
                      <button
                        type="button"
                        onClick={() => onApurar(i.id)}
                        disabled={apurando}
                        title="Apurar este indicador"
                        className="rounded p-1.5 text-slate-400 transition hover:bg-slate-100 hover:text-slate-700 disabled:opacity-50"
                      >
                        {apurando ? (
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <Play className="h-3.5 w-3.5" />
                        )}
                      </button>
                    )}
                    <button
                      type="button"
                      onClick={() => onEditar(i.id)}
                      title={podeEditar ? 'Editar indicador' : 'Ver indicador'}
                      className="rounded p-1.5 text-slate-400 transition hover:bg-slate-100 hover:text-slate-700"
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </button>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>

        <tfoot>
          <tr className="border-t-2 border-slate-200 bg-slate-50 text-sm font-medium">
            <td />
            <td className="px-3 py-3 text-slate-700">Total de pontos</td>
            <td />
            <td className="px-3 py-3 text-right tabular-nums text-slate-600">
              {pesoTotal.toLocaleString('pt-BR')}
            </td>
            <td colSpan={3} />
            <td className="px-3 py-3 text-right tabular-nums text-slate-900">
              {pontosTotal.toLocaleString('pt-BR')}
            </td>
            <td />
          </tr>
          <tr className="bg-slate-50 text-xs">
            <td />
            <td className="px-3 pb-3 text-slate-500">Percentual alcançado da pontuação</td>
            <td colSpan={5} />
            <td className="px-3 pb-3 text-right tabular-nums font-medium text-slate-700">
              {percentual.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%
            </td>
            <td />
          </tr>
        </tfoot>
      </table>
    </div>
  );
}
