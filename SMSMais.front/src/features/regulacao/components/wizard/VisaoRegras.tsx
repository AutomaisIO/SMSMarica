import type { ReactNode } from 'react';
import { AlertTriangle, Ban, CircleCheck, Info, Lock } from 'lucide-react';

import type { AvaliacaoElegibilidade, PerguntaPendente, RespostaRegraRegulacao } from '../../tiposSolicitacao';

type Props = {
  avaliacao: AvaliacaoElegibilidade;
  /**
   * Prévia da curadoria ("Ver como o solicitante vê"): mostra exatamente o mesmo, mas nada
   * responde. É a mesma peça do assistente de propósito — prévia desenhada à parte deixaria de
   * provar o que o solicitante vê no dia em que uma das duas mudasse.
   */
  somenteLeitura?: boolean;
  rascunho?: Record<string, RespostaRegraRegulacao>;
  marcadas?: Record<string, string[]>;
  onAlternarOpcao?: (p: PerguntaPendente, opcaoId: string) => void;
  onResponder?: (p: PerguntaPendente, resposta: RespostaRegraRegulacao) => void;
  /** O botão de salvar respostas, no assistente. */
  rodapePerguntas?: ReactNode;
};

/**
 * As regras do manual, como o passo "Regras" do assistente as mostra (plano 03).
 *
 * <p>Três coisas na mesma tela, e a ordem é deliberada: <b>o que barra</b> vem primeiro, porque
 * decide se vale a pena continuar; depois <b>o que precisa ser respondido</b>; e por último o
 * texto informativo, que o manual traz e ninguém responde.</p>
 *
 * <p><b>Pergunta de lista</b> ("portadores das seguintes condições: …") vira caixas de marcar:
 * basta uma. Marcar é o "Sim" — e as marcadas vão junto, porque é por elas que o regulador sabe
 * qual condição justifica o pedido; "Nenhuma destas" é o "Não".</p>
 *
 * <p><b>O que trava o envio fica dito aqui</b> (09/10/2026): pergunta capaz de barrar e sem
 * resposta, e caixinha obrigatória sem anexo, seguram o "Enviar para a pré-regulação". Avisar
 * no passo é o que poupa o solicitante de descobrir isso só na revisão.</p>
 */
