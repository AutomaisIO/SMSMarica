import { useState } from 'react';
import { Hourglass, Loader2 } from 'lucide-react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ComposedChart,
  Line,
  ReferenceLine,
  ResponsiveContainer,
  Scatter,
  ScatterChart,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useFaixasEspera,
  useOrigemDemanda,
  useProcedimentosDemanda,
  useResumoDemanda,
  useSerieDemanda,
} from '@/features/agenda/api/queries';
import { AvisoCobertura } from '@/features/agenda/components/AvisoCobertura';
import { FiltroDemanda } from '@/features/agenda/components/FiltroDemanda';
import { diaBrasilia, mesCurto } from '@/features/agenda/lib/datasAgenda';
import type { DemandaFiltro, DemandaProcedimento } from '@/features/agenda/api/demandaApi';

/** Vermelho a partir de 90 dias — é o corte a partir do qual a espera deixa de ser rotina. */
const LIMITE_ATENCAO = 90;

function corEspera(dias: number) {
  if (dias > 180) return '#b91c1c';
  if (dias > LIMITE_ATENCAO) return '#ea580c';
  if (dias > 30) return '#ca8a04';
  return '#059669';
}

function classeEspera(dias: number) {
  if (dias > 180) return 'text-red-700';
  if (dias > LIMITE_ATENCAO) return 'text-orange-600';
  if (dias > 30) return 'text-amber-600';
  return 'text-emerald-700';
}

const ORDENS = [
  { id: 'volume', rotulo: 'Mais pedidos' },
  { id: 'espera', rotulo: 'Maior espera' },
  { id: 'atraso', rotulo: 'Mais casos acima de 90 dias' },
] as const;

type TooltipScatter = { active?: boolean; payload?: { payload: DemandaProcedimento }[] };

function DicaProcedimento({ active, payload }: TooltipScatter) {
  const p = payload?.[0]?.payload;
  if (!active || !p) return null;
  return (
    <div className="max-w-xs rounded-lg border border-gray-200 bg-white p-2 text-xs shadow-lg">
      <p className="font-semibold text-gray-900">{p.procedimento}</p>
      <p className="mt-1 text-gray-600">
        {p.volume} pedidos · espera mediana{' '}
        <strong className={classeEspera(p.esperaMediana)}>{p.esperaMediana} dias</strong> (p90{' '}
        {p.esperaP90})
      </p>
      <p className="text-gray-500">
        {p.acima90Dias} acima de 90 dias · {p.unidadesExecutantes} executante(s) ·{' '}
        {p.unidadesSolicitantes} solicitante(s)
      </p>
    </div>
  );
}

function Cartao({
  rotulo,
  valor,
  dica,
  classe = 'text-gray-900',
  carregando,
}: {
  rotulo: string;
  valor: React.ReactNode;
  dica: string;
  classe?: string;
  carregando: boolean;
}) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm" title={dica}>
      <p className="text-xs text-gray-500">{rotulo}</p>
      <p className={`mt-1 text-2xl font-semibold ${classe}`}>{carregando ? '…' : valor}</p>
      <p className="mt-1 text-[11px] leading-tight text-gray-400">{dica}</p>
    </div>
  );
}

/**
 * Demanda regulada: o que a rede pediu, quem pediu, e quanto tempo levou até conseguir vaga.
 *
 * <p>É o outro lado da Análise de vagas. Lá se parte da escala publicada (oferta); aqui, da
 * solicitação. A separação não é organizacional: a escala do SISREG é publicada em grupos que se
 * expandem em itens no agendamento, então cruzar oferta e ocupação <em>por procedimento</em>
 * perderia metade dos casos. Aqui não há cruzamento — conta-se a própria solicitação —, e por isso
 * o procedimento volta a ser um eixo confiável.</p>
 *
 * <p>O gráfico de dispersão é o centro da tela porque volume e espera são eixos independentes:
 * medido em 05/09/2026, Ecocardiograma adulto tinha 139 pedidos com 481 dias de espera enquanto
 * Mamografia bilateral tinha 2.301 com 56. Uma lista ordenada por volume nunca mostraria o
 * primeiro.</p>
 */
