import { useState } from 'react';
import { Loader2, RefreshCw, ShieldAlert } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import {
  useDivergenciasPep,
  useIgnorarDivergenciaPep,
  useReprocessarDivergenciasPep,
  useResumoDivergenciasPep,
  useVerificarDivergenciasPep,
} from '@/features/pep-sincronizacao/api/queries';
import type {
  DivergenciaIdentidade,
  VeredictoDivergencia,
} from '@/features/pep-sincronizacao/types';

/** Rótulo + cor do veredicto da consulta oficial de CPF. */
function RotuloVeredicto({ d }: { d: DivergenciaIdentidade }) {
  if (d.status === 'Ignorada') {
    return <span className="rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-600">Ignorada</span>;
  }
  const mapa: Record<VeredictoDivergencia, { txt: string; cls: string }> = {
    OrigemCorreta: { txt: 'Origem correta', cls: 'bg-emerald-100 text-emerald-800' },
    HubCorreto: { txt: 'Hub correto — origem errada', cls: 'bg-blue-100 text-blue-800' },
    AmbosNegados: { txt: 'CPF suspeito', cls: 'bg-red-100 text-red-800' },
    Inconclusivo: { txt: 'Inconclusivo', cls: 'bg-amber-100 text-amber-800' },
    Indefinido: {
      txt: d.status === 'NaoConclusiva' ? 'Não conclusiva' : 'Aguardando arbitragem',
      cls: 'bg-amber-100 text-amber-800',
    },
  };
  const v = mapa[d.veredicto] ?? mapa.Indefinido;
  return <span className={`rounded px-2 py-0.5 text-xs font-medium ${v.cls}`}>{v.txt}</span>;
}

function cpfMascarado(cpf: string): string {
  if (cpf.length !== 11) return cpf;
  return `${cpf.slice(0, 3)}.***.***-${cpf.slice(9)}`;
}

/**
 * Relatório de divergências de identidade origem×hub (ADR-0039 / plano §3.2). Enquanto a
 * arbitragem não diz quem está certo, o campo fica CONGELADO — a importação não sobrescreve
 * o hub. Fundir às cegas misturaria o histórico clínico de duas pessoas.
 */
