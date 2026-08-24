import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, CalendarClock, Clock, Loader2, Plus, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante, hojeSP } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { BuscaPaciente } from '@/shared/ui/BuscaPaciente';
import { useListarTiposExame } from '@/features/tipos-exame/api/queries';
import {
  useAdicionarAvulso,
  useAdicionarBloqueio,
  useAdicionarRecorrencia,
  useAgenda,
  useAgendamentosDaAgenda,
  useAgendar,
  useDisponibilidades,
  useHorariosLivres,
  useRemoverAvulso,
  useRemoverBloqueio,
  useRemoverRecorrencia,
  useTransicaoAgendamento,
} from '@/features/agendamentos/api/queries';
import {
  DIAS_SEMANA,
  STATUS_LABEL,
  type DiaSemana,
  type SlotLivre,
  type StatusAgendamento,
} from '@/features/agendamentos/types';

const hoje = hojeSP;

function emDias(base: string, dias: number): string {
  const d = new Date(`${base}T00:00:00`);
  d.setDate(d.getDate() + dias);
  return d.toISOString().slice(0, 10);
}

function fmtDataHora(v: string): string {
  return formatarInstante(v);
}

const CORES_STATUS: Record<StatusAgendamento, string> = {
  Agendado: 'bg-blue-100 text-blue-700',
  Confirmado: 'bg-emerald-100 text-emerald-700',
  Realizado: 'bg-gray-200 text-gray-700',
  Cancelado: 'bg-red-100 text-red-700',
  Faltou: 'bg-amber-100 text-amber-800',
};

