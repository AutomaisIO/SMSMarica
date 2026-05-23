import { useMemo, useState } from 'react';
import { ArrowLeft, CalendarPlus, Check, Loader2, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { BuscaPaciente } from '@/shared/ui/BuscaPaciente';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useListarUnidades } from '@/features/unidades/api/queries';
import {
  useCadastrarTratamento,
  useTiposTratamento,
} from '@/features/tratamentos/api/queries';
import {
  DIAS_SEMANA,
  LIMITE_SESSOES,
  diaSemanaCurto,
  expandir,
  formatarDataBr,
  type RegraPeriodicidade,
} from '@/features/tratamentos/lib/expansor';
import {
  TIPO_PERIODICIDADE_VALOR,
  type TipoPeriodicidade,
} from '@/features/tratamentos/types';
import type { PacienteListItem } from '@/features/pacientes/types';

type Passo = 'paciente' | 'dados' | 'periodicidade' | 'revisao';

export function TratamentoFormPage() {
  const navigate = useNavigate();
  const unidades = useListarUnidades();
  const tipos = useTiposTratamento();
  const cadastrar = useCadastrarTratamento();

  const [passo, setPasso] = useState<Passo>('paciente');
  const [paciente, setPaciente] = useState<PacienteListItem | null>(null);
  const [dados, setDados] = useState({
    tipoTratamentoId: '',
    unidadeId: '',
    descricao: '',
    codigoSusLiberacao: '',
    horaPrevistaBusca: '',
    observacoes: '',
  });
  const [regra, setRegra] = useState<RegraPeriodicidade>({
    tipo: 'SemanaDiasFixos',
    intervaloDias: null,
    diasSemanaMascara: (1 << 1) | (1 << 3), // Segunda + Quarta
    dataInicio: '',
    quantidadeSessoes: 12,
  });
  const [datasOverride, setDatasOverride] = useState<string[] | null>(null);
  const [dataManual, setDataManual] = useState('');
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);

  const datasExpansao = useMemo(() => {
    if (datasOverride) return datasOverride;
    if (regra.tipo === 'Manual') return [];
    return expandir(regra);
  }, [regra, datasOverride]);

  function reset() {
    setDatasOverride(null);
  }

  function setTipo(tipo: TipoPeriodicidade) {
    reset();
    setRegra((r) => ({
      ...r,
      tipo,
      intervaloDias: tipo === 'IntervaloDias' ? (r.intervaloDias ?? 2) : null,
      diasSemanaMascara: tipo === 'SemanaDiasFixos' ? (r.diasSemanaMascara ?? (1 << 1)) : null,
    }));
  }

  function alternarDiaSemana(bit: number) {
    reset();
    setRegra((r) => ({
      ...r,
      diasSemanaMascara: ((r.diasSemanaMascara ?? 0) ^ bit) || null,
    }));
  }

  function removerData(iso: string) {
    setDatasOverride((cur) => (cur ?? datasExpansao).filter((d) => d !== iso));
  }

  function adicionarDataManual() {
    if (!dataManual) return;
    setDatasOverride((cur) => {
      const base = cur ?? datasExpansao;
      if (base.includes(dataManual)) return base;
      return [...base, dataManual].sort();
    });
    setDataManual('');
  }

  async function confirmar() {
    if (!paciente) return;
    setErroGlobal(null);
    try {
      const id = await cadastrar.mutateAsync({
        pacienteId: paciente.id,
        unidadeId: dados.unidadeId,
        tipoTratamentoId: dados.tipoTratamentoId || null,
        descricao: dados.descricao.trim(),
        codigoSusLiberacao: dados.codigoSusLiberacao.trim() || null,
        observacoes: dados.observacoes.trim() || null,
        horaPrevistaBusca: dados.horaPrevistaBusca || null,
        periodicidade: {
          tipo: TIPO_PERIODICIDADE_VALOR[regra.tipo],
          intervaloDias: regra.intervaloDias,
          diasSemanaMascara: regra.diasSemanaMascara,
          dataInicio: regra.dataInicio,
          quantidadeSessoes: datasExpansao.length || regra.quantidadeSessoes,
        },
        datas: datasExpansao,
      });
      navigate(`/app/tratamentos/${id}`, { replace: true });
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    }
  }

  const podeAvancarDados =
    paciente &&
    dados.unidadeId &&
    dados.descricao.trim().length > 0;

  const podeConfirmar =
    podeAvancarDados &&
    datasExpansao.length > 0 &&
    datasExpansao.length <= LIMITE_SESSOES;

  const unidadesAtivas = (unidades.data ?? []).filter((u) => u.ativo);

  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <button
          type="button"
          onClick={() => navigate('/app/tratamentos')}
          className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
          aria-label="Voltar"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Novo tratamento</h1>
          <p className="text-sm text-gray-600">
            {paciente ? `Paciente: ${paciente.nomeCompleto}` : 'Selecione o paciente para começar.'}
          </p>
        </div>
      </header>

      <BarraPassos passo={passo} paciente={paciente} dadosOk={Boolean(podeAvancarDados)} />

      {passo === 'paciente' ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-2 text-base font-medium text-gray-900">1. Paciente</h2>
          <p className="mb-4 text-sm text-gray-600">
            Busque por nome (qualquer parte) ou CPF. Resultados limitados a 20.
          </p>
          <BuscaPaciente
            aoSelecionar={(p) => {
              setPaciente(p);
              setPasso('dados');
            }}
          />
        </div>
      ) : null}

      {passo === 'dados' ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-base font-medium text-gray-900">2. Dados do tratamento</h2>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Campo label="Tipo de tratamento" htmlFor="tipo">
              <Select
                id="tipo"
                value={dados.tipoTratamentoId}
                onChange={(e) => setDados((d) => ({ ...d, tipoTratamentoId: e.target.value }))}
              >
                <option value="">— Selecione —</option>
                {(tipos.data ?? []).map((t) => (
                  <option key={t.id} value={t.id}>{t.nome}</option>
                ))}
              </Select>
            </Campo>
            <Campo label="Unidade de atendimento" htmlFor="unidade">
              <Select
                id="unidade"
                value={dados.unidadeId}
                onChange={(e) => setDados((d) => ({ ...d, unidadeId: e.target.value }))}
              >
                <option value="">— Selecione —</option>
                {unidadesAtivas.map((u) => (
                  <option key={u.id} value={u.id}>{u.nome}</option>
                ))}
              </Select>
            </Campo>
            <Campo label="Descrição" htmlFor="descricao" className="md:col-span-2"
              dica="Texto curto que identifica esse tratamento (aparece nas listagens).">
              <Input
                id="descricao"
                value={dados.descricao}
                onChange={(e) => setDados((d) => ({ ...d, descricao: e.target.value }))}
                placeholder="Ex: Hemodiálise — 3x/sem"
              />
            </Campo>
            <Campo label="Código SUS de liberação" htmlFor="codigoSus"
              dica="APAC/AIH ou equivalente.">
              <Input
                id="codigoSus"
                value={dados.codigoSusLiberacao}
                onChange={(e) => setDados((d) => ({ ...d, codigoSusLiberacao: e.target.value }))}
              />
            </Campo>
            <Campo label="Horário previsto da busca" htmlFor="hora"
              dica="Padrão herdado por cada sessão (pode ser ajustado depois).">
              <Input
                id="hora"
                type="time"
                value={dados.horaPrevistaBusca}
                onChange={(e) => setDados((d) => ({ ...d, horaPrevistaBusca: e.target.value }))}
              />
            </Campo>
            <Campo label="Observações" htmlFor="obs" className="md:col-span-2">
              <textarea
                id="obs"
                className="input min-h-[80px]"
                value={dados.observacoes}
                onChange={(e) => setDados((d) => ({ ...d, observacoes: e.target.value }))}
              />
            </Campo>
          </div>
          <div className="mt-6 flex justify-between">
            <Button variante="ghost" onClick={() => setPasso('paciente')}>Voltar</Button>
            <Button disabled={!podeAvancarDados} onClick={() => setPasso('periodicidade')}>
              Avançar
            </Button>
          </div>
        </div>
      ) : null}

      {passo === 'periodicidade' ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-base font-medium text-gray-900">3. Periodicidade e datas</h2>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <Campo label="Tipo de periodicidade" htmlFor="tipoPer">
              <Select
                id="tipoPer"
                value={regra.tipo}
                onChange={(e) => setTipo(e.target.value as TipoPeriodicidade)}
              >
                <option value="Diaria">Diária (1x por dia consecutivo)</option>
                <option value="SemanaDiasFixos">Semanal — dias fixos da semana</option>
                <option value="IntervaloDias">A cada N dias</option>
                <option value="Manual">Manual — lançar datas individualmente</option>
              </Select>
            </Campo>
            <Campo label="Data de início" htmlFor="inicio">
              <Input
                id="inicio"
                type="date"
                value={regra.dataInicio}
                onChange={(e) => {
                  reset();
                  setRegra((r) => ({ ...r, dataInicio: e.target.value }));
                }}
              />
            </Campo>
            <Campo label="Número de sessões" htmlFor="qtd">
              <Input
                id="qtd"
                type="number"
                min={1}
                max={LIMITE_SESSOES}
                value={regra.quantidadeSessoes}
                onChange={(e) => {
                  reset();
                  setRegra((r) => ({ ...r, quantidadeSessoes: Number(e.target.value) || 0 }));
                }}
                disabled={regra.tipo === 'Manual'}
              />
            </Campo>
            {regra.tipo === 'IntervaloDias' ? (
              <Campo label="Intervalo (dias)" htmlFor="intervalo">
                <Input
                  id="intervalo"
                  type="number"
                  min={1}
                  max={365}
                  value={regra.intervaloDias ?? ''}
                  onChange={(e) => {
                    reset();
                    setRegra((r) => ({ ...r, intervaloDias: Number(e.target.value) || null }));
                  }}
                />
              </Campo>
            ) : null}
            {regra.tipo === 'SemanaDiasFixos' ? (
              <Campo label="Dias da semana" htmlFor="dias" className="md:col-span-2">
                <div className="flex flex-wrap gap-2">
                  {DIAS_SEMANA.map((d) => {
                    const selecionado = ((regra.diasSemanaMascara ?? 0) & d.bit) !== 0;
                    return (
                      <button
                        key={d.bit}
                        type="button"
                        onClick={() => alternarDiaSemana(d.bit)}
                        className={`rounded-md border px-3 py-1.5 text-sm transition-colors ${
                          selecionado
                            ? 'border-red-600 bg-red-50 text-red-700'
                            : 'border-gray-300 text-gray-600 hover:bg-gray-50'
                        }`}
                      >
                        {d.curto}
                      </button>
                    );
                  })}
                </div>
              </Campo>
            ) : null}
          </div>

          <div className="mt-6">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold text-gray-900">
                Prévia das sessões ({datasExpansao.length})
              </h3>
              {datasOverride ? (
                <button
                  type="button"
                  className="text-xs text-red-700 hover:underline"
                  onClick={reset}
                >
                  Restaurar datas calculadas
                </button>
              ) : null}
            </div>

            <div className="mt-3 flex flex-wrap gap-2">
              <Input
                type="date"
                value={dataManual}
                onChange={(e) => setDataManual(e.target.value)}
                className="max-w-xs"
              />
              <Button variante="outline" onClick={adicionarDataManual} disabled={!dataManual}>
                <CalendarPlus className="mr-1.5 h-4 w-4" /> Adicionar data
              </Button>
            </div>

            {datasExpansao.length > 0 ? (
              <ul className="mt-4 grid grid-cols-2 gap-2 md:grid-cols-4">
                {datasExpansao.map((d) => (
                  <li
                    key={d}
                    className="flex items-center justify-between rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-sm"
                  >
                    <span>
                      <strong>{formatarDataBr(d)}</strong>
                      <span className="ml-1 text-xs text-gray-500">{diaSemanaCurto(d)}</span>
                    </span>
                    <button
                      type="button"
                      aria-label={`Remover ${d}`}
                      onClick={() => removerData(d)}
                      className="text-red-700 hover:text-red-900"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-4 text-sm text-gray-500">
                {regra.tipo === 'Manual'
                  ? 'Adicione as datas manualmente acima.'
                  : 'Preencha data de início e periodicidade para gerar a prévia.'}
              </p>
            )}
          </div>

          <div className="mt-6 flex justify-between">
            <Button variante="ghost" onClick={() => setPasso('dados')}>Voltar</Button>
            <Button onClick={() => setPasso('revisao')} disabled={datasExpansao.length === 0}>
              Avançar
            </Button>
          </div>
        </div>
      ) : null}

      {passo === 'revisao' ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-base font-medium text-gray-900">4. Confirmação</h2>
          <dl className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Item rotulo="Paciente" valor={paciente?.nomeCompleto ?? '—'} />
            <Item rotulo="Unidade" valor={unidadesAtivas.find((u) => u.id === dados.unidadeId)?.nome ?? '—'} />
            <Item rotulo="Tipo" valor={tipos.data?.find((t) => t.id === dados.tipoTratamentoId)?.nome ?? '—'} />
            <Item rotulo="Código SUS" valor={dados.codigoSusLiberacao || '—'} />
            <Item rotulo="Descrição" valor={dados.descricao} />
            <Item rotulo="Horário previsto" valor={dados.horaPrevistaBusca || '—'} />
            <Item rotulo="Total de sessões" valor={String(datasExpansao.length)} />
          </dl>

          {erroGlobal ? (
            <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {erroGlobal}
            </div>
          ) : null}

          <div className="mt-6 flex justify-between">
            <Button variante="ghost" onClick={() => setPasso('periodicidade')}>Voltar</Button>
            <Button onClick={confirmar} disabled={!podeConfirmar || cadastrar.isPending}>
              {cadastrar.isPending ? (
                <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Salvando…</>
              ) : (
                <><Check className="mr-2 h-4 w-4" /> Cadastrar tratamento</>
              )}
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function BarraPassos({ passo, paciente, dadosOk }: { passo: Passo; paciente: PacienteListItem | null; dadosOk: boolean }) {
  const estados = [
    { id: 'paciente', label: 'Paciente', done: Boolean(paciente) },
    { id: 'dados', label: 'Dados', done: dadosOk && passo !== 'dados' },
    { id: 'periodicidade', label: 'Periodicidade', done: passo === 'revisao' },
    { id: 'revisao', label: 'Confirmação', done: false },
  ];
  return (
    <ol className="flex items-center gap-4 text-sm">
      {estados.map((e, i) => (
        <li key={e.id} className="flex items-center gap-2">
          <span
            className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-semibold ${
              passo === e.id
                ? 'bg-red-600 text-white'
                : e.done
                ? 'bg-green-600 text-white'
                : 'bg-gray-200 text-gray-600'
            }`}
          >
            {i + 1}
          </span>
          <span className={passo === e.id ? 'font-medium text-gray-900' : 'text-gray-500'}>
            {e.label}
          </span>
          {i < estados.length - 1 ? <span className="text-gray-300">›</span> : null}
        </li>
      ))}
    </ol>
  );
}

function Item({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-gray-500">{rotulo}</dt>
      <dd className="text-sm font-medium text-gray-900">{valor}</dd>
    </div>
  );
}
