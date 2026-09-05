import { useState } from 'react';
import { BarChart3, Loader2 } from 'lucide-react';
import {
  Area,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  useAgendaPorDiaSemana,
  useRankingAgenda,
  useResumoAgenda,
  useSerieAgenda,
} from '@/features/agenda/api/queries';
import { FiltroAgenda } from '@/features/agenda/components/FiltroAgenda';
import { CartoesResumo } from '@/features/agenda/components/CartoesResumo';
import { AvisoCobertura } from '@/features/agenda/components/AvisoCobertura';
import { diaBrasilia, diaCurto } from '@/features/agenda/lib/datasAgenda';
import type { AgendaFiltro, AgendaRankingItem } from '@/features/agenda/api/agendaApi';

const EIXOS = [
  { id: 'unidade', rotulo: 'Por unidade' },
  { id: 'especialidade', rotulo: 'Por especialidade' },
  { id: 'profissional', rotulo: 'Por profissional' },
] as const;

/** Escala relativa ao maior da lista — comparar barras entre rankings diferentes não faria sentido. */
function Linha({ item, maximo }: { item: AgendaRankingItem; maximo: number }) {
  const largura = maximo > 0 ? Math.round((item.vagas / maximo) * 100) : 0;
  const pct = item.ocupacaoPercentual;
  const cor = pct > 100 ? 'bg-red-500' : pct >= 80 ? 'bg-emerald-500' : pct >= 50 ? 'bg-amber-400' : 'bg-gray-300';

  return (
    <tr className="border-t border-gray-100">
      <td className="py-2 pr-3 text-xs">{item.rotulo}</td>
      <td className="py-2 pr-3">
        <div className="flex items-center gap-2">
          <div className="h-2 w-32 overflow-hidden rounded-full bg-gray-100">
            <div className={`h-full ${cor}`} style={{ width: `${Math.min(pct, 100)}%` }} />
          </div>
          <span className={`text-xs ${pct > 100 ? 'font-semibold text-red-700' : 'text-gray-600'}`}>
            {pct}%
          </span>
        </div>
      </td>
      <td className="py-2 pr-3 text-right text-xs">{item.vagas}</td>
      <td className="py-2 pr-3 text-right text-xs">{item.agendados}</td>
      <td className={`py-2 text-right text-xs ${item.livres > 0 ? 'text-amber-700' : 'text-gray-400'}`}>
        {item.livres}
      </td>
      <td className="hidden py-2 pl-3 sm:table-cell">
        <div className="h-1.5 w-20 overflow-hidden rounded-full bg-gray-50" title="Tamanho relativo da oferta">
          <div className="h-full bg-primary-200" style={{ width: `${largura}%` }} />
        </div>
      </td>
    </tr>
  );
}

/**
 * Análise de vagas: onde a oferta está, quanto dela é usada, e onde sobra ou falta.
 *
 * <p>Separada da consulta porque as perguntas são outras: ali se procura "a agenda do Dr. Fulano
 * na sexta"; aqui se procura "quais especialidades estão ociosas" e "quem está sobrecarregado".</p>
 */