export function AgendaDetalhePage() {
  const { id = '' } = useParams();
  const agenda = useAgenda(id);

  const [inicio, setInicio] = useState(hoje());
  const [fim, setFim] = useState(emDias(hoje(), 7));

  const livres = useHorariosLivres(id, inicio, fim, Boolean(id));
  const agendamentos = useAgendamentosDaAgenda(id, inicio, fim);
  const disponibilidades = useDisponibilidades(id, inicio, fim);
  const tiposExame = useListarTiposExame();

  const addRecorrencia = useAdicionarRecorrencia(id);
  const delRecorrencia = useRemoverRecorrencia(id);
  const addBloqueio = useAdicionarBloqueio(id);
  const delBloqueio = useRemoverBloqueio(id);
  const addAvulso = useAdicionarAvulso(id);
  const delAvulso = useRemoverAvulso(id);
  const agendar = useAgendar(id);
  const trans = useTransicaoAgendamento(id);

  // Form recorrência
  const [rec, setRec] = useState({ diaSemana: 'Monday' as DiaSemana, horaInicio: '08:00', horaFim: '12:00' });
  // Form bloqueio / avulso
  const [bloqueio, setBloqueio] = useState({ inicioEm: '', fimEm: '', motivo: '' });
  const [avulso, setAvulso] = useState({ inicioEm: '', fimEm: '', motivo: '' });
  // Marcar consulta
  const [slotMarcar, setSlotMarcar] = useState<SlotLivre | null>(null);
  const [pacienteSel, setPacienteSel] = useState<{ id: string; nome: string } | null>(null);
  const [observacao, setObservacao] = useState('');
  const [tipoExameId, setTipoExameId] = useState('');
  const [erroMarcar, setErroMarcar] = useState<string | null>(null);

  if (agenda.isPending) {
    return (
      <div className="flex items-center gap-2 text-gray-500">
        <Loader2 className="h-4 w-4 animate-spin" /> Carregando agenda…
      </div>
    );
  }
  if (agenda.isError || !agenda.data) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(agenda.error)}
      </div>
    );
  }

  const a = agenda.data;

  function aoAddRecorrencia(e: React.FormEvent) {
    e.preventDefault();
    addRecorrencia.mutate(
      { diaSemana: rec.diaSemana, horaInicio: rec.horaInicio, horaFim: rec.horaFim },
      { onError: (err) => window.alert(extrairMensagemDeErro(err)) },
    );
  }

  function aoAddBloqueio(e: React.FormEvent) {
    e.preventDefault();
    if (!bloqueio.inicioEm || !bloqueio.fimEm || !bloqueio.motivo.trim()) return;
    addBloqueio.mutate(bloqueio, {
      onSuccess: () => setBloqueio({ inicioEm: '', fimEm: '', motivo: '' }),
      onError: (err) => window.alert(extrairMensagemDeErro(err)),
    });
  }

  function aoAddAvulso(e: React.FormEvent) {
    e.preventDefault();
    if (!avulso.inicioEm || !avulso.fimEm) return;
    addAvulso.mutate(
      { inicioEm: avulso.inicioEm, fimEm: avulso.fimEm, motivo: avulso.motivo || null },
      {
        onSuccess: () => setAvulso({ inicioEm: '', fimEm: '', motivo: '' }),
        onError: (err) => window.alert(extrairMensagemDeErro(err)),
      },
    );
  }

  function aoConfirmarMarcacao(e: React.FormEvent) {
    e.preventDefault();
    if (!slotMarcar || !pacienteSel) return;
    setErroMarcar(null);
    agendar.mutate(
      {
        agendaId: id,
        pacienteId: pacienteSel.id,
        inicioEm: slotMarcar.inicioEm,
        tipoExameId: a.finalidade === 'Exame' ? tipoExameId || null : null,
        observacao: observacao || null,
      },
      {
        onSuccess: () => {
          setSlotMarcar(null);
          setPacienteSel(null);
          setObservacao('');
        },
        onError: (err) => setErroMarcar(extrairMensagemDeErro(err)),
      },
    );
  }

  function aoCancelar(idAg: string) {
    const motivo = window.prompt('Motivo do cancelamento (opcional):') ?? undefined;
    trans.cancelar.mutate({ id: idAg, motivo });
  }

  return (
    <div className="space-y-6">
      <div>
        <Link to="/app/agendas" className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-800">
          <ArrowLeft className="h-4 w-4" /> Agendas
        </Link>
      </div>

      <header className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <h1 className="flex items-center gap-2 text-xl font-semibold text-gray-900">
          <CalendarClock className="h-5 w-5 text-primary-600" />
          {a.alvo}
        </h1>
        <div className="mt-2 grid grid-cols-2 gap-2 text-sm text-gray-600 sm:grid-cols-4">
          <div><span className="text-gray-400">Tipo:</span> {a.finalidade === 'Exame' ? 'Exame' : 'Consulta'}</div>
          <div><span className="text-gray-400">Unidade:</span> {a.unidadeNome}</div>
          <div><span className="text-gray-400">Slot:</span> {a.duracaoSlotMinutos} min</div>
          <div><span className="text-gray-400">Vigência:</span> {a.vigenciaInicio}{a.vigenciaFim ? ` → ${a.vigenciaFim}` : ''}</div>
        </div>
      </header>

      {/* ── Recorrências ─────────────────────────────────────────── */}
      <section className="space-y-3 rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-gray-900">Disponibilidade recorrente</h2>
        <ul className="space-y-1">
          {a.recorrencias.length === 0 ? (
            <li className="text-sm text-gray-500">Nenhuma regra. Adicione abaixo para gerar horários.</li>
          ) : (
            a.recorrencias.map((r) => (
              <li key={r.id} className="flex items-center justify-between rounded-md bg-gray-50 px-3 py-2 text-sm">
                <span>
                  {DIAS_SEMANA.find((d) => d.id === r.diaSemana)?.rotulo ?? r.diaSemana} · {r.horaInicio.slice(0, 5)}–{r.horaFim.slice(0, 5)}
                </span>
                <button
                  type="button"
                  onClick={() => delRecorrencia.mutate(r.id)}
                  className="text-red-600 hover:text-red-800"
                  title="Remover"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              </li>
            ))
          )}
        </ul>
        <form onSubmit={aoAddRecorrencia} className="flex flex-wrap items-end gap-3">
          <Campo label="Dia" htmlFor="rec-dia">
            <Select
              id="rec-dia"
              value={rec.diaSemana}
              onChange={(e) => setRec((s) => ({ ...s, diaSemana: e.target.value as DiaSemana }))}
            >
              {DIAS_SEMANA.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.rotulo}
                </option>
              ))}
            </Select>
          </Campo>
          <Campo label="Início" htmlFor="rec-ini">
            <Input id="rec-ini" type="time" value={rec.horaInicio} onChange={(e) => setRec((s) => ({ ...s, horaInicio: e.target.value }))} />
          </Campo>
          <Campo label="Fim" htmlFor="rec-fim">
            <Input id="rec-fim" type="time" value={rec.horaFim} onChange={(e) => setRec((s) => ({ ...s, horaFim: e.target.value }))} />
          </Campo>
          <Button type="submit" variante="outline" disabled={addRecorrencia.isPending}>
            <Plus className="mr-1 h-4 w-4" /> Adicionar
          </Button>
        </form>
      </section>

      {/* ── Intervalo ────────────────────────────────────────────── */}
      <section className="flex flex-wrap items-end gap-3 rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <Campo label="De" htmlFor="rng-ini">
          <Input id="rng-ini" type="date" value={inicio} onChange={(e) => setInicio(e.target.value)} />
        </Campo>
        <Campo label="Até" htmlFor="rng-fim">
          <Input id="rng-fim" type="date" value={fim} onChange={(e) => setFim(e.target.value)} />
        </Campo>
        <p className="text-sm text-gray-500">Define o período de horários livres, agendamentos e disponibilidades abaixo.</p>
      </section>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* ── Horários livres ────────────────────────────────────── */}
        <section className="space-y-3 rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
            <Clock className="h-5 w-5 text-primary-600" /> Horários livres
          </h2>
          {livres.isPending ? (
            <p className="text-sm text-gray-500">Calculando…</p>
          ) : (livres.data?.length ?? 0) === 0 ? (
            <p className="text-sm text-gray-500">Nenhum horário livre no período.</p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {livres.data!.map((s) => (
                <button
                  key={s.inicioEm}
                  type="button"
                  onClick={() => {
                    setSlotMarcar(s);
                    setPacienteSel(null);
                    setObservacao('');
                    setTipoExameId('');
                    setErroMarcar(null);
                  }}
                  className="rounded-md border border-primary-200 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
                >
                  {fmtDataHora(s.inicioEm)}
                </button>
              ))}
            </div>
          )}
        </section>

        {/* ── Agendamentos ───────────────────────────────────────── */}
        <section className="space-y-3 rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <h2 className="text-lg font-semibold text-gray-900">Agendamentos</h2>
          {agendamentos.isPending ? (
            <p className="text-sm text-gray-500">Carregando…</p>
          ) : (agendamentos.data?.length ?? 0) === 0 ? (
            <p className="text-sm text-gray-500">Nenhum agendamento no período.</p>
          ) : (
            <ul className="space-y-2">
              {agendamentos.data!.map((ag) => (
                <li key={ag.id} className="rounded-md border border-gray-100 bg-gray-50 px-3 py-2 text-sm">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium text-gray-900">{ag.pacienteNome}</span>
                    <span className={`rounded px-2 py-0.5 text-xs font-medium ${CORES_STATUS[ag.status]}`}>
                      {STATUS_LABEL[ag.status]}
                    </span>
                  </div>
                  <div className="mt-0.5 text-xs text-gray-500">{fmtDataHora(ag.inicioEm)}</div>
                  {ag.status === 'Agendado' || ag.status === 'Confirmado' ? (
                    <div className="mt-2 flex flex-wrap gap-2">
                      {ag.status === 'Agendado' ? (
                        <Button tamanho="sm" variante="outline" onClick={() => trans.confirmar.mutate(ag.id)}>
                          Confirmar
                        </Button>
                      ) : null}
                      <Button tamanho="sm" variante="outline" onClick={() => trans.realizar.mutate(ag.id)}>
                        Realizar
                      </Button>
                      <Button tamanho="sm" variante="ghost" onClick={() => trans.falta.mutate(ag.id)}>
                        Falta
                      </Button>
                      <Button tamanho="sm" variante="danger" onClick={() => aoCancelar(ag.id)}>
                        Cancelar
                      </Button>
                    </div>
                  ) : null}
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* ── Bloqueios ──────────────────────────────────────────── */}
        <section className="space-y-3 rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <h2 className="text-lg font-semibold text-gray-900">Bloqueios</h2>
          <ul className="space-y-1">
            {(disponibilidades.data?.bloqueios ?? []).map((b) => (
              <li key={b.id} className="flex items-center justify-between rounded-md bg-gray-50 px-3 py-2 text-sm">
                <span>
                  {fmtDataHora(b.inicioEm)} → {fmtDataHora(b.fimEm)} · {b.motivo}
                </span>
                <button type="button" onClick={() => delBloqueio.mutate(b.id)} className="text-red-600 hover:text-red-800">
                  <Trash2 className="h-4 w-4" />
                </button>
              </li>
            ))}
            {(disponibilidades.data?.bloqueios?.length ?? 0) === 0 ? (
              <li className="text-sm text-gray-500">Nenhum bloqueio no período.</li>
            ) : null}
          </ul>
          <form onSubmit={aoAddBloqueio} className="flex flex-wrap items-end gap-2">
            <Campo label="Início" htmlFor="blq-ini">
              <Input id="blq-ini" type="datetime-local" value={bloqueio.inicioEm} onChange={(e) => setBloqueio((s) => ({ ...s, inicioEm: e.target.value }))} />
            </Campo>
            <Campo label="Fim" htmlFor="blq-fim">
              <Input id="blq-fim" type="datetime-local" value={bloqueio.fimEm} onChange={(e) => setBloqueio((s) => ({ ...s, fimEm: e.target.value }))} />
            </Campo>
            <Campo label="Motivo" htmlFor="blq-mot">
              <Input id="blq-mot" value={bloqueio.motivo} onChange={(e) => setBloqueio((s) => ({ ...s, motivo: e.target.value }))} placeholder="Férias, feriado…" />
            </Campo>
            <Button type="submit" variante="outline" disabled={addBloqueio.isPending}>
              <Plus className="mr-1 h-4 w-4" /> Bloquear
            </Button>
          </form>
        </section>

        {/* ── Avulsos ────────────────────────────────────────────── */}
        <section className="space-y-3 rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <h2 className="text-lg font-semibold text-gray-900">Horários avulsos</h2>
          <ul className="space-y-1">
            {(disponibilidades.data?.avulsos ?? []).map((av) => (
              <li key={av.id} className="flex items-center justify-between rounded-md bg-gray-50 px-3 py-2 text-sm">
                <span>
                  {fmtDataHora(av.inicioEm)} → {fmtDataHora(av.fimEm)}
                  {av.motivo ? ` · ${av.motivo}` : ''}
                </span>
                <button type="button" onClick={() => delAvulso.mutate(av.id)} className="text-red-600 hover:text-red-800">
                  <Trash2 className="h-4 w-4" />
                </button>
              </li>
            ))}
            {(disponibilidades.data?.avulsos?.length ?? 0) === 0 ? (
              <li className="text-sm text-gray-500">Nenhum horário avulso no período.</li>
            ) : null}
          </ul>
          <form onSubmit={aoAddAvulso} className="flex flex-wrap items-end gap-2">
            <Campo label="Início" htmlFor="avl-ini">
              <Input id="avl-ini" type="datetime-local" value={avulso.inicioEm} onChange={(e) => setAvulso((s) => ({ ...s, inicioEm: e.target.value }))} />
            </Campo>
            <Campo label="Fim" htmlFor="avl-fim">
              <Input id="avl-fim" type="datetime-local" value={avulso.fimEm} onChange={(e) => setAvulso((s) => ({ ...s, fimEm: e.target.value }))} />
            </Campo>
            <Campo label="Motivo" htmlFor="avl-mot">
              <Input id="avl-mot" value={avulso.motivo} onChange={(e) => setAvulso((s) => ({ ...s, motivo: e.target.value }))} placeholder="Mutirão…" />
            </Campo>
            <Button type="submit" variante="outline" disabled={addAvulso.isPending}>
              <Plus className="mr-1 h-4 w-4" /> Adicionar
            </Button>
          </form>
        </section>
      </div>

      {/* ── Modal marcar consulta ──────────────────────────────── */}
      <Modal
        aberto={slotMarcar !== null}
        aoFechar={() => setSlotMarcar(null)}
        titulo="Marcar consulta"
        descricao={slotMarcar ? fmtDataHora(slotMarcar.inicioEm) : undefined}
      >
        <form onSubmit={aoConfirmarMarcacao} className="space-y-4">
          {pacienteSel ? (
            <div className="flex items-center justify-between rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-sm">
              <span className="font-medium text-gray-900">{pacienteSel.nome}</span>
              <button type="button" className="text-xs text-primary-700 hover:underline" onClick={() => setPacienteSel(null)}>
                Trocar
              </button>
            </div>
          ) : (
            <Campo label="Paciente" htmlFor="mk-paciente" required>
              <BuscaPaciente aoSelecionar={(p) => setPacienteSel({ id: p.id, nome: p.nomeCompleto })} />
            </Campo>
          )}

          {a.finalidade === 'Exame' ? (
            <Campo label="Tipo de exame" htmlFor="mk-tipo-exame">
              <Select id="mk-tipo-exame" value={tipoExameId} onChange={(e) => setTipoExameId(e.target.value)}>
                <option value="">Selecione…</option>
                {(tiposExame.data ?? []).map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.nome}
                  </option>
                ))}
              </Select>
            </Campo>
          ) : null}

          <Campo label="Observação" htmlFor="mk-obs" dica="Opcional.">
            <Input id="mk-obs" value={observacao} onChange={(e) => setObservacao(e.target.value)} />
          </Campo>

          {erroMarcar ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erroMarcar}</div>
          ) : null}

          <div className="flex items-center justify-end gap-3">
            <Button type="button" variante="secundaria" onClick={() => setSlotMarcar(null)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={!pacienteSel || agendar.isPending}>
              {agendar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Confirmar marcação
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