export function AgendaDemandaPage() {
  const [filtro, setFiltro] = useState<DemandaFiltro>({
    de: diaBrasilia(-90),
    ate: diaBrasilia(30),
    eixoData: 'agendada',
  });
  const [ordem, setOrdem] = useState<(typeof ORDENS)[number]['id']>('volume');
  const [eixoOrigem, setEixoOrigem] = useState<'solicitante' | 'executante'>('solicitante');

  const resumo = useResumoDemanda(filtro);
  const procs = useProcedimentosDemanda(filtro, ordem);
  const faixas = useFaixasEspera(filtro);
  const origem = useOrigemDemanda(filtro, eixoOrigem);
  const serie = useSerieDemanda(filtro);

  const r = resumo.data;
  const carregando = resumo.isPending;
  const dentro30 = r && r.comEspera > 0 ? Math.round((r.ate30Dias / r.comEspera) * 100) : 0;

  const maxOrigem = Math.max(1, ...(origem.data?.map((o) => o.volume) ?? [1]));

  return (
    <div className="space-y-5">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Hourglass className="h-6 w-6 text-primary-600" />
          Demanda regulada
        </h1>
        <p className="mt-1 max-w-4xl text-sm text-gray-600">
          O que a rede pediu, de onde veio o pedido e <strong>quanto tempo levou até conseguir
          vaga</strong>. A espera é a diferença em dias corridos entre a data da solicitação e o dia
          agendado — sempre pela mediana, porque a cauda desta distribuição vai a anos e a média
          descreveria um caso que não é o de ninguém.
        </p>
      </header>

      <AvisoCobertura de={filtro.de} ate={filtro.ate} />

      <FiltroDemanda filtro={filtro} aoMudar={setFiltro} />

      {resumo.isError ? (
        <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          {extrairMensagemDeErro(resumo.error)}
        </p>
      ) : null}

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-6">
        <Cartao
          rotulo="Regulados"
          valor={(r?.regulados ?? 0).toLocaleString('pt-BR')}
          dica="Solicitações com dia agendado dentro do recorte."
          carregando={carregando}
        />
        <Cartao
          rotulo="Espera mediana"
          valor={`${r?.esperaMediana ?? 0} d`}
          dica="Metade esperou até aqui, da solicitação ao dia agendado."
          classe={classeEspera(r?.esperaMediana ?? 0)}
          carregando={carregando}
        />
        <Cartao
          rotulo="Espera p90"
          valor={`${r?.esperaP90 ?? 0} d`}
          dica="Nove em cada dez esperaram até aqui. É a pior experiência real, não a exceção."
          classe={classeEspera(r?.esperaP90 ?? 0)}
          carregando={carregando}
        />
        <Cartao
          rotulo="Após a regulação"
          valor={`${r?.esperaRegulacaoMediana ?? 0} d`}
          dica="Da regulação até o dia marcado. A diferença para a espera total é o tempo que o pedido passou ANTES de ser regulado."
          carregando={carregando}
        />
        <Cartao
          rotulo="Até 30 dias"
          valor={`${dentro30}%`}
          dica="Fatia que conseguiu vaga dentro de um mês."
          classe={dentro30 >= 50 ? 'text-emerald-700' : 'text-amber-700'}
          carregando={carregando}
        />
        <Cartao
          rotulo="Acima de 180 dias"
          valor={(r?.acima180Dias ?? 0).toLocaleString('pt-BR')}
          dica="Esperaram mais de meio ano pela vaga."
          classe={(r?.acima180Dias ?? 0) > 0 ? 'text-red-700' : 'text-gray-900'}
          carregando={carregando}
        />
      </div>

      {r && r.inconsistentes > 0 ? (
        <p className="rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-[11px] text-gray-600">
          <strong>{r.inconsistentes}</strong> solicitação(ões) estão agendadas para <em>antes</em> da
          data em que foram pedidas. É data errada na origem, não fila negativa — ficam fora do
          cálculo de espera, mas contadas aqui para poderem ser corrigidas.
        </p>
      ) : null}

      {/* Dispersão: onde o gargalo realmente está */}
      <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-gray-900">Volume × espera, por procedimento</h2>
        <p className="mb-3 text-xs text-gray-500">
          Os dois eixos são independentes: o procedimento que mais demora quase nunca é o que mais
          se pede. O que estiver <strong>alto</strong> no gráfico é fila, mesmo com pouco volume; o
          que estiver <strong>alto e à direita</strong> é fila que afeta muita gente. A linha marca
          os {LIMITE_ATENCAO} dias.
        </p>
        {procs.isPending ? (
          <Loader2 className="mx-auto my-10 h-5 w-5 animate-spin text-gray-400" />
        ) : (
          <ResponsiveContainer width="100%" height={320}>
            <ScatterChart margin={{ top: 8, right: 16, bottom: 24, left: 4 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
              <XAxis
                type="number"
                dataKey="volume"
                name="Pedidos"
                tick={{ fontSize: 11 }}
                label={{ value: 'Pedidos no período', position: 'insideBottom', offset: -14, fontSize: 11 }}
              />
              <YAxis
                type="number"
                dataKey="esperaMediana"
                name="Espera"
                tick={{ fontSize: 11 }}
                label={{ value: 'Espera mediana (dias)', angle: -90, position: 'insideLeft', fontSize: 11 }}
              />
              <ReferenceLine
                y={LIMITE_ATENCAO}
                stroke="#ea580c"
                strokeDasharray="4 4"
                label={{ value: `${LIMITE_ATENCAO} dias`, fontSize: 10, fill: '#ea580c', position: 'right' }}
              />
              <Tooltip content={<DicaProcedimento />} cursor={{ strokeDasharray: '3 3' }} />
              <Scatter data={procs.data ?? []}>
                {(procs.data ?? []).map((p) => (
                  <Cell key={p.procedimento} fill={corEspera(p.esperaMediana)} fillOpacity={0.75} />
                ))}
              </Scatter>
            </ScatterChart>
          </ResponsiveContainer>
        )}
      </section>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        {/* Histograma da espera */}
        <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="text-sm font-semibold text-gray-900">Distribuição da espera</h2>
          <p className="mb-3 text-xs text-gray-500">
            Faixas fixas, para que duas consultas sejam comparáveis. O que interessa é a cauda: é
            ela que representa gente esperando meses.
          </p>
          {faixas.isPending ? (
            <Loader2 className="mx-auto my-10 h-5 w-5 animate-spin text-gray-400" />
          ) : (
            <ResponsiveContainer width="100%" height={260}>
              <BarChart data={faixas.data ?? []} margin={{ top: 4, right: 8, bottom: 40, left: 4 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
                <XAxis
                  dataKey="rotulo"
                  tick={{ fontSize: 10 }}
                  angle={-30}
                  textAnchor="end"
                  interval={0}
                  height={60}
                />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip
                  formatter={(v: number) => [v.toLocaleString('pt-BR'), 'Solicitações']}
                  contentStyle={{ fontSize: 12 }}
                />
                <Bar dataKey="volume" radius={[4, 4, 0, 0]}>
                  {(faixas.data ?? []).map((f) => (
                    <Cell
                      key={f.ordem}
                      fill={f.ordem >= 6 ? '#b91c1c' : f.ordem >= 5 ? '#ea580c' : '#0ea5e9'}
                    />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          )}
        </section>

        {/* Série mensal */}
        <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="text-sm font-semibold text-gray-900">Volume e espera mês a mês</h2>
          <p className="mb-3 text-xs text-gray-500">
            As barras são o volume; a linha, a espera mediana daquele mês. Leia a linha, não as
            barras: o volume acompanha o quanto já foi importado, a espera não.
          </p>
          {serie.isPending ? (
            <Loader2 className="mx-auto my-10 h-5 w-5 animate-spin text-gray-400" />
          ) : (
            <ResponsiveContainer width="100%" height={260}>
              <ComposedChart
                data={(serie.data ?? []).map((s) => ({ ...s, rotulo: mesCurto(s.mes) }))}
                margin={{ top: 4, right: 8, bottom: 24, left: 4 }}
              >
                <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
                <XAxis dataKey="rotulo" tick={{ fontSize: 11 }} />
                <YAxis yAxisId="v" tick={{ fontSize: 11 }} />
                <YAxis yAxisId="e" orientation="right" tick={{ fontSize: 11 }} />
                <Tooltip contentStyle={{ fontSize: 12 }} />
                <Bar yAxisId="v" dataKey="volume" name="Solicitações" fill="#bfdbfe" radius={[4, 4, 0, 0]} />
                <Line
                  yAxisId="e"
                  type="monotone"
                  dataKey="esperaMediana"
                  name="Espera mediana (d)"
                  stroke="#b91c1c"
                  strokeWidth={2}
                  dot={{ r: 3 }}
                />
              </ComposedChart>
            </ResponsiveContainer>
          )}
        </section>
      </div>

      {/* Top procedimentos */}
      <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-sm font-semibold text-gray-900">Procedimentos regulados</h2>
          <div className="flex flex-wrap gap-1">
            {ORDENS.map((o) => (
              <button
                key={o.id}
                type="button"
                onClick={() => setOrdem(o.id)}
                className={`rounded-full px-3 py-1 text-xs ${
                  ordem === o.id ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-700'
                }`}
              >
                {o.rotulo}
              </button>
            ))}
          </div>
        </div>

        {procs.isPending ? (
          <Loader2 className="mx-auto my-6 h-5 w-5 animate-spin text-gray-400" />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[48rem] text-left text-sm">
              <thead className="text-xs text-gray-500">
                <tr>
                  <th className="py-1 pr-3 font-medium">Procedimento</th>
                  <th className="py-1 pr-3 text-right font-medium">Pedidos</th>
                  <th className="py-1 pr-3 font-medium">Espera mediana</th>
                  <th className="py-1 pr-3 text-right font-medium">p90</th>
                  <th className="py-1 pr-3 text-right font-medium" title="Quantos passaram de 90 dias">
                    &gt; 90 d
                  </th>
                  <th className="py-1 pr-3 text-right font-medium" title="Unidades que executam este procedimento">
                    Executantes
                  </th>
                  <th className="py-1 text-right font-medium">Solicitantes</th>
                </tr>
              </thead>
              <tbody>
                {procs.data?.map((p) => {
                  const largura = Math.min(100, Math.round((p.esperaMediana / 365) * 100));
                  return (
                    <tr key={p.procedimento} className="border-t border-gray-100">
                      <td className="py-2 pr-3 text-xs">{p.procedimento}</td>
                      <td className="py-2 pr-3 text-right text-xs">{p.volume.toLocaleString('pt-BR')}</td>
                      <td className="py-2 pr-3">
                        <div className="flex items-center gap-2">
                          <div className="h-2 w-24 overflow-hidden rounded-full bg-gray-100">
                            <div
                              className="h-full"
                              style={{ width: `${largura}%`, background: corEspera(p.esperaMediana) }}
                            />
                          </div>
                          <span className={`text-xs font-medium ${classeEspera(p.esperaMediana)}`}>
                            {p.esperaMediana} d
                          </span>
                        </div>
                      </td>
                      <td className="py-2 pr-3 text-right text-xs text-gray-500">{p.esperaP90}</td>
                      <td
                        className={`py-2 pr-3 text-right text-xs ${
                          p.acima90Dias > 0 ? 'text-orange-700' : 'text-gray-400'
                        }`}
                      >
                        {p.acima90Dias}
                      </td>
                      <td
                        className={`py-2 pr-3 text-right text-xs ${
                          p.unidadesExecutantes === 1 ? 'font-semibold text-amber-700' : 'text-gray-500'
                        }`}
                        title={
                          p.unidadesExecutantes === 1
                            ? 'Só uma unidade executa — não há para onde remanejar se ela parar.'
                            : undefined
                        }
                      >
                        {p.unidadesExecutantes}
                      </td>
                      <td className="py-2 text-right text-xs text-gray-500">{p.unidadesSolicitantes}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            {procs.data?.length === 0 ? (
              <p className="py-4 text-center text-xs text-gray-500">Nenhuma solicitação neste recorte.</p>
            ) : null}
            <p className="mt-3 text-[11px] text-gray-400">
              A barra de espera é relativa a um ano. Coluna <strong>Executantes</strong> em destaque
              significa procedimento com <strong>uma única</strong> unidade capaz de executá-lo — se
              ela parar, a fila não tem para onde ir.
            </p>
          </div>
        )}
      </section>

      {/* Origem da demanda */}
      <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-sm font-semibold text-gray-900">
            {eixoOrigem === 'solicitante' ? 'De onde vem o pedido' : 'Para onde o pedido vai'}
          </h2>
          <div className="flex gap-1">
            {(['solicitante', 'executante'] as const).map((e) => (
              <button
                key={e}
                type="button"
                onClick={() => setEixoOrigem(e)}
                className={`rounded-full px-3 py-1 text-xs ${
                  eixoOrigem === e ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-700'
                }`}
              >
                {e === 'solicitante' ? 'Unidade solicitante' : 'Unidade executante'}
              </button>
            ))}
          </div>
        </div>
        <p className="mb-3 text-xs text-gray-500">
          Volume e espera lado a lado. Duas unidades com o mesmo volume e esperas muito diferentes
          apontam para o encaminhamento, não para a capacidade — vale investigar o que uma faz de
          diferente.
        </p>

        {origem.isPending ? (
          <Loader2 className="mx-auto my-6 h-5 w-5 animate-spin text-gray-400" />
        ) : (
          <div className="space-y-2">
            {origem.data?.map((o) => (
              <div key={o.chave} className="flex items-center gap-3">
                <span className="w-56 shrink-0 truncate text-xs text-gray-700" title={o.rotulo}>
                  {o.rotulo}
                </span>
                <div className="h-4 flex-1 overflow-hidden rounded bg-gray-100">
                  <div
                    className="h-full bg-primary-400"
                    style={{ width: `${Math.round((o.volume / maxOrigem) * 100)}%` }}
                  />
                </div>
                <span className="w-16 shrink-0 text-right text-xs text-gray-600">
                  {o.volume.toLocaleString('pt-BR')}
                </span>
                <span
                  className={`w-20 shrink-0 text-right text-xs font-medium ${classeEspera(o.esperaMediana)}`}
                  title="Espera mediana"
                >
                  {o.esperaMediana} d
                </span>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
