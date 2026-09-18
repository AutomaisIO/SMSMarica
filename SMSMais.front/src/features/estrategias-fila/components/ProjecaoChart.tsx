import {
  Area,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { fraseProjecao, n } from '@/features/estrategias-fila/lib/parametros';
import type { Projecao } from '@/features/estrategias-fila/types';

type Props = {
  /** A projeção com os parâmetros atuais. */
  projecao: Projecao | null;
  /** A projeção com o cenário de hoje (sem mudanças), em cinza, para comparar. */
  base: Projecao | null;
  prazoAlvo: number | null;
  carregando?: boolean;
};

/**
 * A curva da fila semana a semana: linha vermelha = com os parâmetros atuais; cinza tracejada =
 * como está hoje; área = capacidade semanal. Marco vertical na semana em que zera e no prazo alvo.
 */
export function ProjecaoChart({ projecao, base, prazoAlvo, carregando }: Props) {
  const horizonte = Math.max(projecao?.horizonteSemanas ?? 0, base?.horizonteSemanas ?? 0);
  const dados = Array.from({ length: horizonte + 1 }, (_, semana) => ({
    semana,
    fila: projecao?.serie[semana]?.fila ?? null,
    filaHoje: base?.serie[semana]?.fila ?? null,
    capacidade: projecao?.serie[semana]?.capacidade ?? null,
  }));

  const p = projecao;

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="mb-2 flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-sm font-semibold text-gray-900">Projeção da fila</h2>
        {p ? (
          <p className={`text-sm font-semibold ${p.zera ? 'text-emerald-700' : 'text-red-700'}`}>
            {carregando ? 'calculando…' : fraseProjecao(p, prazoAlvo)}
          </p>
        ) : null}
      </div>

      {p ? (
        <div className="mb-3 grid grid-cols-2 gap-2 text-xs sm:grid-cols-4 lg:grid-cols-6">
          <Marco rotulo="Capacidade/sem" valor={n(p.capacidadeSemanal, 1)} dica={`${n(p.vagasSemanais, 1)} vagas × aproveitamento`} />
          <Marco rotulo="Entrada/sem" valor={n(p.entradaSemanal, 1)} dica="Pessoas novas por semana" />
          <Marco
            rotulo="Equilíbrio"
            valor={n(p.capacidadeEquilibrio, 1)}
            dica="Capacidade mínima para a fila parar de crescer"
            classe={p.capacidadeSemanal >= p.capacidadeEquilibrio ? 'text-emerald-700' : 'text-red-700'}
          />
          {p.capacidadeParaZerarNoPrazo !== null ? (
            <Marco
              rotulo={`Para zerar em ${prazoAlvo} sem`}
              valor={n(p.capacidadeParaZerarNoPrazo, 1)}
              dica="Capacidade/semana necessária no prazo"
              classe={p.capacidadeSemanal >= p.capacidadeParaZerarNoPrazo ? 'text-emerald-700' : 'text-amber-700'}
            />
          ) : (
            <Marco rotulo="Pico da fila" valor={n(p.picoFila)} dica="Maior tamanho no horizonte" />
          )}
          <Marco rotulo="Atendidos até zerar" valor={p.zera ? n(p.atendidosAteZerar) : '—'} dica="Fila inicial + quem chegou até lá" />
          <Marco rotulo="Fila ao fim" valor={n(p.filaFinal)} dica={`Ao fim de ${p.horizonteSemanas} semanas`} classe={p.filaFinal > 0 ? 'text-red-700' : 'text-emerald-700'} />
        </div>
      ) : null}

      <ResponsiveContainer width="100%" height={260}>
        <ComposedChart data={dados} margin={{ top: 8, right: 12, bottom: 4, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
          <XAxis dataKey="semana" tick={{ fontSize: 10 }} label={{ value: 'semanas', position: 'insideBottomRight', offset: -2, fontSize: 10 }} />
          <YAxis tick={{ fontSize: 10 }} />
          <Tooltip contentStyle={{ fontSize: 11 }} labelFormatter={(s) => `Semana ${s}`} formatter={(v) => (typeof v === 'number' ? n(v, 1) : '—')} />
          <Legend wrapperStyle={{ fontSize: 11 }} />
          <Area type="monotone" dataKey="capacidade" name="Capacidade/semana" stroke="#93c5fd" fill="#dbeafe" isAnimationActive={false} />
          <Line type="monotone" dataKey="filaHoje" name="Fila (como está hoje)" stroke="#9ca3af" strokeDasharray="4 3" strokeWidth={1.5} dot={false} isAnimationActive={false} />
          <Line type="monotone" dataKey="fila" name="Fila (com a estratégia)" stroke="#b91c1c" strokeWidth={2.5} dot={false} isAnimationActive={false} />
          {p?.semanaZera !== null && p?.semanaZera !== undefined ? (
            <ReferenceLine x={p.semanaZera} stroke="#059669" strokeDasharray="3 3" label={{ value: `zera (sem ${p.semanaZera})`, fontSize: 10, fill: '#059669', position: 'top' }} />
          ) : null}
          {prazoAlvo ? (
            <ReferenceLine x={prazoAlvo} stroke="#d97706" strokeDasharray="2 4" label={{ value: `prazo (${prazoAlvo})`, fontSize: 10, fill: '#d97706', position: 'insideTopRight' }} />
          ) : null}
        </ComposedChart>
      </ResponsiveContainer>
    </section>
  );
}

function Marco({ rotulo, valor, dica, classe = 'text-gray-900' }: { rotulo: string; valor: string; dica: string; classe?: string }) {
  return (
    <div className="rounded-lg border border-gray-100 bg-gray-50 px-2 py-1.5" title={dica}>
      <p className="text-[10px] text-gray-500">{rotulo}</p>
      <p className={`text-base font-semibold ${classe}`}>{valor}</p>
    </div>
  );
}
