import { useEffect, useState } from 'react';
import { CalendarClock, CalendarRange, History, Loader2, Play, RotateCcw, Square } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  useAlternarHistoricoVarredura,
  useAvancarHistoricoVarredura,
  useCancelarVarredura,
  useExecutarVarredura,
  useSalvarVarreduraAgenda,
  useStatusVarredura,
  useVarreduraAgenda,
  useVarreduraExecucoes,
} from '@/features/sisreg-mapeamento/api/queries';
import { ModalVarreduraPeriodo } from '@/features/sisreg-mapeamento/components/ModalVarreduraPeriodo';
import { ModalDetalheVarredura } from '@/features/sisreg-mapeamento/components/ModalDetalheVarredura';
import type { StatusVarredura, VarreduraExecucao } from '@/features/sisreg-mapeamento/types';

type Props = { unidadeId: string; podeEditar: boolean };

const CLASSE_STATUS: Record<StatusVarredura, string> = {
  Pendente: 'bg-gray-100 text-gray-700',
  EmExecucao: 'bg-blue-50 text-blue-700',
  Concluida: 'bg-emerald-50 text-emerald-700',
  Parcial: 'bg-amber-50 text-amber-800',
  Erro: 'bg-red-50 text-red-700',
  Cancelada: 'bg-gray-100 text-gray-600',
};

const ROTULO_STATUS: Record<StatusVarredura, string> = {
  Pendente: 'Na fila',
  EmExecucao: 'Rodando',
  Concluida: 'Concluída',
  Parcial: 'Parcial',
  Erro: 'Erro',
  Cancelada: 'Cancelada',
};

/**
 * Sincronismo diário da agenda do SISREG para UMA unidade: liga/desliga e hora,
 * disparo manual e as varreduras recentes.
 *
 * O bloco existe para responder duas perguntas que o operador precisa fazer ANTES de confiar no
 * motor: quanto vai custar (requisições estimadas × teto) e o que está de fora (procedimentos
 * habilitados sem SIGTAP confirmado não são varridos).
 */
