import { useState } from 'react';
import { AlertTriangle, CalendarDays, ChevronDown, ChevronRight, Home, Loader2 } from 'lucide-react';
import { useDatasDaOferta } from '../api/queries';
import type { DiaDaOferta, UnidadeDaOferta } from '../types';

const DIAS = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'];

/** "qua 06/11" — o dia da semana ajuda a reconhecer a agenda ("a terça do Dr. Fulano"). */
function dia(iso: string) {
  const [a, m, d] = iso.slice(0, 10).split('-').map(Number);
  return `${DIAS[new Date(a, m - 1, d).getDay()]} ${String(d).padStart(2, '0')}/${String(m).padStart(2, '0')}`;
}

function hora(h: string) {
  return h.slice(0, 5);
}

function Dia({ d }: { d: DiaDaOferta }) {
  const lotado = d.livres === 0;
  return (
    <li
      className={`rounded-md border px-2 py-1.5 text-xs ${
        lotado ? 'border-gray-200 bg-gray-50 text-gray-400' : 'border-emerald-200 bg-emerald-50/60 text-gray-800'
      }`}
      title={d.profissionais.join(', ')}
    >
      <p className="font-medium">{dia(d.data)}</p>
      <p className="text-[11px]">
        {hora(d.horaInicio)}–{hora(d.horaFim)}
      </p>
      <p className={`text-[11px] ${lotado ? '' : 'font-semibold text-emerald-700'}`}>
        {lotado ? 'lotado' : `${d.livres} de ${d.vagas} livres`}
      </p>
    </li>
  );
}

function Unidade({ u, abertaDeInicio }: { u: UnidadeDaOferta; abertaDeInicio: boolean }) {
  const [aberta, setAberta] = useState(abertaDeInicio);
  const [comLotados, setComLotados] = useState(false);
  const visiveis = comLotados ? u.dias : u.dias.filter((d) => d.livres > 0);
  const lotados = u.dias.length - u.dias.filter((d) => d.livres > 0).length;

  return (
    <li className="rounded-lg border border-gray-200 bg-white">
      <button
        type="button"
        onClick={() => setAberta((v) => !v)}
        className="flex w-full items-start justify-between gap-3 px-3 py-2 text-left hover:bg-gray-50"
      >
        <div className="flex min-w-0 items-start gap-2">
          {aberta ? (
            <ChevronDown className="mt-0.5 h-4 w-4 shrink-0 text-gray-400" />
          ) : (
            <ChevronRight className="mt-0.5 h-4 w-4 shrink-0 text-gray-400" />
          )}
          <div className="min-w-0">
            <p className="truncate text-sm font-medium text-gray-900">{u.unidadeNome}</p>
            <div className="mt-0.5 flex flex-wrap gap-1.5">
              {u.agendaLocal ? (
                <span className="inline-flex items-center gap-1 rounded bg-gray-100 px-1.5 py-0.5 text-[11px] text-gray-600">
                  <Home className="h-3 w-3" />
                  agenda local
                </span>
              ) : null}
              {u.semAgendamentoFuturo ? (
                <span className="inline-flex items-center gap-1 rounded bg-amber-100 px-1.5 py-0.5 text-[11px] text-amber-800">
                  <AlertTriangle className="h-3 w-3" />
                  vagas não confirmadas
                </span>
              ) : null}
            </div>
          </div>
        </div>
        <div className="shrink-0 text-right">
          {u.primeiraVagaLivre ? (
            <>
              <p className="text-sm font-semibold text-gray-900">{dia(u.primeiraVagaLivre)}</p>
              <p className="text-[11px] text-gray-500">{u.vagasLivres} livres no período</p>
            </>
          ) : (
            <p className="text-xs text-gray-500">sem vaga livre no período</p>
          )}
        </div>
      </button>

      {aberta ? (
        <div className="border-t border-gray-100 px-3 py-2">
          {u.semAgendamentoFuturo ? (
            <p className="mb-2 rounded-md bg-amber-50 p-2 text-xs text-amber-800">
              A escala do SISREG declara estas vagas, mas <strong>não há nenhum agendamento futuro</strong>{' '}
              nelas há mais de um mês. O mais provável é que o SISREG não esteja ofertando (bloqueio
              que a escala não mostra). Confirme no SISREG antes de contar com elas.
            </p>
          ) : null}
          {u.agendaLocal ? (
            <p className="mb-2 text-xs text-gray-600">
              Agenda local: a própria unidade marca nestas vagas — elas não passam pela regulação.
            </p>
          ) : null}
          {visiveis.length > 0 ? (
            <ul className="grid grid-cols-3 gap-1.5 sm:grid-cols-5 lg:grid-cols-7">
              {visiveis.map((d) => (
                <Dia key={d.data} d={d} />
              ))}
            </ul>
          ) : (
            <p className="text-xs text-gray-500">Todos os dias do período estão lotados.</p>
          )}
          {lotados > 0 ? (
            <button
              type="button"
              onClick={() => setComLotados((v) => !v)}
              className="mt-2 text-[11px] text-gray-500 underline-offset-2 hover:text-gray-800 hover:underline"
            >
              {comLotados ? 'esconder os dias lotados' : `mostrar também os ${lotados} dia(s) lotados`}
            </button>
          ) : null}
        </div>
      ) : null}
    </li>
  );
}

