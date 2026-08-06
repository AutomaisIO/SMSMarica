import { useEffect, useMemo, useState } from 'react';
import { Activity, DatabaseZap, Loader2, Play, RefreshCw, StopCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import {
  useAgendasPep,
  useBasesPep,
  useCancelarImportacaoPep,
  useDiagnosticoPep,
  useExecucoesPep,
  useIniciarImportacaoPep,
  usePausarMotorPep,
  useSalvarAgendaPep,
  useStatusPep,
} from '@/features/pep-sincronizacao/api/queries';
import { QuadroMotores } from '@/features/pep-sincronizacao/components/QuadroMotores';
import { SecaoDivergencias } from '@/features/pep-sincronizacao/components/SecaoDivergencias';
import type {
  EscopoSincronizacao,
  ModoSincronizacao,
  StatusImportacao,
} from '@/features/pep-sincronizacao/types';

function dataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('pt-BR');
}

function duracao(seg: number | null): string {
  if (seg == null) return '—';
  if (seg < 60) return `${seg.toFixed(1)}s`;
  const m = Math.floor(seg / 60);
  const s = Math.round(seg % 60);
  return `${m}m ${s}s`;
}

function vazao(s: StatusImportacao): string {
  const c = s.contadores;
  const total =
    c.medicos + c.pacientes + c.encounters + c.conditions +
    c.medicationRequests + c.documentReferences + c.observations;
  if (!s.decorridoSegundos || s.decorridoSegundos <= 0 || total === 0) return '—';
  return `${(total / s.decorridoSegundos).toFixed(1)} rec/s`;
}

const CLASSE_STATUS: Record<string, string> = {
  EmExecucao: 'bg-blue-50 text-blue-700 border-blue-200',
  Concluido: 'bg-green-50 text-green-700 border-green-200',
  Erro: 'bg-red-50 text-red-700 border-red-200',
  Cancelado: 'bg-orange-50 text-orange-700 border-orange-200',
  Pendente: 'bg-amber-50 text-amber-700 border-amber-200',
  Nenhuma: 'bg-gray-50 text-gray-600 border-gray-200',
};

