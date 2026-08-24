import { useMemo, useState } from 'react';
import { BarChart3 } from 'lucide-react';
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
import { useTemConsulta } from '@/shared/auth/authStore';
import { useEstatisticasWhatsApp } from '@/features/estatisticas/api/queries';
import type { RotuloContagem } from '@/features/estatisticas/types';

// Paleta categórica CVD-safe (Okabe-Ito + vermelho Maricá). Ordem fixa, nunca ciclada.
const COR_ENVIADAS = '#C8102E'; // vermelho Maricá
const COR_RECEBIDAS = '#2563EB'; // azul
const PALETA_CATEGORIA = ['#2563EB', '#C8102E', '#059669', '#D97706', '#6B7280'];
const PALETA_BARRAS = '#C8102E';
const CORES_STATUS: Record<string, string> = {
  Enviada: '#9CA3AF',
  Entregue: '#2563EB',
  Lida: '#059669',
  Falha: '#DC2626',
  Recebida: '#D97706',
  Outro: '#6B7280',
};

function isoHoje(): string {
  return new Date().toISOString().slice(0, 10);
}
function isoMenosDias(dias: number): string {
  const d = new Date();
  d.setDate(d.getDate() - dias);
  return d.toISOString().slice(0, 10);
}
function nf(n: number): string {
  return n.toLocaleString('pt-BR');
}
function diaCurto(iso: string): string {
  const d = new Date(`${iso}T00:00:00`);
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
}

function Tile({ rotulo, valor, sufixo }: { rotulo: string; valor: string; sufixo?: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-3 shadow-sm">
      <div className="text-[11px] font-medium uppercase tracking-wide text-gray-500">{rotulo}</div>
      <div className="mt-1 text-2xl font-semibold text-gray-900">
        {valor}
        {sufixo ? <span className="ml-0.5 text-sm font-normal text-gray-400">{sufixo}</span> : null}
      </div>
    </div>
  );
}

function Painel({
  titulo,
  children,
  className = '',
}: {
  titulo: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={`rounded-lg border border-gray-200 bg-white p-4 shadow-sm ${className}`}>
      <h3 className="mb-3 text-sm font-semibold text-gray-800">{titulo}</h3>
      {children}
    </div>
  );
}

function SemDados({ altura = 240 }: { altura?: number }) {
  return (
    <div className="flex items-center justify-center text-xs text-gray-400" style={{ height: altura }}>
      sem dados no período
    </div>
  );
}

