import type { ReactNode } from 'react';
import { ArrowDownRight, ArrowUpRight, Loader2 } from 'lucide-react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { diaCurto, mesCurto, numero } from '@/shared/lib/estatisticasPeriodo';
import { horaCurta } from '../lib/rotulos';
import type { DiaSemanaExterno, HoraExterno, SerieDiaExterno, SerieMesExterno, TopItemExterno } from '../types';

/** Cartão de indicador. `variacao` em % contra o período anterior; `referencia` é um texto de comparação. */
export function Cartao({
  rotulo,
  valor,
  dica,
  variacao,
  referencia,
  classe = 'text-gray-900',
}: {
  rotulo: string;
  valor: ReactNode;
  dica: string;
  variacao?: number | null;
  referencia?: ReactNode;
  classe?: string;
}) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm" title={dica}>
      <p className="text-xs text-gray-500">{rotulo}</p>
      <p className={`mt-1 text-2xl font-semibold tabular-nums ${classe}`}>{valor}</p>
      {variacao !== undefined && variacao !== null ? (
        <p
          className={`mt-1 flex items-center gap-0.5 text-xs font-medium ${
            variacao >= 0 ? 'text-emerald-700' : 'text-red-700'
          }`}
        >
          {variacao >= 0 ? <ArrowUpRight className="h-3.5 w-3.5" /> : <ArrowDownRight className="h-3.5 w-3.5" />}
          {variacao >= 0 ? '+' : ''}
          {numero(variacao, 1)}% vs. período anterior
        </p>
      ) : null}
      {referencia ? <p className="mt-1 text-[11px] text-gray-500">{referencia}</p> : null}
      <p className="mt-1 text-[11px] leading-tight text-gray-400">{dica}</p>
    </div>
  );
}

export function Secao({ titulo, descricao, children }: { titulo: string; descricao?: string; children: ReactNode }) {
  return (
    <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <h2 className="text-sm font-semibold text-gray-900">{titulo}</h2>
      {descricao ? <p className="mb-3 text-xs text-gray-500">{descricao}</p> : <div className="mb-3" />}
      {children}
    </section>
  );
}

export function Carregando() {
  return <Loader2 className="mx-auto my-10 h-5 w-5 animate-spin text-gray-400" />;
}

/** Barras de ações por dia (agendamentos destacados) com a linha de pessoas ativas. */
export function GraficoPorDia({ dados, mostrarOperadores = true }: { dados: SerieDiaExterno[]; mostrarOperadores?: boolean }) {
  return (
    <ResponsiveContainer width="100%" height={260}>
      <ComposedChart
        data={dados.map((d) => ({ ...d, rotulo: diaCurto(d.dia), outras: d.acoes - d.agendamentos }))}
        margin={{ top: 4, right: 8, bottom: 8, left: 4 }}
      >
        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
        <XAxis dataKey="rotulo" tick={{ fontSize: 10 }} minTickGap={12} />
        <YAxis yAxisId="a" tick={{ fontSize: 11 }} />
        {mostrarOperadores ? <YAxis yAxisId="o" orientation="right" tick={{ fontSize: 11 }} allowDecimals={false} /> : null}
        <Tooltip contentStyle={{ fontSize: 12 }} />
        <Bar yAxisId="a" dataKey="agendamentos" name="Agendamentos" stackId="a" fill="#60a5fa" />
        <Bar yAxisId="a" dataKey="outras" name="Outras ações" stackId="a" fill="#c7d2fe" radius={[3, 3, 0, 0]} />
        {mostrarOperadores ? (
          <Line
            yAxisId="o"
            type="monotone"
            dataKey="operadores"
            name="Pessoas ativas"
            stroke="#b91c1c"
            strokeWidth={2}
            dot={false}
          />
        ) : null}
      </ComposedChart>
    </ResponsiveContainer>
  );
}

