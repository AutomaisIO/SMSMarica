import { useMemo, useState } from 'react';
import { BarChart3, ChevronDown, Download } from 'lucide-react';
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
import { useListarUnidades } from '@/features/unidades/api/queries';
import { useEstatisticasExamesImagem } from '@/features/relatorios-imagem/api/queries';
import {
  exportarExamesImagem,
  type ConteudoExportacao,
} from '@/features/relatorios-imagem/api/relatoriosImagemApi';
import type { RotuloContagem } from '@/features/relatorios-imagem/types';

// Paleta categórica CVD-safe (Okabe-Ito + vermelho Maricá). Ordem fixa, nunca ciclada.
const COR_REGISTRADOS = '#2563EB'; // azul
const COR_REALIZADOS = '#059669'; // verde
const COR_LAUDADOS = '#C8102E'; // vermelho Maricá
const PALETA_CATEGORIA = ['#2563EB', '#C8102E', '#059669', '#D97706', '#6B7280', '#7C3AED', '#0891B2'];
const PALETA_BARRAS = '#C8102E';

function isoLocal(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
function isoHoje(): string {
  return isoLocal(new Date());
}

type PresetMes = { rotulo: string; de: string; ate: string };

/** Dois meses anteriores + o mês corrente. Ex.: em agosto → [Junho, Julho, Atual]. */
function presetsMeses(): PresetMes[] {
  const hoje = new Date();
  const out: PresetMes[] = [];
  for (let off = 2; off >= 0; off -= 1) {
    const ini = new Date(hoje.getFullYear(), hoje.getMonth() - off, 1);
    const fim = off === 0 ? hoje : new Date(hoje.getFullYear(), hoje.getMonth() - off + 1, 0);
    const rotulo =
      off === 0
        ? 'Atual'
        : ini.toLocaleDateString('pt-BR', { month: 'long' }).replace(/^./, (c) => c.toUpperCase());
    out.push({ rotulo, de: isoLocal(ini), ate: isoLocal(fim) });
  }
  return out;
}
function nf(n: number): string {
  return n.toLocaleString('pt-BR');
}
function horas(v: number | null): string {
  if (v == null) return '—';
  if (v < 48) return `${v.toLocaleString('pt-BR', { maximumFractionDigits: 1 })} h`;
  return `${(v / 24).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} d`;
}
function diaCurto(iso: string): string {
  const d = new Date(`${iso}T00:00:00`);
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
}

function Tile({
  rotulo,
  valor,
  sufixo,
  dica,
}: {
  rotulo: string;
  valor: string;
  sufixo?: string;
  dica?: string;
}) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-3 shadow-sm" title={dica}>
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

/** Barras horizontais (rótulo → contagem). */
function BarrasHorizontais({ dados, cor }: { dados: RotuloContagem[]; cor?: string }) {
  if (dados.length === 0) return <SemDados />;
  return (
    <ResponsiveContainer width="100%" height={Math.max(180, dados.length * 34)}>
      <BarChart data={dados} layout="vertical" margin={{ top: 4, right: 16, bottom: 4, left: 8 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" horizontal={false} />
        <XAxis type="number" tick={{ fontSize: 10 }} allowDecimals={false} />
        <YAxis type="category" dataKey="rotulo" tick={{ fontSize: 10 }} width={160} />
        <ChartTooltip
          formatter={(v: number) => [nf(v), 'Exames']}
          contentStyle={{ fontSize: 11, padding: '4px 8px' }}
        />
        <Bar dataKey="total" radius={[0, 4, 4, 0]} maxBarSize={22} fill={cor ?? PALETA_BARRAS} />
      </BarChart>
    </ResponsiveContainer>
  );
}

/**
 * Relatórios e Estatísticas de Exames de Imagem. Visão gerencial, só leitura: filtro de datas +
 * unidade, KPIs (quantidades, laudos, tempos médios do ciclo), série diária, composição por
 * modalidade e rankings por unidade/tipo/médico. A lista analítica (com PII) sai por exportação.
 */
export function RelatoriosImagemPage() {
  const pode = useTemConsulta('Estatistica');
  const podeExportar = useTemConsulta('SolicitacoesExame');
  const presets = useMemo(presetsMeses, []);
  const [de, setDe] = useState(() => presets[presets.length - 1].de);
  const [ate, setAte] = useState(() => isoHoje());
  const [unidadeId, setUnidadeId] = useState('');
  const [exportando, setExportando] = useState(false);
  const [menuExport, setMenuExport] = useState(false);
  const [erroExport, setErroExport] = useState<string | null>(null);

  const unidades = useListarUnidades();
  const q = useEstatisticasExamesImagem(de, ate, unidadeId);
  const dados = q.data;

  const serieDia = useMemo(
    () => (dados?.porDia ?? []).map((p) => ({ ...p, diaLabel: diaCurto(p.dia) })),
    [dados],
  );

  if (!pode) {
    return (
      <div className="p-6 text-sm text-gray-500">
        Você não tem permissão para ver os relatórios de exames de imagem.
      </div>
    );
  }

  async function exportar(conteudo: ConteudoExportacao) {
    setMenuExport(false);
    setErroExport(null);
    setExportando(true);
    try {
      await exportarExamesImagem(de, ate, unidadeId || undefined, conteudo);
    } catch {
      setErroExport('Não foi possível exportar a lista. Tente novamente.');
    } finally {
      setExportando(false);
    }
  }

  return (
    <div className="space-y-4 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
          <BarChart3 className="h-5 w-5 text-primary-600" /> Relatórios e Estatísticas — Exames de Imagem
        </h1>
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex overflow-hidden rounded-md border border-gray-200">
            {presets.map((p) => {
              const ativo = de === p.de && ate === p.ate;
              return (
                <button
                  key={p.rotulo}
                  type="button"
                  onClick={() => {
                    setDe(p.de);
                    setAte(p.ate);
                  }}
                  className={`border-r border-gray-200 px-2.5 py-1 text-xs capitalize last:border-r-0 hover:bg-gray-50 ${
                    ativo ? 'bg-primary-50 font-medium text-primary-700' : 'text-gray-600'
                  }`}
                >
                  {p.rotulo}
                </button>
              );
            })}
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
          <select
            value={unidadeId}
            onChange={(e) => setUnidadeId(e.target.value)}
            className="max-w-[220px] rounded-md border border-gray-200 px-2 py-1 text-xs"
            title="Unidade executante"
          >
            <option value="">Todas as unidades</option>
            {(unidades.data ?? [])
              .filter((u) => u.ativo)
              .map((u) => (
                <option key={u.id} value={u.id}>
                  {u.nome}
                </option>
              ))}
          </select>
          {podeExportar ? (
            <div className="relative">
              <button
                type="button"
                onClick={() => setMenuExport((v) => !v)}
                disabled={exportando}
                className="flex items-center gap-1.5 rounded-md bg-primary-600 px-2.5 py-1 text-xs font-medium text-white hover:bg-primary-700 disabled:opacity-60"
              >
                <Download className="h-3.5 w-3.5" />
                {exportando ? 'Exportando…' : 'Exportar CSV'}
                <ChevronDown className="h-3.5 w-3.5" />
              </button>
              {menuExport ? (
                <>
                  <button
                    type="button"
                    aria-hidden
                    tabIndex={-1}
                    className="fixed inset-0 z-10 cursor-default"
                    onClick={() => setMenuExport(false)}
                  />
                  <div className="absolute right-0 z-20 mt-1 w-44 overflow-hidden rounded-md border border-gray-200 bg-white py-1 shadow-lg">
                    {(
                      [
                        { c: 'Exames', r: 'Somente Exames' },
                        { c: 'Laudos', r: 'Somente Laudos' },
                        { c: 'ExamesLaudos', r: 'Exames e Laudos' },
                      ] as { c: ConteudoExportacao; r: string }[]
                    ).map((o) => (
                      <button
                        key={o.c}
                        type="button"
                        onClick={() => exportar(o.c)}
                        className="block w-full px-3 py-1.5 text-left text-xs text-gray-700 hover:bg-gray-50"
                      >
                        {o.r}
                      </button>
                    ))}
                  </div>
                </>
              ) : null}
            </div>
          ) : null}
        </div>
      </div>

      {erroExport ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          {erroExport}
        </div>
      ) : null}

      {q.isLoading ? (
        <div className="p-10 text-center text-sm text-gray-400">Carregando…</div>
      ) : q.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Não foi possível carregar os relatórios. Tente novamente.
        </div>
      ) : dados ? (
        <>
          {/* KPIs */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
            <Tile rotulo="Total de exames" valor={nf(dados.resumo.totalExames)} />
            <Tile rotulo="Realizados" valor={nf(dados.resumo.realizados)} />
            <Tile rotulo="Laudados" valor={nf(dados.resumo.laudados)} />
            <Tile rotulo="Aguardando laudo" valor={nf(dados.resumo.aguardandoLaudo)} />
            <Tile rotulo="Laudos emitidos" valor={nf(dados.resumo.laudosEmitidos)} />
            <Tile rotulo="Cancelados" valor={nf(dados.resumo.cancelados)} />
            <Tile rotulo="% laudados" valor={nf(dados.resumo.percentualLaudados)} sufixo="%" />
            <Tile rotulo="Média por dia" valor={nf(dados.resumo.mediaExamesDia)} sufixo="/dia" />
            <Tile rotulo="Médicos laudando" valor={nf(dados.resumo.medicosLaudando)} />
            <Tile
              rotulo="Chegada → execução"
              valor={horas(dados.resumo.tempoMedioChegadaExecucaoHoras)}
              dica={`Média sobre ${nf(dados.resumo.amostraChegadaExecucao)} exame(s) com autorização e execução.`}
            />
            <Tile
              rotulo="Execução → laudo"
              valor={horas(dados.resumo.tempoMedioExecucaoLaudoHoras)}
              dica={`Média sobre ${nf(dados.resumo.amostraExecucaoLaudo)} exame(s) executados e laudados.`}
            />
            <Tile
              rotulo="Ciclo total"
              valor={horas(dados.resumo.tempoMedioTotalHoras)}
              dica={`Chegada → laudo. Média sobre ${nf(dados.resumo.amostraTotal)} exame(s).`}
            />
          </div>

          {/* Série diária (linha) */}
          <Painel titulo="Exames por dia">
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
                    dataKey="registrados"
                    name="Registrados"
                    stroke={COR_REGISTRADOS}
                    strokeWidth={2}
                    dot={false}
                  />
                  <Line
                    type="monotone"
                    dataKey="realizados"
                    name="Realizados"
                    stroke={COR_REALIZADOS}
                    strokeWidth={2}
                    dot={false}
                  />
                  <Line
                    type="monotone"
                    dataKey="laudados"
                    name="Laudados"
                    stroke={COR_LAUDADOS}
                    strokeWidth={2}
                    dot={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            )}
          </Painel>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            {/* Composição por modalidade */}
            <Painel titulo="Composição por modalidade">
              {dados.porModalidade.every((c) => c.total === 0) ? (
                <SemDados />
              ) : (
                <ResponsiveContainer width="100%" height={280}>
                  <PieChart>
                    <Pie
                      data={dados.porModalidade.filter((c) => c.total > 0)}
                      dataKey="total"
                      nameKey="rotulo"
                      cx="50%"
                      cy="50%"
                      outerRadius={95}
                      innerRadius={45}
                      paddingAngle={2}
                    >
                      {dados.porModalidade
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

            {/* Status */}
            <Painel titulo="Exames por status">
              <BarrasHorizontais dados={dados.porStatus} cor={COR_REGISTRADOS} />
            </Painel>

            {/* Por unidade */}
            <Painel titulo="Exames por unidade executante">
              <BarrasHorizontais dados={dados.porUnidade} cor={PALETA_BARRAS} />
            </Painel>

            {/* Por tipo de exame */}
            <Painel titulo="Exames por tipo">
              <BarrasHorizontais dados={dados.porTipoExame} cor={COR_REALIZADOS} />
            </Painel>

            {/* Por médico que laudou */}
            <Painel titulo="Laudos por médico" className="lg:col-span-2">
              <BarrasHorizontais dados={dados.porMedico} cor={COR_LAUDADOS} />
            </Painel>
          </div>
        </>
      ) : null}
    </div>
  );
}
