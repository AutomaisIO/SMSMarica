import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { CalendarCheck2, Check, Loader2, XCircle } from 'lucide-react';
import { usePermissao, useTemConsulta } from '@/shared/auth/authStore';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useAcaoAtendimento, useResumoAgendamento } from '@/features/confirmacoes/api';
import { ChipConfirmacao } from '@/features/conversas/components/ConfirmarAgendamentoChat';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { dataHora } from '@/features/mensageria/lib/rotulos';

/**
 * JANELA SOLTA de atendimento de UM agendamento (aberta pelo botão "Agendamentos" do chat, por
 * abrirJanelaAtendimento — o nome fixo da janela faz o clique seguinte reaproveitá-la). Mostra os
 * dados do agendamento e é AQUI que se decide: confirmar a presença ou registrar que a pessoa
 * NÃO VAI (o cancelamento local, que manda a ficha para a aba Cancelamento — o SISREG segue com a
 * equipe, como sempre). O desfecho aparece nesta janela, não em toast (regra da casa).
 */
export function JanelaAtendimentoPage() {
  const { solicitacaoId } = useParams<{ solicitacaoId: string }>();
  const podeVer = useTemConsulta('Confirmacoes');
  const podeConfirmar = usePermissao('Confirmacoes', 'Edicao');
  const podeCancelar = usePermissao('Confirmacoes', 'Exclusao');
  const resumo = useResumoAgendamento(podeVer ? (solicitacaoId ?? null) : null);
  const acao = useAcaoAtendimento();

  const [motivo, setMotivo] = useState('');
  const [cancelando, setCancelando] = useState(false);
  const [desfecho, setDesfecho] = useState<{ tipo: 'confirmado' | 'cancelado'; extra?: string } | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  // A MESMA janela é reaproveitada para outro agendamento: estado de um não vaza para o outro.
  useEffect(() => {
    setMotivo('');
    setCancelando(false);
    setDesfecho(null);
    setErro(null);
  }, [solicitacaoId]);

  const a = resumo.data;

  useEffect(() => {
    document.title = a?.pacienteNome ? `Atendimento — ${a.pacienteNome}` : 'Atendimento do agendamento';
  }, [a?.pacienteNome]);

  if (!podeVer) {
    return (
      <div className="p-6 text-sm text-gray-600">
        Você não tem acesso ao módulo Confirmações. Feche esta janela.
      </div>
    );
  }

  function confirmar() {
    if (!solicitacaoId) return;
    setErro(null);
    acao.mutate(
      { solicitacaoId, acao: { tipo: 'confirmar', meio: 'WhatsApp' } },
      {
        onSuccess: () => {
          setDesfecho({ tipo: 'confirmado' });
          void resumo.refetch();
        },
        onError: (e) => setErro(extrairMensagemDeErro(e)),
      },
    );
  }

  function cancelar() {
    if (!solicitacaoId || motivo.trim().length === 0) {
      setErro('Escreva o motivo que a pessoa deu — é ele que a equipe do SISREG vai ler.');
      return;
    }
    setErro(null);
    acao.mutate(
      { solicitacaoId, acao: { tipo: 'cancelar', motivo: motivo.trim(), meio: 'WhatsApp' } },
      {
        onSuccess: (r) => {
          setDesfecho({
            tipo: 'cancelado',
            extra: r.pacienteAvisado ? 'O paciente recebeu o aviso do cancelamento no WhatsApp.' : undefined,
          });
          setCancelando(false);
          void resumo.refetch();
        },
        onError: (e) => setErro(extrairMensagemDeErro(e)),
      },
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 p-4">
      <div className="mx-auto max-w-xl space-y-4">
        <header className="flex items-center gap-2">
          <CalendarCheck2 className="h-5 w-5 text-emerald-700" />
          <h1 className="text-base font-semibold text-gray-900">Atendimento do agendamento</h1>
        </header>

        {resumo.isLoading ? (
          <p className="text-sm text-gray-500">Carregando…</p>
        ) : !a ? (
          <p className="text-sm text-red-600">Agendamento não encontrado (pode ter sido excluído).</p>
        ) : (
          <>
            <section className="space-y-1 rounded-lg bg-white p-4 shadow-sm ring-1 ring-gray-200">
              <div className="flex items-start justify-between gap-2">
                {/* Janela aberta pelo próprio chat: o atalho do WhatsApp seria ruído aqui. */}
                <NomePacienteComResumo
                  pacienteId={a.pacienteId}
                  nome={a.pacienteNome ?? 'Paciente'}
                  classNameNome="text-sm font-semibold text-gray-900"
                  mostrarWhatsApp={false}
                />
                <ChipConfirmacao status={a.statusConfirmacao} />
              </div>
              <p className="text-sm text-gray-800">{a.procedimento ?? a.categoria}</p>
              <p className="text-sm text-gray-700">{dataHora(a.dataAgendada)}</p>
              <p className="text-sm text-gray-600">
                {a.unidadeExecutante ?? '—'}
                {a.codigoSolicitacao ? ` · SISREG ${a.codigoSolicitacao}` : ''}
              </p>
              {a.emAtendimentoPorOutro && (
                <p className="text-sm text-amber-700">
                  Em atendimento por {a.atendenteNome ?? 'outra pessoa'} — agir aqui pode dar conflito (409).
                </p>
              )}
            </section>

            {desfecho && (
              <section
                className={`rounded-lg p-4 text-sm ring-1 ${
                  desfecho.tipo === 'confirmado'
                    ? 'bg-emerald-50 text-emerald-800 ring-emerald-200'
                    : 'bg-red-50 text-red-800 ring-red-200'
                }`}
              >
                {desfecho.tipo === 'confirmado' ? (
                  <p>
                    <strong>Presença confirmada ✅</strong> — registrada como resposta pelo WhatsApp.
                  </p>
                ) : (
                  <p>
                    <strong>Registrado que a pessoa NÃO VAI.</strong> A ficha entrou na aba{' '}
                    <strong>Cancelamento</strong> do menu Confirmações para a equipe tratar o SISREG, como
                    sempre. {desfecho.extra}
                  </p>
                )}
              </section>
            )}

            {!desfecho && (
              <section className="space-y-3 rounded-lg bg-white p-4 shadow-sm ring-1 ring-gray-200">
                <p className="text-xs text-gray-500">
                  O que a pessoa disse na conversa? Confirme a presença ou registre que ela não vai.
                </p>
                <div className="flex flex-wrap gap-2">
                  {a.statusConfirmacao !== 'Confirmada' && (
                    <button
                      type="button"
                      disabled={!podeConfirmar || acao.isPending}
                      onClick={confirmar}
                      title={podeConfirmar ? undefined : 'Sem permissão de edição em Confirmações'}
                      className="flex items-center gap-1.5 rounded-md bg-emerald-600 px-3 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
                    >
                      {acao.isPending && !cancelando ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        <Check className="h-4 w-4" />
                      )}
                      Confirmar presença
                    </button>
                  )}
                  {a.statusConfirmacao !== 'Cancelada' && !cancelando && (
                    <button
                      type="button"
                      disabled={!podeCancelar || acao.isPending}
                      onClick={() => setCancelando(true)}
                      title={podeCancelar ? undefined : 'Sem permissão de exclusão em Confirmações'}
                      className="flex items-center gap-1.5 rounded-md border border-red-300 bg-red-50 px-3 py-2 text-sm font-medium text-red-700 hover:bg-red-100 disabled:opacity-50"
                    >
                      <XCircle className="h-4 w-4" />
                      Não vai (cancelar)
                    </button>
                  )}
                </div>

                {cancelando && (
                  <div className="space-y-2 rounded-md border border-red-200 bg-red-50/50 p-3">
                    <label className="block text-xs font-medium text-red-800">
                      Motivo dito pela pessoa (obrigatório)
                    </label>
                    <textarea
                      value={motivo}
                      onChange={(e) => setMotivo(e.target.value)}
                      rows={2}
                      className="w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm"
                      placeholder='Ex.: "vai estar viajando", "conseguiu na rede particular"…'
                    />
                    <div className="flex gap-2">
                      <button
                        type="button"
                        disabled={acao.isPending}
                        onClick={cancelar}
                        className="flex items-center gap-1.5 rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
                      >
                        {acao.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <XCircle className="h-4 w-4" />}
                        Registrar que NÃO VAI
                      </button>
                      <button
                        type="button"
                        onClick={() => setCancelando(false)}
                        className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100"
                      >
                        Voltar
                      </button>
                    </div>
                  </div>
                )}
              </section>
            )}

            {erro && <p className="text-sm text-red-600">{erro}</p>}
          </>
        )}
      </div>
    </div>
  );
}
