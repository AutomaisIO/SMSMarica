import { useState } from 'react';
import { CalendarClock, Check, Loader2, Send } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import {
  useAlteracoesAgenda,
  useComunicarAlteracao,
  useTratarAlteracao,
} from '@/features/alteracoes-agenda/api/queries';
import type { TipoAlteracaoAgenda } from '@/features/alteracoes-agenda/api/alteracoesApi';

const ROTULO_TIPO: Record<TipoAlteracaoAgenda, string> = {
  DataHora: 'Remarcado',
  Executante: 'Trocou o profissional',
  Procedimento: 'Trocou o procedimento',
  Ausente: 'Sumiu do SISREG',
};

/** Remarcação é a única que muda o que o paciente precisa fazer — por isso destoa das outras. */
const CLASSE_TIPO: Record<TipoAlteracaoAgenda, string> = {
  DataHora: 'bg-amber-100 text-amber-900',
  Executante: 'bg-gray-100 text-gray-700',
  Procedimento: 'bg-gray-100 text-gray-700',
  // Sumiu = provável cancelamento lá. Pesa tanto quanto remarcação: a vaga aparece ocupada aqui
  // por alguém que não vem mais.
  Ausente: 'bg-red-100 text-red-800',
};

function dataHora(iso: string | null) {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' });
}

/**
 * A fila do que o SISREG mudou em agendamentos que já estavam aqui.
 *
 * <p>Antes desta tela, uma consulta remarcada no SISREG deixava nosso banco com a data velha
 * indefinidamente — o paciente ficava com um dia na mão que não valia mais e ninguém deste lado
 * sabia. A fila existe para que a regulação e a unidade solicitante vejam e tomem providência.</p>
 */
export function AlteracoesAgendaPage() {
  const [apenasPendentes, setApenasPendentes] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [agindoEm, setAgindoEm] = useState<string | null>(null);

  const lista = useAlteracoesAgenda(apenasPendentes);
  const tratar = useTratarAlteracao();
  const comunicar = useComunicarAlteracao();

  function agir(id: string, acao: 'tratar' | 'comunicar') {
    setErro(null);
    setAgindoEm(id);
    const m = acao === 'tratar' ? tratar : comunicar;
    m.mutate(id, {
      onError: (e) => setErro(extrairMensagemDeErro(e)),
      onSettled: () => setAgindoEm(null),
    });
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <CalendarClock className="h-6 w-6 text-primary-600" />
          Alterações de Agenda
        </h1>
        <p className="mt-1 max-w-3xl text-sm text-gray-600">
          O que o SISREG mudou em agendamentos que já estavam aqui. <strong>Remarcado</strong> é o
          que muda a vida do paciente: ele tem na mão um dia que não vale mais — avisar pelo botão
          reenvia a mensagem com a data nova e <strong>invalida o link antigo</strong>.{' '}
          <strong>Sumiu do SISREG</strong> é provável cancelamento lá: confirme antes de tratar,
          porque enquanto estiver aqui a vaga continua contando como ocupada.
        </p>
      </header>

      <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={apenasPendentes}
            onChange={(e) => setApenasPendentes(e.target.checked)}
          />
          Só as que faltam tratar
        </label>
        {lista.data ? (
          <span className="text-xs text-gray-500">{lista.data.total} no total</span>
        ) : null}
        {lista.isFetching ? <Loader2 className="h-4 w-4 animate-spin text-gray-400" /> : null}
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      {lista.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </div>
      ) : null}

      {lista.data && lista.data.itens.length === 0 ? (
        <p className="rounded-xl border border-gray-200 bg-white p-6 text-center text-sm text-gray-500">
          {apenasPendentes
            ? 'Nada pendente — o SISREG não mudou nenhum agendamento desde a última importação.'
            : 'Nenhuma alteração registrada ainda.'}
        </p>
      ) : null}

      {lista.data && lista.data.itens.length > 0 ? (
        <section className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
          <table className="w-full min-w-[62rem] text-left text-sm">
            <thead className="border-b border-gray-200 text-xs text-gray-500">
              <tr>
                <th className="px-4 py-2 font-medium">O que mudou</th>
                <th className="px-4 py-2 font-medium">De</th>
                <th className="px-4 py-2 font-medium">Para</th>
                <th className="px-4 py-2 font-medium">Procedimento</th>
                <th className="px-4 py-2 font-medium">Executante</th>
                <th className="px-4 py-2 font-medium">Solicitante</th>
                <th className="px-4 py-2 font-medium">Nº SISREG</th>
                <th className="px-4 py-2 font-medium">Detectada</th>
                <th className="px-4 py-2 font-medium">Ações</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {lista.data.itens.map((a) => (
                <tr key={a.id} className={a.tratadaEm ? 'text-gray-400' : ''}>
                  <td className="px-4 py-2">
                    <span className={`rounded-full px-2 py-0.5 text-xs ${CLASSE_TIPO[a.tipo]}`}>
                      {ROTULO_TIPO[a.tipo]}
                    </span>
                  </td>
                  <td className="px-4 py-2 text-xs">{a.valorAntes ?? '—'}</td>
                  <td className="px-4 py-2 text-xs font-medium">{a.valorDepois ?? '—'}</td>
                  <td className="px-4 py-2 text-xs">{a.procedimentoTexto ?? '—'}</td>
                  <td className="px-4 py-2 text-xs">{a.unidadeExecutanteNome ?? '—'}</td>
                  <td className="px-4 py-2 text-xs">{a.unidadeSolicitanteNome ?? '—'}</td>
                  <td className="px-4 py-2 text-xs">{a.codigoSolicitacao ?? '—'}</td>
                  <td className="px-4 py-2 text-xs">{dataHora(a.detectadaEm)}</td>
                  <td className="px-4 py-2">
                    {a.tratadaEm ? (
                      <span className="text-xs">
                        {a.comunicadaEm ? `avisado ${dataHora(a.comunicadaEm)}` : 'tratada'}
                      </span>
                    ) : (
                      <div className="flex flex-wrap gap-1.5">
                        <Button
                          tamanho="sm"
                          disabled={agindoEm === a.id}
                          title="Reenvia a confirmação com a data atual e invalida o link antigo."
                          onClick={() => agir(a.id, 'comunicar')}
                        >
                          {agindoEm === a.id && comunicar.isPending ? (
                            <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                          ) : (
                            <Send className="mr-1.5 h-3.5 w-3.5" />
                          )}
                          Avisar paciente
                        </Button>
                        <Button
                          variante="ghost"
                          tamanho="sm"
                          disabled={agindoEm === a.id}
                          title="Marca como resolvida sem enviar mensagem."
                          onClick={() => agir(a.id, 'tratar')}
                        >
                          <Check className="mr-1.5 h-3.5 w-3.5" />
                          Só tratar
                        </Button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ) : null}
    </div>
  );
}
