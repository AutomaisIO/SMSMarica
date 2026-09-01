import { useEffect, useState } from 'react';
import { CalendarClock, Loader2, RefreshCw, Rocket, RotateCw, XCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  useAgendamentoMapeamentoLote,
  useCancelarMapeamentoLote,
  useExecucoesMapeamentoLote,
  useSalvarAgendamentoMapeamentoLote,
  usePrepararRedeSisreg,
  useSincronizarMapeamentoLote,
  useStatusMapeamentoLote,
} from '@/features/sisreg/api/queries';
import { ModalDetalheMapeamentoLote } from '@/features/sisreg/components/ModalDetalheMapeamentoLote';
import type { MapeamentoLoteExecucao, StatusMapeamentoLote } from '@/features/sisreg/types';

/** Espaço entre os horários de duas unidades. 20 min faz as ~42 caberem entre 15:00 e 07:30. */
const INTERVALO_MINUTOS = 20;

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

/**
 * "SISREG Sincroniza tudo" (#118): lê no SISREG <b>todas</b> as unidades que a credencial enxerga,
 * cria aqui as que faltam e atualiza o mapeamento de médicos e procedimentos de cada uma.
 *
 * <p>A tela precisa contar duas histórias que o botão sozinho não conta: <b>o custo</b> (quantas
 * requisições ainda cabem antes do CAPTCHA anti-robô) e <b>o resultado</b> (quantas unidades o
 * SISREG tem, quantas nasceram aqui, quantos médicos por unidade) — daí o histórico embaixo, que
 * sobrevive ao fim da execução.</p>
 */
