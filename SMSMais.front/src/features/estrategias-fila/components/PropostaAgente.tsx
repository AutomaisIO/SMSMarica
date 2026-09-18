import { Bot, TriangleAlert } from 'lucide-react';
import { n, usd } from '@/features/estrategias-fila/lib/parametros';
import type { PropostaAgente as Proposta, Rodada } from '@/features/estrategias-fila/types';

const TIPO: Record<string, string> = {
  escala: 'Escala',
  profissional: 'Profissional',
  mutirao: 'Mutirão',
  dia: 'Dia',
  horario: 'Horário',
  unidade: 'Unidade',
  aproveitamento: 'Aproveitamento',
  outro: 'Outro',
};

type Props = { rodada: Rodada; mudancas: string[] };

/**
 * O que o agente propôs — resumo, ações concretas e riscos — com o custo ao lado. As ações são a
 * parte que vai para o mundo real (abrir escala no SISREG, habilitar profissional); o sistema não
 * executa nenhuma delas.
 */
export function PropostaAgente({ rodada, mudancas }: Props) {
  const p: Proposta | null = rodada.proposta;

  return (
    <section className="rounded-xl border border-violet-200 bg-violet-50/40 p-4 shadow-sm">
      <div className="mb-2 flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <Bot className="h-4 w-4 text-violet-600" /> Proposta do agente — rodada {rodada.numero}
        </h2>
        <p className="text-[11px] text-gray-500">
          {rodada.modelo ?? '—'} · {n(rodada.tokensEntrada)} + {n(rodada.tokensSaida)} tokens · {usd(rodada.custoUsd)} ·{' '}
          {(rodada.duracaoMs / 1000).toFixed(0)} s{p ? ` · ${p.simulacoes} simulação(ões)` : ''}
        </p>
      </div>

      {rodada.falha ? (
        <p className="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-800">
          <TriangleAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          {rodada.falha}
        </p>
      ) : null}

      {p ? (
        <>
          <p className="whitespace-pre-line text-sm text-gray-800">{p.resumo}</p>

          {mudancas.length > 0 ? (
            <div className="mt-2 flex flex-wrap gap-1">
              {mudancas.map((m) => (
                <span key={m} className="rounded-full bg-white px-2 py-0.5 text-[11px] text-violet-800 ring-1 ring-violet-200">
                  {m}
                </span>
              ))}
            </div>
          ) : null}

          <h3 className="mt-3 text-xs font-semibold text-gray-900">O que fazer no mundo real</h3>
          <ol className="mt-1 space-y-1">
            {p.acoes.map((a, i) => (
              <li key={i} className="flex gap-2 rounded-md bg-white px-3 py-2 text-xs ring-1 ring-gray-100">
                <span className="mt-0.5 shrink-0 rounded bg-violet-100 px-1.5 py-0.5 text-[10px] font-medium text-violet-800">
                  {TIPO[a.tipo] ?? a.tipo}
                </span>
                <span className="flex-1 text-gray-800">
                  {a.descricao}
                  {a.unidade ? <span className="text-gray-500"> — {a.unidade}</span> : null}
                </span>
                {a.impactoVagasSemana !== null ? (
                  <span className="shrink-0 text-[11px] text-gray-500" title="Vagas por semana que a ação acrescenta">
                    +{n(a.impactoVagasSemana, 1)}/sem
                  </span>
                ) : null}
              </li>
            ))}
          </ol>

          {p.riscos.length > 0 ? (
            <>
              <h3 className="mt-3 text-xs font-semibold text-gray-900">Riscos</h3>
              <ul className="mt-1 list-disc space-y-0.5 pl-5 text-xs text-gray-700">
                {p.riscos.map((r, i) => (
                  <li key={i}>{r}</li>
                ))}
              </ul>
            </>
          ) : null}

          <p className="mt-2 text-[11px] text-gray-500">
            Confiança declarada pelo agente: <strong>{Math.round(p.confianca * 100)}%</strong>. Os números da projeção são do
            nosso simulador — o agente só escolheu os parâmetros livres.
          </p>
        </>
      ) : null}
    </section>
  );
}
