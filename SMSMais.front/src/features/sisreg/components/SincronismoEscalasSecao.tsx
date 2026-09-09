import { useEffect, useState } from 'react';
import { CalendarRange, Loader2, RotateCw, UserCheck, X, XCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  useAgendamentoEscalas,
  useCancelarEscalas,
  useExecucoesEscalas,
  useSalvarAgendamentoEscalas,
  useBackfillExecutante,
  useSincronizarEscalas,
  useStatusEscalas,
} from '@/features/sisreg/api/queries';
import type { StatusMapeamentoLote } from '@/features/sisreg/types';

const CLASSE_STATUS: Record<StatusMapeamentoLote, string> = {
  Pendente: 'bg-gray-100 text-gray-700',
  EmExecucao: 'bg-blue-50 text-blue-700',
  Concluida: 'bg-emerald-50 text-emerald-700',
  Parcial: 'bg-amber-50 text-amber-800',
  Erro: 'bg-red-50 text-red-700',
  Cancelada: 'bg-gray-100 text-gray-600',
};

const ROTULO_STATUS: Record<StatusMapeamentoLote, string> = {
  Pendente: 'Na fila',
  EmExecucao: 'Rodando',
  Concluida: 'Concluída',
  Parcial: 'Parcial',
  Erro: 'Erro',
  Cancelada: 'Cancelada',
};

function dataHora(iso: string | null) {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' });
}

/**
 * Sincronismo da grade de ESCALAS do SISREG — a oferta de vagas (profissional × unidade ×
 * procedimento × dia da semana), que é a base da Agenda.
 *
 * <p>Deliberadamente mais simples que a seção do mapeamento logo acima, porque o motor é mais
 * simples: a tela `cons_escalas` aceita recorte sem critério nenhum, então <b>uma requisição traz a
 * rede inteira e todo o histórico</b>. Não há custo por unidade, rodízio, TTL nem prévia de
 * distribuição de horários para mostrar.</p>
 */
