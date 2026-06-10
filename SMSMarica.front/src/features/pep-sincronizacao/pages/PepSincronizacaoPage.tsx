import { useEffect, useMemo, useState } from 'react';
import { DatabaseZap, Loader2, Play, RefreshCw, StopCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import {
  useBasesPep,
  useCancelarImportacaoPep,
  useExecucoesPep,
  useIniciarImportacaoPep,
  useStatusPep,
} from '@/features/pep-sincronizacao/api/queries';
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

function PainelContadores({ s }: { s: StatusImportacao }) {
  const c = s.contadores;
  const itens = [
    ['Médicos', c.medicos],
    ['Pacientes', c.pacientes],
    ['Atendimentos', c.encounters],
    ['Diagnósticos', c.conditions],
    ['Medicações', c.medicationRequests],
    ['Documentos', c.documentReferences],
    ['Sinais/risco', c.observations],
    ['Falhas', c.falhas],
  ] as const;
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      {itens.map(([rotulo, valor]) => (
        <div key={rotulo} className="rounded-lg border border-gray-200 bg-gray-50 px-3 py-2">
          <div className="text-lg font-semibold text-gray-900">{valor}</div>
          <div className="text-xs text-gray-500">{rotulo}</div>
        </div>
      ))}
    </div>
  );
}