function Badge({ status }: { status: string }) {
  const cls = CLASSE_STATUS[status] ?? CLASSE_STATUS.Nenhuma;
  return (
    <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium ${cls}`}>
      {status}
    </span>
  );
}

/**
 * Nota do cartão quando parte do número é releitura. O ciclo incremental relê um bloco fixo de
 * pessoas de propósito — internação em curso volta a cada poll, o que no HMCML são ~150 pacientes
 * sempre os mesmos. Sem esta linha o cartão dizia "170 pacientes" e parecia movimento de cadastro.
 */
function notaReleitura(inalterados: number): string | undefined {
  return inalterados > 0 ? `${inalterados} relido(s) sem mudança` : undefined;
}

function PainelContadores({ s }: { s: StatusImportacao }) {
  const c = s.contadores;
  const itens: Array<[string, number, string?]> = [
    ['Médicos', c.medicos - c.medicosInalterados, notaReleitura(c.medicosInalterados)],
    ['Pacientes', c.pacientes - c.pacientesInalterados, notaReleitura(c.pacientesInalterados)],
    ['Atendimentos', c.encounters],
    ['Diagnósticos', c.conditions],
    ['Medicações', c.medicationRequests],
    ['Documentos', c.documentReferences],
    ['Sinais/risco', c.observations],
    ['Falhas', c.falhas],
  ];
  return (
    <div className="space-y-3">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {itens.map(([rotulo, valor, nota]) => (
          <div key={rotulo} className="rounded-lg border border-gray-200 bg-gray-50 px-3 py-2">
            <div className="text-lg font-semibold text-gray-900">{valor}</div>
            <div className="text-xs text-gray-500">{rotulo}</div>
            {nota ? <div className="mt-0.5 text-[11px] text-gray-400">{nota}</div> : null}
          </div>
        ))}
      </div>
      {s.contadores.retentativas > 0 ? (
        <div className="flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          <span className="text-lg font-semibold">{s.contadores.retentativas}</span>
          <span>
            reenvio(s) por saturação de conexão — registros <strong>pendentes sendo reempurrados</strong>,
            não descartados. Some quando o banco libera.
          </span>
        </div>
      ) : null}
    </div>
  );
}

/** Rótulos amigáveis das contagens do hub no diagnóstico. */
const ROTULOS_HUB: Record<string, string> = {
  patients: 'Pacientes',
  practitioners: 'Médicos',
  encounters: 'Atendimentos',
  encountersInternacao: 'Internações',
  encountersInternacaoEmCurso: 'Internados agora',
  conditions: 'Diagnósticos',
  documentReferences: 'Documentos',
  medicationRequests: 'Medicações',
  observations: 'Sinais/risco',
  locations: 'Setores/leitos',
};

/** Cartão do sincronismo contínuo (ADR-0024): agenda + diagnóstico origem×hub. */
function SecaoContinuidade({ fonteId, podeEditar }: { fonteId: string; podeEditar: boolean }) {
  const agendas = useAgendasPep();
  const salvar = useSalvarAgendaPep();
  const pausar = usePausarMotorPep();
  const diagnostico = useDiagnosticoPep(fonteId);

  const agenda = agendas.data?.find((a) => a.fonteId === fonteId);
  const pausado = !!agenda?.pausadoAte && new Date(agenda.pausadoAte) > new Date();

  function aoPausar() {
    if (!fonteId) return;
    if (pausado) {
      pausar.mutate({ fonteId, horas: undefined });
      return;
    }
    const resp = window.prompt('Pausar o motor por quantas horas? (o sincronismo não dispara nesse período)', '4');
    if (resp === null) return;
    const horas = Number(resp);
    if (!Number.isFinite(horas) || horas <= 0) {
      window.alert('Informe um número de horas maior que zero.');
      return;
    }
    pausar.mutate({ fonteId, horas });
  }
  const [ativo, setAtivo] = useState(false);
  const [intervalo, setIntervalo] = useState('30');
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    setAtivo(agenda?.ativo ?? false);
    setIntervalo(String(agenda?.intervaloMinutos ?? 30));
  }, [agenda?.fonteId, agenda?.ativo, agenda?.intervaloMinutos]);

  if (!fonteId) return null;

  function aoSalvar() {
    setErro(null);
    salvar.mutate(
      { fonteId, ativo, intervaloMinutos: Number(intervalo) || 30 },
      { onError: (err) => setErro(extrairMensagemDeErro(err)) },
    );
  }

  const d = diagnostico.data;

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <h2 className="mb-3 flex items-center gap-2 text-lg font-semibold text-gray-900">
        <Activity className="h-5 w-5 text-primary-600" />
        Sincronismo contínuo
      </h2>

      <div className="flex flex-wrap items-end gap-4">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={ativo} disabled={!podeEditar} onChange={(e) => setAtivo(e.target.checked)} />
          Manter o hub em dia automaticamente (modo Incremental)
        </label>
        <Campo label="Intervalo (min)" htmlFor="pep-agenda-int">
          <Input
            id="pep-agenda-int"
            type="number"
            min={5}
            className="w-24"
            value={intervalo}
            disabled={!podeEditar}
            onChange={(e) => setIntervalo(e.target.value)}
          />
        </Campo>
        <Button type="button" tamanho="sm" onClick={aoSalvar} disabled={!podeEditar || salvar.isPending}>
          {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
          Salvar agenda
        </Button>
        {podeEditar && agenda ? (
          <Button
            type="button"
            tamanho="sm"
            variante={pausado ? 'secundaria' : 'danger'}
            onClick={aoPausar}
            disabled={pausar.isPending}
          >
            {pausar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            {pausado ? 'Retomar motor' : 'Pausar motor'}
          </Button>
        ) : null}
      </div>

      <div className="mt-3 grid grid-cols-2 gap-x-6 gap-y-1 text-sm sm:grid-cols-3">
        <div>
          <span className="text-gray-500">Próximo ciclo:</span>{' '}
          {pausado ? (
            <span className="font-semibold text-red-600">
              PAUSADO até {dataHora(agenda?.pausadoAte ?? null)}
            </span>
          ) : (
            dataHora(agenda?.proximoRunEm ?? null)
          )}
        </div>
        <div>
          <span className="text-gray-500">Re-scan médicos/estrutura:</span> a cada {agenda?.medicoRescanHoras ?? 24}h
        </div>
        <div>
          <span className="text-gray-500">Falhas consecutivas:</span>{' '}
          {agenda && agenda.falhasConsecutivas > 0 ? (
            <span className={agenda.falhasConsecutivas >= 5 ? 'font-semibold text-red-600' : 'font-semibold text-amber-600'}>
              {agenda.falhasConsecutivas} {agenda.falhasConsecutivas >= 5 ? '— verificar túnel/hub!' : '(em backoff)'}
            </span>
          ) : (
            '0'
          )}
        </div>
      </div>

      {erro ? (
        <div className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      <div className="mt-4 border-t border-gray-100 pt-4">
        <div className="flex items-center gap-3">
          <Button
            type="button"
            variante="secundaria"
            tamanho="sm"
            onClick={() => diagnostico.refetch()}
            disabled={diagnostico.isFetching}
          >
            {diagnostico.isFetching ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            {diagnostico.isFetching ? 'Consultando origem e hub…' : 'Ver diagnóstico origem × hub'}
          </Button>
          {diagnostico.isError ? (
            <span className="text-sm text-red-600">{extrairMensagemDeErro(diagnostico.error)}</span>
          ) : null}
        </div>

        {d ? (
          <div className="mt-3 space-y-3">
            <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-sm sm:grid-cols-4">
              <div><span className="text-gray-500">Pacientes pendentes:</span> {d.pacientesPendentes ?? '—'}</div>
              <div><span className="text-gray-500">Atendimentos pendentes:</span> {d.baasPendentes ?? '—'}</div>
              <div><span className="text-gray-500">Internações pendentes:</span> {d.fiasPendentes ?? '—'}</div>
              <div><span className="text-gray-500">Docs no log pendentes:</span> {d.edocLogPendentes ?? '—'}</div>
              <div><span className="text-gray-500">Marca de atendimentos:</span> {dataHora(d.ultimoSyncBaaEm)}</div>
              <div><span className="text-gray-500">Marca de internações:</span> {dataHora(d.ultimoSyncFiaEm)}</div>
              <div><span className="text-gray-500">Marca de pacientes:</span> {dataHora(d.ultimoSyncPacienteEm)}</div>
              <div><span className="text-gray-500">Marca de médicos:</span> {dataHora(d.ultimoSyncMedicoEm)}</div>
            </div>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
              {Object.entries(ROTULOS_HUB).map(([chave, rotulo]) => (
                <div key={chave} className="rounded-lg border border-gray-200 bg-gray-50 px-3 py-2">
                  <div className="text-lg font-semibold text-gray-900">{d.hub?.[chave] ?? '—'}</div>
                  <div className="text-xs text-gray-500">{rotulo} no hub</div>
                </div>
              ))}
            </div>
          </div>
        ) : null}
      </div>
    </section>
  );
}

/**
 * O que o operador quer fazer — em vez de Modo × Escopo × limites × purga, que eram flags
 * ortogonais cujas combinações inválidas o servidor tinha de recusar uma a uma.
 *
 * Fora de propósito: "apagar antes" (o upsert reconcilia sem apagar, e uma purga interrompida
 * some com prontuário), concorrência e cursor digitado à mão — viraram, respectivamente,
 * bloqueio no servidor, default e um "retomar de onde parou".
 */
type Intencao = 'ensaio' | 'carga' | 'atualizar';

const INTENCOES: {
  id: Intencao; titulo: string; acao: string; duracao: string; corDuracao: string; descricao: string;
}[] = [
  {
    id: 'ensaio',
    titulo: 'Ensaio',
    acao: 'Rodar ensaio',
    duracao: 'minutos',
    corDuracao: 'bg-green-100 text-green-800',
    descricao:
      'Importa alguns pacientes e só o histórico deles, para conferir o conector contra a base real. Não avança marca d\'água nenhuma.',
  },
  {
    id: 'carga',
    titulo: 'Carga inicial',
    acao: 'Iniciar carga',
    duracao: 'horas',
    corDuracao: 'bg-amber-100 text-amber-800',
    descricao:
      'Traz a base inteira. Para base nova ou reconciliação geral. Segura os outros motores enquanto roda, e um deploy interrompe (dá para retomar).',
  },
  {
    id: 'atualizar',
    titulo: 'Atualizar agora',
    acao: 'Atualizar',
    duracao: 'minutos',
    corDuracao: 'bg-green-100 text-green-800',
    descricao:
      'Antecipa o ciclo automático: traz só o que mudou na origem desde o último sincronismo.',
  },
];

export function PepSincronizacaoPage() {
  const bases = useBasesPep();
  const status = useStatusPep();
  const execucoes = useExecucoesPep();
  const iniciar = useIniciarImportacaoPep();
  const cancelar = useCancelarImportacaoPep();
  const podeImportar = usePermissao('SincronizacaoPep', 'Edicao');

  const [fonteId, setFonteId] = useState('');
  const [intencao, setIntencao] = useState<Intencao>('ensaio');
  const [quantosEnsaio, setQuantosEnsaio] = useState('100');
  const [retomar, setRetomar] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  // Seleciona a primeira base suportada por padrão.
  useEffect(() => {
    if (!fonteId && bases.data?.length) {
      const sugerida = bases.data.find((b) => b.suportada) ?? bases.data[0];
      setFonteId(sugerida.id);
    }
  }, [bases.data, fonteId]);

  const baseSel = useMemo(() => bases.data?.find((b) => b.id === fonteId), [bases.data, fonteId]);
  const emExecucao = status.data?.emExecucao ?? false;

  function aoIniciar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);

    if (intencao === 'carga' && !window.confirm(
      `Carga inicial de "${baseSel?.nome}": traz a base INTEIRA e leva horas. ` +
      'Enquanto roda, os outros motores ficam esperando (roda uma importação por vez) ' +
      'e um deploy interrompe — dá para retomar depois. Continuar?')) {
      return;
    }

    const n = Math.max(Number(quantosEnsaio) || 0, 1);
    // "Apagar antes" NÃO é enviado por nenhuma intenção: o upsert já reconcilia, e uma purga
    // interrompida deixaria o prontuário incompleto sem aviso. O servidor também recusa.
    const payload =
      intencao === 'ensaio'
        ? { modo: 'Completo' as ModoSincronizacao, escopo: 'Limitado' as EscopoSincronizacao,
            maxMedicos: n, maxPacientes: n, cursorPacienteInicial: null }
        : intencao === 'carga'
          ? { modo: 'Completo' as ModoSincronizacao, escopo: 'Tudo' as EscopoSincronizacao,
              maxMedicos: null, maxPacientes: null,
              cursorPacienteInicial: retomar ? baseSel?.cursorPacienteCd ?? null : null }
          : { modo: 'Incremental' as ModoSincronizacao, escopo: 'Tudo' as EscopoSincronizacao,
              maxMedicos: null, maxPacientes: null, cursorPacienteInicial: null };

    iniciar.mutate(
      { fonteId, ...payload, apagarAntes: false, concorrencia: null },
      { onError: (err) => setErro(extrairMensagemDeErro(err)) },
    );
  }

  function aoParar() {
    if (!window.confirm('Parar a importação em andamento? No modo Completo, dá para retomar de onde parou depois.')) {
      return;
    }
    // Parar sem pausar NÃO segura o motor: o scheduler religa no próximo intervalo. Por isso
    // a pausa vem oferecida junto, e por padrão (é o que o operador espera ao mandar parar).
    const pausar = window.confirm(
      'Pausar também o motor por 4 horas? OK = para e pausa (o sincronismo NÃO religa sozinho). ' +
        'Cancelar = só para este run; o próximo ciclo agendado dispara normalmente.',
    );
    cancelar.mutate(pausar ? 4 : undefined, {
      onError: (err) => window.alert(extrairMensagemDeErro(err)),
    });
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <DatabaseZap className="h-6 w-6 text-primary-600" />
          Sincronização de Prontuários (PEP)
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Importa pacientes, médicos e histórico clínico de uma base de PEP (Salux e futuros) para o
          hub FHIR. Roda em segundo plano; acompanhe o progresso e os tempos abaixo.
        </p>
      </header>

      <QuadroMotores status={status.data} fonteSelecionada={fonteId} aoSelecionar={setFonteId} />

      <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <form onSubmit={aoIniciar} className="space-y-5">
          <Campo label="Base de origem" htmlFor="pep-base">
            <Select id="pep-base" value={fonteId} onChange={(e) => setFonteId(e.target.value)}>
              <option value="" disabled>
                {bases.isPending ? 'Carregando bases…' : 'Selecione uma base'}
              </option>
              {bases.data?.map((b) => (
                <option key={b.id} value={b.id} disabled={!b.suportada}>
                  {b.nome} ({b.tipo} · {b.ambiente}){b.suportada ? '' : ' — sem conector'}
                </option>
              ))}
            </Select>
          </Campo>

          <div className="space-y-2">
            {INTENCOES.map((op) => {
              const ativa = intencao === op.id;
              return (
                <label
                  key={op.id}
                  className={`flex cursor-pointer gap-3 rounded-lg border p-3 transition ${
                    ativa ? 'border-primary-500 bg-primary-50/40 ring-1 ring-primary-200' : 'border-gray-200 hover:border-gray-300'
                  }`}
                >
                  <input
                    type="radio"
                    name="pep-intencao"
                    className="mt-1"
                    checked={ativa}
                    onChange={() => setIntencao(op.id)}
                  />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-medium text-gray-900">{op.titulo}</span>
                      <span className={`rounded-full px-2 py-0.5 text-xs ${op.corDuracao}`}>{op.duracao}</span>
                    </div>
                    <p className="mt-0.5 text-xs text-gray-600">{op.descricao}</p>

                    {ativa && op.id === 'ensaio' ? (
                      <div className="mt-2 flex items-center gap-2 text-xs text-gray-700">
                        <span>Quantos pacientes:</span>
                        <Input
                          type="number"
                          min={1}
                          max={5000}
                          className="w-24"
                          value={quantosEnsaio}
                          onChange={(e) => setQuantosEnsaio(e.target.value)}
                        />
                        <span className="text-gray-500">não move os ponteiros — pode repetir à vontade</span>
                      </div>
                    ) : null}

                    {ativa && op.id === 'carga' && baseSel?.cursorPacienteCd != null ? (
                      <label className="mt-2 flex items-center gap-2 text-xs text-amber-800">
                        <input type="checkbox" checked={retomar} onChange={(e) => setRetomar(e.target.checked)} />
                        Retomar de onde parou (paciente{' '}
                        <span className="font-mono font-semibold">{baseSel.cursorPacienteCd}</span>) — desmarque para
                        recomeçar do zero
                      </label>
                    ) : null}
                  </div>
                </label>
              );
            })}
          </div>

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          <div className="flex flex-wrap items-center justify-end gap-3">
            {!podeImportar ? (
              <span className="text-sm text-gray-500">Sem permissão para iniciar importações.</span>
            ) : null}
            {emExecucao ? (
              <span className="text-sm text-blue-600">Já há uma importação em andamento.</span>
            ) : null}
            <Button type="submit" disabled={!podeImportar || !fonteId || !baseSel?.suportada || emExecucao || iniciar.isPending}>
              {iniciar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Play className="mr-2 h-4 w-4" />}
              {INTENCOES.find((o) => o.id === intencao)?.acao ?? 'Importar'}
            </Button>
          </div>
        </form>
      </section>

      {/* ---- Sincronismo contínuo (agenda + diagnóstico) ---- */}
      <SecaoContinuidade fonteId={fonteId} podeEditar={podeImportar} />

      {/* ---- Divergências de identidade (origem × hub) ---- */}
      <SecaoDivergencias fonteId={fonteId} podeEditar={podeImportar} />

      {/* ---- Status / progresso ---- */}
      {status.data ? (
        <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
              {emExecucao ? <Loader2 className="h-4 w-4 animate-spin text-blue-600" /> : <RefreshCw className="h-4 w-4 text-gray-400" />}
              {emExecucao ? 'Importação em andamento' : 'Última importação'}
            </h2>
            <div className="flex items-center gap-3">
              {emExecucao && podeImportar ? (
                <Button type="button" variante="danger" tamanho="sm" onClick={aoParar} disabled={cancelar.isPending}>
                  {cancelar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <StopCircle className="mr-2 h-4 w-4" />}
                  Parar importação
                </Button>
              ) : null}
              <Badge status={status.data.status} />
            </div>
          </div>

          <div className="mb-4 grid grid-cols-2 gap-x-6 gap-y-1 text-sm sm:grid-cols-4">
            <div><span className="text-gray-500">Base:</span> {status.data.fonteNome ?? '—'}</div>
            <div><span className="text-gray-500">Modo:</span> {status.data.modo ?? '—'} / {status.data.escopo ?? '—'}</div>
            <div><span className="font-medium text-gray-700">Fase:</span> <span className="font-semibold text-primary-700">{status.data.faseAtual ?? '—'}</span></div>
            <div><span className="text-gray-500">Decorrido:</span> {duracao(status.data.decorridoSegundos)}</div>
            <div><span className="text-gray-500">Vazão:</span> {vazao(status.data)}</div>
            <div><span className="text-gray-500">Início:</span> {dataHora(status.data.iniciadoEm)}</div>
            <div><span className="text-gray-500">Fim:</span> {dataHora(status.data.finalizadoEm)}</div>
          </div>

          <PainelContadores s={status.data} />

          {status.data.ultimasFalhas?.length ? (
            <div className="mt-4 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
              <div className="mb-1 font-medium">Falhas recentes ({status.data.contadores.falhas}):</div>
              <ul className="list-disc space-y-0.5 pl-5 font-mono text-xs">
                {status.data.ultimasFalhas.map((f, i) => (<li key={i}>{f}</li>))}
              </ul>
            </div>
          ) : null}

          {status.data.mensagemErro ? (
            <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {status.data.mensagemErro}
            </div>
          ) : null}
        </section>
      ) : null}

      {/* ---- Histórico ---- */}
      <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <h2 className="mb-3 text-lg font-semibold text-gray-900">Histórico de importações</h2>
        {execucoes.isPending ? (
          <p className="text-sm text-gray-500">Carregando…</p>
        ) : !execucoes.data?.length ? (
          <p className="text-sm text-gray-500">Nenhuma importação ainda.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200 text-left text-xs uppercase tracking-wide text-gray-500">
                  <th className="py-2 pr-3">Base</th>
                  <th className="py-2 pr-3">Modo</th>
                  <th className="py-2 pr-3">Disparo</th>
                  <th className="py-2 pr-3">Status</th>
                  <th className="py-2 pr-3">Início</th>
                  <th className="py-2 pr-3">Duração</th>
                  <th className="py-2 pr-3 text-right" title="Pacientes alterados; o +N em cinza são releituras que não mudaram nada">
                    Pac.
                  </th>
                  <th className="py-2 pr-3 text-right">Atend.</th>
                  <th className="py-2 text-right">Obs.</th>
                </tr>
              </thead>
              <tbody>
                {execucoes.data.map((e) => (
                  <tr key={e.id} className="border-b border-gray-100">
                    <td className="py-2 pr-3">{e.fonteNome}</td>
                    <td className="py-2 pr-3">{e.modo} / {e.escopo}</td>
                    <td className="py-2 pr-3 text-xs text-gray-600">{e.disparo === 'Agendado' ? '⏱ Agendado' : 'Manual'}</td>
                    <td className="py-2 pr-3"><Badge status={e.status} /></td>
                    <td className="py-2 pr-3 whitespace-nowrap">{dataHora(e.iniciadoEm)}</td>
                    <td className="py-2 pr-3">{duracao(e.duracaoSegundos)}</td>
                    <td className="py-2 pr-3 text-right">
                      {e.contadores.pacientes - e.contadores.pacientesInalterados}
                      {e.contadores.pacientesInalterados > 0 ? (
                        <span className="text-gray-400" title={`${e.contadores.pacientesInalterados} relido(s) sem mudança`}>
                          {' '}+{e.contadores.pacientesInalterados}
                        </span>
                      ) : null}
                    </td>
                    <td className="py-2 pr-3 text-right">{e.contadores.encounters}</td>
                    <td className="py-2 text-right">{e.contadores.observations}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
