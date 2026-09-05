import { useMemo, useState } from 'react';
import { CalendarClock, Loader2, X } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { useDiaAgenda, useDiasAgenda, useResumoAgenda } from '@/features/agenda/api/queries';
import { FiltroAgenda } from '@/features/agenda/components/FiltroAgenda';
import { CartoesResumo } from '@/features/agenda/components/CartoesResumo';
import { AvisoCobertura } from '@/features/agenda/components/AvisoCobertura';
import { diaBrasilia } from '@/features/agenda/lib/datasAgenda';
import type { AgendaDia, AgendaFiltro } from '@/features/agenda/api/agendaApi';

function dataBr(iso: string) {
  return new Date(`${iso}T12:00:00`).toLocaleDateString('pt-BR', { timeZone: 'America/Sao_Paulo' });
}

function diaDaSemana(iso: string) {
  return new Date(`${iso}T12:00:00`).toLocaleDateString('pt-BR', {
    weekday: 'short',
    timeZone: 'America/Sao_Paulo',
  });
}

function hora(t: string | null) {
  return t ? t.slice(0, 5) : '—';
}

/** Barra de ocupação: passa de 100% em vermelho, porque encaixe além da vaga não é sucesso. */
function Barra({ agendados, vagas }: { agendados: number; vagas: number }) {
  const pct = vagas > 0 ? Math.round((agendados / vagas) * 100) : agendados > 0 ? 999 : 0;
  const largura = Math.min(pct, 100);
  const cor = pct > 100 ? 'bg-red-500' : pct >= 80 ? 'bg-emerald-500' : pct >= 50 ? 'bg-amber-400' : 'bg-gray-300';
  return (
    <div className="flex items-center gap-2">
      <div className="h-2 w-24 overflow-hidden rounded-full bg-gray-100">
        <div className={`h-full ${cor}`} style={{ width: `${largura}%` }} />
      </div>
      <span className={`text-xs ${pct > 100 ? 'font-semibold text-red-700' : 'text-gray-500'}`}>
        {vagas > 0 ? `${pct}%` : 'sem escala'}
      </span>
    </div>
  );
}

/**
 * A Agenda: oferta publicada pelo SISREG × ocupação já importada, no grão
 * <b>unidade × profissional × dia</b>.
 *
 * <p>Cada linha é o dia de um profissional. Clicar abre o que foi publicado (os blocos da escala),
 * quem ocupa, e a grade de horários deduzida — que é dedução declarada, não dado do SISREG.</p>
 */
