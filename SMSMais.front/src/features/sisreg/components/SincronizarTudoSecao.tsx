import { useEffect, useState } from 'react';
import { Loader2, RefreshCw, RotateCw, XCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import {
  useAgendamentoMapeamentoLote,
  useCancelarMapeamentoLote,
  useExecucoesMapeamentoLote,
  useSalvarAgendamentoMapeamentoLote,
  useSincronizarMapeamentoLote,
  useStatusMapeamentoLote,
} from '@/features/sisreg/api/queries';
import { ModalDetalheMapeamentoLote } from '@/features/sisreg/components/ModalDetalheMapeamentoLote';
import type { MapeamentoLoteExecucao, StatusMapeamentoLote } from '@/features/sisreg/types';

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
        </div>
      )}

      {detalhe && (
        <ModalDetalheMapeamentoLote execucao={detalhe} aoFechar={() => setDetalhe(null)} />
      )}
    </section>
  );
}
