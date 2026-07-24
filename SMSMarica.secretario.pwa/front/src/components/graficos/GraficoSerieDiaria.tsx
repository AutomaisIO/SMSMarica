import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  LabelList,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type { PontoDia } from '@/types/painel';
import {
  diaHojeBrasilia,
  diaPorExtenso,
  ehFimDeSemana,
  diaDoMes,
  formatarInteiro,
} from '@/lib/formatos';
import { prefereMenosMovimento } from '@/lib/movimento';
import { TooltipCartao, type LinhaTooltip } from './TooltipCartao';

const VINHO = '#9E1B32';
const VERMELHO_MARICA = '#C8102E';
const VINHO_CLARO = '#C97B86';
const NEUTRO_SERIE = '#C9CED6';

/**
 * As três faixas da internação, na ordem em que empilham (base → topo). Cores
 * validadas com o validador de paleta do dataviz: pior par adjacente ΔE 17,9 em
 * visão com deficiência de cor e 20,8 em visão normal. Toda faixa vem com o nome
 * escrito na legenda — cor nunca carrega o significado sozinha.
 */
export const FAIXAS_INTERNACAO = [
  { chave: 'maternidade', rotulo: 'Maternidade', cor: VINHO },
  { chave: 'ate17', rotulo: 'Até 17 anos', cor: VINHO_CLARO },
  { chave: 'adultos', rotulo: 'Adultos', cor: NEUTRO_SERIE },
] as const;
const GRADE = '#EDF0F3';
const LINHA = '#E6E9EE';
const GRAFITE = '#5A6572';
const TINTA = '#131A22';

interface Props {
  dados: PontoDia[];
  /** Nome da grandeza no tooltip: "atendimentos" | "internações" | "partos". */
  rotuloUnidade: string;
  altura?: number;
}

/**
 * Série diária de 35 dias: barras finas em vinho, topo arredondado, HOJE (último
 * ponto, parcial) em vermelho-marica com rótulo direto; máximo também rotulado.
 * Fins de semana ganham tick discreto. Baseline sempre em zero, um único eixo y.
 * Quando os pontos trazem as três faixas por dia, as barras empilham
 * automaticamente (com legenda) — ver FAIXAS_INTERNACAO.
 */
export function GraficoSerieDiaria({ dados, rotuloUnidade, altura = 240 }: Props) {
  if (dados.length === 0) {
    return <p className="py-10 text-center text-[13.5px] text-grafite">Sem dados no período.</p>;
  }

  // "hoje · em andamento" só quando o último ponto É o dia corrente em
  // America/Sao_Paulo — snapshot velho vira dia fechado normal, sem fingir.
  const ultimoEhHoje = dados[dados.length - 1].dia === diaHojeBrasilia();
  const idxHoje = ultimoEhHoje ? dados.length - 1 : -1;
  const idxMax = dados.reduce((max, d, i) => (d.qtd > dados[max].qtd ? i : max), 0);
  const empilhado = dados.every(
    (d) => d.maternidade != null && d.ate17 != null && d.adultos != null,
  );
  const animar = !prefereMenosMovimento();

  // Rótulo direto só em pontos selecionados: hoje e máximo.
  const renderRotulo = (props: unknown) => {
    const { x, y, width, value, index } = props as {
      x: number;
      y: number;
      width: number;
      value: number;
      index: number;
    };
    if (index !== idxHoje && index !== idxMax) return null;
    const destaque = index === idxHoje;
    return (
      <text
        x={x + width / 2}
        y={y - 6}
        textAnchor="middle"
        fontSize={11}
        fontWeight={600}
        fill={destaque ? VERMELHO_MARICA : TINTA}
        className="tnum"
      >
        {formatarInteiro(value)}
      </text>
    );
  };

  const renderTick = (props: unknown) => {
    const { x, y, payload, index } = props as {
      x: number;
      y: number;
      payload: { value: string };
      index: number;
    };
    if (index === idxHoje) {
      return (
        <text x={x} y={y + 12} textAnchor="middle" fontSize={10} fontWeight={600} fill={VERMELHO_MARICA}>
          hoje
        </text>
      );
    }
    if (ehFimDeSemana(payload.value)) {
      return (
        <text x={x} y={y + 12} textAnchor="middle" fontSize={10} fill="#9AA3AE">
          {diaDoMes(payload.value)}
        </text>
      );
    }
    return <g />;
  };

  const renderTooltip = (props: unknown) => {
    const { active, payload } = props as {
      active?: boolean;
      payload?: { payload: PontoDia }[];
    };
    if (!active || !payload || payload.length === 0) return null;
    const ponto = payload[0].payload;
    const hoje = idxHoje >= 0 && dados[idxHoje].dia === ponto.dia;
    const titulo = `${diaPorExtenso(ponto.dia)}${hoje ? ' · em andamento' : ''}`;
    const linhas: LinhaTooltip[] = empilhado
      ? [
          ...FAIXAS_INTERNACAO.map((faixa) => ({
            cor: faixa.cor,
            rotulo: faixa.rotulo.toLowerCase(),
            valor: formatarInteiro((ponto[faixa.chave] as number | undefined) ?? 0),
          })),
          { rotulo: 'total', valor: formatarInteiro(ponto.qtd) },
        ]
      : [{ rotulo: rotuloUnidade, valor: formatarInteiro(ponto.qtd) }];
    return <TooltipCartao titulo={titulo} linhas={linhas} />;
  };

  return (
    <div>
      {empilhado && (
        <div className="mb-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-[12.5px] text-grafite">
          {FAIXAS_INTERNACAO.map((faixa) => (
            <span key={faixa.chave} className="flex items-center gap-1.5">
              <span className="h-2 w-2 rounded-full" style={{ backgroundColor: faixa.cor }} />
              {faixa.rotulo}
            </span>
          ))}
        </div>
      )}
      <ResponsiveContainer width="100%" height={altura}>
        <BarChart data={dados} margin={{ top: 24, right: 4, left: -6, bottom: 0 }} barCategoryGap={2}>
          <CartesianGrid vertical={false} stroke={GRADE} />
          <XAxis
            dataKey="dia"
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
          {empilhado ? (
            <>
              {FAIXAS_INTERNACAO.map((faixa, i) => {
                const ultima = i === FAIXAS_INTERNACAO.length - 1;
                return (
                  <Bar
                    key={faixa.chave}
                    dataKey={faixa.chave}
                    stackId="serie"
                    fill={faixa.cor}
                    stroke="#FFFFFF"
                    strokeWidth={1}
                    radius={ultima ? [4, 4, 0, 0] : undefined}
                    isAnimationActive={animar}
                  >
                    {/* o rótulo do total vai na última fatia, no topo da pilha */}
                    {ultima && <LabelList dataKey="qtd" content={renderRotulo} />}
                  </Bar>
                );
              })}
            </>
          ) : (
            <Bar dataKey="qtd" radius={[4, 4, 0, 0]} isAnimationActive={animar}>
              {dados.map((ponto, i) => (
                <Cell key={ponto.dia} fill={i === idxHoje ? VERMELHO_MARICA : VINHO} />
              ))}
              <LabelList dataKey="qtd" content={renderRotulo} />
            </Bar>
          )}
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