export function PepSincronizacaoPage() {
  const bases = useBasesPep();
  const status = useStatusPep();
  const execucoes = useExecucoesPep();
  const iniciar = useIniciarImportacaoPep();
  const cancelar = useCancelarImportacaoPep();
  const podeImportar = usePermissao('SincronizacaoPep', 'Edicao');

  const [fonteId, setFonteId] = useState('');
  const [modo, setModo] = useState<ModoSincronizacao>('Completo');
  const [escopo, setEscopo] = useState<EscopoSincronizacao>('Limitado');
  const [maxMedicos, setMaxMedicos] = useState('10');
  const [maxPacientes, setMaxPacientes] = useState('10');
  const [concorrencia, setConcorrencia] = useState('8');
  const [apagarAntes, setApagarAntes] = useState(false);
  const [cursorInicial, setCursorInicial] = useState('');
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
    const cursor =
      modo === 'Completo' && escopo === 'Tudo' ? Number(cursorInicial) || null : null;
    iniciar.mutate(
      {
        fonteId,
        modo,
        escopo,
        maxMedicos: escopo === 'Limitado' ? Number(maxMedicos) || null : null,
        maxPacientes: escopo === 'Limitado' ? Number(maxPacientes) || null : null,
        // Com cursor, NÃO apaga antes: senão zeraria a base e recomeçaria do ponteiro,
        // perdendo tudo antes dele sem repor nesta rodada.
        apagarAntes: modo === 'Completo' && cursor == null ? apagarAntes : false,
        concorrencia: Number(concorrencia) || null,
        cursorPacienteInicial: cursor,
      },
      { onError: (err) => setErro(extrairMensagemDeErro(err)) },
    );
  }

  function aoParar() {
    if (!window.confirm('Parar a importação em andamento? No modo Completo, dá para retomar de onde parou depois.')) {
      return;
    }
    cancelar.mutate(undefined, { onError: (err) => window.alert(extrairMensagemDeErro(err)) });
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

      <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <form onSubmit={aoIniciar} className="space-y-5">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo label="Base de origem" htmlFor="pep-base" className="sm:col-span-2">
              <Select id="pep-base" value={fonteId} onChange={(e) => setFonteId(e.target.value)}>
                <option value="" disabled>
                  {bases.isPending ? 'Carregando bases…' : 'Selecione uma base'}
                </option>
                {bases.data?.map((b) => (
                  <option key={b.id} value={b.id} disabled={!b.suportada}>
                    {b.nome} ({b.tipo} · {b.ambiente}){b.suportada ? '' : ' — não suportada'}
                  </option>
                ))}
              </Select>
            </Campo>

            <Campo label="Modo" htmlFor="pep-modo" dica="Completo = do zero; Incremental = só o que mudou desde o último sincronismo.">
              <Select id="pep-modo" value={modo} onChange={(e) => setModo(e.target.value as ModoSincronizacao)}>
                <option value="Completo">Completo</option>
                <option value="Incremental">Incremental (desde o último)</option>
              </Select>
            </Campo>

            <Campo label="Escopo" htmlFor="pep-escopo" dica="Limitado = quantidade fixa (fase de teste); Tudo = base inteira.">
              <Select id="pep-escopo" value={escopo} onChange={(e) => setEscopo(e.target.value as EscopoSincronizacao)}>
                <option value="Limitado">Limitado (N médicos / N pacientes)</option>
                <option value="Tudo">Tudo</option>
              </Select>
            </Campo>

            {escopo === 'Limitado' ? (
              <>
                <Campo label="Máx. médicos" htmlFor="pep-med">
                  <Input id="pep-med" type="number" min={0} value={maxMedicos} onChange={(e) => setMaxMedicos(e.target.value)} />
                </Campo>
                <Campo label="Máx. pacientes" htmlFor="pep-pac">
                  <Input id="pep-pac" type="number" min={0} value={maxPacientes} onChange={(e) => setMaxPacientes(e.target.value)} />
                </Campo>
              </>
            ) : null}

            <Campo
              label="Requisições em paralelo"
              htmlFor="pep-conc"
              dica="Quantas escritas simultâneas no hub (1–64). Mais = mais rápido; menos = menos memória/carga."
            >
              <Input id="pep-conc" type="number" min={1} max={64} value={concorrencia} onChange={(e) => setConcorrencia(e.target.value)} />
            </Campo>
          </div>

          {modo === 'Completo' && escopo === 'Tudo' ? (
            <div className="rounded-lg border border-gray-200 bg-gray-50 p-3">
              <Campo
                label="Iniciar a partir do cd_paciente (opcional)"
                htmlFor="pep-cursor"
                dica="Vazio = base inteira, do começo. Informe um cd para retomar/recomeçar de um ponto (a paginação é decrescente; reprocessar a fronteira é seguro)."
              >
                <Input
                  id="pep-cursor"
                  type="number"
                  min={0}
                  value={cursorInicial}
                  onChange={(e) => setCursorInicial(e.target.value)}
                  placeholder="ex.: 238000"
                />
              </Campo>
              {baseSel?.cursorPacienteCd != null ? (
                <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-amber-700">
                  <span>
                    Importação completa interrompida nesta base — último bloco em{' '}
                    <span className="font-mono font-semibold">{baseSel.cursorPacienteCd}</span>.
                  </span>
                  <button
                    type="button"
                    onClick={() => setCursorInicial(String(baseSel.cursorPacienteCd))}
                    className="rounded-md border border-amber-300 bg-white px-2 py-0.5 font-medium text-amber-800 hover:bg-amber-50"
                  >
                    Retomar daqui
                  </button>
                </div>
              ) : null}
            </div>
          ) : null}

          {modo === 'Completo' ? (
            <label className={`flex items-center gap-2 text-sm ${cursorInicial ? 'text-gray-400' : 'text-gray-700'}`}>
              <input
                type="checkbox"
                checked={apagarAntes && !cursorInicial}
                disabled={!!cursorInicial}
                onChange={(e) => setApagarAntes(e.target.checked)}
              />
              Apagar os recursos do hub antes de importar (full refresh)
              {cursorInicial ? <span className="text-xs">— desabilitado ao usar ponteiro inicial</span> : null}
            </label>
          ) : null}

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
              Iniciar importação
            </Button>
          </div>
        </form>
      </section>

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
            {emExecucao ? <div><span className="text-gray-500">Paralelas:</span> {concorrencia}</div> : null}
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
                  <th className="py-2 pr-3">Status</th>
                  <th className="py-2 pr-3">Início</th>
                  <th className="py-2 pr-3">Duração</th>
                  <th className="py-2 pr-3 text-right">Pac.</th>
                  <th className="py-2 pr-3 text-right">Atend.</th>
                  <th className="py-2 text-right">Obs.</th>
                </tr>
              </thead>
              <tbody>
                {execucoes.data.map((e) => (
                  <tr key={e.id} className="border-b border-gray-100">
                    <td className="py-2 pr-3">{e.fonteNome}</td>
                    <td className="py-2 pr-3">{e.modo} / {e.escopo}</td>
                    <td className="py-2 pr-3"><Badge status={e.status} /></td>
                    <td className="py-2 pr-3 whitespace-nowrap">{dataHora(e.iniciadoEm)}</td>
                    <td className="py-2 pr-3">{duracao(e.duracaoSegundos)}</td>
                    <td className="py-2 pr-3 text-right">{e.contadores.pacientes}</td>
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