export function SincronismoEscalasSecao() {
  /** Ligado no clique, não na primeira resposta: a execução leva 1–2s para se registrar. */
  const [acompanhando, setAcompanhando] = useState(false);

  const status = useStatusEscalas(acompanhando);
  const execucoes = useExecucoesEscalas(acompanhando);
  const agendamento = useAgendamentoEscalas();
  const sincronizar = useSincronizarEscalas();
  const cancelar = useCancelarEscalas();
  const salvar = useSalvarAgendamentoEscalas();
  const backfill = useBackfillExecutante();

  const [ativo, setAtivo] = useState(false);
  /**
   * Horários do dia. É lista porque a escala é a OFERTA: um bloco novo ("Gastro abriu 20 vagas no
   * Conde") nasce no SISREG a qualquer hora, e vaga que a regulação só vê na madrugada seguinte
   * passa o dia sem ser aproveitada. Sincronizar de novo custa 1 requisição para a rede inteira.
   */
  const [horarios, setHorarios] = useState<string[]>(['02:30']);
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [agendaSalva, setAgendaSalva] = useState(false);
  const [resultadoBackfill, setResultadoBackfill] = useState<string | null>(null);

  useEffect(() => {
    if (agendamento.data) {
      setAtivo(agendamento.data.ativo);
      // `horariosLocais` é a verdade; `horaLocal` fica como reserva para o caso de o backend
      // ainda não ter subido quando o front sobe.
      const lista = agendamento.data.horariosLocais;
      setHorarios(lista && lista.length > 0 ? lista : [agendamento.data.horaLocal]);
    }
  }, [agendamento.data]);

  const emExecucao = status.data?.emExecucao ?? false;

  /** Para de acompanhar um pouco depois do fim, para a última atualização do histórico chegar. */
  useEffect(() => {
    if (!acompanhando || emExecucao) return;
    const t = setTimeout(() => setAcompanhando(false), 8000);
    return () => clearTimeout(t);
  }, [acompanhando, emExecucao]);

  async function aoSincronizar() {
    setErro(null);
    setAviso(null);
    setAcompanhando(true);
    try {
      const r = await sincronizar.mutateAsync();
      setAviso(r.mensagem);
    } catch (err) {
      setAcompanhando(false);
      setErro(extrairMensagemDeErro(err));
    }
  }

  async function aoSalvarAgenda(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setAgendaSalva(false);
    try {
      await salvar.mutateAsync({ ativo, horariosLocais: horarios });
      setAgendaSalva(true);
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <header className="mb-4">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <CalendarRange className="h-4 w-4 text-primary-600" />
          Sincronismo de escalas
        </h2>
        <p className="mt-1 max-w-3xl text-xs text-gray-600">
          Traz do SISREG a <strong>grade de vagas</strong> — quais profissionais atendem, em que
          unidade, para qual procedimento, em que dia da semana e horário, e quantas vagas de
          primeira vez, retorno e reserva. É a oferta; quem ocupa essas vagas são os agendamentos
          importados. Sem ela não dá para dizer quanto de uma agenda está livre.
        </p>
        <p className="mt-1 max-w-3xl text-xs text-gray-500">
          Custa <strong>uma requisição</strong>: o SISREG entrega a rede inteira e todo o histórico
          de uma vez. Escalas vencidas vêm junto de propósito — são o denominador da análise do
          passado.
        </p>
      </header>

      {/* Progresso vivo */}
      {status.data?.emExecucao ? (
        <div className="mb-4 rounded-lg border border-blue-200 bg-blue-50 p-3">
          <div className="flex items-center gap-2 text-sm text-blue-800">
            <Loader2 className="h-4 w-4 animate-spin" />
            <span className="font-medium">{status.data.fase}</span>
          </div>
          <div className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-blue-900 sm:grid-cols-4">
            <span>Lidas: {status.data.escalasLidas}</span>
            <span>Gravadas: {status.data.escalasGravadas}</span>
            <span>Novas: {status.data.escalasNovas}</span>
            <span>Atualizadas: {status.data.escalasAtualizadas}</span>
          </div>
          <div className="mt-2">
            <Button
              variante="ghost"
              tamanho="sm"
              disabled={cancelar.isPending}
              onClick={() => cancelar.mutate()}
            >
              <XCircle className="mr-1.5 h-3.5 w-3.5" />
              Cancelar
            </Button>
          </div>
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <Button disabled={sincronizar.isPending || emExecucao} onClick={aoSincronizar}>
          {sincronizar.isPending ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <RotateCw className="mr-2 h-4 w-4" />
          )}
          Sincronizar escalas agora
        </Button>
        {agendamento.data ? (
          <span className="text-xs text-gray-500">
            {agendamento.data.orcamentoRestante} requisição(ões) disponíveis nesta hora
          </span>
        ) : null}
      </div>

      {aviso ? (
        <p className="mt-3 rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-xs text-gray-700">
          {aviso}
        </p>
      ) : null}
      {erro ? (
        <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          {erro}
        </p>
      ) : null}

      {/* Agendamento diário */}
      <form onSubmit={aoSalvarAgenda} className="mt-4 border-t border-gray-100 pt-4">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} />
          Sincronizar as escalas todo dia
        </label>

        <div className="mt-3 flex flex-wrap items-end gap-3">
          <Campo
            label="Horários (Brasília)"
            htmlFor="escalas-hora-0"
            dica="Vaga e agenda nova nascem no SISREG a qualquer hora. Cada sincronismo custa 1 requisição para a rede inteira, e esta tela não sofre o bloqueio das 07:30 às 15:00."
          >
            <div className="flex flex-wrap items-center gap-2">
              {horarios.map((h, i) => (
                <div key={i} className="flex items-center gap-1">
                  <Input
                    id={`escalas-hora-${i}`}
                    type="time"
                    className="w-32"
                    value={h}
                    onChange={(e) =>
                      setHorarios((atual) => atual.map((v, j) => (j === i ? e.target.value : v)))
                    }
                  />
                  {horarios.length > 1 ? (
                    <button
                      type="button"
                      className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-red-600"
                      title="Remover este horário"
                      onClick={() => setHorarios((atual) => atual.filter((_, j) => j !== i))}
                    >
                      <X className="h-4 w-4" />
                    </button>
                  ) : null}
                </div>
              ))}
              {horarios.length < 6 ? (
                <button
                  type="button"
                  className="rounded-md border border-dashed border-gray-300 px-2 py-1.5 text-xs text-gray-600 hover:border-primary-400 hover:text-primary-700"
                  onClick={() => setHorarios((atual) => [...atual, '12:00'])}
                >
                  + horário
                </button>
              ) : null}
            </div>
          </Campo>
          <Button type="submit" variante="outline" className="mb-0.5" disabled={salvar.isPending}>
            {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Salvar agendamento
          </Button>
          {agendaSalva ? <span className="mb-2 text-xs text-green-600">Agendamento salvo.</span> : null}
        </div>

        <p className="mt-2 text-xs text-gray-500">
          O interruptor de sincronismo automático no topo da tela vale também para esta agenda:
          desligado lá, nada dispara sozinho aqui.
        </p>
      </form>

      {/* Profissional executante das solicitações já importadas */}
      <div className="mt-4 border-t border-gray-100 pt-4">
        <h3 className="flex items-center gap-2 text-sm font-medium text-gray-700">
          <UserCheck className="h-4 w-4 text-gray-500" />
          Completar o profissional executante
        </h3>
        <p className="mt-1 max-w-3xl text-xs text-gray-600">
          A escala é publicada <strong>por profissional</strong>, mas as solicitações importadas até
          agora não guardavam quem executa — o dado era lido do arquivo e descartado. Sem ele dá para
          medir a unidade, nunca abrir a agenda de uma pessoa.
        </p>
        <p className="mt-1 max-w-3xl text-xs text-gray-500">
          Relê a linha crua que já está guardada aqui: <strong>não fala com o SISREG</strong> —
          nenhuma requisição, nenhum risco de CAPTCHA. Rodar de novo é seguro, só toca no que está
          vazio.
        </p>
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <Button
            variante="outline"
            tamanho="sm"
            disabled={backfill.isPending}
            onClick={() => {
              setResultadoBackfill(null);
              setErro(null);
              backfill.mutate(undefined, {
                onSuccess: (r) => setResultadoBackfill(r.mensagem),
                onError: (e) => setErro(extrairMensagemDeErro(e)),
              });
            }}
          >
            {backfill.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <UserCheck className="mr-2 h-4 w-4" />
            )}
            Completar executante
          </Button>
          {backfill.isPending ? (
            <span className="text-xs text-gray-500">
              Relendo as solicitações — pode levar um minuto.
            </span>
          ) : null}
        </div>
        {resultadoBackfill ? (
          <p className="mt-3 rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-800">
            {resultadoBackfill}
          </p>
        ) : null}
      </div>

      {/* Histórico */}
      {execucoes.data && execucoes.data.length > 0 ? (
        <div className="mt-4 border-t border-gray-100 pt-4">
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
            Últimas sincronizações
          </h3>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[46rem] text-left text-xs">
              <thead className="text-gray-500">
                <tr>
                  <th className="py-1 pr-3 font-medium">Quando</th>
                  <th className="py-1 pr-3 font-medium">Status</th>
                  <th className="py-1 pr-3 font-medium">Lidas</th>
                  <th className="py-1 pr-3 font-medium">Novas</th>
                  <th className="py-1 pr-3 font-medium">Atualizadas</th>
                  <th className="py-1 pr-3 font-medium">Sumiram</th>
                  <th className="py-1 pr-3 font-medium" title="Linhas que o SISREG mandou quebradas. Um número estável é o normal — o que importa é ele crescer.">
                    Rejeitadas
                  </th>
                  <th className="py-1 pr-3 font-medium" title="CNES sem unidade no cadastro. Zero é o esperado; valor aqui é unidade nova no SISREG.">
                    Sem unidade
                  </th>
                  <th className="py-1 font-medium">Quem</th>
                </tr>
              </thead>
              <tbody>
                {execucoes.data.map((e) => (
                  <tr key={e.id} className="border-t border-gray-100">
                    <td className="py-1.5 pr-3 text-gray-700">{dataHora(e.iniciadoEm)}</td>
                    <td className="py-1.5 pr-3">
                      <span className={`rounded-full px-2 py-0.5 ${CLASSE_STATUS[e.status]}`}>
                        {ROTULO_STATUS[e.status]}
                      </span>
                      {e.mensagemErro ? (
                        <span className="ml-1 text-gray-400" title={e.mensagemErro}>
                          ⓘ
                        </span>
                      ) : null}
                    </td>
                    <td className="py-1.5 pr-3">{e.escalasLidas}</td>
                    <td className="py-1.5 pr-3">{e.escalasNovas}</td>
                    <td className="py-1.5 pr-3">{e.escalasAtualizadas}</td>
                    <td className="py-1.5 pr-3">{e.escalasAusentes}</td>
                    <td className={`py-1.5 pr-3 ${e.linhasRejeitadas > 0 ? 'text-amber-700' : ''}`}>
                      {e.linhasRejeitadas}
                    </td>
                    <td className={`py-1.5 pr-3 ${e.unidadesNaoEncontradas > 0 ? 'text-amber-700' : ''}`}>
                      {e.unidadesNaoEncontradas}
                    </td>
                    <td className="py-1.5 text-gray-500">
                      {e.disparo === 'Agendado' ? 'automático' : (e.criadoPorNome ?? '—')}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </section>
  );
}
