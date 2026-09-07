import { useState } from 'react';
import { AlertTriangle, Ban, CircleCheck, Info } from 'lucide-react';

import { Button } from '@/shared/ui/Button';

import { useElegibilidade, useResponderRegras } from '../../api/solicitacoesQueries';
import type { RespostaRegraRegulacao } from '../../tiposSolicitacao';

/**
 * As regras do manual, aplicadas a este pedido (plano 03).
 *
 * <p>Três coisas na mesma tela, e a ordem é deliberada: <b>o que barra</b> vem primeiro, porque
 * decide se vale a pena continuar; depois <b>o que precisa ser respondido</b>; e por último o
 * texto informativo, que o manual traz e ninguém responde — 83% das regras extraídas são desse
 * tipo, e afogar a pergunta no meio delas faria o solicitante desistir de ler.</p>
 */
export function PassoRegras({ solicitacaoId }: { solicitacaoId: string | null }) {
  const avaliacao = useElegibilidade(solicitacaoId);
  const responder = useResponderRegras();
  const [rascunho, setRascunho] = useState<Record<string, RespostaRegraRegulacao>>({});

  if (!solicitacaoId) {
    return <p className="text-sm text-slate-500">Escolha o paciente para o sistema conferir as regras.</p>;
  }
  if (avaliacao.isLoading) return <p className="text-sm text-slate-500">Conferindo as regras…</p>;

  const a = avaliacao.data;
  if (!a) return null;

  const informativas = a.regras.filter((r) => r.tipo === 'Informativa');
  const bloqueios = Object.entries(a.motivosDeBloqueio);
  const semRegras = a.regras.length === 0;

  async function salvarRespostas() {
    if (!solicitacaoId || Object.keys(rascunho).length === 0) return;
    await responder.mutateAsync({ id: solicitacaoId, respostas: rascunho });
    setRascunho({});
  }

  return (
    <div className="space-y-4">
      {semRegras && (
        <p className="rounded border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">
          Este procedimento ainda não tem regras cadastradas. Siga para o formulário.
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
            {a.perguntasPendentes.map((p) => (
              <li key={p.regraId} className="border-b border-slate-100 pb-3 last:border-0">
                <p className="text-sm text-slate-900">{p.pergunta}</p>
                <div className="mt-1.5 flex gap-2">
                  {(['Sim', 'Nao', 'NaoSei'] as const).map((opcao) => (
                    <button
                      key={opcao}
                      type="button"
                      onClick={() => setRascunho((r) => ({ ...r, [p.regraId]: opcao }))}
                      className={`rounded border px-3 py-1 text-sm ${
                        rascunho[p.regraId] === opcao
                          ? 'border-red-600 bg-red-600 text-white'
                          : 'border-slate-300 text-slate-700 hover:border-slate-400'
                      }`}
                    >
                      {opcao === 'NaoSei' ? 'Não sei' : opcao === 'Nao' ? 'Não' : 'Sim'}
                    </button>
                  ))}
                </div>
              </li>
            ))}
          </ul>

          <Button
            className="mt-3"
            onClick={salvarRespostas}
            disabled={responder.isPending || Object.keys(rascunho).length === 0}
          >
            Salvar respostas
          </Button>
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
