import { useEffect, useRef, useState } from 'react';
import { CalendarCheck2, ExternalLink } from 'lucide-react';
import { useProximosDoTelefone, abrirJanelaAtendimento } from '@/features/confirmacoes/api';
import { dataHora } from '@/features/mensageria/lib/rotulos';

/**
 * Botão "Agendamentos" no cabeçalho do chat (evolução do "Confirmar" do ticket #133): lista os
 * PRÓXIMOS agendamentos de TODOS os cadastros ligados ao número da conversa — o telefone da
 * família atende vários pacientes, e a conversa pode ser sobre qualquer um deles.
 *
 * Clicar num agendamento abre a JANELA SOLTA de atendimento (sempre a mesma janela do navegador:
 * o nome fixo faz o clique seguinte reaproveitá-la e trazê-la para a frente). É lá que se decide
 * entre CONFIRMAR e CANCELAR, vendo os dados do agendamento — e o desfecho aparece na própria
 * janela.
 */
export function ConfirmarAgendamentoChat({
  telefone,
  pacienteId,
}: {
  telefone: string;
  pacienteId: string | null;
}) {
  const [aberto, setAberto] = useState(false);
  const ref = useRef<HTMLDivElement | null>(null);
  // Só busca quando a lista está aberta — leitura barata, mas não faz sentido a cada conversa.
  const proximos = useProximosDoTelefone(telefone, pacienteId, aberto);

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
  }, [telefone]);

  const lista = proximos.data ?? [];

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setAberto((v) => !v)}
        className="flex items-center gap-1 rounded-md border border-emerald-300 bg-emerald-50 px-2 py-1 text-[11px] font-medium text-emerald-700 hover:bg-emerald-100"
        title="Próximos agendamentos deste número — abre a janela para confirmar ou cancelar"
        aria-expanded={aberto}
      >
        <CalendarCheck2 className="h-3.5 w-3.5" /> Agendamentos
      </button>
      {aberto && (
        <div className="absolute right-0 z-30 mt-1 w-96 overflow-hidden rounded-md bg-white shadow-lg ring-1 ring-gray-200">
          <div className="border-b border-gray-100 px-3 py-2 text-xs font-medium text-gray-500">
            Próximos agendamentos deste número
          </div>
          <div className="max-h-80 overflow-y-auto">
            {proximos.isLoading ? (
              <p className="px-3 py-3 text-xs text-gray-500">Carregando…</p>
            ) : lista.length === 0 ? (
              <p className="px-3 py-3 text-xs text-gray-500">
                Nenhum agendamento futuro para os cadastros deste número.
              </p>
            ) : (
              lista.map((a) => (
                <button
                  key={a.solicitacaoId}
                  type="button"
                  onClick={() => {
                    abrirJanelaAtendimento(a.solicitacaoId);
                    setAberto(false);
                  }}
                  className="flex w-full items-start justify-between gap-2 border-b border-gray-50 px-3 py-2 text-left hover:bg-gray-50 last:border-b-0"
                  title="Abrir a janela de atendimento deste agendamento"
                >
                  <div className="min-w-0 text-xs">
                    <p className="truncate font-semibold text-gray-900">{a.pacienteNome ?? 'Paciente'}</p>
                    <p className="truncate text-gray-700">{a.procedimento ?? a.categoria}</p>
                    <p className="text-gray-600">{dataHora(a.dataAgendada)}</p>
                    <p className="truncate text-gray-500">
                      {a.unidadeExecutante ?? '—'}
                      {a.codigoSolicitacao ? ` · SISREG ${a.codigoSolicitacao}` : ''}
                    </p>
                    {a.emAtendimentoPorOutro && (
                      <p className="text-amber-600">Em atendimento por {a.atendenteNome ?? 'outra pessoa'}</p>
                    )}
                  </div>
                  <span className="flex shrink-0 flex-col items-end gap-1">
                    <ChipConfirmacao status={a.statusConfirmacao} />
                    <ExternalLink className="h-3.5 w-3.5 text-gray-400" />
                  </span>
                </button>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export function ChipConfirmacao({ status }: { status: string }) {
  const [texto, classe] =
    status === 'Confirmada'
      ? ['Confirmado', 'bg-emerald-50 text-emerald-700 ring-emerald-200']
      : status === 'Cancelada'
        ? ['Avisou que não vai', 'bg-red-50 text-red-700 ring-red-200']
        : ['Aguarda resposta', 'bg-gray-50 text-gray-600 ring-gray-200'];
  return (
    <span className={`whitespace-nowrap rounded-full px-2 py-0.5 text-[10px] font-medium ring-1 ${classe}`}>
      {texto}
    </span>
  );
}
