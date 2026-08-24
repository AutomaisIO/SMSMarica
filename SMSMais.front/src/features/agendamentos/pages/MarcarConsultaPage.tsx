import { useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { CalendarPlus, CheckCircle2, Loader2, Search, Stethoscope, User } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { BuscaPaciente } from '@/shared/ui/BuscaPaciente';
import { useListarEspecialidades } from '@/features/especialidades/api/queries';
import { useListarUnidades } from '@/features/unidades/api/queries';
import { useHorariosPorEspecialidade } from '@/features/agendamentos/api/queries';
import { agendar } from '@/features/agendamentos/api/agendamentosApi';
import type { SlotEspecialidade } from '@/features/agendamentos/types';
import { TZ_BR } from '@/shared/lib/datas';

/** Data de um instante como aaaa-mm-dd em Brasília (não no fuso do browser). */
function dataIso(d: Date): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: TZ_BR, year: 'numeric', month: '2-digit', day: '2-digit',
  }).format(d);
}

function hoje(): string {
  return dataIso(new Date());
}

function emDias(n: number): string {
  const d = new Date();
  d.setDate(d.getDate() + n);
  return dataIso(d);
}

function rotuloDia(iso: string): string {
  const d = new Date(`${iso}T12:00:00`);
  return d.toLocaleDateString('pt-BR', { weekday: 'short', day: '2-digit', month: '2-digit' });
}

function hora(dt: string): string {
  return dt.slice(11, 16);
}

type MedicoComSlots = {
  chave: string;
  medicoNome: string;
  unidades: string[];
  /** Slots agrupados por dia (ISO yyyy-MM-dd), em ordem. */
  porDia: [string, SlotEspecialidade[]][];
  totalSlots: number;
};

/**
 * Marcação guiada de consulta (padrão de mercado): seleciona a ESPECIALIDADE →
 * vê os MÉDICOS dela com seus horários livres → escolhe o horário → identifica o
 * paciente → confirma. A agenda é sempre do profissional (ADR-0013 revisado).
 */
