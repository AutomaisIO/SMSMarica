import { useState } from 'react';
import { AlertTriangle, KeyRound, Loader2, Play, RefreshCw, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useDispararVarreduraSer,
  useExecucoesSer,
  useSalvarCredencialSer,
  useStatusMotorSer,
  useTestarCredencialSer,
} from '@/features/ser/api/queries';
import type { ExecucaoSer, ModoVarreduraSer, StatusVarreduraSer } from '@/features/ser/types';

const CLASSE_STATUS: Record<StatusVarreduraSer, string> = {
  Pendente: 'bg-slate-50 text-slate-700 border-slate-200',
  EmExecucao: 'bg-blue-50 text-blue-700 border-blue-200',
  Concluida: 'bg-green-50 text-green-700 border-green-200',
  Parcial: 'bg-amber-50 text-amber-700 border-amber-200',
  Erro: 'bg-red-50 text-red-700 border-red-200',
  Cancelada: 'bg-orange-50 text-orange-700 border-orange-200',
  // Roxo, e não vermelho: parada retomável não é erro — ninguém precisa agir, ela volta sozinha.
  Interrompida: 'bg-purple-50 text-purple-700 border-purple-200',
};

const ROTULO_MODO: Record<ModoVarreduraSer, string> = {
  CargaInicial: 'Carga inicial (tudo + histórico)',
  Diaria: 'Diária (diff + histórico de quem mudou e da fila)',
  SomenteGrade: 'Só a grade (sem histórico)',
};

function dataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('pt-BR');
}

function data(iso: string | null): string {
  if (!iso) return '—';
  const [ano, mes, dia] = iso.slice(0, 10).split('-');
  return dia && mes && ano ? `${dia}/${mes}/${ano}` : '—';
}

/**
 * O ponteiro de retomada em uma linha. Sem isso, uma rodada de horas que reinicia parece travada:
 * o operador precisa enxergar que ela ANDOU, e a partir de onde ela continua.
 */
function descreverFase(x: ExecucaoSer): string {
  switch (x.fase) {
    case 'Grade':
      return x.cursorSituacao
        ? `Grade · ${x.cursorSituacao} a partir de ${data(x.cursorData)}`
        : 'Grade · começando';
    case 'Historico':
      return x.historicosPendentes > 0
        ? `Histórico · faltam ${x.historicosPendentes}`
        : 'Histórico · terminando';
    case 'Finalizada':
      return 'Finalizada';
    default:
      return '—';
  }
}

function duracao(seg: number | null): string {
  if (seg == null) return '—';
  if (seg < 60) return `${seg}s`;
  const m = Math.floor(seg / 60);
  return `${m}m ${seg % 60}s`;
}