export function AgendaPage() {
  const [filtro, setFiltro] = useState<AgendaFiltro>({ de: diaBrasilia(), ate: diaBrasilia(30) });
  const [pagina, setPagina] = useState(0);
  const [aberto, setAberto] = useState<AgendaDia | null>(null);

  const resumo = useResumoAgenda(filtro);
  const dias = useDiasAgenda(filtro, pagina);
  const detalhe = useDiaAgenda(
    aberto?.unidadeId ?? null,
    aberto?.profissionalCpf ?? null,
    aberto?.data ?? null,
  );

  const totalPaginas = useMemo(
    () => Math.max(1, Math.ceil((dias.data?.total ?? 0) / 100)),
    [dias.data?.total],
  );

  function trocarFiltro(f: AgendaFiltro) {
    setFiltro(f);
    setPagina(0);
  }

  return (
    <div className="space-y-5">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <CalendarClock className="h-6 w-6 text-primary-600" />
          Agenda
        </h1>
        <p className="mt-1 max-w-4xl text-sm text-gray-600">
          A <strong>oferta</strong> que o SISREG publica (escalas) cruzada com a{' '}
          <strong>ocupação</strong> que já importamos. Cada linha é o dia de um profissional numa
          unidade — clique para ver os blocos publicados, quem ocupa e a grade de horários deduzida.
        </p>
      </header>

      <AvisoCobertura de={filtro.de} ate={filtro.ate} />

      <FiltroAgenda filtro={filtro} aoMudar={trocarFiltro} />

      <CartoesResumo resumo={resumo.data} carregando={resumo.isPending} />

      {dias.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(dias.error)}
        </div>
      ) : null}

      <section className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
        <table className="w-full min-w-[64rem] text-left text-sm">
          <thead className="border-b border-gray-200 text-xs text-gray-500">
            <tr>
              <th className="px-4 py-2 font-medium">Dia</th>
              <th className="px-4 py-2 font-medium">Unidade</th>
              <th className="px-4 py-2 font-medium">Profissional</th>
              <th className="px-4 py-2 font-medium">Especialidade</th>
              <th className="px-4 py-2 font-medium">Horário</th>
              <th className="px-4 py-2 font-medium">Vagas</th>
              <th className="px-4 py-2 font-medium">Agendados</th>
              <th className="px-4 py-2 font-medium">Livres</th>
              <th className="px-4 py-2 font-medium">Ocupação</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {dias.isPending ? (
              <tr>
                <td colSpan={9} className="px-4 py-6 text-center text-gray-400">
                  <Loader2 className="mx-auto h-5 w-5 animate-spin" />
                </td>
              </tr>
            ) : null}

            {dias.data?.itens.length === 0 ? (
              <tr>
                <td colSpan={9} className="px-4 py-6 text-center text-sm text-gray-500">
                  Nenhuma escala nem agendamento neste recorte. Se a rede acabou de ser sincronizada,
                  confira se o período escolhido tem escala vigente.
                </td>
              </tr>
            ) : null}

            {dias.data?.itens.map((d) => (
              <tr
                key={`${d.data}-${d.unidadeId}-${d.profissionalCpf}`}
                className="cursor-pointer hover:bg-gray-50"
                onClick={() => setAberto(d)}
              >
                <td className="px-4 py-2 whitespace-nowrap">
                  {dataBr(d.data)}{' '}
                  <span className="text-xs text-gray-400">{diaDaSemana(d.data)}</span>
                </td>
                <td className="px-4 py-2 text-xs">{d.unidadeNome}</td>
                <td className="px-4 py-2 text-xs font-medium">{d.profissionalNome}</td>
                <td className="px-4 py-2 text-xs text-gray-500">{d.cbo ?? '—'}</td>
                <td className="px-4 py-2 text-xs whitespace-nowrap">
                  {d.vagas > 0 ? `${hora(d.horaInicio)}–${hora(d.horaFim)}` : '—'}
                  {d.blocos > 1 ? (
                    <span className="ml-1 text-gray-400" title={`${d.blocos} blocos no dia`}>
                      ({d.blocos})
                    </span>
                  ) : null}
                </td>
                <td className="px-4 py-2">{d.vagas}</td>
                <td className="px-4 py-2">{d.agendados}</td>
                <td className={`px-4 py-2 ${d.livres === 0 && d.vagas > 0 ? 'text-gray-400' : ''}`}>
                  {d.vagas > 0 ? d.livres : '—'}
                </td>
                <td className="px-4 py-2">
                  <Barra agendados={d.agendados} vagas={d.vagas} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {(dias.data?.total ?? 0) > 100 ? (
        <div className="flex items-center justify-between text-sm">
          <span className="text-gray-500">
            {dias.data!.total} dias no recorte — página {pagina + 1} de {totalPaginas}
          </span>
          <div className="flex gap-2">
            <Button
              variante="outline"
              tamanho="sm"
              disabled={pagina === 0}
              onClick={() => setPagina((p) => p - 1)}
            >
              Anterior
            </Button>
            <Button
              variante="outline"
              tamanho="sm"
              disabled={pagina + 1 >= totalPaginas}
              onClick={() => setPagina((p) => p + 1)}
            >
              Próxima
            </Button>
          </div>
        </div>
      ) : null}

      {/* Detalhe do dia */}
      {aberto ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
          onClick={() => setAberto(null)}
        >
          <div
            className="max-h-[85vh] w-full max-w-4xl overflow-y-auto rounded-xl bg-white p-5 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-3 flex items-start justify-between">
              <div>
                <h2 className="text-lg font-semibold text-gray-900">{aberto.profissionalNome}</h2>
                <p className="text-sm text-gray-600">
                  {dataBr(aberto.data)} · {aberto.unidadeNome}
                  {aberto.cbo ? ` · ${aberto.cbo}` : ''}
                </p>
              </div>
              <Button variante="ghost" tamanho="sm" onClick={() => setAberto(null)}>
                <X className="h-4 w-4" />
              </Button>
            </div>

            {detalhe.isPending ? (
              <Loader2 className="mx-auto my-8 h-6 w-6 animate-spin text-gray-400" />
            ) : null}

            {detalhe.data ? (
              <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
                <div>
                  <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
                    O que o SISREG publicou
                  </h3>
                  {detalhe.data.blocos.length === 0 ? (
                    <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                      Nenhuma escala publicada para este profissional neste dia — mas há{' '}
                      {detalhe.data.agendados} agendamento(s). Vale conferir a grade no SISREG.
                    </p>
                  ) : (
                    <ul className="space-y-2">
                      {detalhe.data.blocos.map((b, i) => (
                        <li key={i} className="rounded-md border border-gray-200 p-2 text-xs">
                          <div className="flex items-center justify-between">
                            <span className="font-medium">
                              {hora(b.horaInicio)}–{hora(b.horaFim)}
                            </span>
                            <span className="text-gray-500">
                              {b.vagasPrimeiraVez + b.vagasRetorno + b.vagasReserva} vagas
                            </span>
                          </div>
                          <p className="mt-0.5 text-gray-600">{b.procedimentoNome}</p>
                          <p className="mt-0.5 text-[11px] text-gray-400">
                            1ª vez {b.vagasPrimeiraVez} · retorno {b.vagasRetorno} · reserva{' '}
                            {b.vagasReserva}
                            {b.minutosPorVagaEstimado ? (
                              <>
                                {' '}
                                ·{' '}
                                <span title="Duração ÷ vagas. Dedução nossa: o SISREG manda os minutos zerados em boa parte das linhas.">
                                  ~{b.minutosPorVagaEstimado} min por vaga (estimado)
                                </span>
                              </>
                            ) : null}
                          </p>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>

                <div>
                  <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
                    Quem ocupa ({detalhe.data.ocupantes.length})
                  </h3>
                  {detalhe.data.ocupantes.length === 0 ? (
                    <p className="text-xs text-gray-500">
                      Nenhum agendamento importado para este dia.
                    </p>
                  ) : (
                    <ul className="space-y-1">
                      {detalhe.data.ocupantes.map((o) => (
                        <li
                          key={o.solicitacaoId}
                          className="flex items-start justify-between gap-2 rounded-md border border-gray-100 px-2 py-1.5 text-xs"
                        >
                          <div>
                            <span className="font-medium">
                              {new Date(o.dataAgendadaUtc).toLocaleTimeString('pt-BR', {
                                hour: '2-digit',
                                minute: '2-digit',
                                timeZone: 'America/Sao_Paulo',
                              })}
                            </span>
                            <span className="ml-2 text-gray-600">{o.procedimentoTexto ?? '—'}</span>
                            {o.unidadeSolicitanteNome ? (
                              <p className="text-[11px] text-gray-400">
                                solicitado por {o.unidadeSolicitanteNome}
                              </p>
                            ) : null}
                          </div>
                          <span className="shrink-0 text-[11px] text-gray-400">
                            {o.codigoSolicitacao ?? ''}
                          </span>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              </div>
            ) : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}
