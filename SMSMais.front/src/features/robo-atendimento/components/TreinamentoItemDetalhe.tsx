import { useState } from 'react';
import {
  AlertTriangle,
  Code2,
  GraduationCap,
  Loader2,
  PlayCircle,
  Scale,
  Undo2,
  X,
} from 'lucide-react';
import {
  useDescartarTreinamento,
  useDesfazerAlteracaoTreinamento,
  useDispensarPendenciaTreinamento,
  useObterTreinamento,
  useResponderPendenciaTreinamento,
  useSimularTreinamento,
  useTreinarItem,
} from '@/features/robo-atendimento/api/queries';
import { ESTILO_STATUS } from '@/features/robo-atendimento/components/TreinamentoRoboCard';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import type {
  TreinamentoAlteracao,
  TreinamentoPendencia,
  TreinamentoSimulacao,
} from '@/features/robo-atendimento/types';

function dataHora(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit',
  });
}

const ROTULO_OPERACAO: Record<TreinamentoAlteracao['operacao'], string> = {
  Criar: 'criou',
  Atualizar: 'reescreveu',
  Desativar: 'desativou',
};

const ROTULO_ALVO: Record<TreinamentoAlteracao['alvo'], string> = {
  TreinoAssunto: 'regra do assunto',
  CondicaoAssunto: 'condição de roteamento',
};

/** Uma pendência: enquanto tiver alguma aberta, o ciclo não anda. */
function Pendencia({ p, itemId }: { p: TreinamentoPendencia; itemId: string }) {
  const responder = useResponderPendenciaTreinamento();
  const dispensar = useDispensarPendenciaTreinamento();
  const [resposta, setResposta] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const codigo = p.tipo === 'AlteracaoCodigo';
  const respondida = p.status !== 'Aberta';

  return (
    <li className={`rounded-lg border p-3 ${respondida ? 'border-gray-200 bg-gray-50' : 'border-amber-300 bg-amber-50'}`}>
      <div className="flex items-center gap-2 text-xs font-medium">
        {codigo ? <Code2 className="h-3.5 w-3.5 text-indigo-600" /> : <Scale className="h-3.5 w-3.5 text-amber-600" />}
        <span className={codigo ? 'text-indigo-700' : 'text-amber-800'}>
          {codigo ? 'Precisa de alteração de código' : 'Decisão de regra de negócio'}
        </span>
        {respondida && (
          <span className="rounded-full bg-gray-200 px-2 py-0.5 text-[11px] text-gray-600">
            {p.status === 'Dispensada' ? 'dispensada' : 'respondida'}
          </span>
        )}
      </div>

      <p className="mt-1.5 text-sm font-medium text-gray-900">{p.pergunta}</p>
      {p.contexto ? <p className="mt-1 whitespace-pre-wrap text-xs text-gray-600">{p.contexto}</p> : null}

      {respondida ? (
        <p className="mt-2 rounded-md bg-white p-2 text-sm text-gray-700">
          {p.resposta}
          {codigo && p.autorizado !== null && (
            <span className={`ml-2 text-xs font-medium ${p.autorizado ? 'text-emerald-700' : 'text-red-700'}`}>
              {p.autorizado ? '· desenvolvimento autorizado' : '· não autorizado'}
            </span>
          )}
          <span className="ml-1 text-xs text-gray-400">
            {p.respondidoPorNome ? `· ${p.respondidoPorNome}` : ''}
            {p.respondidoEm ? ` · ${dataHora(p.respondidoEm)}` : ''}
          </span>
        </p>
      ) : (
        <>
          {p.opcoes.length > 0 && (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {p.opcoes.map((o) => (
                <button
                  key={o}
                  type="button"
                  onClick={() => setResposta(o)}
                  className="rounded-full border border-amber-300 bg-white px-2.5 py-1 text-xs text-amber-900 hover:bg-amber-100"
                >
                  {o}
                </button>
              ))}
            </div>
          )}
          <textarea
            value={resposta}
            onChange={(e) => setResposta(e.target.value)}
            rows={2}
            maxLength={4000}
            placeholder="Sua decisão. O agente segue isso à risca na próxima análise."
            className="mt-2 w-full rounded-md border border-amber-200 bg-white px-2 py-1.5 text-sm outline-none focus:border-amber-400"
          />
          {erro && <p className="mt-1 text-xs text-red-600">{erro}</p>}
          <div className="mt-2 flex flex-wrap justify-end gap-2">
            <button
              type="button"
              onClick={() => {
                setErro(null);
                dispensar.mutate(
                  { pendenciaId: p.id, itemId, motivo: resposta.trim() || undefined },
                  { onError: (e) => setErro(extrairMensagemDeErro(e)) },
                );
              }}
              className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100"
            >
              Dispensar
            </button>
            {codigo ? (
              <>
                <button
                  type="button"
                  disabled={responder.isPending || resposta.trim().length === 0}
                  onClick={() => {
                    setErro(null);
                    responder.mutate(
                      { pendenciaId: p.id, itemId, resposta: resposta.trim(), autorizado: false },
                      { onError: (e) => setErro(extrairMensagemDeErro(e)) },
                    );
                  }}
                  className="rounded-md border border-red-200 px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-50 disabled:opacity-50"
                >
                  Não autorizar
                </button>
                <button
                  type="button"
                  disabled={responder.isPending || resposta.trim().length === 0}
                  onClick={() => {
                    setErro(null);
                    responder.mutate(
                      { pendenciaId: p.id, itemId, resposta: resposta.trim(), autorizado: true },
                      { onError: (e) => setErro(extrairMensagemDeErro(e)) },
                    );
                  }}
                  className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
                >
                  Autorizar desenvolvimento
                </button>
              </>
            ) : (
              <button
                type="button"
                disabled={responder.isPending || resposta.trim().length === 0}
                onClick={() => {
                  setErro(null);
                  responder.mutate(
                    { pendenciaId: p.id, itemId, resposta: resposta.trim() },
                    { onError: (e) => setErro(extrairMensagemDeErro(e)) },
                  );
                }}
                className="rounded-md bg-amber-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-amber-700 disabled:opacity-50"
              >
                {responder.isPending ? 'Enviando…' : 'Responder e retomar'}
              </button>
            )}
          </div>
        </>
      )}
    </li>
  );
}