export function SecaoDivergencias({ fonteId, podeEditar }: { fonteId: string; podeEditar: boolean }) {
  const [somentePendentes, setSomentePendentes] = useState(true);
  const resumo = useResumoDivergenciasPep(fonteId || undefined);
  const lista = useDivergenciasPep(fonteId || undefined, somentePendentes ? 'Pendente' : undefined);
  const verificar = useVerificarDivergenciasPep();
  const reprocessar = useReprocessarDivergenciasPep();
  const ignorar = useIgnorarDivergenciaPep();
  const [msg, setMsg] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  async function aoVerificar() {
    setMsg(null);
    setErro(null);
    try {
      const r = await verificar.mutateAsync({ fonteId: fonteId || undefined });
      setMsg(
        r.analisadas === 0
          ? 'Nada pendente para arbitrar.'
          : `${r.analisadas} arbitradas — origem ${r.origemCorreta} · hub ${r.hubCorreto} · CPF suspeito ${r.ambosNegados} · inconclusivas ${r.naoConclusivas}` +
              (r.interrompidaPorIndisponibilidade ? ' (interrompida: consulta indisponível)' : ''),
      );
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoReprocessar() {
    if (!fonteId) return;
    const n = resumo.data?.origemCorreta ?? 0;
    const ok = window.confirm(
      `Reprocessar da origem os ${n} pacientes cuja divergência foi resolvida como "origem correta"? ` +
        'O hub recebe o valor correto agora, em vez de esperar o paciente ter atendimento novo.',
    );
    if (!ok) return;
    setMsg(null);
    setErro(null);
    try {
      await reprocessar.mutateAsync({ fonteId });
      setMsg('Reprocessamento enfileirado — acompanhe no cartão de status acima.');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoIgnorar(d: DivergenciaIdentidade) {
    const ok = window.confirm(
      'Marcar como falso positivo? O campo deixa de ser congelado e a origem volta a sobrescrever o hub.',
    );
    if (!ok) return;
    setErro(null);
    try {
      await ignorar.mutateAsync({ id: d.id, motivo: 'Marcada como falso positivo pelo operador.' });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const r = resumo.data;

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
          <ShieldAlert className="h-4 w-4 text-amber-600" />
          Divergências de identidade
        </h2>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={somentePendentes}
              onChange={(e) => setSomentePendentes(e.target.checked)}
            />
            Só pendentes
          </label>
          {podeEditar ? (
            <Button
              type="button"
              tamanho="sm"
              variante="secundaria"
              onClick={aoVerificar}
              disabled={verificar.isPending}
            >
              {verificar.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="mr-2 h-4 w-4" />
              )}
              Arbitrar pendentes
            </Button>
          ) : null}
          {podeEditar && (resumo.data?.origemCorreta ?? 0) > 0 ? (
            <Button
              type="button"
              tamanho="sm"
              variante="secundaria"
              onClick={aoReprocessar}
              disabled={reprocessar.isPending}
              title="Traz da origem o valor correto sem esperar o paciente ter atendimento novo"
            >
              {reprocessar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Reprocessar {resumo.data?.origemCorreta} corrigidos
            </Button>
          ) : null}
        </div>
      </div>

      <p className="mb-3 text-sm text-gray-600">
        Mesmo CPF com data de nascimento diferente entre a origem e o hub. O árbitro é a consulta
        oficial de CPF (Receita/CADSUS), que só valida quando o par CPF + nascimento confere.
        Enquanto não há veredicto — ou quando ele aponta contra a origem — o hub <strong>prevalece</strong>{' '}
        e a importação não sobrescreve o campo.
      </p>

      {r ? (
        <div className="mb-4 grid grid-cols-2 gap-x-6 gap-y-1 text-sm sm:grid-cols-4">
          <div>
            <span className="text-gray-500">Total:</span> {r.total}
          </div>
          <div>
            <span className="text-gray-500">Pendentes:</span>{' '}
            <span className="font-semibold text-amber-700">{r.pendentes}</span>
          </div>
          <div>
            <span className="text-gray-500">Congelados agora:</span>{' '}
            <span className="font-semibold text-blue-700">{r.congelados}</span>
          </div>
          <div>
            <span className="text-gray-500">Não conclusivas:</span> {r.naoConclusivas}
          </div>
          <div>
            <span className="text-gray-500">Origem correta:</span>{' '}
            <span className="text-emerald-700">{r.origemCorreta}</span>
          </div>
          <div>
            <span className="text-gray-500">Hub correto:</span>{' '}
            <span className="text-blue-700">{r.hubCorreto}</span>
          </div>
          <div>
            <span className="text-gray-500">CPF suspeito:</span>{' '}
            <span className="font-semibold text-red-700">{r.ambosNegados}</span>
          </div>
          <div>
            <span className="text-gray-500">Ignoradas:</span> {r.ignoradas}
          </div>
        </div>
      ) : null}

      {msg ? (
        <div className="mb-3 rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
          {msg}
        </div>
      ) : null}
      {erro ? (
        <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </div>
      ) : null}

      {lista.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : !lista.data?.length ? (
        <p className="text-sm text-gray-500">
          Nenhuma divergência {somentePendentes ? 'pendente' : 'registrada'}.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-gray-200 text-xs uppercase text-gray-500">
              <tr>
                <th className="py-2 pr-3">CPF</th>
                <th className="py-2 pr-3">Paciente (origem / hub)</th>
                <th className="py-2 pr-3">Origem</th>
                <th className="py-2 pr-3">Hub</th>
                <th className="py-2 pr-3">Veredicto</th>
                <th className="py-2 pr-3">Correto</th>
                <th className="py-2 pr-3 text-right">Ocor.</th>
                <th className="py-2 text-right">Ação</th>
              </tr>
            </thead>
            <tbody>
              {lista.data.map((d) => (
                <tr key={d.id} className="border-b border-gray-100 align-top">
                  <td className="py-2 pr-3 whitespace-nowrap font-mono text-xs">{cpfMascarado(d.cpf)}</td>
                  <td className="py-2 pr-3 text-xs">
                    <div>{d.nomeOrigem ?? '—'}</div>
                    {d.nomeHub && d.nomeHub !== d.nomeOrigem ? (
                      <div className="text-gray-500">hub: {d.nomeHub}</div>
                    ) : null}
                    {d.nomeOficial ? <div className="text-emerald-700">oficial: {d.nomeOficial}</div> : null}
                  </td>
                  <td className="py-2 pr-3 whitespace-nowrap font-mono text-xs">{d.valorOrigem}</td>
                  <td className="py-2 pr-3 whitespace-nowrap font-mono text-xs">{d.valorHub}</td>
                  <td className="py-2 pr-3">
                    <RotuloVeredicto d={d} />
                    {d.detalhe ? <div className="mt-1 max-w-md text-xs text-gray-500">{d.detalhe}</div> : null}
                  </td>
                  <td className="py-2 pr-3 whitespace-nowrap font-mono text-xs font-semibold">
                    {d.valorCorreto ?? '—'}
                  </td>
                  <td className="py-2 pr-3 text-right text-xs">{d.ocorrencias}</td>
                  <td className="py-2 text-right">
                    {podeEditar && d.status !== 'Ignorada' ? (
                      <button
                        type="button"
                        className="text-xs text-gray-500 underline hover:text-gray-800"
                        onClick={() => aoIgnorar(d)}
                        disabled={ignorar.isPending}
                      >
                        ignorar
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