export function SincronizarTudoSecao() {
  /**
   * Ligado no clique, não na primeira resposta: o lote leva 1–2s para se registrar, e esperar por
   * ele é o que deixava a tela parada em "nada rodando" durante a sincronização inteira.
   */
  const [acompanhando, setAcompanhando] = useState(false);

  const status = useStatusMapeamentoLote(acompanhando);
  const execucoes = useExecucoesMapeamentoLote(acompanhando);
  const agendamento = useAgendamentoMapeamentoLote();
  const sincronizar = useSincronizarMapeamentoLote();
  const cancelar = useCancelarMapeamentoLote();
  const salvar = useSalvarAgendamentoMapeamentoLote();
  const prepararRede = usePrepararRedeSisreg();

  const [ativo, setAtivo] = useState(false);
  const [hora, setHora] = useState('03:30');
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [agendaSalva, setAgendaSalva] = useState(false);
  const [detalhe, setDetalhe] = useState<MapeamentoLoteExecucao | null>(null);

  useEffect(() => {
    if (agendamento.data) {
      setAtivo(agendamento.data.ativo);
      setHora(agendamento.data.horaLocal);
    }
  }, [agendamento.data]);

  const emExecucao = status.data?.emExecucao ?? false;

  /**
   * Para de acompanhar quando o lote sai do ar. O atraso dá tempo de a última atualização do
   * histórico chegar — parar no mesmo instante deixaria a linha final desatualizada na tela.
   */
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

  async function aoCancelar() {
    setErro(null);
    try {
      await cancelar.mutateAsync();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  async function aoSalvarAgenda(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setAgendaSalva(false);
    try {
      await salvar.mutateAsync({ ativo, horaLocal: hora });
      setAgendaSalva(true);
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  async function alternarBootstrap(ligar: boolean) {
    setErro(null);
    setAviso(null);
    try {
      await salvar.mutateAsync({ ativo, horaLocal: hora, bootstrap: ligar });
      if (ligar) setAcompanhando(true);
      setAviso(
        ligar
          ? 'Carga inicial ligada. As rodadas seguem sozinhas, respeitando o limite do SISREG.'
          : 'Carga inicial pausada.',
      );
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  async function aoPrepararRede() {
    setErro(null);
    setAviso(null);
    try {
      const r = await prepararRede.mutateAsync({
        intervaloMinutos: INTERVALO_MINUTOS,
        horaInicialLocal: '15:00',
        diasAFrente: 21,
        habilitar: true,
      });
      setAviso(r.mensagem);
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  const pendentes = agendamento.data?.pendentesPrimeiroMapeamento ?? 0;
  const bootstrapLigado = agendamento.data?.bootstrap ?? false;
  const s = status.data;
  const pct = s && s.unidadesTotal > 0 ? Math.round((s.unidadesFeitas / s.unidadesTotal) * 100) : 0;

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <header className="mb-4">
        <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
          <RefreshCw className="h-5 w-5 text-primary-600" />
          Sincronizar unidades, médicos e procedimentos (rede inteira)
        </h2>
        <p className="mt-1 text-sm text-gray-600">
          Lê no SISREG todas as unidades que a credencial enxerga, cadastra aqui as que ainda não
          existem e atualiza os médicos e procedimentos de cada uma. Roda no servidor, em sequência.
          Para não esbarrar no bloqueio anti-robô do SISREG, cada rodada atualiza primeiro as
          unidades mais desatualizadas e deixa as recém-atualizadas para a próxima — em algumas
          rodadas a rede inteira se cobre.
        </p>
      </header>

      {erro ? (
        <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      {aviso && !emExecucao ? (
        <div className="mb-3 rounded-md border border-blue-200 bg-blue-50 px-3 py-2 text-sm text-blue-700">
          {aviso}
        </div>
      ) : null}

      {/* Progresso do lote em curso */}
      {s && emExecucao ? (
        <div className="mb-4 rounded-lg border border-gray-200 bg-gray-50 p-4">
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2 text-sm text-gray-700">
            <span className="flex items-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin text-primary-600" />
              {s.unidadesTotal > 0
                ? `${s.fase} — ${s.unidadesFeitas}/${s.unidadesTotal}`
                : `${s.fase}…`}
              {s.unidadeAtual ? ` · ${s.unidadeAtual}` : ''}
            </span>
            <span className="tabular-nums text-gray-500">
              {s.requisicoesFeitas} requisições · restam {s.orcamentoRestante} nesta hora
            </span>
          </div>
          <div className="h-2 w-full overflow-hidden rounded-full bg-gray-200">
            <div
              className="h-full rounded-full bg-primary-600 transition-all"
              style={{ width: `${pct}%` }}
            />
          </div>
          <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500">
            <span>{s.unidadesNoSisreg} unidades no SISREG</span>
            {s.unidadesCriadas > 0 ? (
              <span className="font-medium text-emerald-700">
                {s.unidadesCriadas} unidade(s) criada(s)
              </span>
            ) : null}
            <span>{s.unidadesMapeadas} mapeadas</span>
            {s.unidadesPuladas > 0 ? <span>{s.unidadesPuladas} já atualizadas</span> : null}
            <span>
              {s.profissionaisEncontrados} médicos ({s.profissionaisNovos} novos)
            </span>
            <span>
              {s.procedimentosEncontrados} procedimentos ({s.procedimentosNovos} novos)
            </span>
            <span>
              {s.practitionersCriados} criados no hub / {s.practitionersVinculados} vinculados
            </span>
            {s.unidadesComErro > 0 ? (
              <span className="text-amber-600">{s.unidadesComErro} unidade(s) com erro</span>
            ) : null}
          </div>
          {s.ultimoErro ? <p className="mt-2 text-xs text-amber-700">{s.ultimoErro}</p> : null}
          <div className="mt-3">
            <Button type="button" variante="outline" onClick={aoCancelar} disabled={cancelar.isPending}>
              <XCircle className="mr-2 h-4 w-4" />
              Cancelar
            </Button>
          </div>
        </div>
      ) : (
        <div className="mb-4">
          <Button type="button" onClick={aoSincronizar} disabled={sincronizar.isPending}>
            {sincronizar.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <RotateCw className="mr-2 h-4 w-4" />
            )}
            Sincronizar tudo agora
          </Button>
        </div>
      )}

      {/* Carga inicial da rede — o passo que só existe uma vez */}
      {pendentes > 0 ? (
        <div className="mb-4 rounded-lg border border-amber-200 bg-amber-50 p-4">
          <h3 className="text-sm font-semibold text-amber-900">
            Carga inicial: faltam {pendentes} unidades
          </h3>
          <p className="mt-1 text-sm text-amber-800">
            Trazer médicos e procedimentos de todas elas custa cerca de{' '}
            {(pendentes * 42).toLocaleString('pt-BR')} acessos ao SISREG, e o limite é por hora
            (restam {agendamento.data?.orcamentoRestante ?? 0} nesta). Ligando a carga inicial, o
            sistema dispara uma rodada atrás da outra assim que o limite reabre, sozinho, até não
            faltar nenhuma — e então se desliga.
          </p>
          <div className="mt-3">
            {bootstrapLigado ? (
              <div className="flex flex-wrap items-center gap-3">
                <span className="flex items-center gap-2 text-sm font-medium text-amber-900">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Carga inicial em andamento — segue sozinha, inclusive com a aba fechada.
                </span>
                <Button
                  type="button"
                  variante="outline"
                  tamanho="sm"
                  disabled={salvar.isPending}
                  onClick={() => alternarBootstrap(false)}
                >
                  Pausar
                </Button>
              </div>
            ) : (
              <Button
                type="button"
                disabled={salvar.isPending}
                onClick={() => alternarBootstrap(true)}
              >
                {salvar.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Rocket className="mr-2 h-4 w-4" />
                )}
                Ligar carga inicial
              </Button>
            )}
          </div>
        </div>
      ) : null}

      {/* Programar o diário da rede inteira */}
      <div className="mb-4 rounded-lg border border-gray-200 p-4">
        <h3 className="text-sm font-semibold text-gray-900">Programar o sincronismo diário</h3>
        <p className="mt-1 text-sm text-gray-600">
          Habilita todos os médicos e procedimentos já mapeados e liga a importação diária de cada
          unidade, em horários separados por {INTERVALO_MINUTOS} minutos, fora da faixa de 8h às 15h
          em que o SISREG bloqueia a exportação. O aviso por WhatsApp ao paciente fica{' '}
          <strong>desligado</strong> em todas.
        </p>
        <div className="mt-3">
          <Button
            type="button"
            variante="outline"
            disabled={prepararRede.isPending}
            onClick={aoPrepararRede}
          >
            {prepararRede.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <CalendarClock className="mr-2 h-4 w-4" />
            )}
            Programar todas as unidades
          </Button>
        </div>
      </div>

      {/* Agendamento diário */}
      <form onSubmit={aoSalvarAgenda} className="border-t border-gray-100 pt-4">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} />
          Sincronizar automaticamente todo dia
        </label>

        <div className="mt-3 flex flex-wrap items-end gap-3">
          <Campo label="Hora (Brasília)" htmlFor="lote-hora" dica="Recomendado de madrugada (ex.: 03:30).">
            <Input
              id="lote-hora"
              type="time"
              value={hora}
              onChange={(e) => setHora(e.target.value)}
              disabled={!ativo}
              className="w-32"
            />
          </Campo>
          <Button type="submit" variante="outline" disabled={salvar.isPending || agendamento.isPending}>
            {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Salvar agendamento
          </Button>
          {agendaSalva ? <span className="pb-2 text-sm text-green-600">Agendamento salvo.</span> : null}
        </div>
      </form>

      {/* Histórico — o que o progresso vivo esquece assim que termina */}
      {(execucoes.data?.length ?? 0) > 0 && (
        <div className="mt-4 border-t border-gray-100 pt-4">
          <h3 className="mb-2 text-sm font-medium text-gray-700">Sincronizações recentes</h3>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-gray-500">
                  <th className="py-1 pr-3 font-medium">Quando</th>
                  <th className="py-1 pr-3 font-medium">Disparo</th>
                  <th className="py-1 pr-3 font-medium">Status</th>
                  <th className="py-1 pr-3 font-medium">Unid. SISREG</th>
                  <th className="py-1 pr-3 font-medium">Criadas</th>
                  <th className="py-1 pr-3 font-medium">Mapeadas</th>
                  {/* Sem esta coluna, uma rodada que não teve o que fazer aparece como "0/42" e
                      lê-se como falha — quando é a economia funcionando: as unidades já estavam
                      atualizadas e não custaram acesso nenhum ao SISREG. */}
                  <th className="py-1 pr-3 font-medium">Já atualizadas</th>
                  <th className="py-1 pr-3 font-medium">Médicos</th>
                  <th className="py-1 pr-3 font-medium">Procedimentos</th>
                  <th className="py-1 pr-3 font-medium">Req.</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {execucoes.data!.map((e) => (
                  <tr
                    key={e.id}
                    className="cursor-pointer text-gray-700 hover:bg-gray-50"
                    onClick={() => setDetalhe(e)}
                    title="Ver o detalhe por unidade desta sincronização"
                  >
                    <td className="py-1.5 pr-3 whitespace-nowrap">
                      {new Date(e.iniciadoEm).toLocaleString('pt-BR')}
                    </td>
                    <td className="py-1.5 pr-3">{e.disparo === 'Agendado' ? '⏱ Agendado' : 'Manual'}</td>
                    <td className="py-1.5 pr-3">
                      <span className={`rounded px-1.5 py-0.5 text-xs ${CLASSE_STATUS[e.status]}`}>
                        {ROTULO_STATUS[e.status]}
                      </span>
                    </td>
                    <td className="py-1.5 pr-3">{e.unidadesNoSisreg || '—'}</td>
                    <td
                      className={`py-1.5 pr-3 ${
                        e.unidadesCriadas > 0 ? 'font-semibold text-emerald-700' : 'text-gray-400'
                      }`}
                    >
                      {e.unidadesCriadas > 0 ? e.unidadesCriadas : '—'}
                    </td>
                    <td className="py-1.5 pr-3 whitespace-nowrap">
                      {e.unidadesMapeadas}/{e.unidadesTotal}
                    </td>
                    <td className="py-1.5 pr-3 text-gray-500">
                      {e.unidadesPuladas > 0 ? e.unidadesPuladas : '—'}
                    </td>
                    <td className="py-1.5 pr-3 whitespace-nowrap">
                      {e.profissionaisEncontrados}
                      {e.profissionaisNovos > 0 && (
                        <span className="ml-1 text-xs font-semibold text-emerald-700">
                          +{e.profissionaisNovos}
                        </span>
                      )}
                    </td>
                    <td className="py-1.5 pr-3 whitespace-nowrap">
                      {e.procedimentosEncontrados}
                      {e.procedimentosNovos > 0 && (
                        <span className="ml-1 text-xs font-semibold text-emerald-700">
                          +{e.procedimentosNovos}
                        </span>
                      )}
                    </td>
                    <td className="py-1.5 pr-3 text-gray-500">{e.requisicoes}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* A rodada mais recente não mapeou nada e não deu erro: dizer POR QUÊ, aqui, em vez de
              deixar o "0/42" sozinho na tabela sugerindo que alguma coisa quebrou. */}
          {execucoes.data![0].unidadesMapeadas === 0 &&
          execucoes.data![0].unidadesPuladas > 0 &&
          execucoes.data![0].unidadesComErro === 0 ? (
            <p className="mt-2 text-xs text-gray-500">
              A última sincronização não precisou buscar nada: as{' '}
              {execucoes.data![0].unidadesPuladas} unidades já estavam atualizadas. É assim que se
              economiza acesso ao SISREG — só o que está desatualizado é buscado de novo. Para forçar
              a releitura de uma unidade específica, use <strong>Atualizar mapeamento</strong> na aba
              SISREG dela.
            </p>
          ) : null}
        </div>
      )}

      {detalhe && (
        <ModalDetalheMapeamentoLote execucao={detalhe} aoFechar={() => setDetalhe(null)} />
      )}
    </section>
  );
}