function Alteracao({ a, itemId }: { a: TreinamentoAlteracao; itemId: string }) {
  const desfazer = useDesfazerAlteracaoTreinamento();
  const [erro, setErro] = useState<string | null>(null);
  const desfeita = !!a.desfeitoEm;

  return (
    <li className={`rounded-lg border border-gray-200 p-3 ${desfeita ? 'bg-gray-50 opacity-70' : 'bg-white'}`}>
      <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500">
        <span className="font-medium text-gray-700">
          {ROTULO_OPERACAO[a.operacao]} uma {ROTULO_ALVO[a.alvo]}
        </span>
        {a.assuntoNome ? (
          <span className="rounded-full bg-indigo-50 px-2 py-0.5 font-medium text-indigo-700">{a.assuntoNome}</span>
        ) : null}
        <span>{dataHora(a.aplicadoEm)}</span>
        {desfeita && (
          <span className="rounded-full bg-gray-200 px-2 py-0.5 text-gray-600">
            desfeita{a.desfeitoPorNome ? ` por ${a.desfeitoPorNome}` : ''}
          </span>
        )}
      </div>

      {a.antes ? (
        <p className="mt-1.5 text-sm text-gray-500 line-through">{a.antes}</p>
      ) : null}
      {a.depois ? <p className="mt-0.5 text-sm text-gray-900">{a.depois}</p> : null}
      {a.justificativa ? (
        <p className="mt-1 text-xs italic text-gray-500">Por quê: {a.justificativa}</p>
      ) : null}
      {erro && <p className="mt-1 text-xs text-red-600">{erro}</p>}

      {!desfeita && (
        <button
          type="button"
          disabled={desfazer.isPending}
          onClick={() => {
            setErro(null);
            desfazer.mutate(
              { alteracaoId: a.id, itemId },
              { onError: (e) => setErro(extrairMensagemDeErro(e)) },
            );
          }}
          className="mt-2 flex items-center gap-1 text-xs font-medium text-gray-500 hover:text-red-600 disabled:opacity-50"
        >
          <Undo2 className="h-3.5 w-3.5" /> Desfazer
        </button>
      )}
    </li>
  );
}