export function AbaSerConfiguracao() {
  const { data: status, isLoading } = useStatusMotorSer();
  // A lista segue o mesmo ritmo do status: é nela que ficam fase, cursor e pendentes da rodada.
  const { data: execucoes } = useExecucoesSer(20, status?.varreduraEmAndamento ?? false);
  const disparar = useDispararVarreduraSer();
  const testar = useTestarCredencialSer();
  const salvar = useSalvarCredencialSer();

  const [modo, setModo] = useState<ModoVarreduraSer>('Diaria');
  const [usuario, setUsuario] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  async function aoDisparar() {
    setErro(null);
    setAviso(null);
    try {
      await disparar.mutateAsync({ modo });
      setAviso('Varredura enfileirada. Ela roda em segundo plano — acompanhe abaixo.');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoTestar() {
    setErro(null);
    setAviso(null);
    try {
      await testar.mutateAsync({ usuario, senha });
      setAviso('Credencial aceita pelo SER e módulo Ambulatório acessível.');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoSalvar() {
    setErro(null);
    setAviso(null);
    try {
      await salvar.mutateAsync({ usuario, senha });
      // A senha não fica na tela depois de salva: ela é write-only no store.
      setSenha('');
      setAviso('Credencial validada no SER e salva (cifrada). O motor já pode rodar.');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const colunas: Coluna<ExecucaoSer>[] = [
    {
      chave: 'iniciadoEm',
      cabecalho: 'Início',
      className: 'w-40',
      render: (x) => <span className="text-xs">{dataHora(x.iniciadoEm)}</span>,
    },
    {
      chave: 'modo',
      cabecalho: 'Modo',
      className: 'w-32',
      render: (x) => <span className="text-xs">{x.modo}</span>,
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      className: 'w-28',
      render: (x) => (
        <span className={`rounded-full border px-2 py-0.5 text-xs ${CLASSE_STATUS[x.status]}`}>
          {x.status}
        </span>
      ),
    },
    {
      chave: 'volume',
      cabecalho: 'Solicitações',
      render: (x) => (
        <span className="text-xs">
          {x.solicitacoesEncontradas} vistas · {x.solicitacoesNovas} novas ·{' '}
          {x.mudancasSituacao} mudaram
        </span>
      ),
    },
    {
      chave: 'historico',
      cabecalho: 'Histórico',
      render: (x) => (
        <span className="text-xs">
          {x.historicosLidos} lidos · {x.eventosNovos} eventos ·{' '}
          <strong className="text-purple-700">{x.followUpsNovos} FollowUP</strong>
        </span>
      ),
    },
    {
      chave: 'truncadas',
      cabecalho: 'Cobertura',
      className: 'w-36',
      render: (x) =>
        x.fatiasTruncadas > 0 ? (
          // > 0 significa registros que NÃO foram lidos. Precisa gritar.
          <span className="inline-flex items-center gap-1 text-xs font-medium text-amber-700">
            <AlertTriangle className="size-3.5" />
            {x.fatiasTruncadas} fatia(s) truncada(s)
          </span>
        ) : (
          <span className="text-xs text-slate-500">completa</span>
        ),
    },
    {
      chave: 'progresso',
      cabecalho: 'Progresso',
      className: 'w-56',
      render: (x) => (
        <div className="space-y-0.5 text-xs">
          <div className="text-slate-700">{descreverFase(x)}</div>
          {x.retomadas > 0 && (
            // Rodada que reinicia sozinha é normal (deploy no meio); reiniciar MUITAS vezes é
            // sintoma de serviço caindo — por isso o número aparece.
            <div className="text-purple-700">
              retomada {x.retomadas}× · última em {dataHora(x.retomadaEm)}
            </div>
          )}
        </div>
      ),
    },
    {
      chave: 'duracao',
      cabecalho: 'Duração',
      className: 'w-24',
      render: (x) => <span className="text-xs">{duracao(x.duracaoSegundos)}</span>,
    },
  ];

  return (
    <div className="space-y-4">
      {isLoading && <p className="text-sm text-slate-500">Carregando estado do motor…</p>}

      {status && (
        <>
          <div className="grid gap-3 sm:grid-cols-4">
            <div className="rounded-lg border border-slate-200 bg-white p-3">
              <div className="text-lg font-semibold">{status.totalSolicitacoes}</div>
              <div className="text-xs text-slate-500">Solicitações espelhadas</div>
            </div>
            <div className="rounded-lg border border-slate-200 bg-white p-3">
              <div className="text-lg font-semibold">{status.totalEventos}</div>
              <div className="text-xs text-slate-500">Eventos de histórico</div>
            </div>
            <div className="rounded-lg border border-slate-200 bg-white p-3">
              <div className="text-lg font-semibold">{status.gatilhosPendentes}</div>
              <div className="text-xs text-slate-500">Gatilhos na fila</div>
            </div>
            <div className="rounded-lg border border-slate-200 bg-white p-3">
              <div
                className={`text-lg font-semibold ${
                  status.credencialConfigurada ? 'text-green-700' : 'text-red-700'
                }`}
              >
                {status.credencialConfigurada ? 'OK' : 'Falta'}
              </div>
              <div className="text-xs text-slate-500">Credencial do SER</div>
            </div>
          </div>

          {!status.credencialConfigurada && (
            <p className="rounded bg-amber-50 p-3 text-sm text-amber-800">
              A credencial do SER ainda não está cadastrada. Ela é gravada na tela de
              <strong> Integrações</strong>, no provedor <code>ser</code>. Use o teste abaixo para
              confirmar usuário e senha antes de salvar lá.
            </p>
          )}
        </>
      )}

      {erro && <p className="rounded bg-red-50 p-3 text-sm text-red-700">{erro}</p>}
      {aviso && <p className="rounded bg-green-50 p-3 text-sm text-green-800">{aviso}</p>}

      <section className="rounded-lg border border-slate-200 bg-white p-4">
        <h3 className="mb-3 text-sm font-semibold">Motor de atualização</h3>

        <div className="flex flex-wrap items-end gap-3">
          <Campo label="Modo da rodada" htmlFor="ser-modo-da-rodada" className="min-w-80 flex-1">
            <Select id="ser-modo-da-rodada" value={modo} onChange={(e) => setModo(e.target.value as ModoVarreduraSer)}>
              {(Object.keys(ROTULO_MODO) as ModoVarreduraSer[]).map((m) => (
                <option key={m} value={m}>
                  {ROTULO_MODO[m]}
                </option>
              ))}
            </Select>
          </Campo>

          <Button
            onClick={aoDisparar}
            disabled={disparar.isPending || status?.varreduraEmAndamento || !status?.credencialConfigurada}
          >
            {disparar.isPending ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <Play className="size-4" />
            )}
            Rodar agora
          </Button>
        </div>

        {status?.varreduraEmAndamento && (
          <p className="mt-3 flex items-center gap-2 rounded bg-blue-50 p-3 text-sm text-blue-800">
            <RefreshCw className="size-4 animate-spin" />
            Varredura em andamento. Só uma roda por vez — a sessão do SER é única por operador e
            duas rodadas se derrubariam.
          </p>
        )}

        <p className="mt-3 text-xs text-slate-500">
          A rodada leva de ~15 min (só grade) a cerca de 1 h (com o histórico de toda a fila) e
          roda em segundo plano. Não é possível filtrar por “alterado desde” — o SER não tem esse
          filtro, então o delta sai da comparação com o que já temos.
        </p>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-4">
        <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold">
          <KeyRound className="size-4 text-red-600" /> Credencial do SER
        </h3>
        <div className="flex flex-wrap items-end gap-3">
          <Campo label="Usuário" htmlFor="ser-usuario" className="w-56">
            <Input id="ser-usuario" value={usuario} onChange={(e) => setUsuario(e.target.value)} autoComplete="off" />
          </Campo>
          <Campo label="Senha" htmlFor="ser-senha" className="w-56">
            <Input
              id="ser-senha"
              type="password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              autoComplete="new-password"
            />
          </Campo>
          <Button variante="secundaria" onClick={aoTestar} disabled={testar.isPending || salvar.isPending}>
            {testar.isPending ? <Loader2 className="size-4 animate-spin" /> : null}
            Testar
          </Button>
          <Button onClick={aoSalvar} disabled={salvar.isPending || testar.isPending || !usuario || !senha}>
            {salvar.isPending ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
            Salvar
          </Button>
        </div>
        <p className="mt-2 text-xs text-slate-500">
          <strong>Testar</strong> só autentica e confere se o módulo Ambulatório abre, sem gravar
          nada. <strong>Salvar</strong> valida primeiro e só então guarda a credencial cifrada —
          credencial que não funciona faria o motor falhar de madrugada, sem ninguém por perto.
          A senha é write-only: depois de salva, não volta para a tela.
        </p>
        <p className="mt-1 text-xs text-amber-700">
          Atenção: a sessão do SER é única por operador. Testar ou salvar derruba a sessão de quem
          estiver logado no SER com esse usuário.
        </p>
      </section>

      <section>
        <h3 className="mb-2 text-sm font-semibold">Últimas rodadas</h3>
        <Tabela
          colunas={colunas}
          dados={execucoes ?? []}
          chaveLinha={(x) => x.id}
          scrollXFlutuante
          vazio={<div className="py-6 text-center text-sm text-slate-500">Nenhuma rodada ainda.</div>}
        />
      </section>
    </div>
  );
}