export function AgendaAnalisePage() {
  const [filtro, setFiltro] = useState<AgendaFiltro>({ de: diaBrasilia(), ate: diaBrasilia(30) });
  const [eixo, setEixo] = useState<(typeof EIXOS)[number]['id']>('unidade');

  const resumo = useResumoAgenda(filtro);
  const ranking = useRankingAgenda(filtro, eixo);
  const semana = useAgendaPorDiaSemana(filtro);
  const serie = useSerieAgenda(filtro);

  const maximo = Math.max(1, ...(ranking.data?.map((r) => r.vagas) ?? [1]));
  const maxSemana = Math.max(1, ...(semana.data?.map((d) => d.vagas) ?? [1]));

  return (
    <div className="space-y-5">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <BarChart3 className="h-6 w-6 text-primary-600" />
          Análise de vagas
        </h1>
        <p className="mt-1 max-w-4xl text-sm text-gray-600">
          Onde a oferta está concentrada, quanto dela é aproveitada, e onde sobra ou falta. Tudo no
          mesmo recorte da consulta — os números batem entre as duas telas porque saem da mesma
          definição de oferta e ocupação.
        </p>
      </header>

      <AvisoCobertura de={filtro.de} ate={filtro.ate} />

      <FiltroAgenda filtro={filtro} aoMudar={setFiltro} />

      <CartoesResumo resumo={resumo.data} carregando={resumo.isPending} />

      {/* Série diária: onde a oferta cai e onde a ocupação encosta no teto */}
      <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-gray-900">Oferta e ocupação dia a dia</h2>
        <p className="mb-3 text-xs text-gray-500">
          A área clara é a oferta publicada; a linha, a ocupação. Onde a linha encosta na área, a
          agenda lotou. Os vales são fim de semana e feriado — dias sem escala aparecem como zero de
          propósito, para que a queda seja visível em vez de o traço saltar por cima dela.
        </p>
        {serie.isPending ? (
          <Loader2 className="mx-auto my-10 h-5 w-5 animate-spin text-gray-400" />
        ) : (
          <ResponsiveContainer width="100%" height={260}>
            <ComposedChart
              data={(serie.data ?? []).map((d) => ({ ...d, rotulo: diaCurto(d.data) }))}
              margin={{ top: 4, right: 8, bottom: 8, left: 4 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
              <XAxis dataKey="rotulo" tick={{ fontSize: 10 }} interval="preserveStartEnd" minTickGap={20} />
              <YAxis tick={{ fontSize: 11 }} />
              <Tooltip contentStyle={{ fontSize: 12 }} />
              <Legend wrapperStyle={{ fontSize: 11 }} />
              <Area
                type="monotone"
                dataKey="vagas"
                name="Vagas ofertadas"
                stroke="#93c5fd"
                fill="#dbeafe"
              />
              <Line
                type="monotone"
                dataKey="agendados"
                name="Agendados"
                stroke="#b91c1c"
                strokeWidth={2}
                dot={false}
              />
            </ComposedChart>
          </ResponsiveContainer>
        )}
      </section>

      {/* Composição da oferta */}
      {resumo.data ? (
        <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="mb-1 text-sm font-semibold text-gray-900">Composição da oferta</h2>
          <p className="mb-3 text-xs text-gray-500">
            O SISREG separa a vaga em três tipos. A proporção entre eles diz que tipo de agenda a
            rede publica — muita <strong>reserva</strong> costuma ser vaga segurada para regulação,
            não oferta livre ao cidadão.
          </p>
          <div className="grid grid-cols-3 gap-3 text-center">
            {[
              ['Primeira vez', resumo.data.vagasPrimeiraVez],
              ['Retorno', resumo.data.vagasRetorno],
              ['Reserva', resumo.data.vagasReserva],
            ].map(([rotulo, valor]) => (
              <div key={String(rotulo)} className="rounded-lg border border-gray-100 p-3">
                <p className="text-xs text-gray-500">{rotulo}</p>
                <p className="mt-0.5 text-xl font-semibold text-gray-900">{valor}</p>
                <p className="text-[11px] text-gray-400">
                  {resumo.data!.vagas > 0
                    ? `${Math.round((Number(valor) / resumo.data!.vagas) * 100)}% da oferta`
                    : '—'}
                </p>
              </div>
            ))}
          </div>
          <p className="mt-3 text-xs text-gray-500">
            {resumo.data.profissionais} profissionais em {resumo.data.unidades} unidades publicaram
            escala neste período.
          </p>
        </section>
      ) : null}

      {/* Ranking */}
      <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-sm font-semibold text-gray-900">Ocupação</h2>
          <div className="flex gap-1">
            {EIXOS.map((e) => (
              <button
                key={e.id}
                type="button"
                onClick={() => setEixo(e.id)}
                className={`rounded-full px-3 py-1 text-xs ${
                  eixo === e.id ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-700'
                }`}
              >
                {e.rotulo}
              </button>
            ))}
          </div>
        </div>

        {ranking.isError ? (
          <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
            {extrairMensagemDeErro(ranking.error)}
          </p>
        ) : null}

        {ranking.isPending ? (
          <Loader2 className="mx-auto my-6 h-5 w-5 animate-spin text-gray-400" />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[42rem] text-left text-sm">
              <thead className="text-xs text-gray-500">
                <tr>
                  <th className="py-1 pr-3 font-medium">
                    {eixo === 'unidade' ? 'Unidade' : eixo === 'especialidade' ? 'Especialidade (CBO)' : 'Profissional'}
                  </th>
                  <th className="py-1 pr-3 font-medium">Ocupação</th>
                  <th className="py-1 pr-3 text-right font-medium">Vagas</th>
                  <th className="py-1 pr-3 text-right font-medium">Agendados</th>
                  <th className="py-1 text-right font-medium">Livres</th>
                  <th className="hidden py-1 pl-3 font-medium sm:table-cell">Oferta</th>
                </tr>
              </thead>
              <tbody>
                {ranking.data?.map((r) => (
                  <Linha key={r.chave} item={r} maximo={maximo} />
                ))}
              </tbody>
            </table>
            {ranking.data?.length === 0 ? (
              <p className="py-4 text-center text-xs text-gray-500">
                Nenhuma oferta neste recorte.
              </p>
            ) : null}
          </div>
        )}
      </section>

      {/* Dia da semana */}
      <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="mb-1 text-sm font-semibold text-gray-900">Distribuição pela semana</h2>
        <p className="mb-3 text-xs text-gray-500">
          Onde a rede concentra oferta. Dia com muita vaga e pouca ocupação é candidato a
          remanejamento; dia saturado, a reforço.
        </p>
        {semana.isPending ? (
          <Loader2 className="mx-auto my-6 h-5 w-5 animate-spin text-gray-400" />
        ) : (
          <div className="space-y-2">
            {semana.data?.map((d) => (
              <div key={d.diaSemana} className="flex items-center gap-3">
                <span className="w-20 shrink-0 text-xs text-gray-600">{d.rotulo}</span>
                <div className="relative h-5 flex-1 overflow-hidden rounded bg-gray-100">
                  <div
                    className="absolute inset-y-0 left-0 bg-primary-100"
                    style={{ width: `${Math.round((d.vagas / maxSemana) * 100)}%` }}
                    title={`${d.vagas} vagas`}
                  />
                  <div
                    className={`absolute inset-y-0 left-0 ${
                      d.ocupacaoPercentual > 100 ? 'bg-red-400' : 'bg-primary-500'
                    }`}
                    style={{ width: `${Math.round((d.agendados / maxSemana) * 100)}%` }}
                    title={`${d.agendados} agendados`}
                  />
                </div>
                <span className="w-32 shrink-0 text-right text-xs text-gray-500">
                  {d.agendados}/{d.vagas} · {d.ocupacaoPercentual}%
                </span>
              </div>
            ))}
          </div>
        )}
        <p className="mt-3 text-[11px] text-gray-400">
          A barra clara é a oferta; a escura, a ocupação. Escala relativa ao dia de maior oferta.
        </p>
      </section>
    </div>
  );
}