function Simulacao({ s }: { s: TreinamentoSimulacao }) {
  const cor =
    s.veredito === 'Passou'
      ? 'border-emerald-200 bg-emerald-50'
      : s.veredito === 'Falhou'
        ? 'border-red-200 bg-red-50'
        : 'border-gray-200 bg-white';

  return (
    <li className={`rounded-lg border p-3 ${cor}`}>
      <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500">
        {s.veredito && (
          <span
            className={`rounded-full px-2 py-0.5 font-medium ${
              s.veredito === 'Passou'
                ? 'bg-emerald-100 text-emerald-800'
                : s.veredito === 'Falhou'
                  ? 'bg-red-100 text-red-800'
                  : 'bg-yellow-100 text-yellow-800'
            }`}
          >
            {s.veredito}
          </span>
        )}
        <span>{s.automatica ? 'automática' : `manual${s.criadoPorNome ? ` · ${s.criadoPorNome}` : ''}`}</span>
        <span>{dataHora(s.criadoEm)}</span>
        {s.assuntoNome ? (
          <span className="rounded-full bg-indigo-50 px-2 py-0.5 font-medium text-indigo-700">{s.assuntoNome}</span>
        ) : null}
        <span>{(s.duracaoMs / 1000).toFixed(1)}s</span>
      </div>

      {s.erroMensagem ? (
        <p className="mt-1.5 text-sm text-red-700">{s.erroMensagem}</p>
      ) : (
        <>
          <p className="mt-1.5 text-xs text-gray-500">Cidadão: “{s.mensagem}”</p>
          <p className="mt-1 whitespace-pre-wrap rounded-md bg-white/70 p-2 text-sm text-gray-900">
            {s.resposta}
          </p>
          {s.chamadas.length > 0 && (
            <p className="mt-1 text-xs text-gray-500">
              Comandos: {s.chamadas.map((c) => c.comando).join(', ')}
            </p>
          )}
          {s.analise ? <p className="mt-1.5 text-sm text-gray-700">{s.analise}</p> : null}
        </>
      )}
    </li>
  );
}

/**
 * O item aberto: a crítica, o parecer do agente, o que ele mudou (com desfazer), o que travou
 * esperando humano e as simulações contra o modelo treinado atual.
 */