export function SincronismoSisregSecao({ unidadeId, podeEditar }: Props) {
  const [ativo, setAtivo] = useState(false);
  const [hora, setHora] = useState('04:30');
  const [dias, setDias] = useState('21');
  const [aviso, setAviso] = useState<{ tipo: 'ok' | 'erro'; texto: string } | null>(null);

  /**
   * Ligado ao disparar. Enquanto vale, status e histórico se refazem sozinhos — a varredura pode
   * terminar em 2 s, e sem isto a tela fica parada mostrando "Rodando" de algo já encerrado.
   */
  const [acompanhando, setAcompanhando] = useState(false);

  /** Modal de disparo por período aberto. */
  const [periodoAberto, setPeriodoAberto] = useState(false);
  /** Execução cujo detalhe está aberto no modal, ou null. */
  const [detalhe, setDetalhe] = useState<VarreduraExecucao | null>(null);

  const agenda = useVarreduraAgenda(unidadeId);
  const alternarHistorico = useAlternarHistoricoVarredura(unidadeId);
  const avancarHistorico = useAvancarHistoricoVarredura(unidadeId);
  const salvar = useSalvarVarreduraAgenda(unidadeId);
  const executar = useExecutarVarredura(unidadeId);
  const cancelar = useCancelarVarredura(unidadeId);
  const execucoes = useVarreduraExecucoes(unidadeId, acompanhando);

  // Linha viva na tabela = alguém está varrendo, mesmo que não tenha sido nesta aba (o periódico,
  // ou outra janela). Sem isto quem só OBSERVA abre a tela e nada se mexe: `acompanhando` só liga
  // no clique de executar, então o status nem começava a ser buscado e o painel ao vivo não
  // aparecia para quem não disparou.
  const algumaViva =
    execucoes.data?.some(
      (e) => (e.status === 'Pendente' || e.status === 'EmExecucao') && !e.semSinal,
    ) ?? false;

  const status = useStatusVarredura(unidadeId, acompanhando || algumaViva);

  const dados = agenda.data;
  const rodando = Boolean(status.data);

  // Minutos desde o início da corrida. Recalcula a cada poll do status — que é o que mantém o
  // número andando mesmo quando os contadores da varredura não andam, que é justamente o caso em
  // que o operador precisa saber há quanto tempo está assim.
  const decorrido = status.data
    ? Math.floor((Date.now() - new Date(status.data.iniciadoEm).getTime()) / 60000)
    : 0;

  useEffect(() => {
    if (!dados) return;
    setAtivo(dados.ativo);
    setHora(dados.horaLocal.slice(0, 5));
    setDias(String(dados.diasAFrente));
  }, [
    dados?.unidadeId,
    dados?.ativo,
    dados?.horaLocal,
    dados?.diasAFrente,
  ]);

  /**
   * Para de acompanhar quando a varredura sai do ar. O atraso dá tempo de a última atualização do
   * histórico chegar — parar no mesmo instante deixaria a linha final desatualizada na tela.
   */
  useEffect(() => {
    if (!acompanhando || rodando) return;
    const t = setTimeout(() => setAcompanhando(false), 6000);
    return () => clearTimeout(t);
  }, [acompanhando, rodando]);
  const pausado = Boolean(dados?.pausadoAte && new Date(dados.pausadoAte) > new Date());

  async function comAviso(acao: () => Promise<unknown>, sucesso: (r: unknown) => string) {
    setAviso(null);
    try {
      const r = await acao();
      setAviso({ tipo: 'ok', texto: sucesso(r) });
    } catch (e) {
      setAviso({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  if (agenda.isLoading) {
    return <p className="text-sm text-gray-500">Carregando sincronismo…</p>;
  }

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4">
      <header className="mb-3">
        <h3 className="flex items-center gap-2 font-medium text-gray-900">
          <CalendarClock className="h-5 w-5 text-gray-500" />
          Sincronismo diário da agenda
        </h3>
        <p className="mt-1 text-sm text-gray-600">
          Todo dia, no horário escolhido, o sistema lê no SISREG a agenda desta unidade e cria as
          solicitações. Traz a agenda inteira da unidade numa requisição, sem separar por
          profissional ou procedimento — e já aproveita para atualizar o mapeamento de médicos e
          procedimentos com o que vier nela, sem custo adicional.
        </p>
      </header>

      {/* Configuração */}
      {/* A janela deixou de ser configurável: escolher "21 dias" era o que fazia a agenda exibir
          como livre toda vaga além disso, por falta de dado. Quem quiser conferir o alcance real de
          cada rodada tem a coluna "Cobertura" no histórico abaixo. */}
      <p className="mb-3 text-xs text-gray-500">
        A varredura vai de hoje até a <strong>última escala ativa</strong> desta unidade — não até um
        número de dias escolhido. Só o horário do disparo é configurável.
      </p>

      <div className="flex flex-wrap items-end gap-4">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={ativo}
            disabled={!podeEditar}
            onChange={(e) => setAtivo(e.target.checked)}
          />
          Sincronizar a agenda todo dia
        </label>

        <Campo label="Hora (Brasília)" htmlFor="varredura-hora">
          <Input
            id="varredura-hora"
            type="time"
            className="w-32"
            value={hora}
            disabled={!podeEditar}
            onChange={(e) => setHora(e.target.value)}
          />
        </Campo>

        {podeEditar && (
          <Button
            tamanho="sm"
            disabled={salvar.isPending}
            onClick={() =>
              comAviso(
                () =>
                  salvar.mutateAsync({
                    ativo,
                    horaLocal: hora,
                    // A janela nao e mais escolha: vai ate a ultima escala da unidade.
                    // Reenvia o valor que ja esta gravado so para nao quebrar o contrato
                    // da API — o backend nao o usa mais para decidir o alcance.
                    diasAFrente: Number(dias) || 21,
                  }),
                () => 'Sincronismo salvo.',
              )
            }
          >
            {salvar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            Salvar
          </Button>
        )}
      </div>

      <p className="mt-2 text-xs text-gray-500">
        Roda a qualquer hora, <strong>menos</strong> entre {dados?.corteEntradaLocal?.slice(0, 5)} e{' '}
        {dados?.bloqueioFimLocal?.slice(0, 5)} (Brasília): o SISREG bloqueia a exportação da agenda
        das {dados?.bloqueioInicioLocal?.slice(0, 5)} às {dados?.bloqueioFimLocal?.slice(0, 5)}.
      </p>

      {/* Estado */}
      <dl className="mt-4 grid grid-cols-2 gap-3 border-t border-gray-100 pt-3 text-sm md:grid-cols-4">
        <div>
          <dt className="text-xs text-gray-500">Próxima varredura</dt>
          <dd className={pausado ? 'font-medium text-red-600' : 'text-gray-900'}>
            {pausado
              ? `pausada até ${dataHora(dados!.pausadoAte)}`
              : dados?.ativo
                ? dataHora(dados.proximoRunEm)
                : 'desligada'}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-gray-500">Última varredura</dt>
          <dd className="text-gray-900">{dataHora(dados?.ultimaExecucaoEm)}</dd>
        </div>
        {/* "Combinações prontas" saiu daqui: com a agenda vindo inteira numa requisição, o número
            de pares habilitados não tem mais relação com o custo — deixá-lo ao lado das requisições
            estimadas sugeria uma conta que não existe mais. Quantos médicos e procedimentos estão
            habilitados continua logo acima, no bloco do mapeamento. */}
        <div>
          <dt className="text-xs text-gray-500">Requisições estimadas</dt>
          <dd
            className={
              (dados?.requisicoesEstimadas ?? 0) > (dados?.tetoPorExecucao ?? 0)
                ? 'font-medium text-amber-700'
                : 'text-gray-900'
            }
          >
            ≈ {dados?.requisicoesEstimadas ?? 0} de {dados?.tetoPorExecucao ?? 0}
          </dd>
        </div>
      </dl>

      {(dados?.falhasConsecutivas ?? 0) > 0 && (
        <p className="mt-2 text-sm text-amber-700">
          {dados!.falhasConsecutivas} falhas consecutivas — confira a credencial do SISREG.
        </p>
      )}

      {/* Execução */}
      <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-gray-100 pt-3">
        {rodando ? (
          <>
            {/* O relógio ao lado do contador não é enfeite: em 08/09/2026 um operador ficou 46
                minutos olhando "24 requisições · 0 importadas" sem saber se era lentidão ou morte.
                "há 38 min" ao lado do mesmo número responde isso em 5 segundos. */}
            <span className="flex items-center gap-2 text-sm text-blue-700">
              <Loader2 className="h-4 w-4 animate-spin" />
              {status.data!.requisicoes} requisições · {status.data!.validos} importadas
              {status.data!.procedimentoAtual ? ` · ${status.data!.procedimentoAtual}` : ''}
              <span
                className={decorrido >= 20 ? 'font-medium text-amber-700' : 'text-blue-500'}
                title={`Começou ${dataHora(status.data!.iniciadoEm)}. Uma varredura de janela longa leva 20–45 min; muito além disso, confira o log.`}
              >
                · há {decorrido < 1 ? 'menos de 1 min' : `${decorrido} min`}
              </span>
            </span>
            {podeEditar && (
              <Button
                variante="ghost"
                tamanho="sm"
                disabled={cancelar.isPending}
                onClick={() => comAviso(() => cancelar.mutateAsync(), () => 'Varredura interrompida.')}
              >
                <Square className="h-4 w-4" />
                Parar
              </Button>
            )}
          </>
        ) : (
          podeEditar && (
            <>
              <Button
                variante="outline"
                tamanho="sm"
                disabled={executar.isPending}
                onClick={() => {
                  // Acompanha a partir do clique, não a partir da primeira resposta: a varredura
                  // leva 1–2 s para se registrar, e esperar por ela é o que congelava a tela.
                  setAcompanhando(true);
                  void comAviso(
                    () => executar.mutateAsync(),
                    (r) => (r as { mensagem: string }).mensagem,
                  );
                }}
              >
                {executar.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Play className="h-4 w-4" />
                )}
                Sincronizar agora
              </Button>
              <Button variante="ghost" tamanho="sm" onClick={() => setPeriodoAberto(true)}>
                <CalendarRange className="h-4 w-4" />
                Sincronizar período…
              </Button>
            </>
          )
        )}
      </div>

      {aviso && (
        <p
          className={`mt-3 rounded-md border p-3 text-sm ${
            aviso.tipo === 'ok'
              ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
              : 'border-red-200 bg-red-50 text-red-700'
          }`}
        >
          {aviso.texto}
        </p>
      )}

      {/* Importação do passado */}
      <div className="mt-4 border-t border-gray-100 pt-3">
        <h4 className="flex items-center gap-2 text-sm font-medium text-gray-700">
          <History className="h-4 w-4 text-gray-500" />
          Importar o passado desta unidade
        </h4>
        <p className="mt-1 max-w-3xl text-xs text-gray-600">
          Anda para trás em fatias de 31 dias, <strong>uma por vez</strong>, e para sozinho depois de
          seis meses seguidos sem nenhum registro — é o dado que diz onde a unidade começou, não a
          data de cadastro. Traz tudo: cria procedimento e profissional que não existem mais, e
          guarda a linha crua do SISREG. Não avisa paciente.
        </p>
        <p className="mt-1 max-w-3xl text-xs text-gray-500">
          Roda em segundo plano e cede a vez a qualquer outro trabalho do SISREG, então demora — o
          que importa é a cobertura abaixo, não a velocidade.
        </p>

        <div className="mt-3 flex flex-wrap items-center gap-3">
          <span className="text-xs text-gray-600">
            Coberto até:{' '}
            <strong className={dados?.historicoCobertoDe ? 'text-gray-900' : 'text-gray-400'}>
              {dados?.historicoCobertoDe
                ? new Date(`${dados.historicoCobertoDe}T12:00:00`).toLocaleDateString('pt-BR')
                : 'só o que a varredura diária trouxe'}
            </strong>
          </span>
          {dados?.historicoConcluidoEm ? (
            <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs text-emerald-700">
              Chegou ao início da unidade
            </span>
          ) : dados?.historicoAtivo ? (
            <span className="rounded-full bg-blue-50 px-2 py-0.5 text-xs text-blue-700">
              Em andamento
            </span>
          ) : null}
        </div>

        {podeEditar && (
          <div className="mt-3 flex flex-wrap items-center gap-2">
            {dados?.historicoAtivo && !dados?.historicoConcluidoEm ? (
              <Button
                variante="outline"
                tamanho="sm"
                disabled={alternarHistorico.isPending}
                onClick={() => alternarHistorico.mutate({ ativo: false })}
              >
                <Square className="mr-1.5 h-3.5 w-3.5" />
                Parar
              </Button>
            ) : (
              <Button
                tamanho="sm"
                disabled={alternarHistorico.isPending}
                onClick={() => alternarHistorico.mutate({ ativo: true })}
              >
                <Play className="mr-1.5 h-3.5 w-3.5" />
                {dados?.historicoCobertoDe ? 'Continuar de onde parou' : 'Importar o passado'}
              </Button>
            )}

            {/* Só faz sentido quando já houve cobertura: reabre uma unidade dada por concluída,
                para o caso de ela ter parado por um hiato longo e não por ter chegado ao começo. */}
            {dados?.historicoCobertoDe && (
              <Button
                variante="ghost"
                tamanho="sm"
                disabled={alternarHistorico.isPending}
                title="Zera a cobertura e recomeça de hoje para trás."
                onClick={() => alternarHistorico.mutate({ ativo: true, reiniciar: true })}
              >
                <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
                Recomeçar do zero
              </Button>
            )}
            {/* O avanço automático depende do sincronismo automático estar ligado; este botão
                não. Existe porque o operador precisa conseguir ver efeito na hora — e porque, com
                a chave-mestra desligada, ligar o passado sozinho não movia nada. */}
            {dados?.historicoAtivo && !dados?.historicoConcluidoEm && (
              <Button
                variante="outline"
                tamanho="sm"
                disabled={avancarHistorico.isPending}
                title="Busca agora a próxima fatia de 31 dias, sem esperar o motor automático."
                onClick={() => avancarHistorico.mutate()}
              >
                <Play className="mr-1.5 h-3.5 w-3.5" />
                Avançar agora
              </Button>
            )}

            {(alternarHistorico.isPending || avancarHistorico.isPending) && (
              <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
            )}
          </div>
        )}

        {/* Silêncio foi o defeito original: ligar o passado gravava a flag e nada acontecia,
            porque quem executa é um motor automático. Agora todo motivo de recusa aparece. */}
        {(alternarHistorico.isError || avancarHistorico.isError) && (
          <p className="mt-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
            {extrairMensagemDeErro(alternarHistorico.error ?? avancarHistorico.error)}
          </p>
        )}

        {dados?.historicoAtivo && !dados?.historicoConcluidoEm && (
          <p className="mt-2 text-[11px] leading-snug text-gray-500">
            Ligado, ele <strong>anda sozinho até o fim</strong> — não depende do sincronismo
            automático nem de você clicar de novo. Avança uma fatia de 31 dias por vez, cedendo a
            vez a qualquer outro trabalho do SISREG e só tocando com folga de orçamento, então leva
            horas. O <strong>Avançar agora</strong> é opcional: força a próxima fatia sem esperar a
            hora dela.
          </p>
        )}
      </div>

      {/* Histórico */}
      {(execucoes.data?.length ?? 0) > 0 && (
        <div className="mt-4 border-t border-gray-100 pt-3">
          <h4 className="mb-2 text-sm font-medium text-gray-700">Varreduras recentes</h4>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-gray-500">
                  <th className="py-1 pr-3 font-medium">Quando</th>
                  <th className="py-1 pr-3 font-medium">Disparo</th>
                  <th className="py-1 pr-3 font-medium">Status</th>
                  <th className="py-1 pr-3 font-medium">Cobertura</th>
                  <th className="py-1 pr-3 font-medium">Req.</th>
                  {/* "Importadas" era uma coluna só, mostrando o total LIDO — e uma varredura
                      relida dizia "901 importadas" tendo criado zero. Quem olha esta tela está
                      justamente perguntando "entrou coisa nova?"; as três são respostas
                      diferentes. */}
                  <th className="py-1 pr-3 font-medium">Lidas</th>
                  <th className="py-1 pr-3 font-medium">Novas</th>
                  <th className="py-1 pr-3 font-medium">Já existiam</th>
                  <th className="py-1 pr-3 font-medium">Pendências</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {execucoes.data!.map((e) => (
                  <LinhaExecucao key={e.id} execucao={e} aoAbrir={() => setDetalhe(e)} />
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {periodoAberto && (
        <ModalVarreduraPeriodo
          unidadeId={unidadeId}
          corteEntradaLocal={dados?.corteEntradaLocal}
          bloqueioFimLocal={dados?.bloqueioFimLocal}
          aoFechar={() => setPeriodoAberto(false)}
          aoIniciado={(mensagem) => {
            setPeriodoAberto(false);
            setAcompanhando(true);
            setAviso({ tipo: 'ok', texto: mensagem });
          }}
        />
      )}

      {detalhe && (
        <ModalDetalheVarredura
          unidadeId={unidadeId}
          execucao={detalhe}
          aoFechar={() => setDetalhe(null)}
        />
      )}
    </section>
  );
}

function LinhaExecucao({ execucao: e, aoAbrir }: { execucao: VarreduraExecucao; aoAbrir: () => void }) {
  // O que efetivamente virou solicitação nesta rodada. Reler é seguro (a idempotência por nº da
  // solicitação descarta o repetido), então relidas em massa são o caso NORMAL, não anomalia.
  const novas = Math.max(0, e.validos - e.jaExistiam);

  return (
    <>
      <tr
        className="cursor-pointer text-gray-700 hover:bg-gray-50"
        onClick={aoAbrir}
        title="Ver detalhes desta sincronização"
      >
        <td className="py-1.5 pr-3 whitespace-nowrap">{dataHora(e.iniciadoEm)}</td>
        <td className="py-1.5 pr-3">{e.disparo === 'Agendado' ? '⏱ Agendado' : 'Manual'}</td>
        <td className="py-1.5 pr-3">
          {/* "Rodando" no banco não prova que alguém está rodando — o banco não sabe se o processo
              morreu. Quando o backend diz que não há sinal, dizer a verdade vale mais que manter a
              aparência de progresso. */}
          {e.semSinal ? (
            <span
              className="rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-900"
              title={
                e.ultimoSinalEm
                  ? `Sem responder desde ${dataHora(e.ultimoSinalEm)}. Quase sempre é o serviço ter reiniciado (deploy) no meio. Nada do que entrou foi perdido — rode de novo.`
                  : 'Ficou marcada como em andamento e não responde. Quase sempre é o serviço ter reiniciado no meio. Nada do que entrou foi perdido — rode de novo.'
              }
            >
              ⚠ Sem sinal
            </span>
          ) : (
            <span className={`rounded px-1.5 py-0.5 text-xs ${CLASSE_STATUS[e.status]}`}>
              {ROTULO_STATUS[e.status]}
            </span>
          )}
        </td>
        {/* Varredura de hoje traz a agenda inteira numa requisição: "1/1" não informa nada. A
            coluna sobrevive para as execuções do modo antigo, que varriam centenas de pares. */}
        <td className="py-1.5 pr-3 whitespace-nowrap text-gray-500">
          {e.combinacoesTotal > 1 ? `${e.combinacoesFeitas}/${e.combinacoesTotal}` : '—'}
        </td>
        <td className="py-1.5 pr-3">{e.requisicoes}</td>
        {/* LIDAS é `registrosEncontrados`, não `validos`. Eram a mesma coisa só no fim de uma
            varredura que deu certo — e por isso o erro passou. Enquanto ela roda, `validos` fica em
            zero até a importação começar: no CDT em 08/09/2026 a tela dizia "Lidas 0" com 6.268
            agendamentos já lidos e gravados, durante os minutos da pré-carga. É exatamente quando o
            operador está olhando que a coluna mentia. */}
        <td className="py-1.5 pr-3">{e.registrosEncontrados}</td>
        <td className={`py-1.5 pr-3 ${novas > 0 ? 'font-semibold text-emerald-700' : 'text-gray-400'}`}>
          {novas > 0 ? novas : '—'}
        </td>
        <td className="py-1.5 pr-3 text-gray-500">{e.jaExistiam > 0 ? e.jaExistiam : '—'}</td>
        <td className="py-1.5 pr-3">{e.invalidos > 0 ? e.invalidos : '—'}</td>
      </tr>

      {/* O motivo fica VISÍVEL, não num tooltip. É exatamente quando algo deu errado que o
          operador precisa saber o que fazer — e "Parcial" sozinho não diz nada. */}
      {e.mensagemErro && (
        <tr>
          <td colSpan={9} className="pb-2 pr-3">
            <p
              className={`rounded-md border px-3 py-2 text-xs ${
                e.status === 'Erro'
                  ? 'border-red-200 bg-red-50 text-red-700'
                  : 'border-amber-200 bg-amber-50 text-amber-800'
              }`}
            >
              {e.mensagemErro}
            </p>
          </td>
        </tr>
      )}
    </>
  );
}

function dataHora(iso: string | null | undefined) {
  return iso ? new Date(iso).toLocaleString('pt-BR') : '—';
}
