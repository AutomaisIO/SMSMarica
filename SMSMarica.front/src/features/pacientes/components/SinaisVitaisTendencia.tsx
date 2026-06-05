import { HeartPulse } from 'lucide-react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip as ChartTooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type { Atendimento } from '@/features/pacientes/types';

type Ponto = { data: string; valor: number | null; valor2?: number | null };

const COR_PRIMARIA = '#C8102E'; // vermelho Maricá
const COR_SECUNDARIA = '#F59E0B'; // âmbar (diastólica)

function dataCurta(iso?: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime())
    ? '—'
    : d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
}

/** Série temporal de um parâmetro ao longo dos atendimentos (já ordenados do mais antigo ao mais novo). */
function serie(lista: Atendimento[], codigo: string, comDiastolica = false): Ponto[] {
  const pontos: Ponto[] = [];
  for (const a of lista) {
    const v = a.sinaisVitais.find((s) => s.codigo === codigo);
    if (!v || (v.valor == null && v.valor2 == null)) continue;
    pontos.push({
      data: dataCurta(a.inicio),
      valor: v.valor ?? null,
      valor2: comDiastolica ? v.valor2 ?? null : undefined,
    });
  }
  return pontos;
}

function Quadro({
  titulo,
  unidade,
  dados,
  diastolica,
}: {
  titulo: string;
  unidade: string;
  dados: Ponto[];
  diastolica?: boolean;
}) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-3 shadow-sm">
      <div className="mb-1 flex items-baseline justify-between">
        <span className="text-xs font-semibold text-gray-800">{titulo}</span>
        <span className="text-[10px] text-gray-400">{unidade}</span>
      </div>
      {dados.length === 0 ? (
        <div className="flex h-[110px] items-center justify-center text-xs text-gray-400">sem dados</div>
      ) : (
        <ResponsiveContainer width="100%" height={110}>
          <LineChart data={dados} margin={{ top: 5, right: 8, bottom: 0, left: -20 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
            <XAxis dataKey="data" tick={{ fontSize: 9 }} interval="preserveStartEnd" />
            <YAxis tick={{ fontSize: 9 }} width={30} domain={['auto', 'auto']} />
            <ChartTooltip labelStyle={{ fontSize: 11 }} contentStyle={{ fontSize: 11, padding: '4px 8px' }} />
            <Line
              type="monotone"
              dataKey="valor"
              name={diastolica ? 'Sistólica' : titulo}
              stroke={COR_PRIMARIA}
              strokeWidth={2}
              dot={{ r: 2 }}
              connectNulls
            />
            {diastolica ? (
              <Line
                type="monotone"
                dataKey="valor2"
                name="Diastólica"
                stroke={COR_SECUNDARIA}
                strokeWidth={2}
                dot={{ r: 2 }}
                connectNulls
              />
            ) : null}
          </LineChart>
        </ResponsiveContainer>
      )}
    </div>
  );
}

/**
 * Cinco quadros com a tendência de cada sinal vital ao longo de todos os atendimentos
 * do paciente (origem: Observation/vital-signs do hub FHIR). PA mostra sistólica +
 * diastólica no mesmo quadro. Não renderiza nada se nenhum atendimento tiver vitais.
 */
export function SinaisVitaisTendencia({ atendimentos }: { atendimentos: Atendimento[] }) {
  // Mais antigo -> mais novo, para o eixo X cronológico.
  const ordenado = [...atendimentos]
    .filter((a) => a.sinaisVitais.length > 0)
    .sort((a, b) => (a.inicio ?? '').localeCompare(b.inicio ?? ''));

  if (ordenado.length === 0) return null;

  return (
    <section className="space-y-3">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
        <HeartPulse className="h-4 w-4 text-primary-600" /> Sinais vitais — tendência
      </h2>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
        <Quadro titulo="Pressão arterial" unidade="mmHg" dados={serie(ordenado, '85354-9', true)} diastolica />
        <Quadro titulo="Freq. cardíaca" unidade="bpm" dados={serie(ordenado, '8867-4')} />
        <Quadro titulo="Freq. respiratória" unidade="irpm" dados={serie(ordenado, '9279-1')} />
        <Quadro titulo="Temperatura" unidade="°C" dados={serie(ordenado, '8310-5')} />
        <Quadro titulo="Saturação O₂" unidade="%" dados={serie(ordenado, '2708-6')} />
      </div>
    </section>
  );
}