/** Barras horizontais (rótulo → contagem), com cor fixa opcional por rótulo. */
function BarrasHorizontais({
  dados,
  cor,
  corPorRotulo,
}: {
  dados: RotuloContagem[];
  cor?: string;
  corPorRotulo?: Record<string, string>;
}) {
  if (dados.length === 0) return <SemDados />;
  return (
    <ResponsiveContainer width="100%" height={Math.max(180, dados.length * 34)}>
      <BarChart data={dados} layout="vertical" margin={{ top: 4, right: 16, bottom: 4, left: 8 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" horizontal={false} />
        <XAxis type="number" tick={{ fontSize: 10 }} allowDecimals={false} />
        <YAxis type="category" dataKey="rotulo" tick={{ fontSize: 10 }} width={150} />
        <ChartTooltip
          formatter={(v: number) => [nf(v), 'Mensagens']}
          contentStyle={{ fontSize: 11, padding: '4px 8px' }}
        />
        <Bar dataKey="total" radius={[0, 4, 4, 0]} maxBarSize={22}>
          {dados.map((d) => (
            <Cell key={d.rotulo} fill={corPorRotulo?.[d.rotulo] ?? cor ?? PALETA_BARRAS} />
          ))}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

/**
 * Retrato do WhatsApp (Central de Atendimento + envios automáticos). Visão gerencial, só leitura.
 * Filtro de datas + KPIs + série diária + pizza por categoria + rankings.
 */
export function EstatisticasPage() {
  const pode = useTemConsulta('Estatistica');
  const [de, setDe] = useState(() => isoMenosDias(29));
  const [ate, setAte] = useState(() => isoHoje());

  const q = useEstatisticasWhatsApp(de, ate);
  const dados = q.data;

  const serieDia = useMemo(
    () => (dados?.porDia ?? []).map((p) => ({ ...p, diaLabel: diaCurto(p.dia) })),
    [dados],
  );

  if (!pode) {
    return (
      <div className="p-6 text-sm text-gray-500">
        Você não tem permissão para ver as estatísticas de atendimento.
      </div>
    );
  }

  function preset(dias: number) {
    setDe(isoMenosDias(dias - 1));
    setAte(isoHoje());
  }

  return (
    <div className="space-y-4 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
          <BarChart3 className="h-5 w-5 text-primary-600" /> Estatísticas de atendimento
        </h1>
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex overflow-hidden rounded-md border border-gray-200">
            {[
              { r: '7d', d: 7 },
              { r: '30d', d: 30 },
              { r: '90d', d: 90 },
            ].map((b) => (
              <button
                key={b.r}
                type="button"
                onClick={() => preset(b.d)}
                className="border-r border-gray-200 px-2.5 py-1 text-xs text-gray-600 last:border-r-0 hover:bg-gray-50"
              >
                {b.r}
              </button>
            ))}
          </div>
          <input
            type="date"
            value={de}
            max={ate}
            onChange={(e) => setDe(e.target.value)}
            className="rounded-md border border-gray-200 px-2 py-1 text-xs"
          />
          <span className="text-xs text-gray-400">até</span>
          <input
            type="date"
            value={ate}
            min={de}
            max={isoHoje()}
            onChange={(e) => setAte(e.target.value)}
            className="rounded-md border border-gray-200 px-2 py-1 text-xs"
          />
        </div>
      </div>

      {q.isLoading ? (
        <div className="p-10 text-center text-sm text-gray-400">Carregando…</div>
      ) : q.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Não foi possível carregar as estatísticas. Tente novamente.
        </div>
      ) : dados ? (
        <>
          {/* KPIs */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
            <Tile rotulo="Total de mensagens" valor={nf(dados.resumo.totalMensagens)} />
            <Tile rotulo="Enviadas" valor={nf(dados.resumo.enviadas)} />
            <Tile rotulo="Recebidas" valor={nf(dados.resumo.recebidas)} />
            <Tile rotulo="Média diária" valor={nf(dados.resumo.mediaDiaria)} sufixo="/dia" />
            <Tile rotulo="Templates (sistema)" valor={nf(dados.resumo.templatesSistema)} />
            <Tile rotulo="Templates (atendente)" valor={nf(dados.resumo.templatesAtendente)} />
            <Tile rotulo="Mensagens de sessão" valor={nf(dados.resumo.mensagensSessao)} />
            <Tile rotulo="Conversas novas" valor={nf(dados.resumo.conversasNovas)} />
            <Tile rotulo="Taxa de entrega" valor={nf(dados.resumo.taxaEntrega)} sufixo="%" />
            <Tile rotulo="Taxa de leitura" valor={nf(dados.resumo.taxaLeitura)} sufixo="%" />
            <Tile rotulo="Atendentes ativos" valor={nf(dados.resumo.atendentes)} />
            <Tile rotulo="Dias no período" valor={nf(dados.resumo.diasNoPeriodo)} />
          </div>

          {/* Série diária (linha) */}
          <Painel titulo="Mensagens por dia">
            {serieDia.length === 0 ? (
              <SemDados />
            ) : (
              <ResponsiveContainer width="100%" height={280}>
                <LineChart data={serieDia} margin={{ top: 8, right: 16, bottom: 0, left: -12 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                  <XAxis dataKey="diaLabel" tick={{ fontSize: 10 }} interval="preserveStartEnd" />
                  <YAxis tick={{ fontSize: 10 }} allowDecimals={false} />
                  <ChartTooltip contentStyle={{ fontSize: 11, padding: '4px 8px' }} />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                  <Line
                    type="monotone"
                    dataKey="enviadas"
                    name="Enviadas"
                    stroke={COR_ENVIADAS}
                    strokeWidth={2}
                    dot={false}
                  />
                  <Line
                    type="monotone"
                    dataKey="recebidas"
                    name="Recebidas"
                    stroke={COR_RECEBIDAS}
                    strokeWidth={2}
                    dot={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            )}
          </Painel>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            {/* Pizza por categoria */}
            <Painel titulo="Composição por tipo de mensagem">
              {dados.porCategoria.every((c) => c.total === 0) ? (
                <SemDados />
              ) : (
                <ResponsiveContainer width="100%" height={280}>
                  <PieChart>
                    <Pie
                      data={dados.porCategoria.filter((c) => c.total > 0)}
                      dataKey="total"
                      nameKey="rotulo"
                      cx="50%"
                      cy="50%"
                      outerRadius={95}
                      innerRadius={45}
                      paddingAngle={2}
                    >
                      {dados.porCategoria
                        .filter((c) => c.total > 0)
                        .map((c, i) => (
                          <Cell key={c.rotulo} fill={PALETA_CATEGORIA[i % PALETA_CATEGORIA.length]} />
                        ))}
                    </Pie>
                    <ChartTooltip
                      formatter={(v: number, n: string) => [nf(v), n]}
                      contentStyle={{ fontSize: 11, padding: '4px 8px' }}
                    />
                    <Legend wrapperStyle={{ fontSize: 11 }} />
                  </PieChart>
                </ResponsiveContainer>
              )}
            </Painel>

            {/* Status de entrega */}
            <Painel titulo="Status de entrega">
              <BarrasHorizontais dados={dados.porStatus} corPorRotulo={CORES_STATUS} />
            </Painel>

            {/* Top templates */}
            <Painel titulo="Templates mais enviados">
              <BarrasHorizontais dados={dados.porTemplate} cor={PALETA_BARRAS} />
            </Painel>

            {/* Ranking de atendentes */}
            <Painel titulo="Mensagens por atendente">
              <BarrasHorizontais dados={dados.porAtendente} cor={COR_RECEBIDAS} />
            </Painel>
          </div>
        </>
      ) : null}
    </div>
  );
}
