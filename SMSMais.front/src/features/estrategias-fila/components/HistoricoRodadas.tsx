import { Bot, User } from 'lucide-react';
import { dataHoraBr, n, usd } from '@/features/estrategias-fila/lib/parametros';
import type { RodadaResumo } from '@/features/estrategias-fila/types';

type Props = {
  rodadas: RodadaResumo[];
  atualId: string | null;
  selecionada: number | null;
  aoSelecionar: (numero: number) => void;
};

/**
 * Todas as rodadas da estratégia, append-only. Clicar abre a rodada (parâmetros, projeção e
 * proposta daquela vez) sem mudar a atual — é para comparar, não para voltar no tempo.
 */
export function HistoricoRodadas({ rodadas, atualId, selecionada, aoSelecionar }: Props) {
  if (rodadas.length === 0) return null;

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <h2 className="mb-2 text-sm font-semibold text-gray-900">Rodadas</h2>
      <table className="w-full text-xs">
        <thead>
          <tr className="text-left text-[10px] uppercase tracking-wide text-gray-500">
            <th className="py-1 pr-2">#</th>
            <th className="py-1 pr-2">Modo</th>
            <th className="py-1 pr-2">Resultado</th>
            <th className="py-1 pr-2 text-right">Capacidade/sem</th>
            <th className="py-1 pr-2 text-right">Custo</th>
            <th className="py-1">Quando</th>
          </tr>
        </thead>
        <tbody>
          {[...rodadas].reverse().map((r) => {
            const atual = r.id === atualId;
            const sel = r.numero === selecionada;
            return (
              <tr
                key={r.id}
                onClick={() => aoSelecionar(r.numero)}
                className={`cursor-pointer border-t border-gray-100 hover:bg-gray-50 ${sel ? 'bg-red-50/60' : ''}`}
                title="Abrir esta rodada"
              >
                <td className="py-1.5 pr-2 font-medium">
                  {r.numero}
                  {atual ? <span className="ml-1 rounded bg-red-100 px-1 text-[9px] text-red-700">atual</span> : null}
                </td>
                <td className="py-1.5 pr-2">
                  <span className="inline-flex items-center gap-1">
                    {r.modo === 'Agente' ? <Bot className="h-3 w-3 text-violet-600" /> : <User className="h-3 w-3 text-gray-500" />}
                    {r.modo === 'Agente' ? 'Agente' : 'Manual'}
                  </span>
                </td>
                <td className="py-1.5 pr-2">
                  {r.falha ? (
                    <span className="text-red-700">falhou</span>
                  ) : r.zera ? (
                    <span className="text-emerald-700">zera na semana {r.semanaZera}</span>
                  ) : (
                    <span className="text-red-700">não zera</span>
                  )}
                </td>
                <td className="py-1.5 pr-2 text-right">{n(r.capacidadeSemanal, 1)}</td>
                <td className="py-1.5 pr-2 text-right text-gray-500">{r.modo === 'Agente' ? usd(r.custoUsd) : '—'}</td>
                <td className="py-1.5 text-gray-500">{dataHoraBr(r.criadoEm)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}