export function VisaoRegras({
  avaliacao: a,
  somenteLeitura = false,
  rascunho = {},
  marcadas = {},
  onAlternarOpcao,
  onResponder,
  rodapePerguntas,
}: Props) {
  const informativas = a.regras.filter((r) => r.tipo === 'Informativa');
  const bloqueios = Object.entries(a.motivosDeBloqueio);
  const semRegras = a.regras.length === 0;
  const perguntasQueTravam = a.perguntasPendentes.filter((p) => p.severidade === 'Bloqueia').length;
  const documentosObrigatorios = a.documentosPendentes.filter((d) => d.obrigatorio).length;

  function classeBotao(ativo: boolean) {
    return `rounded border px-3 py-1 text-sm ${
      ativo
        ? 'border-red-600 bg-red-600 text-white'
        : 'border-slate-300 text-slate-700 hover:border-slate-400'
    } disabled:cursor-not-allowed disabled:opacity-60`;
  }

  return (
    <div className="space-y-4">
      {semRegras && (
        <p className="rounded border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">
          Este procedimento ainda não tem regras cadastradas. Siga para o formulário.
        </p>
      )}

      {(perguntasQueTravam > 0 || documentosObrigatorios > 0) && (
        <p className="flex items-start gap-2 rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          <Lock className="mt-0.5 size-4 shrink-0" />
          <span>
            {perguntasQueTravam > 0 && (
              <>
                Sem resposta {perguntasQueTravam === 1 ? 'à pergunta abaixo' : `às ${perguntasQueTravam} perguntas abaixo`},
                a solicitação não pode ser enviada à regulação. Se não souber, marque <strong>"Não sei"</strong>: o
                pedido segue e o regulador confere.{' '}
              </>
            )}
            {documentosObrigatorios > 0 && (
              <>
                {documentosObrigatorios === 1
                  ? 'O documento obrigatório também precisa estar anexado'
                  : `Os ${documentosObrigatorios} documentos obrigatórios também precisam estar anexados`}{' '}
                antes do envio.
              </>
            )}
          </span>
        </p>
      )}

      {bloqueios.length > 0 && (
        <section className="rounded-lg border border-red-300 bg-red-50 p-3">
          <h3 className="flex items-center gap-2 text-sm font-semibold text-red-900">
            <Ban className="size-4" /> Destinos bloqueados
          </h3>
          <ul className="mt-2 space-y-1 text-sm text-red-900">
            {bloqueios.map(([sistema, motivo]) => (
              <li key={sistema}>
                <strong>{sistema}:</strong> {motivo}
              </li>
            ))}
          </ul>
          {a.destinosPermitidos.length > 0 && (
            <p className="mt-2 text-xs text-red-800">
              O pedido ainda pode seguir por: {a.destinosPermitidos.join(', ')}.
            </p>
          )}
        </section>
      )}

      {a.destinosComRessalva.length > 0 && (
        <p className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          <AlertTriangle className="mr-1 inline size-4" />
          Passa com ressalva em: {a.destinosComRessalva.join(', ')} — o agente regulador decide na
          triagem.
        </p>
      )}

      {a.perguntasPendentes.length > 0 && (
        <section className="rounded-lg border border-slate-200 bg-white p-4">
          <h3 className="text-sm font-semibold text-slate-900">Perguntas do manual</h3>
          <p className="mb-3 text-xs text-slate-500">
            "Não sei" é uma resposta válida — ela marca o pedido para o agente conferir, em vez de
            travar você aqui.
          </p>

          <ul className="space-y-3">
            {a.perguntasPendentes.map((p) =>
              p.opcoes && p.opcoes.length > 0 ? (
                <li key={p.regraId} className="border-b border-slate-100 pb-3 last:border-0">
                  <p className="text-sm text-slate-900">{p.pergunta}</p>
                  <p className="text-xs text-slate-500">Marque as que se aplicam — basta uma.</p>
                  <div className="mt-1.5 space-y-1">
                    {p.opcoes.map((opcao) => (
                      <label key={opcao.id} className="flex cursor-pointer items-start gap-2 text-sm text-slate-800">
                        <input
                          type="checkbox"
                          className="mt-0.5 size-4 shrink-0 accent-red-600"
                          disabled={somenteLeitura}
                          checked={(marcadas[p.regraId] ?? []).includes(opcao.id)}
                          onChange={() => onAlternarOpcao?.(p, opcao.id)}
                        />
                        <span>{opcao.texto}</span>
                      </label>
                    ))}
                  </div>
                  <div className="mt-2 flex gap-2">
                    {(['Nao', 'NaoSei'] as const).map((opcao) => (
                      <button
                        key={opcao}
                        type="button"
                        disabled={somenteLeitura}
                        onClick={() => onResponder?.(p, opcao)}
                        className={classeBotao(rascunho[p.regraId] === opcao)}
                      >
                        {opcao === 'NaoSei' ? 'Não sei' : 'Nenhuma destas'}
                      </button>
                    ))}
                  </div>
                </li>
              ) : (
                <li key={p.regraId} className="border-b border-slate-100 pb-3 last:border-0">
                  <p className="text-sm text-slate-900">{p.pergunta}</p>
                  <div className="mt-1.5 flex gap-2">
                    {(['Sim', 'Nao', 'NaoSei'] as const).map((opcao) => (
                      <button
                        key={opcao}
                        type="button"
                        disabled={somenteLeitura}
                        onClick={() => onResponder?.(p, opcao)}
                        className={classeBotao(rascunho[p.regraId] === opcao)}
                      >
                        {opcao === 'NaoSei' ? 'Não sei' : opcao === 'Nao' ? 'Não' : 'Sim'}
                      </button>
                    ))}
                  </div>
                </li>
              ),
            )}
          </ul>

          {rodapePerguntas}
        </section>
      )}

      {a.documentosPendentes.length > 0 && (
        <section className="rounded-lg border border-slate-200 bg-white p-4">
          <h3 className="text-sm font-semibold text-slate-900">Documentos exigidos</h3>
          <p className="mb-2 text-xs text-slate-500">
            As caixinhas para anexar estão no próximo passo, junto do formulário.
          </p>
          <ul className="space-y-1 text-sm text-slate-700">
            {a.documentosPendentes.map((d) => (
              <li key={d.regraId} className="flex items-start gap-2">
                <CircleCheck className="mt-0.5 size-4 shrink-0 text-slate-400" />
                <span>
                  {d.rotulo}
                  {!d.obrigatorio && <span className="text-slate-500"> (opcional)</span>}
                  {d.examesInternosCandidatos.length > 0 && (
                    <span className="text-emerald-700">
                      {' '}
                      — {d.examesInternosCandidatos.length} exame(s) nosso(s) podem servir
                    </span>
                  )}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {informativas.length > 0 && (
        <details className="rounded-lg border border-slate-200 bg-white p-4">
          <summary className="cursor-pointer text-sm font-semibold text-slate-900">
            <Info className="mr-1 inline size-4" />
            O que o manual diz sobre este procedimento ({informativas.length})
          </summary>
          <ul className="mt-2 space-y-1 text-sm text-slate-600">
            {informativas.map((r) => (
              <li key={r.regraId}>• {r.descricao}</li>
            ))}
          </ul>
        </details>
      )}
    </div>
  );
}