export function GraficoPorMes({ dados, mostrarOperadores = true }: { dados: SerieMesExterno[]; mostrarOperadores?: boolean }) {
  return (
    <ResponsiveContainer width="100%" height={260}>
      <ComposedChart
        data={dados.map((m) => ({ ...m, rotulo: mesCurto(m.mes), outras: m.acoes - m.agendamentos }))}
        margin={{ top: 4, right: 8, bottom: 8, left: 4 }}
      >
        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
        <XAxis dataKey="rotulo" tick={{ fontSize: 11 }} />
        <YAxis yAxisId="a" tick={{ fontSize: 11 }} />
        {mostrarOperadores ? <YAxis yAxisId="o" orientation="right" tick={{ fontSize: 11 }} allowDecimals={false} /> : null}
        <Tooltip contentStyle={{ fontSize: 12 }} />
        <Bar yAxisId="a" dataKey="agendamentos" name="Agendamentos" stackId="a" fill="#3b82f6" />
        <Bar yAxisId="a" dataKey="outras" name="Outras ações" stackId="a" fill="#a5b4fc" radius={[4, 4, 0, 0]} />
        {mostrarOperadores ? (
          <Line
            yAxisId="o"
            type="monotone"
            dataKey="operadores"
            name="Pessoas ativas"
            stroke="#b91c1c"
            strokeWidth={2}
            dot={{ r: 3 }}
          />
        ) : null}
      </ComposedChart>
    </ResponsiveContainer>
  );
}

export function GraficoDiaSemana({ dados }: { dados: DiaSemanaExterno[] }) {
  return (
    <ResponsiveContainer width="100%" height={220}>
      <BarChart data={dados} margin={{ top: 4, right: 8, bottom: 8, left: 4 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
        <XAxis dataKey="rotulo" tick={{ fontSize: 11 }} />
        <YAxis tick={{ fontSize: 11 }} />
        <Tooltip
          contentStyle={{ fontSize: 12 }}
          formatter={(v: number, nome: string) => [numero(v, nome === 'Média por dia' ? 1 : 0), nome]}
        />
        <Bar dataKey="mediaPorDiaComAtividade" name="Média por dia" fill="#34d399" radius={[4, 4, 0, 0]} />
        <Bar dataKey="acoes" name="Total" fill="#d1d5db" radius={[4, 4, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
  );
}

/** Distribuição pelas 24 horas do dia (relógio de Brasília) — o SER registra a hora, o SISREG não. */
export function GraficoPorHora({ dados }: { dados: HoraExterno[] }) {
  return (
    <ResponsiveContainer width="100%" height={220}>
      <BarChart
        data={dados.map((h) => ({ ...h, rotulo: horaCurta(h.hora), outras: h.acoes - h.agendamentos }))}
        margin={{ top: 4, right: 8, bottom: 8, left: 4 }}
      >
        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
        <XAxis dataKey="rotulo" tick={{ fontSize: 10 }} interval={1} />
        <YAxis tick={{ fontSize: 11 }} />
        <Tooltip contentStyle={{ fontSize: 12 }} />
        <Bar dataKey="agendamentos" name="Agendamentos" stackId="h" fill="#60a5fa" />
        <Bar dataKey="outras" name="Outras ações" stackId="h" fill="#c7d2fe" radius={[3, 3, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
  );
}

/** Lista "top" com barra proporcional. */
export function ListaTop({ itens, vazio = 'Nada no período.' }: { itens: TopItemExterno[]; vazio?: string }) {
  const max = Math.max(1, ...itens.map((i) => i.acoes));
  if (itens.length === 0) return <p className="py-4 text-center text-xs text-gray-500">{vazio}</p>;
  return (
    <ol className="space-y-1.5">
      {itens.map((i, n) => (
        <li key={i.rotulo} className="flex items-center gap-2 text-xs">
          <span className="w-5 shrink-0 text-right text-gray-400">{n + 1}</span>
          <div className="min-w-0 flex-1">
            <div className="flex items-baseline justify-between gap-2">
              <span className="truncate text-gray-800" title={i.rotulo}>
                {i.rotulo}
              </span>
              <span className="shrink-0 tabular-nums text-gray-600">{numero(i.acoes)}</span>
            </div>
            <div className="mt-0.5 h-1.5 overflow-hidden rounded-full bg-gray-100">
              <div className="h-full bg-primary-400" style={{ width: `${Math.round((i.acoes / max) * 100)}%` }} />
            </div>
          </div>
        </li>
      ))}
    </ol>
  );
}
