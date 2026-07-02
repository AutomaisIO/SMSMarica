import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { CalendarClock, Loader2, Plus } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { hojeSP } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useListarUnidades } from '@/features/unidades/api/queries';
import { useListarEspecialidades } from '@/features/especialidades/api/queries';
import { useListarEquipamentos } from '@/features/equipamentos/api/queries';
import { BuscaMedico } from '@/features/agendamentos/components/BuscaMedico';
import { GradeSemanalEditor } from '@/features/agendamentos/components/GradeSemanalEditor';
import { useCadastrarAgenda, useListarAgendas } from '@/features/agendamentos/api/queries';
import {
  TIPOS_AGENDA,
  type AdicionarRecorrenciaPayload,
  type AgendaListItem,
  type CadastrarAgendaPayload,
  type TipoAgenda,
} from '@/features/agendamentos/types';

const hoje = hojeSP;

type FormAgenda = {
  tipoAgenda: TipoAgenda;
  unidadeId: string;
  especialidadeId: string;
  medicoId: string;
  medicoNome: string;
  equipamentoId: string;
  duracaoSlotMinutos: number;
  vigenciaInicio: string;
  vigenciaFim: string;
};

const FORM_VAZIO: FormAgenda = {
  tipoAgenda: 'ConsultaMedico',
  unidadeId: '',
  especialidadeId: '',
  medicoId: '',
  medicoNome: '',
  equipamentoId: '',
  duracaoSlotMinutos: 20,
  vigenciaInicio: hoje(),
  vigenciaFim: '',
};

/** Grade padrão sugerida: seg–sex, 08:00–12:00 (o usuário ajusta no editor). */
const GRADE_PADRAO: AdicionarRecorrenciaPayload[] = (
  ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'] as const
).map((dia) => ({ diaSemana: dia, horaInicio: '08:00', horaFim: '12:00' }));

