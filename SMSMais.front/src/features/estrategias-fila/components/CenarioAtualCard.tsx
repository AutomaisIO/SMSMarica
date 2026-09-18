import { useState } from 'react';
import { ChevronDown, ChevronUp, Info } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, Line, ResponsiveContainer, Tooltip, XAxis, YAxis, ComposedChart } from 'recharts';
import { diaBr, diaCurto } from '@/features/agenda/lib/datasAgenda';
import { DIAS_CURTOS, n, pct } from '@/features/estrategias-fila/lib/parametros';
import type { CenarioFila } from '@/features/estrategias-fila/types';

function Cartao({
  rotulo,
  valor,
  dica,
  classe = 'text-gray-900',
}: {
  rotulo: string;
  valor: React.ReactNode;
  dica: string;
  classe?: string;
}) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-3 shadow-sm" title={dica}>
      <p className="text-[11px] text-gray-500">{rotulo}</p>
      <p className={`mt-0.5 text-xl font-semibold ${classe}`}>{valor}</p>
      <p className="mt-0.5 text-[10px] leading-tight text-gray-400">{dica}</p>
    </div>
  );
}

function classeEspera(dias: number | null) {
  if (dias === null) return 'text-gray-900';
  if (dias > 180) return 'text-red-700';
  if (dias > 90) return 'text-orange-600';
  if (dias > 30) return 'text-amber-600';
  return 'text-emerald-700';
}

const RISCO: Record<string, { rotulo: string; classe: string }> = {
  '0': { rotulo: 'Vermelho', classe: 'bg-red-600' },
  '1': { rotulo: 'Amarelo', classe: 'bg-amber-400' },
  '2': { rotulo: 'Verde', classe: 'bg-emerald-500' },
  '3': { rotulo: 'Azul', classe: 'bg-sky-500' },
  sem: { rotulo: 'Sem risco', classe: 'bg-gray-300' },
};

/**
 * O retrato de hoje: fila, ritmo de entrada × vazão, oferta publicada e quanto dela é usada.
 *
 * <p>É deliberadamente denso — é o subsídio que vai ao agente, e o gestor precisa ver o MESMO
 * que o modelo viu para julgar a proposta. O que o modelo não vê (a lista de pessoas) não
 * aparece aqui também.</p>
 */