/**
 * Quando dá para marcar — por unidade executante, dia a dia.
 *
 * <p>É o que o SISREG mostra depois de "Autorizado": as unidades que executam e, em cada uma, as
 * datas. Existe porque a tela mostrava a VIGÊNCIA da escala ("01/07/25 a 31/12/26") e a reguladora
 * leu aquilo como período com vaga — no SISREG o eletrocardiograma só tinha vaga em novembro.</p>
 *
 * <p>Regulada primeiro; agenda local à parte, porque o regulador não marca nela.</p>
 */
export function DatasDaOfertaPainel({ codigo }: { codigo: string }) {
  const { data, isLoading, isError } = useDatasDaOferta(codigo);
  const [verLocais, setVerLocais] = useState(false);

  const reguladas = data?.unidades.filter((u) => !u.agendaLocal) ?? [];
  const locais = data?.unidades.filter((u) => u.agendaLocal) ?? [];
  const confiavel = reguladas.find((u) => !u.semAgendamentoFuturo && u.primeiraVagaLivre);
  const primeira = reguladas
    .filter((u) => !u.semAgendamentoFuturo && u.primeiraVagaLivre)
    .sort((a, b) => a.primeiraVagaLivre!.localeCompare(b.primeiraVagaLivre!))[0] ?? confiavel;

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
        <CalendarDays className="h-4 w-4 text-primary-600" />
        Quando dá para marcar
      </h2>

      {isLoading ? (
        <p className="flex items-center gap-2 py-4 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" />
          Montando as datas…
        </p>
      ) : null}
      {isError ? (
        <p className="mt-2 rounded-md bg-red-50 p-3 text-sm text-red-700">Não foi possível carregar as datas.</p>
      ) : null}

      {data ? (
        <>
          <p className="mt-1 text-xs text-gray-600">
            {primeira ? (
              <>
                Primeira vaga livre pela regulação:{' '}
                <strong className="text-gray-900">{dia(primeira.primeiraVagaLivre!)}</strong> em{' '}
                {primeira.unidadeNome}.
              </>
            ) : (
              <>Nenhuma vaga livre pela regulação até {dia(data.ate)}.</>
            )}
          </p>

          {reguladas.length > 0 ? (
            <ul className="mt-3 space-y-2">
              {reguladas.map((u, i) => (
                <Unidade key={`${u.unidadeId}-${u.agendaLocal}`} u={u} abertaDeInicio={i === 0} />
              ))}
            </ul>
          ) : (
            <p className="mt-3 rounded-lg border border-dashed border-gray-200 p-4 text-center text-sm text-gray-500">
              Nenhuma unidade regulada tem escala vigente para este procedimento.
            </p>
          )}

          {locais.length > 0 ? (
            <div className="mt-3">
              <button
                type="button"
                onClick={() => setVerLocais((v) => !v)}
                className="flex items-center gap-1 text-xs text-gray-600 hover:text-gray-900"
              >
                {verLocais ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
                Agenda local de {locais.length} unidade(s) — fora da regulação
              </button>
              {verLocais ? (
                <ul className="mt-2 space-y-2">
                  {locais.map((u) => (
                    <Unidade key={`${u.unidadeId}-${u.agendaLocal}`} u={u} abertaDeInicio={false} />
                  ))}
                </ul>
              ) : null}
            </div>
          ) : null}

          <p className="mt-3 border-t border-gray-100 pt-2 text-[11px] text-gray-500">
            Até {dia(data.ate)}. Contamos as vagas de <strong>1ª vez e de reserva</strong> (retorno fica com
            a unidade) e descontamos os
            agendamentos que já importamos — é uma estimativa. A grade do SISREG, que aparece ao
            autorizar, é a verdade.
          </p>
        </>
      ) : null}
    </section>
  );
}
