import { useEffect, useRef, useState } from 'react';
import { CalendarCheck2, Check, Loader2 } from 'lucide-react';
import { usePendentesDoPaciente, useAcaoAtendimento } from '@/features/confirmacoes/api';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { dataHora } from '@/features/mensageria/lib/rotulos';

/**
 * Botão "Confirmar" no cabeçalho do chat (ticket #133): confirma a presença de um agendamento do
 * paciente sem sair da conversa nem abrir o menu Confirmações.
 *
 * A conversa não guarda agendamento — guarda o paciente. Por isso abrimos a lista dos agendamentos
 * dele que ainda esperam confirmação e a pessoa escolhe qual confirmar (pode ter zero, um ou
 * vários). Confirmar aqui grava o canal WhatsApp: é onde a atendente está falando com o paciente.
 * O endpoint de confirmar já auto-assume a ficha; se outra pessoa estiver atendendo, volta 409 e o
 * erro aparece embaixo.
 */
export function ConfirmarAgendamentoChat({ pacienteId }: { pacienteId: string }) {
  const [aberto, setAberto] = useState(false);
  const [confirmandoId, setConfirmandoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const ref = useRef<HTMLDivElement | null>(null);
  // Só busca quando a lista está aberta — leitura barata, mas não faz sentido a cada conversa.
  const pendentes = usePendentesDoPaciente(pacienteId, aberto);
  const acao = useAcaoAtendimento();

  useEffect(() => {
    if (!aberto) return;
    const fechar = (e: MouseEvent) => {
      if (!ref.current?.contains(e.target as Node)) setAberto(false);
    };
    document.addEventListener('mousedown', fechar);
    return () => document.removeEventListener('mousedown', fechar);
  }, [aberto]);

  useEffect(() => {
    setAberto(false);
    setErro(null);
  }, [pacienteId]);

  const lista = pendentes.data ?? [];

  function confirmar(solicitacaoId: string) {
    setErro(null);
    setConfirmandoId(solicitacaoId);
    acao.mutate(
      { solicitacaoId, acao: { tipo: 'confirmar', meio: 'WhatsApp' } },
      {
        onSuccess: () => {
          setConfirmandoId(null);
          void pendentes.refetch();
        },
        onError: (e) => {
          setErro(extrairMensagemDeErro(e));
          setConfirmandoId(null);
        },
      },
    );
  }

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setAberto((v) => !v)}
        className="flex items-center gap-1 rounded-md border border-emerald-300 bg-emerald-50 px-2 py-1 text-[11px] font-medium text-emerald-700 hover:bg-emerald-100"
        title="Confirmar a presença de um agendamento deste paciente sem sair da conversa"
        aria-expanded={aberto}
      >
        <CalendarCheck2 className="h-3.5 w-3.5" /> Confirmar
      </button>
      {aberto && (
        <div className="absolute right-0 z-30 mt-1 w-80 overflow-hidden rounded-md bg-white shadow-lg ring-1 ring-gray-200">
          <div className="border-b border-gray-100 px-3 py-2 text-xs font-medium text-gray-500">
            Agendamentos aguardando confirmação
          </div>
          <div className="max-h-72 overflow-y-auto">
            {pendentes.isLoading ? (
              <p className="px-3 py-3 text-xs text-gray-500">Carregando…</p>
            ) : lista.length === 0 ? (
              <p className="px-3 py-3 text-xs text-gray-500">
                Nenhum agendamento pendente de confirmação para este paciente.
              </p>
            ) : (
              lista.map((a) => (
                <div
                  key={a.solicitacaoId}
                  className="flex items-start justify-between gap-2 border-b border-gray-50 px-3 py-2 last:border-b-0"
                >
                  <div className="min-w-0 text-xs">
                    <p className="truncate font-medium text-gray-900">{a.procedimento ?? a.categoria}</p>
                    <p className="text-gray-600">{dataHora(a.dataAgendada)}</p>
                    <p className="truncate text-gray-500">
                      {a.unidadeExecutante ?? '—'}
                      {a.codigoSolicitacao ? ` · SISREG ${a.codigoSolicitacao}` : ''}
                    </p>
                    {a.emAtendimentoPorOutro && (
                      <p className="text-amber-600">Em atendimento por {a.atendenteNome ?? 'outra pessoa'}</p>
                    )}
                  </div>
                  <button
                    type="button"
                    disabled={confirmandoId === a.solicitacaoId}
                    onClick={() => confirmar(a.solicitacaoId)}
                    className="flex shrink-0 items-center gap-1 rounded-md bg-emerald-600 px-2 py-1 text-[11px] font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
                  >
                    {confirmandoId === a.solicitacaoId ? (
                      <Loader2 className="h-3.5 w-3.5 animate-spin" />
                    ) : (
                      <Check className="h-3.5 w-3.5" />
                    )}
                    Confirmar
                  </button>
                </div>
              ))
            )}
          </div>
          {erro && <p className="border-t border-gray-100 px-3 py-2 text-xs text-red-600">{erro}</p>}
        </div>
      )}
    </div>
  );
}