export function AgendasPage() {
  const navigate = useNavigate();
  const [incluirInativas, setIncluirInativas] = useState(false);
  const agendas = useListarAgendas({ incluirInativas });
  const unidades = useListarUnidades();
  const especialidades = useListarEspecialidades();
  const equipamentos = useListarEquipamentos();
  const cadastrar = useCadastrarAgenda();

  const [modalAberto, setModalAberto] = useState(false);
  const [form, setForm] = useState<FormAgenda>(FORM_VAZIO);
  const [faixas, setFaixas] = useState<AdicionarRecorrenciaPayload[]>(GRADE_PADRAO);
  const [erro, setErro] = useState<string | null>(null);

  function abrirNova() {
    setForm(FORM_VAZIO);
    setFaixas(GRADE_PADRAO);
    setErro(null);
    setModalAberto(true);
  }

  function set<K extends keyof FormAgenda>(chave: K, valor: FormAgenda[K]) {
    setForm((f) => ({ ...f, [chave]: valor }));
  }

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (!form.unidadeId) {
      setErro('Selecione a unidade.');
      return;
    }

    if (faixas.length === 0) {
      setErro('Defina a grade semanal: pelo menos uma faixa de atendimento.');
      return;
    }
    if (faixas.some((f) => !f.horaInicio || !f.horaFim || f.horaFim <= f.horaInicio)) {
      setErro('Há faixa de horário inválida na grade (fim deve ser maior que o início).');
      return;
    }

    let payload: CadastrarAgendaPayload;
    if (form.tipoAgenda === 'Exame') {
      if (!form.equipamentoId) {
        setErro('Selecione o equipamento.');
        return;
      }
      payload = {
        finalidade: 'Exame',
        unidadeId: form.unidadeId,
        equipamentoId: form.equipamentoId,
        duracaoSlotMinutos: form.duracaoSlotMinutos,
        vigenciaInicio: form.vigenciaInicio,
        vigenciaFim: form.vigenciaFim || null,
        recorrencias: faixas,
      };
    } else {
      if (!form.especialidadeId) {
        setErro('Selecione a especialidade.');
        return;
      }
      if (!form.medicoId) {
        setErro('Selecione o médico — a agenda é do profissional.');
        return;
      }
      payload = {
        finalidade: 'Consulta',
        unidadeId: form.unidadeId,
        especialidadeId: form.especialidadeId,
        medicoId: form.medicoId,
        duracaoSlotMinutos: form.duracaoSlotMinutos,
        vigenciaInicio: form.vigenciaInicio,
        vigenciaFim: form.vigenciaFim || null,
        recorrencias: faixas,
      };
    }

    cadastrar.mutate(payload, {
      onSuccess: (id) => {
        setModalAberto(false);
        navigate(`/app/agendas/${id}`);
      },
      onError: (err) => setErro(extrairMensagemDeErro(err)),
    });
  }

  const colunas: Coluna<AgendaListItem>[] = [
    { chave: 'alvo', cabecalho: 'Agenda', render: (a) => <span className="font-medium text-gray-900">{a.alvo}</span> },
    {
      chave: 'finalidade',
      cabecalho: 'Tipo',
      render: (a) => (
        <span className="rounded bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-700">
          {a.finalidade === 'Exame' ? 'Exame' : 'Consulta'}
        </span>
      ),
    },
    { chave: 'unidade', cabecalho: 'Unidade', render: (a) => a.unidadeNome },
    { chave: 'slot', cabecalho: 'Slot', render: (a) => `${a.duracaoSlotMinutos} min` },
    { chave: 'status', cabecalho: 'Status', render: (a) => <StatusBadge ativo={a.ativo} /> },
    {
      chave: 'acoes',
      cabecalho: '',
      className: 'text-right',
      render: (a) => (
        <Button variante="outline" tamanho="sm" onClick={() => navigate(`/app/agendas/${a.id}`)}>
          Abrir
        </Button>
      ),
    },
  ];

  const ehConsulta = form.tipoAgenda !== 'Exame';

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <CalendarClock className="h-6 w-6 text-primary-600" />
            Agendas
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            A agenda é do profissional (médico + especialidade) ou do equipamento (exame). A
            marcação parte da especialidade e lista os médicos dela.
          </p>
        </div>
        <Button onClick={abrirNova}>
          <Plus className="mr-2 h-4 w-4" />
          Nova agenda
        </Button>
      </header>

      <label className="flex items-center gap-2 text-sm text-gray-600">
        <input type="checkbox" checked={incluirInativas} onChange={(e) => setIncluirInativas(e.target.checked)} />
        Incluir inativas
      </label>

      {agendas.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(agendas.error)}
        </div>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={agendas.data ?? []}
        chaveLinha={(a) => a.id}
        carregando={agendas.isPending}
        vazio="Nenhuma agenda cadastrada."
      />

      <Modal aberto={modalAberto} aoFechar={() => setModalAberto(false)} titulo="Nova agenda" largura="lg">
        <form onSubmit={aoSalvar} className="space-y-4">
          <Campo label="Tipo de agenda" htmlFor="ag-tipo" required>
            <Select id="ag-tipo" value={form.tipoAgenda} onChange={(e) => set('tipoAgenda', e.target.value as TipoAgenda)}>
              {TIPOS_AGENDA.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.rotulo}
                </option>
              ))}
            </Select>
            <p className="mt-1 text-xs text-gray-500">
              {TIPOS_AGENDA.find((t) => t.id === form.tipoAgenda)?.descricao}
            </p>
          </Campo>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo label="Unidade" htmlFor="ag-unidade" required>
              <Select id="ag-unidade" value={form.unidadeId} onChange={(e) => set('unidadeId', e.target.value)}>
                <option value="">Selecione…</option>
                {(unidades.data ?? [])
                  .filter((u) => u.ativo)
                  .map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.nome}
                    </option>
                  ))}
              </Select>
            </Campo>

            {ehConsulta ? (
              <Campo label="Especialidade" htmlFor="ag-especialidade" required>
                <Select
                  id="ag-especialidade"
                  value={form.especialidadeId}
                  onChange={(e) => set('especialidadeId', e.target.value)}
                >
                  <option value="">Selecione…</option>
                  {(especialidades.data ?? []).map((esp) => (
                    <option key={esp.id} value={esp.id}>
                      {esp.nome}
                    </option>
                  ))}
                </Select>
              </Campo>
            ) : (
              <Campo label="Equipamento" htmlFor="ag-equipamento" required>
                <Select id="ag-equipamento" value={form.equipamentoId} onChange={(e) => set('equipamentoId', e.target.value)}>
                  <option value="">Selecione…</option>
                  {(equipamentos.data ?? [])
                    .filter((eq) => eq.ativo)
                    .map((eq) => (
                      <option key={eq.id} value={eq.id}>
                        {eq.nome} ({eq.modalidadeDicom})
                      </option>
                    ))}
                </Select>
              </Campo>
            )}
          </div>

          {ehConsulta ? (
            <Campo
              label="Médico"
              htmlFor="ag-medico"
              required
              dica={form.medicoId ? `Selecionado: ${form.medicoNome}` : 'A agenda é do profissional — selecione o médico desta especialidade.'}
            >
              {form.medicoId ? (
                <div className="flex items-center justify-between rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-sm">
                  <span className="font-medium text-gray-900">{form.medicoNome}</span>
                  <button
                    type="button"
                    className="text-xs text-primary-700 hover:underline"
                    onClick={() => setForm((f) => ({ ...f, medicoId: '', medicoNome: '' }))}
                  >
                    Trocar
                  </button>
                </div>
              ) : (
                <BuscaMedico aoSelecionar={(m) => setForm((f) => ({ ...f, medicoId: m.id, medicoNome: m.nomeCompleto }))} />
              )}
            </Campo>
          ) : null}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Campo label="Duração do slot (min)" htmlFor="ag-duracao" required>
              <Input
                id="ag-duracao"
                type="number"
                min={5}
                max={240}
                value={form.duracaoSlotMinutos}
                onChange={(e) => set('duracaoSlotMinutos', Number(e.target.value) || 20)}
              />
            </Campo>
            <Campo label="Vigência início" htmlFor="ag-vig-inicio" required>
              <Input id="ag-vig-inicio" type="date" value={form.vigenciaInicio} onChange={(e) => set('vigenciaInicio', e.target.value)} />
            </Campo>
            <Campo label="Vigência fim" htmlFor="ag-vig-fim" dica="Opcional.">
              <Input id="ag-vig-fim" type="date" value={form.vigenciaFim} onChange={(e) => set('vigenciaFim', e.target.value)} />
            </Campo>
          </div>

          <Campo
            label="Grade semanal de atendimento"
            htmlFor="ag-grade"
            required
            dica="Faixas por dia (ex.: manhã e tarde). Cada faixa é fatiada em horários pela duração do slot. Bloqueios pontuais (férias, feriados) são gerenciados depois, na agenda."
          >
            <GradeSemanalEditor faixas={faixas} aoMudar={setFaixas} />
          </Campo>

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          <div className="flex items-center justify-end gap-3 pt-2">
            <Button type="button" variante="secundaria" onClick={() => setModalAberto(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={cadastrar.isPending}>
              {cadastrar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Criar agenda
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
