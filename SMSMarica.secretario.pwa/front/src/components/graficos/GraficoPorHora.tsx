import {
  Bar,
  BarChart,
  CartesianGrid,
  LabelList,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type { PontoHora } from '@/types/painel';
import { formatarInteiro } from '@/lib/formatos';
import { prefereMenosMovimento } from '@/lib/movimento';
import { TooltipCartao } from './TooltipCartao';

const VINHO = '#9E1B32';
const GRADE = '#EDF0F3';
const LINHA = '#E6E9EE';
const GRAFITE = '#5A6572';
const TINTA = '#131A22';

const HORAS_COM_TICK = new Set([0, 6, 12, 18, 23]);

interface Slot {
  hora: number;
  qtd: number | null;
}

/**
 * Hoje, hora a hora: eixo fixo 0–23h (as horas que ainda não chegaram ficam
 * vazias — nada de eixo que encolhe ao longo do dia). Máximo com rótulo direto.
 */
export function GraficoPorHora({ dados, altura = 200 }: { dados: PontoHora[]; altura?: number }) {
  const porHora = new Map(dados.map((d) => [d.hora, d.qtd]));
  const slots: Slot[] = Array.from({ length: 24 }, (_, h) => ({
    hora: h,
    qtd: porHora.has(h) ? (porHora.get(h) as number) : null,
  }));

  const idxMax = slots.reduce(
    (max, s, i) => ((s.qtd ?? -1) > (slots[max].qtd ?? -1) ? i : max),
    0,
  );
  const animar = !prefereMenosMovimento();

  const renderRotulo = (props: unknown) => {
    const { x, y, width, value, index } = props as {
      x: number;
      y: number;
      width: number;
      value: number | null;
      index: number;
    };
    if (index !== idxMax || value == null) return null;
    return (
      <text
        x={x + width / 2}
        y={y - 6}
        textAnchor="middle"
        fontSize={11}
        fontWeight={600}
        fill={TINTA}
        className="tnum"
      >
        {formatarInteiro(value)}
      </text>
    );
  };

  const renderTick = (props: unknown) => {
    const { x, y, payload } = props as { x: number; y: number; payload: { value: number } };
    if (!HORAS_COM_TICK.has(payload.value)) return <g />;
    return (
      <text x={x} y={y + 12} textAnchor="middle" fontSize={10} fill={GRAFITE}>
        {payload.value}h
      </text>
    );
  };

  const renderTooltip = (props: unknown) => {
    const { active, payload } = props as { active?: boolean; payload?: { payload: Slot }[] };
    if (!active || !payload || payload.length === 0) return null;
    const slot = payload[0].payload;
    if (slot.qtd == null) return null;
    return (
      <TooltipCartao
        titulo={`hoje, ${slot.hora === 23 ? '23h à 0h' : `${slot.hora}h às ${slot.hora + 1}h`}`}
        linhas={[{ rotulo: 'atendimentos', valor: formatarInteiro(slot.qtd) }]}
      />
    );
  };

  return (
    <ResponsiveContainer width="100%" height={altura}>
      <BarChart data={slots} margin={{ top: 24, right: 4, left: -6, bottom: 0 }} barCategoryGap={2}>
        <CartesianGrid vertical={false} stroke={GRADE} />
        <XAxis
          dataKey="hora"
          interval={0}
          tickLine={false}
          axisLine={{ stroke: LINHA }}
          tick={renderTick}
          height={20}
        />
        <YAxis
          tickLine={false}
          axisLine={false}
          tick={{ fill: GRAFITE, fontSize: 11 }}
          width={40}
          domain={[0, 'auto']}
          tickFormatter={(v: number) => formatarInteiro(v)}
        />
        <Tooltip content={renderTooltip} cursor={{ fill: 'rgba(19, 26, 34, 0.05)' }} />
        <Bar dataKey="qtd" fill={VINHO} radius={[4, 4, 0, 0]} isAnimationActive={animar}>
          <LabelList dataKey="qtd" content={renderRotulo} />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}
