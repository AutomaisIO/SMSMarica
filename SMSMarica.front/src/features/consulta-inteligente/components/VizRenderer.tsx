import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip as ChartTooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { MapPin } from 'lucide-react';
import type { VizSpec } from '../types';

const CORES = ['#C8102E', '#E4572E', '#F4A259', '#1B998B', '#2D7DD2', '#7B2D8B', '#9CA3AF'];

function texto(v: unknown): string {
  if (v === null || v === undefined) return '—';
  if (typeof v === 'object') return JSON.stringify(v);
  return String(v);
}

/**
 * Renderiza a visualização declarada pelo agente (tool `visualizar`). Gráficos saem via
 * recharts; mapas caem num fallback (lista de pontos/polígonos) até haver a chave do Google
 * Maps — a troca depois é local a este componente.
 */
export function VizRenderer({ spec }: { spec: VizSpec }) {
  const dados = spec.dados ?? [];

  if (spec.tipo === 'numero') {
    return (
      <div className="my-2 flex flex-col items-start gap-1 rounded-lg bg-primary-50 px-5 py-4">
        <span className="text-3xl font-bold text-primary-700">
          {texto(spec.valor)}
          {spec.unidade ? <span className="ml-1 text-lg text-primary-500">{spec.unidade}</span> : null}
        </span>
        {spec.titulo ? (
          <span className="text-xs uppercase tracking-wide text-primary-600">{spec.titulo}</span>
        ) : null}
      </div>
    );
  }

  if (spec.tipo === 'tabela') {
    const colunas = spec.colunas ?? [];
    const linhas = spec.linhas ?? [];
    return (
      <div className="my-2 overflow-x-auto rounded-lg border border-gray-200">
        <table className="min-w-full text-sm">
          <thead className="bg-gray-50">
            <tr>
              {colunas.map((c, i) => (
                <th key={i} className="px-3 py-2 text-left font-semibold text-gray-700">{c}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {linhas.map((linha, i) => (
              <tr key={i}>
                {linha.map((c, j) => (
                  <td key={j} className="px-3 py-1.5 text-gray-700">{texto(c)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  if (spec.tipo === 'pizza') {
    return (
      <VizCard titulo={spec.titulo}>
        <ResponsiveContainer width="100%" height={300}>
          <PieChart>
            <Pie data={dados} dataKey="valor" nameKey="rotulo" outerRadius={100} label>
              {dados.map((_, i) => (
                <Cell key={i} fill={CORES[i % CORES.length]} />
              ))}
            </Pie>
            <ChartTooltip />
            <Legend />
          </PieChart>
        </ResponsiveContainer>
      </VizCard>
    );
  }

  if (spec.tipo === 'barra') {
    return (
      <VizCard titulo={spec.titulo}>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={dados}>
            <CartesianGrid strokeDasharray="3 3" stroke="#eee" />
            <XAxis dataKey="rotulo" tick={{ fontSize: 12 }} />
            <YAxis tick={{ fontSize: 12 }} />
            <ChartTooltip />
            <Bar dataKey="valor" fill="#C8102E" radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </VizCard>
    );
  }

  if (spec.tipo === 'linha') {
    return (
      <VizCard titulo={spec.titulo}>
        <ResponsiveContainer width="100%" height={300}>
          <LineChart data={dados}>
            <CartesianGrid strokeDasharray="3 3" stroke="#eee" />
            <XAxis dataKey="rotulo" tick={{ fontSize: 12 }} />
            <YAxis tick={{ fontSize: 12 }} />
            <ChartTooltip />
            <Line type="monotone" dataKey="valor" stroke="#C8102E" strokeWidth={2} dot={false} />
          </LineChart>
        </ResponsiveContainer>
      </VizCard>
    );
  }

  // Mapas: fallback até a chave do Google Maps. Mostra o que o agente enviou, sem perder o dado.
  const pontos = spec.pontos ?? [];
  const poligonos = spec.poligonos ?? [];
  return (
    <VizCard titulo={spec.titulo}>
      <div className="rounded-md border border-dashed border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-800">
        <p className="flex items-center gap-1.5 font-medium">
          <MapPin className="h-3.5 w-3.5" />
          Mapa ({spec.tipo.replace('mapa_', '')}) — visualização no mapa entra quando a chave do
          Google Maps for configurada. Dados abaixo:
        </p>
        {pontos.length > 0 && (
          <ul className="mt-1 max-h-40 space-y-0.5 overflow-y-auto">
            {pontos.map((p, i) => (
              <li key={i}>
                • {p.rotulo ? `${p.rotulo}: ` : ''}({p.lat}, {p.lng})
                {p.valor != null ? ` — ${p.valor}` : ''}
                {p.peso != null ? ` — peso ${p.peso}` : ''}
              </li>
            ))}
          </ul>
        )}
        {poligonos.length > 0 && (
          <ul className="mt-1 space-y-0.5">
            {poligonos.map((pg, i) => (
              <li key={i}>
                • {pg.rotulo ?? `Polígono ${i + 1}`} — {pg.coordenadas.length} vértices
                {pg.valor != null ? ` — ${pg.valor}` : ''}
              </li>
            ))}
          </ul>
        )}
      </div>
    </VizCard>
  );
}

function VizCard({ titulo, children }: { titulo?: string; children: React.ReactNode }) {
  return (
    <div className="my-2 rounded-lg border border-gray-200 bg-white p-3">
      {titulo ? <p className="mb-2 text-sm font-semibold text-gray-800">{titulo}</p> : null}
      {children}
    </div>
  );
}