export function TreinamentoItemDetalhe({ id, onFechar }: { id: string; onFechar: () => void }) {
  const { data: item, isLoading } = useObterTreinamento(id);
  const treinar = useTreinarItem();
  const simular = useSimularTreinamento();
  const descartar = useDescartarTreinamento();

  const [observacao, setObservacao] = useState('');
  const [confirmarTreino, setConfirmarTreino] = useState(false);
  const [mensagemSimulada, setMensagemSimulada] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  const pendenciasAbertas = item?.pendencias.filter((p) => p.status === 'Aberta') ?? [];
  const podeTreinar =
    !!item && item.status !== 'Analisando' && pendenciasAbertas.length === 0;

  return (
    <div className="fixed inset-0 z-[70] flex items-start justify-center overflow-y-auto bg-black/40 p-4">
      <div className="my-4 w-full max-w-3xl space-y-4 rounded-lg bg-white p-4 shadow-xl">
        <div className="flex items-start justify-between gap-3">
          <h2 className="flex items-center gap-2 text-base font-semibold text-gray-900">
            <GraduationCap className="h-4 w-4 text-amber-500" /> Item de treinamento
          </h2>
          <button type="button" onClick={onFechar} className="rounded-md p-1 text-gray-400 hover:bg-gray-100">
            <X className="h-4 w-4" />
          </button>
        </div>

        {isLoading && <p className="text-sm text-gray-500">Carregando…</p>}

        {item && (
          <>
            <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500">
              <span className={`rounded-full px-2 py-0.5 font-medium ${ESTILO_STATUS[item.status].classe}`}>
                {item.status === 'Analisando' && <Loader2 className="mr-1 inline h-3 w-3 animate-spin" />}
                {ESTILO_STATUS[item.status].rotulo}
              </span>
              <span className="rounded-full bg-indigo-50 px-2 py-0.5 font-medium text-indigo-700">
                {item.assuntoNome ?? 'sem assunto'}
              </span>
              <span>{dataHora(item.criadoEm)}</span>
              {item.criadoPorNome ? <span>· por {item.criadoPorNome}</span> : null}
              {item.custoUsd ? <span>· US$ {item.custoUsd.toFixed(4)}</span> : null}
            </div>
            <p className="text-xs text-gray-500">{ESTILO_STATUS[item.status].dica}</p>

            {item.trecho ? (
              <div>
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-400">
                  Resposta criticada
                </h3>
                <p className="mt-1 whitespace-pre-wrap rounded-md bg-gray-50 p-2 text-sm text-gray-800">
                  {item.trecho}
                </p>
              </div>
            ) : null}

            <div>
              <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-400">Crítica</h3>
              <p className="mt-1 whitespace-pre-wrap text-sm text-gray-900">{item.critica}</p>
              {item.observacao ? (
                <p className="mt-1 whitespace-pre-wrap text-sm text-gray-600">
                  <span className="font-medium">Observação:</span> {item.observacao}
                </p>
              ) : null}
            </div>

            {item.contexto.length > 0 && (
              <details className="rounded-md border border-gray-200 p-2">
                <summary className="cursor-pointer text-xs font-medium text-gray-600">
                  Diálogo em volta ({item.contexto.length} mensagens)
                </summary>
                <ul className="mt-2 space-y-1 text-sm">
                  {item.contexto.map((t, i) => (
                    <li key={i} className="text-gray-700">
                      <span className="text-xs font-medium uppercase text-gray-400">{t.papel}</span>{' '}
                      {t.texto}
                    </li>
                  ))}
                </ul>
              </details>
            )}

            {item.erroMensagem ? (
              <p className="flex items-start gap-2 rounded-md bg-red-50 p-2 text-sm text-red-700">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /> {item.erroMensagem}
              </p>
            ) : null}

            {item.analise ? (
              <div>
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-400">
                  Parecer do agente {item.modelo ? `(${item.modelo})` : ''}
                </h3>
                <p className="mt-1 whitespace-pre-wrap rounded-md border border-gray-200 p-2 text-sm text-gray-800">
                  {item.analise}
                </p>
              </div>
            ) : null}

            {pendenciasAbertas.length > 0 && (
              <div>
                <h3 className="text-xs font-semibold uppercase tracking-wide text-amber-600">
                  Precisa da sua decisão
                </h3>
                <ul className="mt-1 space-y-2">
                  {pendenciasAbertas.map((p) => (
                    <Pendencia key={p.id} p={p} itemId={item.id} />
                  ))}
                </ul>
              </div>
            )}

            {item.pendencias.some((p) => p.status !== 'Aberta') && (
              <details className="rounded-md border border-gray-200 p-2">
                <summary className="cursor-pointer text-xs font-medium text-gray-600">
                  Decisões já tomadas
                </summary>
                <ul className="mt-2 space-y-2">
                  {item.pendencias
                    .filter((p) => p.status !== 'Aberta')
                    .map((p) => (
                      <Pendencia key={p.id} p={p} itemId={item.id} />
                    ))}
                </ul>
              </details>
            )}

            {item.alteracoes.length > 0 && (
              <div>
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-400">
                  O que mudou no robô
                </h3>
                <ul className="mt-1 space-y-2">
                  {item.alteracoes.map((a) => (
                    <Alteracao key={a.id} a={a} itemId={item.id} />
                  ))}
                </ul>
              </div>
            )}

            <div>
              <h3 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-gray-400">
                <PlayCircle className="h-3.5 w-3.5" /> Simulações
              </h3>
              <div className="mt-1 flex flex-wrap items-center gap-2">
                <input
                  value={mensagemSimulada}
                  onChange={(e) => setMensagemSimulada(e.target.value)}
                  placeholder={
                    item.contexto.length > 0
                      ? 'Deixe em branco para repetir o caso original'
                      : 'Mensagem do cidadão para ensaiar'
                  }
                  className="min-w-[16rem] flex-1 rounded-md border border-gray-200 px-2 py-1.5 text-sm outline-none focus:border-primary-400"
                />
                <button
                  type="button"
                  disabled={simular.isPending}
                  onClick={() => {
                    setErro(null);
                    simular.mutate(
                      { id: item.id, mensagem: mensagemSimulada.trim() || undefined },
                      {
                        onSuccess: () => setMensagemSimulada(''),
                        onError: (e) => setErro(extrairMensagemDeErro(e)),
                      },
                    );
                  }}
                  className="rounded-md bg-purple-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-purple-700 disabled:opacity-50"
                >
                  {simular.isPending ? 'Simulando…' : 'Simular situação'}
                </button>
              </div>
              {item.simulacoes.length === 0 ? (
                <p className="mt-2 text-xs text-gray-500">
                  Nenhuma ainda. A simulação roda o caso contra o robô como ele está agora — sem
                  falar com o cidadão e sem executar comando que altere dado.
                </p>
              ) : (
                <ul className="mt-2 space-y-2">
                  {item.simulacoes.map((s) => (
                    <Simulacao key={s.id} s={s} />
                  ))}
                </ul>
              )}
            </div>

            {erro && <p className="text-sm text-red-600">{erro}</p>}

            <div className="flex flex-wrap items-center justify-end gap-2 border-t border-gray-100 pt-3">
              <button
                type="button"
                onClick={() => descartar.mutate(item.id, { onSuccess: onFechar })}
                disabled={descartar.isPending || item.status === 'Descartado'}
                className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100 disabled:opacity-50"
              >
                Descartar item
              </button>
              <button
                type="button"
                disabled={!podeTreinar}
                onClick={() => setConfirmarTreino(true)}
                className="rounded-md bg-amber-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-amber-700 disabled:opacity-50"
                title={
                  pendenciasAbertas.length > 0
                    ? 'Responda as pendências primeiro'
                    : 'Roda a análise: proposta, adversários e juiz'
                }
              >
                {item.status === 'Analisando' ? 'Analisando…' : 'Treinar'}
              </button>
            </div>

            {confirmarTreino && (
              <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/40 p-4">
                <div className="w-full max-w-md rounded-lg bg-white p-4 shadow-xl">
                  <h3 className="text-sm font-semibold text-gray-900">
                    Mais alguma observação sobre esta crítica?
                  </h3>
                  <p className="mt-1 text-xs text-gray-500">
                    O que você escrever aqui pesa na análise. Contexto que só você sabe — o porquê da
                    regra, o que já foi decidido antes — evita que o agente pergunte de volta.
                  </p>
                  <textarea
                    value={observacao}
                    onChange={(e) => setObservacao(e.target.value)}
                    rows={4}
                    maxLength={4000}
                    autoFocus
                    placeholder="Opcional."
                    className="mt-2 w-full rounded-md border border-gray-200 px-2 py-1.5 text-sm outline-none focus:border-primary-400"
                  />
                  <div className="mt-3 flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={() => setConfirmarTreino(false)}
                      className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100"
                    >
                      Cancelar
                    </button>
                    <button
                      type="button"
                      disabled={treinar.isPending}
                      onClick={() => {
                        setErro(null);
                        treinar.mutate(
                          { id: item.id, observacao: observacao.trim() || undefined },
                          {
                            onSuccess: () => { setConfirmarTreino(false); setObservacao(''); },
                            onError: (e) => setErro(extrairMensagemDeErro(e)),
                          },
                        );
                      }}
                      className="rounded-md bg-amber-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-amber-700 disabled:opacity-50"
                    >
                      {treinar.isPending ? 'Enviando…' : 'Treinar agora'}
                    </button>
                  </div>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
