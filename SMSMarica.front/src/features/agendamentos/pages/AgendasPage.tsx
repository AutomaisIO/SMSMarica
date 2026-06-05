import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { CalendarClock, Loader2, Plus } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useListarUnidades } from '@/features/unidades/api/queries';
import { useListarEspecialidades } from '@/features/especialidades/api/queries';
import { BuscaMedico } from '@/features/agendamentos/components/BuscaMedico';
import { useCadastrarAgenda, useListarAgendas } from '@/features/agendamentos/api/queries';
import type { AgendaListItem } from '@/features/agendamentos/types';

function hoje(): string {
  return new Date().toISOString().slice(0, 10);
}

type FormAgenda = {
  unidadeId: string;
  especialidadeId: string;
  medicoId: string;
  medicoNome: string;
  duracaoConsultaMinutos: number;
  vigenciaInicio: string;
  vigenciaFim: string;
};

const FORM_VAZIO: FormAgenda = {
  unidadeId: '',
  especialidadeId: '',
  medicoId: '',
  medicoNome: '',
  duracaoConsultaMinutos: 20,
  vigenciaInicio: hoje(),
  vigenciaFim: '',
};

export function AgendasPage() {
  const navigate = useNavigate();
  const [incluirInativas, setIncluirInativas] = useState(false);
  const agendas = useListarAgendas({ incluirInativas });
  const unidades = useListarUnidades();
  const especialidades = useListarEspecialidades();
  const cadastrar = useCadastrarAgenda();

  const [modalAberto, setModalAberto] = useState(false);
  const [form, setForm] = useState<FormAgenda>(FORM_VAZIO);
  const [erro, setErro] = useState<string | null>(null);

  function abrirNova() {
    setForm(FORM_VAZIO);
    setErro(null);
    setModalAberto(true);
  }

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (!form.unidadeId || !form.especialidadeId || !form.medicoId) {
      setErro('Selecione unidade, especialidade e médico.');
      return;
    }
    cadastrar.mutate(
      {
        unidadeId: form.unidadeId,
        especialidadeId: form.especialidadeId,
        medicoId: form.medicoId,
        duracaoConsultaMinutos: form.duracaoConsultaMinutos,
        vigenciaInicio: form.vigenciaInicio,
        vigenciaFim: form.vigenciaFim || null,
      },
      {
        onSuccess: (id) => {
          setModalAberto(false);
          navigate(`/app/agendas/${id}`);
        },
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  const colunas: Coluna<AgendaListItem>[] = [
    {
      chave: 'medico',
      cabecalho: 'Médico',
      render: (a) => <span className="font-medium text-gray-900">{a.medicoNome}</span>,
    },
    { chave: 'especialidade', cabecalho: 'Especialidade', render: (a) => a.especialidadeNome },
    { chave: 'unidade', cabecalho: 'Unidade', render: (a) => a.unidadeNome },
    { chave: 'duracao', cabecalho: 'Consulta', render: (a) => `${a.duracaoConsultaMinutos} min` },
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

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <CalendarClock className="h-6 w-6 text-primary-600" />
            Agendas
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            Grade de cada médico, por unidade e especialidade. Abra uma agenda para definir horários e marcar consultas.
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
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo label="Unidade" htmlFor="ag-unidade" required>
              <Select
                id="ag-unidade"
                value={form.unidadeId}
                onChange={(e) => setForm((f) => ({ ...f, unidadeId: e.target.value }))}
              >
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

            <Campo label="Especialidade" htmlFor="ag-especialidade" required>
              <Select
                id="ag-especialidade"
                value={form.especialidadeId}
                onChange={(e) => setForm((f) => ({ ...f, especialidadeId: e.target.value }))}
              >
                <option value="">Selecione…</option>
                {(especialidades.data ?? []).map((esp) => (
                  <option key={esp.id} value={esp.id}>
                    {esp.nome}
                  </option>
                ))}
              </Select>
            </Campo>
          </div>

          <Campo label="Médico" htmlFor="ag-medico" required dica={form.medicoId ? `Selecionado: ${form.medicoNome}` : undefined}>
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
              <BuscaMedico
                aoSelecionar={(m) => setForm((f) => ({ ...f, medicoId: m.id, medicoNome: m.nomeCompleto }))}
              />
            )}
          </Campo>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Campo label="Duração (min)" htmlFor="ag-duracao" required>
              <Input
                id="ag-duracao"
                type="number"
                min={5}
                max={240}
                value={form.duracaoConsultaMinutos}
                onChange={(e) => setForm((f) => ({ ...f, duracaoConsultaMinutos: Number(e.target.value) || 20 }))}
              />
            </Campo>
            <Campo label="Vigência início" htmlFor="ag-vig-inicio" required>
              <Input
                id="ag-vig-inicio"
                type="date"
                value={form.vigenciaInicio}
                onChange={(e) => setForm((f) => ({ ...f, vigenciaInicio: e.target.value }))}
              />
            </Campo>
            <Campo label="Vigência fim" htmlFor="ag-vig-fim" dica="Opcional.">
              <Input
                id="ag-vig-fim"
                type="date"
                value={form.vigenciaFim}
                onChange={(e) => setForm((f) => ({ ...f, vigenciaFim: e.target.value }))}
              />
            </Campo>
          </div>

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
