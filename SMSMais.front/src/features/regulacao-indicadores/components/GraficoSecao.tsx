import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { numero } from '@/shared/lib/estatisticasPeriodo';
import { formatarValor, mesAno, rotuloLimpo } from '../lib/indicadores';
import type { Formato, SecaoIndicador, SerieIndicador } from '../types';

/**
 * Cores das séries, nesta ordem e sempre nesta ordem (validadas para daltonismo). O vermelho da
 * marca fica só no cromo da tela — nunca em dado, para não ser lido como alerta.
 */
const CORES = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100'];
const GRADE = '#eceef1';
const EIXO = '#6b7280';

type Ponto = { mes: string; rotulo: string } & Record<string, number | string | null>;

type ItemTooltip = { dataKey?: string | number; value?: number | string | null; color?: string; name?: string };

function Dica({
  active,
  payload,
  label,
  formatos,
}: {
  active?: boolean;
  payload?: ItemTooltip[];
  label?: string;
  formatos: Record<string, Formato>;
}) {
  if (!active || !payload?.length) return null;
  return (
    <div className="rounded-md border border-gray-200 bg-white px-3 py-2 text-xs shadow-md">
      <p className="mb-1 font-semibold text-gray-900">{label}</p>
      <ul className="space-y-0.5">
        {payload.map((p) => (
          <li key={String(p.dataKey)} className="flex items-center gap-2 text-gray-700">
            <span aria-hidden className="h-2.5 w-2.5 shrink-0 rounded-sm" style={{ background: p.color }} />
            <span className="min-w-0 flex-1">{p.name}</span>
            <span className="tabular-nums font-medium text-gray-900">
              {formatarValor(typeof p.value === 'number' ? p.value : null, formatos[String(p.dataKey)] ?? 'Inteiro')}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Legenda em HTML (tinta de texto, cor só no quadradinho) — só existe com duas séries ou mais. */
function Legenda({ series }: { series: SerieIndicador[] }) {
  if (series.length < 2) return null;
  return (
    <div className="mb-2 flex flex-wrap gap-x-5 gap-y-1 text-xs text-gray-700">
      {series.map((s, i) => (
        <span key={s.rotulo} className="inline-flex items-center gap-1.5">
          <span aria-hidden className="h-2.5 w-2.5 rounded-sm" style={{ background: CORES[i % CORES.length] }} />
          {rotuloLimpo(s.rotulo)}
        </span>
      ))}
    </div>
  );
}

/**
 * Gráfico mensal de uma seção: as séries que o backend escolheu em `grafico.series`. Barras
 * agrupadas (uma série = uma cor; duas = lado a lado) ou empilhadas. Sem dado nenhum, não desenha.
 */
export function GraficoSecao({ secao, meses }: { secao: SecaoIndicador; meses: string[] }) {
  const g = secao.grafico;
  if (!g || g.series.length === 0) return null;

  const alvo = g.series.map(rotuloLimpo);
  const series = alvo
    .map((r) => secao.series.find((s) => rotuloLimpo(s.rotulo) === r))
    .filter((s): s is SerieIndicador => Boolean(s))
    .filter((s) => meses.some((m) => typeof s.valores[m] === 'number' && s.valores[m] !== 0));
  if (series.length === 0) return null;

  const empilhado = g.tipo === 'empilhado';
  const formatos: Record<string, Formato> = {};
  series.forEach((s, i) => {
    formatos[`s${i}`] = s.formato;
  });
  const percentual = series.every((s) => s.formato === 'Percentual');

  const dados: Ponto[] = meses.map((m) => {
    const p: Ponto = { mes: m, rotulo: mesAno(m) };
    series.forEach((s, i) => {
      p[`s${i}`] = s.valores[m] ?? null;
    });
    return p;
  });

  return (
    <figure className="min-w-0">
      <Legenda series={series} />
      <div className="h-56 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart
            data={dados}
            margin={{ top: 6, right: 4, bottom: 0, left: 0 }}
            barGap={2}
            barCategoryGap={series.length > 1 && !empilhado ? '18%' : '28%'}
          >
            <CartesianGrid vertical={false} stroke={GRADE} />
            <XAxis
              dataKey="rotulo"
              tick={{ fontSize: 11, fill: EIXO }}
              tickLine={false}
              axisLine={{ stroke: '#c9cdd2' }}
              interval="preserveStartEnd"
              minTickGap={6}
            />
            <YAxis
              tick={{ fontSize: 11, fill: EIXO }}
              tickLine={false}
              axisLine={false}
              width={52}
              allowDecimals={!series.every((s) => s.formato === 'Inteiro')}
              tickFormatter={(v: number) => (percentual ? `${numero(v)}%` : numero(v))}
            />
            <Tooltip cursor={{ fill: 'rgba(17, 24, 39, 0.04)' }} content={<Dica formatos={formatos} />} />
            {series.map((s, i) => (
              <Bar
                key={s.rotulo}
                dataKey={`s${i}`}
                name={rotuloLimpo(s.rotulo)}
                fill={CORES[i % CORES.length]}
                maxBarSize={24}
                stackId={empilhado ? 'pilha' : undefined}
                // Empilhado: só o topo arredonda, e a borda branca abre o respiro entre os segmentos.
                radius={!empilhado || i === series.length - 1 ? [4, 4, 0, 0] : 0}
                stroke={empilhado ? '#ffffff' : undefined}
                strokeWidth={empilhado ? 1 : 0}
                isAnimationActive={false}
              />
            ))}
          </BarChart>
        </ResponsiveContainer>
      </div>
    </figure>
  );
}