export function CenarioAtualCard({ cenario: c }: { cenario: CenarioFila }) {
  const [mostrarProfissionais, setMostrarProfissionais] = useState(false);
  const o = c.oferta;
  const balanco = c.entrada.mediaSemanal12 - c.vazao.mediaSemanal12;

  const serie = c.entrada.serie.map((e, i) => ({
    rotulo: diaCurto(e.semana),
    entrada: e.quantidade,
    vazao: c.vazao.serie[i]?.quantidade ?? 0,
  }));

  return (
    <section className="space-y-3">
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-6">
        <Cartao rotulo="Na fila hoje" valor={n(c.fila.total)} dica="Pessoas esperando no SISREG (família inteira)." classe="text-red-700" />
        <Cartao
          rotulo="Espera mediana"
          valor={c.fila.esperaMedianaDias === null ? '—' : `${c.fila.esperaMedianaDias} d`}
          dica={`p90 ${c.fila.esperaP90Dias ?? '—'} d · máxima ${c.fila.esperaMaxDias ?? '—'} d · mais antigo ${diaBr(c.fila.maisAntigoEm)}`}
          classe={classeEspera(c.fila.esperaMedianaDias)}
        />
        <Cartao
          rotulo="Entram por semana"
          valor={n(c.entrada.mediaSemanal12, 1)}
          dica={`Média de 12 semanas (26 sem.: ${n(c.entrada.mediaSemanal26, 1)})${c.entrada.tendencia !== null ? ` · tendência ${n(c.entrada.tendencia, 2)}×` : ''}`}
        />
        <Cartao
          rotulo="Saem por semana"
          valor={n(c.vazao.mediaSemanal12, 1)}
          dica={`Marcações reais por semana (26 sem.: ${n(c.vazao.mediaSemanal26, 1)}).`}
          classe={balanco > 0 ? 'text-amber-700' : 'text-emerald-700'}
        />
        <Cartao
          rotulo="Vagas de regulação/sem"
          valor={n(o.vagasRegulacaoSemana)}
          dica={`1ª vez ${n(o.vagasPrimeiraVezSemana)} + reserva ${n(o.vagasReservaSemana)}; retorno ${n(o.vagasRetornoSemana)} fica com a unidade${o.vagasAgendaLocalSemana > 0 ? `; agenda local ${n(o.vagasAgendaLocalSemana)} fora da conta` : ''}.`}
        />
        <Cartao
          rotulo="Aproveitamento"
          valor={pct(c.ocupacao.aproveitamento)}
          dica={
            c.ocupacao.aproveitamento === null
              ? 'Sem oferta nas últimas 8 semanas — não medido.'
              : `${n(c.ocupacao.agendados)} marcações em ${n(c.ocupacao.vagasRegulacaoOfertadas)} vagas nas últimas ${c.ocupacao.semanasMedidas} semanas.`
          }
          classe={(c.ocupacao.aproveitamento ?? 1) < 0.5 ? 'text-amber-700' : 'text-gray-900'}
        />
      </div>

      <div className="grid gap-3 lg:grid-cols-3">
        {/* Entrada × vazão */}
        <div className="rounded-xl border border-gray-200 bg-white p-3 shadow-sm lg:col-span-2">
          <h3 className="text-xs font-semibold text-gray-900">Entrada × vazão, semana a semana</h3>
          <p className="mb-2 text-[11px] text-gray-500">
            Barras: quem pediu naquela semana. Linha: quem foi marcado naquela semana. Barra acima da linha = fila crescendo.
          </p>
          <ResponsiveContainer width="100%" height={170}>
            <ComposedChart data={serie} margin={{ top: 4, right: 8, bottom: 0, left: -12 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
              <XAxis dataKey="rotulo" tick={{ fontSize: 9 }} interval="preserveStartEnd" minTickGap={18} />
              <YAxis tick={{ fontSize: 10 }} />
              <Tooltip contentStyle={{ fontSize: 11 }} />
              <Bar dataKey="entrada" name="Pediram" fill="#fca5a5" radius={[2, 2, 0, 0]} />
              <Line type="monotone" dataKey="vazao" name="Marcados" stroke="#1d4ed8" strokeWidth={2} dot={false} />
            </ComposedChart>
          </ResponsiveContainer>
        </div>

        {/* Fila por risco e faixa */}
        <div className="rounded-xl border border-gray-200 bg-white p-3 shadow-sm">
          <h3 className="text-xs font-semibold text-gray-900">Quem espera</h3>
          <div className="mt-2 flex flex-wrap gap-1.5">
            {Object.entries(c.fila.porRisco)
              .sort(([a], [b]) => a.localeCompare(b))
              .map(([k, v]) => (
                <span key={k} className="inline-flex items-center gap-1 rounded-full bg-gray-50 px-2 py-0.5 text-[11px] text-gray-700">
                  <span className={`h-2 w-2 rounded-full ${RISCO[k]?.classe ?? 'bg-gray-300'}`} />
                  {RISCO[k]?.rotulo ?? k}: <strong>{n(v)}</strong>
                </span>
              ))}
          </div>
          <ResponsiveContainer width="100%" height={120}>
            <BarChart data={c.fila.faixas} margin={{ top: 8, right: 4, bottom: 0, left: -18 }}>
              <XAxis dataKey="rotulo" tick={{ fontSize: 8 }} interval={0} angle={-20} height={34} textAnchor="end" />
              <YAxis tick={{ fontSize: 9 }} />
              <Tooltip contentStyle={{ fontSize: 11 }} />
              <Bar dataKey="volume" name="Pessoas" fill="#ef4444" radius={[2, 2, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
          {c.saidaSemAgendarFracao !== null ? (
            <p className="mt-1 text-[10px] text-gray-500">
              De quem saiu da fila desde a carga, <strong>{pct(c.saidaSemAgendarFracao)}</strong> saiu sem agendar.
            </p>
          ) : null}
        </div>
      </div>

      {/* Oferta */}
      <div className="rounded-xl border border-gray-200 bg-white p-3 shadow-sm">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h3 className="text-xs font-semibold text-gray-900">Oferta vigente (regulada)</h3>
          <p className="text-[11px] text-gray-500">
            {o.profissionais.length} profissional(is) · {o.unidades.filter((u) => !u.agendaLocal).length} unidade(s) ·{' '}
            {o.diasSemana.map((d) => DIAS_CURTOS[d]).join(', ') || 'sem dia'}
            {o.horaInicioTipica ? ` · ${o.horaInicioTipica.slice(0, 5)}–${o.horaFimTipica?.slice(0, 5)}` : ''}
            {' · '}
            <strong>{n(o.turnosSemana, 1)} turnos/semana</strong> ({n(o.turnosPorProfissionalSemana, 1)} por profissional) ·{' '}
            <strong>{n(o.atendimentosPorTurno, 1)} atendimentos por turno</strong>
          </p>
        </div>

        <p className="mt-1 text-[10px] text-gray-400">
          Turno = um profissional num dia com escala. As horas de início e fim da escala do SISREG não são tempo de trabalho
          (há blocos de 5 minutos com dezenas de vagas), por isso não entram na conta — só a janela típica é mostrada.
        </p>

        {o.unidades.length === 0 ? (
          <p className="mt-2 flex items-center gap-1 text-xs text-amber-700">
            <Info className="h-3.5 w-3.5" /> Nenhuma escala vigente no SISREG para este procedimento: a fila cresce sem oferta.
          </p>
        ) : (
          <table className="mt-2 w-full text-xs">
            <thead>
              <tr className="text-left text-[10px] uppercase tracking-wide text-gray-500">
                <th className="py-1 pr-2">Unidade</th>
                <th className="py-1 pr-2">Agenda</th>
                <th className="py-1 pr-2 text-right">Profissionais</th>
                <th className="py-1 pr-2 text-right">Vagas reg./sem</th>
                <th className="py-1 text-right">Total/sem</th>
              </tr>
            </thead>
            <tbody>
              {o.unidades.map((u) => (
                <tr key={`${u.unidadeId}-${u.agendaLocal}`} className="border-t border-gray-100">
                  <td className="py-1 pr-2">{u.nome}</td>
                  <td className="py-1 pr-2">
                    <span className={`rounded-full px-1.5 py-0.5 text-[10px] ${u.agendaLocal ? 'bg-gray-100 text-gray-600' : 'bg-red-50 text-red-700'}`}>
                      {u.agendaLocal ? 'local' : 'regulada'}
                    </span>
                  </td>
                  <td className="py-1 pr-2 text-right">{u.profissionais}</td>
                  <td className="py-1 pr-2 text-right font-medium">{n(u.vagasRegulacaoSemana)}</td>
                  <td className="py-1 text-right text-gray-500">{n(u.vagasTotalSemana)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {o.profissionais.length > 0 ? (
          <div className="mt-2">
            <button
              type="button"
              onClick={() => setMostrarProfissionais((v) => !v)}
              className="inline-flex items-center gap-1 text-[11px] text-gray-600 hover:text-gray-900"
            >
              {mostrarProfissionais ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
              {mostrarProfissionais ? 'Esconder' : 'Ver'} os {o.profissionais.length} profissionais
            </button>
            {mostrarProfissionais ? (
              <table className="mt-1 w-full text-xs">
                <thead>
                  <tr className="text-left text-[10px] uppercase tracking-wide text-gray-500">
                    <th className="py-1 pr-2">Profissional</th>
                    <th className="py-1 pr-2">CBO</th>
                    <th className="py-1 pr-2">Unidade</th>
                    <th className="py-1 pr-2">Dias</th>
                    <th className="py-1 pr-2 text-right">Por turno</th>
                    <th className="py-1 pr-2 text-right">Vagas reg./sem</th>
                    <th className="py-1">Já ocupado em</th>
                  </tr>
                </thead>
                <tbody>
                  {o.profissionais.map((p) => (
                    <tr key={p.id} className="border-t border-gray-100">
                      <td className="py-1 pr-2">{p.nome}</td>
                      <td className="py-1 pr-2 text-gray-500">{p.cbo ?? '—'}</td>
                      <td className="py-1 pr-2">{p.unidade}</td>
                      <td className="py-1 pr-2">{p.dias.map((d) => DIAS_CURTOS[d]).join(', ')}</td>
                      <td className="py-1 pr-2 text-right">{n(p.atendimentosPorTurno, 1)}</td>
                      <td className="py-1 pr-2 text-right">{n(p.vagasRegulacaoSemana)}</td>
                      <td className="py-1 text-[10px] text-amber-800">
                        {Object.entries(p.outrasEscalas)
                          .map(([d, o]) => `${DIAS_CURTOS[Number(d)]}: ${o}`)
                          .join(' · ') || '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : null}
          </div>
        ) : null}
      </div>

      {c.procedimento.familia.length > 1 ? (
        <p className="text-[11px] text-gray-500">
          Família considerada na fila: {c.procedimento.familia.join(' · ')}
        </p>
      ) : null}
    </section>
  );
}