export function MarcarConsultaPage() {
  const queryClient = useQueryClient();
  const especialidades = useListarEspecialidades();
  const unidades = useListarUnidades();

  const [especialidadeId, setEspecialidadeId] = useState('');
  const [unidadeId, setUnidadeId] = useState('');
  const [de, setDe] = useState(hoje());
  const [ate, setAte] = useState(emDias(14));

  const slots = useHorariosPorEspecialidade(especialidadeId, unidadeId || undefined, de, ate);

  // Confirmação da marcação
  const [slotEscolhido, setSlotEscolhido] = useState<SlotEspecialidade | null>(null);
  const [pacienteSel, setPacienteSel] = useState<{ id: string; nome: string } | null>(null);
  const [observacao, setObservacao] = useState('');
  const [erroMarcar, setErroMarcar] = useState<string | null>(null);
  const [marcando, setMarcando] = useState(false);
  const [sucesso, setSucesso] = useState<string | null>(null);

  const medicos = useMemo<MedicoComSlots[]>(() => {
    const grupos = new Map<string, SlotEspecialidade[]>();
    for (const s of slots.data ?? []) {
      const chave = s.medicoId ?? 'equipe';
      const lista = grupos.get(chave) ?? [];
      lista.push(s);
      grupos.set(chave, lista);
    }
    return [...grupos.entries()].map(([chave, lista]) => {
      const porDia = new Map<string, SlotEspecialidade[]>();
      for (const s of lista) {
        const dia = s.inicioEm.slice(0, 10);
        const doDia = porDia.get(dia) ?? [];
        doDia.push(s);
        porDia.set(dia, doDia);
      }
      return {
        chave,
        medicoNome: lista[0]?.medicoNome ?? 'Equipe da especialidade',
        unidades: [...new Set(lista.map((s) => s.unidadeNome))],
        porDia: [...porDia.entries()].sort(([a], [b]) => a.localeCompare(b)),
        totalSlots: lista.length,
      };
    });
  }, [slots.data]);

  function abrirConfirmacao(slot: SlotEspecialidade) {
    setSlotEscolhido(slot);
    setPacienteSel(null);
    setObservacao('');
    setErroMarcar(null);
    setSucesso(null);
  }

  async function aoConfirmar() {
    if (!slotEscolhido || !pacienteSel) return;
    setErroMarcar(null);
    setMarcando(true);
    try {
      await agendar({
        agendaId: slotEscolhido.agendaId,
        pacienteId: pacienteSel.id,
        inicioEm: slotEscolhido.inicioEm,
        observacao: observacao || null,
      });
      setSucesso(
        `Consulta marcada: ${pacienteSel.nome} com ${slotEscolhido.medicoNome ?? 'a equipe'} em ` +
          `${rotuloDia(slotEscolhido.inicioEm.slice(0, 10))} às ${hora(slotEscolhido.inicioEm)} (${slotEscolhido.unidadeNome}).`,
      );
      setSlotEscolhido(null);
      queryClient.invalidateQueries({ queryKey: ['agendamentos', 'livres-especialidade'] });
      queryClient.invalidateQueries({ queryKey: ['agendas'] });
    } catch (e) {
      setErroMarcar(extrairMensagemDeErro(e));
    } finally {
      setMarcando(false);
    }
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <CalendarPlus className="h-6 w-6 text-primary-600" />
          Marcar consulta
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Escolha a especialidade para ver os médicos e horários disponíveis; depois selecione o
          horário e identifique o paciente.
        </p>
      </header>

      {/* Passo 1 — filtros */}
      <div className="grid grid-cols-1 gap-4 rounded-lg border border-gray-200 bg-white p-4 shadow-sm sm:grid-cols-2 lg:grid-cols-4">
        <Campo label="Especialidade" htmlFor="mc-especialidade" required>
          <Select id="mc-especialidade" value={especialidadeId} onChange={(e) => setEspecialidadeId(e.target.value)}>
            <option value="">Selecione…</option>
            {(especialidades.data ?? []).map((esp) => (
              <option key={esp.id} value={esp.id}>
                {esp.nome}
              </option>
            ))}
          </Select>
        </Campo>
        <Campo label="Unidade" htmlFor="mc-unidade" dica="Opcional — todas se vazio.">
          <Select id="mc-unidade" value={unidadeId} onChange={(e) => setUnidadeId(e.target.value)}>
            <option value="">Todas</option>
            {(unidades.data ?? [])
              .filter((u) => u.ativo)
              .map((u) => (
                <option key={u.id} value={u.id}>
                  {u.nome}
                </option>
              ))}
          </Select>
        </Campo>
        <Campo label="De" htmlFor="mc-de">
          <Input id="mc-de" type="date" value={de} onChange={(e) => setDe(e.target.value)} />
        </Campo>
        <Campo label="Até" htmlFor="mc-ate">
          <Input id="mc-ate" type="date" value={ate} onChange={(e) => setAte(e.target.value)} />
        </Campo>
      </div>

      {sucesso ? (
        <div className="flex items-center gap-2 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">
          <CheckCircle2 className="h-4 w-4 flex-shrink-0" />
          {sucesso}
        </div>
      ) : null}

      {/* Passo 2 — médicos da especialidade com horários */}
      {!especialidadeId ? (
        <div className="flex items-center justify-center rounded-lg border border-dashed border-gray-300 bg-gray-50 py-12 text-sm text-gray-500">
          <Search className="mr-2 h-4 w-4" />
          Selecione a especialidade para listar os médicos e horários.
        </div>
      ) : slots.isPending ? (
        <div className="flex items-center justify-center rounded-lg border border-gray-200 bg-white py-12 text-gray-500">
          <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Buscando horários…
        </div>
      ) : slots.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(slots.error)}
        </div>
      ) : medicos.length === 0 ? (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-6 text-center text-sm text-amber-800">
          Nenhum horário livre nesta especialidade no período. Verifique as agendas dos
          profissionais (grade, vigência e bloqueios) ou amplie o período.
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {medicos.map((m) => (
            <div key={m.chave} className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <h3 className="flex items-center gap-2 font-semibold text-gray-900">
                    <Stethoscope className="h-4 w-4 text-primary-600" />
                    {m.medicoNome}
                  </h3>
                  <p className="text-xs text-gray-500">{m.unidades.join(' · ')}</p>
                </div>
                <span className="rounded-full bg-primary-50 px-2 py-0.5 text-xs font-medium text-primary-700">
                  {m.totalSlots} horário{m.totalSlots === 1 ? '' : 's'}
                </span>
              </div>
              <div className="mt-3 max-h-64 space-y-2 overflow-y-auto pr-1">
                {m.porDia.map(([dia, doDia]) => (
                  <div key={dia}>
                    <p className="text-xs font-semibold uppercase text-gray-500">{rotuloDia(dia)}</p>
                    <div className="mt-1 flex flex-wrap gap-1.5">
                      {doDia.map((s) => (
                        <button
                          key={`${s.agendaId}-${s.inicioEm}`}
                          type="button"
                          onClick={() => abrirConfirmacao(s)}
                          title={`${s.unidadeNome} — marcar ${hora(s.inicioEm)}`}
                          className="rounded-md border border-primary-200 bg-primary-50 px-2 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
                        >
                          {hora(s.inicioEm)}
                        </button>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Passo 3 — confirmar paciente + horário */}
      <Modal
        aberto={Boolean(slotEscolhido)}
        aoFechar={() => setSlotEscolhido(null)}
        titulo="Confirmar agendamento"
        largura="md"
      >
        {slotEscolhido ? (
          <div className="space-y-4">
            <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-sm">
              <p>
                <span className="text-gray-500">Médico:</span>{' '}
                <strong>{slotEscolhido.medicoNome ?? 'Equipe da especialidade'}</strong>
              </p>
              <p>
                <span className="text-gray-500">Quando:</span>{' '}
                <strong>
                  {rotuloDia(slotEscolhido.inicioEm.slice(0, 10))} às {hora(slotEscolhido.inicioEm)}
                </strong>
              </p>
              <p>
                <span className="text-gray-500">Unidade:</span> {slotEscolhido.unidadeNome}
              </p>
            </div>

            <Campo label="Paciente" htmlFor="mc-paciente" required>
              {pacienteSel ? (
                <div className="flex items-center justify-between rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-sm">
                  <span className="flex items-center gap-1.5 font-medium text-gray-900">
                    <User className="h-4 w-4 text-gray-400" />
                    {pacienteSel.nome}
                  </span>
                  <button
                    type="button"
                    className="text-xs text-primary-700 hover:underline"
                    onClick={() => setPacienteSel(null)}
                  >
                    Trocar
                  </button>
                </div>
              ) : (
                <BuscaPaciente aoSelecionar={(p) => setPacienteSel({ id: p.id, nome: p.nomeCompleto })} />
              )}
            </Campo>

            <Campo label="Observação" htmlFor="mc-obs" dica="Opcional.">
              <Input id="mc-obs" value={observacao} onChange={(e) => setObservacao(e.target.value)} />
            </Campo>

            {erroMarcar ? (
              <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {erroMarcar}
              </div>
            ) : null}

            <div className="flex items-center justify-end gap-3 pt-2">
              <Button type="button" variante="secundaria" onClick={() => setSlotEscolhido(null)}>
                Cancelar
              </Button>
              <Button type="button" onClick={aoConfirmar} disabled={!pacienteSel || marcando}>
                {marcando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CheckCircle2 className="mr-2 h-4 w-4" />}
                Confirmar agendamento
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}
